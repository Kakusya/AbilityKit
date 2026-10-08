# Delivered and locally applied

Source base aa5e26cc8b05d24f86e7952b7ffa495a03f535b1, dirty=true for this task and the pre-existing private/cache paths. New tools use Windows PowerShell5.1; no dependencies, C#/protocol/Unity/gameplay changes. Owner requested implementation/application after reviewing the prior design. Native Trellis implement/check agents loaded task/spec context; main applied live local changes. No worktree creation/cleanup, merge, commit, push or publication.

## Results
- Passed: worker and distinct reviewer parser checks, zero errors in both scripts.
- Passed: worker, independent checker and main each ran `powershell -NoProfile -ExecutionPolicy Bypass -File tools/cooking-firewall.tests.ps1`, native0, **27 isolated controls**. Test firewall/PATH/elevation/lock APIs mocked; no privileged writes from tests.
- Passed: read-only source Show, native0, default config18090–18099; saved config remained absent before/after Show.
- Passed: actual Install, native0, fixed `%LOCALAPPDATA%/AbilityKit/CookingNetwork/bin`, source/installed hashes equal, prior user PATH retained with one appended bin.
- Passed: real Open with explicit UDP18090–18099, exact Cooking NetworkAcceptance ProgramPath and RepairProgramBlock, native0. Both PersistentStore and ActiveStore readback agree. Saved config schema1 is UDP/18090/18099/Private+Public/LocalSubnet.
- Passed: independent actual-rule readback finds exactly one enabled inbound Allow rule `AbilityKit.Cooking.TestPorts`, UDP18090–18099, Private/Public, LocalSubnet. Exact original Cooking TCP rule fields remain identical. Original UDP block remains enabled at `1-18089,18100-65535`. Network category unchanged.
- Passed: actual RestoreBlock native0 returns both original program-rule snapshots exactly. Subsequent Check native1/ProgramBlocked is the **expected negative control**, not a failed run mislabeled Passed. Reapplication from saved config native0 restores final desired range/complement.
- Passed: installed `cook-firewall -Action Show`, `Check -ProgramPath <exact EXE>`, and repeated `Open -ProgramPath <exact EXE>` from outside repo (`C:/Users/Administrator/AppData/Local/Temp`), native0 each. SavedConfig selected, verified ports agree, blockers empty, no firewall mutation in these three calls. Main appended installed bin only to its own process PATH for immediate command discovery; user registry PATH persists for new environments.
- Passed: final independent TCP/complement/network preservation and `git diff --check`, native0.
- NotRun: PSScriptAnalyzer, not installed. TypeCheck N/A for this PowerShell change; C#/Unity/broad gates N/A for tool-only scope. Physical UDP connectivity/LAN NotRun/NOT_VERIFIED; no claim from local policy readback.

## Raw evidence and identities
Ignored evidence root: `local/Logs/cooking-firewall-tool/20261008-113245`. Contains original environment/rules/network profile, isolated-controls.log, install/open/restore/check receipts, installed-command receipts, final source hashes/config/rules/network profile and both block-backup snapshots. Individual operation native exits are retained in session tool output and the check ledger.

Tool SHA256 at distinct final review: `0D9EE7448190FCAD8950EE148638D21408B613864DEE85FCA8E1C0F1A47D7B77`.
Tests SHA256: `08BC619EE1B7F381648BA28D8B906318841F3EC0DC5182F4F85B972593CF1396`.
Final restore backup: `%LOCALAPPDATA%/AbilityKit/CookingNetwork/block-backup-df5c305a6b0944d8af507a19bdb2ebfe.json`. First backup also preserved; no credentials logged.

## Original failures retained
Implementer controls originally failed native1 after2 (File.Replace null string coercion), after13 (PowerShell comma/arithmetic precedence), and after20 (native cwd vs PowerShell location in alias handling). Implementer diagnostic native1 isolated File.Replace($null) illegal path. Fixes use NullString.Value, parenthesized subtraction, and PowerShell provider path normalization. Final suite covers regressions. Original tool transcript remains authoritative for those failures; no fabricated log files. Final git review identified the old failed alias test's newly generated root ports.json (schema1 backup, synthetic rule name block, temp cooking-firewall-tests-da5d98be9c784d3bad1572da4d68edd6 program). Main verified its exact root path and synthetic identity, preserved it as original-failed-alias-test-backup.json in ignored evidence, and removed only that generated file. Final rerun27/native0 did not recreate it; result is isolated-controls-final-cleanliness.log. No user-existing file was removed.

Main's first comparison command native1 said TCP changed because PowerShell5.1 ConvertFrom-Json returned the root array as a single pipeline value wrapped in an extra array; it never reached Restore. Both saved snapshots showed identical TCP fields. Main corrected snapshot parsing (direct array assignment), reran comparison, restore and reapplication, and finally independently rechecked all fields. Tool code was unchanged. Original command failure remains in session transcript. Initial before-state collection native1 for absent owned-rule lookup is documented separately. Initial cleanup identity guard native1 preserved the stray file because the current TEMP-based pattern did not match its recorded canonical program path; main used the already-observed exact synthetic path to verify identity before preservation/removal. Final cleanup/control command native0.

## Scope and follow-up
AGENTS documents actual tool commands/defaults/config/admin/backup/check limits. The optional host-launch/status/range-allocation CLI was not requested in the latest bounded task and is not delivered; existing hosts can receive a chosen configured port and report READY. Saved range is ready for that future consumer. Code remains uncommitted for review; task/session records do not auto-commit or archive unrelated work.
