namespace AbilityKit.Game.Cooking.Tests.Harness;

/// <summary>
/// 代表一个高层可复用的业务用例场景契约。
/// </summary>
public interface ICookingScenario
{
    string ScenarioName { get; }

    /// <summary>
    /// 在指定的拓扑环境下执行本场景用例并完成端到端闭环验证。
    /// </summary>
    Task ExecuteAsync(ICookingTestTopology topology, CancellationToken ct = default);
}
