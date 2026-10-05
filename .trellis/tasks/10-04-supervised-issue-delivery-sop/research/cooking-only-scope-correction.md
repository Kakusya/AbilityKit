# Cooking 唯一产品范围纠偏（2026-10-05）

Owner 原话：“我记得我明确说过只做Cook项目，其他项目都是事例，我们不要动，所有的改动都没有意义……请你把这项要求提高优先级并且再次落盘。”

协调者承认：将 Issue #6 展开为 Shooter、MOBA 等示例适配，偏离 Owner 产品边界。该边界是优先级排序之前的授权检查，不能因 Issue 的旧计划、P0 标签或自检通过而绕过。规范权威在 [AGENTS](../../../../AGENTS.md)，派发及审计规则见 [SOP](../../../spec/abilitykit/supervised-issue-delivery.md)。

## 跨工作树来源核对

Owner 再次要求不得只搜索当前 worktree。Git 和 Orca 当前均完整列出两棵工作树：`D:/MyWorkTree/AbilityKit` 与 `C:/Users/Administrator/orca/workspaces/AbilityKit/issue6-test-gate-results`，无省略 host 或截断项。本次两边均搜索 `.trellis`、Docs、AGENTS 的范围表述，并检查 #6 当前 PRD/design/implement 与真实分支差异。

两棵工作树的 [原产品规划 PRD](../../09-19-cooking-productization-network-slice-planning/prd.md#只推进-cooking) 第 27–32 行已有“只推进 Cooking”，明确 Shooter、MOBA 不进入主动功能开发，其他模块仅保留 Cooking 所需共享依赖和回归门禁。该决定早已存在；本次重申加强为禁止示例代码及专用脚本／门禁改动，不是新引入产品方向。旧 #6 实施清单却要求全部十八个 producer 适配，包含 Shooter／MOBA，协调者此前未将产品边界应用到该计划。

## 本次处置

- 停止旧 B2b Shooter、后续 MOBA、原五项目运行验收及整个宽范围分支的合并安排。不重启 worker，不继续示例适配；共享工具的任何后续工作须重新落实 Cooking 消费者、必要性和验收。
- Orca runtime 已变化。向旧 dispatch `ctx_7a6677366596` 发送停止通知得到 `dispatch_inactive`，未声称已送达；读 worker 返回 `worker_identity_changed`。随后核对当前工作树终端列表为空，worker projection 为 failed/abandoned、terminal_missing；实际匹配进程查询无活动执行／测试命中。旧统一测试 session `10216` 已不可用，未据此声称测试完成或通过。
- `issue6-test-gate-results` 分支 HEAD 为 `4dd4f3fff0d7d08cf59dfdbbadc73fe35836705f`，核对时 tracked/untracked 状态干净。保留所有已有提交与原始证据；不 reset、删除或把越界提交合入 master。master 仍为 `7aa3e8b67c13a469192edaa761cb3dfc1fa9294b`。
- 已有示例专用改动包括 `tools/run_shooter_aoi_lod_gate.ps1`、MOBA 内容图／IR／报告工具、MOBA business-id 与 codegen／hero-acceptance 检查。它们位于隔离分支；“示例主逻辑未改”不能掩盖这些脚本已经越界。
- 主工作区落盘 AGENTS、workflow、Cooking index/progress、SOP；原执行工作区同步 AGENTS 与 task 停止记录，防止恢复旧计划。仅改约束／任务文档，不触及示例代码。

## 检查与未完成项

本轮仅检查文档差异、链接、task JSON、Git 范围与实际运行状态；[文档验证记录](cooking-only-scope-verification.json) 核对两树约束一致、计划顶部停止声明与 blocked 状态。产品运行测试 NotRun。Issue #6 未完成、未合并、worktree 未删除；SOP 全流程试行未完成。后续恢复必须先收窄成真正服务 Cooking 的任务，已产生的越界改动继续隔离。
