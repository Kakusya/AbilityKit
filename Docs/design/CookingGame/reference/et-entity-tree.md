# Cooking ET Entity/Component 树参考

> 性质：设计参考，不表示完整 ET 树已经实现或验证。目标是统一 `[ComponentOf]`、`[ChildOf]`、命名和生命周期所有权，避免后续 task 边实现边发明结构。

## 1. ET 关系规则

### 1.1 Component Entity

Component Entity 必须：

- 直接继承 `ET.Entity`；
- 标记 `[ComponentOf(typeof(OwnerType))]`；
- 类型名以 `Component` 结尾；
- 通过 `owner.AddComponent<T>()` 创建；
- 表示 Owner 唯一的能力、状态面、索引或服务入口；
- 不具有脱离 Owner 的独立业务生命周期。

示例：

```csharp
[ComponentOf(typeof(CookingMatchEntity))]
public sealed class CookingMatchLifecycleComponent : Entity, IAwake
{
}
```

ET 每个 Owner 对每种 Component 类型最多保存一个实例。Component 与 Owner 共用 Entity Id，Owner Dispose 时递归 Dispose。

### 1.2 Child Entity

Child Entity 必须：

- 直接继承 `ET.Entity`；
- 标记 `[ChildOf(typeof(ParentType))]`；
- 类型名以 `Entity` 结尾；
- 通过 `parent.AddChild<T>()` 创建；
- 表示有独立身份、独立生命周期或同类多实例的对象。

示例：

```csharp
[ChildOf(typeof(CookingMatchEntity))]
public sealed class CookingConnectionEntity : Entity, IAwake
{
}
```

Child 有独立 ET Entity Id；Parent Dispose 时递归 Dispose Child。

### 1.3 Analyzer 与运行时边界

现有 ET 3.0.3 的 `[ComponentOf]`/`[ChildOf]` parent 匹配主要由 Source Generator Analyzer 在编译期检查，Core `AddComponent`/`AddChild` 不依据这些 Attribute 做完整运行时校验。

因此：

- 必须启用并验证对应 Analyzer；
- 测试应检查关键树关系和递归释放；
- 不能把“写了 Attribute”当作运行时安全证明；
- 不应建立多层业务 Entity 基类，业务 Entity 直接继承 `Entity`。

错误：

```csharp
public abstract class CookingEntityBase : Entity
{
}

public sealed class CookingMatchEntity : CookingEntityBase
{
}
```

正确：

```csharp
[ComponentOf(typeof(Scene))]
public sealed class CookingApplicationComponent : Entity
{
}

[ComponentOf(typeof(CookingApplicationComponent))]
public sealed class CookingMatchRegistryComponent : Entity
{
}

[ChildOf(typeof(CookingMatchRegistryComponent))]
public sealed class CookingMatchEntity : Entity
{
}
```

## 2. Entity 与 Component 选择规则

已确认采用“按能力拆分 + 基数决定关系”的统一规则：

- 某个 Owner 下零或一个、没有独立多实例身份、生命周期完全从属于 Owner 的子对象或能力，使用 `[ComponentOf]`，类型名以 `Component` 结尾。
- 某个 Owner 下可能存在多个、需要独立业务 ID、独立生命周期或独立引用的子对象，使用 `[ChildOf]`，类型名以 `Entity` 结尾。
- 多实例 Entity 不直接混挂在 Owner 下，而是挂在对应的 typed Registry/Book Component 下，既表达能力边界，也隔离 `Entity.Id` 数值空间。
- Component 按能力拆分；总是一起验证和原子变化的字段保持在同一个 Component，不能机械地每字段一个 Component。
- 不相关、可选、由不同 System 处理或生命周期不同的能力应拆成不同 Component。
- 普通值对象、配置引用、一次性命令、事件 DTO、snapshot DTO 和文件 envelope 不因“是对象”就自动成为 Entity。

示例：

