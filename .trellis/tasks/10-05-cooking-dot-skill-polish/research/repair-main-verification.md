# Main independent verification of repaired core

Repaired exact Git source: `4a4f36e6435f1315ddcec1d462c924e16b44ec5f`. Main inspected the three-line helper fix, the 68 added expected/actual regression assertions and semantics-preserving template formatting. Four delivery files changed; original independent and implementation failures/hashes were read back unchanged.

Main ran auditor-authored contract checks with **only two identity inputs changed**: candidate SHA and freeze-manifest filename. `independent-controls-repair.py` preserves the original expectations; the original script and Failed runs remain unchanged. Actual command: `python -B .trellis/tasks/10-05-cooking-dot-skill-polish/research/independent-controls-repair.py independent-control-repair-01`. Exit 0; 93 assertions Passed, 0 Failed. Git blobs and working bytes are pinned by repair-freeze-receipt/source-identity; one actual diagnostic subprocess is local/read-only, not exclusive coordinator takeover. This is main execution of independent contract controls, **not** a new Orca auditor review of this SHA.

Worker's expanded checks: 190 Passed / 0 Failed on hash-matched repaired bytes; 12 actual self-only fault/diagnostic subprocesses. Baseline regression failures and fixture mistake remain recorded. Product gates N/A; production merge/postmerge and host autoactivation NotRun; ordinary-Run exclusive cross-main takeover Blocked.

Full staged diff check: Failed because raw original whitespace failures and original template snapshots deliberately retain their bytes. Delivered skill/SOP scoped staged check: Passed, exit0. No historical failure was relabeled.

Manual main reloaded the repaired skill/conditional contracts and verified Git content/hash binding before the actual pilot. Push succeeded, immediate readback failed128 (its stderr was not persisted and remains unknown); main read-only verified exact remote ref and recovered missing receipt on op000005 without repush. Actual pilot dispatch op000006 has accepted-input and turn-start proof for Task task_0e2318dcd203 / Dispatch ctx_31a62bbd882d. Startup applied does not mean worker done or final skill accepted.
