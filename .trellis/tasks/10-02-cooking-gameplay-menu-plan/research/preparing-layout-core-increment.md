# Prepared geometry core: limited source increment

2026-10-02, integration working changes after `d9b0e5603`, not committed or accepted as a complete layout exit.

The existing Lifecycle gate now exposes internal `IsLayoutInstallationOpen`, default false; its Level implementation opens only Created/Preparing while the kitchen is not closed. Gameplay admission and layout admission are separate, because Preparing already permits gameplay and Running must prohibit layout edits. Geometry installation still requires the existing authority window.

`ConfiguredAppliances` exposes the immutable fixture appliance map internally for trusted policy validation. Existing CanInstall/InstallPreparedGeometry accepts an optional `CookingRecipeCheckpoint restoreReferences`. Live editing validates current item/process/pose references and movement watermarks. Recovery validates saved item/process anchors and saved live poses against the staged geometry, instead of requiring discarded factory seed items to match the restored layout. Full payload integrity and allocations remain validated by Recipe.RestoreCheckpoint; this preflight does not replace it or grant authority.

Projected initial seed poses and restored live poses are separate: installation validates both against geometry but does not require their coordinates/watermarks to coincide. The geometry seed identity uses normalized LastMovementTick=-1; the live restored watermark comes from the Recipe payload.

AcceptSuccessHandoff now reads EffectiveSpatial.InitialPoses. That change alone does not establish next-Level geometry admission: the host currently adopts a handoff before next preparation, so the next-Level trusted geometry/permission transaction remains required.

Actual focused verification: CookingPreparedGeometryTests 5/5 passed, zero skips, integration `local/Logs/prepared-layout-core-final.log` and `local/Logs/prepared-layout-core/prepared-layout-core-final.trx`. Added controls prove that a saved new world anchor can replace a factory-only old anchor before Recipe restoration, and that an explicit Preparing layout gate works while gameplay is open but rejects a closed layout phase without state changes. The original three live geometry controls remain passing.

Root also completed the existing Preparing factory fixture's missing queue/table/source/receiving/storage/plate targets to support actual trusted initial installation. Host producer owns only host and new PreparedLayout ET tests. No full gate is claimed for the later host/layout WIP; master production remains `7a043b6cb`.
