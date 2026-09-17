# Implementation checklist

> 状态：planning。只有 owner 审阅并明确批准本任务后，才能运行 `task.py start` 和修改产品代码。

## 0. Before start

- [ ] Owner 审阅并批准 `prd.md` 的五项 Key decisions。
- [ ] 运行 `task.py validate 09-17-cooking-et-match-fixed-tick`，确认 context manifests 有效。
- [ ] 确认实际工作分支和 base branch metadata，不与并行 UDP task 混淆。
- [ ] 开工前重新读取 Cooking recipe/match specs、ADR-0002、归档 ET 路线验证记录和当前源文件。
- [ ] 检查 Git 状态；保留并行任务与用户练习目录改动。

## 1. Red tests — domain fixed tick

- [ ] 在 `AbilityKit.Game.Cooking.Tests` 增加“空固定 Tick 每帧只增加一次 LogicalTick/Version”的失败测试。
- [ ] 增加一个 Process 每帧推进一次、第三帧恰好完成的失败测试。
- [ ] 增加两个 Process 同帧完成时全局 Tick/Version 各只增加一次、产品和结果顺序稳定的失败测试。
- [ ] 增加 fixed Tick preflight 碰撞测试：用既有 setup API 预占确定性的下一 product identity，在多 Process 完成帧触发冲突，证明 LogicalTick、Version、Process、Item、计数器和两类事件历史全部保持帧前状态；不加入测试专用生产钩子。
- [ ] 增加 lifecycle closed 后固定 Tick 拒绝且状态不变的失败测试。
- [ ] 保留并运行旧 direct `AdvanceTicks` 行为测试，锁定兼容边界。

Red evidence 必须说明失败来自缺少 fixed-tick API/行为，而不是测试编译错误或 fixture 猜错。

## 2. Implement simulation-owned fixed tick

预计修改：

- `src/AbilityKit.Game.Cooking/CookingRecipeLoop.cs`
- `src/AbilityKit.Game.Cooking.Tests/` 中聚焦测试

步骤：

- [ ] 定义 domain-only `CookingRecipeTickEvent`、`CookingRecipeTickResult` 与 ordered per-process result；结果携带 Scope/Epoch/HostFrameSequence。
- [ ] 实现 `CookingRecipeSimulation.AdvanceFixedTick(...)` 的 staged transaction：closed/scope 检查、ProcessId ordinal 排序、完整预验证、临时状态构建、一次性 commit。
- [ ] 每个固定帧只增加一次 `LogicalTick` 和 `_stateVersion`；command events 在前，tick event 共享 `_eventSequence` 且在后；不写入玩家命令 ledger。
- [ ] 确保空 Tick 同样可观察，且多 Process 数量不影响 frame-level version 增量。
- [ ] 运行聚焦 domain tests，记录 green evidence。

Rollback point：若新增 fixed tick 破坏旧 direct `AdvanceTicks`，先恢复兼容合同，不以删除旧测试解决。

## 3. Red tests — Match ET host

预计新增：

- `src/AbilityKit.ET.Runtime.Tests/CookingMatchEtHostTests.cs`

覆盖：

- [ ] Preparing/Ready 的结构化 admission rejection，不推进时钟。
- [ ] Prepare 先创建 inert Match Entity；ET entity 安装失败不调用 gameplay factory，Start 失败保持 admission closed。
- [ ] 每帧只执行最小 SimulationBatch，future batch 留待下一帧，stale batch admission 拒绝。
- [ ] 同 batch 稳定排序；相同 fingerprint duplicate collapse；conflicting fingerprint 整组拒绝且到达排列无影响。
- [ ] Tick 冻结批次，并在命令后执行一次 fixed tick；同帧 StartProcess 得到 elapsed=1。
- [ ] 外部 `AdvanceTicks` 在 TryEnqueue 阶段被拒绝且不占容量。
- [ ] End/Restart 对每条 pending command 给出 ordered disposition，HostFrameSequence 不因 Restart 归零。
- [ ] queue capacity、owner thread、reentry、fault propagation、Dispose 后 FinalDispositionHistory。

## 4. Implement CookingMatchEtHost

预计修改/新增：

