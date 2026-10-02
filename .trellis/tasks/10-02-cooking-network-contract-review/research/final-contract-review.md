# N01 final contract review ? 2026-10-03

## Latest decision

Ready for the bounded N02 production increment after the final frozen-text re-review below. No remaining design blocker was found. This is contract approval only; network implementation and verification remain pending. Earlier rejection findings below are retained as historical review steps.

## Initial decision (superseded)

The transport/authority direction is reconciled, but the reviewed draft is **not yet production-ready**. The implementation refinements below must be specified and independently reviewed before N02/N03 production wiring. No network implementation or test pass is inferred from this review.

Read-only review of N01 design, implement, PRD, manifests and ready-contract-review; ADR 0002/0003, long-term goals, technical roadmap and dated cooking spec notes; owner discussion sections 28/29 of 2026-09-24; current Session, transport and ET sources. Concurrent root/design-worker edits are preserved. This report concerns the draft reviewed before their forthcoming refinements.

## Reconciled decisions

- Owner 2026-09-24 specifies LiteNet as the sole real UDP transport and InProcess for host-local/full framed-path tests. Earlier KCP passages are historical, not a second selectable production transport.
- CookingLevelEtHost remains the unique simulation/tick/lifecycle/checkpoint authority. Network callbacks freeze/enqueue ingress only; owner processing admits commands and ticks once. Client is a passive full-baseline projection, without prediction or a second simulation.
- ET may reference Game.Cooking; Game.Cooking must not reference ET. The application authority port must keep this dependency direction. Existing SessionHost owning its own RecipeSimulation/NetManager is a legacy path, not evidence of the new ET integration.
- Pause is control-only and does not advance business/host clocks. Close/rebind must invalidate the old connection generation, cancel pending business and require a full baseline acknowledgment before new business ingress.
- New wire version 3 rejects legacy packets. Recipe 5 / Level 8 are current checkpoint schema authorities; required nullable fields mean present-null, not absent/default. Baselines do not confer policy, choices or server authority.
- ADR 0003 remains Proposed; reconciliation does not authorize framework/ECS removal or Unity work. The final singleplayer gates reported by root establish the dependency exit, not network capability.

## Concrete implementation blockers in the reviewed draft

### 1. Authority-owned batch mapping is required, in addition to receive ordering

Current `CookingLevelEtHost.cs` fingerprints SimulationBatch (line 74), rejects a new batch at/below LastCommittedSimulationBatch (line 820), and FreezeNextBatch selects only the minimum pending SimulationBatch (lines 1692?1709). Merely replacing Player/Command sorting with ingress ordinal therefore does not make one frozen network prefix one authoritative tick. Client-supplied batches could split/delay a prefix or advance the watermark.

Specify the trusted owner mapping before submission: wire client sequence/business identity is distinct from domain SimulationBatch; every newly admitted command in one owner-frozen prefix receives one server batch; accepted retransmission retains its original mapped command/fingerprint. Connection-generation sequence reset must not reset/reuse domain business identity. Opposing-player arrival and mixed/malicious wire-batch tests must prove one prefix/one tick and unchanged singleplayer default ordering. Root has assigned this refinement to the design worker; it is not yet accepted by this report.

### 2. Exact authority port and cancellation boundary remain unresolved

The design leaves final application port signatures to later mapping. Define concrete DTOs/methods for admission, owner drain/tick, observation, lifecycle controls and connection-generation cancellation. Existing envelope SourceConnectionId alone is not an owner cancellation API. Specify how close/rebind terminalizes Session-pending and already ET-admitted work without executing stale commands on a later tick; identify the linearization point and returned dispositions. Do not retain the legacy SessionHost simulation as a parallel owner.

### 3. Wire and queue bounds need executable constants/configuration

Business queue capacity 256 is specified, but exact frame/string/collection limits, reserved control capacity, active connection/generation limits and overflow tombstone retention are not fully fixed. A bounded business queue plus an unbounded pending-close map is not a bounded ingress contract. Specify decode-before-allocation limits and deterministic overflow/cleanup behavior, including malformed required/enumeration fields and complete command payload coverage. Current legacy LAN command packet lacks several real operations/fields and cannot silently serve as the new full contract.

### 4. Baseline acknowledgment needs an exact issued-baseline identity

Define acknowledgment against the particular baseline issued for the current participant/connection generation/scope/epoch/snapshot sequence/hash. Do not require an endlessly advancing global latest snapshot, or accept an old generation acknowledgment after rebind/scope transition. Specify outstanding baseline replacement and stale client projection rejection. Full paused/Created baselines must explicitly advertise checkpoint unavailability rather than fabricating a resumable checkpoint.

## Required evidence after refinement

