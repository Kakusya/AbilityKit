#requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('Open', 'Check', 'Show', 'Install', 'RestoreBlock', 'GetPort')][string]$Action = 'Check',
    $StartPort, $EndPort,
    [string]$ProgramPath,
    [switch]$RepairProgramBlock,
    [string]$BackupPath,
    [string]$ConfigPath
)

# Dot sourcing only loads functions, allowing isolated tests without touching the machine.
function Get-CookingStateDirectory {
    Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'AbilityKit\CookingNetwork'
}
function Assert-CookingAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Open and RestoreBlock require an elevated PowerShell window.'
    }
}
function Get-CookingUserPath { [Environment]::GetEnvironmentVariable('Path', 'User') }
function Set-CookingUserPath([string]$Value) { [Environment]::SetEnvironmentVariable('Path', $Value, 'User') }
function Assert-CookingRange($Start, $End, [switch]$CommandLine) {
    foreach ($value in @($Start, $End)) {
        $integer = $value -is [int] -or $value -is [long]
        if ($CommandLine -and $value -is [string] -and $value -cmatch '^[0-9]{1,5}$') { $integer = $true }
        if (-not $integer -or [long]$value -lt 1 -or [long]$value -gt 65535) {
            throw 'Ports must be integers in 1..65535.'
        }
    }
    if ([long]$Start -gt [long]$End) { throw 'StartPort must be <= EndPort.' }
}
function Assert-CookingConfig($Config) {
    $required = @('schemaVersion', 'startPort', 'endPort', 'protocol', 'profiles', 'remoteAddress', 'ruleName')
    $actual = @($Config.PSObject.Properties.Name)
    if (@(Compare-Object $required $actual).Count -ne 0) { throw 'Unsupported config fields.' }
    if (-not ($Config.schemaVersion -is [int] -or $Config.schemaVersion -is [long]) -or $Config.schemaVersion -ne 1) {
        throw 'Unsupported config schemaVersion.'
    }
    Assert-CookingRange $Config.startPort $Config.endPort
    if ($Config.protocol -cne 'UDP' -or $Config.remoteAddress -cne 'LocalSubnet' -or
        $Config.ruleName -cne 'AbilityKit.Cooking.TestPorts' -or
        -not ($Config.profiles -is [array]) -or $Config.profiles.Count -ne 2 -or
        @($Config.profiles | Where-Object { $_ -cne 'Private' -and $_ -cne 'Public' }).Count -ne 0 -or
        @($Config.profiles | Select-Object -Unique).Count -ne 2) {
        throw 'Unsupported config policy: UDP, Private/Public, LocalSubnet and the owned rule name are required.'
    }
}
function Read-CookingJson([string]$Path) {
    Get-Content -LiteralPath $Path -Raw -Encoding UTF8 -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
}
function Resolve-CookingFilePath([string]$Path) {
    $provider = $null; $drive = $null
    $resolved = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path, [ref]$provider, [ref]$drive)
    if ($provider.Name -ne 'FileSystem') { throw 'Cooking state paths must use the filesystem provider.' }
    $resolved
}
function Write-CookingJson([string]$Path, $Value, [switch]$CreateOnly) {
    $fullPath = Resolve-CookingFilePath $Path
    $directory = [IO.Path]::GetDirectoryName($fullPath)
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    $temporary = Join-Path $directory ([IO.Path]::GetRandomFileName())
    try {
        [IO.File]::WriteAllText($temporary, ($Value | ConvertTo-Json -Depth 12), (New-Object Text.UTF8Encoding($false)))
        if ($CreateOnly -or -not [IO.File]::Exists($fullPath)) { [IO.File]::Move($temporary, $fullPath) }
        else { [IO.File]::Replace($temporary, $fullPath, [NullString]::Value) }
    } finally {
        if ([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) }
    }
}
function Get-CookingConfig([hashtable]$Options) {
    $path = $Options.ConfigPath
    if (-not $path) { $path = Join-Path (Get-CookingStateDirectory) 'ports.json' }
    $path = Resolve-CookingFilePath $path
    $source = 'BundledDefaults'
    $config = Read-CookingJson (Join-Path $PSScriptRoot 'cooking-network.defaults.json')
    Assert-CookingConfig $config
    if (Test-Path -LiteralPath $path) {
        $config = Read-CookingJson $path
        Assert-CookingConfig $config
        $source = 'SavedConfig'
    }
    if ($Options.ContainsKey('StartPort') -xor $Options.ContainsKey('EndPort')) { throw 'Supply both StartPort and EndPort.' }
    if ($Options.ContainsKey('StartPort')) {
        Assert-CookingRange $Options.StartPort $Options.EndPort -CommandLine
        $config.startPort = [int]$Options.StartPort
        $config.endPort = [int]$Options.EndPort
        $source = 'ExplicitRange'
    }
    [pscustomobject]@{ Config = $config; Path = $path; Source = $source }
}
function Get-CookingRules([string]$Store) {
    @(Get-NetFirewallRule -PolicyStore $Store -ErrorAction Stop)
}
function Assert-CookingLocalRule($Rule) {
    if ([string]$Rule.PolicyStoreSourceType -ne 'Local') { throw "Refusing non-local rule $($Rule.Name)." }
}
function Test-CookingRule($Rule, $Config) {
    if (-not $Rule) { return $false }
    $ports = $Rule | Get-NetFirewallPortFilter -ErrorAction Stop
    $address = $Rule | Get-NetFirewallAddressFilter -ErrorAction Stop
    $application = $Rule | Get-NetFirewallApplicationFilter -ErrorAction Stop
    $service = $Rule | Get-NetFirewallServiceFilter -ErrorAction Stop
    $profiles = @(([string]$Rule.Profile -split ',\s*') | Sort-Object)
    return ([string]$Rule.Enabled -eq 'True' -and [string]$Rule.Direction -eq 'Inbound' -and
        [string]$Rule.Action -eq 'Allow' -and [string]$Rule.PolicyStoreSourceType -eq 'Local' -and
        $Rule.Group -eq 'AbilityKit.Cooking.TestTools' -and ($profiles -join ',') -eq 'Private,Public' -and
        [string]$ports.Protocol -in @('UDP', '17') -and
        (@($ports.LocalPort) -join ',') -eq "$($Config.startPort)-$($Config.endPort)" -and
        (@($ports.RemotePort) -join ',') -eq 'Any' -and
        (@($address.RemoteAddress) -join ',') -eq 'LocalSubnet' -and
        (@($address.LocalAddress) -join ',') -eq 'Any' -and
        [string]$application.Program -eq 'Any' -and [string]$service.Service -eq 'Any' -and
        [string]$Rule.EdgeTraversalPolicy -eq 'Block')
}
function Get-CookingIntervals($Ports) {
    foreach ($token in @($Ports)) {
        foreach ($part in ([string]$token -split ',')) {
            if ($part -eq 'Any') { ,@(1, 65535); continue }
            if ($part -notmatch '^([0-9]{1,5})(?:-([0-9]{1,5}))?$') { throw "Unsupported LocalPort '$part'." }
            $first = [int]$Matches[1]
            $last = $first
            if ($Matches[2]) { $last = [int]$Matches[2] }
            Assert-CookingRange $first $last
            ,@($first, $last)
        }
    }
}
function Get-CookingPortComplement($Ports, [int]$Start, [int]$End) {
    foreach ($interval in @(Get-CookingIntervals $Ports)) {
        $first = $interval[0]; $last = $interval[1]
        $pieces = @()
        if ($last -lt $Start -or $first -gt $End) { $pieces = @(,@($first, $last)) }
        else {
            if ($first -lt $Start) { $pieces += ,@($first, ($Start - 1)) }
            if ($last -gt $End) { $pieces += ,@(($End + 1), $last) }
        }
        foreach ($piece in $pieces) {
            if ($piece[0] -eq $piece[1]) { [string]$piece[0] }
            else { "$($piece[0])-$($piece[1])" }
        }
    }
}
function Resolve-CookingProgram([string]$Path) {
    $item = Get-Item -LiteralPath $Path -ErrorAction Stop
    if ($item.PSIsContainer -or $item.Name -notlike 'AbilityKit.Game.Cooking.*.exe') {
        throw 'ProgramPath must be an existing AbilityKit.Game.Cooking.*.exe file.'
    }
    $item.FullName
}
function Get-CookingProgramBlocks([string]$Path, $Config, [string]$Store = 'ActiveStore') {
    foreach ($rule in @(Get-CookingRules $Store)) {
        if ([string]$rule.Enabled -ne 'True' -or [string]$rule.Direction -ne 'Inbound' -or [string]$rule.Action -ne 'Block') { continue }
        if ([string]$rule.Profile -ne 'Any' -and ([string]$rule.Profile -split ',\s*' | Where-Object { $_ -in @('Private', 'Public') }).Count -eq 0) { continue }
        $application = $rule | Get-NetFirewallApplicationFilter -ErrorAction Stop
        if ([string]$application.Program -ine $Path) { continue }
        $filter = $rule | Get-NetFirewallPortFilter -ErrorAction Stop
        if ([string]$filter.Protocol -notin @('UDP', '17', 'Any', '256')) { continue }
        $overlap = $false; $unsupported = $false
        try {
            foreach ($interval in @(Get-CookingIntervals $filter.LocalPort)) {
                if ($interval[0] -le $Config.endPort -and $interval[1] -ge $Config.startPort) { $overlap = $true }
            }
        } catch { $overlap = $true; $unsupported = $true }
        if ($overlap) {
            [pscustomobject]@{ Rule = $rule; Filter = $filter; UnsupportedPorts = $unsupported }
        }
    }
}
function Repair-CookingProgramBlocks([string]$Path, $Config, [string]$BackupPath) {
    $blocks = @(Get-CookingProgramBlocks $Path $Config 'ActiveStore')
    $entries = @()
    foreach ($block in $blocks) {
        Assert-CookingLocalRule $block.Rule
        if ([string]$block.Filter.Protocol -notin @('UDP', '17') -or $block.UnsupportedPorts) {
            throw "Refusing unsupported protocol/port filter on $($block.Rule.Name)."
        }
        $local = @(Get-CookingRules 'PersistentStore' | Where-Object { $_.Name -eq $block.Rule.Name })
        if ($local.Count -ne 1) { throw 'Block rule is not uniquely local and persistent.' }
        Assert-CookingLocalRule $local[0]
        $remaining = @(Get-CookingPortComplement $block.Filter.LocalPort $Config.startPort $Config.endPort)
        $entries += [pscustomobject]@{ name = $block.Rule.Name; programPath = $Path;
            localPort = @($block.Filter.LocalPort | ForEach-Object { [string]$_ }); enabled = [string]$block.Rule.Enabled;
            remaining = $remaining }
    }
    if ($entries.Count -eq 0) { return $null }
    if (-not $BackupPath) {
        $BackupPath = Join-Path (Get-CookingStateDirectory) ("block-backup-{0}.json" -f [Guid]::NewGuid().ToString('N'))
    }
    $BackupPath = Resolve-CookingFilePath $BackupPath
    Write-CookingJson $BackupPath ([pscustomobject]@{ schemaVersion = 1; rules = $entries }) -CreateOnly
    foreach ($entry in $entries) {
        if ($entry.remaining.Count -eq 0) { Set-NetFirewallRule -PolicyStore PersistentStore -Name $entry.name -Enabled False -ErrorAction Stop | Out-Null }
        else { Set-NetFirewallRule -PolicyStore PersistentStore -Name $entry.name -LocalPort $entry.remaining -ErrorAction Stop | Out-Null }
    }
    if (@(Get-CookingProgramBlocks $Path $Config 'ActiveStore').Count -ne 0) { throw 'Block repair readback failed.' }
    $BackupPath
}
function Restore-CookingProgramBlocks([string]$BackupPath) {
    if (-not $BackupPath) { throw 'RestoreBlock requires BackupPath.' }
    $backup = Read-CookingJson $BackupPath
    if (-not ($backup.schemaVersion -is [int] -or $backup.schemaVersion -is [long]) -or
        $backup.schemaVersion -ne 1 -or -not ($backup.rules -is [array]) -or $backup.rules.Count -eq 0) { throw 'Invalid block backup.' }
    if (@(Compare-Object @('schemaVersion', 'rules') @($backup.PSObject.Properties.Name)).Count -ne 0) { throw 'Unsupported backup fields.' }
    $seen = @{}
    foreach ($entry in $backup.rules) {
        if (@(Compare-Object @('name', 'programPath', 'localPort', 'enabled', 'remaining') @($entry.PSObject.Properties.Name)).Count -ne 0) { throw 'Unsupported backup entry fields.' }
        if (-not ($entry.name -is [string]) -or [string]::IsNullOrWhiteSpace($entry.name) -or
            -not ($entry.programPath -is [string]) -or [string]::IsNullOrWhiteSpace($entry.programPath) -or
            -not ($entry.enabled -is [string]) -or $entry.enabled -cnotin @('True', 'False') -or
            $seen.ContainsKey([string]$entry.name) -or -not ($entry.localPort -is [array]) -or
            $entry.localPort.Count -eq 0 -or -not ($entry.remaining -is [array])) { throw 'Invalid or duplicate backup entry.' }
        foreach ($token in @($entry.localPort) + @($entry.remaining)) {
            if (-not ($token -is [string]) -or [string]::IsNullOrWhiteSpace($token)) { throw 'Invalid backup port token type.' }
        }
        @(Get-CookingIntervals $entry.remaining) | Out-Null
        $seen[[string]$entry.name] = $true
        $path = Resolve-CookingProgram $entry.programPath
        @(Get-CookingIntervals $entry.localPort) | Out-Null
        $rules = @(Get-CookingRules 'PersistentStore' | Where-Object { $_.Name -eq $entry.name })
        if ($rules.Count -ne 1) { throw 'Backup rule no longer exists uniquely.' }
        $rule = $rules[0]; Assert-CookingLocalRule $rule
        $application = $rule | Get-NetFirewallApplicationFilter -ErrorAction Stop
        $filter = $rule | Get-NetFirewallPortFilter -ErrorAction Stop
        if ([string]$application.Program -ine $path -or [string]$rule.Action -ne 'Block' -or
            [string]$rule.Direction -ne 'Inbound' -or [string]$filter.Protocol -notin @('UDP', '17')) {
            throw 'Backup rule identity changed; refusing restore.'
        }
    }
    foreach ($entry in $backup.rules) {
        Set-NetFirewallRule -PolicyStore PersistentStore -Name $entry.name -LocalPort $entry.localPort -Enabled $entry.enabled -ErrorAction Stop | Out-Null
        $rule = @(Get-CookingRules 'PersistentStore' | Where-Object { $_.Name -eq $entry.name })[0]
        $filter = $rule | Get-NetFirewallPortFilter -ErrorAction Stop
        if ([string]$rule.Enabled -ne $entry.enabled -or (@($filter.LocalPort) -join ',') -ne ($entry.localPort -join ',')) {
            throw 'Restore readback failed.'
        }
    }
}
function Install-CookingFirewall {
    $bin = Join-Path (Get-CookingStateDirectory) 'bin'
    [IO.Directory]::CreateDirectory($bin) | Out-Null
    foreach ($name in @('cooking-firewall.ps1', 'cooking-network.defaults.json')) {
        $source = Join-Path $PSScriptRoot $name; $target = Join-Path $bin $name
        if ([IO.Path]::GetFullPath($source) -ine [IO.Path]::GetFullPath($target)) { Copy-Item -LiteralPath $source -Destination $target -Force -ErrorAction Stop }
    }
    [IO.File]::WriteAllText((Join-Path $bin 'cook-firewall.cmd'), "@echo off`r`npowershell.exe -NoProfile -ExecutionPolicy Bypass -File `"%~dp0cooking-firewall.ps1`" %*`r`nexit /b %errorlevel%`r`n", [Text.Encoding]::ASCII)
    $userPath = Get-CookingUserPath
    $exists = @($userPath -split ';' | Where-Object { $_.Trim().TrimEnd('\') -ieq $bin.TrimEnd('\') }).Count -gt 0
    if (-not $exists) {
        $updated = $bin
        if ($userPath) { $updated = $userPath.TrimEnd(';') + ';' + $bin }
        Set-CookingUserPath $updated
    }
    $bin
}
function Enter-CookingMutationLock {
    $mutex = New-Object Threading.Mutex($false, 'Global\AbilityKit.Cooking.TestPorts')
    try {
        $acquired = $false
        try { $acquired = $mutex.WaitOne(15000) }
        catch [Threading.AbandonedMutexException] { $acquired = $true }
        if (-not $acquired) { throw 'Timed out waiting for another Cooking firewall mutation.' }
        return $mutex
    } catch { $mutex.Dispose(); throw }
}
function Exit-CookingMutationLock($Lock) { $Lock.ReleaseMutex(); $Lock.Dispose() }
function Invoke-CookingFirewallCore([hashtable]$Options) {
    $ErrorActionPreference = 'Stop'
    $result = [ordered]@{ action = 'Check'; status = 'Failed'; localPolicy = 'NotRun'; remoteConnectivity = 'NotRun'; firewallMutationAttempted = $false }
    try {
        $command = 'Check'; if ($Options.Action) { $command = $Options.Action }
        $result.action = $command
        if ($command -notin @('Open', 'Check', 'Show', 'Install', 'RestoreBlock', 'GetPort')) { throw 'Unknown action.' }
        if ($Options.RepairProgramBlock -and ($command -ne 'Open' -or -not $Options.ProgramPath)) { throw 'RepairProgramBlock requires Open and ProgramPath.' }
        if ($command -eq 'RestoreBlock') {
            Assert-CookingAdministrator
            $result.firewallMutationAttempted = $true
            Restore-CookingProgramBlocks $Options.BackupPath
            $result.status = 'Passed'; $result.localPolicy = 'BlockFieldsRestored'
            return [pscustomobject]$result
        }
        $state = Get-CookingConfig $Options; $config = $state.Config
        $result.configPath = $state.Path; $result.configSource = $state.Source; $result.config = $config
        if ($command -eq 'Install') {
            $result.installDirectory = Install-CookingFirewall
            $result.status = 'Passed'; $result.note = 'Open a new shell to discover cook-firewall.'
            return [pscustomobject]$result
        }
        if ($command -eq 'GetPort') {
            $result.portReserved = $false
            $occupied = @{}
            foreach ($endpoint in @(Get-NetUDPEndpoint -ErrorAction Stop)) {
                $occupied[[int]$endpoint.LocalPort] = $true
            }
            for ($candidate = [int]$config.startPort; $candidate -le [int]$config.endPort; $candidate++) {
                if (-not $occupied.ContainsKey($candidate)) {
                    $result.port = $candidate
                    $result.status = 'Passed'
                    $result.note = 'Unreserved candidate based on local UDP endpoints; host binding/READY is authoritative. Firewall and LAN connectivity were not tested.'
                    return [pscustomobject]$result
                }
            }
            throw 'No unoccupied UDP port was found in the configured range.'
        }
        $program = $null
        if ($Options.ProgramPath) { $program = Resolve-CookingProgram $Options.ProgramPath; $result.programPath = $program }
        if ($command -eq 'Show') {
            $result.status = 'Passed'; $result.note = 'Configured range only; firewall and UDP connectivity were not tested.'
            return [pscustomobject]$result
        }
        $owned = @(Get-CookingRules 'PersistentStore' | Where-Object { $_.Name -eq $config.ruleName })
        if ($owned.Count -gt 1) { throw 'Owned rule name is not unique.' }
        $rule = $null; if ($owned.Count -eq 1) { $rule = $owned[0] }
        if ($command -eq 'Open') {
            if ($Options.RepairProgramBlock -and $Options.BackupPath -and
                (Resolve-CookingFilePath $Options.BackupPath).Equals($state.Path, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'BackupPath must differ from ConfigPath; the backup must survive config persistence.'
            }
            Assert-CookingAdministrator
            if ($rule) {
                Assert-CookingLocalRule $rule
                if ($rule.Group -ne 'AbilityKit.Cooking.TestTools') { throw 'Rule name belongs to an unowned rule.' }
            }
            if ($Options.RepairProgramBlock) {
                # Backup precedes every approved block mutation; errors report possible partial mutation.
                $result.backupPath = $Options.BackupPath
                if (-not $result.backupPath) {
                    $result.backupPath = Join-Path (Get-CookingStateDirectory) ("block-backup-{0}.json" -f [Guid]::NewGuid().ToString('N'))
                }
                if ((Resolve-CookingFilePath $result.backupPath).Equals($state.Path, [StringComparison]::OrdinalIgnoreCase)) {
                    throw 'BackupPath must differ from ConfigPath.'
                }
                $result.firewallMutationAttempted = $true
                $appliedBackup = Repair-CookingProgramBlocks $program $config $result.backupPath
                if (-not $appliedBackup) { $result.backupPath = $null }
            }
            if (-not (Test-CookingRule $rule $config)) {
                $settings = @{ PolicyStore = 'PersistentStore'; Name = $config.ruleName; Enabled = 'True'; Direction = 'Inbound';
                    Action = 'Allow'; Profile = @('Private', 'Public'); Protocol = 'UDP';
                    LocalPort = "$($config.startPort)-$($config.endPort)"; RemotePort = 'Any';
                    RemoteAddress = 'LocalSubnet'; LocalAddress = 'Any'; Program = 'Any'; Service = 'Any'; EdgeTraversalPolicy = 'Block'; ErrorAction = 'Stop' }
                $result.firewallMutationAttempted = $true
                if ($rule) { Set-NetFirewallRule @settings | Out-Null }
                else { New-NetFirewallRule @settings -DisplayName 'Cooking test UDP ports' -Group 'AbilityKit.Cooking.TestTools' | Out-Null }
                $rule = @(Get-CookingRules 'PersistentStore' | Where-Object { $_.Name -eq $config.ruleName })[0]
            }
        }
        $configured = Test-CookingRule $rule $config
        $active = @(Get-CookingRules 'ActiveStore' | Where-Object { $_.Name -eq $config.ruleName })
        $activeVerified = $false
        if ($active.Count -eq 1) { $activeVerified = Test-CookingRule $active[0] $config }
        $result.ownedRuleVerified = ($configured -and $activeVerified)
        $configured = $result.ownedRuleVerified
        $result.localPolicy = 'MissingOrMismatch'
        if ($configured) { $result.localPolicy = 'ConfiguredLocalRule' }
        $blocks = @()
        if ($program) { $blocks = @(Get-CookingProgramBlocks $program $config 'ActiveStore') }
        $result.programBlockers = @($blocks | ForEach-Object { [pscustomobject]@{ name = $_.Rule.Name; protocol = [string]$_.Filter.Protocol;
            localPort = @($_.Filter.LocalPort); policySource = [string]$_.Rule.PolicyStoreSourceType; unsupportedPorts = $_.UnsupportedPorts } })
        if ($blocks.Count -gt 0) { $result.localPolicy = 'ProgramBlocked' }
        if (-not $configured -or $blocks.Count -gt 0) { throw 'Local rule is missing/mismatched or exact-program blocks remain.' }
        if ($command -eq 'Open') { Write-CookingJson $state.Path $config }
        $result.status = 'Passed'
        $result.note = 'Verified owned local rule only; other policy, routing and physical UDP connectivity are not proven.'
    } catch {
        $result.error = $_.Exception.Message
        if ($result.firewallMutationAttempted) { $result.note = 'Firewall may be partially changed; saved config is updated only after verification. Inspect rules and any block backup.' }
    }
    [pscustomobject]$result
}
function Invoke-CookingFirewall([hashtable]$Options) {
    $lock = $null
    try {
        if ($Options.Action -in @('Open', 'Install', 'RestoreBlock')) { $lock = Enter-CookingMutationLock }
        Invoke-CookingFirewallCore $Options
    } catch {
        [pscustomobject]@{ action = $Options.Action; status = 'Failed'; localPolicy = 'NotRun'; remoteConnectivity = 'NotRun';
            firewallMutationAttempted = $false; error = $_.Exception.Message }
    } finally { if ($lock) { Exit-CookingMutationLock $lock } }
}

if ($MyInvocation.InvocationName -ne '.') {
    $options = @{}
    foreach ($key in $PSBoundParameters.Keys) { $options[$key] = $PSBoundParameters[$key] }
    $outcome = Invoke-CookingFirewall $options
    $outcome | ConvertTo-Json -Depth 12
    if ($outcome.status -ne 'Passed') { exit 1 }
    exit 0
}
