param(
 [Parameter(Mandatory=$true)][ValidateSet('concurrency21','rich-service','rich-recovery4-single','rich-recovery4')][string]$Suite,
 [Parameter(Mandatory=$true)][string]$HostDirectory,[Parameter(Mandatory=$true)][string]$ClientDirectory,
 [Parameter(Mandatory=$true)][string]$FrozenManifest,[Parameter(Mandatory=$true)][string]$RunId,
 [string]$Case='', [string]$PhysicalAttestation='', [string]$OutputPath='pair-verification.json'
)
$ErrorActionPreference='Stop'
$workspaceRoot=Split-Path -Parent $PSScriptRoot
$directory=Join-Path $workspaceRoot 'src/AbilityKit.Game.Cooking.NetworkPairVerifier/bin/Debug/net10.0'
$dll=Join-Path $directory 'AbilityKit.Game.Cooking.NetworkPairVerifier.dll'
if(!(Test-Path -LiteralPath $dll)){throw 'Verifier binary missing; run approved rich wrapper BuildOnly first. Comparator never compiles stale inputs implicitly.'}
$manifest=Get-Content -LiteralPath $FrozenManifest -Raw|ConvertFrom-Json
if($manifest.schema -ne 2 -or !$manifest.buildSucceeded){throw 'Original successful compile manifest required.'}
if($manifest.comparatorScriptSha256 -ne (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash){throw 'Original compiled companion comparator version changed.'}
function Relative([string]$root,[string]$path){$prefix=[IO.Path]::GetFullPath($root).TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar;$absolute=[IO.Path]::GetFullPath($path);if(!$absolute.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Resolved path escaped root.'};return $absolute.Substring($prefix.Length).Replace('\','/')}
$files=@(Get-ChildItem -LiteralPath $directory -File -Recurse|Sort-Object FullName|ForEach-Object{@{relativePath=(Relative $directory $_.FullName);sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
function Equal-Manifest($left,$right,[string]$label){
 $a=@($left);$b=@($right);if($a.Count -ne $b.Count){throw ('Frozen manifest file count mismatch: '+$label)}
 $map=New-Object 'System.Collections.Generic.Dictionary[string,string]' ([StringComparer]::Ordinal)
 foreach($row in $a){if($map.ContainsKey($row.relativePath)){throw 'Duplicate manifest file.'};$map.Add($row.relativePath,$row.sha256)}
 foreach($row in $b){if(!$map.ContainsKey($row.relativePath) -or $map[$row.relativePath] -cne $row.sha256){throw ('Frozen manifest mismatch: '+$label+' '+$row.relativePath)}}
}
Equal-Manifest $manifest.verifierFiles $files 'complete verifier binaries'
$roots=@((Join-Path $workspaceRoot 'src'),(Join-Path $workspaceRoot 'Unity/Packages'))
$inputs=@(foreach($root in $roots){Get-ChildItem -LiteralPath $root -File -Recurse|Where-Object{$_.FullName -notmatch '[\\/](bin|obj|Library|Temp)[\\/]' -and $_.Extension -in @('.cs','.csproj','.props','.targets','.json','.asmdef','.config')}})
$inputs+=@(Get-ChildItem -LiteralPath $workspaceRoot -File|Where-Object{$_.Extension -in @('.props','.targets','.json','.config','.sln','.slnx')})
$current=@($inputs|Sort-Object FullName -Unique|ForEach-Object{@{relativePath=(Relative $workspaceRoot $_.FullName);sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
Equal-Manifest $manifest.sourceInputs $current 'complete actual verifier/application dependency inputs'
$cases=if($Suite -eq 'rich-recovery4'){@('manual-paused','automatic-active','unbound-cup','submitted-reply-lost')}else{@($Case)}
$verified=@();$instances=@()
foreach($selected in $cases){
 $hostRoot=if($Suite -eq 'rich-recovery4'){Join-Path $HostDirectory $selected}else{$HostDirectory}
 $clientRoot=if($Suite -eq 'rich-recovery4'){Join-Path $ClientDirectory $selected}else{$ClientDirectory}
 $singleOutput=if($Suite -eq 'rich-recovery4'){Join-Path ([IO.Path]::GetFullPath($OutputPath)+'.cases') ($selected+'.json')}else{$OutputPath}
 $selectedSuite=if($Suite -eq 'rich-recovery4'){'rich-recovery4-single'}else{$Suite}
 $arguments=@($dll,'--suite',$selectedSuite,'--host',(Join-Path $hostRoot 'host.json'),'--client',(Join-Path $clientRoot 'client.json'),'--host-exit',(Join-Path $hostRoot 'host.exit.json'),'--client-exit',(Join-Path $clientRoot 'client.exit.json'),'--manifest',$FrozenManifest,'--run-id',$RunId,'--case',$selected,'--output',$singleOutput)
 if($PhysicalAttestation){$arguments+=@('--attestation',$PhysicalAttestation)}
 & dotnet @arguments
 if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
 $proof=Get-Content -LiteralPath $singleOutput -Raw|ConvertFrom-Json
 if(!$proof.passed){throw 'Missing genuine deep verifier pass.'}
 $verified+=@($proof);$instances+=@($proof.serverInstance)
}
if($Suite -eq 'rich-recovery4'){
 if(@($instances|Select-Object -Unique).Count -ne 4){throw 'Aggregate requires four actual distinct authority instances.'}
 $physical=if(@($verified|Where-Object{$_.physicalTwoPc -ne 'TwoPhysicalPcAttestedPairedEvidence'}).Count -eq 0){'TwoPhysicalPcAttestedPairedEvidence'}else{'NOT_VERIFIED'}
 @{passed=$true;suite=$Suite;runId=$RunId;cases=$cases;instances=$instances;physicalTwoPc=$physical;formalPerformanceTarget='UNSET';proofs=$verified}|ConvertTo-Json -Depth 16|Set-Content -LiteralPath $OutputPath -Encoding UTF8
}
exit 0
