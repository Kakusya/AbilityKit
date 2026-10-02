> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# U01 初始设计

状态：draft；未实施。

## 本轮审议结论

本 Task 明确为单机和网络工程出口后的条件性 Unity 阶段，当前不创建 Cooking Unity package、scene、authoring 或 UI。未来以同一权威的只读投影实现镜头、玩家颜色、目标高亮、手持与容器成分、加工和订单反馈，不在 MonoBehaviour 重写规则。届时须单独解除已有禁令并确认环境；实际 compile/EditMode/场景 smoke 与玩家走位、交接手感共同验收。纯 .NET 成功不代替可玩体验。

解除禁令后才能实施；看清物品/内容/进度/订单，连续走位与拿放顺畅；compile/EditMode/scene smoke 实跑。

沿用 [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md) 的 owner 与数据边界，不创建平行模拟，不修改通用框架的产品职责。具体 API/数据形状、错误矩阵、版本迁移、恢复/跨关状态与测试断言在执行前补齐；本初稿不声称已达到实施就绪。
