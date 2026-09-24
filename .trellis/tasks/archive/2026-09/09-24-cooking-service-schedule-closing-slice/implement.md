# Cooking 前厅营业节奏与小关自然收尾切片实施计划 (Implement)

## 实施步骤

1. **快照与规范哈希扩展 (`CookingRecipeLoop.cs`)**
   - 在 `CookingRecipeSnapshot` 增加 `bool IsClosing` 与 `bool IsCompleted`；
   - 更新 `CanonicalSnapshot` 与 `Sha256()` 摘要；
   - 在 `CookingRecipeSimulation` 增加同步前厅状态接口 `UpdateFrontOfHouseState(bool isClosing, bool isCompleted)`。

2. **Host 端集成前厅节奏驱动 (`CookingSessionHost.cs`)**
   - 构造参数支持传入 `CookingFrontOfHouse? frontOfHouse` 与默认开单模板；
   - 在 `AdvanceFixedTick` 中循环推进 `frontOfHouse.Step`，当满足自然结束条件时自动更新快照。

3. **测试用例编写与局域网网络验证**
   - 编写多订单自动生成、顾客离席收尾与自然结束测试；
   - 验证双端局域网网络传输下 SHA-256 共识；
   - 运行全部门禁验证并记录 `check.jsonl`。
