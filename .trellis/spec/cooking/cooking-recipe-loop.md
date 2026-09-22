# P2 一条完整配方：cooking-recipe-loop
## 2026-09-22 前厅询问、洗碗与小关时间修约（任务 09-22-cooking-front-of-house）

来源：09-19 已确认一位固定伙伴问完才开单、空闲才洗碗，营业走完再收尾，顾客离席后才成功。收益、评价、伙伴成长和失败条件仍不在本条。

| 位置 | 旧条款 | 新条款 | 来源 |
|---|---|---|---|
| 开单 | 测试直接调用 `OpenOrder` | 顾客先占空桌。伙伴按到店顺序询问配置的 tick，问完才开单。没有空桌不入座。收尾不再来人 | 09-19 询问 |
| 洗碗 | 测试直接调用 `CompleteWash` | 没有待询问桌子时，伙伴按脏碗顺序清洗配置的 tick，再调用 `CompleteWash` | 09-19 洗碗 |
| 离席 | 订单只有开放和完成 | 等到上限仍未送到则离席。已开的订单标为未满足，不写结算。未满足不是失败 | 09-19 超时离席 |
| 成功 | 调用方直接给成功结局 | 前厅驱动的成功要等营业结束、座位空、伙伴空闲。旧的直接 `BeginEnd` 仍可用 | 09-19 时间结构 |

### 实现状态声明

- 已实现并验证（单机纯 C#）：领域 F01–F05，宿主 F06。宿主只在运行帧之后推进一步，暂停帧不推进。
- 仍未实现：可见顾客、收益与评价、伙伴成长、失败条件。

## 2026-09-22 跨小关装修、道具、Buff 与成功检查点修约（任务 09-22-cooking-level-progression）

来源：09-19 已确认关与关之间的装修、解锁和煮制加速，以及只有成功收口才写检查点。本条只覆盖厨房里已经有对象的这四件事。前厅和 NPC 不在内。

| 位置 | 旧条款 | 新条款 | 来源 |
|---|---|---|---|
| 工位 | 成功交接原样保留工序所在工位 | `Created` 可以一次把旧工位换成新工位。没做完的工序改挂过去，已用 tick 和所需 tick 不变。未知工位或新工位已有工序则整次拒绝 | §5.3 |
| 解锁 | 标准供应只在开局或失败重开时摆放 | 准备态可以把一个已有定义再摆进延续厨房，实例号带 `unlock`，不重摆整间厨房 | 09-19 新道具 |
| Buff | 配方时长只来自内容 | 煮制加速只缩短之后新开始的番茄蛋花汤，至少剩 1 tick。正在煮的不改。重复选择不叠加 | 09-19 煮制加速 |
| 检查点 | 代际文件只存结算列表 | 大关检查点另存锁定的装修、解锁、Buff 和交接后的厨房现场。未锁定不能写。失败不写，坏文件拒绝 | task PRD R5 |

### 实现状态声明

- 已实现并验证（单机纯 C#）：领域 P01–P04，宿主 P04。检查点用同一测试进程里的新读取器。
- 仍未实现：前厅与 NPC、评分与收益、失败条件、真实断电恢复。

## 2026-09-22 小关结算落盘修约（任务 09-22-cooking-level-settlement-store）

来源：成功确认之后，这一代的结算列表还只活在内存里。本条只把这份列表按代际写到调用方给的目录。评分、收益和长期进度入账仍未实现。

| 位置 | 旧条款 | 新条款 | 来源 |
|---|---|---|---|
| 确认记录 | 确认只活在 `CookingLevelSettlementLedger` | 同一份确认可以写入目录。新的读取器按 Match、`LevelId`、`LevelEpoch` 读回同一列表。同一列表再写是重复；换一份列表则拒绝 | 实现契约：task design |
| 空列表 | 空列表可以确认 | 空列表落盘后仍是空列表，并且和「目录里没有这一代」不是同一种读取结果 | task PRD R1 |
| 坏记录 | 无文件契约 | 截断、篡改或未知格式版本结构化拒绝，不当成空确认，也不覆盖上一次完整记录 | task PRD R2 |
| 写入失败 | 内存确认一旦记下就留着 | 这一代原先没有确认时，文件没写上就撤回这一次内存登记。原先已经确认过的不撤回 | task design |
| 长期进度 | 不调用 `Apply` | 不变。落盘不改货币、解锁、升级或经营进度 | 奖励公式未定 |

### 实现状态声明

- 已实现并验证（单机纯 C#）：领域 D01/D02/D03，宿主 D04。验收用同一测试进程里的新读取器，不另开操作系统进程。
- 仍未实现：评分与收益、把确认应用到长期进度、Profile/SaveSlot、真实断电或杀进程恢复。

## 2026-09-22 小关成功结算确认修约（任务 09-22-cooking-level-settlement-confirmation）

来源：`Docs/Todo.md` P0-C1「只在小关成功完成时应用 confirmed settlement」。本条只覆盖确认边界。评分、收益、评价和写盘仍未实现。

