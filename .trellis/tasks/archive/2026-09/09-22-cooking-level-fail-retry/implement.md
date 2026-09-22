# 小关失败重开：实施清单

需求见 [prd.md](prd.md)，设计见 [design.md](design.md)。未批准前不改产品代码。

## 顺序

1. 确认 `CookingLevelEtHost.CreateRetry` 在身份校验失败时仍零变更，并且 `Failed` 的 `CompleteEnd` 仍释放源厨房。
2. 在 `InstallGeneration(isRetry: true)` 成功之后创建新仿真，调用 `ApplyStandardInitialSupply`，再 `AdoptSuccessorKitchen`。候选保持 `Created`。
3. 工厂、供应或绑定失败时 fault，并释放已经创建但未绑定的仿真。不把空厨房或失败现场报成重开成功。
4. 领域测试做对照臂：同一 `CookingContent` 的新仿真加标准供应。宿主测试走 `Ended` + `Failed` 然后 `CreateRetry`。
5. 补 spec 修约、`Docs/Todo.md` 失败重开条目、`progress.md` 新小节。待办里「关闭失败 Match，创建新 MatchId」改成同一 Match、同一 LevelId、更高 epoch。
6. 跑 `cooking-kitchen-loop` 与 `cooking-et-level-runtime`，把实际结果写入 `check.jsonl`。不新增 gate。

## 测试

- 宿主：改过的现场（移走标准供应、加工、订单、结算、非零 Tick）重开后，与对照臂 canonical 一致；`LevelId` 不变，epoch 升高，状态 `Created`，`HostFrameSequence` 不回退。
- 宿主：`Created` 上 `Tick` 与 `TryEnqueue` 返回 `LevelNotRunning`。随后 `Prepare` + `Start` 仍是对照臂那份厨房。
- 宿主：`Running`、`Success`、epoch 不前进拒绝，且源代际未被标记为已创建下一代。
- 回归：现有 `Retry_cancels_old_epoch_queue_resets_local_watermarks_and_preserves_host_sequence` 仍通过。成功交接测试仍通过。checkpoint 恢复测试仍整册还原订单与 Tick。

## 验证

```text
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime
```

先跑宿主过滤测试确认 `CreateRetry`，再跑这两个门禁。缺 Unity 或另一 Editor 占用项目时记跳过，不记通过。

## 回退点

- 只改 `CreateRetry` 的厨房安装。`CreateSuccessor`、checkpoint 导出/恢复、持久化管理不改。
- 若供应绑定让现有 retry 测试红，先看是不是 `Start` 又向工厂要了第二份仿真。修复绑定，不放宽身份拒绝。
