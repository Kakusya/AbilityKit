using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

/// <summary>
/// 任务 <c>09-21-cooking-et-closed-loop-acceptance</c>：ET fixed-tick Level 宿主承载同一条番茄蛋花汤闭环验收。
/// 与领域侧 <c>CookingKitchenLoopFixtureTests</c> L01 同构：fixture 与标准初始供应全部来自正式内容目录
/// （<c>cooking-definition-v2</c>），玩家动作只经宿主命令 ingress（<c>TryEnqueue</c>）与固定 Tick 推进，
/// 开单与清洗完成是帧间注入（前厅/NPC 缝隙，不是玩家命令）。宿主拥有时钟：<c>AdvanceTicks</c> 在 admission
/// 即被保留拒绝，加工只能逐帧完成，因此本验收结束于"命令帧 + 时钟帧"总数的 LogicalTick，与领域运行的
/// 计数器不同（task design.md §6 显式记录该口径）。
/// </summary>
[Trait("Gate", "CookingLevelRuntime")]
public sealed class CookingLevelClosedLoopTests
{
    private static readonly SessionId Session = new("loop-session");
    private static readonly WorldId World = new("loop-world");
    private static readonly MatchId Match = new("loop-match");
    private static readonly PlayerId Player = new("chef-a");
    private static readonly StationSlotId Board = new("board-a");
    private static readonly StationSlotId Stove = new("stove-a");
    private static readonly StationSlotId Oven = new("oven-a");
    private static readonly StationSlotId Counter = new("counter-a");
    private static readonly DefinitionId ChoppedTomato = new("chopped-tomato");
    private static readonly DefinitionId BeatenEgg = new("beaten-egg");
    private static readonly DefinitionId Soup = new("tomato-egg-soup");
    private static readonly DefinitionId ToastedBread = new("toasted-bread");
    private static readonly DefinitionId BowlDefinition = new("bowl");
    private static readonly DefinitionId PotDefinition = new("pot");
    private static readonly RecipeId ChopRecipe = new("chop-tomato");
    private static readonly RecipeId BeatRecipe = new("beat-egg");
    private static readonly RecipeId SoupRecipe = new("tomato-egg-soup");
    private static readonly RecipeId BakeRecipe = new("bake-bread");
    private static readonly OrderTemplateId SoupOrderTemplate = new("tomato-egg-soup-order");
    private static readonly OrderId SoupOrder = new("order-soup-1");
    private static readonly ItemId Pot = new("pot-1");
    private static readonly ItemId Bowl = new("pool-bowl-1");
    private const string CleanPoolLocation = "clean-pool";
    private const int ChopTicks = 2;
    private const int BeatTicks = 2;
    private const int SoupTicks = 6;
    private const int BakeTicks = 2;
    private const string Runner = "dotnet test AbilityKit.ET.Runtime.Tests";

    [Fact]
    public void E01_full_tomato_egg_soup_loop_through_the_et_level_host()
    {
        using var evidence = CreateEvidence("E01");
        using var fixture = RunLoop(evidence, "E01");
        var simulation = fixture.Simulation;

        Assert.False(string.IsNullOrWhiteSpace(simulation.Snapshot().Sha256()));

        // 宿主拥有时钟：AdvanceTicks 在 admission 即被保留拒绝，不进仿真命令账本。
        var reserved = fixture.Host.TryEnqueue(new CookingLevelCommandEnvelope(fixture.LevelScope,
            new CookingRecipeCommand(fixture.LevelScope.MatchScope, fixture.NextBatch(), Player,
                new RecipeCommandId("reserved-advance"), CookingRecipeOperation.AdvanceTicks,
                null, new ProcessId("reserved-clock-probe"), null, null, null, null, 0, 1),
            "loop-connection", "reserved-advance"));
        Assert.False(reserved.Accepted);
        Assert.Equal(CookingLevelAdmissionReason.ReservedClockOperation, reserved.Reason);
        Assert.Equal(0, fixture.Host.PendingCommandIdentityCount);

        AssertEvidence(evidence.Path, "E01", 17);
    }

    [Fact]
    public void E02_order_requirement_rejects_a_foreign_product_at_the_host_boundary()
    {
        using var evidence = CreateEvidence("E02");

        // 拒绝臂：开单后提交烤面包，被订单要求拒绝；落 evidence。
        var rejected = RunBreadArm(submit: true, evidence, "E02");
        // 对照臂：同一序列，最后一帧只推进时钟、不发命令。ET 宿主是进程级单例，两臂顺序执行。
        var control = RunBreadArm(submit: false, null, null);

        // 被拒绝的命令帧与纯时钟帧到达同一 canonical 状态：拒绝对领域零变更
        // （每帧固有的时钟推进在两臂相同，已由对照臂抵消）。
        Assert.Equal(control, rejected);
        AssertEvidence(evidence.Path, "E02", 8);
    }

