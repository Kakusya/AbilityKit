# Core increment independent review

Reviewed 2026-10-02: `cooking-core-s01-s03`, commit `8111472f2`. Read-only source and existing raw gate/TRX inspection; no core edits, builds, or tests were run by this reviewer. Review contracts: `core-review.md`, `final-review.md`, `completion-contract.md`. This report distinguishes the S01–S03 increment from complete singleplayer/menu/Unity delivery.

## Blocking findings

1. **Active process restore does not validate the complete counted input set.** `CookingRecipeCheckpoint.cs:495–522` validates each LockedInputs entry with membership and requires the anchor, but does not compare the locked material multiset with the recipe or all current container contents. `CookingRecipeLoop.cs:1295–1320` repeats membership checks during FixedTick; `ConsumedInputs` consumes only this locked list. An active two-input container process can have one material removed from its checkpoint LockedInputs while both real materials remain registered in the container. The remaining structural checks accept this shape; the omitted ingredient becomes unlocked after reconstruction and the process can still produce the complete result. The new duplicate-input contract makes membership insufficient. Add a tampered active-process regression and require exact effective input counts plus exact container/lock references before installation, accounting explicitly for configured defaults. This is a source-path finding, not an executed failing reproduction in this review.

2. **Ordinary product provenance is not validated on restore.** `CookingRecipeCheckpoint.cs:438–453` checks item identities/definitions/hand owners, but no later item validator checks ordinary `IsProduct`/`Recipe` against the declared recipe ProductDefinition. `CookingExtendedCheckpointValidation.cs:13–24` validates only completed retained-input containers. `SubmitOrder` uses IsProduct and Recipe to identify a dish, without checking ProductDefinition. Consequently checkpoint alteration of a normal product's Recipe can change its apparent dish identity without changing the food definition. Add unknown/mismatched product recipe tests and enforce provenance consistency for active and consumed products before restoring. A distinct provenance data model is a design decision; existing Recipe/IsProduct fields can at least be validated without inventing a new owner.

3. **RequiredProcessingContainerDefinition is absent in this commit.** `CookingRecipeDefinition` ends with Execution/YieldPortions (`CookingRecipeLoop.cs:47–58`); content, identity and restore therefore cannot carry the requested processing-container constraint. `StartProcess` derives only its internal material definitions and appliance capability, and does not constrain the anchor container definition. This is an unresolved S04/menu integration interface, not proof that the S01 movement work failed. The owning coordinator must establish and propagate the reviewed field through configuration, startup, running/completed checkpoint validation and executable menu tests before menu acceptance.

## Observed repaired behavior

- S01 uses configured speed and exact integer diagonal normalization, caps translating movement once per logical Tick, retains facing for zero-facing input, tries stable X-then-Y wall sliding, checks swept obstacles/other actors, and handles integer overflow by rejection. Ordinary World/Station anchors are single-slot except the explicit managed clean-container dispenser.
- ExecuteValidatedCommand applies shared geometry admission; Continue rechecks its process anchor and station. Stop is deliberately usable to release a claim. Preview runs commands in an isolated authority, deduplicates the action per target, compares normalized facing angle before distance/stable ID, and does not reuse the production allocator.
- S02 enforces one ActiveWorker per process/player, releases unavailable/out-of-reach workers, preserves elapsed progress, rejects legacy manual AdvanceTicks, and advances only available manual claims in the same FixedTick. Automatic devices remain portable/automatic.
- S03 stages generated items, container indexes and allocation watermarks before commit. ServePortion decrements exactly one portion; final serving consumes retained materials and resets the source. Completed multi-yield Pour rejects even when only one portion remains; single-yield Pour retains the old one-product behavior. Clear preserves containers; Discard rejects containers.
- New movement fields/WorldAnchor enter the handwritten ET fingerprint. Recipe schema 3 and Level format 4 reject old/missing required fields. Same-Level reconstruction preserves poses and movement watermark; AcceptSuccessHandoff substitutes destination InitialPoses and clears manual claims.

## Independent evidence inspection

Both copied gate summaries resolve to existing raw logs/TRX in this core worktree. Parsed raw TRX counters, not merely task metadata:

| Gate | Scope | Passed / failed / not executed |
|---|---|---|
| cooking-kitchen-loop, 20261002-184326 | Kitchen contracts | 133 / 0 / 0 |
| same | Cooking domain | 254 / 0 / 0 |
| same | ET runtime | 73 / 0 / 0 |
| cooking-et-level-runtime, 20261002-184348 | Cooking domain | 254 / 0 / 0 |
| same | ET runtime | 73 / 0 / 0 |

The raw kitchen TRX includes the 28 CoreExpansion test cases and six CoreEtExpansion cases. Reviewed tests cover geometry, pause/swap, counted-input startup, final portion contention, allocator failure, same-Level reconstruction and destination spawn. These tests do not cover the blocking active-lock/provenance alterations above. Test success does not close those findings. Coordinator integration must rerun relevant gates after fixes and any new interface changes. Existing warnings are retained; this reviewer did not run lint/type-check or Unity/network checks.

## Boundary

Core branch was clean at initial observation. No source, metadata or branch changes were made by this reviewer. S06/S07/S08 installation, full 87-dish execution, S14 closure, network codecs/topology and Unity are not proven by this increment or its TRX results.
