# Cooking 网络异常流与断线恢复切片实施计划 (Implement)

## 实施步骤

1. **协议层扩展 (`CookingLanProtocol.cs`)**
   - 在 `HandshakeRequest`、`HandshakeAccepted` 中增加 `ReconnectToken` 支持；
   - 在 `RecipeCommand` 与 `RecipeCommandResult` 中增加 `CommandId`；
   - 更新封包序列化与反序列化逻辑。

2. **Host 端实现会话与断线保护 (`CookingLanHost.cs`)**
   - 维护 `PlayerSession` 结构，存储 `ReconnectToken`、`IsConnected` 与命令去重缓存；
   - 增加网络断开事件监听：检测到 Peer 离线时，检查玩家手持物并权威将其安全释放/置入工作台或丢弃，向其余玩家广播快照；
   - 增加命令幂等去重判断：相同 `CommandId` 直接返回已记录的响应，不二次执行；
   - 增加重连握手逻辑：根据 `ReconnectToken` 重新绑定 Peer，并下发当前最新全量快照。

3. **Client 端支持序列号与重连 (`CookingLanClient.cs`)**
   - 发送命令自增生成 `CommandId`；
   - 记录握手返回的 `ReconnectToken`；
   - 实现 `DisconnectAsync()` 与 `ReconnectAsync(...)` 接口。

4. **编写端到端验收用例并回归验证**
   - 在 `CookingReusableHarnessAcceptanceTests.cs` 中添加：
     - 重复发送相同 CommandId 的幂等性验证；
     - 客户端手持番茄断线后食材安全掉落释放验证；
     - 客户端重连后快照自动对齐与 SHA-256 共识断言；
   - 执行 `dotnet test src/AbilityKit.Game.Cooking.Tests/` 确保 100% 通过；
   - 记录 `check.jsonl`。
