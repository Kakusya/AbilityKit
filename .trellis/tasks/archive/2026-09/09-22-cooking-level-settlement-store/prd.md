# 把已确认的小关结算写到磁盘

> 状态：`planning`。本文件只记录需求与验收，不授权实现。实现须等最终规划摘要被明确批准，并经 `task.py start` 进入 `in_progress`。

## Goal

一个小关的结算列表在成功确认之后写进磁盘。关掉进程、再用同一目录新建读取器，读回的仍是这一代的同一份列表。不改货币、解锁、升级或经营进度。

用户价值：现在的确认账只活在内存里。进程一退出，成功交接前记住的那一关结果就没了。后面的长期进度入账还没有奖励公式，不能先假装发了 0 块钱。先让已经确认的列表在进程之间还在。

## Background

2026-09-22 已交付并归档的确认边界（`09-22-cooking-level-settlement-confirmation`）是本任务的输入，不在本任务里重做。

- `CookingLevelSettlementLedger`（`src/AbilityKit.Game.Cooking/CookingLevelSettlementConfirmation.cs`）按 `CookingLevelScope` 记住 `CookingOrderSettlement` 列表。同一份列表再确认是 `Duplicate`。同一代际换一份列表是 `LedgerConflict`，不覆盖第一次。空列表可以确认。不写文件，不改长期进度。
- 宿主 `ConfirmSettlements(ledger)` 只在 `Ended` + `Success` 且还持有本关厨房时把 `SettlementHistory` 交给上述账本。失败、未结束、交接之后都拒绝。交接会清空厨房里的结算账，所以确认必须发生在 `CreateSuccessor` 之前。
- `CookingOrderSettlement` 只含序号、订单、模板、recipe、产物、玩家、容器和逻辑 Tick。类型本身不含评分、收益或评价。
- `CookingProgressPersistence.Apply`（`src/AbilityKit.Game.Cooking/CookingPersistenceManagement.cs`）要求 `CookingConfirmedSettlement`，其中包含 `CookingProgressReward`。`Apply` 会改货币、解锁、升级和经营进度，并走内存 `ICookingProgressStore` 的 `prepare → commit`。现有 store 实现是 `InMemoryCookingProgressStore`，没有文件。评分和收益没有公式。把奖励填 0 再调用 `Apply`，会把「还没决定奖励」写成「奖励是 0」。
- 运行态 checkpoint（`CookingLevelCheckpoint`）是一局厨房的恢复载荷，不是这一关确认结果。成功交接会裁掉结算账。不能把 checkpoint 当作本任务的存档。
- 持久化规范（`.trellis/spec/cooking/cooking-persistence-management.md`）允许单独验证幂等、完整性和无半写入。存档归属、Profile、SaveSlot、保存时机的产品策略、退出/断电语义、备份和损坏恢复策略仍未决，完整 P5 验收因此保持 blocked。参考 [`save-storage.md`](../../../Docs/design/CookingGame/reference/save-storage.md) 要求领域层不持有绝对路径、盘符或文件句柄；PC/Android 私有目录和授权副本都不在本任务。
- 2026-09-21 已放弃把 UDP 接回当前主线。本任务不改传输。

## Requirements

### R1 只落已经确认的那一份列表

- 写入对象是一条 `CookingLevelSettlementConfirmation`：身份是 Match、`LevelId`、`LevelEpoch`，载荷是交接前的结算列表，顺序不变。
- 第一次写入成功后，磁盘上能按这个身份读回同一份列表。
- 同一身份、同一份列表再写一次，返回重复，不产生第二份文件，也不改已落盘的字节所代表的列表。
- 同一身份、不同列表，结构化拒绝，不覆盖第一次。
- 另一个 `LevelEpoch` 是另一条记录，即使 `LevelId` 相同。
- 空列表是合法记录。读回应仍是空列表，不能把「没有这条记录」和「确认过的空列表」混成一种结果。

### R2 写入要么整份可见，要么保持上一次完整记录

- 记录带格式版本和完整性校验。截断、篡改、未知格式版本，读取都结构化拒绝。
- 拒绝时不得把坏记录当成一份空确认，也不得静默改成新的空列表。
- 写入过程中出现的临时文件不是已提交记录。已有一份完整记录时，半成品不得让读取者看到新内容。这一代还没有任何完整记录时，读取者得到「没有记录」，而不是一份解析到一半的列表。
- 领域类型、ET 实体和 checkpoint 载荷里不出现绝对路径、盘符或文件句柄。测试传入一个目录根；记录按代际身份落在这个根下。

