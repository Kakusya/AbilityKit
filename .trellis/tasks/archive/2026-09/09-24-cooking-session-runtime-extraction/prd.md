# Cooking 生产级局域网会话与协议抽象提取 PRD

## 1. 目标与背景

在前两轮迭代中，我们在 `AbilityKit.Game.Cooking.Tests/Harness` 下完成了局域网真实网络纵切与异常流恢复切片（`CookingLanProtocol`、`CookingLanHost`、`CookingLanClient`、`LoopbackUdpTopology`），成功验证了双端 SHA-256 共识、命令幂等与掉线恢复。
然而，这些网络与会话能力目前依然位于测试程序集内部。为了使 Cooking 成为可直接供外部上层（包括未来的 Unity 接入层或独立宿主）复用的业务模块，需要将其提炼为生产级运行时组件。

## 2. 需求范围 (Scope)

1. **协议层与 DTO 独立提取**：
   - 提取网络信封 `CookingLanEnvelope`、握手包 `CookingLanHandshakeRequest`/`Accepted`、命令包 `CookingLanRecipeCommandPacket`/`Result` 与快照包 `CookingLanSnapshotPacket`；
   - 提取跨平台标准的 `CookingLanCodec`，并保证 JSON / 二进制扩展能力。

2. **生产级 Session Host (`CookingSessionHost`)**：
   - 将原 `CookingLanHost` 提炼为通用的 `CookingSessionHost`，归属到 `AbilityKit.Game.Cooking`；
   - 负责封装 `CookingRecipeSimulation` 权威仲裁、客户端 Peer 会话管理、断线物品保护逻辑、命令幂等去重、定期/按需广播全量与增量快照。

3. **生产级 Session Client (`CookingSessionClient`)**：
   - 将原 `CookingLanClient` 提炼为通用的 `CookingSessionClient`，归属到 `AbilityKit.Game.Cooking`；
   - 封装握手、重连凭证持有、状态投影监听、基于 `TaskCompletionSource` 的异步命令提交与超时控制。

4. **测试 Harness 瘦身适配**：
   - 重构 `src/AbilityKit.Game.Cooking.Tests/Harness`：使 `LoopbackUdpTopology` 直接基于生产级 `CookingSessionHost` 和 `CookingSessionClient` 进行适配；
   - 保证既有 206 个测试用例（包括 `TomatoEggSoupScenario`、幂等性测试、断线重连测试）全部保持绿灯。
