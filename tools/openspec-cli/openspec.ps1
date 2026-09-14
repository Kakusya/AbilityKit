$ErrorActionPreference = 'Stop'
$cli = Join-Path $PSScriptRoot 'node_modules/@fission-ai/openspec/bin/openspec.js'
if (-not (Test-Path $cli)) {
    throw 'OpenSpec dependencies missing. Run: npm ci --prefix tools/openspec-cli'
}
$previousTelemetry = $env:OPENSPEC_TELEMETRY
$previousDnt = $env:DO_NOT_TRACK
$previousConfigHome = $env:XDG_CONFIG_HOME
$updateConfigHome = $null
$exitCode = 1
try {
    $env:OPENSPEC_TELEMETRY = '0'
    $env:DO_NOT_TRACK = '1'
    if ($args.Count -gt 0 -and $args[0] -eq 'update') {
        # Isolate update settings from the user's global delivery/profile preferences.
        $updateConfigHome = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString())
        $configDir = Join-Path $updateConfigHome 'openspec'
        New-Item -ItemType Directory -Path $configDir | Out-Null
        [System.IO.File]::WriteAllText((Join-Path $configDir 'config.json'), '{"profile":"core","delivery":"both","telemetry":{"enabled":false}}')
        $env:XDG_CONFIG_HOME = $updateConfigHome
        & node (Join-Path $PSScriptRoot 'update.mjs') @($args | Select-Object -Skip 1)
    }
    else {
        & node $cli @args
    }
    $exitCode = $LASTEXITCODE
}
finally {
    $env:OPENSPEC_TELEMETRY = $previousTelemetry
    $env:DO_NOT_TRACK = $previousDnt
    $env:XDG_CONFIG_HOME = $previousConfigHome
    if ($null -ne $updateConfigHome -and (Test-Path $updateConfigHome)) {
        Remove-Item -LiteralPath $updateConfigHome -Recurse -Force
    }
}
exit $exitCode
