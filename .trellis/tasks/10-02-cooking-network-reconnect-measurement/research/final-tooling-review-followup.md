# Final tooling review follow-up

2026-10-03. Root inspected current source on master after commits 2d671ebe9 and 62e2ad365. Root .NET session50139 remains live: cooking-kitchen-loop actually completed exit0, ET328 passed, while cooking-et-level-runtime is still running. No competing .NET window has been granted.

## Reference evaluator acceptance gap

The reference plan states every repeat/participant must satisfy latency limits. Current evaluator checks participant completion counts but only aggregate repeat RTT/projection percentiles. Aggregate percentiles do not prove individual participant limits. Measurement owner is assigned source-only schema inspection: enforce existing per-participant distributions if present; otherwise identify missing evidence as NOT_VERIFIED and propose reviewed instrumentation before any C# edit. A malformed NaN in retained summary must yield a finite NOT_VERIFIED report, rather than crashing JSON output. No Release execution or performance acceptance follows from synthetic controls.

## Datagram build provenance gap

Current impairment wrapper hashes source at invocation and complete runtime binaries, and compares P0 manifests. This establishes cross-profile equality but does not establish that current source is the source compiled into those binaries. Relay owner is assigned source-only build provenance with compiled-source hash agreement before execution. Frozen helpers and production remain outside ownership. Numerical runs remain gated on build, actual relay socket/queue controls, and independently reviewed P0 three fresh triplets.

Root additionally inspected relay ordinary/default queue caps, control-only hold/release, five-second residence enforcement, pending-Off retained entries and raw unchanged-byte forwarding. These are source findings, not executed control results. The driver records a completed load task exception separately and attempts subsequent Off/rebind recovery; recovery success cannot convert a failed healthy sample to acceptance. Final frozen source will be reviewed again before the exclusive runtime grant.
