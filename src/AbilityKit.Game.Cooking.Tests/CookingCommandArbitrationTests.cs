using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

/// <summary>
/// 任务 <c>09-21-cooking-kitchen-loop-simulation</c> 的争抢仲裁契约测试：
/// 同一封闭批次内对同一物品/工位/容器的并发争抢按 (LogicalTick, 玩家 ID, 命令 ID) 稳定排序裁决，
/// 乱序抵达与重复投放结果一致。规则先落地，联机本身后续再说。
/// </summary>
[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingCommandArbitrationTests
{
    private static readonly SessionId Session = new("arbitration-session");
    private static readonly WorldId World = new("arbitration-world");
    private static readonly MatchId Match = new("arbitration-match");
    private static readonly PlayerId First = new("chef-a");
    private static readonly PlayerId Second = new("chef-b");
    private static readonly StationSlotId Stove = new("stove-a");
    private static readonly StationSlotId Counter = new("counter-a");
    private static readonly DefinitionId Raw = new("raw");
    private static readonly DefinitionId BowlDefinition = new("bowl");
    private static readonly ItemId Bowl = new("bowl-1");
    private static readonly ItemId Ingredient = new("ingredient-1");

    [Fact]
    public void G01_two_players_contending_for_one_item_resolve_by_stable_order()
    {
        var forwardRun = CreateSimulation();
        var forward = forwardRun.SubmitBatch(new[]
        {
            Command(First, "pickup-a", CookingRecipeOperation.Pickup, item: Ingredient, expectedVersion: 1),
            Command(Second, "pickup-b", CookingRecipeOperation.Pickup, item: Ingredient, expectedVersion: 1),
        });

        Assert.Equal(CookingRecipeOutcome.Accepted, forward[0].Outcome);
        Assert.Equal(CookingRecipeOutcome.Rejected, forward[1].Outcome);
        // 败者的 expected version 已被胜者的拾取推高：按乐观并发拒绝，证明胜者变更被观察到。
        Assert.Equal(CookingRecipeRejectionReason.ItemStale, forward[1].Reason);
        Assert.Equal(Ingredient, forwardRun.ItemInHand(First));
        Assert.Null(forwardRun.ItemInHand(Second));

        var reorderedRun = CreateSimulation();
        var reordered = reorderedRun.SubmitBatch(new[]
        {
            Command(Second, "pickup-b", CookingRecipeOperation.Pickup, item: Ingredient, expectedVersion: 1),
            Command(First, "pickup-a", CookingRecipeOperation.Pickup, item: Ingredient, expectedVersion: 1),
        });

        Assert.Equal(forward[0].Outcome, reordered[0].Outcome);
        Assert.Equal(forward[1].Outcome, reordered[1].Outcome);
        Assert.Equal(forward[1].Reason, reordered[1].Reason);
        Assert.Equal(forwardRun.Snapshot().CanonicalText(), reorderedRun.Snapshot().CanonicalText());
        Assert.Equal(forwardRun.Snapshot().Sha256(), reorderedRun.Snapshot().Sha256());
    }

    [Fact]
    public void G02_batch_commands_observe_earlier_mutations_in_sorted_order()
    {
        var simulation = CreateSimulation();
        var spawned = simulation.Snapshot().Items.Single(item => item.Id == Ingredient);

        var batch = simulation.SubmitBatch(new[]
        {
            Command(First, "drop-a", CookingRecipeOperation.Drop, item: Ingredient, station: Counter,
                expectedVersion: spawned.Version),
            Command(First, "pickup-a", CookingRecipeOperation.Pickup, item: Ingredient,
                expectedVersion: spawned.Version),
        });

        // 排序按命令 ID：“drop-a” 先于 “pickup-a”。drop 时物品还在世界（拒绝），
        // 随后 pickup 按初始版本成功——证明批次内排序执行且后者观察到前者的结果。
        Assert.Equal(CookingRecipeOutcome.Rejected, batch[0].Outcome);
        Assert.Equal(CookingRecipeRejectionReason.CurrentLocationMismatch, batch[0].Reason);
        Assert.Equal(CookingRecipeOutcome.Accepted, batch[1].Outcome);
        Assert.Equal(Ingredient, simulation.ItemInHand(First));
    }

    [Fact]
    public void G03_two_players_contending_for_one_container_resolve_by_stable_order()
    {
        var simulation = CreateSimulation();
        simulation.Submit(Command(First, "pickup-a", CookingRecipeOperation.Pickup, item: Ingredient, expectedVersion: 1));
        var held = simulation.Snapshot().Items.Single(item => item.Id == Ingredient);

        var batch = simulation.SubmitBatch(new[]
        {
            Command(Second, "pickup-b", CookingRecipeOperation.Pickup, item: Ingredient, expectedVersion: held.Version),
            Command(First, "put-in-a", CookingRecipeOperation.PutIn, item: Ingredient, container: Bowl,
                expectedVersion: held.Version),
        });

        // 排序按玩家 ID：chef-a 的放入先执行并成功，chef-b 的拾取随后观察到物品已入容器。
        Assert.Equal(CookingRecipeOutcome.Accepted, batch[0].Outcome);
        Assert.Equal(CookingRecipeOutcome.Rejected, batch[1].Outcome);
        Assert.Equal(CookingRecipeRejectionReason.ItemStale, batch[1].Reason);
        Assert.Equal(new[] { Ingredient }, simulation.ItemsInContainer(Bowl));
    }

    [Fact]
    public void G04_duplicate_identity_in_one_batch_executes_once()
    {
        var simulation = CreateSimulation();
        var command = Command(First, "pickup-once", CookingRecipeOperation.Pickup, item: Ingredient, expectedVersion: 1);

        var batch = simulation.SubmitBatch(new[] { command, command, command });

        Assert.Equal(3, batch.Count);
        Assert.Equal(CookingRecipeOutcome.Accepted, batch[0].Outcome);
        Assert.True(batch[1].IsDuplicate);
        Assert.True(batch[2].IsDuplicate);
        Assert.Empty(batch[1].Events);
        Assert.Empty(batch[2].Events);
        Assert.Single(simulation.EventHistory);
    }

    [Fact]
    public void G05_batch_rejects_mixed_simulation_batches()
    {
        var simulation = CreateSimulation();

        Assert.Throws<ArgumentException>(() => simulation.SubmitBatch(new[]
        {
            Command(First, "pickup-a", CookingRecipeOperation.Pickup, item: Ingredient, expectedVersion: 1),
            Command(Second, "pickup-b", CookingRecipeOperation.Pickup, item: Ingredient, expectedVersion: 1) with { SimulationBatch = 11 },
        }));
    }

    private static CookingRecipeSimulation CreateSimulation()
    {
        var scope = new CookingScope(Session, World, Match);
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [First] = new(First, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new HashSet<string>(StringComparer.Ordinal) { Stove.Value, Counter.Value }),
            [Second] = new(Second, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new HashSet<string>(StringComparer.Ordinal) { Stove.Value, Counter.Value }),
        };
        var items = new Dictionary<DefinitionId, CookingItemDefinition>
        {
            [Raw] = new(Raw, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [BowlDefinition] = new(BowlDefinition, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new CookingItemContainerCapability(1, new HashSet<DefinitionId> { Raw })),
        };
        var appliances = new Dictionary<StationSlotId, CookingApplianceDefinition>
        {
            [Stove] = new(Stove, new HashSet<string>(StringComparer.Ordinal) { "heat" }),
            [Counter] = new(Counter, new HashSet<string>(StringComparer.Ordinal)),
        };
        var simulation = new CookingRecipeSimulation(
            new CookingRecipeFixture(scope, players, items, appliances, new Dictionary<RecipeId, CookingRecipeDefinition>()));
        simulation.AddItem(Bowl, BowlDefinition, ItemLocation.Station(Counter));
        simulation.AddWorldIngredient(Ingredient, Raw, "spawn");
        return simulation;
    }

    private static CookingRecipeCommand Command(PlayerId player, string commandId, CookingRecipeOperation operation,
        ItemId? item = null, StationSlotId? station = null, ItemId? container = null, int expectedVersion = 0) =>
        new(new CookingScope(Session, World, Match), 10, player, new RecipeCommandId(commandId), operation,
            null, null, item, station, container, null, expectedVersion, 0);

}
