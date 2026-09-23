# Cooking Game 当前工程进度

> 文档类型：跨阶段状态入口，不是行为规格或新的完成证明。行为契约以 [`.trellis/spec/cooking/`](../../../.trellis/spec/cooking/index.md) 为准；历史命令与结果以对应归档 task 的 `check.jsonl` 为准。

## 1. 2026-09-16 治理结论

Owner 已批准把过去混在同一阶段 task 中的状态拆为三类：

1. **Archived verified delivery**：P0–P6 各自已有 `check.jsonl` 支持的纯 .NET 受限增量，按 `completed-limited-scope` 语义归档。
2. **Non-Unity successor backlog**：P1–P6 仍可能有价值但未启动、未批准、无时间表的产品化工作，统一见 [successor backlog](successor-backlog.md)；P0 无 successor。
3. **Prohibited Unity future scope**：Cooking Unity package、asmdef、scene、authoring、projection、UI、EditMode 与 scene smoke 长期禁止、不可领取、非 blocker，统一见 [future scope](future-scope.md)。

`archived`/`completed` 只修饰重划后的有限纯 .NET 交付，不表示完整 P0–P6 产品出口、Unity 可玩版本、production transport、真实两 PC LAN 或 durable storage 已完成。

## 2. Archived verified delivery

| 阶段 | 已验证并收口的受限交付 | 历史 evidence |
|---|---|---|
| **P0 交互基础** | 纯 .NET 权威拾取/放下、唯一位置、稳定排序、原子校验提交、命令幂等与 canonical snapshot/hash | `10/10`；8 个 JSONL、21 条记录；[check](../../../.trellis/tasks/archive/2026-09/09-15-cooking-interaction-foundation/check.jsonl) |
| **P1 会话权威** | transport-neutral binding、handshake、bounded ingress、baseline/delta、epoch/sequence、dedup 与 transport-loss diagnostics | build 0 warning/error；权威摘要 `20/20`；6 个 JSONL、50 条记录；[check](../../../.trellis/tasks/archive/2026-09/09-15-cooking-lan-session/check.jsonl) |
| **P2 配方循环** | 单输入/单工序/3 tick fixture、product、plate、注入式 order accept/reject、幂等与 in-process equivalence | 当时完整回归 `27/27`；5 个 JSONL、19 条记录；[check](../../../.trellis/tasks/archive/2026-09/09-15-cooking-recipe-loop/check.jsonl) |
| **P3 配置校验** | definition batch 全错误诊断、原子替换、不可变 snapshot、canonical identity/hash、schema migration blocked result | focused `6/6`，当时完整回归 `34/34`；[check](../../../.trellis/tasks/archive/2026-09/09-15-cooking-config-validation/check.jsonl) |
| **P4 Match 生命周期** | fixture `Preparing → Ready → Started → Ended`、restart 新 Match/epoch、隔离、snapshot watermark | focused `6/6`，当时完整回归 `40/40`；10 条 evidence；[check](../../../.trellis/tasks/archive/2026-09/09-15-cooking-match-lifecycle/check.jsonl) |
| **P5 持久化技术合同** | 长期 progress、confirmed settlement、幂等 ledger、integrity envelope、in-memory `prepare → commit → read` 与 validated restart | focused `12/12`，当时完整回归 `52/52`；7 条 evidence；[check](../../../.trellis/tasks/archive/2026-09/09-15-cooking-persistence-management/check.jsonl) |
| **P6 网络测量基线** | 显式 `InProcess` workload/report、ingress-to-commit、queue/throughput/p50/p95/p99/allocation、fault trace 与 optimization blocker | focused `5/5`，完整回归 `57/57`；JSON/CSV/JSONL；[check](../../../.trellis/tasks/archive/2026-09/09-15-cooking-network-measurement/check.jsonl) |

P1 旧 metadata 曾写 `19/19`，属于摘要漂移；原始权威 check event 为 `20/20`，本次不改写该历史事件，只修正摘要。P0 的运行时 artifact 目录仍可在 check event/evidence 文字中引用，但不再作为 manifest `file` context。

## 3. 当前可运行纵向链路

