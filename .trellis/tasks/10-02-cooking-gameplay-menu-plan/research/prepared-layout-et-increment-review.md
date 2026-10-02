# Preparing layout installation and same-Level recovery

Base: integration d9b0e5603. Reviewer ownership: CookingLevelEtHost.cs and new CookingPreparedLayoutEtTests.cs only. Root supplied explicit core layout admission, optional restoreReferences APIs, configured appliance reader, EffectiveSpatial handoff seed correction and old Preparing fixture targets. No commit by reviewer.

## Implemented behavior

- Optional preparation factory's initial layout is actually projected and installed in its single initialized kitchen before publication. Trusted policy, equipment definitions, station bindings, immutable fixture supplier anchors and real item/process locations are validated. No fallback to the old fixture geometry.
- Public TryInstallPreparedLayout accepts only idle initialized Created/Preparing with no admitted/in-flight commands. Running rejects. Candidate geometry and all derived front routes, endpoints, menu, delivery/manual anchors and pristine business state are checked before installation.
- Trusted readonly front configuration/identity remain authorization sources. Separate effective front configuration derives only geometry flow. Candidate house and real manual callbacks are staged before commit; installation's prepared binding skips repeated validation/manual configuration after swap. No external factory runs after commit.
- Exact repeated layout is idempotent: no version increment, pose change or worker claim release. A changed layout preserves item identities/versions, content, allocator, elapsed work and movement watermarks while releasing active manual worker ownership for explicit continuation.
- InstalledLayout stores frozen layout and normalized initial seed poses (movement tick -1); runtime Recipe.Poses retain actual movement ticks. Export identity continues using trusted preparation/front policies.
- Same-Level restore reprojects saved layout against current trusted policy, configured appliances and normalized seeds; derives front configuration rather than accepting payload flow. Initialized Created/Preparing kitchen installs geometry using recipe restoreReferences before Recipe.Restore, allowing new valid anchors and runtime poses even when fresh factory seed objects used removed old anchors. Preparing remains Preparing; Running takes its existing lifecycle start path. Current preparation factories require installed-layout payload; legacy factories cannot self-grant one. This is candidate-format evolution, not a published save migration claim.
- Invalid trusted initial layout faults the host, releases simulation ownership, aborts/removes the unpublished level and rejects later ticks. The disposed failed host does not prevent a fresh host from initializing.

## Actual verification

- Initial compilation caught ambiguous ExportCheckpoint target-typed constructor calls; fixed with explicit CookingFrontOfHouseMenu. New-test compilation caught wrong recalled command/manual enum parameter names; corrected to actual MoveX/MoveY and Execution APIs. Compile failure logs retained, not reported as test executions.
- Old Preparing suite: 9/9 after actual initial installation and fixture targets.
- New layout plus Preparing focused run: 17/17 (8 new layout cases and 9 Preparing cases).
- Final combined PreparedLayout, Preparing, supply ET, front/delivery ET and level checkpoint run: 46/46 passed, zero skips, no compiler warning/error. git diff --check passed with Git line-ending normalization notices only.
- New tests prove actual bounds/obstacles/derived front spatial identity, duplicate idempotence, Running rejection, missing live anchor and missing front table zero-checkpoint-change rejection, Preparing and Running exact canonical recovery with payload new anchor replacing fresh old item location, runtime pose watermark preservation and legal movement after restore, manual elapsed/locks/version/allocator preservation and release/reclaim, self-granted equipment/radius/seed/flow rejection, and invalid trusted initial route/anchor fault cleanup.
- Existing preparation supply/partial process uninterrupted-versus-restored final canonical control remains passing under the installed layout.

Evidence: local/Logs/cooking-execution/prepared-layout-et-increment/ includes initial/new compilation failures, focused and final logs/TRX. No full gate claimed for this changed source; root owns independent review/full gates.

## Remaining boundary

Cross-Level layout transfer requires a separate transaction: current CreateSuccessor accepts handoff before installing the next level's trusted layout. Reading EffectiveSpatial alone cannot guarantee next-level seed when the old pose remains valid. Root explicitly deferred this transaction; this increment leaves successor behavior intact and does not claim full S08, retry layout reset or next-level birth-seed completion. Unity remains deferred.

Source and integration .NET window released after final focused run. Other operators' source edits preserved.

## Final equipment-movement and continuation evidence

Independent root review requested direct equipment interaction evidence and final-state recovery comparison. This follow-up changes only the new ET test file; production host/core remain frozen.

- New occupied station control starts manual work at elapsed one, then moves stove footprint from (4,2) to (6,2), keeping its StationID. Item identity/location/version, process identity/anchor/locks and elapsed progress remain intact; worker is released. From the old real player pose, both ContinueProcess and Pickup return TargetOutOfRange. Actual ET Move operations reach the new interaction side; ContinueProcess succeeds and work completes with the product at the retained station. No assertions rely solely on anchor coordinate values.
- Both Preparing and Running changed-layout recovery cases now execute the same Move followed by another Tick on the uninterrupted original host and recovered host, comparing complete final checkpoint canonical text after those actions, in addition to immediate saved/restored equality.
- First compilation exposed an incorrectly recalled placement property (Origin); corrected to the actual Cell property. The failure log is preserved.
- Final new-layout suite: 9/9 passed, zero skips, no compiler warning/error. Scoped diff check passed. Logs/TRX cooking-prepared-layout-station-first/final are in the evidence directory above. Earlier 46-case combined result predates this test-only addition; production code has not changed since that result.

Source and .NET window released to root again for final independent gate. No commit by reviewer.
