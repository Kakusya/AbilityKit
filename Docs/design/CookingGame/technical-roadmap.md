# 做菜经营游戏技术路线

> 文档类型：应用层技术路线图（roadmap），不是 AbilityKit 框架规范、Trellis 行为草案或实施任务清单。
>
> 状态：路线保留长期分层与行为方向；截至 2026-09-16，P0–P6 各有一份已验证并完成归档的纯 .NET 受限交付，但完整产品出口仍未完成。Cooking Unity 执行长期禁止；非 Unity 后续未启动、未批准、无时间表。详见 [当前进度](progress.md)、[future scope](future-scope.md) 与 [successor backlog](successor-backlog.md)。近期 LAN 仍只是产品方向，不表示 production transport 或真实 LAN 已交付；主机角色决策见 [ADR-0001](../../../ADR/decisions/0001-player-host.md)。
>
> 归属：本文件是做菜经营游戏应用技术路线的唯一正文；框架机制仍以 [`Docs/design/`](../00-index.md) 及其 canonical 文档为准，待审阅的功能边界、验证设想和实施计划位于 `.trellis/spec/cooking/` 与 `.trellis/tasks/`。

## 1. 路线边界与分层

AbilityKit 提供可复用的 World、ECS、Host、Config、Luban、HFSM、FrameSync、StateSync、Snapshot、Record 等机制；本游戏负责组合这些机制，并自行建设烹饪领域模型、局域网 listen session、wire codec 与业务状态导出。不要假设仓库已经存在旧的 `SessionCoordinator` 或 `Local/Remote/Hybrid` 实现；Orleans 只是可选宿主示例，不是本游戏必需依赖。

```text
Prohibited Unity Host        Pure C# Simulation            Host/Network
(no current implementation)  Rules, Entities, Tick  <---->  adapters / clients
                                      |
                              Snapshots / Events
```

- 当前禁止实施 Cooking Unity authoring/view/runtime；历史 Unity 条目与重新授权条件统一见 [future scope](future-scope.md)。
- 若将来重新授权，Unity 编辑器只可负责地图空间 authoring，运行时消费可序列化逻辑布局，规则与模拟保持纯 C#。
- 配置表保存定义、规则和引用，不保存某一局的实例状态。
- `DefinitionId` 标识跨局复用的配置定义；`InstanceId` 标识当前 Level/Match 中的运行时实例，二者不可混用。
- 地图定义空间布局；关卡引用地图并配置订单、时限、目标与可用内容；Match/World 是实际运行的一局。同一张地图可被多个关卡复用，同一关卡也可创建多次独立对局，三者不是一一对应关系。

## 2. 配置与地图模型

### 2.1 定义、实例和表

建议的配置目录包含 `Item`、`Ingredient`、`Appliance`、`Process`、`Recipe`、`Level`、`Order` 等表。配置加载后应在开局前校验：外键存在性、器具能力与工序要求、配方拓扑、等级/关卡引用以及启动配置 hash；客户端与主机的配置 hash 不兼容时阻断加入或启动。表描述定义和规则，容器、物品、订单进度、计时器、玩家持有物等属于运行时实例状态。

地图空间 authoring 曾规划由 Unity 编辑器输出逻辑布局，但当前属于长期禁止的 future scope。宿主无关约束仍保留：逻辑布局至少表达处理站、可携带容器（如锅、盘）、手持工具、食材/菜品和放置槽等逻辑类别；任何将来获准的宿主都不得把 Unity `GameObject` 身份当作网络或模拟身份。

### 2.2 位置与所有权不变量

每个物品在任一时刻只有一个位置，位置取值为：

```text
WorldPosition | PlayerHand | StationSlot | ContainerSlot
```

运行时必须保持无重复所有权、无重复占位和无包含环。容器可以拥有内容物，但不能通过任何路径形成 containment cycle。`ItemDefinition` 的能力、容量和可处理规则来自配置；`ItemInstance` 的位置、内容、加工进度和生命周期来自模拟。

## 3. 权威交互与模拟时钟

### 3.1 命令路径

拾取、放下、转移、放入容器、从站点取出等交互采用权威原子命令。主机和远端必须走同一套命令验证路径，验证至少覆盖实体生命周期、动作资格、距离/可达范围、目标容量、资源可用性和当前位置；失败不得产生部分变更。

```text
Network / Local Input
        |
        v
Queue -> Stable Order -> Validate -> Atomic Commit -> Events / Snapshots
                                          |
                              Simulation Thread Only
```

同时争抢同一物品、槽位或器具时使用确定且稳定的仲裁规则（例如模拟 Tick、玩家 ID、命令序号的明确排序），不能依赖线程调度或字典枚举顺序。一次性命令需要去重键/幂等处理；持续移动、瞄准或按住交互采用独立的 continuous-input 策略，不把持续输入伪装成无限重复的一次性命令。

### 3.2 固定 Tick

