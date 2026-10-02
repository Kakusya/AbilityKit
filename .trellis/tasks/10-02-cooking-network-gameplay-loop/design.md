> Current owner instruction (2026-10-02): continue auditing all topics, resolve their plans, then implement and verify to completion without stopping for routine confirmations. Preserve architecture and stage order: singleplayer -> network -> singleplayer Unity -> network Unity. Current stage: singleplayer. Older planning-only notices below are historical. Unity remains deferred under its separate gate.

# N02 初始设计

状态：draft；未实施。

## 审议后的实施边界

前置 S14 的单机世界与可观察状态稳定，N01 正式修约完成。网络只投递同一套 gameplay 命令，不创建第二个配方、订单、供应或前厅 owner。每条指令携带 Participant/Connection 绑定、Match/Level scope、客户端 sequence 和幂等身份，Host 分配权威 receive ordinal；无效身份、旧局和重复 payload 冲突在消费前明确拒绝。有界队列满时给出 backpressure，不偷偷丢掉已经确认接受的业务输入。

Host 本地玩家通过 InProcess Client Connection 完整经过相同 framing/session/pipeline/ingress，与远端 UDP Client 共用固定 Tick 仲裁队列。客户端只显示完整权威快照，禁止直接写玩法状态；新增 position/facing、manual worker/progress、batch balance、绑定、供应和前厅字段必须进 codec 和 hash。

验收采用实际双进程 Host/Client：从供应制作菜品，分别测试同件争抢、同槽放置、半成品接手、容器转移释放设备、饮品贴单、错误恢复、多菜并行及成功跨关。测试证明 callback 收包后、Tick 消费前仿真未变；暂停收到命令不消费；本地与远端竞争遵循同一 receive ordinal。每步比对 Full Snapshot canonical/hash，拒绝后对照臂状态相同。

提供可指定远端 IP/端口的运行器和 artifacts，报告 SameMachineProcess UDP 与 TwoPhysicalPC LAN 的不同拓扑。后者缺环境时保持未完成项，不伪装为已通过。真实双机缺口不能豁免 codec、队列和同机真实 socket 的实现验证。

两台物理 PC 争抢/并行/绑定/提交/跨关状态一致；host 本地与远端共享验证；拒绝不丢料。

沿用 [规划架构记录](../../../Docs/design/CookingGame/gameplay-plan-architecture.md) 的 owner 与数据边界，不创建平行模拟，不修改通用框架的产品职责。具体 API/数据形状、错误矩阵、版本迁移、恢复/跨关状态与测试断言在执行前补齐；本初稿不声称已达到实施就绪。
