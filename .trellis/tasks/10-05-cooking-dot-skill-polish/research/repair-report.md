# Bounded binding repair — worker evidence

Implementation Dispatch: `task_0bd0234a0eec` / `ctx_90bfffaf9252`, terminal `term_b71b47d0-c93b-4393-a01e-a43fb1eba562`. Main remains sole coordinator; the independent auditor owns independent reports/controls and the next frozen-source review. This repair is separate from the operator-example pilot.

## Result and boundary

**Passed within the local helper validation envelope:** `copies` now compares the entire validated immutable binding for every entry sharing a flow UUID, rejecting disagreement before that entry can join the result. Existing comparison against each reachable declared canonical binding and record-subset validation remain in place. Equal copies, old-subset evidence and legitimate separate UUID groups still work. This adds no election, lock, lease, schema or write authority mechanism.

Only four delivery files changed:

- `.agents/skills/cooking-dot-workflow/scripts/records.py`: keep a per-UUID binding and require complete dictionary equality on every subsequent entry.
- `.agents/skills/cooking-dot-workflow/scripts/test_controls.py`: add observable positive and negative copy-binding controls using existing fixtures and assertions.
- `.agents/skills/cooking-dot-workflow/assets/dot-request.md` and `dot-decision.md`: replace trailing-space hard breaks with separated paragraphs; field text and semantics are unchanged.

Worker evidence is confined to this report, `repair-controls-baseline-01-console.txt`, `repair-controls-baseline-01/`, `repair-controls-baseline-02/`, `repair-controls-run-01/` and `repair-file-hashes.json`. No commits, pushes, GitHub/browser writes, tools installation, nested agents, new worktrees/Run, resource cleanup or product/Unity/example changes were made. Existing untracked coordinator/auditor artifacts and `__pycache__` were preserved.

Loaded trellis-start, trellis-before-dev, trellis-check, skill-creator and version-matched orca-cli guidance; read the owning task contracts/manifests, dot-plan, dot-pilot-plan, timing rule, recovery contract and independent finding. The existing task is approved/in_progress; no new Trellis task was created. The phase loader printed `Phase Index section not found in workflow.md`; that context-loader limitation is not a Passed workflow check. Git/Orca worktree inventories still show main, this child and the stopped issue6 tree; the existing cross-tree provenance in `source-search.md` was read. This repair did not re-audit or alter other trees or current Issues.

## Regression controls and actual commands

All commands ran from the child worktree, whose HEAD remained `32b400a5c6d375be366faa606e867ef68037cfa8`. Repaired source is **dirty working bytes**, not a new frozen Git candidate. Exact argv, before/after dirty state, native exits, executable/file hashes and console outputs are recorded in `repair-controls-run-01/execution.json`; baseline 02 has its own `execution.json` and source snapshots.

| Command / evidence | Native exit | Actual result |
| --- | --- | --- |
| `python -B .agents/skills/cooking-dot-workflow/scripts/test_controls.py --output-dir .trellis/tasks/10-05-cooking-dot-skill-polish/research/repair-controls-baseline-01` | 1 | **Failed**: 149 Passed / 37 Failed. Includes 36 demonstrated binding failures and one new fixture error described below. |
| Same command with output directory `repair-controls-baseline-02` | 1 | **Failed** on original helper: 151 Passed / 39 Failed, 190 assertions. All 39 failures are expected conflict-refusal regressions. |
| Same command with output directory `repair-controls-run-01` | 0 | **Passed** on repaired bytes: 190 Passed / 0 Failed. Original 122 controls plus 68 new assertions. |
| `git diff --check 4cf5b38bcb06b2f58d49fd73fc9de26e5cec02fe -- .agents/skills/cooking-dot-workflow .trellis/spec/abilitykit/supervised-issue-delivery.md` | 0 | **Passed** for planning-to-working-tree range, including tracked templates. |
| `git diff --check HEAD` | 0 | **Passed** for current tracked repair diff. |
| `git diff --name-only HEAD` | 0 | Exactly the four owned delivery files listed above. |
| PowerShell here-string piped to `python -B -` for final source/evidence/template readback | 0 | **Passed**: 6 checks; template nonblank text matches original, four-file boundary, baseline helper matches frozen Git blob, delivered/original evidence hashes match. Raw results: `repair-controls-run-01/final-readback.json`. |