纯 .NET fixture 已能无界面地创建 scope 与实例、完成 in-process handshake、执行权威交互、运行最小配方、推进 Match、应用 settlement/progress，并生成 InProcess 测量诊断。此外曾有 pure .NET Cooking UDP 链路（LiteNetLib reliable-UDP loopback 与同机双进程 listen-host/client），其 task 已于 2026-09-21 因 owner 决定放弃并归档；三个 Cooking UDP 项目退出当前构建范围，源码与 artifact 证据保留，不得据此宣称能力被删除或从未存在。真实 artifact 仍将其标为 same-machine，且两台物理 PC LAN 未运行。以上证明有限领域合同、测试 transport 边界和同机真实 socket 曾经可以运行；不证明 Unity 表现、物理两机 LAN 或 durable storage，也不证明当前范围包含网络内容。2026-09-21 起，单机纯 .NET 范围另有一条可运行的番茄蛋花汤厨房闭环，其契约层、仿真规则、正式内容与 ET 宿主闭环验收分三个 task 交付，见第 4、5、6 节。 2026-09-22 起，该范围另有一条运行态恢复契约（checkpoint 与销毁重建等价验收），见第 7 节。

## 4. 2026-09-21 单机厨房闭环增量

Owner 于 2026-09-21 把范围收敛为单机纯 C#/.NET，并按两个 Trellis task 交付厨房闭环的契约层与仿真规则；两个 task 均已归档，均不含传输、UDP/KCP 与 Unity 内容。

| Task | 已验证并收口的受限交付 | Evidence |
|---|---|---|
| `09-21-cooking-kitchen-loop-contracts` | schema 升 `cooking-definition-v2`（物品容器能力、多输入集合、默认供应、完成形态；v1 配置与快照以结构化 blocked 诊断拒绝，不写迁移）、纯函数配方匹配（集合相等，歧义时返回确定候选表）、命令形状放松与仿真/ET 两套指纹跨面对齐 | [check](../../../.trellis/tasks/archive/2026-09/09-21-cooking-kitchen-loop-contracts/check.jsonl)；`artifacts/cooking-kitchen-loop-contracts/` |
| `09-21-cooking-kitchen-loop-simulation` | 容器即物品（`CookingContainerDefinition` 退役，内容物按容器物品 ID 归属，快照与 canonical 可观察）、七项权威原子动作（拾取/放下/放入/取出/启动加工/倒出/提交，`Plate` 退役）、放入即拒绝、锁输入与 item→process 反查、端走继续的位置白名单（腐败检测器保持可达）、两种完成形态进入 fixed-tick 原子交换、命令与 fixed-tick 共享产物 ID allocator、按容器当前位置判定提交可达性、番茄蛋花汤闭环 fixture（免工位打蛋、工位按加工类型绑定、烤箱配方、订单要求与完成回报、碗脏/净与带上限的干净碗池加 NPC 清洗注入端口）、批次争抢按 Tick/玩家/命令序号稳定排序 | [check](../../../.trellis/tasks/archive/2026-09/09-21-cooking-kitchen-loop-simulation/check.jsonl)；[research/verification-2026-09-21.md](../../../.trellis/tasks/archive/2026-09/09-21-cooking-kitchen-loop-simulation/research/verification-2026-09-21.md)；`artifacts/cooking-kitchen-loop-domain/`（21 个目录） |

门禁：新增 P1 `cooking-kitchen-loop`（域构建、ET runtime 构建、focused `Gate=CookingKitchenLoop` 42/42、Cooking 163/163、ET runtime 40/40），回归门禁 `cooking-et-level-runtime` 同时通过；[`tools/test-gates.json`](../../../tools/test-gates.json) 与 [测试门禁规范](../../AbilityKit测试门禁与批量回归规范.md) §3 已同步。

以上只证明单机纯 .NET 厨房闭环规则与闭环 fixture 可运行、可确定性重放、可按门禁验证，且 ET 命令指纹金样已随 `Plate` 退役处理（pickup 与 StartProcess 两个既有金样字节不变，新增 PutIn 金样）。不证明失败条件与小关成功条件（owner 明确推迟）、订单生成节奏与前厅、固定伙伴最终人数、生产传输（KCP，未启动未批准）、durable storage、真实两 PC LAN、Unity 可玩版本或 ET Phase B 权威迁移。计划中任务③的单机验收动作（指纹金样重锚、验收 evidence 落盘、回归门禁实际运行与记录）已由任务①②连带完成，本文与 [Todo.md](../../Todo.md) 的状态表述即其收口，未为此新建独立 task。

