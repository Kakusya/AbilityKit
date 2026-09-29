# Cooking 固定伙伴小关内成长 — Implementation Plan

## Preconditions

- 任务保持 `planning`，直到 owner 对最终规划摘要做出新的明确实施批准。
- 实施前运行 `python ./.trellis/scripts/task.py start cooking-companion-level-growth`。
- 实施前加载 `trellis-before-dev`，并读取 `implement.jsonl` 中列出的规范和研究上下文。
- 保留当前工作树内 09-19 规划源、Cooking spec、progress 和 Todo 的未提交修改，不做清理或覆盖。

## Ordered Checklist

1. **先补领域失败测试**
   - 在 `CookingFrontOfHouseTests.cs` 添加 G1–G5、G7、生命周期重置用例。
   - 明确既有“认领后立即推进 1 Tick”的 elapsed 口径。
   - 先运行聚焦测试，确认新增用例在未实现时按预期失败。
2. **实现单一成长状态**
   - 给 schedule 增加尾部可选阈值并校验正数。
   - 给 front-of-house 增加完成计数和派生解锁状态。
   - 只在成功询问/洗碗终点增加计数。
3. **冻结任务启动耗时**
   - 给 `CookingCompanionWork` 增加 `RequiredTicks`。
   - 询问认领写入基础询问耗时；洗碗认领按当时解锁状态写入基础/加速耗时。
   - `AdvanceCompanion` 与 snapshot 改用冻结值。
4. **扩展 snapshot/canonical/checkpoint**
   - 投影完成计数、派生解锁状态和冻结耗时。
   - canonical 固定顺序加入新字段并验证 SHA-256 敏感性。
   - `Apply`/`CopyFrom` 恢复计数；checkpoint 校验新增负值、派生不一致和 required ticks 规则。
5. **实现生命周期重置**
   - `DropFailedScene` 清零成长。
   - `ResetForNextLevel` 在既有成功收口后清零成长。
   - 验证 `FinishInProgress` 只统计真正成功终点。
6. **补 ET host 恢复验收**
   - 扩展 `CookingLevelCheckpointTests.cs`，验证成长状态经现有宿主 checkpoint 往返。
   - 添加投毒恢复失败和继续运行等价用例。
   - 仅当测试证明现有透传不足时修改 `CookingLevelEtHost.cs`。
7. **同步稳定规范**
   - 实现与门禁通过后，把已验证行为写入 `.trellis/spec/cooking/cooking-recipe-loop.md` 和 index 状态入口。
   - 更新 `Docs/design/CookingGame/progress.md` 与 `Docs/Todo.md` 的实际完成状态；不得提前表述为已实现。
8. **执行质量门禁并记录证据**
   - 运行聚焦领域测试、Cooking gate、ET runtime gate 和 `git diff --check`。
   - 把实际执行结果追加到 `check.jsonl`，区分通过、失败、受阻和跳过。
   - 检查 diff 不含 LAN/session/UDP/Unity 改动。

## Validation Commands

```powershell
dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj --filter FullyQualifiedName~CookingFrontOfHouseTests -v minimal

dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj --filter "FullyQualifiedName~CookingLevelCheckpointTests|FullyQualifiedName~CookingLevelClosedLoopTests" -v minimal

powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop

powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime

git diff --check
```

Unity 编译与 EditMode 不在本任务范围；不得把未运行记录为通过。

## Risky Files And Rollback Points

- `src/AbilityKit.Game.Cooking/CookingFrontOfHouse.cs`
  - 风险：位置构造参数、固定 Tick 的首 Tick 口径、checkpoint canonical 变化。
  - 回滚点：schedule 参数、work required ticks、snapshot 字段必须作为一个一致变更集处理。
- `src/AbilityKit.Game.Cooking.Tests/CookingFrontOfHouseTests.cs`
  - 风险：测试辅助函数若默认阈值过小，会无意改变既有前厅用例。
  - 处理：默认保持产品阈值 3，仅聚焦用例显式覆写。
- `src/AbilityKit.ET.Runtime.Tests/CookingLevelCheckpointTests.cs`
  - 风险：宿主恢复夹具必须让厨房与前厅 checkpoint 属于同一代际，不能只拼接孤立 snapshot。
- `.trellis/spec/cooking/*`、`Docs/design/CookingGame/progress.md`、`Docs/Todo.md`
  - 风险：只能在实现和验证后标记完成。

## Definition Of Done

- G1–G8 全部有自动化测试证据。
- 领域只有一个成长计数源，解锁状态无可变副本。
- 进行中任务耗时由认领时冻结，checkpoint 可恢复。
- 下一 Level 和失败重开清零；同 Level 恢复保留。
- Cooking 与 ET runtime 门禁通过，`git diff --check` 通过。
- 无 LAN/session/UDP/Unity、长期成长或经济范围扩张。
- `check.jsonl` 记录实际证据，稳定 spec 只描述已验证能力。