- `src/AbilityKit.Game.Cooking.EtRuntime/CookingMatchEtHost.cs`（新增）
- `src/AbilityKit.Game.Cooking.EtRuntime/CookingRecipeTickHost.cs`（仅在共享内部机制必要时小范围调整）
- `src/AbilityKit.Game.Cooking.EtRuntime/AbilityKit.Game.Cooking.EtRuntime.csproj`（通常不需改引用）

步骤：

- [ ] Prepare 成功后建立 inert Match Entity；只有 Entity 已存在时才调用 lifecycle Start，并以非抛异常的引用赋值绑定 gameplay driver。
- [ ] 定义 `TryEnqueue` admission、frame result、host operation result 与 pending disposition，不把 orchestration rejection 塞入错误的领域枚举。
- [ ] Tick 选择 live queue 的最小 SimulationBatch；future batch 保留，按 Player/Command ordinal 排序。
- [ ] 执行前按 Session/Player/Command 分组：相同 fingerprint 折叠并返回 duplicate disposition，冲突 fingerprint 整组拒绝且不进入 ledger。
- [ ] 提交合法命令，随后调用一次 `AdvanceFixedTick(scope, epoch, hostFrameSequence)`。
- [ ] 捕获 ET System 内异常并在 Tick 重新抛出，宿主进入 faulted。
- [ ] End/Restart 返回 lifecycle result + ordered pending dispositions；Dispose 保存 immutable FinalDispositionHistory；所有取消项不进入 simulation ledger。
- [ ] HostFrameSequence 在宿主生命周期内单调且 Restart 不归零；frame/tick result 携带 Scope/Epoch。
- [ ] 保持 owner-thread、idle/reentry 与 bounded queue 约束。

## 5. Compatibility and regression

- [ ] 现有 `CookingRecipeTickHost` 回归继续通过；若被新 host 替代，迁移测试必须证明相同行为后再删除。
- [ ] `CookingMatchLifecycleTests` 全部通过。
- [ ] `CookingRecipeLoopTests`/完整 Cooking tests 全部通过。
- [ ] ET runtime 现有 lifecycle/vertical slice tests 全部通过。
- [ ] 通用 ET runtime 项目不新增 Cooking 引用；`EtRuntimeHost` 不引入 fixed-rate/domain API。
- [ ] 搜索确认没有新增 Cooking Unity 文件或修改自动生成 Unity `.csproj`。

## 6. Planned validation commands

```text
dotnet build src/AbilityKit.Game.Cooking.EtRuntime/AbilityKit.Game.Cooking.EtRuntime.csproj

dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj \
  --logger "trx;LogFileName=cooking-match-fixed-tick.trx" \
  --results-directory artifacts/cooking-et-match-fixed-tick

dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj \
  --logger "trx;LogFileName=et-match-fixed-tick.trx" \
  --results-directory artifacts/cooking-et-match-fixed-tick

powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-udp
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate precheck
```

条件门禁：

- [ ] 若修改 shared core/runtime 行为，追加 `core-stability`。
- [ ] 若引入专用 `cooking-et-runtime` gate，更新 `tools/test-gates.json` 与测试门禁文档，并把它作为新宿主聚焦出口。
- [ ] `cooking-udp` 只记录旧外部 AdvanceTicks/UDP 兼容回归，不作为新 Match host 或自动 fixed tick 的验收证据。
- [ ] 若构建触碰生成 DLL，核对并恢复无关副作用，不把它们混入提交。
- [ ] Unity compile 不作为本任务通过条件；未运行必须记录为 not-run，不能写 passed。

## 7. Review and delivery

- [ ] 对照 PRD AC1–AC10 逐条记录 pass/fail/blocked/not-run。
- [ ] 运行 Trellis check，区分聚焦通过与全局门禁状态。
- [ ] 更新 `Docs/Todo.md` 只勾选本任务真实完成的子项；checkpoint/UDP/迁移/持久化/ECS 清退保持未完成。
- [ ] 提交按可回滚边界拆分：domain fixed tick；ET Match host；tests/docs evidence。
- [ ] 不自动归档，直到代码提交、检查记录和 owner 验收边界一致。

## Explicitly deferred

- checkpoint restore；
- 跨成功小关延续；
- 失败标准供应；
- 工位升级 Process 迁移；
- settlement/durable persistence；
- UDP/network-thread ingress；
- 多 Match scene；
- Unity Cooking 实现；
- ECS 清退。
