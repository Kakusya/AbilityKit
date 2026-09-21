# AbilityKit 项目待办事项

## 1. 文件定位

本文件记录 AbilityKit 工作区当前已知的项目级待办事项，汇总各模块尚未完成、需要继续审议或等待验证的工作，避免待办散落在阶段性文档中后无人跟进。

本文件只记录待办、优先级、完成条件和权威来源，不替代以下内容：

- 产品方向与范围决定：`ADR/long-term-goals.md`
- 架构决策：`ADR/decisions/`
- 框架与应用设计：`Docs/design/`
- 稳定工程规则与 Cooking 行为合同：`.trellis/spec/`
- 已启动工作的目标、设计、实施与检查证据：`.trellis/tasks/`

待办进入实施前仍需创建或恢复对应 Trellis task；勾选完成必须引用真实实现和验证证据，不能只依据路线文档或计划描述。

当前排序原则：

1. 先处理会影响主链路正确性、恢复能力、权威时钟和正式同步能力的事项。
2. 再处理会显著提升产品纵切完整性、大规模示例说服力和框架抽象稳定性的事项。
3. 最后处理文档校正、体验增强和结构拆分类事项。

## 2. 总体判断

当前存在两条需要重点推进的 P0 主线：

- **Cooking ET 应用运行时主线**：独立 ET runtime 和最小权威命令 Tick 接点已经完成，但正式 Match 宿主、单一加工时钟、checkpoint、跨小关规则与 ECS 清退尚未完成。原 UDP ingress 方向已于 2026-09-21 放弃（传输计划改用 KCP，属未启动后续工作），不再计入本主线待办。单机纯 .NET 厨房闭环仿真（容器即物品、七项权威动作、两种完成形态、订单要求与碗池、批次争抢仲裁）已于 2026-09-21 由两个 Trellis task 实现并验证，状态见 [Cooking 当前工程进度](design/CookingGame/progress.md) 第 4 节；正式 Recipe/Process/Appliance/Container/Order 内容与 timing（数据驱动 `cooking-definition-v2` 内容目录）与 order owner（订单簿、开单注入、提交/结算契约）已于同日由 task `09-21-cooking-formal-content-and-orders` 实现并验证，状态见同文第 5 节；同一条番茄蛋花汤闭环已在 ET fixed-tick Level 宿主上以正式内容跑通验收（命令 ingress + 固定 Tick、拒绝零变更、确定性重放），状态见同文第 6 节。 运行态恢复契约（同步 snapshot 与恢复 checkpoint 区分、覆盖表逐项入账、导出→销毁 host→重建→继续与不中断基线不可区分）已于 2026-09-22 落地，状态见同文第 7 节。评分/收益/评价与小关结算、失败条件与前厅订单生成节奏、固定伙伴最终人数、生产传输与 Unity 范围仍未启动。
- **Shooter 同步正式化主线**：已有同步主干和 pure-state runtime 起点，但仍缺客户端消费、AOI、delta/keyframe/resync 和远程服务端预算闭环。

MOBA、Client Flow、Presentation 与通用工程治理属于后续正式化主线，其中完整 build 阻塞和主流程失败语义仍应优先处理。

## 3. P0：必须优先处理

### P0-C1：完成 Cooking ET 应用运行时纵切

**当前基线**：独立 ET runtime 已从 Demo 中提炼，`CookingRecipeTickHost` 已通过真实 ET `UpdateSystem` 在显式 Tick 内调用 `CookingRecipeSimulation.Submit`。ET 当前只负责生命周期底座、owner-thread 约束和命令调度；Cooking 领域模拟仍是唯一权威状态 owner。对应提交为 `38d822271`，验证记录见 [归档任务](../.trellis/tasks/archive/2026-09/09-17-cooking-et-runtime-roadmap/research/validation.md)。

**待办**：

