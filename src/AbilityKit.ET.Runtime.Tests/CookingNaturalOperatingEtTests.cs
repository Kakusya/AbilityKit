using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

[Trait("Gate", "CookingLevelRuntime")]
public sealed class CookingNaturalOperatingEtTests
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
