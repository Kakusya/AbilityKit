param(
    [ValidateSet('InProcess','SameMachineUdp')][string]$Topology = 'InProcess',
    [ValidateSet(2,4)][int]$Participants = 2,
    [ValidateSet(5,20,50)][int]$OfferedRate = 5,
    [switch]$ControlOnly,
    [switch]$NoBuild,
    [string]$OutputDirectory = 'local/Logs/cooking-network-measurement'
)
$ErrorActionPreference = 'Stop'
$measurementRoot = Split-Path -Parent $PSScriptRoot
$measurementProject = Join-Path $measurementRoot 'src/AbilityKit.Game.Cooking.NetworkMeasurement/AbilityKit.Game.Cooking.NetworkMeasurement.csproj'
$measurementDll = Join-Path $measurementRoot 'src/AbilityKit.Game.Cooking.NetworkMeasurement/bin/Debug/net10.0/AbilityKit.Game.Cooking.NetworkMeasurement.dll'
$measurementStamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fffffff')
$measurementDirectory = [IO.Path]::GetFullPath((Join-Path (Join-Path $measurementRoot $OutputDirectory) $measurementStamp))
New-Item -ItemType Directory -Path $measurementDirectory -Force | Out-Null
& git -C $measurementRoot rev-parse HEAD | Set-Content (Join-Path $measurementDirectory 'source-commit.txt')
& git -C $measurementRoot status --short | Set-Content (Join-Path $measurementDirectory 'source-status.txt')
& dotnet --info *> (Join-Path $measurementDirectory 'sdk-info.txt')
if (!$NoBuild) {
    & dotnet build $measurementProject --verbosity minimal *> (Join-Path $measurementDirectory 'build.log')
    if ($LASTEXITCODE -ne 0) { throw "Build failed; inspect $measurementDirectory/build.log" }
}
if (!(Test-Path -LiteralPath $measurementDll)) { throw 'Measurement executable absent.' }
Get-FileHash -LiteralPath $measurementDll -Algorithm SHA256 | ConvertTo-Json | Set-Content (Join-Path $measurementDirectory 'executable-hash.json')
$measurementReport = Join-Path $measurementDirectory 'measurement.json'
$measurementArguments = @($measurementDll, $measurementReport, $Topology, '--participants', $Participants, '--offered-rate', $OfferedRate)
if ($ControlOnly) { $measurementArguments += '--control-only' }
# Foreground console owns bounded run deadlines and deterministic ET owner cleanup.
# No external process enumeration, global termination, or receipt pruning.
& dotnet @measurementArguments *> (Join-Path $measurementDirectory 'run.log')
$measurementExit = $LASTEXITCODE
if (!(Test-Path -LiteralPath $measurementReport)) { throw "No report; inspect $measurementDirectory/run.log" }
$measurementResult = Get-Content -LiteralPath $measurementReport -Raw | ConvertFrom-Json
if ($measurementExit -ne 0 -or !$measurementResult.passed) { throw "Measurement failed; inspect $measurementReport" }
if ($measurementResult.participantCount -ne $Participants -or $measurementResult.offeredRate -ne $OfferedRate) { throw 'Requested profile was not executed.' }
if (!$ControlOnly -and @($measurementResult.reports | Where-Object kind -eq 'BaselineMeasurement').Count -ne 3) { throw 'Three fresh repeats were not recorded.' }
Write-Output "PASS bounded correctness/measurement path $Topology participants=$Participants offeredRate=$OfferedRate; performance threshold UNSET; physical LAN NOT_VERIFIED."
Write-Output "Artifacts: $measurementDirectory"
