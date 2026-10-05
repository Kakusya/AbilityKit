# Cooking dot supervised workflow skill

## Goal

交付仅显式调用的 `$cooking-dot-workflow` 项目技能：任何加载技能的对话可作为唯一调度者，利用 Trellis 对齐需求、dot 做技术思考和审阅、Orca worktree worker 实施，并从中断处恢复。Owner 已在本会话最终 proposed_plan 之后明确回复 “Implement the plan.”，批准本次技能制作。

## Background and confirmed facts

当前源码 `0ca6d2d2df4af789948857f1f960be285313936b`，分支 `docs/supervised-issue-sop`，初始 clean。现有 SOP 为协调者接替 dot；本技能显式激活时使用新职责，历史记录保留。Git 和 Orca 均列出主工作树及已停止的 `issue6-test-gate-results`；其 `15e79fefd5ba1d9294491122c91df286ef94e2f8` 含示例改动，不恢复或集成。origin 为 `Kakusya/AbilityKit`，Orca projectId 仍为 `github:hobobo/abilitykit`，正式远端操作前必须核验目标。Orca 1.4.220 可读取 dot 页面、管理 supervised Run/Dispatch；当前 dot 页面 `https://chatgpt.com/dots/01a10258-c084-74bb-8bcc-ec01958e5023` 仅为发现的入口，不自动绑定每个新流程。Python task 脚本可用，`trellis` 不在 PATH，Phase Index 读取失败；不修复全局安装或修改通用工作流作为隐藏前置。

## Requirements

- R1：新增显式调用技能，加载者是唯一调度者；不自动替换其他会话默认流程。需求及边界由 Owner 批准后，委托技术规划与拆分；dot 是最终技术裁决者，主控呈现完整冲突证据并落实裁决。dot 不执行代码、启动云工作或写 GitHub。
- R2：主控提供 dot 可读取的明确 commit、需求和证据，按 dot 方案维护 Trellis artifacts/manifest 和 GitHub Issue/PR；worker 只使用 Orca 受管 worktree，默认一名，无文件/契约/验证冲突时最多两名。模型使用 Orca 配置，记录实际值，不套用旧 Issue 的指定模型。
- R3：需求批准后授权本次需求内的任务分支推送、Issue/PR 操作及满足验收后的自动合并、关闭；发布及资源删除另批。记录授权出处和目标分支，保留 Cooking-only、Unity 后置、ET 固定及真实证据规则。新流程不得追溯解锁旧 #6。
- R4：每次等待 dot 总预算 30 分钟；轮询间隔 30/60/120 秒，之后 5 分钟。确认未生成且未回复时最多补问一次，不重置预算。主控暂停新派发和集成；已派发 worker 完成当前有界任务后 idle，需新决定时等待。dot 仍不可用则保存交接并停止，不能接替 dot 思考或绕过审阅。
- R5：Trellis task 内保存持久检查点，外部操作前记意图、之后记回执。新加载自动识别唯一未完成的本技能流程；多个候选询问 Owner；状态模糊或旧主控仍活动时不接管、不重复执行。枚举相关工作树并核验真实 GitHub/Orca/dot 状态，live 等待、完成收集、确认失败/退出才重试。
- R6：dot 技术接受与主控范围/原始验证核验都是交付条件。合并后必要集成检查成功才能关闭/归档；真实失败和环境缺失不得改为 Passed。完成本次需求后结束，其他发现只作候选待办。

## Acceptance criteria

- AC1 (R1/R2)：技能及显式调用元数据有效；SOP/AGENTS 路由一致，普通子代理不能替代 Orca 实施 worker。
- AC2 (R3/R6)：清楚定义授权、commit 绑定、真实证据、自动交付终点；禁止发布/资源删除和旧 #6 恢复。
- AC3 (R4)：按情景核验 dot 正在生成、旧回复、补问/超时；预算不延长，worker 可完成但无新任务或集成。
- AC4 (R5)：按情景核验主控崩溃、live/done/unknown worker、多个任务、回执丢失、旧主控活动；不出现双重执行者或重复远端写入。
- AC5 (R1–R6)：技能 validator、文档链接/格式检查 Passed；独立 Orca reviewer 按原始情景评估行为，发现修正有复审。live dot/GitHub 自动交付试跑本次 NotRun，不宣称完整流程已验证。

## Out of scope

不修改产品源码、协议、生成器、示例、门禁、全局 Trellis 安装、通用 `.trellis/workflow.md` 或默认 agent 配置；不发送 dot 消息、写 GitHub、发布、清理既有 worktree 或恢复 #6。本次只交付技能与必要路由及本任务证据，Owner 明确要求执行者为 Orca worker。

## Installation acceptance

2026-10-05：AC1–AC5 在文档/技能制作范围满足，原版 validator、格式/链接检查、上下文清单和 15 项独立静态情景审阅 Passed，无阻塞发现。源码及内容 hash、实际命令/结果、最初失败与依赖临时加载方式见 [执行记录](research/execution.md)、[独立审阅](research/independent-review.md) 和 [实施报告](research/implementation-report.md)。live dot/GitHub 自动交付、宿主实际加载和跨会话 Run 恢复 NotRun，不以静态审阅替代。归档只代表本次技能制作完成。
