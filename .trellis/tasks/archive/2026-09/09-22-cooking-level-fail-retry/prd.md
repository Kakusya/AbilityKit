# 小关失败重开按标准供应重建厨房

> 状态：`planning`。本文件只记录需求与验收，不授权实现。实现须等最终规划摘要被明确批准，并经 `task.py start` 进入 `in_progress`。

## Goal

玩家在一个小关已经失败收口之后，同一关可以重开：失败现场被丢掉，厨房按这一关的标准初始供应重新摆好，游戏停在准备态，而不是接着失败厨房开跑，也不是新建一家空厨房。

用户价值：成功交接已经规定「成功之后厨房留下什么」。失败是对称的另一半：现场不留下，关卡身份留下。没有这条边界，失败之后的 `CreateRetry` 只换 epoch，食材、半成品、订单和 Tick 没有明确归宿。

## Background

已确认的产品决定来自 [`Docs/design/CookingGame/reference/product-lifetimes.md`](../../../Docs/design/CookingGame/reference/product-lifetimes.md) §4、§5.2，以及 [`09-19-cooking-gameplay-business-discussion/prd.md`](../09-19-cooking-gameplay-business-discussion/prd.md)。当前代码事实如下。

- `CookingLevelLifecycle.CreateRetry`（`src/AbilityKit.Game.Cooking/CookingLevelLifecycle.cs`）只在源代际 `Ended` 且 Outcome 为 `Failed` 时，复用同一 `LevelId`、使用严格更高的 `LevelEpoch`、同一 Match 与 RestaurantRuntime，创建下一代 lifecycle。源代际保持 `Ended`，且只能创建一次。`Success` 走 `RetryRequiresFailedOutcome`。
- `CookingLevelEtHost.CreateRetry`（`src/AbilityKit.Game.Cooking.EtRuntime/CookingLevelEtHost.cs`）只安装这代新 lifecycle。`CompleteEnd` 在 `Failed` 时已经 `ReleaseSimulationOwnership`，失败厨房不再交给下一代。新代际的 `Start()` 仍经 `ICookingLevelGameplayFactory` 新建一份空 `CookingRecipeSimulation`，不会按标准初始供应摆厨房。
- 标准初始供应已经有唯一入口：`CookingContentCatalog.ApplyStandardInitialSupply`（`src/AbilityKit.Game.Cooking/CookingContentCatalog.cs`）。它按内容文档生成确定的实例 ID，`cleanPool` 项由仿真构造器建池，不在这里重复放入。
- 同代际崩溃恢复仍走 `CookingRecipeCheckpoint` 整册换入。失败重开不是恢复，也不是 `ExportSuccessHandoff`。
- 09-19 已决定：首个闭环不定义失败条件。失败或中断不得覆盖最近一次成功检查点。装修、道具、Buff 还没有领域模型；「失败重试时这些选择仍然有效」不能在本任务里建模。
- `Docs/Todo.md` P0-C1 写「关闭失败 Match，创建新 MatchId」。这与产品参考冲突：失败重试不结束 Match，也不换 MatchId。本任务以产品参考为准，实施时要改掉待办这句，不能按待办原文做。

## Requirements

### R1 失败重开的入口

- 只接受已经 `Ended` 且 Outcome 为 `Failed` 的源代际。
- 新代际复用源 `LevelId`，使用调用方给出的严格更高的 `LevelEpoch`，Match 与 RestaurantRuntime 不变。
- 源代际保持 `Ended`，不复活；同一源代际只能创建一次下一代。
- `Running`、`Paused`、`Ending`、`Success`、`Aborted`，以及 epoch 不前进，全部结构化拒绝，源代际零变更。
- 拒绝沿用现有 `CookingLevelLifecycleReason`（`InvalidState`、`RetryRequiresFailedOutcome`、`EpochNotAdvanced`、`GenerationAlreadyCreated`），不发明第二套原因码。
- 本任务不判断「这一关为什么失败」。调用方必须已经把 Outcome 收成 `Failed`。

### R2 失败现场整份丢掉

重开后的厨房不得再观察到来源代际的任何现场，包括：

