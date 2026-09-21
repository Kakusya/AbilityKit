# Implementation checklist：厨房闭环契约层

> 状态：`planning`。只有 owner 批准本清单后才可 `task.py start`。每步完成后在 `check.jsonl` 记录真实命令与结果；不得把未运行记为通过。

## 0. Before start

- [ ] owner 已审阅 `prd.md` 与 `design.md` 并明确批准。
- [ ] 确认 `git status`，只包含本任务预期的文件；不夹带其他改动。
- [ ] 确认 Cooking UDP 三项目与 `cooking-udp` gate 已退役（阶段 0 已完成），本任务不恢复它们。
- [ ] 记录阶段 0 改动尚未提交这一事实（见第 9 节）。
- [ ] 运行基线：`powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime`，记录当前通过数作为回归基线。

## 1. 红测先行（红 = 契约缺失的 API 不存在，不是编译错误）

> 口径：红测必须指向本任务契约定义的、尚不存在的类型/成员/诊断码。凡需要任务②状态（容器即物品、`PutInto`）才能构造的场景，本任务用纯函数或内部状态 seam 表达，不得提前实现状态模型。

- [ ] v2 身份：schema 常量、v1 候选得到 `MigrationPolicyNotApproved`、v1 快照得到身份拒绝、sha256 不等得到 `IdentityMismatch`。
- [ ] v2 数据模型：多输入集合、默认供应、完成形态、物品容器能力的构造与 canonical 文本包含新字段。
- [ ] v2 校验：多输入外键缺失、输入为空、输入重复、默认供应与物品输入重叠、容器能力引用缺失、容量非正——每条一个诊断，且一次性收集、确定性排序。
- [ ] 纯匹配函数 `CookingRecipeMatcher.Match`：顺序无关（两种输入顺序同一结果）、默认供应并入后命中、无命中返回 `NotMatched`、多命中返回 `Ambiguous`。
- [ ] 命令形状：`IsWellFormed` 对不携带 `Recipe` 的 StartProcess 的判定；`RecipeNotMatched` / `RecipeAmbiguous` / `ContainerRejectsItem` 拒绝原因存在。
- [ ] 指纹：既有 golden 向量断言保持为回归锚；新增 recipe-less StartProcess 的 golden 期望（先写“必须存在且稳定”的断言框架，期望值在实现后按真实输出锚定并记录）。
- [ ] 两种命令模式（携带 `Recipe` / 不携带）的 JSON 指纹不同且各自幂等。

## 2. 配置 schema v2

- [ ] `src/AbilityKit.Game.Cooking/CookingConfigurationValidation.cs`：`CurrentSchema = "cooking-definition-v2"`。
- [ ] canonical 文本与 record 增加物品容器能力、配方输入集合、默认供应、完成形态；可空集合统一归一化为空集（不得时缺席时为空）。
- [ ] `CreateSnapshot` 逐条重建时**透传新增的 `Container` 参数**（漏掉会静默丢失容器能力）。
- [ ] 校验补齐 R1 全部诊断码，保持一次性收集与确定性排序。
- [ ] `CookingItemDefinition`（`CookingDomain.cs`）尾部追加可选 `Container` 参数；确认旧构造点仍编译。
- [ ] `CookingRecipeDefinition` 输入改集合；同步 `CookingRecipeLoop.cs` fixture 构造校验（:65）。
- [ ] 更新 `CookingConfigurationValidationTests` 中依赖旧诊断字段名的断言（原 `InputDefinition` / `missing-input` / `missing` 相关断言）。

## 3. 纯匹配函数与命令形状

