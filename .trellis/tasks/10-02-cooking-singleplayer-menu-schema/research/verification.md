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
