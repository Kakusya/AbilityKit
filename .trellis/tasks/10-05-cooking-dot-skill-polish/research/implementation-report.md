# Cooking dot skill implementation report

Bounded implementation completed on `Kakusya/cooking-dot-skill-polish`; main still owns commit, candidate review, pilot, Run accounting and task completion. Planning/source base is `4cf5b38bcb06b2f58d49fd73fc9de26e5cec02fe`, with dirty implementation bytes. Authority is [dot-plan](dot-plan.md), request `AK-CDF-POLISH-PLAN-01`, plus [dot-timing-rule](dot-timing-rule.md), `AK-CDF-TIMING-CLARIFY-01`; neither is final skill acceptance.

Worker: Task `task_b1846da24106`, Dispatch `ctx_2ede5e596818`, observed Run `run_840202cc5621`. No commits/pushes, GitHub/browser actions, nested workers, model/branch/permission changes, cleanup or live pilot were performed. Other writers' plans/dialogue/reviewer artifacts were preserved.

## Changed delivery files, consumer and acceptance

All consumers are Cooking workflow coordinators/workers; no product or example code is involved.

| Exact file | Need and validation |
| --- | --- |
| `.agents/skills/cooking-dot-workflow/SKILL.md` | Short normal path with stage inputs/exits, mode-specific delivery, manual-loading proof, frozen candidate and explicit paused-session guard; structural/link inspection |
| `.agents/skills/cooking-dot-workflow/references/dot-dialogue.md` | Sole request/budget/quota/late-read/reopening contract, including approved complete-observation provenance; executable timing controls |
| `.agents/skills/cooking-dot-workflow/references/recovery.md` | Sole format/discovery/copy/authority/reconciliation contract, supported API, effective Run derivation and premerge conditions; record/fault controls |
| `.agents/skills/cooking-dot-workflow/assets/checkpoint-operation.md` | Concise binding/checkpoint/operation pair fields and ordering; linked by entry |
| `.agents/skills/cooking-dot-workflow/assets/dot-request.md` | One request format distinguishing planning/final-review; identities, original budget, paused fact and provenance |
| `.agents/skills/cooking-dot-workflow/assets/dot-decision.md` | Decision/timing/scope/next-authority record with null posted time and complete-observation proof |
| `.agents/skills/cooking-dot-workflow/scripts/records.py` | Optional local identity/reference/record validation, Trellis atomic write/readback, derived-index reconstruction and pure gates; no scheduler/API/effect/lease/database |
| `.agents/skills/cooking-dot-workflow/scripts/test_controls.py` | Isolated observable positive/negative controls and self-exiting subprocess fault adapter; simulations explicitly labeled |
| `.trellis/spec/abilitykit/supervised-issue-delivery.md` | Only opt-in Cooking dot section deduplicated to authoritative references, roles/authorization/modes/retention; preceding and following sections compared to Git source unchanged |

`AGENTS.md` and `agents/openai.yaml` are unchanged. No generic Trellis runtime/schema/skill, Unity, Practice, Shooter/MOBA/Orleans or old #6 changes.

## Original six findings addressed

1. Fixed discovery: task-local `research/cooking-dot-flow/flow.json`, version 1, generated stable UUID, declared canonical directory/host, immutable numbered intent/receipt pairs. Parsed equal/prefix copies dedup against reachable authority; conflicts/missing authority block. Index is derived only.
2. Supported authority: original verified sole main plus separate canonical record-write attestation; new/unproven recoverer only reads/reconstructs. No ordinary-Run exclusive takeover claim or invented scheduling mechanism. Existing Run may bind at initialization; initially null Run derives from applied run-create receipt without editing binding.
3. Deterministic dialogue: one supplement reserved by intent even with lost receipt, original 30-minute budget retained. Timely posted or verified complete in-budget observation may establish reply validity; late/unknown/wrong identities fail. Late read preserves/enters pause even if crash left stale `paused=false`; reopening requires Owner resume or an actually timely valid revision decision.
4. End-session guard: pause/awaiting review/unknown authority/failed integration only saves handoff. No completed/archive/finish-work completion path until declared mode exits pass. Pure gate rejects pause and unresolved effects.
5. Premature Issue close: inspect closing keywords and server associations, handle only with authority/readback; unknown/unsafe blocks production merge. Explicit close follows actual successful integration. Branch-only always refuses merge even if other flags are true.
6. Usable path: seven-stage entry, three concise templates, sole dialogue/recovery references and documented read/write Python API. No decorative Issue/PR requirement. Freeze delivered hashes separately from later review/accounting evidence.

