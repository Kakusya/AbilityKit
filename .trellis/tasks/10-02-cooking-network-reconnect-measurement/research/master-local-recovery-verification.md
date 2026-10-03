# N03 master local recovery and measurement evidence

2026-10-03. This records actual local results, not completion of N02/N03 or physical LAN acceptance.

Latest follow-up: production0d0beb2f1 fixes actual exact-ACK cleanup starvation and paused-state publication. Independent focused evidence is original2red→corrected2green; tests imported aseff49ca90. See ack-cleanup-paused-publication-review.md, paused-publication-increment.md and ack-fix-independent-review.md. The broad gates listed below predate this correction and do not validate it. Corrected master regression, rich four-cutpoint completion and additional deferred-slot isolation controls remain pending.

## Reviewed imports and actual master checks

- Cold four-case tests: producer c15fde9d6, master c7ffb49ee; see cold-recovery-increment.md and independent review.
- Measurement tool: producer 9e6c5c511 and final readiness correction 17218c56e; master 51ecdb52f/ffecf1205. Independent review accepts the actual three fresh repeats for each topology, including final readiness.
- Live six-case tests: producer 852619887, master 8c9903488. Independent review accepts six actual passes, zero skipped, without attributing helper compile and clock-assertion failures to production defects.
- On master ffecf1205, ControlOnly actually passed for both InProcess and SameMachineUdp. Artifacts: local/Logs/cooking-network-measurement/20261002-221233-1575292 and 20261002-221239-7667986. Root logs: local/Logs/master-measurement-InProcess-control.log and master-measurement-SameMachineUdp-control.log. These are bounded capacity correctness controls, not repeated full measurement samples.
- NetworkAcceptance built on master with zero warnings/errors in this command: local/Logs/master-network-acceptance-build.log. Its metrics wording correction does not manufacture a new complete independent-process run.
- On master 8c9903488, actual cooking-et-level-runtime gate passed: Cooking 808 and ET 324, zero failed/skipped, 54.0 seconds. Exact summary: local/Logs/test-gates/20261003-061642-cooking-et-level-runtime/cooking-et-level-runtime/gate-summary.json and its two TRX files.

Previously accepted master network-sdk311 and kitchen644/808/314 remain in N02/master-network-verification.md. They were not rerun or relabeled as covering the latest six test-only additions.

## What is established

The six small actual-ET framed cases compose manual pause/disconnect/partner continuation, automatic progress while disconnected, completed unbound cup handoff, committed submission with deliberately lost reply followed by cached live retry, queued pre-mapping callers closed before legitimate fresh execution, and a real natural successor with scope/ACK/sequence isolation. The four cold cases construct a new store/factory/Host/Session after disposal and reject old instance credentials before fresh legal execution.

Partial/all logical-source cancellation is separately exercised through the real authority adapter. Ordinary Session ingress cannot manufacture two simultaneously legal bindings for one participant; queued pre-mapping cancellation is not a mapped CancelledNoExecution receipt. See local-recovery-coverage-audit.md for this reachability distinction.

The producer's final three SameMachineUdp repeats each accepted73 of600 offered opportunities,527 explicitly backpressured, no pending/rejected/cancelled commands, and final readiness true. RTT p95 was1404.667–1488.811ms. InProcess owner managed allocation was47.9–49.2GB per60-second sample. These expose follow-up work; performance thresholds are UNSET and no smoothness/performance acceptance is claimed. CPU/memory include same-process Host and clients; owner allocation includes inline callbacks. See measurement-increment.md for exact per-repeat data.

## Still required or deferred

- Rich F01+D31 finite-supply four-cutpoint recovery is now integrated and passed in the corrected master ET gate below; see rich-recovery-independent-review.md for its precise boundaries.
- Independent-process UDP fault/load combinations and physical two-PC paired evidence remain distinct, unverified exits. The two-player same-process measurement tool cannot substitute for them.
- Formal workload/threshold approval and any subsequent optimization remain separate from measurement correctness. Higher participant/load and packet fault combinations have not been run.
- Unity implementation and verification remain deferred.

N03 remains in_progress. No baseline history is truncated, no default bounds are silently widened, and no secondary simulation or network restore grant is introduced.


## Corrected master verification ? 2026-10-03

Production ACK correction 0d0beb2f1, paused ACK regressions eff49ca90, rich four-cutpoint tests 621f3142f and deferred ACK isolation tests 444f03d84 are integrated. The first broad run exposed seven test failures from parallel classes constructing the process-wide singleton ET Host. Test collection correction 3979beed5 serializes only those three classes, preserving the production singleton and parallel pure tests. The first failed run is retained at local/Logs/master-n03-corrected-et-first.log and the 20261003-120928 gate directory.

Actual corrected cooking-et-level-runtime passed: Cooking 815 and ET 328, zero failed/skipped, 629.653 seconds. Summary: local/Logs/test-gates/20261003-121713-cooking-et-level-runtime/cooking-et-level-runtime/gate-summary.json; log local/Logs/master-n03-corrected-et-second.log. Actual network-sdk passed: 311 tests, zero failed/skipped, 42.4 seconds; summary local/Logs/test-gates/20261003-122742-network-sdk/network-sdk/gate-summary.json and log local/Logs/master-n03-corrected-sdk.log. Test source was 3979beed5; documentation-only 6b49cc05d was committed during the run. Existing compiler warnings are not represented as a warning-free build.

This closes the rich local four-cutpoint test follow-up, not N02/N03 overall. The rich tests use framed peers in one process; cold-generation recovery is separately covered. Independent-process fault/load, higher loads/packet fault combinations, physical two-PC acceptance and performance thresholds remain open.