- [x] 在 `AbilityKit.Game.Cooking.EtRuntime` 建立 canonical Level Phase A host seam，编排 `CookingLevelLifecycle`、当前 simulation、ET Scene、命令 ingress 和 frame/disposition 输出；旧 `CookingMatchLifecycle` 仅保留 obsolete 兼容 facade。证据见 [当前 task](../.trellis/tasks/09-17-cooking-et-level-fixed-tick/research/validation.md)。
- [x] 明确最小 Match/RestaurantRuntime/Kitchen/Level ET 所有权结构；ET 只承载生命周期与 driver seam，不复制 Cooking gameplay 权威状态；精确 parent 由窄范围 Roslyn relation analyzer 与运行时树测试共同验证。
- [x] 建立单一固定加工时钟：每个 Running 权威帧全局 `LogicalTick` 只推进一次，批量稳定推进全部加工；Paused 停止 Tick/新命令，Resume 延续；fixed-step delta 原子提交。
- [x] 让 ET fixed-tick host 承载同一条番茄蛋花汤闭环验收：已于 2026-09-21 由 task `09-21-cooking-et-closed-loop-acceptance` 交付——fixture 与标准初始供应来自正式内容目录，17 条玩家命令全部经宿主命令 ingress 与固定 Tick 驱动（10 个纯时钟帧），开单/清洗完成走帧间注入，拒绝零变更以命令级版本相等 + 对照臂 canonical 相等两级证明，两遍重放 canonical/Sha256 一致；门禁 `cooking-et-level-runtime`（Cooking 175/175、ET runtime 43/43）与 `cooking-kitchen-loop`（focused 54/54）通过，证据见 [归档 task](../.trellis/tasks/archive/2026-09/09-21-cooking-et-closed-loop-acceptance/check.jsonl) 与 `artifacts/cooking-et-closed-loop/`。宿主与领域闭环计数器差异（终态 LogicalTick 27 vs 8）是显式口径；评分、失败条件、前厅、传输与 Unity 仍未启动。
- [x] 区分同步 snapshot 与恢复 checkpoint：已于 2026-09-22 由 task `09-22-cooking-checkpoint-recovery` 交付——同步快照是每帧投影（过滤墓碑），恢复 checkpoint（`CookingRecipeCheckpoint`）覆盖物品/tombstone、活动加工、容器有序内容、订单、逻辑 Tick、宿主命令水位、去重账本、事件/tick 历史与 event sequence、三个 ID 计数器、Match/epoch/config identity 与 lifecycle 状态，并经 `CookingLevelCheckpointCodec`（格式版本 + 完整性）序列化自包含存在；校验全部结构化且拒绝零变更。证据见 [归档 task](../.trellis/tasks/archive/2026-09/09-22-cooking-checkpoint-recovery/check.jsonl) 与 `artifacts/cooking-checkpoint-recovery/`。
- [x] 验证“导出 checkpoint -> 销毁 host -> 重建 -> 继续运行”与不中断基线不可区分：同一条番茄蛋花汤闭环在煮制进行中导出→销毁→按同一代际重建→继续，终态 canonical/Sha256、state version、logical tick、下一产物 ID、结算次数与两份终态 checkpoint canonical 全部相等，两臂 evidence 逐位一致；HostFrameSequence 单调不 reset；导出前置 Running 且 pending 为空，跨代际/跨配置/载荷投毒结构化拒绝。门禁 `cooking-et-level-runtime`（Cooking 179/179、ET runtime 47/47）与 `cooking-kitchen-loop`（focused 58/58）通过。证据同上。durable storage、跨小关 checkpoint 产品语义、失败条件、前厅、传输与 Unity 仍未启动。
- [ ] 落实成功进入下一小关：保留食材、半成品和未完成加工；清理本关订单；准备阶段暂停并在下一小关恢复。
- [ ] 落实失败重开：关闭失败 Match，创建新 MatchId/更高 epoch，清空失败现场，按关卡定义生成标准初始供应；失败和准备阶段修改不写盘。
- [ ] 落实工位升级迁移：使用显式领域操作迁移未完成加工并保留进度，不通过销毁父 Entity 隐式丢失 Process。
- [ ] 只在小关成功完成时应用 confirmed settlement；durable storage、进程崩溃恢复和磁盘原子性必须有真实实现与测试。
- [ ] ~~将 UDP 接入 ET owner-thread ingress~~：已于 2026-09-21 放弃（owner 决定传输改用 KCP），不在当前范围；若将来立项 KCP，必须重新审议回调解码/入队、ET Tick 取稳定批次与本地/远端同一权威入口的契约。
- [ ] 校验 connection 到 PlayerId 的绑定以及 scope、MatchId、epoch、config identity；重复、乱序、过期命令不得重复推进或重复提交订单。
- [ ] 在允许范围内补 Unity compile evidence；未获重新授权前不实施 Cooking Unity 应用层、场景、authoring、projection 或 UI。
- [ ] 只有消费者迁移、依赖清零、等价测试迁移和适用门禁通过后，才分批退役 `world.entitas` 与 `world.ecs`；不删除测试换取通过。

