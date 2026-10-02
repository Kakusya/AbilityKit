> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S08 初始设计

状态：draft；未实施。

## 本次审议：准备态原子安装

沿现有 Level preparation owner，Created/Preparing 可提交完整布局，Ready/Running/Paused/Ending/Ended 拒绝。设备许可为默认基础授权加已解锁，与本关允许集取交集；占地与交互面来自定义，不信任调用方缩小设备大小。

布局包含地板区域/墙/设备旋转和占地/入口/队列/桌位/出餐/收货/仓储/清洗目标；扩建是可用地板集合，首轮不引入购买价格。一次候选中校验占位、角色半径净空、连通、权限、物件与工位迁移、工作引用及玩家保留或确定性重定位，再原子安装。失败厨房/选择/位姿/allocator/hash均不变，修正现有装修空旧选择回滚和解锁先写厨房风险。

成功更新真实S01障碍与锚点，后续旧位置操作失败、新位置成功。路径用相同尺度、半径和稳定邻居顺序；顾客入口→桌→出口及工作入口→必需设备均可达，不以格中心BFS代替半径净空。保留分支静态辅助尚未接入，不算功能已完成。

几何全量及hash进入checkpoint，先验证候选再安装和恢复对象引用。同Level恢复保持有效几何；跨关允许集变化需要合法新布局或明确拒绝，不偷偷删带食材容器。版本与核心/供应/前厅由同一协调者统一；每次正式字段集合变化显式版本化，不复用历史版本隐藏差异。

准备态移动/旋转/区域扩建；占位/交互面/顾客与玩家通路验证后提交；营业中拒绝修改。

沿用 [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md) 的 owner 与数据边界，不创建平行模拟，不修改通用框架的产品职责。具体 API/数据形状、错误矩阵、版本迁移、恢复/跨关状态与测试断言在执行前补齐；本初稿不声称已达到实施就绪。
