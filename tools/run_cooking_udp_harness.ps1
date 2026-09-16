[CmdletBinding()]
param(
    [ValidateSet('SameMachine')]
    [string]$Topology = 'SameMachine',
    [string]$ResultsDirectory = 'artifacts/cooking-udp',
    [int]$StartupTimeoutSeconds = 10,
    [int]$ScenarioTimeoutSeconds = 15
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $ResultsDirectory ("same-machine-" + [guid]::NewGuid().ToString('N'))))
New-Item -ItemType Directory -Force -Path $root | Out-Null
$hostLog = Join-Path $root 'host.stdout.log'
$hostError = Join-Path $root 'host.stderr.log'
$clientLog = Join-Path $root 'client.stdout.log'
$clientError = Join-Path $root 'client.stderr.log'
$harnessProject = 'src/AbilityKit.Game.Cooking.UdpHarness/AbilityKit.Game.Cooking.UdpHarness.csproj'

function Get-FreeUdpPort {
    $udp = [System.Net.Sockets.UdpClient]::new(0)
    try { return ([System.Net.IPEndPoint]$udp.Client.LocalEndPoint).Port }
    finally { $udp.Dispose() }
}

$port = Get-FreeUdpPort
$manifest = [ordered]@{
    schema = 'abilitykit.cooking-udp-harness.v1'
    topology = 'same-machine-udp'
    startedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    port = $port
    status = 'running'
    hostProcessId = $null
    clientProcessId = $null
    twoPcLanAcceptance = 'not-run; this artifact is same-machine only'
}
$manifestPath = Join-Path $root 'manifest.json'
$manifest | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 $manifestPath

try {
    dotnet build $harnessProject --nologo | Out-Host
    $hostArguments = @('run', '--no-build', '--project', $harnessProject, '--', '--role', 'host', '--port', "$port", '--topology', 'udp-same-machine', '--nic-identity', 'loopback', '--nic-status', 'not-applicable', '--firewall-status', 'local-process', '--artifact-directory', $root, '--duration-seconds', "$ScenarioTimeoutSeconds")
    $hostProcess = Start-Process -FilePath 'dotnet' -ArgumentList $hostArguments -RedirectStandardOutput $hostLog -RedirectStandardError $hostError -PassThru
    $manifest.hostProcessId = $hostProcess.Id
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 $manifestPath

    $ready = Join-Path $root 'host-ready.json'
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($StartupTimeoutSeconds)
    while (-not (Test-Path $ready) -and [DateTimeOffset]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }
    if (-not (Test-Path $ready)) { throw "Host did not produce readiness artifact within $StartupTimeoutSeconds seconds." }

    $clientArguments = @('run', '--no-build', '--project', $harnessProject, '--', '--role', 'client', '--peer', '127.0.0.1', '--port', "$port", '--topology', 'udp-same-machine', '--nic-identity', 'loopback', '--nic-status', 'not-applicable', '--firewall-status', 'local-process', '--artifact-directory', $root, '--timeout-seconds', "$StartupTimeoutSeconds")
    $client = Start-Process -FilePath 'dotnet' -ArgumentList $clientArguments -RedirectStandardOutput $clientLog -RedirectStandardError $clientError -PassThru
    $manifest.clientProcessId = $client.Id
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 $manifestPath

    if (-not $client.WaitForExit($ScenarioTimeoutSeconds * 1000) -or -not $client.HasExited) { throw "Client exceeded scenario timeout of $ScenarioTimeoutSeconds seconds." }
    $client.Refresh()
    if (-not (Test-Path (Join-Path $root 'client-result.json'))) { throw 'Client completed without client-result.json.' }
    if (Test-Path (Join-Path $root 'failure.json')) { throw 'Client or host produced failure.json.' }

    if (-not $hostProcess.WaitForExit(($ScenarioTimeoutSeconds + 5) * 1000) -or -not $hostProcess.HasExited) { throw 'Host did not stop within its bounded lifetime.' }
    $hostProcess.Refresh()
    if (-not (Test-Path (Join-Path $root 'host-result.json'))) { throw 'Host completed without host-result.json.' }
    $clientResult = Get-Content -Raw (Join-Path $root 'client-result.json') | ConvertFrom-Json
    $hostResult = Get-Content -Raw (Join-Path $root 'host-result.json') | ConvertFrom-Json
    if ($clientResult.topology -ne 'udp-same-machine' -or $hostResult.topology -ne 'udp-same-machine') { throw 'Harness result topology is not udp-same-machine.' }
    if ($clientResult.command.AuthorityResult.Outcome -ne 0) { throw 'Client command was not authoritatively accepted.' }
    if (-not $clientResult.synchronization.Accepted) { throw 'Client synchronization result was not accepted.' }
    if ([string]::IsNullOrWhiteSpace($clientResult.stateHash) -or $clientResult.stateHash -ne $hostResult.stateHash) { throw 'Host and client state hashes do not match.' }
    if ([string]::IsNullOrWhiteSpace($clientResult.workload.id) -or $clientResult.workload.configIdentity -ne $hostResult.workload.configIdentity) { throw 'Harness workload metadata is missing or incompatible.' }

    $manifest.status = 'passed'
}
catch {
    $manifest.status = 'failed'
    $manifest.failure = $_.Exception.Message
    throw
}
finally {
    foreach ($processId in @($manifest.clientProcessId, $manifest.hostProcessId)) {
        if ($null -eq $processId) { continue }
        $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
        if ($null -ne $process -and -not $process.HasExited) { Stop-Process -Id $processId -Force }
    }
    $manifest.finishedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 $manifestPath
}

Write-Host "Cooking UDP same-machine artifacts: $root"
