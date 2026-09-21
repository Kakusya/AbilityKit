# PRD：Cooking ET Level 闭环验收

> 状态：in_progress（owner 已批准开始本任务；文档随实现进度同步更新）。
> 上游契约：`.trellis/spec/cooking/`（任务①②与正式内容 task 已修约并落地）；待办来源：`Docs/Todo.md` P0-C1 未勾项"让 ET fixed-tick host 承载同一条番茄蛋花汤闭环验收"。
> 本任务只覆盖纯 C#/.NET 单机范围：ET fixed-tick Level 宿主及其测试。Unity 一切范围禁止；传输/联机不做。

## Goal

让 ET fixed-tick Level 宿主（`CookingLevelEtHost`）承载同一条番茄蛋花汤闭环验收：从正式内容目录（`cooking-definition-v2`，经 `CookingContentCatalog` 加载）构造 fixture 与标准初始供应，全部玩家动作经宿主命令 ingress（`TryEnqueue`）与固定 Tick（每帧一个最小命令批次 + 一次 fixed tick）驱动，前厅/NPC 注入（开单、清洗完成）走既有领域缝隙，最终订单完成一次、结算记录一条、碗回池；重放得到相同 canonical 状态。命令 wire 形状与 ET 二进制指纹金样不变。

## 1. 背景与问题

番茄蛋花汤闭环（含 2026-09-21 正式内容目录）目前只在领域仿真层运行并验证（`CookingKitchenLoopFixtureTests` L01–L05，直接 `Submit` + `AdvanceFixedTick`）。ET 侧的情况：

- **ET 侧仍是最小 fixture**：`CookingLevelEtHostTests` 与 `CookingVerticalSliceTests` 各自手写 raw→cooked 最小字典，只完成命令形状迁移与指纹金样维护，没有跑过正式内容的闭环 fixture；
- **宿主时钟与命令路径未被闭环覆盖**：宿主拥有固定 Tick（`AdvanceTicks` 在 admission 即被 `ReservedClockOperation` 拒绝），时间只能靠逐帧 `Tick()` 推进；闭环的三种加工时长（切 2、打蛋 2、煮 6）在宿主路径下如何逐帧完成、命令帧与时钟帧如何交替，尚无验收；
- **正式内容到 Level 生命周期还差接线**：Level 生命周期消费 `CookingConfigurationSnapshot` 与 `CookingLevelPreparation`（布局 + 配置身份），正式内容目录目前只暴露候选/身份，没有把校验后快照直接交给 Level 宿主的路径；
- **Todo P0-C1 该条目未勾**："当前闭环（含 2026-09-21 正式内容目录）只在领域仿真层运行并验证，ET 侧仍使用各自的最小 fixture……尚未跑通闭环 fixture"。

## 2. Requirements

- **R1 正式内容直达 ET 宿主**：ET 侧闭环验收从 `cooking-content-v2.json`（测试输出目录，已经项目引用流到 ET.Runtime.Tests）加载内容，经 `CookingContentCatalog.BuildFixture` 构造 fixture、`ApplyStandardInitialSupply` 落地标准初始供应；不再为闭环手写第二套 items/appliances/recipes 字典。
- **R2 内容快照暴露**：`CookingContent` 暴露加载时经 v2 校验得到的 `CookingConfigurationSnapshot`，使 Level 生命周期与 preparation 的配置身份来自同一份已验证内容（单一加载路径、单一身份），不为验收在测试侧重放候选。
- **R3 全链路走宿主命令路径**：闭环的七项动作（拾取、放下、放入、取出、启动加工、倒出、提交）全部经 `host.TryEnqueue(envelope)` + `host.Tick()` 执行；每帧先执行最小命令批次再推进一次 fixed tick；加工完成只由宿主帧驱动（`AdvanceTicks` 保持 admission 保留拒绝）。
- **R4 前厅/NPC 注入缝隙不变**：开单（`OpenOrder`）与清洗完成（`CompleteWash`）是领域注入入口、不是玩家命令，在帧间直接调用（与领域 L01 同一模式）；提交经命令路径执行并在成功后触发洗碗端口请求。
- **R5 验收断言与领域闭环对齐**：同一结果契约——切番茄在砧板生成产物、蛋液留碗、倒出腾碗、锅"已完成"可端走、倒出生成蛋花汤入碗、提交后订单 Completed 一次且结算记录一条、碗脏交 NPC 且离册、干净池计数下降、注入清洗后回池计数恢复；订单要求拒绝错误产物且零变更（领域 L03 的宿主侧对应）。
- **R6 确定性**：同一宿主闭环完整跑两遍，canonical 文本与 Sha256 一致；帧结构可观察（命令帧恰好一个 Executed disposition、纯时钟帧零 disposition、`HostFrameSequence == LogicalTick`）。
- **R7 wire 与指纹不变**：命令枚举、`IsWellFormed`、`CookingCommandFingerprint.CanonicalBytes` 不动；三个既有二进制指纹金样（pickup/start-process/put-in）字节不变，由既有测试守护；不新增 gate（两个既有 Cooking gate 均已全量运行 ET.Runtime.Tests）。
- **R8 evidence**：闭环每条命令落 `CookingRecipeAcceptanceEvidence`（before/after hash、outcome、reason、事件、runner 标注 `dotnet test AbilityKit.ET.Runtime.Tests`），目录由 `COOKING_RECIPE_EVIDENCE_DIRECTORY` 控制，落 `artifacts/cooking-et-closed-loop/`。

