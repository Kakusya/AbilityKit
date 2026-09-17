# Implementation checklist

> 状态：planning。只有 owner 审阅并明确批准本次重写后的最终任务，才能运行 `task.py start` 和修改产品代码。

## 0. Before start

- [ ] Owner 审阅并批准 PRD 的八项 Key decisions。
- [ ] 运行 `python ./.trellis/scripts/task.py validate .trellis/tasks/09-17-cooking-et-level-fixed-tick`。
- [ ] 确认 task metadata 的 title/description 已迁移为 Level 语义，branch/base branch 正确。
- [ ] 开工前读取 `Docs/design/CookingGame/reference/README.md`、`product-lifetimes.md`、`et-entity-tree.md`、相关 Cooking specs、ADR-0002 和当前源文件。
- [ ] 检查 Git 状态；保留并行 UDP task、参考文档和用户练习目录改动。

## 1. Canonical Level lifecycle red tests

在 `AbilityKit.Game.Cooking.Tests` 新增 Level-named tests：

- [ ] `CookingLevelScope` 校验 MatchScope/RuntimeId/LevelId/Epoch。
- [ ] 增加完整状态转换表测试：Created/Preparing/Ready 可 Aborted；Paused 只可 Aborted；Success/Failed 只从 Running；BeginEnd 锁定 Outcome；CompleteEnd 必须从 Ending 且 Outcome 已设置。
- [ ] Retry 仅 Failed+Ended、same LevelId、higher Epoch；Successor 仅 Success+Ended、nonblank different LevelId、higher Epoch；所有非法输入 mutation-free。
- [ ] Start 创建且只创建一个 gameplay simulation。
- [ ] Pause 关闭新 admission、保留旧 queue/watermarks；Resume 恢复执行。
- [ ] Failed+Ended retry 复用 LevelId、增加 Epoch。
- [ ] Success+Ended successor 使用新 LevelId、增加 Epoch。
- [ ] Ended 关闭 gameplay，旧 generation 命令/snapshot 拒绝。

Red evidence 必须来自缺少 canonical Level API/行为，而不是测试编译错误或猜错现有 fixture。

## 2. Implement canonical Level lifecycle

预计修改/新增：

- `src/AbilityKit.Game.Cooking/CookingLevelLifecycle.cs`（新增，或按项目文件组织拆分）
- `src/AbilityKit.Game.Cooking.Tests/CookingLevelLifecycleTests.cs`

步骤：

- [ ] 定义 `RestaurantRuntimeId`、`CookingLevelScope`、Level state/outcome/reason/event/result/snapshot/successor contracts。
- [ ] 定义 `ICookingLevelGameplayFactory`，创建 `CookingRecipeSimulation`。
- [ ] 实现单一 `CookingLevelLifecycle` 状态机、preparation 校验、snapshot/version/event sequence。
- [ ] 实现 Pause/Resume、Ending/Ended outcome、Retry/Successor identity rules。
- [ ] 保证所有 rejection mutation-free。
- [ ] 运行 Level lifecycle 聚焦测试并记录 green evidence。

Rollback point：如果 Level scope 被迫复用 `CookingScope.Match`，停止实现并修订设计，不把语义冲突隐藏在字段名中。

## 3. Match compatibility facade red tests

- [ ] 现有 `CookingMatchLifecycleTests` 不修改预期地继续通过。
- [ ] 增加 facade/canonical shared-owner test：同一 gameplay、state/version/event source，不存在双状态机。
- [ ] 增加 obsolete conversion tests，覆盖 legacy state/result/snapshot/restart 投影。
- [ ] 证明 legacy facade 不会把 canonical Paused/Ending 误报为 Started/Ended success；不支持的兼容观察结构化拒绝。

## 4. Implement obsolete Match facade

预计修改：

- `src/AbilityKit.Game.Cooking/CookingMatchLifecycle.cs`
- 旧 focused tests（只增加 compatibility assertions，不重写历史行为）

步骤：

- [ ] 将旧 implementation 收敛为 delegate facade，唯一 mutable 字段是 canonical `CookingLevelLifecycle _inner`；不得保留 facade 自己的 state/version/events/preparation/gameplay/counter。
- [ ] 为 public Match-named API 添加 `[Obsolete]` 指引。
- [ ] 完整转换旧 state/reason/result/event/snapshot/restart contracts。
- [ ] 保留旧 evidence writer/file format，避免破坏历史验证工具。
- [ ] 不重命名 `CookingScope`、`MatchId` 或广泛 LAN/UDP/persistence contracts。
- [ ] 运行旧 lifecycle tests 和新 compatibility tests。

Rollback point：若 facade 需要复制 mutable lifecycle/gameplay state，则方案不合格，回到 canonical API 边界重设，而不是容忍双权威。

