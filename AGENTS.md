> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# AbilityKit 工作区指引

- Cooking 本次最新指令是持续审计所有议题、审清后执行到完成。恢复时先读 [完整出口审计表](.trellis/tasks/10-02-cooking-gameplay-menu-plan/research/completion-contract.md)、[execution.md](.trellis/tasks/10-02-cooking-gameplay-menu-plan/execution.md)，再读各 Task 设计/manifests。不要把历史“只列不执行”重新当作当前指令，也不要把已停止 worker 当作存活；保留分支必须审阅和复跑。仍不改变架构，单机优先，Unity 由独立执行门控制。

## 项目与边界

- 这是 Unity UPM + 纯 C#/.NET 工具库；做菜经营游戏是本工作区的产品方向，不是框架已经具备的成品。先读 `ADR/long-term-goals.md` 和 `ADR/README.md`。
- `Unity/Packages/` 是主要共享源码；`src/` 的 SDK 项目通过 `Compile Include` 复用它。修改前检查对应 `.csproj` 与 `.asmdef`，不能只验证单一宿主。
- 核心逻辑保持纯 C#；Unity 负责场景、资源、表现和编辑器。游戏规则、房间流程、网络权威策略由应用层拥有，不塞进通用框架。
- `Server/` 是 Orleans 等宿主示例，不是游戏必须依赖的服务；当前 Coordinator 是精简契约包，不要假设存在旧 SessionCoordinator 或 Local/Remote/Hybrid 实现。
- `Docs/design/` 是既有跨模块设计入口；协议变更先读 `Protocols/README.md`，测试策略先读 `Docs/AbilityKit测试门禁与批量回归规范.md`。
- 做菜经营游戏的应用层技术路线唯一正文是 [`Docs/design/CookingGame/technical-roadmap.md`](Docs/design/CookingGame/technical-roadmap.md)；其迁移规划入口是 [`.trellis/spec/cooking/index.md`](.trellis/spec/cooking/index.md)。两者均不把路线或未执行计划表述为已实现能力。
- [`Docs/design/CookingGame/reference/`](Docs/design/CookingGame/reference/) 目录内的所有 Markdown 文件都是 Cooking 产品语义与 ET 结构的设计参考文件。开始相关规划或实现前应读取其 [`README.md`](Docs/design/CookingGame/reference/README.md) 和关联主题文件；它们用于记录已确认决定与统一术语，但不替代 ADR、`technical-roadmap.md`、`.trellis/spec/cooking/`、活动 task 或 `check.jsonl` 证据。来源冲突时必须显式指出并重新审议，不得把参考文件当作已实现或已验证能力。
- 项目级待办事项统一记录在 [`Docs/Todo.md`](Docs/Todo.md)。该文件用于汇总尚未完成、待审议或待验证的工作；开始实施时仍须按本节的唯一归属规则读取对应 ADR、设计、spec 与 Trellis task/check 证据。
- 恢复或审议做菜项目工作前，先读 [`Docs/design/CookingGame/progress.md`](Docs/design/CookingGame/progress.md) 确认当前执行基线、实际验证指针和完整出口 blocker。该文件只做跨阶段汇总；行为契约与验证证据仍分别以 `.trellis/spec/cooking/` 和对应 task 的 `check.jsonl` 为准。
- 当前 Preparing 运行增量与准确恢复边界见 [preparing-composite-verification.md](.trellis/tasks/10-02-cooking-gameplay-menu-plan/research/preparing-composite-verification.md)；其中集成分支通过不代表已合并 master，也不代表布局安装或准备态恢复已经完成。后续实际结果继续由 progress 与活动 task 路由。
- Cooking 功能、菜品和后续 Task 规划先读 [`.trellis/spec/cooking/gameplay-menu-plan.md`](.trellis/spec/cooking/gameplay-menu-plan.md)，再按其路由读取功能对照、[Task 注册表](.trellis/tasks/10-02-cooking-gameplay-menu-plan/task-register.md)、[菜单整合](Docs/design/CookingGame/reference/menu-integration.md)和[规划架构记录](Docs/design/CookingGame/gameplay-plan-architecture.md)。推进顺序为“单机 → 网络 → 单机 Unity → 网络 Unity”，当前专注单机、不改变架构；未提细节授权助手补足并及时落盘。早期“只列 Task 不执行”已由本文顶部及下节的后续明确授权取代。网络依赖单机出口，Unity 条件性 Task 仍 prohibited；不能从登记或排序本身推导执行批准。
- 87 款菜单的原始 Markdown/Excel 位于 [`Docs/design/CookingGame/reference/menu-v0.1/`](Docs/design/CookingGame/reference/menu-v0.1/README.md)，是候选总目录，不是首关菜单、runtime 配置或已通过证据。恢复规划时同时读[现有 plan 接续路由](.trellis/tasks/10-02-cooking-gameplay-menu-plan/research/current-plans.md)，保持已确认的固定伙伴、自然完成与成功检查点语义；现有网络来源冲突按架构记录显式审议。

