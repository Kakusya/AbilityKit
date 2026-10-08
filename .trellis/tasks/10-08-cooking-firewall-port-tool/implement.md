# Execution
1. Main reviewed PRD/design against user-approved preceding design and explicit implementation request. R1–R6 stay Cooking-only, use OS APIs, retain Public profile, backup/restore scoped changes.
2. Trellis implement worker owns three tools files only; read task/manifests. No actual firewall/PATH writes, AGENTS edits, worktree creation, commits or broad gates. Main owns documentation/local application/task evidence.
3. Worker implements and runs parser plus isolated controls. Main documents actual interface in AGENTS.md.
4. Trellis check agent reviews bounded tool/docs diff and runs isolated controls; no privileged side effects.
5. Main captures original rules, Install/Open, restores/reapplies scoped block change, checks exact real rule/config and installed command outside repo.
6. Record SHA/dirty, command exits/counts/hashes, raw rule evidence/config/backup, physical LAN NotRun. Leave changes reviewable and uncommitted; no auto-commit/archive unrelated work.
