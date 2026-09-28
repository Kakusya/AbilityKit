# Cooking 可见顾客与固定伙伴运行时纵切：实施清单

更新：2026-09-28。状态：implemented / verified。

## 1. 测试先行：身份与投影

- [x] 增加同桌连续两批顾客测试，先证明当前订单身份复用问题。
- [x] 增加顾客四阶段和伙伴三类工作状态的 snapshot 断言。
- [x] 增加 canonical/hash 确定性和敏感性测试。

## 2. 领域状态改造

- [x] 引入顾客与伙伴强类型身份。
- [x] 将桌位内部状态改为持有稳定顾客运行态；订单身份跟随顾客。
- [x] 新增不可变 snapshot、深拷贝、canonical 和 SHA-256。
- [x] 保持既有调度、超时、用餐和收口行为不变。

## 3. Checkpoint 与 ET 恢复

- [x] 新增前厅 checkpoint、完整校验与原子恢复。
- [x] 将可选前厅载荷接入 `CookingLevelCheckpoint`，格式版本升 v2。
- [x] 扩展 ET host 导出/恢复，使前厅与厨房共同恢复或共同失败。
- [x] 覆盖询问中、洗碗中、用餐中的基线臂/恢复臂等价验收。

## 4. 生命周期回归

- [x] 验证暂停不推进前厅。
- [x] 验证成功收口完成进行中的询问/洗碗。
- [x] 验证失败重开和下一 Level 清空顾客/伙伴工作，并保持脏碗边界。
- [x] 验证既有 Tomato Egg Soup 可复用场景和跨关逻辑无回归。

## 5. 文档与证据

- [x] 更新 Cooking spec index 与 recipe-loop 稳定契约。
- [x] 更新 `Docs/design/CookingGame/progress.md` 和 `Docs/Todo.md`。
- [x] 在 task `research/` 写入验证记录，在 `check.jsonl` 写入实际门禁结果。

## Validation Commands

```powershell
dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj --filter "Gate=CookingKitchenLoop"
dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj
dotnet test src/AbilityKit.Game.Cooking.EtRuntime.Tests/AbilityKit.Game.Cooking.EtRuntime.Tests.csproj
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime
```

## Risky Changes / Rollback Points

- 订单 ID 生成规则变化：先以聚焦测试锁定，再批量更新 fixture 断言。
- `CookingLevelCheckpoint` 格式版本变化：序列化与完整性测试必须先通过，再接 ET restore。
- 前厅恢复跨厨房订单/脏碗一致性：任何校验失败必须销毁新宿主，不允许局部采用。
- 不修改 LAN wire、Unity 生成文件、`Library/`、`Temp/` 或自动生成 `.csproj`。

## Definition of Done

- PRD 的 AC1–AC8 全部有自动化证据。
- Cooking 与 ET runtime 指定门禁全步骤 exit 0，无半恢复或静默降级路径。
- 工作树只包含本任务批准范围内的代码、测试、spec、进度和 task 证据。
