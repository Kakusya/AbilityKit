# F08 前厅交付策略只读独立审阅

2026-10-02。对象：integration 当前 `CookingFrontOfHouseConfiguration`、ET host、SubmitOrder 与 `CookingFrontDeliveryEtTests`。与 layout_check 并行只读，不写生产代码，不运行 .NET。本记录只审议 F08 增量，不声称 S06/S14 整体完成。

## 结论

限定静态通过：新增策略接入正确，未发现阻断问题。实际聚焦与组合门禁仍由 root 验证，本审阅不作新运行通过声明。

- optional DeliveryPolicy 由既有可信factory配置拥有，Freeze拒绝非法mode、ServingAnchor缺名、CustomerTable附带不该有的anchor。配置Canonical/Identity包含策略；Level checkpoint包含并比较FrontOfHouseConfigurationIdentity，改变可信factory策略无法继承原checkpoint。
- ET启动/绑定校验真实厨房几何，要求目的ID在所有anchor Kind中唯一，避免同名World/Station歧义。CustomerTable按配置桌数要求table-1..N；ServingAnchor按唯一指定出餐点要求。恢复先依据可信配置identity预检，重建前厅后重新BindFrontOfHouse，回调捕获新kitchen与新house而非旧实例。
- ValidateFrontDelivery从当前唯一前厅snapshot查询同Order且Phase=Ordered的顾客，再选择当前顾客TableId或可信出餐anchor；不是从用户输入的任意桌号推断归属，也不按左边第几个顾客。调用核心ValidateSpatialReach，读取真实player pose/朝向、范围和阻挡规则；没有单独引入UI距离或固定桌分配缓存。
- SubmitOrder在配方/容器/能力/绑定检查之后调用只读predicate，且在移除物品、扣净容器、更新订单、settlement与版本之前拒绝。回调本身只读snapshot/几何。失败不吞食物或餐盘，不清绑定或开幽灵settlement。Tick正常时钟推进不等于拒绝动作改写食物，测试按items/hands/orders/settlements核对这一边界。
- legacy null策略显式绑定null predicate，保持旧generic交付行为；不是默认给既有菜单新增桌送约束。有策略却无当前Ordered顾客则拒绝；已离席订单在更早的终态订单检查已拒绝也安全，不必强行进入新predicate才算拒绝有效。

## 测试审阅与证据边界

测试真实Host.TryEnqueue/Tick：远端拒绝、拿同一份菜去另一桌拒绝、背对目标拒绝，再移动/转向后向实际订单桌提交成功；原拒绝命令到达目的后重放仍保留幂等拒绝结果。恢复Theory涵盖两种交付模式、可信factory策略改变拒绝恢复、新host继续远端拒绝再合法提交。null策略兼容与非法/缺失/跨Kind歧义anchor的配置拒绝均有用例。

离席Theory审阅时已改为两种mode；它验证顾客离开后的旧订单被OrderAlreadyCompleted拒绝，并非证明新predicate的customer-null分支单独覆盖。fixture直接由可信factory配置旧无Flow前厅，真实订单由伙伴开出；不据此证明顾客动态路径/可变布局适配或完整经营结束。

恢复测试包含dispose旧owner并重新建立callback，但本增量尚未通过自身测试比较不中断对照臂最终fullcheckpoint；已有其他恢复用例不代替这条新策略的独立端到端等价证明。若要求F08专属恢复等价，可补两mode control-arm测试，当前不视为阻止实际新host空间限制行为的缺口。

Lint/TypeCheck/Tests：本审阅未运行，等待root实际复跑。未修改代码或task metadata。
