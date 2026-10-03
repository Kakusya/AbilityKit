# N03 external measurement profile increment

2026-10-03. Root approves diagnostic-only follow-up to17218c56e; ownership restricted to measurement Program/Fixture/new same-directory helper and this report. Production Session/ET/domain/wire, bounds, publication cadence and fixture/business scheduling are unchanged. No20Hz/delta/history deletion/forcedGC/statichooks.

## Reviewed observation boundary

Existing owner synchronous duration/current-thread allocation and Session Diagnostics same-clock receive→map→result-send remain. New MeasurementProfile records GC.GetTotalAllocatedBytes(precise:true) at sample boundaries and CollectionCount(0/1/2) deltas, covering allmanaged threads of this SAME process (Host+bothclients+runtime), not a standalone Host allocation. Generation counts are nested and must not be summed as distinct collections. Precision APIs are called twice/window, noGC.Collect.

Each completed issued command records terminal observed by owner continuation→first fullbaseline covering committedStateVersion (including existing validation) and send→that projection. ExistingRTT stays send→terminal; neither is a rawpacket receive timestamp. Polling/delivery scheduling is included, and projection already available at terminal may yield nearzero waiting. It does not bypass the original wait before reusing the tool version.

After owner calls the helper observes only each client's immutable LatestBaseline.Identity.SnapshotSequence, recording distinct observed changes as LOWER BOUND, not actual published/sent/delivered count. NoSnapshotobject history retained and no perframe extraEncode/capture is introduced. Actual payloadbytes use existingSessionDiagnostics deltas; finalretainedbaseline encodedbytes are size only, not network cumulative bytes. Ownercall duration distribution is opaque aggregate; internalcapture/hash/codec/audit-copy stages have no externally available timing and remain UNMEASURED, not estimated percentages.

Observer captures its own currentownerthread allocation/time for polling and preciseGC boundary calls across the whole run. PostsampleSummary/outputserialization and simple latency assignments are outside that counter; observer numbers are not an exhaustive instrumentationoverhead estimate. Keep this comparability limit visible when comparing to the prior tool. Real allthreadsGC delta includes instrumentation within the sample naturally.

## Preserved controls

Same truetrustedFactory, oneET authority, twoemptytools/twoindependentanchors,10msownerpoll,10swarmup+60s5offered/s eachplayer,3freshrepeats pert topology, oneinflight/backpressure/scheduler accounting. Originalreal16/17/cachedduplicate+idleclock invariance, percommandfullprojection, fullbaselinehash and finalReady/currentbinding/sameframecapture checks retained. Two topologiesInProcess/SameMachineUdp separately; physicalLAN NOT_VERIFIED and performanceUNSET.

## Status and proposed actual commands

### Diagnostic field schema and failure accounting

- `externalProfile.allThreadsManagedAllocatedBytes`: precise process-wide managed cumulative allocation delta across the observed sample window; bytes, not retained heap size.
- `gcCollectionCountDeltas`: ordered generation 0/1/2 counters; nested counters, not three independent collection totals.
- `observedWindowMilliseconds`: actual monotonic boundary interval, distinct from the nominal 60-second offered schedule.
- `observedBaselineIdentityChangesPerClientLowerBound`: two counters of distinct sequences observed after owner calls; intermediate identities may be missed.
- `ownerCallMs`: count and p50/p95/p99 of opaque synchronous owner calls during the sample window; no internal stage attribution.
- `observerOverheadMilliseconds` and `observerOwnerManagedAllocatedBytes`: helper observation/boundary costs over the entire run, excluding report construction and latency assignments.
- `terminalObservedToCommittedProjectionMs` and `sendToCommittedProjectionMs`: completed issued sample cohorts, including the existing full-projection validation before the next operation. The first begins when the terminal continuation runs; neither starts at a raw network receive callback.
- `projectionCompleted`, `terminalTimeouts`, `projectionTimeouts`: explicit sample counts, distinct from existing terminal `completed`. Failure artifacts include each failed operation's phase, stage, timeout flag and original exception. A terminal result followed by projection timeout remains terminal-completed and projection-incomplete.
- `activeFailureEvidence.diagnosticWindow`: started/ended flags, observed counts and available raw boundary counters. It does not invoke the guarded successful Summary, finalize an incomplete window or replace the original failure with a diagnostic guard exception.

