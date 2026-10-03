# ACK cleanup and paused publication correction

2026-10-03. Root-approved implementation draft with independent original-source red; no corrected PASS is claimed by this document.

## Actual red and source cause

Rich F01+D31 first and second four-case runs failed; helper timing/geometry diagnostics remain in the producer report. The focused automatic diagnostic then independently isolates a production publication deadlock: fixed target RecipeVersion1314, owner1537, surviving Chef Ready=true/Cleanup=false/gen1 but its complete baseline stays at sequence691/version664, with actual ack-691:BaselineRequired. Rebound Partner/gen2 observes baseline1248/version1537. Source rejects an exact issued ACK while global cleanup is pending; the client ACKs only when receiving a baseline, while the server refuses replacement baselines until that ACK is accepted. Cleanup subsequently completes without reopening this publication slot.

Artifacts are in the managed recovery tree local/Logs/cooking-execution/network-rich-recovery/rich-diagnostic.log and .trx: one executed case, one failure. Thread scheduling and a constant version barrier did not remove the failure. This is distinct from legal movement rejection or a throughput threshold.

Independent source review also identifies a paused variant: ApplyControl(Pause) cannot publish while an old baseline is awaiting ACK, and the paused owner branch returns after processing the eventual ACK without publishing the new complete paused state.

Two new focused actual-ET Session regressions independently failed on original producer852619887471f0ef479578d5c26c2cd2b03cecf2: two executed/two failed/zero skipped, process exit1. The paused case expected a third paused baseline but stayed at two; the cleanup case received BaselineRequired and never Ready after Resume and actual cleanup. Exact artifacts: managed recovery local/paused-publication-original-red.log and local/Logs/cooking-execution/network-paused-publication/paused-publication-original-red.trx. An earlier fixture compilation error (nonpublic Driver.Simulation) is preserved separately and is not production red.

## Approved bounded correction

Each connection may retain at most one exact currently-issued ACK identity and its first correlation while cleanup remains pending. Repeated exact ACKs coalesce; invalid/stale ACKs still reject. This does not clear AwaitingAck, emit Ready or skip cleanup. After actual accepted owner cleanup and successful capture, the server revalidates active connection/issued identity and completes the retained ACK before publishing the latest full state. Close, supersession, scope retirement and unavailable capture clear this record. Paused cleanup remains deferred until resume; no ACK operation advances business clocks.

The paused branch publishes current complete authority plus Session projection only when its combined hash differs from that connection's issued state hash, respecting the existing one-AwaitingAck guard. The old issued identity is never overwritten; after the new state is ACKed, unchanged paused frames do not create an unlimited publication stream. Hash comparison includes the Session view, not only Recipe.Version.

No wire/schema, transport, owner authority, baseline bounds or restoration grants change. Root owns the production file; a separate worker owns new actual regression tests. Original-source focused red, corrected green, rich four-cutpoint acceptance and appropriate broad master gates must be recorded before acceptance. Pending source review/testing is not completed validation.

The rich producer independently reviewed the candidate production diff and found no blocker: one retained identity/correlation slot, exact current issued validation, active binding revalidation, deferred cleanup precedence, close/fault/scope clearing and no Tick in the paused branch. Duplicate pending exact ACK controls coalesce into the first correlation rather than allocating arbitrary response waiters. This review is source-only, not corrected execution evidence.

Subsequent corrected execution on recovery ea7567980 passed both regressions, zero failed/skipped. The independently reviewed tests were frozen as3333c5311 and imported into master eff49ca90. Exact final TRX: recovery local/Logs/cooking-execution/network-paused-publication/paused-publication-final-duplicate.trx; final log: local/paused-publication-final-duplicate.log. See paused-publication-increment.md and ack-fix-independent-review.md. Rich four-cutpoint and corrected broad master gates are still pending; no physical LAN or performance acceptance follows from these two cases.
