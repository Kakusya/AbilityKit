# Reviewed front-house domain increment

2026-10-02. This extends the existing CookingFrontOfHouse owner; no parallel simulation or framework gameplay owner is added. Optional flow configuration references validated S01 spatial geometry, with immutable identity and paths. The domain models bounded FIFO arrivals/seating/walking/leaving, dirty-table cleaning and manual/companion work arbitration. Manual work preserves progress and does not award companion experience. New flows cannot use FinishInProgress to create ghost orders or complete manual work for free.

Snapshot/canonical/checkpoint include configured flow, manual-policy identity, work ownership/progress and table/customer routes. Restored manual policy must be rebound to the same identity; instance restore rejects incompatible policy/geometry without replacing current state. Wash work IDs include the bowl cycle, and same-bowl unfinished work is unique and must be the latest cycle. The independent review found and the implementer repaired the multi-active-cycle recovery hole; toxic checkpoints and valid controls exercise it.

Coordinator actual focused run: `dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj --filter FullyQualifiedName~CookingFrontOfHouseTests|FullyQualifiedName~CookingFrontOfHouseFlowTests --nologo --verbosity minimal`: **37/37**, failed/skipped0, `independent-domain-tests.log`. The filter was passed as one quoted shell argument.

Coordinator actual `cooking-kitchen-loop` gate: two builds exit0, focused**223/223**, Cooking**344/344**, ET**73/73**, failed/skipped0. `domain-increment-gate-summary.json` identifies actual raw logs/TRX in `local/Logs/test-gates/20261002-191938-cooking-kitchen-loop/cooking-kitchen-loop/`.

This is a reviewed domain increment only. Real front-work ET payload/fingerprints/ingress, cross-kitchen manual-job exclusion, initial/layout route projection, unified Level/checkpoint installation and full closing/next-Level application tests remain pending. Existing ET73 passing does not establish the new front-work ingress. S06 remains incomplete; the full goal remains active. No Unity or network claim is made.
