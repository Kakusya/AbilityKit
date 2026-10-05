# AbilityKit 测试门禁与批量回归规范

## 1. 目标

AbilityKit 当前同时包含 Unity UPM 包源码、纯 C# runtime、MOBA/Shooter 示例、服务端实验工程和工具链。为了避免大型项目常见的“功能继续堆叠但基础链路已回归”的问题，测试流程必须从零散命令升级为统一门禁制度。

本规范的目标是：

1. 把单元测试、contract 测试、smoke 测试和批量回归测试分层管理。
2. 明确哪些门禁是继续开发前必须通过的阻断项。
3. 为后续 CI、批量回归、阶段性收口提供统一入口。
4. 让每个关键功能都有可追踪的测试标签和文档依据。

统一执行入口是 [`tools/run_test_gate.ps1`](../tools/run_test_gate.ps1)，门禁清单是 [`tools/test-gates.json`](../tools/test-gates.json)。

## 2. 门禁分层

| 层级 | 类型 | 典型门禁 | 触发时机 | 失败处理 |
| --- | --- | --- | --- | --- |
| P0 | Development Blocker | `precheck`、`moba-console-smoke` | 每次继续功能开发前、提交相关改动前 | 必须立即修复，不继续写新功能 |
| P1 | Contract Blocker | `core-stability`、`runtime-contracts`、`moba-content-contracts`、`moba-xiaoqiao-unity` | 核心包、网络、DI、表现运行时、内容配置或跨模块契约变化后，以及需要确认 Unity 权威技能结果时 | 修复契约破坏，或同步更新设计文档和测试预期 |
| P2 | Regression Baseline | `regression` | 大范围重构、合并前、阶段性收口、候选发布前 | 作为合并/发布阻断项处理 |

### 2.1 P0：继续开发前置门禁

P0 关注最短反馈链路。任何影响 MOBA console、战斗入口、表现层可测试性、runtime 输入链路的改动，都必须至少通过 `moba-console-smoke`。

推荐命令：

```powershell
powershell -ExecutionPolicy Bypass -File tools\run_test_gate.ps1 -Gate moba-console-smoke
```

如果只是确认本地继续开发状态，运行默认门禁：

```powershell
powershell -ExecutionPolicy Bypass -File tools\run_test_gate.ps1
```

### 2.2 P1：运行时契约门禁

P1 关注跨模块契约是否被破坏，例如：

- Record 协议兼容性、StateSync/FrameSync 状态与所有权契约。
- Host、Triggering、Context 生命周期和失败语义。
- Attributes/Modifiers 正确性，以及 Attributes 已预热 dirty recompute 的严格零分配契约。
- 核心 UPM 包生产程序集引用与直接 `package.json` 依赖声明的一致性。
- 网络 runtime 协议行为、World DI 生命周期和通用表现运行时 contract。

核心包改动推荐运行：

```powershell
powershell -ExecutionPolicy Bypass -File tools\run_test_gate.ps1 -Gate core-stability
```

网络、DI 或表现运行时边界改动推荐运行：

```powershell
powershell -ExecutionPolicy Bypass -File tools\run_test_gate.ps1 -Gate runtime-contracts
```

### 2.3 P2：批量回归门禁

P2 是阶段性收口标准，用于大范围重构或合并前。它不替代 P0/P1，而是在 P0/P1 已经稳定后，用来确认主要纯 C# 测试面没有明显回归。

推荐命令：

```powershell
powershell -ExecutionPolicy Bypass -File tools\run_test_gate.ps1 -Gate regression
```

## 3. 当前门禁清单

门禁定义以 [`tools/test-gates.json`](../tools/test-gates.json) 为准。当前门禁包括：

