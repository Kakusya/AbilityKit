> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# S03 初始设计

状态：draft；未实施。

## 审议后的批次与恢复契约

Recipe 增正整数 YieldPortions（默认 1），首轮多份仅用于 RetainInputs 完成容器；完成时 RemainingPortions 初始化为产量。新增 ServePortion 每次创建一个可搬运成品，先检查源版本、双端可达性、目标空间/类型、锁和 allocator，再一次提交源余额 -1 与目标新增。最后一份消费原加工输入并复位源容器；目标失败不减份数、不消耗 ID 水位。

**Pour 裁定：**未完成内容的整体转移保持现有语义；完成态 Yield=1 保持既有单个成品行为；多份完成容器使用 Pour 明确拒绝，必须 ServePortion 逐份。停止分支把 Pour 改成整批输出，与此契约不符，实施时纠正并加入回归，不静默扩大旧动作。

完成批次禁止追加原料、取出原加工输入和重复 Start。ClearContents 删除内容及 recipe/completed/余额，保留容器 identity、位置和 dirty 状态；加工锁定时拒绝。嵌套容器不随清空销毁，应先 TakeOut 后清空。DiscardItem 仅销毁普通非容器物件并维护手槽/内部索引；丢弃内容与销毁容器不是同一动作。

错误覆盖 ContainerNotFound、ItemStale、ProcessNotComplete、TargetOutOfRange、BatchCompleted、ContainerFull、ContainerRejectsItem；allocator 空/重复/溢出也须保证 staged 状态和计数器不提交。恢复校验余额范围、完成输入多重集、worker/位置及配置身份，坏 checkpoint 原子拒绝。同 Level 保留余额，成功交接不得回满，失败重开用标准供应。

初版 schema/checkpoint 明确升版并拒绝未支持旧格式，不猜测缺失的份数/worker/位置；旧格式迁移另行明确实施。实际版本由同一核心 owner 统一，主分支当前仍为 definition-v2、Level format3，保留分支 v3/v4 只是待验实现候选。

验收必须证明三份分三次、最后份两人争抢仅一次成功、重复命令无复制、所有目标拒绝零变更、清空后容器可重新制作、嵌套错误恢复、allocator 故障水位不变，以及真实 ET 恢复后继续到同一终态。

一锅多份/最后一份争抢守恒；清空保留容器；重复命令不复制；非法投料拒绝。

沿用 [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md) 的 owner 与数据边界，不创建平行模拟，不修改通用框架的产品职责。具体 API/数据形状、错误矩阵、版本迁移、恢复/跨关状态与测试断言在执行前补齐；本初稿不声称已达到实施就绪。
