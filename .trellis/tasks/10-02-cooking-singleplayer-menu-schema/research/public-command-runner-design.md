# All87 public-command acceptance runner design

2026-10-02 coordinator directed this design while waiting for validated core/S05 hooks. This is an executable-test design, not executed evidence. Existing RecipeSimulation and CookingLevelEtHost remain the sole production authority.

## Entry and content boundary

Load original bundled baseline through CookingContentCatalog and select one menu source ID via CookingMenuCatalog.Requirements/ToContentDocument. The direct typed mapper must publish Execution/YieldPortions/RequiredProcessingContainerDefinition and ContentProvenance; order template passes RequiresBinding; cups pass DisposableOnSubmission. Full registry validation and its config identity precede fixture creation. Test setup may create initial **raw supply and empty containers only**, sourced from validated content. Never AddItem a Preparation/Stage/Finished or call internal completion methods.

Every source-ID has one parametrized test case, grouped25/19/12/14/17 across S09-S13;87 candidates are not a gameplay level menu. The fixture includes only selected closure plus unchanged legacy baseline. Recipe IDs and vessel definitions come from the graph, never dish-specific switch branches. Fixture numbers remain explicitly marked; proof concerns semantics rather than balance.

## Physical fixture and initial spawn

Ordinary world/hand/station slots hold one object. Default compatibility supply Count8 at one world anchor is not used to claim spatial production. The test layout generator allocates a unique stable ordinary anchor per raw unit and empty world vessel, plus processing station instances and transit/staging counters. Each raw unit gets an ID derived from menu/source definition/copy ordinal. No same-slot stack. CleanPool retains its specialized finite-source semantics; disposable cups are ordinary unique source slots, never the clean pool.

S07 may replace these initial individual source units with stock/package containers containing unit children at receiving/supply anchors. Both approaches must expose the same production contents through actual Pickup/TakeOut commands. Supply fixture setup is completed before Start and uses validated content/S07 placement APIs; do not mutate stock after starting to keep a route alive. Planned anchor count can be estimated from graph inputs and fixture yield, but actual remaining portions and objects are always read from authoritative snapshots.

Spatial configuration gives every ordinary anchor and station a pose, traversable cells, bounds/obstacles and legal interaction distance. Driver moves the logical player through public movement commands and uses preview only to discover legal affordances. Start/PutIn/TakeOut/ServePortion/Bind/Submit each revalidate geometry. Station instances model functional capabilities; heat/foam share one steam station and cannot overlap occupancy. Position/version expectations are captured immediately before each command. A missing path/anchor is a failing fixture, not permission to disable spatial checks.

## Driver interfaces and host adapters

A single test-only action driver takes validated CookingContent, selected catalog, LevelScope and adapter exposing Snapshot, SubmitPublicCommand, Tick, ExportCheckpoint, RebuildFromCheckpoint. The simulation adapter submits through its public command lane and fixed ticks; the ET adapter uses TryEnqueue(CookingLevelCommandEnvelope) and Host.Tick. Neither supplies alternate matching/progress/consumption logic. Driver tracks only source IDs, desired recipe and command sequence; all state, counts, versions, hands, process IDs and outcomes are queried from authority.

To avoid duplicate test assembly dependencies, shared driver may be a test fixture source linked into both test projects; the coordinator reviews any test csproj include. Its adapters remain test code. ET host tests run serially where existing runtime singleton demands it. Exact movement/manual/ServePortion fields come from the published core DTO; no Enum.Parse/reflection backdoor or uncompiled production stub.

## Generic recursive production algorithm

1. For desired DefinitionId/count, find actual available loose output objects and actual completed batch containers in snapshot. Reserve test intentions only locally; command arbitration remains authoritative. Supply leaves choose raw units from distinct validated anchors/stock children. If missing, production fails; no free DefaultInputs.
2. For each unmet non-supply output, find its unique catalog producer. Recursively acquire each counted input. Independent branches may be chosen in normal or reverse topological-ready order; no unrelated global chain.
3. Obtain the required real processing vessel and ensure it is empty/reusable. To load inputs move to each source, public Pickup or TakeOut, navigate to vessel, PutIn with current versions. Repeated DefinitionIds use separate object instances; ensure the complete multiset including x2 is physically inside the vessel.
4. Navigate/carrier-transfer to an appliance offering the exact capability. Place vessel into its vacant station slot through public Pickup/Drop as required. Submit StartProcess with explicit recipe and anchor. Validate accepted event/process identity. Manual progression requires active public work commands and fixed ticks; Automatic uses fixed ticks only. Reject/pause/continue scenarios read same process progress rather than restart.
5. Single output: TakeOut the completed product, leave empty carrier reusable, hand/stage it or PutIn directly into the next required carrier. When consecutive stages share a mold/tray/basket, preserve the real carrier and its single stage output, then Start the next linked recipe; no unwrap/re-create shortcut.
6. Batch output: request each portion with public ServePortion into an empty compatible transfer/next carrier according to published core semantics. Verify source remaining count decreases exactly1, each extracted unit is a new single-portion product object, and exhausted source resets without duplicating remaining contents. Batch source may stay in station until needed; driver reads remaining count instead of recalculating.
7. Final recipe is executed only after preceding explicit stages exist. Remove final product and put it in its required serving vessel (or retain if same legal vessel). Processing vessel must never masquerade as serving vessel. OpenOrder is authorized front-house fixture setup and does not create any product. Before Submit, drinks BindOrder at real binding station; meals/desserts submit directly.
8. SubmitOrder through the same public lane. Assert exact template/recipe/product/vessel/order settlement, fixture score100 and exactly once. Product disappears; cups are consumed; washable plates/bowls enter the real washing queue and return through existing NPC/CompleteWash fixture port. Repeating Submit is rejected/idempotent and cannot produce another settlement.