| 门禁 | 层级 | 负责人域 | 作用域 | 阻断点 |
| --- | --- | --- | --- | --- |
| `precheck` | P0 | Core/MOBA Runtime | 本地开发、MOBA console、战斗入口 | 继续功能开发、提交 runtime/console 相关改动前 |
| `moba-console-smoke` | P0 | MOBA Runtime/Presentation | MOBA console、表现层可测试性、战斗 smoke、技能 trace | 继续 MOBA 表现层/runtime 后续开发前 |
| `moba-codegen` | P1 | MOBA Runtime/Compile Time | MOBA Source Generator、Analyzer、Unity package 所有权 | 合并 MOBA 代码生成、静态分析或生成清单改动前 |
| `cooking-et-level-runtime` | P1 | Cooking Game Runtime | canonical Level lifecycle、旧 Match facade、simulation fixed Tick、ET relation analyzer 与 ET Level host | 合并 Cooking Level lifecycle/fixed-tick/ET host 改动或宣称对应 runtime contract 前；构建 relation analyzer 与带 analyzer 的 ET runtime，并完整执行 Cooking 与 ET 两个测试项目 |
| `cooking-kitchen-loop` | P1 | Cooking Game Runtime | 容器即物品、七项权威动作、放入即拒绝、锁输入、端走继续、两种完成形态、番茄蛋花汤闭环 fixture、命令争抢仲裁与恢复 checkpoint 契约（snapshot/checkpoint 区分、导出-恢复等价） | 合并 Cooking 厨房闭环仿真改动或宣称对应 runtime contract 前；构建 Cooking 域与带 analyzer 的 ET runtime，执行 `Gate=CookingKitchenLoop` focused 测试并完整跑通 Cooking 与 ET 两个测试项目 |
| `core-stability` | P1 | Runtime Platform | Record、StateSync、FrameSync、Host、Triggering、Context、Attributes、Modifiers、核心 UPM 直接依赖与 Attributes 零分配契约 | 合并核心包、生命周期、同步或 Attributes/Modifiers 热路径改动前 |
| `runtime-contracts` | P1 | Runtime Platform | 网络 runtime、World DI、Game View Runtime | 合并 runtime contract 变化前 |
| `moba-content-contracts` | P1 | MOBA Content Pipeline | 包资源所有权、TriggerPlan 聚合漂移、配置资源可加载性、跨表有效时序 | 发布 MOBA 内容或合并配置/资源改动前 |
| `moba-acceptance-dotnet` | P1 | MOBA Acceptance/Testing | 无 Unity 的 dotnet 验收判定（STJ 解析真实期望 + harness-free 判定器 + 批量 runner → batch_summary.json）、期望 schema、dsl-regression 核心 | 合并验收判定层（`AbilityKit.Demo.Moba.Acceptance`）或 `MobaAcceptanceModels`/期望 schema 改动前；行为覆盖随 trace fixture 增长 |
| `regression` | P2 | AbilityKit Engineering | 核心包、MOBA、Shooter、runtime contracts 批量回归 | 大范围重构、候选发布、批量合并前 |
| `moba-xiaoqiao-unity` | P1 | MOBA Runtime/Unity Test | 小乔四个 Unity EditMode 权威用例及其落盘产物 | 宣称最新小乔 Unity 结论前 |

查看完整门禁：

```powershell
powershell -ExecutionPolicy Bypass -File tools\run_test_gate.ps1 -List
```

## 4. 配置规范

### 4.1 顶层字段

[`tools/test-gates.json`](../tools/test-gates.json) 的顶层字段用于描述门禁体系本身：

| 字段 | 含义 |
| --- | --- |
| `schemaVersion` | 配置结构版本。脚本和文档演进时递增。 |
| `owner` | 门禁体系维护责任域。 |
| `defaultGate` | 不传 `-Gate` 时默认执行的门禁。 |
| `documentation` | 对应规范文档路径。 |
| `policies` | P0/P1/P2 的统一规则说明。 |
| `gates` | 实际门禁列表。 |

### 4.2 单个门禁字段

每个门禁必须包含以下信息：

| 字段 | 必填 | 说明 |
| --- | --- | --- |
| `name` | 是 | 命令行使用的稳定门禁名，使用 kebab-case。 |
| `level` | 是 | P0/P1/P2。 |
| `owner` | 是 | 维护责任域，不要求是个人。 |
| `description` | 是 | 一句话说明门禁目标。 |
| `scope` | 是 | 该门禁覆盖的功能域。 |
| `requiredBefore` | 是 | 哪些行为前必须通过该门禁。 |
| `failurePolicy` | 是 | 失败后的阻断和处理策略。 |
| `steps` | 是 | 实际执行步骤。 |

### 4.3 Step 类型

历史配置包含以下 step；2026-10-06 起的可执行合同与兼容状态以第 15 节为准：

| kind | 用途 | 必要字段 |
| --- | --- | --- |
| `dotnet-build` | 构建指定项目 | `name`、`project` |
| `dotnet-test` | 测试指定项目 | `name`、`project`，可选 `filter` |
| `powershell-script` | 执行仓库内 PowerShell 契约或验收脚本 | `name`、`script`，可选 `arguments`、`timeoutSeconds` |
| `unity-editmode-test` | 运行 Unity batchmode EditMode 测试并固定落盘 `command/log/xml` 产物 | `name`、`projectPath`，可选 `testPlatform`、`testFilter`、`editorPath`、`extraArgs` |
| `gate` | 嵌套执行另一个门禁 | `name`、`gate` |

## 5. xUnit 标签规范

门禁过滤依赖 xUnit `Trait`，规则如下：

| Trait | 用途 | 示例 |
| --- | --- | --- |
| `Gate` | 硬门禁标签，用于 `dotnet test --filter` | `MobaConsoleSmoke` |
| `Category` | 功能域或测试类型 | `Smoke`、`MobaConsole`、`Presentation`、`RuntimeContract` |
| `Feature` | 可选，具体功能名 | `ViewTimeline`、`SkillCastTrace` |

