# Cooking worktree retirement and evidence routes

2026-10-03. Owner requests timely removal of unused worktrees. The following removals are actual, not planned. Active or dirty recovery/integration worktrees are not implicitly disposable.

| Retired worktree | Original HEAD | Retained evidence location under main local/Archives/worktrees | Actual removal result |
|---|---|---|---|
| cooking-core-verified-snapshot | 501bf383d | cooking-core-verified-snapshot-501bf383d/Logs; manifest.json,9 files | Orca removed:true |
| cooking-network-process-current | 6891afdb9 | cooking-network-process-current-6891afdb9/local/Logs and NetworkAcceptance build; manifest.json,223 files | Orca removed:true |
| cooking-core-s01-s03 | 8111472f2 | retired-singleplayer-clean-20261003/cooking-core-s01-s03/Logs | Orca removed:true; original branch retained |
| cooking-menu-s04 | 6e2ecdafe | retired-singleplayer-clean-20261003/cooking-menu-s04/Logs | Orca removed:true; original branch retained |
| cooking-order-s05 | 580033908 | retired-singleplayer-clean-20261003/cooking-order-s05/Logs | Orca removed:true |

The last three archived244 log files in total, with each source/destination SHA256 compared and stored in logs-manifest.json. Their exact original commits and complete histories are additionally retained in retired-singleplayer-clean-20261003/source-refs.bundle, created with the three original branch refs and successfully verified by git bundle verify/list-heads. They are not represented as literal master ancestors: reviewed production was imported via other commits. Saving the original refs/history and retaining non-ancestor branches avoids erasing that distinction.

The first two worktrees were clean and their commits actual ancestors of master. All candidates had no active worker/terminal or attached PTY; the network process candidate additionally had no matching live non-coordinator process. Task source/evidence references are updated to main/archive locations. Historical absolute log paths in old reports identify provenance; read their relative suffix inside the archive above. A retired directory must not be recreated or treated as a live worker solely because an old record mentions it.

No new tests, physical LAN evidence or product capability follow from cleanup. No user Practice directory or main tools/__pycache__ files were removed. Any remaining dirty/unreviewed source must be reviewed or durably saved before retirement; currently active recovery work is explicitly retained.
