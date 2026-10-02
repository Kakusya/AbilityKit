# N02 authority increment - 2026-10-03

## Boundary and source

Base bdd5cbede. Shared pure-domain interface commit 8297ff788 contains CookingNetworkAuthority.cs; the following implementation commit adds its CleanupRejected enum member, existing ET Host narrow extension, new CookingNetworkAuthorityAdapter and new ET controls. No Session, transport, domain simulation, lifecycle/checkpoint schema, or Unity gameplay source changed. Recipe 5 / Level 8 remain unchanged. SDK projects compile these Cooking sources directly; there is no existing Cooking UPM asmdef to update. ET shared runtime asmdef remains unchanged.

Applied trellis-before-dev: read task artifacts, accepted N01 final contract/root acceptance, cooking/abilitykit specs, cross-layer/reuse guides, csproj and applicable ET asmdef. The actual gap is in Host pending arbitration/terminalization and full-state reading, not in another simulation. DTOs live in Game.Cooking; adapter implementation references them from EtRuntime, without reverse dependency.

## Implemented behavior

- Adapter constructor opts the existing idle Host into trusted ordinal ordering. Default singleplayer sorting remains Player/Command. Each newly mapped same-batch network prefix executes with one Host Tick; mixed batches reject before business admission. Session remains responsible for server-owned first batch/domain ID mapping and cached terminal retransmissions.
- Qualified source cancellation removes only matching pending envelopes and returns caller-only cancellation DTOs. Partial cancellation leaves other callers and real execution receipt intact. All-caller cancellation removes pending without a fake logical terminal. Session owns CancelledNoExecution/live-instance mapping and generation-qualified source construction.
- A bounded 2048-slot notification path reserves capacity for accepted network callers (8 waiters per identity). Pending conflicts notify every previously admitted caller; the rejected newcomer gets its immediate admission disposition, without a 2049th outbox slot. Notifications drain independently of logical receipt retention. Fault notifications can drain on the idle faulted owner thread.
- Capture reads existing committed Observation plus full Recipe (including hidden tombstones/allocator/receipts), Front, installed layout/seeds, preparation/config/menu identities and clocks. Resumable Level image remains optional under its existing ExportCheckpoint rules. Created without a kitchen and Paused are honest capture states; Faulted/Disposed/Busy return null state without reading damaged authority.
- Disconnect cleanup uses existing StopProcess/StopFrontWork through the same Host command lane, one batch and one Tick. Internal cleanup priority precedes user ordinal 1; no client-visible priority flag, fake participant or extra batch. Each adapter has a random reserved cleanup-ID namespace and counter, avoiding cold receipt reuse. Paused cleanup defers until Resume. Rejected cleanup is CleanupRejected, retains the unavailable participant, and retries next owner frame; input from that participant stays Unauthorized until recovery. This can happen after a frame advanced and must not trigger replay of the entire frame.
- Lifecycle controls are owner-only. Authorized transitions reference an optional application request handler; absence yields Unauthorized. No client provider, store or restore authority is introduced.

## Actual verification

Serial .NET window granted by root; released after final focused run. Exact command (logger changed per attempt):

`dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj --filter FullyQualifiedName~CookingNetworkAuthorityEtTests --logger "trx;LogFileName=authority-eighth.trx" --results-directory local/Logs/network-authority --verbosity minimal`

PowerShell stdout/stderr redirected to local-authority-eighth.log; byte-preserving copy at local/Logs/network-authority/authority-eighth.log. Final exit 0, actual 13 passed / 0 failed / 0 skipped, 320 ms test duration (about 3.2 s command). Build/type-check succeeded in that test invocation. `git diff --check` passed. Existing shared transport XML/nullable warnings appeared on the initial clean build; they are outside this worker's ownership and are not new Cooking errors.

Final controls:

- Actual_fixed_tick_fault_capture_returns_failure_null_and_dispose_releases_owner
- All_source_cancel_has_no_fake_logical_terminal_and_new_generation_is_not_cancelled
- Cleanup_admission_failure_is_structured_and_participant_stays_unavailable_until_recovery
- Created_and_paused_capture_are_readonly_full_state_not_resumable_images
- Default_singleplayer_still_sorts_by_player
- Disconnect_cleanup_precedes_user_ordinal_one_and_releases_manual_worker
- Full_capture_preserves_hidden_tombstone_allocator_and_front_while_paused
- Mixed_batches_reject_before_admission_tick_or_cleanup
- Notification_capacity_reserves_accepted_callers_and_conflict_newcomer_returns_immediately
- Partial_source_cancel_keeps_duplicate_group_and_true_execution_receipt
- Paused_disconnect_defers_cleanup_until_resume_without_clock_or_worker_mutation
- Pending_conflict_notifies_prior_and_new_caller_without_execution
- Trusted_ordinal_wins_opposing_player_race_in_one_tick

## Preserved failures and corrections

Logs authority-first through authority-eighth are retained under local/Logs/network-authority/. First/second were fixture compile errors (allocator interface name/constructor argument). Third had a malformed test preparation treating raw material as a container; assertion during factory setup leaked the test host into later singleton failures. The fixture now uses an empty container list and disposes on setup failure. Fourth was 6/7: Continue fixture sent ExpectedItemVersion 1, but real Continue requires 0; corrected without weakening production validation. Fifth was 9/9. Sixth found an ambiguous target-typed UseFrontOfHouse test overload; made the menu type explicit. Seventh was 11/11. Eighth is final 13/13, adding cleanup-rejection recovery and capacity reservation controls. These are fixture/debugging evidence, not claimed product bug red tests.

Root's static production findings were addressed before final verification: cleanup candidate staging precedes mutation for running mixed/overflow rejection; cleanup failure is explicit and blocks the participant; cleanup IDs have a per-adapter namespace. Actual rejected cleanup control uses a stale authority batch, yielding real Host BatchStale and preserving the worker until the following valid cleanup. No forced domain-state corruption or new stop rules were introduced.

## Remaining composition/exits

Session must enforce frozen wire v3 field presence/bounds, instance-qualified stable command mapping, fully generation-qualified source IDs, authorized lifecycle ingress, CancelledNoExecution and outstanding issued-baseline ack. Capture DTO is not an authority grant or a new recovery format. Domain required-null validation remains in existing schemas; the v3 codec must enforce its required capture fields as well. Adapter is not a socket callback API. No SDK-wide/Cooking-wide/ET-wide gate, framed Session round-trip, actual UDP process test, physical two-PC LAN or Unity pass is claimed here. Root independently reviews and integrates before broad gates.
