# N03 bounded measurement increment

2026-10-03, natural_operating_implement. Root-approved local engineering scope; original N03 higher-load/physical exits remain deferred. Owned files: new src/AbilityKit.Game.Cooking.NetworkMeasurement/ directory, tools/run-cooking-network-measurement.ps1 and this report only. Cold-recovery worker owns separate ET tests/report; no existing production or helper file changed.

## Implementation

Real trusted ICookingPreparationGameplayFactory builds a minimal validated configuration/layout, two logical players at separate anchors and two empty container tools. It creates exactly one simulation under the existing CookingLevelEtHost.BeginPreparation path. Adapter and Session use that authority; framed InProcess clients use Session.CreateLocalClientTransport; optional SameMachineUdp uses actual loopback LiteNet sockets in the SAME process. No fake port or client-side authority, no long movement/menu workflow, no Unity.

The console links the existing NetworkAcceptance SingleThreadOwner helper without editing it, and keeps owner continuations on the ET construction thread. Two clients independently alternate legal Pickup/Drop using each last confirmed full projection's item version. A participant has one inflight operation; its next operation requires accepted terminal plus committed projection. Offered ticks are every200ms; busy opportunities are explicitly skipped and late scheduler opportunities are separately skipped, never catch-up burst. Warmup10s + sample60s gives each participant50 warmup/300 sample offered opportunities. Three repetitions recreate/dispose the entire Host/Session; history is not cleared within a run.

Before measurement a dedicated ReceiptCapacity16 control executes16 real accepted commands, requires actual seventeenth ReceiptCapacityExceeded and verifies the original stable ID still yields a cached duplicate. Rejection/duplicate business invariance compares complete recipe canonical after normalizing ONLY the idle clock/version/event-sequence/tick-audit fields; it also checks their actual equal deltas and retained tick-event count. This is necessary because real ProcessOwnerFrame advances an empty fixed tick even when a command is rejected before admission; no production clock is bypassed. Other items/poses/containers/orders/allocator/receipt/events are compared unchanged.

## Metrics and failure artifacts

Success requires every sample offer accounted for by issued/backpressure/scheduler-skip, all issued legal operations real ET-admitted and accepted, all tasks terminal-completed, one authority still Preparing and valid full issued baseline hashes. Rejected/cancelled/pending success counts are zero assertions, not fabricated throughput. On failure active evidence records actually observed offered/issued/admitted/accepted/rejected/pending/results and last full capture/diagnostics; the wrapper retains logs and nonzero exit.

RTT is client-local send→terminal. Host receive→map→result-send uses Diagnostics.Timings in one monotonic domain, with a separate receipt-in-sample cohort. Owner GC/timing wraps only synchronous ProcessOwnerFrame; CPU/memory cover BOTH clients and authority in this process, memory is sampled rather than OS lifetime peak. Payload bytes/rejections use sample-boundary differences; queue high-water remains explicitly cumulative warmup+sample. Final baseline byte/token sizes, config, instance, full capture and client issued hashes are recorded. Performance threshold UNSET; physicalTwoPc NOT_VERIFIED.

Wrapper records SDK info, source HEAD/dirty status and executable SHA256, builds unless NoBuild, runs foreground and verifies3 repeat reports. Console operation/run deadlines bound execution; cleanup disposes only its own clients/Session/Host. No process enumeration/global dotnet kill, receipt deletion or enlarged wire budgets. CPU/performance results are unsuitable for remote-LAN conclusions.

## Verification status

Static PS Parser.ParseFile: no errors. git diff --check passed. NO .NET build/test/measurement yet: root's paired run/final gates own the machine window, then cold worker has first slot. Proposed next focused command: powershell -ExecutionPolicy Bypass -File tools/run-cooking-network-measurement.ps1 -ControlOnly (then full default InProcess measurement). Optional SameMachineUdp is a distinct later run, not claimed executed. Compilation and actual duration/counts must be appended after the granted window.

## Remaining limits

This is intentionally closed loop, not a strict sustained5accepted/s load generator: slow projection manifests in skipped offered opportunities. Sample emitted at second69.8 may finish during bounded drain; completed count cohort follows offered index while Host timing follows actual receipt timestamp. Diagnostics peak includes warmup; memory/CPU include same-process clients. Higher load/fault injection/physical LAN/formal thresholds are not included or passed.

## Actual ControlOnly verification

