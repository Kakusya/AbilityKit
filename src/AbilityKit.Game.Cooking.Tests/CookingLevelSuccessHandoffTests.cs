using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

/// <summary>
/// 任务 <c>09-22-cooking-level-success-handoff</c>：小关成功交接只保留厨房现场。
/// 订单、结算、去重账本、事件历史和逻辑 Tick 属于本关，不进入下一小关。
/// </summary>
[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingLevelSuccessHandoffTests
{
    private static readonly SessionId Session = new("handoff-session");
    private static readonly WorldId World = new("handoff-world");
    private static readonly MatchId Match = new("handoff-match");
    private static readonly PlayerId Player = new("chef-a");
    private static readonly StationSlotId Board = new("board-a");
    private static readonly StationSlotId Stove = new("stove-a");
    private static readonly StationSlotId Oven = new("oven-a");
    private static readonly StationSlotId Counter = new("counter-a");
    private static readonly DefinitionId ChoppedTomato = new("chopped-tomato");
    private static readonly DefinitionId BeatenEgg = new("beaten-egg");
    private static readonly DefinitionId Soup = new("tomato-egg-soup");
    private static readonly DefinitionId BowlDefinition = new("bowl");
    private static readonly RecipeId ChopRecipe = new("chop-tomato");
    private static readonly RecipeId BeatRecipe = new("beat-egg");
    private static readonly RecipeId SoupRecipe = new("tomato-egg-soup");
    private static readonly OrderTemplateId SoupOrderTemplate = new("tomato-egg-soup-order");
    private static readonly OrderId SoupOrder = new("order-soup-1");
    private static readonly ItemId Pot = new("pot-1");
    private static readonly ItemId Bowl = new("pool-bowl-1");
    private static readonly ItemId Tomato = new("tomato-1");
    private static readonly ItemId Egg = new("egg-1");

    [Fact]
    public void H01_success_handoff_keeps_the_kitchen_and_clears_the_level_context()
    {
        var source = CreateSimulation();
        CookAndServe(source);
        var before = source.ExportCheckpoint();
        Assert.NotEmpty(before.Orders);
        Assert.NotEmpty(before.Settlements);
        Assert.True(before.LogicalTick > 0);
        var processId = Assert.Single(before.Processes).Id;
        var itemIds = before.Items.Select(item => item.Id).OrderBy(id => id.Value, StringComparer.Ordinal).ToArray();

        var handoff = source.ExportSuccessHandoff();
        var next = CreateSimulation();
        var accepted = next.AcceptSuccessHandoff(handoff);

        Assert.True(accepted.Accepted, accepted.Reason.ToString());
        var restored = next.ExportCheckpoint();
        Assert.Equal(itemIds, restored.Items.Select(item => item.Id).OrderBy(id => id.Value, StringComparer.Ordinal));
        Assert.Equal(processId, Assert.Single(restored.Processes).Id);
        Assert.Equal(before.Containers.Select(container => container.Id), restored.Containers.Select(container => container.Id));
        Assert.Equal(before.CleanContainerCounts, restored.CleanContainerCounts);
        Assert.Equal(before.ConsumedProducts, restored.ConsumedProducts);
        Assert.Equal(before.NextProcessId, restored.NextProcessId);
        Assert.Equal(before.NextProductId, restored.NextProductId);
        Assert.Equal(0, restored.NextSettlementSequence);
        Assert.Empty(restored.Orders);
        Assert.Empty(restored.Settlements);
        Assert.Empty(restored.Deduplication);
        Assert.Empty(restored.Events);
        Assert.Empty(restored.TickEvents);
        Assert.Equal(0, restored.LogicalTick);
        Assert.Equal(0, restored.EventSequence);
        Assert.Equal(0, restored.StateVersion);
        Assert.Null(restored.LevelScope);
        Assert.Equal(before.CanonicalText(), source.ExportCheckpoint().CanonicalText());
    }

    [Fact]
    public void H03_a_recovery_checkpoint_is_not_a_success_handoff()
    {
        var source = CreateSimulation();
        CookAndServe(source);
        var recovery = source.ExportCheckpoint();
        var next = CreateSimulation();
        var before = next.ExportCheckpoint().CanonicalText();

        var rejected = next.AcceptSuccessHandoff(recovery);

        Assert.False(rejected.Accepted);
        Assert.Equal(CookingCheckpointRestoreReason.NotSuccessHandoff, rejected.Reason);
        Assert.Equal(before, next.ExportCheckpoint().CanonicalText());
    }

    [Fact]
    public void H03_a_handoff_that_rewinds_product_ids_is_rejected()
    {
        var source = CreateSimulation();
        CookAndServe(source);
        var handoff = source.ExportSuccessHandoff() with { NextProductId = 0 };
        var next = CreateSimulation();
        CookAndServe(next);
        var before = next.ExportCheckpoint().CanonicalText();

        var rejected = next.AcceptSuccessHandoff(handoff);

        Assert.False(rejected.Accepted);
        Assert.Equal(CookingCheckpointRestoreReason.CounterInvalid, rejected.Reason);
        Assert.Equal(before, next.ExportCheckpoint().CanonicalText());
    }

    private static void CookAndServe(CookingRecipeSimulation simulation)
    {
        var frame = 0;
        Submit(simulation, Command(CookingRecipeOperation.Pickup, "pickup-bowl", item: Bowl, expectedVersion: Version(simulation, Bowl)));
        Submit(simulation, Command(CookingRecipeOperation.Drop, "drop-bowl", item: Bowl, station: Counter, expectedVersion: Version(simulation, Bowl)));
        Submit(simulation, Command(CookingRecipeOperation.Pickup, "pickup-tomato", item: Tomato, expectedVersion: Version(simulation, Tomato)));
        Submit(simulation, Command(CookingRecipeOperation.Drop, "drop-tomato", item: Tomato, station: Board, expectedVersion: Version(simulation, Tomato)));
        Submit(simulation, Command(CookingRecipeOperation.StartProcess, "chop", recipe: ChopRecipe, item: Tomato, station: Board, expectedVersion: Version(simulation, Tomato)));
        frame = Advance(simulation, frame, 2);
        var chopped = Assert.Single(simulation.Snapshot().Items, item => item.Definition == ChoppedTomato);
        Submit(simulation, Command(CookingRecipeOperation.Pickup, "pickup-chopped", item: chopped.Id, expectedVersion: chopped.Version));
        Submit(simulation, Command(CookingRecipeOperation.PutIn, "chopped-in", item: chopped.Id, container: Pot, expectedVersion: Version(simulation, chopped.Id)));
        Submit(simulation, Command(CookingRecipeOperation.Pickup, "pickup-egg", item: Egg, expectedVersion: Version(simulation, Egg)));
        Submit(simulation, Command(CookingRecipeOperation.PutIn, "egg-in", item: Egg, container: Bowl, expectedVersion: Version(simulation, Egg)));
        Submit(simulation, Command(CookingRecipeOperation.Pickup, "pickup-bowl-egg", item: Bowl, expectedVersion: Version(simulation, Bowl)));
        Submit(simulation, Command(CookingRecipeOperation.StartProcess, "beat", recipe: BeatRecipe, item: Bowl, expectedVersion: Version(simulation, Bowl)));
        frame = Advance(simulation, frame, 2);
        Submit(simulation, Command(CookingRecipeOperation.Pour, "pour-egg", item: Bowl, container: Pot, expectedVersion: Version(simulation, Bowl)));
        Submit(simulation, Command(CookingRecipeOperation.Drop, "bowl-back", item: Bowl, station: Counter, expectedVersion: Version(simulation, Bowl)));
        Submit(simulation, Command(CookingRecipeOperation.StartProcess, "soup", recipe: SoupRecipe, item: Pot, station: Stove, expectedVersion: Version(simulation, Pot)));
        frame = Advance(simulation, frame, 6);
        Submit(simulation, Command(CookingRecipeOperation.Pickup, "carry", item: Pot, expectedVersion: Version(simulation, Pot)));
        Submit(simulation, Command(CookingRecipeOperation.Pour, "pour-soup", item: Pot, container: Bowl, expectedVersion: Version(simulation, Pot)));
        var soup = Assert.Single(simulation.Snapshot().Items, item => item.Definition == Soup);
        Assert.True(simulation.OpenOrder(SoupOrder, SoupOrderTemplate).Accepted);
        Submit(simulation, Command(CookingRecipeOperation.SubmitOrder, "submit", item: soup.Id, order: SoupOrder, expectedVersion: soup.Version));
        Assert.NotEmpty(simulation.SettlementHistory);
        Submit(simulation, Command(CookingRecipeOperation.Drop, "drop-pot", item: Pot, station: Stove, expectedVersion: Version(simulation, Pot)));
        Submit(simulation, Command(CookingRecipeOperation.Pickup, "pickup-slice", item: new ItemId("bread-slice-1"), expectedVersion: Version(simulation, new ItemId("bread-slice-1"))));
        Submit(simulation, Command(CookingRecipeOperation.Drop, "drop-slice", item: new ItemId("bread-slice-1"), station: Oven, expectedVersion: Version(simulation, new ItemId("bread-slice-1"))));
        Submit(simulation, Command(CookingRecipeOperation.StartProcess, "bake", recipe: new RecipeId("bake-bread"), item: new ItemId("bread-slice-1"), station: Oven, expectedVersion: Version(simulation, new ItemId("bread-slice-1"))));
        Advance(simulation, frame, 1);
        Assert.NotEmpty(simulation.ExportCheckpoint().Processes);
    }

    private static int Advance(CookingRecipeSimulation simulation, int frame, int ticks)
    {
        for (var tick = 1; tick <= ticks; tick++)
            Assert.True(simulation.AdvanceFixedTick(LevelScope(), frame + tick).AfterLogicalTick > frame);
        return frame + ticks;
    }

    private static int Version(CookingRecipeSimulation simulation, ItemId item) =>
        simulation.Snapshot().Items.Single(candidate => candidate.Id == item).Version;

    private static void Submit(CookingRecipeSimulation simulation, CookingRecipeCommand command)
    {
        var result = simulation.Submit(command);
        Assert.Equal(CookingRecipeOutcome.Accepted, result.Outcome);
    }

    private static CookingLevelScope LevelScope() => new(
        new CookingScope(Session, World, Match), new RestaurantRuntimeId(1), new LevelId("level-1"), 1);

    private static CookingRecipeCommand Command(
        CookingRecipeOperation operation,
        string commandId,
        RecipeId? recipe = null,
        ItemId? item = null,
        StationSlotId? station = null,
        ItemId? container = null,
        OrderId? order = null,
        int expectedVersion = 0) =>
        new(new CookingScope(Session, World, Match), 10, Player, new RecipeCommandId(commandId), operation,
            recipe, null, item, station, container, order, expectedVersion, 0);

    private static CookingRecipeSimulation CreateSimulation()
    {
        var scope = new CookingScope(Session, World, Match);
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [Player] = new(Player, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new HashSet<string>(StringComparer.Ordinal) { Board.Value, Stove.Value, Oven.Value, Counter.Value }),
        };
        var content = CookingContentCatalog.Load(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, CookingContentCatalog.ContentFileName)));
        var simulation = new CookingRecipeSimulation(CookingContentCatalog.BuildFixture(content, scope, players, "clean-pool"));
        CookingContentCatalog.ApplyStandardInitialSupply(simulation, content);
        return simulation;
    }
}
