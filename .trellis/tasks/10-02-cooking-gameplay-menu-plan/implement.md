> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# 规划维护清单与验证入口

## 最新实施清单（2026-10-02）

- [x] 审阅并合并 S01–S03 核心；master 两门禁实际验证。
- [x] 审阅并合并 87 菜制作/盛装/ET 恢复与 S05 核心，修复嵌套容器归属；master kitchen 340/462/166。
- [ ] 独立审阅 31 饮品实际绑定交付，完成五批内容出口。
- [ ] 完成 S06 同一 ET 入口、受信恢复、厨房/前厅人工排他与营业收尾。
- [ ] 完成 S07 实物包装/原料、准备与营业采购、供应守恒及成功/失败恢复。
- [ ] 完成 S08 准备态原子空间安装、布局/路径/带料迁移、装修与解锁失败原子性。
- [ ] 完成 S14 同一固定 Tick 完整营业、许可与可观察数据、重放和恢复。
- [ ] 单机出口后完成 N01–N03 实际网络；物理两机验收未具备不能冒充通过。
- [ ] Unity 条件性记录继续后置，由独立执行门控制。

以上完成项以 execution.md、progress.md 和具体门禁证据为准。以下初始“只记录/不执行”段落保留为较早规划快照。

本任务只记录，未执行产品实施。已完成记录工作不代表子 Task 已实施。

- [x] 核对 Git 初始状态，读取 ADR、路线、progress、cooking spec 和已有 planning task。
- [x] 保存原始菜单 Markdown/Excel，记录 SHA-256，核对 87 个成品编号和工作表结构。
- [x] 记录 A–K 124 项与额外机制/扩建，对照代码和历史 check。
- [x] 保持架构，明确新功能 owner 与冲突登记。
- [x] 总 plan、菜单整合、20 个 planning 子 Task 与真实上下文落盘。
- [x] 检查本轮引用、依赖、manifest、编号与 planning 状态；实际结果见 check.jsonl 和 research/verification.md。

## 当前允许的检查

`git diff --check`、`python .trellis/scripts/task.py validate <task-dir>`、JSON/Markdown 链接及依赖/源 SHA-256 静态检查。Trellis 当前缺本机 developer 身份，创建记录沿用旧 Task assignee Li He，通过 --assignee 指定，没有创建/提交本机身份；get_context --mode phase 未找到本仓库 Phase Index，采用已读取 workflow.md 的阶段约束。trellis mem CLI 不在 PATH，本轮历史依据来自已落盘讨论纪要和归档 task。

## 子 Task 将来获批准后

- `dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj`：按变更选择聚焦过滤，不在本轮执行。
- `powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop`：厨房改变时核查当时配置范围。
- `powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-et-level-runtime`：ET/生命周期/checkpoint 跨层变更时核查当时配置范围。
- 共享框架改变才选择 core-stability/runtime-contracts；协议/schema 改变先读取 Protocols 和生成器规则。
- Unity 仍禁止；解除禁令并确认 Editor/managed DLL 环境后，再确定 compile/EditMode/scene smoke 命令。

上述是计划命令，不是已通过。本轮不提交、不归档、不 start。

## Current execution sequence after review

Owner authorizes audit then implementation. Follow the reviewed design and parent research/final-review.md; historical planning-only text is superseded. Check actual dependency evidence before starting, preserve stopped worktree edits, fix review findings before accepting prior implementation, update payload/fingerprints/config identity/canonical/checkpoint together, run focused behavioral and applicable integration gates, record actual pass/fail/blocked/skip and commit evidence. Never declare this Task complete from metadata or directory counts.
