# S14 manufacturing availability and menu Ready increment

Base: master e3a84d765. Isolated branch/worktree cooking-menu-ready-s14. Scope: three new source/test files only, plus this report. Existing Recipe/Host/factory/checkpoint/content generation remain unchanged. Prior supply-worktree uncommitted Observation sources were preserved; no merge performed.

## Read/query boundary

CookingRecipeSimulation.DescribeManufacturingAvailability() returns detached actual ItemDefinitions, Recipes, Appliances, Players, OrderTemplates, CurrentSpatial, SupplyConfiguration, Current snapshot, RegisteredSeeds, configured CleanContainerSupply, current clean pool counts/location and Scope. Every nested set/list/dictionary is defensively frozen. The simulation does not own a configuration identity; no invented identity is returned.

RegisteredSeeds are real registry entries with AllocationSequence zero and IsProduct false, including retained tombstones. Container ancestry resolves through the real registry to its recorded source location. This is a historical registration witness, not a configuration baseline, material grant, infinite source or automatic revival. Only closed Supply nodes and container acquisition use source witnesses. Generated outputs remain recipe dependencies. Current physical snapshots/counts remain distinct from external supplier balances and histories.

## Trusted policy and helper API

CookingLevelMenuConfiguration(CatalogIdentity, SelectedMenuIds, BaseAuthorizedMaterialDefinitions, ConfirmedMaterialUnlocks, AllowedMaterialDefinitions, BindingCommandsEnabled) provides Freeze(), CanonicalText(), Identity(). Canonical text sorts stable scalar ID arrays, includes confirmed unlocks, and never serializes DefinitionId-key dictionaries. Root must construct policy from trusted factory and confirmed progress, never ordinary commands or checkpoint self-grants. Effective material permission is (Base union ConfirmedUnlocks) intersect Allowed; equipment permission remains S08-owned. There is no second AllowedRecipeIds universe.

CookingMenuReadyValidation.Validate(catalog, trustedPolicy, actual) returns CookingMenuReadyResult with frozen per-menu/node/code/relation/blocking diagnostics and IsReady. Unknown menus are diagnostics. Each selected dependency closure checks runtime input multiset/defaults, output, process, appliance capability, carrier, execution, completion, yield, ticks and station binding. Actual item/container permission, input capacity/acceptance, storage/serving sources, final order template and binding policy are checked. One available player must cover all inputs and the carrier for each work node; no union of different players substitutes for that. Different nodes may use different workers and reuse stations/vessels.

Only actual available appliances provide production capabilities. Binding is trusted command support matching S05; no new physical ticket-station or binding-distance requirement. Sources are real current/historical registration, configured clean pool or verified supplier with permitted unit/package definitions and actual reachable source/receiving anchors. Finite exhausted balance and zero current stock are nonblocking SupplyExhausted/NoCurrentStock diagnostics, not new business failure rules.

## Static reachability boundary

Legacy uses player ReachableStations. Spatial uses current legal player pose, actual typed SpatialAnchor interaction coordinates, inset world bounds, and closed obstacles inflated by actor radius. Candidate coordinates comprise start/anchor, inset bounds, anchor plus/minus interaction radius and inflated obstacle outer edges plus/minus one integer subunit. Nodes must pass existing ValidPose; visibility edges reuse existing exact SegmentHits against inflated obstacles. A goal must be inside the interaction circle and have raw-obstacle line of sight. This is a bounded static configuration-space witness, ignores temporary actors/work occupancy and does not require current proximity or facing. It does not execute Move commands or become a second navigation/clock owner.

Candidate cross-product is capped at 4096; exhaustion is Unreachable, and complexity overflow is explicit blocking ReachabilityLimit when no alternative source/appliance proves reachability. Results cache by player/location for a validation call. A reachable alternative prevents a rejected alternative's limit from falsely blocking the menu.

## Actual evidence

Independent command: dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj --filter FullyQualifiedName~CookingMenuReadyValidationTests --logger trx --results-directory TEMP/menu-ready-results.

Final: 117 passed / 0 failed / 0 skipped. Logs: TEMP/menu-ready-production-final.log; TRX: TEMP/menu-ready-results/menu-ready-production-final.trx. Includes 87 individual closures plus full combined catalog, exact recipe mismatch matrix, permissions/confirmed unlock identity, incapable or unreachable workers, container source/compatibility, binding without ticket station, actual spatial kitchen far/behind-facing versus separating wall, radius-sensitive corridor, inaccessible anchor center with reachable interaction face, complexity rejection, external mutable collection freezing, actual finite supplier zero-stock warning, and real DiscardItem retaining historical witness without reviving stock. Readiness tests compare unchanged owner checkpoint.

Historical actual runs retained: first 110/110; second 114/114; pre-extra-test 116/116. Additional real-source tests initially failed compilation because test used Execute/Committed instead of Submit/Accepted (TEMP/menu-ready-source-final.log; no TRX), then ran 116 pass/1 fail because the real supplier fixture lacked mandatory spatial anchors (TEMP/menu-ready-source-fixed.log/TRX). Corrected the fixture with actual source/receiving/station/seed/clean-pool anchors; production rules were not relaxed. Final 117/117 is the current evidence.

No complete integration gate, host Ready/factory trust/checkpoint rebind, operating flow, Unity or network exit was claimed. Root owns subsequent serial Host/format wiring and independent review/gates. These helper tests validate the increment, not full S14.

## Independent review corrections (2026-10-02)

Root review found three execution-model mapping defects; no existing execution rule was changed.

1. Catalog projection has no implicit defaults. A null or empty runtime DefaultInputs collection is allowed; any nonempty collection is RecipeMismatch, including one equal to the declared multiset. Previous validation had wrongly rejected empty and accepted equal nonempty defaults.
2. Supply eligibility now follows actual ExecuteSupply: infinite Take uses only unit authorization/qualification and source reach, never receiving reach or package acquisition. Infinite supplier metadata cannot witness an unmaterialized package source. Finite Request requires unit/source qualification; Receive separately requires unit+package/receiving qualification, and these may be different available players. Finite package compatibility/authorization checks remain. Actual production Request/Receive and Take acceptance are directly compared with readiness in the new regression fixtures.
3. Final delivery requires at least one available player qualified for both final product and required serving container. NoEligibleDeliveryPlayer diagnoses unsupported final output or split qualifications; the delivery player may differ from manufacturing workers. This follows existing SubmitOrder and adds no ServePortion eligibility rule.

Real red run: 7 failed / 118 passed / 0 skipped (125 total), TEMP/menu-ready-review-red-seven.log and TEMP/menu-ready-results/menu-ready-review-red-seven.trx. An earlier six-failure red was retained too; the package-source fixture was then made unobstructed to isolate unmaterialized Infinite package availability rather than also failing an unrelated receiving path.

After the three focused fixes: 125 passed / 0 failed / 0 skipped. Final log TEMP/menu-ready-review-fixed.log and TRX TEMP/menu-ready-results/menu-ready-review-fixed.trx. The original 117 evidence remains historical. Three helper source files frozen; only validator/tests/report changed in this correction. Host/format and complete S14 exit still pending root integration/review.
