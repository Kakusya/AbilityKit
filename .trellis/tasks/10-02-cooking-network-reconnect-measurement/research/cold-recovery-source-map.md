# N03 cold recovery: public API and evidence source map

2026-10-03; research only. No production/tests/task metadata edits and no .NET run. N03 remains planning; root owns the ongoing N02 representative process and integration gates. Unity and physical two-host LAN remain outside this local evidence.

## Read boundary

Read N03 PRD/design/implement/check manifest and readiness review, N01/N02 routing, completion-contract current stage, main singleplayer durable/recovery source and tests. Main read boundary: `da5be7e73ba3f24a6290d5cf0bbcc260c4d433de`. Current v3 Session/runner source read from root's `cooking-network-process-current`, HEAD `98396db9fc764f6b1594c9e3a48ae4db056a8c9b`, plus our frozen reviewed increments through `9b2d6e75e`. Do not assume these network sources are already merged into main: main currently lacks the v3 Session test file, and has root-owned runner/manifest changes. Future execution must freeze its actual selected source and manifests again. This research adds only this file and must preserve those other edits.

## Public APIs to compose, without a second simulation

| Step | Existing public API / owner | Source |
|---|---|---|
| Trusted content/geometry/Front/choices | `ICookingLevelGameplayFactory`; optional `ICookingPreparationGameplayFactory`, scoped Front/menu factory; independently trusted `ICookingConfirmedMajorChoicesGameplayFactory.CreateConfirmedMajorChoices(scope,configuration)` | `CookingLevelLifecycle.cs`, `CookingMajorBaselineHostResult.cs`, ET Host and fixture files below |
| Produce real successful durable target | `host.TryFinishService()`, `host.CompleteEnd()`, then `host.CreateSuccessor(LevelId,long,CookingLevelPreparation,CookingMajorProgress,CookingMajorCheckpointStore)` with already locked trusted choices | `src/AbilityKit.Game.Cooking.EtRuntime/CookingLevelEtHost.cs` |
| Dispose old owners | Dispose old clients, old `CookingNetworkSessionHost`, then old `CookingLevelEtHost`; retain bytes/metadata and old instance/token/packet identity for assertions only | New v3 Session files and Host.Dispose |
| Read a new store object | `new CookingMajorCheckpointStore(root)`; `ReadBaseline(CookingScope expectedMatch)` ? `CookingMajorBaselineRead(Accepted,Reason,Payload?)` | `src/AbilityKit.Game.Cooking/CookingMajorProgress.cs`, `CookingMajorBaseline.cs` |
| Reconstruct new trusted Host | `CookingLevelEtHost.LoadMajorBaseline(store,expectedMatch,configuration,factory)` ? `CookingMajorBaselineHostLoadResult(Accepted,Reason,BaselineReason,Host?,Progress?,Detail?)` | `CookingLevelEtHost.cs`, `CookingMajorBaselineHostResult.cs` |
| Inspect recovered readonly Created | `host.Observe()` and `new CookingNetworkAuthorityAdapter(host).CaptureFullState()`; no resumable Level image is invented for Created | Host capture and `CookingNetworkAuthorityAdapter.cs` |
| Build a new Session owner | `new CookingNetworkSessionHost(port,listener,joinCredentials,options?)`; `Start()`, `ProcessOwnerFrame()`, `ServerSessionInstance`, `LatestCapture`, `LatestSessionProjection` | `src/AbilityKit.Game.Cooking/Session/CookingNetworkSessionHost.cs` |
| Legitimate cold fresh connection | A **new** `CookingNetworkSessionClient(participant,credential,transportFactory?,options?)`, then `ConnectAsync` and exact internally issued ACK; `LatestBaseline` / `IsSynchronized` | `CookingNetworkSessionClient.cs` |
| Trusted lifecycle continuation | Existing host Prepare/BeginPreparation/CompletePreparation/Start on the same owner; for current Session harness route through `ApplyControl(ExecuteAuthorizedTransition,requestId)` with a trusted server callback registry | Host lifecycle methods and adapter constructor's authorizedTransition delegate; root runner registry is a reference, not a new authority |
| Actual new gameplay | `SendCommandAsync(stableWireId,CookingRecipeCommand{SimulationBatch=0,...})`; the Session maps current instance-qualified domain ID and owner batch | Client/Host + wire codec |
| Raw delayed/forged controls | Existing `ConnectionManager`, framed v3 `CookingNetworkWireCodec.Encode` and `Join/BaselineAck/Command`; owner consumes the same queue as normal clients | Session tests' raw helpers; no reflection or special domain bypass |

`LoadMajorBaseline` independently validates trusted config/preparation/Front/menu identities, confirmed choices and spawn/layout before owning and restoring the candidate kitchen. Payload choices are not grants. Successful load is Created, with external trusted factory retained. Read/load itself must not rewrite `major.checkpoint.json`; the recorded successor is success-only. Store format3, Recipe5, Level8 and wire3 remain distinct. The store has its own 1048576-character record bound; it is not the network 8MiB baseline budget.

