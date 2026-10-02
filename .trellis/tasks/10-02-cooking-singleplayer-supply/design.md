> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S07 初始设计

状态：draft；未实施。

## 本次审议：物理供应与守恒

定义 Supplier、SourceAnchor、ReceivingAnchor、UnitDefinition、PackageDefinition、UnitsPerPackage、DeliveryTicks、FiniteAvailable 或 Infinite。申请 RequestSupply 原子预留有限外部供应，分配稳定 DeliveryId；同一固定 Tick 时钟 Pending→Arrived，ReceiveSupply 只在收货槽可用时一次创建包装及 N 个内部单位物件。槽满保留 Arrived，重复接收不复制，不维护与物件分离的可消费厨房库存数。

包装为现有容器物件，搬运/拆份用既有拿放和TakeOut；无限源 TakeSupply 每次一份入空手，有限源不暗中回满。允许准备和营业中申请/补料，收尾禁止新申请，已有批准货照常到货；在途或待收货可成功跨关保留，不强制全部搬尽才自然结束。首轮不加入取消采购、价格或经济惩罚。

供应余额、预留、在途、接收、消费、交付和丢弃按游戏单位记录守恒，产量转换单独解释；清空不退款。位置/权限/容量/版本/重复等失败零变更，包括allocator。供应状态与身份水位进入snapshot/canonical/checkpoint，同Level恢复保留；失败重开回标准供应/既有成功基线，不保留失败新采购；成功保留与厨房物件一致的余额和在途。

实际验收走 ET Request→Tick等待→Receive→搬包→拆份→加工→交付，覆盖有限耗尽后补料、槽满不吞货、暂停不推进、重复ID、错误清空损耗和销毁重建继续守恒。

包装与游戏份区分、采购/到货/接收/仓储原子化；无限和有限供应不混写；库存不足可恢复。

沿用 [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md) 的 owner 与数据边界，不创建平行模拟，不修改通用框架的产品职责。具体 API/数据形状、错误矩阵、版本迁移、恢复/跨关状态与测试断言在执行前补齐；本初稿不声称已达到实施就绪。