| 位置 | 旧条款 | 新条款 | 来源 |
|---|---|---|---|
| 成功结算 | 局内 `SettlementHistory` 在成功交接时被清空，没有单独的确认记录 | `Ended` + `Success` 且尚未交接时，按 Match、`LevelId`、`LevelEpoch` 确认一份结算列表。同一列表再确认是重复；换一份列表则拒绝 | 实现契约：task design |
| 失败与准备 | 失败重开丢掉现场 | 失败、未结束和准备态不能确认。交接后的空账不能覆盖确认前的列表 | §5.1、§5.2 |
| 长期进度 | `CookingProgressPersistence.Apply` 入账奖励 | 本条不调用它，也不填写货币、解锁或升级 | 奖励公式未定 |

### 实现状态声明

- 已实现并验证（单机纯 C#）：领域 S01/S02，宿主 S03。确认只活在内存账本里。
- 仍未实现：评分与收益、把确认应用到长期进度、写盘、崩溃恢复。

## 2026-09-22 小关失败重开修约（任务 09-22-cooking-level-fail-retry）

来源：`Docs/Todo.md` P0-C1「落实失败重开」，产品语义基准为 `Docs/design/CookingGame/reference/product-lifetimes.md` §5.2。只覆盖已经 `Ended` + `Failed` 之后的厨房重建。失败条件、工位升级、写盘仍未实现。

| 位置 | 旧条款 | 新条款 | 来源 |
|---|---|---|---|
| 失败重开厨房 | `CreateRetry` 只换同一 `LevelId` 的更高 epoch；下一次 `Start` 由工厂新建空厨房 | 失败 `Ended` 后丢掉失败现场。宿主安装下一代后，用工厂新建仿真并调用 `ApplyStandardInitialSupply`，再挂到下一代，停在 `Created`。物品与干净碗池和「新仿真 + 同一份标准供应」一致 | 实现契约：task design；§5.2 |
| Match | 待办曾写关闭失败 Match 并新建 MatchId | 不采用。失败重开不结束 Match，也不换 MatchId | §5.2，覆盖 `Docs/Todo.md` 旧句 |
| 准备态 | 成功交接后 `Created` 只读 | 失败重开同样停在 `Created`，不提供改厨房入口。`Start` 绑定这份标准供应厨房，不接回失败现场，也不再新建空仿真 | 实现契约：task PRD R4 |
| 同代际恢复与成功交接 | 恢复整册换入；成功交接保留现场 | 不变。失败重开既不走 `RestoreCheckpoint`，也不走 `ExportSuccessHandoff` | 既有契约 |

### 实现状态声明

- 已实现并验证（单机纯 C#）：宿主 F01/F02。失败换代丢掉现场、按标准供应重建、停在 `Created`，随后 `Prepare` + `Start` 绑定同一份厨房。`HostFrameSequence` 不回退，重开不写盘。
- 仍未实现：失败条件、失败界面、装修/道具/Buff、工位升级迁移、写盘、评分、前厅。

## 2026-09-22 小关成功交接修约（任务 09-22-cooking-level-success-handoff）

来源：`Docs/Todo.md` P0-C1「落实成功进入下一小关」，产品语义基准为 `Docs/design/CookingGame/reference/product-lifetimes.md` §5.1。只覆盖成功收口后的厨房保留与本关上下文清除。失败重开、工位升级、durable storage 仍未实现。

| 位置 | 旧条款 | 新条款 | 来源 |
|---|---|---|---|
| 跨小关厨房 | `CreateSuccessor` 只换 Level 身份；下一次 `Start` 由工厂新建空厨房 | 成功 `Ended` 后，`ExportSuccessHandoff` 保留物品、加工、容器、脏/净碗、消耗产物账、下一 Process 与下一产物 ID；清除订单、结算、去重、两类事件；逻辑 Tick、事件序号、状态版本、下一结算序号与 Level 绑定归零。宿主把这份厨房换入下一代并停在 `Created` | 实现契约：task design；§5.1 |
| 准备态 | 无厨房只读条款 | `Created` / `Preparing` 不提供改厨房入口。延续状态只能在下一代 `Running` 后由权威命令修改 | 实现契约：task PRD R4 |
| 同代际恢复 | 恢复整册换入 | 不变。恢复 checkpoint 不是交接载荷；`AcceptSuccessHandoff` 拒绝仍带订单、Tick 或 Level 绑定的载荷 | 既有恢复契约 |

### 实现状态声明

- 已实现并验证（单机纯 C#）：领域 H01/H03，宿主 H02/H03。成功换代保留现场、清除本关上下文、停在 `Created`，随后 `Prepare` + `Start` 绑定同一份厨房。
- 失败重开的标准供应重建已由 `09-22-cooking-level-fail-retry` 落地。仍未实现：失败条件、工位升级迁移、写盘、评分、装修/道具/Buff、前厅。

## 2026-09-22 恢复 checkpoint 契约修约（任务 09-22-cooking-checkpoint-recovery）

来源：`Docs/Todo.md` P0-C1 前两条未勾项（“区分同步 snapshot 与恢复 checkpoint”与“导出 checkpoint -> 销毁 host -> 重建 -> 继续运行”等价验收），产品语义基准为 `Docs/design/CookingGame/reference/product-lifetimes.md` §4.1（HostFrameSequence 单调不 reset、Level 身份含 MatchId/RestaurantRuntime/LevelId/LevelEpoch），经 Trellis task `09-22-cooking-checkpoint-recovery` 实现并验证。验证证据见 task `check.jsonl`、`research/verification-2026-09-22.md` 与 `artifacts/cooking-checkpoint-recovery/`。durable storage、跨小关 checkpoint 产品语义、失败条件、前厅、传输与 Unity 仍范围外。

