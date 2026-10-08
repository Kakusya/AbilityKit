# Cooking local firewall port-range tool

## Goal and authorization
Owner reviewed the multi-worktree range/CLI design, then explicitly requested a firewall opening tool, actual port-range opening, range persistence and AGENTS.md documentation. The latest request approves this bounded implementation; do not request repeated permission. The optional host-launch/status CLI is excluded absent an answer to the pending scope question.

## Evidence
Source master aa5e26cc8b05d24f86e7952b7ffa495a03f535b1; prior dirty paths are private/cache only. Git and Orca enumerate only this checkout; issue6-test-gate-results no longer exists as a checkout. No cleanup authorized. Owner confirmed firewall popup from Cooking NetworkAcceptance at UDP 18090; observation PID 65344 stopped. WLAN 6 is Public; exact Cooking EXE has enabled local inbound TCP and UDP block rules with LocalPort Any. Shell is elevated. Native Windows NetSecurity suffices; no dependency/daemon is needed.

## Requirements
- R1: PowerShell 5.1 command opens UDP 18090–18099 by default, custom validated complete StartPort/EndPort pair supported. Private/Public profiles, LocalSubnet remote address. Never disable firewall/change adapter profile.
- R2: One stable owned rule name; Open idempotently reconciles. Check/Show read-only, JSON results, nonzero failures. Configured local policy is distinct from remote connectivity (NotRun).
- R3: Write verified applied ports to LocalApplicationData/AbilityKit/CookingNetwork/ports.json shared across worktrees. Explicit range overrides saved config overrides bundled defaults. Malformed/unsupported config fails visibly. Normal restart persistence only, no power-loss/cross-resource transaction claims.
- R4: Install copies tool/defaults/cook-firewall.cmd to fixed per-user bin and deduplicates user PATH append without replacing entries. New shells discover command; no background service/UAC helpers.
- R5: Optional exact Cooking ProgramPath diagnoses conflicts. Explicit RepairProgramBlock permits subtracting approved range only from local exact-program UDP blocks, backing up LocalPort/Enabled before change. Preserve TCP and ports outside range; reject unsupported protocol/filter/policy sources. RestoreBlock validates and restores the backed-up fields only; never delete unrelated rules.
- R6: AGENTS.md explains usage, ports, shared config, PATH, elevation, block repair/restore and honest verification limits.

## Acceptance
- Meaningful isolated controls cover range/schema validation, config/write failures, idempotence, exact-program UDP block repair/restore, unrelated/TCP preservation and read-only actions. No real firewall/PATH writes in tests.
- Main performs Install/Open with raw before/after evidence. Owned rule/config agree on UDP 18090–18099; original TCP block unchanged, UDP block retains range-external ports.
- Installed Show/Check work outside repo. No false UDP connectivity claim; physical LAN remains NOT_VERIFIED.
- Only tools/cooking-firewall.ps1, tools/cooking-network.defaults.json, tools/cooking-firewall.tests.ps1, AGENTS.md and this task's records change. These serve Cooking network testing. No gameplay/C#/protocol/Unity/other example changes, unrelated gates or old evidence changes.
