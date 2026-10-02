> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# N01 初始设计

状态：draft；未实施。

## 来源冲突审议结果

已核对 `09-19-cooking-productization-network-slice-planning/research/discussion-notes.md` 第 28、29 节的 owner 明确决定：它们晚于 KCP 临时方案，且显式确认唯一真实传输为通用 LiteNet reliable-UDP；InProcess 仅用于本地与测试。TCP 不进入该路线，KCP 不是当前前置。不删除历史实现或证据。网络阶段开始时把此来源同步进 ADR 的带日期附注及生效 spec，保留旧决策追溯。

沿用既有 IChannelListener、IServerChannel、NetworkHost、ServerNetworkSession；通用 LiteNet 包补齐 Listener/ServerChannel，不新增平行传输抽象。Cooking Session 不直接拥有 LiteNet NetManager，通用传输包不拥有菜品或订单。共享源码变化必须同步检查 Unity asmdef/package 与 SDK csproj。

网络回调仅收发、解帧和有界入队，不改变模拟。Host 在固定 Tick 消费队列；网络命令使用权威接收序号先到先得，序号在入队原子分配，不能依赖线程再次竞争。单机已有稳定排序不静默变更；实现时明确两种入口的排序策略并测试 host 本地与远端使用同一网络队列。暂停停止逻辑 Tick 和命令消费；断线丢弃该 Connection 未消费输入。

首个纵切采用完整快照广播与被动 Client，不实现预测/回滚/delta 优化。重连由物理 ConnectionManager 和应用 Session 分担；Session 验证凭证、绑定原 Participant、清理临时队列、发送完整 baseline。旧 Match/Level/epoch/sequence 拒绝。新增单机位姿、加工、份数、绑定、前厅和供应状态必须真实 wire round-trip，不只携带 opaque hash。

N01 可提前研究和审议，N02/N03 生产整合等待 S14。实际双物理 PC LAN 与同机双进程 UDP 分开记证据；当前没有已配对第二主机，不能把同机通过标成两机通过。该环境缺口不阻止单机实施、通用 adapter 和可移植双端运行器准备。

更新权威决定与唯一传输方向，确定身份、命令/快照契约和真实 LAN 验收；不以旧纪要自动执行。

沿用 [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md) 的 owner 与数据边界，不创建平行模拟，不修改通用框架的产品职责。具体 API/数据形状、错误矩阵、版本迁移、恢复/跨关状态与测试断言在执行前补齐；本初稿不声称已达到实施就绪。