## Source and freeze evidence

[implementation-file-hashes.json](implementation-file-hashes.json) freezes ten exact-file SHA-256 entries (nine changed delivery files plus unchanged openai.yaml) at **2026-10-05T12:50:32.287201Z / 2026-10-05T20:50:32.287201+08:00**, with actual Python executable SHA-256. Candidate commit is explicitly null: main must commit/pin it, not treat the dirty source base as a reviewed candidate. All manifest hashes were read back against current bytes. Later evidence commits must prove these delivery bytes unchanged and list their diff; this report is not an extension of acceptance to new implementation.

Tools observed: Python **3.14.8**, Orca **1.4.220**. No .NET/Unity binary exists for this change (N/A); Python interpreter identity and both script hashes are in the manifest. Control timestamps are explicitly synthetic UTC, while report/freeze observation timestamps are actual UTC with Asia/Shanghai conversion; no dot server posted time was fabricated.

Git and Orca enumerated three AbilityKit trees, local host with no omitted host IDs; [worker-worktree-inventory.json](worker-worktree-inventory.json) and main's [source-search](source-search.md) preserve routing. Read relevant AGENTS/archived task/actual prior review in `D:/MyWorkTree/AbilityKit`, and stopped tree's AGENTS/task/PRD/design/implement/research stop record plus actual Git changes. Main remains dirty only in existing journal/review work observed at entry; stopped issue6 tree was clean at `15e79fefd5ba1d9294491122c91df286ef94e2f8`, task blocked and example changes preserved/isolated. No other-tree writes or cleanup occurred.

## Actual commands and results

Run from repository root. Commands use the named output directory once; harness refuses overwrite of prior results.

```powershell
python -B .agents/skills/cooking-dot-workflow/scripts/test_controls.py --output-dir .trellis/tasks/10-05-cooking-dot-skill-polish/research/controls-run-01
python -B .agents/skills/cooking-dot-workflow/scripts/test_controls.py --output-dir .trellis/tasks/10-05-cooking-dot-skill-polish/research/controls-run-02
python -B .agents/skills/cooking-dot-workflow/scripts/test_controls.py --output-dir .trellis/tasks/10-05-cooking-dot-skill-polish/research/controls-run-03
python -B .agents/skills/cooking-dot-workflow/scripts/test_controls.py --output-dir .trellis/tasks/10-05-cooking-dot-skill-polish/research/controls-run-04
python -B .trellis/tasks/10-05-cooking-dot-skill-polish/research/source-and-structure-check.py
git diff --check -- .agents/skills/cooking-dot-workflow .trellis/spec/abilitykit/supervised-issue-delivery.md
python -B C:/Users/Administrator/AppData/Roaming/orca/codex-accounts/81627c2b-efc2-4498-aeb6-64c652c0acab/home/skills/.system/skill-creator/scripts/quick_validate.py .agents/skills/cooking-dot-workflow
```

| Check | Actual exit and coverage | Result |
| --- | --- | --- |
| Controls run 01 | 0; 90 observable assertions, 0 unexpected failures; pre-timing supplement/intermediate source | Passed at its stated intermediate envelope |
| Controls run 02 | 0; 113 assertions, 0 failures; timing supplement and lost-supplement child | Passed intermediate envelope |
| Controls run 03 | 0; 117 assertions, 0 failures; durable timing subprocess and late revision guard | Passed intermediate envelope |
| **Controls run 04** | **0; 122 expected/actual assertions, 0 failures, 12 actual subprocess executions** | **Passed final helper/test bytes** |
| Scoped structure/source inspection | 0; 11 checks, 19 local link destinations, both Python ASTs, unchanged SOP outside opt-in section and unchanged AGENTS/openai | Passed declared document/syntax/scope envelope |
| Focused `git diff --check` | 0; changed delivery paths | Passed |
| Original `quick_validate.py` | **1**, `ModuleNotFoundError: No module named 'yaml'`; no checks executed | **Blocked: missing PyYAML**, original failure retained; coordinator will separately run original validator with its approved no-install method |