| 位置 | 旧条款（原文可定位） | 新条款 | 来源 |
|---|---|---|---|
| Requirement「权威状态必须可序列化并可重放」（同步快照语义） | 运行态唯一出口是 `CookingRecipeSimulation.Snapshot()` 同步投影；快照过滤墓碑、不含去重账本、事件/tick 历史、ID 计数器、干净池计数与消耗产物账 | 显式区分两条出口：**同步快照**（每帧投影，服务远端对齐，过滤墓碑）与**恢复 checkpoint**（`CookingRecipeCheckpoint`，覆盖物品/tombstone、活动加工、容器有序内容、订单、结算、消耗产物账、干净池计数、去重账本、事件/tick 历史与 event sequence、三个 ID 计数器、state version/logical tick 与 scope）；可派生索引（持物、station/anchor 进程索引、锁输入反查）由载荷重建，不进信封 | 实现契约：Todo P0-C1 覆盖表逐项；task design §2（存什么与为什么不存） |
| Requirement「加工进度与产物必须由模拟逻辑驱动」（恢复正确性） | 无（新增条款） | `CookingRecipeSimulation.RestoreCheckpoint` 整册换入载荷（fresh 构造期状态被完全替换，原子赋值提交）；恢复校验全部结构化（scope、物品定义、recipe/工位/容器外键、墓碑与锁输入一致、计数器与结算序列单调、干净池上下限、命令事件与 tick 事件序号互不重复且各自严格递增、tick 历史与逻辑 tick/宿主帧首尾相接），失败零变更；容器内容顺序进入 canonical（空槽名按 `SlotId` 占用集分配，但按列表顺序遍历的路径会随分叉）；恢复后既有 `ValidateProcessIndexesForFixedTick`/`ValidateProcessForFixedTick` 在下一 tick 兜底复核 | 实现契约：task design §6；既有腐败检测器复用，不为恢复新造第二套校验语义 |
| Requirement「同步与持久化边界」（宿主级信封） | 恢复只在领域层存在概念；宿主 watermarks（HostFrameSequence、命令水位）无跨宿主契约 | 宿主级 `CookingLevelCheckpoint` 携带 level scope/epoch、config identity、preparation、lifecycle 状态/version、HostFrameSequence、LastCommittedSimulationBatch 与整册仿真载荷；`CookingLevelCheckpointCodec`（格式版本 + 完整性 + 结构化读回，形态对照 P5 envelope）使 checkpoint 可脱离宿主自包含存在；`CookingLevelEtHost.ExportCheckpoint` 前置 `Running` 且 pending 为空，`Restore` 销毁后按同一代际重建并继续。载荷在第一次 fixed tick 后绑定 `CookingLevelScope`（含 epoch）；宿主恢复要求该绑定存在且等于信封 scope，只改 epoch 或尚未绑定的载荷结构化拒绝（`TickHistoryScopeMismatch`），不得建成另一代宿主 | 实现契约：`reference/product-lifetimes.md` §4.1（HostFrameSequence 单调不 reset、Level-local 水位按代际恢复）；task design §4–§5 |
| Requirement「同步与持久化边界」（恢复等价口径） | 无（新增条款） | “导出 → 销毁 host → 重建 → 继续运行”与不中断基线终态不可区分：canonical/Sha256、state version、logical tick、下一产物 ID、结算次数与两份终态 checkpoint canonical 全部相等（R01 两臂 evidence 逐位一致）。口径差异显式记录：宿主终态簿记不进 checkpoint——基线臂原批量重投给 `Duplicate` disposition，恢复臂同命令在 admission 判 `BatchStale`（命令水位已恢复），两臂均不二次推进；去重指纹覆盖整条命令（含 `SimulationBatch`），同 identity 换批量重投判 `CommandIdentityConflict`（既有契约） | 实现契约：task design §7 口径决定；证据 `artifacts/cooking-checkpoint-recovery/R01-*` |

### 实现状态声明

- 已实现并验证（单机纯 C#）：`CookingRecipeCheckpoint` 与仿真导出/整册恢复（结构化拒绝、零变更）、`CookingLevelCheckpoint` 与 codec（信封/完整性/截断与篡改拒绝）、宿主导出（Running + pending 空前置）与静态恢复入口（同代际重建、水位续接、失败释放宿主）、域内 C01–C04 与宿主级 R01–R04、变异测试 5 项全部杀死、evidence 经独立脚本复验（两臂逐位一致）。证据见 task `check.jsonl` 与 `artifacts/cooking-checkpoint-recovery/`。
- 仍未实现（范围外）：durable store 与进程崩溃恢复、跨小关成功/失败/升级规则与 checkpoint 产品语义（保存什么/清除什么/加载流程）、Paused 代际导出、lifecycle 事件历史恢复、评分/收益/评价、失败条件、前厅与订单生成节奏、生产传输、真实 LAN、Unity 一切范围、ET Phase B 权威迁移。
- 推定项（无单独 owner 裁决原文，实现按 task design 执行）：checkpoint 记录字段集与 canonical 结构、envelope 体积上限（1M 字符）、导出前置仅 Running、宿主终态簿记不恢复的口径、`AdoptRecoveredVersion` 只推进版本计数、恢复入口以静态工厂形态落在 ET 宿主、测试 trait 归属（域内 `CookingKitchenLoop`、宿主级 `CookingLevelRuntime`）。

