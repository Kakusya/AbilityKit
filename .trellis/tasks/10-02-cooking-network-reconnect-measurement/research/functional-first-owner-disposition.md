# Owner disposition: functional correctness first, latency deferred

2026-10-03 owner: defer latency consideration and solve other issues with existing framework capabilities wherever possible; continue active goal. This explicitly supersedes the previous timer/O04-first order. Architecture and singleplayer/network/Unity ordering remain, Unity/S15 still deferred. Whole goal stays active.

## Current disposition

- Ordinary latency/throughput targets and reference profiling, native scheduling/timer optimization and O04 source integration are OWNER_DEFERRED, not passed, removed or rejected. Preserve existing thresholds/results/designs for later. No new timer/native configuration enters main.
- Root told live O04 dispatchctx_f39f066627b8 to stop new scheduling implementation and save only an OWNER_DEFERRED SOURCE_ONLY_UNVERIFIED checkpoint. Message msg_0bdcca4e175e; actual stop/checkpoint/cleanup pending authoritative receipts. Dirty draft must be saved including untracked files before unused-tree cleanup; user-owned terminal state must be respected.
- Immediate work: existing ET singleauthority/fixedticks, Session client/host, framed reliable transport, fullbaseline identity/ACK/Ready, generation/rebind, existing checkpoint/carry/durable flow. First audit application drivers against those capabilities, then close full recipe/front/supply/allocator/receipt recovery and naturalservice/successor coverage. Add only demonstrated missing glue/tests or narrow corrections; no broad framework rewrite or new architecture.
- Existing operation deadlines/capacity/history/integrity/recovery contracts stay intact. Separate a functionality failure from deferred benchmark/QoS assessment; never mark an old timed-out sample healthy after later recovery. Timing-derived P6/reference rows remain explicitly incomplete/deferred where applicable. Fault recovery/cancellation/oldgeneration/duplicate/arbitration correctness remains required.
- Retain accepted S01-S14/N01 and scopednetwork evidence; no blind repetition of unaffected passing gates. PhysicaltwoPC remains unavailable/NOT_VERIFIED, no repeat question; prepare handoff once localtools valid.

## Next evidence

1. Concrete current-framework API/driver alignment review: identify exact duplicated/manual protocol responsibilities and supported reuse seams, preserving genuine current identities, response loss and full graph recovery.
2. Reviewed existing-capability integration and meaningful focused controls, then actual full rich four-cut recovery/natural completion/durable successor. If fixedbudget prevents a complete run, record exact failure; changing or decomposing acceptance requires an explicit separate disposition, not a silent timeout increase or data pruning.
3. Functional fault/arbitration/verifier coverage and applicable assembled gates on frozen source; retain remaining physical and deferredperformance exits with truthful status.

Historical timer diagnostics remain valid within their stated scope but no longer lead current implementation.