Static review: staged range remains empty; `git diff --check` passes. Other workers' rich-recovery and process-measurement files remain untouched. No compile or actual new diagnostic run has occurred. Root's planned next window is ControlOnly for both topologies, followed by three fresh repeats per topology; execution still awaits that explicit window grant.

Source modified onlywithin owned files; no.NET while rootrichworker owns the window. Next after explicitrootgrant: existingPSControlOnly build/control, then default3freshInProcess, then3freshSameMachineUdp only when granted. Retainoriginalartifacts/diagnosticred/green/rawcounts and report everyrepeat. Capture costs cannot be attributed to internalstages from these observations alone; follow-up instrumentation of production requires separateapproval.

## Actual granted-window verification and provenance

Root granted the exclusive .NET window after master Cooking815/ET328 and SDK311 gates. No broad recovery-tree test ran. All four commands below actually exited0, sequentially, from `C:/Users/Administrator/orca/workspaces/AbilityKit/cooking-network-recovery-current`:

1. `powershell -ExecutionPolicy Bypass -File tools/run-cooking-network-measurement.ps1 -Topology InProcess -ControlOnly`; outer log `local/Logs/profile-control-inprocess.log`, artifact `20261003-043935-3245247`.
2. Same command with `-Topology SameMachineUdp -ControlOnly`; outer log `profile-control-udp.log`, artifact `20261003-043950-0445466`.
3. Same runner with `-Topology InProcess -NoBuild`; outer log `profile-samples-inprocess.log`, artifact `20261003-044001-1247420`.
4. Same runner with `-Topology SameMachineUdp -NoBuild`; outer log `profile-samples-udp.log`, artifact `20261003-044342-8372946`.

All artifact paths are under `local/Logs/cooking-network-measurement/`. Both controls built the actual project and each executed16accepted, seventeenth rejected, cached original duplicate and final-readiness assertions. Both full commands independently repeated the capacity control before3fresh authorities. No compile/control/sample failure occurred in this increment; no fabricated red/green claim. Original172 measurement artifacts remain unchanged.

Executed source HEAD was `111c4b055d6caadcf968eac624967a53407b8c0c` plus this profile's owned Program/new helper WIP. Every artifact retains source-commit.txt/source-status.txt/sdk-info.txt/executable-hash.json/build.log where built, run.log and measurement.json. Dirty status also contained the other worker's new NetworkProcessMeasurement project/wrapper/report, which is not referenced by this measurement project and was not staged here. SDK actual10.0.300, Windows win-x64; no globally pinned SDK claim.

All four runs used identical measurement executable SHA256 `2C61ABC87D25C83791866D047DCF421A14C020CE775D97E055E32696EF46E0AD`, ET MVID `80c3eed7-7074-4b58-a476-2b00bfd252b0`, Session MVID `11a40961-f635-478e-a482-a012db9b6112`. Measured Program source SHA256 `1A17E1F94252292D9939FE7014C53F90B36F7BE276B7E46A4F5C437F4E5F8AF4`; helper SHA256 `C72ED87B957F4D7A5E48DD2771CF6BEA06A333F91B2AB95F7F146C129E1EC89F`. No code edits followed the measured runs. Full sample processes were PID24864 (InProcess) and19360 (same-process UDP); each exited0. Both use one Host plus two clients in the SAME process, never physical LAN or independent-process proof.

## Six actual fresh sample results

Each repeat has600sample offers (300per player),100warmup offers, exactly issued=ET admitted=accepted=terminal completed=projection completed, and zero rejected/cancelled/pending/scheduler misses/terminal timeouts/projection timeouts. Offers reconcile with issued+explicit backpressure skips. Every finalReadiness.asserted is true; current instance/scope/generation/synchronized clients and same-owner-frame authoritative capture are actually checked, as are full issued baseline hashes. No asynchronous clientworld==serverlatest requirement was invented.