## 2026-09-21 正式内容与 order owner 修约（任务 09-21-cooking-formal-content-and-orders）

来源：owner 逐轮决定（09-19 notes §10–§12）与任务①②落地契约，经 Trellis task `09-21-cooking-formal-content-and-orders` 实现并验证。本次修约把 successor backlog P2 的“正式 Recipe/Process/Appliance/Container/Order 内容与 timing”与“order owner”从“未启动”改为“已在单机纯 C# 范围实现并验证”，验证证据见 task `check.jsonl`、`research/verification-2026-09-21.md` 与 `artifacts/cooking-formal-content/`。评分/收益/评价（小关结算）、失败条件与前厅订单生成节奏仍范围外；正式 schema 推广与 Level/Map 对内容的引用属 P3 successor。

| 位置 | 旧条款（原文可定位） | 新条款 | 来源 |
|---|---|---|---|
| Requirement「正式配方与订单语义必须经过决策门」 | 正式 Recipe/Process/Appliance/Container/Order 内容与 timing 未启动，测试 fixture 内联手写 items/appliances/recipes 字典 | 正式内容以数据文档 `cooking-definition-v2` 唯一存在（`src/AbilityKit.Game.Cooking/Content/cooking-content-v2.json`），经 `CookingContentCatalog` 加载并过 v2 校验；加工时长等数值只存在于内容文档；烤面包按 owner 采纳版本“面包片（开局供应）→ 烤面包”，占位 `dough -> bread-slice` 退役 | owner 决定：notes 12.1 第十轮（采纳烤面包）；notes 11.1 第六轮（数值归属配置）＋successor backlog P2 首条（经 owner 批准开工） |
| Requirement「装盘与提交订单是独立的权威原子步骤」 | 订单“要求”由注入端口 `ICookingOrderPort` 判定，领域只维护已完成订单集合 | order owner 移入领域：仿真拥有订单簿（模板、要求、Open/Completed），开单是前厅注入入口（`OpenOrder`，与 `CompleteWash` 同模式，不产生命令事件）；`ICookingOrderPort`/`CookingOrderSubmission`/`CookingOrderAcceptance` 退役；提交校验（订单存在且 Open、产物 recipe 与容器物品定义匹配要求）全部在领域内执行 | owner 决定：notes 11.1 第四轮（NPC 询问生成订单进订单簿）＋successor backlog P2“明确 order owner”（经 owner 批准开工；评分/失败处理/产品 UX 按 owner 推迟另行立项） |
| Requirement「装盘与提交订单是独立的权威原子步骤」 | 提交成功只更新已完成订单集合 | 提交成功原子地消耗产物、订单转 Completed（一次）、追加一条结算记录 `CookingOrderSettlement`（sequence/order/template/recipe/product/player/container/logicalTick，不含评分字段）；订单簿与结算记录进入快照与 canonical；同 command identity 重放返回缓存结果，跨 identity 不得二次变更 | 实现契约：successor backlog P2“维持装盘、提交、幂等和原子失败不变量”；评分字段缺席是 owner 推迟项，不是遗漏 |
| Requirement「配置加载必须在提交前完成一致性验证」（P3 契约交叉） | 工位必须声明至少一个能力；任何 recipe 要求的能力必须有工位声明 | 两处显式放宽：(a) 工位可声明零能力（台面是放置/中转面，空白能力字符串仍拒）；(b) `RequiresStation == false` 的配方豁免 `CapabilityUnavailable`（免工位加工的能力只是标识），supported 能力检查保留 | 实现契约：任务 design §2（正式内容要求 counter-a 无能力、打蛋免工位且无工位声明 beat） |

### 实现状态声明

- 已实现并验证（单机纯 C#）：正式内容目录与加载器（含 v2 校验扩展：订单模板外键、初始供应校验、两处规则放宽）、订单簿与开单注入、提交/结算契约（五个结构化拒绝分支 + 幂等 + 结算记录）、闭环 fixture 改从正式内容运行、烤面包配方修正、快照/canonical 订单簿与结算可观察。证据见 task `check.jsonl` 与 `artifacts/cooking-formal-content/`。
- 仍未实现（范围外）：评分/收益/评价与小关结算、失败条件与失败重试、前厅顾客/NPC 过程与订单生成节奏、过度加工与烧焦、跨小关装修/道具/Buff、检查点、Level/Map schema 对内容的正式引用（P3）、生产传输、真实 LAN、Unity 一切范围。
- 推定项（无单独 owner 裁决原文，实现按 task design §9 执行）：供应 location 语法与实例 ID 生成规则、订单模板只声明“要求 recipe + 要求容器定义”、开单/提交新增 reason 的命名、结算记录字段集、内容文档位置与随程序集输出、`AcceptedOrders` 保留为 Completed 订单 ID 列表、两处 v2 校验放宽。

