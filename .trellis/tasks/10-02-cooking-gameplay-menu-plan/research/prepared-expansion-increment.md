# Preparing floor expansion acceptance increment

Scope: explicit S08 spatial expansion exit using the existing trusted natural fixture and existing production preparation/layout/ET authority. No existing fixture, production source, architecture or original natural proofs changed. Owned files are only the new `CookingPreparedExpansionEtTests.cs` and this report.

The Fact runs uninterrupted and cold-restored branches. Each begins Preparing with the existing fixture's initial floor. Real continuous Move commands take Chef through the empty bottom corridor to the original east boundary; the next eastward Move actually rejects with MovementBlocked and leaves X/Y unchanged. TryInstallPreparedLayout extends the existing floor by four cells. The same Move now succeeds, and subsequent movement places Chef beyond the original maximum X.

While Chef is inside that added area and the Host remains Preparing, the actual checkpoint is enveloped, serialized and decoded with the unchanged production codec. The recovered branch disposes its Host, creates a fresh trusted factory and restores the decoded checkpoint. Its complete checkpoint canonical text and frozen Observe canonical text match the saved state immediately. Both branches then perform the same further continuous Moves, reach Ready/Start, and tick Running. Every captured command frame, layout installation and final Running frame compares full checkpoint plus Observe canonical text; this is not a position-only or projected-layout assertion.

## Actual verification

Command: `dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj --filter FullyQualifiedName~CookingPreparedExpansionEtTests --no-restore --logger "trx;LogFileName=prepared-expansion-first.trx" --results-directory local/Logs/prepared-expansion`.

Actual result: **1 / 1 passed, 0 failed, 0 skipped, 964 ms**, exit 0. First focused run was green. Log: `local/Logs/prepared-expansion-first.log`; TRX: `local/Logs/prepared-expansion/prepared-expansion-first.trx`. `git diff --check` passed. Machine .NET window was released after completion.

The copied coordinator-owned production prerequisites (RecipeLoop, RecipeCheckpoint, GenerationTransactionCopy, RetryChoiceTransaction and ET Host) are byte-identical to master `86c3eb41d`; they remain excluded from this owner's commit. The frozen natural fixture matches master ignoring checkout line endings and was read-only throughout this increment.

Root has separately read the complete new test source. Independent integration/master gates and local merge remain coordinator work; this focused proof alone does not close all S08/S14 exits, network or Unity. It proves usable added floor space and Preparing cold recovery, not purchase cost, construction presentation or Unity scene expansion.
