> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# U02 初始设计

状态：draft；未实施。

## 本轮审议结论

依赖 U01 及 N03，只将同一网络快照与命令接入 Unity 表现；不建立 Unity 私有模拟或绕过 Session 的远程动作。当前保持条件性、不可执行。未来验收两台物理 PC 的走位、交接、成分/订单可见性和断线后继续工作；临时断线的视觉状态不能造成实际物品复制或客户端自行结算。

解除禁令后两机多人可协作、看懂接手成果，延迟/断线恢复不复制物品；真实场景验收。

沿用 [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md) 的 owner 与数据边界，不创建平行模拟，不修改通用框架的产品职责。具体 API/数据形状、错误矩阵、版本迁移、恢复/跨关状态与测试断言在执行前补齐；本初稿不声称已达到实施就绪。
