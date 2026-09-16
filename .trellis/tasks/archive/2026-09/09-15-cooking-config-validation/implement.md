# P3 数据配置验证：实施清单

> 归档状态：本 task 已于 2026-09-16 按 `completed-limited-scope` 归档，Trellis status 为 `completed`；完成只覆盖现有 `check.jsonl` 支持的纯 .NET 受限交付。

> 以下未勾选段落是**只读历史规划**：不可执行、不是当前 task checklist，也不是 archived limited delivery 的 blocker。non-Unity 后续只见 successor backlog；Unity 只见 prohibited future scope。

## 迁移前置条件

- 依赖：P2 的实际 Recipe/Process/Appliance/Level 数据类型。
- 阻塞：旧 config/snapshot 迁移策略与相关 owner 决策尚未确认。
- 规划验证：C01-C08（均为 future 规划，尚未执行）
- 实施前将 `.trellis/spec/abilitykit/index.md`、`.trellis/spec/abilitykit/validation.md` 与本任务对应 cooking spec 加入 context manifests。

## 当前受限实施状态

- [x] 1.1 已复核当前 P2 实际定义并在 `src/AbilityKit.Game.Cooking/` 新建应用层 registry；未改动通用 ConfigDatabase、Unity `.asmdef`、自动生成 `.csproj` 或 Orleans。
- [x] 1.2 已实现候选批次、重复/必填字段检查、全量结构化诊断与原子提交；C01/C02 当前定义层范围已实际通过。
- [x] 2.1 已实现现有类型可表达的 Item 引用、Appliance capability 与 Container 容量验证；Item/Ingredient/Process/Level 的完整表关系仍需真实 schema。
- [x] 2.3 已以第二 Recipe/Appliance 数据 fixture 实际验证同一规则路径（C04/C05 当前范围）；这不是正式内容批准，也不替代 P2/P3 完整出口。
- [x] 3.1 已实现 definition-only canonical identity/hash 与加载顺序无关的 C08 回归；不使用 runtime snapshot hash。
- [x] 3.3 已实现 schema 不一致的显式 blocked 结果（C07 的无迁移分支）；没有实现、选择或暗示旧 config/snapshot 转换。
- [ ] 2.2 C03 Process 图循环、断裂和不可达终点：**blocked**，当前 P2 没有 Process graph 或 Level 数据形状。
- [ ] 3.2 C06 host/client binding compatibility：**blocked**，未接入 real transport/LAN；P1 D1-D4 和 LAN 门仍有效。
- [ ] **只读历史、不可执行** 4.1 Unity EditMode/加载 smoke：**not run / future**，本轮无 Unity Cooking 宿主。
- [ ] 4.2 LAN 分层 host/client 验收：**blocked/not run**，不以 in-process 或当前 config identity 替代。
- [ ] 4.3 全局 `runtime-contracts`/`core-stability`：**not run**，现有 gate 未覆盖独立 Cooking 项目；实际 .NET build/test 与 `git diff --check` 在 check evidence 记录。
- [ ] 4.4 P4 准备条件：完整 P3 尚 blocked，不能解除后续任务阻塞。


## 只读历史规划说明

- 下文仍保留的 `Draft`、`blocked`、future matrix 与未勾选 checklist 仅用于历史追溯，**不可执行、不是当前 task checklist，也不是本 limited delivery 的 blocker**。
- 未完成的 non-Unity 范围（正式 Process/Level/Map schema、host/client compatibility 与 migration policy）只以 [`successor-backlog.md`](../../../Docs/design/CookingGame/successor-backlog.md) 为入口；如获批准必须新建 task。
- Unity package、asmdef、scene、authoring、projection、UI、EditMode 与 Unity smoke 长期禁止；只见 [`future-scope.md`](../../../Docs/design/CookingGame/future-scope.md)，不得按下文历史指令实施。

## 迁移的原实施清单

## 规划状态

以下任务均为未执行的实施规划，当前保持未勾选；checkbox 应在未来实施并完成对应验证后更新，不是本 change 的永久验收条件。

## 1. 配置注册与候选批次

- [ ] 1.1 复核阶段 3 实际 Recipe/Process/Appliance/Level 数据类型并建立应用层 registry；验证意图仅作历史记录；non-Unity registry 后续见 successor backlog，Unity 宿主审查已禁止。
- [ ] 1.2 建立候选配置批次加载、唯一 ID 与必需字段检查；验证：C01 合法批次可查询且提交一次，C02 重复 ID/缺字段输出稳定诊断并保持旧版本不变。

## 2. 跨表校验与数据扩展

- [ ] 2.1 实现外键、Appliance 能力、Container/产物和 Level 引用验证；验证：C02 对每类缺失/不匹配关系输出表、记录、字段定位并阻断启动。
- [ ] 2.2 实现 Process 图的循环、断裂和不可达终点检测；验证：C03 输出可审阅的关系路径/诊断 artifact，非法批次不提交。
- [ ] 2.3 建立“仅修改数据新增 Recipe/Appliance”的参数化 fixture；验证：C04-C05 使用同一 validator 与阶段 3 loop runner 完成闭环，不改规则代码；未知能力/Process 按 C02 失败。

## 3. 配置身份与兼容门

- [ ] 3.1 实现已验证配置的稳定规范化 identity/hash，区分 DefinitionId 与 Match InstanceId；验证：C01/C08 在不同表加载顺序下 hash 相同，单字段变更可检测。
- [ ] 3.2 在阶段 2 handshake seam 可用后接入 host/client hash compatibility gate；验证：C06 一致 hash 可继续，不一致在 gameplay binding 前拒绝并产出报告；缺少阶段 2 证据时保持 blocked。
- [ ] 3.3 定义旧 config/snapshot 的拒绝与 blocked 结果，不实现未批准迁移；验证：C07 明确输出 owner 决策引用，不能静默转换。

## 4. 宿主测试与阶段出口

- **只读历史、不可执行：**该 Unity package/asmdef/scene/EditMode/host 指令已由 prohibited future scope 取代；本 task 不实施，也不把它作为 blocker。
- [ ] 4.2 在阶段 2 LAN/决策门需要时执行分层 host/client 验收；验证：C06 的 LAN 证据不以同机/in-process 替代，P1 D1-D4 仍按 owner 门检查。
- [ ] 4.3 执行实际受影响门禁（至少评估 `runtime-contracts`/`core-stability`）及 `git diff --check`；验证：交付报告记录真实 pass 或环境导致的 skip，任务仅在实施完成对应验证后更新 checkbox。
- [ ] 4.4 将 P3 交付证据指向阶段 5/P4 的准备条件；验证：未完成 config/layout 校验时 P4 任务标为 blocked，并直接引用现有后续 change 的规划文档 `../add-cooking-match-lifecycle/proposal.md`；不把后续规划当作实现证据。
## 2026-09-16 归档范围

- [x] 已按现有 `check.jsonl` 将最终交付重划为：**当前 definition 范围的纯 .NET registry/validation/hash 增量（focused 6/6；当时完整回归 34/34）**。
- [x] Process/Level/Map 正式 schema、host/client compatibility 与 migration policy 移至 successor backlog；Unity 移至 future scope。
- [x] 已确认未运行项不再属于本旧 task 的完成条件，且没有被改写为 pass。
- [x] 治理 task 已于 2026-09-16 执行 archive；本 task status 为 `completed`。
