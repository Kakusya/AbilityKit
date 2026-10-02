# Independent menu production increment review

Reviewed 2026-10-02, immutable commit `973cb3319075c72066684a73cb6a2675e398dd88` in `cooking-menu-s04`. Source inspection used `git show`/`git ls-tree`, not the concurrently edited working files. No menu-branch modifications or .NET commands were run. Raw existing logs/TRX were read only. This commit predates the later processing-carrier/provenance/binding hooks.

## Conclusion

Acceptable as a limited **87 spatial domain production and plating** test increment, with **56 actual meal/dessert settlements and 31 drinks explicitly awaiting binding/delivery**. No new blocking defect was found within that advertised increment. It does not complete S04/S09–S13/S14, all storage semantics, player handoff, ET reconstruction, S07 procurement or S05 drink delivery. Later carrier/source hooks require their own rerun; these earlier passing results do not prove later rules.

## Actual authority paths

- `Fixtures/CookingMenuProductionFixture.cs` constructs validated content with eight finite raw units per required supply definition, two serving vessels and empty working/storage containers. Every raw unit and empty non-pool vessel has a unique WorldPosition anchor. Setup checks each manually seeded definition is a Supply material or declared container; it does not seed preparation/stage/final products. Ordinary cells are not used as a shared infinite inventory.
- Setup calls public AddItem only for those raw/empty instances, with entries present in validated StandardInitialSupply. It intentionally bypasses the legacy standard spawner's per-entry ordinal reset; this is approved test seeding, not evidence that S07 packages/procurement or that spawner's repeated-definition handling works.
- Navigation repeatedly sends public Move commands and advances the authority's fixed Tick; there is no teleport, pose restore or free reset. Pickup/TakeOut/PutIn/Drop/StartProcess and ServePortion likewise use simulation.Submit, assert Accepted and advance the same fixed Tick.
- Produce recursively acquires actual supply/preparation instances, transfers each immediately, asserts the full material multiset in the working vessel, starts the actual recipe, and waits for the real process to disappear. Manual recipes remain actively claimed by their starter; the driver does not call ContinueProcess or exercise pause/other-player continuation. Those are separate core tests, not part of this menu proof.
- Multi-yield results are ServePortion-extracted one by one into real output storage; each remaining-portion decrement is asserted, then empty source contents and reset completion are asserted. The working vessel is reused through actual transfer operations. The test-only reserved set tracks intentions; changing it does not alter authority items or release device occupancy.
- Same SourceId/carrier stages share the same working instance. Earlier stage outputs remain physical instances and are consumed by later declared recipes; no intermediate injection, checkpoint editing, hidden disposal or simulation reinitialization was found.
- ProduceAndPlate physically moves a real serving vessel and transfers the actual product, asserting product definition, final Recipe identity and the serving container owner. SubmitMeal opens a genuine order, submits via public command, and checks exact recipe/template/container and score 100.

## Negative and branch evidence

- Four premature-topping cases (D31/D18/D20/D02) physically add the topping and earlier-stage ingredients. Extra capacity is explicitly a negative fixture value to isolate recipe-stage rejection. Start rejects RecipeNotMatched and snapshot canonical is unchanged; actual TakeOut/PutIn returns the topping to a real storage/working vessel before the same simulation completes the dish.
- Four missing-second-unit cases (D12/D13/D31/S11) physically load one fewer required instance. Start rejects with unchanged snapshot. Existing material instances remain in the vessel; the driver releases only its test reservation and reuses these instances while acquiring the missing unit. No defaults or fabricated completed states are introduced.
- F21 executes pasta/sauce branches in opposite real preparation orders and checks order difference, per-definition remaining inventory and score equality. This proves dependency flexibility, not simultaneous multi-player execution.

## Remaining material evidence limits

There are **39 single-yield preparation steps with OutputStorageContainer** in the committed catalog. The driver creates their storage vessels but only transfers completed outputs to those dedicated storage vessels when YieldPortions > 1. A single-yield output remains in its working vessel until TakeOut directly into a downstream route. Thus these tests prove production/transfer, but do not prove all 60 declared storage choices are physically exercised. Add explicit single-yield storage acceptance if that is part of the final exit.

The flat fixture has no equipment footprint obstacles or customer paths and one logical player. It cannot prove S08 real installed restaurant clearance/layout, natural multiplayer division, ContinueProcess takeover, Level pause/reconnect, or destination reconstruction. Its complete-domain regression gate includes preexisting ET tests, but its 87 theory cases call CookingRecipeSimulation directly and do not pass through CookingLevelEtHost.TryEnqueue.

## Independent evidence inspection

Parsed all **87 committed spatial-production JSON records** using git-show bytes: **56 submitted**, **31 produced/plated binding-pending**, **56 settlement entries total**. For every submitted trace, settlement product/container IDs match recorded product/vessel IDs; pending traces contain no settlement. Initial world anchors are unique within each record; executed recipe lists, positive logical tick and 64-character snapshot hash are present. The evidence files contain initial supply, executed recipe identities and ending authority state, not a full serialized Move/action replay log.

Read committed `spatial-complete-tests.log`: catalog suite **124 passed / 0 failed / 0 skipped**. Independently parsed raw TRX referenced by `local/Logs/test-gates/20261002-191248-cooking-kitchen-loop/cooking-kitchen-loop/gate-summary.json`:

| Actual scope | Passed / failed / not executed |
|---|---|
| Kitchen focused contracts | 257 / 0 / 0 |
| Cooking domain | 378 / 0 / 0 |
| ET runtime | 73 / 0 / 0 |

The raw Cooking TRX includes **124 CookingMenuCatalogTests**, specifically **87 all-menu spatial production cases**, **4 premature-topping cases**, **4 missing-count cases**, all Passed. Log paths exist. This is inspection of existing execution evidence, not a fresh reviewer-run gate or proof of the concurrently edited worker HEAD.

## Explicit next boundaries

RequiredProcessingContainerDefinition and ContentProvenance are not enforced by this old commit's mapper/driver. Carrier and catalog/source SHA must subsequently enter actual formal configuration identity and be enforced and rerun. All 31 drink binding/disposal/delivery and all 87 ET ingress/checkpoint continuation remain pending in these traces. No Unity/network or full-business completion claim is justified.
