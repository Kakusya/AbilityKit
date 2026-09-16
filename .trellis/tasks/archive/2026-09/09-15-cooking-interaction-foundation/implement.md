# P0 交互基础：实施清单

> 归档状态：本 task 已于 2026-09-16 按 `completed-limited-scope` 归档，Trellis status 为 `completed`；完成只覆盖现有 `check.jsonl` 支持的纯 .NET 受限交付。

> 本 task 已于 2026-09-16 归档，status 为 `completed`，最终语义为 `completed-limited-scope`。以下勾选记录纯 .NET 实施和实际验证；T09-T10 已移入长期禁止的 future scope，未运行且不属于本 task 完成条件。

## 当前实施边界

- 领域/测试项目：`src/AbilityKit.Game.Cooking/`、`src/AbilityKit.Game.Cooking.Tests/`，均已加入 `src/AbilityKit.sln`。
- 首期验证：T01-T08 已实际运行通过。完整测试 10/10 通过；实际命令、8 个 JSONL 文件和 21 条记录见 [design.md](design.md#test-matrix) 与 `check.jsonl`。
- Unity projection、scene fixture、asmdef 与 EditMode T09-T10：`deferred`，没有运行。
- 实施与检查 context 保持 `.trellis/spec/abilitykit/index.md`、`.trellis/spec/abilitykit/validation.md`、对应 cooking spec 和迁移来源。

## 1. 纯 .NET 领域契约与状态

- [x] 1.1 建立独立 Cooking .NET 项目的 Session/World/Match/Player/Item/Definition/Location/生命周期 scope 契约；T01、T02 在同一最小 fixture 中解析跨 session、removed 与 stale 实例。
- [x] 1.2 实现 Item 的全位置唯一占用（world/hand/station/container）、容量与有效性不变量；T01、T04 与额外回归测试验证成功后唯一位置/所有者、初始占位合法性和容量拒绝的 mutation-free snapshot。

## 2. 权威命令处理

- [x] 2.1 实现 pickup/drop envelope、session/player/command 幂等作用域和单一封闭 batch 的稳定排序；T06、T08 在不同入队顺序下得到相同赢家、snapshot 与事件序列，额外回归拒绝混合 simulation batch。
- [x] 2.2 实现统一验证与原子提交，覆盖 scope、生命周期、资格、范围、当前位置、可用性和容量；T02-T04 对拒绝原因、前后 canonical state hash 与无部分事件断言。
- [x] 2.3 实现一次性命令幂等记录与旧/未知实例拒绝；T02、T07 验证重复命令至多一次提交/事件且 stale/removed 受拒绝，额外回归验证内容不同的同 identity command 被 `CommandIdentityConflict` 拒绝。

## 3. 进程内消费者 adapter 与证据

- [x] 3.1 添加 host-local 与 remote-in-process adapter；二者仅调用同一 `CookingSimulation.Submit`，T05 验证结果、snapshot、事件与提交次数等价。
- [x] 3.2 为争抢、重复、重排和失败场景增加 xUnit fixture；T05-T08 全部通过。
- [x] 3.3 为每个受测命令输出 JSONL evidence；字段包括 test ID、scope、排序键、完整 command/result/事件、前后 state hash、断言、runner 与时间。最终实际运行生成 21 条记录；JSONL 不替代测试退出码。

## 4. 延期 Unity 工作（不在本次完成条件）

- [ ] 4.1 **Deferred**：建立只读权威快照到 Unity 表现对象的 projection；T09 EditMode 验证旧/未知输入不覆盖新视图且不改变 authority。
- [ ] 4.2 **Deferred**：建立最小 scene/config fixture；T10 EditMode scene smoke 加载并解析 item、hand、station 与两名逻辑玩家。
- [ ] 4.3 **Deferred**：检查未来 Unity asmdef 与应用层业务边界的消费关系；不得修改自动生成 `.csproj`。

## 5. 测试接入与交付门禁

- [x] 5.1 将 T01-T08 接入 `AbilityKit.Game.Cooking.Tests`，以 `dotnet test` 实际执行：T01-T08 全部通过，完整测试 10/10 通过；可设置 `COOKING_EVIDENCE_DIRECTORY` 以在忽略的 `artifacts/cooking-interaction-foundation/` 保留 21 条 JSONL 工件记录。
- [ ] 5.2 **Deferred**：将 T09-T10 接入未来 Unity EditMode 测试程序集和 fixture 场景；这不是本期通过的证据。
- [x] 5.3 已运行新项目 `dotnet build --no-restore` 与 `git diff --check` 并记录实际结果。既有 `test-gates.json` 没有覆盖新 Cooking 应用层的专属 gate，因此没有误报 `core-stability` / `runtime-contracts` 通过；后续应在 gate 配置中显式纳入本测试项目。
## 2026-09-16 归档范围

- [x] 已按现有 `check.jsonl` 将最终交付重划为：**T01-T08 纯 .NET authority/interaction contract（10/10；8 个 JSONL、21 条记录）**。
- [x] P0 无 non-Unity successor；T09-T10 及全部 Unity 执行移至 `Docs/design/CookingGame/future-scope.md`。
- [x] 已确认未运行项不再属于本旧 task 的完成条件，且没有被改写为 pass。
- [x] 治理 task 已于 2026-09-16 执行 archive；本 task status 为 `completed`。
