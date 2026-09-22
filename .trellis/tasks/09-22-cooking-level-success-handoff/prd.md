# 小关成功收口进入下一小关准备态

> 状态：`planning`。本文件只记录需求与验收，不授权实现。实现须等最终规划摘要被明确批准，并经 `task.py start` 进入 `in_progress`。

## Goal

玩家打完一个小关并成功收口后，同一家餐厅的厨房现场还在，但本关的订单和关卡上下文已经清掉，游戏停在下一小关的准备态，而不是立刻开跑。

用户价值：跨小关递进有一条可验证的领域边界。现在 `CreateSuccessor` 只换 Level 身份并新建一份厨房仿真，食材、半成品和未完成加工不会留下来，本关订单也不会被显式清除。没有这条边界，后面的装修选择、失败重开和成功写盘都没有可依附的状态。

## Background

已确认的产品决定来自 [`09-19-cooking-gameplay-business-discussion/prd.md`](../09-19-cooking-gameplay-business-discussion/prd.md) 与 [`Docs/design/CookingGame/reference/product-lifetimes.md`](../../../Docs/design/CookingGame/reference/product-lifetimes.md) §4、§5.1。当前代码事实如下。

- `CookingLevelLifecycle.CreateSuccessor`（`src/AbilityKit.Game.Cooking/CookingLevelLifecycle.cs`）只在源代际 `Ended` 且 Outcome 为 `Success` 时，创建新 `LevelId`、更高 `LevelEpoch`、同一 Match 与 RestaurantRuntime 的下一代 lifecycle。源代际保持 `Ended`，且只能创建一次。
- `CookingLevelEtHost.CreateSuccessor`（`src/AbilityKit.Game.Cooking.EtRuntime/CookingLevelEtHost.cs`）安装这代新 lifecycle。新代际的 `Start()` 会经 `ICookingLevelGameplayFactory` 新建一份 `CookingRecipeSimulation`，不接收上一代厨房状态。
- 订单簿与结算记录活在 `CookingRecipeSimulation` 上（`OpenOrder`、`Orders`、`SettlementHistory`）。恢复 checkpoint（`CookingRecipeCheckpoint`）把订单、结算、逻辑 Tick、事件历史和三个 ID 计数器整册带走。那是同代际崩溃恢复，不是小关成功交接。
- 生命周期已有 `Preparing`，但 `CreateSuccessor` 成功后再 `Prepare` + `Start` 会直接进入 `Running`。产品要求加载成功检查点后回到准备态，先做下一小关选择，再进入运行。
- 评分、收益、评价、失败条件、前厅订单节奏、装修/道具/Buff 的运行时效果都还没有领域模型。本任务不补这些模型。

## Requirements

### R1 成功收口的入口

- 只接受已经 `Ended` 且 Outcome 为 `Success` 的源代际。
- 新代际使用调用方给出的新 `LevelId` 和严格更高的 `LevelEpoch`，Match 与 RestaurantRuntime 不变。
- 源代际保持 `Ended`，不复活；同一源代际只能交接一次。
- `Running`、`Paused`、`Ending`、`Failed`、`Aborted`，以及缺 LevelId、LevelId 不变、epoch 不前进，全部结构化拒绝，厨房与源代际零变更。
- 拒绝沿用现有 `CookingLevelLifecycleReason`，不发明第二套原因码。

### R2 厨房延续状态原样保留

交接后的厨房与源代际成功收口前的厨房，在下列观察上不可区分：

- 物品与墓碑、手持、位置、版本；
- 活动加工的 elapsed、required、完成形态、容器锚点与锁定输入；
- 容器的有序内容；
- 脏碗与干净碗池计数；
- 已消耗产物账；
- 下一 Process 与下一产物 ID 不回退。下一结算序号归零，因为本关结算账本已清空，已发出的序号不带入下一关。

不在本任务重放或迁移未完成加工。工位升级迁移仍是独立契约。

