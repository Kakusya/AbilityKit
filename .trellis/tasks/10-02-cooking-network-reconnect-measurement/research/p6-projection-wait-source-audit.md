# P6 projection-wait source audit

2026-10-03. Independent read-only diagnosis; no correction or instrumentation execution approved by this report. Source inspected at main a57e17c5f7ef779342e40f47ab1608fb88eaddf9. Original evidence remains immutable in `local/Logs/cooking-execution/network-impairment/20261003-084307-4087945/`, particularly repeat-2 host/client/paired/relay JSON and stdout, summary.json, build-provenance.json and root-independent-red-recovery-review.json.

## Actual failure and predicate

Repeat-2 paired.json records `healthySample=false`, `recoveryPassed=true`, endpoint exits [0,0,0], and client `Full committed projection deadline.` The original stack identifies Program.cs line117 (Complete), line121 (RunLoad), and line51 (Client); these are original compiled-source positions, not today's line numbers. Client evidence has load=null. Host retained local300 sampled and50 warmup accepted commands.

Current `src/AbilityKit.Game.Cooking.NetworkImpairmentMeasurement/Program.cs` lines104-128 define RunLoad. Complete at lines115-119 first awaits send, requires Accepted/nonduplicate, records response RTT, then creates a fresh Stopwatch. It waits while latest FullRecipe.StateVersion is below accepted result.StateVersion and elapsed is below30s, using Task.Delay(1). The final Require checks only elapsed<30s. Thus failure proves deadline expiration; it does not prove the version predicate remained false at the final assertion. A qualifying projection arriving at/after the boundary still fails. Response latency is outside this fresh projection timer.

Client Program.cs lines45-47 and52 pump the genuine peer independently while RunLoad waits. `src/AbilityKit.Game.Cooking.NetworkProcessMeasurement/FramedFaultPeer.cs` Poll validates typed baseline instance/participant/generation/scope/schema/full hash and increasing snapshot sequence, freezes Latest, and sends the exact baseline ACK. Ready requires a received identity equal to the peer's acknowledged identity. The application wait is a state-version projection test; it does not itself require latest exact ACK/Ready or record their identities.

The original artifact retains no failing-flight command/result version, projection-wait begin/end timestamps, terminal latest identity/version, or issued/ACK ledger at that boundary. Null client load also loses accumulated load accounting. Consequently no specific missing baseline, native scheduling interval, fragment delivery failure, or timer cause is established. Final reencoded baseline sizes are not substitute observations of the failing wire image.

## Later recovery is separate evidence

Original repeat-2 final remote generation2 ACK has snapshot sequence2105, state hash `01adbe4d36ab17c8e248639abec5adf01a1701aa8b18d8035d53821f30ad2eb4`, instance `a047b016725c465e8983d46c33cf2eb6`. Final FullRecipe.StateVersion is2443. The final projection records both participants Connected/Ready and no cleanup pending. These later complete recovery facts do not establish timely generation1 projection during the failed sample. Endpoint pass/native0 is not whole-pair acceptance.

## Proposed bounded application instrumentation only

Before any correction or rerun, separately review a diagnostic increment confined to this application runner. Retain bounded per-flight evidence for all at-most350 offered slots: offered index/cohort, stable/domain/correlation IDs, accepted result and target state version, response timestamp, projection-wait start/end/deadline timestamps on the same endpoint Stopwatch clock, terminal elapsed, observed predicate, timeout outcome, and immutable before/after latest baseline and genuine Ready identity metadata. Preserve instance/participant/generation/scope and exact hashes; do not invent Host-issued or acknowledged identities from local expectations.

Keep accumulated actual offered/issued/accepted/skipped counts and completed result lists on failure, plus the single failing flight; do not replace partial accounting with null. Distinguish accepted response, qualifying full projection, and exact ACK/Ready when genuinely observed. If Host issued-history correlation is needed, retain a bounded actual issuance ledger rather than reconstructing it from the final capture. Preserve failure evidence even if recovery/export later fails.

No changes to30s projection timeout, operation/whole-process bounds, payload/history/queue limits, healthy criteria, production transport or frozen peer helper are authorized here. No inferred cause, normalization, retry-to-fill, or reencoded-size substitution. This proposal is a diagnosis prerequisite only; source correction and execution require separate root grants. No .NET was run for this audit.
