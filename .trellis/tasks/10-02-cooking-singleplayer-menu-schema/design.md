# S04 design and contract

Reviewed against D:/MyWork/AbilityKit S04 design and parent research/menu-review.md/final-review.md on2026-10-02. This document records implementation design, not completed runtime acceptance.

## Data and identity

Read original Markdown and XLSX with Python zip/XML only. tools/cooking-menu-source-ids.json maps source IDs to expected names and stable slugs; unknown/missing/duplicate source IDs, name changes and ambiguous names reject. Row order never selects runtime identity. Source hashes preserve original attachment identity.

CookingMenuDocument contains supply/preparation/stage/finished materials (DefinitionId), stations (StationSlotId/capabilities), carriers/serving vessels, steps (RecipeId/ProcessId), menus (OrderTemplateId) and all367 CookingMenuSourceNode records. Each node retains10 original cells and row/locator, classification and exact recipe projection or delivery operation. Source IDs differ from runtime IDs; ketchup versus fresh tomato sauce, instant versus fresh tea, and tea-free D28 remain distinct.

Inputs use positive integer counts; ExpandInputs produces repeated definitions for the core multiset interface. Each non-supply output has one producer; graph traversal proves acyclicity and selected-menu dependency closure. Expanded stages consume previous stage objects; no global serial order between unrelated preparation branches. Source audit independently flattens expanded stages and verifies original direct input multiset, including D13 milk x2 and S11 biscuit x2.

Thermal stages retain actual vessel identity: S04-S06 load/mix -> bake -> unmold all reference cake-mold; bake/steam/fry/grill branches load their thermal tray/basket. Unmolding is an explicit manual recipe consuming baked stage inside the same mold, producing cake that is then taken out to the serving plate. Vessel ID must be passed to core RequiredProcessingContainerDefinition and checked by StartProcess/preview; metadata alone is not enforcement.

## API and production integration

Load(json/document) validates, owns a defensive copy, normalizes unordered tables, publishes Canonical/Sha256. SourceCells preserve semantic cell order; input counts do not normalize away. Requirements(sourceIds) returns supplies, recipes, containers and capabilities, with separate ProductionCapabilities/DeliveryCapabilities/RefillContainers. ValidateLevel checks full availability, without claiming physical reachability or replenishment proven.

ToContentDocument(baseline, selected, recipeFactory) projects only dependency closure and appends to existing content. LoadContent invokes CookingContentCatalog.Load, so existing configuration validation remains authoritative. Adapter must preserve identity, full input multiset, process, capability, completion, ticks and station requirement. Current legacy adapter remains explicitly blocked for Manual/batch/multiset pending core publication; it is not accepted production completion. Once core is available replace it with direct typed mapping and verify Execution/YieldPortions/required vessel plus order RequiresBinding and disposable policy.

Catalog canonical/hash must be consumed by formal compatibility identity, not only held in this class; requested core hook is documented in research/core-integration-request.md. Existing soup/toast IDs remain baseline additions, never overwritten. Delivery capability is not a fake recipe step. Disposable cups are world supply, never cleanPool; washable serving plates/bowls enter existing pool. Core must destroy disposable vessels on Submit.

## Errors and examples

| Diagnostic | Positive case | Rejection |
|---|---|---|
| UnknownSchema/InvalidValueStatus | catalog v1 fixture defaults | unknown version/balance claim |
| DuplicateId/InvalidMaterial/InvalidSource | stable distinct IDs, attachment hashes | duplicate producer/ID, malformed hash |
| MissingReference/MissingProducer/Cycle | all upstream source closure | unknown material, producer removed, self cycle |
| InvalidPortions/InvalidMode | integer counts >=1, typed mode | zero/negative counts/yield/ticks, unknown mode |
| MissingCapability/MissingContainer/ContainerCapacity | complete permitted closure | missing workstation/vessel or too-small carrier |
| MustLastViolation/InvalidFinal | D31 tea -> ice -> cap | cap in earlier stage, no explicit prior stage |
| InvalidProvenance/MissingProvenance | source node maps to actual recipes/delivery | nonexistent recipe or omitted FINAL/SERVE |
| MissingSupply | full source/cup refill availability | required ingredient absent |
| InvalidRuntimeAdapter/UnsupportedRuntimeContract | exact core mapping | drops repeated input/Manual/yield/vessel |

87 candidates are not a level assignment. F21 pasta and sauce branches can be prepared in either order; D28 has milk/brown-sugar pearls and no tea. Physical supply, reachability and capacity remain joint S07/S08/S14 acceptance.

## Version and recovery

Catalog v1 has no migration from unknown catalog versions. Core owner alone chooses cooking-definition/checkpoint upgrade and old-format rejection. No menu-specific checkpoint schema: recipes/objects/processes/orders use the same runtime authority. Catalog provenance and fixture status participate in normalized catalog hash; actual production compatibility must carry it through core integration. Recovery tests compare uninterrupted and rebuilt ET host canonical terminal states per87 menu after real production, including unfinished stages and remaining batch portions.

## Decisions and limitations

Actual source audit found31 computedStationClosure differences, exactly D01-D31 missing binding station from source directory. Preserve originals and source cells; distinguish production and delivery closure. Treat mold load/bake/unmold as operations with container enforcement; treat compound rows as linked stage recipes. New numeric values: ticks1, selected shared-prep yield2, raw initial8, serving vessels2, score100; all fixtures. Tests cannot convert these to balance or supply-operation claims.

## Preparation output storage and working-vessel availability

Further source reading preserved P19 cook-pot/rice-tub, P20 cook-pot/prep-bowl, P35 cook-pot/sauce-bowl and P53 pearl-jar. CookingMenuStep.OutputStorageContainer on the final preparation recipe selects the source carrier column storage end (last alternative), adds it to selected dependency closure, validates its output acceptance and fixture yield capacity, and projects a real empty item-container. Processing Carrier remains independent. The runner extracts real batch portions into storage with public commands before reusing the working vessel; it never creates stored preparation objects directly.

Source lists a single vessel for some shared batches (mix-bowl/tea-tub); that can remain the live batch anchor. Spatial fixture must allocate sufficient distinct working instances for concurrent retained batches or completely extract a batch to legal storage before reuse. One definition in closure does not establish instance availability. F21 pasta/sauce must not be declared producible merely because both can use cook-pot capability; real runner will demonstrate output storage and empty-vessel availability. Catalog v1 remains prerelease; source increment is not runtime compatibility acceptance.
