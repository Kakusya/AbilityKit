# Cooking 局域网结算积分与星级评价切片 PRD

## 1. 目标与背景

根据 `09-19-cooking-productization-network-slice-planning/prd.md` 中的产品确认决定：
- **小关自然完成**：业务失败与网络/运行时终止严格分离。小关不设置必须达成的业务目标，也不设置业务失败条件，按营业时间或收尾规则自然完成；
- **积分与星级合同（第一版）**：
  1. 每个订单模板从配置读取固定基础分（BaseScore）；
  2. 成功提交订单时计入结算，获得该订单基础分；
  3. 未完成或超时离席的订单计 0 分，不倒扣；
  4. 本关总分等于全部已确认结算订单的基础分之和；
  5. 总分通过小关配置的分数阈值数组（如 1 星、2 星、3 星分数线）映射为 0–3 星；
  6. 0 星仍表示自然完成，不构成业务失败；
  7. 局域网下，权威端将累计积分与结算评价纳入快照/同步包下发给 Client，保持双端共识一致。

## 2. 需求范围 (Scope)

1. **配置扩展**：
   - `CookingOrderTemplateDefinition` 增加 `int BaseScore = 100`；
   - `CookingScoreThresholds`：定义 `OneStarScore`、`TwoStarScore`、`ThreeStarScore`。
2. **结算与星级评估模型**：
   - 纯函数或状态只读计算器：输入 `IReadOnlyList<CookingOrderSettlement>` 与关卡配置，输出当前总分 `TotalScore` 与达成星级 `Stars` (0~3)；
   - 纳入 `CookingRecipeSnapshot`（或专门的只读评分快照字段），确保 SHA-256 哈希计算包含积分与星级共识。
3. **局域网全链路验收**：
   - 在已提取的 `CookingSessionHost` + `CookingSessionClient` 局域网环境下，完成订单提交后，断言客户端与 Host 均正确获得对应订单得分、累加总分和计算星级，且快照哈希共识一致。