**架构约束**：

- 不在 ET Entity 与 `CookingRecipeSimulation` 中维护两份可独立修改的权威状态。
- ET EntityId 不作为网络身份或存档稳定身份。
- Cooking 规则、房间流程和网络权威策略不进入通用 `AbilityKit.ET.Runtime` 包。
- ~~`cooking-udp` 回归通过不等于 ET/UDP 已接合~~：该 gate 已随 UDP 方向放弃而退役；任何将来传输 gate 的通过同样不等于权威入口已接合或两 PC LAN 已通过。
- Unity、两 PC LAN、durable storage、checkpoint 恢复和 ECS 清退没有真实证据前不得宣称完成。

**权威来源**：

- [Cooking 技术路线](design/CookingGame/technical-roadmap.md)
- [Cooking 当前工程进度](design/CookingGame/progress.md)
- [Cooking spec index](../.trellis/spec/cooking/index.md)
- [ET/Cooking 路线归档验证记录](../.trellis/tasks/archive/2026-09/09-17-cooking-et-runtime-roadmap/research/validation.md)

### P0-S1：补齐纯状态同步客户端消费闭环

**问题**：Shooter pure-state exporter 已能输出 `FullBaseline` / `Delta` / `LowFrequency`，但客户端尚未形成正式 import、插值 buffer、延迟播放、低频补间和关键本地实体预测标记消费链。

**建议落地**：

1. 增加 pure-state 客户端接收/解码入口，与 packed snapshot controller 分离，避免把纯状态同步混入旧帧同步路径。
2. 建立 pure-state interpolation buffer，按 server frame / server ticks 做延迟播放。
3. 对 `LowFrequency` 实体使用补间/保持策略，而不是逐帧预测回滚。
4. 消费 `PredictedLocal` / `KeyInteraction` 标记，只允许少数关键本地实体进入局部预测。
5. 补 focused tests：baseline 应用、delta 应用、low-frequency smoothing、stale/gap 行为。

**来源**：

- [Shooter 大规模纯状态同步方案](Shooter大规模纯状态同步方案.md)
- [Shooter 示例定位与完成度阶段性分析](Shooter示例定位与完成度阶段性分析.md)

### P0-S2：实现 AOI slice 与兴趣管理预算

**问题**：当前 pure-state exporter 已有全局 `MaxEntityCount` / `ActiveSyncBudget`，但还没有按客户端视点、兴趣区块、队伍、距离或优先级生成 per-client AOI slice。

**建议落地**：

1. 增加 AOI query 输入：client/player id、位置、视野半径、兴趣分区或订阅层。
2. 将 `VisibilityHints` 从“跟随输出实体的一一映射”升级为 AOI/interest 的真实消费入口。
3. 按 `KeyInteraction`、`Combat`、`Decorative` 排序并裁剪。
4. 让 `DistanceAoi` / `PriorityBudget` / `LodFrequency` 在 Shooter 中拥有真实 runtime 消费方。
5. 增加大实体数预算测试，验证关键实体优先、普通实体降频、超视野实体裁剪。

