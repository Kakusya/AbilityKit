> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S14 初始设计

状态：draft；未实施。

## 本次审议：单机完整出口

前置 S05/S06/S07/S08 及 S09–S13 全部内容批次。关卡 Ready 前验证每条菜单供应→共享准备→工序→工作容器→出餐容器→交付闭包，包含实际数量、设备能力和可达工位；缺项报告菜品ID/节点/原因，禁止只查最终Recipe已注册。

只读提示投影来自同一authority：玩家身份/手持、材料状态、容器成分和份数、设备进度、订单编号与绑定、前厅工作认领、库存/在途、营业/收尾。提供稳定图标键，不增加UI账本。练习以配置控制有限可重放场景，不引入奖励经济。固定伙伴、评分和跨关规则保持回归。

实际出口为 ET 命令→固定Tick→可观察提示→自然收尾→成功下一关，覆盖完整87菜路线、关卡许可交集、错误恢复、旧scope拒绝、同Level销毁重建继续全态等价与失败重开无采购/认领泄漏。最终门禁必须覆盖集成后行为；各辅助类focused通过不是完整出口。

单机 ET 命令→Tick→提示数据→营业收尾可重放；checkpoint 重建等价；已解锁与本关允许取交集。

沿用 [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md) 的 owner 与数据边界，不创建平行模拟，不修改通用框架的产品职责。具体 API/数据形状、错误矩阵、版本迁移、恢复/跨关状态与测试断言在执行前补齐；本初稿不声称已达到实施就绪。
