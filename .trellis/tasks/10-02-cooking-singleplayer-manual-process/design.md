> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S02 初始设计

状态：draft；未实施。

## 审议后的执行契约

沿用 CookingRecipeSimulation。Recipe 增 `Execution=Automatic|Manual`（默认 Automatic），既有 StartProcess 建立任务；Manual 起始时绑定唯一 ActiveWorker，新增 ContinueProcess/StopProcess 表达认领和停止。开始者身份与当前 worker 分开，甲开始不要求甲完成。一个玩家最多一项手工作业、一项作业最多一个 worker；不叠加加速。

状态流：Start→Active→Stop/离开→Paused→另一人 Continue→Active→Completed。停止、离开范围或失去可操作朝向保留进度和输入锁；每个固定 Tick 活跃手工进度最多 +1，Automatic 不依赖玩家停留。容器搬移后的可达性再次检查；legacy AdvanceTicks 不绕过手工参与条件。Host lifecycle Pause 停止整个模拟，不能等同于作业的 Paused。

ProcessNotFound、WorkerUnavailable、TargetOutOfRange、版本/scope/幂等冲突遵循结构化错误，拒绝时进度、锁、worker、allocator 和 canonical 均不变。Stop 只允许当前 worker，Continue 只允许空闲合法参与者认领暂停任务。worker、elapsed 与配置 Execution 进入 snapshot/config identity/checkpoint；同 Level 恢复保留合法认领，成功交接清 worker 保留工作成果，失败重开采用标准初始状态。

正式验收覆盖两人同 Tick 认领、甲推进后停工再由乙继续、暂停期间不偷进度、离开与搬移释放、自动设备继续、完成释放以及真实 ET ingress→Tick→销毁重建→继续等价。证据与保留分支差异见总任务 research/core-review.md；当前不以 worker 自报测试证明完成。

松手与离开暂停保留，换人接续，不默认叠加加速；设备与端锅加工继续符合既有契约。

沿用 [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md) 的 owner 与数据边界，不创建平行模拟，不修改通用框架的产品职责。具体 API/数据形状、错误矩阵、版本迁移、恢复/跨关状态与测试断言在执行前补齐；本初稿不声称已达到实施就绪。