**来源**：

- [Shooter 大规模纯状态同步方案](Shooter大规模纯状态同步方案.md)
- [网络同步抽象审计与能力矩阵](网络同步抽象审计与能力矩阵.md)

### P0-S3：补齐 delta / keyframe / resync 的正式恢复语义

**问题**：当前 full snapshot 主链路较稳定，但 delta import、keyframe 恢复、`ShouldResync` 自动恢复和专用 resync 请求还没有完整闭环。

**建议落地**：

1. 明确 delta import 语义：基于 baseline merge，而不是继续使用覆盖式 import。
2. 增加 keyframe 请求与恢复路径，避免 delta gap 后只能依赖 full snapshot。
3. 把 `ShouldResync=true` 的 input response 接入客户端恢复状态机。
4. 增加专用 `RequestFullStateSync` / `RequestBattleResync` 请求，替代“重复 Subscribe 触发 full snapshot”的过渡语义。
5. 增加端到端验证：delta push、gap、import failure、resync request、full snapshot recovery。

**来源**：

- [Shooter 协议与战斗流程阶段性总结](Shooter协议与战斗流程阶段性总结.md)
- [Shooter 客户端漂移检测与强同步恢复方案](Shooter客户端漂移检测与强同步恢复方案.md)
- [网络同步抽象审计与能力矩阵](网络同步抽象审计与能力矩阵.md)

### P0-S4：远程服务器链路接入 lag compensation 与 sync health

**问题**：本地 runtime → host diagnostics 已能观察 lag compensation evaluation，但远程 Orleans/Gateway 链路、健康事件和验收场景仍需接入。

**建议落地**：

1. 在远程 Shooter battle runtime adapter 中接入 lag-compensated hit validation 请求/结果。
2. 将 accepted/rejected reason 进入 `SyncHealthEvent` 或统一 diagnostics。
3. 增加 smoke 或 Orleans focused test：高延迟 shot request、服务器回溯判定、结果推送/诊断可观测。
4. 避免把 lag compensation 做成独立 sync model，应作为 `ServerValidationPolicy.LagCompensatedHitValidation` 能力组合。

**来源**：

- [Shooter 示例定位与完成度阶段性分析](Shooter示例定位与完成度阶段性分析.md)
- [网络同步抽象审计与能力矩阵](网络同步抽象审计与能力矩阵.md)

### P0-E1：恢复全仓完整 build 绿色

**问题**：Shooter smoke 自身通过，但文档记录完整依赖 build 曾被 MOBA 代码阻塞。进入更大范围重构前，完整 build 绿色是 CI 信心前提。

**建议落地**：

1. 修复 MOBA 当前编译阻塞。
2. 确认 Shooter runtime tests、network runtime tests、Orleans smoke 相关工程不再被无关编译错误阻断。
3. 将 full build 作为大改前检查项。

**来源**：

- [Shooter 协议与战斗流程阶段性总结](Shooter协议与战斗流程阶段性总结.md)

## 4. P1：显著提升正式化程度

### P1-1：服务端 pure-state push 预算与网络条件联动

**问题**：`NetworkConditioningMiddleware` 和网络 profile 已存在，但生产链路还未根据弱网、带宽预算、延迟和丢包动态调整 snapshot 预算。

**建议落地**：

1. 在服务端 snapshot publisher 中选择 pure-state payload opCode。
2. 根据 network profile / runtime stats 调整 active budget、low-frequency interval、baseline interval。
3. 输出 degraded diagnostics：预算降级、实体裁剪、低频加重、full snapshot 延后。
4. DemoHarness 记录 weak network 下 pure-state 场景的 completed/degraded 结果。

**来源**：

