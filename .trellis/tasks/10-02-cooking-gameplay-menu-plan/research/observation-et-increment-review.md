# Read-only observation ET integration

Base integration b503c1a7d. Reviewer ownership: CookingLevelEtHost.cs and new CookingObservationEtTests.cs. Root imported domain observation projector/tests and ConfiguredPlayers reader. No commit by reviewer.

## Behavior

Host Observe() checks the idle owner boundary, then projects the current Binding.LevelScope, lifecycle snapshot, actual HostFrameSequence, owned kitchen snapshot, current front snapshot, actually installed layout, trusted preparation identity, effective geometry identity and configured player identities. It does not create a kitchen, issue commands, advance a tick or create a supply ledger. Initial Created without a kitchen returns null recipe/layout and empty physical inventory. Preparing, Ready, Running and Paused expose actual retained owner data. Installed layout identity is distinct from the lifecycle's logical preparation layout id.

## Actual verification

- First build: test fixture attempted a with-assignment to the readonly LevelEpoch property. Corrected by constructing a new CookingLevelScope. This full dependency build also emitted existing LiteNet transport warnings; these are retained and are not changes within this ownership scope.
- Second/third focused tests: 1/2 passed; the pipeline attempted BindOrder on the held cup. Actual production contract binds the contained product; failure ProductNotFound was preserved and fixture corrected to bind the actual meal id. Production binding semantics were unchanged.
- Final combined observation/preparation/layout/supply/binding/front/delivery/level-checkpoint filter: 52/52 passed, no skips. Incremental changed-source build emitted no warning/error. Host diff check passed.
- The two new ET controls use actual requests, pending delivery and physical ReceiveSupply. Observation before queued request commitment does not invent deliveries; supplier balance does not create physical inventory. A held package exposes its real three contents; extracting one exposes actual hand supply count. A real automatic recipe exposes elapsed-one processing, then the produced meal, held cup content and actual BoundOrder.
- StableRead compares owner recipe checkpoint, lifecycle snapshot, host frame, pending command count and disposition history before/after consecutive Observe calls, and compares projected recipe/front canonical text with actual snapshots. Checks run through Preparing, Ready, Running and Paused. Pause Tick rejects without progress. A previously captured frozen observation remains unchanged after later gameplay.
- Running checkpoint restore reconstructs the same observation canonical text, including scope/frame/layout/geometry/supply/binding. A new Created generation has distinct epoch/state and zero frame, creates no kitchen, and does not alter the old captured observation.

Evidence: local/Logs/cooking-execution/observation-et-increment/ contains initial compile failure, two fixture-failure runs and final logs/TRX. This is focused evidence, not full gates or network/UI implementation.

Root requested renaming the projector's misleading SupplyOriginalPackageUnits field to SupplyUnitsInTrackedPackages without changing its existing predicate. The new ET test has one reference to the old name; root will update it in the subsequent serialized rename window after this source/window release. Current 52/52 precedes that mechanical rename.

Source and integration .NET window released to root. Other edits preserved. No commit by reviewer.
