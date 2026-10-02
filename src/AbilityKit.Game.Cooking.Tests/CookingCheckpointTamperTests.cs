using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingCheckpointTamperTests
{
    private static readonly PlayerId Player = new("cook");
    private static readonly ItemId Pot = new("pot"), First = new("first"), Second = new("second");
    private static readonly DefinitionId Raw = new("raw"), Product = new("product"), Other = new("other"), PotDefinition = new("pot"), OtherPotDefinition = new("other-pot");
    private static readonly RecipeId Recipe = new("recipe"), OtherRecipe = new("other-recipe");
    private static readonly StationSlotId Board = new("board"), OtherBoard = new("other-board");
    private static readonly CookingScope Scope = new(new("session"), new("world"), new("match"));

    private static CookingRecipeSimulation Start(bool useDefault = false, bool requireCarrier = false, bool retain = false,
        bool physicalDefault = false)
    {
        var fixture = new CookingRecipeFixture(Scope,
            new Dictionary<PlayerId, CookingPlayerConfig>
            {
                [Player] = new(Player, new HashSet<string> { "cook" }, new HashSet<string> { "board" }),
            },
            new Dictionary<DefinitionId, CookingItemDefinition>
            {
                [Raw] = new(Raw, new HashSet<string> { "cook" }),
                [Product] = new(Product, new HashSet<string> { "cook" }),
                [Other] = new(Other, new HashSet<string> { "cook" }),
                [PotDefinition] = new(PotDefinition, new HashSet<string> { "cook" },
                    new(4, new HashSet<DefinitionId> { Raw, Product, Other })),
                [OtherPotDefinition] = new(OtherPotDefinition, new HashSet<string> { "cook" },
                    new(4, new HashSet<DefinitionId> { Raw, Product, Other })),
            },
            new Dictionary<StationSlotId, CookingApplianceDefinition>
            {
                [Board] = new(Board, new HashSet<string> { "work" }),
                [OtherBoard] = new(OtherBoard, new HashSet<string> { "work" }),
                [new("counter")] = new(new("counter"), new HashSet<string>()),
                [new("disabled")] = new(new("disabled"), new HashSet<string> { "work" }, IsAvailable: false),
            },
            new Dictionary<RecipeId, CookingRecipeDefinition>
            {
                [Recipe] = new(Recipe, useDefault ? new[] { Raw } : new[] { Raw, Raw }, Product,
                    new("process"), "work", 2, DefaultInputs: useDefault ? new[] { Other } : null,
                    Completion: retain ? CookingRecipeCompletionKind.RetainInputs : CookingRecipeCompletionKind.ConsumeInputs,
                    RequiredProcessingContainerDefinition: requireCarrier ? PotDefinition : null),
                [OtherRecipe] = new(OtherRecipe, new[] { Raw }, Other, new("other-process"), "work", 2),
            });
        var simulation = new CookingRecipeSimulation(fixture);
        simulation.AddItem(Pot, PotDefinition, ItemLocation.Station(Board));
        simulation.AddItem(First, Raw, ItemLocation.Container(Pot, "one"));
        if (!useDefault) simulation.AddItem(Second, Raw, ItemLocation.Container(Pot, "two"));
        else if (physicalDefault) simulation.AddItem(Second, Other, ItemLocation.Container(Pot, "two"));
        var result = simulation.Submit(new(Scope, 1, Player, new("start"), CookingRecipeOperation.StartProcess,
            Item: Pot, Station: Board, Recipe: Recipe,
            ExpectedItemVersion: simulation.Snapshot().Items.Single(item => item.Id == Pot).Version));
        Assert.Equal(CookingRecipeOutcome.Accepted, result.Outcome);
        return simulation;
    }

    private static void RejectUnchanged(CookingRecipeSimulation simulation, CookingRecipeCheckpoint forged)
    {
        var before = simulation.ExportCheckpoint().CanonicalText();
        Assert.False(simulation.RestoreCheckpoint(forged).Accepted);
        Assert.Equal(before, simulation.ExportCheckpoint().CanonicalText());
    }

    [Fact]
    public void Active_process_restore_rejects_missing_capability_and_unavailable_appliances()
    {
        var simulation = Start();
        var checkpoint = simulation.ExportCheckpoint();
        foreach (var station in new[] { new StationSlotId("counter"), new StationSlotId("disabled") })
            RejectUnchanged(simulation, checkpoint with
            {
                Processes = checkpoint.Processes.Select(process => process with { Station = station }).ToArray(),
            });
        Assert.True(simulation.RestoreCheckpoint(checkpoint).Accepted);
    }

    [Fact]
    public void Active_processes_cannot_claim_the_same_anchor_on_different_stations()
    {
        var simulation = Start();
        var checkpoint = simulation.ExportCheckpoint();
        var process = Assert.Single(checkpoint.Processes);
        RejectUnchanged(simulation, checkpoint with
        {
            Processes = checkpoint.Processes.Append(process with { Id = new("second-process"), Station = OtherBoard }).ToArray(),
        });
    }

    [Fact]
    public void Active_processes_cannot_share_a_station_or_material_lock()
    {
        var simulation = Start();
        var checkpoint = simulation.ExportCheckpoint();
        var process = Assert.Single(checkpoint.Processes);
        var other = process with
        {
            Id = new("second-process"), Recipe = OtherRecipe, Anchor = First,
            Container = null, LockedInputs = new[] { First },
        };
        RejectUnchanged(simulation, checkpoint with { Processes = checkpoint.Processes.Append(other).ToArray() });
        RejectUnchanged(simulation, checkpoint with
        {
            Processes = checkpoint.Processes.Append(other with { Station = OtherBoard }).ToArray(),
        });
    }

    [Fact]
    public void Active_container_process_must_lock_every_material_identity()
    {
        var simulation = Start();
        var checkpoint = simulation.ExportCheckpoint();
        RejectUnchanged(simulation, checkpoint with
        {
            Processes = checkpoint.Processes.Select(process => process with
            {
                LockedInputs = process.LockedInputs.Where(id => id != Second).ToArray(),
            }).ToArray(),
        });
        Assert.True(simulation.RestoreCheckpoint(checkpoint).Accepted);
    }

    [Fact]
    public void Active_process_restore_must_preserve_duplicate_material_counts()
    {
        var simulation = Start();
        var checkpoint = simulation.ExportCheckpoint();
        RejectUnchanged(simulation, checkpoint with
        {
            Items = checkpoint.Items.Select(item => item.Id == Second ? item with { Removed = true } : item).ToArray(),
            Containers = checkpoint.Containers.Select(container => container with
            {
                ItemIds = container.ItemIds.Where(id => id != Second).ToArray(),
            }).ToArray(),
            Processes = checkpoint.Processes.Select(process => process with
            {
                LockedInputs = process.LockedInputs.Where(id => id != Second).ToArray(),
            }).ToArray(),
        });
    }

    [Fact]
    public void Physically_supplied_default_material_is_locked_and_restores_as_a_real_instance()
    {
        var simulation = Start(useDefault: true, physicalDefault: true);
        var checkpoint = simulation.ExportCheckpoint();
        Assert.Equal(3, Assert.Single(checkpoint.Processes).LockedInputs.Count);
        Assert.True(simulation.RestoreCheckpoint(checkpoint).Accepted);
        Assert.Equal(checkpoint.CanonicalText(), simulation.ExportCheckpoint().CanonicalText());
    }

    [Fact]
    public void Default_materials_remain_virtual_and_are_not_required_as_locked_instances()
    {
        var simulation = Start(useDefault: true);
        var checkpoint = simulation.ExportCheckpoint();
        Assert.Equal(2, Assert.Single(checkpoint.Processes).LockedInputs.Count);
        Assert.True(simulation.RestoreCheckpoint(checkpoint).Accepted);
        Assert.Equal(checkpoint.CanonicalText(), simulation.ExportCheckpoint().CanonicalText());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Product_provenance_is_validated_for_live_instances_and_tombstones(bool discarded)
    {
        var simulation = Start();
        var level = new CookingLevelScope(Scope, new(1), new("level"), 1);
        simulation.AdvanceFixedTick(level, 1);
        simulation.AdvanceFixedTick(level, 2);
        var product = Assert.Single(simulation.Snapshot().Items, item => item.IsProduct);
        if (discarded)
        {
            var take = simulation.Submit(new(Scope, 2, Player, new("take"), CookingRecipeOperation.TakeOut,
                Item: product.Id, Container: Pot, ExpectedItemVersion: product.Version));
            Assert.True(take.Outcome == CookingRecipeOutcome.Accepted, take.Reason.ToString());
            product = Assert.Single(simulation.Snapshot().Items, item => item.Id == product.Id);
            var discard = simulation.Submit(new(Scope, 2, Player, new("discard"), CookingRecipeOperation.DiscardItem,
                Item: product.Id, ExpectedItemVersion: product.Version));
            Assert.True(discard.Outcome == CookingRecipeOutcome.Accepted, discard.Reason.ToString());
        }
        var checkpoint = simulation.ExportCheckpoint();
        foreach (var recipe in new RecipeId?[] { null, new("unknown"), OtherRecipe })
            RejectUnchanged(simulation, checkpoint with
            {
                Items = checkpoint.Items.Select(item => item.Id == product.Id ? item with { Recipe = recipe } : item).ToArray(),
            });
        Assert.True(simulation.RestoreCheckpoint(checkpoint).Accepted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Required_processing_carrier_is_checked_for_active_and_completed_batches(bool completed)
    {
        var simulation = Start(requireCarrier: true, retain: true);
        if (completed)
        {
            var level = new CookingLevelScope(Scope, new(1), new("level"), 1);
            simulation.AdvanceFixedTick(level, 1);
            simulation.AdvanceFixedTick(level, 2);
        }
        var checkpoint = simulation.ExportCheckpoint();
        Assert.True(simulation.RestoreCheckpoint(checkpoint).Accepted);
        Assert.Equal(checkpoint.CanonicalText(), simulation.ExportCheckpoint().CanonicalText());
        RejectUnchanged(simulation, checkpoint with
        {
            Items = checkpoint.Items.Select(item => item.Id == Pot ? item with { Definition = OtherPotDefinition } : item).ToArray(),
        });
    }

    [Fact]
    public void Ordinary_raw_material_cannot_claim_a_recipe_identity()
    {
        var simulation = Start();
        var checkpoint = simulation.ExportCheckpoint();
        RejectUnchanged(simulation, checkpoint with
        {
            Items = checkpoint.Items.Select(item => item.Id == First ? item with { Recipe = Recipe } : item).ToArray(),
        });
        Assert.True(simulation.RestoreCheckpoint(checkpoint).Accepted);
    }
}