```text
CookingItemEntity
├── CookingItemDefinitionComponent       exactly one definition reference
├── CookingItemLocationComponent         exactly one authoritative location
├── CookingItemLifecycleComponent        exactly one lifecycle/version state
└── CookingContainerComponent            optional zero-or-one capability

CookingKitchenComponent
├── CookingItemRegistryComponent         exactly one item-management capability
│   └── CookingItemEntity *               zero-to-many independently identified items
└── CookingProcessRegistryComponent      exactly one process-management capability
    └── CookingProcessEntity *            zero-to-many independently identified processes
```

使用 Child Entity，当对象满足任一条件：

- 同类可能存在多个实例；
- 有稳定运行时身份；
- 有独立出生、暂停、恢复或销毁时间；
- 需要挂载多个 Component；
- 需要被其他对象以稳定业务 ID 或 `EntityRef` 引用；
- 需要单独参与快照、诊断或生命周期测试。

使用 Component Entity，当状态满足以下特征：

- 是 Owner 的唯一能力或唯一状态面；
- 同一 Owner 不需要多个同类型实例；
- 离开 Owner 没有独立意义；
- 不应被当成单独业务对象；
- 生命周期必须完全从属于 Owner。

倾向于 Entity：

```text
MatchmakingTicket
Match
Participant
Connection
SaveCandidate
SaveTransfer
SaveResult
SaveClaim
Station
Item
Process
LevelParticipant
Order
```

倾向于唯一 Component 对象：

```text
Application
LoadedSave
RestaurantRuntime
Kitchen
Level
```

持久化 `Profile`/`SaveSlot` 默认不是 ET Entity；它们是平台 repository 中的文件/记录，PC 与 Android 的实际根目录由 host adapter 提供。只有被 Match 选中并完成读取、传输和校验后，才创建 Match-scoped `CookingLoadedSaveComponent`。绝对路径不得进入 Component、snapshot 或 wire DTO；存储细节见 [`save-storage.md`](save-storage.md)。

倾向于 Component：

```text
Identity/scope metadata when not derivable from Entity.Id alone
Definition reference
Lifecycle/version
Location/ownership reference
Authority/policy
Clock
Queue
Registry/Book/Slot
Handshake
Synchronization
Binding/Reference
Preparation
Objective
Settlement
Diagnostics
Progress
Deadline
Container capability
SnapshotWatermark
```

## 3. 树只表达生命周期所有权

ET Parent/Child 不得替代领域关系、网络身份或存档关系。

以下变化不得使用 reparent 表达：

- 玩家拾取 Item；
- Item 放入 Container；
- Process 迁移到升级后的 Station；
- Connection 绑定或解绑 Participant；
- Match 选择某个 SaveSlot；
- Participant 选择角色；
- Level 使用某个配置 Definition。

这些关系使用稳定业务 ID、不可变引用 DTO 或受控 `EntityRef<T>` Component 表示。

错误：

```text
ParticipantEntity
└── ItemEntity             because player picked it up
```

正确：

```text
CookingKitchenComponent
├── CookingItemRegistryComponent
│   └── CookingItemEntity
│       └── CookingItemLocationComponent(PlayerId / StationId / ItemId container)
├── CookingStationRegistryComponent
│   ├── CookingStationEntity V1
│   └── CookingStationEntity V2
└── CookingProcessRegistryComponent
    └── CookingProcessEntity
        └── CookingProcessStationReferenceComponent = Station V2
```

## 4. 业务身份与 Entity.Id

已确认可以在必要时复用业务 ID 数值作为 ET `Entity.Id`，但唯一来源必须是领域 ID 分配/恢复契约，而不是先生成任意 ET Id 再把它解释成业务身份。

