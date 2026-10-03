# P6 diagnostic source: independent review

2026-10-03. Source-only review of frozen main `39c4579a3` against `2c5fe0bb0`. Scope: NetworkImpairmentMeasurement Program.cs and new LoadWaitEvidence.cs. Read N03 PRD/design/implement/check manifest, cooking-network-measurement spec, the parent injected check context and p6-projection-wait-source-audit.md. Current unrelated Rich changes and other review files are preserved. No code self-fix, compilation, .NET, sockets, endpoint run or numerical acceptance is performed.

## Findings not fixed: source blockers

1. **Original elapsed-only deadline observation moves after an extra diagnostic read.** Program.RunLoad.Complete currently executes `finalVersion=read().FullRecipe!.StateVersion` before `finalElapsed=wait.Elapsed`. The original post-loop operation was directly `Require(wait.Elapsed.TotalSeconds<30,...)`. An extra cached read can move a boundary case from below30s to expired. Capture the original elapsed-only eligibility immediately after the unchanged loop, before final diagnostic read/observation; record predicate independently and disclose that it is observed just after the decision. Keep the original failure even if this later predicate becomes true. This does not identify the historical P6 failure cause.

2. **RTT boundary includes new diagnostic work.** After await-return, ResponseAt is captured, but the legacy elapsed uses a fresh GetTimestamp after AtResponse observation, IDs/flags and accepted counter assignment. The reported RTT consequently includes diagnostic observer/allocation cost. Freeze the genuine response elapsed before those operations (reuse ResponseAt-started) and preserve the existing definition excluding projection waiting. Preserve the original wait timer creation ordering; do not turn diagnostic time into send time or hide it by subtraction estimates.

3. **Completed terminal result lists still disappear on load failure.** Legacy load remains null on failure. Slots retain stable/domain IDs, outcome/version/duplicate metadata and counts, but not the actual prior CookingNetworkWireResult objects, their reasons/events/supply, or the completed result list explicitly required by the audit proposal. Retain the actual received immutable terminal per offered slot (at most350), including the accepted-but-unprojected failing flight; projected completion remains separately flagged. This satisfies full received-result survival without fabricating missing responses, widening bounds or changing legacy load healthy criteria. There is no need to duplicate full baseline captures per slot.

These are local application diagnostic correctness/metric issues; left unchanged because dispatch expressly requires report first. Source should be corrected and frozen again before root serial compilation/control grant.

## Source paths checked and retained constraints

The diff contains exactly Program.cs, LoadWaitEvidence.cs and the audit grant record; wrapper, FramedFaultPeer, transport/framework, command construction, owner tick/pump, timeouts and wire/history/queue limits are unchanged. RunLoad still offers350 slots at200ms intervals, first50 warmup and next300 sample, one in-flight task,70s schedule, existing Ready/Toggle gates, existing send/operation timeouts and original30s elapsed-only projection failure rule. No retry-to-fill or fabricated success is introduced.

Slots survive top-level failure because loadWaitEvidence exists outside Host/Client and Save includes it even if later recovery/export throws. A failed prior flight and the next offered slot are distinctly recorded; no assertion that a never-sent offered slot was issued. AcceptedResponses increments when a genuinely received result reports Accepted, independently of CompletedProjections; accepted duplicate fails the unchanged fresh-ID requirement and must not be represented as projected completion.

Cached views do not capture/hash/serialize or advance owner frames. Baseline identity is retained as immutable metadata, including its full existing identity fields. Remote ReadyIdentity is the actual peer matched Ready message, not an invented current baseline ACK; it can legitimately lag Latest. Local exact Ready identity is explicitly unavailable. Correlation IDs likewise remain explicitly unavailable through existing send APIs. None is evidence of Host-issued history at the historical timeout.

SlotBound350 is enforced before insertion; DiagnosticError clips retained Text to4096 characters while retaining OriginalLength/Truncated. Original endpoint failure/exception remains authoritative and unmodified. Nominal DeadlineAt is explicitly distinguished from internal Stopwatch timer start. Slots can derive offered/cohort/skipped/issued/accepted/projected counts, but currently cannot recover the full actual prior terminal result lists (blocker3).

## Minimum meaningful controls before a numerical rerun

After corrections, root should grant an audited fresh build first, then bounded app-only controls against the actual shared completion/diagnostic path rather than copied Boolean logic. If a tiny pure helper is extracted, its decision must be the one actually called by RunLoad.

- Immediate genuine received Accepted/nonduplicate and qualifying cached projection: accepted1/projected1, actual terminal retained, actual response RTT captured before an observer deliberately consumes time.
- Accepted result with projection held below target: original30s failure, accepted1/projected0, actual result/target and pre/post genuine identities survive. Do not shorten production timeout for the real-path control.
- Boundary controls for original elapsed just below/at/above30s with both predicate values; delayed diagnostic observation must not change the frozen decision. A qualifying late predicate remains failed.
- Rejected or failed send and duplicate result: no fabricated accepted/projected count; actual received rejection retained where present; no received result fabricated on send exception.
- Successful earlier flights followed by projection failure and then recovery/export failure: top-level serialized partial still contains prior actual terminal results plus failing flight and exact accepted/projected counts.
-350 insertion and351 refusal;4096/4097 clipping; scheduler/backpressure skip outcomes; prior-flight/readiness/Toggle outside-Complete failures distinguish unsent offered slots from the real failing flight.

Synthetic held-image/helper controls do not prove genuine UDP/ET issued identities or explain the original P6 failing flight. Following controls, the authorized original workload/policy and fresh paired full-state/ACK/close/source provenance remain necessary before P6 numerical/recovery acceptance. No timer/default/native cadence change is approved by this review.

## Verification

- Static whitespace check: `git diff --check 2c5fe0bb0 39c4579a3` actual exit0.
- Lint: NOT_RUN (no separate lint execution grant).
- Type-check/compile: NOT_RUN (root .NET window required).
- Tests/runtime: NOT_RUN; controls above are proposed, not executed or passed.
- No overall P6, ordinary performance, rich recovery, physical LAN or N02/N03 completion inferred.

## Subsequently authorized source corrections (not built/run)

Root authorized this reviewer to correct the three local issues after the findings were delivered. Original elapsed is now frozen immediately after the unchanged while-loop, before final diagnostic read; DecisionObservedAt and later PredicateObservedAt explicitly separate their observation times. ResponseElapsed uses the already captured genuine await-return timestamp before response diagnostics. Each slot retains its actual immutable CookingNetworkWireResult, including accepted-but-unprojected and rejected results; outer Save retains the bounded list on later failure. Shared RecordFailure preserves complete/outside-Complete boundary labels.

New explicit `--load-wait-controls` branch exercises actual helpers called by RunLoad: elapsed-only decision boundaries, frozen response-time arithmetic, received terminal versus projected counts, actual result retention through synthetic partial JSON serialization, unsent/readiness/prior-flight/send-failure labels, skip outcomes,350/+1 slots and4096/4097 text clipping. These are synthetic diagnostic-helper controls, not genuine gameplay/ET/UDP controls. Real accepted response with held projection and actual later recovery/export exception are still required at runner scope; existing30s is unchanged. No control was executed, no compilation granted to this reviewer, and no performance/whole-exit claim is made. Static `git diff --check` passes on owned source; root must independently review the new freeze and grant fresh compilation/control execution.
