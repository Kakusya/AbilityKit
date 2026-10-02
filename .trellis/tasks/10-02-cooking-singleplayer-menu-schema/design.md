> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S04 初始设计

状态：draft；未实施。

## 本次审议：内容与运行契约

以原附件 SHA 和源编号建立稳定映射，禁止按 Excel 行序 zip 分配 ID。72 供应、60 准备状态、19 功能工位、87 成品各自身份独立；中文名冲突拒绝，别名显式声明。保留原367节点，对生成的共享/展开 recipe、交付操作、非独立步骤逐节点记录去向，不能只验生成210步的数量。

Recipe 输入为正整数份数 multiset；展开重复项时校验器、matcher、输入锁、消耗、容量和恢复均保留次数。DefaultInputs 不能免费补成第二份。每个非供应状态有明确 producer，图无环；每关许可闭包包含供应、工位、加工及出餐容器，解锁与允许范围取交集。

阶段以可搬运中间物件和限定后继 recipe 实现，不能仅写 MustLast 或文字 Operation。苏打最后、奶泡/奶油/奶盖最后等规则实际投料时验证；错误早加拒绝并允许恢复。模具倒入→烤制→脱模、烤盘/蒸篮/炸篮装卸须保留真实容器关系，不能只用通用 carrier 字符串替代。

19工位是功能能力，不是每关19固定实例；蒸汽机加热/打泡共享同一设备占用，搅拌/搅打/榨汁及不同热加工能力分开。31饮品原Excel未列贴单台的差异明确保留：制作依赖和交付依赖分列，贴单进入交付闭包，不伪造配方加工步骤。

配置提供显式 fixture 耗时/产量/供应量/评分，用于验证全部路线；不覆盖旧汤/吐司，不宣称正式数值平衡。实际运行 adapter 接入 core Execution/YieldPortions、S05 RequireBinding；不能在适配时拒绝核心必需能力后仍声称目录可制作。

细化证据、全部批次与正反例见总任务 research/menu-review.md。验收包括源行重排身份稳定、未知/冲突诊断、重复份数消耗、公开命令完整生产、必须最后收尾反例、旧菜回归与ET恢复。

逐菜来源拓扑无环、重复份数及阶段顺序正确；本关菜单闭合；旧汤/吐司回归不变；明确版本迁移。

沿用 [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md) 的 owner 与数据边界，不创建平行模拟，不修改通用框架的产品职责。具体 API/数据形状、错误矩阵、版本迁移、恢复/跨关状态与测试断言在执行前补齐；本初稿不声称已达到实施就绪。
