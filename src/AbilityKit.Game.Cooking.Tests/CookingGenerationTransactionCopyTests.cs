using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingGenerationTransactionCopyTests
{
    [Fact]
    public void Private_copy_mutates_independently_and_does_not_call_external_allocator()
    {
        var kitchen = Kitchen();
        var before = kitchen.ExportCheckpoint().CanonicalText();
        var staged = kitchen.CreateGenerationTransactionCopy();
        Assert.Equal(before, staged.ExportCheckpoint().CanonicalText());
        var item = staged.Snapshot().Items.Single(x => x.Id.Value == "bread-slice-1");
        var accepted = staged.Submit(new CookingRecipeCommand(kitchen.Snapshot().Scope, 1,
            new PlayerId("chef"), new RecipeCommandId("staged-pickup"), CookingRecipeOperation.Pickup,
            null, null, item.Id, null, null, null, item.Version, 0));
        Assert.Equal(CookingRecipeOutcome.Accepted, accepted.Outcome);
        Assert.Equal(before, kitchen.ExportCheckpoint().CanonicalText());
        Assert.NotEqual(before, staged.ExportCheckpoint().CanonicalText());
    }

    [Fact]
    public void Staged_legacy_front_reset_is_unpublished_until_commit_and_preserves_identity()
    {
        var kitchen = Kitchen();
        var template = new OrderTemplateId("tomato-egg-soup-order");
        var house = new CookingFrontOfHouse(new(1, 8, 2, 2, 2, 1, 9));
        house.Step(kitchen, template);
        Assert.Equal(1, house.SeatedCount);
        var original = house.Snapshot().CanonicalText();
        var copy = kitchen.CreateGenerationTransactionCopy();
        var restored = CookingFrontOfHouse.Restore(house.ExportCheckpoint(template), copy);
        Assert.True(restored.Accepted);
        var staged = restored.FrontOfHouse!;
        staged.DropFailedScene();
        var commit = house.PrepareGenerationStateAdoption(staged);
        Assert.Equal(original, house.Snapshot().CanonicalText());
        Assert.Equal(0, staged.SeatedCount);
        commit();
        Assert.Equal(staged.Snapshot().CanonicalText(), house.Snapshot().CanonicalText());
        Assert.Equal(0, house.SeatedCount);
    }

    [Fact]
    public void Incompatible_front_state_is_rejected_before_any_source_change()
    {
        var source = new CookingFrontOfHouse(new(1, 8, 2, 2, 2, 1, 9));
        var before = source.Snapshot().CanonicalText();
        Assert.Throws<ArgumentException>(() => source.PrepareGenerationStateAdoption(
            new CookingFrontOfHouse(new(2, 8, 2, 2, 2, 1, 9))));
        Assert.Equal(before, source.Snapshot().CanonicalText());
    }

    private static CookingRecipeSimulation Kitchen()
    {
        var content = CookingContentCatalog.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            CookingContentCatalog.ContentFileName)));
        var player = new PlayerId("chef");
        var match = new CookingScope(new SessionId("stage"), new WorldId("world"), new MatchId("match"));
        var fixture = CookingContentCatalog.BuildFixture(content, match,
            new Dictionary<PlayerId, CookingPlayerConfig> {
                [player] = new(player, new HashSet<string> { "cook" },
                    new HashSet<string> { "board-a", "stove-a", "oven-a", "counter-a" }) });
        var kitchen = new CookingRecipeSimulation(fixture, new NoAllocator());
        CookingContentCatalog.ApplyStandardInitialSupply(kitchen, content);
        return kitchen;
    }

    private sealed class NoAllocator : ICookingProductIdAllocator
    {
        public ItemId GetProductId(long productSequence) => throw new InvalidOperationException("External allocator must not be called.");
    }
}