2026-09-21 另有一条单机增量把正式内容与 order owner 落地，见第 5 节。

## 5. 2026-09-21 单机正式内容与 order owner 增量

Owner 于 2026-09-21 批准 Trellis task `09-21-cooking-formal-content-and-orders`（successor backlog P2 首条），把“单输入/单工序/3 Tick fixture + 测试端口判定订单要求”的临时形态替换为正式内容与领域订单归属；task 已归档，不含传输、UDP/KCP 与 Unity 内容。

| 受限交付 | Evidence |
|---|---|
| 正式内容目录：数据文档 `cooking-definition-v2`（`src/AbilityKit.Game.Cooking/Content/cooking-content-v2.json`）经 `CookingContentCatalog` 加载并过 v2 校验；10 物品、4 工位、4 配方（切番茄/打蛋/番茄蛋花汤/烤面包）、1 订单模板、5 项标准初始供应；加工时长等数值只存在于内容文档；烤面包按 owner 采纳版本“面包片→烤面包”，占位 dough 退役 | [check](../../../.trellis/tasks/archive/2026-09/09-21-cooking-formal-content-and-orders/check.jsonl)；[research/verification-2026-09-21.md](../../../.trellis/tasks/archive/2026-09/09-21-cooking-formal-content-and-orders/research/verification-2026-09-21.md)；`artifacts/cooking-formal-content/` |
| v2 校验扩展：候选/canonical 增加订单模板与标准初始供应两段（身份字符串保持 v2）；订单模板外键（要求 recipe 存在、要求容器定义存在、带容器能力且接受该 recipe 产物）与供应项校验（定义存在、数量为正、位置语法、station 引用）；两处既有规则显式放宽（工位可声明零能力、免工位配方豁免 `CapabilityUnavailable`） | 同上；spec [cooking-config-validation](../../../.trellis/spec/cooking/cooking-config-validation.md) P3 行 |
| order owner 移入领域：订单簿（模板、要求、Open/Completed）与快照/canonical 可观察；开单是前厅注入入口（`OpenOrder`，与 `CompleteWash` 同模式，不产生命令事件）；`ICookingOrderPort`/`CookingOrderSubmission`/`CookingOrderAcceptance` 退役 | [check](../../../.trellis/tasks/archive/2026-09/09-21-cooking-formal-content-and-orders/check.jsonl) |
| 提交/结算契约：五个结构化拒绝分支（未开单、订单已完成、recipe 不匹配、容器定义不匹配、产物已消费）全部 mutation-free；成功提交原子消耗产物、订单 Completed 一次、追加结算记录（sequence/order/template/recipe/product/player/container/logicalTick，无评分字段）；闭环 fixture 改从正式内容运行并保持确定性重放 | 同上 |

门禁：沿用 P1 `cooking-kitchen-loop`（focused `Gate=CookingKitchenLoop` 54/54、Cooking 175/175、ET runtime 40/40，含三个二进制指纹金样字节不变），回归 `cooking-et-level-runtime` 同时通过；未新增 gate。

以上只证明正式内容可数据驱动加载与校验、订单归属与提交/结算契约在领域内确定成立、闭环可从正式内容确定性重放。不证明评分/收益/评价与小关结算（owner 推迟，结算记录刻意不含这些字段）、失败条件与失败重试（owner 推迟）、订单生成节奏与前厅顾客/NPC 过程（订单只能由前厅经 `OpenOrder` 注入）、过度加工与烧焦、跨小关装修/道具/Buff、检查点、Level/Map schema 对内容的正式引用（P3）、生产传输、真实两 PC LAN、Unity 可玩版本或 ET Phase B 权威迁移。

## 6. 2026-09-21 ET Level 闭环验收增量

