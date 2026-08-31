param(
    [switch]$NoBuild,
    [string]$Configuration = 'Debug',
    [int]$TcpPort = 41101,
    [switch]$NoCleanup
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'abilitykit_process_utils.ps1')

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir '..\..\..')
$project = Join-Path $repoRoot 'Server\Orleans\src\AbilityKit.Orleans.MobaSmoke\AbilityKit.Orleans.MobaSmoke.csproj'

if (-not (Test-Path $project)) {
    throw "MOBA smoke project was not found: $project"
}

if ($TcpPort -lt 1 -or $TcpPort -gt 65535) {
    throw "TcpPort must be between 1 and 65535. Actual: $TcpPort"
}

if (-not $NoCleanup) {
    Stop-AbilityKitServices `
        -Ports @($TcpPort, 12211, 31101) `
        -CommandPatterns @('AbilityKit.Orleans.MobaSmoke.csproj') `
        -GraceSeconds 2
}

$commonArgs = @(
    '-p:UseSharedCompilation=false',
    '-p:nodeReuse=false'
)

if (-not $NoBuild) {
    Write-Host 'Building MOBA smoke project...' -ForegroundColor Cyan
    & dotnet build $project -c $Configuration @commonArgs
    if ($LASTEXITCODE -ne 0) {
        throw "MOBA smoke build failed with exit code $LASTEXITCODE."
    }
}

Write-Host "Running MOBA two-client TCP Gateway smoke on 127.0.0.1:$TcpPort..." -ForegroundColor Cyan
$runArgs = @(
    'run',
    '--project', $project,
    '-c', $Configuration
)

if ($NoBuild) {
    $runArgs += '--no-build'
}

$runArgs += $commonArgs
$runArgs += @('--', '--tcp-port', $TcpPort)

Push-Location (Split-Path -Parent $project)
try {
    & dotnet @runArgs
    if ($LASTEXITCODE -ne 0) {
        throw "MOBA smoke failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