    [Fact]
    public void F01_failed_retry_rebuilds_the_standard_supply_and_stays_created()
    {
        var content = LoadContent();
        var fixture = CreateFixture(content);
        using var host = fixture.CreateStartedHost(state =>
            CookingContentCatalog.ApplyStandardInitialSupply(state, content));
        var tomato = ItemState(fixture.Simulation, new ItemId("tomato-1"));
        Assert.True(host.TryEnqueue(new CookingLevelCommandEnvelope(fixture.LevelScope,
            new CookingRecipeCommand(fixture.LevelScope.MatchScope, fixture.NextBatch(), Player,
                new RecipeCommandId("move-tomato"), CookingRecipeOperation.Pickup,
                null, null, tomato.Id, null, null, null, tomato.Version, 0),
            "loop-connection", "move-tomato")).Accepted);
        Assert.True(host.Tick().Accepted);
        Assert.True(fixture.Simulation.OpenOrder(SoupOrder, SoupOrderTemplate).Accepted);
        var frameAfterPlay = host.HostFrameSequence;
        var failedKitchen = fixture.Simulation.ExportCheckpoint().CanonicalText();
        var reference = ReferenceSupply(content, fixture.LevelScope.MatchScope);

        Assert.True(host.BeginEnd(CookingLevelOutcome.Failed).Accepted);
        Assert.True(host.CompleteEnd().Accepted);
        var retry = host.CreateRetry(2, content);

        Assert.True(retry.Accepted, retry.Reason);
        Assert.Equal(fixture.LevelScope.Level, retry.NewScope!.Level);
        Assert.Equal(2, retry.NewScope.LevelEpoch);
        Assert.Equal(fixture.LevelScope.MatchScope, retry.NewScope.MatchScope);
        Assert.Equal(CookingLevelState.Created, host.Lifecycle.State);
        Assert.True(host.HostFrameSequence >= frameAfterPlay);
        Assert.True(host.TryPeekBoundKitchen(out var rebuilt));
        var rebuiltCheckpoint = rebuilt!.ExportCheckpoint();
        Assert.NotEqual(failedKitchen, rebuiltCheckpoint.CanonicalText());
        Assert.Equal(reference, rebuiltCheckpoint.CanonicalText());
        Assert.Empty(rebuiltCheckpoint.Orders);
        Assert.Empty(rebuiltCheckpoint.Processes);
        Assert.Empty(rebuiltCheckpoint.Settlements);
        Assert.Equal(0, rebuiltCheckpoint.LogicalTick);
        Assert.Equal(CookingLevelFrameReason.LevelNotRunning, host.Tick().Reason);
        Assert.Equal(CookingLevelAdmissionReason.LevelNotRunning, host.TryEnqueue(
            new CookingLevelCommandEnvelope(retry.NewScope, new CookingRecipeCommand(
                retry.NewScope.MatchScope, 1, Player, new RecipeCommandId("early"),
                CookingRecipeOperation.Pickup, null, null, new ItemId("tomato-1"), null, null, null, 1, 0),
            "loop-connection", "early")).Reason);

        Assert.True(host.Prepare(fixture.Preparation).Accepted);
        Assert.True(host.Start().Accepted);
        Assert.True(host.Lifecycle.TryGetGameplay(out var started));
        var startedCheckpoint = started.ExportCheckpoint();
        Assert.Equal(reference, (startedCheckpoint with { LevelScope = null }).CanonicalText());
        Assert.Equal(host.Lifecycle.Scope, startedCheckpoint.LevelScope);
        Assert.DoesNotContain(started.ExportCheckpoint().Items, item => item.Location.Kind == LocationKind.PlayerHand);
        Assert.True(host.Tick().Accepted);
    }

    [Fact]
    public void S03_only_a_successful_level_confirms_its_settlements()
    {
        var content = LoadContent();
        var failed = CreateFixture(content);
        using var failedHost = failed.CreateStartedHost(state =>
            CookingContentCatalog.ApplyStandardInitialSupply(state, content));
        Assert.True(failed.Simulation.OpenOrder(SoupOrder, SoupOrderTemplate).Accepted);
        Assert.True(failedHost.BeginEnd(CookingLevelOutcome.Failed).Accepted);
        Assert.True(failedHost.CompleteEnd().Accepted);
        var ledger = new CookingLevelSettlementLedger();
        var rejected = failedHost.ConfirmSettlements(ledger);
        Assert.Equal(CookingLevelSettlementConfirmationDisposition.Rejected, rejected.Disposition);
        Assert.False(ledger.TryRead(failed.LevelScope, out _));
        failedHost.Dispose();

        var succeeded = CreateFixture(content);
        using var host = succeeded.CreateStartedHost(state =>
            CookingContentCatalog.ApplyStandardInitialSupply(state, content));
        Assert.True(host.BeginEnd(CookingLevelOutcome.Success).Accepted);
        Assert.True(host.CompleteEnd().Accepted);
        var beforeHandoff = succeeded.Simulation.SettlementHistory.ToArray();
        var confirmed = host.ConfirmSettlements(ledger);
        Assert.Equal(beforeHandoff, confirmed.Confirmation!.Settlements);
        var duplicate = host.ConfirmSettlements(ledger);
        Assert.Equal(CookingLevelSettlementConfirmationDisposition.Confirmed, confirmed.Disposition);
        Assert.Empty(confirmed.Confirmation!.Settlements);
        Assert.Equal(CookingLevelSettlementConfirmationDisposition.Duplicate, duplicate.Disposition);

        Assert.True(host.CreateSuccessor(new LevelId("level-2"), 2).Accepted);
        var afterHandoff = host.ConfirmSettlements(ledger);
        Assert.Equal(CookingLevelSettlementConfirmationDisposition.Rejected, afterHandoff.Disposition);
        Assert.True(ledger.TryRead(succeeded.LevelScope, out var stored));
        Assert.Empty(stored!.Settlements);
    }

    [Fact]
    public void D04_the_host_stores_the_success_list_and_rejects_a_failed_level()
    {
        var content = LoadContent();
        using var directory = new TempSettlementDirectory();
        var failed = CreateFixture(content);
        using var failedHost = failed.CreateStartedHost(state =>
            CookingContentCatalog.ApplyStandardInitialSupply(state, content));
        Assert.True(failedHost.BeginEnd(CookingLevelOutcome.Failed).Accepted);
        Assert.True(failedHost.CompleteEnd().Accepted);
        var ledger = new CookingLevelSettlementLedger();
        var store = new CookingLevelSettlementStore(directory.Path);
        var rejected = failedHost.StoreSettlements(ledger, store);
        Assert.Equal(CookingLevelSettlementStoreReason.InvalidState, rejected.Reason);
        Assert.False(ledger.TryRead(failed.LevelScope, out _));
        Assert.Equal(CookingLevelSettlementStoreReason.Missing, store.Read(failed.LevelScope).Reason);
        failedHost.Dispose();

        var succeeded = CreateFixture(content);
        using var host = succeeded.CreateStartedHost(state =>
            CookingContentCatalog.ApplyStandardInitialSupply(state, content));
        Assert.True(succeeded.Simulation.OpenOrder(SoupOrder, SoupOrderTemplate).Accepted);
        Assert.True(host.BeginEnd(CookingLevelOutcome.Success).Accepted);
        Assert.True(host.CompleteEnd().Accepted);
        var beforeHandoff = succeeded.Simulation.SettlementHistory.ToArray();
        var storedOnDisk = host.StoreSettlements(ledger, store);
        Assert.Equal(beforeHandoff, storedOnDisk.Confirmation!.Settlements);
        Assert.True(host.CreateSuccessor(new LevelId("level-2"), 2).Accepted);
        var afterHandoff = host.StoreSettlements(ledger, store);
        Assert.Equal(CookingLevelSettlementStoreReason.InvalidState, afterHandoff.Reason);

        var reread = new CookingLevelSettlementStore(directory.Path).Read(succeeded.LevelScope);
        Assert.Equal(beforeHandoff, reread.Confirmation!.Settlements);
    }

