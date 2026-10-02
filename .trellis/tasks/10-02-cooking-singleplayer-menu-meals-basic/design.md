> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S09 初始设计

状态：draft；未实施。

先 F01/F11/F21 验证再扩完整批，切配/煮/煎/组合/分装全部从供应可达；不等同首关菜单。

沿用 [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md) 的 owner 与数据边界，不创建平行模拟，不修改通用框架的产品职责。具体 API/数据形状、错误矩阵、版本迁移、恢复/跨关状态与测试断言在执行前补齐；本初稿不声称已达到实施就绪。

## Reviewed content-batch contract

Implement every source identity in this Task PRD through the existing authority. Use the source-to-stable-ID map, exact multiset counts, explicit intermediate stages and real processing/serving carriers from the reviewed S04 contract. The complete batch and special production cases are specified in the parent research/menu-review.md batch table. Validate each dish from physical supply through public commands to delivery; do not inject finished items, use direct mutation, or substitute catalog counts for executable recipes. Include wrong-order addition, vessel mismatch, handoff, portions and checkpoint recovery; preserve existing soup/toast regressions.
