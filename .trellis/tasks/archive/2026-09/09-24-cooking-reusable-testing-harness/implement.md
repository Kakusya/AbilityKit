# Cooking 局内与网络纵切可复用测试框架实施计划 (Implement)

## 步骤清单

1. **基础契约定义**
   - 新建 `ICookingTestTopology.cs`、`ICookingActor.cs`、`CookingCommandResult.cs`。
   - 定义标准的拓扑生命周期与 Actor 交互契约。

2. **核心拓扑实现**
   - 实现 `DirectMemoryTopology.cs`：单机模拟拓扑适配器。
   - 实现 `InProcessPairTopology.cs`：基于 `InMemoryTransport` 管道的双端拓扑适配器。
   - 实现 `LoopbackLiteNetTopology.cs`：基于通用 `LiteNetTransport` 的双端网络拓扑。

3. **可复用场景用例提取**
   - 编写 `TomatoEggSoupLoopScenario.cs`，实现番茄蛋花汤全生命周期闭环。
   - 编写 `CookingReusableHarnessAcceptanceTests.cs`，以参数化 `[Theory]` 在 `DirectMemory` 与 `InProcessPair` 等拓扑下统一执行并断言状态共识。

4. **构建与门禁验证**
   - 运行 `dotnet test src/AbilityKit.Game.Cooking.Tests/AbilityKit.Game.Cooking.Tests.csproj`。
   - 确保通过并记录 `check.jsonl`。
