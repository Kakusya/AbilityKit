$ErrorActionPreference = 'Stop'
$cli = Join-Path $PSScriptRoot 'node_modules/@fission-ai/openspec/bin/openspec.js'
if (-not (Test-Path $cli)) {
    throw 'OpenSpec dependencies missing. Run: npm ci --prefix tools/openspec-cli'
}
$previousTelemetry = $env:OPENSPEC_TELEMETRY
$previousDnt = $env:DO_NOT_TRACK
$exitCode = 1
try {
    $env:OPENSPEC_TELEMETRY = '0'
    $env:DO_NOT_TRACK = '1'
    & node $cli @args
    $exitCode = $LASTEXITCODE
}
finally {
    $env:OPENSPEC_TELEMETRY = $previousTelemetry
    $env:DO_NOT_TRACK = $previousDnt
}
exit $exitCode
