param(
    [ValidateSet('InProcess','SameMachineUdp')][string]$Topology = 'InProcess',
    [ValidateSet('Debug','Release')][string]$Configuration = 'Debug',
    [ValidateSet(2,4)][int]$Participants = 2,
    [ValidateSet(5,20,50)][int]$OfferedRate = 5,
    [switch]$ControlOnly,
    [switch]$NoBuild,
    [string]$OutputDirectory = 'local/Logs/cooking-network-measurement'
)
$ErrorActionPreference = 'Stop'
$measurementRoot = Split-Path -Parent $PSScriptRoot
$measurementProject = Join-Path $measurementRoot 'src/AbilityKit.Game.Cooking.NetworkMeasurement/AbilityKit.Game.Cooking.NetworkMeasurement.csproj'
$measurementBuildDirectory = Join-Path $measurementRoot ('src/AbilityKit.Game.Cooking.NetworkMeasurement/bin/' + $Configuration + '/net10.0')
$measurementDll = Join-Path $measurementBuildDirectory 'AbilityKit.Game.Cooking.NetworkMeasurement.dll'
$measurementBuildProvenance = Join-Path $measurementBuildDirectory 'measurement-build-provenance.json'
$measurementStamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fffffff')
$measurementDirectory = [IO.Path]::GetFullPath((Join-Path (Join-Path $measurementRoot $OutputDirectory) $measurementStamp))
New-Item -ItemType Directory -Path $measurementDirectory -Force | Out-Null
& git -C $measurementRoot rev-parse HEAD | Set-Content (Join-Path $measurementDirectory 'source-commit.txt')
& git -C $measurementRoot status --short | Set-Content (Join-Path $measurementDirectory 'source-status.txt')
Set-Content -LiteralPath (Join-Path $measurementDirectory 'build-configuration.txt') -Value $Configuration
& dotnet --info *> (Join-Path $measurementDirectory 'sdk-info.txt')
if (!$NoBuild) {
    & dotnet build $measurementProject --configuration $Configuration --verbosity minimal *> (Join-Path $measurementDirectory 'build.log')
    if ($LASTEXITCODE -ne 0) { throw "Build failed; inspect $measurementDirectory/build.log" }
    $measurementBuiltSources = @(); foreach ($measurementBuiltSource in @('src/AbilityKit.Game.Cooking.NetworkMeasurement/Program.cs','src/AbilityKit.Game.Cooking.NetworkMeasurement/MeasurementFixture.cs','src/AbilityKit.Game.Cooking.NetworkMeasurement/MeasurementProfile.cs','src/AbilityKit.Game.Cooking.NetworkMeasurement/AbilityKit.Game.Cooking.NetworkMeasurement.csproj','src/AbilityKit.Game.Cooking.NetworkAcceptance/SingleThreadOwner.cs')) { $measurementBuiltSources += [pscustomobject]@{ sourcePath=$measurementBuiltSource; sha256=(Get-FileHash -LiteralPath (Join-Path $measurementRoot $measurementBuiltSource) -Algorithm SHA256).Hash } }
    [pscustomobject]@{ configuration=$Configuration; sourceHead=(& git -C $measurementRoot rev-parse HEAD); sourceStatus=(& git -C $measurementRoot status --short); sourceHashes=$measurementBuiltSources; builtUtc=[DateTime]::UtcNow.ToString('o'); executableSha256=(Get-FileHash -LiteralPath $measurementDll -Algorithm SHA256).Hash } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $measurementBuildProvenance
}
if (!(Test-Path -LiteralPath $measurementDll)) { throw 'Measurement executable absent.' }
Get-FileHash -LiteralPath $measurementDll -Algorithm SHA256 | ConvertTo-Json | Set-Content (Join-Path $measurementDirectory 'executable-hash.json')
if (Test-Path -LiteralPath $measurementBuildProvenance) { Copy-Item -LiteralPath $measurementBuildProvenance -Destination (Join-Path $measurementDirectory 'build-provenance.json') }
$measurementSources = @('tools/run-cooking-network-measurement.ps1','tools/evaluate-cooking-network-reference.py','src/AbilityKit.Game.Cooking.NetworkMeasurement/Program.cs','src/AbilityKit.Game.Cooking.NetworkMeasurement/MeasurementFixture.cs','src/AbilityKit.Game.Cooking.NetworkMeasurement/MeasurementProfile.cs','src/AbilityKit.Game.Cooking.NetworkMeasurement/AbilityKit.Game.Cooking.NetworkMeasurement.csproj','src/AbilityKit.Game.Cooking.NetworkAcceptance/SingleThreadOwner.cs')
$measurementSourceDirectory = Join-Path $measurementDirectory 'sources'
$measurementBinaryDirectory = Join-Path $measurementDirectory 'binaries'
New-Item -ItemType Directory -Path $measurementSourceDirectory,$measurementBinaryDirectory -Force | Out-Null
$measurementSourceProof = @(); $measurementBinaryProof = @()
foreach ($measurementSource in $measurementSources) {
    $measurementSourceAbsolute = Join-Path $measurementRoot $measurementSource
    $measurementSourceName = $measurementSource.Replace('/','_')
    Copy-Item -LiteralPath $measurementSourceAbsolute -Destination (Join-Path $measurementSourceDirectory $measurementSourceName)
    $measurementSourceProof += [pscustomobject]@{ sourcePath=$measurementSource; artifactPath=('sources/'+$measurementSourceName); sha256=(Get-FileHash -LiteralPath $measurementSourceAbsolute -Algorithm SHA256).Hash }
}
foreach ($measurementBinary in (Get-ChildItem -LiteralPath $measurementBuildDirectory -File | Where-Object { $_.Extension -in @('.dll','.json') -and $_.Name -ne 'measurement-build-provenance.json' })) {
    Copy-Item -LiteralPath $measurementBinary.FullName -Destination (Join-Path $measurementBinaryDirectory $measurementBinary.Name)
    $measurementBinaryProof += [pscustomobject]@{ name=$measurementBinary.Name; artifactPath=('binaries/'+$measurementBinary.Name); sha256=(Get-FileHash -LiteralPath $measurementBinary.FullName -Algorithm SHA256).Hash }
}
[pscustomobject]@{ configuration=$Configuration; invocationNoBuild=[bool]$NoBuild; sourceHead=(& git -C $measurementRoot rev-parse HEAD); sources=$measurementSourceProof; binaries=$measurementBinaryProof } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $measurementDirectory 'tool-provenance.json')
# Read-only inventory immediately before measurement; no power/GC/process settings change.
$measurementInventoryErrors = @(); $measurementCpu=$null; $measurementOs=$null; $measurementComputer=$null; $measurementPower=$null; $measurementProcesses=@()
try { $measurementCpu = @(Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores,NumberOfLogicalProcessors,AddressWidth) } catch { $measurementInventoryErrors += $_.Exception.Message }
try { $measurementOs = Get-CimInstance Win32_OperatingSystem | Select-Object Caption,Version,BuildNumber,OSArchitecture } catch { $measurementInventoryErrors += $_.Exception.Message }
try { $measurementComputer = Get-CimInstance Win32_ComputerSystem | Select-Object Manufacturer,Model,TotalPhysicalMemory } catch { $measurementInventoryErrors += $_.Exception.Message }
try { $measurementPowerRaw = (& powercfg /getactivescheme | Out-String).Trim(); if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect active power scheme.' }; $measurementPower = [pscustomobject]@{ raw=$measurementPowerRaw; guid=([regex]::Match($measurementPowerRaw,'[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}')).Value } } catch { $measurementInventoryErrors += $_.Exception.Message }
try { $measurementProcesses = @(Get-Process | Sort-Object WorkingSet64 -Descending | Select-Object -First 20 Id,ProcessName,CPU,WorkingSet64); $measurementManagedProcesses = @(Get-CimInstance Win32_Process -Filter "name='dotnet.exe' or name='testhost.exe'" | Select-Object ProcessId,CommandLine) } catch { $measurementInventoryErrors += $_.Exception.Message }
$measurementGcEnvironment=@{}; foreach ($measurementEnvironment in (Get-ChildItem Env: | Where-Object { $_.Name -match '^(DOTNET|COMPlus)_.*(GC|Tiered|ReadyToRun)' })) { $measurementGcEnvironment[$measurementEnvironment.Name]=$measurementEnvironment.Value }
[pscustomobject]@{ capturedUtc=[DateTime]::UtcNow.ToString('o'); machine=$env:COMPUTERNAME; referenceSelection='local-amd9800x3d-32gb-win11-balanced'; cpu=$measurementCpu; os=$measurementOs; computer=$measurementComputer; power=$measurementPower; topProcesses=$measurementProcesses; managedProcesses=$measurementManagedProcesses; processInventoryDefinition='Top20 working-set snapshot; CPU cumulative process seconds, not a sampled CPU utilization. Contention is recorded, not automatically declared absent.'; gcEnvironment=$measurementGcEnvironment; gcModeEvidence='Runtime default/config/environment recorded; no live GC mode inferred by inventory.'; errors=$measurementInventoryErrors } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $measurementDirectory 'environment-inventory.json')
$measurementReport = Join-Path $measurementDirectory 'measurement.json'
$measurementArguments = @($measurementDll, $measurementReport, $Topology, '--participants', $Participants, '--offered-rate', $OfferedRate)
if ($ControlOnly) { $measurementArguments += '--control-only' }
# Foreground console owns bounded run deadlines and deterministic ET owner cleanup.
# No external process enumeration, global termination, or receipt pruning.
$measurementStartedUtc=[DateTime]::UtcNow.ToString('o')
& dotnet @measurementArguments *> (Join-Path $measurementDirectory 'run.log')
$measurementExit = $LASTEXITCODE
[pscustomobject]@{ launched=$true; exitCode=$measurementExit; startedUtc=$measurementStartedUtc; completedUtc=[DateTime]::UtcNow.ToString('o'); configuration=$Configuration; executableSha256=(Get-FileHash -LiteralPath $measurementDll -Algorithm SHA256).Hash; arguments=$measurementArguments; mode='Foreground native invocation; exitCode is actual LASTEXITCODE, runtime PID comes from measurement.json separately.' } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $measurementDirectory 'process-exit.json')
if (!(Test-Path -LiteralPath $measurementReport)) { throw "No report; inspect $measurementDirectory/run.log" }
$measurementResult = Get-Content -LiteralPath $measurementReport -Raw | ConvertFrom-Json
if ($measurementExit -ne 0 -or !$measurementResult.passed) { throw "Measurement failed; inspect $measurementReport" }
if ($measurementResult.participantCount -ne $Participants -or $measurementResult.offeredRate -ne $OfferedRate) { throw 'Requested profile was not executed.' }
if (!$ControlOnly -and @($measurementResult.reports | Where-Object kind -eq 'BaselineMeasurement').Count -ne 3) { throw 'Three fresh repeats were not recorded.' }
Write-Output "PASS bounded correctness/measurement path $Topology configuration=$Configuration participants=$Participants offeredRate=$OfferedRate; performance threshold UNSET; physical LAN NOT_VERIFIED."
Write-Output "Artifacts: $measurementDirectory"