新增关键功能时必须遵守：

1. 至少补一个主链路测试。
2. 如果该功能会阻断后续开发，必须标记 `Gate`。
3. `Gate` 名使用 PascalCase，门禁名使用 kebab-case。
4. 测试加入门禁后，同步更新 [`tools/test-gates.json`](../tools/test-gates.json) 和本文档。

当前 MOBA console smoke 使用：

```csharp
[Trait("Gate", "MobaConsoleSmoke")]
[Trait("Category", "Smoke")]
[Trait("Category", "MobaConsole")]
```

## 6. 测试优先开发流程

### 6.1 改动前

1. 判断影响域：console、runtime、view runtime、network、DI、Shooter、Unity package。
2. 选择最小必要门禁。
3. 如果没有可覆盖该影响域的门禁，先补测试或把新测试纳入已有门禁。

### 6.2 开发中

1. 优先抽出可测试 seam，避免逻辑只能在 Unity 场景中验证。
2. 主链路优先接入 smoke 或 contract 测试。
3. 每完成一个小功能点，先跑相关 P0/P1 门禁，再继续下一批功能。
4. 新增功能默认遵循“先补测试、再扩实现、再跑门禁”的顺序。
5. 如果一项改动会影响后续多人协作或 CI 定时检查，必须同步补充门禁标签或门禁配置。

### 6.3 提交、合并或继续开发前

1. 普通小改动：至少运行 `precheck`。
2. MOBA 表现层或 console 链路改动：运行 `moba-console-smoke`。
3. 核心包、生命周期、同步或 Attributes/Modifiers 热路径改动：运行 `core-stability`。
4. network/DI/view runtime contract 改动：运行 `runtime-contracts`。
5. 大范围重构或阶段收口：运行 `regression`。
6. CI 定时检查失败后，必须先修复失败门禁，再继续合并或继续开发。

## 7. 失败处理流程

门禁失败时按以下流程处理：

1. 停止继续写新功能。
2. 确认失败类型：编译失败、测试失败、测试过滤无命中、配置错误、环境问题。
3. 如果是编译失败，先修复最小项目构建。
4. 如果是测试失败，优先判断是功能回归还是测试预期需要更新。
5. 如果是契约变化，必须同步更新设计文档、门禁配置和测试预期。
6. 修复后重新运行同一个门禁，直到通过。

不能用以下方式绕过门禁：

- 删除 `Trait` 让测试不被过滤命中。
- 把失败测试移出门禁但不补等价覆盖。
- 只跑单个测试方法后宣称门禁通过。
- 将 P0 失败延后到 P2 批量回归再处理。

## 8. CI 接入与定时检测

当前脚本已经可直接用于 CI，且会统一输出到 [`artifacts/test-gates`](../artifacts/test-gates) 下的门禁运行目录。每次运行都会生成：

- 门禁输出目录。
- 每个 `dotnet test` 的日志文件，或 Unity batchmode 的原始 `.log` 文件。
- 每个测试步的 TRX 结果文件，或 Unity step 的 `.xml` / `.command.txt` 结果文件。
- `gate-summary.json` 汇总文件。

推荐阶段如下：

| CI 阶段 | 命令 | 目的 |
| --- | --- | --- |
| Pull Request quick check | `powershell -ExecutionPolicy Bypass -File tools\run_test_gate.ps1 -Gate precheck -CI -ResultsDirectory artifacts\test-gates\precheck` | 快速阻断明显回归 |
| MOBA compile-time contract check | `powershell -ExecutionPolicy Bypass -File tools\run_test_gate.ps1 -Gate moba-codegen -CI -ResultsDirectory artifacts\test-gates\moba-codegen` | 校验 Roslyn 构建、生成/分析契约和 package 所有权 |
| Core stability check | `powershell -ExecutionPolicy Bypass -File tools\run_test_gate.ps1 -Gate core-stability -CI -ResultsDirectory artifacts\test-gates\core-stability` | 校验八个核心包测试、Attributes 零分配契约和 UPM 直接依赖 |
| Runtime contract check | `powershell -ExecutionPolicy Bypass -File tools\run_test_gate.ps1 -Gate runtime-contracts -CI -ResultsDirectory artifacts\test-gates\runtime-contracts` | 校验网络、DI 和表现运行时契约 |
| Nightly regression | `powershell -ExecutionPolicy Bypass -File tools\run_test_gate.ps1 -Gate regression -CI -ResultsDirectory artifacts\test-gates\regression` | 批量回归，包含嵌套 `core-stability` |

对应的 GitHub Actions 工作流位于 [`/.github/workflows/abilitykit-test-gates.yml`](../.github/workflows/abilitykit-test-gates.yml)。

### 8.1 定时策略

当前定时策略以 `workflow schedule` 为准：