Actual framed local and remote paths must share decode/admission, with callbacks demonstrated enqueue-only; same-prefix receive FIFO and first accepted duplicate semantics; pause/close/rebind and baseline-ack controls; complete observation/checkpoint versions and required-null rejection; scope-successor and cold-fault recovery through the existing owner. SDK/ET gates and an actual independent-process LiteNet UDP loop are implementation evidence. Physical two-PC LAN remains a separately disclosed unavailable environment, and Unity remains deferred.

## Verification

Static source and document review only. No production files modified. No .NET, lint/type-check or network execution performed in this read-only window. This report does not certify the forthcoming design-worker revisions, network implementation, physical LAN or Unity.

## Final-interface independent re-review ? 2026-10-03

Read the final-interface tail through its final implementation-boundary section against actual Host TryEnqueue/TerminalAdmission/FreezeNextBatch and Recipe checkpoint deduplication DTO. No production edits or .NET runs.

Closed design findings: CaptureResult now represents Faulted/Disposed/Busy with null State and never reads damaged authority; successful uninitialized capture is distinct from failure. Partial-source cancellation is caller-only and does not poison logical terminal; all-callers cancellation removes the group and Session retains CancelledNoExecution. Pending fingerprint conflict explicitly publishes every affected caller through an independent drainable notification outbox. Cold Session no longer pretends to reconstruct first mapped command/batch from Recipe receipt hashes. Trusted ordinal is opt-in; default singleplayer Player/Command sorting and batch behavior remain, and one newly frozen network prefix receives one authority batch/one Tick. Concrete queue/codec bounds and issued-baseline acknowledgment identity close the earlier unspecified constraints.

**Not yet ready: two remaining concrete contract points.**

1. The earlier first-mapping paragraph still says a different wire fingerprint is rejected directly. For a *pending* identity that would bypass Host's actual whole-group conflict terminalization. Specify that pending differing payload reaches the trusted Host conflict path (or an equivalent owner-only conflict API), then drains all old/new caller dispositions. Terminal differing payload may simply return the existing logical conflict/rejection semantics. Tests must show Session A pending, then B differing payload, then both terminate Conflicted, zero domain execution.
2. Cold new-instance isolation is not yet specified at the domain CommandId mapping. Host/domain dedup keys do not contain network server instance, and restored Recipe receipts contain Session/Player/CommandId/hash, not the first batch. Merely rejecting an old instance header cannot prevent a newly valid instance with the same StableCommandId from colliding with the old restored receipt. Define deterministic collision-free domain CommandId encoding of server instance plus stable wire ID within the actual command-ID bounds, while preserving same-live-instance rebind identity. This is not ordering manipulation or a new persisted network ledger. Test cold valid-new-instance same stable wire ID against restored receipt and old-instance rejection.

Implementation boundary to make explicit: a 2048-slot outbox sized only for accepted pending callers cannot blindly enqueue the extra rejected conflict caller when 2048 accepted callers are already pending. Return the new caller's immediate disposition separately and reserve the outbox for prior accepted callers, or specify sufficient bounded capacity. Do not turn allocation/capacity failure after group terminalization into silently lost notifications.

Required cancellation controls include all-callers close followed by the same live identity CancelledNoExecution without a new batch/execution; a new stable identity may proceed; generation-qualified sources never reuse/clear a newer connection; partial A-close/B-live executes B and later duplicate returns its actual result. These remain proposed tests, not observed passes.

The latest amendments replace earlier overlapping prose where explicitly stated; the two remaining points above require final designer reconciliation before production-ready acceptance.

## Final frozen-text disposition

Re-read the final identity section and the revised first-mapping/conflict/outbox paragraphs after the designer's final edit. **Ready for the bounded N02 production increment defined here; no remaining design blocker found in this review.** This supersedes the earlier not-ready dispositions in this report, which remain historical findings.

Both remaining points are closed: wire business identity now includes ServerSessionInstance; trusted adapter freezes the DTO and overwrites the domain CommandId with `net3-` plus lowercase SHA256 of tagged, length-prefixed UTF8 fields and big-endian scope integers. The actual CookingLevelScope RestaurantRuntime/Level/LevelEpoch fields are covered. The resulting 69 ASCII bytes fit the specified 128-byte network bound; server instance changes separate cold operations from restored domain receipts, while same-instance rebind retains first identity/batch. Hashing defines identity, not arbitration. Pending different fingerprints explicitly reach the real Host whole-group conflict path using the first mapped batch/ID/ordinal. The first paragraph no longer authorizes Session-only rejection. The outbox reserves only prior admitted callers; the new rejected conflict caller's disposition is returned immediately, avoiding a 2049th queued notification.

Production review must still enforce the documented caller-cancellation and full-capture invariants, default singleplayer sorting regressions, source-generation qualification, single-Tick prefix behavior, and executable schema/bounds controls. This approval is for contract readiness, not proof those APIs already exist or that network/physical-LAN/Unity exits passed. There was no production modification or test execution in this review.
