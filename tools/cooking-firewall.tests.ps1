#requires -Version 5.1
# Isolated controls: every firewall, elevation, mutation lock and PATH API is mocked.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'cooking-firewall.ps1')
$script:testRoot = Join-Path ([IO.Path]::GetTempPath()) ('cooking-firewall-tests-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($script:testRoot) | Out-Null
$script:configPath = Join-Path $script:testRoot 'ports.json'
$script:exePath = Join-Path $script:testRoot 'AbilityKit.Game.Cooking.NetworkAcceptance.exe'
[IO.File]::WriteAllText($script:exePath, 'test fixture, never executed')
$script:writer = ${function:Write-CookingJson}
function Write-CookingJson([string]$Path, $Value, [switch]$CreateOnly) {
    if ($script:failConfigWrite -and $Path -eq $script:configPath) { throw 'Injected config write failure.' }
    & $script:writer $Path $Value -CreateOnly:$CreateOnly
}
function Get-CookingStateDirectory { Join-Path $script:testRoot 'state' }
function Assert-CookingAdministrator { if (-not $script:admin) { throw 'Mock: elevation required.' } }
function Get-CookingUserPath { $script:userPath }
function Set-CookingUserPath([string]$Value) { $script:pathWrites++; $script:userPath = $Value }
function Enter-CookingMutationLock {
    $script:lockCalls++
    if ($script:lockFailure) { throw 'Mock: bounded lock timeout.' }
    [pscustomobject]@{ mocked = $true }
}
function Exit-CookingMutationLock($Lock) { $script:lockReleases++ }
function Get-NetUDPEndpoint {
    param($ErrorAction)
    $script:udpReads++
    if ($script:udpQueryFailure) { throw 'Mock: UDP endpoint query failed.' }
    @($script:udpEndpoints)
}
function Get-NetFirewallRule([string]$PolicyStore) {
    $script:reads++
    @($script:rules | Where-Object {
        ($PolicyStore -eq 'ActiveStore' -or $_.PolicyStoreSourceType -eq 'Local') -and
        -not ($PolicyStore -eq 'ActiveStore' -and $script:suppressOwnedActive -and $_.Name -eq 'AbilityKit.Cooking.TestPorts')
    })
}
function Get-NetFirewallPortFilter { param([Parameter(ValueFromPipeline)]$InputObject) process { $InputObject.Ports } }
function Get-NetFirewallAddressFilter { param([Parameter(ValueFromPipeline)]$InputObject) process { $InputObject.Addresses } }
function Get-NetFirewallApplicationFilter { param([Parameter(ValueFromPipeline)]$InputObject) process { $InputObject.Application } }
function Get-NetFirewallServiceFilter { param([Parameter(ValueFromPipeline)]$InputObject) process { $InputObject.ServiceFilter } }
function New-TestRule([string]$Name = 'block', [string]$Protocol = 'UDP', $Ports = @('Any'), [string]$Program = $script:exePath) {
    [pscustomobject]@{ Name = $Name; Enabled = 'True'; Direction = 'Inbound'; Action = 'Block'; Profile = 'Public';
        Group = ''; EdgeTraversalPolicy = 'Block'; PolicyStoreSourceType = 'Local';
        Ports = [pscustomobject]@{ Protocol = $Protocol; LocalPort = @($Ports); RemotePort = @('Any') };
        Addresses = [pscustomobject]@{ LocalAddress = @('Any'); RemoteAddress = @('Any') };
        Application = [pscustomobject]@{ Program = $Program }; ServiceFilter = [pscustomobject]@{ Service = 'Any' } }
}
function Set-NetFirewallRule {
    param($PolicyStore, $Name, $Enabled, $Direction, $Action, $Profile, $Protocol, $LocalPort, $RemotePort,
        $RemoteAddress, $LocalAddress, $Program, $Service, $EdgeTraversalPolicy, $ErrorAction)
    $script:mutations++
    $rule = @($script:rules | Where-Object { $_.Name -eq $Name })[0]
    if (-not $rule) { throw 'Missing mocked rule.' }
    foreach ($property in @('Enabled', 'Direction', 'Action', 'EdgeTraversalPolicy')) {
        if ($PSBoundParameters.ContainsKey($property)) { $rule.$property = [string]$PSBoundParameters[$property] }
    }
    if ($PSBoundParameters.ContainsKey('Profile')) { $rule.Profile = $Profile -join ', ' }
    foreach ($property in @('Protocol', 'LocalPort', 'RemotePort')) {
        if ($PSBoundParameters.ContainsKey($property)) { $rule.Ports.$property = $PSBoundParameters[$property] }
    }
    foreach ($property in @('LocalAddress', 'RemoteAddress')) {
        if ($PSBoundParameters.ContainsKey($property)) { $rule.Addresses.$property = $PSBoundParameters[$property] }
    }
    if ($PSBoundParameters.ContainsKey('Program')) { $rule.Application.Program = $Program }
    if ($PSBoundParameters.ContainsKey('Service')) { $rule.ServiceFilter.Service = $Service }
}
function New-NetFirewallRule {
    param($PolicyStore, $Name, $Enabled, $Direction, $Action, $Profile, $Protocol, $LocalPort, $RemotePort,
        $RemoteAddress, $LocalAddress, $Program, $Service, $EdgeTraversalPolicy, $ErrorAction, $DisplayName, $Group)
    $rule = New-TestRule -Name $Name
    $rule.Group = $Group
    $script:rules += $rule
    $arguments = @{}
    foreach ($key in $PSBoundParameters.Keys) { if ($key -notin @('DisplayName', 'Group')) { $arguments[$key] = $PSBoundParameters[$key] } }
    Set-NetFirewallRule @arguments
}
function Assert-Test([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Assert-Throws([scriptblock]$Body) {
    $threw = $false
    try { & $Body | Out-Null } catch { $threw = $true }
    Assert-Test $threw 'Expected a failure.'
}
function Reset-Test {
    $script:rules = @(); $script:mutations = 0; $script:reads = 0; $script:pathWrites = 0
    $script:lockCalls = 0; $script:lockReleases = 0; $script:lockFailure = $false
    $script:admin = $true; $script:failConfigWrite = $false; $script:userPath = 'C:\Existing;D:\Tools'
    $script:suppressOwnedActive = $false
    $script:udpEndpoints = @(); $script:udpReads = 0; $script:udpQueryFailure = $false
    if (Test-Path -LiteralPath $script:configPath) { Remove-Item -LiteralPath $script:configPath -Force }
}
function Run-Tool([hashtable]$Options) { $Options.ConfigPath = $script:configPath; Invoke-CookingFirewall $Options }
$script:passed = 0
function Test-Case([string]$Name, [scriptblock]$Body) {
    Reset-Test
    & $Body
    $script:passed++
    Write-Output "Passed: $Name"
}
try {
    Test-Case 'Show reads defaults without firewall/elevation/lock/PATH writes' {
        $script:admin = $false
        $r = Run-Tool @{ Action = 'Show' }
        Assert-Test ($r.status -eq 'Passed' -and $r.config.startPort -eq 18090 -and $r.config.endPort -eq 18099) 'Wrong defaults.'
        Assert-Test ($script:reads -eq 0 -and $script:mutations -eq 0 -and $script:lockCalls -eq 0 -and $script:pathWrites -eq 0) 'Show has side effects.'
    }
    Test-Case 'Open creates exact owned rule and saves verified range' {
        $r = Run-Tool @{ Action = 'Open' }
        Assert-Test ($r.status -eq 'Passed' -and $r.localPolicy -eq 'ConfiguredLocalRule' -and $r.remoteConnectivity -eq 'NotRun') ($r | ConvertTo-Json -Depth 8)
        $saved = Read-CookingJson $script:configPath
        Assert-Test ($saved.startPort -eq 18090 -and $saved.endPort -eq 18099) 'Range was not saved.'
        Assert-Test ($script:rules.Count -eq 1 -and $script:mutations -eq 1 -and $script:lockReleases -eq 1) 'Incorrect mutation/lock count.'
    }
    Test-Case 'Repeated Open is idempotent' {
        Run-Tool @{ Action = 'Open' } | Out-Null
        $r = Run-Tool @{ Action = 'Open' }
        Assert-Test ($r.status -eq 'Passed' -and $script:rules.Count -eq 1 -and $script:mutations -eq 1) ($r | ConvertTo-Json -Depth 8)
    }
    Test-Case 'Complete explicit range overrides saved range and reconciles' {
        Run-Tool @{ Action = 'Open' } | Out-Null
        $r = Run-Tool @{ Action = 'Open'; StartPort = '18100'; EndPort = '18105' }
        Assert-Test ($r.status -eq 'Passed' -and $r.configSource -eq 'ExplicitRange') 'Explicit range failed.'
        $r = Run-Tool @{ Action = 'Show' }
        Assert-Test ($r.config.startPort -eq 18100 -and $r.config.endPort -eq 18105 -and $r.configSource -eq 'SavedConfig') 'Saved range not reused.'
    }
    Test-Case 'Incomplete/reversed/out-of-range/boolean/fractional ranges fail before mutation' {
        foreach ($options in @(@{ StartPort = 18090 }, @{ EndPort = 18099 }, @{ StartPort = 20; EndPort = 10 },
            @{ StartPort = 0; EndPort = 10 }, @{ StartPort = 10; EndPort = 65536 },
            @{ StartPort = $true; EndPort = 10 }, @{ StartPort = 1.5; EndPort = 10 })) {
            $options.Action = 'Open'; $r = Run-Tool $options
            Assert-Test ($r.status -eq 'Failed') 'Invalid range accepted.'
        }
        Assert-Test ($script:mutations -eq 0) 'Invalid range changed firewall.'
    }
    Test-Case 'Malformed config is not bypassed by explicit range' {
        [IO.File]::WriteAllText($script:configPath, '{oops')
        $r = Run-Tool @{ Action = 'Open'; StartPort = 18090; EndPort = 18099 }
        Assert-Test ($r.status -eq 'Failed' -and $script:mutations -eq 0) 'Malformed config ignored.'
    }
    Test-Case 'Strict schema rejects string/bool ports, unknown fields and unsupported policy' {
        foreach ($field in @('schemaVersion', 'startPort', 'protocol', 'profiles', 'remoteAddress', 'extra')) {
            $c = Read-CookingJson (Join-Path $PSScriptRoot 'cooking-network.defaults.json')
            switch ($field) {
                schemaVersion { $c.schemaVersion = $true }
                startPort { $c.startPort = '18090' }
                protocol { $c.protocol = 'TCP' }
                profiles { $c.profiles = @('Private', 'Private') }
                remoteAddress { $c.remoteAddress = 'Any' }
                extra { $c | Add-Member NoteProperty extra 1 }
            }
            Write-CookingJson $script:configPath $c
            $r = Run-Tool @{ Action = 'Open' }
            Assert-Test ($r.status -eq 'Failed' -and $script:mutations -eq 0) "Invalid config accepted: $field"
        }
    }
    Test-Case 'Check reports absent rule without writes' {
        $r = Run-Tool @{ Action = 'Check' }
        Assert-Test ($r.status -eq 'Failed' -and $r.localPolicy -eq 'MissingOrMismatch' -and $script:mutations -eq 0 -and $script:lockCalls -eq 0) 'Check result/side effects wrong.'
    }
    Test-Case 'Check reads existing local rule and reports remote connectivity NotRun' {
        Run-Tool @{ Action = 'Open' } | Out-Null
        $before = [IO.File]::ReadAllText($script:configPath); $count = $script:mutations
        $r = Run-Tool @{ Action = 'Check' }
        Assert-Test ($r.status -eq 'Passed' -and $r.remoteConnectivity -eq 'NotRun' -and $script:mutations -eq $count) 'Check changed firewall or claimed UDP proof.'
        Assert-Test ([IO.File]::ReadAllText($script:configPath) -ceq $before) 'Check rewrote config.'
    }
    Test-Case 'Suppressed active-policy owned rule fails Open/Check without saving config' {
        $script:suppressOwnedActive = $true
        $r = Run-Tool @{ Action = 'Open' }
        Assert-Test ($r.status -eq 'Failed' -and -not $r.ownedRuleVerified -and -not (Test-Path -LiteralPath $script:configPath)) 'Suppressed active rule counted as verified.'
        $count = $script:mutations
        $r = Run-Tool @{ Action = 'Check' }
        Assert-Test ($r.status -eq 'Failed' -and $script:mutations -eq $count) 'Check ignored effective suppression or changed rules.'
    }
    Test-Case 'Unowned rule name collision is rejected' {
        $script:rules = @(New-TestRule -Name 'AbilityKit.Cooking.TestPorts')
        $r = Run-Tool @{ Action = 'Open' }
        Assert-Test ($r.status -eq 'Failed' -and $script:mutations -eq 0) 'Unowned rule changed.'
    }
    Test-Case 'Non-elevated Open fails without mutation' {
        $script:admin = $false; $r = Run-Tool @{ Action = 'Open' }
        Assert-Test ($r.status -eq 'Failed' -and $script:mutations -eq 0 -and $script:lockReleases -eq 1) 'Non-admin mutation or leaked lock.'
    }
    Test-Case 'Persistence failure retains old config and reports partial mutation' {
        Run-Tool @{ Action = 'Open' } | Out-Null
        $before = [IO.File]::ReadAllText($script:configPath); $script:failConfigWrite = $true
        $r = Run-Tool @{ Action = 'Open'; StartPort = 18100; EndPort = 18109 }
        Assert-Test ($r.status -eq 'Failed' -and $r.firewallMutationAttempted -and $r.note -match 'partially') 'Partial failure concealed.'
        Assert-Test ([IO.File]::ReadAllText($script:configPath) -ceq $before) 'Old config overwritten.'
    }
    Test-Case 'Exact UDP block diagnosis is read-only and ignores TCP/other programs' {
        Run-Tool @{ Action = 'Open' } | Out-Null
        $script:rules += New-TestRule -Name 'udp'
        $script:rules += New-TestRule -Name 'tcp' -Protocol 'TCP'
        $script:rules += New-TestRule -Name 'other' -Program 'C:\Other.exe'
        $count = $script:mutations
        $r = Run-Tool @{ Action = 'Check'; ProgramPath = $script:exePath }
        Assert-Test ($r.status -eq 'Failed' -and $r.programBlockers.Count -eq 1 -and $r.programBlockers[0].name -eq 'udp' -and $script:mutations -eq $count) 'Block diagnosis wrong.'
    }
    Test-Case 'Domain-only block is outside approved profiles and remains unchanged' {
        $block = New-TestRule -Name 'domain'; $block.Profile = 'Domain'; $script:rules = @($block)
        $r = Run-Tool @{ Action = 'Open'; ProgramPath = $script:exePath; RepairProgramBlock = $true }
        Assert-Test ($r.status -eq 'Passed' -and $r.programBlockers.Count -eq 0 -and $null -eq $r.backupPath) 'Domain-only block treated as relevant.'
        Assert-Test (($block.Ports.LocalPort -join ',') -eq 'Any' -and $block.Enabled -eq 'True' -and $script:mutations -eq 1) 'Domain-only block changed.'
    }
    Test-Case 'Repair subtracts approved range, backs up, preserves TCP/other program and restores' {
        $script:rules = @((New-TestRule -Name 'udp'), (New-TestRule -Name 'tcp' -Protocol 'TCP'), (New-TestRule -Name 'other' -Program 'C:\Other.exe'))
        $r = Run-Tool @{ Action = 'Open'; ProgramPath = $script:exePath; RepairProgramBlock = $true }
        Assert-Test ($r.status -eq 'Passed' -and (Test-Path -LiteralPath $r.backupPath)) ($r | ConvertTo-Json -Depth 8)
        Assert-Test (($script:rules[0].Ports.LocalPort -join ',') -eq '1-18089,18100-65535' -and $script:rules[0].Enabled -eq 'True') 'Complement wrong.'
        Assert-Test (($script:rules[1].Ports.LocalPort -join ',') -eq 'Any' -and ($script:rules[2].Ports.LocalPort -join ',') -eq 'Any') 'Unrelated block changed.'
        $restored = Run-Tool @{ Action = 'RestoreBlock'; BackupPath = $r.backupPath }
        Assert-Test ($restored.status -eq 'Passed' -and ($script:rules[0].Ports.LocalPort -join ',') -eq 'Any') 'Restore failed.'
    }
    Test-Case 'Fully covered block disables then restores only fields' {
        $script:rules = @(New-TestRule -Ports @('18090-18099'))
        $r = Run-Tool @{ Action = 'Open'; ProgramPath = $script:exePath; RepairProgramBlock = $true }
        Assert-Test ($r.status -eq 'Passed' -and $script:rules[0].Enabled -eq 'False') 'Covered rule not disabled.'
        $script:rules[0].Profile = 'Private'
        $restored = Run-Tool @{ Action = 'RestoreBlock'; BackupPath = $r.backupPath }
        Assert-Test ($restored.status -eq 'Passed' -and $script:rules[0].Enabled -eq 'True' -and $script:rules[0].Profile -eq 'Private') 'Restore changed unrelated fields.'
    }
    Test-Case 'Interval subtraction handles disjoint union and boundary ports' {
        Assert-Test ((@(Get-CookingPortComplement @('10-20', '18085-18095', '18097', '18099-18105') 18090 18099) -join ',') -eq '10-20,18085-18089,18100-18105') 'Union subtraction wrong.'
        Assert-Test ((@(Get-CookingPortComplement @('Any') 1 65535)).Count -eq 0) 'Full subtraction wrong.'
        Assert-Test ((@(Get-CookingPortComplement @('Any') 1 1) -join ',') -eq '2-65535') 'Boundary subtraction wrong.'
    }
    Test-Case 'Unsupported named ports, Any protocol and policy-managed blocks reject repair before writes' {
        foreach ($kind in @('named', 'protocol', 'policy')) {
            $block = New-TestRule
            switch ($kind) {
                named { $block.Ports.LocalPort = @('RPC') }
                protocol { $block.Ports.Protocol = 'Any' }
                policy { $block.PolicyStoreSourceType = 'GroupPolicy' }
            }
            $script:rules = @($block); $script:mutations = 0
            $r = Run-Tool @{ Action = 'Open'; ProgramPath = $script:exePath; RepairProgramBlock = $true }
            Assert-Test ($r.status -eq 'Failed' -and $script:mutations -eq 0) "Unsupported repair accepted: $kind"
        }
    }
    Test-Case 'Existing backup is never overwritten and mutation is withheld' {
        $script:rules = @(New-TestRule)
        $backup = Join-Path $script:testRoot 'existing-backup.json'; [IO.File]::WriteAllText($backup, 'preserve')
        $r = Run-Tool @{ Action = 'Open'; ProgramPath = $script:exePath; RepairProgramBlock = $true; BackupPath = $backup }
        Assert-Test ($r.status -eq 'Failed' -and $script:mutations -eq 0 -and [IO.File]::ReadAllText($backup) -eq 'preserve') 'Backup overwritten.'
    }
    Test-Case 'Backup cannot alias absent config by absolute, case or relative path' {
        $script:rules = @(New-TestRule)
        $current = (Get-Location).Path
        try {
            Set-Location -LiteralPath $script:testRoot
            foreach ($backup in @($script:configPath, $script:configPath.ToUpperInvariant(), '.\ports.json')) {
                $r = Run-Tool @{ Action = 'Open'; ProgramPath = $script:exePath; RepairProgramBlock = $true; BackupPath = $backup }
                Assert-Test ($r.status -eq 'Failed' -and $script:mutations -eq 0 -and -not (Test-Path -LiteralPath $script:configPath)) 'Config alias destroyed block backup.'
            }
        } finally { Set-Location -LiteralPath $current }
    }
    Test-Case 'Restore refuses changed program identity before mutation' {
        $script:rules = @(New-TestRule)
        $r = Run-Tool @{ Action = 'Open'; ProgramPath = $script:exePath; RepairProgramBlock = $true }
        $script:rules[0].Application.Program = 'C:\Other.exe'; $count = $script:mutations
        $restored = Run-Tool @{ Action = 'RestoreBlock'; BackupPath = $r.backupPath }
        Assert-Test ($restored.status -eq 'Failed' -and $script:mutations -eq $count) 'Restore accepted changed identity.'
    }
    Test-Case 'Restore remains usable when unrelated applied config is corrupt' {
        $script:rules = @(New-TestRule)
        $r = Run-Tool @{ Action = 'Open'; ProgramPath = $script:exePath; RepairProgramBlock = $true }
        [IO.File]::WriteAllText($script:configPath, '{corrupt')
        $restored = Run-Tool @{ Action = 'RestoreBlock'; BackupPath = $r.backupPath }
        Assert-Test ($restored.status -eq 'Passed' -and ($script:rules[0].Ports.LocalPort -join ',') -eq 'Any') 'Corrupt config blocked rollback.'
    }
    Test-Case 'Restore strictly rejects malformed backup schema/fields/token types before mutation' {
        $script:rules = @(New-TestRule)
        $r = Run-Tool @{ Action = 'Open'; ProgramPath = $script:exePath; RepairProgramBlock = $true }
        $original = [IO.File]::ReadAllText($r.backupPath); $count = $script:mutations
        foreach ($kind in @('booleanSchema', 'stringSchema', 'rootExtra', 'entryExtra', 'numericPort', 'invalidPort', 'invalidRemaining', 'booleanEnabled', 'numericName', 'emptyProgram')) {
            $backup = $original | ConvertFrom-Json
            switch ($kind) {
                booleanSchema { $backup.schemaVersion = $true }
                stringSchema { $backup.schemaVersion = '1' }
                rootExtra { $backup | Add-Member NoteProperty extra 'unexpected' }
                entryExtra { $backup.rules[0] | Add-Member NoteProperty extra 'unexpected' }
                numericPort { $backup.rules[0].localPort = @(18090) }
                invalidPort { $backup.rules[0].localPort = @('RPC') }
                invalidRemaining { $backup.rules[0].remaining = @($true) }
                booleanEnabled { $backup.rules[0].enabled = $true }
                numericName { $backup.rules[0].name = 123 }
                emptyProgram { $backup.rules[0].programPath = '' }
            }
            Write-CookingJson $r.backupPath $backup
            $restored = Run-Tool @{ Action = 'RestoreBlock'; BackupPath = $r.backupPath }
            Assert-Test ($restored.status -eq 'Failed' -and $script:mutations -eq $count) "Invalid backup accepted: $kind"
        }
    }
    Test-Case 'Repair requires Open and existing Cooking executable' {
        foreach ($options in @(@{ Action = 'Check'; RepairProgramBlock = $true; ProgramPath = $script:exePath },
            @{ Action = 'Open'; RepairProgramBlock = $true }, @{ Action = 'Open'; RepairProgramBlock = $true; ProgramPath = 'C:\Missing.exe' })) {
            $r = Run-Tool $options
            Assert-Test ($r.status -eq 'Failed' -and $script:mutations -eq 0) 'Invalid repair request accepted.'
        }
    }
    Test-Case 'Install uses fixed bin, preserves PATH and deduplicates repeated installs' {
        $r = Run-Tool @{ Action = 'Install' }
        Assert-Test ($r.status -eq 'Passed' -and (Test-Path -LiteralPath (Join-Path $r.installDirectory 'cook-firewall.cmd'))) 'Install failed.'
        Assert-Test ($script:userPath.StartsWith('C:\Existing;D:\Tools;') -and $script:pathWrites -eq 1) 'PATH entries lost.'
        $r = Run-Tool @{ Action = 'Install' }
        Assert-Test ($r.status -eq 'Passed' -and $script:pathWrites -eq 1 -and $script:mutations -eq 0) 'Repeated install duplicated PATH or wrote firewall.'
    }
    Test-Case 'Bounded mutation lock failure prevents firewall/config/PATH writes' {
        $script:lockFailure = $true
        foreach ($action in @('Open', 'Install', 'RestoreBlock')) {
            $r = Run-Tool @{ Action = $action }
            Assert-Test ($r.status -eq 'Failed' -and -not $r.firewallMutationAttempted) 'Lock failure concealed.'
        }
        Assert-Test ($script:mutations -eq 0 -and $script:pathWrites -eq 0 -and -not (Test-Path -LiteralPath $script:configPath)) 'Lock failure had side effects.'
    }
    Test-Case 'GetPort selects first default candidate without mutation or reservation' {
        $script:admin = $false
        $r = Run-Tool @{ Action = 'GetPort' }
        Assert-Test ($r.status -eq 'Passed' -and $r.port -eq 18090 -and $r.portReserved -eq $false -and $r.remoteConnectivity -eq 'NotRun') 'Incorrect candidate/claims.'
        Assert-Test ($script:udpReads -eq 1 -and $script:reads -eq 0 -and $script:mutations -eq 0 -and $script:pathWrites -eq 0 -and $script:lockCalls -eq 0 -and -not (Test-Path -LiteralPath $script:configPath)) 'GetPort changed state.'
    }
    Test-Case 'GetPort respects IPv4/IPv6/all-address occupancy, duplicates and range boundaries' {
        $script:udpEndpoints = @(
            [pscustomobject]@{ LocalAddress = '0.0.0.0'; LocalPort = 18090 },
            [pscustomobject]@{ LocalAddress = '::'; LocalPort = 18091 },
            [pscustomobject]@{ LocalAddress = '::1'; LocalPort = 18091 },
            [pscustomobject]@{ LocalAddress = '192.168.1.10'; LocalPort = 18092 },
            [pscustomobject]@{ LocalAddress = '127.0.0.1'; LocalPort = 18089 },
            [pscustomobject]@{ LocalAddress = '::'; LocalPort = 18100 })
        $r = Run-Tool @{ Action = 'GetPort' }
        Assert-Test ($r.status -eq 'Passed' -and $r.port -eq 18093) 'Occupied UDP address/family port was selected.'
    }
    Test-Case 'GetPort consumes saved custom range without changing config' {
        $c = Read-CookingJson (Join-Path $PSScriptRoot 'cooking-network.defaults.json')
        $c.startPort = 18200; $c.endPort = 18202; Write-CookingJson $script:configPath $c
        $before = [IO.File]::ReadAllText($script:configPath)
        $script:udpEndpoints = @([pscustomobject]@{ LocalAddress = '::'; LocalPort = 18200 })
        $r = Run-Tool @{ Action = 'GetPort' }
        Assert-Test ($r.status -eq 'Passed' -and $r.port -eq 18201 -and $r.configSource -eq 'SavedConfig') 'Saved range ignored.'
        Assert-Test ([IO.File]::ReadAllText($script:configPath) -ceq $before) 'GetPort rewrote saved config.'
    }
    Test-Case 'GetPort honors explicit complete range without persisting overrides' {
        $c = Read-CookingJson (Join-Path $PSScriptRoot 'cooking-network.defaults.json'); Write-CookingJson $script:configPath $c
        $before = [IO.File]::ReadAllText($script:configPath)
        $r = Run-Tool @{ Action = 'GetPort'; StartPort = '18300'; EndPort = '18300' }
        Assert-Test ($r.status -eq 'Passed' -and $r.port -eq 18300 -and $r.configSource -eq 'ExplicitRange') 'Explicit range ignored.'
        Assert-Test ([IO.File]::ReadAllText($script:configPath) -ceq $before -and $script:mutations -eq 0 -and $script:pathWrites -eq 0) 'Override mutated state.'
    }
    Test-Case 'GetPort exhaustion fails without candidate or mutation' {
        $script:udpEndpoints = @(18090..18099 | ForEach-Object { [pscustomobject]@{ LocalAddress = '::'; LocalPort = $_ } })
        $r = Run-Tool @{ Action = 'GetPort' }
        Assert-Test ($r.status -eq 'Failed' -and $null -eq $r.port -and $r.portReserved -eq $false -and $r.error -match 'No unoccupied') 'Exhaustion concealed.'
        Assert-Test ($script:mutations -eq 0 -and $script:pathWrites -eq 0 -and $script:lockCalls -eq 0 -and -not (Test-Path -LiteralPath $script:configPath)) 'Exhaustion changed state.'
    }
    Test-Case 'GetPort endpoint-query failure is surfaced without candidate or writes' {
        $script:udpQueryFailure = $true
        $r = Run-Tool @{ Action = 'GetPort' }
        Assert-Test ($r.status -eq 'Failed' -and $null -eq $r.port -and $r.error -match 'UDP endpoint query failed' -and $r.remoteConnectivity -eq 'NotRun') 'Query failure concealed.'
        Assert-Test ($script:mutations -eq 0 -and $script:pathWrites -eq 0 -and $script:lockCalls -eq 0 -and -not (Test-Path -LiteralPath $script:configPath)) 'Query failure changed state.'
    }
    Test-Case 'GetPort validates range/config before endpoint query' {
        $r = Run-Tool @{ Action = 'GetPort'; StartPort = 18090 }
        Assert-Test ($r.status -eq 'Failed' -and $script:udpReads -eq 0) 'Invalid range queried endpoints.'
        [IO.File]::WriteAllText($script:configPath, '{bad')
        $r = Run-Tool @{ Action = 'GetPort' }
        Assert-Test ($r.status -eq 'Failed' -and $script:udpReads -eq 0) 'Malformed config ignored.'
    }
    Write-Output "Passed: $script:passed isolated controls; real firewall/PATH writes: 0; remote UDP: NotRun."
} finally {
    $resolved = [IO.Path]::GetFullPath($script:testRoot)
    $tempPrefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notlike 'cooking-firewall-tests-*') { throw 'Refusing cleanup outside isolated test directory.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
