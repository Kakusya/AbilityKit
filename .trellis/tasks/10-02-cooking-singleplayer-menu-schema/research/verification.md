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

## Carrier/provenance hooks and actual ET recovery increment

Imported coordinator501bf383d as1e4e4e67f. Mapper now projects actual RequiredProcessingContainerDefinition and ContentProvenance into formal content validation/configuration identity; normalized SHA hexadecimal case is canonical.128 catalog tests include three actual cake intermediates rejected in a counterfeit vessel with equal material acceptance/capacity, then transferred back to correct mold and completed.

Approved test-only linked source fixture uses delegates into Host.TryEnqueue/Tick for production, with no direct domain submission in ET paths. All87 candidates pass two deterministic arms: uninterrupted versus first completed intermediate/batch/final export, codec roundtrip, host dispose, fresh empty simulation factory, actual host restore and continue. Immediate restored and final full checkpoint CanonicalText match.56 meals/desserts actually submit;31 drinks remain plated pending S05. Every declared single-yield OutputStorageContainer is now physically filled by TakeOut/PutIn, not only represented in configuration; batch storage uses actual ServePortion.

Actual cooking-kitchen-loop gate passed281 focused/402 Cooking/160 ET; cooking-et-level-runtime passed160 ET. Broader runtime-contracts passed48.0s, with existing Moba test nullability and transitive warnings recorded in log. Logs hooks-*.log and et-menu-tests.log are actual output. Previous spatial-production records remain explicitly pre-hook historical evidence. This does not prove manual pause/change-player continuation, S05 binding/disposal, S07 procurement or S14 full level lifecycle exit; those remain separate acceptance work.

## Actual menu manual handoff and partial-batch ET checkpoints

F01 and D31 exercise a real selected manual recipe with explicit test-only RequiredTicks4 and second-player interaction reach1200. Working vessel is publicly dropped on its correct processing station before Start; StopProcess freezes progress, checkpoint is captured while unclaimed, second player navigates within reach and ContinueProcess resumes the same process, then completion and remaining menu steps settle/plate normally. Interrupted and uninterrupted full final checkpoints match. D31 separately captures a batch after the first real ServePortion leaves RemainingPortions1, restores, serves the remaining unit and completes the drink.3 focused tests pass; cooking-kitchen-loop gate281focused/402Cooking/163ET passed28.5s.

Initial fixture attempts were rejected for overlapping player poses, collision with a parked player on a production path, and trying to claim an anchor held by another player. Corrections use a distinct off-path parking position, physically drop the vessel at its station, approach within reach, and park the partner again via public movement. No authority-state mutation or skipped rejected operation is used. Tests retain explicit fixture values and do not claim final balance or all multiplayer schedules. S05 import still awaits coordinator validation.

## Complete menu production, delivery and bound-cup recovery acceptance

Coordinator validated original S05 c6bc46c5b/580033908 and nested-vessel ownership901f465b5; imported as789a70879/6fcbf33b7/bfe9fb2ee. Only menu mapper/fixture/tests changed afterward; no worker edits to core schema/checkpoint/configuration authority. Formal content now maps RequiresBinding for all31 drinks and DisposableOnSubmission for their cup definitions, leaving56 meals/desserts ordinary washable delivery.

160 catalog tests pass, including all87 complete finite-supply production, correct plating, exact order submission and score100; all31 drinks also reject naked/wrong-ticket submission without mutation, publicly rebind/unbind/rebind and settle once. Actual initial failure was a test assertion assuming a washable vessel remained in live snapshot; core rightly tombstones it pending wash, so assertions now inspect real exported Removed/IsDirty and distinguish discarded cups from dirty washable vessels. No core workaround or state reset.

121 ET tests pass: all87 actual host ingress production and submission compare full final checkpoints with uninterrupted execution,31 additionally checkpoint a real bound cup then restore and consume it on matching delivery,2 paused-manual handoff and1 partially served batch. Evidence is written only after canonical equality is asserted. Actual final cooking-kitchen-loop gate330 focused/452 Cooking/197 ET passed33.7s,0failed/skip. Python generator5/5 and deterministic --check passed,72/60/19/87/367 and31 source discrepancies unchanged. Logs binding-*.log and complete-kitchen-gate.log record actual commands/results.

Complete current evidence: complete-domain-production/87 submitted records; complete-et-production/121 restored/compared records, including31 bound cups; complete-evidence-audit.json independently checks counts, identity/provenance, unique physical supply anchors, final retired-vessel semantics, and all settlements. Test process resolves relative evidence directories under its output directory; records were copied without modification into canonical task research paths after successful tests. Future invocations should use absolute environment evidence paths. Historical spatial-production remains pre-hook evidence, not final acceptance.

This closes worker menu-content acceptance S04/S09-S13, ready for coordinator independent review/integration. Fixtures create finite declared raw units/empty vessels and directly open the matching order template; procurement/package budgets(S07), front/supply/layout full level lifecycle(S14), balance values and Unity remain separate pending work. No first-level menu selection or capability license is inferred from the87 candidate catalog. No master merge/push performed by worker.