- [ ] 新增 `CookingRecipeMatcher`（纯函数，无仿真状态依赖）：输入物品定义集合 + 候选配方默认供应 + 工位能力 → 唯一配方 / `NotMatched` / `Ambiguous`；按 DefinitionId 排序比较。
- [ ] `CookingRecipeCommandValidation.IsWellFormed` 的 StartProcess 分支不再强制 `Recipe`。
- [ ] 拒绝原因枚举**尾部**追加三个新值（不重排、不插入）。
- [ ] `StartProcess`：携带 `Recipe` 时按显式配方校验（`item.Definition ∈ recipe.Inputs`，替代原单值相等）；不携带时返回 `RecipeNotMatched` 结构化拒绝，不抛异常。
- [ ] `ValidateProcessForFixedTick`：输入定义成员判定改为 `Input ∈ recipe.Inputs`（过渡语义，多输入进程属任务②）。
- [ ] 帧路径无异常污染；匹配失败/歧义零 mutation。

## 4. 指纹与金样

- [ ] 确认 `CookingCommandFingerprint.CanonicalBytes` 因命令 record 无字段变化而输出不变：既有 golden 十六进制向量断言保持通过（作为“指纹未被意外改动”的回归锚）。
- [ ] 新增 recipe-less StartProcess 命令的 golden 向量（canonical 字节 + SHA-256），并在证据中记录该向量的生成方式。
- [ ] 三处身份结论一致性的测试：同一命令在 JSON 指纹、`IsWellFormed`、二进制指纹下得到相同的接受/拒绝与去重结论。
- [ ] 两种命令模式的 JSON 指纹不同，且各自重放返回 `IsDuplicate` 且零事件。

## 5. spec 修约

- [ ] `.trellis/spec/cooking/cooking-recipe-loop.md`：完成语义与幂等延伸到倒出。
- [ ] `.trellis/spec/cooking/cooking-interaction-foundation.md`：位置枚举补 ContainerSlot 与容器能力表述。
- [ ] `.trellis/spec/cooking/cooking-config-validation.md`：v1→v2 blocked、无迁移落到具体条款。
- [ ] `.trellis/spec/cooking/index.md`：P2/P3 行边界与“未完成范围入口”列更新。
- [ ] 每处修订注明“旧条款 → 新条款 → 来源”，区分 owner 决定与经批准的本 design。
- [ ] **每份被修订 spec 的头部补一条带日期（2026-09-21）与来源的修约记录**，避免新条款与既有“2026-09-16 收口状态”声明静默并存。

## 6. 回归与证据

- [ ] `powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime` 实际运行并记录（含 gate-summary.json 路径）。
- [ ] evidence 落 `artifacts/cooking-kitchen-loop-contracts/`（JSONL，含既有 golden 向量未变的确认与新增向量的生成方式）。
- [ ] `git diff --check` 无空白错误。

## 7. Review and delivery

- [ ] 逐条对照 `prd.md` Acceptance Criteria 记录通过/失败/受阻。
- [ ] 未运行项（如 Unity 环境缺失）记 not-run 与原因，不计为通过。
- [ ] `task.py finish`；归档用 `task.py archive 09-21-cooking-kitchen-loop-contracts --no-commit --skip-branch-validation`，是否提交由 owner 决定。

## 8. Explicitly deferred

- 容器即物品的运行时实现、七项动作（`PutInto`/`Drop`/`TakeOut`/`Pour`）、两种完成形态的推进逻辑、闭环 fixture（任务②）。
- 纯匹配函数到仿真状态的接线（任务②：容器内容集合 → matcher）。
- `AdvanceTicks` 命令路径的产品 ID 分配收敛到 `ICookingProductIdAllocator`（当前内联 `product-{n}`，本任务测试只覆盖 fixed-tick 路径）。
- harness、golden 之外的验收证据与门禁扩展（任务③）。
- 前厅、跨小关、检查点、失败条件、ET 权威迁移、UDP/KCP、Unity。

## 9. 工作树状态提醒

- 阶段 0 的 UDP 退役改动（含 09-16 归档移动、test-gates.json、门禁文档、harness 脚本、progress/Todo/acceptance 文档）目前是**未提交**的工作树状态。步骤 0 的基线 gate 结果包含这些未提交改动，check 证据中必须记录这一点；本任务交付时应提示 owner 将阶段 0 与本任务改动一并提交或分别提交，避免出现“门禁已删但归档未入库”的中间态。