## Concrete reusable fixture/test sources

1. `src/AbilityKit.ET.Runtime.Tests/CookingMajorBaselineEtTests.cs`:
   - `Durable_success_dispose_and_load_target_created_preserves_complete_continuation_and_pending_allocator`: actual `PrepareCarry` ? successful durable successor ? dispose scope ? new factory/Load ? exact Created and uninterrupted continuation canonical. Includes retained portions, stopped manual process, pending supply and allocator.
   - `Invalid_baseline_is_rejected_without_leaking_authority_and_valid_record_can_load`, `Rehashed_saved_choices_cannot_grant_buff_but_independent_trusted_provider_can_confirm_it`, `Rehashed_legal_alternate_seed_cannot_replace_trusted_target_spawn_and_rejection_releases_authority`, `Factory_io_failure_returns_structured_load_rejection_and_later_load_owns_a_clean_tree`: reusable rejection/no-grant/no-ownership-leak assertions.
   - Existing nested factories/helpers are private: later work must copy the minimal fixture into its owned new test file or explicitly coordinate extraction; cannot call them as public APIs or silently edit another owner's tests.
2. `src/AbilityKit.ET.Runtime.Tests/CookingTechnicalRecoveryEtTests.cs`:
   - `SuccessfulBaseline`, `Load`, `Factory`, `Files` implement real successful file publication, a **new store instance**, independent confirmed choices (including CookFaster and optional future global unlock), validated temporary-path cleanup and exact byte conservation.
   - `Actual_fixed_tick_fault_is_quarantined_and_dispose_new_store_load_recovers_last_success_only`: real allocator fault quarantines old Host; BeginEnd/retry/Tick unavailable; Dispose then fresh factory+store load recovers only last success, and genuinely completes later work. This remains separate from live healthy rebind, without a Faulted?BeginEnd bridge.
   - `Accelerated_inflight_process_survives_optional_same_level_restore_and_durable_created_cold_load`: rich retained work and trusted timing through actual durable load. `Trusted_global_unlock_absent_from_current_level_survives_cold_load_and_retry_without_spawning`: global progress survives without granting forbidden current-level stock.
3. Frozen v3 `src/AbilityKit.Game.Cooking.Tests/CookingNetworkSessionV3Tests.cs`:
   - `Created_readonly_join_disconnect_rebind_finishes_without_clock_advance_or_carried_hand_loss` (initial no-kitchen + actual Successor): authentic Created handshake/rebind, no ticks, exact full state and hand conservation. It is **live same-Session**, not cold restart evidence.
   - `Rebind_rotates_token_and_old_token_cannot_supersede_the_current_generation`: old live token rejection; no cold claim.
   - Raw framed helpers in issued ACK / one-awaiting-ACK tests provide packet injection and exact reply reasons; fixture also demonstrates immutable full state and instance-bound scope chain.
4. Frozen `CookingNetworkWireV3Tests.Instance_qualified_domain_ID_is_stable_across_transport_and_distinct_after_cold_restart`: deterministic 69-byte mapping test with supplied strings. It is a codec identity unit, not an actual disposed Host/store/Session restart.
5. Root-owned `src/AbilityKit.Game.Cooking.NetworkAcceptance/ProcessServiceFixture.cs` and `Program.cs`: real F01+D31 finite menu/scoped Front/config provenance, trusted timing and owner transition registry; Program publishes actual durable successor. The fixture class implements menu/scoped Front, not the nontrivial confirmed-choice provider. Its current empty locked choices are compatible; if a cold test introduces buff/unlocks/decoration, add a genuinely trusted provider in the test's owned fixture rather than grant from saved payload. Runner remains root-owned and its existing process result does not automatically include N03 cold load.

## Proposed cold matrix and exact observations

All rows below are planned, not run in this research. Prefer a deterministic InProcess first control; separately apply the same matrix to independent-process UDP and, when available, physical LAN.