- [Shooter 大规模纯状态同步方案](Shooter大规模纯状态同步方案.md)
- [Shooter 示例定位与完成度阶段性分析](Shooter示例定位与完成度阶段性分析.md)
- [网络同步抽象审计与能力矩阵](网络同步抽象审计与能力矩阵.md)

### P1-2：DemoHarness 从模式匹配升级为能力矩阵验证

**问题**：文档要求 DemoHarness 按 capability 判断 supported / unsupported / degraded / failed，而不是只用 `CarrierName + NetworkSyncModel` 做名字匹配。部分内容可能已落地，但仍需要与真实 Shooter/Moba carrier 和 UI 矩阵对齐。

**建议落地**：

1. 确认 `ISyncDemoCarrierCapabilities`、`SyncDemoCapabilityResult`、`DemoHarnessRunStatus` 是否全链路使用。
2. Shooter carrier 明确声明 PredictRollback、AuthoritativeInterpolation、PureState/MassBattle 能力边界。
3. Moba carrier 明确声明 AuthoritativeInterpolation 与缺失 AOI/Prediction 的 degraded/unsupported。
4. 批量结果统计 completed / unsupported / degraded / failed。
5. UI 把 unsupported 显示为能力缺口，不显示为运行错误。

**来源**：

- [网络同步能力档案与 DemoHarness 矩阵设计](网络同步能力档案与DemoHarness矩阵设计.md)
- [网络同步抽象审计与能力矩阵](网络同步抽象审计与能力矩阵.md)

### P1-3：协议版本与兼容策略文档化并测试化

**问题**：Room wire 与 Shooter payload 都在扩展字段，但 MemoryPack 字段追加、默认值、版本协同边界还没有明确策略。

**建议落地**：

1. 定义 wire DTO 字段追加规则、不可变 order 规则、默认值兼容规则。
2. 明确 Room wire 版本与 Shooter payload version 的协同方式。
3. 对关键 DTO 增加兼容 round-trip 或旧 payload 默认值测试。
4. 把版本策略写入协议文档，作为后续新增 resync/pure-state 字段的约束。

**来源**：

- [Shooter 协议与战斗流程阶段性总结](Shooter协议与战斗流程阶段性总结.md)

### P1-4：远程版登录恢复与房间生命周期闭环

**问题**：远程版已有基础房间/战斗入口，但登录后恢复当前房间/战斗上下文、成员在线状态、离线宽限期、自动清理和战斗结束回收规则仍不完整。

**建议落地**：

1. 增加登录后恢复查询：当前 room、battle、world、room state、是否运行中。
2. RoomGrain 维护成员在线状态、最后活跃时间、离线开始时间。
3. 配置离线宽限期和自动清理策略。
4. 客户端登录后自动决定创建、加入、重连房间或重连战斗。
5. Shooter 战斗结束后明确房间回收或结果态保留规则。

**来源**：

- [Shooter 远程版闭环实施计划](Shooter远程版闭环实施计划.md)

### P1-5：MOBA 主链路失败语义收紧

**问题**：MOBA 战斗逻辑层仍有默认玩法启动、无效玩家绑定跳过、热更路由异常吞掉、技能 runtime/trace 创建失败静默继续、快照参数错误返回空等问题。

**建议落地**：

1. 启动必须有明确玩法计划；缺失玩法配置应阻断。
2. 无效玩家 Actor 映射改为启动校验错误。
3. 输入热更路由异常进入统一异常策略，不回落默认分发。
4. 技能 runtime/trace 必选能力缺失时阻断技能启动。
5. 快照调用参数错误抛异常，合法无快照才返回空。

**来源**：

- [MOBA 战斗逻辑层整改流程图与分析](MobaBattleLogicLayerRectification.md)

## 5. P2：结构化扩展与维护性提升

### P2-1：MOBA BattleStartPlan / Command Contract / Snapshot Contract 正式化

**建议落地**：

