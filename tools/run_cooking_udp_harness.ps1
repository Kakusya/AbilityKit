# 已退役：owner 于 2026-09-21 决定当前范围收敛为单机，传输方向改用 KCP。
# 本脚本与 AbilityKit.Game.Cooking.UdpHarness 项目退出当前构建范围；源码保留供参考，不作删除声明。
# KCP 接入是后续独立任务，需重新审议传输契约、身份、快照与门禁，届时再决定本脚本去留。
[CmdletBinding()]
param(
    [ValidateSet('SameMachine')]
    [string]$Topology = 'SameMachine',
    [string]$ResultsDirectory = 'artifacts/cooking-udp',
    [int]$StartupTimeoutSeconds = 10,
    [int]$ScenarioTimeoutSeconds = 15,
    [int]$SuccessRetention = 10,
    [switch]$KeepArtifacts
)

$ErrorActionPreference = 'Stop'
if ($SuccessRetention -lt 0) { throw '-SuccessRetention must be zero or greater.' }
$runId = [guid]::NewGuid().ToString('N')
$root = [IO.Path]::GetFullPath((Join-Path $ResultsDirectory ("same-machine-" + $runId)))
$hostDirectory = Join-Path $root 'host'
$clientDirectory = Join-Path $root 'client'
New-Item -ItemType Directory -Force -Path $hostDirectory, $clientDirectory | Out-Null
if ($hostDirectory -eq $clientDirectory -or $clientDirectory.StartsWith($hostDirectory + [IO.Path]::DirectorySeparatorChar) -or $hostDirectory.StartsWith($clientDirectory + [IO.Path]::DirectorySeparatorChar)) {
    throw 'Role artifact directories must be distinct and non-nested.'
}
$harnessProject = 'src/AbilityKit.Game.Cooking.UdpHarness/AbilityKit.Game.Cooking.UdpHarness.csproj'

function Get-FreeUdpPort {
    $udp = [System.Net.Sockets.UdpClient]::new(0)
    try { return ([System.Net.IPEndPoint]$udp.Client.LocalEndPoint).Port }
    finally { $udp.Dispose() }
}

function Write-AtomicJson([string]$Path, [object]$Value) {
    $temporary = "$Path.$([guid]::NewGuid().ToString('N')).tmp"
    $Value | ConvertTo-Json -Depth 16 | Set-Content -Encoding utf8 -NoNewline $temporary
    Move-Item -Force $temporary $Path
}

function Test-Acceptance([string]$Root, [string]$ExpectedRunId) {
    $acceptanceProject = 'src/AbilityKit.Game.Cooking.UdpHarness/AbilityKit.Game.Cooking.UdpHarness.csproj'
    $arguments = @('run', '--no-build', '--project', $acceptanceProject, '--', '--acceptance-root', $Root, '--run-id', $ExpectedRunId)
    $output = & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Streaming acceptance reducer failed for $Root." }
    return $output | ConvertFrom-Json
}

function Remove-SurplusSuccessfulRuns([string]$RootDirectory, [string]$CurrentRun, [int]$KeepCount) {
    $successful = Get-ChildItem -LiteralPath $RootDirectory -Directory -Filter 'same-machine-*' -ErrorAction SilentlyContinue | ForEach-Object {
        $summary = Join-Path $_.FullName 'acceptance-summary.json'
        if (Test-Path -LiteralPath $summary) {
            try {
                $parsed = Get-Content -Raw -LiteralPath $summary | ConvertFrom-Json
                if ($parsed.status -eq 'passed' -and -not $parsed.keepArtifacts -and $_.FullName -ne $CurrentRun) { $_ }
            } catch { }
        }
    } | Sort-Object LastWriteTimeUtc -Descending
    # KeepCount is the total allowed successful retention, including the current successful run.
    $successful | Select-Object -Skip ([Math]::Max(0, $KeepCount - 1)) | Remove-Item -Recurse -Force
}

