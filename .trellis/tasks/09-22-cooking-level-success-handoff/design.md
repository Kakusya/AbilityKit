# 小关成功收口：技术设计

需求见 [prd.md](prd.md)。本设计不新增厨房状态模型，也不改同代际恢复语义。

## 决定

准备态厨房只读。PRD 的开放问题按这个决定关闭：装修、道具、Buff 没有领域模型，准备态若允许改厨房，保留现场就守不住。准备态拒绝 fixed tick 与 gameplay 命令；延续状态只能在下一代进入 `Running` 之后被权威命令修改。

## 边界

| 层 | 责任 | 不负责 |
|---|---|---|
| `CookingRecipeSimulation` | 把当前权威状态收成成功交接载荷：保留厨房现场，清除本关上下文 | Level 身份、生命周期、宿主队列 |
| `CookingLevelLifecycle` | 继续只判定能不能创建下一代（已有 `CreateSuccessor`） | 厨房内容 |
| `CookingLevelEtHost` | 在安装下一代之前完成厨房交接，并让新代际停在 `Created` | 自动 `Prepare` / `Start`、写盘、失败重开 |

失败重开继续走现有 `CreateRetry`：新厨房由以后的 `Start()` 按关卡标准供应创建。本任务不改这条路径。

## 数据流

```text
源代际 Ended + Success
  -> 仿真 ExportSuccessHandoff()
       基于 ExportCheckpoint()
       清除订单、结算、去重、命令事件、tick 事件
       LogicalTick = 0，EventSequence = 0，StateVersion = 0
       LevelScope = null
       保留物品、加工、容器、脏/净碗、消耗产物账、三个 ID 计数器
  -> 校验新 LevelId / 更高 epoch（现有规则）
  -> 安装下一代 lifecycle，状态停在 Created
  -> 源仿真整册 RestoreCheckpoint(交接载荷)
  -> 源生命周期标记已创建下一代，源状态保持 Ended
  -> 取消源宿主尚未执行的命令
```

任一步失败则整笔拒绝：源厨房 canonical 不变，不安装新代际，不标记已创建下一代。

## 契约

### 交接载荷

新增 `CookingRecipeSimulation.ExportSuccessHandoff()`，返回 `CookingRecipeCheckpoint`。不新增第二种 checkpoint 记录。

裁剪规则：

- 保留：`Items`、`Processes`、`Containers`、`ConsumedProducts`、`CleanContainerCounts`、`NextProcessId`、`NextProductId`、`Scope`（Match scope）。
- 清空：`Orders`、`Settlements`、`Deduplication`、`Events`、`TickEvents`。
- 归零：`LogicalTick`、`EventSequence`、`StateVersion`、`NextSettlementSequence`。结算账本已清空，恢复校验要求下一序号等于账本长度；已发出的结算序号不带入下一关。物品与加工序号仍不回退。
- `LevelScope` 置空。源代际的 Level 绑定不能进入下一代；下一代第一次 `AdvanceFixedTick` 再按现有规则绑定自己的 scope。

现有 `RestoreCheckpoint` 对空订单、空结算、`NextSettlementSequence == Settlements.Count`、`LevelScope == null` 已经是合法载荷。未推进 tick 的新仿真就是这种形状。交接复用这条校验，不放宽外键、加工或容器规则。

`StateVersion` 归零是交接事实，不是恢复。同代际 `ExportCheckpoint` 仍写出真实版本；恢复仍整册换入，不得调用 `ExportSuccessHandoff`。

### 仿真入口

新增 `CookingRecipeSimulation.AcceptSuccessHandoff(CookingRecipeCheckpoint handoff)`：

- 只接受上述裁剪后的形状。非空订单、非空结算、非零 Tick、非零事件序号、非空事件历史、已绑定 `LevelScope`，或计数器回退，一律结构化拒绝且零变更。
- 接受时走现有 `RestoreCheckpoint`。物品、加工、容器仍受现有外键校验。
- 拒绝原因沿用 `CookingCheckpointRestoreReason`。现有枚举没有「这不是交接载荷」时，只加一个交接专用原因，不重载 `ScopeMismatch`。

领域验收可以直接对一份仿真调用「导出交接载荷，再由同 fixture 的新仿真 Accept」。宿主验收必须走宿主入口，不能只测这个纯函数。

### 宿主入口

`CookingLevelEtHost.CreateSuccessor` 改为交接，而不是换 lifecycle、厨房留到下次 `Start` 再新建。

顺序：

1. 源状态必须是 `Ended` + `Success`，且本宿主仍能读到那份仿真。今天 `CompleteEnd` 会 `ReleaseSimulationOwnership()`。交接需要关闭后仍可只读导出；关闭后不得再 mutation。若今天 `ExportCheckpoint` 在关闭后被拒绝，只放行只读导出。
2. 导出交接载荷。
3. 现有 `TryCreateSuccessorCandidate` 校验身份。失败则零变更。
4. 安装候选 lifecycle，提交 `CommitSuccessorCandidate`。
5. 候选保持 `Created`。不调用 `Prepare`，不调用 `Start`。
6. 把源仿真转到候选名下并 `AcceptSuccessHandoff`。不通过 factory 再造一份空厨房。
7. 宿主命令水位归零，pending 按现有 `level-generation-replaced` 取消。`HostFrameSequence` 单调不重置，与 product-lifetimes §4.1 的失败重试口径一致。

`CreateRetry` 保持原语义，不接收交接载荷。

### 准备态

新代际停在 `Created`。这是现有 `CreateSuccessor` 已经留下的状态，也是 `Prepare` 的合法起点。

- `Created` 与之后的 `Preparing` 都不是 `Running`。现有 admission 已经拒绝 gameplay 命令，fixed tick 也不跑。本任务不新加状态。
- 准备态只读由「没有 mutation 入口」保证：不提供准备阶段改厨房的 API。
- 调用方稍后 `Prepare` + `Start`。`Start` 不得再替换这份已经交接的仿真。交接后的代际，`Start` 绑定已有仿真并进入 `Running`。
- 普通新开局的 `Start` 仍由 factory 新建。用 lifecycle 上的显式标记区分，不用「仿真是否为 null」猜测。

### 原子性

宿主交接在 owner thread、非 ticking 时执行，与现有 `InstallGeneration` 一样。

- 身份校验失败：导出是只读的，源 canonical 不变。
- 安装或 `AcceptSuccessHandoff` 失败：卸下已安装的候选，源生命周期不标记 `_hasCreatedNextGeneration`，源仿真不被替换。
- 不引入跨进程事务。进程崩溃恢复仍不在本任务。

## 兼容

- 同代际 `ExportCheckpoint` / `Restore` / R01–R04 验收的字节与语义不变。
- 三个命令指纹金样不碰。
- `CreateSuccessor` 的拒绝原因保持现有 `CookingLevelLifecycleReason` 字符串。
- 成功结果需要能看到新 scope 与「厨房已交接」。现有 `CookingLevelHostGenerationResult` 若没有厨房摘要，只加只读观察（保留的加工数、清除的订单数、清除的结算数），不改身份字段。

## 明确不做

失败重开的厨房重建、工位迁移、写盘、评分字段、装修/Buff 写入、前厅与 NPC 任务。交接载荷不给这些预留可选段。