推荐采用权威固定 Tick 的纯 C# 模拟；逻辑 Tick 独立于渲染帧，烹饪、处理和订单计时均依据模拟 Tick 或逻辑时钟，不依据动画回调、Unity `Update` 偶然频率或墙钟。30Hz 只是原型候选值，必须通过响应性、负载、网络和玩法测量后再决定，不是已锁定的硬性要求。

主机负责权威推进和命令裁决；listen host 的本地玩家与远端玩家进入同一命令验证路径。网络线程只负责收发与入队，不能直接修改模拟状态。

## 4. 同步路线与确定性边界

### 4.1 首选方案

首阶段采用“权威固定 Tick + 状态同步/快照”作为基线：主机推进模拟，按稳定顺序处理输入并广播快照或状态更新；客户端以快照顺序和版本为基准，表现层做插值，必要时再量化本地预测。`FramePacket` 可以携带输入和可选快照，但**不保证严格 lockstep**，不能把现有 FrameSync 能力直接宣称为跨机器全战斗确定性。

| 方案 | 优点 | 成本与当前结论 |
|---|---|---|
| 严格确定性 lockstep | 输入驱动一致，带宽取决于输入规模 | 要求全链路确定性和缺输入策略；仅引入预测时才需要相应回滚，暂不作为首阶段基线 |
| 权威状态同步 | 对跨机器差异更宽容、快照可校正 | 需要快照版本、丢包/乱序、插值和状态导出；首阶段推荐 |

客户端表现采用快照作为 baseline，并按序处理版本；插值和局部预测是测量后可启用的优化，不是初始正确性前提。

### 4.2 未来若转向严格确定性

必须显式解决并验证：随机种子、稳定实体/命令排序、数值表示与舍入、碰撞/空间查询、状态保存与恢复、状态 hash、缺失输入策略、预测与 rollback。不能假设不同机器上的 Unity `Rigidbody` 或一般物理执行天然确定。

## 5. 运行时结构

### 5.1 HFSM 与玩法编排

建议以 HFSM 表达相对稳定的层级流程：

```text
Room -> Match -> Role -> Action / Processing
```

房间、对局、角色和动作/加工是不同维度；持有物槽位、移动和当前动作应分开建模，避免把所有组合塞进一个状态机造成状态爆炸。配方应由数据驱动的处理/组合规则表达，而不是为每一种菜建立巨型状态机。

“番茄 -> 切碎 -> 入锅烹饪 -> 汤 -> 装盘”的链路仅是说明数据驱动处理的示例，不是已决定的正式配方。

### 5.2 局内与长期状态

Round/Match 状态（当前订单、实例位置、加工进度、计时器、得分结算上下文）与长期餐厅/存档状态（解锁、升级、货币、经营进度）必须分离。奖励与结算应具备幂等语义，避免重连、重复提交或主机恢复导致重复发奖。

房主退出、存档归属、断线恢复、主机迁移、玩家人数、首发平台，以及动作烹饪与经营管理的平衡仍是 OPEN；本路线确认不回答这些产品选择，也不把 LAN 近期目标升级为 WAN 承诺。

## 6. 推荐验证顺序（不是正式 tasks）

P0–P6 已各完成并验证一个纯 .NET 受限增量，详细边界与历史证据见 [当前进度](progress.md) 和对应归档 task；这不等于下列产品切片完整出口已完成。未完成的 P1–P6 非 Unity 工作统一进入 [successor backlog](successor-backlog.md)，不是 active task；Unity 执行统一进入 [future scope](future-scope.md)，长期禁止且不是 blocker。

1. 逻辑拾取/放下与唯一所有权。
2. 两个局域网实例的争抢与仲裁。
3. 一条完整配方闭环。
4. 配置表扩展、引用校验和启动 hash 兼容性。
5. 地图、Round/Match 生命周期与快照接入。
6. 长期经营和持久化管理。
7. 以实测结果推进响应性、插值/预测和网络优化。

每个切片的稳定行为契约见 [`.trellis/spec/cooking/index.md`](../../../.trellis/spec/cooking/index.md)，历史实现与命令证据见对应归档 task。归档只说明重划后的 pure .NET limited delivery 完成；正式内容、production transport、真实 LAN、durable storage 与完整产品出口仍是缺口。阶段及状态入口见 [交付计划](delivery-plan.md)。

## 7. 关联入口

- [ADR-0002：权威固定 Tick 与首阶段状态同步](../../../ADR/decisions/0002-authoritative-fixed-tick-state-sync.md)：已接受的结构性取舍与边界。
- [ADR-0001：主机同时作为服务器与玩家](../../../ADR/decisions/0001-player-host.md)：listen host 角色决策。
- [长期目标](../../../ADR/long-term-goals.md)：产品方向、近期 LAN 和未决范围。
- [AbilityKit 框架设计索引](../00-index.md)：World、Config、HFSM、FrameSync、StateSync、Snapshot 等既有机制的 canonical 入口。
- [做菜 Trellis 规范与归档交付索引](../../../.trellis/spec/cooking/index.md)：已验证 pure .NET limited delivery、non-Unity successor backlog 与 prohibited Unity future scope 的统一入口；不表示完整产品出口完成。
