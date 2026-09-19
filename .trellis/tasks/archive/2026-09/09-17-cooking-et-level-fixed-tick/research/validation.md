# 实际验证记录

日期：2026-09-19。范围：canonical `CookingLevelLifecycle`、obsolete `CookingMatchLifecycle` facade、simulation-owned atomic fixed Tick、最小 ET Level host、窄范围 ET relation analyzer 与专用 P1 gate。实现已纳入本次提交，任务尚未执行 finish/archive。

## 实际实现

- 新增 canonical Level scope/state/outcome/lifecycle/snapshot/successor contracts；Pause/Resume、Ending/Ended、failed retry 与 success successor 使用显式 Match/Runtime/Level/Epoch 身份。
- gameplay simulation 由 lifecycle-owned gate 控制；调用方保留的 simulation 引用在 Paused/Ending/Ended 下不能通过 `Submit`、`AdvanceFixedTick` 或 fixture item injection 绕过生命周期；host 在 lifecycle 发布 gameplay 前预占 simulation driver ownership，双向冲突不会关闭或重绑定既有 owner。
- Start factory 使用通用重入闸门；返回 simulation 的 ownership、gate 绑定与 publication 在状态发布前完成，失败不开放 admission。
- 旧 Match family 为 `[Obsolete]` facade，准备时使用真实 `CookingMatchPreparation.Level` 创建 canonical scope；Paused/Ending 投影为 unsupported，不伪装 Started；legacy Restart 产生的新代际仍可继续 Prepare/Start。
- fixed Tick 预构造 replacement dictionaries、reverse indexes、event history、immutable process results、tick event 和 final result，commit 只交换引用/写入已计算 scalar；完整校验双向 Process index。
- `CookingLevelEtHost` 使用 `(LevelScope, PlayerId, RecipeCommandId)` typed group identity；实现 minimum batch、canonical SHA-256 fingerprint、duplicate/conflict terminal ledger、fault disposition materialization、Pause queue preservation、retry/successor replacement 与 simulation single-host ownership；canonical host 接管后 raw lifecycle mutation 和 raw simulation command/fixed-tick 写入均被 owner gates 拒绝，只有 host operation/ET frame 可写；Dispose 将未结束 lifecycle 显式收尾为 Aborted/Ended 后再释放 ET tree。
- generation replacement 先安装候选 ET Level/Driver，成功后才提交 source lifecycle 的 next-generation event；安装失败保留 source snapshot/binding/ledger 并 fault host。
- 新增 `AbilityKit.ET.RelationAnalyzer`：只检查真实 `ET.Entity` 的 `AddComponent`/`GetComponent`/`AddChild`/`AddChildWithId`，程序集名无白名单；错误或缺失 `[ComponentOf]`/`[ChildOf]` parent 分别报告 `AKET001`/`AKET002`。该 analyzer 发现并修正 legacy driver 缺失 `[ChildOf(typeof(Scene))]` 与 Entity 后缀的问题。

## 权威通过证据

### Analyzer 与真实项目构建

```text
dotnet build src/AbilityKit.ET.RelationAnalyzer/AbilityKit.ET.RelationAnalyzer.csproj --no-incremental --verbosity minimal
```

exit 0；0 warnings / 0 errors。

```text
dotnet build src/AbilityKit.Game.Cooking.EtRuntime/AbilityKit.Game.Cooking.EtRuntime.csproj --no-incremental --verbosity minimal
```

exit 0；0 warnings / 0 errors。真实 `AbilityKit.Game.Cooking.EtRuntime` 编译加载 relation analyzer；日志中无 `CS8032`、`CS8784`、`CS8785` 或 analyzer/generator load failure。

### 完整测试项目与 TRX

```text
dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj --logger "trx;LogFileName=cooking-level-fixed-tick.trx" --results-directory artifacts/cooking-et-level-fixed-tick
```

exit 0；91 total / 91 passed / 0 failed / 0 skipped。TRX：`artifacts/cooking-et-level-fixed-tick/cooking-level-fixed-tick.trx`。

