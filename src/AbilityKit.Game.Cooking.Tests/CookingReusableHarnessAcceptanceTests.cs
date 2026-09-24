using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.Session;
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

    [Fact]
    public async Task Client_command_with_same_CommandId_is_idempotent()
    {
        var simulation = CreateSimulation(new[] { ChefA, ChefB });
        var descriptor = Descriptor();
        var levelScope = LevelScope();

        await using var topology = await LoopbackUdpTopology.CreateAsync(
            simulation, descriptor, levelScope, ChefA, ChefB);

        // Client 第一次拿番茄
        var firstResult = await topology.Client.SendCommandAsync(
            CookingRecipeOperation.Pickup,
            new ItemId("tomato-1"),
            explicitCommandId: 1001);

        Assert.Equal(CookingRecipeOutcome.Accepted, firstResult.Outcome);
        var versionAfterFirst = simulation.Snapshot().Version;

        // Client 重复发送相同 CommandId 的指令
        var duplicateResult = await topology.Client.SendCommandAsync(
            CookingRecipeOperation.Pickup,
            new ItemId("tomato-1"),
            explicitCommandId: 1001);

        // 验证幂等：返回与之前相同的成功结果，且权威端状态版本不重复自增
        Assert.Equal(CookingRecipeOutcome.Accepted, duplicateResult.Outcome);
        Assert.Equal(versionAfterFirst, simulation.Snapshot().Version);
    }

    [Fact]
    public async Task Client_disconnection_triggers_safe_item_drop_and_reconnection_restores_consensus()
    {
        var simulation = CreateSimulation(new[] { ChefA, ChefB });
        var descriptor = Descriptor();
        var levelScope = LevelScope();

        await using var topology = await LoopbackUdpTopology.CreateAsync(
            simulation, descriptor, levelScope, ChefA, ChefB);

        // Client 拾取番茄
        var pickupResult = await topology.Client.SendCommandAsync(
            CookingRecipeOperation.Pickup,
            new ItemId("tomato-1"));
        Assert.Equal(CookingRecipeOutcome.Accepted, pickupResult.Outcome);

        // 验证番茄在 ChefB 手上
        Assert.Equal(new ItemId("tomato-1"), simulation.ItemInHand(ChefB));

        // Client 模拟网络断开
        topology.Client.Disconnect();

        // 等待 Host 捕获断开并执行安全释放 (至多等待 2 秒)
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < TimeSpan.FromSeconds(2))
        {
            if (simulation.ItemInHand(ChefB) == null)
            {
                break;
            }
            await Task.Delay(50);
        }

        // 断言：ChefB 手上的番茄已安全卸下/掉落到可用工作台（counter-a 或 board-a）
        Assert.Null(simulation.ItemInHand(ChefB));
        var tomatoItem = simulation.Snapshot().Items.First(i => i.Id == new ItemId("tomato-1"));
        Assert.Equal(LocationKind.StationSlot, tomatoItem.Location.Kind);
        Assert.NotNull(tomatoItem.Location.SlotId);

        // Client 模拟断线重连
        var token = topology.Client.ReconnectToken;
        Assert.NotNull(token);

        await topology.Client.ReconnectAsync("127.0.0.1", topology.Host.Port);
        await topology.SyncAndDrainAsync(TimeSpan.FromSeconds(2));

        // 验证重连后快照自动对齐，双端重新达成 SHA-256 共识
        topology.AssertStateHashConsensus();
        Assert.Equal(simulation.Snapshot().Sha256(), topology.Client.LatestProjection!.Sha256());
    }

    [Fact]
    public async Task Scenario_under_LoopbackUdpTopology_synchronizes_score_and_star_consensus()
    {
        var simulation = CreateSimulation(new[] { ChefA, ChefB });
        var descriptor = Descriptor();
        var levelScope = LevelScope();

        await using var topology = await LoopbackUdpTopology.CreateAsync(
            simulation, descriptor, levelScope, ChefA, ChefB);
        var scenario = new TomatoEggSoupScenario(ChefA, ChefB);

        // 执行全流程做菜并提交订单
        await scenario.ExecuteAsync(topology);

        // 验证 Host 与 Client 状态共识
        topology.AssertStateHashConsensus();

        var hostSnap = topology.GetAuthoritySnapshot();
        var clientSnap = topology.GetClientSnapshot();

        // 验证积分与星级：提交 1 份订单获得 100 基础分，达到 1 星线（100分）
        Assert.Equal(100, hostSnap.TotalScore);
        Assert.Equal(1, hostSnap.Stars);

        Assert.NotNull(clientSnap);
        Assert.Equal(100, clientSnap.TotalScore);
        Assert.Equal(1, clientSnap.Stars);
    }

    [Fact]
    public void Score_calculator_evaluates_stars_across_score_tiers()
    {
        var thresholds = new CookingScoreThresholds(100, 250, 400);

        Assert.Equal(0, thresholds.EvaluateStars(0));
        Assert.Equal(0, thresholds.EvaluateStars(99));
        Assert.Equal(1, thresholds.EvaluateStars(100));
        Assert.Equal(1, thresholds.EvaluateStars(249));
        Assert.Equal(2, thresholds.EvaluateStars(250));
        Assert.Equal(2, thresholds.EvaluateStars(399));
        Assert.Equal(3, thresholds.EvaluateStars(400));
        Assert.Equal(3, thresholds.EvaluateStars(1000));
    }

    [Fact]
    public async Task FrontOfHouse_service_schedule_closes_naturally_and_achieves_consensus_under_LAN()
    {
        var simulation = CreateSimulation(new[] { ChefA, ChefB });
        var descriptor = Descriptor();
        var levelScope = LevelScope();

        // 配置简短的营业时间表：4 Ticks 营业期，10 Ticks 等待上限
        var schedule = new CookingFrontOfHouseSchedule(
            TableCount: 1,
            ServiceTicks: 4,
            ArrivalIntervalTicks: 2,
            InquiryTicks: 1,
            WashTicks: 2,
            DiningTicks: 3,
            WaitLimitTicks: 6);

        var frontOfHouse = new CookingFrontOfHouse(schedule);

        var host = new CookingSessionHost(
            simulation, descriptor, levelScope, ChefA, ChefB,
            frontOfHouse: frontOfHouse, activeOrderTemplate: new OrderTemplateId("tomato-egg-soup-order"));
        await host.StartAsync();

        var client = new CookingSessionClient(ChefB);
        await client.ConnectAndHandshakeAsync("127.0.0.1", host.Port);

        // 阶段 1：推进 3 ticks（营业期），顾客到店
        host.AdvanceFixedTick(3);
        await Task.Delay(50);
        Assert.False(host.LatestSnapshot.IsClosing);

        // 阶段 2：推进至营业结束（> 4 ticks），进入收尾期
        host.AdvanceFixedTick(3);
        await Task.Delay(50);
        Assert.True(host.LatestSnapshot.IsClosing);
        Assert.False(host.LatestSnapshot.IsCompleted);

        // 阶段 3：继续推进固定 Tick 直至顾客离席、前厅自然结束
        for (var i = 0; i < 20; i++)
        {
            host.AdvanceFixedTick(1);
            if (host.LatestSnapshot.IsCompleted) break;
            await Task.Delay(10);
        }

        Assert.True(host.LatestSnapshot.IsCompleted);

        // 等待客户端同步并验证哈希完全共识
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < TimeSpan.FromSeconds(2))
        {
            if (client.LatestProjection != null && client.LatestProjection.IsCompleted) break;
            await Task.Delay(20);
        }

        Assert.NotNull(client.LatestProjection);
        Assert.True(client.LatestProjection.IsCompleted);
        Assert.True(client.LatestProjection.IsClosing);
        Assert.Equal(host.LatestSnapshot.Sha256(), client.LatestProjection.Sha256());

        await client.DisposeAsync();
        await host.DisposeAsync();
    }
}