- `0 18 * * *` UTC，每天触发一次。
- 对应北京时间约为每天 02:00。
- pull request 和 push 独立执行 `core-stability`，便于快速定位核心包失败。
- 定时和手工任务由 `regression` 嵌套执行 `core-stability`，避免同一工作流重复运行核心测试。
- 定时任务默认执行 `regression` 门禁，适合做夜间批量回归和失效检测。

### 8.2 CI 产物约定

每次 CI 运行建议上传整个门禁目录作为 artifact，便于排查：

- `gate-summary.json`：门禁状态、耗时、失败步骤。
- `*.log`：每个 step 的原始命令输出。
- `*.trx`：Test Explorer / CI 可直接读取的测试结果。

如果后续要引入覆盖率或更多 Unity batchmode 门禁，只需要继续沿用同一目录结构即可。

## 9. 维护规则

1. 新增门禁必须同时更新 [`tools/test-gates.json`](../tools/test-gates.json) 和本文档。
2. 删除门禁必须说明替代门禁或删除原因。
3. 修改 P0 门禁前必须确认不会降低继续开发前置保障。
4. `regression` 可以变大，但 P0 应保持快速。
5. 文档中的命令必须与脚本实际参数保持一致。

## 10. 当前 MOBA console smoke 覆盖范围

`moba-console-smoke` 当前覆盖：

1. 完整战斗 smoke 场景。
2. Console 表现层时间线对齐决策。
3. 真实 tick 链路中的表现层时间线对齐。
4. 技能释放、runtime 输入端口、技能效果 trace、导出 artifact 主链路。

该门禁是后续 MOBA 表现层可测试性优化的默认前置条件。

## 11. Shooter / MOBA 示例测试资产地图

Shooter 和 MOBA 示例不是普通展示 demo，而是 AbilityKit 框架能力的验收样板。测试资产应按“纯 C# 快速验证优先，Unity batchmode 权威验证补充”的方式分层管理。

| 示例 | 测试资产 | 当前入口 | 已覆盖能力 | 当前缺口 |
| --- | --- | --- | --- | --- |
| MOBA Console | xUnit smoke | `src/AbilityKit.Demo.Moba.Tests/Smoke/ConsoleMobaSmokeFlowTests.cs` | 完整战斗入口、表现时间线对齐、技能 cast trace、artifact 导出 | 覆盖面集中在 `Gate=MobaConsoleSmoke`，其他 smoke/contract 用例尚未进入独立门禁 |
| MOBA DSL / Script | xUnit 单元与 runner 测试 | `src/AbilityKit.Demo.Moba.Tests/Smoke/BattleTestScenarioLibraryTests.cs`、`src/AbilityKit.Demo.Moba.Tests/Smoke/BattleTestScriptRunnerTests.cs` | 平台无关脚本模型、随机种子、stress/full battle 场景、runner 失败回传 | 需要提升为独立 DSL/环境构建门禁，验证脚本资产能驱动 console 和 view runtime |
| MOBA View Runtime | xUnit runtime 测试 | `src/AbilityKit.Demo.Moba.View.Runtime.Tests/AbilityKit.Demo.Moba.View.Runtime.Tests.csproj` | client sync strategy、DemoHarness carrier、remote interpolation playback | 已进入 `regression`，但尚未形成更小的 P1/P0 view runtime 门禁 |
| MOBA Unity EditMode | Unity batchmode EditMode 测试 | `moba-content-contracts`、`moba-xiaoqiao-unity` | 通用内容契约、包资源所有权、聚合漂移、资源加载、有效时序，以及小乔权威技能结果和落盘产物 | 通用内容门禁仍为手工 P1；应在发布流水线稳定后纳入配置/资源变更的 CI 路径 |
| Shooter Runtime | xUnit runtime 测试 | `src/AbilityKit.Demo.Shooter.Runtime.Tests/AbilityKit.Demo.Shooter.Runtime.Tests.csproj` | sync mode、snapshot、protocol、gateway、rollback、determinism、presentation contracts | 测试资产丰富，但缺少统一 `Gate`/`Category` 标签，不能按 P0/P1 精确过滤 |
| Shooter DSL / Scenario | xUnit JSON scenario 与 benchmark 测试 | `src/AbilityKit.Demo.Shooter.Runtime.Tests/Gameplay/ShooterSveltoGameplayScenarioRunnerTests.cs` | JSON 场景、loadout、battle flow、确定性结果、benchmark profile | 需要明确哪些场景属于 smoke，哪些属于 nightly regression |
| Shooter Sync Matrix | xUnit 同步模式 smoke | `src/AbilityKit.Demo.Shooter.Runtime.Tests/Client/ShooterSyncModeSmokeTests.cs` | PredictRollback、AuthoritativeInterpolation、BatchStateSync、MassBattleLodSync、HybridHeroPrediction | 适合拆出 `shooter-runtime-smoke` 和 `shooter-sync-contracts` 门禁 |