## 5. Domain fixed-tick red tests

- [ ] 空 fixed Tick 每帧只增加一次 LogicalTick/Version。
- [ ] 一个 Process 每帧推进一次，RequiredTicks=3 时第三帧完成。
- [ ] 两个 Process 同帧完成时全局 Tick/Version 各只增加一次，产品和结果顺序稳定。
- [ ] 用 injected fixed allocator 触发 product collision（不使用 reflection/test-only mutable production hook），证明 fixed-step state/hash/counter/event/HostFrameSequence 全部不变。
- [ ] closed/wrong Level scope fixed Tick rejection mutation-free。
- [ ] 保留并运行旧 direct `AdvanceTicks` tests。

## 6. Implement simulation-owned fixed tick

预计修改：

- `src/AbilityKit.Game.Cooking/CookingRecipeLoop.cs`
- `src/AbilityKit.Game.Cooking.Tests/` fixed-tick tests

步骤：

- [ ] 定义纯 C# `CookingRecipeTickEvent`、`CookingRecipeTickResult`、ordered process results。
- [ ] API 携带 `CookingLevelScope` 和 `HostFrameSequence`。
- [ ] 用 optional pure `ICookingProductIdAllocator` 注入 sequential production allocator；fixed/recording test allocator 触发 deterministic collision 和验证 ProcessId allocation order。
- [ ] 增加 private `FixedTickPlan`，stage scalar watermarks、item updates/products、process upserts/removals、indexes、counter、ordered results 和 tick event；final commit 不做 validation/allocator/external calls。
- [ ] scope/lifecycle 检查、ProcessId ordinal、完整 preflight、immutable result construction、一次 no-throw commit。
- [ ] 明确 fixed-step delta atomicity：不回滚此前 command-lane mutation；collision equality test 使用零 accepted mutating commands。
- [ ] fixed Tick 永不调用 `ICookingOrderPort`；SubmitOrder 继续普通 command lane 既有语义，并用 call-count test 锁定边界。
- [ ] candidate HostFrameSequence 只在 fixed Tick 成功后发布；失败不消耗 sequence、不生成 tick event/result并 fault host。
- [ ] 每帧只增加一次 LogicalTick/state version。
- [ ] command events 在前，tick event 共用 `_eventSequence` 且在后。
- [ ] fixed Tick 不进入 `_processedCommands`。
- [ ] 保持 existing snapshot/UDP DTO shape 不变。

## 7. ET tree and host red tests

预计新增：

- `src/AbilityKit.ET.Runtime.Tests/CookingLevelEtHostTests.cs`

覆盖：

- [ ] Application/Runtime/Kitchen/Level/Driver 使用 `[ComponentOf]`/Component 后缀；Match 在 MatchRegistry 下使用 `[ChildOf]`/Entity 后缀。
- [ ] Prepare/driver install/factory failure 不留下 Running 或 admitted simulation。
- [ ] Running 每帧选择最小 batch，future batch 保留，stale admission 拒绝。
- [ ] canonical fingerprint golden tests：explicit field order/length/null/invariant encoding，transport/ET metadata excluded。
- [ ] typed group/disposition tests：representative `(ConnectionId,CorrelationId)`、duplicate cached result、all-member conflict rejection、terminal conflict/cancel/stale ledger、arrival-independent ordering。
- [ ] capacity 按 unique pending identity；`batch <= committed watermark` stale；future batch/count/release semantics。
- [ ] command-then-clock ordering；同帧 StartProcess 得到 elapsed=1。
- [ ] host 拒绝 external AdvanceTicks 且不占容量。
- [ ] Pause 拒绝新命令，保留旧 queue 和所有 watermarks，Tick 不推进。
- [ ] Resume 执行旧 queue 并做普通 stale/version validation。
- [ ] Failed retry 取消旧 Epoch pending/frozen envelope，local watermarks reset，HostFrameSequence continuous。
- [ ] Success successor 新 LevelId/higher Epoch，旧 Component/driver 释放。
- [ ] owner thread、reentry、capacity、fault propagation、Dispose/FinalDispositionHistory。

## 8. Implement minimal ET Phase A seam

预计修改/新增：

- `src/AbilityKit.Game.Cooking.EtRuntime/CookingLevelEtHost.cs`
- `src/AbilityKit.Game.Cooking.EtRuntime/` Level Component/System types
- `src/AbilityKit.Game.Cooking.EtRuntime/CookingRecipeTickHost.cs`（仅必要兼容调整）
- project file only if new source layout requires it

步骤：

