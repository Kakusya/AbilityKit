# Design

## Structure and authority

新增 `.agents/skills/cooking-dot-workflow/SKILL.md` 作为精简主控入口，`agents/openai.yaml` 设置 `allow_implicit_invocation: false`；`references/dot-dialogue.md` 处理输入/回复/轮询，`references/recovery.md` 处理检查点及恢复，其他详细引用按实际需要添加，不复制 Orca 整份手册。技能使用项目文件的相对链接，不硬编码本机 skill 路径、页面 ID 或 terminal handle。

SOP 加入显式技能激活条款和批准的权威/授权，AGENTS 原“协调者接替 dot”段落补充路由。本次 Cooking 消费者是后续已批准 Cooking 需求的主控会话：技能必要于正确调度/恢复，SOP 必要于排除旧职责冲突，AGENTS 必要于可发现入口。每个文件均按 R1–R6/AC1–AC5 验收；不改变全仓默认流程。

## Data flow

Owner 需求批准 → Trellis 需求/授权记录 → 推送任务分支 commit → dot 技术方案/裁决 → Trellis 子任务及 Issue → Orca Run/Task/Dispatch → worker 提交和真实验证 → dot 对应 SHA 审阅 + 主控证据检查 → 合并/集成检查 → 关闭 Issue/归档。本次安装任务本身由已批准 proposed_plan 授权，不调用新技能联系 dot。

需求和计划以 Trellis 为准，Issue 只保存短合同和链接，PR 保存 diff；Orca 仅权威运行事实。检查点在拥有流程的 task 内存储操作意图/结果、授权、身份和恢复索引，不维护第二套任务完成状态；遵循现有 Trellis 状态，不修改 task.py schema/生命周期。

## Recovery and limitations

检查点早于外部副作用；硬中断按原 request 回执或远端实际结果核对，不能假定无回执即未执行。新会话先只读核验，证明旧主控不可用及实际 Run 绑定能力后才能接任。无法核验绑定或 worker 状态则停止；不猜测不存在的通用 takeover 命令。dot 原回复必须匹配问题和 SHA，网络/UI refs 动态发现；不可访问或旧答案都不能作为审批。

dot 等待预算的起始 UTC 时间和补问状态持久保存，主控在原轮次中断后不能重新获得 30 分钟。预算到期保留活跃 worker；完成 worker 在暂停状态按明确保留约定处理，禁止借清理中断活跃任务。没有后台调度服务，技能本身不保证主控退出后继续轮询。

## Compatibility and rollback

沿用现有 Trellis/Orca CLI 并实时发现能力。origin/Orca metadata 不一致时只读核验，必要明确路径/仓库参数；不能据旧 projectId 写错仓库。当前 phase reader 问题记录限制，不扩范围修复。技能加载能力仍取决于宿主发现，磁盘文件不证明本会话已加载。回退移除本次新技能和路由增量即可，保留 task 证据与历史 SOP，不动用户工作。