1. 新增不可变 `BattleStartPlan` 与 validator。
2. 新增 command contract registry，覆盖 opcode、权限、帧策略、payload schema。
3. 快照 emitter 由玩法模式声明必选集，缺失时阻断启动。
4. `MobaLogicWorldDriveGate` 扩展为 readiness / pause / settlement / replay / authority mode 闸门。

**来源**：

- [MOBA 战斗逻辑层整改流程图与分析](MobaBattleLogicLayerRectification.md)

### P2-2：MOBA execution context fallback 继续收紧

**建议落地**：

1. 根据 warning 和搜索结果找出仍依赖 fallback 的入口。
2. 严格 action 使用 `TryResolve` 并显式处理失败。
3. 将 fallback create 缩小为 debug/assert 路径。
4. Area / Summon / Channel / Unit State 统一接入 retain/release、root/owner/origin 和 `ExecuteTriggerId`。
5. 精细化 trace action child 与具体 action 调用、skip/fail reason 的绑定。

**来源**：

- [MOBA 战斗上下文阶段性代码总结](Moba战斗上下文阶段性代码总结.md)

### P2-3：Client Flow 边界校准

**建议落地**：

1. 将 MOBA flow state/event id 从 `GameFlowDomain` nested enum 独立出来。
2. 增加 condition/action catalog/resolver，减少 Domain 硬编码。
3. 将流程文档区分 Current Implementation 与 Target Flow。
4. 设计 frozen spec 或 builder/model 分离。
5. 暂不急于物理拆包，但代码按 `game.clientflow.runtime` / `game.view.runtime` 的未来边界写。

**来源**：

- [客户端流程编排阶段性复盘](客户端流程编排阶段性复盘.md)

### P2-4：Presentation 契约用第二示例验证

**建议落地**：

1. 不强迁 MOBA 全表现层，只选择一个小切面定义 `MobaBattleViewBatch`。
2. 验证 MOBA Entitas/ECS 表现系统消费 `IViewSink<TViewBatch>` 是否自然。
3. 若 `IViewBatch.WorldId/Frame/Sequence` 对非网络场景偏重，拆为 marker + optional metadata interfaces。
4. 保持 Presentation 不承担 Client Flow action、连接、进房、场景切换等职责。

**来源**：

- [客户端表现层框架契约设计](客户端表现层框架契约设计.md)
- [客户端流程编排阶段性复盘](客户端流程编排阶段性复盘.md)

### P2-5：统一 sync diagnostics / health events 与示例面板

**建议落地**：

1. 将 snapshot received/dropped/stale/gap、interpolation starved/recovered、full snapshot requested/applied、input accepted/rejected、lag-comp accepted/rejected 汇总到统一 health event 视图。
2. Editor/Unity 面板展示 time anchor、server ticks、drift/recovery、network condition、budget/degraded 状态。
3. 避免继续膨胀 `SyncReconciliationReport`，预测回滚以外的诊断走 health event。

**来源**：

- [网络同步抽象审计与能力矩阵](网络同步抽象审计与能力矩阵.md)
- [Shooter 示例定位与完成度阶段性分析](Shooter示例定位与完成度阶段性分析.md)

## 6. P3：可延后处理

### P3-1：文档陈旧描述清理

**问题**：部分设计文档仍保留已过期判断，例如 time anchor 未驱动客户端步进、lag compensation 仍 idle、LOD frequency 无 runtime 消费方等。近期实现已经关闭了其中一部分。

**建议落地**：

1. 将旧文档中的“仍未接入时间锚”“lag compensation 只是 helper”“LOD frequency 无 runtime 消费方”改为阶段性口径。
2. 把“已完成 / 仍缺口 / 下一阶段”分节，避免读者误判现状。
3. 对 `Shooter验收外壳绑定说明.md` 等操作文档做 API 口径校正。

**来源**：

- [Shooter 示例定位与完成度阶段性分析](Shooter示例定位与完成度阶段性分析.md)
- [网络同步抽象审计与能力矩阵](网络同步抽象审计与能力矩阵.md)

