# Durable records and diagnostic recovery

This is the sole discovery/authority/reconciliation contract. Trellis owns engineering status; these are evidence/derived pointers, not a scheduler, lease or completion database. Fault model: **process crash only**. Atomic replacement/readback does not prove OS/power-loss durability or external idempotency.

## Fixed entry and format (version 1)

One flow per task: `<task>/research/cooking-dot-flow/flow.json`, UTF-8 JSON object, `version: 1`, `skill: cooking-dot-workflow`. Generate UUID once as `flow_id`, persist before first effect; never derive from branch/handle/title. Archive preserves ID/frozen evidence; active canonical location is not silently relocated/recreated.

Immutable binding fields: `flow_id`, `canonical_root` (resolved absolute record directory), `execution_host`, `worktree_id`, `task_path` (repository-relative), `repository`, `branch`, `target_branch`, `delivery_mode`, `original_session`, `original_terminal`, `run_id` (verified existing Run or null), `approval_ref` (root-relative evidence), `created_utc`. When initially null, an `action: run-create` intent and applied receipt with `result.run_id` establish the effective Run; reconstruct derives `effective_run_id` without editing flow.json. Recover a lost run-create receipt on its original operation after authoritative lookup; a conflicting Run identity blocks. Following Run-bound intents carry `preconditions.run_id` matching the effective Run. Unknown observations are null with explanatory raw evidence; missing required identity/approval blocks writes. Runtime IDs are observations, never defaults. Approval evidence contains invocation, scoped consumers/acceptance and mode authorization.

```text
research/cooking-dot-flow/
  flow.json
  records/000001.intent.json
  records/000001.receipt.json
  records/000002.intent.json
  index.json                       # disposable derived pointers
  evidence/<unique-name>           # raw receipts/requests/decisions/checkpoints
```

Operation ID: `<flow_id>:<six-digit-sequence>`, contiguous from 000001, never reused for another payload. Every record has `version`, `flow_id`, `seq`, `op_id`, `kind`, `recorded_utc`. Intent adds `action`, exact `target`, `authorization_ref`, `payload_ref`, `preconditions` (candidate/request/Run/Task/Dispatch/host as relevant). Receipt adds `intent_hash` (SHA-256 of sorted compact UTF-8 intent JSON), `outcome` (`applied`, `not-applied`, `unknown`), `raw_ref`, `result` (command/exit/actual identities/postconditions/replay/stages). File refs are existing root-relative evidence paths without traversal/symlink escape. Store bounded nonsecret evidence, not credentials/unrestricted payloads.

Use [checkpoint template](../assets/checkpoint-operation.md) for binding/resource inventory/operation pair. Checkpoints capture real Git/Orca HEAD/dirty/hosts, workers' Task/Dispatch/owner/model/effort/turn-start/liveness/accounting, candidate/hashes, dot request and next bounded action. No completion field in derived index.

## Write order and privileges

1. Verify **original sole coordinator** and separately its canonical record-write authority; save live identity/Run evidence and approval ref. Owner field/task pointer/silence/timeout/`run-use` is not proof. New/unproven recoverer can only read and reconstruct in memory; no shared index repair before authority.
2. Write independent immutable intent via existing `.trellis/scripts/common/io.py` atomic utility, read back exact content. Only then external action. Failed checkpoint prevents effect. Evidence uses unique immutable names; preserve old failures/replies.
3. Save raw result and independent immutable receipt referencing intent hash; read back/validate. Uncertain result uses `unknown`, requiring read-only external reconciliation. Later reconciliation uses a **new operation pair**, `action: reconcile`, `preconditions.resolves` naming unresolved earlier op, and `result.resolves` matching it with `applied`/`not-applied`; never overwrite an unknown receipt. Only this local reconciliation recording may follow an unresolved predecessor.
4. Replace/read back derived index after receipt. Scan immutable records to rebuild stale/corrupt/missing index. Missing receipt means unresolved, **not** safe retry. No later external effect until all earlier uncertainty is authoritatively resolved. Intent absent from old index is still discovered.