- Child Entity 使用强类型业务 ID，例如 `ItemId`、`ProcessId`、`OrderId`；其底层为正 `long`，0 保留为无效值。
- 创建新 Child 实例时由对应 Registry 的权威 allocator 单调分配，检测重复与溢出。
- 创建 ET Child 时使用显式 Id API，使 `Entity.Id == StrongDomainId.Value`。
- 唯一 Component 对象可以拥有独立的强类型业务 ID 字段，例如 RuntimeId、LevelId；但 ET Component 按框架规则与 Owner 共用 `Entity.Id`，不能为了复用数值再包装一层唯一 Entity。
- 从 Save/Checkpoint 重建多实例 Child 时恢复原业务 ID/Entity.Id 数值，并让 allocator 跳到已恢复最大值之后；唯一 Component 则恢复其业务 ID 字段。
- DTO 使用强类型业务 ID，并携带足够的 Match/Runtime/Level scope；禁止暴露名为裸 `EntityId` 的无类型字段，也禁止传输 `Entity.InstanceId` 或 `EntityRef<T>`。

ET 的 `ChildrenCollection` 在同一直接 Parent 下仅以 `Entity.Id` 为键，不区分 Child 类型。因此已确认用**类型化 Registry/Slot Component**落实“按父级类型唯一”：

```text
CookingKitchenComponent
├── CookingStationRegistryComponent
│   └── CookingStationEntity *       StationId sequence
├── CookingItemRegistryComponent
│   └── CookingItemEntity *          ItemId sequence
└── CookingProcessRegistryComponent
    └── CookingProcessEntity *       ProcessId sequence
```

Registry Component 是其 Owner 的唯一能力面，同时作为该类型 Child Entity 的直接 Parent。这样 `ItemId(7)` 与 `ProcessId(7)` 可以共存，因为它们位于不同直接 Parent 下；同一 Registry 内禁止重复数值。

相同规则适用于 Application、Match、Runtime 和 Level 中的多实例或唯一类型：

```text
Application
├── MatchmakingTicketRegistryComponent -> MatchmakingTicketEntity *
└── MatchRegistryComponent              -> MatchEntity *

Match
├── ParticipantRegistryComponent -> ParticipantEntity *
├── ConnectionRegistryComponent  -> ConnectionEntity *
├── SaveCandidateRegistryComponent -> SaveCandidateEntity *
├── SaveTransferRegistryComponent  -> SaveTransferEntity *
└── SaveResultRegistryComponent     -> SaveResultEntity *

Level
├── LevelParticipantRegistryComponent -> LevelParticipantEntity *
└── OrderBookComponent                 -> OrderEntity *
```

唯一子对象直接使用 Component，不再使用 slot Component 包装唯一 Entity：

```text
Scene
└── ApplicationComponent

Match
├── LoadedSaveComponent        zero or one
└── RestaurantRuntimeComponent zero or one

RestaurantRuntimeComponent
├── KitchenComponent           exactly one while open
└── LevelComponent             zero or one unfinished level
```

这些 Component 可以包含独立的强类型业务 ID 字段，但它们的 ET `Entity.Id` 与 Owner 相同。生命周期边界通过 AddComponent/RemoveComponent 和业务状态机表达；不存在唯一 Entity 的 reparent。

## 5. 已确认的目标结构

标记：

- `[E]`：`[ChildOf]`，类型名以 `Entity` 结尾；
- `[C]`：`[ComponentOf]`，类型名以 `Component` 结尾；
- `-->`：引用/绑定，不是父子关系。