Raw final controls: [controls-run-04/controls-output.json](controls-run-04/controls-output.json), including every expected/actual value and subprocess argv/exit/stdout/stderr; [controls-run-04/controls-fixtures.json](controls-run-04/controls-fixtures.json) retains all final fixture bytes including intentionally damaged/conflicting records. Intermediate runs 01–03 retain the corresponding two files. Console logs are `controls-run-01-console.txt` through `controls-run-04-console.txt`. These are **assertion counts, not 122 product tests**.

Structural raw output is [source-and-structure-output-final.json](source-and-structure-output-final.json); [source-and-structure-output-final-utf8.json](source-and-structure-output-final-utf8.json) is its decoded UTF-8 copy. Earlier `source-and-structure-output.json`, `diff-check-output.txt`, final `diff-check-output-final.txt`, [validation-exits.json](validation-exits.json), [validation-exits-final.json](validation-exits-final.json) and [quick-validate-output.txt](quick-validate-output.txt) preserve actual outputs/exits. Windows PowerShell stdout redirects produced UTF-16 originals; do not parse them as UTF-8. Skill flow/record JSON and Python-written control JSON are UTF-8.

## Observable controls, expected = actual

| Control | Observed outcome |
| --- | --- |
| Pre-effect child crash | Self exit **71**, durable intent discovered by actual subprocess read; ledger count **0**, unresolved before/after reconciliation **1/0** |
| Effect applied / receipt lost | Self exit **72**, actual subprocess queries unique mock operation, recovers receipt; effect invocation count remains **1**, no second application |
| Receipt landed / index stale | Self exit **73**, persisted index last_seq **0**, actual subprocess reconstruction **1**, unresolved **0**, invocation count **1** |
| Corrupt index | Actual subprocess scan reconstructs last_seq **2**; read-only inspection preserves corrupt shared index bytes; separately authorized writer restores derived index |
| Old/conflicting copies | Same flow dedups to **one** group; old subset identified; copy index write refused. Valid conflicting same-op bytes refuse election |
| Unknown receipt and record faults | Cannot overwrite receipt or start later external intent; new reconcile pair resolves it. Gaps, unsupported version/wrong flow/op/reference and contradictory action/request/Dispatch receipt fields refused |
| Run identity | Applied run-create receipt derives effective Run; immutable binding unchanged. Contradictory Run/caller identity refused |
| Reply timing/identity | Ambiguity, old request/SHA, wrong conversation/boundary, truly late and unsupported time do not accept; reliable timely late-read remains technically valid but cannot continue |
| Approved null posted UTC rule | Complete verified observation within deadline accepts timely upper-bound basis with null posted UTC; fragment-first, first full after deadline, clock jump/cross-deadline uncertainty and unverifiable proof refused |
| Earlier durable complete proof | Full/prior snapshots plus request/reply persisted; **actual subprocess read** adopts timely evidence after restore but returns can_apply **false**, pause preserved |
| Stale pause flag after expiry | Request paused=false plus timely posted reply read next day gives technical validity **true**, can_apply **false**, pause_required/preserve_pause **true**; late-read revision cannot refresh budget |
| Lost supplement receipt | Child self exit **74** after supplement intent/readback, no receipt; persisted unresolved count **1**, supplement eligibility **false**; generating/unknown/deadline also refused |
| Paused session finish | Pure `delivery_allowed` returns **false** even with all other branch exits; unresolved effects/wrong SHA/changed hashes/missing pilot or worker accounting also refuse |
| Missing exclusive recovery authority | Different/new session, unverified sole coordinator or absent write authorization returns dispatch/mutation eligibility **false** and rejects canonical record **and index** writes before file changes; all shared bytes identical |
| Production predicates | Branch-only refuses merge, automatic-close/unknown association/missing authority blocks premerge; failed integration prevents completion. Positive production predicate is pure simulation only |