Optional [helper](../scripts/records.py) validates identities/references/order, uses Trellis atomic I/O and pure wait/delivery predicates. CLI `inspect`/`copies` is **read-only**, never repairs. Write API requires separate `WriteAuthority` attestation backed by current live evidence: caller proves authority; helper cannot obtain/prove exclusivity. No Orca/GitHub/browser calls, effects, locks/background service. Controls: `python -B .agents/skills/cooking-dot-workflow/scripts/test_controls.py --output-dir <task-research-controls>`. Local mock reconciliation is simulation, not real Orca/GitHub idempotency.

From repository root, read with `python -B .agents/skills/cooking-dot-workflow/scripts/records.py inspect <records-root>` or `copies <root> <copy-root>`. For authorized recording, use the Python API below with real values from live verification and the template. There is deliberately no write/rebind/dispatch CLI. `WriteAuthority` is an attestation, not a newly acquired privilege or concurrency mechanism; caller must already have proven sole authority. The exists/atomic-replace sequence relies on that sole writer and does not claim safety against competing writers.

```python
import sys
sys.path.insert(0, ".agents/skills/cooking-dot-workflow/scripts")
from records import WriteAuthority, initialize, append, write_index, reconstruct, fingerprint

# root/flow/intent/receipt follow the checkpoint template; evidence refs already exist.
authority = WriteAuthority(session, terminal, sole_verified, record_write_authorized, live_evidence_ref)
initialize(root, flow, authority)       # once; identical retry only, never replace binding
append(root, intent, authority)        # validated immutable intent + readback
# Caller now performs the authorized effect, or reconciles an uncertain one read-only.
receipt["intent_hash"] = fingerprint(intent)
append(root, receipt, authority)       # actual raw evidence + readback
write_index(root, authority)           # optional derived pointer replacement
diagnostic = reconstruct(root)        # read-only; callable without any authority
```

Pure `reply_decision`/`supplement_allowed`/`reopen_allowed` consume request fields (`candidate_sha`, `type`, `flow_id`, `request_id`, `conversation_id`, `prior_message_boundary`, `request_message_id`, original budget, explicit `paused` fact). Reply adds `full`, `generating`, `decision`, `reviewed_sha`, `message_id`, matching conversation/request/flow, verified after-request/absent-at-boundary flags, `time_reliable`, `reply_posted_utc`. Optional `timing_proof` follows dialogue: matching identities/boundary, `verified`, `full`, `generating`, `full_snapshot_ref`, `prior_snapshot_ref`, `observed_utc`, trusted `time_source`, `clock_trusted`, `clock_jump: false`, nonnegative `uncertainty_ms`. Latest upper bound is computed, not guessed. Eligibility never grants execution/write rights; elapsed deadline rejects applying/continuing even with stale pause flag. `mutation_allowed` checks separately attested original identity/write qualification. `delivery_allowed` checks mode, explicit unpaused fact, matching candidate/accepted SHA, scope/independent/check/worker/hash evidence and mode exits; production needs premerge/merge/integration verification and required Issue close verification (`issue_required: false` means N/A). `premerge_allowed` checks explicit authorization/current head-base/accepted candidate/checks/scope/sole authority/inspected-clear closing links. Missing flags fail closed; booleans assert linked raw evidence, not proof generated by helper.

Gate input keys: `delivery_allowed` requires `mode`, `paused: false`, `candidate_sha`, `accepted_sha` and true `scope_checked`, `independent_check`, `workers_accounted`, `required_checks_passed`, `candidate_accepted`, `hashes_unchanged`, `effects_reconciled`. Branch-only adds true `branch_push_verified`, `required_pilot_passed`. Production adds true `premerge_verified`, `merge_verified`, `integration_passed`, plus explicit `issue_required` and true `issue_close_verified` when required. `premerge_allowed` accepts **only** `mode: production`, with true `authorized`, `head_base_current`, `candidate_accepted`, `required_checks_passed`, `scope_checked`, `sole_coordinator_verified`, `closing_links_inspected`, `closing_links_clear`, `effects_reconciled`. Branch-only cannot pass this merge gate even if all booleans are true. Unresolved effects cannot pass either completion or merge.

## Discover copies and reconcile