```text
ET Scene
└── [C] CookingApplicationComponent
    ├── [C] CookingConfigurationComponent
    ├── [C] CookingProfileRepositoryComponent
    │       local account access + lightweight Profile/SaveSlot metadata;
    │       persistent files are not ET Entities by default
    ├── [C] CookingMatchmakingServiceComponent
    ├── [C] CookingTransportServiceComponent
    │
    ├── [C] CookingMatchmakingTicketRegistryComponent
    │   └── [E] CookingMatchmakingTicketEntity *
    │       ├── [C] CookingMatchmakingRequestComponent
    │       └── [C] CookingMatchmakingStateComponent
    │
    └── [C] CookingMatchRegistryComponent
        └── [E] CookingMatchEntity *
            ├── [C] CookingMatchIdentityComponent
            ├── [C] CookingMatchLifecycleComponent
            ├── [C] CookingMatchAuthorityComponent
            ├── [C] CookingMatchPolicyComponent
            ├── [C] CookingSelectedSaveComponent
            │
            ├── [C] CookingParticipantRegistryComponent
            │   └── [E] CookingParticipantEntity *
            │       ├── [C] CookingParticipantIdentityComponent
            │       ├── [C] CookingParticipantStateComponent
            │       ├── [C] CookingProfileReferenceComponent
            │       ├── [C] CookingConnectionReferenceComponent
            │       └── [C] CookingReadyStateComponent
            │
            ├── [C] CookingConnectionRegistryComponent
            │   └── [E] CookingConnectionEntity *
            │       ├── [C] CookingTransportBindingComponent
            │       ├── [C] CookingHandshakeComponent
            │       ├── [C] CookingConnectionStateComponent
            │       ├── [C] CookingParticipantBindingComponent
            │       └── [C] CookingSynchronizationComponent
            │
            ├── [C] CookingSaveCandidateRegistryComponent
            │   └── [E] CookingSaveCandidateEntity *
            │       ├── [C] CookingSaveCandidateOwnerComponent
            │       ├── [C] CookingSaveCandidateMetadataComponent
            │       └── [C] CookingSaveCandidateAvailabilityComponent
            │
            ├── [C] CookingSaveTransferRegistryComponent
            │   └── [E] CookingSaveTransferEntity *
            │       ├── [C] CookingSaveTransferIdentityComponent
            │       ├── [C] CookingSaveTransferSourceComponent
            │       ├── [C] CookingSaveTransferTargetComponent
            │       ├── [C] CookingSaveTransferStateComponent
            │       ├── [C] CookingSaveValidationComponent
            │       └── [C] CookingSaveTransferPayloadComponent
            │
            ├── [C] CookingLoadedSaveComponent                 zero or one
            │       LoadedSaveId/Owner/Revision/Integrity/Payload capabilities
            │
            ├── [C] CookingSaveResultRegistryComponent
            │   └── [E] CookingSaveResultEntity *
            │       ├── [C] CookingSaveResultIdentityComponent
            │       ├── [C] CookingSaveResultPayloadComponent
            │       ├── [C] CookingSaveResultRosterComponent
            │       ├── [C] CookingSaveResultDeliveryComponent
            │       └── [C] CookingSaveClaimRegistryComponent
            │           └── [E] CookingSaveClaimEntity *
            │               ├── [C] CookingSaveClaimIdentityComponent
            │               ├── [C] CookingSaveClaimRecipientComponent
            │               ├── [C] CookingSaveClaimCredentialComponent
            │               ├── [C] CookingSaveClaimDeliveryComponent
            │               └── [C] CookingSaveClaimReceiptComponent
            │
            └── [C] CookingRestaurantRuntimeComponent          zero or one
                ├── RuntimeId/source-save/lifecycle/progress/settlement capabilities
                ├── [C] CookingKitchenComponent                exactly one while runtime open
                │   ├── KitchenId/authority/clock/command-queue capabilities
                │   ├── [C] CookingStationRegistryComponent
                │   │   └── [E] CookingStationEntity *
                │   ├── [C] CookingItemRegistryComponent
                │   │   └── [E] CookingItemEntity *
                │   │       └── [C] CookingContainerComponent   optional capability
                │   └── [C] CookingProcessRegistryComponent
                │       └── [E] CookingProcessEntity *
                │
                └── [C] CookingLevelComponent                  zero or one unfinished level
                    ├── LevelId/definition/lifecycle/preparation/authority/clock capabilities
                    ├── command-batch/objective/settlement/snapshot-watermark/driver capabilities
                    ├── [C] CookingLevelParticipantRegistryComponent
                    │   └── [E] CookingLevelParticipantEntity *
                    └── [C] CookingOrderBookComponent
                        └── [E] CookingOrderEntity *
```

