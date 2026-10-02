using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using Xunit;
using Xunit.Abstractions;

namespace AbilityKit.ET.Runtime.Tests;

[Trait("Gate", "CookingLevelRuntime")]
public sealed class CookingNaturalOperatingEtTests(ITestOutputHelper output)
{
    [Fact]
    public void Finite_procurement_two_player_catalog_service_replays_and_restores_each_frame()
    {
        var uninterrupted = Run(false);
        var replay = Replay(uninterrupted); var recovered = Run(true);
        Assert.Equal(uninterrupted.Trace, replay.Trace); Assert.Equal(uninterrupted.FinalCanonical, replay.FinalCanonical);
        Assert.Equal(uninterrupted.Trace, recovered.Trace); Assert.Equal(uninterrupted.FinalCanonical, recovered.FinalCanonical);
        var runningRecovered = Run(true, runningCup: true);
        Assert.Equal(uninterrupted.Trace, runningRecovered.Trace); Assert.Equal(uninterrupted.FinalCanonical, runningRecovered.FinalCanonical);
    }

    [Fact]
    public void Naturally_finished_service_durable_successor_restarts_as_created_and_continues_like_live_successor()
    {
        var live = RunDurableSuccessor(false); var restarted = RunDurableSuccessor(true);
        Assert.Equal(live.CreatedCanonical, restarted.CreatedCanonical);
        Assert.Equal(live.Trace, restarted.Trace);
        Assert.Equal(live.FinalCanonical, restarted.FinalCanonical);
        Assert.Equal(live.BaselineCharacters, restarted.BaselineCharacters);
        output.WriteLine($"Typed major baseline3: live={live.BaselineCharacters} characters, restarted={restarted.BaselineCharacters}; maximum={CookingMajorCheckpointStore.MaximumBaselineRecordCharacters}. Continuation frames={live.Trace.Count}.");
    }

    private sealed record DurableRun(string CreatedCanonical, IReadOnlyList<string> Trace, string FinalCanonical, int BaselineCharacters);
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Full_catalog_healthy_owner_failed_retry_preserves_unique_standard_tools_and_scoped_unlocks(bool scopedUnlock)
    {
        var fixture = new CookingNaturalOperatingFixture(fullCatalog: true, disallowedRetryUnlock: scopedUnlock);
        fixture.Begin();
        try
        {
            Assert.Equal(87, fixture.Catalog.Document.Menus.Count);
            Assert.True(fixture.Host.CompletePreparation().Accepted); Assert.True(fixture.Host.Start().Accepted);
            fixture.Tick(); Assert.False(fixture.Host.IsFaulted);
            var progress = new CookingMajorProgress();
            if (fixture.DisallowedRetryUnlock is { } unlocked)
            {
                Assert.Contains(unlocked, fixture.Content.Items.Keys);
                Assert.True(progress.Unlock(unlocked).Accepted);
            }
            progress.Lock();
            // Explicit application-owner failure control; normal service still uses natural success.
            Assert.True(fixture.Host.BeginEnd(CookingLevelOutcome.Failed).Accepted);
            Assert.True(fixture.Host.CompleteEnd().Accepted);
            var result = fixture.Host.CreateRetry(2, fixture.Content, progress);
            if (!result.Accepted)
            {
                var diagnosticSeed = fixture.Create(new(fixture.Scope.MatchScope, fixture.Scope.RestaurantRuntime, fixture.Scope.Level, 2), fixture.Content.Snapshot);
                var failure = Record.Exception(() => CookingContentCatalog.ApplyStandardInitialSupply(diagnosticSeed, fixture.Content));
                output.WriteLine("Rejected Host retry standard-supply diagnostic: " + failure);
            }
            Assert.True(result.Accepted, result.ToString()); fixture.UseLoadedHost(fixture.Host);
            Assert.Equal(CookingLevelState.Created, fixture.Host.Lifecycle.State);
            fixture.ResetActionPlanner(); fixture.BeginCurrentPreparation();
            var occupied = fixture.Items.Where(i => i.Location.Kind is LocationKind.WorldPosition or LocationKind.StationSlot)
                .GroupBy(i => i.Location).Where(g => g.Count() > 1).ToArray();
            Assert.Empty(occupied);
            if (fixture.DisallowedRetryUnlock is { } forbidden)
            {
                Assert.Contains(forbidden, progress.Unlocks);
                Assert.DoesNotContain(fixture.Items, i => i.Definition == forbidden);
                Assert.DoesNotContain(forbidden, fixture.CreateMenuConfiguration(fixture.Scope, fixture.Content.Snapshot).AllowedMaterialDefinitions);
            }
            Assert.True(fixture.Host.CompletePreparation().Accepted); Assert.True(fixture.Host.Start().Accepted);
            fixture.Tick(); Assert.False(fixture.Host.IsFaulted);
        }
        finally { fixture.Host.Dispose(); }
    }