    [Fact]
    public void P04_decoration_moves_an_unfinished_process_and_only_success_writes_the_checkpoint()
    {
        var content = LoadContent();
        var fixture = CreateFixture(content);
        using var host = fixture.CreateStartedHost(state =>
            CookingContentCatalog.ApplyStandardInitialSupply(state, content));
        var progress = new CookingMajorProgress();
        Assert.Equal(CookingMajorProgressReason.InvalidState,
            host.ChooseDecoration(progress, new[]
            {
                new CookingStationReplacement(Stove, new StationSlotId("stove-b")),
            }).Reason);

        Assert.True(host.BeginEnd(CookingLevelOutcome.Failed).Accepted);
        Assert.True(host.CompleteEnd().Accepted);
        using var directory = new TempSettlementDirectory();
        var checkpoints = new CookingMajorCheckpointStore(directory.Path);
        progress.Lock();
        Assert.Equal(CookingMajorProgressReason.InvalidState, host.WriteMajorCheckpoint(progress, checkpoints).Reason);
        Assert.Equal(CookingMajorProgressReason.Missing, checkpoints.Read(fixture.LevelScope.MatchScope).Reason);
        host.Dispose();

        var succeeded = CreateFixture(content);
        using var success = succeeded.CreateStartedHost(state =>
            CookingContentCatalog.ApplyStandardInitialSupply(state, content));
        Assert.True(success.BeginEnd(CookingLevelOutcome.Success).Accepted);
        Assert.True(success.CompleteEnd().Accepted);
        Assert.True(success.CreateSuccessor(new LevelId("level-2"), 2).Accepted);
        var next = new CookingMajorProgress();
        Assert.True(next.EnableCookFaster().Accepted);
        next.Lock();
        Assert.True(success.WriteMajorCheckpoint(next, checkpoints).Accepted);
        var read = new CookingMajorCheckpointStore(directory.Path).Read(succeeded.LevelScope.MatchScope);
        Assert.True(read.Progress!.CookFaster);
        Assert.False(string.IsNullOrWhiteSpace(read.KitchenCanonical));
    }

    [Fact]
    public void F06_a_running_frame_opens_an_order_and_a_paused_frame_does_not()
    {
        var content = LoadContent();
        var fixture = CreateFixture(content);
        using var host = fixture.CreateStartedHost(state =>
            CookingContentCatalog.ApplyStandardInitialSupply(state, content));
        var house = new CookingFrontOfHouse(new CookingFrontOfHouseSchedule(1, 8, 2, 2, 2, 1, 9));
        host.UseFrontOfHouse(house, SoupOrderTemplate);

        Assert.True(host.Tick().Accepted);
        Assert.Empty(fixture.Simulation.Orders);
        Assert.True(host.Tick().Accepted);
        Assert.True(host.Tick().Accepted);
        Assert.Single(fixture.Simulation.Orders);

        Assert.False(host.TryFinishService().Accepted);
        Assert.Equal(CookingLevelState.Running, host.Lifecycle.State);
        Assert.True(host.Pause().Accepted);
        var paused = host.Tick();
        Assert.False(paused.Accepted);
        Assert.Single(fixture.Simulation.Orders);
    }

    [Fact]
    public void G02_service_finishes_only_after_the_seat_is_empty()
    {
        var content = LoadContent();
        var bare = CreateFixture(content);
        using var withoutHouse = bare.CreateStartedHost(state =>
            CookingContentCatalog.ApplyStandardInitialSupply(state, content));
        Assert.False(withoutHouse.TryFinishService().Accepted);
        Assert.Equal(CookingLevelState.Running, withoutHouse.Lifecycle.State);
        withoutHouse.Dispose();

        var fixture = CreateFixture(content);
        using var host = fixture.CreateStartedHost(state =>
            CookingContentCatalog.ApplyStandardInitialSupply(state, content));
        var house = new CookingFrontOfHouse(new CookingFrontOfHouseSchedule(1, 1, 2, 1, 2, 2, 9));
        host.UseFrontOfHouse(house, SoupOrderTemplate);
        Assert.True(host.Tick().Accepted);
        Assert.False(host.TryFinishService().Accepted);
        Assert.True(host.Tick().Accepted);
        Assert.Single(fixture.Simulation.Orders);

        fixture.Simulation.MarkOrderCompletedForTest(new OrderId("table-1-order"));
        Assert.True(host.Tick().Accepted);
        Assert.False(host.TryFinishService().Accepted);
        Assert.True(host.Tick().Accepted);
        Assert.True(host.Tick().Accepted);
        var finished = host.TryFinishService();
        Assert.True(finished.Accepted);
        Assert.Equal(CookingLevelState.Ending, host.Lifecycle.State);
    }

    [Fact]
    public void N02_the_next_level_can_seat_a_new_guest()
    {
        var content = LoadContent();
        var fixture = CreateFixture(content);
        using var host = fixture.CreateStartedHost(state =>
            CookingContentCatalog.ApplyStandardInitialSupply(state, content));
        var house = new CookingFrontOfHouse(new CookingFrontOfHouseSchedule(1, 4, 2, 1, 2, 1, 9));
        host.UseFrontOfHouse(house, SoupOrderTemplate);
        Assert.True(host.Tick().Accepted);
        Assert.True(host.Tick().Accepted);
        Assert.True(host.Tick().Accepted);
        Assert.True(host.Tick().Accepted);
        Assert.True(house.IsClosing);
        Assert.True(host.BeginEnd(CookingLevelOutcome.Success).Accepted);
        Assert.True(host.CompleteEnd().Accepted);
        Assert.True(host.CreateSuccessor(new LevelId("level-2"), 2).Accepted);

        Assert.False(house.IsClosing);
        Assert.Empty(house.UnsatisfiedOrders);
        Assert.Equal(CookingLevelState.Created, host.Lifecycle.State);
        Assert.True(host.Prepare(fixture.Preparation with { Level = new LevelId("level-2") }).Accepted);
        Assert.True(host.Start().Accepted);
        Assert.True(host.Tick().Accepted);
        Assert.Equal(1, house.SeatedCount);
        Assert.False(house.IsClosing);
    }