## Recovery and deterministic arms

For each87 test route run an uninterrupted arm and an interrupted arm with identical source/command identities. Interrupt after at least one real preparation or stage output (and where applicable while batch still has portions); export the **ET host** checkpoint, destroy host, create a fresh host with the same validated content/config identity, restore, then continue the exact same command plan from authority. Compare full final host/recipe canonical and hash, settlement identities, consumed IDs, remaining supply, container positions/dirty/disposal state, hands and command watermarks. Resume discovery after restore reads snapshot; test does not patch checkpoint fields. A source/catalog hash mutation must fail restoration before install.

All87 can use one checkpoint at a deterministic graph-ready boundary; representative routes additionally cover manual pause, partial batch extraction and bound finished cup. S09-S13 evidence contains every source ID and exact executed recipe trace, intermediate/final object IDs, supply consumption, remaining batch counts, settlement and restored canonical.

## Negative and regression matrix

- F21 independent pasta/sauce branches in both legal orders; same material conservation and valid final result. Distinct command/tick schedules compare semantically expected identities, not artificially demand identical event history.
- D13/D14 milk x2 and S11 biscuit x2: load only1 and explicit Start rejects zero mutation; correct2 succeeds, extra1 rejects. No default input manufactures second object.
- D31 cap before iced tea stage, D18 foam early, D20 cream early, D02/D03 soda early: actually load wrong early multiset then Start rejects; remove erroneous ingredient/restore vessel through public commands, continue valid chain.
- S04-S06 cake mold load/bake/unmold; wrong generic mixing bowl at bake Start rejects zero mutation. Correct mold survives unloading, final product goes to dessert plate.
- Unbaked pizza and unsteamed dumpling can be moved between players without becoming finished products. Fry/steam/bake/grill capabilities remain distinct.
- D28 requires no tea; supplier ketchup and freshly cooked tomato sauce remain different IDs; instant tea uses no leaf source. D25 cooked pearl production is required; nata/jelly use direct supply.
- Manual stop/leave retains progress, different player continues; same physical steam station cannot heat and foam simultaneously.
- Wrong serving vessel, naked drink submission, wrong binding/order, working-pot delivery, stale version/scope and repeated command reject without partial material consumption. Compare rejection with no-command control at the same ET tick to account for legitimate clock advancement.
- Missing ingredient/capability/vessel/stock refill closure fails before Level start; spatial source/target inaccessible fails actual command without teleport or disabled collision.
- Original tomato-egg-soup and baked toast regression from baseline unchanged, same raw IDs/order IDs/scoring/container rules; catalog additions never overwrite baseline records.

## Exit and pending interfaces

No case is marked passed until production and restore arms run. Static source/graph tests are already evidence for the source increment only. Waiting for validated core recipe/multiset/portion/spatial/vessel commit, coordinator S05 binding/disposal/ContentProvenance hook, and S07 stock or reviewed individual-anchor fixture. S14 integrates physical supply and full-level behavior; no network/Unity code belongs to this runner.

### Working-vessel fixture refinement

Catalog now preserves final-preparation OutputStorageContainer. Before starting another recipe in the same physical cook-pot, move completed batches to that real storage vessel using ServePortion until the source is empty, reading remaining authority counts after every command. P19 -> rice-tub; P20 -> prep-bowl; P35 -> sauce-bowl; P53 -> pearl-jar. Source storage choices equal to processing definition need a distinct empty supplied instance when transferring. Provide enough **unique anchored empty instances** for retained shared batches; derive fixture provision from selected producers, not a fixed one-per-definition count. Driver selects actual empty unlocked vessels and fails if none exists, rather than clearing valuable contents or spawning replacements mid-run. F21 specifically demonstrates independent pasta/sauce batches with conserved stored leftovers and correct final product.
