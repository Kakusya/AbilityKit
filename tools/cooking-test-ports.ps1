#requires -Version 5.1
# Thin launch adapter. GetPort remains the only Windows range/occupancy authority.
function Test-CookingWindows { [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT }
function Resolve-CookingPortSelector([string]$WorkspaceRoot) {
    $installed = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'AbilityKit\CookingNetwork\bin\cooking-firewall.ps1'
    if (Test-Path -LiteralPath $installed -PathType Leaf) { return $installed }
    $source = Join-Path $WorkspaceRoot 'tools/cooking-firewall.ps1'
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw 'Cooking GetPort tool is absent; install it or restore the repository tool.' }
    $source
}
function Invoke-CookingPortSelector([string]$ScriptPath, [string]$OutputPath, [string]$ErrorPath) {
    if ($ScriptPath.Contains('"')) { throw 'Unsupported quotation mark in selector path.' }
    $process = $null
    try {
        $process = Start-Process powershell.exe -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -File "' + $ScriptPath + '" -Action GetPort') -WindowStyle Hidden -PassThru -RedirectStandardOutput $OutputPath -RedirectStandardError $ErrorPath
        $null = $process.Handle
        $timedOut = -not $process.WaitForExit(30000)
        if ($timedOut) { $process.Kill() }
        $process.WaitForExit()
        $raw = ''
        if (Test-Path -LiteralPath $OutputPath) { $raw = Get-Content -LiteralPath $OutputPath -Raw -Encoding UTF8 }
        [pscustomobject]@{ ExitCode = $process.ExitCode; Raw = $raw; TimedOut = $timedOut }
    } finally {
        if ($null -ne $process) {
            $process.Refresh()
            if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
            $process.Dispose()
        }
    }
}
function Assert-CookingPortSelection($Response) {
    if ($null -eq $Response -or $Response -is [array] -or $Response.status -cne 'Passed' -or $Response.action -cne 'GetPort' -or
        -not ($Response.port -is [int] -or $Response.port -is [long]) -or
        $Response.port -lt 1 -or $Response.port -gt 65535 -or
        -not ($Response.portReserved -is [bool]) -or $Response.portReserved -or
        -not ($Response.firewallMutationAttempted -is [bool]) -or $Response.firewallMutationAttempted -or
        $Response.remoteConnectivity -cne 'NotRun' -or $Response.localPolicy -cne 'NotRun') {
        throw 'Invalid GetPort response or mutation/connectivity claim.'
    }
    $config = $Response.config
    if ($null -eq $config -or -not ($config.schemaVersion -is [int] -or $config.schemaVersion -is [long]) -or $config.schemaVersion -ne 1 -or
        -not ($config.startPort -is [int] -or $config.startPort -is [long]) -or
        -not ($config.endPort -is [int] -or $config.endPort -is [long]) -or
        $config.startPort -lt 1 -or $config.endPort -gt 65535 -or $config.startPort -gt $config.endPort -or
        $Response.port -lt $config.startPort -or $Response.port -gt $config.endPort -or $config.protocol -cne 'UDP' -or
        $config.remoteAddress -cne 'LocalSubnet' -or $config.ruleName -cne 'AbilityKit.Cooking.TestPorts' -or
        -not ($config.profiles -is [array]) -or $config.profiles.Count -ne 2 -or
        @($config.profiles | Where-Object { $_ -cne 'Private' -and $_ -cne 'Public' }).Count -ne 0 -or
        @($config.profiles | Select-Object -Unique).Count -ne 2 -or
        -not ($Response.configPath -is [string]) -or [string]::IsNullOrWhiteSpace($Response.configPath) -or
        $Response.configSource -cnotin @('SavedConfig', 'BundledDefaults', 'ExplicitRange')) {
        throw 'Invalid GetPort configuration, range or provenance.'
    }
}
function Write-CookingPortSidecar([string]$Path, $Value) {
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 12) + [Environment]::NewLine, (New-Object Text.UTF8Encoding($false)))
}
function Get-CookingTestPort([string]$WorkspaceRoot, [string]$RunDirectory, [int]$RequestedPort = 0, [string]$Mode = 'Host') {
    if ($RequestedPort -lt 0 -or $RequestedPort -gt 65535) { throw 'Requested Cooking port must be in 0..65535.' }
    $sidecar = Join-Path $RunDirectory 'port-selection.json'
    $record = [ordered]@{ schemaVersion = 1; mode = $Mode; requestedPort = $RequestedPort; selectedPort = $RequestedPort;
        selection = 'Explicit'; portReserved = $false; remoteConnectivity = 'NotRun'; readyVerified = $false }
    if ($Mode -eq 'BuildOnly') { return [pscustomobject]@{ Port = $RequestedPort; Sidecar = $null } }
    try {
        if ($RequestedPort -eq 0 -and $Mode -eq 'Client') { throw 'Client requires the actual host port; it never allocates a port.' }
        if ($RequestedPort -eq 0 -and (Test-CookingWindows)) {
            $record.selection = 'GetPort'
            $script = Resolve-CookingPortSelector $WorkspaceRoot
            $record.selectorScript = $script
            $record.selectorSha256 = (Get-FileHash -LiteralPath $script -Algorithm SHA256 -ErrorAction Stop).Hash
            $record.selectorStdout = Join-Path $RunDirectory 'port-selector.stdout.json'
            $record.selectorStderr = Join-Path $RunDirectory 'port-selector.stderr.log'
            $selection = Invoke-CookingPortSelector $script $record.selectorStdout $record.selectorStderr
            $record.selectorExitCode = $selection.ExitCode
            $record.selectorTimedOut = $selection.TimedOut
            [IO.File]::WriteAllText($record.selectorStdout, $selection.Raw, (New-Object Text.UTF8Encoding($false)))
            if ($selection.TimedOut) { throw 'Cooking GetPort exceeded its 30-second deadline; owned selector stopped. Rerun to reacquire.' }
            if ($selection.ExitCode -ne 0 -or $null -eq $selection.ExitCode) { throw 'Cooking GetPort failed; inspect the retained selector output and rerun to reacquire.' }
            $response = $selection.Raw | ConvertFrom-Json -ErrorAction Stop
            Assert-CookingPortSelection $response
            $record.selectedPort = [int]$response.port
            $record.configPath = $response.configPath; $record.configSource = $response.configSource
            $record.config = $response.config
        } elseif ($RequestedPort -eq 0) { $record.selection = 'OsEphemeral' }
        $record.status = 'Passed'
        Write-CookingPortSidecar $sidecar $record
        [pscustomobject]@{ Port = [int]$record.selectedPort; Sidecar = $sidecar }
    } catch {
        $record.status = 'Failed'; $record.error = $_.Exception.Message
        Write-CookingPortSidecar $sidecar $record
        throw
    }
}
function Assert-CookingTestReady($Selection, [int]$ReadyPort, [int]$ReadyPid, [int]$ExpectedPid) {
    $record = Get-Content -LiteralPath $Selection.Sidecar -Raw -Encoding UTF8 -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
    $record | Add-Member NoteProperty readyPort $ReadyPort -Force
    $record | Add-Member NoteProperty readyPid $ReadyPid -Force
    $record | Add-Member NoteProperty expectedPid $ExpectedPid -Force
    try {
        if ($ExpectedPid -le 0 -or $ReadyPid -ne $ExpectedPid -or $ReadyPort -lt 1 -or $ReadyPort -gt 65535 -or
            ($Selection.Port -ne 0 -and $ReadyPort -ne $Selection.Port)) {
            throw 'Cooking Host READY differs from the launched PID/selected port; rerun to reacquire, before starting a client.'
        }
        $record.readyVerified = $true
    } catch {
        $record.status = 'Failed'; $record | Add-Member NoteProperty error $_.Exception.Message -Force
        Write-CookingPortSidecar $Selection.Sidecar $record
        throw
    }
    Write-CookingPortSidecar $Selection.Sidecar $record
}
