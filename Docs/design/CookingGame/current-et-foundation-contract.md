# 当前 ET 底座契约（Issue #5）

2026-10-04，文档与规则交付；没有运行时迁移。源码审计基线为 `a2cd7284e12d50a10bbb7abee6fc265577b9aa7c`，本工作区起点为 `5312c6e4bf2b612260297e2d8623aa362a9051e6`。实际 `git diff --stat` 仅有 [reclone handoff](reclone-handoff-2026-10-04.md) 新增 48 行。本文的“已接线”指源码编译入口和调用路径，**不是本轮编译或运行通过**；本轮 .NET、游戏、Unity、IL2CPP 全部 NotRun。历史 S01–S14/N01 的受限接受保留，N02/N03、物理 LAN、性能与 Unity 不因本文完成。

授权与最新状态分别见 [Issue #5](https://github.com/Kakusya/AbilityKit/issues/5) 和 [progress](progress.md)。#1–#4、#6–#9 仍 blocked；阅读、草案和标签不解锁实现。ADR [0003](../../../ADR/decisions/0003-cooking-et-runtime-direction.md) 保持 Proposed。本文固定既有语义并具体化 ET 目标；新 System 名称、纵切与新增检查都标为提案。事实来源见 [本轮源码审计](../../../.trellis/tasks/10-04-current-et-foundation-contract/research/source-audit.md)。

<a id="a"></a>

## A. 实际树、目标树和所有权

实际树由 [CookingLevelEtHost.cs](../../../src/AbilityKit.Game.Cooking.EtRuntime/CookingLevelEtHost.cs) 的 `CookingApplicationComponent` 至 `CookingLevelDriverComponent`、`InstallLevel`、`RemoveLevel` 建立：

```text
EtRuntimeHost -> Scene
  CookingApplicationComponent
    CookingMatchRegistryComponent
      CookingMatchEntity
        CookingMatchIdentityComponent
        CookingRestaurantRuntimeComponent
          CookingKitchenComponent
          CookingLevelComponent -> ordinary CookingLevelLifecycle
            CookingLevelDriverComponent -> Host + ordinary CookingRecipeSimulation
Host ordinary fields: pending / terminal / history / networkNotifications / front / layout
CookingNetworkSessionHost -> ordinary connections / participants / mapping / waiting
CookingNetworkAuthorityAdapter -> same Host (no independent simulation or clock)
```

Kitchen 和 Level 是 Runtime 下的同级 Component；Driver 在 Level 下。Simulation 的字典仍持有领域状态，ET 壳并不证明 Item/Process 已迁移。树测试入口为 [CookingLevelEtHostTests.cs](../../../src/AbilityKit.ET.Runtime.Tests/CookingLevelEtHostTests.cs) 的树及写权限测试。

目标树是提案，按 [reference/et-entity-tree](reference/et-entity-tree.md) 选择有生命周期的聚合：

```text
Scene / Application / MatchRegistry / Match
  MatchIdentity + ParticipantRegistry / ParticipantEntity / connection binding
  RestaurantRuntime
    Kitchen
      ItemRegistry -> ItemEntity + location/version/container capability
      StationRegistry -> StationEntity + installed capability/occupancy
      ProcessRegistry -> ProcessEntity + progress/worker/input locks
      Inventory/Supply ledger + allocator + domain lookup indices
    Level
      OrderRegistry -> OrderEntity + binding/settlement
      front-of-house/customer/work state
      CommandQueue + CommandLedger + EventJournal + lifecycle/scope
      LevelDriver -> explicit transaction Systems (one authoritative tick)
  Session owner -> transport binding / receipt / baseline ACK ledger
```

这是 owner 目标而非已存在的类型列表。命令、终态、事件、baseline 是 DTO/账本条目，不逐条变成 Entity。Parent 仅表达创建/释放责任；Item 的容器关系、Process 的工位关系、Participant 的 Connection 关系用稳定业务 ID，不能以 Parent 替代。`InstanceId`/`EntityRef` 是同进程 incarnation 检查，不能成为 wire/save 身份。业务关闭、结算、网络 drain 必须先显式执行；Dispose 不替代它们。

### 状态族清单

表中所有目标 System 均为**提案**，普通 C# 规则、算法、配置、DTO、存储接口可保留。查询索引只允许从权威图重建，不能单独成为第二份可写真相。`S` 指 [CookingRecipeLoop.cs](../../../src/AbilityKit.Game.Cooking/CookingRecipeLoop.cs) 的 `CookingRecipeSimulation`；`H` 指上述 Host；checkpoint 源为 [CookingRecipeCheckpoint.cs](../../../src/AbilityKit.Game.Cooking/CookingRecipeCheckpoint.cs)。每行的检查是后续迁移验收要求，尚未执行。

| 状态族与当前文件/符号 | 目标 owner / 允许 writer | 创建、切关、销毁与恢复/索引 | 一致性闭包、旧入口撤销、消费者与控制 |
| --- | --- | --- | --- |
| Item：S `_items`, `ItemState`, `AddItemCore`, `Pickup`, `Drop`, `CommitMoveToStation`；生产物由 `BuildFixedTickPlan`，供应物由 `ExecuteSupply` 创建 | Runtime/Kitchen ItemRegistry、ItemEntity；ItemTransactionSystem 在 Driver 授权批次内修改 | 配置/准备供应创建；成功 carry 按 `ExportSuccessHandoff`/`AcceptSuccessHandoff` 白名单，失败重开标准供应；消费保留 tombstone；Runtime 结束释放；恢复按 ItemId 重建后验证定义、location、version | 与手持、容器、process 锁、订单绑定、供应 provenance、allocator 同组。撤 `_items` 写入，旧 API 只委托；Snapshot/checkpoint/canonical、前厅交付、Session capture 均适配。正：pickup→drop→restore→继续；负：越权 AddItem、重复 ID、旧 EntityRef、消费后再次交付 |
| 手持/容器：S `_hands`, `_containerItems`, `_cleanContainerCount`, `PutIn`, `TakeOut`, `Pour`, `WouldCreateContainmentCycle` | Item location/capability 与 Participant hand index；ItemTransactionSystem | 玩家/容器创建空槽；成功 carry 以 checkpoint 实际规则保留，失败清场；容器被消费/释放时引用清理；恢复由 location 重建 hand 和 containment 并比对 DTO | 物品 location/版本、容器槽位/容量、锁、干净容器数一起提交；撤旧可写 hands/container 集合；洗碗 port、Process、订单、导出器适配。正：嵌套搬运/倒出后恢复；负：环、双手占有、foreign slot、重复 release/重复洗碗不得增计数 |
| Station：S fixture/effective layout 的配置工位与 `_processesByStation`, `CommitMoveToStation`；H `_installedLayout` | Kitchen StationEntity/installed capability；LayoutInstallSystem（准备态）和 ProcessTransactionSystem（占用） | 布局创建；同关安装/成功 carry 与失败重置按 lifecycle 契约；撤布局使对应旧引用失效；恢复校验 layout identity、station slot、占用映射 | 与 Item location、Process station/anchor、空间可达规则同组；撤旧独立占用字典，配置可保持只读。准备布局、recipe selection、checkpoint validator 适配。正：换位→开始加工→恢复；负：双占用、不存在 slot、运行态布局旁路写 |
| Process：S `_processesByStation`, `_stationsByProcess`, `_processesByAnchorItem`, `_anchorItemsByProcess`, `_inputsByProcessItem`, `_lockedInputsByProcess`, `ProcessState`, `RemoveProcess` | Kitchen ProcessEntity/Registry；ProcessTransactionSystem、FixedStepSystem | StartProcess 创建；手工暂停/自动加工保留各自语义；成功 carry 只保留批准子图，失败重开清空；完成/取消释放锁一次；恢复按 ProcessId 重建全部双向索引 | input/anchor/container/station/worker、产品分配、材料 tombstone、版本与 event 水位同组；撤所有旧 process/lock 写路径。前厅 busy predicate、断线 cleanup、Snapshot/checkpoint 适配。正：manual pause/resume、automatic-active 恢复；负：重复锁、缺输入、旧 worker callback、重复完成产出 |
| Order/结算：S `_orders`, `_settlements`, `_consumedProducts`, `OrderState`, `SubmitOrder`；[CookingFrontOfHouse.cs](../../../src/AbilityKit.Game.Cooking/CookingFrontOfHouse.cs) 顾客与工作 | Level OrderEntity、SettlementLedger、front work；OrderTransactionSystem/FrontWorkSystem | 到店/询问开单；成功/失败换关清 Level-local 订单与历史，不把旧客户带新关；厨房可 carry；恢复验证 OrderId/模板/菜品、customer/work 外键、settlement 序号 | product 消费、订单 binding、计分、前厅交付、干净容器、dedup/event 同组。撤旧 orders/settlements 写入；前厅、评分、结算确认/store、exporter 适配。正：交付→receipt→冷恢复→续营；负：重复交付扣料/计分、旧订单、产品重复消费、重复确认 |
| 库存/供应：S partial [CookingRecipeSupply.cs](../../../src/AbilityKit.Game.Cooking/CookingRecipeSupply.cs) `_supply`, `_supplyOrigins`, `ExecuteSupply`；[CookingSupply.cs](../../../src/AbilityKit.Game.Cooking/CookingSupply.cs) `CookingSupplyState` | Kitchen SupplyLedger/Inventory capability；SupplyTransactionSystem | request/delivery/materialization 有稳定请求与 provenance；成功 handoff reopen 仅按既有规则、失败重新供应；取消/closing 禁新请求；restore 校验 origin 与 item/tombstone 图、request/delivery 索引 | package/units/containment、allocator、请求余额/到货、dedup/event 同组；撤两处独立可写 supply stores，规则函数可委托。准备厨房、Snapshot、checkpoint 验证器适配。正：request→receive→恢复重复领取；负：重复到货、allocation collision/异常、伪 provenance、closing 后领取 |
| Session： [CookingNetworkSessionHost.cs](../../../src/AbilityKit.Game.Cooking/Session/CookingNetworkSessionHost.cs) `Connection`, `Participant`, `_connections`, `_participants`, `_waiting`, `_cleanup`, `Join`, `CloseOwner` | Match Participant/SessionBinding owner（是否 ET 化另案）；SessionOwnerSystem 串行处理，业务提交只转 Host | ServerSessionInstance 启动新值；Join/rebind generation 递增；换关 scope 失效但 Match token 生命周期独立；close cancels sources 并安排业务 cleanup；冷重启新 server instance，不复用活 Connection；connection/player 索引重建 | 网络绑定、cleanup、Ready/Issued baseline 和 receipt 关联原子更新，不能创建第二 Session owner；旧 wrapper 单向转交。network callbacks、Client、acceptance raw-wire 适配。正：rebind→cleanup→exact baseline ACK/Ready；负：旧 token/instance/generation、关闭后回调、重复 close/release |
| 去重账本：S `_processedCommands` / `RecipeCommandKey` / fingerprint；H `_pending`, `_terminal`, `_history`；Session `_mapping`, `_domain` / `Mapping.Terminal` | Level CommandLedger + Session wire receipt ledger（不同职责，保留映射）；CommandAdmissionSystem/CommandTerminalSystem | admission 创建，terminal 永久记录该 scope 的结果；同关恢复带领域 ledger，成功换关按 handoff 清旧 Level 账本；Session receipt 不冒充 durable 账本；按 scope/player/stable 与 domain ID 查询 | payload fingerprint、domain ID、waiters、terminal、事件/扣料同组；撤对应旧 writer，不把三层 ledger 盲目合并。TryEnqueue/Map/retry/checkpoint consumers 适配。正：lost reply 同 stable/payload 返回缓存；负：同 key 不同 payload、旧 scope、队满、关闭未执行后的重试不得执行 |
| 事件水位：S `_events`, `_tickEvents`, `_eventSequence`, `Commit`, `CommitFixedTick`；H terminal notifications | Level EventJournal capability；各事务 System 在提交点写入，通知出口只读 | 已提交 command 与 fixed-step 各自写序号；same-level checkpoint 带全历史，成功 handoff 清 Level 历史；destroy 退订；恢复保存水位并继续单调；按 sequence 稳定排序 | 事件与造成事实的状态/version/dedup 一起提交，禁止通知失败回滚事实；撤旧 event writers。snapshot/canonical、诊断、session result、checkpoint 适配。正：恢复续写保持水位；负：重试新增业务事件、通知 sink 抛异常被当业务成功/失败、未批准裁剪 |
| 版本/分配器：S `_stateVersion`, `_nextProcessId`, `_nextProductId`, `_nextSettlementSequence`, item Version，H `HostFrameSequence`, `LastCommittedSimulationBatch` | 分别由 Kitchen/Level Counter/Allocator capability 持有；对应事务 System，唯一 LevelDriver 推时钟 | 创建初始计数；同关恢复保存确切值并验证已分配最大值；成功 carry 依当前 narrowed checkpoint，失败新 Level 按标准初始化；InstanceId 不复用业务 ID；typed ID lookup 重建 | 产品生成/供应/Process/结算/事件、版本与去重的事务闭包；撤旧计数写入，allocator interface 可为无状态规则。所有比较版本、ordering、exporter 消费者适配。正：保存恢复后继续分配无碰撞；负：溢出、重复 ID、旧版本写、fixed-step 失败推进 tick |

所有行共有生命周期检查：owner-thread 越权、重入、跨 Scene/scope 访问、候选树构造失败只释放候选、重复 Dispose 不重复业务效果、恢复后旧引用失效。实际候选安装入口是 H `InstallGeneration`/`InstallLevel`，先完整验证再发布；不能用影子双写做回退。全图比对采用顺序或独立进程，因为 EtRuntimeHost 限制一个进程只有一个 active host。

### 迁移施工出口（均未启动）

M1 先列所有 mutator/exporter，建立窄 authority seam；M2 以 Item/hand/container/关联 Process lock/allocator/订单引用为完整事务闭包，不能只搬 `_items`；M3 扩展 process/station/order/supply/front；M4 同一最终源重新跑完整菜单、自然结束、durable successor、独立进程四断点、cached retry、P6、ACK/Ready/close；M5 在反向消费者清单与替代验证完整后退役 facade。每切片撤旧 mutable store，旧 API 单向委托；回退是已验收提交与兼容 checkpoint，不能保留两路生产提交。Entity 数量、编译通过或 DTO 外观一致不足以证明迁移。

<a id="b"></a>

## B. 五类消息契约与真实时序

消息定义/边界来源：[CookingNetworkWireCodec.cs](../../../src/AbilityKit.Game.Cooking/Session/CookingNetworkWireCodec.cs)、[CookingNetworkAuthority.cs](../../../src/AbilityKit.Game.Cooking/CookingNetworkAuthority.cs)、H 与 S。不可变 record 不能单独保证深层集合不可变：wire decode/`Freeze`、Host 入队 fingerprint 和复制、exporter DTO 才组成隔离边界。字段可以通过明确 owner 上下文关联，禁止机械给所有 DTO 增添所有身份字段。

| 类别 | producer → consumer / 线程与 payload | 身份、顺序和幂等 | 重复、迟到、取消、异常 / 容量、观察 |
| --- | --- | --- | --- |
| Command 意图 | local/client `SendCommandAsync` → Session `Map` → Adapter `ConsumeFrame` → H `TryEnqueueNetwork` → S `Submit`；transport callback 只入冻结 bounded ingress，owner `ProcessOwnerFrame` 提交；WireCommand 包含 command、player、expected item version | wire stable ID 不等于 domain CommandId；domain ID 由 server instance/scope/player/stable 派生；correlation 属一次请求；ConnectionGeneration/ClientSequence 为连接水位；scope Match+Level，epoch 在 authority capture；Session 分配 ordinal/batch，H 网络按 ordinal 再 player/command 排序 | 同 stable/payload 返回缓存或折叠等待者，不重做业务；冲突结构化拒绝；旧 instance/generation/scope/sequence 拒绝；close cancels caller 并 cleanup；提交异常 terminal Cancelled，authority fault 明示。BusinessCapacity256/control32/perConnection64/MaximumPrefix256，command16KiB，ReceiptCapacity16384、waiters8（默认）。观察 ordinal/source/correlation、queue/high-water、stable/domain、fingerprint、admission/terminal |
| 准入/处理终态 | H `CookingLevelAdmissionResult` 与 `CookingLevelPendingDisposition` → Adapter `CookingNetworkAdmission`/`CookingNetworkDisposition` → Session `Complete`/WireResult → Client request Task；owner 建立终态 | admission Accepted 仅已入队；Executed 表示确实调用 Submit，Result.Outcome 仍可能 Rejected；Duplicate/Conflicted/Cancelled/Stale 分开。terminal key 为 scope/player/domain command，source/correlation 是 caller；wire result 带 stable/domain/reason/disposition/result | 同一业务身份 terminal 不重新执行；Result duplicate events 为空；取消未执行不得冒认提交；迟到 response 不满足新的 request/target；fault drain 保留原终态。H `_networkNotifications`/accepted callers 有界，历史不因满容量裁剪。观察 FinalDispositionHistory、Outcome/version、terminal client watermark、transport reply timestamp；Task 完成不是 projection |
| Domain Event 已提交事实 | S `Commit` 或 `CommitFixedTick` → command result/history/checkpoint/exporter；owner-thread 生成冻结 DTO，后续通知订阅者只读；当前未将 ET EventSystem 装为业务提交总线 | event sequence、command/domain ID、scope 由明确所属 Simulation/DTO context 关联；command 与 tick 的事实按已定义序号排序；不是 transport correlation。相同 stable/payload 不追加 event | 已提交事实不因监听抛异常回滚；需要参与决策的规则调用必须显式返回失败而非隔离异常广播；迟到订阅不改状态、退订/释放幂等。历史完整性与 checkpoint 上限按既有契约，禁止任意裁剪；观察 event/tick journal、水位、canonical/hash、cause |
| 完整 Projection/Baseline | H `CaptureReadOnlyFullState` → Session `Publish` → Client `TryInstallBaseline`；完整 State+Session 投影，不是事件增量或 resume checkpoint；只在 idle owner capture，客户端验证后 `Freeze` | Identity=server instance/participant/generation/scope/epoch/snapshot sequence/state hash/issue ID；LevelFormat8、RecipeSchema5 独立于 Wire3；hash 包括 typed State 与 Session；exact issued identity 为 ACK/Ready 的目标，sequence 单调 | client 拒绝旧/错 scope、identity/hash、schema、participant 水位；尚未 ACK 不覆盖 issued baseline；paused unchanged suppress 依实际代码；capture unavailable 清同步资格而非伪造状态。8MiB payload、depth32/token1048576/collection16384；观察完整图、version/frame、hash、Issued/awaitingAck、installed identity/Ready |
| Session control | Join/Joined/BaselineAck/Ready 经 wire；Rebind 是带 instance/token 的 Join，Close 由 transport disconnect→`CloseOwner`，不是虚构 Close/Rebind enum。Session owner 持有 binding；client ACK 在安装后发；owner exact compare 后 Ready | credential/participant，随机 server instance/token（不得写公开日志），generation、sequence、scope/epoch/issued identity；旧 physical Connection 不变新 participant；control 与 command 在 owner prefix 分阶段处理 | duplicate/stale ACK 不授 Ready；rebind 撤旧 active binding/cancel sources，cleanup 未完可 DeferredAck；close 重复无重复释放；旧 generation callback 禁止写新对象；Dispose 关闭 transport 不是业务结算。control4KiB、connections8、control queue32；观察 closed ordinal/cleanupPending、binding generation、ACK/Ready exact identity、drain/endpoint native exit |

### 普通操作：pickup（Running 且已 Ready）

1. Client `SendCommandAsync(stable, command)` 要求 `IsSynchronized`，从 binding/baseline 带 instance/generation/scope、递增 sequence、batch=0，用新的 correlation 发 Command。返回 Task 暂未完成；send 成功只证明传输调用。
2. Session `CreateHost` callback 严格解码并入 `_ingress`；`ProcessOwnerFrame` 消费最多 MaximumPrefix，处理 close/control 后 `Map` 检查当前 binding/Ready/scope/顺序/形状，建立 stable→domain mapping 与 waiter。队列入场不证明 terminal。
3. Adapter `ConsumeFrame` 校验 owner capture、cleanup 与批次，经 H `TryEnqueueNetwork` 准入。H `ExecuteFrameCore` freeze 排序，S `Submit` 验证版本/location，更新 Item/hand、dedup/version/event，H 立刻记录 Executed+Outcome。Outcome Accepted 才证明业务效果；准入 Accepted 单独不足。
4. H 调 S `AdvanceFixedTick`，成功后更新 HostFrameSequence，再前厅 Step，完成 frame；Adapter drain dispositions/capture。fixed-step 失败仍保留第3步效果和 terminal，见 C。
5. Session `Complete` 将 disposition 回 correlation（request Task 此时可完成）；`AcceptCapture` + `Publish` 发完整 baseline。普通 terminal 回包不是目标 projection 完成；必须比较 installed full graph/version 与目标 identity。
6. Client `TryInstallBaseline` 验证并冻结完整 State+Session 后发 BaselineAck；owner `CompleteAck` 仅接受 `ack == connection.Issued`，回 Ready；client 同 `LatestBaseline.Identity` 比较后同步。要证明此操作闭环，分别留 terminal、目标版本/完整图、hash、exact ACK/Ready；仅 IsSynchronized 或 elapsed 不足。

### 断线重试：submitted-reply-lost

1. 保存 stable ID 与原业务 payload；原命令可能已提交而 reply 丢失，不能凭 timeout 判未执行。保留原 send/ingress/admission/domain/terminal 因果证据。
2. Disconnect 触发 closed ordinal，owner `CloseOwner` 撤 active binding/cancel pending sources，并安排 worker cleanup；未执行 mapping 标 CancelledNoExecution，已提交 terminal 不删。新连接 `ReconnectAsync` 以原 server instance/token 发 Join；若冷重启 instance 改变，旧凭据必须拒绝，走独立 durable 恢复流程，不能冒认同实例 rebind。
3. owner `Join` 增 generation、清连接 sequence/Ready，发布当前完整 baseline；必要 cleanup 完成前 ACK 延后。安装、exact ACK/Ready 完成才允许发送重试。
4. 新 correlation、新 client sequence、当前 connection generation，**同 scope/stable/payload**：`Map` fingerprint match+Terminal 直接回缓存，duplicate result 的 Events 为空；无需再 Submit。pending 情况按有界 waiter 折叠；Cancelled 返回未执行，冲突拒绝；换 Level 后旧 scope 拒绝，不能把旧命令自动改 scope。
5. 比对扣料/settlement/event 水位/receipt 守恒，并独立证明当前完整 baseline 和 exact ACK/Ready、close/drain 与双方真实退出。generation2 恢复不证明 generation1 flight 在原 deadline 完成，单进程验证不证明真实双端/物理 LAN。

上述是源码时序，不是本轮跑过的验收。旧 [CookingSessionHost.cs](../../../src/AbilityKit.Game.Cooking/Session/CookingSessionHost.cs)、`CookingSessionAuthority`、UDP fixture 各有独立消费者；不能拿它们替代当前网络 acceptance 路径，亦不能未查消费者就删除。

<a id="c"></a>

## C. 分阶段事务、时钟和故障

当前 **command lane → fixed-step → front step → capture/publication** 分阶段执行，不是全帧回滚：H `ExecuteFrameCore` 对每组先 S `Submit` 并 `Terminalize`，随后 `AdvanceFixedTick`。S `BuildFixedTickPlan` 先校验/stage，`CommitFixedTick` 发布该 step。源码测试 `Fixed_step_failure_after_accepted_command_preserves_the_command_effect_and_final_history` 断言：process-started event、清空手持、ElapsedTicks0、Executed+Accepted 都保留，LogicalTick0/HostFrameSequence0 不推进。故障后取消尚未终态的 admitted group，Host faulted；Adapter 捕捉 authority fault、drain 原 dispositions，不能把已有 Executed 改写为 Cancelled 或补造成功 capture。

`reference/et-entity-tree.md` §8 的“validate complete frame / atomically commit once”是目标描述，与当前命令保留语义冲突；本文明确采用实际源与测试作为当前行为。整帧原子回滚、事件共用新水位或提交点改变须另建行为变更并审议，不能在迁移里隐藏。前厅 Step 在固定 step 成功后执行；本文不声称后续异常能回滚已推进时钟或已提交厨房。

| 时间/完成事实 | owner 与定义 | 不能推导的事实 |
| --- | --- | --- |
| LogicalTick | Simulation 成功 fixed-step 的逻辑时间，不含 wall-clock 等待；手工推进/自动加工遵守既有规则 | 不等于 HostFrameSequence 或网络 elapsed |
| HostFrameSequence | H 唯一 Driver 在厨房 `AdvanceFixedTick` 成功后赋值为 candidateFrame，赋值先于前厅 Step；后续前厅异常不撤销该计数；durable baseline 保存恢复时钟 | 不等于整个 frame 后续步骤均成功；不由 transport PollEvents、Unity Update 或诊断另加一拍 |
| SimulationBatch/ordinal | Session owner 冻结 batch/命令仲裁序号，LastCommittedSimulationBatch 独立保存 | 不等于客户端 sequence 或事件 sequence |
| 网络 deadline / 实际耗时 | acceptance/调用方单调 Stopwatch 等测量、cancel budget；用于超时判定 | timeout 不回滚已提交命令，不可提高原30s/600s边界美化结果 |
| Task 完成 / terminal / ACK / projection | 分别是本地异步请求完成、权威处理结果、exact issued 状态确认、客户端安装完整目标图 | 传输 ACK、业务接受、业务提交和客户端投影完成互不替代 |

owner-thread/reentry 检查由 H `Check`/mutation gates 与 EtRuntimeHost `Check` 执行。跨 await/池复用/恢复后重验 owner 存活、cancel、instance、generation/scope，重新解析 typed ID；旧 incarnation 的回调不能因相同业务 ID 写新对象。重复 stable+payload 不重复扣料、结算或 event；冲突/旧 scope 保持结构化拒绝。发布异常只影响通知/同步资格，不能假造业务失败/成功或删除原失败。

<a id="d"></a>

## D. 框架、协议与来源台账

状态分类：**接线**=源码被项目 Compile/Reference 纳入并有实际调用入口（本轮 fresh compile NotRun）；**编入未接线**=项目包含但当前 Cooking owner 未启用；**源/工具**=存在文件或导出器，不能证明应用 runtime；**提案**=未实现。历史构建证据只按其原 SHA/task 解释。本轮没有一项“新编译并调用通过”。

| 能力与状态 | 入口/声明闭包 | 来源、许可、宿主/消费者与退役条件 |
| --- | --- | --- |
| 提炼 ET Entity/System/Fiber/ETTask：接线 | [runtime csproj](../../../src/AbilityKit.ET.Runtime/AbilityKit.ET.Runtime.csproj) Compile Include UPM Core/Attributes/EtRuntimeHost，net10/C#9/unsafe/IsPackable=false；[EtRuntimeHost](../../../Unity/Packages/com.abilitykit.et.runtime/Runtime/EtRuntimeHost.cs) 安装 EntitySystem 并显式 Tick；[Cooking EtRuntime csproj](../../../src/AbilityKit.Game.Cooking.EtRuntime/AbilityKit.Game.Cooking.EtRuntime.csproj) 引用 runtime/analyzer | [notice](../../../Unity/Packages/com.abilitykit.et.runtime/THIRD-PARTY-NOTICES.md) 标记从 vendored core@3.0.3 与 sourcegenerator@3.0.1 裁剪/适配，提炼包0.1.0，82份 C#；实际副本以本仓 SHA/hash 身份固定，未证明统一 ET 上游 tag/commit。internal-only，[license](../../../Unity/Packages/com.abilitykit.et.runtime/LICENSE.ET-Core) 保留；.NET 与 UPM asmdef 闭包分别审查，Unity NotRun；Cooking 当前 owner 不退役，仅按后续纵切减少 facade |
| EventSystem/AEvent：编入未接线 | Core glob 编入 [EventSystem.cs](../../../Unity/Packages/com.abilitykit.et.runtime/Runtime/Core/World/EventSystem/EventSystem.cs)，EtRuntimeHost 未 AddSingleton EventSystem；[IEvent.cs](../../../Unity/Packages/com.abilitykit.et.runtime/Runtime/Core/World/EventSystem/IEvent.cs) 通知异常隔离 | 当前 vendor 来源/许可同上；类型/SceneType 分派不自动提供 room instance/transaction 隔离。新增应用接线须验证订阅、重入、异常、释放；不替换必须失败的业务调用 |
| 完整 ET network/mailbox/loader/scheduler/Timer：源/工具，Cooking 未接线 | 完整 [Demo Share csproj](../../../src/AbilityKit.Demo.ET.Share/AbilityKit.Demo.ET.Share.csproj) 与 vendored core；提炼 notice 明确省略；`FiberManager.Create` 仅完整 vendor | 不升级/复制整栈；Demo 独立闭包/消费者保留。选用先定位缺口、来源与许可、init failure/cancel/shutdown 有界负例；不能把静态等待风险说成 Cooking 已复现根因 |
| LiteNetLib / Network SDK / framing：接线 | [NetworkAcceptance Program](../../../src/AbilityKit.Game.Cooking.NetworkAcceptance/Program.cs) 装配 Session+Adapter+LiteNet；[LiteNetTransport](../../../Unity/Packages/com.abilitykit.network.transport.litenet/Runtime/Transport/LiteNetTransport.cs) ReliableOrdered；[header](../../../Unity/Packages/com.abilitykit.network.runtime/Runtime/Network/Protocol/NetworkPacketHeader.cs)、[frame codec](../../../Unity/Packages/com.abilitykit.network.runtime/Runtime/Network/Protocol/NetworkFrameCodec.cs) | UPM共享源码+src项目依赖；LiteNet实际 NuGet/vendor闭包需最终 restore 核对，不能从目录认定全部在用。保留当前 transport；退役需同版本 ET 网络有明确替代及消费者验证，不永久两套 Session owner |
| Cooking Wire3 JSON：接线 | Codec System.Text.Json；Host Encode/Decode、Client Encode/TryInstall，baseline State+Session hash；LevelFormat8、RecipeSchema5、durable major baseline3 独立 | schema 源为 typed DTO+codec，.NET application，Unity compatibility 未验。保持现格式，不同时改 checkpoint/log/report；编码替换须另案兼容矩阵 |
| catalog MemoryPack/custom-binary：接线的其他协议边界 | [protocol manifest](../../../Protocols/Generated/protocol-manifest.json)、[Protocols README](../../../Protocols/README.md)；4 catalogs88条：75 MemoryPack/13 custom-binary/0protobuf（MOBA17/9、Room51/0、Shooter7/2、System0/2） | manifest/generated 源与生成器唯一；Cooking 不在该88条。Unity MemoryPack1.10.0、src常见1.21.0、Orleans1.21.4 是声明差异，最终兼容未证明；不得机械拉齐或手改生成物 |
| ET .proto → MemoryPack：源/工具与 Demo 接线 | [Proto2CS.cs](../../../src/AbilityKit.Demo.ET.Share/src/cn.etetet.proto@3.0.2/DotNet~/Proto2CS.cs) 输出 MemoryPackable/MemoryPackOrder；[MessageSerializeHelper.cs](../../../src/AbilityKit.Demo.ET.Share/src/cn.etetet.core@3.0.3/Scripts/Core/Share/Network/MessageSerializeHelper.cs) 调 MemoryPackHelper | proto包装3.0.2，不等于 protobuf wire；Demo闭包/原许可独立，Cooking无接线；退役前验证Demo生成与消费者 |
| protobuf exporter：源/工具 | [EditorWorkflowCommands.cs](../../../tools/AbilityKit.Protocol.CatalogCompiler/EditorWorkflowCommands.cs) --export-protobuf；[ProtobufProtocolBackend.cs](../../../tools/AbilityKit.Protocol.CatalogCompiler/Emit/ProtobufProtocolBackend.cs)、[ProtocolBackendTests.cs](../../../src/AbilityKit.Protocol.CatalogCompiler.Tests/ProtocolBackendTests.cs) | 当前测试覆盖文本/文件，未执行protoc；package/type/import/uint8静态缺口待#9。Google.Protobuf DLL/工具存在不证明Cooking采用。不是ET迁移硬前置；采用须生成编译/runtime roundtrip、旧新互读/明确拒绝、missing/unknown/corrupt/limit与宿主矩阵 |
| Luban/YamlDotNet 配置：其他应用接线/工具 | [LubanConfig/Moba README](../../../LubanConfig/Moba/README.md) Excel→json/bin；Cooking正式 JSON 内容仍有独立定义 | generator/runtime工具二进制来源/tag/hash/notice仍需#7补齐，包装号不能替代上游；已有MOBA消费者不删除，不强迫Cooking所有配置转表；同输入重建/跨表引用/宿主比较后再采用 |
| World.DI / Microsoft.Extensions：其他宿主接线，Cooking替代策略提案 | [WorldContainer.cs](../../../Unity/Packages/com.abilitykit.world.di/Runtime/World/DI/WorldContainer.cs) borrowed/owned/seed/init/dispose；[Orleans Host csproj](../../../Server/Orleans/src/AbilityKit.Orleans.Host/AbilityKit.Orleans.Host.csproj) 平台Host | 保留旧消费者、冻结无必要扩张；替换须证明生命周期等价，ET/DI不可共同释放同一对象。平台宿主优先现成能力，薄适配无状态复用允许 |
| 日志/文件存储/SQLite：现有接线/SQLite提案 | [CookingMajorBaseline.cs](../../../src/AbilityKit.Game.Cooking/CookingMajorBaseline.cs)、[SettlementStore](../../../src/AbilityKit.Game.Cooking/CookingLevelSettlementStore.cs) 文件写入/rename；Host日志/诊断旁路 | 当前文件/hash不证明掉电原子性，也未证明丢数据；故障模型与结算幂等属应用。SQLite仅多记录事务有明确缺口时评估；ActivitySource/telemetry backend提案不驱动提交 |
| xUnit/Test SDK/Roslyn/analyzer：接线工具 | [test-gates.json](../../../tools/test-gates.json)，[EtRelationAnalyzer.cs](../../../src/AbilityKit.ET.RelationAnalyzer/EtRelationAnalyzer.cs) AKET001/002 | Analyzer只覆盖特定关系调用，不是全部owner/async风格防线；保持现框架，新增检查必须正负例。SDK无global.json/NuGet lock，UPM有lock；最终闭包需#7，不能宣称SDK固定 |
| EtBridge/World.ECS/Entitas/Svelto/Orleans/Flow/HFSM/Timer：保留独立消费者 | [CookingEtBridge](../../../src/AbilityKit.Game.Cooking.EtBridge/CookingEtBridge.cs) 旧Demo snapshot探针；[Projectile csproj](../../../src/AbilityKit.Combat.Projectile/AbilityKit.Combat.Projectile.csproj)；Server Orleans闭包 | 不因为Cooking全面ET就全仓清退；reverse-reference清单、替代验证和来源/许可先齐。Flow/HFSM/Timer不同语义不能按名称合并；Entitas1.5.0/1.14.2声明差异需闭包核验，未证实冲突 |

宿主闭包的具体差异：提炼 [Runtime asmdef](../../../Unity/Packages/com.abilitykit.et.runtime/Runtime/AbilityKit.ET.Runtime.asmdef) 的 `references=[]`、`noEngineReferences=true`、`allowUnsafeCode=true`、`autoReferenced=false`，与 SDK 项目的显式 Core/Attributes/EtRuntimeHost Compile Include 分别审阅，不能推断 Unity 已编译。LiteNet 的 [SDK csproj](../../../src/AbilityKit.Network.Transport.LiteNet/AbilityKit.Network.Transport.LiteNet.csproj) 明确引用 Network.Runtime、Network.Host 和 NuGet `LiteNetLib 2.1.4`；[UPM manifest](../../../Unity/Packages/com.abilitykit.network.transport.litenet/package.json) 只声明两个框架包 `0.1.0`，说明需另装 LiteNetLib.dll，没有给出 DLL 的已验证身份。声明版本不等于实际 restore/Unity 二进制闭包。

源身份必须逐副本记录官方 URL、tag/commit、文件/二进制 SHA-256、补丁、license/notice、宿主、消费者、负责人和发布范围。当前能证明的是固定仓库 SHA 下的文件与 notice；缺上游 commit/hash 不猜造，列入#7。保留 [release manifest](../../../tools/publish/release-manifest.json) allow-list 无 ET 的现状；IsPackable=false/前缀deny不足以证明传递发布闭包安全。internal-only边界继续有效，本文不批准分享/发布，也不判既有许可违法。

本轮实际读取字节的 SHA-256（文件名相对于仓库根；这些是本地副本身份，不是上游提交）：

| 文件 | SHA-256 |
| --- | --- |
| Unity/Packages/com.abilitykit.et.runtime/THIRD-PARTY-NOTICES.md | `98a038f0b503ae7a60141ca06a6a715ec10a45259afaedef8734e01f9d516867` |
| Unity/Packages/com.abilitykit.et.runtime/Runtime/EtRuntimeHost.cs | `b130d3fc1232fa5a3a1048e109881c8ae1ab47c18a2f3abc18ab4e27268d6c85` |
| src/AbilityKit.Game.Cooking.EtRuntime/CookingLevelEtHost.cs | `c2810be6a1e692fa3b51a939d75d58e9c9f417a95fcc174afc4e5bdd5d02f759` |
| src/AbilityKit.Demo.ET.Share/src/cn.etetet.core@3.0.3/package.json | `38dbee5e0f6e8ccd110a504a80b2d26080ef8eb756e0f65e58a0d64c17b0afc0` |

已知本地补丁是 notice 声明的裁剪及适配（保留 Entity/System/Fiber/ETTask，省略 serialization/network/mailbox/loader/scheduler）；不能把 notice 当逐文件补丁清单。完整上游 URL/tag 对应 commit、逐文件差异、generator/tool DLL hash 与各依赖负责人仍 Unknown/待#7核验，不以当前包装号填补。当前仓库树及已保存历史提炼 task 是复核起点；不能下载最新版重新解释旧副本。

分帧与编码分开：4-byte little-endian length prefix（含16-byte header，不含prefix）+ header + payload；header Flags:uint16/HeaderSize:uint16/OpCode:uint32/Seq:uint32/PayloadLength:uint32，HeaderSize16，Cooking opcode0x434F4F33。protobuf不提供此 framing，也不替代 canonical/hash。JSON拒绝未知/重复属性、缺required、整数枚举，depth32；payload配额与FrameReader FrameBytes+64、transport FrameBytes+4+header不同，不能合并成一个buffer数字。换库/格式不能同时替换日志/报告/存档。

<a id="e"></a>

## E. 长期规则 → gate / 人工 check

根 [AGENTS](../../../AGENTS.md) 只保留长期执行规则与唯一 progress 路由。下表不把待建检查称成已实现；本项仅文档检查，所有 runtime gates NotRun。源文件共享并不证明Mono/IL2CPP/AOT兼容。

检查责任：本地 Orca 实施者保存每次 diff、调用路径及实际命令证据；dot 审阅者按下表人工 check 核对范围、行为前后对照与完整性。未来机器检查的有界任务归属分别为 #6（门禁状态与正负控制）、#7（来源/依赖/CI/发布闭包）、#8（领域 owner 纵切与撤销旧 writer）、#9（协议导出与兼容矩阵）。这是责任路由，尚未指定上述 blocked Issue 的执行者，也不授予启动权限；获准后对应 task 必须登记实际 owner 与证据。

| 规则 | 现有机器入口 / 当前不足 | 明确人工check与后续正负控制 |
| --- | --- | --- |
| 固定当前ET，无隐式升级 | `node tools/publish/audit-versions.js` 仅包装版本cohort，不证明ET来源闭包；版本/source manifest待#7 | diff检查package/csproj/asmdef/notice/vendor与baseline，无运行时/版本修改；后续已知副本正例与改tag/hash负例 |
| 单一writer/释放者、生命周期与DTO边界 | cooking-et-level-runtime / cooking-kitchen-loop；AKET001/002只有关系范围，不能证明无双写 | 按A表列前后stores/mutators/exporters/indices/consumer；只读查询、DTO导出、无状态库合法。真实host正例+越权/双写/旧EntityRef/重复dispose/旧callback负例；新architecture gate待#8/#6 |
| 五类消息/唯一时钟/分阶段事务 | runtime-contracts、network-sdk、Cooking gates及现有fixed-step failure测试源码；未覆盖所有新切片 | 按B/C前后对照身份、提交点、terminal/projection/ACK；same stable retry守恒、conflict/stale/fault/reentry负例，禁止通知异常冒认提交 |
| 基础设施复用与退出条件 | 无通用自动选型gate | ADR人工核对现框架→BCL→既有适配→成熟依赖的缺口、拒绝理由、宿主/维护人/回退/退役；薄业务适配不强制新增框架；第二机制需具体批准 |
| schema唯一、生成物不可手改 | `tools/compile-protocol-catalogs.ps1 -Check`、`tools/export-protocol-wire.ps1 -Projects shooter,moba -Check -Strict`；不证明所有Cooking/Unity兼容 | 源/生成器/输出 diff 与独立版本前后表；旧新读取或明确拒绝、missing/unknown/corrupt/oversize/runtime宿主正负矩阵；protobuf protoc+编译+roundtrip待#9 |
| Passed/Failed/Blocked/Skipped/NotRun 与真实覆盖 | tools/test-gates.json；[Unity compile helper](../../../tools/run-unity-compile-check.ps1) 缺DLL exit0而 [父gate](../../../tools/run_test_gate.ps1) 按exit标Passed，修正待#6；根配置引用workflow缺失待#7 | 人工将缺环境标Skipped/Blocked，零/旧结果/错SHA不接受；证据记录actual command/exit/source dirty/test counts/artifact identity/raw log。后续成功/真实失败/缺工具/零测试/旧产物正负控制；本轮.NET/Unity NotRun |
| internal-only/依赖来源与跨宿主 | `node tools/publish/release.js` 默认dry-run仅候选，不批准发布；传递限制gate待#7 | 人工review source/patch/license/适用host与publish闭包；已允许包正例，fixture引入ET直接/传递依赖负例；无global.json/lock不假称版本锁定 |
| dot审阅/Orca实施/任务依赖 | Orca issue/context只提供调度事实，不提供范围授权 | 核对最新Issue正文/标签/依赖/active worker与source-impact；#1–#4/#6–#9 blocked；ready标签不授权升级/合并/发布；后续逐案明确批准 |

完整后续验收继续保存command366原失败、P6原30s边界、四断点完整图、自然收尾与durable successor证据；不提高deadline、不缩菜单/负载、不补造terminal/ACK、不裁剪业务历史。性能timer/native scheduling延期、physical two-PC NOT_VERIFIED、Unity/S15后置。文档契约落地不是这些出口通过。
