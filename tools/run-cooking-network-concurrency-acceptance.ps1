param(
    [ValidateSet('SameMachine','Host','Client')][string]$Mode = 'SameMachine',
    [ValidateSet('remote-first','local-first')][string]$Order = 'remote-first',
    [string]$RemoteIp = '127.0.0.1', [string]$BindIp = '0.0.0.0',
    [ValidateRange(0,65535)][int]$Port = 0,
    [string]$RunId = '', [string]$Source = '', [string]$Dirty = '',
    [string]$OutputDirectory = 'local/Logs/cooking-network-concurrency', [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $workspaceRoot 'src/AbilityKit.Game.Cooking.NetworkConcurrencyAcceptance/AbilityKit.Game.Cooking.NetworkConcurrencyAcceptance.csproj'
$dll = Join-Path $workspaceRoot 'src/AbilityKit.Game.Cooking.NetworkConcurrencyAcceptance/bin/Debug/net10.0/AbilityKit.Game.Cooking.NetworkConcurrencyAcceptance.dll'
if (!$RunId) {
    if ($Mode -ne 'SameMachine') { throw 'Separate Host/Client require the same explicit RunId.' }
    $RunId = [Guid]::NewGuid().ToString('N')
}
if (!$Source) { $Source = (& git -C $workspaceRoot rev-parse HEAD).Trim() }
if (!$Dirty) { $Dirty = if (@(& git -C $workspaceRoot status --porcelain).Count -eq 0) { 'clean' } else { 'dirty' } }
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fffffff')
$runDirectory = [IO.Path]::GetFullPath((Join-Path (Join-Path $workspaceRoot $OutputDirectory) $stamp))
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
if (!$NoBuild) {
    & dotnet build $project --verbosity minimal *> (Join-Path $runDirectory 'build.log')
    if ($LASTEXITCODE -ne 0) { throw "Build failed; retained $runDirectory/build.log" }
}
if (!(Test-Path -LiteralPath $dll)) { throw "Executable not found: $dll" }
function Quote-Argument([string]$value) { if ($value.Contains('"')) { throw 'Unsupported quotation in argument.' }; return '"' + $value + '"' }
function Start-Endpoint([string]$role, [string]$address, [int]$endpointPort, [string]$topology) {
    $arguments = (Quote-Argument $dll) + " $role --ip " + (Quote-Argument $address) + " --port $endpointPort --run-id " + (Quote-Argument $RunId) + ' --source ' + (Quote-Argument $Source) + ' --dirty ' + (Quote-Argument $Dirty) + ' --order ' + $Order + ' --topology ' + $topology + ' --report ' + (Quote-Argument (Join-Path $runDirectory "$role.json"))
    $process = Start-Process dotnet -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runDirectory "$role.stdout.log") -RedirectStandardError (Join-Path $runDirectory "$role.stderr.log")
    $null = $process.Handle
    return $process
}
$ownedProcesses = @()
$script:readyAnnounced = $false
$wholeDeadline = [DateTime]::UtcNow.AddSeconds(210)
try {
    if ($Mode -ne 'SameMachine') {
        if ($Mode -eq 'Client' -and $Port -eq 0) { throw 'Client requires the actual READY endpoint port.' }
        $role = $Mode.ToLowerInvariant(); $address = if ($Mode -eq 'Host') { $BindIp } else { $RemoteIp }
        $endpoint = Start-Endpoint $role $address $Port 'SeparateHostsRequiresPairedEvidence'; $ownedProcesses += $endpoint
        while (!$endpoint.HasExited -and [DateTime]::UtcNow -lt $wholeDeadline) {
            if ($role -eq 'host' -and (Test-Path -LiteralPath (Join-Path $runDirectory 'host.stdout.log'))) {
                $text = Get-Content -LiteralPath (Join-Path $runDirectory 'host.stdout.log') -Raw
                if ($text -match '(?m)^READY ([0-9]+) ([0-9]+)\s*$' -and !$script:readyAnnounced) { Write-Output $Matches[0]; $script:readyAnnounced = $true }
            }
            Start-Sleep -Milliseconds 100; $endpoint.Refresh()
        }
        if (!$endpoint.HasExited) { throw '210-second wrapper deadline.' }
        $endpoint.WaitForExit(); Write-Output "Artifacts: $runDirectory; separate endpoints require manual paired physical topology review."
        exit $endpoint.ExitCode
    }
    $hostProcess = Start-Endpoint 'host' '127.0.0.1' $Port 'SameMachineIndependentProcessesUdp'; $ownedProcesses += $hostProcess
    $readyPort = 0; $readyDeadline = [DateTime]::UtcNow.AddSeconds(20)
    while ([DateTime]::UtcNow -lt $readyDeadline) {
        $hostProcess.Refresh(); if ($hostProcess.HasExited) { throw 'Host exited before READY.' }
        $stdout = Join-Path $runDirectory 'host.stdout.log'
        if (Test-Path -LiteralPath $stdout) {
            $text = Get-Content -LiteralPath $stdout -Raw
            if ($text -match '(?m)^READY ([0-9]+) ([0-9]+)\s*$') {
                if ([int]$Matches[2] -ne $hostProcess.Id) { throw 'READY PID mismatch.' }
                $readyPort = [int]$Matches[1]; break
            }
        }
        Start-Sleep -Milliseconds 100
    }
    if ($readyPort -le 0) { throw 'READY deadline.' }
    $clientProcess = Start-Endpoint 'client' '127.0.0.1' $readyPort 'SameMachineIndependentProcessesUdp'; $ownedProcesses += $clientProcess
    while ([DateTime]::UtcNow -lt $wholeDeadline) {
        $hostProcess.Refresh(); $clientProcess.Refresh()
        if ($hostProcess.HasExited -and $clientProcess.HasExited) { break }
        Start-Sleep -Milliseconds 100
    }
    if (!$hostProcess.HasExited -or !$clientProcess.HasExited) { throw '210-second paired deadline.' }
    $hostProcess.WaitForExit(); $clientProcess.WaitForExit()
    $hostReport = Get-Content -LiteralPath (Join-Path $runDirectory 'host.json') -Raw | ConvertFrom-Json
    $clientReport = Get-Content -LiteralPath (Join-Path $runDirectory 'client.json') -Raw | ConvertFrom-Json
    if ($hostProcess.ExitCode -ne 0 -or $clientProcess.ExitCode -ne 0 -or !$hostReport.passed -or !$clientReport.passed) { throw 'Endpoint failure; original reports/stdout/stderr retained.' }
    if ($hostReport.pid -ne $hostProcess.Id -or $clientReport.pid -ne $clientProcess.Id -or $hostReport.pid -eq $clientReport.pid) { throw 'Distinct launched process evidence mismatch.' }
    foreach ($field in @('runId','source','dirty','fixture','protocol','checkpointFormat','order','topology','etMvid','sessionMvid')) {
        if ($hostReport.$field -ne $clientReport.$field -or [string]::IsNullOrWhiteSpace([string]$hostReport.$field)) { throw "Pair mismatch: $field" }
    }
    if ($hostReport.etMvid -eq [Guid]::Empty.ToString() -or $hostReport.sessionMvid -eq [Guid]::Empty.ToString()) { throw 'Missing real MVID.' }
    if (@($hostReport.binaries.PSObject.Properties).Count -ne @($clientReport.binaries.PSObject.Properties).Count) { throw 'Binary/content manifest count mismatch.' }
    foreach ($property in $hostReport.binaries.PSObject.Properties) {
        if ($clientReport.binaries.($property.Name) -ne $property.Value) { throw "Binary/content provenance mismatch: $($property.Name)" }
    }
    if ($hostReport.final.hash -ne $clientReport.final.hash -or [string]::IsNullOrWhiteSpace($hostReport.final.hash) -or $hostReport.final.serverSessionInstance -ne $clientReport.final.serverSessionInstance) { throw 'Full-state consensus/instance mismatch.' }
    if (($hostReport.final.scope | ConvertTo-Json -Depth 20 -Compress) -ne ($clientReport.final.scope | ConvertTo-Json -Depth 20 -Compress)) { throw 'Exact scope mismatch.' }
    if (($hostReport.final.remoteIssued.identity | ConvertTo-Json -Depth 20 -Compress) -ne ($clientReport.final.remoteBaseline.identity | ConvertTo-Json -Depth 20 -Compress)) { throw 'Final current issued remote ACK identity mismatch.' }
    if (@($hostReport.final.preCloseProjection.participants | Where-Object { !$_.connectedOwnerBinding -or !$_.ready -or $_.cleanupPending }).Count -ne 0 -or @($hostReport.final.preCloseProjection.participants).Count -ne 2) { throw 'Both live Ready/no-cleanup pre-close evidence absent.' }
    if (!$hostReport.final.remoteCurrentExactAckReady -or !$hostReport.final.localExactAckReady -or !$clientReport.final.remoteExactAckReady -or $clientReport.final.generation -ne 2) { throw 'Exact complete baseline/rebound Ready proof absent.' }
    for ($phase = 1; $phase -le 21; $phase++) {
        $hostEvidence = @($hostReport.evidence | Where-Object { $_.phase -eq $phase })
        $remoteEvidence = @($clientReport.evidence | Where-Object { $_.phase -eq $phase })
        if ($hostEvidence.Count -ne 1 -or $remoteEvidence.Count -ne 1) { throw "Missing case $phase" }
        if (($hostEvidence[0].remote | ConvertTo-Json -Depth 40 -Compress) -ne ($remoteEvidence[0].result | ConvertTo-Json -Depth 40 -Compress)) { throw "Actual outbound/received terminal mismatch case $phase" }
        if (($hostEvidence[0].delivered.command | ConvertTo-Json -Depth 40 -Compress) -ne ($remoteEvidence[0].wire | ConvertTo-Json -Depth 40 -Compress)) { throw "Actual received/sent wire mismatch case $phase" }
    }
    @{passed=$true;runId=$RunId;hostPid=$hostProcess.Id;clientPid=$clientProcess.Id;hostExit=$hostProcess.ExitCode;clientExit=$clientProcess.ExitCode;hash=$hostReport.final.hash;physicalTwoPc='NOT_VERIFIED';topology='SameMachineIndependentProcessesUdp';richServiceCompanion='SEPARATE_REQUIRED'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runDirectory 'paired.json') -Encoding UTF8
    Write-Output "PASS real concurrency controls; artifacts $runDirectory; physical LAN NOT_VERIFIED."
} catch {
    @{passed=$false;runId=$RunId;failure=$_.Exception.ToString();physicalTwoPc='NOT_VERIFIED'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runDirectory 'wrapper-failure.json') -Encoding UTF8
    throw
} finally {
    foreach ($ownedProcess in $ownedProcesses) {
        $ownedProcess.Refresh()
        if (!$ownedProcess.HasExited) {
            $current = Get-Process -Id $ownedProcess.Id -ErrorAction SilentlyContinue
            if ($null -ne $current -and $current.StartTime -eq $ownedProcess.StartTime) { Stop-Process -InputObject $current }
        }
        $ownedProcess.Dispose()
    }
}