    [Fact]
    public void H07_an_inquiry_in_progress_opens_before_the_handoff_clears_orders()
    {
        var content = LoadContent();
        var fixture = CreateFixture(content);
        using var host = fixture.CreateStartedHost(state =>
            CookingContentCatalog.ApplyStandardInitialSupply(state, content));
        var house = new CookingFrontOfHouse(new CookingFrontOfHouseSchedule(1, 8, 2, 4, 2, 1, 30));
        host.UseFrontOfHouse(house, SoupOrderTemplate);
        Assert.True(host.Tick().Accepted);
        Assert.True(host.Tick().Accepted);
        Assert.Empty(fixture.Simulation.Orders);

        Assert.True(host.BeginEnd(CookingLevelOutcome.Success).Accepted);
        Assert.True(host.CompleteEnd().Accepted);
        var successor = host.CreateSuccessor(new LevelId("level-2"), 2);
        Assert.True(successor.Accepted);
        Assert.Equal(1, successor.ClearedOrderCount);
        Assert.Empty(fixture.Simulation.Orders);
        Assert.Equal(0, house.SeatedCount);
    }

    [Fact]
    public void Q02_a_failed_retry_starts_the_front_of_house_again()
    {
        var content = LoadContent();
        var fixture = CreateFixture(content);
        using var host = fixture.CreateStartedHost(state =>
            CookingContentCatalog.ApplyStandardInitialSupply(state, content));
        var house = new CookingFrontOfHouse(new CookingFrontOfHouseSchedule(1, 4, 2, 1, 2, 1, 9));
        host.UseFrontOfHouse(house, SoupOrderTemplate);
        Assert.True(host.Tick().Accepted);
        Assert.True(host.Tick().Accepted);
        Assert.True(host.Tick().Accepted);
        Assert.True(host.Tick().Accepted);
        Assert.True(house.IsClosing);
        Assert.True(host.BeginEnd(CookingLevelOutcome.Failed).Accepted);
        Assert.True(host.CompleteEnd().Accepted);
        Assert.True(host.CreateRetry(2, content).Accepted);

        Assert.False(house.IsClosing);
        Assert.Empty(house.UnsatisfiedOrders);
        Assert.Empty(fixture.Simulation.Orders);
        Assert.Equal(CookingLevelState.Created, host.Lifecycle.State);
        Assert.True(host.Prepare(fixture.Preparation).Accepted);
        Assert.True(host.Start().Accepted);
        Assert.True(host.Tick().Accepted);
        Assert.Equal(1, house.SeatedCount);
        Assert.False(house.IsClosing);
    }

    [Fact]
    public void Q03_a_failed_retry_keeps_the_unlock_and_the_faster_cook()
    {
        var content = LoadContent();
        var fixture = CreateFixture(content);
        using var host = fixture.CreateStartedHost(state =>
            CookingContentCatalog.ApplyStandardInitialSupply(state, content));
        var house = new CookingFrontOfHouse(new CookingFrontOfHouseSchedule(1, 4, 2, 1, 2, 1, 9));
        host.UseFrontOfHouse(house, SoupOrderTemplate);
        var tomato = ItemState(fixture.Simulation, new ItemId("tomato-1"));
        Assert.True(host.TryEnqueue(new CookingLevelCommandEnvelope(fixture.LevelScope,
            new CookingRecipeCommand(fixture.LevelScope.MatchScope, fixture.NextBatch(), Player,
                new RecipeCommandId("move-tomato"), CookingRecipeOperation.Pickup,
                null, null, tomato.Id, null, null, null, tomato.Version, 0),
            "loop-connection", "move-tomato")).Accepted);
        Assert.True(host.Tick().Accepted);
        Assert.True(fixture.Simulation.OpenOrder(SoupOrder, SoupOrderTemplate).Accepted);
        Assert.True(host.BeginEnd(CookingLevelOutcome.Failed).Accepted);
        Assert.True(host.CompleteEnd().Accepted);
        using var directory = new TempSettlementDirectory();
        var checkpoints = new CookingMajorCheckpointStore(directory.Path);
        var progress = new CookingMajorProgress();
        Assert.True(progress.Unlock(new DefinitionId("bread-slice")).Accepted);
        Assert.True(progress.EnableCookFaster().Accepted);

        Assert.True(host.CreateRetry(2, content, progress).Accepted);

        Assert.True(host.TryPeekBoundKitchen(out var rebuilt));
        Assert.Empty(rebuilt!.Orders);
        Assert.Empty(rebuilt.ExportCheckpoint().Processes);
        Assert.Contains(rebuilt.Snapshot().Items, item => item.Id == new ItemId("tomato-1") && item.Location.Kind == LocationKind.WorldPosition);
        Assert.Contains(rebuilt.Snapshot().Items, item => item.Id == new ItemId("bread-slice-unlock-1"));
        Assert.Equal(0, house.SeatedCount);
        Assert.Empty(house.UnsatisfiedOrders);
        Assert.Equal(CookingMajorProgressReason.Missing, checkpoints.Read(fixture.LevelScope.MatchScope).Reason);
        Assert.True(progress.ChooseDecoration(new[]
        {
            new CookingStationReplacement(Stove, Counter),
        }).Accepted);
        progress.Lock();
        Assert.Equal(CookingMajorProgressReason.InvalidState, progress.EnableCookFaster().Reason);
        Assert.Equal(3, progress.CookTicks(SoupRecipe, SoupTicks));
    }

