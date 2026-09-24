# Cooking 网络异常流与断线恢复切片设计 (Design)

## 1. 协议扩展 (`CookingLanProtocol.cs`)

### 1.1 消息信封与重连字段
- `HandshakeRequest`: 增加 `string? ReconnectToken`，若非空表示请求恢复既有 Session/Player；
- `HandshakeAccepted`: 返回 `string ReconnectToken`（由 Host 分配生成 GUID），客户端保存在内存以便重连时提供；
- `RecipeCommand`: 增加 `long CommandId`（全局递增序列号）；
- `RecipeCommandResult`: 增加 `long CommandId`，以便客户端区分是针对哪次指令的响应；
- `PlayerDisconnectedNotice`: 可选的权威广播通知。

## 2. Host 端断线与幂等机制 (`CookingLanHost.cs`)

### 2.1 玩家会话与重连映射
- `PlayerSessionState`:
  - `PlayerId`: 玩家 ID
  - `ReconnectToken`: 唯一安全令牌
  - `LastPeerId`: 当前绑定的网络端点 ID
  - `IsConnected`: 是否在线
  - `ExecutedCommands`: `Dictionary<long, RecipeCommandResult>` 缓存已执行命令历史（或记录已处理最大 `CommandId`），实现幂等拦截。

### 2.2 断线物品清理 (Disconnection Cleanup)
- 当捕获 `LiteNetTransport` 触发的 `OnPeerDisconnected`：
  - 将对应 `PlayerSessionState.IsConnected = false`；
  - 查询当前 Simulation 中该玩家手持状态：若玩家手持物体，调用 `ExecuteSimulationCommand(new DropCommand(playerId))` 或权威卸下物品逻辑，放置至当前位置/地面；
  - 广播最新 `RecipeSnapshot` 给所有剩余在线 Peer。

### 2.3 重连认领 (Reconnection Handshake)
- 当收到带 `ReconnectToken` 的 `HandshakeRequest`：
  - 校验 Token 是否有效：有效则更新其 `LastPeerId`，置 `IsConnected = true`；
  - 回复 `HandshakeAccepted`；
  - 发送全量 `RecipeSnapshot` 进行强制状态同步。

## 3. Client 端断线模拟与重连 (`CookingLanClient.cs`)

- 提供 `Disconnect()` 主动断开；
- 提供 `ReconnectAsync(string reconnectToken, int playerId)` 模拟重连恢复；
- 命令发送时自动附加自增 `CommandId`。

## 4. 验证策略与测试用例

- **Idempotency Test**: 客户端向 Host 连续发送相同 `CommandId` 的同一操作（如拿碗），断言权威端只执行一次，返回幂等结果，不产生二次拿碗或状态漂移。
- **Disconnect Drop Test**: 客户端持有食材后断线，断言 Host 检测到断线后自动将食材丢下/释放，剩余快照中该食材变为可拾取，客户端重连后确认状态一致。
- **Reconnection Consensus Test**: 客户端断线期间 Host 推进模拟，客户端重连后收到全量快照，双方快照 SHA-256 再次达成共识。
