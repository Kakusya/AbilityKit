# Owner 范围纠偏与停止记录（2026-10-05）

Owner 重申：“只做Cook项目，其他项目都是事例，我们不要动”，并要求提高优先级、再次落盘，以及搜索其他 worktree。

两棵现存 Git／Orca 工作树都已核对：主树 `D:/MyWorkTree/AbilityKit`、本树 `issue6-test-gate-results`。两边 [09-19 原产品 PRD](../../09-19-cooking-productization-network-slice-planning/prd.md#只推进-cooking) 都明确只推进 Cooking，Shooter/MOBA 不进入主动开发。此前 #6 全示例适配计划没有正确落实这一边界；原批准不能继续覆盖越界范围。

本树 AGENTS 已同步最高优先级约束。PRD/design/implement 增加停止声明；task 改为 blocked，保留原批准与提交证据。B2b Shooter、MOBA、原五项目运行验收与整个宽范围分支合并安排停止。共享工具后续改动必须先明确 Cooking 的具体需求、消费者、必要性和验收，不得继续以全示例完整性为目标。

处置前 HEAD 为 `4dd4f3fff0d7d08cf59dfdbbadc73fe35836705f`，状态干净；已有示例专用脚本改动和证据继续保留并隔离。master 仍为 `7aa3e8b67c13a469192edaa761cb3dfc1fa9294b`，未合入这些提交。本轮只修改约束／任务文档，不修改示例代码，不 reset 或删除原证据。

Orca 新 runtime 核对本树终端数为 0，旧 Shooter dispatch `ctx_7a6677366596` 为 failed/abandoned，实际相关进程搜索无活动测试／执行命中。旧 mailbox 停止消息因 dispatch_inactive 未送达，不声称执行者已确认；不重启失效 worker。产品运行测试 NotRun；Issue 未验收、未归档、worktree 未删除。
