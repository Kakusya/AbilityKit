# Implement：Cooking checkpoint 与恢复契约

> 上游：`design.md`。所有验证命令在仓库根目录执行；报告必须区分“计划 / 实际通过 / 失败 / 跳过”，缺少 Unity 环境不得记为通过。

## Phase 1：领域载荷与仿真出口

- [x] 新增 `src/AbilityKit.Game.Cooking/CookingRecipeCheckpoint.cs`：
  - `CookingRecipeCheckpoint` 及子记录（Item/Process/Container/Order/CleanPool/Deduplication 条目），`CanonicalText()/Sha256()`；
  - `CookingRecipeSimulation.ExportCheckpoint()`；
  - `CookingRecipeSimulation.RestoreCheckpoint(CookingRecipeCheckpoint)`（design §6 校验清单，原子替换提交，结构化 `CookingCheckpointRestoreReason`）。
- [x] 快照与 checkpoint 的区分在类型文档注释中写死（覆盖范围、消费者、不得互替）。
- [x] `dotnet build src/AbilityKit.Game.Cooking/AbilityKit.Game.Cooking.csproj` 通过。

## Phase 2：宿主级信封、codec 与恢复入口

- [x] `CookingLevelCheckpoint`（scope/config identity/preparation/lifecycle 状态与 version/HostFrameSequence/LastCommittedSimulationBatch/Recipe 载荷）。
- [x] `CookingLevelCheckpointCodec`（Serialize/Deserialize + `CookingCheckpointEnvelope` + `CookingCheckpointReadReason`），对照 P5 envelope 形态。
- [x] `CookingLevelEtHost.ExportCheckpoint()`（Running + pending 空前置，结构化拒绝）。
- [x] `CookingLevelEtHost.Restore(checkpoint, configuration, factory)`（design §5 七步；失败释放宿主）。
- [x] internal 接缝：`CookingLevelLifecycle.AdoptRecoveredVersion`、宿主 `HostFrameSequence`/`LastCommittedSimulationBatch` 的恢复写入。
- [x] `dotnet build src/AbilityKit.Game.Cooking.EtRuntime/AbilityKit.Game.Cooking.EtRuntime.csproj`（含 relation analyzer）通过。

## Phase 3：测试

- [x] `src/AbilityKit.Game.Cooking.Tests/CookingCheckpointRecoveryTests.cs`（C01–C04，Gate=CookingKitchenLoop）。
- [x] `src/AbilityKit.ET.Runtime.Tests/CookingLevelCheckpointTests.cs`（R01–R04，Gate=CookingLevelRuntime；宿主单例顺序执行；失败路径释放宿主）。
- [x] 运行两个 gate（`tools/run_test_gate.ps1 -Gate cooking-et-level-runtime`、`-Gate cooking-kitchen-loop`），记录真实输出与计数。
- [x] 既有回归：三个二进制指纹金样字节不变（`CookingLevelEtHostTests` 零 diff）。

## Phase 4：变异测试与独立复验

- [x] 变异候选（design §9 末段）：逐项临时破坏 → 对应测试转红 → 精确还原（同脚本同字符串回滚，不用 `git checkout --`）。
- [x] evidence 独立脚本复验（只读 JSONL，不加载被测程序集）：字段完整、批量/tick 递增、两臂终态记录一致。

## Phase 5：文档与收口

- [x] `.trellis/spec/cooking/cooking-recipe-loop.md`（或按归属新增条款到 `cooking-recipe-loop.md` + `cooking-persistence-management.md` 交叉引用）新增 2026-09-22 修约节：snapshot/checkpoint 区分、覆盖表、口径 §7 四项、实现状态声明、推定项。
- [x] `.trellis/spec/cooking/index.md` 头部追加本次修约注记。
- [x] `Docs/design/CookingGame/progress.md` 新增第 7 节（本次增量：契约、恢复验收、门禁数字、不证明项）。
- [x] `Docs/Todo.md` P0-C1 前两条勾选并写证据指针；总体判断段同步。
- [x] `tools/test-gates.json` 的 `cooking-kitchen-loop` description 补“恢复 checkpoint 契约”措辞 + `Docs/AbilityKit测试门禁与批量回归规范.md` §3 同步。
- [x] check.jsonl 记录真实命令/结果；`research/verification-2026-09-22.md` 写方法与复验。
- [x] `python .trellis/scripts/task.py validate` 通过后归档（`--no-commit --skip-branch-validation`，branch == base_branch == master）。

## 环境与卫生

- ET 宿主进程级单例：所有宿主用例顺序执行；任何中途失败路径先释放宿主再抛。
- evidence 目录环境变量 `COOKING_RECIPE_EVIDENCE_DIRECTORY` 用绝对路径（testhost 工作目录不是仓库根）。
- 仓库 `core.autocrlf=true`：CRLF 文件的多行编辑走 python 二进制脚本，不用 Edit 工具匹配 LF。
- 不新建分支；提交与否由 owner 决定。
