# Dot dialogue and bounded waiting

This is the sole request/supplement/timing contract. Use live Orca browser guidance for commands/tab affinity and [recovery](recovery.md) for uncertain sends. Dot converses and reads published GitHub commits; it does not run code/cloud work or write GitHub.

## Bind and send

1. Verify conversation URL/identity/purpose and current generation state. Reuse verified binding, not a default dot ID; multiple suitable conversations require selection. Never overwrite/repeat a generating prompt.
2. Use [request template](../assets/dot-request.md): `planning` or `final-review`, unique request ID, stable flow ID, approved scope, full published SHA/link, decision/evidence/conflicts and the required API response below. Include current API/source references, and the accepted design for final review. Verify visibility in the explicit remote repository; dirty local files are not review input.
3. Persist/read back send intent **before input**: fingerprint, conversation/prior-message boundary, `wait_started_utc`, `deadline_utc = start + 30 minutes`. Save posted-message identity/raw send receipt afterwards. Input acceptance is not a posted prompt. Lost receipt consumes original budget; reconcile before repeat.
4. Save full completed reply, conversation/message/request identity, full reviewed SHA, `reply_posted_utc` (null if unavailable), timestamp provenance/reliability and separate `observed_utc`. Generating text, generic praise, ambiguity, old request or different SHA is not acceptance. Check required reply content below and use [decision template](../assets/dot-decision.md) to record API references and completeness. Preserve conflicts/failures; apply dot's technical decision within Owner scope/honest evidence.

## Required reply content

Every planning request must explicitly ask dot to return **API design**, alongside its technical plan and decision. For APIs affected by the approved scope, require:

- Concrete interfaces/types/method signatures, parameters and return/DTO shapes, plus whether each API is existing, changed or new. Use the target language; C# declarations are appropriate for C# changes. Mark proposals as proposals, not implemented facts.
- Responsibilities and Cooking callers/consumers, observable behavior, preconditions, rejection/error results and a minimal calling example or call sequence.
- Relevant contract details: state writer and lifecycle/release owner; driving Tick/thread, async cancellation/generation checks, idempotency, event publication and schema compatibility where the change touches them. Reference unchanged authoritative contracts rather than inventing new mechanisms.

For final review, ask dot to compare the frozen candidate's API with the accepted design, identify differences (or explicitly state none), and decide whether they are acceptable or require revision. Keep this finding distinct from the full-SHA candidate decision and validation scope. For a documentation-only task or a scope with no API design relevance, dot must explicitly return **API design: N/A** with a reason; an absent section is not N/A.

Preserve dot's full original reply and link its API design/finding in the decision record. Carry the accepted design into the owning task's planning artifacts before implementation. Main checks completeness and scope; it must not invent missing signatures or silently substitute its own design. An otherwise accepting reply missing required content is `ambiguous` for continuation; retain dot's stated decision verbatim. A clear blocker or revision request remains actionable as such and cannot authorize implementation or acceptance through the missing design.

Request missing content only through the existing one-supplement rule and original deadline below. No new quota, repeated questions or renewed budget. Until complete design or justified N/A exists, do not advance from Dot plan to Prepare/Implement; until the final API finding or justified N/A exists, do not accept/deliver the candidate. At deadline without valid resolution, use the existing timeout/pause path.

## Budget and one supplement

Reminders, clarification and explicit confirmation share **one supplement per request**. Its persisted intent reserves quota even if receipt is lost. Reconcile uncertain supplement instead of sending another. Send only when generation is positively stopped, no valid final resolution exists, quota remains and deadline has not arrived. Ambiguity may consume this one clarification, not unlimited questions.

Persist poll ordinal, last/next UTC and generation/full-response observations. Poll 30 seconds after start, then 60 seconds after that observation, then 120, then 300-second intervals, capped at original deadline. Split waits into at most 60-second responsive slices; slices do not fetch dot/increase cadence. Recovery permits one read-only reconciliation observation then restores cadence; observe overdue polls once, not in bursts. Reminder, tab rebinding, interruption and uncertain sends never extend deadline. Missing/untrustworthy budget blocks continuation.

While waiting, no new dispatch, technical decision, integration or merge. Existing workers finish only approved bounded assignment and idle; new technical questions wait. Collect reports/accounting without acceptance. Owner scope withdrawal stops affected work through supported authority and preserves evidence.

## Reply timing and reopening

| Evidence | Deterministic result |
| --- | --- |
| Full explicit resolution of current request/SHA, reliably posted from start through deadline | Valid resolution; apply scope/evidence gates under valid coordinator authority |
| No reliable posted UTC, but complete current reply reliably observed within original deadline, with provenance below | Valid timely resolution by full-observation upper bound; posted UTC stays null |
| Same timely reply first observed after deadline | Add valid **timely, late-read** decision to old request; preserve prior timeout/pause. Does not restore scheduling/write authority |
| Reliably posted after deadline | Late evidence only; expired request stays paused |
| Unknown/unreliable time without complete timely observation proof, ambiguity, old request or wrong SHA/conversation | No automatic acceptance/reopening. Save evidence; within budget use one eligible supplement, otherwise pause at deadline |
| Generating, unknown generation or browser failure | No speculative send; reconcile read-only and pause dependent work if facts remain unknown |
| Deadline without valid resolution | Persist timeout/handoff/accounting and stop main turn |

**Complete-observation proof** (dot's approved timing clarification): verify conversation/request/full SHA, request message and prior-message boundary, that this reply was absent at that boundary and belongs after this request. Persist full final raw snapshot/message ID, prior snapshot, complete/not-generating judgment, trusted observation UTC/time source and uncertainty. `timing_basis: complete-observation-upper-bound`; retain `reply_posted_utc: null`. Latest possible observation UTC (observed + uncertainty) must be no later than original deadline. An optional UTC lower bound uses trusted pre-send recording, never receipt arrival masquerading as server posted time; leave unknown lower bound null. Message order is causal evidence, not precise UTC.

A fragment observed before deadline cannot timestamp a full reply first seen later. After-deadline first full observation without reliable timely posted UTC or prior durable complete-observation proof is unknown-time; relative page times/guesses do not prove timeliness. On restore, verify earlier durable complete proof before adopting it; crossed-deadline uncertainty, clock jumps, wrong identities or unverifiable snapshots reject timely classification. Technical validity never supplies authority: elapsed deadline requires preserving/entering pause even if a crash left `paused: false`; no automatic dispatch/shared write from a late read. Original main must explicitly resume under verified authority. No fabricated timestamps, added quota or refreshed budget.

On timeout/failure implementations remain **awaiting review**. Save original budget/reply/timeout facts, next evidence and each worker's Run/Task/Dispatch/host/assignment/liveness. Account settled workers using live `worker-retain` under recorded Owner permission. Unknown liveness stays unknown; no blanket close/restart. No active main means no polling/continuation.

Expired requests remain history. Owner explicit resume/reopen permits **new ID/new budget**, linked to old request/approval; never overwrite old timeout. A normal valid `needs-revision` decision permits next candidate and linked review with its own budget. “Iterate until accepted” never permits endless timeout-driven requests. Timely late-read reply resolves the old technical question, but paused execution still needs explicit resume and verified original sole authority. Unsupported cross-main recovery stays blocked.
