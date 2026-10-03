[CmdletBinding()]
param(
 [ValidateSet('SameMachine','Host','Client','BuildOnly','ConcurrencyControls')][string]$Mode='SameMachine',
 [ValidateSet('All','manual-paused','automatic-active','unbound-cup','submitted-reply-lost')][string]$Case='All',
 [string]$RemoteIp='127.0.0.1',[string]$BindIp='0.0.0.0',[ValidateRange(0,65535)][int]$Port=0,
 [string]$RunId='',[string]$Source='',[string]$Dirty='',
 [string]$OutputDirectory='local/Logs/cooking-network-rich-recovery',[string]$FrozenManifest='',[switch]$NoBuild,
 [ValidateSet('OFF','ON')][string]$Diagnostics='OFF',[string]$DiagnosticParticipant='',[string]$DiagnosticStable=''
)
$ErrorActionPreference='Stop'
if($Diagnostics -eq 'OFF' -and ($DiagnosticParticipant -ne '' -or $DiagnosticStable -ne '')){throw 'OFF cannot have diagnostic selectors.'}
if($Diagnostics -eq 'ON' -and ($DiagnosticParticipant -notmatch '^[A-Za-z0-9_-]{1,128}$' -or $DiagnosticStable -notmatch '^[A-Za-z0-9_-]{1,128}$')){throw 'ON requires bounded participant/stable selectors.'}
if($Diagnostics -eq 'ON' -and $Mode -eq 'ConcurrencyControls'){throw 'Rich diagnostics do not configure the concurrency executable.'}
function Write-Utf8Json { param([Parameter(ValueFromPipeline=$true)][string]$Text,[string]$LiteralPath) process { [IO.File]::WriteAllText($LiteralPath,$Text+[Environment]::NewLine,(New-Object System.Text.UTF8Encoding($false))) } }
$workspaceRoot=Split-Path -Parent $PSScriptRoot
$project=Join-Path $workspaceRoot 'src/AbilityKit.Game.Cooking.NetworkRichRecoveryAcceptance/AbilityKit.Game.Cooking.NetworkRichRecoveryAcceptance.csproj'
$dll=Join-Path $workspaceRoot 'src/AbilityKit.Game.Cooking.NetworkRichRecoveryAcceptance/bin/Debug/net10.0/AbilityKit.Game.Cooking.NetworkRichRecoveryAcceptance.dll'
$controlProject=Join-Path $workspaceRoot 'src/AbilityKit.Game.Cooking.NetworkConcurrencyAcceptance/AbilityKit.Game.Cooking.NetworkConcurrencyAcceptance.csproj'
$controlDll=Join-Path $workspaceRoot 'src/AbilityKit.Game.Cooking.NetworkConcurrencyAcceptance/bin/Debug/net10.0/AbilityKit.Game.Cooking.NetworkConcurrencyAcceptance.dll'
$verifierProject=Join-Path $workspaceRoot 'src/AbilityKit.Game.Cooking.NetworkPairVerifier/AbilityKit.Game.Cooking.NetworkPairVerifier.csproj'
$verifier=Join-Path $workspaceRoot 'src/AbilityKit.Game.Cooking.NetworkPairVerifier/bin/Debug/net10.0/AbilityKit.Game.Cooking.NetworkPairVerifier.dll'
if (!$RunId) { if ($Mode -in @('Host','Client')) { throw 'Physical endpoints require explicit shared RunId.' }; $RunId=[Guid]::NewGuid().ToString('N') }
if ($Mode -in @('Host','Client') -and ($Case -eq 'All' -or !$Source -or !$Dirty)) { throw 'Separate endpoints require explicit one Case and frozen Source/Dirty.' }
if (!$NoBuild -and !$Source) { $Source=(& git -C $workspaceRoot rev-parse HEAD).Trim() }
if (!$NoBuild -and !$Dirty) { $Dirty=if (@(& git -C $workspaceRoot status --porcelain).Count -eq 0) {'clean'}else{'dirty'} }
$rootDirectory=[IO.Path]::GetFullPath((Join-Path (Join-Path $workspaceRoot $OutputDirectory) ([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fffffff'))))
New-Item -ItemType Directory -Force $rootDirectory | Out-Null
function Q([string]$value) { if ($value.Contains('"')) {throw 'Unsupported double quotation.'}; return '"'+$value+'"' }
function File-Sha([string]$path) { return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
function Write-Patch([string]$path,[object[]]$lines) {
 $text=if($lines.Count -eq 0){''}else{[string]::Join([Environment]::NewLine,[string[]]$lines)+[Environment]::NewLine}
 [IO.File]::WriteAllText($path,$text,(New-Object System.Text.UTF8Encoding($false)))
}
function Relative([string]$root,[string]$path) {
 $prefix=[IO.Path]::GetFullPath($root).TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar
 $absolute=[IO.Path]::GetFullPath($path)
 if(!$absolute.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Resolved file escaped expected root.'}
 return $absolute.Substring($prefix.Length).Replace('\','/')
}
function Inputs {
 $roots=@((Join-Path $workspaceRoot 'src'),(Join-Path $workspaceRoot 'Unity/Packages'))
 $values=@(foreach($root in $roots){Get-ChildItem -LiteralPath $root -File -Recurse|Where-Object{$_.FullName -notmatch '[\\/](bin|obj|Library|Temp)[\\/]' -and $_.Extension -in @('.cs','.csproj','.props','.targets','.json','.asmdef','.config')}})
 $values+=@(Get-ChildItem -LiteralPath $workspaceRoot -File|Where-Object{$_.Extension -in @('.props','.targets','.json','.config','.sln','.slnx')})
 return @($values|Sort-Object FullName -Unique|ForEach-Object{@{relativePath=(Relative $workspaceRoot $_.FullName);sha256=(File-Sha $_.FullName)}})
}
function Binary-Files([string]$root){return @(Get-ChildItem -LiteralPath $root -File -Recurse|Sort-Object FullName|ForEach-Object{@{relativePath=(Relative $root $_.FullName);sha256=(File-Sha $_.FullName)}})}
function Equal-Manifest($left,$right,[string]$label){
 $a=@($left);$b=@($right);if($a.Count -ne $b.Count){throw ('Frozen manifest file count mismatch: '+$label)}
 $map=New-Object 'System.Collections.Generic.Dictionary[string,string]' ([StringComparer]::Ordinal)
 foreach($row in $a){if($map.ContainsKey($row.relativePath)){throw 'Duplicate manifest file.'};$map.Add($row.relativePath,$row.sha256)}
 foreach($row in $b){if(!$map.ContainsKey($row.relativePath) -or $map[$row.relativePath] -cne $row.sha256){throw ('Frozen manifest mismatch: '+$label+' '+$row.relativePath)}}
}
$currentHead=(& git -C $workspaceRoot rev-parse HEAD).Trim()
$binDirectory=Split-Path $dll;$verifierDirectory=Split-Path $verifier
$manifestPath=Join-Path $rootDirectory 'frozen-manifest.json'
if ($NoBuild) {
 if(!$FrozenManifest -or !(Test-Path -LiteralPath $FrozenManifest)){throw 'NoBuild requires original immutable successful compile manifest; labels cannot attest old DLLs.'}
 $original=Get-Content -LiteralPath $FrozenManifest -Raw|ConvertFrom-Json
 if(!$Source){$Source=$original.sourceHead};if(!$Dirty){$Dirty=$original.dirty}
 if($original.schema -ne 2 -or !$original.buildSucceeded){throw 'Original successful compile manifest required.'}
 if($Source -ne $original.sourceHead -or $Dirty -ne $original.dirty){throw 'Requested frozen source labels differ from actual original compile provenance.'}
 Equal-Manifest $original.sourceInputs (Inputs) 'ALL source/config/dependency input files'
 Equal-Manifest $original.files (Binary-Files $binDirectory) 'complete runner binaries/config/helpers'
 Equal-Manifest $original.verifierFiles (Binary-Files $verifierDirectory) 'complete verifier binaries/config/dependencies'
 if($Mode -eq 'ConcurrencyControls'){Equal-Manifest $original.controlFiles (Binary-Files (Split-Path $controlDll)) 'complete controls frozen binary set'}
 if($original.runnerScriptSha256 -ne (File-Sha $PSCommandPath)){throw 'Original runner script changed.'}
 if($original.comparatorScriptSha256 -ne (File-Sha (Join-Path $PSScriptRoot 'compare-cooking-network-pair.ps1'))){throw 'Original comparator script changed.'}
 Copy-Item -LiteralPath $FrozenManifest -Destination $manifestPath
} else {
 if($Source -ne $currentHead){throw 'Build Source must equal actual compile HEAD.'}
 $beforeInputs=Inputs
 function Build-Project([string]$target,[string]$label) {
  $process=Start-Process dotnet -ArgumentList ('build '+(Q $target)+' --no-incremental --verbosity minimal') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $rootDirectory ($label+'-build.log')) -RedirectStandardError (Join-Path $rootDirectory ($label+'-build.stderr.log'))
  $null=$process.Handle; $start=$process.StartTime; $until=[DateTime]::UtcNow.AddSeconds(180)
  try{while(!$process.HasExited -and [DateTime]::UtcNow -lt $until){Start-Sleep -Milliseconds 100;$process.Refresh()};if(!$process.HasExited){throw 'Build180s deadline.'};$process.WaitForExit();if($process.ExitCode -ne 0){throw ($label+' build failed.')}}
  finally{$process.Refresh();if(!$process.HasExited){$current=Get-Process -Id $process.Id -ErrorAction SilentlyContinue;if($current -and $current.StartTime -eq $start){Stop-Process -InputObject $current};$process.WaitForExit()};@{pid=$process.Id;startUtc=$start.ToUniversalTime().ToString('O');exitCode=$process.ExitCode;project=$target}|ConvertTo-Json|Write-Utf8Json -LiteralPath (Join-Path $rootDirectory ($label+'-build.exit.json'));$process.Dispose()}
 }
 $patchPath=Join-Path $rootDirectory 'tracked-source.patch'
 $patchLines=@(& git -C $workspaceRoot diff --binary HEAD)
 $patchExit=$LASTEXITCODE
 if($patchExit -ne 0){throw ('Actual git diff failed: '+$patchExit)}
 Write-Patch $patchPath $patchLines
 Build-Project $project 'runner'
 Build-Project $controlProject 'controls'
 Build-Project $verifierProject 'verifier'
 Equal-Manifest $beforeInputs (Inputs) 'compile before/after ALL actual dependency inputs'
 if($currentHead -ne (& git -C $workspaceRoot rev-parse HEAD).Trim()){throw 'HEAD changed while compiling.'}
 @{schema=2;buildSucceeded=$true;sourceHead=$currentHead;dirty=$Dirty;savedPatchSha256=(File-Sha $patchPath);runnerScriptSha256=(File-Sha $PSCommandPath);comparatorScriptSha256=(File-Sha (Join-Path $PSScriptRoot 'compare-cooking-network-pair.ps1'));sourceInputs=$beforeInputs;files=(Binary-Files $binDirectory);verifierFiles=(Binary-Files $verifierDirectory);controlFiles=(Binary-Files (Split-Path $controlDll));buildReceipts=@('runner','controls','verifier'|ForEach-Object{Get-Content -LiteralPath (Join-Path $rootDirectory ($_+'-build.exit.json')) -Raw|ConvertFrom-Json});buildUtc=[DateTime]::UtcNow.ToString('O')}|ConvertTo-Json -Depth 12|Write-Utf8Json -LiteralPath $manifestPath
}
if (!(Test-Path -LiteralPath $dll) -or !(Test-Path -LiteralPath $verifier)){throw 'Frozen runner/verifier binaries missing.'}
@{currentHead=$currentHead;frozenManifestSha256=(File-Sha $manifestPath);frozenSourceHead=$Source;diagnostics=$Diagnostics;diagnosticParticipant=$DiagnosticParticipant;diagnosticStable=$DiagnosticStable}|ConvertTo-Json|Write-Utf8Json -LiteralPath (Join-Path $rootDirectory 'invocation-provenance.json')
if ($Mode -eq 'BuildOnly') { Write-Output "BUILD ONLY frozen artifacts $rootDirectory"; exit 0 }
$cases=if($Mode -eq 'ConcurrencyControls'){@('remote-first','local-first')}elseif ($Case -eq 'All') {@('manual-paused','automatic-active','unbound-cup','submitted-reply-lost')}else{@($Case)}
$pairSeconds=if($Mode -eq 'ConcurrencyControls'){210}else{630}
$aggregateDeadline=[DateTime]::UtcNow.AddSeconds($pairSeconds*$cases.Count)
$instances=@()
foreach ($selected in $cases) {
 $runDirectory=Join-Path $rootDirectory $selected; New-Item -ItemType Directory -Force $runDirectory | Out-Null
 Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $runDirectory 'frozen-manifest.json')
 $owned=@();$pairDeadline=[DateTime]::UtcNow.AddSeconds($pairSeconds)
 function Endpoint([string]$role,[string]$ip,[int]$endpointPort,[string]$topology) {
  $effectiveDll=if($Mode -eq 'ConcurrencyControls'){$controlDll}else{$dll}
  $arguments=(Q $effectiveDll)+' '+$role+' --case '+$selected+' --run-id '+(Q $RunId)+' --ip '+(Q $ip)+' --port '+$endpointPort+' --source '+(Q $Source)+' --dirty '+(Q $Dirty)+' --topology '+$topology+' --report '+(Q (Join-Path $runDirectory ($role+'.json')))
  if($Mode -eq 'ConcurrencyControls'){$arguments+=' --order '+$selected}
  else{$arguments+=' --diagnostics '+$Diagnostics;if($Diagnostics -eq 'ON'){$arguments+=' --diagnostic-participant '+(Q $DiagnosticParticipant)+' --diagnostic-stable '+(Q $DiagnosticStable)}}
  $process=Start-Process dotnet -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runDirectory ($role+'.stdout.log')) -RedirectStandardError (Join-Path $runDirectory ($role+'.stderr.log'))
  $null=$process.Handle; return $process
 }
 function Receipt($process,[string]$role) {
  $process.Refresh();$process.WaitForExit()
  @{schema=1;role=$role;runId=$RunId;caseId=$selected;pid=$process.Id;machine=[Environment]::MachineName;startUtc=$process.StartTime.ToUniversalTime().ToString('O');exitCode=$process.ExitCode;executableSha256=(File-Sha $(if($Mode -eq 'ConcurrencyControls'){$controlDll}else{$dll}));reportSha256=if(Test-Path -LiteralPath (Join-Path $runDirectory ($role+'.json'))){File-Sha (Join-Path $runDirectory ($role+'.json'))}else{$null}} | ConvertTo-Json | Write-Utf8Json -LiteralPath (Join-Path $runDirectory ($role+'.exit.json'))
 }
 try {
  if ($Mode -eq 'Client' -and $Port -eq 0) { throw 'Client requires actual Host READY port.' }
  $hostProcess=$null;$clientProcess=$null
  if ($Mode -ne 'Client') {$hostProcess=Endpoint 'host' $(if($Mode -in @('SameMachine','ConcurrencyControls')){'127.0.0.1'}else{$BindIp}) $Port $(if($Mode -in @('SameMachine','ConcurrencyControls')){'SameMachineIndependentProcessesUdp'}else{'SeparateHostsRequiresPairedEvidence'});$owned+=@(@{process=$hostProcess;role='host';start=$hostProcess.StartTime})}
  if ($Mode -in @('SameMachine','ConcurrencyControls')) {
   $readyDeadline=[DateTime]::UtcNow.AddSeconds(20);$readyPort=0
   while ([DateTime]::UtcNow -lt $readyDeadline) {
    $hostProcess.Refresh();if($hostProcess.HasExited){throw 'Host exited before READY.'}
    $stdout=Join-Path $runDirectory 'host.stdout.log'
    if(Test-Path -LiteralPath $stdout){$text=Get-Content -LiteralPath $stdout -Raw;if($text -match '(?m)^READY ([0-9]+) ([0-9]+)\s*$'){if([int]$Matches[2] -ne $hostProcess.Id){throw 'Host READY PID mismatch.'};$readyPort=[int]$Matches[1];break}}
    Start-Sleep -Milliseconds 100
   }
   if($readyPort -le 0){throw 'Actual READY20s deadline.'}
   $clientProcess=Endpoint 'client' '127.0.0.1' $readyPort 'SameMachineIndependentProcessesUdp';$owned+=@(@{process=$clientProcess;role='client';start=$clientProcess.StartTime})
  } elseif ($Mode -eq 'Client') {$clientProcess=Endpoint 'client' $RemoteIp $Port 'SeparateHostsRequiresPairedEvidence';$owned+=@(@{process=$clientProcess;role='client';start=$clientProcess.StartTime})}
  $announced=$false
  while([DateTime]::UtcNow -lt $pairDeadline -and [DateTime]::UtcNow -lt $aggregateDeadline){
   foreach($entry in $owned){$entry.process.Refresh()}
   if($Mode -eq 'Host' -and !$announced -and (Test-Path -LiteralPath (Join-Path $runDirectory 'host.stdout.log'))){$text=Get-Content -LiteralPath (Join-Path $runDirectory 'host.stdout.log') -Raw;if($text -match '(?m)^READY ([0-9]+) ([0-9]+)\s*$'){Write-Output $Matches[0];$announced=$true}}
   if(@($owned|Where-Object{!$_.process.HasExited}).Count -eq 0){break};Start-Sleep -Milliseconds 100
  }
  if(@($owned|Where-Object{!$_.process.HasExited}).Count -ne 0){throw 'Real pair630s / aggregate2520s deadline.'}
  foreach($entry in $owned){Receipt $entry.process $entry.role;if($entry.process.ExitCode -ne 0){throw 'Actual endpoint failed; preserved complete artifacts.'}}
  if($Mode -ne 'ConcurrencyControls'){
   foreach($entry in $owned){
    $endpointReport=Get-Content -LiteralPath (Join-Path $runDirectory ($entry.role+'.json')) -Raw|ConvertFrom-Json
    $effective=$endpointReport.diagnosticOptions
    if($null -eq $effective -or $effective.enabled -ne ($Diagnostics -eq 'ON')){throw 'Compiled endpoint diagnostic option missing/mismatched.'}
    if($Diagnostics -eq 'ON' -and ($effective.participant -ne $DiagnosticParticipant -or $effective.stable -ne $DiagnosticStable -or $null -eq $endpointReport.commandPathDiagnostics)){throw 'Endpoint selector/diagnostic provenance mismatch.'}
    if($Diagnostics -eq 'OFF' -and $null -ne $endpointReport.commandPathDiagnostics){throw 'OFF endpoint collected diagnostics.'}
   }
  }
  if($Mode -in @('SameMachine','ConcurrencyControls')){
   & dotnet $verifier --suite $(if($Mode -eq 'ConcurrencyControls'){'concurrency21'}else{'rich-recovery4-single'}) --host (Join-Path $runDirectory 'host.json') --client (Join-Path $runDirectory 'client.json') --host-exit (Join-Path $runDirectory 'host.exit.json') --client-exit (Join-Path $runDirectory 'client.exit.json') --manifest (Join-Path $runDirectory 'frozen-manifest.json') --run-id $RunId --case $selected --output (Join-Path (Join-Path $rootDirectory 'pairings') ($selected+'.json')) *> (Join-Path $runDirectory 'verifier.log')
   if($LASTEXITCODE -ne 0){throw 'Deep pairing failed/insufficient; original artifacts preserved.'}
   $pair=Get-Content -LiteralPath (Join-Path (Join-Path $rootDirectory 'pairings') ($selected+'.json')) -Raw|ConvertFrom-Json;$instances+=@($pair.serverInstance)
  }
  Write-Output "Completed $selected artifacts $runDirectory; physical LAN NOT_VERIFIED."
 } catch {
  @{passed=$false;runId=$RunId;caseId=$selected;failure=$_.Exception.ToString();physicalTwoPc='NOT_VERIFIED'} | ConvertTo-Json | Write-Utf8Json -LiteralPath (Join-Path $runDirectory 'wrapper-failure.json')
  throw
 } finally {
  foreach($entry in $owned){$process=$entry.process;$process.Refresh();if(!$process.HasExited){$current=Get-Process -Id $process.Id -ErrorAction SilentlyContinue;if($null -ne $current -and $current.StartTime -eq $entry.start){Stop-Process -InputObject $current};$process.WaitForExit()};if(!(Test-Path -LiteralPath (Join-Path $runDirectory ($entry.role+'.exit.json')))){Receipt $process $entry.role};$process.Dispose()}
 }
}
if($Mode -eq 'SameMachine' -and $Case -eq 'All'){
 if(@($instances|Select-Object -Unique).Count -ne 4){throw 'Four fresh distinct authority instances required.'}
 @{passed=$true;runId=$RunId;cases=$cases;instances=$instances;physicalTwoPc='NOT_VERIFIED';formalPerformanceTarget='UNSET'}|ConvertTo-Json -Depth 8|Write-Utf8Json -LiteralPath (Join-Path $rootDirectory 'four-case-paired.json')
}
