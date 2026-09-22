# 已确认小关结算落盘：实施清单

需求见 [prd.md](prd.md)，设计见 [design.md](design.md)。未批准前不改产品代码。

## 顺序

1. 新增按代际写读的文件记录器。输入是已经构造好的确认和调用方给的目录根。同一列表重复，不同列表拒绝，空列表可区分于缺失。
2. 写入使用临时文件再替换。截断和篡改在读取时结构化拒绝，不覆盖上一次完整记录。
3. 接上现有内存确认：文件写入失败时撤回这一次内存登记。不调用 `CookingProgressPersistence.Apply`。
4. 宿主测试只在成功收口、交接前把 `SettlementHistory` 交给记录器。`Failed` 和交接后的空账不得写或覆盖。
5. 补 spec 修约、`Docs/Todo.md` 和 `progress.md`。待办里评分、收益、长期进度入账、Profile/SaveSlot、真实断电恢复保持未完成。
6. 跑下面两个既有门禁。不改 `tools/test-gates.json`。

## 验证

```text
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime
```

领域测试挂 `Gate=CookingKitchenLoop`。宿主测试留在 ET runtime 测试程序集，随 `cooking-et-level-runtime` 一起跑。临时目录在测试结束时删除。

## 回退点

- 不改 `ExportSuccessHandoff`、`CreateRetry`、`CreateSuccessor` 和 `CookingProgressPersistence.Apply`。
- 若长期进度测试出现货币或解锁变化，说明确认被送进了奖励入账。撤回文件记录器与调用点，不把奖励填 0。
- 若坏文件被读成空列表，撤回读取端的默认值，改成结构化拒绝。
