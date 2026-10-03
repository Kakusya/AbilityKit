# Independent-process fault/load increment

Status: source drafted, process runner NOT compiled or executed. Production Session changes are owned by the root agent.

## Scope

New NetworkProcessMeasurement project links the existing SingleThreadOwner helper and uses one existing ET host. Its separate UDP client drops exactly one actual Submit application response, observes the committed full baseline, performs live token rebind, and retries the original stable ID/payload as a cached duplicate. This is application response loss, not a UDP packet-loss claim.

The planned wrapper runs three fresh process pairs, each with two participants, 10 seconds warmup and 60 seconds sampling at five offered operations per second per participant. Pickup/Drop are independent real item operations. Busy and scheduler skips remain visible; accepted IDs are checked against actual ET admission. Final paused full-state consensus is required. Performance thresholds are UNSET and physical LAN is NOT VERIFIED.

Only new project, wrapper, regression tests and this report are owned here. Other dirty measurement/rich files belong to other workers and were preserved.

## Original-production regression evidence

Source HEAD: 852619887471f0ef479578d5c26c2cd2b03cecf2.

Focused command:

`dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj --filter FullyQualifiedName~CookingNetworkPausedPublicationTests --logger "trx;LogFileName=paused-publication-original-red.trx" --results-directory local/Logs/cooking-execution/network-paused-publication --verbosity minimal`

Actual exit 1: 2 failed, 0 passed, 0 skipped, 311 ms test duration.

- Old_pending_ack_releases_one_current_paused_baseline_without_ticks_or_same_state_ack_flood: expected third Paused baseline after exact old ACK, actual remained two baselines.
- Exact_ack_during_paused_cleanup_is_retained_until_resume_and_does_not_strand_ready: exact ACK received BaselineRequired while cleanup pending; after Resume/cleanup no Ready was delivered.

Log: `local/paused-publication-original-red.log`.
TRX: `local/Logs/cooking-execution/network-paused-publication/paused-publication-original-red.trx`.

The earlier `local/paused-publication-red.log` was compilation-only fixture failure: Driver.Simulation is not public. Test reads were corrected to the public CaptureReadOnlyFullState API before the actual production red. It is not counted as behavioral evidence.

No production fix was applied here. The .NET window was explicitly released after the two original-source failures. Root will provide bounded deferred-ACK and changed-state-only Paused publication fixes before green verification and process execution.

## Ownership continuation and source corrections

Root delegated the remaining runner WIP to ack_fix_review after the prior worker stopped. Current owned files are only NetworkProcessMeasurement new project, its wrapper and this report; DeferredAckIsolation tests/report are separately owned and must be committed separately. Rich tests and NetworkMeasurement profile files remain other workers' ownership.

Source reviewed against approved process-fault-load-plan and N03 design. Root ACK correction is the required production base, not modified here. Corrected lost Submit conservation gate now requires actual exact ReadyIdentity for committed complete baseline before close/rebind; Host final trusted Pause goes through Session.ApplyControl, then local synchronized full Paused projection/hash consensus before report; Host requires both configured connected Ready bindings instead of accepting remote already closed. Remote keeps polling its real connection for five seconds after final complete ACK so Host can capture paired provenance. Load schedule waits until full70seconds (10warmup+60sample) before final drain, and reports memory peaks sampled at350 offered instants/5Hz separately from OS lifetime peaks. Owner timing/allocation and raw Session monotonic timings remain whole-pair diagnostics, not isolated production component profiling.

PowerShell wrapper parsed with native Parser: zero errors. Actual .NET compile/tests/process runs remain NOT_RUN pending root's serialized window. No performance or physical LAN PASS is claimed.

## Source-only second audit and execution queue

2026-10-03 after root corrected master evidence7c06ac7db (815/328 and SDK311). Profile worker retains exclusive .NET window; no runner build/test/execution was started by this audit.

Source hardening found and fixed only in this owned runner:

