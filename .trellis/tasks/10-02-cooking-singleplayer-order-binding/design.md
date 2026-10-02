> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S05 初始设计

状态：draft；未实施。

## 本次审议：订单绑定与提交

在现有 Recipe/Order owner 内绑定最终成品 ItemId→OrderId，不另造票据物品或第二套订单。成品制作不要求订单，未绑定可搬运暂存。BindOrder、UnbindOrder、RebindOrder 走既有 ingress/scope/版本/幂等；绑定时必须是处于正确出餐容器的唯一合法最终产品、匹配同 Level Open 订单，并通过位置校验。每成品一绑定、每订单一待交付成品；竞争按现有稳定排序，一人成功。

解绑保留食物；换绑先验证目标单再一次提交，失败保留旧绑定。订单关闭、成品销毁或成功跨关清理对应绑定；同 Level checkpoint 保留合法绑定并校验双向唯一性。绑定版本进入物品/绑定快照及canonical，不能仅藏在UI。

OrderTemplate 的 RequireBinding 默认为 false，31饮品为 true；配置校验和identity、真实 SubmitOrder 同步检查。餐食和甜品直接核单提交，不额外贴标。绑定不是实际交付；成功交付消费产品、撤销绑定、结算一次，重复提交不能多结算。

容器提交后的归宿显式配置，保持旧 washable 行为兼容。新菜单餐盘/碗采用现有可洗回收；透明/热饮杯以一次性出餐容器处理，提交消耗并释放手槽/位置，S07 提供补杯。工作锅/模具不允许直接交付。不得把未配置可洗的杯默认为无限复用，也不得把一次性杯送入洗碗队列。默认历史非可洗容器行为保留，新增一次性集合或等价配置进入identity和恢复校验。

错误矩阵覆盖非成品/错容器、无单/已完成、配方不符、旧scope/版本、已绑定占用、不可达；全部拒绝零变更。验收包括饮品裸交拒绝、错误换绑保留原单、两人同杯绑定、解绑换人交付、消耗杯补充、餐食不贴票，以及绑定中ET恢复继续到同一终态。

未绑定成品可暂存；饮品绑定/换绑/解绑明确；餐食不强制贴标；重复交付只结算一次。

沿用 [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md) 的 owner 与数据边界，不创建平行模拟，不修改通用框架的产品职责。具体 API/数据形状、错误矩阵、版本迁移、恢复/跨关状态与测试断言在执行前补齐；本初稿不声称已达到实施就绪。
