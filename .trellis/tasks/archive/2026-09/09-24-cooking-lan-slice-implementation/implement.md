# Cooking 双人局域网网络纵切实施计划 (Implement)

## 实施步骤

1. **协议层与数据载荷实现 (`CookingLanPackets.cs` / `CookingLanCodec.cs`)**
   - 定义双端握手、命令分发、结果通知、快照同步包结构；
   - 实现高可靠且跨平台一致的序列化/反序列化工具。

2. **Host 端网络接入服务 (`CookingLanHost.cs`)**
   - 管理 LiteNetLib 监听端点与客户端 Peer 列表；
   - 处理客户端握手并分配 `PlayerId`；
   - 接收网络命令并在权威 Simulation 中安全单读派发执行；
   - 回传命令结果并广播状态快照。

3. **Client 端网络接入与投影 (`CookingLanClient.cs`)**
   - 基于通用 `LiteNetTransport` 建立连接与握手；
   - 维护本地只读快照投影；
   - 提供异步命令发送与结果等待机制。

4. **实现测试拓扑适配器 (`LoopbackUdpTopology.cs`)**
   - 继承 `ICookingTestTopology`；
   - 集成 `CookingLanHost` 与 `CookingLanClient`；
   - 提供双端 Actor 门面适配与状态共识断言。

5. **编写端到端验收用例并验证**
   - 在 `CookingReusableHarnessAcceptanceTests.cs` 中添加 `Scenario_executes_successfully_and_achieves_consensus_under_LoopbackUdpTopology`；
   - 运行 `dotnet test src/AbilityKit.Game.Cooking.Tests/`；
   - 记录 `check.jsonl`，确保全部测试用例 100% 通过。
