# Durable Host baseline publication and restart increment

## Boundary

Baseline main fa2f60e17 imported into integration via verified source import31c369c1c and merged as d83dc5cf5. Owned changes: CookingLevelEtHost.cs, new CookingMajorBaselineHostResult.cs, new CookingMajorBaselineEtTests.cs, this research. Root owns the prepared lifecycle commit and typed format3 prerequisites listed below. Natural operating producer owns its separate fixture/test. No networking/Unity or additional simulation owner.

## Contract implemented

The durable success overload requires explicit next preparation, locked confirmed progress and store. Both candidate ownership and match scope are checked before mutation. Next trusted menu/Front/geometry, handoff, callback bindings and typed payload are staged before IO. The source lifecycle commit is precomputed before rollbackable ET tree installation. Every fault injector precedes WriteBaseline. A failed write restores the original ET tree/lifecycle/Observe and leaves previous baseline bytes readable. Rename success is the durable commit point; only precomputed lifecycle and ownership/binding assignments follow. A baselineCommitted marker prevents ordinary rollback/rejection or candidate closure after rename if an exceptional runtime failure occurs. No fsync or power-loss guarantee is claimed.

Confirmed choices come from ICookingConfirmedMajorChoicesGameplayFactory independently of saved payloads. Save and load compare Locked/CookFaster, ordered Decoration and unique Unlock set to that provider. Without it only locked empty choices with no buff are accepted. Current menu ConfirmedMaterialUnlocks cannot be granted by saved choices; current externally supplied menu permissions still govern. Global confirmed unlocks may survive a narrower level. Load restores progress once, without replaying PlaceUnlock or decoration migration into already carried stock.

Typed baseline format3 stores required HostFrameSequence. This preserves the existing monotonic Host clock instead of resetting it at generation boundaries. Published old typed2 records are explicitly rejected; Level8 and Recipe5 are unchanged. Legacy major format1 APIs remain separate and do not prove restart.

LoadMajorBaseline first reads/validates typed data and compares external configuration/preparation/Front/menu identities using one frozen factory result each. It creates an isolated kitchen, acquires ownership, checks match, installs trusted geometry before accepting the normalized success handoff, reconstructs runtime menu/progress/initial Front, and returns Created. It never fabricates Running or auto-starts service. Created spawn derives from the actual target factory InitialPoses projected under the stored validated layout, and is compared with both stored geometry seeds and normalized handoff poses. The rule is restricted to durable Created restart; saved live poses in same-Level Running restore remain supported.

## Actual verification

Final command in integration:

`dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj --filter "FullyQualifiedName~CookingMajorBaselineEtTests|FullyQualifiedName~CookingGenerationTransactionEtTests|FullyQualifiedName~CookingMenuReadyEtTests" --logger "trx;LogFileName=cooking-major-baseline-host-composite-final.trx"`

Actual **49 passed, 0 failed, 0 skipped**: 21 new durable cases, 16 prior generation cases, 12 prior Host menu cases. Final incremental compile had no new warnings/errors. The initial rebuild emitted existing LiteNet warnings; it passed14/14 durable cases. Subsequent seed/config/old2 controls passed17/17; the intermediate combination passed48/48; final extra legacy factory seed/Start-reuse case is included in49/49. `git diff --check` passed.

Evidence is under `local/Logs/cooking-execution/major-baseline-host/` including every initial/intermediate log/TRX and final cooking-major-baseline-host-composite-final.log/.trx. No failures were hidden or weakening changes required in this increment.

New controls prove exact uninterrupted-vs-new-store/dispose/load Created Observe and final complete checkpoint equivalence INCLUDING Host clock; pending delivery reception, manual partial continuation, remaining portions and next allocator identity; five ET publication failure points preserve source/tree/old bytes; blocked .next IO failure retains the previous readable file; retries never write it; unconfirmed/unlocked choices are rejected before factory stage/file write; rehashed buff requires independently confirmed provider; config/menu/hash/truncated/wrong-match/format1/typed2 rejection leaves authority reusable; legal alternate rehashed spawn cannot replace the trusted seed; foreign ownership is neither changed nor closed; legacy no-preparation factory durable transition uses the next trusted seed and its loaded kitchen is reused at Start.

These are production-kernel unit controls, with explicit BeginEnd shortcuts in the raw one-player fixture. They are NOT proof of natural service completion. The separately reviewed natural F01+D31 two-player fixture still needs Ended -> durable successor Created -> load acceptance before that combined product boundary is claimed. Root independent review and broad assembled gates remain outstanding.

## Uncommitted root prerequisite manifest

- `src/AbilityKit.Game.Cooking/CookingLevelLifecycle.cs`: 57555163E6922DFB731B9EAF79A992D967F9B42E44F0082CD44E8EB8D7E58EF9
- `src/AbilityKit.Game.Cooking/CookingLevelGenerationCommit.cs`: 197D2866D76E163714557849C8E933C37DABD940204D1E77E9B76534FEC7EB77
- `src/AbilityKit.Game.Cooking/CookingMajorBaseline.cs`: 861AB736ABEB8350E5CA22871A261FCE83769A544942E59A09BA2A541B970B37
- `src/AbilityKit.Game.Cooking.Tests/CookingPreparedGenerationCommitTests.cs`: 3E09CBA0EE409B4E6AD86640E2A1AB7C45D9B588A6B624328FE8DD17FF7F6D90
- `src/AbilityKit.Game.Cooking.Tests/CookingMajorBaselineStoreTests.cs`: C275974E7CD3E9616F950D333267D9AA2E7D7AFF35233152CD1FC5C740305BD9

All five were copied verbatim after root reported9/9 actual focused domain success. They remain uncommitted by this Host increment. Source and integration .NET window have been released for parent review.

## Independent review correction and final freeze

Reviewer found Factory.Create IOException escaped the initial filtered Load catch, even though cleanup ran. Load now returns structured InitializationFailed for ordinary factory exceptions consistently with generation staging; this affects only unpublished load and does not change the post-rename durable commit boundary. A real IOException control proves a structured result, identical existing file bytes and successful subsequent Created load with no singleton leak.

Final actual combination: **50 passed, 0 failed, 0 skipped** (22 durable +16 generation +12 menu). Command uses the same three filters and logger cooking-major-baseline-host-io-final.trx; command5.1seconds, tests2seconds. Final incremental build had no new warnings/errors. Prior49/49 remains historical pre-IO-fix evidence. All logs/TRX are preserved. Root-owned five core prerequisite files and two imported natural fixture/test files remain uncommitted by this Host commit. Source frozen, integration .NET window released for parent full gates; no full gate claimed here.