## 3. Acceptance Criteria

- **K01 内容直达**：ET 验收 fixture 的全部定义来自加载的正式内容（物品/工位/配方/订单模板/供应），测试内不出现第二套内容字典；配置身份与 preparation 一致，`Prepare/Start` 被接受。
- **K02 全链路**：E01 从标准初始供应出发经宿主走完取料→切番茄→入锅→打蛋→倒蛋液→煮制→端走→倒汤→开单→提交→洗碗回池；每个动作帧返回 Accepted 且恰好一个 Executed disposition；纯时钟帧零 disposition 且推进一个 logical tick。
- **K03 订单与结算**：提交成功后订单状态 Completed（快照可观察）、`SettlementHistory` 恰一条（字段完整：sequence/order/template/recipe/product/player/container/logicalTick）、洗碗端口恰一次请求、碗离册、干净碗计数 1；注入清洗后碗回干净池、计数恢复 2。
- **K04 完成形态与端走**：两种完成形态在宿主帧下正确落地（切=ConsumeInputs 在工位生成；打蛋=免工位于容器生成；煮=RetainInputs 切"已完成"）；锅在完成后可被拾取端走，进度保留在"已完成"状态上。
- **K05 拒绝零变更**：E02 以领域 L03 同构场景（烤面包入碗、蛋花汤订单）经宿主提交，disposition 记录 Rejected + `OrderRequirementMismatch`，提交前后 canonical 一致、无结算、碗中菜品不回滚。
- **K06 确定性**：E03 两遍完整宿主闭环的 canonical 文本与 Sha256 相等；证据记录条数与命令数一致。
- **K07 回归**：`cooking-et-level-runtime` 与 `cooking-kitchen-loop` 两个 P1 gate 全绿（含 Cooking 全量、ET runtime 全量与 focused）；三个二进制指纹金样字节不变；既有 ET 宿主测试不改写法全部通过。
- **K08 文档**：spec 记录"ET Level 宿主承载正式内容闭环"的契约与注入缝隙口径；`progress.md` 增加本节交付；`Docs/Todo.md` P0-C1 该条勾选并引用证据；不宣称评分/失败/前厅/传输/Unity 已完成。

## 4. 范围外（明确不做）

评分、收益、评价与小关结算（owner 推迟）；失败条件与失败重试（owner 推迟）；过度加工与烧焦产物；前厅一切范围（顾客、餐桌、NPC 询问过程、订单生成节奏——订单只能由前厅经 `OpenOrder` 注入）；跨小关装修/道具/Buff；检查点与 snapshot/checkpoint 分离（P0-C1 后续条）；UDP/KCP 等一切传输与网络内容；ET Phase B 权威迁移；ID 从 string 迁 long；Unity 一切范围。既有 ET 宿主机制测试（仲裁、故障注入、生命周期、指纹）只做回归，不扩写。

## 5. 来源冲突与显式审议

- **"同一条闭环"的口径**：领域 L01 用 `AdvanceTicks` 命令一次完成切番茄、用显式帧号 1–8 推进 fixed tick，结束于 `LogicalTick=8`；宿主拥有时钟后 `AdvanceTicks` 被保留拒绝，每帧只推进一个 tick，同一闭环结束于更高 `LogicalTick`（命令帧 + 时钟帧总数）。因此 ET 侧闭环的 canonical 文本与领域运行**不要求字节一致**（计数器必然不同），"同一条"指同一 fixture、同一动作序列语义、同一结果契约，外加 ET 侧自身的确定性重放。此口径显式记录，避免把计数器差异误判为回归。
- **Todo P0-C1 条目顺序**：该条目之后还有 checkpoint、跨小关成功/失败、工位迁移、settlement 落盘等未勾项，本任务只闭环验收这一条，不抢先实现后续条目。
- **successor backlog / 阶段计划** 均未把"ET 侧闭环验收"单列；本任务依据 Todo P0-C1 现行未勾项与 owner"继续下一个任务"的指示立项，不推导其他范围。

## 6. 约束与风险

- 命令记录（`CookingRecipeCommand`）与指纹 canonical 字节不动；若实现中发现必须改命令形状，必须先重锚金样并在 design.md 记录（预期不需要）。
- 产品侧改动限定为 `CookingContent` 增加快照暴露（加法、无行为变化）；领域仿真、Level 生命周期、两个 ET 宿主的既有行为不改。
- 批量编号必须严格递增（宿主对 `<= LastCommittedSimulationBatch` 的入队判 `BatchStale`）；期望版本必须逐命令从快照读取。
- 标准初始供应必须在 `Start` 之前应用到预建仿真（沿用既有 `CreateStartedHost(initialize)` 接缝）：`Start` 之后 `Submit`/`AdvanceFixedTick` 被门控拒绝，`AddItem` 虽仍放行但语义上属于关卡初始化。
- 验收 evidence 与 check.jsonl 必须记录真实命令与结果；未运行的门禁/环境不得记为通过。
