# Operator handoff: one bounded Cooking documentation pilot

Use this example with [recovery](recovery.md), [dialogue](dot-dialogue.md), and the [checkpoint](../assets/checkpoint-operation.md), [request](../assets/dot-request.md), and [decision](../assets/dot-decision.md) templates. Those contracts remain authoritative. This document shows one normal branch-only handoff and one stopped diagnostic handoff, not a new runner or an authorization mechanism.

## Read the evidence boundary first

| Category | What this example contains |
| --- | --- |
| Actually observed | Existing flow, repaired source/manual loading, original-coordinator Orca dispatch and turn-start, push/readback failure and read-only reconciliation, worker document checks described in the pilot report |
| Teaching inputs | The `TEACHING_*` fixture and `<NEXT_…>` placeholders below; neither identifies a live coordinator, Run, candidate, or approved action |
| Still pending at writing | Main observation of this worker's completion, independent artifact inspection, new candidate freeze/publication, exact-SHA final dot acceptance and worker accounting |
| Outside demonstrated coverage | Automatic host invocation, actual coordinator crash/exclusive takeover, production merge/postmerge/automatic Issue closure, product runtime tests |

The loaded repaired source is [4a4f36e6435f1315ddcec1d462c924e16b44ec5f](https://github.com/Kakusya/AbilityKit/commit/4a4f36e6435f1315ddcec1d462c924e16b44ec5f). The full dot pilot plan is [AK-CDF-LIVE-PILOT-PLAN-01 at that immutable source](https://github.com/Kakusya/AbilityKit/blob/4a4f36e6435f1315ddcec1d462c924e16b44ec5f/.trellis/tasks/10-05-cooking-dot-skill-polish/research/dot-pilot-plan.md); its planning review concerns **32b400a5c6d375be366faa606e867ef68037cfa8**, not a final acceptance of the repaired source or these new documents. Main reran the original independent auditor's contract expectations on the repaired SHA (93/93 local assertions); this is not a fresh Orca auditor review. The worker read that report, rather than claiming to have performed that audit.

Task evidence links in this reference use full Git SHAs so they survive task archive. Current uncommitted receipts are identified below by exact locators, not misleading links to a commit that does not contain them. Main must publish them and record their actual immutable commit links in its subsequent evidence envelope. `<NEXT_EVIDENCE_COMMIT_SHA>` is an explicit unfilled handoff field, never an assertion that publication already happened.

## Normal case: original main, branch-only artifact handoff

### Find the canonical checkpoint and correlate identities

The fixed entry is `<task>/research/cooking-dot-flow/flow.json`. Read the [existing immutable binding](https://github.com/Kakusya/AbilityKit/blob/4a4f36e6435f1315ddcec1d462c924e16b44ec5f/.trellis/tasks/10-05-cooking-dot-skill-polish/research/cooking-dot-flow/flow.json), then scan the live declared root. The derived `index.json` is a convenience; reconstruction scans immutable records even when the index is stale. This observed binding has the full version-1 field set:

```json
{
  "version": 1,
  "skill": "cooking-dot-workflow",
  "flow_id": "a2c3dd65-4a91-43f6-9c33-ad891f7563c4",
  "canonical_root": "C:\\Users\\Administrator\\orca\\workspaces\\AbilityKit\\cooking-dot-skill-polish\\.trellis\\tasks\\10-05-cooking-dot-skill-polish\\research\\cooking-dot-flow",
  "execution_host": "local",
  "worktree_id": "aaa97e0d-5026-4e5c-95f8-bb420dcb4c65::C:/Users/Administrator/orca/workspaces/AbilityKit/cooking-dot-skill-polish",
  "task_path": ".trellis/tasks/10-05-cooking-dot-skill-polish",
  "repository": "https://github.com/Kakusya/AbilityKit",
  "branch": "Kakusya/cooking-dot-skill-polish",
  "target_branch": "Kakusya/cooking-dot-skill-polish",
  "delivery_mode": "branch-only",
  "original_session": "codex_01a10a3d-c893-70a3-af86-173caf5351a8",
  "original_terminal": "term_4d2338db-ef10-45a0-9316-90733d30e4cb",
  "run_id": "run_840202cc5621",
  "approval_ref": "evidence/approval.md",
  "created_utc": "2026-10-05T13:01:19.886030Z"
}
```

Run these read-only commands from the repository root after locating the reachable canonical root; do not substitute a convenient copy for it:

```powershell
git worktree list --porcelain
orca worktree list --repo id:aaa97e0d-5026-4e5c-95f8-bb420dcb4c65 --json
python -B .agents/skills/cooking-dot-workflow/scripts/records.py inspect .trellis/tasks/10-05-cooking-dot-skill-polish/research/cooking-dot-flow
# If discovery finds a real evidence copy, pass BOTH resolved roots:
# python -B .agents/skills/cooking-dot-workflow/scripts/records.py copies <CANONICAL_ROOT> <DISCOVERED_COPY_ROOT>
```

At the writing checkpoint, reconstruction returned `effective_run_id: run_840202cc5621`, `last_seq: 6`, `unresolved: []`, plus twelve record fingerprints. No absent/unknown operation receipt remained. Worker business completion and candidate acceptance remained separate pending facts.

| Correlation | Actual observation / next evidence |
| --- | --- |
| Trellis task | `10-05-cooking-dot-skill-polish`, `in_progress`; session bootstrap has no active task pointer, so the dispatch's explicit owning task is used |
| Loaded source | `4a4f36e6435f1315ddcec1d462c924e16b44ec5f`; three new document edits require a different candidate before final review |
| Original main / Run | Binding above; main's saved live Run proof reports the same coordinator handle and `consumer_generation: 1`; these observations do not confer authority |
| Current worker | `term_04f2f37b-9f35-46d6-81cc-b26500d3c821` on the same managed worktree |
| Current Orca Task / Dispatch | `task_0e2318dcd203` / `ctx_31a62bbd882d`, both under `run_840202cc5621`; do not confuse the Orca Task with the Trellis directory |
| Start operation | `a2c3dd65-4a91-43f6-9c33-ad891f7563c4:000006`, `action: worker-start`, applied receipt |
| Raw start proof | `research/pilot-worker-start.json` and identical canonical `evidence/pilot-worker-start.json`: `prompt.stages` includes `input_accepted` and `turn_started`, `turnStart: observed` |
| Launch observation | Requested/effective agent `codex`; model and effort are null in the receipt, so no actual model/effort is asserted or changed |
| Current writer rights | This dispatched worker may edit only the three documents. Only main may verify its original sole identity and independent record-write qualification and then record canonical operations |
| Review identity | Planning request `AK-CDF-LIVE-PILOT-PLAN-01`, source `32b400a5c6d375be366faa606e867ef68037cfa8`; final request and new full candidate SHA are `<NEXT_FINAL_REQUEST_ID>` / `<NEXT_CANDIDATE_SHA>` |

`run-use`, historical generation, copied owner/session/terminal fields, and helper booleans never prove sole authority. Before each dependent effect, original main must verify current runtime/session/terminal identity, actual Run coordinator and active writers/Dispatches on the reachable host, and Owner-scoped canonical record-write permission outside the helper, preserving the raw evidence. Unknowns block that effect. A matching binding is only a comparison target.

Discovery observed three Git/Orca trees: this tree, `D:/MyWorkTree/AbilityKit` (`docs/supervised-issue-sop`), and `issue6-test-gate-results`. Related AGENTS/task/research and diffs were read. The issue6 task is blocked with Cooking-only stop records; its broad example changes remain isolated. None of those trees may be edited, cleaned, or resumed by this worker. Equal/subset flow copies are evidence-only; conflicting/newer records or unavailable canonical host block mutation rather than electing a new authority.

### What actually happened, and where the normal path stops

1. Main manually loaded the repaired project-path SKILL and conditional references at the SHA above, checked the freeze manifest, and applied the unchanged pilot plan. `research/pilot-repaired-manual-load.json` and `research/repair-freeze-receipt.json` record the source and hashes; disk presence is not automatic host invocation.
2. Main persisted/read back push intent 000005. Push exited 0; immediate readback exited 128. Main stopped dependent effects, retained `evidence/publish-repair-raw.json`, and read-only verified `refs/heads/Kakusya/cooking-dot-skill-polish` at exactly the repaired SHA. `evidence/publish-repair-recovery.json` records lookup exit 0 and the exact ref. Main recovered the previously **missing** 000005 receipt without repushing. Earlier readback stderr was not captured; its cause remains unknown. An already-written `unknown` receipt would instead require a new `reconcile` pair, never replacement.
3. Main persisted/read back 000006 before the bounded dispatch. Its applied receipt records matching Run/Task/Dispatch and raw accepted/start proof. The worker received the approved filenames, stop boundary, full dot plan, and repaired load source. This operation proves startup only, not worker success or candidate acceptance.
4. The worker reads inbox at natural checkpoints, sends five-minute heartbeats, preserves others' edits, and writes only `references/operator-example.md`, one conditional link in `SKILL.md`, and owning `research/pilot-report.md`. Questions use the live preamble's `orca orchestration ask`; one `worker_done` uses its live Task and Dispatch, outcome and three-sentence body, then the worker idles. Do not replay this dispatch or copy its settled IDs for later work.
5. **Next, main-owned, pending:** main observes and saves the actual `worker_done` for this Task/Dispatch, inspects the three-file diff and validation independently, and accounts for this worker under approved retention/reuse. Main freezes a new commit and delivered-file SHA-256 manifest, publishes/readbacks that exact candidate, and sends a uniquely identified final-review request. Final acceptance, branch-only completion, and any later archive remain unfilled exits.

The real ending represented by this example is **artifact complete, awaiting final candidate review**. At report writing there is no future completion-message ID to cite. Main's later completion observation belongs to main's subsequent evidence; the worker report must not manufacture it.

Preserve the [failed preliminary API call](https://github.com/Kakusya/AbilityKit/blob/4a4f36e6435f1315ddcec1d462c924e16b44ec5f/.trellis/tasks/10-05-cooking-dot-skill-polish/research/pilot-init-failure.json), [rejected preflight operation 000003](https://github.com/Kakusya/AbilityKit/blob/4a4f36e6435f1315ddcec1d462c924e16b44ec5f/.trellis/tasks/10-05-cooking-dot-skill-polish/research/cooking-dot-flow/records/000003.receipt.json), and original audit/control failures. Later correction does not convert these historical failures into Passed. Main's repaired exact-source check is a separately bounded result.

### Public record API: useful teaching data, never a live reset

This snippet supplies every binding/intent/receipt field. It is a **non-live isolated fixture** illustrating the normal record order, not another live case. A validation harness supplies a temporary `nonlive-…` root, existing `evidence/approval.txt`, `authority.txt`, `payload.txt`, `raw.txt`, and synthetic authority. No snippet creates permission or dispatches an external action. The root-name guard only prevents accidental example use; it is not an authority test.

```python
from pathlib import Path
import hashlib
import json
import sys

sys.path.insert(0, ".agents/skills/cooking-dot-workflow/scripts")
from records import (WriteAuthority, initialize, append, fingerprint,
                     write_index, reconstruct, copies)

TEACHING_FLOW_ID = "11111111-1111-4111-8111-111111111111"
TEACHING_SESSION = "TEACHING_ONLY_SESSION"
TEACHING_TERMINAL = "TEACHING_ONLY_TERMINAL"
TEACHING_SHA = "1" * 40

def exercise_teaching_api(root, independently_verified_authority):
    root = Path(root).resolve()
    if not root.name.startswith("nonlive-"):
        raise ValueError("Teaching fixture only; never initialize the live flow")
    # In a live caller, independently_verified_authority must come from
    # current external identity/sole-writer/permission verification and raw
    # evidence, NOT from copying flow fields or setting booleans to true.
    authority = independently_verified_authority
    flow = {
        "version": 1, "skill": "cooking-dot-workflow",
        "flow_id": TEACHING_FLOW_ID, "canonical_root": str(root),
        "execution_host": "TEACHING_ONLY_HOST",
        "worktree_id": "TEACHING_ONLY_WORKTREE",
        "task_path": ".trellis/tasks/TEACHING_ONLY_TASK",
        "repository": "https://example.invalid/TEACHING_ONLY",
        "branch": "TEACHING_ONLY_BRANCH", "target_branch": "TEACHING_ONLY_BRANCH",
        "delivery_mode": "branch-only", "original_session": TEACHING_SESSION,
        "original_terminal": TEACHING_TERMINAL, "run_id": "TEACHING_ONLY_RUN",
        "approval_ref": "evidence/approval.txt",
        "created_utc": "2000-01-01T00:00:00Z",
    }
    initialize(root, flow, authority)  # First creation of THIS isolated fixture.
    # Live a2c3dd65-… is already initialized: never rerun initialize to reset it.
    intent = {
        "version": 1, "flow_id": TEACHING_FLOW_ID, "seq": 1,
        "op_id": f"{TEACHING_FLOW_ID}:000001", "kind": "intent",
        "recorded_utc": "2000-01-01T00:00:01Z",
        "action": "worker-start", "target": "TEACHING_ONLY_WORKTREE",
        "authorization_ref": "evidence/approval.txt",
        "payload_ref": "evidence/payload.txt",
        "preconditions": {
            "run_id": "TEACHING_ONLY_RUN", "candidate_sha": TEACHING_SHA,
            "execution_host": "TEACHING_ONLY_HOST",
        },
    }
    append(root, intent, authority)  # Validates and reads back immutable intent.
    # Live main performs the approved effect ONLY after this succeeds;
    # teaching performs no effect, and raw.txt is labeled simulation.
    receipt = {
        "version": 1, "flow_id": TEACHING_FLOW_ID, "seq": 1,
        "op_id": intent["op_id"], "kind": "receipt",
        "recorded_utc": "2000-01-01T00:00:02Z",
        "intent_hash": fingerprint(intent), "outcome": "applied",
        "raw_ref": "evidence/raw.txt",
        "result": {
            "action": "worker-start", "command": "TEACHING_ONLY_NO_EXECUTION",
            "exit": 0, "run_id": "TEACHING_ONLY_RUN",
            "candidate_sha": TEACHING_SHA, "execution_host": "TEACHING_ONLY_HOST",
            "task_id": "TEACHING_ONLY_ORCA_TASK",
            "dispatch_id": "TEACHING_ONLY_DISPATCH",
            "stages": ["input_accepted", "turn_started"], "replayed": False,
            "postconditions": {"simulation": True, "completion_observed": False},
        },
    }
    # Exact algorithm hashes parsed intent JSON, not pretty file bytes/BOM/newlines.
    expected = hashlib.sha256(json.dumps(
        intent, sort_keys=True, separators=(",", ":"),
        ensure_ascii=False, allow_nan=False).encode("utf-8")).hexdigest()
    assert receipt["intent_hash"] == expected
    append(root, receipt, authority)  # Immutable receipt validation/readback.
    index = write_index(root, authority)  # Derived only; no task completion.
    assert index == reconstruct(root)  # Read-only, no authority argument.
    return flow, intent, receipt, index
```

`WriteAuthority(session, terminal, sole_coordinator_verified, record_write_authorized, evidence_ref)` is a caller attestation backed by existing evidence, not a helper-acquired lease. All five values must describe independently verified facts for live writing. A simulator may exercise gates with synthetic facts but cannot establish live authority. `initialize(root, flow, authority)` is only first creation/identical retry; `append(root, record, authority)` preserves immutable records; `fingerprint(value)` hashes the canonical parsed JSON; `write_index(root, authority)` requires authority even if records are readable; `reconstruct(root)` and `copies([root, copy_root])` are read-only. No private atomic API is called.

In live use, the order is existing valid evidence → independently verified authority → append intent/readback → main's approved action → raw actual result → append receipt/readback → derived index/readback. Missing receipt blocks later effects until main read-only reconciles the same operation. A persisted `unknown` receipt stays immutable: new intent uses `action: reconcile`, next contiguous seq/op ID, `preconditions.resolves: <OLD_OP_ID>`; its applied/not-applied receipt uses `result.resolves: <OLD_OP_ID>` and existing raw external verification. Reconciliation records the earlier result, not another external effect. Wrong Run/Task/Dispatch/candidate, absent evidence, or an unresolved predecessor blocks further ordinary intents.

### Complete handoff fields for the next original-main step

These fields instantiate the checkpoint template without claiming future completion. Each `<NEXT_…>` must be filled from actual evidence by main. Local locators below are relative to the owning task unless prefixed `evidence/`, which is canonical-root-relative.

| Checkpoint field | Observed value or explicitly pending evidence |
| --- | --- |
| Source / dirty | HEAD `4a4f36e6435f1315ddcec1d462c924e16b44ec5f`, branch above; dirty main-owned records/evidence present before dispatch, plus these three worker documents at handoff; exact final dirty inventory belongs to main |
| Invocation / approval / consumers | `evidence/approval.md`; Owner approved manual-load branch-only Cooking pilot; SKILL link enables discovery, operator reference enables handoff, pilot report preserves actual validation; all consumed by Cooking coordinators/workers |
| Host / Run | `local` / `run_840202cc5621`, declared canonical root above; fresh live exclusive-original-main verification is required before writes |
| Worker identity / owner / assignment | Current Task/Dispatch/terminal above, worker owns only the three documents; no commits/pushes, helper edits, nested workers, Run rebinding, cleanup or archive |
| Model / effort / startup | Receipt null/null, actual values unknown; accepted input and observed turn-start supported; no override inferred |
| Liveness / raw evidence / accounting | Worker is active while writing, reports heartbeat/inbox checks; `research/pilot-worker-start.json`; `<NEXT_DONE_MESSAGE_AND_RAW_REF>` and `<NEXT_WORKER_RETENTION_OR_REUSE_RECEIPT>` remain pending; existing permission allows retention, not closure |
| Candidate / delivered SHA-256 map | Loaded repaired source above; `<NEXT_CANDIDATE_SHA>` and `<NEXT_DELIVERED_FILE_HASH_MAP>` must include the changed SKILL and operator document; report/evidence diff must be declared separately; no final acceptance map exists yet |
| Checks / count / commands / exits / raw / binary | Worker results in `research/pilot-report.md`; main must inspect/reproduce scoped checks and bind final source; Python fixture is simulation, no product binary; `<NEXT_INDEPENDENT_CHECK_ENVELOPE>` pending |
| Request / budget / quota | Resolved planning ID above, start `2026-10-05T13:07:54.386249Z`, original deadline `2026-10-05T13:37:54.386249Z`, supplement not reserved, observed plan acceptance `2026-10-05T13:19:10.605689Z`; preserve `research/pilot-plan-wait.json`. These are history, not a reusable final-review budget |
| Unresolved operations / business exits | Scan at last_seq 6: `[]`; no completion observation, new candidate acceptance or final worker accounting at report writing |
| Pause / next bounded action / owner | Artifact awaiting review; if main cannot continue, it records explicit paused fact and this handoff without completing task. Original main owns next observation/check/freeze/review; this worker stops after exactly one completion message |
| Evidence publication | Current new receipts/report have no immutable commit yet; `<NEXT_EVIDENCE_COMMIT_SHA>` and full-SHA task evidence links to be supplied by main |

For the next review, main uses the full request template, not the resolved planning request's timestamps. The following form is **pending teaching text, not a posted request**:

```text
Type: final-review
Flow ID / request ID: a2c3dd65-4a91-43f6-9c33-ad891f7563c4 / <NEXT_FINAL_REQUEST_ID>
Parent + reopening/revision authority: AK-CDF-LIVE-PILOT-PLAN-01; accepted plan,
  approved next final review after artifact/check/freeze, not an expired-request restart
Repository / full published SHA / link: https://github.com/Kakusya/AbilityKit /
  <NEXT_CANDIDATE_SHA> / https://github.com/Kakusya/AbilityKit/commit/<NEXT_CANDIDATE_SHA>
Scope/consumers/mode/excluded exits: approved Cooking workflow docs, coordinators/
  workers, branch-only; no master merge, product changes, release, cleanup
Decision / conflicts: explicit accept-candidate or revisions; preserve old failures
Plans/raw/checks/delivered hashes: <NEXT_IMMUTABLE_EVIDENCE_LINKS_AND_HASH_MAP>
Conversation/boundary/prior snapshot/fingerprint: <NEXT_VERIFIED_CONVERSATION_AND_BOUNDARY>
  / <NEXT_PRIOR_SNAPSHOT_REF> / <NEXT_PAYLOAD_FINGERPRINT>
Posted request identity: null until actual send/readback or reconciliation
wait_started_utc / deadline_utc: <NEXT_ACTUAL_START_UTC> / <START_PLUS_30_MINUTES>
Paused fact / timeout or resume provenance: <NEXT_ACTUAL_PAUSE_FACT_AND_PROVENANCE>
Supplement intent: none initially; one shared quota, reserved by intent
Identify this request and full SHA; converse/read commits only
```

The resulting decision record must fill **all** decision-template fields: flow/request/type; verified conversation/reply/full raw ref; full reviewed SHA/explicit decision; posted UTC (null when unknown)/reliability/source and observed UTC; timing basis/trusted upper UTC/uncertainty/time source/optional lower UTC; complete/not-generating/full and prior snapshots/verified boundary; timely/timely-late-read/late/unknown classification; original deadline/supplement intent+receipt/prior timeout+pause refs; accepted scope and delivered hashes/blockers/conflicts; main scope/evidence finding and next action/authority; new linked request and reopening/revision provenance if needed. Until that reply exists, **no accept-candidate or archive decision is prefilled**.

If awaiting dot, follow the original 30-minute budget and 30/60/120/300-second polling schedule in dialogue, splitting waits into at most 60-second slices. No new dispatch or technical/integration/merge decision during the wait. At timeout main records original budget/quota, failures, workers and next evidence, then ends its turn paused. A timely late-read decision can resolve the old technical question while preserving pause; it never grants cross-main authority or refreshes the budget.

Only after actual exact-candidate acceptance, unchanged delivered hashes, scoped checks/pilot/independent inspection, verified branch push and worker accounting may main evaluate branch-only completion. Merge, postmerge and GitHub auto-close remain NotRun. Neither a passed helper predicate nor a completed worker authorizes archive by itself.

## Stopped case: another conversation reads this same flow

This is an **illustrative diagnostic recovery**, not an observed crash or real takeover. `<NEXT_DIAGNOSTIC_SESSION>` reads the same six-operation snapshot above. Its session/terminal differ from original main, and no supported actual runtime contract proves exclusive normal-Run transfer. A copy carrying original owner fields changes nothing.

The reader may enumerate trees/hosts, read AGENTS and complete task artifacts, run `inspect`/`copies`, call `reconstruct(root)` in memory, and preserve a diagnostic note in its separately authorized location. It must not write/repair canonical `index.json`, append shared records, use `run-use` to claim takeover, dispatch a replacement worker, reset this flow, create a replacement UUID/Run, or archive. It does not use the fixture's synthetic authority.

```python
# Read-only diagnostic fragment; root is the discovered reachable declared root.
from records import reconstruct, copies
diagnostic = reconstruct(root)
# Only if a real copy was discovered:
# copy_classes = copies([root, discovered_copy_root])
# No initialize / append / write_index here, even for a stale index.
```

The diagnostic handoff can be concise and exact:

```text
Category: hypothetical stopped recovery from this document's writing snapshot
Status: Blocked for shared writes/dispatch; read-only diagnosis supported
Reader: <NEXT_DIAGNOSTIC_SESSION>/<NEXT_DIAGNOSTIC_TERMINAL>, not original main
Canonical: exact flow.json canonical_root above, local host; copies are evidence
Flow/Run/source: a2c3dd65-4a91-43f6-9c33-ad891f7563c4 / run_840202cc5621 /
  loaded 4a4f36e6435f1315ddcec1d462c924e16b44ec5f, not a new accepted candidate
Last reliable operation: 000006 applied worker-start with accepted + turn-start
Missing/unknown operation receipts at scan: none, last_seq 6, unresolved []
Worker: task_0e2318dcd203 / ctx_31a62bbd882d /
  term_04f2f37b-9f35-46d6-81cc-b26500d3c821; completion/liveness now unknown
Pending business evidence: matching actual worker_done + outcome/report,
  independent artifact check, new frozen candidate/hashes, final dot decision,
  branch publication/readback and individual worker accounting
Preserved failures: preliminary API failure, rejected dispatch preflight,
  original audit failures, push-exit0/readback-exit128 with recovered missing receipt
Budget: preserve resolved planning start/deadline above; next review budget absent;
  if a later real request exists, read its persisted original budget/timeout/quota
Pause: this diagnostic session ends stopped; it does not complete the requirement
Next owner: original verified sole main, not this reader
```

The next required evidence is concrete:

1. Original main must return with fresh independently verified session/terminal, live Run coordinator/host and writer inventory, plus approved canonical record-write qualification. Saved strings/booleans and silence cannot satisfy this. If it cannot return, a separately reviewed supported runtime exclusivity-transfer contract is required; this skill version does not implement one. Keep Blocked rather than improvising `run-use` or another Run.
2. Main must read actual current Task/Dispatch/worker/process facts and the completion inbox, preserving a matching `worker_done` outcome and report if found. Startup receipt alone is insufficient. Unknown writer liveness retains the worker; prove exit and individual accounting before any replacement dispatch. The diagnostic reader does not send this worker's lifecycle message or close its terminal.
3. Main must reconcile any operations discovered beyond this snapshot by their immutable intents and raw external facts before further effects. For 000005 retain original exit128 and read-only exact-ref recovery; do not repush. A new missing receipt needs authoritative lookup; an `unknown` receipt needs a new reconcile pair. Failed/incomplete lookup is not proof of absence.
4. Main must inspect actual document bytes, checks, candidate SHA/hashes and any later dot request/reply/deadline. No candidate acceptance is assumed. Preserve prior pause/timeout; an expired request requires the dialogue's explicit resume/reopen provenance before a linked new request, not a guessed fresh deadline.

If any required authority/effect fact remains unknown, the stopped session saves only its permitted diagnostic handoff and ends. Task remains unfinished; no `task.py archive`, no complete/archive finish-work path, no duplicate external effect. Session end is an evidence boundary, not product or branch delivery acceptance.
