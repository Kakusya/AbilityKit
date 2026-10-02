# Paused publication and deferred ACK regression

Original production HEAD 852619887471f0ef479578d5c26c2cd2b03cecf2: actual 2 failed / 0 passed / 0 skipped. Exact old ACK failed to publish current Paused state (baseline count expected 3, actual 2). Exact ACK while cleanup pending was rejected BaselineRequired and never yielded Ready after Resume.

Root production fix ea7567980: actual 2 passed / 0 failed / 0 skipped. Final tests additionally repeat exact deferred ACK while Paused and assert no Ready, no rejection, unchanged Observe and HostFrameSequence. After Resume, cleanup clears, prior exact ACK yields Ready and a newer complete baseline. In the old-awaiting-ACK case, exact Paused ACK followed by twelve owner frames does not flood unchanged full state; recipe canonical and clock remain unchanged.

Command: `dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj --filter FullyQualifiedName~CookingNetworkPausedPublicationTests --logger "trx;LogFileName=paused-publication-final-duplicate.trx" --results-directory local/Logs/cooking-execution/network-paused-publication --verbosity minimal`.

Original log: `local/paused-publication-original-red.log`; original TRX: `local/Logs/cooking-execution/network-paused-publication/paused-publication-original-red.trx`.
Final log: `local/paused-publication-final-duplicate.log`; final TRX: `local/Logs/cooking-execution/network-paused-publication/paused-publication-final-duplicate.trx`.

Initial compile-only fixture error was corrected from inaccessible Driver.Simulation to public CaptureReadOnlyFullState; its separate log is retained and is not behavioral red evidence. A failed edit command did not alter source; final actual test run follows the successful patch.

Boundary: these two cases cover paused old-slot release, retained exact ACK across Resume, duplicate coalescing and no paused Tick. Dedicated close/scope/fault deferred-ACK controls are not added here; their branches still require broader integration/recovery evidence. No process-runner, physical LAN or performance verification is claimed. No production file was changed by this worker. The serialized .NET window was released before rich testing.