```text
dotnet test src/AbilityKit.ET.Runtime.Tests/AbilityKit.ET.Runtime.Tests.csproj --logger "trx;LogFileName=et-level-fixed-tick.trx" --results-directory artifacts/cooking-et-level-fixed-tick
```

exit 0；37 total / 37 passed / 0 failed / 0 skipped。包含 4 条 relation analyzer 正负编译测试、canonical host raw lifecycle/simulation authority gates、双向 simulation ownership isolation、host fault retained-reference gate、failed host-construction transaction 与 Dispose lifecycle 收尾回归。TRX：`artifacts/cooking-et-level-fixed-tick/et-level-fixed-tick.trx`。

### 专用 P1 gate

```text
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime
```

exit 0；四步全部通过：

1. ET relation analyzer build：0 warning / 0 error；
2. Cooking ET runtime build with relation analyzer：0 warning / 0 error；
3. Cooking：91/91；
4. ET runtime/analyzer：37/37。

权威 summary：`local/Logs/test-gates/20260919-164215-cooking-et-level-runtime/cooking-et-level-runtime/gate-summary.json`。

### UDP compatibility

```text
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-udp
```

exit 0；23/23。summary：`local/Logs/test-gates/20260919-164225-cooking-udp/cooking-udp/gate-summary.json`。这只证明既有 UDP codec/loopback compatibility，不证明 UDP 已接入 ET，也不证明同机跨进程或两台物理 PC LAN。

## 跨 Sample 观察（不属于本任务验收）

曾额外运行仓库默认 `precheck`：

```text
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate precheck
```

该 gate 在 `tools/test-gates.json` 中明确归属 `Core/MOBA Runtime`，scope 为 `moba-console` / `battle-entry`，步骤仅构建和测试 MOBA Console；它不依赖 Cooking，也不在本任务 AC14、`cooking-et-level-runtime` 或 `cooking-udp` 的验收合同内。因此以下结果只保留为历史诊断，不构成本 Cooking task 的失败、交付 blocker 或归档 blocker：

- `20260918-202548-precheck`：build passed；MOBA smoke 10/10。
- `20260919-053249-precheck`：build passed；MOBA smoke 9/10，失败 `LiveSimSetupActionExecutorTests.Setup_actions_execute_against_real_console_world`（`add_buff failed`）；该精确测试随后单独复跑通过 1/1。
- `20260919-053404-precheck`：build passed；MOBA smoke 9/10，失败项变为 `LiveSimAcceptanceScenarioRunnerTests.Real_dash_hit_expectation_runs_live_through_full_scenario_pipeline`。

本任务不修改、不修复也不继续重跑 MOBA Sample。Cooking task-specific 验收已全部通过；task finish/archive 作为后续工作流步骤单独执行。

## 未运行 / 不适用

- `core-stability`：未运行。本任务未修改通用 ET/Core runtime；新增 analyzer 仅接入 Cooking ET 应用项目，专用 gate 已直接构建并负向验证。
- Unity compile/EditMode：not run / not required。Cooking Unity scope 仍被明确禁止，本任务没有 Unity gameplay/scene/asmdef 变更。
- 同机跨进程 ET、两物理 PC LAN、durable storage、checkpoint restore、successful Kitchen continuation、failed standard-supply rebuild、upgrade Process migration、settlement/persistence、UDP ingress：均未实现或未验证。

## 清理与限制

- 构建触碰的 `AbilityKit.Analyzer.Plugin.dll`、`AbilityKit.Demo.Moba.CodeGen.dll`、`ET.SourceGenerator.dll` 已恢复为 Git 版本。
- 原 ET Source Generator 因程序集名硬编码不能验证当前 Cooking 程序集；本任务未做虚假接线，而是新增窄范围、程序集名无关且有负向测试的 relation analyzer。
- `CookingRecipeSimulation` 仍是 Phase A 唯一 gameplay mutable authority；未创建 Item/Station/Process/Order ET authority state。
- 本实现已纳入提交；task finish/archive 尚未执行。
