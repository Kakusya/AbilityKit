# com.abilitykit.network.transport.litenet

> AbilityKit 的**可选可靠 UDP 传输**（`ITransport` 实现），基于 [LiteNetLib](https://github.com/RevenantX/LiteNetLib)，使用 `DeliveryMethod.ReliableOrdered`。它是面向低时延游戏网络的候选实现；当前仓库没有真实弱网或 TCP 对比数据，不能据此声称已获得更低延迟。

- **版本**：0.1.0
- **命名空间**：`AbilityKit.Network.Transport.LiteNet`
- **依赖**：`com.abilitykit.network.runtime` 0.1.0 + **LiteNetLib**（.NET 经 NuGet `LiteNetLib 2.1.4`；Unity 需 `LiteNetLib.dll`，经 NuGet-for-Unity 或手动放入）
- **类型**：`LiteNetTransport : ITransport`、`LiteNetChannelListener : IChannelListener`、`LiteNetServerChannel : IServerChannel`。服务端另依赖 `com.abilitykit.network.host` 0.1.0。

## 适用与边界

- 候选场景是 FPS、动作和竞技游戏，但是否优于 TCP 必须在目标部署网络、消息模型和弱网参数下压测。
- 当前固定使用 LiteNetLib `ReliableOrdered` 通道承载 `ConnectionManager` 的成帧字节。LiteNetLib 的连接/可靠机制与上层心跳、重连会叠加，应联合验证超时和恢复时序。
- 这不是“把 TCP 端口改成 UDP 端口”即可运行的替换：服务端必须实现相同 connection key、LiteNetLib 会话和 AbilityKit frame protocol。

## Unity 设置

**无需手动操作** —— `LiteNetLib.dll`（netstandard2.1）已内置在 `Runtime/Plugins/`，Unity 自动引用。

## 用法

```csharp
var sdk = new NetworkSdkBuilder()
    .UseTransportFactory(() => new LiteNetTransport(connectionKey: "your-shared-key"))
    .ConfigureConnection(o => { /* heartbeat / reconnect / FrameCodec */ })
    .Build();
sdk.Open(host, port);
```

- ctor：`LiteNetTransport(connectionKey: "abilitykit")`；客户端与服务端的 connection key 必须一致。
- `UnsyncedEvents = true` 使事件由 LiteNetLib 内部线程触发，无需外部 `PollEvents`。默认 inline dispatcher 会让上层回调继续运行在该线程；Unity 业务应显式派发到主线程。

## 服务端与验证状态

- 通用 server listener 复用既有 `NetworkHost` / `ServerNetworkSession` framing 和 pipeline；本包不拥有业务 DTO、玩家身份、重连凭证或模拟状态。
- 测试目录包含本机 ephemeral UDP echo、framed Host/pipeline、listener/channel ownership、回调清理、消息寿命和并发生命周期控制；实际验证结果见活动 N02 的 `research/transport-increment.md`。这些场景不代表真实弱网、NAT、双物理 PC 或长期稳定性已经通过。
- 在服务端监听、部署网络、线程派发和恢复策略共同验收前，应将本包视为 E0 实现 + E3 局部回环测试，而不是生产成熟 transport。

## 相关
- 默认传输 → `com.abilitykit.network.runtime`（`TcpTransport`）
- 另一可选传输 → `com.abilitykit.network.transport.websocket`（WebSocket）
- 组装根 → `com.abilitykit.network.sdk`
- 接入清单 → `Docs/design/07-NetworkSynchronization/07-MultiplayerSdkIntegrationGuide.md`

## Listener ownership

```csharp
var listener = new LiteNetChannelListener(IPAddress.Loopback, port: 0, connectionKey: "your-shared-key");
var host = new NetworkHost(listener, new NetworkHostOptions { RequestHandler = router });
host.Start(); // Endpoint contains the actual ephemeral port.
```

`ChannelAccepted` 返回后，订阅者拥有该 channel。`listener.Stop()` 仅停止接入；`listener.Dispose()` 同样不会关闭已经转移的 peer。它们继续共享 LiteNet manager，最后一名 channel owner 释放后才终止该 manager。`NetworkHost` 自己负责关闭其 session；直接接收 channel 的用户必须 Dispose channel。关闭 manager 的工作派发到线程池，避免 receive callback 自我 join，因此底层 socket 释放是异步的。

收包使用 `GetRemainingBytes()` 的独立数组，并启用 `AutoRecycle`。接受回调完成前或尚未安装 receive handler 时消息暂存；每个 peer 默认最多 8 MiB / 1024 条待投递消息。超过限制会报告 Error 并关闭 peer，不能继续无界堆积。回调在内部线程或安装 handler 的调用线程执行，始终不在 channel/listener 状态锁内执行；应用必须使用适当的有序 dispatcher/有界 ingress。

Client `Close()` 允许同实例重新连接；`Dispose()` 是终态，后续 Connect 拒绝。manager 身份检查拒绝关闭后才进入的旧回调；已进入执行的回调仍须由上层 connection generation/owner ingress 隔离。发送、关闭和 start/dispose 使用同一生命周期锁。Error/Closed/Disconnected 订阅者的异常不能阻止必要的资源清理。
