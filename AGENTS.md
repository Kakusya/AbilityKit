# AbilityKit 工作区指引

## 项目与边界
- 这是 Unity UPM + 纯 C#/.NET 工具库；做菜经营游戏是本工作区的产品方向，不是框架已经具备的成品。先读 `ADR/long-term-goals.md` 和 `ADR/README.md`。
- `Unity/Packages/` 是主要共享源码；`src/` 的 SDK 项目通过 `Compile Include` 复用它。修改前检查对应 `.csproj` 与 `.asmdef`，不能只验证单一宿主。
- 核心逻辑保持纯 C#；Unity 负责场景、资源、表现和编辑器。游戏规则、房间流程、网络权威策略由应用层拥有，不塞进通用框架。
- `Server/` 是 Orleans 等宿主示例，不是游戏必须依赖的服务；当前 Coordinator 是精简契约包，不要假设存在旧 SessionCoordinator 或 Local/Remote/Hybrid 实现。
- `Docs/design/` 是既有跨模块设计入口；协议变更先读 `Protocols/README.md`，测试策略先读 `Docs/AbilityKit测试门禁与批量回归规范.md`。
- 做菜经营游戏的应用层技术路线唯一正文是 [`Docs/design/CookingGame/technical-roadmap.md`](Docs/design/CookingGame/technical-roadmap.md)；框架机制仍以 `Docs/design/` 既有 canonical 文档为准，不把路线或长期目标当作已实现能力。

## 构建与验证（仓库根目录）
- .NET 项目使用 `net10.0`。README 提及 SDK 10.0.300，但本次检查根目录没有 `global.json`，不要宣称已固定 SDK。
- Unity 版本为 `2022.3.62f1`；打开 `Unity/`。不要编辑 Unity 自动生成的 `.csproj`、`Library/` 或 `Temp/`。
- 默认门禁：`powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1`；用 `-List` 查看门禁，按范围选择 `-Gate core-stability` 或 `-Gate runtime-contracts`。配置权威是 `tools/test-gates.json`。
- 聚焦构建：`dotnet build src/AbilityKit.Demo.Moba.Console/AbilityKit.Demo.Moba.Console.csproj`。
- Unity 编译辅助：`powershell -ExecutionPolicy Bypass -File tools/run-unity-compile-check.ps1`。需要本机 Unity managed DLL；缺少环境而跳过不算通过。
- EditMode：`powershell -ExecutionPolicy Bypass -File tools/run-unity-editmode-tests.ps1 -TestAssembly AbilityKit.Ability.Editor.Tests`。另一 Editor 占用项目时不要运行批处理，也不要删除锁文件强行执行。
- 协议检查：`powershell -ExecutionPolicy Bypass -File tools/compile-protocol-catalogs.ps1 -Check`；wire 检查用 `tools/export-protocol-wire.ps1 -Projects shooter,moba -Check -Strict`。改 Catalogs/WireSchemas 后通过生成器更新派生文件，不手改生成结果。
- 以上是已发现的命令，不代表每次交付已运行；报告实际执行与跳过原因。

## 统一知识入口（OpenSpec 与 CodeStable 均须遵守）
规划或修改代码前：
1. 读取 `.codestable/attention.md`（若存在）；按任务关键词检索 `.codestable/lessons/`，读取相关命中，不全量加载。
2. 读取相关 `openspec/specs/` 行为契约；若有当前 change，读取其 proposal、差量 specs、design（若存在）与 tasks。没有活动 change 时不要自行猜测一个。
3. 涉及架构时读取相关 `ADR/decisions/` 与 `Docs/design/`。新游戏范围尚未落成行为规格时读取 `ADR/long-term-goals.md`，不要把目标当成已实现能力。
4. 来源冲突时显式指出并核实，不能静默选取；owner 决定前不扩大范围。

唯一归属：行为与验收契约 → `openspec/specs/`；当前变更意图、差量与任务 → `openspec/changes/`；长期取舍 → `ADR/decisions/`；未被测试/规范承接的经验 → `.codestable/lessons/`；高优先级提醒与指针 → `.codestable/attention.md`。只链接，不复制事实；事实迁入更权威归宿时旧处改为指针或退役。

## 工作分工
- 新功能、功能改造和产品行为变更只使用 OpenSpec；不使用 CodeStable 开发功能或创建产品 Epic。
- CodeStable 用于 bug 诊断修复、行为等价重构、文档维护、审查与工程经验管理。文档维护在原归属内进行，不建立第二份产品契约。
- 做菜项目规划使用路线图作为优先级与技术方向指针；精确功能、协议和验收契约必须另行进入 `openspec/changes/`，毕业后进入 `openspec/specs/`。路线图不是 formal tasks，也不是完成证据。
- 修复违背既有契约的 bug 可直接走 `cs-issue`；若需改变预期行为则转 OpenSpec。规格缺失且预期不明时先澄清，不把当前代码自动当作正确要求。
- 以上项目职责覆盖技能上游的通用路由默认值。提案完成、任务勾选、归档与验证通过互不等价；分别报告证据，不由文档状态推断实现完成。

## 本地工作流
- 融合细则见 `ADR/reference/workflow-integration.md`。`.codestable/work/` 仅保存必要的跨会话游标与证据指针，不复制 OpenSpec tasks；现有 `Docs/design/` 保持框架设计权威。
- CodeStable、OpenSpec 和 grilling 技能统一位于 `.agents/skills/`；ZCode 专用命令位于 `.zcode/commands/`。`ADR/reference/upstream/` 只是参考和备份，不是技能发现目录。
- OpenSpec 本地 CLI：`powershell -ExecutionPolicy Bypass -File tools/openspec-cli/openspec.ps1 <参数>`。技能和命令模板中的裸 `openspec ...` 在本项目均替换为此入口，参数保持不变。依赖恢复用 `npm ci --prefix tools/openspec-cli`，版本由该目录锁文件固定；不要求全局安装。
- `grilling` 位于 `.agents/skills/grilling/SKILL.md`：只把已回答的产品选择当成决策，不能替用户回答。安装/升级后当前会话目录可能仍旧，不能将磁盘验证当成宿主刷新成功。
- 修改前检查 Git 状态，保留用户的解决方案与练习目录改动；不要把 `Unity/Assets/Practice/`、`src/AbilityKit.Demo.MyPractice/` 当作可清理的生成目录。

- 做菜项目的阶段顺序与测试出口见 `Docs/design/CookingGame/delivery-plan.md`；实施时读取对应 OpenSpec change 的 proposal/specs/design/tasks。每项实现必须关联测试计划与实际验证证据，不能把计划中的测试当作已执行。
