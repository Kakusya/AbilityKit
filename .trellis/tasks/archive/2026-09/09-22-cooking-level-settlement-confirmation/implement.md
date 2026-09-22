# 小关成功结算确认：实施清单

需求见 [prd.md](prd.md)，设计见 [design.md](design.md)。未批准前不改产品代码。

## 顺序

1. 新增代际确认账。身份是 Match、`LevelId`、`LevelEpoch`。载荷是 `CookingOrderSettlement` 列表。第一次记下，同一列表返回重复，不同列表拒绝。
2. 宿主只在 `Ended` + `Success` 且尚未交接时导出 `SettlementHistory` 并提交。其他状态拒绝。
3. 领域测试覆盖重复、冲突、空列表。宿主测试覆盖成功确认、失败拒绝，以及交接后再读厨房结算账为空且不能覆盖原确认。
4. 补 spec 修约、`Docs/Todo.md` 和 `progress.md`。待办里「durable storage、进程崩溃恢复和磁盘原子性」保持未勾。
5. 跑 `cooking-kitchen-loop` 与 `cooking-et-level-runtime`。不新增 gate。

## 验证

```text
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime
```

## 回退点

- 不改 `CookingProgressPersistence.Apply`，不改 `ExportSuccessHandoff`，不改 `CreateRetry`。
- 确认账若让长期进度测试红，说明奖励被写进去了。撤回确认账，不把奖励填 0。
