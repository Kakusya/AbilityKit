# Research: S06 服务目的地缺口与最小修约

- Query: F08 明确出餐口/桌位校验是否已接入，以及沿既有authority最小补足合同。
- Scope: internal；integration工作区只读，供implement角色执行。
- Date: 2026-10-02

## Findings

确认真实缺口：`10-02-cooking-singleplayer-front-house/design.md:29` 要求提交按出餐口/桌位服务方式校验；当前 `src/AbilityKit.Game.Cooking/CookingRecipeLoop.cs:1959` SubmitOrder只查产品/订单/版本、物件位于出餐容器、容器对玩家可达及配方/绑定匹配。持物容器天然可达，无顾客桌位/出餐口位置前置，因此拿盘在任意厨房位置可以远程提交。`CookingFrontOfHouseConfiguration`尚无服务方式合同。前一独立接线审阅未覆盖此设计验收，本文件修正该审阅证据边界；不能以gate通过宣称完整S06交付。

最小修约：

1. 可信front配置添加可选 `CookingFrontDeliveryPolicy`：模式 ServingAnchor/CustomerTable，ServingAnchor模式须有非空稳定anchor ID；CustomerTable目标由现有客户/订单关系派生，不接受命令自报destination。legacy null保持现有通用SubmitOrder。
2. 配置Freeze校验模式和字段互斥；绑定实际厨房时校验目标anchors存在且空间配置有效。canonical/Identity纳入mode/anchor；配置外部factory可信，checkpoint既有FrontOfHouseConfigurationIdentity比较涵盖新增字段。格式6未发布可保留，明确null是legacy，不能丢JsonRequired现有身份字段。
3. 厨房新增internal只读delivery predicate，输入当前PlayerId/OrderId，输出结构化是否允许/原因。SubmitOrder所有现有纯验证后、任何结算/销毁/allocator mutation前调用；不改变物件ownership、绑定或结算规则。错误目的地/超距拒绝不改变物件版本/状态/hash/事件序号，仅原幂等拒绝账按既有规则记录。
4. predicate由host BindFrontOfHouse绑定，捕获当前新simulation/当前house；ServingAnchor也验证订单对应的客人仍在可服务阶段，避免给已离席客人交餐。CustomerTable从当前house Customers中找匹配Order且处于Ordered的实际桌号，拒绝未找到/离开/用餐中；按实际姿态ValidateSpatialReach选可信anchor.Kind/Id。不使用Recipe.Player历史字段。
5. 同Level恢复重新绑定predicate到新owner。成功/失败下一关保持可信policy但客户/订单短态按既有规则重置；旧scope命令沿原lane拒绝。不得用payload伪造ServingAnchor绕过factoryidentity。

实际测试最低组合：同一host两张同类订单/两桌；持盘远处提交失败，错误桌附近失败，正确桌附近成功一次；ServingAnchor远处失败近处成功；已离席客户拒绝；legacy无policy继续旧fixture；同identity拒绝重放不变，freshidentity到正确位置可成功；保存未交付成品后Dispose/Restore新factory、新pose移动后目的地校验依然正确；伪造policyanchor/模式checkpoint身份拒绝。运行实际专属ETfilter和适用gate后再完成S06记录。

## Caveats / Not Found

本research角色developer禁止修改源，只能写总任务research；已通知root将实施转交implement角色。未执行.NET、未改变生产文件。服务目的地校验属于S06缺失验收，不撤销S05通用制作/绑定组件已验证scope，不扩大到贴票工位强制或S07/S08实现。
