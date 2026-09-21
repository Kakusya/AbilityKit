# 任务②验证记录（2026-09-21）

> 本文件记录 task `09-21-cooking-kitchen-loop-simulation` 的验收方法与实际证据。所有结论以本文件与 `check.jsonl` 记录的真实命令为准；`artifacts/` 与 `local/Logs/` 为 gitignore 的本地工件，不随提交分发。

## 1. 验收标准对照（PRD K01-K12）

| 标准 | 内容 | 测试 | 结果 |
|---|---|---|---|
| K01 | 容器即物品，独立容器表退役，快照/canonical 可观察内容物 | CookingContainerAsItemTests C01-C06 | 通过 |
| K02 | 七项动作各有 happy path + mutation-free 拒绝；Plate 退役；ET 指纹金样重锚 | CookingKitchenActionsTests A01-A11；CookingRecipeCommandShapeTests S10；CookingLevelEtHostTests PutIn golden | 通过 |
| K03 | 放入即拒绝（锅自己的配置声明） | A03/A04；P01/P09 | 通过 |
| K04 | 锁输入：放下/放入/取出/倒出拒绝；容器锚点可拾取（端走继续） | A04/A10；P04/P05 | 通过 |
| K05 | 白名单腐败检测器可达（注入证明） | P06/P07/P08 | 通过 |
| K06 | 两种完成形态原子提交；倒出至多生成一次 | F01-F04；L01/L02 | 通过 |
| K07 | 命令路径与 fixed-tick 路径共享 allocator；故障 mutation-safe | F05/F06；既有 fixed-tick allocator 回归 | 通过 |
| K08 | 番茄蛋花汤闭环 fixture 端到端 + 确定性重放 | L01/L02 | 通过 |
| K09 | 订单要求匹配/拒绝/不回滚 | L03 | 通过 |
| K10 | 碗池上限、提交后交 NPC、注入清洗回池 | L01/L04 | 通过 |
| K11 | 批次争抢稳定排序与幂等 | G01-G05 | 通过 |
| K12 | 新 gate 存在且可运行；门禁文档同步 | tools/test-gates.json；规范文档 §3 | 通过 |

## 2. 实际执行的门禁

```
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop
→ Gate 'cooking-kitchen-loop' passed（5 步骤：域构建、ET runtime 构建、focused 42/42、Cooking 163/163、ET 40/40）

powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime
→ Gate 'cooking-et-level-runtime' passed（既有回归）

dotnet test src/AbilityKit.Game.Cooking.EtBridge.Tests/... → 4/4（不在门禁步骤内，补跑）
dotnet build src/AbilityKit.sln → 通过（仅既有 Unity GameFramework 包警告）
```

门禁日志：`local/Logs/test-gates/20260921-173258-cooking-kitchen-loop/`（TRX ×3 + gate-summary.json）。

## 3. 独立复验（不调用被测 C# 代码）

复验器：`local/verify/kitchen-loop-verify.py`（gitignore 的本地工具）。实际输出：

```
independent verification: 44 checks, 0 failures
```

覆盖：

1. **门禁 TRX**：3 个测试步骤 TRX 全部解析，failed=0、notExecuted=0；focused=42、Cooking=163、ET=40 与门禁定义一致。
2. **ET 指纹金样独立解码**：按 `CanonicalWriter` 布局（大端 int32/int64、长度前缀字符串、0x00/0x01 可空标记）在 Python 手工解码 PutIn golden 十六进制，逐字段核对（session/world/match/runtime/level/epoch/batch/player/command/operation=5/item/container/version/ticks），消耗长度等于全长，并用 hashlib 独立重算 SHA-256 与锚定值一致。
3. **evidence JSONL 自洽性**：21 个文件逐行合法 JSON（utf-8-sig，C# `Encoding.UTF8` 写 BOM）；TestId/断言摘要非空；前后状态哈希为 64 位大写十六进制；拒绝记录零事件、接受记录至少一事件、重复重放零事件、接受记录前后哈希不同；七项动作在 evidence 中全部出现；L01 闭环 16 条命令全部 Accepted 且覆盖 Pickup/Drop/PutIn/Pour/StartProcess/SubmitOrder。
4. **门禁配置一致性**：`cooking-kitchen-loop` 在 `tools/test-gates.json` 注册为 P1，含 `Gate=CookingKitchenLoop` focused 步骤，覆盖两个测试项目。

## 4. 变异测试（证明关键断言非空转）

对 `CookingRecipeLoop.cs` 临时施加三类破坏，确认测试失败后恢复原文件（diff 校验一致）：

| 变异 | 破坏的行为 | 被杀情况 |
|---|---|---|
| 移除 RetainInputs 完成的 `ContainerCompleted` 标记 | 锅切“已完成” | 完成形态 + 闭环 fixture 11 个测试中 7 个失败 |
| 倒出后不清除已完成标记 | 倒出幂等（至多生成一次） | 完成形态测试 1 个失败（F02/F03） |
| `IsLockedInput` 恒 false | 锁输入 | 动作 + 锚点测试 20 个中 2 个失败（A10/P05） |

## 5. 测试方法学说明

- 红测先行不适用于纯 API 形状重构（测试无法编译）：阶段 1-2 采用“先迁移既有测试到绿、再补新行为测试”；阶段 3-6 的新行为均有独立新测试，并在开发中实际抓到 6 个产品缺陷（锚点自身被误用容器槽规则、工位绑定分歧被匹配失败掩盖、容器内产物未入容器索引、命令路径 LogicalTick 先于 allocator 推进、消耗先于分配、显式配方只查外域不查齐全）。
- 拒绝路径统一断言“前后 canonical 相等”（mutation-free），由 evidence 写入帮助方法强制执行。
- 快照哈希与 evidence 由测试自身断言（行数、TestId、哈希非空），复验器只做独立解析，不调用产品代码。

## 6. 未覆盖/边界

- Unity 一切范围未运行（prohibited）；EtBridge 投影测试仅既有 4 个，未新增容器内容投影用例（投影非权威且本任务范围外）。
- 前厅（顾客/NPC 过程/订单生成节奏）、小关时间结构、失败条件未实现，spec 修约已显式声明。
- 推定项（无单独 owner 裁决原文）清单见 `.trellis/spec/cooking/cooking-recipe-loop.md` 头部“实现状态声明”。
