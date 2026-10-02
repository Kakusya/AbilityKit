# N01 ready contract review — 2026-10-03

Design-only increment based on master86c3eb41d and parent network-resume-audit. Owned N01/design.md and implement.md updated; no ADR/spec/source/metadata changes, no .NET execution. N02/N03 remain unstarted pending root review and S14 exit.

Verified current API facts: TryEnqueue checks lifecycle/scope/reserved clock and fingerprints; Tick advances existing ET authority; DrainPending groups sorted by Player/Command, so FIFO Session alone cannot guarantee network receive ordinal. ExportCheckpoint refusesPaused. Observation is immutable committed projection; checkpoint format8/Recipe5 includes external-policy identity but does not carry authorization. Game.Cooking.EtRuntime depends on Game.Cooking; Session referencing ET back would introduce circular dependency.

Design proposes trusted network ordering at the same Host with unchanged default singleplayer sorting, owner-only connection event processing/cancellation, upper-layer authority-port implementation and explicit unavailable checkpoint status. These are proposed narrow implementation interfaces, not existing verified capabilities. Root must review final signatures, bounded control-event/tombstone accounting and exact state schema before execution. Existing durable lifecycle remains sole authority; no old Session simulation/transition as parallel world.

No new completed-function claim. Physical two-PC LAN remains missing; independent local processes are distinct future verification. Formal dated ADR/spec reconciliation remains root-owned.

## Follow-up blockers refinement

Added concrete server frozen-prefix batch mapping: wire batch=0, first mapped batch/domain fingerprint retained across retries, business stable identity distinct from transport sequence; unchanged singleplayer default ordering. Added proposed pure-domain port/Host readonly full capture/cancellation signatures, explicit bounded queues/connection tombstones/receipt backpressure, exact issued-baseline acknowledgment, owner-only disconnect cleanup and paused resume ordering.

Important source facts preserved: Observation alone omits tombstones/NextProductId; Created/Paused resumable Level image remains unavailable, full internal read comes from same Host readonly Recipe/Front/layout capture without schema/grant changes. Real cleanup operation is StopFrontWork, not ReleaseFrontWork. Old CookingSessionAuthority ExecuteBatch owns legacy CookingSimulation and cannot implement the current production port; historical tests remain separately scoped. No source/.NET/ADR/spec/metadata work. Root + layout review pending; document completion is not implementation acceptance.

## Four source-boundary closure

Final appended design overrides earlier ambiguous capture signature and cold receipt recovery text. CaptureResult explicitly separates failure/state; closed owner controls and frame/cancel DTOs are concrete. Partial source cancellation has per-caller history distinct from logical terminal; surviving identical caller executes, cached duplicates use actual executed result. All-caller cancellation has explicit live CancelledNoExecution mapping. Proposed bounded Host terminal notification outbox delivers every caller affected by pending fingerprint conflict. Cold startup new serverSessionInstance rejects all old-instance messages; Recipe receipt hash does not rebuild command/batch mapping. No persistent mapping or migration scope added.

Remaining: root+layout acceptance and final enum/code placement; formal ADR/spec synchronization, implementation and actual negative controls; physical second-PC proof unavailable. Design-only changes in three owned files, no source/metadata/.NET.

## Freeze-ready identity amendment

Concrete domain ID: net3- plus lowercase SHA256 of fixed-tag, length-prefixed UTF8 instance/full MatchLevel/participant/stable wire ID; numeric scope fields big-endian Int64. Fixed69ASCII bytes within128 network limit; source validation only requires nonblank, network enforces length. Frozen DTO mapped server-side; digest is identity encoding, never arbitration order. Cold instance changes domainID, live rebind retains it. Layout agreed minimal mapping and conflict/outbox boundary; final reread pending.

Contradictory earlier sentences were replaced in place: pending fingerprint conflict reaches Host with original batch/domainID and actual payload, terminal conflict never executes; accepted outbox2048 excludes immediate newly rejected caller; all cold old-instance requests reject, no receipt-hash reconstruction. No source/.NET/storage/schema expansion.