Enumerate Git/Orca worktrees/hosts; inspect relevant other trees' AGENTS/task/manifests/research/actual diffs, especially stopped tree named by progress. Search fixed entry across active/archived tasks. Legacy unversioned records need read-only provenance reconciliation, not replacement flow creation. Missing canonical entry/host visibility blocks writes. Dedup before unique unfinished flow selection; multiple distinct IDs need Owner selection.

Group by `flow_id`; immutable binding must match including canonical host/location. Equal parsed records dedup; strict subsets are old evidence copies, not another flow. Conflicting same seq/kind, changed binding, non-prefix histories or newer copy records missing from reachable canonical root block mutation. Preserve copies/identify missing evidence; index never elects a winner. Only declared canonical root is writable. Missing/corrupt authority does not promote a copy. Archive is evidence-only after completion: preserve archive routing receipt/original location, do not resume guessed-path writes.

Scan records validating version/flow/sequence/op ID/references/hash/time/receipt pairing; reject corrupt/unknown formats. Index is only `{version, flow_id, effective_run_id, last_seq, unresolved, record_hashes}`. Read complete task plans/manifests/research/latest applicable Issue before execution. Read actual remote/dot/Orca Run/Dispatch/process facts and discover fresh handles; never dual-send stale/current handles.

Receipt `result.action`, when supplied, matches its intent. Shared declared request/Task/Dispatch/Run/host/candidate identity fields match intent preconditions; contradictory identities reject the receipt. The helper's hash/field checks do not prove omitted facts or external raw receipt authenticity; main must independently verify required action-specific identities/stages.

Normal-Run cross-main takeover is **Blocked until positively proven exclusive by a supported actual runtime contract**; this version does not implement it. No `takeover-legacy` workaround or competing Run/worker to resolve silence. Another main diagnoses last reliable boundary/blockers but cannot dispatch/write shared records. Fresh subprocess read is not Orca coordinator takeover. Resume requires original verified sole main regaining authorized context and reconciled facts, or a separately reviewed supported exclusive transfer contract.

## Uncertain effects

| Intent/effect | Read-only evidence before retry |
| --- | --- |
| Dot request/supplement | Conversation/prior-message boundary/fingerprint/request/SHA/posted message. Found gets recovered receipt/original budget/quota; only proven absence and no generation permits same send inside budget. Unknown pauses |
| Worker placement/start/dispatch | Supported request replay/stages/residual resources, actual Run worker/Dispatch and host writer facts. Accepted input differs from turn start; live waits, done needs Task+Dispatch settlement, unknown retains. Preserve failure/prove old writer exit and accounting before new bounded Dispatch |
| Push/Issue/PR edit | Explicit remote/ref/object/content/task provenance. Found gets receipt; incomplete search is not absence, multiple matches block. Retry only proven non-application with documented identity |
| Merge/close | PR merged state/SHA/target containment/candidate/Issue state. Applied gets receipt/pending checks, never duplicate. Unapplied still needs fresh authority/head/base/acceptance/checks; unknown blocks writes |

Use returned supported replay argv/identity unchanged on same executable/host; no invented idempotency flags. Fully process/persist messages/accounting before acknowledgment. Crash leaves workers running; scope withdrawal overrides bounded completion. Keep unknown writers/branches/failures. Owner-approved retention is per worker through live contract, not release/delete permission. Pause checkpoint links unresolved operations/old deadlines/next evidence/owner/accounting; cannot complete/archive.

## Production merge and close

Before merge inspect actual PR body/title, relevant commit closing keywords, linked automatic-close associations and server-side state. Use non-closing references before integration. Handle unsafe links only within authorization and reread clearance; unknown/unsafe associations block merge. Repository settings need separate authorization.

Recheck full candidate SHA/base/accepted hashes, actual required CI/checks, scope and coordinator authority immediately before intent/effect. Changed code/base requires affected checks and renewed dot review as needed; old acceptance does not cover new code. Record merge identity, run required integration on actual merged source/binaries. Only Passed integration permits explicit Issue close/readback. Failed/NotRun leaves task incomplete/Issue open; preserve failure/review repair or rollback. Branch-only keeps these actions NotRun.