相关设计依据：

1. Shooter 纯 C# 验收外壳与 Unity 薄层绑定见 [`Docs/Shooter验收外壳绑定说明.md`](Shooter验收外壳绑定说明.md)。
2. Shooter view/runtime 分层和测试先行原则见 [`Docs/Shooter示例View与Runtime整体设计.md`](Shooter示例View与Runtime整体设计.md)。
3. Shooter 同步能力矩阵见 [`Docs/Shooter同步能力演示流程与设计.md`](Shooter同步能力演示流程与设计.md) 和 [`Docs/网络同步能力档案与DemoHarness矩阵设计.md`](网络同步能力档案与DemoHarness矩阵设计.md)。
4. MOBA runtime 启动链、技能主链路和上线化缺口见 [`Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Docs/StartupChainGuide.md`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Docs/StartupChainGuide.md)、[`Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Docs/GoldenSkillFlowGuide.md`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Docs/GoldenSkillFlowGuide.md)、[`Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Docs/MobaRuntimeProductionReadinessReview.md`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Docs/MobaRuntimeProductionReadinessReview.md)。

## 12. 推荐门禁演进矩阵

当前门禁已经能跑通 CI 和批量回归，但 Shooter/MOBA 示例还需要从“测试资产存在”推进到“测试资产可被稳定门禁选择”。建议按以下顺序演进：

| 推荐门禁 | 层级 | 建议来源 | 覆盖目标 | 纳入时机 |
| --- | --- | --- | --- | --- |
| `moba-runtime-smoke` | P0 | `src/AbilityKit.Demo.Moba.Tests` | runtime first frame、startup validation、skill pipeline prewarm、snapshot buffer 消费 | 当 MOBA runtime 启动链继续扩展时 |
| `moba-dsl-env` | P1 | MOBA script library 与 runner tests | full/random/stress script、seed 稳定性、console/view runtime 共用脚本模型 | 当 DSL 成为测试场景构建入口时 |
| `moba-view-runtime-contracts` | P1 | `src/AbilityKit.Demo.Moba.View.Runtime.Tests` | client sync strategy、carrier、interpolation playback | 当表现层同步策略或 view runtime contract 变化时 |
| `shooter-runtime-smoke` | P0 | Shooter sync mode smoke、scenario runner smoke | 关键同步模式能启动、基础场景能稳定完成、最终 snapshot 健康 | 当 Shooter runtime/view runtime 继续扩展示例能力时 |
| `shooter-sync-contracts` | P1 | Shooter protocol/snapshot/gateway/rollback tests | 协议、快照、网关、回滚、重连、同步策略契约 | 当网络同步抽象或 DemoHarness carrier 改动时 |
| `unity-editmode-authoritative` | P1/P2 | Unity EditMode tests | Unity 侧权威用例、编辑器集成、落盘验收产物 | 夜间或合并前专项运行，不建议默认阻断所有小 PR |

落地顺序建议：

1. 先给 Shooter 核心 smoke 和 contract 用例补 `Trait`，不要急于扩大 CI 默认耗时。
2. 把 MOBA 已有 DSL/runner 用例归入 `moba-dsl-env`，确认脚本资产是 console 和 view runtime 的共同输入。
3. 将 P0 保持在几分钟内，P1 面向跨模块 contract，P2/nightly 承担完整矩阵。
4. 每新增一个门禁，都先在本地跑通，再加入 [`tools/test-gates.json`](../tools/test-gates.json) 和 CI workflow。

## 13. DSL / 环境构建测试规范

DSL 或脚本场景测试的目标不是复刻所有人工操作，而是把“能稳定构建战斗环境并走完主链路”变成可重复资产。新增 DSL/环境构建测试时遵守以下规则：

1. 场景输入必须平台无关，优先放在纯 C# 层，避免依赖 Unity 场景对象才能解释测试意图。
2. 每个脚本场景必须有稳定 ID、随机种子、固定 tick 数或明确结束条件。
3. 测试断言优先检查关键状态、trace、snapshot、错误报告和 artifact，而不是只检查没有抛异常。
4. full battle、random、stress 三类脚本应分层进入不同门禁：full battle 可进 P0/P1，random/stress 默认进 P2/nightly。
5. 同一个 DSL 场景如果需要覆盖 console 和 view runtime，应共享脚本模型，只替换 driver/harness。
6. 任何 flaky 场景必须先退出 P0，定位为 deterministic replay 问题、时间依赖问题、资源加载问题或环境问题后再恢复。

推荐将 DSL 测试拆为三类：

| 类型 | 作用 | 适合门禁 |
| --- | --- | --- |
| Script model tests | 验证脚本结构、默认值、seed、序列化或 catalog 稳定 | P0/P1 |
| Runner contract tests | 验证 step 调度、duration、失败回传、driver 协议 | P1 |
| Scenario execution tests | 真实驱动 console/view/runtime，验证状态、trace、snapshot | P1/P2 |

