# 小关成功结算确认：技术设计

需求见 [prd.md](prd.md)。本设计不计算奖励，不写盘，不把确认塞进 `CreateSuccessor`。

## 决定

确认结算是一条关卡代际记录，不是 `CookingProgressPersistence.Apply` 的一次奖励入账。

现有 `CookingConfirmedSettlement` 必须带 `CookingProgressReward`，并且 `Apply` 会改货币、解锁、升级和经营进度。评分和收益还没有公式。本任务若复用 `Apply`，就只能填 0 奖励来冒充确认。那会把「没有奖励」写成「奖励是 0」，以后的真实奖励无法再区分。

因此新增一条只读确认账：身份是 Match + `LevelId` + `LevelEpoch`，载荷是 `CookingOrderSettlement` 列表。它不进入 `CookingLongTermProgress`。

## 边界

| 层 | 责任 | 不负责 |
|---|---|---|
| `CookingRecipeSimulation` | 继续持有本关 `SettlementHistory`；交接前可只读导出 | 判断这关能不能确认 |
| 确认账 | 收下一份结算列表，按代际身份去重，拒绝内容冲突 | 改厨房、改长期进度、写文件 |
| `CookingLevelEtHost` | 在 `Ended` + `Success` 且尚未交接时取出列表并提交确认 | 自动调用 `CreateSuccessor`、计算收益 |

## 数据流

```text
源代际 Ended + Success
  -> 只读导出 SettlementHistory
  -> 确认账按 Match + LevelId + LevelEpoch 登记
       第一次：记下列表
       同一身份同一列表：重复，账不变
       同一身份不同列表：拒绝，账不变
  -> 调用方之后自行 CreateSuccessor
```

`Failed`、未结束、已交接后的空账，都进不了这个入口。交接仍由调用方在确认成功之后单独调用。

## 契约

- 确认记录是不可变的。列表顺序与 `SettlementHistory` 一致。
- 空列表允许确认。它表示这一关没有成功提交。
- 身份不含玩家、订单或产物。那些在列表里。
- 不新增文件格式，不改 `CookingLevelCheckpointCodec`。同代际恢复仍整册换入结算账。
- 不改 `CookingProgressPersistence.Apply` 的奖励语义。

## 风险

- 调用方若先交接再确认，读到的是空账。宿主入口必须在交接前取列表。领域入口不负责拦这个顺序，测试要钉住宿主顺序。
- 确认账只活在内存。进程退出后它不在。这是范围，不是遗漏。写盘是下一条。
