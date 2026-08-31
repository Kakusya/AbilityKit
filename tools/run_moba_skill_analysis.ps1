[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('lianpo-skill1-dash', 'lianpo-skill2-area', 'lianpo-skill3-combo', 'xiaoqiao-skill1-projectile', 'xiaoqiao-skill2-area', 'xiaoqiao-skill3-ultimate')]
    [string]$ScenarioId,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [string]$UnityEditorPath = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$unityProject = Join-Path $repoRoot 'Unity'
$outputPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
$artifactRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))

if (-not $outputPath.StartsWith($artifactRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must be under artifacts/: $OutputDirectory"
}
if (-not (Test-Path -LiteralPath $UnityEditorPath -PathType Leaf)) {
    throw "Unity editor not found: $UnityEditorPath"
}
if (-not (Test-Path -LiteralPath $unityProject -PathType Container)) {
    throw "Unity project not found: $unityProject"
}

New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
$logPath = Join-Path $outputPath 'unity.log'
$arguments = @(
    '-batchmode',
    '-nographics',
    '-projectPath', $unityProject,
    '-executeMethod', 'AbilityKit.Game.Test.UnitTest.MobaAcceptanceWebCommand.RunFromCommandLine',
    '-mobaAcceptanceScenario', $ScenarioId,
    '-mobaAcceptanceOutput', $outputPath,
    '-logFile', $logPath
)

$startedAt = [DateTime]::UtcNow
$process = Start-Process -FilePath $UnityEditorPath -ArgumentList $arguments -Wait -PassThru
$endedAt = [DateTime]::UtcNow
$summaryFiles = @(Get-ChildItem -LiteralPath $outputPath -Filter '*_summary.json' -File -ErrorAction SilentlyContinue)
$traceFiles = @(Get-ChildItem -LiteralPath $outputPath -Filter '*_trace.jsonl' -File -ErrorAction SilentlyContinue)
$result = [ordered]@{
    scenarioId = $ScenarioId
    executionMode = 'unity-execute-method'
    status = if ($process.ExitCode -eq 0 -and $summaryFiles.Count -eq 1 -and $traceFiles.Count -eq 1) { 'passed' } else { 'failed' }
    exitCode = $process.ExitCode
    startedAtUtc = $startedAt.ToString('O')
    endedAtUtc = $endedAt.ToString('O')
    durationMs = [int]($endedAt - $startedAt).TotalMilliseconds
    logPath = $logPath
    summaryPath = if ($summaryFiles.Count -eq 1) { $summaryFiles[0].FullName } else { $null }
    tracePath = if ($traceFiles.Count -eq 1) { $traceFiles[0].FullName } else { $null }
}
if ($result.status -ne 'passed') {
    $result.error = "Unity exitCode=$($process.ExitCode), summaryCount=$($summaryFiles.Count), traceCount=$($traceFiles.Count)."
}

$resultPath = Join-Path $outputPath 'execution-result.json'
$result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $resultPath -Encoding UTF8
if ($result.status -ne 'passed') { exit 1 }