Owner 于 2026-09-21 批准 Trellis task `09-21-cooking-et-closed-loop-acceptance`（Todo P0-C1 未勾项"让 ET fixed-tick host 承载同一条番茄蛋花汤闭环验收"），把第 4/5 节只在领域仿真层运行的闭环搬到 ET fixed-tick Level 宿主；task 已归档，不含传输、UDP/KCP 与 Unity 内容。

| 受限交付 | Evidence |
|---|---|
| 正式内容直达 ET 宿主：`CookingContentCatalog.Load`（测试输出目录的 `cooking-content-v2.json`）→ `BuildFixture` → `ApplyStandardInitialSupply`，ET 侧不再手写第二套 items/appliances/recipes 字典；`CookingContent` 增露加载时经 v2 校验的快照，Level 生命周期与 preparation 配置身份同一来源 | [check](../../../.trellis/tasks/archive/2026-09/09-21-cooking-et-closed-loop-acceptance/check.jsonl)；[research/verification-2026-09-21.md](../../../.trellis/tasks/archive/2026-09/09-21-cooking-et-closed-loop-acceptance/research/verification-2026-09-21.md)；`artifacts/cooking-et-closed-loop/` |
| 全链路走宿主命令路径：E01 从标准初始供应出发，17 条玩家命令全部经 `TryEnqueue` + `Tick()`（每帧一个最小命令批次 + 一次 fixed tick），切番茄/打蛋/煮制的 10 个纯时钟帧只由宿主帧驱动，走完取料、切、入锅、打蛋、倒蛋液、煮制、端走、倒汤、开单（帧间注入）、提交、洗碗回池；帧结构断言钉住命令帧单 disposition、时钟帧零 disposition、`HostFrameSequence == LogicalTick`；`AdvanceTicks` 在 admission 被 `ReservedClockOperation` 拒绝（宿主拥有时钟） | 同上；`artifacts/cooking-et-closed-loop/E01/`（17 条证据，全部 Accepted，终态 `LogicalTick=27`） |
| 拒绝零变更两级证明：E02 以领域 L03 同构场景（烤面包入碗 + 蛋花汤订单）经宿主提交，命令级 `result.StateVersion == before.Version` 且无事件，帧级与"同序列纯时钟帧"对照臂 canonical 相等（每帧固有的 tick 推进由对照臂抵消）；无结算、订单仍 Open、碗中菜品不回滚 | 同上；`artifacts/cooking-et-closed-loop/E02/`（8 条证据，末条 Rejected/OrderRequirementMismatch、0 事件） |
| 确定性与回归：E03 两遍完整宿主闭环 canonical 文本与 Sha256 一致；既有 24 个宿主机制测试与 legacy 纵切不改写法；三个二进制指纹金样（pickup/start-process/put-in）字节不变（`CookingLevelEtHostTests` 零改动）；命令 wire 形状不变；未新增 gate | 同上 |

门禁：沿用 P1 `cooking-et-level-runtime`（Cooking 175/175、ET runtime 43/43，本任务 +3）与 P1 `cooking-kitchen-loop`（focused `Gate=CookingKitchenLoop` 54/54、Cooking 175/175、ET runtime 43/43），均全步骤 exit 0；[`tools/test-gates.json`](../../../tools/test-gates.json) 未改动。变异测试 4 项全部杀死（订单完成断言、标准供应工位项、宿主每帧 fixed tick 数、内容快照赋值）；evidence 经不加载被测程序集的独立脚本复验（字段完整、批量严格递增、tick 递增、操作序列与时钟间隔、拒绝记录零事件）。

以上只证明同一条番茄蛋花汤闭环可在 ET fixed-tick Level 宿主上以正式内容运行、拒绝在宿主命令路径零变更、可确定性重放，且宿主机制与指纹金样无回归。不证明评分/收益/评价与小关结算（owner 推迟）、失败条件与失败重试（owner 推迟）、前厅与订单生成节奏（订单只能由前厅经 `OpenOrder` 注入）、过度加工与烧焦、跨小关规则、检查点与 snapshot/checkpoint 分离、生产传输、真实两 PC LAN、Unity 可玩版本或 ET Phase B 权威迁移。宿主与领域闭环的 canonical 不要求字节一致：宿主每帧推进一个 tick（终态 `LogicalTick=27`），领域 L01 用 `AdvanceTicks` 与显式帧 1–8（终态 `8`），该差异是宿主拥有时钟的必然结果，已在 spec 显式记录为口径而非回归。

