using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingEffectiveProcessTimingTests
{
    private static readonly CookingScope Scope = new(new("effective-ticks"), new("world"), new("match"));
    private static readonly CookingLevelScope Level = new(Scope, new(1), new("level"), 1);
    private static readonly PlayerId Chef = new("chef");
    private static readonly StationSlotId Stove = new("stove");
    private static readonly DefinitionId Raw = new("raw"), Food = new("food");
    private static readonly RecipeId Soup = new("tomato-egg-soup");
    private static CookingRecipeSimulation Create(CookingMajorProgress progress)
    {
        var capabilities = new HashSet<string> { "cook" };
        var fixture = new CookingRecipeFixture(Scope,
            new Dictionary<PlayerId, CookingPlayerConfig> { [Chef] = new(Chef, capabilities, new HashSet<string> { Stove.Value }) },
            new Dictionary<DefinitionId, CookingItemDefinition> { [Raw] = new(Raw, capabilities), [Food] = new(Food, capabilities) },
            new Dictionary<StationSlotId, CookingApplianceDefinition> { [Stove] = new(Stove, new HashSet<string> { "heat" }) },
            new Dictionary<RecipeId, CookingRecipeDefinition> { [Soup] = new(Soup, new[] { Raw }, Food, new("heat"), "heat", 6) });
        var simulation = new CookingRecipeSimulation(fixture); simulation.UseMajorProgress(progress);
        simulation.AddItem(new("raw-one"), Raw, ItemLocation.Station(Stove));
        return simulation;
    }
    private static void Start(CookingRecipeSimulation simulation)
    {
        var result = simulation.Submit(new(Scope, 1, Chef, new("start"), CookingRecipeOperation.StartProcess,
            Item: new("raw-one"), Recipe: Soup, Station: Stove, ExpectedItemVersion: 1));
        Assert.Equal(CookingRecipeOutcome.Accepted, result.Outcome);
    }
    private static CookingMajorProgress Faster()
    {
        var progress = new CookingMajorProgress(); Assert.True(progress.EnableCookFaster().Accepted); progress.Lock(); return progress;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Fixed_tick_preserves_valid_start_duration_when_acceleration_is_confirmed(bool enabledBeforeStart)
    {
        var progress = new CookingMajorProgress();
        if (enabledBeforeStart) Assert.True(progress.EnableCookFaster().Accepted);
        var simulation = Create(progress); Start(simulation);
        var expected = enabledBeforeStart ? 3 : 6;
        Assert.Equal(expected, Assert.Single(simulation.Snapshot().Processes).RequiredTicks);
        if (!enabledBeforeStart) Assert.True(progress.EnableCookFaster().Accepted);
        progress.Lock();
        for (var tick = 1; tick < expected; tick++)
        {
            simulation.AdvanceFixedTick(Level, tick);
            Assert.Equal(tick, Assert.Single(simulation.Snapshot().Processes).ElapsedTicks);
            Assert.Equal(expected, Assert.Single(simulation.Snapshot().Processes).RequiredTicks);
        }
        simulation.AdvanceFixedTick(Level, expected);
        Assert.Empty(simulation.Snapshot().Processes);
        Assert.Single(simulation.Snapshot().Items, item => item.IsProduct && item.Definition == Food);
    }

    [Fact]
    public void Accelerated_checkpoint_requires_trusted_progress_and_rejects_arbitrary_duration()
    {
        var original = Create(Faster()); Start(original); var checkpoint = original.ExportCheckpoint();
        var withoutGrant = Create(new CookingMajorProgress());
        var before = withoutGrant.ExportCheckpoint().CanonicalText();
        Assert.Equal(CookingCheckpointRestoreReason.ProcessTicksMismatch, withoutGrant.RestoreCheckpoint(checkpoint).Reason);
        Assert.Equal(before, withoutGrant.ExportCheckpoint().CanonicalText());
        var restored = Create(Faster()); before = restored.ExportCheckpoint().CanonicalText();
        var tampered = checkpoint with { Processes = checkpoint.Processes.Select(process => process with { RequiredTicks = 2 }).ToArray() };
        Assert.Equal(CookingCheckpointRestoreReason.ProcessTicksMismatch, restored.RestoreCheckpoint(tampered).Reason);
        Assert.Equal(before, restored.ExportCheckpoint().CanonicalText());
        Assert.True(restored.RestoreCheckpoint(checkpoint).Accepted);
        for (var tick = 1; tick <= 3; tick++)
        {
            original.AdvanceFixedTick(Level, tick); restored.AdvanceFixedTick(Level, tick);
            Assert.Equal(original.ExportCheckpoint().CanonicalText(), restored.ExportCheckpoint().CanonicalText());
        }
        Assert.Empty(restored.Snapshot().Processes);
    }

    [Fact]
    public void Retry_choices_cannot_remove_the_grant_required_by_active_work()
    {
        var source = Create(Faster()); Start(source);
        var before = source.ExportCheckpoint().CanonicalText();
        var content = CookingContentCatalog.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, CookingContentCatalog.ContentFileName)));
        var replacement = new CookingMajorProgress(); replacement.Lock();
        Assert.False(source.ApplyRetryChoices(content, replacement).Accepted);
        Assert.Equal(before, source.ExportCheckpoint().CanonicalText());
        source.AdvanceFixedTick(Level, 1);
        Assert.Equal(1, Assert.Single(source.Snapshot().Processes).ElapsedTicks);
    }

    [Fact]
    public void Private_generation_copy_installs_trusted_progress_before_validating_active_process()
    {
        var source = Create(Faster()); Start(source);
        var copy = source.CreateGenerationTransactionCopy();
        Assert.Equal(source.ExportCheckpoint().CanonicalText(), copy.ExportCheckpoint().CanonicalText());
    }
}