## 构建与验证（仓库根目录）

- 2026-10-02 后续 owner 已明确授权按 Cooking 总 plan 实施单机与联网部分，使用 **Orca 受管 worktree + supervised orchestration** 并行作业，验证通过后本地合并 `master`。这更新上文“只列 Task 不执行”的本轮历史边界；Unity 单机/联网继续后置且未获实施授权，S15 扩展参考池不自动纳入基础。执行恢复先读 [execution.md](.trellis/tasks/10-02-cooking-gameplay-menu-plan/execution.md)，核对实际 worker/dispatch 与 Git 状态，不能仅凭状态文件重启活跃 worker。

- .NET 项目使用 `net10.0`。README 提及 SDK 10.0.300，但本次检查根目录没有 `global.json`，不要宣称已固定 SDK。
- Unity 版本为 `2022.3.62f1`；打开 `Unity/`。不要编辑 Unity 自动生成的 `.csproj`、`Library/` 或 `Temp/`。
- 默认门禁：`powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1`；用 `-List` 查看门禁，按范围选择 `-Gate core-stability` 或 `-Gate runtime-contracts`。配置权威是 `tools/test-gates.json`。
- 聚焦构建：`dotnet build src/AbilityKit.Demo.Moba.Console/AbilityKit.Demo.Moba.Console.csproj`。
- Unity 编译辅助：`powershell -ExecutionPolicy Bypass -File tools/run-unity-compile-check.ps1`。需要本机 Unity managed DLL；缺少环境而跳过不算通过。
- EditMode：`powershell -ExecutionPolicy Bypass -File tools/run-unity-editmode-tests.ps1 -TestAssembly AbilityKit.Ability.Editor.Tests`。另一 Editor 占用项目时不要运行批处理，也不要删除锁文件强行执行。
- 协议检查：`powershell -ExecutionPolicy Bypass -File tools/compile-protocol-catalogs.ps1 -Check`；wire 检查用 `tools/export-protocol-wire.ps1 -Projects shooter,moba -Check -Strict`。改 Catalogs/WireSchemas 后通过生成器更新派生文件，不手改生成结果。
- 上述是已发现的命令，不代表每次交付已运行；报告实际执行与跳过原因。

## Trellis 工作流

- 本仓库只使用 Trellis 管理 AI 工程任务、会话上下文、规划、实现、检查、归档与可复用工程知识。项目共享配置位于 `.trellis/`；本机开发者身份、runtime session、缓存和局部日志遵循 `.trellis/.gitignore`，不得提交。
- 复杂工作先创建 Trellis task；在 task 仍为 `planning` 时完成 `prd.md`、`design.md`、`implement.md` 和 context manifests 的审阅，只有明确批准后才可进入 `in_progress`。
- 开始或恢复任务时，读取活动 task 的 `prd.md`、`design.md`、`implement.md`、`research/` 与 `implement.jsonl`/`check.jsonl` 引用的规范。涉及架构时再读取相关 `ADR/decisions/` 与 `Docs/design/`；新游戏范围尚未确认时读取 `ADR/long-term-goals.md`。来源冲突必须显式指出并核实，owner 决定前不扩大范围。
- 唯一归属：长期产品方向与范围空白 → `ADR/long-term-goals.md`；架构取舍 → `ADR/decisions/`；框架设计 → `Docs/design/`；工程规则与稳定的做菜规划草案 → `.trellis/spec/`；当前任务的目标、设计、实施清单、研究与检查记录 → `.trellis/tasks/`；旧流程迁移来源 → `.trellis/migration/`，只读且不作为活动任务来源。
- 做菜迁移任务处于 `planning`，其 `migration_status` 为 `planned` 或 `blocked` 的原因写在 task PRD 与 metadata 中。迁移只保留计划，不代表任务已启动、实现、验证或归档。
- Trellis check 只编排和记录验证；交付必须区分计划命令、实际通过、失败、受阻和跳过。缺少 Unity 环境或另一 Editor 占用项目不得记录为通过。
- 修改前检查 Git 状态，保留用户的解决方案与练习目录改动；不要把 `Unity/Assets/Practice/`、`src/AbilityKit.Demo.MyPractice/` 当作可清理的生成目录。

## Trellis 安装与宿主集成

- 已使用 Trellis `0.6.17` 在项目根目录初始化。开发环境要求 Node.js `>=18`、Python `>=3.9`；安装与升级方式见 `ADR/reference/README.md`。
- Trellis 生成的 ZCode 与 Codex 集成位于 `.zcode/`、`.codex/` 与 `.agents/skills/trellis-*`。当前会话的宿主技能列表不会自动热刷新；新开会话后加载新的工作流入口。
- ZCode hooks 已在 `.zcode/config.json` 启用。若宿主禁用项目 hooks，按 Trellis 输出提示安装对应 bridge 后新开会话；不要将磁盘存在的 hook 当作已获宿主授权或实际执行的证据。
