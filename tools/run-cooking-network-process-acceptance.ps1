param(
    [ValidateSet('SameMachine', 'Host', 'Client')][string]$Mode = 'SameMachine',
    [string]$RemoteIp = '127.0.0.1',
    [string]$BindIp = '0.0.0.0',
    [ValidateRange(0, 65535)][int]$Port = 0,
    [string]$OutputDirectory = 'local/Logs/cooking-network-process',
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $workspaceRoot 'src/AbilityKit.Game.Cooking.NetworkAcceptance/AbilityKit.Game.Cooking.NetworkAcceptance.csproj'
$dll = Join-Path $workspaceRoot 'src/AbilityKit.Game.Cooking.NetworkAcceptance/bin/Debug/net10.0/AbilityKit.Game.Cooking.NetworkAcceptance.dll'
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fffffff')
$runDirectory = [IO.Path]::GetFullPath((Join-Path (Join-Path $workspaceRoot $OutputDirectory) $stamp))
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
if (!$NoBuild) {
    & dotnet build $project --verbosity quiet *> (Join-Path $runDirectory 'build.log')
    if ($LASTEXITCODE -ne 0) { throw "Build failed; inspect $runDirectory/build.log" }
}
if (!(Test-Path -LiteralPath $dll)) { throw "Executable not found: $dll" }
if ($Mode -ne 'SameMachine') {
    if ($Port -eq 0 -and $Mode -eq 'Client') { throw 'Client mode requires the actual host port.' }
    $role = $Mode.ToLowerInvariant()
    $address = if ($Mode -eq 'Host') { $BindIp } else { $RemoteIp }
    & dotnet $dll $role --ip $address --port $Port --report (Join-Path $runDirectory "$role.json") --topology 'SeparateHostsRequiresPairedEvidence'
    exit $LASTEXITCODE
}

# Paths become process arguments only; no shell command text or destructive cleanup.
function Quote-Argument([string]$value) {
    if ($value.Contains('"')) { throw 'An argument contains an unsupported quotation mark.' }
    return '"' + $value + '"'
}
$hostReportPath = Join-Path $runDirectory 'host.json'
$clientReportPath = Join-Path $runDirectory 'client.json'
$hostStdout = Join-Path $runDirectory 'host.stdout.log'
$hostArguments = (Quote-Argument $dll) + ' host --ip 127.0.0.1 --port ' + $Port + ' --report ' + (Quote-Argument $hostReportPath) + ' --topology SameMachineIndependentProcessesUdp'
$hostProcess = Start-Process dotnet -ArgumentList $hostArguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $hostStdout -RedirectStandardError (Join-Path $runDirectory 'host.stderr.log')
$clientProcess = $null
try {
    $readyPort = $null
    $readyDeadline = [DateTime]::UtcNow.AddSeconds(120)
    while ([DateTime]::UtcNow -lt $readyDeadline) {
        $hostProcess.Refresh()
        if ($hostProcess.HasExited) { throw 'Host exited before publishing its ready endpoint.' }
        if (Test-Path -LiteralPath $hostStdout) {
            $readyText = Get-Content -LiteralPath $hostStdout -Raw
            if ($readyText -match '(?m)^READY ([0-9]+) ([0-9]+)\s*$') {
                $readyPort = [int]$Matches[1]
                if ([int]$Matches[2] -ne $hostProcess.Id) { throw 'Ready message PID does not match the launched Host.' }
                break
            }
        }
        Start-Sleep -Milliseconds 100
    }
    if ($null -eq $readyPort -or $readyPort -le 0) { throw 'Host readiness deadline expired.' }
    $clientArguments = (Quote-Argument $dll) + ' client --ip 127.0.0.1 --port ' + $readyPort + ' --report ' + (Quote-Argument $clientReportPath) + ' --topology SameMachineIndependentProcessesUdp'
    $clientProcess = Start-Process dotnet -ArgumentList $clientArguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runDirectory 'client.stdout.log') -RedirectStandardError (Join-Path $runDirectory 'client.stderr.log')
    $exitDeadline = [DateTime]::UtcNow.AddSeconds(600)
    while ([DateTime]::UtcNow -lt $exitDeadline) {
        $hostProcess.Refresh(); $clientProcess.Refresh()
        if ($clientProcess.HasExited -and (Test-Path -LiteralPath $clientReportPath)) {
            $interimClient = Get-Content -LiteralPath $clientReportPath -Raw | ConvertFrom-Json
            if (!$interimClient.passed) { throw 'Client acceptance failed; inspect retained JSON/stderr.' }
        }
        if ($hostProcess.HasExited -and (Test-Path -LiteralPath $hostReportPath)) {
            $interimHost = Get-Content -LiteralPath $hostReportPath -Raw | ConvertFrom-Json
            if (!$interimHost.passed) { throw 'Host acceptance failed; inspect retained JSON/stderr.' }
        }
        if ($hostProcess.HasExited -and $clientProcess.HasExited) { break }
        Start-Sleep -Milliseconds 100
    }
    if (!$hostProcess.HasExited -or !$clientProcess.HasExited) { throw 'Independent-process acceptance deadline expired.' }
    if (!(Test-Path -LiteralPath $hostReportPath) -or !(Test-Path -LiteralPath $clientReportPath)) { throw 'One or both endpoint reports are absent.' }
    $hostReport = Get-Content -LiteralPath $hostReportPath -Raw | ConvertFrom-Json
    $clientReport = Get-Content -LiteralPath $clientReportPath -Raw | ConvertFrom-Json
    if ($hostProcess.ExitCode -ne 0 -or $clientProcess.ExitCode -ne 0 -or !$hostReport.passed -or !$clientReport.passed) { throw 'Endpoint acceptance failed; inspect JSON and stderr artifacts.' }
    if ($hostReport.pid -ne $hostProcess.Id -or $clientReport.pid -ne $clientProcess.Id -or $hostReport.pid -eq $clientReport.pid) { throw 'Reports do not prove separate launched processes.' }
    if ($hostReport.fixture -ne $clientReport.fixture -or $hostReport.protocol -ne 3 -or $clientReport.protocol -ne 3 -or $hostReport.topology -ne 'SameMachineIndependentProcessesUdp' -or $clientReport.topology -ne $hostReport.topology) { throw 'Fixture, protocol or topology provenance differs.' }
    if ([string]::IsNullOrWhiteSpace($hostReport.detail.hash) -or $hostReport.detail.hash -ne $clientReport.detail.hash) { throw 'Full gameplay-capture consensus hash differs.' }
    if ($hostReport.buildIdentity -ne $clientReport.buildIdentity -or $hostReport.sessionBuildIdentity -ne $clientReport.sessionBuildIdentity) { throw 'Endpoint runtime build identities differ.' }
    if ($hostReport.detail.serverSessionInstance -ne $clientReport.detail.serverSessionInstance -or [string]::IsNullOrWhiteSpace($clientReport.detail.baselineHash) -or [string]::IsNullOrWhiteSpace($hostReport.detail.baselineHash)) { throw 'Session identity or baseline hash evidence is absent/inconsistent.' }
    $hostParticipants = @($hostReport.detail.sessionProjection.participants | ForEach-Object { $_.participant.value })
    $clientParticipants = @($clientReport.detail.sessionProjection.participants | ForEach-Object { $_.participant.value })
        if ($hostParticipants.Count -ne 2 -or $clientParticipants.Count -ne 2 -or ($hostParticipants -join ',') -ne 'natural-chef,natural-partner' -or ($hostParticipants -join ',') -ne ($clientParticipants -join ',')) { throw 'Configured Session participant views differ or are empty.' }
    foreach ($participant in $clientReport.detail.sessionProjection.participants) {
        $paired = @($hostReport.detail.sessionProjection.participants | Where-Object { $_.participant.value -eq $participant.participant.value })
        if ($paired.Count -ne 1 -or $participant.connectionGeneration -le 0 -or !$participant.connectedOwnerBinding -or $paired[0].connectionGeneration -ne $participant.connectionGeneration) { throw 'Session generation or owner binding provenance is inconsistent.' }
    }
    # Connection Ready/connected state may change after Client exits; each endpoint
    # reports its full hash-covered view, while exact consensus covers gameplay capture.
    Write-Output "PASS independent Host PID=$($hostReport.pid) Client PID=$($clientReport.pid) UDP=$readyPort hash=$($hostReport.detail.hash)"
    Write-Output "Artifacts: $runDirectory; physical two-PC LAN remains NOT_VERIFIED."
} finally {
    foreach ($ownedProcess in @($clientProcess, $hostProcess)) {
        if ($null -ne $ownedProcess) {
            $ownedProcess.Refresh()
            if (!$ownedProcess.HasExited) { $ownedProcess.Kill(); $ownedProcess.WaitForExit() }
            $ownedProcess.Dispose()
        }
    }
}
