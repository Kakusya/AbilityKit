# Cooking worktree retirement and evidence routes

2026-10-03. Owner requests timely removal of unused worktrees. The following removals are actual, not planned. Active or dirty recovery/integration worktrees are not implicitly disposable.

| Retired worktree | Original HEAD | Retained evidence location under main local/Archives/worktrees | Actual removal result |
|---|---|---|---|
| cooking-core-verified-snapshot | 501bf383d | cooking-core-verified-snapshot-501bf383d/Logs; manifest.json,9 files | Orca removed:true |
| cooking-network-process-current | 6891afdb9 | cooking-network-process-current-6891afdb9/local/Logs and NetworkAcceptance build; manifest.json,223 files | Orca removed:true |
| cooking-core-s01-s03 | 8111472f2 | retired-singleplayer-clean-20261003/cooking-core-s01-s03/Logs | Orca removed:true; original branch retained |
| cooking-menu-s04 | 6e2ecdafe | retired-singleplayer-clean-20261003/cooking-menu-s04/Logs | Orca removed:true; original branch retained |
| cooking-order-s05 | 580033908 | retired-singleplayer-clean-20261003/cooking-order-s05/Logs | Orca removed:true |
| cooking-network-authority-current | 021eb626c | retired-network-frozen-20261003/cooking-network-authority-current/Logs | Orca removed:true |
| cooking-network-et-s14-integration | 73378b39b | retired-network-frozen-20261003/source-refs.bundle; no local/Logs directory was present | Orca removed:true |
| cooking-network-transport-current | a0ca8151b | retired-network-frozen-20261003/cooking-network-transport-current/Logs | Orca removed:true |

The last three archived244 log files in total, with each source/destination SHA256 compared and stored in logs-manifest.json. Their exact original commits and complete histories are additionally retained in retired-singleplayer-clean-20261003/source-refs.bundle, created with the three original branch refs and successfully verified by git bundle verify/list-heads. They are not represented as literal master ancestors: reviewed production was imported via other commits. Saving the original refs/history and retaining non-ancestor branches avoids erasing that distinction.

The first two worktrees were clean and their commits actual ancestors of master. All candidates had no active worker/terminal or attached PTY; the network process candidate additionally had no matching live non-coordinator process. Task source/evidence references are updated to main/archive locations. Historical absolute log paths in old reports identify provenance; read their relative suffix inside the archive above. A retired directory must not be recreated or treated as a live worker solely because an old record mentions it.

The three frozen network producer trees were subsequently confirmed clean/inactive with no matching live non-coordinator processes. Their exact refs and complete histories were saved and git-bundle-verified in retired-network-frozen-20261003/source-refs.bundle;27 present log files were copied with source/destination SHA256 comparison and logs-manifest.json. This preserves the original producer sources independently of cherry-import equivalence; it does not infer extra files or test runs for the Session tree where no local/Logs directory existed. Current implementations and accepted broad evidence remain on main and in the continuing recovery tree.

No new tests, physical LAN evidence or product capability follow from cleanup. No user Practice directory or main tools/__pycache__ files were removed. Any remaining dirty/unreviewed source must be reviewed or durably saved before retirement; currently active recovery work is explicitly retained.


## Additional actual retirement ? cooking-menu-ready-s14

2026-10-03: cooking-menu-ready-s14 at 2ba0217645785c01e7153a7e9b3af0d7900a6b6f was clean including untracked files, had no task.json worktree reference, no matching live non-coordinator process and no entry in current Orca worktree ps. Its source import and original actual master validation are documented in master-menu-policy-verification.md; this commit is not a literal master ancestor. Before removal, its complete original history/ref was saved to local/Archives/worktrees/cooking-menu-ready-s14-2ba021764/source-refs.bundle, successfully verified by git bundle verify and list-heads. No local directory/log artifacts were present in this tree; existing root validation logs remain at their original root paths.

Native git worktree remove succeeded without force after resolving/checking the absolute path inside the managed workspace root. Test-Path then returned false; the original source branch remains available at the exact SHA above. This brings actual retirements to nine. No active recovery tree, dirty source or user Practice files were removed.