唯一 Component 对象可以按能力继续拆分内部状态，但这些从属能力仍以 Component 形式存在；实现时不得为了视觉上的“层级”重新包装成唯一 Child Entity。例如 `CookingRestaurantRuntimeComponent` 可以拥有 lifecycle、source binding、progress staging 等独立 Component，也可以在字段总是原子变化时保留于同一能力 Component，最终以实现 task 的可测原子边界为准。

活 `CookingRestaurantRuntimeComponent` 不能从 Match A 移动到 Match B。跨 Match 只导出稳定 Save/未来 Checkpoint DTO；Match B 创建新的 Runtime Component，并恢复新的运行代际。

## 6. 已确认的数量与生命周期约束

### Match

- Match 从玩家建立联机协作关系持续到明确解散。
- Match 可以跨存档选择、新建存档和多个 Level。
- 一个 Match 同时最多拥有一个 LoadedSaveComponent 和一份未关闭 RestaurantRuntimeComponent。
- 每个成功 Level 可以在 Match 下产生一个独立 SaveResultEntity；每个 eligible Profile 对应一个 SaveClaimEntity，负责凭证、持久化 ACK、幂等重发和本地领取 receipt 状态。
- SaveResult/Claim 是 Match 生命周期内的网络交付实体；Match 终止前需先导出已持久化离线凭证所需 DTO，不能把活 Entity 带出 Match。
- ParticipantEntity 与 ConnectionEntity 的生命周期都归属于 Match，但直接 ET parent 分别是 `CookingParticipantRegistryComponent` 与 `CookingConnectionRegistryComponent`；二者通过引用 Component 绑定，Connection Dispose 不得递归销毁 Participant。
- Match 不等于 Connection、SaveSlot、RestaurantRuntime 或 Level。

### RestaurantRuntime

- 由选中存档或新存档模板实例化。
- 存档 Owner 与 listen host 解耦。
- 一个 Match 不得保留多份暂停 Runtime。
- 切换存档前必须显式关闭当前 Runtime。
- 一个 Runtime 同时最多有一个未结束 Level。

### Kitchen

- 属于 RestaurantRuntime，并与 Level 并列。
- 成功 Level 之间保留物品、容器、半成品和加工进度。
- 失败重试按标准初始供应重建。
- Level 间升级时加工暂停，Process 原子迁移并保留 elapsed progress。

### Level

- 与 Match 解耦，canonical scope 包含 MatchId、RestaurantRuntimeId、LevelId 和独立 LevelEpoch。
- 主状态为 `Created -> Preparing -> Ready -> Running <-> Paused -> Ending -> Ended`；Success/Failed/Aborted 是结束 Outcome。
- 失败重试复用 LevelId 但增加 LevelEpoch；成功下一关使用新 LevelId/更高 Epoch。
- Pause 保留同一个 Level Component、权威状态及暂停前已经准入的 pending/frozen commands，只停止 Tick；Paused 期间新 gameplay command 结构化拒绝且不入队。
- Pause/Resume 保持 LevelEpoch、HostFrameSequence、LogicalTick、batch/snapshot/event watermark 原值。Resume 后旧命令继续稳定仲裁并接受普通 stale/版本验证。
- 失败进入 Ending 时取消旧 Epoch 的全部 pending/frozen commands；重试复用 LevelId、增加 LevelEpoch，重建 Level-local Tick/batch/snapshot/event 水位。Match/Application HostFrameSequence 跨重试持续单调。
- Ended Level 不原地复活；重试/下一关均重新初始化 Level Component 的新运行代际。

### Kitchen gameplay entities

