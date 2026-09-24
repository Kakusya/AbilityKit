# Cooking 局域网结算积分与星级评价切片实施计划 (Implement)

## 实施步骤

1. **订单模板与星级阈值数据结构扩充 (`CookingDomain.cs` / `CookingRecipeLoop.cs`)**
   - 给 `CookingOrderTemplateDefinition` 增加 `int BaseScore`；
   - 增加 `CookingScoreThresholds` 记录。
2. **在 Simulation 与 Snapshot 中集成评分计算**
   - 编写 `CookingScoreCalculator` 或在 `CookingRecipeLoop` 中维护累计积分与星级；
   - 在 `CookingRecipeSnapshot` 增加 `TotalScore` 与 `Stars`；
   - 更新 `CookingRecipeSnapshot.Sha256()` 规范序列化。
3. **编写评分与网络共识集成测试**
   - 单机测试：提交不同订单、超时离席订单验证不计分不扣分、不同总分下正确映射 0 星、1 星、2 星、3 星；
   - 局域网测试：通过 `LoopbackUdpTopology` 执行番茄蛋花汤提交，断言客户端与 Host 端的 `TotalScore` 和 `Stars` 实时同步且达成哈希共识。
4. **全量门禁回归验证**
   - 运行 `powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate cooking-kitchen-loop`；
   - 记录 `check.jsonl`。
