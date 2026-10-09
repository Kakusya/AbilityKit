# AGENTS 模块路由优化

## 目标与授权

Owner 要求优化 AGENTS.md 对各模块的路由，允许必要时咨询 dot，明确禁止任何测试。
本任务只整理导航与操作文档，不恢复暂停的 Cooking 工程，不改变产品行为、架构或授权。
属于轻量文档任务，使用 PRD-only；不派发子代理。

## 允许修改的文件

- `AGENTS.md`：保留 Owner 约束与用户现有核心规则，补任务入口、读取顺序和缺失入口处理；移出防火墙操作细节；去掉默认全仓／示例执行路由。
- `.trellis/spec/abilitykit/module-routing.md`：按问题与源码路径路由到既有文档，区分 Cooking 应用、共享模块和只读示例。
- `.trellis/spec/abilitykit/cooking-network-tooling.md`：接收现有防火墙操作说明，标明历史测试命令不提供执行授权。
- `.trellis/spec/abilitykit/index.md`：增加两个规范入口。
- 本任务目录：需求、简短审阅记录与完成状态；不存测试输出、证据包或全量状态。

## 设计与实施顺序

1. 核对根指引、Cooking progress、两个 spec 索引、设计总索引与真实源码位置。
2. 明确路由顺序：Owner 约束 → 当前任务范围 → 对应模块规范与设计 → 实际源码和宿主依赖。
3. 根文件只承载全局约束与一级入口；模块表放入工程规范；操作细节单独按需读取。
4. 对历史入口加授权边界，避免 MOBA、旧 rich、Unity 和宽门禁被当默认任务。
5. 只人工审阅文本差异与链接路径，记录测试 NotRun；不运行测试、构建、门禁或 SHA 校验。

## 研究与审阅结论

- 当前只有根目录 AGENTS.md，未发现模块级 AGENTS.md。
- Cooking 应用源码自有于 `src/AbilityKit.Game.Cooking*`，共享框架主要在 `Unity/Packages/com.abilitykit.*`；需要分别路由。
- 已删除的 `.claude/skills/` 不作为入口；保留删除，不重新安装或恢复。
- 根文件、validation 与 workflow 的历史命令包含示例／旧入口；本任务收紧根路由，不扩展到 Trellis 工作流重写。
- Git 与 Orca 枚举均只发现当前 AbilityKit 工作树；未发现 `issue6-test-gate-results`。其他仓库不纳入搜索。
- `get_context.py --mode phase` 退出 1：当前 workflow 没有 Phase Index；已直接读现有 workflow，本任务不修脚本。
- 模块导航不涉及新的架构取舍，本次不咨询 dot，不启动 cooking-dot-workflow。

## 文档验收

- [x] 根入口能分别定位 Cooking、共享模块、协议、工具与 Trellis。
- [x] 模块路由列出触发条件、源码位置和按需读取的文档。
- [x] Owner 禁令、Cooking 暂停与 Unity 边界保持，用户已有改动保留。
- [x] 防火墙说明原文迁移，根文件不再列默认示例／全仓测试命令。
- [x] 只审阅文本与路径；测试、构建、门禁均 NotRun，无 SHA 校验，无新增 JSON 证据。

## 完成记录

2026-10-09：四个允许范围内的指引／规范文件已修改，人工审阅根指引与索引差异、模块表及迁移后的工具说明；通过目录列举与文件读取确认所引用入口。未运行任何测试、构建、门禁、SHA 校验或产品进程。测试状态 NotRun（Owner 明确禁止）。没有咨询 dot，没有启动其他 agent／工作树，没有修改产品代码、旧任务、既有 journal 或用户删除的技能。

本任务只完成文档工作；变更留在当前工作区供 Owner 审阅，未提交、推送或归档。旧 workflow／validation 的历史命令仍保留，根路由明确其不提供默认执行授权；本任务不声称已安装机器权限或运行时封锁。

随后 Owner 明确要求 commit 然后 push，授权提交本次四个指引／规范文件及本任务记录，推送当前 master。其他已有改动保留；继续禁止测试、构建、门禁和 SHA 校验。提交与推送的实际结果以主会话 Git 命令回执为准，不提前声明成功。