    [Fact]
    public void Q04_an_unknown_retry_choice_leaves_the_failed_kitchen()
    {
        var content = LoadContent();
        var fixture = CreateFixture(content);
        using var host = fixture.CreateStartedHost(state =>
            CookingContentCatalog.ApplyStandardInitialSupply(state, content));
        Assert.True(fixture.Simulation.OpenOrder(SoupOrder, SoupOrderTemplate).Accepted);
        Assert.True(host.BeginEnd(CookingLevelOutcome.Failed).Accepted);
        Assert.True(host.CompleteEnd().Accepted);
        Assert.Contains(fixture.Simulation.Orders, order => order.Id == SoupOrder);
        var progress = new CookingMajorProgress();
        Assert.True(progress.ChooseDecoration(new[]
        {
            new CookingStationReplacement(Stove, new StationSlotId("stove-b")),
        }).Accepted);

        var retry = host.CreateRetry(2, content, progress);

        Assert.False(retry.Accepted);
        Assert.Equal(CookingLevelState.Ended, host.Lifecycle.State);
        Assert.Equal(fixture.LevelScope, host.Lifecycle.Scope);
        Assert.True(host.TryPeekBoundKitchen(out var failedKitchen));
        Assert.Contains(failedKitchen!.Orders, order => order.Id == SoupOrder);
        Assert.False(progress.Locked);
    }

    private sealed class TempSettlementDirectory : IDisposable
    {
        public TempSettlementDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cooking-host-settlement-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }

