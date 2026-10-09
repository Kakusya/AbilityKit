# 任务与模块路由

本文件只负责定位现有规范、设计与源码，不新增产品语义或执行许可。
先读 [根 AGENTS](../../../AGENTS.md) 的 Owner 约束，再选与当前问题相关的行。Cooking 工程暂停、禁止 SHA 校验、JSON 证据限制与测试范围约束优先适用。

## 阅读顺序

1. 判断是 Cooking 行为、共享机制、宿主工具还是工程流程。
2. 涉及 Cooking 当前目标，先读 [progress](../../../Docs/design/CookingGame/progress.md)，再读 [Cooking 索引](../cooking/index.md) 和对应 task／最新 Issue。历史状态不恢复授权。
3. 按下表读取对应稳定契约与设计；跨模块只追踪当前需求实际经过的消费链。
4. 查看真实源码、`.csproj`、包 `package.json`、`.asmdef` 和生成器依赖。文档中的 API 与实现不一致时，分别记录当前实现与目标契约，不自行修改产品语义。

## Cooking 应用

下表源码路径从仓库根目录计算。`src/AbilityKit.Game.Cooking*` 是应用源码，不能机械地映射到一个同名 Unity package。

| 问题／触发条件 | 源码定位 | 先读文档 |
|---|---|---|
| 拿取、放置、距离、工位、物品位置和容器关系 | `src/AbilityKit.Game.Cooking/` 的交互与物品代码 | [交互基础](../cooking/cooking-interaction-foundation.md)；新增玩法规划再读 [功能与菜单 plan](../cooking/gameplay-menu-plan.md) |
| 配方、加工、份数、订单、前厅和固定伙伴 | `src/AbilityKit.Game.Cooking/` 的配方／经营代码 | [配方循环](../cooking/cooking-recipe-loop.md)、[功能与菜单 plan](../cooking/gameplay-menu-plan.md) 与该功能 task；保留自然完成与成功检查点语义 |
| 菜单导入、正式内容、schema 和定义校验 | `src/AbilityKit.Game.Cooking/Content/` 及其加载／校验调用方 | [配置验证](../cooking/cooking-config-validation.md)、[菜单合同](../cooking/cooking-menu-catalog.md)；原始菜单先读 [reference 索引](../../../Docs/design/CookingGame/reference/README.md) |
| ET 状态 writer、Entity 生命周期、System 或固定 Tick | `src/AbilityKit.Game.Cooking.EtRuntime/`、`src/AbilityKit.Game.Cooking.EtBridge/`；底层 `Unity/Packages/com.abilitykit.et.runtime/` | [当前 ET 合同](../../../Docs/design/CookingGame/current-et-foundation-contract.md)；再定位该状态族的 Cooking 契约。EtBridge 与 EtRuntime 不因名称相近而视为同一 owner |
| Room／Match／Level 生命周期、跨关和失败重开技术入口 | `src/AbilityKit.Game.Cooking/` 的 Level／Match 与 `Session/` | [Match 生命周期](../cooking/cooking-match-lifecycle.md)、[LAN 会话](../cooking/cooking-lan-session.md) 与对应 task |
| Host／Client、Join／ACK／Ready、投影、重连和传输 | `src/AbilityKit.Game.Cooking/Session/`、`src/AbilityKit.Game.Cooking.Udp/` 及实际 transport 引用 | [LAN 会话](../cooking/cooking-lan-session.md)、[当前 ET 合同](../../../Docs/design/CookingGame/current-et-foundation-contract.md)；共享传输按下一表继续定位 |
| Checkpoint、保存、恢复与故障模型 | `src/AbilityKit.Game.Cooking/` 的 checkpoint／store 与 ET 恢复调用方 | [持久化合同](../cooking/cooking-persistence-management.md)、[存档参考](../../../Docs/design/CookingGame/reference/save-storage.md)；区分进程内、正常重启、崩溃和掉电 |
| 测量、物理双机 LAN 与尚未验证的出口 | 对应 Cooking measurement 工程与调用工具，仅按授权读取 | [网络测量](../cooking/cooking-network-measurement.md)、[物理 LAN runbook](../../tasks/10-02-cooking-network-gameplay-loop/research/physical-lan-runbook.md)；没有物理双机证据仍为 NOT_VERIFIED |
| Owner 指定的真实 Flow 场景与验收入口 | `src/AbilityKit.Game.Cooking.FlowAcceptance/` | [fixed-flow](../../../Docs/design/CookingGame/testing/fixed-flow.md)；只允许已指定场景，旧 rich／NetworkRichRecoveryAcceptance 禁止执行。文档内诊断控制与历史命令不提供新增测试授权 |
| Windows 防火墙、共享端口与 READY | `tools/cooking-firewall.ps1` 及 Cooking 启动调用方 | [本机联网工具规范](cooking-network-tooling.md)；工具查询与真实连通是不同事实 |
| Cooking Unity、场景、UI、表现与 authoring | 先判断授权，不按未来计划创建包或修改场景 | [future-scope](../../../Docs/design/CookingGame/future-scope.md)；当前仍禁止实施 |