## 14. 适用边界与成本控制

这套流程适合 AbilityKit 的原因是：AbilityKit 是框架型能力库，同时承担 MOBA/Shooter 示例、网络同步、运行时契约和 Unity 包交付。它的风险不是单个 demo 能不能跑，而是后续重构、多人协作、跨包发布和 CI 演进时，基础链路是否还能被快速证明。

适合使用完整流程的项目类型：

1. 长线运营、多人在线、竞技、同步或强数值规则项目。
2. 有公共 runtime/framework/package，需要被多个示例或游戏复用的项目。
3. 已进入多人协作或频繁重构阶段，人工验收成本持续上升的项目。
4. 需要 CI/nightly regression 作为合并或发布依据的项目。

不需要一开始完整使用的项目类型：

1. 一次性原型、短周期玩法验证或单人离线小项目。
2. 规则仍在剧烈探索、接口还没有稳定下来的早期 demo。
3. 没有 CI、没有多人协作、没有长期维护目标的展示项目。

成本控制原则：

1. P0 只保护最短主链路，不能把所有测试都塞进 P0。
2. 单元测试覆盖规则和边界，smoke 测试覆盖主链路，DSL 测试覆盖组合场景，Unity batchmode 覆盖引擎集成结论。
3. 每个线上 bug 或关键回归都应沉淀为一个最小测试，但不要求每个临时实现细节都有测试。
4. 测试用例数量增长后，必须通过标签、门禁和 nightly 分层管理，否则 CI 时间和维护成本会反噬开发效率。
5. 文档、门禁配置和测试标签必须同步演进，避免测试存在但无人知道何时运行。

## 15. Cooking 结果真实性合同（2026-10-06）

本节为 Owner Cooking-only 范围及 [dot 完整规划决定](../.trellis/tasks/10-06-cooking-issue6-truthful-gates/research/cooking-dot-flow/evidence/dot-plan-reply-raw.txt) 的共享 runner 合同。上面的示例命令、推荐 CI、历史门禁说明继续保留来源，不能视为当前适配、实际 CI 或执行通过的证明。此改动只服务既有 `cooking-et-level-runtime` 与 `cooking-kitchen-loop`；其他示例不迁移、不修复。旧 Unity 脚本独立运行的 skip 行为未修改。

### 状态、身份与覆盖

| canonical status | CLI exit | 事实 |
| --- | --- | --- |
| Passed | 0 | 所声明覆盖由已验证的当前执行证据完成 |
| Failed | 1 | 已执行失败、坏/矛盾证据、配置错误、超时或启动后取消 |
| Blocked | 2 | 缺必需工具、缺明确覆盖/未适配 producer 或缺复用证明，不能宣称成功 |
| Skipped | 3 | 仅预先声明的 optional `MissingTool` 非执行政策；保留原原因 |
| NotRun | 4 | 未选择/尚未执行；或聚焦结果导致父门禁覆盖不完整 |

大小写严格；`processExitCode` 单独保存真实原生值，原生 exit2 不自动变成 Blocked。N/A 只用于验收范围说明。纯 build 的 `tests=null`；测试零命中、零实际条目、失败、必需跳过/未执行和计数矛盾不能 Passed。

每次调用分配 UUID `runId`，完整计划树为每个调用分配独立 UUID `resultId`、`parentResultId`、`invocationPath`。全新 run 目录内以 result UUID 的 12 个字符分配短目录键，创建时拒绝碰撞；重复 nested 调用不能共享目录。所有兄弟及后续节点都保留，包括未执行的 NotRun。`-StepName` 接受唯一名称或准确调用路径，缺失/歧义为 Failed；成功选中叶子仍不等于父 gate 完整通过。未启动叶子的 command、执行时间和原生退出码均为 null。

配置权威仍是 `tools/test-gates.json`。适配 entry 显式提供 `requiredCoverage`、`optionalCoverage`；每个叶子有 `coverage`、project、TFM、filter。required/optional 不重叠；Cooking 全部九个步骤仍 required，步骤次序、focused filter 和两个完整测试项目保留。完整字段模型见 [result-contract-fields](../.trellis/tasks/10-06-cooking-issue6-truthful-gates/research/result-contract-fields.md)。

`coverage.completed` 仅来自通过唯一 validator 的 Passed 叶子。每个 token 的 `bindings` 是非空、唯一、可解析的叶子 ID；父节点重新推导 completed/missing/bindings/status/fullGateAccepted，拒绝伪造汇总、缺孩子、旧/重复身份与空序列化绑定。内存对象及 JSON 写入再读取使用同一个 validator。所有真实失败和损坏均传播；optional 政策不豁免失败。required 未完成的聚焦父结果为 NotRun/4，`fullGateAccepted=false`。

### 执行与复用

