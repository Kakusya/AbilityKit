using System.Text.Json;
using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingAllocationWatermarkTests
{
    private static readonly CookingScope Scope = new(new("allocation-session"), new("world"), new("match"));
    private static readonly CookingLevelScope Level = new(Scope, new(1), new("level"), 1);
    private static readonly PlayerId Chef = new("chef");
    private static readonly DefinitionId Raw = new("raw"), Product = new("product"), PotDef = new("pot-def"), BowlDef = new("bowl-def");
    private static readonly ItemId RawItem = new("raw"), Pot = new("pot"), Bowl = new("bowl");
    private static readonly StationSlotId Board = new("board"), Counter = new("counter");
    private static readonly RecipeId Recipe = new("recipe");
    private static CookingRecipeFixture Fixture(bool retain = false, bool manual = false, int portions = 2)
    {
        var caps = new HashSet<string> { "cook" };
        return new(Scope, new Dictionary<PlayerId, CookingPlayerConfig> { [Chef] = new(Chef, caps, new HashSet<string> { "board", "counter" }) },
            new Dictionary<DefinitionId, CookingItemDefinition> {
                [Raw] = new(Raw, caps), [Product] = new(Product, caps),
                [PotDef] = new(PotDef, caps, new(3, new HashSet<DefinitionId> { Raw, Product })),
                [BowlDef] = new(BowlDef, caps, new(3, new HashSet<DefinitionId> { Product })) },
            new Dictionary<StationSlotId, CookingApplianceDefinition> { [Board] = new(Board, new HashSet<string> { "work" }), [Counter] = new(Counter, new HashSet<string>()) },
            new Dictionary<RecipeId, CookingRecipeDefinition> { [Recipe] = new(Recipe, new[] { Raw }, Product, new("work"), "work", 2,
                Completion: retain ? CookingRecipeCompletionKind.RetainInputs : CookingRecipeCompletionKind.ConsumeInputs,
                Execution: manual ? CookingRecipeExecutionKind.Manual : CookingRecipeExecutionKind.Automatic, YieldPortions: retain ? portions : 1) });
    }
    private static CookingRecipeSimulation Sim(bool retain = false, bool manual = false, ICookingProductIdAllocator? allocator = null, int portions = 2)
    {
        var sim = new CookingRecipeSimulation(Fixture(retain, manual, portions), allocator);
        if (retain) sim.AddItem(Pot, PotDef, ItemLocation.Station(Board));
        sim.AddItem(Bowl, BowlDef, ItemLocation.Station(Counter));
        sim.AddItem(RawItem, Raw, retain ? ItemLocation.Container(Pot, "slot-0") : ItemLocation.Station(Board));
        return sim;
    }
    private static CookingRecipeCommand Command(CookingRecipeSimulation sim, CookingRecipeOperation op, string id,
        ItemId? item = null, ItemId? container = null, ProcessId? process = null, int ticks = 0) =>
        new(Scope, 1, Chef, new(id), op, Item: item, Container: container, Process: process, TickCount: ticks,
            Station: op == CookingRecipeOperation.StartProcess ? Board : null,
            ExpectedItemVersion: item is { } value ? sim.Snapshot().Items.Single(i => i.Id == value).Version : 0);
    private static void Accept(CookingRecipeCommandResult result) => Assert.True(result.Outcome == CookingRecipeOutcome.Accepted, result.Reason.ToString());
    private static void Complete(CookingRecipeSimulation sim, bool retain, bool legacy = false)
    {
        Accept(sim.Submit(Command(sim, CookingRecipeOperation.StartProcess, "start", retain ? Pot : RawItem)));
        if (legacy) Accept(sim.Submit(Command(sim, CookingRecipeOperation.AdvanceTicks, "advance", process: sim.Snapshot().Processes.Single().Id, ticks: 2)));
        else { sim.AdvanceFixedTick(Level, 1); sim.AdvanceFixedTick(Level, 2); }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ordinary_product_watermark_rollback_is_rejected_without_mutation(bool retainedPortion)
    {
        var sim = Sim(retainedPortion); Complete(sim, retainedPortion);
        if (retainedPortion) Accept(sim.Submit(Command(sim, CookingRecipeOperation.ServePortion, "portion", Pot, Bowl)));
        var before = sim.ExportCheckpoint();
        Assert.False(sim.RestoreCheckpoint(before with { NextProductId = 0 }).Accepted);
        Assert.Equal(before.CanonicalText(), sim.ExportCheckpoint().CanonicalText());
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Fixed_and_legacy_consumed_products_preserve_sequence_even_after_discard_and_handoff(bool legacy)
    {
        var sim = Sim(); Complete(sim, false, legacy);
        var product = sim.Snapshot().Items.Single(i => i.IsProduct).Id;
        Assert.Equal(1, sim.ExportCheckpoint().Items.Single(i => i.Id == product).AllocationSequence);
        Accept(sim.Submit(Command(sim, CookingRecipeOperation.DiscardItem, "discard", product)));
        var checkpoint = sim.ExportCheckpoint();
        Assert.True(checkpoint.Items.Single(i => i.Id == product).Removed);
        var restored = new CookingRecipeSimulation(Fixture());
        if (!legacy) {
            var restore = restored.RestoreCheckpoint(checkpoint); Assert.True(restore.Accepted, restore.Reason.ToString());
            Assert.Equal(checkpoint.CanonicalText(), restored.ExportCheckpoint().CanonicalText());
        }
        var next = new CookingRecipeSimulation(Fixture());
        Assert.True(next.AcceptSuccessHandoff(sim.ExportSuccessHandoff()).Accepted);
        Assert.Equal(1, next.ExportCheckpoint().Items.Single(i => i.Id == product).AllocationSequence);
        next.AddItem(new("next-raw"), Raw, ItemLocation.Station(Board));
        Accept(next.Submit(Command(next, CookingRecipeOperation.StartProcess, "next-start", new("next-raw"))));
        next.AdvanceFixedTick(Level, 1); next.AdvanceFixedTick(Level, 2);
        Assert.Equal(2, next.ExportCheckpoint().Items.Single(i => i.IsProduct && !i.Removed).AllocationSequence);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Manual_retained_anchor_keeps_seed_sequence_while_serve_and_pour_allocate_new_objects(bool pour)
    {
        var sim = Sim(true, true, portions: pour ? 1 : 2); Complete(sim, true);
        Assert.Equal(0, sim.ExportCheckpoint().Items.Single(i => i.Id == Pot).AllocationSequence);
        if (pour) Accept(sim.Submit(Command(sim, CookingRecipeOperation.Pour, "pour", Pot, Bowl)));
        else {
            Accept(sim.Submit(Command(sim, CookingRecipeOperation.ServePortion, "one", Pot, Bowl)));
            Accept(sim.Submit(Command(sim, CookingRecipeOperation.ServePortion, "two", Pot, Bowl)));
        }
        var checkpoint = sim.ExportCheckpoint();
        Assert.Equal(pour ? new long[] { 1 } : new long[] { 1, 2 }, checkpoint.Items.Where(i => i.IsProduct).Select(i => i.AllocationSequence).Order());
        Assert.Equal(0, checkpoint.Items.Single(i => i.Id == Pot).AllocationSequence);
        Assert.True(checkpoint.Items.Single(i => i.Id == RawItem).Removed);
        var restored = new CookingRecipeSimulation(Fixture(true, true, pour ? 1 : 2));
        var restore = restored.RestoreCheckpoint(checkpoint); Assert.True(restore.Accepted, restore.Reason.ToString());
        Assert.Equal(checkpoint.CanonicalText(), restored.ExportCheckpoint().CanonicalText());
    }

    [Theory]
    [InlineData("negative")]
    [InlineData("duplicate")]
    [InlineData("above-watermark")]
    public void Ordinary_allocation_sequence_poison_rejects_exactly_without_live_mutation(string poison)
    {
        var sim = Sim(true); Complete(sim, true);
        Accept(sim.Submit(Command(sim, CookingRecipeOperation.ServePortion, "one", Pot, Bowl)));
        Accept(sim.Submit(Command(sim, CookingRecipeOperation.ServePortion, "two", Pot, Bowl)));
        var before = sim.ExportCheckpoint();
        var second = before.Items.Single(i => i.AllocationSequence == 2);
        var sequence = poison switch { "negative" => -1, "duplicate" => 1, _ => 3 };
        Assert.False(sim.RestoreCheckpoint(before with { Items = before.Items.Select(i => i.Id == second.Id
            ? i with { AllocationSequence = sequence } : i).ToArray() }).Accepted);
        Assert.Equal(before.CanonicalText(), sim.ExportCheckpoint().CanonicalText());
    }

    [Fact]
    public void Seed_products_can_keep_zero_and_watermark_may_legitimately_exceed_maximum_sequence()
    {
        var sim = Sim(); sim.AddItem(new("seed-product"), Product, ItemLocation.World("legacy-seed"));
        var checkpoint = sim.ExportCheckpoint();
        checkpoint = checkpoint with { NextProductId = 100, Items = checkpoint.Items.Select(i => i.Id == new ItemId("seed-product")
            ? i with { IsProduct = true, Recipe = Recipe } : i).ToArray() };
        Assert.True(sim.RestoreCheckpoint(checkpoint).Accepted);
        Assert.Equal(checkpoint.CanonicalText(), sim.ExportCheckpoint().CanonicalText());
        Complete(sim, false);
        Assert.Equal(101, sim.ExportCheckpoint().Items.Single(i => i.AllocationSequence > 0).AllocationSequence);
        Assert.Equal(0, sim.ExportCheckpoint().Items.Single(i => i.Id == new ItemId("seed-product")).AllocationSequence);
    }

    [Fact]
    public void Recipe_five_requires_item_allocation_sequence_even_for_legacy_seed_objects()
    {
        var checkpoint = Sim().ExportCheckpoint();
        var json = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(checkpoint))!;
        json["Items"]![0]!.AsObject().Remove("AllocationSequence");
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CookingRecipeCheckpoint>(json.ToJsonString()));
    }

    private sealed class ThrowingAllocator : ICookingProductIdAllocator
    {
        public ItemId GetProductId(long sequence) => throw new InvalidOperationException("allocation failed");
    }

    [Theory]
    [InlineData("fixed")]
    [InlineData("legacy")]
    [InlineData("portion")]
    public void Allocation_fault_keeps_all_sequence_state_and_watermarks_unchanged(string path)
    {
        var retain = path == "portion";
        var sim = Sim(retain, allocator: new ThrowingAllocator());
        Accept(sim.Submit(Command(sim, CookingRecipeOperation.StartProcess, "start", retain ? Pot : RawItem)));
        if (path == "fixed") sim.AdvanceFixedTick(Level, 1);
        if (retain) { sim.AdvanceFixedTick(Level, 1); sim.AdvanceFixedTick(Level, 2); }
        var before = sim.ExportCheckpoint().CanonicalText();
        Assert.Throws<InvalidOperationException>(() => {
            if (path == "fixed") sim.AdvanceFixedTick(Level, 2);
            else if (path == "legacy") sim.Submit(Command(sim, CookingRecipeOperation.AdvanceTicks, "advance", process: sim.Snapshot().Processes.Single().Id, ticks: 2));
            else sim.Submit(Command(sim, CookingRecipeOperation.ServePortion, "portion", Pot, Bowl));
        });
        Assert.Equal(before, sim.ExportCheckpoint().CanonicalText());
    }    private sealed class OpaqueAllocator : ICookingProductIdAllocator
    {
        public ItemId GetProductId(long sequence) => new("opaque-identity");
    }
    [Fact]
    public void Restore_validates_saved_sequences_without_invoking_the_custom_allocator()
    {
        var sim = Sim(allocator: new OpaqueAllocator()); Complete(sim, false);
        var checkpoint = sim.ExportCheckpoint();
        Assert.Equal(new ItemId("opaque-identity"), checkpoint.Items.Single(i => i.AllocationSequence == 1).Id);
        var restored = new CookingRecipeSimulation(Fixture(), new ThrowingAllocator());
        Assert.True(restored.RestoreCheckpoint(checkpoint).Accepted);
        Assert.Equal(checkpoint.CanonicalText(), restored.ExportCheckpoint().CanonicalText());
    }}