## 共享框架

包名均位于 `Unity/Packages/`。对应 `src/AbilityKit.*` 工程是否复用这些源码，以实际 `Compile Include` 为准。
这些入口可用于阅读；改动必须先列出已批准的 Cooking 消费者、必要性和验收。读到示例消费者不代表允许修改示例。

| 问题／触发条件 | 包／源码组 | 先读文档 |
|---|---|---|
| 不知道模块归属、能力组合或项目结构 | 先定位包与 `.csproj` | [设计总索引](../../../Docs/design/00-index.md)、[能力地图](../../../Docs/design/01-OverviewAndGettingStarted/00-AbilityKitCapabilityMap.md)、[项目结构](../../../Docs/design/01-OverviewAndGettingStarted/04-ProjectStructure.md) |
| 基础集合、事件、对象池、定时器、Context 和环境 | `com.abilitykit.core`、`unity.pool`、`timer`、`context`、`environment` | [事件系统](../../../Docs/design/05-CommonModules/01-EventSystem.md)、[对象池](../../../Docs/design/05-CommonModules/02-ObjectPool.md)、[定时器](../../../Docs/design/05-CommonModules/03-TimerFramework.md)；其他条目读对应包 README／Document |
| 通用流程编排与 HFSM 状态转移 | `com.abilitykit.flow`、`hfsm` | [Flow](../../../Docs/design/05-CommonModules/05-FlowEngine.md)、[HFSM 演进](../../../Docs/design/13-FrameworkCore/08-HfsmDeterministicRuntimeEvolution.md)；Cooking FlowAcceptance 是应用验收入口，不能等同通用 Flow 包 |
| World、DI、ECS 查询与组件存储 | `com.abilitykit.world.di`、`world.ecs`、`world.entitas`、`world.svelto` | [World](../../../Docs/design/02-LogicalWorldDesign/01-WorldOverview.md)、[DI](../../../Docs/design/02-LogicalWorldDesign/05-ServiceContainer.md)、[ECS](../../../Docs/design/06-ECSArchitecture/01-ECSCoreConcepts.md)；Cooking ET owner 另按当前 ET 合同读取 |
| Host 装配、Tick、模块与 teardown | `com.abilitykit.host`、`host.extension`、`host.network` | [Host](../../../Docs/design/03-LogicalWorldHostDesign/01-HostRuntime.md)、[模块系统](../../../Docs/design/03-LogicalWorldHostDesign/02-HostModules.md) 与相关包文档；不替代 Cooking 唯一 Tick 入口 |
| Coordinator、游戏流程和表现端口 | `com.abilitykit.coordinator`、`gameframework`、`gameframework.network`、`game.view.runtime`、`game.battle.runtime` | [会话协调](../../../Docs/design/07-NetworkSynchronization/05-SessionCoordination.md)、[客户端流程](../../../Docs/design/04-PresentationLayerDesign/04-ClientGameFlowAndPhaseArchitecture.md) 与包源码；Coordinator 只按现有精简契约，不照旧 README 假定旧类可用 |
| 帧同步、状态同步、快照、预测、回放与确定性 | `com.abilitykit.world.framesync`、`world.statesync`、`world.snapshot`、`world.networkfragments`、`record`、`record.memorypack`、`deterministic` | [同步能力地图](../../../Docs/design/07-NetworkSynchronization/00-SynchronizationCapabilityMap.md)，再读命中机制；Cooking 权威与恢复策略仍由应用合同决定，禁止 SHA 校验 |
| Network SDK、Room、Battle、Host 与 transport | `com.abilitykit.network.*` | [SDK 接入](../../../Docs/design/07-NetworkSynchronization/07-MultiplayerSdkIntegrationGuide.md)、对应包 README 与 Cooking 当前 transport 引用；不是要求引入登录／Gateway／Orleans 全栈 |
| 协议 ID、wire 字段、serializer 与生成代码 | `Protocols/`、`com.abilitykit.protocol*`、相关生成器 | [Protocols README](../../../Protocols/README.md)、[协议目录与观测](../../../Docs/design/07-NetworkSynchronization/11-ProtocolCatalogAndTrafficObservability.md)；先找 schema 权威源，勿手改派生文件，示例协议不在修改范围 |
| 技能输入、执行管线、触发器、动作与 Buff | `com.abilitykit.ability`、`pipeline`、`triggering`、`actionschema`、`continuous`、`modifiers`、`gameplaytags`、`attributes` | [玩法能力地图](../../../Docs/design/08-GameplayModules/00-GameplayCapabilityMap.md)，再选 Pipeline／Triggering／Buff 等专题；不是 Cooking 必须采用的整套战斗模型 |
| Damage、Projectile、Targeting、Motion 与实体索引 | `com.abilitykit.combat.*`、`dataflow` | [玩法能力地图](../../../Docs/design/08-GameplayModules/00-GameplayCapabilityMap.md) 与对应包 Document／Documentation~；先证明 Cooking 是否实际消费 |
| 碰撞查询、导航、计算后端与 Jobs | `com.abilitykit.combat.collision.abstractions`、`combat.navigation`、`compute`、`compute.unityjobs` | [碰撞](../../../Docs/design/13-FrameworkCore/01-CollisionSystemDesign.md)、[导航](../../../Docs/design/13-FrameworkCore/05-DeterministicGridNavigation.md)、[计算后端](../../../Docs/design/13-FrameworkCore/10-ComputeAccelerationBackends.md)；Jobs／Unity 实施边界不因可复用而解除 |
| 行为执行、行为树和 AI 桥接 | `com.abilitykit.behavior`、`behaviortree`、`ai.*` | [行为树包](../../../Docs/design/13-FrameworkCore/07-BehaviorTreePackageDesign.md) 与对应包文档；固定伙伴行为由 Cooking 规则拥有 |
| Trace、诊断与可解释化 | `com.abilitykit.trace`、`diagnostics`、`ability.explain` | [Trace](../../../Docs/design/13-FrameworkCore/03-TraceLifecycleAndExportProtocol.md)、对应包文档；只旁路观察已提交结果，遵守 JSON 证据限制 |
| Editor、配置生成、热更新、分析器和第三方来源 | `com.abilitykit.base.editor`、`actioneditor.impl`、`excel-sync`、`analyzer`、`hotreload`、`thirdparty.*` | 对应包文档、[编辑器平台](../../../Docs/design/13-FrameworkCore/09-EditorPlatformConvergence.md) 与来源／许可说明；新依赖或 fork 按根 AGENTS 的 ADR 规则处理 |
| 测试／验收工具、scenario、samples 与发布 | `com.abilitykit.battlescenario`、`scenario`、`samples`、相关工具 | 先应用根 AGENTS 的 Owner 测试约束；Cooking 验收只路由到上表 FlowAcceptance。历史宽门禁、框架控制与示例运行命令不能作为默认出口 |
| MOBA、Shooter、Tiny、ET Demo、Orleans 或用户练习 | `com.abilitykit.demo.*`、`src/AbilityKit.Demo.*`、`Server/Orleans/`、`Unity/Assets/Practice/` | 仅在当前 Cooking 问题确实需要时只读参考；不修改、不运行其专用门禁、不恢复已删除示例技能，保留用户练习 |

## 路由维护

- 根 AGENTS 保存跨任务约束与一级入口；本文件保存模块索引；稳定行为正文继续保留在既有 spec／设计中。
- 增加路由时写明触发条件、真实源码位置、首先读取的权威文档和必要边界；不复制产品状态或 API 正文。
- 包局部说明优先使用现有 README、Document 或 Documentation~。缺少入口时先从 [设计总索引](../../../Docs/design/00-index.md) 与源码定位，记录缺失，不凭空补规则。
- 未来确需目录级 AGENTS 时，只写该目录增量规则并链接根约束；不能覆盖 Owner 边界，也不为当前禁止修改的示例增设文件。
- `.claude/skills/` 中已删除的模块技能不作为路由目标。实际技能可用性以本次会话列表为准，磁盘文件存在不证明已加载。
- 修改本路由后更新 [工程索引](index.md)，人工审阅路径与文本差异。文档维护不触发测试、构建或广泛门禁。
