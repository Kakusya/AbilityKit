> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S07 初始设计

状态：执行设计收敛，等待 S03/S04 生产契约合入；本文件不证明代码完成。

## 本次审议：物理供应与守恒

定义 Supplier、SourceAnchor、ReceivingAnchor、UnitDefinition、PackageDefinition、UnitsPerPackage、DeliveryTicks、FiniteAvailable 或 Infinite。申请 RequestSupply 原子预留有限外部供应，分配稳定 DeliveryId；同一固定 Tick 时钟 Pending→Arrived，ReceiveSupply 只在收货槽可用时一次创建包装及 N 个内部单位物件。槽满保留 Arrived，重复接收不复制，不维护与物件分离的可消费厨房库存数。

包装为现有容器物件，搬运/拆份用既有拿放和TakeOut；无限源 TakeSupply 每次一份入空手，有限源不暗中回满。允许准备和营业中申请/补料，收尾禁止新申请，已有批准货照常到货；在途或待收货可成功跨关保留，不强制全部搬尽才自然结束。首轮不加入取消采购、价格或经济惩罚。

供应余额、预留、在途、接收、消费、交付和丢弃按游戏单位记录守恒，产量转换单独解释；清空不退款。位置/权限/容量/版本/重复等失败零变更，包括allocator。供应状态与身份水位进入snapshot/canonical/checkpoint，同Level恢复保留；失败重开回标准供应/既有成功基线，不保留失败新采购；成功保留与厨房物件一致的余额和在途。

实际验收走 ET Request→Tick等待→Receive→搬包→拆份→加工→交付，覆盖有限耗尽后补料、槽满不吞货、暂停不推进、重复ID、错误清空损耗和销毁重建继续守恒。

包装与游戏份区分、采购/到货/接收/仓储原子化；无限和有限供应不混写；库存不足可恢复。

沿用 [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md) 的 owner 与数据边界，不创建平行模拟，不修改通用框架的产品职责。具体 API/数据形状、错误矩阵、版本迁移、恢复/跨关状态与测试断言在执行前补齐；本初稿不声称已达到实施就绪。

## Scope / Trigger

当前只有标准初始物品和净碗池，没有有限库存采购/到货/包装。增加供应 owner 状态和命令，并接入 RecipeSimulation 同一固定 Tick；不能用 UI 数量或独立供应模拟替代真实物品。负责新 CookingSupply.cs、供应测试，以及既有 command/config/snapshot/checkpoint 的必要增量；核心 S01–S03 写权限在其交付前不抢占。

## Signatures 与 Contracts（实施时与核心字段名称对齐）

- `CookingSupplyDefinition(Id, UnitDefinition, PackageDefinition, UnitsPerPackage, DeliveryTicks, SourceAnchor, ReceivingAnchor, Infinite)`；正份数、正时间、unit/package 必须是内容定义，package 是接受 unit 的可移动容器且容量足够。
- `RequestDelivery`：SupplyId、PackageCount、Player、scope/version/command identity；请求登记待到货单，不直接加厨房库存，不引入采购价格。
- `ReceiveDelivery`：已到货单身份、目标接收槽/位置；完整预验证后生成独立包装实例与内部 N 份物品，库存来自真实实例，不另记一份重复数字账。
- `TakeSupply`：固定无限来源生成一份进入空手；有限来源从实物包装 `TakeOut`，不得隐式补满。拿包装、暂存、倒出复用现有交互。
- `CookingSupplySnapshot`：定义/剩余可申请包数、pending/arrived/received 订单、数量与到货剩余 Tick、ID 水位。现货数量从在册单位实例/容器内容推导。

## Validation & Error Matrix

未知供应/定义→拒绝；数量≤0/溢出→拒绝；缺料或可申请数不足→拒绝零预留；未到货→拒绝接收；接收槽满/包装容量不够/ID分配冲突→一次失败零生成；不可达/非运行态/scope过期→保持既有原因；重复请求/接收→返回原结果，不再扣库存或生成物品。

## Good / Base / Bad

Good：先申请两箱、Tick后到货、一次接收一箱、玩家搬到备料位取单份、另一箱仍待接收。Base：配置无限取料点按一次产生一份；对有限包装同样按键只拿现有份。Bad：接收区已占用，不能先扣到货单再失败；营业补货不隐式跳过等待或接收。

## Tests Required

真实 ET ingress→固定 Tick 到货→接收生成容器→pickup/drop/takeout→recipe，用实例守恒证明包装与份数分离。重复 request/receive、最后一箱/最后一份争抢、满槽失败、非法allocator、暂停不倒计时。pending/arrived/received checkpoint重建与基线等价；成功承接库存与待到货、失败标准重开清新局状态，产品存盘仍只在规定成功出口。

## Wrong vs Correct

Wrong：请求到货就 `inventory += amount`，另在厨房凭空生成，或任何 box 操作都无限取物。Correct：待到货订单、接收生成实物包装与份实例、后续既有移动命令消费同一物件身份；只有配置为 infinite 的来源能产生新份。
