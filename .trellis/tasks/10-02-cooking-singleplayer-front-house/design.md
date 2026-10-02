> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S06 初始设计

状态：draft；未实施。

## 本次审议：前厅状态与工作

现有前厅扩展 Arriving→Queued→WalkingToTable→WaitingForInquiry→Ordered→Dining→Leaving→Departed；桌位为 Free/Reserved/Occupied/Dirty，顾客身份与桌号分开。有界 FIFO 排队，截止后不再生成客人，已到店者按既有等待规则排空；不满足订单仍自然结束。

询问、清桌、洗碗为前厅工作，记录稳定ID/目标/elapsed/required与 Available/Working/Paused/Completed/Cancelled，执行者为固定伙伴或 PlayerId。ClaimFrontWork/ContinueFrontWork/StopFrontWork 经同一 ET ingress、scope、幂等和几何验证；一工作一执行者，一玩家不能同时操作厨房手工和前厅工作。玩家停止/离开保留进度释放认领，伙伴只按既有询问优先/无询问洗碗次序取未认领工作，不增加优先级微操。

人工完成不增长伙伴经验；伙伴实际完成才按既有规则增长一次。顾客离开取消未完成询问，不开幽灵订单。清桌释放桌位，脏餐具进入定义的回收路径，不与洗原料混同。自然成功检查队列、行走客人、桌位和人工任务收口，不强制销毁后台半成品或所有脏碗。整局 Pause 不推进任何时钟。

路径读取 S01 的已配置初始几何/目标，不等待可变装修系统；S08 后来提供修改后的同一几何，因此不增加 S06→S08 依赖环。新增状态进入前厅snapshot/canonical/checkpoint，同Level恢复保留，失败丢现场，成功清营业与伙伴成长。具体证据和回归风险见 research/operation-review.md。

复用询问/队列/用餐/离席与洗碗，补人工接手及通路；停止接单与结束分离；未满足不业务失败。

沿用 [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md) 的 owner 与数据边界，不创建平行模拟，不修改通用框架的产品职责。具体 API/数据形状、错误矩阵、版本迁移、恢复/跨关状态与测试断言在执行前补齐；本初稿不声称已达到实施就绪。