两个最终真实命令保持：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime -Configuration Debug -CI
powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop -Configuration Debug -CI
```

每个 test 适配器显式执行 restore → build → test `--no-build --no-restore`，逐阶段记录 argv、宿主/SDK/工具身份、时间和原生退出码；build 步骤也显式 restore/build。不能把准备阶段隐藏成未执行。`-TimeoutSeconds` 为每个子进程超时；`-CancelSignalPath` 为明确的取消信号文件，启动后的取消保留 Failed 和后续 NotRun。只处置本次 Process 实例，不全局 kill；若退出无法确认，UnknownWriter 阻止继续。

`-NoBuild` 只跳过 test 的准备 build，同时复用恢复证据；配置中的显式 build 步骤仍执行。`-NoRestore` 只跳过 restore，build 继续执行。使用 `-ReuseManifestPath` 指向一个明确 run 的 `reuse-manifest.json`，按 project/TFM/Configuration 索引原始阶段回执；不搜索最新目录。NoRestore 只要求有效 restore，允许原 build 失败；NoBuild 要求 restore 与 build 均有效。缺证明 Blocked，坏/失配证明 Failed。Reused 阶段 command、时间和 processExitCode 为 null，原始 producer 身份/回执和归档副本另外保留，不能获得“本次执行”的信用。

### 证据与验收界限

源证据记录精确 Git SHA、dirty 与完整 dirty 明细、输入指纹和前后检查。输入取自真实 MSBuild 评估的项目引用、Compile Include、SDK/条件导入、配置、包解析及生成/分析工具；引用的 `Unity/Packages` 输入也读取/hash，但不运行 Unity。不能靠扩展名白名单、文件存在或 mtime 宣称源码闭包。

每次 build 另在本 run 拥有的目录写入临时 compiler target，通过命令参数导入；不修改项目文件。该 target 在每个项目的 CoreCompile 前使用 MSBuild 自带 [GetFileHash](https://learn.microsoft.com/en-us/visualstudio/msbuild/getfilehash-task) 记录最终 Compile、ReferencePath、Analyzer、AdditionalFiles、EmbeddedResource 与导入的实际路径/hash，包括 target 加入的生成文件。runner 归档此 trace 与输入副本，执行后逐项复验；SDK 的 MSBuild 与 Roslyn 工具输入也归档。普通评估和实际编译 trace 各自保留，不能用评估的静态 Compile 列表冒充完整编译输入。

每个阶段的输入、restore assets、build/test 程序集和依赖二进制归档进本 run 独立目录，记录路径/长度/SHA-256/所属 run/result，执行前后核对。artifact 只允许节点拥有的相对路径，拒绝逃逸、reparse/junction、旧身份和替换 DLL。stdout/stderr 直接从两个原生流保存为独立文件，允许空日志；不经过 ErrorRecord 排版，也不宣称两条流的合并顺序是真实时序。

TRX 按实际 UnitTestResult、Execution、UnitTest/TestMethod 程序集与 summary/counters 解析并绑定当前调用、源 SHA 和 filter argv。total=resultEntries=passed+failed+skipped+notExecuted；executed=passed+failed。TRX 的其它 outcome counters 按本身语义核对，不再相加；例如 VSTest 的 [TestRunSummary](https://source.dot.net/Microsoft.VisualStudio.TestPlatform.Extensions.Trx.TestLogger/ObjectModel/TestRunSummary.cs.html) 将普通 Passed 测试的 completed 写为 0，它不是 executed 的别名。失败却 native0、零实际执行、错误程序集/执行身份、旧时间/旧 run/错 SHA、覆盖二进制和计数矛盾均拒绝。

隔离自测命令：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/tests/test-gate-result-contract.tests.ps1
```

自测仅使用唯一 TEMP 项目、明确假执行器与 `local/Artifacts/issue6-cooking-slice1-<unique-id>` 全新目录；逐项保存 expected/actual、命令、原生退出码、计数、raw 路径、源码前后身份，并保留失败运行。`-ControlContextPath` 只接受 TEMP 中明确 fixture 的上下文与配置及仓库指定 fixture 的 hash；所有此类结果 `example=true`，生产 validator 拒绝 synthetic/example，不能据此声称真实 .NET/Unity 验收。slice1 的两项真实 Cooking gate、CI、Issue 最终技术接受、merge/close 仍 NotRun，须独立派发与精确提交验证。

