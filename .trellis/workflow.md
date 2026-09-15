# AbilityKit Trellis 工作流

## 项目约束

- AbilityKit 是 Unity UPM + 纯 C#/.NET 工具库；做菜经营游戏是应用层方向，不能把游戏规则、房间流程或网络权威策略加入通用框架。
- 共享源码以 `Unity/Packages/` 为主，`src/` 通过 `Compile Include` 复用。改动共享逻辑时同时检查相关 `.csproj` 与 `.asmdef`。
- 不编辑 Unity 自动生成 `.csproj`、`Library/` 或 `Temp/`；不因其他 Editor 占用项目而删除锁文件或强制执行批处理。
- `ADR/long-term-goals.md` 记录方向与未决范围，`ADR/decisions/` 记录接受的架构取舍，`Docs/design/` 记录框架和应用设计，`.trellis/spec/` 记录工程规则与待审阅的稳定规划草案，`.trellis/tasks/` 记录当前工作的 PRD、设计、实施清单和检查证据。
- `.trellis/migration/` 是只读来源快照，不是活动任务、实施许可或验收状态来源。迁移的 cooking task 全部仍为 `planning`；`migration_status` 仅说明阶段是否受已知决策门阻塞。

## 验证规则

- 默认门禁：`powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1`；先用 `-List`，再按范围选择 `-Gate core-stability` 或 `-Gate runtime-contracts`。配置权威是 `tools/test-gates.json`。
- 聚焦构建：`dotnet build src/AbilityKit.Demo.Moba.Console/AbilityKit.Demo.Moba.Console.csproj`。
- Unity 编译辅助：`powershell -ExecutionPolicy Bypass -File tools/run-unity-compile-check.ps1`。
- EditMode：`powershell -ExecutionPolicy Bypass -File tools/run-unity-editmode-tests.ps1 -TestAssembly AbilityKit.Ability.Editor.Tests`。
- 协议 catalog：`powershell -ExecutionPolicy Bypass -File tools/compile-protocol-catalogs.ps1 -Check`；wire：`powershell -ExecutionPolicy Bypass -File tools/export-protocol-wire.ps1 -Projects shooter,moba -Check -Strict`。
- Trellis check 只编排、执行和记录检查。交付必须逐项说明命令、pass/fail/blocked/skip 及原因；环境缺失或 Unity Editor 占用绝不表示通过。

## 阶段

```text
Phase 1: Plan    → 建立并审阅任务产物
Phase 2: Execute → 仅在任务进入 in_progress 后实施
Phase 3: Finish  → 检查、知识更新、提交和归档
```

### 无活动任务

[workflow-state:no_task]
No active task. 先判断请求是否需要 Trellis task。涉及多文件、产品行为、架构、测试或跨会话工作时，创建 task 后再进入规划；简单问答或极小改动可直接处理。
开始规划前读取相关 `.trellis/spec/`；涉及做菜规划时读取 `.trellis/spec/cooking/index.md` 和对应 task；涉及架构时读取 ADR 与 Docs/design。
[/workflow-state:no_task]

[workflow-state:task_error]
活动任务记录不可读。先检查 task.json 和相关 artifacts，保留已有字段；不能安全判断状态时停止并向用户说明，不能创建或激活另一任务掩盖错误。
[/workflow-state:task_error]

### Phase 1: Plan

1. 创建 task：`python ./.trellis/scripts/task.py create "<title>" --slug <name> --description "<description>"`。
2. 复杂任务在 `planning` 阶段完成 `prd.md`、`design.md`、`implement.md`。PRD 记录目标、范围、约束、验收与 blocker；设计记录结构、取舍、风险；实施清单记录有验证方式的有序步骤。
3. 将适用的 `.trellis/spec/` 与研究文档加入 `implement.jsonl` 和 `check.jsonl`；不把待修改的产品代码列为 context。
4. 规划经审阅并获实施批准后才运行 `task.py start <task>`。未实施的迁移任务不得因为文档转换而启动、勾选、完成或归档。

[workflow-state:planning]
保持规划阶段。复杂任务必须完成并审阅 PRD、design、implement 和 context manifests。先核实来源冲突、依赖和 owner 决策；不明产品预期不得用当前代码或路线图示例代替决定。
[/workflow-state:planning]

[workflow-state:planning-inline]
保持规划阶段。复杂任务必须完成并审阅 PRD、design、implement。先核实来源冲突、依赖和 owner 决策；不明产品预期不得用当前代码或路线图示例代替决定。
[/workflow-state:planning-inline]

### Phase 2: Execute

1. 读取 task 的 PRD、design、implement、research 与 manifests 指向的规范。
2. 实施只在任务为 `in_progress` 后进行。共享逻辑修改保持纯 C#，同时核对 Unity 与 .NET 宿主边界。
3. 运行与范围匹配的真实检查；失败时修复或记录受阻原因，不能将计划或 Trellis 状态当作测试结果。
4. 对涉及 cooking 迁移任务的工作，保留 Draft/Blocked 决策门，直到 owner 明确解除；真实两 PC LAN 证据不能由同机多实例替代。

[workflow-state:in_progress]
读取活动 task 的 artifacts 和 context manifests 后再实施。实现和检查均以 `.trellis/spec/`、ADR、Docs/design 及真实命令结果为准；完成前进行全范围检查并记录证据。
[/workflow-state:in_progress]

[workflow-state:in_progress-inline]
读取活动 task 的 artifacts 和 context manifests 后再实施。实现和检查均以 `.trellis/spec/`、ADR、Docs/design 及真实命令结果为准；完成前进行全范围检查并记录证据。
[/workflow-state:in_progress-inline]

### Phase 3: Finish

1. 检查是否有新发现需要写入 `.trellis/spec/`、ADR、设计文档或测试；不要复制为第二份事实。
2. 检查 `git status`，只提交本任务产生且已理解的变更，不静默包含用户或其他会话的改动。
3. 实际验证、提交和归档是独立状态。仅在实现完成、检查证据已记录且工作树符合归档条件时运行 `task.py finish` 与 `task.py archive <task>`。

[workflow-state:completed]
任务已归档。确认工作日志和知识更新已经落盘；不要把归档状态描述为未运行检查的通过证明。
[/workflow-state:completed]

## 运行态与宿主

- 项目共享 Trellis 配置在 `.trellis/`；`.trellis/.gitignore` 定义本机身份、session、runtime、cache 和局部日志边界。
- ZCode hooks 位于 `.zcode/config.json`；Codex hooks 还需要用户级 `~/.codex/config.toml` 启用 `features.hooks = true` 并完成宿主批准。hook 文件存在不代表宿主已经加载。
- Trellis 版本、安装与升级边界见 `ADR/reference/README.md`。升级前先检查 Git 状态和自定义 workflow/spec/task，升级后重新审阅 diff 和 hooks。