Root granted the serialized .NET window after master gates. Exact invocation: powershell -ExecutionPolicy Bypass -File tools/run-cooking-network-measurement.ps1 -ControlOnly, captured local-measurement-control-first.log. Initial compile succeeded (0 errors), then real layout Freeze rejected fixture ActorRadius default250 versus trusted policy40. Preserved failed JSON/log/build under local/Logs/cooking-network-measurement/20261002-214340-2548119. This is a fixture geometry mismatch, not a production failure or reason to relax validation.

Owned fixture now explicitly sets ActorRadius40. Same command rerun, captured local-measurement-control-radius-fixed.log, actual exit0. Report 20261002-214425-5311962:16accepted, actual17th rejected, old cached duplicate true, retained idleLogicalTickDelta2. No production or budget changes. Following approved full three-repeat InProcess invocation uses -NoBuild and the same corrected executable; actual results will follow, not pre-accepted.

## Actual three-repeat InProcess result

Exact approved command: powershell -ExecutionPolicy Bypass -File tools/run-cooking-network-measurement.ps1 -NoBuild. Wrapper stdout retained local-measurement-inprocess-first.log; actual exit0. Artifacts local/Logs/cooking-network-measurement/20261002-214447-1948773 (measurement.json, run.log, executable hash/source status/SDK). Corrected ControlOnly binary was reused, and the full command repeats the16/17/duplicate control before three freshly created authorities. No UDP or physical run was executed.

All repeats had600 sample offered opportunities (300 per participant),100 warmup offered, zero scheduler misses. Issued/admitted/accepted/completed sample counts were respectively578/575/570; backpressure skips22/25/30; rejected/cancelled/pending0. Each source assertion verified actual ET admission, full projection continuation, one authority and two empty tools. These values mean accepted throughput9.633/9.583/9.5 commands/s aggregate, not sustained5 accepted/s per participant.

| Repeat | Offered | Accepted/completed | Busy skipped | Final baseline bytes/tokens | RTT p50/p95/p99 ms | Owner-call managed allocation bytes | Sample process CPU ms | Sampled working-set peak bytes |
|---|---:|---:|---:|---|---|---:|---:|---:|
| 1 | 600 | 578 | 22 | 2484414 / 161410 | 107.461 / 157.527 / 167.589 | 48102621448 | 47531.25 | 317943808 |
| 2 | 600 | 575 | 25 | 2510092 / 165971 | 110.201 / 157.249 / 179.177 | 49537365152 | 47640.625 | 326430720 |
| 3 | 600 | 570 | 30 | 2493302 / 165008 | 109.581 / 157.664 / 167.072 | 49193959200 | 48140.625 | 322756608 |

Cumulative queue peak4 in each repeat. Default structural/byte budgets were unchanged; final complete baselines exceed the historical65536-token bound but remain within approved1048576-token/8MiB limits. Reports retain receive/consume/send distributions, payload bytes, all warmup counts and hash/config provenance. Performance target remainsUNSET. Allocation and latency values are substantial actual measurements; no efficiency/performance acceptance is asserted.

### Metadata-only GC clarification

During review/root approval, the original metricDefinitions sentence was found too broad: it said client/callback allocations were excluded. InProcess delivery can inline client callbacks on the owner thread within ProcessOwnerFrame; these allocations ARE counted. Accurate definition: all managed allocations on the owner thread during the synchronous owner call, INCLUDING inlined InProcess client callbacks, excluding other-thread/native/await allocations. Root explicitly approved this metadata-only correction after the binary completed. Program explanation and this report are corrected; original raw JSON is preserved, with this erratum rather than rewriting measured artifacts. Numeric measurements and executed logic did not change; no210-second rerun is required for this explanation correction.

All actual processes have exited. No unrelated files staged; ownership remains the five new files. Serial .NET window released after final verification/report freeze; higher loads, UDP, physical LAN and performance thresholds remain separate approvals/exits.

## Original SameMachineUdp run and strengthened exit gate

Additional topology explicitly approved by root. ControlOnly actual exit0 using frozen9e6 rebuilt explanation, artifact20261002-215115-6240986. Three-repeat SameMachineUdp wrapper actual exit0, artifact20261002-215130-8508562; no production/budget change, two clients and real Host remain in ONE process. Counts per600 sample offers:75/73/74 issued/admitted/accepted/completed,525/527/526 backpressure skipped, scheduler/rejected/cancelled/pending0. RTTp95=1445.449/1433.010/1461.956ms. Finalbaseline bytes/tokens3324018/437367,3244720/426358,3355630/442443. Owner-thread allocation6.179/6.125/6.187GB includes whatever inline callbacks run there; processCPU21843.75/22593.75/22156.25ms and sampledworking464146432/411906048/375005184bytes include all same-process endpoints. Actual session sendpayload236037497/233337342/237117665bytes within sample; queuepeak4. These measurements do NOT support sustained5 accepted/s perparticipant or physicalLAN/performance acceptance.

