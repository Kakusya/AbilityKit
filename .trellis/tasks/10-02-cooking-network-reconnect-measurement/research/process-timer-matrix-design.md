# Isolated process timer 2x2 matrix design

2026-10-03. DESIGN ONLY, pending root review and separate source/execution grants. This diagnostic does not adopt timer resolution or native cadence into production. It preserves the complete N02/N03 objective; physical NOT_VERIFIED, Unity deferred. Generic transport default15 and every original artifact remain unchanged.

## Motivation and hypothesis

Root's actual nine-group net10 primitive calibration terminated native0: OFF WaitOne(1) median15.5102ms, ON1.0145ms, restored OFF15.5099ms; Task.Delay(10) OFF15.5062ms, ON10.0167ms, restored OFF15.518ms. These are reported calibration results, not a transport profile or native wake trace. Existing equal-byte mid profile263047B was approximately150-170ms at requested native15 and1. A requested1ms period is not an observed1ms update period.

Pinned LiteNet2.1.4 UpdateLogic uses AutoResetEvent.WaitOne(UpdateTime - elapsed integer milliseconds). The process timer request also changes actual Task.Delay(10) behavior. Hence this matrix tests a combined scheduling intervention; ON-vs-OFF cannot isolate native scheduling from owner wait, thread-pool scheduling, contention, GC or observation work. Native update/wake timestamps remain absent. No claimed latency floor, attribution percentage, causal native win or product acceptance from this design.

## Fixed experiment

| Requested native period | Process timer request | Fresh paired repetitions |
|---|---|---|
| 15ms | OFF | 3 |
| 1ms | OFF | 3 |
| 15ms | ON (timeBeginPeriod1) | 3 |
| 1ms | ON (timeBeginPeriod1) | 3 |

Exactly12 rows/24 fresh Host/Client processes, one unchanged frozen Release build. Both endpoint processes in a row use the same options; wrapper itself makes no timer request. Unique run IDs/server instances/PIDs per row; actual pending/gen/issued/ACK/Ready checks remain. Milestone mid means128 actual accepted producer commands with all their ticks, routes, captures, receipts and full history retained. Compare source-backed producer accounting and full business graphs, not bytes alone. Host-frame counts/timing may differ as scheduler changes; record every actual tick, never pad/prune/normalize unexplained differences. Reject a partial producer row as NOT_MEASURED.

Run serial repeat blocks with fixed alternating order to reduce simple temporal bias: repeat1 15/OFF,1/OFF,15/ON,1/ON; repeat2 reversed; repeat3 repeat1. Persist planned and actual order and start/end UTC. Every row retains existing180s endpoint,30s operation/producer,5000 producer-frame and all queue/report/wire bounds. Requested owner await remains Task.Delay(10) everywhere; do not change to calibrated compensation, spin, yield, manual update or TriggerUpdate. Keep original profile initial/final modes intact; matrix is a distinct opt-in mode restricted to mid, never an accidental truncation of original Profile.

## Concrete isolated source ownership proposal

Only managed cooking-network-cadence-o03 tree, after existing producer terminal and independent source review. Proposed owned application files:

- NEW src/AbilityKit.Game.Cooking.NetworkCadenceProfile/ProcessTimerScope.cs: Windows-only local P/Invoke wrapper and injectable native-call seam for controls.
- Program.cs: validated optional process timer OFF|ON, defaultOFF; reject unsupported/missing/duplicate values before production of a row. Never silently fall back ON to OFF.
- ProfileRunner.cs: acquire timer scope before endpoint work, keep it through transport disposal, then release; preserve requested owner10 and current producer/fullgraph behavior. Cleanup failures must produce failed status/native nonzero, not a previously written pass.
- ProfileEvidence.cs: immutable requested/effective timer evidence and cleanup state, numeric native return codes and UTC/Stopwatch brackets; schema version/compatibility explicit.
- Existing tools/run-cooking-network-cadence-profile.ps1: separate TimerMatrix mode, exact12 mid rows and options-aware supervisor identity/receipts/summary, force compile provenance covering new source and script.
- NEW focused process-timer controls in an explicitly reviewed test harness/project; do not make production Cooking depend on winmm.

No generic transport/UPM, LiteNet window/MTU, recipe, Session authority/wire/history, existing acceptance runner, .NET timer configuration or OS policy edits. SDK app project wildcard includes its new .cs; no Unity file or asmdef dependency is needed. Production default constructors stay15.

## Timer lifecycle and provenance

OFF invokes neither native Begin nor End. ON requires OperatingSystem.IsWindows(), calls timeBeginPeriod(1) once per endpoint before work; nonzero result fails row immediately with no effective ON claim. Track successful acquisition; exactly one corresponding timeEndPeriod(1) is attempted in an outer finally even if endpoint work/export or transport disposal fails. Ensure individual endpoint resource cleanup exceptions cannot bypass timer cleanup; retain original and cleanup failures together. No End when Begin failed; repeated Dispose must not issue a second End. No finalizer-based success, no leaked nested acquisitions, no retry that hides native failures.

Final successful report/exit must occur after confirmed timer release. Preserve report export failure receipts and failed stage if final export fails. Include OS/build/runtime architecture, requested and actual API outcomes, native cadence assigned both ends, SDK/HEAD/dirty/source hashes, loaded DLL SHA/MVID/runtimeconfig, original compiled inventory, actual native process exit and hardware/power/contention before/after. Name effective state BEGIN_SUCCEEDED, not a claim of measured1ms native wake. Describe Windows-version/process applicability without claiming global resolution restored by one endpoint's End; external requests may exist. Fresh endpoint exit prevents this tool intentionally retaining its own request between rows.

## Verification and interpretation gates

Source review first, then serial root .NET grant. Deterministic seam controls: OFF zero native calls; ON Begin/End exact1/1; work exception still End; Begin failure no End; End failure nonzero outcome; duplicate Dispose idempotence; invalid CLI; non-Windows ON rejected. Real Windows control must preserve native Begin/End receipt and actual exit, without sockets before matrix grant. Existing generic/ABI controls remain accepted for unchanged sources; no blanket rerun solely for this app option. Rebuild actual application with audited fresh compiler/input/binary inventories; verify no stale output reuse.

Require both full typed endpoint controls and source reviewed timer lifecycle before twelve rows. Preserve all raw failures and every original trace/capture/resource/component/report. Wrapper verifies exact3 fresh rows per cell,128 accepted producer commands, actual deep identity/readiness/final projection and all native0; missing/partial/cleanup/provenance failures are NOT_MEASURED, not healthy missing data. No retry-to-fill silent replacements. Original paired timestamps remain per-machine monotonic; no fabricated socket or wake timestamps.

Analyze per repeat distributions and all raw component intervals; retain nested/overlapping timing caveats rather than adding them or subtracting observer overhead. Compare native1-vs15 separately within OFF and ON; compare ON-vsOFF separately within15 and1; show cell counts, actual owner/frame intervals and allocation/GC/contention. An interaction is diagnostic evidence of this whole experiment, not proof of native-only mechanism. Unchanged full byte equality alone is insufficient workload equivalence. No target tuned around observed profiles.

Ordinary reference285/300 and100/250ms terminal,200/400ms projection gates remain distinct and unmet. This12-row matrix is not those12 ordinary reference samples, not rich4/P6/physical acceptance, and grants no production timer/cadence adoption. A subsequent proposal requires explicit root review of measured benefit, CPU/resource effects, supported Windows applicability, cleanup behavior and affected full application gates.