- Raw UDP peer previously emitted Join immediately after Open. LiteNetTransport.Send requires actual Connected; now Open retains one pending Join and the client owner Poll sends it only once physical Connected is true. Callback still only decodes/freezes/enqueues/drops chosen reply and never controls domain.
- Warmup now reconciles offered50 against issued/accepted/backpressure/scheduler skips separately; all warmup and measured issued domain IDs are preserved and matched to actual Host ET admissions by wrapper. Fresh load IDs must return nonduplicate terminals. Sample completions after the70s window are explicitly counted as drain of the offered-index cohort.
- Reports include factory CreateCount1 and actual configuration identity, both complete final snapshots and exact ACK identities, final encoded baseline bytes/tokens with unchanged finite bounds. Wrapper rejects missing configuration identity, wrong loopback endpoint/port/machine/PID, repeated actual admission IDs, or broken warmup/sample accounting. The configuration SHA uses actual definition codec uppercase hex rather than the lower-case wire hash format.

Source-only parser returned0errors; .NET NOT_RUN. Current commands, to execute only after explicit root window grant in the recovery worktree:

1. `dotnet build src/AbilityKit.Game.Cooking.NetworkProcessMeasurement/AbilityKit.Game.Cooking.NetworkProcessMeasurement.csproj --verbosity minimal` (save first stdout/stderr and exit to local/Logs/cooking-execution/network-process-fault-load/build-first.log).
2. `powershell -ExecutionPolicy Bypass -File tools/run-cooking-network-process-measurement.ps1 -Repeats 1 -NoBuild -OutputDirectory local/Logs/cooking-execution/network-process-fault-load-diagnostic` (one actual pair, preserve failure artifacts; no acceptance claim for3repeats).
3. After diagnosis/source freeze and root approval: `powershell -ExecutionPolicy Bypass -File tools/run-cooking-network-process-measurement.ps1 -Repeats 3 -NoBuild -OutputDirectory local/Logs/cooking-execution/network-process-fault-load-final` (three fresh pairs; six actual endpoint exits and three paired reports).

Wrapper readiness30s, each pair process deadline300s; runner240s overall, fault phase120s, connect15s, raw operation30s, local actual productionClient15s. Warmup10s/sample60s/5offered opportunities per player per second. Local/remote legal Pickup/Drop markers are verified through real accepted domain receipts; no new wire controls or file mutation grants. Finally stops/disposes only exact Process objects launched by that invocation; Handle is retained before Refresh, WaitForExit occurs before reading nonnull exits. No global process termination or worktree deletion is authorized here. This remains synthetic same-machine independent-process fault/load, not full-menu UDP, random network packet loss, physical LAN or formal performance PASS.

## Actual diagnostic red/green and three fresh pairs

Root granted this worker the sole .NET window after profile source/sample release. First build exit0 with one owned nullable CS8602 warning; explicit already-validated FullRecipe dereference annotation corrected it. All subsequent builds including `local/Logs/cooking-execution/network-process-fault-load/build-final-provenance.log` exit0/0warnings/0errors.

Actual diagnostic attempts retained in `local/Logs/cooking-execution/network-process-fault-load-diagnostic/`:

1. `20261003-044856-1225440`: wrapper exit1; marker Drop after actual lost-Submit/rebind reached domain rejection. Original message lacked domain reason, so details were added to runner-only failure logging.
2. `20261003-044932-7231670`: wrapper exit1; actual Drop rejected TargetOutOfRange13. Fixture had world tool locations but no Spatial; production requires real EffectiveSpatial for world Drop. Fixed only trusted fixture by adding two separated actor poses, independent real tool world anchors and remote board/counter station anchors through public registry/fixture Spatial. No production rule relaxation or alternative operation to bypass Drop.
3. `20261003-045038-9355865`: Host endpoint passed; client failed `Final ACK hold disconnected`. Two fixed5s holds raced because Host starts earlier than remote final ACK. Corrected bounded runner close-handshake: freeze/validate both ConnectedReady/Paused/noCleanup final report gate, then Host pumps at most30s until actual remote close. Post-close connection cleanup is not claimed simultaneous Ready. Original reports preserved.
4. `20261003-045314-4708009`: both endpoints passed/full hash equal, wrapper exit1 on configuration shape mismatch (actual Host typed identity DTO versus client actual lifecycle canonical string). Host now preserves actual typed DTO plus its canonical ToString; wrapper checks two nonempty actual canonical strings with definition-v3/64uppercaseSHA and exact equality. No original reports rewritten or expected fixture identity substituted.
5. `20261003-045550-5554323`: whole wrapper exit0, one actual paired proof with both actual exits0. This diagnostic is NOT counted among the official three.

