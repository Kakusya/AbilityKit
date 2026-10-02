> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S15 初始设计

状态：draft；未实施。

## 本轮审议结论

既有固定基础分、0–3 星、未满足 0 分、0 星自然完成，以及伙伴 Level-local 成长保持回归；不因为菜单扩充新增处罚。K03–K14 的新成长、卡片、角色能力、家具收益、挑战与随机事件，H03–H06 的烧焦/火灾/故障/污渍，以及投掷/接取/冲刺/DLC 特殊运输继续留作参考池，不是基础验收前置。此结论已明确范围，不把“尚未选择扩展数值”当作阻止 S01–S14 的未决议题。

先保留既有评分/伙伴 Level-local 成长，再逐项审议扩展；任何风险/失败/随机/经济规则不默认为基础。

沿用 [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md) 的 owner 与数据边界，不创建平行模拟，不修改通用框架的产品职责。具体 API/数据形状、错误矩阵、版本迁移、恢复/跨关状态与测试断言在执行前补齐；本初稿不声称已达到实施就绪。
