# S04 source-catalog increment verification

2026-10-02, branch cooking-menu-s04, Orca task_0b550b23177b / dispatch ctx_cf4084e88097. Increment is independently verified source/catalog work, not full S04 or87 runtime completion.

| Actual command | Result |
|---|---|
| python tools/generate-cooking-menu.py --check | Pass:87 directory rows,367 nodes,60 preparations,72 supplies,19 stations,7 references;31 retained drink delivery closure differences |
| python tools/test_cooking_menu_generator.py | Pass5/5: row reorder semantics, source-ID/name rejection,367 exact node dispositions, preserved differences, mold graph |
| dotnet test src/AbilityKit.Game.Cooking.Tests --filter FullyQualifiedName~CookingMenuCatalogTests --verbosity quiet | Pass23/23 after provenance/cup closure changes |
| powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop | Pass focused128, Cooking249, ET67; builds zero warnings/errors; exit0 |
| git diff --check | Pass (line-ending normalization notices only) |

Gate log pointer: local/Logs/test-gates/20261002-182720-cooking-kitchen-loop/cooking-kitchen-loop/gate-summary.json. Raw focused-test log is overwritten with this dispatch actual revalidation before commit; preserved previous-session logs are not evidence for this delivery. A final gate rerun is recorded separately if additional changes follow.

Original attachment SHA values remain the README values; generator reads originals and never writes them. The catalog itself validates provenance in addition to source-audit.json. Production and delivery capabilities are separate; new cup supply is not marked washable.

## Outstanding acceptance and coordinator request

Core recipe Execution/YieldPortions/multiset/required vessel commit pending; S05 RequiresBinding and disposable Submit pending; catalog/source SHA must enter actual config/checkpoint identity. Exact requested additions are in core-integration-request.md. Legacy adapter still explicitly blocks unsupported routes:23 catalog tests do not prove87 production. S09-S13 public-command simulation + ET production/submission/recovery, physical supply/refill/reachability and final-stage runtime negative tests remain unexecuted. Unity/network are outside this worker scope.

Final rerun after provenance validation: same128/249/67 counts, exit0,18.2s. Durable build/test logs and gate summary copied to research/gate-source-increment/.

## Limited automatic production-route revalidation

A further F31 compatibility test uses formal selected content with one raw unit and one of each vessel, then public Pickup/Drop/PutIn/StartProcess, one fixed Tick, TakeOut/PutIn/Submit. Exact recipe/template/vessel settlement and score100, raw/product consumption and empty reusable basket are asserted. No intermediate/product injection. This remains explicitly nonspatial/legacy compatibility proof, not87/Manual/batch/binding/ET recovery acceptance.

Actual focused new test passed1/1; cooking-kitchen-loop rerun passed focused129, Cooking250, ET67 with zero build warnings/errors, exit0,20.5s. Durable raw logs: automatic-route-test.log and automatic-route-gate.log; local gate summary at local/Logs/test-gates/20261002-183750-cooking-kitchen-loop/cooking-kitchen-loop/gate-summary.json. Initial test compile failed due to incorrect TickResult.Accepted and snapshot Removed assumptions; fixed to authoritative tick fields/active snapshot membership before these actual successful runs. The first failed rebuild emitted preexisting transitive XML-documentation warnings; they were not suppressed or counted as a successful gate.

## Source output-storage refinement

Typed OutputStorageContainer preserves60 preparation storage choices and joins real container closure. Source examples P19 rice-tub, P20 prep-bowl, P35 sauce-bowl, P53 pearl-jar have positive/negative storage tests; transfer/empty-vessel availability remains planned real-runtime acceptance. Actual generator check passes; generator5/5 passes; catalog tests28/28 (final command adds --no-build after successful complete gate build). cooking-kitchen-loop passes133 focused/254 Cooking/67 ET, builds0 warnings/errors, exit0,17.9s. Durable logs storage-tests.log and storage-gate.log; summary local/Logs/test-gates/20261002-184302-cooking-kitchen-loop/cooking-kitchen-loop/gate-summary.json. The earlier focused rebuild emitted preexisting transitive CS1591 warnings and passed; final --no-build rerun only verifies tests, not a fresh clean build. No full87/spatial/runtime integration claim.

## Published core typed projection

Imported core8111472f2 as98316258e (only csproj copy-content conflict resolved, preserving corev3 and menu content). MapRuntimeRecipe now uses actual published Execution/YieldPortions/multiset; adapter checks those semantics and full input counts. All87 can pass the actual formal content registry with28 catalog tests passing, including field-loss rejection; this is loading/projection evidence only, not production/ET recovery or binding/vessel/source identity enforcement. Complete kitchen gate rerun exit0,20.6s, ET73; exact focused/domain counts are in core-projection-gate.log. Build summaries0 warnings/errors. Final focused command used --no-build after successful gate build; earlier transitive rebuild had existing XML documentation warnings, unsuppressed.

Exact pending hooks remain RequiredProcessingContainerDefinition, ContentProvenance and S05 RequiresBinding/DisposableOnSubmission (root owner). New core definitionv3/checkpoint format is imported from core owner, not chosen by menu worker. Spatial stock/unique-anchor fixture must still be reviewed for complete87 proof.

## Real spatial domain driver increment (before carrier/source/S05 hooks)

Approved unique-slot fixture: every raw unit and empty working/storage vessel has a distinct validated initial world anchor; raw8 per definition, serving2 remain finite fixture values. Driver uses only public movement/pickup/drop/put-in/start/fixed Tick/take-out/ServePortion for all production. Same physical mold/tray stages share their working vessel; each batch is actually extracted into storage before working-vessel reuse. Setup seeds only raw units and empty vessels represented by validated StandardInitialSupply entries; no preparation/stage/final injection, free resets, discarded leftovers or alternate recipe engine.

Actual87/87 production and correct plating pass:56 meals/desserts submit with exact template/recipe/vessel/score100;31 drinks remain produced/plated and explicitly binding-pending. Actual4 premature topping negatives(D31/D18/D20/D02) reject Start with unchanged snapshot then recover via true take-out/put-in; negative fixture working capacity has explicit+1 slack to isolate recipe order guard. F21 pasta/sauce work in both real branch orders with same per-definition remaining inventory/score. D12/D13/D31/S11 missing second physical unit reject before process, retain inputs and recover without defaults. Full catalog suite124/124; kitchen gate257focused/378Cooking/73ET, builds0warnings/errors, exit0,20.6s. Logs spatial-*.log; actual per-menu initial supply, executed recipes, product/vessel IDs, leftovers, settlements and canonical hashes under spatial-production/ (87 records;56submitted/31pending). Gate summary local/Logs/test-gates/20261002-191248-cooking-kitchen-loop/cooking-kitchen-loop/gate-summary.json.

Initial all-menu run81/87 exposed selected heat-only station missing its other declared foam mode from SupportedCapabilities, fixed in mapper while retaining one physical multi-mode station. Driver originally gathered repeated single-yield products before freeing producer vessel, fixed to transfer each unit immediately and process explicit earlier stages before final additions. Initial navigation fixture used a half-speed-grid start while movement normalizes intent to full speed; corrected initial pose to the movement grid and added bounded convergence. These failures were fixed before successful runs, not passed evidence.

This increment predates requested carrier/source hash and S05 hooks. It is domain spatial production proof, not completed S04/S09-S13/ET recovery/S07procurement/S14exit. Next: import validated501bf383d and map/check real RequiredProcessingContainerDefinition/ContentProvenance; S05 validated binding/disposal still awaited. Approved ET test-only source link and new per-menu host test ownership remain next.
