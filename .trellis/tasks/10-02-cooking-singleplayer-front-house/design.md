> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S06 初始设计

状态：执行设计收敛，等待 S01/S05 接口合入；不是已交付。

## 本次审议：前厅状态与工作

现有前厅扩展 Arriving→Queued→WalkingToTable→WaitingForInquiry→Ordered→Dining→Leaving→Departed；桌位为 Free/Reserved/Occupied/Dirty，顾客身份与桌号分开。有界 FIFO 排队，截止后不再生成客人，已到店者按既有等待规则排空；不满足订单仍自然结束。

询问、清桌、洗碗为前厅工作，记录稳定ID/目标/elapsed/required与 Available/Working/Paused/Completed/Cancelled，执行者为固定伙伴或 PlayerId。ClaimFrontWork/ContinueFrontWork/StopFrontWork 经同一 ET ingress、scope、幂等和几何验证；一工作一执行者，一玩家不能同时操作厨房手工和前厅工作。玩家停止/离开保留进度释放认领，伙伴只按既有询问优先/无询问洗碗次序取未认领工作，不增加优先级微操。

人工完成不增长伙伴经验；伙伴实际完成才按既有规则增长一次。顾客离开取消未完成询问，不开幽灵订单。清桌释放桌位，脏餐具进入定义的回收路径，不与洗原料混同。自然成功检查队列、行走客人、桌位和人工任务收口，不强制销毁后台半成品或所有脏碗。整局 Pause 不推进任何时钟。

路径读取 S01 的已配置初始几何/目标，不等待可变装修系统；S08 后来提供修改后的同一几何，因此不增加 S06→S08 依赖环。新增状态进入前厅snapshot/canonical/checkpoint，同Level恢复保留，失败丢现场，成功清营业与伙伴成长。具体证据和回归风险见 research/operation-review.md。

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