### P3-2：MOBA 结构清理与工具化

**建议落地**：

1. 删除或迁移旧 `MobaBattleDriverHost` 输入处理函数为 trace/debug hook。
2. 将局部依赖诊断下沉为统一诊断工具。
3. 将大清单式 validator 拆成配置、输入、执行、输出、临时实体等能力域 validator。
4. 拆分 read model 与正式 snapshot DTO。

**来源**：

- [MOBA 战斗逻辑层整改流程图与分析](MobaBattleLogicLayerRectification.md)

### P3-3：体验与可视化增强

**建议落地**：

1. 增强诊断面板可读性。
2. 增加 smoke 脚本状态汇总与失败定位提示。
3. 增加更多可视化皮肤、UI 美化和非核心玩法变体。

**来源**：

- [Shooter 示例定位与完成度阶段性分析](Shooter示例定位与完成度阶段性分析.md)

## 7. 建议执行顺序

推荐按以下顺序推进；P0-C 与 P0-S 是并列产品主线，实际开工仍以 owner 批准的 Trellis task 为准：

1. P0-E1：先恢复完整 build 绿色，避免后续 Cooking、同步或文档调整被无关编译错误干扰。
2. P0-C1：把已验证的 ET Tick 接点扩展为正式 Cooking Match 宿主与单一加工时钟。
3. P0-C1：随后完成 checkpoint、跨小关成功/失败/升级规则与成功 settlement；原 UDP owner-thread ingress 已放弃，传输接入待 KCP 方向另行立项。
4. P0-S1：补 pure-state 客户端消费闭环，让纯状态同步真正可演示。
5. P0-S2：补 AOI slice / interest budget，支撑上万实体论证。
6. P0-S3：补 delta/keyframe/resync，解决恢复链路正式性。
7. P0-S4：把 lag compensation 接入远程服务器链路与 health event。
8. P1-1：把服务端 pure-state push 和网络条件/预算联动。
9. P1-2：校正 DemoHarness capability matrix，让多同步策略展示不再依赖名字匹配。
10. P1-3 / P1-4：补协议版本策略与远程房间恢复闭环。
11. P1-5 / P2-1 / P2-2：推进 MOBA 主链路治理，作为第二大型示例的正式化支撑。
12. P2-3 / P2-4：校准 Client Flow 与 Presentation 边界。
13. P3：清理陈旧文档、结构工具化和体验增强。

## 8. 已关闭但需要回写口径的事项

以下事项不应继续作为最高优先级待办，但相关旧文档可能还残留旧说法：

- Shooter PlayMode / Remote PlayMode 已开始使用 `SyncClock` / `SyncTimeAnchor` 投影本地 time anchor；剩余问题是远程服务器时间、恢复追帧和插值播放全链路贯通。
- lag compensation 已有 runtime → host diagnostics 的本地可观察闭环；剩余问题是远程服务器链路和 sync health event。
- pure-state sync 已从协议推进到 runtime exporter/port，并已有预算和低频输出；剩余问题是 AOI、字段级 diff、客户端消费和服务端推送预算。
- DemoHarness capability/profile 相关内容需要以当前代码实际落地状态复核；若 runtime 已有类型和四态报告，应把文档从“草案”改为“基线 + 下一步”。

## 9. 结论

当前项目待办不再只围绕同步展柜：Cooking ET 应用运行时和 Shooter 同步正式化是两条并列 P0 主线。前者重点是正式 Match 宿主、单一加工时钟、可恢复 checkpoint、跨小关规则与最终 ECS 清退（UDP ingress 已于 2026-09-21 放弃，传输改用 KCP 属后续未启动工作）；后者重点是纯状态客户端消费、AOI/预算、delta/keyframe/resync、远程 lag compensation 与网络条件预算联动。MOBA 和 Client Flow 作为后续正式化主线，重点是收紧失败语义、建立不可变计划/契约、校准包边界，而不是先做大规模功能扩张。