## 2026-09-21 ET Level 宿主闭环验收修约（任务 09-21-cooking-et-closed-loop-acceptance）

来源：`Docs/Todo.md` P0-C1 未勾项“让 ET fixed-tick host 承载同一条番茄蛋花汤闭环验收”与 owner“继续下一个任务”的指示，经 Trellis task `09-21-cooking-et-closed-loop-acceptance` 实现并验证。本次修约把“闭环只在领域仿真层运行、ET 侧仍是最小 fixture”改为“ET fixed-tick Level 宿主（`CookingLevelEtHost`）承载同一 fixture 的全链路闭环验收”，验证证据见 task `check.jsonl`、`research/verification-2026-09-21.md` 与 `artifacts/cooking-et-closed-loop/`。评分/失败条件/前厅/传输/Unity 仍范围外。

| 位置 | 旧条款（原文可定位） | 新条款 | 来源 |
|---|---|---|---|
| Requirement「加工进度与产物必须由模拟逻辑驱动」（宿主边界） | 宿主侧时间推进未被闭环覆盖；`AdvanceTicks` 与 fixed tick 的关系只在最小 fixture 中断言 | 宿主拥有时钟：玩家命令经 `TryEnqueue` + `Tick()` 每帧执行一个最小批次再推进一次 fixed tick，`AdvanceTicks` 在 admission 即被 `ReservedClockOperation` 拒绝；加工完成只由宿主帧驱动（切 2、打蛋 2、煮 6 个纯时钟帧）。闭环验收显式钉住该边界 | 实现契约：既有 `CookingLevelEtHost` admission 与 fixed-tick 行为（09-17 task 落地），本次补闭环级验收 |
| Requirement「装盘与提交订单是独立的权威原子步骤」（注入缝隙） | 开单/清洗完成是领域直接调用，宿主拥有仿真后是否仍可调用未在闭环中验证 | 帧间注入口径确认：`OpenOrder`（前厅）与 `CompleteWash`（NPC）不走命令路径、不受 authority gate 约束，在宿主 Running 期间帧间直接调用；提交经命令路径执行并触发洗碗端口。闭环验收按此口径执行 | 实现契约：正式内容 task 的“注入缝隙对称”设计，本次在宿主侧验证 |
| Requirement「加工进度与产物必须由模拟逻辑驱动」（计数器口径） | 无（新增口径） | 宿主闭环与领域闭环的 canonical 不要求字节一致：领域用 `AdvanceTicks` 一次完成切番茄、显式帧 1–8（终态 `LogicalTick=8`）；宿主每帧一个 tick（17 命令帧 + 10 时钟帧，终态 `LogicalTick=27`）。“同一条闭环”指同一 fixture、同一动作序列语义、同一结果契约，加 ET 侧自身确定性重放；计数器差异不是回归 | 实现契约：task design §6（宿主拥有时钟的必然结果），显式记录以防误判 |
| Requirement「取料与加工必须复用权威物品交互边界」（拒绝零变更） | 拒绝“零变更”在领域层以整段 canonical 前后一致证明 | 宿主层两级证明：(a) 命令级——拒绝结果 `StateVersion` 等于执行前版本且无事件；(b) 帧级——被拒绝的命令帧与同序列纯时钟帧（对照臂）到达同一 canonical（每帧固有的 tick 推进由对照臂抵消） | 实现契约：task design §5；领域 L03 语义在宿主命令路径的对应 |

### 实现状态声明

- 已实现并验证（单机纯 C#）：`CookingContent` 暴露加载时经 v2 校验的快照（Level 生命周期与 preparation 身份同一来源）；`CookingLevelClosedLoopTests`（E01 全链路 17 命令 + 10 时钟帧、E02 拒绝零变更对照臂、E03 两遍 canonical/Sha256 一致）；宿主失败路径的释放保证（`RunLoop`/`CreateStartedHost` 异常时释放进程级单例宿主）。证据见 task `check.jsonl` 与 `artifacts/cooking-et-closed-loop/`。
- 仍未实现（范围外）：评分/收益/评价与小关结算、失败条件与失败重试、前厅与订单生成节奏、过度加工与烧焦、跨小关规则、检查点与 snapshot/checkpoint 分离、生产传输、真实 LAN、Unity 一切范围、ET Phase B 权威迁移。
- 推定项（无单独 owner 裁决原文，实现按 task design 执行）：ET 侧 evidence 的 `fixtureId` 取 `et-level-closed-loop`、E03 不落证据（纯确定性比较）、变异测试选项、宿主失败路径释放的测试卫生规则。

## 2026-09-21 仿真落地修约（任务 09-21-cooking-kitchen-loop-simulation）

来源：owner 逐轮决定（09-19 notes §11.1 第一至九轮）与任务①修约契约，经实现落地为可验证行为。本次修约把上一轮“属后续任务”的容器即物品、七项动作与闭环 fixture 从“未实现”改为“已在单机纯 C# 范围实现并验证”，验证证据见 task `check.jsonl` 与 `artifacts/cooking-kitchen-loop-domain/`。前厅（顾客、NPC 询问过程、订单生成节奏、用餐离席）、小关时间结构、失败条件仍范围外。