## 7. 2026-09-22 恢复 checkpoint 契约增量

Owner 于 2026-09-22 批准 Trellis task `09-22-cooking-checkpoint-recovery`（Todo P0-C1 前两条未勾项：区分同步 snapshot 与恢复 checkpoint；“导出 checkpoint -> 销毁 host -> 重建 -> 继续运行”等价验收），task 已归档，不含传输、UDP/KCP 与 Unity 内容。

| 受限交付 | Evidence |
|---|---|
| 同步快照与恢复 checkpoint 显式区分：`CookingRecipeCheckpoint` 覆盖物品/tombstone、活动加工（elapsed/required/completion/container/lockedInputs）、容器**有序**内容、订单、结算、消耗产物账、干净碗池计数、去重账本、事件/tick 历史与 event sequence、三个 ID 计数器、state version/logical tick 与 scope；可派生索引（持物、进程索引、锁输入反查）由载荷重建；`CookingRecipeSimulation.RestoreCheckpoint` 整册换入（原子替换、结构化拒绝、零变更），恢复后既有 fixed-tick 腐败检测器在下一 tick 兜底复核 | [check](../../../.trellis/tasks/archive/2026-09/09-22-cooking-checkpoint-recovery/check.jsonl)；[research/verification-2026-09-22.md](../../../.trellis/tasks/archive/2026-09/09-22-cooking-checkpoint-recovery/research/verification-2026-09-22.md)；`artifacts/cooking-checkpoint-recovery/` |
| 宿主级信封与恢复入口：`CookingLevelCheckpoint`（level scope/epoch、config identity、preparation、lifecycle 状态/version、HostFrameSequence、命令水位 + 整册仿真载荷）经 `CookingLevelCheckpointCodec`（格式版本 + 完整性 + 结构化读回）序列化；`CookingLevelEtHost.ExportCheckpoint` 前置 Running 且 pending 为空；静态 `Restore` 按同一代际重建（同 scope/epoch/config identity、仿真由工厂创建后整册换入、HostFrameSequence 单调不 reset、失败即释放宿主编修） | 同上；spec [`cooking-recipe-loop.md`](../../../.trellis/spec/cooking/cooking-recipe-loop.md) 2026-09-22 修约节 |
| 等价验收：R01 基线臂与恢复臂（煮制进行中导出 → 销毁 → 重建 → 继续）终态 canonical/Sha256、state version、logical tick、下一产物 ID（烤面包探针 `product-4`）、结算次数与两份终态 checkpoint canonical 全部相等，两臂 evidence 逐位一致；R02 导出前置（pending 非空/Paused 拒绝）；R03 跨代际/跨配置/载荷投毒结构化拒绝；R04 HostFrameSequence 连续；域内 C01 覆盖表逐项、C02 序列化往返后续跑等价、C03 篡改/截断/外键拒绝零变更、C04 恢复后去重账本仍生效 | 同上；`artifacts/cooking-checkpoint-recovery/`（R01 两臂各 21 条、C02 双臂 21/7 条证据，独立脚本复验通过） |

门禁：沿用 P1 `cooking-et-level-runtime`（Cooking 179/179、ET runtime 47/47，本任务 +8）与 P1 `cooking-kitchen-loop`（focused `Gate=CookingKitchenLoop` 58/58、Cooking 179/179、ET runtime 47/47），均全步骤 exit 0；[`tools/test-gates.json`](../../../tools/test-gates.json) 未新增 gate，仅 `cooking-kitchen-loop` description 补“恢复 checkpoint 契约”措辞（域内新测试挂 `CookingKitchenLoop` trait）。变异测试 5 项全部杀死（tombstone 不入账、干净池计数不入账、去重账本不入账、锁输入不入账、HostFrameSequence 不续接）；既有测试零回归，三个二进制指纹金样字节不变（`CookingLevelEtHostTests` 零改动）。

