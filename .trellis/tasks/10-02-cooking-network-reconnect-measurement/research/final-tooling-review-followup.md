# Final tooling review follow-up

2026-10-03. Root inspected current source on master after commits 2d671ebe9 and 62e2ad365. Root .NET session50139 remains live: cooking-kitchen-loop actually completed exit0, ET328 passed, while cooking-et-level-runtime is still running. No competing .NET window has been granted.

## Reference evaluator acceptance gap

The reference plan states every repeat/participant must satisfy latency limits. Current evaluator checks participant completion counts but only aggregate repeat RTT/projection percentiles. Aggregate percentiles do not prove individual participant limits. Measurement owner is assigned source-only schema inspection: enforce existing per-participant distributions if present; otherwise identify missing evidence as NOT_VERIFIED and propose reviewed instrumentation before any C# edit. A malformed NaN in retained summary must yield a finite NOT_VERIFIED report, rather than crashing JSON output. No Release execution or performance acceptance follows from synthetic controls.

## Datagram build provenance gap

Current impairment wrapper hashes source at invocation and complete runtime binaries, and compares P0 manifests. This establishes cross-profile equality but does not establish that current source is the source compiled into those binaries. Relay owner is assigned source-only build provenance with compiled-source hash agreement before execution. Frozen helpers and production remain outside ownership. Numerical runs remain gated on build, actual relay socket/queue controls, and independently reviewed P0 three fresh triplets.

Root additionally inspected relay ordinary/default queue caps, control-only hold/release, five-second residence enforcement, pending-Off retained entries and raw unchanged-byte forwarding. These are source findings, not executed control results. The driver records a completed load task exception separately and attempts subsequent Off/rebind recovery; recovery success cannot convert a failed healthy sample to acceptance. Final frozen source will be reviewed again before the exclusive runtime grant.

## 2026-10-03 actual kitchen gate and worktree inspection

Root read the actual 20261003-140451-cooking-kitchen-loop/gate-summary.json and terminal session50139: kitchen gate Passed/exit0 in645.259s; focused644/644, Cooking859/859 and ET328/328, zero failures. The subsequent cooking-et-level-runtime gate is still live on the same root-owned session, not yet accepted. The evidence lives under main local/Logs/test-gates; no historical result was substituted.

Actual Orca worktree ps currently shows recovery-current with one attached live terminal despite no worker. It is therefore not eligible for automatic removal now. Natural-operating and network-n01-n03 have no attached terminal but actual Git status contains uncommitted tracked and untracked source; they remain preserved pending full review/archive. Nine prior retirements remain the actual count. A clean Git tree or absent worker alone is not sufficient proof that a worktree is unused.

Root inspected actual measurement Program.cs: perParticipant contains completion counts but no latency distributions. Root explicitly approves adding per-player distributions from existing immutable measuredSamples.Player/RttMs/SendToProjectionMs only, and corresponding strict evaluator checks. No schedule, full-projection barrier, bounds, runtime architecture or threshold change is authorized by this correction. Historical reports lacking those distributions cannot prove individual latency acceptance.

## Independent evaluator CLI replay and physical companion gap

Root independently invoked the actual corrected evaluator CLI on all25 retained synthetic cases from20261003-062244. Each status and actual0/1/2 exit matched the declared expectation; root outputs are separate local/Logs/reference-evaluator-root-review/*.json and summary.json. Source18f5734bc adds only two filtered per-player distributions using existing samples. This accepts evaluator controls and scoped instrumentation source, not compiled instrumentation or real Release performance.

The physical handoff audit found genuine tool gaps: separate-role concurrency artifacts need an offline full-field paired verifier, and existing independent-process service tool does not implement the required rich four selectable disconnect cutpoints. Existing in-process rich proofs remain accepted in their original scope. New physical-two-pc-handoff.md records exact boundaries; root has assigned documentary, code-backed designs for an isolated rich companion and offline verifier before implementation. Physical LAN remains NOT_VERIFIED. Root50139 remains the sole live .NET session.
