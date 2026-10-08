# Design

## Command
PowerShell 5.1: cooking-firewall.ps1 -Action Open|Check|Show|Install|RestoreBlock (default Check); optional complete StartPort/EndPort pair, ProgramPath, RepairProgramBlock, BackupPath, ConfigPath. JSON result includes config/ports/protocol/profiles/restriction/ruleName/local status, blockers if ProgramPath supplied, remoteConnectivity=NotRun. Nonzero exit on failures. No full effective-policy proof from merely reading an Allow rule.

## State
Bundled defaults schemaVersion=1: startPort=18090, endPort=18099, protocol=UDP, profiles=Private/Public, remoteAddress=LocalSubnet, ruleName=AbilityKit.Cooking.TestPorts. Saved applied config uses same contract. Strict integer/range/schema validation; never silently coerce invalid strings/bools. Default per-user machine-local state is LocalApplicationData/AbilityKit/CookingNetwork/ports.json, independent of cwd/worktree. Explicit ConfigPath supports isolated tests. Serialize Open/Install/RestoreBlock using a short-lived named mutex shared for the owned rule; bounded acquisition failure surfaces instead of overlapping mutations. Write UTF-8 through same-directory temp/replace after exact firewall readback. On persistence failure report partial firewall mutation; retain earlier config, do not claim transaction success.

## Firewall
Native NetSecurity cmdlets and elevated-token check for mutation. Own one named enabled inbound UDP Allow rule with Private/Public, LocalSubnet. Readback checks exact fields. Block repair requires explicit RepairProgramBlock and existing exact Cooking EXE ProgramPath, basename AbilityKit.Game.Cooking.*.exe. Only local enabled inbound UDP block rules for that exact path. Subtract requested range from numeric LocalPort unions/Any=1–65535. Preserve complements, TCP and other programs. If complement empty, disable only that approved UDP rule and back up Enabled. Reject Any-protocol/named-port/policy-managed cases instead of weakening them. Backup Name, ProgramPath, original LocalPort and Enabled before mutation. Restore validates original rule/program identity then only restores saved port filter/enabled fields.

## Install
LocalApplicationData/AbilityKit/CookingNetwork/bin contains script, defaults and cmd shim. User PATH append deduplicated, does not replace entries. Fixed launcher is not the firewall identity of worktree children. Source stays tools/. No host-runner/allocator/service in this bounded task.

## Checks
Self-contained mocked PowerShell controls, no package/Pester dependency. Main privileged integration separately verifies actual rule/filter/config and installed command. No TCP probes for UDP, no loopback substitution for physical LAN. Preserve original failures and ignored local evidence.