原生宽度控制明确以 `RawUI.WindowSize.Width` 为覆盖维度，请求 40/80/120，保留实际 window height，并在独立 `width-evidence.json` 中记录维度、请求、setter 是否成功、实际应用值、观测 buffer/window 尺寸与位置、重定向及限制。窗口宽度直接对应 formatter 的窗口维度，满足已批准的不同控制台宽度要求；此前 40/80/180 的 buffer 模式及其 Blocked 记录保留，不作为窗口模式证据。只有三个不同请求各有成功 setter 与匹配读回、高度保持、native7 和精确的每流 8192 字符 payload 时才接受宽度覆盖；参数值或重复观测不能代替实际覆盖。`contractControlsAccepted` 单独记录字节与合同断言；缺实际宽度覆盖时 `consoleWidthCoverage=Blocked`，整套 `fullControlSuiteAccepted=false`、自测 CLI2，不能借已通过的原始流校验冒称完整控制组通过。此证据不声明交互控制台的渲染验收。

### 只读兼容清单

生产仅适配 dotnet-build / dotnet-test / gate。所选执行树含未适配 kind 或缺明确覆盖合同，启动任何 producer 前完整输出 Blocked/exit2。未知/损坏配置 Failed。只检查所选树的适配要求，其他 legacy 定义存在不阻止两个 Cooking gate；`-List` 保留所有 entry 并给出诊断。defaultGate=`precheck` 保持原值，当前默认执行因此 Blocked；调用方必须检查 canonical CLI 与 JSON 覆盖事实。

| entry | 配置 kind（含直接步骤） | 当前边界 |
| --- | --- | --- |
| precheck | build/test | Blocked: MissingCoverageContract |
| moba-codegen | build/test/script | Blocked: MissingCoverageContract + UnsupportedProducerContract |
| moba-console-smoke | test | Blocked: MissingCoverageContract |
| runtime-contracts | test | Blocked: MissingCoverageContract |
| moba-network-options | test | Blocked: MissingCoverageContract |
| moba-acceptance-dotnet | test | Blocked: MissingCoverageContract |
| cooking-et-level-runtime | build/test | 显式合同；真实执行另验 |
| cooking-kitchen-loop | build/test | 显式合同；真实执行另验 |
| network-sdk | test | Blocked: MissingCoverageContract |
| core-stability | test/script | Blocked: MissingCoverageContract + UnsupportedProducerContract |
| foundation-units | build/test/script | Blocked: MissingCoverageContract + UnsupportedProducerContract |
| regression | test/gate | Blocked: MissingCoverageContract；nested core-stability 也未适配 |
| moba-content-contracts | script/EditMode | Blocked: MissingCoverageContract + UnsupportedProducerContract |
| moba-xiaoqiao-unity | EditMode | Blocked: MissingCoverageContract + UnsupportedProducerContract |
| moba-lianpo-unity | EditMode | Blocked: MissingCoverageContract + UnsupportedProducerContract |
| moba-zhaoyun-unity | EditMode | Blocked: MissingCoverageContract + UnsupportedProducerContract |
| moba-mozi-unity | EditMode | Blocked: MissingCoverageContract + UnsupportedProducerContract |
| moba-daji-unity | EditMode | Blocked: MissingCoverageContract + UnsupportedProducerContract |
| moba-yingzheng-unity | EditMode | Blocked: MissingCoverageContract + UnsupportedProducerContract |
| moba-config-sync | EditMode | Blocked: MissingCoverageContract + UnsupportedProducerContract |
| shooter-fast | test | Blocked: MissingCoverageContract |
| shooter-integration | test | Blocked: MissingCoverageContract |
| shooter-unity-playmode | PlayMode | Blocked: MissingCoverageContract + UnsupportedProducerContract |
| shooter-multiprocess | script | Blocked: MissingCoverageContract + UnsupportedProducerContract |
| shooter-multiprocess-compatibility | script | Blocked: MissingCoverageContract + UnsupportedProducerContract |
| shooter-multiprocess-soak | script | Blocked: MissingCoverageContract + UnsupportedProducerContract |
| shooter-multiprocess-ownership-cleanup | script | Blocked: MissingCoverageContract + UnsupportedProducerContract |
| runtime-performance-measurement | test/script | Blocked: MissingCoverageContract + UnsupportedProducerContract |
| shooter-performance | script | Blocked: MissingCoverageContract + UnsupportedProducerContract |
| moba-complete-battle-journey | test/EditMode | Blocked: MissingCoverageContract + UnsupportedProducerContract |
| moba-smoke | script | Blocked: MissingCoverageContract + UnsupportedProducerContract |
| moba-multiprocess | script | Blocked: MissingCoverageContract + UnsupportedProducerContract |

兼容消费者搜索中，本 checkout 的生产 `.ps1/.yml/.yaml` 没有另一个 runner/summary 消费调用方；`tools/README.md`、本规范历史命令和 task/设计文档仍引用旧入口/exit0 成功习惯。历史 `gate-summary.json.steps` 已由完整 `children` 与声明覆盖取代，外部未发现的消费端仍须自行迁移；不推断外部使用为零。磁盘未发现 `.github/workflows`，文档的旧 workflow 引用不能证明 CI 存在/通过，主控另行核实远端实际 checks。该只读清单不授权修改任何禁止的示例消费者。