- [ ] 精确声明并测试：
  - `CookingApplicationComponent` = `[ComponentOf(typeof(Scene))]`；
  - `CookingMatchRegistryComponent` = `[ComponentOf(typeof(CookingApplicationComponent))]`；
  - `CookingMatchEntity` = `[ChildOf(typeof(CookingMatchRegistryComponent))]`；
  - `CookingRestaurantRuntimeComponent` = `[ComponentOf(typeof(CookingMatchEntity))]`；
  - `CookingKitchenComponent`/`CookingLevelComponent` = `[ComponentOf(typeof(CookingRestaurantRuntimeComponent))]`；
  - `CookingLevelDriverComponent` = `[ComponentOf(typeof(CookingLevelComponent))]`。
- [ ] 使用对应 `AddComponent`/`AddChild` API，验证 direct parent、lookup、recursive Dispose 和 Analyzer；不得复制 legacy bridge 中“Component 后缀但无 ComponentOf”的形状。
- [ ] 实现 `CookingLevelSimulationBinding`：full LevelScope 由 host/lifecycle 校验，legacy simulation/commands/snapshot 继续使用 `LevelScope.MatchScope`；不改 UDP/snapshot schema，不把 CookingScope.Match 解释为 LevelId。
- [ ] 实现 TryEnqueue/admission/frame/operation/pending disposition contracts。
- [ ] 实现 minimum-batch、fingerprint grouping、stable sort、command submission、one fixed Tick。
- [ ] 实现 Pause/Resume queue preservation and watermarks。
- [ ] 实现 End/Retry/Successor pending dispositions 与 Component 安装/移除。
- [ ] 捕获 ET System exception，Tick 重抛并 fault host。
- [ ] HostFrameSequence 跨 Pause/Retry/Successor 单调。

## 9. Compatibility and layering regression

- [ ] legacy `CookingRecipeTickHost/CookingRecipeDriver` 与 canonical host 完全隔离；同一 simulation 不得被两个 host/driver 接管，测试证明每帧只有一个 driver 调用。
- [ ] `CookingMatchLifecycleTests` 全部通过。
- [ ] canonical `CookingLevelLifecycleTests` 全部通过。
- [ ] 完整 Cooking tests 通过。
- [ ] 完整 ET runtime tests 通过。
- [ ] 通用 `AbilityKit.ET.Runtime` 不引用 Cooking、不增加 domain/fixed-rate API。
- [ ] 搜索确认没有 Cooking Unity 文件或生成 Unity `.csproj` 改动。
- [ ] 搜索确认本 task 未创建 Item/Station/Process/Order ET authority state。

## 10. Planned validation commands

```text
dotnet build src/AbilityKit.Game.Cooking.EtRuntime/AbilityKit.Game.Cooking.EtRuntime.csproj

dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj \
  --logger "trx;LogFileName=cooking-level-fixed-tick.trx" \
  --results-directory artifacts/cooking-et-level-fixed-tick

dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj \
  --logger "trx;LogFileName=et-level-fixed-tick.trx" \
  --results-directory artifacts/cooking-et-level-fixed-tick

powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-udp
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate precheck
```

Conditional:

- [ ] 新增 `cooking-et-level-runtime` P1 gate 到 `tools/test-gates.json` 并更新测试门禁文档；gate 完整执行 `AbilityKit.Game.Cooking.Tests` 与 `AbilityKit.ET.Runtime.Tests` 两个项目，避免 Trait 零匹配假通过。
- [ ] canonical lifecycle、facade、fixed-tick、ET tree/host 聚焦测试仍统一添加 `Gate=CookingLevelRuntime` Trait，便于报告与单测过滤，但 gate 权威出口执行完整项目。
- [ ] generated DLL touched -> restore unrelated build artifacts;
- [ ] Unity compile remains not-run/not-required because Cooking Unity is prohibited.

## 11. Review and delivery

- [ ] 对照 PRD AC1–AC14 记录 pass/fail/blocked/not-run。
- [ ] 运行 Trellis check；区分 focused pass 与全局门禁状态。
- [ ] 更新 `Docs/Todo.md` 只勾选真实完成的 Level lifecycle/fixed-tick 子项。
- [ ] 提交按可回滚边界拆分：canonical lifecycle + facade；domain fixed tick；ET Level host；tests/docs evidence。
- [ ] 不自动归档，直到代码、检查和 owner 验收边界一致。

## Explicitly deferred

- 完整 Match/Connection/Participant 产品运行时；
- Profile/SaveSlot repository、授权与 claim；
- gameplay Entity authority migration；
- checkpoint；
- success/failure Kitchen migration behavior；
- station upgrade migration；
- settlement/persistence；
- UDP ingress；
- reconnect/host migration；
- multi-Match runtime；
- real-time scheduler；
- Unity Cooking；
- ECS retirement。