| Topology/repeat | Issued/completed/projected | Backpressure | All-thread managed allocated bytes | GC collection deltas 0/1/2 | Actual observation window ms | Observed identities client0/client1 lower bound | Final baseline bytes/tokens |
|---|---:|---:|---:|---|---:|---|---|
| InProcess1 |570|30|49409101896|1343 /1140 /768|60108.1146|607 /607|2469676 /161614|
| InProcess2 |582|18|50084344712|1273 /1057 /688|60073.8382|594 /594|2528974 /166652|
| InProcess3 |574|26|50914848352|1309 /1081 /713|60094.9676|605 /605|2515626 /167056|
| SameMachineUdp1 |73|527|12006092448|259 /153 /92|59987.6226|74 /73|3251022 /427258|
| SameMachineUdp2 |73|527|11976722480|235 /132 /69|60002.4950|72 /72|3339488 /439858|
| SameMachineUdp3 |73|527|12009411768|232 /134 /66|59995.4045|73 /72|3286130 /432828|

GC counters are nested generation counters, never sum them into an independent collection total. Managed allocation is cumulative over the entire measurement process including client/background work, not retained memory or standalone Host. Observer helper totals over the entire run were InProcess1/2/3 1.7210/0.4856/0.5271ms and16888/16824/16824bytes; UDP1/2/3 3.3880/1.9836/1.9361ms and66088/66024/66024bytes. These do not cover post-sample report/JSON construction or latency assignments, nor prove zero instrumentation impact. Fixed nominal60s remains the throughput denominator; actual diagnostic boundaries above disclose owner-loop sampling jitter.

| Topology/repeat | Terminal RTT p50/p95/p99 ms | Terminal observed to committed projection p50/p95/p99 ms | Send to committed projection p50/p95/p99 ms | Opaque owner-call count; p50/p95/p99 ms |
|---|---|---|---|---|
| InProcess1 |100.0088 /155.7271 /159.5021|0.0033 /0.0048 /0.0055|100.0132 /155.7274 /159.5025|607;73.1755 /135.2104 /140.4337|
| InProcess2 |107.0372 /156.8454 /161.7784|0.0034 /0.0047 /0.0053|107.0375 /156.8500 /161.7823|594;77.3453 /137.5708 /142.1188|
| InProcess3 |107.7778 /155.1486 /164.5122|0.0033 /0.0047 /0.0054|107.7782 /155.1528 /164.5162|605;76.1950 /135.8788 /142.5846|
| SameMachineUdp1 |614.9334 /1388.0947 /1549.6793|786.9404 /1617.2099 /1706.0587|1395.7452 /3019.5009 /3255.7380|3588;0.6162 /3.1437 /34.5509|
| SameMachineUdp2 |623.7014 /1487.7152 /1641.0989|822.8670 /1663.2594 /1784.9320|1437.3929 /3126.8889 /3426.0309|3579;0.6228 /4.0902 /33.4654|
| SameMachineUdp3 |613.3385 /1497.7015 /1651.3176|780.7498 /1676.9202 /1749.1894|1404.6514 /3140.6050 /3377.5854|3564;0.6184 /2.9928 /36.7985|

Each latency distribution's sample count matches issued count in the table. p95 of the sum is measured directly; it is not calculated by adding independently selected percentiles. InProcess frequently has the full projection installed before the terminal continuation; nearzero subsequent waiting is expected with synchronous callbacks. UDP requires substantial additional full-projection waiting after terminal; closed-loop throughput is73/60=1.2167commands/s aggregate,527/600offers were not sent due to backpressure. These observations support inspecting full publication/ACK/projection costs, but cannot isolate network retransmission, encoding, capture or GC-pause component percentages. Internal stage timing remains UNMEASURED. The prior owner-only allocation and new all-thread allocation have distinct scopes; their numeric difference cannot be claimed as optimization.

## Freeze and remaining exits

`git diff --check` passed after runs. Only owned Program.cs, new MeasurementProfile.cs and this report are to be committed. Production source, fixture, bounds, tick scheduling, receipt history, offered settings and correctness gates are unchanged by this increment. Both sample processes have exited and the exclusive .NET window is released; remaining observed dotnet processes were MSBuild nodeReuse services, not measurement/testhost, and none was killed. Root independent review/integration and any appropriate master compilation remain separate. Physical two-PC remains NOT_VERIFIED, performance threshold UNSET; no formal product-performance acceptance, stage completion, delay/loss experiment or Unity evidence is inferred.