### R3 进程重启后原样读回

- 写入成功后丢掉内存中的账本对象，用同一目录根新建读取器，按原身份读回的列表与写入前逐项一致。
- 读回之后再按同一列表确认，结果是重复，不重写奖励，也不新增长期进度。
- 本任务用「新对象、同一目录」代表新进程。不启动第二个操作系统进程，不杀进程，不证明断电时磁盘控制器的行为。

### R4 不改长期进度，也不改确认资格

- 不调用 `CookingProgressPersistence.Apply`。货币、解锁、升级、经营进度保持调用前的值。
- 不新增评分、收益或评价字段。
- 内存账本 `CookingLevelSettlementLedger` 的现有语义保持不变：无文件时仍只在内存去重。
- 宿主仍只允许 `Ended` + `Success` 且尚未交接时确认。失败、进行中、准备态不能因为多了一个文件入口就写成记录。
- 文件写入失败时，这一次确认不算成功：内存账本不把这次当成已确认，已有的其他代际记录不变。调用方可以带着同一份列表再试。
- 不把写盘塞进 `CreateSuccessor` 或 `CreateRetry`。确认失败时调用方不能进入下一小关；本任务仍不自动调用下一关。

### R5 宿主能把这一关的列表交给文件记录

- ET 宿主在成功收口、交接之前，可以把 `SettlementHistory` 交给文件记录。
- 成功之后丢掉宿主和内存账本，再用同一目录读回，列表与交接前一致。
- 交接之后的空账不能通过这个入口覆盖已经落盘的列表。
- `Failed` 收口后的文件入口同样拒绝，并且不创建记录。

## Acceptance Criteria

- [ ] 一份至少含一条结算的确认写入临时目录后，新建读取器按同一 Match、`LevelId`、`LevelEpoch` 读回逐项相同的列表。同一列表再写返回重复。同一身份换列表被拒绝，磁盘上仍是第一次的列表。
- [ ] 空列表可以落盘，读回仍是空列表，且与「目录里没有这一代」不是同一种结果。更高 epoch 可以另存一条。
- [ ] 截断或篡改已提交文件时，读取结构化拒绝，不把它当成空确认。写入中的临时文件不改变已提交记录的读取结果。
- [ ] 写入失败时内存确认账不增加这一代。长期进度对象的货币、解锁、升级和经营进度不变。测试中不出现对 `Apply` 的调用。
- [ ] ET 宿主在 `Ended` + `Success` 且尚未交接时把本关列表写入目录；交接后的空账和 `Failed` 收口都不能写入或覆盖。随后新建读取器读回交接前的列表。
- [ ] 领域测试与 ET 宿主测试覆盖上述观察。沿用 `cooking-kitchen-loop` 与 `cooking-et-level-runtime`，不新增 gate。

## Out of Scope

- 评分、收益、评价的计算公式，以及把确认结果应用到解锁、升级、货币或经营进度。
- Profile、SaveSlot、存档 Owner、授权副本、PC 与 Android 的私有目录适配器，以及玩家可见的导入导出。
- 备份、隔离区、坏档修复、迁移旧格式。坏记录只拒绝，不自动救回。
- 真实进程崩溃、断电、第二个操作系统进程。验收只覆盖同一测试进程里换一个新的读取器。
- 把运行态 checkpoint 或成功交接载荷写成这个存档。
- 失败条件、失败界面、装修、道具、Buff、工位升级。
- 前厅、NPC、UDP、KCP、LAN、connection 到 PlayerId。
- Unity 应用层，以及 ET Phase B 和旧 ECS 清退。

## 已决事项

- 下一步做这条落盘，不把 UDP/KCP 接回当前主线。来源是本轮讨论：传输仍按 2026-09-21 的放弃决定保留为未启动工作。
- 落盘的是代际确认列表，不是 `CookingProgressPersistence` 的奖励入账。奖励公式未决，不能填 0。
- 不引入 Profile 或 SaveSlot。身份继续用已经确认的 `CookingLevelScope`。
- 目录根由调用方提供。本任务不选择 Windows 或 Android 的默认存档位置。
