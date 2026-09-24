# Cooking 局域网结算积分与星级评价切片设计 (Design)

## 1. 评分领域模型设计

### 1.1 订单基础分与关卡阈值
- `CookingOrderTemplateDefinition`:
  - 增加 `int BaseScore { get; init; } = 100`；
- `CookingScoreThresholds`:
  - `(int OneStar, int TwoStar, int ThreeStar)`；
  - 校验规则：`OneStar <= TwoStar <= ThreeStar`。

### 1.2 评分计算纯函数
- `CookingScoreCalculator.Calculate(IReadOnlyList<CookingOrderSettlement> settlements, IReadOnlyDictionary<OrderTemplateId, CookingOrderTemplateDefinition> templates, CookingScoreThresholds thresholds)`:
  - 遍历有效结算列表，累加对应 Template 的 `BaseScore`；
  - 根据阈值计算当前星级 (0, 1, 2, 3)；
  - 返回 `CookingLevelScoreEvaluation(int TotalScore, int Stars)`。

## 2. 快照同步与共识

- `CookingRecipeSnapshotItem` / `CookingRecipeSnapshot` 扩展：
  - 在 `CookingRecipeSnapshot` 中增加当前结算评价字段：`int TotalScore` 与 `int Stars`；
  - `Sha256()` 规范哈希包含 `TotalScore` 与 `Stars`，保证客户端投影与权威 Host 强一致。

## 3. 局域网与网络切片集成

- `CookingRecipeSimulation` 每次提交订单生成 `CookingOrderSettlement` 时自动刷新 `Snapshot.TotalScore` 和 `Snapshot.Stars`；
- `CookingSessionHost` 将最新快照广播至 `CookingSessionClient`；
- `LoopbackUdpTopology.AssertStateHashConsensus()` 自动涵盖评分与星级的一致性断言。
