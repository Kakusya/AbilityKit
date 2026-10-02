# S06 初始设计

状态：执行设计收敛，等待 S01/S05 接口合入；不是已交付。

复用询问/队列/用餐/离席与洗碗，补人工接手及通路；停止接单与结束分离；未满足不业务失败。

沿用 [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md) 的 owner 与数据边界，不创建平行模拟，不修改通用框架的产品职责。具体 API/数据形状、错误矩阵、版本迁移、恢复/跨关状态与测试断言在执行前补齐；本初稿不声称已达到实施就绪。

## Scope / Trigger

扩展现有 CookingFrontOfHouse，不另建顾客/订单。复用桌位、稳定顾客身份、固定伙伴询问优先/洗碗次之和 Level-local 成长；正常营业自然结束保持。前厅人工接手不是对伙伴做岗位分派或优先级微操。

## Signatures / Contracts

前厅人工命令通过 ET host 固定 Tick command ingress，携带 PlayerId、CustomerId/ItemId、操作、scope、命令ID/序号。Inquiry/Wash work 声明唯一执行者与 elapsed/required ticks；玩家认领尚未被占用工作，离开范围/松手保留进度，伙伴也不能重复认领该项。伙伴完成数只统计伙伴实际完成，不把玩家任务算成长。洗碗对具体脏容器实例而非每帧加计数。

顾客运行态补入口等待/队列/到桌/用餐/离席/待清桌，保存身份、桌引用、路径/进度；通路复用 S01/S08 空间，不能用模型动画结束推进领域时间。订单身份仍跟随顾客而非桌位。提交按明确服务方式（出餐口或桌）校验，放普通台面不等于交付。停止来客/接单与处理已有营业、收尾分别记录。

## Validation & Error Matrix

已离席/错误顾客桌引用→拒绝；被伙伴/他人占用工作→WorkOccupied；同玩家同时工作→PlayerAlreadyWorking；不在交互范围/非法杯碗→拒绝；重复询问→不重开订单；重复洗碗→不增加净容器；未满足不新增业务失败。关闭收尾只在已服务完/离席/必要清理完成时自然成功。

## Good / Base / Bad

Good：伙伴问一桌，玩家问另一桌，菜品可由任意玩家加工/核单送出；有人离岗，成果仍可识别接手。Base：无人工动作的旧固定伙伴场景结果保留。Bad：不可因为 manual wash 再完成一次已被伙伴洗掉的碗，或新顾客复用旧桌号串旧订单。

## Tests Required

ET命令与伙伴同帧冲突只有一个owner、未满足0分且0星完成、营业结束仍处理待交付/用餐/清桌、pause不推进。手工接续、顾客路径不可穿阻挡、重复询问/洗碗、厨具回收。前厅 snapshot/canonical/hash/checkpoint覆盖人工工作/顾客路径/队列；恢复与基线相等，success/reset和failed重开保持既有语义。

## Wrong vs Correct

Wrong：多个问单入口直接开同一顾客的订单；伙伴与玩家各有脏碗池。Correct：现有前厅 owner 唯一顾客和任务状态，复用现有厨房容器实例池，命令在同一固定 Tick 的明确顺序裁决。
