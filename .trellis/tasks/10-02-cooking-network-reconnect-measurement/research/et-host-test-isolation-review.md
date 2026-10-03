# Actual ET test host collection isolation

2026-10-03 root-dispatched test-only fix on main base `8e9d52ee5`. No production singleton, runtime owner, gate filter or whole-assembly parallelism change.

## Preserved actual master red

Gate `20261003-120928-cooking-et-level-runtime` failed in Game.Cooking.Tests:815 executed,808 passed,7 failed,0 skipped. Original stdout remains `local/Logs/master-n03-corrected-et.log`; original TRX `local/Logs/test-gates/20261003-120928-cooking-et-level-runtime/cooking-et-level-runtime/test-results/03-Cooking_domain_and_Level_lifecycle_tests.trx`. All7 error messages were inspected and are `Only one ET runtime host may be active per process.` They are the new paused2/deferred5 tests competing with another test class, not weakened behavior assertions or recovered production failures.

## Source cause and bounded fix

`ET.EtRuntimeHost` uses a process-wide World singleton and Interlocked active guard in constructor. Independent xUnit classes form separate collections by default and can therefore construct real hosts concurrently. A repository search for CookingLevelEtHost/EtRuntimeHost in all Game.Cooking.Tests C# sources found exactly three host-creating classes: CookingNetworkSessionV3Tests, CookingNetworkPausedPublicationTests and CookingNetworkDeferredAckIsolationTests. All three now use `[Collection(CookingEtHostTestCollection.Name)]`; one new CollectionDefinition names their shared collection. xUnit serializes tests in that collection. Pure Cooking test classes retain normal parallel scheduling. There is no global DisableTestParallelization or production synchronization/guard relaxation.

No assertion, theory case, scenario, factory or public API was changed. BOM/trailing blank changes from PowerShell writing were removed; existing source diff is one collection attribute line per class. SDK default glob includes the new collection definition without .csproj edits.

## Actual green validation

Root granted this reviewer the serialized .NET window for exactly these commands; it was released after completion.

1. `dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj --filter "FullyQualifiedName~CookingNetworkSessionV3Tests|FullyQualifiedName~CookingNetworkPausedPublicationTests|FullyQualifiedName~CookingNetworkDeferredAckIsolationTests" --logger "trx;LogFileName=et-host-combined.trx" --results-directory local/Logs/cooking-execution/network-et-test-isolation --verbosity minimal`: exit0,33 passed/0 failed/skipped. Actual TRX groups: SessionV3=26, Paused=2, Deferred=5. The dispatch estimate43 was not the current discoverable three-class count and is not used as evidence. Stdout `local/Logs/et-host-test-isolation-focused.log`.
2. `dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj --logger "trx;LogFileName=et-host-whole-cooking.trx" --results-directory local/Logs/cooking-execution/network-et-test-isolation --verbosity minimal`: exit0,815 passed/0 failed/skipped,5s test duration. Stdout `local/Logs/et-host-test-isolation-whole-cooking.log`.

Both complete build/type-check paths succeeded. `git diff --check` passed; only Git line-ending normalization warnings were emitted. No separate C# lint tool was invented. Final post-test writing only removed BOM/trailing whitespace; behavior compiled/tested is unchanged. Original7red and actual815green remain distinct artifacts.

Root still owns full configured gate replay including ET rich cases and SDK. These focused/full Game.Cooking results do not claim those other gates, physical LAN, process runner or performance acceptance.