- 可携带锅、盘等容器统一使用 `CookingItemEntity`，通过可选 `CookingContainerComponent` 表达容量和 contents 能力；不再建立独立 `CookingContainerEntity`，避免一个物体拥有 Item/Container 两套身份和位置。
- 固定工位使用 `CookingStationEntity`，生命周期由 Kitchen 拥有，直接 ET parent 是 `CookingStationRegistryComponent`。
- `CookingProcessEntity` 生命周期由 Kitchen 拥有，直接 ET parent 是 `CookingProcessRegistryComponent`；通过 `ProcessStationReferenceComponent`、input/recipe references 关联工位、输入和配方，工位升级只改变引用，不 reparent Process。
- 运行时 `CookingOrderEntity` 是当前 Level 的 Child；`CookingOrderBookComponent` 负责索引、配置引用和生成策略，不用一个集合记录取代所有订单实体。Level Dispose 回收本关订单。
- `CookingParticipantEntity` 在 Match 生命周期内存在；Level 启动冻结 roster 后，为每位成员创建 `CookingLevelParticipantEntity`，通过 Component 引用 Match Participant，并保存本关角色、avatar、资格和结果关联。Level 结束时释放这些投影，不销毁 Match Participant。

### Component 粒度示例

```text
CookingStationEntity
├── StationDefinitionComponent
├── StationLifecycleComponent
├── StationCapabilityComponent
└── StationOccupancyComponent

CookingProcessEntity
├── ProcessRecipeComponent
├── ProcessInputReferenceComponent
├── ProcessStationReferenceComponent
└── ProcessProgressComponent

CookingOrderEntity
├── OrderDefinitionComponent
├── OrderLifecycleComponent
├── OrderRequirementComponent
├── OrderDeadlineComponent
└── OrderSettlementComponent
```

这些是目标能力边界参考，不等于当前 task 必须一次创建全部类型。实现 task 可以在不改变已确认语义的前提下合并总是原子变化的字段，但不得退化为包含所有不相关能力的巨型 `XxxStateComponent`，也不得把每个标量机械拆成独立 Component。

## 7. Authority 迁移阶段

已确认最终方向：Item、Station、Process、Order 等运行时领域状态最终迁入 ET Entity/Component 树，ET 成为应用领域权威，而不是永久只做 `CookingRecipeSimulation` 外壳或只读投影。

迁移必须分阶段保持单一可写权威：

```text
Phase A
  CookingRecipeSimulation is the only mutable gameplay authority
  ET owns lifecycle + LevelDriver seam only

Phase B..N
  migrate one aggregate/state family into ET Entity/Component
  add equivalent validation/atomic commit/snapshot tests
  remove the corresponding mutable Simulation state in the same migration closure

Final
  ET Entity/Component tree is the gameplay authority
  legacy CookingRecipeSimulation mutable dictionaries are retired
```

当前 `CookingRecipeSimulation` 仍是物品、Process、容器、订单、版本、事件和命令 ledger 的唯一权威 owner。当前 fixed-tick task 只能处于 Phase A，不得提前创建可写 Item/Station/Process/Order ET 状态。

在迁移完成前，推荐阶段是：

```text
CookingLevelComponent
└── CookingLevelDriverComponent
      -> reference current CookingRecipeSimulation during Phase A
```

不得同时让以下两边独立可写：

```text
CookingRecipeSimulation._items/_processes/...
```

和：

```text
CookingItemEntity
CookingProcessEntity
CookingStationEntity
```

Item/Process/Station ET Entity 在明确 checkpoint、快照、引用重建、原子提交和同步协议之前，只能是只读 projection，或者完全不创建。迁移某一类状态时，必须在同一迁移闭环中删除旧 Simulation 对应可写状态；禁止任何阶段形成长期双权威。

最终 Snapshot、网络同步和存档数据使用专用 exporter 从当前权威 ET 树按稳定业务 ID 和明确排序导出纯 DTO。禁止直接序列化 ET Parent/Child/Component tree，也禁止把 `Entity.InstanceId`、`EntityRef<T>` 或运行时 parent 结构写入 wire/save schema。

