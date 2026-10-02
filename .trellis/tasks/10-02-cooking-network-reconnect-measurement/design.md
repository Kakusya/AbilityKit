> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# N03 初始设计

状态：draft；未实施。

## 审议后的恢复与测量契约

基于 N02 完整状态网络闭环。物理连接重建不自动证明 Participant 恢复：Session 验证 Match-scoped 凭证，在当前 Level 重绑定原逻辑参与者，先发送最新完整 baseline，再接收当前 scope 的新 sequence；断线前未消费输入丢弃，已经提交输入靠幂等记录保证不重复。失效/伪造凭证、旧 epoch、旧 Level、相同 sequence 不同 payload 均有明确拒绝。

固定 Tick 处理连接生命周期产生的玩法影响；callback 不直接放下玩家手物品或释放加工状态。断线处理与暂停规则必须测试先后：暂停不推进模拟；恢复 baseline 对齐 host 当前状态，不恢复客户端旧缓存。既有成功检查点时机不因 reconnect 改变，不扩大 durable storage 或主机迁移范围。

实际测量包括接收→消费→提交时间、队列高水位/拒绝数、吞吐、p50/p95/p99、消息和分配数量；报告 SDK、commit、固定 Tick 参数、客户端数、运行时长、网络拓扑及采样口径。InProcess 只作确定性对照，不替代 UDP 延迟结果。重连中途的半成品、剩余份数、manual 暂停、订单绑定、顾客/供应身份水位须共同恢复。

验收以可重放故障脚本覆盖 Client 断线/重连、Host 暂停/继续、旧局延迟输入、重复提交和跨 Level 凭证恢复。完整 baseline 的 hash 一致仅是必要条件，还须在恢复后继续制作、交付和收尾，证明不是只接收一次快照。

完整基线恢复，旧 epoch/sequence 拒绝，断线队列明确；真实拓扑测量不冒充 InProcess 结果。

沿用 [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md) 的 owner 与数据边界，不创建平行模拟，不修改通用框架的产品职责。具体 API/数据形状、错误矩阵、版本迁移、恢复/跨关状态与测试断言在执行前补齐；本初稿不声称已达到实施就绪。