- 物品、墓碑、手持、位置与版本；
- 活动加工及其 elapsed、锁定输入、容器锚点；
- 容器内容；
- 脏碗与被改过的干净碗池计数；
- 订单簿与结算记录；
- 已消耗产物账、命令去重账本、命令事件与 tick 事件；
- 逻辑 Tick、事件序号、状态版本、下一 Process / 产物 / 结算序号。

这些计数器回到新仿真的起点，不继承失败代际的水位。Level 命令水位同样从新起点开始。Match/Application 级 `HostFrameSequence` 保持单调，不重置。

### R3 按标准初始供应重建

- 重开厨房的物品与干净碗池，和同一份 `CookingContent` 在全新仿真上调用 `ApplyStandardInitialSupply` 的结果不可区分：同一实例 ID、同一位置、同一数量。
- 不保留失败代际里多出来的物品，也不保留玩家已经移走或消耗掉的标准供应缺口。供应以关卡定义为准，不以失败现场的残量为准。
- 重建使用现有内容目录与现有供应入口，不另写一套供应表。
- 新厨房没有订单、没有结算、没有进行中加工。

### R4 停在准备态

- 重开完成后，新代际处于现有 `Created`。这是 `Prepare` 的合法起点，不新增生命周期状态。
- `Created` 与之后的 `Preparing` 都不推进 fixed tick，也不接受 gameplay 命令。
- 本任务不提供准备阶段改厨房、改装修、改道具或改 Buff 的入口。
- 不自动 `Start`。调用方之后按现有 `Prepare` 与 `Start` 才进入 `Running`。
- `Start` 绑定这份已经按标准供应摆好的厨房，不得再新建一份空仿真，也不得把失败现场接回来。

### R5 不写盘，也不覆盖成功检查点

- 失败重开不调用持久化 prepare、commit 或 read。
- 不产生新的恢复 checkpoint，也不改写调用方已经持有的上一次成功检查点。
- 同代际 `ExportCheckpoint` / `Restore` 的行为不变：恢复仍整册换入订单与 Tick。失败重开不得走这条路径偷偷清现场。

### R6 原子与同一口径

- 重开要么整笔提交，要么结构化拒绝且源代际零变更。
- 领域生命周期与 ET Level 宿主走同一条验收：同一 `LevelId`、更高 epoch、标准供应厨房、空订单、Tick 为 0、停在 `Created`。
- 成功交接路径保持不变：`Success` 不能从 `CreateRetry` 进入，`Failed` 不能从 `CreateSuccessor` 把厨房交下去。

## Acceptance Criteria

- [ ] 一条已经改过现场的关卡（移走或消耗过标准供应中的物品、有进行中加工、有订单、有结算、逻辑 Tick 非 0）在 `Ended` + `Failed` 后重开：新代际 `LevelId` 相同、`LevelEpoch` 更高、Match 与 RestaurantRuntime 相同；物品与干净碗池和「新仿真 + 同一份标准初始供应」一致；订单、结算、加工、去重账本和事件历史为空；逻辑 Tick、事件序号和三个 ID 计数器处于新仿真起点。
- [ ] 新代际停在 `Created`。fixed tick 与 gameplay 命令在该状态被拒绝且零变更。之后显式 `Prepare` + `Start` 绑定的仍是这份标准供应厨房，不会再出现失败现场里的物品或订单。
- [ ] `Running`、`Success`、`Aborted` 和 epoch 不前进都被结构化拒绝，源厨房与源代际零变更。
- [ ] 重开路径不调用持久化写入，也不改变同代际 checkpoint 的整册恢复。`HostFrameSequence` 不回退。
- [ ] 领域测试与 ET 宿主测试覆盖上述观察。沿用 `cooking-kitchen-loop` 与 `cooking-et-level-runtime`，不新增 gate。

## Out of Scope

- 定义什么时候算失败，包括顾客不满、超时、烧焦、过度加工。
- 失败界面、玩家点击按钮、准备界面里改装修 / 道具 / Buff。这些选择还没有领域模型。
- 工位升级，以及未完成加工向新工位迁移。
- confirmed settlement、durable storage、进程崩溃恢复、磁盘原子性。
- 成功交接行为的再设计。
- 前厅订单节奏、NPC、评分 / 收益 / 评价。
- KCP、LAN、connection 到 PlayerId 绑定。
- Unity 应用层、场景、authoring、projection、UI。
- ET Phase B，以及 `world.entitas` / `world.ecs` 清退。
