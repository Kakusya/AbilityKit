# Cooking 前厅营业节奏与小关自然收尾切片设计 (Design)

## 1. 结构与生命周期设计

### 1.1 前厅状态在快照中的投影
- `CookingRecipeSnapshot` 增加：
  - `bool IsClosing`: 当前是否处于营业结束后的收尾阶段；
  - `bool IsCompleted`: 当前小关营业与收尾是否均已全部结束（对应 `CanSucceed` 且达到结算）。

### 1.2 Host 运行时 Tick 推进
- `CookingSessionHost.AdvanceFixedTick(int tickCount)`:
  - 推进 `_simulation.AdvanceFixedTick(...)`；
  - 若配置了 `CookingFrontOfHouse`，每 Tick 调用 `frontOfHouse.Step(simulation, activeTemplate)`；
  - 刷新快照并向局域网 Client 广播。

## 2. 自然收尾与共识测试

- 配置短营业周期 schedule（例如 10 ticks 营业，5 ticks 离席），单机与局域网均可精确验证：
  - 营业期：顾客到店并生成订单；
  - 收尾期：`IsClosing == true`，新顾客不再到店；
  - 完成期：所有顾客离席，`IsCompleted == true`，双端状态哈希一致。