The four deliberate nonzero child exits are expected fault controls, not unexpected suite failures. No real agent/worker was killed. All side-effect reconciliation is **local mock simulation**, not proof of GitHub or Orca idempotency. Fresh-process reads are **actual subprocess reads**, never described as Orca coordinator takeover. The archived snapshots support reinspection; they are not promoted canonical flow records for a live workflow.

## Failures and unresolved limits retained

- Entry `get_context.py --mode phase` returned `Phase Index section not found in workflow.md`; no generic workflow fix attempted.
- Initial authoring patch was rejected: `apply_patch verification failed: invalid patch: multiple operations target .../SKILL.md`; no partial edit applied. Subsequent scoped edits succeeded.
- Early `rg` searches with wildcard directory operands were rejected with Windows invalid path error; corrected to `rg --files` on actual roots. An attempted main archive filename `research/prior-dot-review.md` was absent; discovered actual `research/dot-review.md`, read it and confirmed provenance. These were read-only discovery failures.
- First freeze extraction decoded PowerShell UTF-16 output as UTF-8 and failed with `UnicodeDecodeError ... byte 0xff ...`; created no manifest. Corrected explicit BOM decoding, retained raw output and produced UTF-8 copy before successful manifest/readback. Earlier premature freeze-status message was corrected to coordinator with actual successful receipt.
- Original missing-PyYAML failure is preserved, not overwritten or relabeled Passed. Scoped stdlib inspection is not a general YAML validation substitute.

| Exit/capability | Current evidence status |
| --- | --- |
| Skill-local record/timing/gate controls | Passed isolated process-crash/pure-predicate envelope |
| Manual-load bounded live pilot / pilot independent acceptance | **NotRun by this worker**; main will use frozen skill and separate bounded pilot worker |
| Final exact candidate SHA acceptance | **NotRun/pending main and dot**; no source commit created here |
| Cross-main ordinary-Run exclusive takeover | **Blocked**, no positive exclusive runtime contract proven |
| Real concurrent recoverers / real coordinator session recovery | **NotRun**; local refusal controls are not a concurrency authority proof |
| Actual Trellis finish/archive command during pause | **NotRun**; documented guard/pure rejection verified, generic lifecycle unchanged |
| Real production merge/postmerge/GitHub automatic Issue close/CI | **NotRun**, not authorized in this branch-only task |
| Product/.NET/Unity/LAN/performance gates | **N/A to this skill implementation**, not claimed passed |

## Exact task-local evidence files created by this worker

Under `.trellis/tasks/10-05-cooking-dot-skill-polish/research/` only:

- `implementation-report.md`, `implementation-file-hashes.json`, `worker-worktree-inventory.json`.
- `source-and-structure-check.py`, `source-and-structure-output.json`, `source-and-structure-output-final.json`, `source-and-structure-output-final-utf8.json`.
- `quick-validate-output.txt`, `diff-check-output.txt`, `diff-check-output-final.txt`, `validation-exits.json`, `validation-exits-final.json`.
- `controls-run-01-console.txt`, `controls-run-02-console.txt`, `controls-run-03-console.txt`, `controls-run-04-console.txt`.
- `controls-run-01/controls-output.json`, `controls-run-01/controls-fixtures.json`.
- `controls-run-02/controls-output.json`, `controls-run-02/controls-fixtures.json`.
- `controls-run-03/controls-output.json`, `controls-run-03/controls-fixtures.json`.
- `controls-run-04/controls-output.json`, `controls-run-04/controls-fixtures.json`.

These evidence files do not mutate main-owned plans/dialogue/Run accounting. After worker_done this terminal idles and remains available under approved awaiting-review retention; main owns settlement/retention and the next bounded Dispatch.