以上只证明单机纯 C# 的运行态恢复契约成立：checkpoint 自包含、可结构化拒绝、销毁重建后与不中断基线不可区分。不证明 durable storage 或进程崩溃恢复（store 未实现）、跨小关 checkpoint 产品语义（保存/清除/加载流程，见 09-19 讨论 PRD）、失败条件与失败重试、评分/收益/评价、前厅与订单生成节奏、Paused 代际导出、生产传输、真实两 PC LAN、Unity 可玩版本或 ET Phase B 权威迁移。宿主与领域闭环计数器口径不变：宿主每帧推进一个 fixed tick，checkpoint 的 LogicalTick 与 HostFrameSequence 在导出点相等。

## 8. 2026-09-22 小关成功交接增量

成功换代不再新建空厨房。`ExportSuccessHandoff` 保留物品、加工、容器、脏/净碗、消耗产物账、下一 Process 与下一产物 ID；清除订单、结算、去重和两类事件；逻辑 Tick、事件序号、状态版本、下一结算序号归零。宿主把这份厨房换入下一代并停在 `Created`。准备态不能改厨房。工位升级和写盘仍未做。失败重开见第 9 节。

门禁：`cooking-kitchen-loop`（focused 61/61、Cooking 182/182、ET runtime 49/49）与 `cooking-et-level-runtime`（Cooking 182/182、ET runtime 49/49）均 exit 0。日志在 `local/Logs/test-gates/20260922-112553-cooking-kitchen-loop` 与 `local/Logs/test-gates/20260922-112611-cooking-et-level-runtime`。

## 9. 2026-09-22 小关失败重开增量

失败换代不再把空厨房留到下一次 `Start`。`CreateRetry` 在同一 `LevelId`、更高 `LevelEpoch`、同一 Match 上安装下一代，丢掉失败现场，用工厂新建仿真并按 `ApplyStandardInitialSupply` 摆厨房，然后停在 `Created`。准备态不能改厨房。不写盘，也不改同代际恢复或成功交接。失败条件仍未定义。

门禁：`cooking-kitchen-loop`（focused 61/61、Cooking 182/182、ET runtime 51/51）与 `cooking-et-level-runtime`（Cooking 182/182、ET runtime 51/51）均 exit 0。日志在 `local/Logs/test-gates/20260922-154801-cooking-kitchen-loop` 与 `local/Logs/test-gates/20260922-154818-cooking-et-level-runtime`。

## 10. 2026-09-22 小关成功结算确认增量

成功收口后、交接前，可以按这一代的 Match、`LevelId`、`LevelEpoch` 记住本关结算条。同一份列表再确认一次不再生效。失败、未结束和准备态不能确认。交接后的空账不能盖掉已经记住的列表。不打分，不加钱，不写盘。

门禁：`cooking-kitchen-loop`（focused 63/63、Cooking 184/184、ET runtime 52/52）与 `cooking-et-level-runtime`（Cooking 184/184、ET runtime 52/52）均 exit 0。日志在 `local/Logs/test-gates/20260922-170119-cooking-kitchen-loop` 与 `local/Logs/test-gates/20260922-170138-cooking-et-level-runtime`。

## 11. 2026-09-22 小关结算落盘增量

成功确认后的结算列表可以按这一代的 Match、`LevelId`、`LevelEpoch` 写到调用方给的目录。关掉读取器再用同一目录新建一个，读回的仍是同一份列表。同一份再写一次是重复。换一份列表被拒绝，不覆盖第一次。空列表和「没有这条记录」不是同一种结果。截断或篡改后的文件读取失败，不当成空确认。文件没写上时，这一次新的内存确认会撤回。失败关卡和交接之后的空账不能写。不打分，不加钱。

门禁：`cooking-kitchen-loop`（focused 66/66、Cooking 187/187、ET runtime 53/53）与 `cooking-et-level-runtime`（Cooking 187/187、ET runtime 53/53）均 exit 0。日志在 `local/Logs/test-gates/20260922-174507-cooking-kitchen-loop` 与 `local/Logs/test-gates/20260922-174525-cooking-et-level-runtime`。验收只用同一测试进程里的新读取器，不证明断电或另一个操作系统进程被杀死后的恢复。

## 12. 2026-09-22 跨小关装修、道具、Buff 与成功检查点增量