$port = Get-FreeUdpPort
$manifest = [ordered]@{
    schema = 'abilitykit.cooking-harness-manifest.v1'; runId = $runId; topology = 'same-machine-udp'; startedAtUtc = [DateTimeOffset]::UtcNow.ToString('O');
    port = $port; status = 'running'; keepArtifacts = [bool]$KeepArtifacts; successRetention = $SuccessRetention;
    hostDirectory = 'host'; clientDirectory = 'client'; twoPcLanAcceptance = 'not-run; same-machine UDP fixture only';
}
$manifestPath = Join-Path $root 'manifest.live.json'
Write-AtomicJson $manifestPath $manifest
$passed = $false
try {
    dotnet build $harnessProject --nologo | Out-Host
    $hostArguments = @('run', '--no-build', '--project', $harnessProject, '--', '--role', 'host', '--run-id', $runId, '--port', "$port", '--topology', 'udp-same-machine', '--artifact-directory', $hostDirectory, '--timeout-seconds', "$ScenarioTimeoutSeconds")
    $hostProcess = Start-Process -FilePath 'dotnet' -ArgumentList $hostArguments -RedirectStandardOutput (Join-Path $hostDirectory 'stdout.log') -RedirectStandardError (Join-Path $hostDirectory 'stderr.log') -PassThru
    $manifest.hostProcessId = $hostProcess.Id
    Write-AtomicJson $manifestPath $manifest
    $ready = Join-Path $hostDirectory 'ready.json'
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($StartupTimeoutSeconds)
    while (-not (Test-Path -LiteralPath $ready) -and [DateTimeOffset]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }
    if (-not (Test-Path -LiteralPath $ready)) { throw "Host did not produce readiness artifact within $StartupTimeoutSeconds seconds." }

    $clientArguments = @('run', '--no-build', '--project', $harnessProject, '--', '--role', 'client', '--run-id', $runId, '--peer', '127.0.0.1', '--port', "$port", '--topology', 'udp-same-machine', '--artifact-directory', $clientDirectory, '--timeout-seconds', "$ScenarioTimeoutSeconds")
    $clientProcess = Start-Process -FilePath 'dotnet' -ArgumentList $clientArguments -RedirectStandardOutput (Join-Path $clientDirectory 'stdout.log') -RedirectStandardError (Join-Path $clientDirectory 'stderr.log') -PassThru
    $manifest.clientProcessId = $clientProcess.Id
    Write-AtomicJson $manifestPath $manifest
    if (-not $clientProcess.WaitForExit($ScenarioTimeoutSeconds * 1000) -or -not $clientProcess.HasExited) { throw 'Client exceeded bounded scenario timeout.' }
    $clientProcess.Refresh()
    if (-not $hostProcess.WaitForExit(($ScenarioTimeoutSeconds + 5) * 1000) -or -not $hostProcess.HasExited) { throw 'Host did not stop within bounded lifetime.' }
    $hostProcess.Refresh()
    $acceptance = Test-Acceptance $root $runId
    $passed = $true
}
catch {
    $manifest.failure = $_.Exception.Message
    throw
}
finally {
    foreach ($processId in @($manifest.clientProcessId, $manifest.hostProcessId)) {
        if ($null -ne $processId) {
            $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
            if ($null -ne $process -and -not $process.HasExited) { Stop-Process -Id $processId -Force }
        }
    }
    $manifest.status = if ($passed) { 'passed' } else { 'failed' }
    $manifest.finishedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    $summary = [ordered]@{
        schema = 'abilitykit.cooking-harness-acceptance.v1'; runId = $runId; topology = 'same-machine-udp'; status = $manifest.status;
        keepArtifacts = [bool]$KeepArtifacts; hostDirectory = 'host'; clientDirectory = 'client'; finishedAtUtc = $manifest.finishedAtUtc;
        failure = $manifest.failure; evidence = if ($passed) { $acceptance } else { $null };
    }
    # Final files are not observable until both child roles have exited and acceptance has reached its terminal result.
    Write-AtomicJson (Join-Path $root 'manifest.json') $manifest
    Write-AtomicJson (Join-Path $root 'acceptance-summary.json') $summary
    Remove-Item -LiteralPath $manifestPath -Force -ErrorAction SilentlyContinue
    if ($passed -and -not $KeepArtifacts) { Remove-SurplusSuccessfulRuns ([IO.Path]::GetFullPath($ResultsDirectory)) $root $SuccessRetention }
}
Write-Host "Cooking UDP same-machine artifacts: $root"