## 8. Driver 与 System 约定

固定 Tick 采用少数 aggregate/root driver 批处理，不让每个 Item、Process 或 Order 独立 `IUpdate`。

如果 Tick driver 是 Level 的唯一调度能力，它应当是 Level Component 的子 Component：

```csharp
[ComponentOf(typeof(CookingLevelComponent))]
public sealed class CookingLevelDriverComponent : Entity, IAwake, IUpdate
{
}
```

不应继续使用无法表达关系的名称：

```csharp
public sealed class CookingRecipeDriver : Entity
{
}
```

Driver 只负责调度：

```text
freeze command batch
  -> stable arbitration
  -> snapshot/sort affected entities by stable domain identity
  -> stage aggregate mutations and generated identities
  -> validate the complete frame
  -> atomically commit once
  -> export immutable frame result/events
```

Item/Process/Order System 可以提供 `Awake`、`Destroy` 或由 root driver 显式调用的批处理逻辑，但不得各自通过 `IUpdate` 独立推进同一个逻辑帧。这样可以维持单一 Tick、水位、稳定排序和整帧原子性。

Driver 不复制 Item、Process、Order、Save 或网络状态。

## 9. 引用、命令、事件与 Checkpoint

### 9.1 Entity 引用

权威领域关系保存强类型业务 ID，而不是把 `EntityRef<T>` 作为唯一事实来源。

```text
ProcessStationReferenceComponent -> StationId
ProcessInputReferenceComponent   -> ItemId
ConnectionBindingComponent       -> ParticipantId
LevelParticipantReferenceComponent -> ParticipantId
```

同进程可以缓存 `EntityRef<T>` 以减少 Registry 查询，但缓存必须满足：

- 可从业务 ID 和 typed Registry 重新解析；
- Entity Dispose、树重建、Checkpoint 恢复或代际切换后失效并重新绑定；
- 提交前验证业务 scope、目标存活和 InstanceId；
- 不进入 Snapshot、Save、Checkpoint 或 wire DTO；
- 缓存缺失不能改变领域结果，只影响查询成本。

### 9.2 命令

一次性玩家命令使用不可变 DTO/value object，进入 Kitchen/Level 的 CommandQueueComponent。命令不是 Entity，不为每次请求创建和销毁 `CookingCommandEntity`。

```text
input/network adapter
  -> immutable command DTO
  -> bounded queue
  -> freeze one batch
  -> stable sort/dedup/conflict validation
  -> aggregate frame transaction
```

只有具有跨帧独立身份和生命周期的持续过程才建模为 Entity，例如 `CookingProcessEntity`；不能把一次性 command envelope 和持续 Process 混为一类。

### 9.3 事件

领域事件和同步事件是 frame commit 生成的不可变 DTO，写入有单调水位的 `CookingEventJournalComponent` 或直接导出流，不为每个历史事件创建 `EventEntity`。

事件日志必须：

- 按 commit sequence 稳定排序；
- command event 与 fixed-tick event 共用已定义的事件水位；
- 支持 snapshot/sync exporter 按 watermark 导出；
- 具有明确的保留、裁剪和 checkpoint watermark 策略；
- 裁剪历史不改变当前权威 Entity 状态。

### 9.4 Checkpoint

Checkpoint 是从权威树稳定导出的不可变纯 DTO/artifact，不是活 ET Entity，也不直接序列化 Parent/Child/Component tree。

恢复流程：

```text
read immutable checkpoint artifact
  -> validate schema/configuration/scope/integrity
  -> validate all typed IDs and references
  -> create a new Match/Runtime/Level tree
  -> restore original strong domain ID values into Entity.Id where applicable
  -> rebuild typed Registry indexes and EntityRef caches
  -> advance each allocator beyond restored maxima
  -> publish a new synchronization baseline
```