| 位置 | 旧条款（原文可定位） | 新条款 | 来源 |
|---|---|---|---|
| Requirement「装盘与提交订单是独立的权威原子步骤」 | “将完成产物装入合法容器/槽位” 的 `Plate` 操作（容器 ID 直传） | `Plate` 退役，由七项动作中的“放入”（`PutIn`，手持物品进容器槽）与“倒出”（`Pour`，容器间转移或从已完成容器生成成品）取代；提交的产物必须在容器槽中，容器按当前位置判定可达 | owner 决定：notes 11.1 第六轮（七项动作：拾取、放下、放入、取出、启动加工、倒出、提交）、第九轮（蛋液倒进锅后碗即空出） |
| Requirement「取料与加工必须复用权威物品交互边界」 | 未规定活动进程输入的移动限制 | 启动加工即锁定输入：放下、放入、取出、倒出拒绝属于活动进程输入的物品；拾取仅放行活动进程的容器锚点（端走继续） | owner 决定：notes 11.1 第三轮（启动煮制后不可取消）＋第七轮（可以把锅从灶台端走、加工继续并保留进度） |
| Requirement「加工进度与产物必须由模拟逻辑驱动」 | 完成时“输入必须停在工位”的强约束（`ValidateProcessForFixedTick`） | 输入位置白名单：世界、工位、容器槽可，手持不可；加“输入未被移除且仍属该进程”。容器锚定加工的内容物必须仍留在该容器槽中。白名单保持腐败检测器可达（测试注入证明） | owner 决定：notes 11.1 第七轮（端走继续）＋实现契约（plan 任务②“保留它作为腐败检测器，不能退化成永不触发”） |
| Requirement「加工进度与产物必须由模拟逻辑驱动」 | 完成形态只在配置层存在 | 两种完成形态都经 `FixedTickPlan` 原子提交：`ConsumeInputs` 消耗输入并在工位或容器上生成输出（打蛋的蛋液落碗）；`RetainInputs` 保留输入、容器切“已完成”，成品在倒出时才生成；倒出至多生成一次产物（重复倒出拒绝） | owner 决定：notes 11.1 第二/三/八轮 |
| Requirement「取料与加工必须复用权威物品交互边界」 | 未规定工位绑定 | 工位绑定按加工定义声明（`RequiresStation`）：切→砧板、煮→灶台、烤→烤箱、打蛋→免工位；免工位加工不得携带工位，需工位加工缺工位即结构化拒绝 | owner 决定：notes 11.1 第三/七/八轮（三种加工工位要求并存） |
| Requirement「多输入下达与匹配」 | 匹配在单物品命令上表达 | 容器锚定加工按容器内容集合匹配；水是默认供应（`DefaultInputs`）不占物品不占容量；显式 Recipe 与自动识别都要求输入集合齐全 | owner 决定：notes 11.1 第一/七轮 |
| Requirement「争抢仲裁」（P0 交互基础延伸） | recipe 命令路径无批次仲裁 | 封闭批次按 (LogicalTick, 玩家 ID, 命令 ID) 稳定排序；乱序抵达与重复投放结果一致；同一 command identity 重放返回缓存结果 | 实现契约：plan 任务②“多人争抢仲裁按 Tick、玩家 ID、命令序号稳定排序，规则先落地” |
| Requirement「装盘与提交订单」 | 订单端口无“要求”概念，碗无脏/净与池 | 订单端口按要求（recipe identity）接受/拒绝，提交成功回报订单完成一次；可洗碗定义提交后变脏并交 NPC 端口，注入“清洗完成”后回池且不超过配置上限 | owner 决定：notes 11.1 第二/六轮（碗脏由 NPC 清洗归还、干净碗总量由配置定义）＋plan“NPC 清洗做成注入端口” |

### 实现状态声明

- 已实现并验证（单机纯 C#）：容器即物品（`CookingContainerDefinition` 与 fixture 独立容器表已退役）、七项动作、放入即拒绝、锁输入、端走继续、两种完成形态、统一产品 ID allocator、番茄蛋花汤闭环 fixture（L01 端到端 + L02 确定性重放）、订单要求、碗池、批次争抢仲裁。
- 仍未实现（范围外）：前厅顾客/NPC 过程、订单生成节奏、小关时间结构与成功条件、失败条件、Unity 一切范围。
- 推定项（无单独 owner 裁决原文，实现按 task design §9 执行）：Pour 的源/目标用 command.Item/Container 表达、锁定拒绝复用 `ItemStale`、免工位进程按锚点物品建索引、`RequiresStation` 布尔字段、提交后容器直接移除出厨房、仲裁键 (LogicalTick, Player, CommandId)、`OrderCompleted` 透传、`CompleteWash` 返回结果记录、普通容器间 Pour 转移全部内容物、canonical 增加 `RequiresStation` 但保持 v2 身份字符串。

## 2026-09-21 契约修约

来源：owner 在 `.trellis/tasks/09-19-cooking-gameplay-business-discussion/` 的逐轮决定（notes 11.1 第二/三轮），经 owner 批准由 Trellis task `09-21-cooking-kitchen-loop-contracts` 的 `design.md` 落地为契约。本次修约不解除 2026-09-16 收口状态中"未完成的非 Unity 范围不得写成已实现"的约束，也不代表容器即物品、七项动作或闭环 fixture 已实现——那些属后续任务。