Root/reviewer identified a tool correctness exit gap: the original numeric/terminal assertions did not explicitly rule out loss of synchronization in the final idle interval. The old raw artifact stays unchanged and cannot be cited as passing the subsequently added final-readiness assertions. No unavailable/error output was observed in its raw report, but missing assertions cannot be retroactively assumed executed.

Root approved minimal shared exit Readiness used both ControlOnly and measurements: exactly2 expectedconfiguredparticipants, serverConnectedOwnerBinding/Ready/noCleanup/positivegeneration, clientsIsSynchronized and instance/participant/scope/generation matched to server, and adapter fullcaptureHash==Session.LatestCaptureHash at the same owner frame. Asynchronous clientworldhash need not equal serverlatest; no pause, clock trick or business mutation is used to force equality.

New shared exit gate ControlOnly actually built/passed under20261002-215806-4936833 (earlier compile-only control20261002-215729-8921788 also preserved). Full newsource revalidation order is InProcess3fresh then SameMachineUdp3fresh under the explicitly extended serial window; results pending, not pre-accepted.

## Final-readiness strengthened actual revalidation

Source tool correction only (Program shared Readiness + this report), no production change, no clock pause/stabilization or extra business action. After the shared ControlOnly green, exact commands sequentially: powershell -ExecutionPolicy Bypass -File tools/run-cooking-network-measurement.ps1 -NoBuild, then the same with -Topology SameMachineUdp -NoBuild. Both actual exit0; each reran capacity control and3fresh10+60s repeats. Original raw artifacts remain unchanged. Code indentation normalized after execution only; no semantic/numeric change.

Final InProcess artifact20261002-215837-9205945, final UDPartifact20261002-220224-3428631. Each six repeat JSON records has finalReadiness.asserted=true and both clients synchronized to the servercurrentinstance/scope/generation; exactlytwo configured boundReady/noCleanup participants and same-frame authoritative/sessioncapturehash assertions really executed. This closes the tool exit-gap for these runs, without requiring asynchronousclientworldhash==serverlatest.

| Topology/repeat | Offered | Issued/admitted/accepted/completed | Backpressure skipped | Baseline bytes/tokens | RTT p50/p95/p99 ms | Owner managed bytes | Process CPU ms | Sampled working bytes |
|---|---:|---:|---:|---|---|---:|---:|---:|
| InProcess1 | 600 | 580 | 20 | 2489290 / 161528 | 108.756 / 157.878 / 170.337 | 47885941608 | 46937.5 | 335953920 |
| InProcess2 | 600 | 578 | 22 | 2514998 / 165804 | 110.338 / 159.096 / 178.689 | 49206858224 | 48281.25 | 324677632 |
| InProcess3 | 600 | 571 | 29 | 2494024 / 164823 | 108.229 / 158.578 / 166.494 | 49118869136 | 47875 | 322248704 |
| SameMachineUdp1 | 600 | 73 | 527 | 3287512 / 432452 | 633.592 / 1463.529 / 1547.203 | 6085564024 | 22734.375 | 463540224 |
| SameMachineUdp2 | 600 | 73 | 527 | 3248210 / 426567 | 635.385 / 1404.667 / 1645.954 | 6142424344 | 21812.5 | 402325504 |
| SameMachineUdp3 | 600 | 73 | 527 | 3308604 / 435743 | 595.753 / 1488.811 / 1644.399 | 6082372976 | 21781.25 | 389038080 |

All six repeats: schedulerMiss0, rejected/cancelled/pending0, warmup100offered, actual capacity17th rejection separate. UDP cumulativequeuepeak4/4/3. UDPaggregateaccepted throughput73/60=1.217commands/s; sample offered600/60=10/s. Over87% of opportunities are explicitly backpressured, and neither table represents a formal latency/efficiency PASS. Default wire bounds unchanged; all final bytes/tokens remain within them. CPU/working-set include sameprocess realHost+two clients; ownermanagedallocations include inlined InProcess callbacks (corrected explanation in this actual rebuilt binary).

No packetloss/jitter/fault injection, physicaltwoPC, high-load4participant, or formal performance threshold was tested. Root must independently review/integrate frozen tool source; these local measured exits do not complete allN03/N02 gates. Serial.NETwindow released after both processes exited; only ownedProgram/report update committed.
