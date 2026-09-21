# 验证记录：ET Level 闭环验收（2026-09-21）

任务 `09-21-cooking-et-closed-loop-acceptance`。本记录只写实际运行的命令与结果；未运行的环境/门禁不记为通过。证据根目录 `artifacts/cooking-et-closed-loop/`（gitignored，本地留存）。

## 1. 验收方法

- **产品侧唯一改动**：`CookingContent` 增加 `Snapshot` init 属性并在 `Load` 赋值注册表校验后快照（`src/AbilityKit.Game.Cooking/CookingContentCatalog.cs`）；领域规则、命令 wire、指纹与门禁配置零改动。
- **新增验收**：`src/AbilityKit.ET.Runtime.Tests/CookingLevelClosedLoopTests.cs`（`[Trait("Gate", "CookingLevelRuntime")]`），三个测试：
  - E01 全链路：正式内容（测试输出目录 `cooking-content-v2.json`）→ `BuildFixture` → `ApplyStandardInitialSupply`（宿主 `Start` 前）→ 17 条玩家命令全部 `host.TryEnqueue` + `host.Tick()` → 切 2/打蛋 2/煮 6 共 10 个纯时钟帧 → 帧间 `OpenOrder`/`CompleteWash` → 提交 → 洗碗回池。断言：产物定义与位置、订单 Completed、结算 1 条（全字段）、洗碗 1 次、碗池 1→2、命令帧单 disposition、时钟帧零 disposition、`HostFrameSequence == LogicalTick`、每帧 `tick.AfterLogicalTick == before+1`、`AdvanceTicks` admission 拒绝 `ReservedClockOperation`。
  - E02 拒绝零变更：领域 L03 同构（烤面包入碗 + 蛋花汤订单）。命令级：`result.StateVersion == before.Version`、无事件；帧级：与"同序列纯时钟帧"对照臂 canonical 相等（ET 宿主是进程级单例，两臂顺序执行）。
  - E03 确定性：两遍完整闭环 canonical 文本与 Sha256 相等。
- **测试卫生**（实现中发现并修复）：`EtRuntimeHost` 是进程级单例；`RunLoop` 中途失败与 `CreateStartedHost` 初始化失败都会先释放宿主再抛，避免失败测试把宿主泄漏进同进程后续测试（变异测试中曾观察到该类串联假象，修复后消失）。

## 2. 门禁实际输出（powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1）

| Gate | 结果 | 步骤实测 |
|---|---|---|
| `cooking-et-level-runtime` | passed in 9.1s | 4 步全 exit 0：analyzer build、Cooking ET runtime build、Cooking 175/175、ET runtime 43/43（本任务 +3） |
| `cooking-kitchen-loop` | passed in 10.6s | 5 步全 exit 0：Cooking domain build、ET runtime build、focused `Gate=CookingKitchenLoop` 54/54、Cooking 175/175、ET runtime 43/43 |

聚焦运行：`dotnet test src/AbilityKit.ET.Runtime.Tests --filter FullyQualifiedName~CookingLevelClosedLoopTests` → 3/3。ET 项目全量 43/43、Cooking 项目全量 175/175 与门禁一致。门禁日志：`local/Logs/test-gates/20260921-233*-cooking-*/`（本地）。

指纹金样：`CookingLevelEtHostTests.cs` 三个十六进制金样（6B91D5…/10F9BA…/70181A…）所在文件 `git diff` 零改动，且 ET 全量通过；命令记录与 `CanonicalBytes` 未触碰。

## 3. Evidence 与独立复验

`COOKING_RECIPE_EVIDENCE_DIRECTORY=artifacts/cooking-et-closed-loop`（绝对路径；相对路径会被 testhost 的工作目录解析）下 2 个 JSONL：

- `E01/.../et-closed-loop.jsonl`：17 条，全部 Accepted；相邻命令 logicalTick = 1,1,1,1,3,1,1,1,1,1,3,1,1,7,1,1（三处加工等待 3/3/7），终态 27；runner `dotnet test AbilityKit.ET.Runtime.Tests`，fixtureId `et-level-closed-loop`。
- `E02/.../et-closed-loop.jsonl`：8 条，7 Accepted + 末条 Rejected/OrderRequirementMismatch（SubmitOrder、0 事件）。

独立复验（`local/verify_et_closed_loop_evidence.py`，只读 JSONL、不加载被测程序集）：字段完整（含 before/after 64 位 hash、runner、fixtureId、assertionSummary）、批量严格递增且从 1 连续、logicalTick 严格递增、Accepted 记录形状（reason None、非 duplicate、恰 1 事件）、Rejected 记录零事件、E01 操作序列与时钟间隔、E02 拒绝分支——结果 `independent verification: OK`。

## 4. 变异测试（改后必须红，原样精确字符串还原）

| # | 变异 | 结果 |
|---|---|---|
| M1 | 翻转 RunLoop 的订单完成断言（Completed→Open） | E01/E03 红，E02 绿（精确杀死目标断言，无串联） |
| M2 | `ApplyStandardInitialSupply` 跳过工位项（锅不再入供） | 3/3 红（`ArgumentException: station:stove-a` 不受支持） |
| M3 | `CookingLevelEtHost.ExecuteFrameCore` 每帧多调一次 `AdvanceFixedTick` | 3/3 红（时钟帧纪律断言首杀：`Expected: 2`） |
| M4 | 去掉 `Load` 的 `Snapshot = snapshot` 赋值 | 3/3 红（生命周期 `ArgumentNullException: configuration`） |

还原后双 gate 复跑全绿（见 §2 末行）。还原方式全部为同一处精确字符串互换，未对含未提交改动的文件使用 `git checkout --`。

## 5. 未覆盖边界（显式记录，不宣称完成）

- **checkpoint/恢复、跨小关成功/失败/升级、settlement 落盘、connection→PlayerId 绑定、ECS 清退**：Todo P0-C1 后续未勾项，本任务不触碰。
- **前厅与订单生成节奏**：订单只能由前厅经 `OpenOrder` 帧间注入；验收不模拟顾客/NPC。
- **评分/收益/评价与小关结算、失败条件与失败重试**：owner 推迟项；结算记录仍不含评分字段。
- **多玩家/争抢仲裁在闭环中的行为**：仲裁由既有宿主机制测试覆盖，闭环验收用单玩家。
- **重复命令/乱序批次在闭环中的行为**：由既有宿主测试覆盖；E01 只发一条 `AdvanceTicks` 保留拒绝探针。
- **Unity、生产传输（KCP）、真实两 PC LAN、ET Phase B 权威迁移**：范围外/未启动。
- **领域闭环与宿主闭环的 canonical 字节一致**：不要求也不成立（终态 LogicalTick 27 vs 8，spec 已显式记录为口径）。