| 位置 | 旧条款（原文可定位） | 新条款 | 来源 |
|---|---|---|---|
| Requirement「加工进度与产物必须由模拟逻辑驱动」 | “在完成条件满足时一次性消耗声明的输入、创建声明的产物” | 按加工定义的完成形态原子提交：`ConsumeInputs` 消耗输入并生成输出；`RetainInputs` 保留输入、容器切“已完成”，成品在倒出时才生成 | owner 决定：09-19 notes 11.1 第二/三轮（锅保留输入、倒出时才生成成品；单步预处理类加工消耗输入生成输出；两种形态并存） |
| Scenario「重复完成命令」 | 幂等只覆盖“工序完成”一次 | 幂等延伸到倒出：倒出至多生成一次产物 | owner 决定 + 经 owner 批准本 design 生效的实现契约 |
| Requirement「加工进度与产物必须由模拟逻辑驱动」 | 未规定多输入如何下达与匹配 | 多输入靠多条“放入”把输入逐个累积进容器，一次“启动”按容器内容集合匹配配方；输入顺序不重要，匹配按 DefinitionId 排序后比较以保证确定性与可重放 | owner 决定：notes 11.1 第一/七轮（多条放入加一次启动、配方自动识别、顺序不重要） |

“同一输入集合 + 同一工位能力必须唯一匹配一个配方”是**实现契约而非 owner 裁决**：它由“配方自动识别必须可判定”推导，命中多个配方返回结构化拒绝 `RecipeAmbiguous`，不是运行时异常。

## 2026-09-16 收口状态

- R01-R06 纯 .NET fixture loop 已验证并作为 limited delivery 收口。2026-09-21 三次增量（厨房闭环仿真、正式内容与 order owner、ET Level 宿主闭环验收）已分别落地并验证，见本文件头部三则修约；评分/收益/评价、失败条件、前厅与真实 LAN 仍未启动，见 successor backlog。
- 对应 `09-15-cooking-*` task 已按 `completed-limited-scope` 语义归档；`completed` 不表示完整 P2 或完整 P0-P6 产品出口。
- Cooking Unity package、asmdef、scene、authoring、projection、UI、EditMode 与 scene smoke 长期禁止实施；原 Unity 场景及宿主无关不变量统一见 [`future-scope.md`](../../../Docs/design/CookingGame/future-scope.md)。
- 本文以下 authority、identity、atomicity、sequence、stale-input、persistence 或 measurement 行为不变量继续有效；未完成的非 Unity 范围不得写成已实现，P1-P6 入口见 [`successor-backlog.md`](../../../Docs/design/CookingGame/successor-backlog.md)。

> 交付状态：**completed-limited-scope**；原完整能力迁移状态为 `blocked`。本规范由只读来源快照 `.trellis/migration/legacy-cooking-changes/add-cooking-recipe-loop/specs/cooking-recipe-loop/spec.md` 转换；原始 SHA-256 见 [迁移清单](../../migration/legacy-cooking-changes/manifest.json)。
>
> 当前已实现并验证：R01-R06 单输入/单工序/3 Tick fixture；2026-09-21 厨房闭环仿真（容器即物品、七项动作、两种完成形态、订单要求与碗池、批次争抢仲裁）、正式内容及 order owner（数据驱动内容目录、订单簿、提交/结算契约）与 ET Level 宿主闭环验收（正式内容全链路经宿主命令路径与固定 Tick 驱动、拒绝零变更两级证明、确定性重放）。正式 score、失败处理、前厅订单生成节奏与真实 LAN R07 是 successor backlog 中未启动、未批准的 non-Unity 范围；Unity 是 prohibited/not-run future scope。

## 当前边界

- 已验证：R01-R06 pure .NET fixture contract；实际命令和 JSONL evidence 见 P2 task `check.jsonl`。
- Fixture：单 input、单 process、3 logical ticks、injected accept/reject order port；不代表正式 content 或产品 timing。
- 联机 R07、正式 content/order/settlement/score/failure UX、production transport、two-PC LAN、benchmark 与完整 P2 exit：未完成并进入 successor backlog；Unity 未运行并进入 future scope。

## 迁移边界

- 依赖：P0；联机验收时还依赖 P1 的稳定 session。
- 阻塞：评分、失败处理与前厅订单生成节奏均待 owner 确认（正式配方与订单 owner 已于 2026-09-21 由 task `09-21-cooking-formal-content-and-orders` 落地，ET Level 宿主闭环验收已于同日由 task `09-21-cooking-et-closed-loop-acceptance` 落地，见头部修约）。
- 验证状态：R01-R06 与 2026-09-21 三次增量已实现并通过 focused pure .NET tests（证据见对应归档 task）；R07 two-PC LAN、正式 score/失败处理/前厅节奏属于 successor backlog，未启动、未批准、未执行。

## 迁移的行为草案

## Purpose

为做菜经营游戏提供一条可由纯 C# 权威模拟验证的最小数据驱动闭环，覆盖取料、加工、装盘与提交订单，同时把未决的正式内容和 owner 决策明确隔离。

## ADDED Requirements

### Requirement: 取料与加工必须复用权威物品交互边界
系统 SHALL 允许玩家从合法来源取得配方所需食材，并仅在身份、当前位置、资格、范围、容量和生命周期检查全部通过时创建加工动作；失败 MUST 保持权威状态不变。