准备态可以把旧工位换成新工位。没做完的工序跟着走，已经走过的进度和所需时长都不变。工位不存在，或者新工位上已经有工序，整次拒绝。解锁的定义按标准供应再摆进延续厨房，不把厨房推倒重来。煮制加速只缩短这之后新开始的番茄蛋花汤，正在煮的那一锅不改，也不能叠两层。大关检查点只在这些选择锁定、并且这一关已经成功交接之后写入。失败不写，坏文件不能当成一份新餐厅。

门禁：`cooking-kitchen-loop`（focused 70/70、Cooking 191/191、ET runtime 54/54）与 `cooking-et-level-runtime`（Cooking 191/191、ET runtime 54/54）均 exit 0。日志在 `local/Logs/test-gates/20260922-185452-cooking-kitchen-loop` 与 `local/Logs/test-gates/20260922-185531-cooking-et-level-runtime`。评分和真实断电恢复仍未做。

## 13. 2026-09-22 前厅询问、洗碗与小关时间增量

一位固定伙伴在营业时间按到店顺序询问，问完才开单。没有空桌不入座，收尾不再来人。没有待询问的桌子时，伙伴才去洗脏碗。顾客等到上限仍没吃上就离席；已经开出的订单标成未满足，不写结算，也不算失败。营业结束、座位空、伙伴空闲之后，才允许由前厅驱动成功。旧的直接成功收口仍可调用。宿主只在运行帧之后推进一步，暂停不推进。

门禁：`cooking-kitchen-loop`（focused 75/75、Cooking 196/196、ET runtime 55/55）与 `cooking-et-level-runtime`（Cooking 196/196、ET runtime 55/55）均 exit 0。日志在 `local/Logs/test-gates/20260922-224049-cooking-kitchen-loop` 与 `local/Logs/test-gates/20260922-224111-cooking-et-level-runtime`。可见顾客、收益、评价、伙伴成长和失败条件仍未做。

## 14. 2026-09-22 用餐占桌增量

订单完成后，顾客按配置的用餐 tick 继续占桌，到点才离席。这段时间不能由前厅结束服务。未满足仍然马上离席，不写结算。没挂前厅的关卡仍可直接结束。

门禁：`cooking-kitchen-loop`（focused 76/76、Cooking 197/197、ET runtime 56/56）与 `cooking-et-level-runtime`（Cooking 197/197、ET runtime 56/56）均 exit 0。日志在 `local/Logs/test-gates/20260922-232144-cooking-kitchen-loop` 与 `local/Logs/test-gates/20260922-232204-cooking-et-level-runtime`。收益、评价和可见顾客仍未做。

## 15. 2026-09-22 下一小关清空前厅增量

成功交接后，前厅的座位、未满足和营业时钟清空。正在洗的碗会先洗完。还没洗的脏碗留在厨房，下一关伙伴空闲时继续洗。失败重开不走这次清空。

门禁：`cooking-kitchen-loop`（focused 77/77、Cooking 198/198、ET runtime 57/57）与 `cooking-et-level-runtime`（Cooking 198/198、ET runtime 57/57）均 exit 0。日志在 `local/Logs/test-gates/20260922-235333-cooking-kitchen-loop` 与 `local/Logs/test-gates/20260922-235404-cooking-et-level-runtime`。收益、评价和可见顾客仍未做。

## 16. 2026-09-23 失败重开丢掉前厅增量

失败重开接受新厨房后，前厅的座位、未满足、营业时钟和洗碗队列都丢掉。正在询问的顾客不会在新厨房开出订单，正在洗的旧碗也不会被洗进新厨房。成功交接仍先洗完正在洗的碗。

门禁：`cooking-kitchen-loop`（focused 78/78、Cooking 199/199、ET runtime 58/58）与 `cooking-et-level-runtime`（Cooking 199/199、ET runtime 58/58）均 exit 0。日志在 `local/Logs/test-gates/20260923-005749-cooking-kitchen-loop` 与 `local/Logs/test-gates/20260923-005810-cooking-et-level-runtime`。失败条件、收益和可见顾客仍未做。

## 17. 2026-09-23 交接前收完询问增量

成功交接导出前，本关来过但还没开出订单的桌子会先开单。这张订单计入本关被清除的数量，下一关订单簿为空。伙伴已经开始洗的碗仍先洗完。

