# First Preparing ET increment review

Scope: integration worktree host `CookingLevelEtHost.cs` and new `CookingPreparingEtTests.cs` only. No commit by reviewer. Lifecycle, configuration and checkpoint DTO changes belong to root and are retained.

## Implemented contract

- Optional preparation factory initializes and publishes the same kitchen before Preparing; legacy factory remains lazy until Start. Input validation precedes factory creation; publication uses existing ownership and authority binding. Reentry is rejected.
- BeginPreparation/CompletePreparation expose the phase boundary; Prepare remains the convenience operation. Preparing uses the existing ET fixed tick and driver for movement, object operations, processing and supply. Binding, submission, front-of-house work and legacy advance are rejected at admission.
- Front-of-house does not advance during Preparing. Start reuses the kitchen, establishes ServiceStartLogicalTick once, and subsequent service ticks are measured from that offset.
- Export records the frozen trusted preparation configuration identity and service offset. Restore compares trusted identity and offset/front-clock relationships. InstalledLayout is explicitly null: frozen policy does not establish installation.

## Findings fixed

1. Actual positive restore exposed a second front-clock guard comparing recipe logical time directly. Both restore guards now subtract ServiceStartLogicalTick.
2. Existing cross-generation checkpoint test exposed changed rejection precedence for a missing recipe payload. Payload/scope validation now precedes the new offset validation, preserving the established reason.
3. Nullable flow warning after the explicit non-null payload guard was corrected mechanically. New tests were rerun afterward.
4. Initial ownership fixture attempted simultaneous ET runtimes, which the actual runtime prohibits. The fixture now checks that runtime ownership rejection leaves the active preparation host usable; it does not claim a two-host shared-kitchen control.

## Actual verification

- First Preparing run: 3/5 passed; two failures retained and addressed as above.
- Combined Preparing, supply ET, delivery/front-of-house ET and level checkpoint regression: first 33/34 passed; corrected rejection precedence, then 34/34 passed, no skips.
- After the final nullable-only source correction: Preparing 5/5 passed, no skips, no compiler warning/error in incremental build output.
- Scoped git diff --check passed. This is focused build/test evidence, not a full repository gate or Unity verification.
- Logs and TRX are copied to `local/Logs/cooking-execution/preparing-et-increment/`, including first/second failure evidence, final combined run and final clean focused run.

## Remaining boundaries

- Initial or dynamic layout installation, installed-layout checkpoint restore and derived front configuration are not implemented by this increment.
- Root identified that CanInstallPreparedGeometry's old IsGameplayMutationOpen proxy now also covers Preparing and would reject it. The next serialized installation increment must use explicit Created/Preparing layout admission and widen only the internal preparation authority window; Running layout admission remains prohibited.
- Preparing checkpoint export/recovery is outside this increment's Running checkpoint contract. Full S08 completion and full combined gates remain root responsibilities.

Integration source was frozen and its .NET window released to root after focused verification.
