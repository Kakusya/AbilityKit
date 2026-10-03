param([ValidateSet('P0','P1','P2','P3','P4','P5','P6')][string]$Profile='P0',[string]$OutputDirectory='local/Logs/cooking-execution/network-impairment',[string]$ControlEvidence,[string]$RelayControlEvidence,[switch]$BuildOnly)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$driver=Join-Path $root 'src/AbilityKit.Game.Cooking.NetworkImpairmentMeasurement/bin/Debug/net10.0/AbilityKit.Game.Cooking.NetworkImpairmentMeasurement.dll'
$relay=Join-Path $root 'src/AbilityKit.Game.Cooking.DatagramImpairmentRelay/bin/Debug/net10.0/AbilityKit.Game.Cooking.DatagramImpairmentRelay.dll'
$buildManifest=Join-Path (Split-Path $driver -Parent) 'impairment-build-provenance.json'
$buildProjects=@('src/AbilityKit.Game.Cooking.DatagramImpairmentRelay/AbilityKit.Game.Cooking.DatagramImpairmentRelay.csproj','src/AbilityKit.Game.Cooking.NetworkImpairmentMeasurement/AbilityKit.Game.Cooking.NetworkImpairmentMeasurement.csproj')
function Build-Inputs {
 $inputs=@()
 foreach($base in @((Join-Path $root 'src'),(Join-Path $root 'Unity/Packages'))){
  foreach($file in @(Get-ChildItem -LiteralPath $base -File -Recurse|Where-Object {$_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in @('.cs','.csproj','.props','.targets','.asmdef','.json')}|Sort-Object FullName)){
   $inputs+=[pscustomobject]@{path=$file.FullName.Substring($root.Length+1);sha=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash}
  }
 }
 foreach($name in @('Directory.Build.props','Directory.Build.targets','Directory.Packages.props','NuGet.Config','global.json','tools/run-cooking-network-impairment-measurement.ps1')){if(Test-Path -LiteralPath (Join-Path $root $name)){$inputs+=[pscustomobject]@{path=$name;sha=(Get-FileHash -LiteralPath (Join-Path $root $name) -Algorithm SHA256).Hash}}}
 return @($inputs|Sort-Object path)
}
function Assert-InputsEqual($accepted,$current){
 if(@($accepted).Count -ne @($current).Count){throw 'Compiled-source provenance file-set changed'}
 for($i=0;$i -lt @($current).Count;$i++){if($accepted[$i].path -ne $current[$i].path -or $accepted[$i].sha -ne $current[$i].sha){throw "Compiled-source provenance differs: $($current[$i].path)"}}
}
if($BuildOnly){
 if($Profile -ne 'P0'){throw 'Audited build is separate from impairment execution; use P0/default build-only.'}
 $before=Build-Inputs;$buildDirectory=Join-Path $root ('local/Logs/cooking-execution/impairment-build/'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fffffff'));New-Item -ItemType Directory -Path $buildDirectory -Force|Out-Null
 if(Test-Path -LiteralPath $buildManifest){Copy-Item -LiteralPath $buildManifest -Destination (Join-Path $buildDirectory 'previous-build-provenance.json');Remove-Item -LiteralPath $buildManifest}
 $commands=@();$success=$false;$buildFailure=$null
 try{
  foreach($project in $buildProjects){
   $log=Join-Path $buildDirectory ([IO.Path]::GetFileNameWithoutExtension($project)+'.log')
   & dotnet build (Join-Path $root $project) --configuration Debug --no-incremental *> $log
   $nativeExit=$LASTEXITCODE;$commands+=@{project=$project;configuration='Debug';noIncremental=$true;exitCode=$nativeExit;log=$log}
   if($nativeExit -ne 0){throw "Actual audited build failed: $project exit$nativeExit"}
  }
  Assert-InputsEqual $before (Build-Inputs)
  $success=$true
  @{passed=$true;inputs=$before;commands=$commands;driverSha=(Get-FileHash -LiteralPath $driver -Algorithm SHA256).Hash;relaySha=(Get-FileHash -LiteralPath $relay -Algorithm SHA256).Hash;machine=$env:COMPUTERNAME;sourceHead=(& git -C $root rev-parse HEAD);builtUtc=[DateTime]::UtcNow.ToString('o');scope='Audited two-project no-incremental build with repository compile inputs unchanged before/after; complete runtime content is separately frozen by execution manifest.'}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $buildManifest
  Write-Output "BUILD PASS $buildManifest"
 }catch{$buildFailure=$_.ToString();throw}finally{@{passed=$success;failure=$buildFailure;commands=$commands;inputs=$before}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $buildDirectory 'build-attempt.json')}
 return
}
if(!(Test-Path -LiteralPath $driver) -or !(Test-Path -LiteralPath $relay)){throw 'Build both owned projects explicitly first.'}
if(!(Test-Path -LiteralPath $buildManifest)){throw 'Audited -BuildOnly compiled-source provenance required before any execution.'}
$built=Get-Content -LiteralPath $buildManifest -Raw|ConvertFrom-Json
if(!$built.passed -or $built.machine -ne $env:COMPUTERNAME -or $built.driverSha -ne (Get-FileHash -LiteralPath $driver -Algorithm SHA256).Hash -or $built.relaySha -ne (Get-FileHash -LiteralPath $relay -Algorithm SHA256).Hash -or @($built.commands).Count -ne 2 -or @($built.commands|Where-Object {$_.exitCode -ne 0 -or !$_.noIncremental -or $_.configuration -ne 'Debug'}).Count){throw 'Audited actual build manifest/binary mismatch'}
for($i=0;$i -lt $buildProjects.Count;$i++){if($built.commands[$i].project -ne $buildProjects[$i]){throw 'Audited build manifest actual project paths/order mismatch'}}
Assert-InputsEqual @($built.inputs) (Build-Inputs)
if(!$RelayControlEvidence){throw 'Actual same-binary relay control evidence required.'}
$sourcePaths=@('src/AbilityKit.Game.Cooking.DatagramImpairmentRelay/AbilityKit.Game.Cooking.DatagramImpairmentRelay.csproj','src/AbilityKit.Game.Cooking.DatagramImpairmentRelay/Program.cs','src/AbilityKit.Game.Cooking.DatagramImpairmentRelay/RelayModel.cs','src/AbilityKit.Game.Cooking.DatagramImpairmentRelay/RelayControls.cs','src/AbilityKit.Game.Cooking.DatagramImpairmentRelay/ControlledRelayChecks.cs','src/AbilityKit.Game.Cooking.DatagramImpairmentRelay/ControlledQueueChecks.cs','src/AbilityKit.Game.Cooking.DatagramImpairmentRelay/ClosedPortControls.cs','src/AbilityKit.Game.Cooking.DatagramImpairmentRelay/ControlMailbox.cs','src/AbilityKit.Game.Cooking.NetworkImpairmentMeasurement/AbilityKit.Game.Cooking.NetworkImpairmentMeasurement.csproj','src/AbilityKit.Game.Cooking.NetworkImpairmentMeasurement/Program.cs','src/AbilityKit.Game.Cooking.NetworkImpairmentMeasurement/LoadWaitEvidence.cs','src/AbilityKit.Game.Cooking.NetworkProcessMeasurement/ProcessMeasurementFixture.cs','src/AbilityKit.Game.Cooking.NetworkProcessMeasurement/FramedFaultPeer.cs','src/AbilityKit.Game.Cooking.NetworkAcceptance/SingleThreadOwner.cs','tools/run-cooking-network-impairment-measurement.ps1')
$sourceManifest=@();foreach($sourcePath in $sourcePaths){$sourceManifest+=@{path=$sourcePath;sha=(Get-FileHash -LiteralPath (Join-Path $root $sourcePath) -Algorithm SHA256).Hash}}
$runtimeManifest=@();foreach($assembly in @($driver,$relay)){$base=Split-Path $assembly -Parent;foreach($file in @(Get-ChildItem -LiteralPath $base -File -Recurse|Sort-Object FullName)){$runtimeManifest+=@{role=[IO.Path]::GetFileNameWithoutExtension($assembly);path=$file.FullName.Substring($base.Length+1);sha=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash}}}
$provenance=@{driverSha=(Get-FileHash -LiteralPath $driver -Algorithm SHA256).Hash;relaySha=(Get-FileHash -LiteralPath $relay -Algorithm SHA256).Hash;machine=$env:COMPUTERNAME;sources=$sourceManifest;runtime=$runtimeManifest;auditedBuildManifestSha=(Get-FileHash -LiteralPath $buildManifest -Algorithm SHA256).Hash}
$relayControls=Get-Content -LiteralPath $RelayControlEvidence -Raw|ConvertFrom-Json
if(!$relayControls.passed -or $relayControls.relaySha -ne $provenance.relaySha -or $relayControls.machine -ne $provenance.machine){throw 'Actual relay control execution against current binary required.'}
$provenance.relayControlReportSha=(Get-FileHash -LiteralPath $RelayControlEvidence -Algorithm SHA256).Hash
if($Profile -ne 'P0'){
 if(!$ControlEvidence){throw 'Actual P0 three-pair evidence required before impairment.'}
 $control=Get-Content -LiteralPath $ControlEvidence -Raw | ConvertFrom-Json
 if(!$control.passed -or $control.profile -ne 'P0' -or @($control.pairs).Count -ne 3 -or $control.provenance.driverSha -ne $provenance.driverSha -or $control.provenance.relaySha -ne $provenance.relaySha -or $control.provenance.machine -ne $provenance.machine){throw 'P0 evidence provenance mismatch.'}
 foreach($field in @('sources','runtime')){
  $accepted=@($control.provenance.$field);$current=@($provenance[$field]);if($accepted.Count -ne $current.Count){throw 'P0 full manifest count mismatch'}
  for($i=0;$i -lt $current.Count;$i++){if($accepted[$i].path -ne $current[$i].path -or $accepted[$i].sha -ne $current[$i].sha -or $accepted[$i].role -ne $current[$i].role){throw 'P0 full runtime/source/csproj manifest mismatch'}}
 }
}
$output=Join-Path ([IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))) ([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fffffff'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
Copy-Item -LiteralPath $buildManifest -Destination (Join-Path $output 'build-provenance.json')
$pairs=@()
function Start-Owned($role,$assembly,$arguments,$directory){
 $info=New-Object Diagnostics.ProcessStartInfo
 $info.FileName='dotnet';$info.Arguments='"'+$assembly+'" '+$arguments;$info.WorkingDirectory=$root;$info.UseShellExecute=$false;$info.CreateNoWindow=$true
 $info.RedirectStandardInput=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
 $process=New-Object Diagnostics.Process;$process.StartInfo=$info;if(!$process.Start()){throw 'Process start failed'}
 return @{role=$role;process=$process;sequence=0;events=(New-Object Collections.Generic.List[object]);stdout=$process.StandardOutput.ReadLineAsync();stderr=$process.StandardError.ReadLineAsync();outPath=(Join-Path $directory "$role.stdout.log");errPath=(Join-Path $directory "$role.stderr.log")}
}
function Poll-Owned($owned,$nonce){
 foreach($stream in @('stdout','stderr')){
  $task=$owned[$stream]
  if($null -ne $task -and $task.IsCompleted){
   $line=$task.GetAwaiter().GetResult()
   if($null -eq $line){$owned[$stream]=$null;continue}
   if($line.Length -gt 16384){throw 'Output line bound'}
   $path=if($stream -eq 'stdout'){$owned.outPath}else{$owned.errPath};[IO.File]::AppendAllText($path,$line+[Environment]::NewLine)
   if($stream -eq 'stdout' -and $line.StartsWith('CONTROL_EVENT ')){
    $event=$line.Substring(14)|ConvertFrom-Json
    if($event.nonce -ne $nonce -or $event.pid -ne $owned.process.Id){throw 'Control output process provenance mismatch'}
    if($owned.events.Count -ge 64){throw 'Control event queue cap'};$owned.events.Add($event)
   }
   $owned[$stream]=if($stream -eq 'stdout'){$owned.process.StandardOutput.ReadLineAsync()}else{$owned.process.StandardError.ReadLineAsync()}
  }
 }
}
function Send-Control($owned,$nonce,$kind,$extra=@{}){
 $owned.sequence++;$value=@{nonce=$nonce;sequence=$owned.sequence;kind=$kind};foreach($key in $extra.Keys){$value[$key]=$extra[$key]}
 $owned.process.StandardInput.WriteLine(($value|ConvertTo-Json -Compress -Depth 10));$owned.process.StandardInput.Flush()
}
function Drain-Terminal($owned,$nonce){
 $drain=[Diagnostics.Stopwatch]::StartNew()
 while($null -ne $owned.stdout -or $null -ne $owned.stderr){Poll-Owned $owned $nonce;if($drain.Elapsed.TotalSeconds -gt 5){throw 'Owned redirected output EOF drain5s'};Start-Sleep -Milliseconds 1}
}
function Wait-Event($owned,$kind,$all,$nonce,$watch){
 while($true){
  if($watch.Elapsed.TotalSeconds -gt 450){throw 'Wrapper450s deadline'}
  foreach($p in $all){Poll-Owned $p $nonce}
  for($i=0;$i -lt $owned.events.Count;$i++){if($owned.events[$i].kind -eq $kind){$result=$owned.events[$i];$owned.events.RemoveAt($i);return $result.data}}
  foreach($p in $all){if($p.process.HasExited){throw "Premature $($p.role) exit $($p.process.ExitCode) waiting $kind"}}
  Start-Sleep -Milliseconds 2
 }
}
function Allow-Candidate($candidate,$relayOwned,$client,$nonce,$directory){
 $matches=@(Get-NetUDPEndpoint -OwningProcess $client.process.Id -ErrorAction Stop | Where-Object {$_.LocalPort -eq $candidate.port -and $_.LocalAddress -in @('127.0.0.1','0.0.0.0')})
 if($matches.Count -ne 1){throw 'Candidate not backed by exactly one owned client UDP socket'}
 $matches|Select-Object LocalAddress,LocalPort,OwningProcess,CreationTime|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $directory "route-$($candidate.route)-owned-socket.json")
 Send-Control $relayOwned $nonce 'AllowSource' @{route=$candidate.route;address=$candidate.address;port=$candidate.port;clientPid=$client.process.Id}
}
try{
 for($repeat=1;$repeat -le 3;$repeat++){
  $dir=Join-Path $output "repeat-$repeat";New-Item -ItemType Directory -Path $dir|Out-Null
  $nonce=[Guid]::NewGuid().ToString();$watch=[Diagnostics.Stopwatch]::StartNew();$all=New-Object Collections.Generic.List[object]
  try{
   $hostOwned=Start-Owned 'host' $driver ('host --nonce '+$nonce+' --profile '+$Profile+' --repeat '+$repeat+' --report "'+(Join-Path $dir 'host.json')+'"') $dir;$all.Add($hostOwned)
   $hostReady=Wait-Event $hostOwned 'HOST_READY' $all $nonce $watch
   $relayOwned=Start-Owned 'relay' $relay ('--nonce '+$nonce+' --profile '+$Profile+' --repeat '+$repeat+' --backend-port '+$hostReady.backendPort+' --report "'+(Join-Path $dir 'relay.json')+'"') $dir;$all.Add($relayOwned)
   $route1=Wait-Event $relayOwned 'ROUTE_READY' $all $nonce $watch
   $client=Start-Owned 'client' $driver ('client --nonce '+$nonce+' --profile '+$Profile+' --repeat '+$repeat+' --report "'+(Join-Path $dir 'client.json')+'"') $dir;$all.Add($client)
   $front1=[int]($route1.frontend.Split(':')[-1]);Send-Control $client $nonce 'OPEN' @{frontendPort=$front1}
   $candidate=Wait-Event $relayOwned 'SOURCE_CANDIDATE' $all $nonce $watch;Allow-Candidate $candidate $relayOwned $client $nonce $dir
   $null=Wait-Event $relayOwned 'SOURCE_ALLOWED' $all $nonce $watch
   $clientReady=Wait-Event $client 'WAIT_LOAD' $all $nonce $watch;$hostLoad=Wait-Event $hostOwned 'WAIT_LOAD' $all $nonce $watch
   if($hostLoad.instance -ne $clientReady.instance -or $hostLoad.configuration -ne $clientReady.configuration -or $clientReady.generation -ne 1 -or !$clientReady.ack){throw 'Initial actual complete ACK/config/instance barrier'}
   if($hostLoad.configuration -notmatch '^cooking-definition-v3:[0-9A-F]{64}$' -or !$clientReady.ready -or $clientReady.ack.ServerSessionInstance -ne $hostLoad.instance -or $clientReady.ack.ConnectionGeneration -ne 1 -or $clientReady.currentBaselineIdentity.StateHash -ne $clientReady.currentValidatedBaselineHash -or !$clientReady.preflightDomainId -or ($hostLoad.scope|ConvertTo-Json -Compress -Depth 10) -ne ($clientReady.ack.Scope|ConvertTo-Json -Compress -Depth 10)){throw 'Actual matched-issued ACK and independently validated current image facts'}
   foreach($participant in $hostLoad.sessionProjection.Participants){if(!$participant.Ready -or !$participant.ConnectedOwnerBinding -or $participant.CleanupPending -or $participant.ConnectionGeneration -ne 1){throw 'Initial authority Ready gate'}}
   Send-Control $relayOwned $nonce 'Arm';$null=Wait-Event $relayOwned 'ARMED' $all $nonce $watch
   Send-Control $hostOwned $nonce 'BEGIN_LOAD';Send-Control $client $nonce 'BEGIN_LOAD'
   $null=Wait-Event $hostOwned 'SAMPLE_DRAINED' $all $nonce $watch;$null=Wait-Event $client 'SAMPLE_DRAINED' $all $nonce $watch
   Send-Control $relayOwned $nonce 'Off';$null=Wait-Event $relayOwned 'OFF' $all $nonce $watch;$null=Wait-Event $relayOwned 'OFF_DRAINED' $all $nonce $watch
   Send-Control $relayOwned $nonce 'DeclareClientClose' @{route=1};$null=Wait-Event $relayOwned 'CLIENT_CLOSE_DECLARED' $all $nonce $watch
   Send-Control $hostOwned $nonce 'EXPECT_RECOVERY';Send-Control $client $nonce 'CLOSE_OLD';$null=Wait-Event $client 'OLD_CLOSED' $all $nonce $watch;$null=Wait-Event $hostOwned 'REMOTE_CLOSED' $all $nonce $watch
   Send-Control $relayOwned $nonce 'RetireRoute' @{route=1};$null=Wait-Event $relayOwned 'RETIRED' $all $nonce $watch
   Send-Control $relayOwned $nonce 'PrepareRoute';$route2=Wait-Event $relayOwned 'ROUTE_READY' $all $nonce $watch;$front2=[int]($route2.frontend.Split(':')[-1]);Send-Control $client $nonce 'RECOVERY_ROUTE' @{frontendPort=$front2}
   $candidate=Wait-Event $relayOwned 'SOURCE_CANDIDATE' $all $nonce $watch;Allow-Candidate $candidate $relayOwned $client $nonce $dir;$null=Wait-Event $relayOwned 'SOURCE_ALLOWED' $all $nonce $watch
   $finalReady=Wait-Event $client 'FINAL_READY' $all $nonce $watch
   if(!$finalReady.paused -or !$finalReady.ready -or !$finalReady.connected -or $finalReady.generation -ne 2 -or $finalReady.frontendPort -ne $front2 -or $finalReady.ack.StateHash -ne $finalReady.baselineHash){throw 'Genuine final Paused complete ACK before close declaration'}
   $closeRequest=Wait-Event $client 'FINAL_CLOSE_REQUEST' $all $nonce $watch
   if(!$closeRequest.paused -or !$closeRequest.ready -or !$closeRequest.connected -or $closeRequest.holdMilliseconds -lt 5000 -or $closeRequest.generation -ne 2 -or $closeRequest.frontendPort -ne $front2 -or $closeRequest.hash -ne $finalReady.hash -or $closeRequest.baselineHash -ne $finalReady.baselineHash -or ($closeRequest.ack|ConvertTo-Json -Compress -Depth 10) -ne ($finalReady.ack|ConvertTo-Json -Compress -Depth 10)){throw 'Actual completed5s Ready hold and exact final close request gate'}
   Send-Control $relayOwned $nonce 'DeclareClientClose' @{route=2};$finalDeclaration=Wait-Event $relayOwned 'CLIENT_CLOSE_DECLARED' $all $nonce $watch
   if($finalDeclaration.route -ne 2 -or $finalDeclaration.deadline-$finalDeclaration.declared -ne 5*[Diagnostics.Stopwatch]::Frequency -or $finalDeclaration.declared -lt $closeRequest.timestamp -or [Diagnostics.Stopwatch]::GetTimestamp() -gt $finalDeclaration.deadline){throw 'Actual route2 exact5s close declaration required before authorization'}
   Send-Control $client $nonce 'ALLOW_FINAL_CLOSE' @{route=2;frontendPort=$front2;ackStateHash=$closeRequest.baselineHash;declared=$finalDeclaration.declared;deadline=$finalDeclaration.deadline}
   while(!$hostOwned.process.HasExited -or !$client.process.HasExited){foreach($p in $all){Poll-Owned $p $nonce};if($watch.Elapsed.TotalSeconds -gt 450){throw 'Wrapper450s final deadline'};Start-Sleep -Milliseconds 2}
   Send-Control $relayOwned $nonce 'Stop'
   while(!$relayOwned.process.HasExited){foreach($p in $all){Poll-Owned $p $nonce};if($watch.Elapsed.TotalSeconds -gt 450){throw 'Wrapper450s relay exit'};Start-Sleep -Milliseconds 2}
   foreach($p in $all){$p.process.WaitForExit();Drain-Terminal $p $nonce;if($p.process.ExitCode -ne 0){throw "$($p.role) actual nonzero exit"}}
   $h=Get-Content (Join-Path $dir 'host.json') -Raw|ConvertFrom-Json;$c=Get-Content (Join-Path $dir 'client.json') -Raw|ConvertFrom-Json;$r=Get-Content (Join-Path $dir 'relay.json') -Raw|ConvertFrom-Json
   foreach($tuple in @(@($h,$hostOwned),@($c,$client),@($r,$relayOwned))){if(!$tuple[0].passed -or $tuple[0].nonce -ne $nonce -or $tuple[0].pid -ne $tuple[1].process.Id){throw 'Final report owned PID/nonce/pass mismatch'}}
   if($h.evidence.hash -ne $c.evidence.hash -or $h.evidence.baselineHash -ne $c.evidence.baselineHash -or !$c.evidence.cachedDuplicate -or !$c.evidence.ready -or !$c.evidence.connected -or $c.evidence.generation -ne 2 -or $h.evidence.authorityCreateCount -ne 1 -or $c.evidence.droppedApplicationResponses -ne 0){throw 'Final paired full state/recovery facts'}
   $closeFacts=$c.evidence.finalClose
   if(!$closeFacts.authorized -or $closeFacts.holdMilliseconds -lt 5000 -or $closeFacts.requestedAt -ne $closeRequest.timestamp -or $closeFacts.declaredAt -ne $finalDeclaration.declared -or $closeFacts.deadline -ne $finalDeclaration.deadline -or $closeFacts.declaredAt -lt $closeFacts.requestedAt -or $closeFacts.authorizedAt -lt $closeFacts.declaredAt -or $closeFacts.disposeStartedAt -lt $closeFacts.authorizedAt -or $closeFacts.disposedAt -lt $closeFacts.disposeStartedAt -or $closeFacts.disposedAt -gt $closeFacts.deadline){throw 'Actual request/declaration/authorization/dispose ordering within unchanged5s window'}
   if(($h.evidence.finalCapture|ConvertTo-Json -Compress -Depth 100) -ne ($c.evidence.finalBaseline.State|ConvertTo-Json -Compress -Depth 100)){throw 'Complete capture deep mismatch'}
   if($h.etMvid -ne $c.etMvid -or $h.sessionMvid -ne $c.sessionMvid -or $h.stopwatchFrequency -ne $c.stopwatchFrequency -or $r.stopwatchFrequency -ne $h.stopwatchFrequency){throw 'Binary/timebase mismatch'}
   foreach($participant in $h.evidence.sessionProjection.Participants){if(!$participant.Ready -or !$participant.ConnectedOwnerBinding -or $participant.CleanupPending){throw 'Actual final readiness'}}
   if(!$h.evidence.healthySample -or !$c.evidence.healthySample){
    @{passed=$false;healthySample=$false;recoveryPassed=($h.evidence.recoveryPassed -and $c.evidence.recoveryPassed);repeat=$repeat;nonce=$nonce;hash=$h.evidence.hash;hostSampleError=$h.evidence.sampleError;clientSampleError=$c.evidence.sampleError;exits=@(0,0,0)}|ConvertTo-Json -Depth 20|Set-Content (Join-Path $dir 'paired.json')
    throw 'Unhealthy sampling; subsequent successful recovery does not retroactively pass profile.'
   }
   foreach($load in @($h.evidence.localLoad,$c.evidence.load)){if($load.Offered -ne 300 -or $load.WarmupOffered -ne 50 -or $load.Accepted+$load.SkippedBackpressure+$load.SchedulerSkipped -ne 300 -or $load.Rejected -ne 0 -or $load.Cancelled -ne 0 -or $load.Pending -ne 0){throw 'Actual load accounting'};foreach($id in @($load.IssuedDomainIds)+@($load.WarmupIssuedDomainIds)){if($id -notin $h.evidence.actuallyAdmittedDomainIds){throw 'Issued ID not actually ET admitted'}}}
   if(@($r.routes).Count -ne 2 -or $r.queued -ne 0 -or $r.unverifiedCount -ne 0 -or $r.epoch -ne 2){throw 'Relay final route/drain facts'}
   if($r.controlMode -or $r.holdQueue -or $r.limits.bytes -ne 67108864 -or $r.limits.datagrams -ne 65536){throw 'Numerical profile rejects control-mode or nondefault queue provenance'}
   if(@($r.closeWindows).Count -ne 2){throw 'Both actual close declarations required'}
   $closeNotifications=0;foreach($window in $r.closeWindows){if($window.declared -le 0 -or $window.deadline-$window.declared -ne 5*$r.stopwatchFrequency -or $window.notifications -lt 0 -or $window.notifications -gt 64){throw 'Exact bounded close window provenance'};$closeNotifications+=$window.notifications}
   if($r.handledCloseNotifications -ne $closeNotifications -or $r.socketFaultObservationCount -ne $r.handledCloseNotifications){throw 'Unexpected transport fault cannot pass numerical measurement'}
   foreach($fault in $r.socketFaults){if(!$fault.handledCloseNotification -or $fault.operation -ne 'ReceiveFrom' -or $fault.socket -ne 'frontend' -or $fault.nativeCode -ne 10054 -or $fault.timestamp -gt $fault.deadline -or $null -ne $fault.notificationPayloadBytes){throw 'Transport notification accounting/window mismatch'}}
   for($routeIndex=0;$routeIndex -lt 2;$routeIndex++){
    $actual=$r.routes[$routeIndex];$expected=@($route1,$route2)[$routeIndex]
    if($actual.frontend -ne $expected.frontend -or $actual.upstream -ne $expected.upstream -or $actual.ClientPid -ne $client.process.Id -or $r.backend -ne $expected.backend -or [int]($r.backend.Split(':')[-1]) -ne $h.evidence.backendPort -or $c.evidence.frontendHistory[$routeIndex] -ne [int]($actual.frontend.Split(':')[-1]) -or !$actual.client){throw 'Actual backend/frontend/upstream/client/PID route history mismatch'}
   }
   if(!$r.routes[0].Retired -or $r.routes[1].Retired -or $front1 -eq $front2 -or $front1 -eq $h.evidence.backendPort -or $front2 -eq $h.evidence.backendPort){throw 'Proxy bypass or retired route provenance'}
   foreach($counter in $r.counters){if($counter.values.received -ne $counter.values.forwarded+$counter.values.intentionallyDropped -or $counter.values.receivedBytes -ne $counter.values.forwardedBytes+$counter.values.intentionallyDroppedBytes){throw 'Raw complete count/byte conservation'}}
   $modeledCount=0L;$modeledBytes=0L;foreach($counter in $r.counters){$modeledCount+=$counter.values.received;$modeledBytes+=$counter.values.receivedBytes}
   if($r.overflowDatagrams -ne 0 -or $r.ingressDatagrams -ne $modeledCount+$r.wrongSources -or $r.ingressBytes -ne $modeledBytes+$r.wrongSourceBytes){throw 'Global ingress/verification/rejection count-byte conservation'}
   foreach($direction in @('c2s','s2c')){foreach($routeId in @(1,2)){if(@($r.counters|Where-Object {$_.route -eq $routeId -and $_.direction -eq $direction -and $_.values.forwarded -gt 0}).Count -eq 0){throw 'No actual raw traffic on required route/direction'}}}
   if($Profile -eq 'P0' -and @($r.counters|Where-Object {$_.values.intentionallyDropped -ne 0}).Count){throw 'Control raw drops'}
   $pair=@{passed=$true;repeat=$repeat;nonce=$nonce;serverInstance=$h.evidence.serverInstance;hash=$h.evidence.hash;hostPid=$hostOwned.process.Id;clientPid=$client.process.Id;relayPid=$relayOwned.process.Id;routes=@($route1,$route2);exits=@(0,0,0);directory=$dir};$pair|ConvertTo-Json -Depth 20|Set-Content (Join-Path $dir 'paired.json');$pairs+=$pair
  }finally{foreach($p in $all){if(!$p.process.HasExited){$p.process.Kill();$p.process.WaitForExit()};try{Drain-Terminal $p $nonce}catch{[IO.File]::AppendAllText((Join-Path $dir 'cleanup-errors.log'),$_.ToString()+[Environment]::NewLine)};$p.process.Dispose()}}
 }
 if(@($pairs.serverInstance|Select-Object -Unique).Count -ne 3){throw 'Three fresh actual authority instances required'}
 @{passed=$true;profile=$Profile;provenance=$provenance;pairs=$pairs;physicalTwoPc='NOT_VERIFIED';performanceTarget='UNSET'}|ConvertTo-Json -Depth 30|Set-Content (Join-Path $output 'summary.json')
 Write-Output "PASS $output"
}catch{@{passed=$false;profile=$Profile;provenance=$provenance;pairs=$pairs;failure=$_.ToString()}|ConvertTo-Json -Depth 30|Set-Content (Join-Path $output 'summary.json');throw}
