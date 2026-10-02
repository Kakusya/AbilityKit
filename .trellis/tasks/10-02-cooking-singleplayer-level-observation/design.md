# S14 初始设计

状态：draft；未实施。

单机 ET 命令→Tick→提示数据→营业收尾可重放；checkpoint 重建等价；已解锁与本关允许取交集。

沿用 [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md) 的 owner 与数据边界，不创建平行模拟，不修改通用框架的产品职责。具体 API/数据形状、错误矩阵、版本迁移、恢复/跨关状态与测试断言在执行前补齐；本初稿不声称已达到实施就绪。
