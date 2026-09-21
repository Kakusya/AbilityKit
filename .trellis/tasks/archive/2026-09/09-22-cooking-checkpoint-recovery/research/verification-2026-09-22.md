# 验证记录：Cooking checkpoint 与恢复契约（2026-09-22）

> 任务 `09-22-cooking-checkpoint-recovery`。方法、真实命令与结果；复验只用 JSONL 文本，不加载被测程序集。

## 1. 门禁（实际运行，仓库根目录）

| Gate | 命令 | 结果 |
|---|---|---|
| `cooking-et-level-runtime` | `powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime` | 全步骤 exit 0；Cooking domain 179/179、ET runtime 47/47 |
| `cooking-kitchen-loop` | `powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop` | 全步骤 exit 0；focused `Gate=CookingKitchenLoop` 58/58、Cooking domain 179/179、ET runtime 47/47 |

基数为上一任务收盘的 Cooking 175/175、ET 43/43、focused 54/54；本任务新增域内 4 项（C01–C04，`CookingKitchenLoop`）与宿主级 4 项（R01–R04，`CookingLevelRuntime`），无既有测试改写或删除。`CookingLevelEtHostTests`（三个二进制指纹金样）零改动、`git diff` 为空；`tools/test-gates.json` 仅 `cooking-kitchen-loop` description 增加“恢复 checkpoint 契约”措辞，门禁文档 §3 表格同步，无新增 gate、无步骤变化。

证据复算命令：

```bash
COOKING_RECIPE_EVIDENCE_DIRECTORY=<repo>/artifacts/cooking-checkpoint-recovery \
  dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj --filter FullyQualifiedName~CookingCheckpointRecoveryTests
COOKING_RECIPE_EVIDENCE_DIRECTORY=<repo>/artifacts/cooking-checkpoint-recovery \
  dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj --filter FullyQualifiedName~CookingLevelCheckpointTests
```

产物：`artifacts/cooking-checkpoint-recovery/R01-baseline`（21 条）、`R01-recovery`（21 条）、`C02-source`（21 条）、`C02-restored`（7 条）。

## 2. 独立复验（`local/verify_checkpoint_evidence.py`，仅读 JSONL）

```bash
python local/verify_checkpoint_evidence.py artifacts/cooking-checkpoint-recovery
# VERIFY OK: R01-baseline=21 R01-recovery=21 C02-source=21 C02-restored=7;
# fields, sequences, batch monotonicity and pairwise arm equality all hold
```

校验内容：必填字段非空（testId/fixtureId/command/logicalTick/outcome/before+afterStateHash/assertionSummary/runner/timestampUtc）、Runner 与 FixtureId 归属、21 条操作序列逐位匹配预期序列（碗上台面→切→入锅→打蛋→倒蛋液→煮制启动【导出点】→端走→倒汤→提交→锅放回→烤面包探针）、宿主两臂 `simulationBatch` 从 1 起严格递增、域臂 logicalTick 单调、**R01 两臂逐位一致**（command identity/operation/outcome/before-after 哈希/logicalTick/isDuplicate）与 C02 源臂尾 7 条 == 恢复臂 7 条、以及“before==after 必须伴随 Rejected”纪律。

## 3. 变异测试（临时破坏 → 对应测试转红 → 同字符串精确还原）

| 变异 | 破坏点 | 结果 |
|---|---|---|
| M1 tombstone 不入账 | `ExportCheckpoint` 过滤 `Removed` 物品 | 杀死：C01（墓碑断言）红 |
| M2 干净池计数不入账 | `ExportCheckpoint` 干净池改空字典 | 杀死：C01、C02、R01 红 |
| M3 去重账本不入账 | `ExportCheckpoint` 去重账本改空字典 | 杀死：C01（账本条数）、C04 红 |
| M4 锁输入不入账 | 导出进程 `LockedInputs` 置空 | 杀死：C01–C04 全红、R01/R02 红（恢复校验拒绝） |
| M5 HostFrameSequence 不续接 | `AdoptRecoveredCheckpoint` 置 0 | 杀死：R01（终态 canonical 不等）、R04（帧序列断言）红 |

还原后双套件复跑全绿（域内 4/4、宿主级 4/4，exit 0）。

## 4. 实现期发现并修复的问题

- ET 宿主进程级单例约束：R01 初版让基线宿主与恢复宿主同时存在，触发 “Only one ET runtime host may be active per process”；改为两臂顺序执行（基线臂含去重探针后先释放）。与既有 ClosedLoop 测试同一纪律。
- 契约缺口（实现期发现）：仅改 epoch 的 checkpoint 会绕过全部校验被静默恢复成另一代际。修复：`Restore` 增加 payload-scope 一致性前置（`CheckpointPayloadScopeMismatch`），并在 R03 固化。
- 口径修正：去重指纹覆盖整条命令（含 `SimulationBatch`），同 identity 换新批量重投在仿真侧判 `CommandIdentityConflict`；因此宿主级“过期命令不二次推进”的证明是 基线臂终态簿记 `Duplicate` vs 恢复臂 admission `BatchStale`，两臂状态版本与结算次数不变（design §7.1 已按实测改写，域级同命令重放由 C04 证明）。

## 5. 未覆盖边界（范围外，非通过项）

durable store 与进程崩溃恢复；跨小关 checkpoint 产品语义（保存/清除/加载流程）；失败条件与失败重试；评分/收益/评价；前厅与订单生成节奏；Paused 代际导出；lifecycle 事件历史恢复；生产传输；真实两 PC LAN；Unity 一切范围；ET Phase B 权威迁移。