The regression controls first establish valid equal-record and old-subset groups, then a legitimate separate UUID. For each of 13 binding fields, a second self-canonical root is individually valid and reachable with the same UUID. Pair discovery must reject it in both input orders and when it appears as the third entry after a valid equal copy. All input bytes must remain unchanged. Fields exercised: canonical_root, execution_host, worktree_id, task_path, repository, branch, target_branch, delivery_mode, original_session, original_terminal, run_id, approval_ref and created_utc. Version/skill validation remains unchanged; different UUIDs are intentionally separate groups.

The conflict fixtures necessarily change canonical_root when constructing a second self-canonical authority; cases for other fields change that field as well. They are full-binding conflict cases, not a claim that all differing fields can independently vary against one physical canonical file. The implementation compares the complete dictionary rather than a selected field subset.

Baseline 01 initially moved created_utc after the fixture records, which made that last fixture invalid (`record predates flow`) and stopped its control group. Corrected only the fixture to an earlier valid timestamp before baseline 02, retained all baseline 01 output, and did not count its exception as evidence of the intended conflict refusal. Baseline 01 did not capture an exact pre-run source manifest; baseline 02 and repaired run did. Their snapshots identify the exact tested scripts without relying on HEAD alone.

`repair-controls-run-01/controls-output.json` preserves every expected/actual assertion and diagnostic, plus all 12 subprocess argv/stdout/stderr/exit records. Preset self-only crash exits are 71/72/73/74; other subprocesses exit 0. `controls-fixtures.json` preserves all isolated flow/evidence/record bytes after the run. No product test count is inferred from these assertions.

## Identity and original evidence

Python: `3.14.8 (tags/v3.14.8:8e6e75d, Sep 30 2026, 18:19:33) [MSC v.1944 64 bit (AMD64)]`; executable `C:\Python314\python.exe`, SHA-256 `434b361fe0c5960d404974feed493db47f2603a1ace2574312104fbed0182c6f`. Git: `2.56.0.windows.1`. No product binaries were built. The helper and Trellis I/O utility execute as Python source; the four repaired file hashes are in `repair-file-hashes.json` and the execution manifest, and source snapshots are in the repaired run directory.

The manifest also records the consumed, unchanged `.trellis/scripts/common/io.py` SHA-256. Final readback adds six source/format/evidence assertions separately from the 190 helper controls; it is not an additional product test run.

Original independent B1 evidence remains unchanged: `independent-control-run-01/results.json` (92 checks, 91 Passed / 1 Failed), `independent-control-run-02/results.json` (93 checks, 92 Passed / 1 Failed), `independent-controls.py` and `independent-review.md`. Original E1 evidence remains unchanged in `independent-control-run-01/frozen-diff-check.txt` and `original-evidence-audit.json`: the earlier frozen range still failed with exit 2. The new planning-to-working-tree check does not rewrite that historical result or retroactively validate its original dirty-diff check.

The hash manifest records every file in both original independent output directories and original controls-run-01 through controls-run-04, plus independent script/report, and confirms equal hashes before/after repaired checks. All four delivered source hashes were likewise stable through checks. The independent controls intentionally were **not rerun or modified by this implementation worker**: they load the old frozen Git blob and belong to the auditor. Main must announce the repaired exact frozen identity for independent verification.

## Limits and next step

- Local helper regressions/fault controls: **Passed** on the recorded dirty bytes; effects are a local simulator and controlled timestamps. The 12 real subprocesses read/reconcile local fixtures and exit only themselves, not live coordinators or workers.
- Template formatting/diff checks: **Passed** in their stated working-tree ranges; no change to fields/semantics.
- Independent audit of repaired exact Git source: **NotRun**, pending main's freeze and auditor dispatch.
- Operator-example pilot and host automatic skill invocation: **NotRun by this repair**.
- Real ordinary-Run exclusive takeover: **Blocked**, positive exclusivity proof remains unavailable; no takeover attempted or claimed.
- Real transport idempotency, production merge/postmerge/automatic Issue closure, CI and real fresh-coordinator restore: **NotRun**.
- Product/.NET/Unity/LAN/performance gates: **N/A** to these skill/helper files.

Main owns source freeze, independent exact-source review and any later authorized pilot. This worker completion does not accept the overall skill candidate, finish/archive the Trellis task, or authorize production integration. Terminal is retained under existing Owner approval.
