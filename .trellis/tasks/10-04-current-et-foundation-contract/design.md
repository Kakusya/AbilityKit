# 文档设计与最小变更计划

1. 新建 Docs/design/CookingGame/current-et-foundation-contract.md，A–D 合并为带独立锚点的合同，E 规则检查矩阵同置于此。
2. AGENTS 原通知迁入 Docs/design/CookingGame/history/agents-notices-2026-10-04.md；记录原 SHA、字节摘要、顺序和 superseded。根文件保留安全、Orca/Trellis、构建和来源约束，加入稳定原则及合同链接。
3. progress 保留历史正文并新增唯一当前区，区分历史验收和当前调度，不补造旧验证。
4. ADR-0003 保持 Proposed。参考树增加适用性说明，解释命令原子性不同于整帧回滚。

## 冲突清单

- 历史不改架构限制 vs 最新 ET 目标：最新 Issue 仅批准文档，迁移目标不构成运行时批准。
- 原子提交口号 vs fixed-step 失败测试：保留分阶段提交，改变需另案。
- 多份 Current/LIVE 与字面问号：原文归档，不猜修复，不恢复旧授权。
- internal-only 防线、Unity skip 被父 gate 记绿等：仅记录待建检查，不修脚本或虚报通过。

## 风险与回退

可回退本任务文档 diff；禁止双写、提升 Proposed、覆盖故障原件。源码可达与历史实测分开标注。

## 首轮复审的最小修订

仅修 AGENTS、合同 A/E、task 检查方法和本轮证据。R1 源行为以 StopNewSupplyRequests、Request、AdvanceFixedTick、PreviewReceive/CommitReceive、PreviewInfiniteTake 为准；关闭新预约不等于所有供应操作关闭。R2 将结果判断绑定声明覆盖。R3 明确已提交版本与本地差异的检查命令和实际 HEAD；不改运行器，不实施 #6。复审材料见 research/dot-review-round1.md。