    private static string ReferenceSupply(CookingContent content, CookingScope scope)
    {
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [Player] = new(Player, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new HashSet<string>(StringComparer.Ordinal) { Board.Value, Stove.Value, Oven.Value, Counter.Value }),
        };
        var simulation = new CookingRecipeSimulation(
            CookingContentCatalog.BuildFixture(content, scope, players, CleanPoolLocation));
        CookingContentCatalog.ApplyStandardInitialSupply(simulation, content);
        return simulation.ExportCheckpoint().CanonicalText();
    }

    /// <summary>
    /// 烤面包臂：标准初始供应 → 碗上台面 → 烤箱烤面包 → 入碗 → 前厅开单；
    /// <paramref name="submit"/> 为 true 时提交烤面包（被蛋花汤订单要求拒绝），否则只推进一个时钟帧。
    /// 返回该臂结束时的 canonical 文本；宿主随 using 在方法内释放。
    /// </summary>
    private static string RunBreadArm(bool submit, EvidenceScope? evidence, string? testId)
    {
        var content = LoadContent();
        var fixture = CreateFixture(content);
        using var host = fixture.CreateStartedHost(state =>
            CookingContentCatalog.ApplyStandardInitialSupply(state, content));
        var simulation = fixture.Simulation;

        // 同一开场：干净池的碗上台面。
        Execute(fixture, evidence, testId, "pickup-pool-bowl", CookingRecipeOperation.Pickup,
            item: Bowl, expectedVersion: ItemState(simulation, Bowl).Version,
            summary: "pick up a clean bowl from the pool");
        Execute(fixture, evidence, testId, "drop-bowl", CookingRecipeOperation.Drop,
            item: Bowl, station: Counter, expectedVersion: ItemState(simulation, Bowl).Version,
            summary: "put the bowl on the counter");

        // 正式内容的烤面包配方：面包片（标准初始供应）放入烤箱，消耗输入并在烤箱生成烤面包。
        var slice = ItemState(simulation, new ItemId("bread-slice-1"));
        Execute(fixture, evidence, testId, "pickup-slice", CookingRecipeOperation.Pickup,
            item: slice.Id, expectedVersion: slice.Version,
            summary: "pick up the bread slice from the standard supply");
        Execute(fixture, evidence, testId, "drop-slice", CookingRecipeOperation.Drop,
            item: slice.Id, station: Oven, expectedVersion: ItemState(simulation, slice.Id).Version,
            summary: "drop the bread slice onto the oven");
        Execute(fixture, evidence, testId, "bake-start", CookingRecipeOperation.StartProcess,
            recipe: BakeRecipe, item: slice.Id, station: Oven,
            expectedVersion: ItemState(simulation, slice.Id).Version,
            summary: "the oven bakes with a single input");
        AdvanceClock(fixture, BakeTicks);
        var bread = Assert.Single(simulation.Snapshot().Items, item => item.IsProduct);
        Assert.Equal(ToastedBread, bread.Definition);
        Assert.Equal(ItemLocation.Station(Oven), bread.Location);

        Execute(fixture, evidence, testId, "pickup-bread", CookingRecipeOperation.Pickup,
            item: bread.Id, expectedVersion: bread.Version, summary: "pick up the toasted bread");
        Execute(fixture, evidence, testId, "bread-into-bowl", CookingRecipeOperation.PutIn,
            item: bread.Id, container: Bowl, expectedVersion: ItemState(simulation, bread.Id).Version,
            summary: "put the toasted bread into the bowl");

        // 订单簿要求番茄蛋花汤：开单后提交烤面包被领域拒绝，已装盘菜品不回滚。
        Assert.True(simulation.OpenOrder(SoupOrder, SoupOrderTemplate).Accepted);
        if (submit)
        {
            Execute(fixture, evidence, testId, "submit-bread", CookingRecipeOperation.SubmitOrder,
                item: bread.Id, order: SoupOrder, expectedVersion: ItemState(simulation, bread.Id).Version,
                summary: "the order requires the soup recipe, so the toasted-bread submission is rejected",
                expectedRejection: CookingRecipeRejectionReason.OrderRequirementMismatch);
            Assert.Empty(simulation.SettlementHistory);
            Assert.Empty(fixture.WashPort.Requests);
            Assert.Equal(new[] { bread.Id }, simulation.ItemsInContainer(Bowl));
            Assert.Equal(CookingOrderStatus.Open.ToString(),
                simulation.Snapshot().Orders.Single(order => order.Id == SoupOrder).Status);
        }
        else
        {
            AdvanceClock(fixture, 1);
            Assert.Empty(simulation.SettlementHistory);
            Assert.Empty(fixture.WashPort.Requests);
        }

        return simulation.Snapshot().CanonicalText();
    }

    [Fact]
    public void E03_the_same_host_loop_reaches_the_same_canonical_state_when_replayed()
    {
        // ET 宿主是进程级单例：两遍顺序执行，各自释放后再比较。
        string firstCanonical;
        string firstHash;
        using (var first = RunLoop(null, null))
        {
            firstCanonical = first.Simulation.Snapshot().CanonicalText();
            firstHash = first.Simulation.Snapshot().Sha256();
        }

        string secondCanonical;
        string secondHash;
        using (var second = RunLoop(null, null))
        {
            secondCanonical = second.Simulation.Snapshot().CanonicalText();
            secondHash = second.Simulation.Snapshot().Sha256();
        }

        Assert.Equal(firstCanonical, secondCanonical);
        Assert.Equal(firstHash, secondHash);
    }

    /// <summary>
    /// 完整闭环：标准初始供应 → 碗上台面 → 切番茄 → 入锅 → 打蛋 → 倒蛋液 → 煮制 → 端走 → 倒汤 →
    /// 开单 → 提交 → 洗碗回池。宿主保持运行，由调用方释放；循环中途失败时在此释放宿主，
    /// 避免 ET 宿主（进程级单例）泄漏进同进程的后续测试。
    /// </summary>
    private static Fixture RunLoop(EvidenceScope? evidence, string? testId)
    {
        var content = LoadContent();
        var fixture = CreateFixture(content);
        try
        {
            RunStartedLoop(fixture, content, evidence, testId);
            return fixture;
        }
        catch
        {
            fixture.Dispose();
            throw;
        }
    }

    private static void RunStartedLoop(
        Fixture fixture,
        CookingContent content,
        EvidenceScope? evidence,
        string? testId)
    {
        fixture.CreateStartedHost(state => CookingContentCatalog.ApplyStandardInitialSupply(state, content));
        var simulation = fixture.Simulation;

        // 标准初始供应可观察：锅落灶台、干净碗池 2 只。
        Assert.Equal(ItemLocation.Station(Stove), ItemState(simulation, Pot).Location);
        Assert.Equal(2, simulation.CleanContainerCount(BowlDefinition));

        // 干净池的碗上台面。
        Execute(fixture, evidence, testId, "pickup-pool-bowl", CookingRecipeOperation.Pickup,
            item: Bowl, expectedVersion: ItemState(simulation, Bowl).Version,
            summary: "pick up a clean bowl from the pool");
        Execute(fixture, evidence, testId, "drop-bowl", CookingRecipeOperation.Drop,
            item: Bowl, station: Counter, expectedVersion: ItemState(simulation, Bowl).Version,
            summary: "put the bowl on the counter for reuse");

        // 番茄 -> 砧板 -> 切（ConsumeInputs：消耗输入、在工位生成番茄块）。
        Execute(fixture, evidence, testId, "pickup-tomato", CookingRecipeOperation.Pickup,
            item: new ItemId("tomato-1"), expectedVersion: ItemState(simulation, new ItemId("tomato-1")).Version,
            summary: "pick up the tomato from the pantry");
        Execute(fixture, evidence, testId, "drop-tomato", CookingRecipeOperation.Drop,
            item: new ItemId("tomato-1"), station: Board,
            expectedVersion: ItemState(simulation, new ItemId("tomato-1")).Version,
            summary: "drop the tomato onto the board");
        Execute(fixture, evidence, testId, "start-chop", CookingRecipeOperation.StartProcess,
            recipe: ChopRecipe, item: new ItemId("tomato-1"), station: Board,
            expectedVersion: ItemState(simulation, new ItemId("tomato-1")).Version,
            summary: "start chopping on the board");
        AdvanceClock(fixture, ChopTicks);
        var chopped = Assert.Single(simulation.Snapshot().Items, item => item.IsProduct);
        Assert.Equal(ChoppedTomato, chopped.Definition);
        Assert.Equal(ItemLocation.Station(Board), chopped.Location);
        Assert.Empty(simulation.Snapshot().Processes);

        // 番茄块入锅。
        Execute(fixture, evidence, testId, "pickup-chopped", CookingRecipeOperation.Pickup,
            item: chopped.Id, expectedVersion: chopped.Version, summary: "pick up the chopped tomato");
        Execute(fixture, evidence, testId, "chopped-into-pot", CookingRecipeOperation.PutIn,
            item: chopped.Id, container: Pot, expectedVersion: ItemState(simulation, chopped.Id).Version,
            summary: "put the chopped tomato into the pot");

        // 鸡蛋入碗 -> 手持碗免工位打蛋（ConsumeInputs：蛋液落碗）。
        Execute(fixture, evidence, testId, "pickup-egg", CookingRecipeOperation.Pickup,
            item: new ItemId("egg-1"), expectedVersion: ItemState(simulation, new ItemId("egg-1")).Version,
            summary: "pick up the egg from the pantry");
        Execute(fixture, evidence, testId, "egg-into-bowl", CookingRecipeOperation.PutIn,
            item: new ItemId("egg-1"), container: Bowl,
            expectedVersion: ItemState(simulation, new ItemId("egg-1")).Version,
            summary: "put the egg into the bowl");
        Execute(fixture, evidence, testId, "pickup-bowl", CookingRecipeOperation.Pickup,
            item: Bowl, expectedVersion: ItemState(simulation, Bowl).Version,
            summary: "pick up the bowl holding the egg");
        Execute(fixture, evidence, testId, "beat-start", CookingRecipeOperation.StartProcess,
            recipe: BeatRecipe, item: Bowl, expectedVersion: ItemState(simulation, Bowl).Version,
            summary: "beating eggs needs no station");
        AdvanceClock(fixture, BeatTicks);
        var liquid = Assert.Single(simulation.Snapshot().Items, item => item.Definition == BeatenEgg);
        Assert.True(liquid.IsProduct);
        Assert.Equal(ItemLocation.Container(Bowl, "slot-0"), liquid.Location);

        // 蛋液倒进锅，碗腾空复用。
        Execute(fixture, evidence, testId, "pour-egg", CookingRecipeOperation.Pour,
            item: Bowl, container: Pot, expectedVersion: ItemState(simulation, Bowl).Version,
            summary: "pour the egg liquid into the pot and free the bowl");
        Assert.Empty(simulation.ItemsInContainer(Bowl));
        Execute(fixture, evidence, testId, "bowl-back", CookingRecipeOperation.Drop,
            item: Bowl, station: Counter, expectedVersion: ItemState(simulation, Bowl).Version,
            summary: "put the emptied bowl back on the counter");

        // 多输入煮制（RetainInputs：输入保留、锅切"已完成"；水是默认供应）。
        Execute(fixture, evidence, testId, "soup-start", CookingRecipeOperation.StartProcess,
            recipe: SoupRecipe, item: Pot, station: Stove,
            expectedVersion: ItemState(simulation, Pot).Version, summary: "start the multi-input cooking");
        AdvanceClock(fixture, SoupTicks);
        Assert.True(ItemState(simulation, Pot).ContainerCompleted);
        Assert.Empty(simulation.Snapshot().Processes);

        // 端走锅：加工已完成，进度保留在"已完成"状态上。
        Execute(fixture, evidence, testId, "carry-pot", CookingRecipeOperation.Pickup,
            item: Pot, expectedVersion: ItemState(simulation, Pot).Version,
            summary: "carry the completed pot away from the stove");

        // 倒出：碗中生成蛋花汤，锅清空。
        Execute(fixture, evidence, testId, "pour-soup", CookingRecipeOperation.Pour,
            item: Pot, container: Bowl, expectedVersion: ItemState(simulation, Pot).Version,
            summary: "pour the soup into the bowl");
        var soup = Assert.Single(simulation.Snapshot().Items, item => item.IsProduct);
        Assert.Equal(Soup, soup.Definition);
        Assert.Equal(ItemLocation.Container(Bowl, "slot-0"), soup.Location);
        Assert.Empty(simulation.ItemsInContainer(Pot));

        // 前厅开单（注入缝隙，不是玩家命令，不进命令路径）。
        Assert.True(simulation.OpenOrder(SoupOrder, SoupOrderTemplate).Accepted);

        // 提交：订单完成一次、结算一条、碗脏交 NPC 且离册、干净池下降。
        Execute(fixture, evidence, testId, "submit-soup", CookingRecipeOperation.SubmitOrder,
            item: soup.Id, order: SoupOrder, expectedVersion: soup.Version,
            summary: "submit the bowl of soup to the order");
        Assert.Equal(CookingOrderStatus.Completed.ToString(),
            simulation.Snapshot().Orders.Single(order => order.Id == SoupOrder).Status);
        var settlement = Assert.Single(simulation.SettlementHistory);
        Assert.Equal(1, settlement.Sequence);
        Assert.Equal(SoupOrder, settlement.Order);
        Assert.Equal(SoupOrderTemplate, settlement.Template);
        Assert.Equal(SoupRecipe, settlement.Recipe);
        Assert.Equal(soup.Id, settlement.Product);
        Assert.Equal(Player, settlement.Player);
        Assert.Equal(Bowl, settlement.Container);
        Assert.True(settlement.LogicalTick > 0);
        Assert.Equal(Bowl, Assert.Single(fixture.WashPort.Requests));
        Assert.DoesNotContain(simulation.Snapshot().Items, item => item.Id == Bowl);
        Assert.Equal(1, simulation.CleanContainerCount(BowlDefinition));

        // 注入清洗完成：碗回池。
        Assert.True(simulation.CompleteWash(Bowl).Accepted);
        var returned = ItemState(simulation, Bowl);
        Assert.False(returned.IsDirty);
        Assert.Equal(ItemLocation.World(CleanPoolLocation), returned.Location);
        Assert.Equal(2, simulation.CleanContainerCount(BowlDefinition));
    }

    /// <summary>
    /// 经宿主执行一条玩家命令：入队被接受、一帧执行恰好一个 disposition、该帧推进一个 fixed tick。
    /// 拒绝时钉住命令级零变更（结果状态版本等于执行前版本、无事件）；帧级零变更由 E02 对照臂证明。
    /// </summary>
    private static void Execute(
        Fixture fixture,
        EvidenceScope? evidence,
        string? testId,
        string commandId,
        CookingRecipeOperation operation,
        string summary,
        RecipeId? recipe = null,
        ItemId? item = null,
        StationSlotId? station = null,
        ItemId? container = null,
        OrderId? order = null,
        int expectedVersion = 0,
        CookingRecipeRejectionReason expectedRejection = CookingRecipeRejectionReason.None)
    {
        var host = fixture.Host;
        var simulation = fixture.Simulation;
        var batch = fixture.NextBatch();
        var command = new CookingRecipeCommand(fixture.LevelScope.MatchScope, batch, Player,
            new RecipeCommandId(commandId), operation, recipe, null, item, station, container, order,
            expectedVersion, 0);
        var before = simulation.Snapshot();
        Assert.True(host.TryEnqueue(
            new CookingLevelCommandEnvelope(fixture.LevelScope, command, "loop-connection", commandId)).Accepted);

        var frame = host.Tick();
        Assert.True(frame.Accepted);
        Assert.Equal(batch, frame.SimulationBatch);
        Assert.Equal(simulation.LogicalTick, frame.HostFrameSequence);
        Assert.Equal(before.LogicalTick + 1, frame.Tick!.AfterLogicalTick);
        var disposition = Assert.Single(frame.Dispositions);
        Assert.Equal(CookingLevelDispositionKind.Executed, disposition.Kind);
        var result = disposition.Result!;
        if (expectedRejection == CookingRecipeRejectionReason.None)
        {
            Assert.Equal(CookingRecipeOutcome.Accepted, result.Outcome);
            Assert.Equal(CookingRecipeRejectionReason.None, result.Reason);
        }
        else
        {
            Assert.Equal(CookingRecipeOutcome.Rejected, result.Outcome);
            Assert.Equal(expectedRejection, result.Reason);
            Assert.Empty(result.Events);
            // 命令本身零变更：结果状态版本等于执行前版本（本帧固有的时钟推进由对照臂在 E02 抵消）。
            Assert.Equal(before.Version, result.StateVersion);
        }

        if (evidence is not null)
        {
            CookingRecipeAcceptanceEvidenceWriter.Append(evidence.Path, new CookingRecipeAcceptanceEvidence(
                testId!, "et-level-closed-loop", command, simulation.LogicalTick, result.Outcome.ToString(),
                result.Reason.ToString(), result.IsDuplicate, result.Events, before.Sha256(),
                simulation.Snapshot().Sha256(), summary, Runner, DateTimeOffset.UtcNow.ToString("O")));
        }
    }

    /// <summary>纯时钟帧：无命令入队，只推进一个 fixed tick，不产生 disposition。</summary>
    private static void AdvanceClock(Fixture fixture, int frames)
    {
        for (var frame = 1; frame <= frames; frame++)
        {
            var before = fixture.Simulation.LogicalTick;
            var result = fixture.Host.Tick();
            Assert.True(result.Accepted);
            Assert.Empty(result.Dispositions);
            Assert.Equal(before + 1, result.Tick!.AfterLogicalTick);
            Assert.Equal(before + 1, fixture.Simulation.LogicalTick);
        }
    }

    private static CookingContent LoadContent() => CookingContentCatalog.Load(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, CookingContentCatalog.ContentFileName)));

    private static Fixture CreateFixture(CookingContent content)
    {
        var matchScope = new CookingScope(Session, World, Match);
        var levelScope = new CookingLevelScope(matchScope, new RestaurantRuntimeId(1),
            new LevelId("loop-level"), 1);
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [Player] = new(Player, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new HashSet<string>(StringComparer.Ordinal)
                {
                    Board.Value, Stove.Value, Oven.Value, Counter.Value,
                }),
        };
        var simulationFixture = CookingContentCatalog.BuildFixture(content, matchScope, players,
            CleanPoolLocation);
        var washPort = new RecordingWashPort();
        var factory = new Factory(simulationFixture, washPort);
        var lifecycle = new CookingLevelLifecycle(levelScope, content.Snapshot, factory);
        var preparation = new CookingLevelPreparation(levelScope.Level, new MapId("map"),
            new CookingLogicalLayout(new LayoutId("layout"),
                new[] { Board, Stove, Oven, Counter },
                new[] { BowlDefinition, PotDefinition }),
            content.Identity);
        return new Fixture(levelScope, lifecycle, preparation, factory);
    }

    private static CookingRecipeSnapshotItem ItemState(CookingRecipeSimulation simulation, ItemId id) =>
        simulation.Snapshot().Items.Single(item => item.Id == id);

    private static EvidenceScope CreateEvidence(string testId) => new(testId);

    private static void AssertEvidence(string path, string testId, int recordCount)
    {
        var records = CookingRecipeAcceptanceEvidenceWriter.ReadAll(path);
        Assert.Equal(recordCount, records.Count);
        Assert.All(records, record =>
        {
            Assert.Equal(testId, record.TestId);
            Assert.Equal("et-level-closed-loop", record.FixtureId);
            Assert.False(string.IsNullOrWhiteSpace(record.BeforeStateHash));
            Assert.False(string.IsNullOrWhiteSpace(record.AfterStateHash));
            Assert.False(string.IsNullOrWhiteSpace(record.AssertionSummary));
            Assert.Equal(Runner, record.Runner);
        });
    }

    private sealed class Fixture : IDisposable
    {
        private readonly Factory _factory;
        private CookingLevelEtHost? _host;
        private long _batch;

        public Fixture(
            CookingLevelScope levelScope,
            CookingLevelLifecycle lifecycle,
            CookingLevelPreparation preparation,
            Factory factory)
        {
            LevelScope = levelScope;
            Lifecycle = lifecycle;
            Preparation = preparation;
            _factory = factory;
            WashPort = factory.WashPort;
        }

        public CookingLevelScope LevelScope { get; }
        public CookingLevelLifecycle Lifecycle { get; }
        public CookingLevelPreparation Preparation { get; }
        public RecordingWashPort WashPort { get; }
        public CookingRecipeSimulation Simulation => _factory.Simulation!;
        public CookingLevelEtHost Host =>
            _host ?? throw new InvalidOperationException("The level host is not started.");

        public long NextBatch() => ++_batch;

        public CookingLevelEtHost CreateStartedHost(Action<CookingRecipeSimulation> initialize)
        {
            var host = new CookingLevelEtHost(Lifecycle);
            try
            {
                Assert.True(host.Prepare(Preparation).Accepted);
                initialize(_factory.EnsureSimulation(LevelScope));
                Assert.True(host.Start().Accepted);
                return _host = host;
            }
            catch
            {
                // 初始化失败同样释放宿主：ET 宿主是进程级单例，泄漏会污染同进程后续测试。
                host.Dispose();
                throw;
            }
        }

        public void Dispose() => _host?.Dispose();
    }

    private sealed class Factory : ICookingLevelGameplayFactory
    {
        private readonly CookingRecipeFixture _fixture;
        private readonly RecordingWashPort _washPort;
        private CookingRecipeSimulation? _precreated;

        public Factory(CookingRecipeFixture fixture, RecordingWashPort washPort)
        {
            _fixture = fixture;
            _washPort = washPort;
        }

        public RecordingWashPort WashPort => _washPort;
        public CookingRecipeSimulation? Simulation { get; private set; }

        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            Assert.Equal(scope.MatchScope, _fixture.Scope);
            if (_precreated is not null)
            {
                Simulation = _precreated;
                _precreated = null;
                return Simulation;
            }

            return Simulation = NewSimulation();
        }

        public CookingRecipeSimulation EnsureSimulation(CookingLevelScope scope)
        {
            Assert.Equal(scope.MatchScope, _fixture.Scope);
            return _precreated ??= NewSimulation();
        }

        private CookingRecipeSimulation NewSimulation() => new(_fixture, null, _washPort);
    }

    private sealed class RecordingWashPort : ICookingBowlWashingPort
    {
        public List<ItemId> Requests { get; } = new();

        public void RequestWash(ItemId bowl, DefinitionId definition) => Requests.Add(bowl);
    }

    private sealed class EvidenceScope : IDisposable
    {
        private readonly string _directory;
        private readonly bool _keepArtifacts;

        public EvidenceScope(string testId)
        {
            var requestedRoot = Environment.GetEnvironmentVariable("COOKING_RECIPE_EVIDENCE_DIRECTORY");
            _keepArtifacts = !string.IsNullOrWhiteSpace(requestedRoot);
            var root = _keepArtifacts
                ? System.IO.Path.GetFullPath(requestedRoot!)
                : System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AbilityKit.ET.Runtime.Tests", "closed-loop");
            _directory = System.IO.Path.Combine(root, testId, Guid.NewGuid().ToString("N"));
            Path = System.IO.Path.Combine(_directory, "et-closed-loop.jsonl");
        }

        public string Path { get; }

        public void Dispose()
        {
            if (!_keepArtifacts && Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }
    }
}
