# N02 authority independent review

Reviewer: natural_operating_implement, 2026-10-03. Read-only production review of authority worktree commits 925d9f8db + 1446063b9 and the uncommitted Major projection capture/test increment. Only this report is owned by the reviewer. No .NET invocation and no transport self-review.

## Evidence and current disposition

N01 final design was checked against CookingNetworkAuthority.cs, CookingNetworkAuthorityAdapter.cs, CookingLevelEtHost.cs and the producer tests/report. The actual local/Logs/network-authority/authority-eighth.trx was independently inspected: 13 executed, 13 passed, zero failed/skipped. This establishes the producer focused run only; it does not establish SDK broad gates or the pending Major projection test.

Review is pending the two follow-ups below and the final frozen Major increment. No established production blocker was found in the inspected ordering/cancellation/outbox paths.

## Follow-ups sent to producer/root

1. Actual domain cleanup rejection remains unproved. Cleanup_admission_failure_is_structured_and_participant_stays_unavailable_until_recovery uses a stale batch and rejects admission before a domain Stop command executes. It correctly covers admission recovery, but does not cover N01's separate actual StopProcess/StopFrontWork domain rejection requirement. Add a real rejected domain cleanup result and prove participant unavailability persists until actual accepted release.
2. Busy reentrancy assessment: Host NetworkUnavailable (lines 383–392) checks tick/in-flight/preparation flags, but not _executingLifecycleOperation or _executingFrontOperation. RunLifecycleOperation (2176 onward) and RunFrontOperation (561 onward) explicitly track these states. Determine whether trusted callbacks can reach capture/control during either intermediate operation; if reachable, reject with Busy before exporting partial state. This is a static risk, not an executed product failure.

## Checked contracts

- Network ordering is opt-in and rejects installation with pending/in-flight work; default player ordering remains unchanged. Server cleanup receives internal ordinal zero, precedes user ordinal one, and cannot be selected through a DTO priority field.
- Source cancellation removes only matching pending callers and their reservation, emits caller cancellation without a logical terminal/history entry, preserves a surviving group's ordinal/fingerprint and permits a future first mapping after all cancellation. SourceConnectionId is generation-qualified by N01; the separate generation field is validated, not a substitute for that identifier.
- Reservations plus queued terminal notifications share the 2048 bound. Pending conflict notifies already accepted callers while the newcomer receives its disposition immediately; it does not allocate a 2049th queued notification. Per-group waiter bound is eight.
- TerminalizeRemainingIdentity directly materializes history without the normal network notification path. This was investigated but is not an established reachable network bug: canonical fingerprint includes SimulationBatch (Host line 75), so a same-identity cross-batch admission conflicts before a split duplicate group can form. Do not report the initial suspicion as a proven defect.
- Adapter derives cleanup from actual active recipe/front claims; paused disconnects defer without ticking. Failed cleanup retains the participant exclusion and retry bookkeeping. Cleanup uses normal domain commands, not grant mutation. Mixed batch/invalid ordinal checks precede cleanup/admission/tick.
- Capture retains unfiltered recipe tombstones/allocator/receipts/process/supply and full front state alongside observation, installed layout, preparation, identities and host/batch clocks. Faulted capture returns false/null before damaged exports; wrong-owner calls fail explicitly. Paused/Created views are distinct from resumable checkpoints.
- Major WIP now populates the previously DTO-only nullable required projection, cloning/read-only wrapping decoration and sorted unlock arrays. Global unlock display outside the active menu does not grant live definitions. The pending test checks snapshot immutability, JSON round-trip/required-field rejection and unchanged gameplay canonical state; its actual execution is not yet independently evidenced.
- Authorized transition remains an optional owner-supplied request-ID handler; no wire DTO imports a provider/store or arbitrary runtime grant. Ownership remains the existing Host/world rather than a second simulation.

## Limits

This review neither runs .NET nor declares the pending production/test changes complete. Producer focused evidence and independent source assessment must remain distinct from root integration/master broad gates. Final follow-up disposition should be appended after the producer freezes the reviewed changes.

## Final frozen re-review — ready for root integration

Re-reviewed frozen 021eb626c (including 925d9f8db/1446063b9) read-only. Independently parsed authority-display-busy-final-v2.trx: total/executed/passed 15, failed 0, notExecuted 0. Independently parsed authority-busy-explicit-red.trx: executed 1, failed 1. These are retained producer artifacts, not a reviewer-run .NET or broad gate.

The Busy finding is fixed: NetworkUnavailable now includes lifecycle, front-operation and authority-mutation flags. The real Factory.Create callback regression observes typed Busy, Accepted=false, State=null during Start, then successful capture after Start. It records the callback result and asserts afterward, so a callback assertion cannot be confused with a setup failure.

Major projection now exports copied/read-only decoration and sorted unlocks from trusted owned progress. The real factory test confirms future out-of-menu unlock remains display-only (no spawned item and unchanged full gameplay canonical checkpoint), old capture survives external progress mutation/locking, JSON round-trip preserves choices and missing required MajorProgress rejects. No provider/grant wire path was introduced.

Cleanup follow-up is resolved as a precise source-proven boundary rather than an invented executed branch: ChangeWorker (CookingSpatialInteraction.cs:211) Stop checks process existence, manual execution and matching ActiveWorker, and bypasses reach; StopFrontWork (CookingFrontOfHouse.cs:145) checks existing job and matching owner. Healthy owner-frozen capture selects those exact owned active manual/working claims, and generated cleanup precedes user mutation/advance. Under those preconditions domain rejection is unreachable; arithmetic/authority faults remain distinct. The producer now explicitly records that BatchStale is actual admission rejection and that domain-result CleanupRejected remains defensive, not exercised evidence.

Independent source review disposition: READY for root integration review/gates; no outstanding blocker found in this reviewed increment. Session codec/mapping/authorized ingress, actual socket composition, and root broad integration/master gates remain separate exits. No production files or other worker files were edited, and no .NET was run by this reviewer.
