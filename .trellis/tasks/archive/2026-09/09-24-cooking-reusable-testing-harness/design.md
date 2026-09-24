# Cooking 局内与网络纵切可复用测试框架设计文档 (Design)

## 1. 结构与类图设计

测试框架放置在测试程序集内（`AbilityKit.Game.Cooking.Tests/Harness/`）：

- **`ICookingTestTopology`**
  - 管理执行上下文（生命周期、Session、Match、玩家加入）。
  - `GetActor(PlayerId id)` 返回具体玩家的 `ICookingActor`。
  - `AdvanceTicksAsync(int ticks)` 统一推进世界逻辑。
  - `AssertStateHashConsensus()` 校验权威端与各参与方投影端快照哈希一致性。

- **三种拓扑实现**：
  1. `DirectMemoryTopology`：包装单机 `CookingRecipeSimulation`，命令派发直接调 `simulation.SubmitCommand()`。
  2. `InProcessPairTopology`：包装 Host 侧 Simulation 与 Client 侧 Projection，通过双向 `InMemoryTransport` 管道传输 `CookingUdpCodec`（或通用 FramePacket 协议），测试在同一进程内进行网络消息封包与解包验证。
  3. `LoopbackUdpTopology`：基于 `LiteNetTransport`，启动真实监听端口与客户端连接，验证传输帧与真实网络驱动。

- **场景抽象与复用 (`ICookingScenario`)**：
  - `Task ExecuteAsync(ICookingTestTopology topology, CancellationToken ct)`
  - 典型实现：`TomatoEggSoupScenario`，包含从标准供应出发完成一份番茄蛋花汤并交付的标准动作流。

## 2. 确定性与一致性保证

- 命令提交返回 `CookingCommandResult`（包含结果 Outcome, Reason, StateVersion, Events 等）。
- 每次关键交互后调用 `topology.AssertStateHashConsensus()`，确保不仅没有引发异常，而且 Client 侧投影计算出的快照 SHA-256 与 Host 权威端字节级一致。