Only provenance metadata then added: actual Stopwatch.Frequency and owned/linked-wrapper source SHA files. Build-final-provenance remained0warnings/errors. Official command:

`powershell -ExecutionPolicy Bypass -File tools/run-cooking-network-process-measurement.ps1 -Repeats 3 -NoBuild -OutputDirectory local/Logs/cooking-execution/network-process-fault-load-final`

Actual command exit0; artifact directory `local/Logs/cooking-execution/network-process-fault-load-final/20261003-045802-8496423`, stdout `local/Logs/cooking-execution/network-process-fault-load/final-three-first.log`; summary.json passed=true/repeats3. All six endpoints actually exited0; each pair has one new actual ET authority, fresh server instance, frozen complete shared gameplay hash, both configured ConnectedReady generations1/2 at final acknowledged Paused gate, null resumable checkpoint, no pending cleanup there, one intentionally dropped real Submit response, exact acknowledged committed baseline before closing, rotated-token live rebind and original-domain cached duplicate with one settlement/two consumed cup+product records and six unchanged pre-marker receipts. Closing after frozen report is separately observed and does not advance paused gameplay clocks.

| Repeat | Host/client PID | Sample local accepted/backpressure | Sample remote accepted/backpressure | Remote RTT p95 ms | Final client wire bytes/tokens | Owner managed bytes, whole pair |
|---|---|---|---|---|---|---|
| 1 |31936/1520|300/0|44/256|1081.7538|1103600/96259|51744570064|
| 2 |43200/13460|300/0|48/252|994.0031|1116907/97583|53638464016|
| 3 |16424/33004|300/0|44/256|1042.7261|1099429/95659|51123939648|

Each participant offered300 sample and50 warmup opportunities. No sample scheduler skip/rejection/cancellation/pending; warmup local50/50/50 accepted, remote26/25/26 accepted with its other opportunities explicitly accounted. All sample AND warmup issued domain IDs match actual ET admissions; fresh load terminals are nonduplicate. Sample RTT cohort is offered-index terminal response; it includes explicitly reported post-window drains. CPU covers that process's whole load/warmup/drain, memory peak is350offered-time samples (not OS lifetime peak); owner allocation covers whole pair and includes synchronous local callbacks while excluding other-thread/native/await. Stopwatch frequency10000000 is recorded, enabling raw Host receive/map/result-send timings to be converted in one clock domain. Payload counters/queue peak are cumulative and exclude wire framing/UDP overhead. These costs/backpressure are substantial; performance thresholds remain UNSET, no smoothness or performance PASS is asserted.

Pair identities/hashes:

- 1 instance `dfc62978b51549d5a139b8c1838c2617`, hash `424fbdb007b2e141061b753fe86e82b2811d6aee987a3e9b9de47b7450c7be68`.
- 2 instance `ad1b68f7d1c94c838d5d2e3e1b8941ff`, hash `6fa1a84705d4ed1571f723ce3ed2a4e8be944b953eedea9c727eacb7a84076d4`.
- 3 instance `92450361789b410aaede51f0cba11e6b`, hash `5e108c5b165a0979b27ff8917d20dbf02376f1840d1bb701e7baad6c7f9a5f8f`.

Build: ET MVID `894a8784-a2b9-44b1-8646-9f90519b144e`, Session MVID `e61d0035-5d0c-4423-8255-2822d5ca9b8c`; runner SHA256 `62EFBD007FA2733E8E48B1264AD47D068FD80AD7BE899F708EDEFF091CEA44F6`. Definition canonical identity `cooking-definition-v3:D10B3A7B645D8124860C1DB48FD5F57BA6799EA66B6EB8670A8FD3524879CCBA`. Source HEAD/dirty, SDK info, executable SHA, all four owned source hashes and linked SingleThreadOwner/wrapper hashes preserved at directory root. Report-only appendix does not alter executable input. Complete final Host capture/client baseline remain in endpoint JSON for independent rehash, not only string comparison.

Serialized .NET window released immediately after official command termination; root may now run other tests. No production file changed, no unrelated WIP staged, no external publication or worktree cleanup. This is bounded synthetic same-machine independent-process reliable UDP + local framed participant evidence. It is NOT full-menu UDP, actual two-PC LAN, random UDP packet loss or formal performance acceptance. Master import/review remains root-owned.
