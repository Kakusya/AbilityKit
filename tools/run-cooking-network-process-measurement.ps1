param(
    [ValidateRange(1,3)][int]$Repeats=3,
    [switch]$NoBuild,
    [string]$OutputDirectory='local/Logs/cooking-network-process-measurement'
)
$ErrorActionPreference='Stop'
$measurementRoot=Split-Path -Parent $PSScriptRoot
$measurementProject=Join-Path $measurementRoot 'src/AbilityKit.Game.Cooking.NetworkProcessMeasurement/AbilityKit.Game.Cooking.NetworkProcessMeasurement.csproj'
$measurementDll=Join-Path $measurementRoot 'src/AbilityKit.Game.Cooking.NetworkProcessMeasurement/bin/Debug/net10.0/AbilityKit.Game.Cooking.NetworkProcessMeasurement.dll'
$measurementDirectory=[IO.Path]::GetFullPath((Join-Path (Join-Path $measurementRoot $OutputDirectory) ([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fffffff'))))
New-Item -ItemType Directory -Path $measurementDirectory -Force | Out-Null
& git -C $measurementRoot rev-parse HEAD | Set-Content -LiteralPath (Join-Path $measurementDirectory 'source-commit.txt')
& git -C $measurementRoot status --short | Set-Content -LiteralPath (Join-Path $measurementDirectory 'source-status.txt')
& dotnet --info *> (Join-Path $measurementDirectory 'sdk-info.txt')
if(!$NoBuild){
    & dotnet build $measurementProject --verbosity minimal *> (Join-Path $measurementDirectory 'build.log')
    if($LASTEXITCODE -ne 0){throw 'Process measurement build failed.'}
}
if(!(Test-Path -LiteralPath $measurementDll)){throw 'Process measurement executable absent.'}
Get-FileHash -LiteralPath $measurementDll -Algorithm SHA256 | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $measurementDirectory 'executable-hash.json')
Get-ChildItem -LiteralPath (Split-Path -Parent $measurementProject) -File | Where-Object { $_.Extension -in '.cs','.csproj' } | ForEach-Object { Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256 } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $measurementDirectory 'owned-source-hashes.json')
Get-FileHash -LiteralPath $PSCommandPath,(Join-Path $measurementRoot 'src/AbilityKit.Game.Cooking.NetworkAcceptance/SingleThreadOwner.cs') -Algorithm SHA256 | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $measurementDirectory 'linked-wrapper-source-hashes.json')
function Quote-MeasurementArgument([string]$value){if($value.Contains('"')){throw 'Unsupported quote in argument.'};return '"'+$value+'"'}
function Require-Measurement([bool]$condition,[string]$message){if(!$condition){throw $message}}
$paired=@()
for($measurementRepeat=1;$measurementRepeat -le $Repeats;$measurementRepeat++){
    $pairDirectory=Join-Path $measurementDirectory ('repeat-'+$measurementRepeat)
    New-Item -ItemType Directory -Path $pairDirectory -Force | Out-Null
    $hostReportPath=Join-Path $pairDirectory 'host.json';$clientReportPath=Join-Path $pairDirectory 'client.json';$hostOutput=Join-Path $pairDirectory 'host.stdout.log'
    $hostProcess=$null;$clientProcess=$null
    try{
        $hostArgs=(Quote-MeasurementArgument $measurementDll)+' host --ip 127.0.0.1 --port 0 --repeat '+$measurementRepeat+' --report '+(Quote-MeasurementArgument $hostReportPath)
        $hostProcess=Start-Process dotnet -ArgumentList $hostArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput $hostOutput -RedirectStandardError (Join-Path $pairDirectory 'host.stderr.log')
        $null=$hostProcess.Handle
        $readyPort=$null;$readyDeadline=[DateTime]::UtcNow.AddSeconds(30)
        while([DateTime]::UtcNow -lt $readyDeadline){
            $hostProcess.Refresh();if($hostProcess.HasExited){throw 'Host exited before readiness.'}
            if(Test-Path -LiteralPath $hostOutput){
                $text=Get-Content -LiteralPath $hostOutput -Raw
                if($text -match '(?m)^READY ([0-9]+) ([0-9]+)\s*$'){
                    Require-Measurement ([int]$Matches[2] -eq $hostProcess.Id) 'Readiness PID mismatch.'
                    $readyPort=[int]$Matches[1];break
                }
            }
            Start-Sleep -Milliseconds 100
        }
        Require-Measurement ($null -ne $readyPort -and $readyPort -gt 0) 'Readiness deadline.'
        $clientArgs=(Quote-MeasurementArgument $measurementDll)+' client --ip 127.0.0.1 --port '+$readyPort+' --repeat '+$measurementRepeat+' --report '+(Quote-MeasurementArgument $clientReportPath)
        $clientProcess=Start-Process dotnet -ArgumentList $clientArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $pairDirectory 'client.stdout.log') -RedirectStandardError (Join-Path $pairDirectory 'client.stderr.log')
        $null=$clientProcess.Handle
        $exitDeadline=[DateTime]::UtcNow.AddSeconds(300)
        while([DateTime]::UtcNow -lt $exitDeadline){
            $hostProcess.Refresh();$clientProcess.Refresh()
            if($hostProcess.HasExited -and $clientProcess.HasExited){break}
            foreach($entry in @(@($hostProcess,$hostReportPath),@($clientProcess,$clientReportPath))){
                if($entry[0].HasExited -and (Test-Path -LiteralPath $entry[1])){
                    $interim=Get-Content -LiteralPath $entry[1] -Raw | ConvertFrom-Json
                    if($interim.passed -ne $true){throw 'Endpoint failed; retain original JSON/stderr.'}
                }
            }
            Start-Sleep -Milliseconds 100
        }
        Require-Measurement ($hostProcess.HasExited -and $clientProcess.HasExited) 'Pair deadline.'
        $hostProcess.WaitForExit();$clientProcess.WaitForExit()
        Require-Measurement ($null -ne $hostProcess.ExitCode -and $null -ne $clientProcess.ExitCode -and $hostProcess.ExitCode -eq 0 -and $clientProcess.ExitCode -eq 0) 'Endpoint exit codes not both zero.'
        Require-Measurement ((Test-Path -LiteralPath $hostReportPath) -and (Test-Path -LiteralPath $clientReportPath)) 'Endpoint report absent.'
        $h=Get-Content -LiteralPath $hostReportPath -Raw | ConvertFrom-Json;$c=Get-Content -LiteralPath $clientReportPath -Raw | ConvertFrom-Json
        Require-Measurement ($h.passed -is [bool] -and $c.passed -is [bool] -and $h.passed -and $c.passed) 'Endpoint passed flags invalid.'
        Require-Measurement ($h.pid -eq $hostProcess.Id -and $c.pid -eq $clientProcess.Id -and $h.pid -ne $c.pid) 'Separate launched PIDs not proven.'
        Require-Measurement ($h.address -eq '127.0.0.1' -and $c.address -eq '127.0.0.1' -and $h.port -eq $readyPort -and $c.port -eq $readyPort -and $h.machine -eq [Environment]::MachineName -and $c.machine -eq [Environment]::MachineName) 'Actual loopback endpoint/machine provenance mismatch.'
        foreach($r in @($h,$c)){
            Require-Measurement ($r.fixture -eq 'cooking-two-process-submit-fault-load-v3' -and $r.topology -eq 'SameMachineIndependentProcessesUdp' -and $r.protocol -eq 3 -and $r.recipeSchema -eq 5 -and $r.levelFormat -eq 8 -and $r.repeat -eq $measurementRepeat) 'Fixture/schema/topology/repeat mismatch.'
            foreach($field in @('etMvid','sessionMvid')){
                $parsed=[Guid]::Empty;Require-Measurement ([Guid]::TryParse([string]$r.$field,[ref]$parsed) -and $parsed -ne [Guid]::Empty) 'Missing/invalid build GUID.'
            }
            Require-Measurement ($r.evidence.configurationIdentity -is [string] -and $r.evidence.configurationIdentity -cmatch '^cooking-definition-v3:[0-9A-F]{64}$') 'Missing/invalid configuration identity.'
            Require-Measurement ($r.evidence.finalBaselineSize.bytes -gt 0 -and $r.evidence.finalBaselineSize.bytes -le 8388608 -and $r.evidence.finalBaselineSize.tokens -gt 0 -and $r.evidence.finalBaselineSize.tokens -le 1048576) 'Final complete wire baseline size outside bounds.'
            $parsed=[Guid]::Empty;Require-Measurement ([Guid]::TryParse([string]$r.evidence.serverInstance,[ref]$parsed) -and $parsed -ne [Guid]::Empty) 'Missing/invalid instance GUID.'
            Require-Measurement ($r.evidence.hash -cmatch '^[0-9a-f]{64}$' -and $r.evidence.baselineHash -cmatch '^[0-9a-f]{64}$') 'Complete hashes absent.'
        }
        Require-Measurement ($h.evidence.localIsSynchronized -eq $true -and $c.evidence.connected -eq $true -and $c.evidence.ready -eq $true -and $h.evidence.finalLifecycle -eq 'Paused' -and $c.evidence.finalLifecycle -eq 'Paused' -and $h.evidence.resumableCheckpointPresent -eq $false -and $c.evidence.resumableCheckpointPresent -eq $false) 'Final complete Paused readiness absent.'
        Require-Measurement (($c.evidence.finalAckIdentity | ConvertTo-Json -Depth 10 -Compress) -eq ($c.evidence.finalBaseline.Identity | ConvertTo-Json -Depth 10 -Compress)) 'Final exact issued ACK identity absent.'
        Require-Measurement ($h.role -eq 'host' -and $c.role -eq 'client' -and $h.etMvid -eq $c.etMvid -and $h.sessionMvid -eq $c.sessionMvid) 'Role/build pairing mismatch.'
        Require-Measurement ($h.evidence.serverInstance -eq $c.evidence.serverInstance -and $h.evidence.hash -eq $c.evidence.hash) 'Instance/full gameplay consensus mismatch.'
        Require-Measurement (($h.evidence.scope | ConvertTo-Json -Depth 10 -Compress) -eq ($c.evidence.scope | ConvertTo-Json -Depth 10 -Compress)) 'Complete scope pairing mismatch.'
        Require-Measurement ($h.evidence.scope.LevelEpoch -eq 1 -and $h.evidence.scope.Level.Value -eq 'load') 'Unexpected measurement scope.'
        Require-Measurement ($c.evidence.generation -eq 2 -and $c.evidence.fault.droppedResponses -eq 1 -and $c.evidence.fault.cachedDuplicate -eq $true -and $c.evidence.fault.settlements -eq 1 -and $c.evidence.fault.tombstones -eq 2) 'Actual lost-submit/rebind conservation proof absent.'
        Require-Measurement ($h.evidence.authorityCreateCount -eq 1) 'One actual authority not proven.'
        Require-Measurement ($h.evidence.configurationIdentity -ceq $c.evidence.configurationIdentity) 'Trusted configuration identity mismatch.'
        foreach($load in @($h.evidence.localLoad,$c.evidence.load)){
            Require-Measurement ($load.Offered -eq 300 -and $load.WarmupOffered -eq 50 -and $load.Issued -eq $load.Accepted -and $load.Pending -eq 0 -and $load.Rejected -eq 0 -and $load.Cancelled -eq 0 -and $load.Issued+$load.SkippedBackpressure+$load.SchedulerSkipped -eq $load.Offered) 'Offered/terminal accounting inconsistent.'
            Require-Measurement (@($load.IssuedDomainIds).Count -eq $load.Issued) 'Issued-ID sample count inconsistent.'
            Require-Measurement ($load.WarmupIssued -eq $load.WarmupAccepted -and $load.WarmupIssued+$load.WarmupSkippedBackpressure+$load.WarmupSchedulerSkipped -eq $load.WarmupOffered -and @($load.WarmupIssuedDomainIds).Count -eq $load.WarmupIssued) 'Warmup cohort accounting inconsistent.'
            foreach($id in @($load.IssuedDomainIds)+@($load.WarmupIssuedDomainIds)){Require-Measurement ($h.evidence.actuallyAdmittedDomainIds -contains $id) 'Issued sample missing real ET admission.'}
        }
        Require-Measurement (@($h.evidence.actuallyAdmittedDomainIds | Sort-Object -Unique).Count -eq @($h.evidence.actuallyAdmittedDomainIds).Count) 'Actual ET admission IDs repeated.'
        $proof=@{passed=$true;repeat=$measurementRepeat;hostPid=$hostProcess.Id;clientPid=$clientProcess.Id;hostExitCode=$hostProcess.ExitCode;clientExitCode=$clientProcess.ExitCode;hash=$h.evidence.hash;instance=$h.evidence.serverInstance;scope=$h.evidence.scope;hostAccepted=$h.evidence.localLoad.Accepted;remoteAccepted=$c.evidence.load.Accepted;topology='SameMachineIndependentProcessesUdp';performanceTarget='UNSET';physicalTwoPc='NOT_VERIFIED'}
        $proof | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $pairDirectory 'paired.json') -Encoding UTF8
        $paired+=$proof
    }finally{
        foreach($owned in @($clientProcess,$hostProcess)){if($null -ne $owned){$owned.Refresh();if(!$owned.HasExited){$owned.Kill();$owned.WaitForExit()};$owned.Dispose()}}
    }
}
Require-Measurement ($paired.Count -eq $Repeats -and @($paired | ForEach-Object instance | Sort-Object -Unique).Count -eq $Repeats) 'Fresh pairs not proven.'
@{passed=$true;repeats=$Repeats;paired=$paired;performanceTarget='UNSET';physicalTwoPc='NOT_VERIFIED'} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $measurementDirectory 'summary.json') -Encoding UTF8
Write-Output "PASS bounded correctness/fault/load collection ($Repeats pairs); no performance threshold; physical LAN NOT_VERIFIED."
Write-Output "Artifacts: $measurementDirectory"
