#requires -Version 5.1
# Selector subprocesses and platform/path discovery are mocked; no hosts or firewall writes.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'cooking-test-ports.ps1')
$script:originalResolver = ${function:Resolve-CookingPortSelector}
$script:originalInvoker = ${function:Invoke-CookingPortSelector}
$script:testRoot = Join-Path ([IO.Path]::GetTempPath()) ('cooking-test-ports-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($script:testRoot) | Out-Null
$script:fixtureScript = Join-Path $script:testRoot 'mock-selector.ps1'
[IO.File]::WriteAllText($script:fixtureScript, '# fixture never executed')
function Test-CookingWindows { $script:windows }
function Resolve-CookingPortSelector([string]$WorkspaceRoot) { $script:fixtureScript }
function Invoke-CookingPortSelector([string]$ScriptPath, [string]$OutputPath, [string]$ErrorPath) {
    $script:selectorCalls++
    [IO.File]::WriteAllText($ErrorPath, 'mock stderr')
    [pscustomobject]@{ ExitCode = $script:selectorExit; Raw = $script:raw; TimedOut = $script:selectorTimedOut }
}
function Test-Path {
    param([string]$LiteralPath, $PathType)
    if ($script:mockDiscovery) {
        if ($LiteralPath -eq $script:installedPath) { return $script:installedExists }
        if ($LiteralPath -eq (Join-Path $script:testRoot 'tools/cooking-firewall.ps1')) { return $script:sourceExists }
    }
    Microsoft.PowerShell.Management\Test-Path -LiteralPath $LiteralPath
}
function Start-Process {
    param($FilePath, $ArgumentList, $WindowStyle, [switch]$PassThru, $RedirectStandardOutput, $RedirectStandardError)
    if ($null -eq $script:fakeProcess) { throw 'Unexpected process launch in isolated controls.' }
    if ($WindowStyle -ne 'Hidden' -or -not $PassThru) { throw 'Selector must be hidden and owned.' }
    [IO.File]::WriteAllText($RedirectStandardOutput, $script:raw)
    [IO.File]::WriteAllText($RedirectStandardError, '')
    $script:fakeProcess
}
function New-Response {
    [pscustomobject]@{ status = 'Passed'; action = 'GetPort'; port = 18090; portReserved = $false;
        firewallMutationAttempted = $false; remoteConnectivity = 'NotRun'; localPolicy = 'NotRun';
        configPath = 'C:\mock\ports.json'; configSource = 'SavedConfig';
        config = [pscustomobject]@{ schemaVersion = 1; startPort = 18090; endPort = 18099; protocol = 'UDP';
            profiles = @('Private','Public'); remoteAddress = 'LocalSubnet'; ruleName = 'AbilityKit.Cooking.TestPorts' } }
}
function New-FakeProcess {
    $process = [pscustomobject]@{ Handle = 1; ExitCode = 0; HasExited = $false; Killed = $false; Disposed = $false; WaitResult = $false; WaitThrows = $false }
    $process | Add-Member ScriptMethod WaitForExit {
        param($Milliseconds)
        if ($this.WaitThrows -and $null -ne $Milliseconds) { throw 'Injected wait failure.' }
        if ($null -eq $Milliseconds) { $this.HasExited = $true; return }
        if ($this.WaitResult) { $this.HasExited = $true }
        return $this.WaitResult
    }
    $process | Add-Member ScriptMethod Kill { $this.Killed = $true; $this.HasExited = $true; $this.ExitCode = -1 }
    $process | Add-Member ScriptMethod Refresh { }
    $process | Add-Member ScriptMethod Dispose { $this.Disposed = $true }
    $process
}
function Assert-Test([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Assert-Throws([scriptblock]$Body) {
    $failed = $false
    try { & $Body | Out-Null } catch { $failed = $true }
    Assert-Test $failed 'Expected a failure.'
}
$script:count = 0
function Test-Case([string]$Name, [scriptblock]$Body) {
    $script:windows = $true; $script:selectorCalls = 0; $script:selectorExit = 0; $script:selectorTimedOut = $false
    $script:raw = New-Response | ConvertTo-Json -Depth 8
    $script:mockDiscovery = $false; $script:fakeProcess = $null
    $script:caseDirectory = Join-Path $script:testRoot ('case-' + $script:count)
    [IO.Directory]::CreateDirectory($script:caseDirectory) | Out-Null
    & $Body
    $script:count++
    Write-Output "Passed: $Name"
}
function Select-Port([int]$Port = 0, [string]$Mode = 'Host') {
    Get-CookingTestPort -WorkspaceRoot $script:testRoot -RunDirectory $script:caseDirectory -RequestedPort $Port -Mode $Mode
}
try {
    Test-Case 'Valid selection records raw response, script identity, config and unreserved candidate' {
        $s = Select-Port
        $r = Get-Content -LiteralPath $s.Sidecar -Raw | ConvertFrom-Json
        Assert-Test ($s.Port -eq 18090 -and $r.selection -eq 'GetPort' -and -not $r.portReserved -and $r.configSource -eq 'SavedConfig') 'Selection/provenance incorrect.'
        Assert-Test ($r.selectorSha256 -eq (Get-FileHash -LiteralPath $script:fixtureScript).Hash -and [IO.File]::ReadAllText($r.selectorStdout) -ceq $script:raw -and $script:selectorCalls -eq 1) 'Raw response/hash not retained.'
    }
    Test-Case 'Saved custom range response is consumed without hardcoded range assumptions' {
        $r = New-Response; $r.port = 19302; $r.config.startPort = 19300; $r.config.endPort = 19305
        $script:raw = $r | ConvertTo-Json -Depth 8
        Assert-Test ((Select-Port).Port -eq 19302) 'Custom range not consumed.'
    }
    Test-Case 'Native selector failure retains output and prevents candidate return' {
        $script:selectorExit = 1; $r = New-Response; $r.status = 'Failed'; $script:raw = $r | ConvertTo-Json -Depth 8
        Assert-Throws { Select-Port }
        $record = Get-Content -LiteralPath (Join-Path $script:caseDirectory 'port-selection.json') -Raw | ConvertFrom-Json
        Assert-Test ($record.status -eq 'Failed' -and $record.selectorExitCode -eq 1 -and [IO.File]::ReadAllText($record.selectorStdout) -ceq $script:raw) 'Failure evidence lost.'
    }
    Test-Case 'Malformed JSON is rejected and raw output retained' {
        $script:raw = '{invalid'
        Assert-Throws { Select-Port }
        Assert-Test ([IO.File]::ReadAllText((Join-Path $script:caseDirectory 'port-selector.stdout.json')) -eq '{invalid') 'Malformed raw output lost.'
    }
    Test-Case 'Strict response rejects wrong action/status/types/range/policy and false claims' {
        foreach ($kind in @('action','status','portString','portBool','portFraction','outsideRange','reserved','reservedString','mutation','mutationString','connectivity','protocol','schema','startString','endBool','reversed','profiles','source','path')) {
            $r = New-Response
            switch ($kind) {
                action { $r.action = 'Open' }; status { $r.status = 'Failed' }
                portString { $r.port = '18090' }; portBool { $r.port = $true }; portFraction { $r.port = 18090.5 }
                outsideRange { $r.port = 18100 }; reserved { $r.portReserved = $true }; reservedString { $r.portReserved = 'false' }
                mutation { $r.firewallMutationAttempted = $true }; mutationString { $r.firewallMutationAttempted = 'false' }
                connectivity { $r.remoteConnectivity = 'Passed' }; protocol { $r.config.protocol = 'TCP' }
                schema { $r.config.schemaVersion = $true }; startString { $r.config.startPort = '18090' }; endBool { $r.config.endPort = $true }
                reversed { $r.config.startPort = 18100 }; profiles { $r.config.profiles = @('Private','Private') }
                source { $r.configSource = 'Unknown' }; path { $r.configPath = '' }
            }
            $script:raw = $r | ConvertTo-Json -Depth 8
            Assert-Throws { Select-Port }
        }
    }
    Test-Case 'Explicit port bypasses selector and records caller-owned selection' {
        $s = Select-Port -Port 22000
        $r = Get-Content -LiteralPath $s.Sidecar -Raw | ConvertFrom-Json
        Assert-Test ($s.Port -eq 22000 -and $r.selection -eq 'Explicit' -and $script:selectorCalls -eq 0) 'Explicit port allocated or changed.'
        Assert-CookingTestReady $s 22000 123 123
    }
    Test-Case 'Client never allocates and rejects zero before a selector invocation' {
        Assert-Throws { Select-Port -Mode Client }
        $s = Select-Port -Mode Client -Port 23000
        Assert-Test ($s.Port -eq 23000 -and $script:selectorCalls -eq 0) 'Client allocated.'
    }
    Test-Case 'BuildOnly skips selection and sidecar creation' {
        $s = Select-Port -Mode BuildOnly
        Assert-Test ($s.Port -eq 0 -and $null -eq $s.Sidecar -and $script:selectorCalls -eq 0 -and -not (Test-Path -LiteralPath (Join-Path $script:caseDirectory 'port-selection.json'))) 'BuildOnly selected a port.'
    }
    Test-Case 'Non-Windows retains OS ephemeral binding and validates real READY PID/port' {
        $script:windows = $false; $s = Select-Port
        Assert-Test ($s.Port -eq 0 -and $script:selectorCalls -eq 0) 'Non-Windows invoked selector.'
        Assert-CookingTestReady $s 40000 321 321
        $r = Get-Content -LiteralPath $s.Sidecar -Raw | ConvertFrom-Json
        Assert-Test ($r.selection -eq 'OsEphemeral' -and $r.readyVerified -and $r.readyPort -eq 40000) 'Ephemeral READY not recorded.'
    }
    Test-Case 'Matching READY records actual selected endpoint and PID' {
        $s = Select-Port; Assert-CookingTestReady $s 18090 456 456
        $r = Get-Content -LiteralPath $s.Sidecar -Raw | ConvertFrom-Json
        Assert-Test ($r.readyVerified -and $r.readyPort -eq $r.selectedPort -and $r.readyPid -eq $r.expectedPid) 'Ready proof absent.'
    }
    Test-Case 'READY mismatched port/PID/invalid endpoint fails before client with preserved evidence' {
        foreach ($values in @(@(18091,456,456),@(18090,457,456),@(0,456,456),@(65536,456,456),@(18090,0,0))) {
            $s = Select-Port
            Assert-Throws { Assert-CookingTestReady $s $values[0] $values[1] $values[2] }
            $r = Get-Content -LiteralPath $s.Sidecar -Raw | ConvertFrom-Json
            Assert-Test ($r.status -eq 'Failed' -and -not $r.readyVerified -and $r.error -match 'before starting a client') 'Mismatch concealed.'
        }
    }
    Test-Case 'Each case/repeat reacquires through selector and writes distinct sidecars' {
        $first = Select-Port
        $next = Join-Path $script:caseDirectory 'repeat-2'; [IO.Directory]::CreateDirectory($next) | Out-Null
        $r = New-Response; $r.port = 18091; $script:raw = $r | ConvertTo-Json -Depth 8
        $second = Get-CookingTestPort -WorkspaceRoot $script:testRoot -RunDirectory $next
        Assert-Test ($first.Port -eq 18090 -and $second.Port -eq 18091 -and $script:selectorCalls -eq 2 -and $first.Sidecar -ne $second.Sidecar) 'Repeat reused prior selection.'
    }
    Test-Case 'Resolver prefers installed fixed script, falls back to repository and fails if absent' {
        $script:mockDiscovery = $true
        $script:installedPath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'AbilityKit\CookingNetwork\bin\cooking-firewall.ps1'
        $script:installedExists = $true; $script:sourceExists = $true
        Assert-Test ((& $script:originalResolver $script:testRoot) -eq $script:installedPath) 'Installed tool not preferred.'
        $script:installedExists = $false
        Assert-Test ((& $script:originalResolver $script:testRoot) -eq (Join-Path $script:testRoot 'tools/cooking-firewall.ps1')) 'Repository fallback missing.'
        $script:sourceExists = $false
        Assert-Throws { & $script:originalResolver $script:testRoot }
    }
    Test-Case 'Selector timeout stops and disposes owned hidden process, preserves raw output' {
        $script:fakeProcess = New-FakeProcess
        $r = & $script:originalInvoker $script:fixtureScript (Join-Path $script:caseDirectory 'native.stdout') (Join-Path $script:caseDirectory 'native.stderr')
        Assert-Test ($r.TimedOut -and $script:fakeProcess.Killed -and $script:fakeProcess.HasExited -and $script:fakeProcess.Disposed -and $r.Raw -eq $script:raw) 'Timeout leaked selector or lost raw output.'
        $script:selectorTimedOut = $true
        Assert-Throws { Select-Port }
    }
    Test-Case 'Selector wait failure still cleans up owned process' {
        $script:fakeProcess = New-FakeProcess; $script:fakeProcess.WaitThrows = $true
        Assert-Throws { & $script:originalInvoker $script:fixtureScript (Join-Path $script:caseDirectory 'native.stdout') (Join-Path $script:caseDirectory 'native.stderr') }
        Assert-Test ($script:fakeProcess.Killed -and $script:fakeProcess.HasExited -and $script:fakeProcess.Disposed) 'Wait failure leaked selector.'
    }
    Test-Case 'Successful selector process is awaited and disposed without killing' {
        $script:fakeProcess = New-FakeProcess; $script:fakeProcess.WaitResult = $true
        $r = & $script:originalInvoker $script:fixtureScript (Join-Path $script:caseDirectory 'native.stdout') (Join-Path $script:caseDirectory 'native.stderr')
        Assert-Test (-not $r.TimedOut -and $r.ExitCode -eq 0 -and -not $script:fakeProcess.Killed -and $script:fakeProcess.Disposed -and $r.Raw -eq $script:raw) 'Success process/evidence incorrectly handled.'
    }
    Write-Output "Passed: $script:count isolated launch-port controls; real process launches/firewall writes: 0; physical LAN: NOT_VERIFIED."
} finally {
    $resolved = [IO.Path]::GetFullPath($script:testRoot)
    $prefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notlike 'cooking-test-ports-*') { throw 'Refusing cleanup outside isolated test directory.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
