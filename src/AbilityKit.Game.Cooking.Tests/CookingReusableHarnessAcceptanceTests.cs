using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.Tests.Harness;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

/// <summary>
/// 验证可复用测试 Harness 在不同拓扑下执行相同高层场景（如番茄蛋花汤闭环）。
/// </summary>
[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingReusableHarnessAcceptanceTests
{
    private static readonly SessionId Session = new("harness-session");
    private static readonly WorldId World = new("harness-world");
    private static readonly MatchId Match = new("harness-match");
    private static readonly PlayerId ChefA = new("chef-a");
    private static readonly PlayerId ChefB = new("chef-b");

    private static readonly StationSlotId Board = new("board-a");
    private static readonly StationSlotId Stove = new("stove-a");
    private static readonly StationSlotId Oven = new("oven-a");
    private static readonly StationSlotId Counter = new("counter-a");

    private static readonly OrderId SoupOrder = new("order-soup-1");
    private static readonly OrderTemplateId SoupOrderTemplate = new("tomato-egg-soup-order");

    private static CookingLevelScope LevelScope() => new(
        new CookingScope(Session, World, Match), new RestaurantRuntimeId(1), new LevelId("harness-level"), 1);

    private static CookingSessionDescriptor Descriptor() => new(
        new CookingScope(Session, World, Match), 1, new CookingProtocolIdentity("cooking-session", 1, 1),
        "harness-config-v1", new HashSet<string>(StringComparer.Ordinal) { "cook" }, new Dictionary<string, string>());

    private static CookingRecipeSimulation CreateSimulation(IEnumerable<PlayerId> players)
    {
        var scope = new CookingScope(Session, World, Match);
        var playerMap = players.ToDictionary(
            p => p,
            p => new CookingPlayerConfig(p, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new HashSet<string>(StringComparer.Ordinal) { Board.Value, Stove.Value, Oven.Value, Counter.Value }));

        var content = CookingContentCatalog.Load(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, CookingContentCatalog.ContentFileName)));
        var simulation = new CookingRecipeSimulation(
            CookingContentCatalog.BuildFixture(content, scope, playerMap, "clean-pool"), null, null);
        CookingContentCatalog.ApplyStandardInitialSupply(simulation, content);

        // 开出番茄蛋花汤订单
        simulation.OpenOrder(SoupOrder, SoupOrderTemplate);

        return simulation;
    }

    [Fact]
    public async Task Scenario_executes_successfully_under_DirectMemoryTopology()
    {
        var simulation = CreateSimulation(new[] { ChefA });
        var descriptor = Descriptor();
        var levelScope = LevelScope();

        await using var topology = new DirectMemoryTopology(simulation, descriptor, levelScope, new[] { ChefA });
        var scenario = new TomatoEggSoupScenario(ChefA);

        await scenario.ExecuteAsync(topology);

        topology.AssertStateHashConsensus();
        Assert.Single(topology.GetAuthoritySnapshot().AcceptedOrders);
    }

    [Fact]
    public async Task Scenario_executes_successfully_and_achieves_consensus_under_InProcessPairTopology()
    {
        var simulation = CreateSimulation(new[] { ChefA, ChefB });
        var descriptor = Descriptor();
        var levelScope = LevelScope();

        await using var topology = new InProcessPairTopology(simulation, descriptor, levelScope, ChefA, ChefB);
        var scenario = new TomatoEggSoupScenario(ChefA, ChefB);

        await scenario.ExecuteAsync(topology);

        // 验证 Host 与 Client 状态严格共识
        topology.AssertStateHashConsensus();
        Assert.Single(topology.GetAuthoritySnapshot().AcceptedOrders);
        Assert.Single(topology.GetClientSnapshot().AcceptedOrders);
    }

    [Fact]
    public async Task Scenario_executes_successfully_and_achieves_consensus_under_LoopbackUdpTopology()
    {
        var simulation = CreateSimulation(new[] { ChefA, ChefB });
        var descriptor = Descriptor();
        var levelScope = LevelScope();

        await using var topology = await LoopbackUdpTopology.CreateAsync(
            simulation, descriptor, levelScope, ChefA, ChefB);
        var scenario = new TomatoEggSoupScenario(ChefA, ChefB);

        await scenario.ExecuteAsync(topology);

        // 验证真实回环 UDP 传输下双端状态哈希一致
        topology.AssertStateHashConsensus();
        Assert.Single(topology.GetAuthoritySnapshot().AcceptedOrders);
        Assert.Single(topology.GetClientSnapshot()!.AcceptedOrders);
    }
}
