# Cooking 生产级局域网会话与协议抽象提取设计 (Design)

## 1. 模块归属与命名空间设计

按照架构边界要求：
- `AbilityKit.Game.Cooking` 作为纯 C# 领域与会话层，不强依赖具体 Unity 或 ET；
- 会话层只依赖通用底层传输抽象或通用可靠网络传输（如 `AbilityKit.Network.Transport.LiteNet` 或 `ITransport`）。

目录划分：
- `src/AbilityKit.Game.Cooking/Session/`
  - `CookingLanProtocol.cs`：通用协议信封与编解码；
  - `CookingSessionHost.cs`：局域网权威主机运行时（支持 LiteNet UDP 绑定、断线保护、幂等去重、快照广播）；
  - `CookingSessionClient.cs`：局域网客户端运行时（支持通用 LiteNetTransport 连接、重连凭证持有、状态投影监听）。

## 2. 与测试 Harness 的关系

- `src/AbilityKit.Game.Cooking.Tests/Harness/` 下原有的 `CookingLanProtocol.cs`、`CookingLanHost.cs`、`CookingLanClient.cs` 废除/替换为对 `AbilityKit.Game.Cooking.Session` 的引用；
- `LoopbackUdpTopology.cs` 直接持有 `CookingSessionHost` 和 `CookingSessionClient`，仅保留作为测试 Actor 适配器；
- 测试用例零破坏，所有签名与断言平滑升级。