失败恢复不得留下半棵权威树；必须先完整 stage/validate，再一次发布新树，或销毁未发布的候选树。

## 10. 正常解散与 Dispose 顺序

正常 Match 解散必须先完成业务收尾，再使用 ET 递归 Dispose：

```text
close admission for new Match/Level/save operations
  -> freeze/cancel pending gameplay commands with explicit dispositions
  -> end or abort the current Level
  -> close RestaurantRuntime and resolve allowed staged progress
  -> finish SaveResult/SaveClaim export and delivery state
  -> send/persist normal-close receipts where observable
  -> close Connection entities
  -> close/remove RestaurantRuntime and LoadedSave Components
  -> dispose SaveResult/SaveClaim trees
  -> dispose Participant entities
  -> dispose Match entity
```

如果收尾中途失败，必须报告结构化 fault/abnormal termination，不能继续宣称正常解散。直接 `Dispose(CookingMatchEntity)` 只能作为异常资源回收安全网，不能代替 Level 结束、Runtime 关闭、SaveResult/Claim 导出、normal-close receipt 或 pending disposition。

Participant 与 Connection 并列，因此关闭 Connection 不会递归销毁 Participant；Match 最终 Dispose 才统一回收两者。

## 11. 禁止模式

### 11.1 名称与 Attribute 不一致

禁止：

```csharp
[ChildOf(typeof(CookingItemRegistryComponent))]
public sealed class CookingItemComponent : Entity
{
}
```

正确：

```csharp
[ChildOf(typeof(CookingItemRegistryComponent))]
public sealed class CookingItemEntity : Entity
{
}
```

### 11.2 Entity 冒充 Component

现有历史类型 `CookingItemRegistryComponent : Entity` 必须核对是否真正使用 `[ComponentOf]`/`AddComponent`。仅有 `Component` 后缀不能证明它是 ET Component。

### 11.3 Component 冒充多实例对象

如果同一 Owner 需要多个 SaveCandidate、Order 或 Connection，不得建成单一 Component 集合来掩盖独立生命周期；应使用 Child Entity。

### 11.4 用递归 Dispose 决定业务结果

ET Dispose 只负责资源释放，不能隐式决定：

- 是否写存档；
- 是否发放 settlement；
- 失败是否重建标准供应；
- 是否保留 Kitchen；
- Match 解散时如何处理未提交 Runtime 变化。

业务流程必须先显式收尾，再 Dispose Entity 树。

## 12. 最小 fixed-tick 任务的影响

当前 fixed-tick task 的生命周期迁移策略已确认：新增 canonical `CookingLevelLifecycle`/Level identity/Level host seam；旧 `CookingMatchLifecycle*` 作为 `[Obsolete]` 兼容 facade 委托给同一实现，保留历史 regression，但不得复制状态机。当前 task 不全局重命名 `CookingScope`、`MatchId`、LAN/UDP 或 persistence 合同。

当前 `.trellis/tasks/09-17-cooking-et-level-fixed-tick/` 仍处于 planning 阻塞状态；必须先把 PRD/design/implement 完成 Level Component fixed-tick seam 的一致性收口并由 owner 重新批准后才能实现。候选结构是：

```text
ET Scene
└── CookingApplicationComponent or test-owned application seam
    └── CookingMatchRegistryComponent
        └── CookingMatchEntity
            ├── CookingLoadedSaveComponent       optional test seam
            └── CookingRestaurantRuntimeComponent
                ├── CookingKitchenComponent / Simulation authority reference in Phase A
                └── CookingLevelComponent
                    └── CookingLevelDriverComponent
```

即使重新批准，当前任务也不应一次实现完整 Match、存档传输、Profile、Connection、SaveResult/Claim、Kitchen gameplay Entity migration 或持久化。最终 task 必须选择最小兼容 seam，并使用已经确认的 Component/Entity 命名，不再把 Level 生命周期称为 Match 生命周期。在重新批准前，本节只提供迁移方向，不授权实现。