    private DurableRun RunDurableSuccessor(bool restart)
    {
        var temporaryRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "AbilityKit-natural-baseline-" + Guid.NewGuid().ToString("N")));
        var fixture = new CookingNaturalOperatingFixture(); fixture.Begin();
        try
        {
            fixture.Procure(); var drink = fixture.ProduceAndPlate("D31"); fixture.PrepareComponents("F01");
            Assert.True(fixture.Host.CompletePreparation().Accepted); Assert.True(fixture.Host.Start().Accepted);
            fixture.Deliver(drink, "D31"); var salad = fixture.ProduceAndPlate("F01"); fixture.Deliver(salad, "F01");
            fixture.FinishNaturally();
            var sourceScope = fixture.Scope;
            var targetScope = new CookingLevelScope(sourceScope.MatchScope, sourceScope.RestaurantRuntime, new("service-2"), 2);
            var choices = new CookingMajorProgress(); choices.Lock();
            var store = new CookingMajorCheckpointStore(temporaryRoot);
            var successor = fixture.Host.CreateSuccessor(targetScope.Level, targetScope.LevelEpoch, fixture.Preparation(targetScope), choices, store);
            Assert.True(successor.Accepted, successor.ToString()); fixture.UseLoadedHost(fixture.Host); Assert.Equal(targetScope, fixture.Scope);
            Assert.Equal(CookingLevelState.Created, fixture.Host.Lifecycle.State);
            var created = fixture.FinalCanonical;
            var baselineText = File.ReadAllText(Path.Combine(temporaryRoot, "major.checkpoint.json"));
            Assert.InRange(baselineText.Length, 1, CookingMajorCheckpointStore.MaximumBaselineRecordCharacters);
            output.WriteLine($"Natural major baseline v3: {baselineText.Length} characters; restart={restart}.");
            var baseline = store.ReadBaseline(sourceScope.MatchScope); Assert.True(baseline.Accepted, baseline.ToString());
            var allocator = baseline.Payload!.Kitchen.NextProductId;
            var carriedSupply = baseline.Payload.Kitchen.Supply!;
            if (restart)
            {
                fixture.Host.Dispose();
                var freshFactory = new CookingNaturalOperatingFixture();
                var loaded = CookingLevelEtHost.LoadMajorBaseline(new CookingMajorCheckpointStore(temporaryRoot), sourceScope.MatchScope,
                    freshFactory.Content.Snapshot, freshFactory);
                Assert.True(loaded.Accepted, loaded.ToString()); fixture = freshFactory; fixture.UseLoadedHost(loaded.Host!);
                Assert.Equal(CookingLevelState.Created, fixture.Host.Lifecycle.State); Assert.Equal(targetScope, fixture.Scope);
                Assert.Equal(created, fixture.FinalCanonical);
            }
            fixture.ResetActionPlanner(); fixture.BeginCurrentPreparation(); fixture.PrepareComponents("F01");
            Assert.True(fixture.Host.CompletePreparation().Accepted); Assert.True(fixture.Host.Start().Accepted);
            var nextSalad = fixture.ProduceAndPlate("F01"); fixture.Deliver(nextSalad, "F01");
            for (var tick = 0; tick < 5; tick++) fixture.Tick();
            Assert.Single(fixture.Simulation.SettlementHistory);
            Assert.True(fixture.Simulation.ExportCheckpoint().NextProductId > allocator);
            Assert.Equal(carriedSupply.Balances, fixture.Simulation.ExportCheckpoint().Supply!.Balances);
            Assert.Equal(carriedSupply.Deliveries, fixture.Simulation.ExportCheckpoint().Supply!.Deliveries);
            return new(created, fixture.Trace, fixture.FinalCanonical, baselineText.Length);
        }
        finally
        {
            fixture.Host.Dispose();
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), temporaryRoot, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("AbilityKit-natural-baseline-", Path.GetFileName(temporaryRoot), StringComparison.Ordinal);
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    private sealed record RunResult(IReadOnlyList<string> Trace, IReadOnlyList<CookingRecipeCommand?> Inputs, int StartAt, string FinalCanonical);
    private static RunResult Replay(RunResult baseline)
    {
        var fixture = new CookingNaturalOperatingFixture(); fixture.Begin();
        try
        {
            for (var index = 0; index < baseline.Inputs.Count; index++)
            {
                if (index == baseline.StartAt) { Assert.True(fixture.Host.CompletePreparation().Accepted); Assert.True(fixture.Host.Start().Accepted); }
                fixture.ReplayFrame(baseline.Inputs[index]);
            }
            Assert.True(fixture.Host.TryFinishService().Accepted); Assert.True(fixture.Host.CompleteEnd().Accepted);
            Assert.Equal(2, fixture.Simulation.SettlementHistory.Count); Assert.NotEmpty(fixture.Host.FrontOfHouseSnapshot!.UnsatisfiedOrders);
            return new(fixture.Trace, fixture.Inputs, baseline.StartAt, fixture.FinalCanonical);
        }
        finally { fixture.Host.Dispose(); }
    }
    private static RunResult Run(bool recover, bool runningCup = false)
    {
        var fixture = new CookingNaturalOperatingFixture(); fixture.Begin();
        var restored = false;
        var drinkMenu = fixture.Catalog.Document.Menus.Single(m => m.SourceId == "D31");
        fixture.AfterFrame = () =>
        {
            if (!recover || restored) return;
            var checkpointPoint = runningCup
                ? fixture.Host.Lifecycle.State == CookingLevelState.Running && fixture.Host.FrontOfHouseSnapshot!.Customers.Any(c => c.OrderTemplate == drinkMenu.OrderTemplate && c.Order is not null)
                    && fixture.Items.Any(i => i.Definition == drinkMenu.Product && i.BoundOrder is null)
                : fixture.Simulation.Snapshot().Processes.Any(p => p.ActiveWorker is null && fixture.Content.Recipes[p.Recipe].Execution == CookingRecipeExecutionKind.Manual);
            if (!checkpointPoint) return;
            fixture.Restore(); restored = true;
        };
        try
        {
            fixture.Procure();
            var drink = fixture.ProduceAndPlate("D31");
            fixture.PrepareComponents("F01");
            Assert.True(fixture.HandedOff); Assert.True(fixture.AutomaticLeftUnattended);
            Assert.Equal(0, fixture.Host.FrontOfHouseSnapshot!.ServiceTicks);
            var startAt = fixture.Inputs.Count;
            Assert.True(fixture.Host.CompletePreparation().Accepted); Assert.True(fixture.Host.Start().Accepted);
            fixture.Deliver(drink, "D31");
            var salad = fixture.ProduceAndPlate("F01");
            fixture.Deliver(salad, "F01");
            Assert.True(fixture.UnboundCupHandedOff); Assert.Equal(2, fixture.Simulation.SettlementHistory.Count);
            fixture.FinishNaturally(); Assert.Equal(recover, restored);
            Assert.Equal(fixture.Host.HostFrameSequence, fixture.Trace.Count);
            return new(fixture.Trace, fixture.Inputs, startAt, fixture.FinalCanonical);
        }
        finally { fixture.Host.Dispose(); }
    }
}
