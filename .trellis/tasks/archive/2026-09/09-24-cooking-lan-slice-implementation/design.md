# Cooking 双人局域网网络纵切技术设计 (Design)

## 1. 架构总览与分层

```text
+-------------------------------------------------------------+
|                  TomatoEggSoupScenario (测试用例)            |
+-------------------------------------------------------------+
               |                               |
       (Chef A: Host Actor)            (Chef B: Client Actor)
               |                               |
+------------------------------+ +----------------------------+
|  Host Simulation Authority   | | Client Network Controller  |
|  - CookingRecipeSimulation   | | - CookingRecipeSnapshot 投影|
|  - Single-Reader Dispatcher  | | - Pending Commands & Tcs   |
+------------------------------+ +----------------------------+
               ^                               ^
               |                               |
+------------------------------+ +----------------------------+
| Host Transport / NetManager  | | Client LiteNetTransport    |
| (LiteNetLib Reliable-Ordered)| | (LiteNetLib Reliable-UDP)  |
+------------------------------+ +----------------------------+
               \                               /
                <======== Loopback UDP =======>
                         (127.0.0.1:port)
```

## 2. 协议与消息定义 (`CookingNetworkProtocol`)

采用统一的消息信封格式（Envelope），基于 JSON UTF-8 序列化并进行字节传输：

1. **`HandshakeRequest`** (Client -> Host): 携带客户端请求标识、ProtocolVersion、能力清单。
2. **`HandshakeResponse`** (Host -> Client): 携带分配的 `PlayerId`、初始 Session/Level 信息。
3. **`RecipeCommandPacket`** (Client -> Host):
   - `CorrelationId`: 客户端命令追踪唯一标识；
   - `PlayerId`: 发送者玩家 ID；
   - `Operation`: 动作类型（Pickup, Drop, PutIn, StartProcess, Pour, SubmitOrder 等）；
   - `Item`, `TargetItem`, `Station`, `Recipe`, `Order`: 相关参数。
4. **`RecipeCommandResultPacket`** (Host -> Client):
   - `CorrelationId`: 对应的命令追踪标识；
   - `Outcome`: `Accepted` / `Rejected`；
   - `Reason`: 失败原因；
   - `Version`: 执行后快照版本。
5. **`SnapshotSyncPacket`** (Host -> Client):
   - `Snapshot`: 权威端在 Tick 推进或状态变更后的全量/基线快照（包含 Items, Stations, Orders, Version, TimestampTicks, SHA-256 StateHash 等）。

## 3. Host 端网络调度器 (`CookingLanHost`)

- 内部运行 `LiteNetLib.NetManager` 监听指定端口（使用 UnsyncedEvents 保证事件即时投递）。
- 接收客户端握手并绑定连接与 `PlayerId`（Chef B）。
- 拥有唯一的 `CookingRecipeSimulation` 权威实例与单写/单读锁。
- 接收到客户端命令后，在权威线程排队调用 `simulation.ExecuteCommand(...)`，并向客户端回送 `RecipeCommandResultPacket`。
- 在逻辑 Tick 推进（`AdvanceFixedTick`）后，提取权威快照，打包 `SnapshotSyncPacket` 广播给所有客户端。

## 4. Client 端网络控制器 (`CookingLanClient`)

- 基于通用 `LiteNetTransport` 连接 Host。
- 启动时自动发送握手并等待分配角色。
- 维护本地状态投影 `CookingRecipeSnapshot`。
- 实现 `ICookingActor` 接口：
  - 调用 `actor.PickupAsync(...)` 等高层方法时，生成唯一 `CorrelationId`，封包发送至 Host，并在返回 `RecipeCommandResultPacket` 后完成 Task。
- 接收 Host 的 `SnapshotSyncPacket` 并即时刷新本地投影对象。

## 5. Loopback 拓扑适配器 (`LoopbackUdpTopology`)

- 实现 `ICookingTestTopology`；
- 构造时选择未被占用的端口，启动 `CookingLanHost`，并初始化 `CookingLanClient` 完成握手与对齐；
- `GetActor(playerId)`：Host 玩家返回 Host 本地 Actor（直接调用 Host 权威排队），Client 玩家返回网络 Client Actor；
- `AdvanceTicksAsync(count)`：驱动 Host 逻辑时钟推进，并等待快照同步送达 Client；
- `SyncAndDrainAsync(timeout)`：等待并排空管道中的所有在途命令与快照；
- `AssertStateHashConsensus()`：对比 Host 权威快照与 Client 本地投影的 SHA-256 哈希值，断言二者严格一致。