### R3 本关上下文必须清除

交接后的厨房不得再观察到来源代际的：

- 订单簿（Open 与 Completed 都清除）；
- 本关结算记录（`SettlementHistory` 为空；已消耗产物账仍保留，避免同一产物被再次提交）；
- 命令去重账本；
- 命令事件历史与 tick 事件历史；
- 事件序号回到交接后的新起点；
- 逻辑 Tick 回到 0。

Level 命令水位与快照水位属于新代际，从新起点开始，不继承源代际水位。

### R4 停在准备态，厨房只读

- 交接完成后，新代际处于现有 `Created`。这是 `Prepare` 的合法起点，不新增生命周期状态。
- `Created` 与之后的 `Preparing` 都不推进 fixed tick，也不接受 gameplay 命令。
- 本任务不提供准备阶段改厨房的入口。装修、道具、Buff 选择没有领域模型，不能从交接口写入。延续状态只能在下一代进入 `Running` 之后由权威命令修改。
- 不自动 `Start`。调用方之后按现有 `Prepare` 与 `Start` 才进入 `Running`。
- `Start` 绑定已经交接的厨房，不得再新建一份空仿真。`Start` 之后，R2 保留的状态仍在，R3 清除的上下文不会回来。

### R5 原子与可观察

- 交接要么整笔提交，要么结构化拒绝且零变更。
- 成功结果必须能观察：源 scope、新 scope、源 outcome、厨房延续摘要、被清除的订单数与结算数。
- 交接不是同代际 `RestoreCheckpoint`。恢复 checkpoint 继续整册换入；本任务不得让恢复路径偷偷清订单或重置 Tick。

### R6 宿主与领域同一口径

- 领域仿真与 ET Level 宿主都要能跑同一条验收：成功收口后的厨房状态一致，订单与关卡上下文一致为空，新代际未进入 `Running`。
- 宿主路径不得再经 `Start()` 隐式新建一份空厨房来冒充交接。

## Acceptance Criteria

- [ ] 一条已有番茄蛋花汤闭环（含进行中的加工、至少一个 Open 或 Completed 订单、至少一条结算、非零逻辑 Tick）在成功 `Ended` 后交接：物品、加工、容器、干净碗池、消耗产物账以及下一 Process / 下一产物 ID 与收口前一致；订单簿与结算历史为空；逻辑 Tick、事件序号和下一结算序号为 0；去重账本与事件历史为空。
- [ ] 新代际 `LevelId` 不同、`LevelEpoch` 更高、Match 与 RestaurantRuntime 相同，状态停在 `Created`；fixed tick 与 gameplay 命令在该状态被拒绝且零变更。准备态没有改厨房的入口。
- [ ] 之后显式 `Start`，保留的加工与物品仍在，被清除的订单与结算不会恢复。
- [ ] `Running` / `Paused` / `Failed` / `Aborted` / 重复交接 / epoch 不前进 / LevelId 缺失或不变，全部返回现有结构化原因，源厨房 canonical 不变。
- [ ] 同代际 checkpoint 恢复仍整册换入订单、结算与逻辑 Tick，既有恢复验收不回归。
- [ ] 领域与 ET 宿主各有一条上述验收；focused 与既有 Cooking / ET runtime 门禁按 design 选定的 gate 实跑并记入 `check.jsonl`。

## Out of Scope

- 失败重开：丢弃失败现场、按标准初始供应重建、失败与准备阶段不写盘。
- 工位升级：未完成加工迁移到新工位并保留进度。
- durable storage、进程崩溃恢复、磁盘原子性、成功检查点的文件格式。
- 评分、收益、评价、小关结算展示字段。
- 失败条件、前厅、订单生成节奏、NPC 任务与固定伙伴人数。
- 装修、道具解锁、Buff 的候选、选择、锁定与对下一小关的效果。
- 生产传输、两台物理 PC、Unity 应用层、ET Phase B 权威迁移、旧 ECS 清退。