#### Scenario: 合法取料并开始加工
- **WHEN** 玩家在有效 Match 中取得满足配方输入的食材，并向具备所需能力的厨具提交加工命令
- **THEN** 系统 SHALL 原子地锁定输入、创建加工进度，并记录可观察的 recipe/process identity；不得产生重复位置或所有权

#### Scenario: 取料或厨具资格失败
- **WHEN** 食材缺失、玩家无资格、距离不可达、厨具能力不匹配或输入已被占用
- **THEN** 系统 SHALL 返回稳定失败原因，且加工进度、输入位置、厨具占用和事件序列均保持不变

### Requirement: 加工进度与产物必须由模拟逻辑驱动
系统 SHALL 使用明确的模拟 Tick 或逻辑时钟推进已接受的工序，并按加工定义的完成形态原子提交结果：完成形态为 `ConsumeInputs` 时消耗声明的输入并在工位或容器上生成输出；完成形态为 `RetainInputs` 时保留输入、把容器切换为“已完成”状态，成品在倒出时才生成；两种形态由加工定义声明，不由运行时推断。完成 MUST NOT 依赖 Unity 动画回调或墙钟作为完成依据。

#### Scenario: 工序按逻辑时间完成
- **WHEN** 已开始工序经过不足完成阈值的 Tick，再经过达到阈值的 Tick
- **THEN** 系统 SHALL 在前者保持进行中，在后者只提交一次完成结果，并使产物身份、数量与配方定义一致

#### Scenario: 重复完成命令与重复倒出
- **WHEN** 相同 command identity 或同一工序完成请求被重复提交，或同一“已完成”容器被重复倒出
- **THEN** 系统 SHALL 返回幂等结果，输入只消耗一次、产物只创建一次、事件只发布一次；倒出至多生成一次产物

### Requirement: 装盘与提交订单是独立的权威原子步骤
系统 SHALL 将完成产物装入合法容器/槽位作为独立的权威原子命令，并将已装盘且仍可交单的菜品提交给订单 owner 作为另一独立的权威原子命令。装盘成功后状态 MUST 保留并可被拿取或后续提交；订单校验失败只拒绝本次提交，不得回滚已装盘菜品或产生其他部分变更。

#### Scenario: 合法装盘并保留
- **WHEN** 完成产物、容器容量、装盘资格和当前位置均有效
- **THEN** 系统 SHALL 原子地完成装盘并发布一次装盘结果；菜品保留在容器/槽位中，可被拿取或作为后续订单提交物，订单状态不因装盘命令改变

#### Scenario: 装盘失败
- **WHEN** 容器已满、产物不适配、当前位置不符或装盘范围无效
- **THEN** 系统 SHALL 拒绝本次装盘，产物、容器、订单进度和事件保持不变

#### Scenario: 订单校验失败但装盘已成功
- **WHEN** 已装盘菜品提交时订单不可接受、菜品不匹配或提交范围无效
- **THEN** 系统 SHALL 只拒绝本次订单提交，已装盘菜品和容器状态保持不变，不得回滚装盘

#### Scenario: 合法订单提交
- **WHEN** 已装盘菜品、订单所需 recipe identity、提交资格和订单状态均有效
- **THEN** 系统 SHALL 原子地消费该提交物并更新订单，产生一次可关联的提交结果

#### Scenario: 提交命令重复与跨身份重放
- **WHEN** 相同 command identity 重复提交，或不同 command identity 再次提交已消费的菜品
- **THEN** 系统 SHALL 对相同 command identity 返回已处理结果且不产生二次变更；对不同 command identity 拒绝已消费菜品，且不得重复更新订单

### Requirement: 正式配方与订单语义必须经过决策门
系统 SHALL 将正式配方内容、订单 owner、评分/结算、计时边界和失败处理标记为 Draft / Blocked，未获 owner 确认前不得把路线图示例升级为产品承诺；测试 fixture MUST 明确仅用于验收。

#### Scenario: 决策未确认时创建闭环 fixture
- **WHEN** 实施者使用最小测试配方验证领域流程，但正式配方或订单 owner 尚未确认
- **THEN** 系统 SHALL 允许以 fixture 验证结构和原子性，但交付状态保持 Draft / Blocked，不得宣称正式内容或结算已接受

### Requirement: 联机验收必须依赖稳定阶段 2 会话
阶段 3 的纯 C# 领域闭环已按 limited scope 验证。若 successor 将来声称 host/client 联机一致性，则 MUST 新建 task，并依赖稳定 session、共享 command path、snapshot seam、LAN 集成证据及 D1-D4 决策。

#### Scenario: 纯 C# 闭环验收
- **WHEN** 阶段 1 证据齐全且执行最小 fixture 的取料、加工、装盘流程
- **THEN** 系统 SHALL 能验证最终状态、产物与失败原子性，而不要求真实 transport

#### Scenario: 联机闭环验收前置缺失
- **WHEN** 阶段 2 session 或 LAN 集成证据、transport/D4 决策门尚未满足
- **THEN** 系统 MUST 将该 successor 验收标记为未获批准/未执行，不得以同机或 in-process 结果替代真实 LAN 出口