门禁：`cooking-kitchen-loop`（focused 78/78、Cooking 199/199、ET runtime 59/59）与 `cooking-et-level-runtime`（Cooking 199/199、ET runtime 59/59）均 exit 0。日志在 `local/Logs/test-gates/20260923-090136-cooking-kitchen-loop` 与 `local/Logs/test-gates/20260923-090154-cooking-et-level-runtime`。收益、评价和可见顾客仍未做。

## 18. 未完成范围

### Non-Unity successor backlog

P1–P6 尚可另行审议的工作包括两台物理 PC LAN 验收、正式 recipe/order/content/schema、Room/Match 产品语义、durable store、process-crash 恢复、批准 workload/threshold 和非 Unity 优化验证。原 UDP task 曾提供 LiteNetLib minimal wire/adapter、loopback 与 same-machine harness，但未替代两 PC 证据，也未解决完整 production transport 的认证、安全、重连或产品生命周期语义；该方向已于 2026-09-21 放弃，传输计划改用 KCP，属未启动、未批准、无时间表的后续工作。其两机执行入口见 [UDP two-PC LAN acceptance](udp-two-pc-lan-acceptance.md)（已退役，仅作历史参考）。其余工作均未启动、未批准、没有时间表，详见 [successor backlog](successor-backlog.md)。2026-09-21 单机厨房闭环、正式内容与 ET Level 闭环验收增量落地后，与现状衔接最直接的是 P2 余下的评分、收益、评价与小关结算契约，以及失败条件与失败重试（均属 owner 明确推迟项，需重新审议后立项）；Todo P0-C1 的成功进入下一小关已由 `09-22-cooking-level-success-handoff` 落地，失败重开已由 `09-22-cooking-level-fail-retry` 落地，成功结算确认已由 `09-22-cooking-level-settlement-confirmation` 落地。评分与收益、把确认应用到长期进度、可见顾客、伙伴成长、Profile/SaveSlot、真实断电恢复、connection→PlayerId 绑定与 ECS 清退仍未开始。已确认结算列表按代际落盘已由 `09-22-cooking-level-settlement-store` 落地，见第 11 节。装修换工位、解锁摆放、煮制加速和成功检查点已由 `09-22-cooking-level-progression` 落地，见第 12 节。前厅询问、洗碗和时间结构已由 `09-22-cooking-front-of-house` 落地，见第 13 节。用餐占桌已由 `09-22-cooking-dining-seat` 落地，见第 14 节。下一小关清空前厅已由 `09-22-cooking-front-house-reset` 落地，见第 15 节。失败重开丢掉前厅已由 `09-23-cooking-failed-front-house` 落地，见第 16 节。交接前收完询问已由 `09-23-cooking-handoff-inquiry` 落地，见第 17 节。上述均未批准，也未为此新建 task。

### Prohibited Unity scope

Cooking Unity 应用层、场景、authoring/export、projection、UI、动画、EditMode 与 scene smoke 长期禁止实施，不再作为旧 task、successor 或完整出口的当前 blocker。其历史来源、跨宿主 authority/identity/stale-input 不变量和重新授权条件见 [future scope](future-scope.md)。

## 19. 后续读取顺序

1. 读取本文确认三类状态。
2. 读取 [技术路线](technical-roadmap.md)、[交付计划](delivery-plan.md) 与 [Cooking spec index](../../../.trellis/spec/cooking/index.md)。
3. 需要历史证据时读取归档后的 task PRD/design/implement/check；不得因归档推导完整产品完成。
4. 只有 owner 明确批准新范围后才新建 Trellis task；不要恢复已归档 task。
5. Unity 重新授权必须满足 [future scope](future-scope.md) 的独立条件；non-Unity 后续从 [successor backlog](successor-backlog.md) 选择并重新审议。

## 20. 维护规则

- 本文只汇总状态与链接，不复制行为契约、测试矩阵或未来 checklist。
- 未实际运行的 Unity、LAN、protocol、durability 或 global gate 继续是 not-run/未完成，不能因 task archive 记为通过。
- `.trellis/migration/legacy-cooking-changes/` 保持只读。