| Control | Actual sequence | Expected result / reusable assertion |
|---|---|---|
| Successful cold Created | Publish a real successful target baseline; attach old Session and obtain its issued identity; Dispose old Session/Host; create new Store+factory; read and Load; create new Session | New Host and new instance; same saved target Match/runtime/Level/epoch; exact expected Created Observation/full Recipe/installed layout/watermarks and confirmed progress; unchanged file bytes; no business clock advance during readonly handshake |
| Old automatic reconnect | Old client object retains old instance/token across Disconnect/Reconnect; connect it to the new Session | `Unauthorized` Join, no Joined/baseline/Ready/worker claim; old display unsynchronized. This should not silently become fresh credential-only join |
| Old token relabelled as new instance | Raw Join uses real old token, actual new instance header and valid join credential on generation0 | Must reject; source gap and root decision below. No assertion of current implementation pass |
| Old instance with null token | Raw Join supplies old instance and null token on generation0 | Must reject under root's confirmed first-Join rule; otherwise an old identity can masquerade as initial join |
| Legitimate new identity | New client object has no previous binding; sends instance=null,token=null with valid credential | Joined new instance/generation1/new token; passive full baseline and exact ACK enable Ready. Current Client ConnectAsync already sends both null when `_binding` is null |
| Old issued ACK | On a new bound connection, inject exact old issued identity | `BaselineRequired`; cannot release a new pending issued snapshot, cannot confer Ready or scope/grant |
| Delayed old command before new Ready | Inject captured old-instance packet before handshake finishes | Existing check order permits `BaselineRequired`; do not demand ServerInstanceMismatch before binding/Ready |
| Delayed old command after legitimate new Ready | Same old packet on a correctly new bound Ready connection | `ServerInstanceMismatch` before command execution; no old domain ID, receipt or physical action installs |
| Old scope/epoch but correct new instance | New wire instance/generation with retired scope | `ScopeMismatch` once Ready; no domain execution |
| New command with same wire stable ID | After trusted Prepare/Start of recovered Host, issue an actual valid Move or other reproducible business operation under the new instance with the same stable ID as before disposal | Accepted real result, new instance-qualified domain ID distinct from old, current scope/batch and state version; not IsDuplicate of old receipt. Proves recovery is not permanent denial |
| Damaged/foreign baseline | Reuse genuine file then malformed record/wrong match/unconfirmed policy tests with new Store and trusted factory | Structured read/load rejection, null Host and no leaked owned candidate; untouched good bytes or restored fixture then actual later valid load succeeds |
| Faulted old Host | Real allocator fault and old Session unavailable; Dispose; new Store/load/new Session | No damaged export or live retry bridge; exactly last successful file target is recovered; old post-baseline purchase/work is absent by design |

For a zero-business-mutation wire rejection, readonly Created is a useful genuine phase: complete Observation/full checkpoint/Host frame canonical can compare exactly around owner consumption because no Tick occurs. Running owner frames legitimately advance business clock even when an input is rejected; do not mistakenly impose a clock freeze or compare unadjusted Running canonical to claim zero change. Use an independently loaded twin with the same empty owner frame for Running rejection controls if necessary. Session rejection counters/connection watermarks are allowed audit bookkeeping and are inspected separately.

## Explicit identity/persistence boundaries

`CookingRecipeSimulation.ExportSuccessHandoff` sets `Deduplication=[]`; handoff validator requires that emptiness. Therefore a normal Major success baseline is not a persisted old Session mapping/receipt recovery image. No claim that the above store test recreates an old mapped batch/ordinal from receipt hashes is justified. Session mapping is live-instance-only; a new Session uses its new GUID instance and `DomainId(instance,fullMatchLevel,participant,stableID)` fixed hash tag/length encoding. The same wire stable ID under a genuine new instance is intentionally a **new operation**. ?Reject old ID? means reject an old instance-qualified packet, not globally ban that stable string. Client-supplied RecipeCommandId is not authoritative and is overwritten by the server mapping. If separate same-level Running checkpoint restoration later verifies retained old receipt IDs, that is another explicit cold test using `Host.Restore`, not a substitute for the mandated successful MajorStore load.

Keep old and new artifacts in separate identities/directories; never record private token/credential values in public reports. Store checks compare raw byte hash, payload canonical, target scope/config/prep/Front/menu identities and trusted progress; network checks compare full Capture canonical/hash and Session projection, with exact issued identity included. New Session connection projections and tokens are expected to differ, not be forced equal to the disposed Session.

## Confirmed first-Join source gap for a later owned increment

At the source read boundary `CookingNetworkSessionHost.Join` checks:

- token!=null and supplied instance!=current ? Unauthorized;
- when participant.Generation>0, instance and token must both match current ? Unauthorized otherwise.

For Generation0 it lacks an explicit first-Join null-pair check. Consequently a nonissued old token relabelled with the actual new instance plus valid credential can pass, and an old supplied instance with null token can pass. Root explicitly confirmed this gap during this research: **Generation0 only permits instance=null AND token=null**; valid initial JoinCredential does not replace rebind identity validation. Client first Connect is already compatible (`_binding?.ServerSessionInstance` / `_binding?.RebindToken`, both null for a new client).

Recommended later N02 identity-boundary regression, separately authorized after root process/window release: genuine two-Session/cold store fixture captures an old token; new first Join permutations `(oldInstance,oldToken)`, `(newInstance,oldToken)`, `(oldInstance,null)`, `(null,oldToken)` all Unauthorized with zero binding changes; legitimate `(null,null)` succeeds and executes a true new command. Preserve live generation>0 current token acceptance, rotation and stale-token rejection. This is a source-backed unimplemented control, not an N03 or physical-LAN completion claim. This research itself does not change production.

## Remaining execution gates

N02 representative process, applicable broad SDK gates and actual selected source integration remain root-owned. N03 local cold controls need an explicit implementation/window assignment and frozen fixture/source, then actual red/green; load helper and codec units above are not an end-to-end cold network pass. Physical second host is unavailable (NOT_VERIFIED). Performance targets remain UNSET, and warm local timings do not establish smooth rich gameplay. No Unity work, migration authority or network mapping persistence is proposed.
