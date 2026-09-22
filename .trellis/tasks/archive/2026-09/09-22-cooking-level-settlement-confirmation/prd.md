# 小关成功结算确认边界

> 状态：`planning`。本文件只记录需求与验收，不授权实现。实现须等最终规划摘要被明确批准，并经 `task.py start` 进入 `in_progress`。

## Goal

一个小关成功收口时，这一关里已经提交成功的订单变成一条可识别的确认结算。同一关再确认一次不再生效。失败收口和准备态不产生这条确认。

用户价值：成功交接已经清掉本关结算账，失败重开已经丢掉失败现场。缺的是成功之前，这些结算记录有没有被确认过。没有这条边界，后面的写盘不知道该提交哪一笔，失败也没法保证自己没留下可提交的结果。

## Background

已确认的产品决定来自 [`Docs/design/CookingGame/reference/product-lifetimes.md`](../../../Docs/design/CookingGame/reference/product-lifetimes.md) §5.1、§5.2，以及 [`09-19-cooking-gameplay-business-discussion/prd.md`](../09-19-cooking-gameplay-business-discussion/prd.md)。当前代码事实如下。

- 局内提交成功会追加 `CookingOrderSettlement`（`src/AbilityKit.Game.Cooking/CookingRecipeLoop.cs`）。记录含序号、订单、模板、recipe、产物、玩家、容器和逻辑 Tick。类型注释写明不含评分、收益或评价。
- 成功交接 `ExportSuccessHandoff` 清空这份结算账，并让下一结算序号归零。失败重开丢掉整份厨房，其中也包括这份账。
- 长期进度已经有 `CookingConfirmedSettlement` 和 `CookingProgressPersistence.Apply`（`src/AbilityKit.Game.Cooking/CookingPersistenceManagement.cs`）。它要求 `IsConfirmed`，同一 `SettlementId` 再应用一次返回 `Duplicate`，身份相同但内容不同返回 `SettlementIdentityConflict`。提交走内存 `prepare → commit`。
- `CookingConfirmedSettlement` 还要求 owner scope、目标进度版本、配置身份和 `CookingProgressReward`。奖励含解锁、升级、货币和经营进度。评分、收益、评价的产品规则还没落到这些字段上。09-19 只决定小关结算展示完成订单数、收益与评价，没有给出计算公式。
- 检查点写入失败应阻止进入下一小关。真实文件 store、进程崩溃恢复和磁盘原子性都还没有。

## Requirements

### R1 只有成功收口才能确认

- 只接受已经 `Ended` 且 Outcome 为 `Success` 的这一代。
- 确认对象是这一代厨房在交接前的 `SettlementHistory`，不是交接后那份空账。
- `Running`、`Paused`、`Ending`、`Failed`、`Aborted` 和 `Created` 全部结构化拒绝，结算账与长期进度零变更。
- 失败重开和准备态不产生确认结算。

### R2 一条关卡确认，而不是逐单再结算

- 一次成功收口产生一条确认，身份由 Match、`LevelId`、`LevelEpoch` 决定。
- 这条确认携带本关结算记录的原样列表：序号、订单、模板、recipe、产物、玩家、容器、逻辑 Tick。不增删、不改序。
- 本关没有成功提交的订单时，确认仍然成立，列表为空。空列表也是这一关的结果。
- 不计算评分、收益或评价。不填写 `CookingProgressReward` 的货币、解锁或升级。这些字段现在没有产品公式。

### R3 重复确认无效

- 同一 Match、同一 `LevelId`、同一 `LevelEpoch` 再确认一次，返回重复，长期进度与确认账不变。
- 同一身份但结算列表不同，结构化拒绝，不覆盖第一次的列表。
- 不同 `LevelEpoch` 是另一条确认，即使 `LevelId` 相同。

### R4 确认不推进下一关，也不写盘

- 确认失败不得让调用方以为可以进入下一小关。本任务不把确认塞进 `CreateSuccessor` 里自动调用。
- 成功交接保持现有语义：厨房留下，本关订单和结算账清除。确认要在交接裁账之前取得结算列表。
- 不调用文件 store，不新增磁盘写入，不实现进程崩溃恢复。
- 内存中的确认结果必须能被再次读到。读到的是同一条身份和同一份结算列表。

### R5 领域与宿主同一口径

- 领域可以单独确认一份已经导出的结算列表。
- ET 宿主在 `Ended` + `Success`、且还没交接时，能取出本关结算列表并完成同一条确认。
- 宿主在 `Failed` 收口后确认，必须拒绝。

## Acceptance Criteria

- [ ] 一条有至少两条成功提交的关卡，在 `Ended` + `Success` 后确认：确认身份含 Match、`LevelId`、`LevelEpoch`；结算列表与确认前的 `SettlementHistory` 逐项一致；长期进度的货币、解锁、升级和经营进度不变。
- [ ] 同一代再确认一次返回重复，列表不变。同一身份换一份不同列表被拒绝。另一个更高 epoch 可以另确认一条。
- [ ] `Failed`、`Running` 和空结算列表按 R1、R2 处理：前两者拒绝且零变更，空列表确认成功。
- [ ] 确认不调用 `CreateSuccessor`，也不产生文件写入。成功交接之后再拿厨房里的结算账，列表为空，不能用空列表覆盖确认前的结果。
- [ ] 领域测试与 ET 宿主测试覆盖上述观察。沿用 `cooking-kitchen-loop` 与 `cooking-et-level-runtime`，不新增 gate。

## Out of Scope

- 评分、收益、评价的计算公式，以及小关结算展示。
- 把确认结果应用到解锁、升级、货币或经营进度。
- durable storage、进程崩溃恢复、磁盘原子性、真实文件 store。
- 确认失败时如何停留在成功结算界面。本任务只保证确认失败时没有下一关，也不改已经确认的账。
- 失败条件、失败界面、装修、道具、Buff、工位升级。
- 前厅订单节奏、NPC、KCP、LAN、connection 到 PlayerId。
- Unity 应用层，以及 ET Phase B 和旧 ECS 清退。
