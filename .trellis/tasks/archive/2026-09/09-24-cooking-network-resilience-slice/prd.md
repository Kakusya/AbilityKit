# Cooking 网络异常流与断线恢复切片 PRD

## 1. 目标与背景

在完成局域网基础网络纵切（`LoopbackUdpTopology`、`TomatoEggSoupScenario` 共识）后，根据 `09-19-cooking-productization-network-slice-planning` 中关于多人断线与异常处理的规划：
- 业务失败与网络/运行时终止严格分离，Client 异常断开不引起业务失败；
- 玩家断线时，权威端自动将玩家手持的物品合法安全释放（优先掉落至就近可用工作台或地面，避免物品死锁或丢失）；
- 客户端指令具备序列号（Sequence Number / CommandId）和幂等性（Idempotency），网络丢包重发或重复执行不会产生多次扣除/误切菜等副作用；
- 客户端支持断开后使用重连凭证（Reconnect Token）重新接入，重新同步最新权威快照，恢复只读投影与操作能力。

## 2. 需求范围 (Scope)

1. **断线安全物品掉落**：
   - 当 `CookingLanHost` 检测到某个 Peer/Player 发生 Disconnect 时，权威触发 `HandlePlayerDisconnected(playerId)`；
   - 若该玩家手上持有物品/容器，执行权威丢弃或安全放置逻辑，确保其不卡在玩家手中；
   - 广播最新快照给其它在线玩家。

2. **命令幂等性与去重 (Command Idempotency)**：
   - 协议命令包新增 `CommandId`（或 SequenceNumber）；
   - `CookingLanHost` 为每个玩家维护已执行的最新 `LastHandledCommandId` 或执行窗口缓存；
   - 对重复收到的历史命令直接返回已有的结果状态，不重复向 `CookingRecipeSimulation` 执行。

3. **断线重连与快照对齐 (Reconnection & Resync)**：
   - 握手协议扩充支持 `ReconnectRequest(ReconnectToken, PlayerId)`；
   - Host 校验重连凭证合法后重新绑定 Peer，并立即向 Client 推送当前权威的最新全量 `RecipeSnapshot`；
   - Client 本地投影重新对齐，双端重新恢复 SHA-256 状态哈希共识。

4. **端到端测试覆盖**：
   - 在 `CookingReusableHarnessAcceptanceTests.cs` 中增加网络异常流与断线恢复集成测试用例。
