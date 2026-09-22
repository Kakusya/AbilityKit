# 跨小关装修、道具、Buff 与成功检查点：实施清单

需求见 [prd.md](prd.md)，设计见 [design.md](design.md)。未批准前不改产品代码。

## 顺序

1. 新增大关进度：装修标识、解锁定义、煮制加速。`Created` 可改，成功锁定后拒绝再改。
2. 厨房在 `Created` 接受一次工位迁移。未完成工序改挂新工位，已用 tick 不变。冲突整次拒绝。
3. 解锁定义按标准供应摆进延续厨房。失败重开的重建同样摆上当前未锁定选择。未知定义零变更。
4. 新开始的煮制读取加速比例，写下缩短后的所需 tick。已在跑的工序不改。
5. 成功收口且结算已确认后，把锁定进度和交接厨房写到调用方给的目录。失败不写。坏文件拒绝。写入失败不进入下一小关。
6. 补 spec 修约、`Docs/Todo.md` 和 `progress.md`。前厅、评分、失败条件保持未完成。
7. 跑下面两个既有门禁。不改 `tools/test-gates.json`。

## 验证

```text
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime
```

领域测试挂 `Gate=CookingKitchenLoop`。宿主测试留在 ET runtime 测试程序集。临时目录在测试结束时删除。

## 回退点

- 不改结算列表文件的字段，不调用 `CookingProgressPersistence.Apply`。
- 不改 `CreateRetry` 的「丢掉失败现场」；只在重建之后追加已选择的解锁道具。若追加失败，整次重开保持今天的标准供应，不留下半成品。
- 迁移若让 `Running` 的工序也能改工位，撤回入口，不把时钟暂停塞进运行态。
