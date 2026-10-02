using System.Text.Json;
using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingRecipeSupplyTests
{
    private static readonly CookingScope Scope = new(new("supply-session"), new("world"), new("match"));
    private static readonly PlayerId Chef = new("chef");
    private static readonly DefinitionId Raw = new("raw"), Box = new("box"), Product = new("meal"), Plate = new("plate");
    private static readonly StationSlotId Stove = new("stove");
    private static readonly RecipeId Recipe = new("cook");
    private static readonly CookingLevelScope Level = new(Scope, new(1), new("level"), 1);

    private static CookingRecipeFixture Fixture(bool infinite = false, int ticks = 2, long stock = 6)
    {
        var caps = new HashSet<string> { "cook" };
        return new(Scope, new Dictionary<PlayerId, CookingPlayerConfig> { [Chef] = new(Chef, caps, new HashSet<string> { "stove" }) },
            new Dictionary<DefinitionId, CookingItemDefinition> {
                [Raw] = new(Raw, caps), [Product] = new(Product, caps),
                [Box] = new(Box, caps, new(3, new HashSet<DefinitionId> { Raw })),
                [Plate] = new(Plate, caps, new(1, new HashSet<DefinitionId> { Product })) },
            new Dictionary<StationSlotId, CookingApplianceDefinition> { [Stove] = new(Stove, new HashSet<string> { "heat" }) },
            new Dictionary<RecipeId, CookingRecipeDefinition> { [Recipe] = new(Recipe, new[] { Raw }, Product, new("heat"), "heat", 1) },
            orderTemplates: new Dictionary<OrderTemplateId, CookingOrderTemplateDefinition> { [new("meal-order")] = new(new("meal-order"), Recipe, Plate) },
            spatial: new(-5000, -5000, 5000, 5000, 100, 2000,
                new[] { new CookingPlayerPose(Chef, 0, 0, 1, 0) },
                new[] { new CookingSpatialAnchor(LocationKind.WorldPosition, "source", 200, 0),
                    new CookingSpatialAnchor(LocationKind.WorldPosition, "receiving", 300, 0),
                    new CookingSpatialAnchor(LocationKind.WorldPosition, "storage", 400, 0),
                    new CookingSpatialAnchor(LocationKind.WorldPosition, "plate", 500, 0),
                    new CookingSpatialAnchor(LocationKind.StationSlot, "stove", 600, 0) }, Array.Empty<CookingSpatialObstacle>()),
            supply: new(new[] { new CookingSupplierDefinition("supplier", "source", "receiving", Raw, Box, 3, ticks, infinite ? 0 : stock, infinite) }));
    }

    private static CookingRecipeCommand Cmd(CookingRecipeOperation op, string id, string? request = null, string? delivery = null,
        ItemId? item = null, ItemId? container = null, int version = 0, string? world = null, StationSlotId? station = null,
        OrderId? order = null) => new(Scope, 1, Chef, new(id), op, Item: item, Container: container,
            ExpectedItemVersion: version, WorldAnchor: world, Station: station, Order: order,
            SupplierId: request is null ? null : "supplier", SupplyRequestId: request, DeliveryId: delivery);

    private static CookingRecipeCommandResult Accept(CookingRecipeSimulation sim, CookingRecipeCommand command)
    {
        var r = sim.Submit(command);
        Assert.True(r.Outcome == CookingRecipeOutcome.Accepted, $"{command.Command}: {r.Reason}");
        return r;
    }

    private static CookingSupplyPhysicalResult Receive(CookingRecipeSimulation sim, string request = "r")
    {
        var delivery = Accept(sim, Cmd(CookingRecipeOperation.RequestSupply, "request-" + request, request: request)).Supply!.DeliveryId;
        sim.AdvanceFixedTick(Level, sim.LogicalTick + 1);
        sim.AdvanceFixedTick(Level, sim.LogicalTick + 1);
        return Accept(sim, Cmd(CookingRecipeOperation.ReceiveSupply, "receive-" + request, delivery: delivery)).Supply!;
    }

    [Theory]
    [InlineData("zero")]
    [InlineData("negative")]
    [InlineData("above-watermark")]
    [InlineData("duplicate")]
    [InlineData("reordered")]
    public void Corrupt_supply_allocation_sequences_reject_without_mutation(string corruption)
    {
        var sim = new CookingRecipeSimulation(Fixture()); Receive(sim);
        var before = sim.ExportCheckpoint();
        var origin = before.SupplyOrigins!.Single();
        var first = before.Items.Single(i => i.Id == origin.Units[0]);
        var second = before.Items.Single(i => i.Id == origin.Units[1]);
        var sequence = corruption switch {
            "zero" => 0, "negative" => -1, "above-watermark" => before.NextProductId + 1,
            _ => second.SupplyProvenance!.AllocationSequence };
        var poisoned = before with { Items = before.Items.Select(i => i.Id == first.Id
            ? i with { SupplyProvenance = i.SupplyProvenance! with { AllocationSequence = sequence } }
            : corruption == "reordered" && i.Id == second.Id
                ? i with { SupplyProvenance = i.SupplyProvenance! with { AllocationSequence = first.SupplyProvenance!.AllocationSequence } } : i).ToArray() };
        Assert.False(sim.RestoreCheckpoint(poisoned).Accepted);
        Assert.Equal(before.CanonicalText(), sim.ExportCheckpoint().CanonicalText());
    }

    [Fact]
    public void Legal_allocation_sequences_survive_consumed_tombstones_and_restore_continues_above_watermark()
    {
        var sim = new CookingRecipeSimulation(Fixture());
        var received = Receive(sim);
        var raw = received.Units[0];
        Accept(sim, Cmd(CookingRecipeOperation.DiscardItem, "discard-unit", item: raw, version: 1));
        var package = received.Package!.Value;
        Accept(sim, Cmd(CookingRecipeOperation.Pickup, "pickup-box", item: package, version: 1));
        var before = sim.ExportCheckpoint();
        Assert.True(before.Items.Single(i => i.Id == raw).Removed);
        Assert.Equal(Enumerable.Range(1, 4).Select(n => (long)n), before.Items.Where(i => i.SupplyProvenance is not null)
            .Select(i => i.SupplyProvenance!.AllocationSequence).Order());
        var restored = new CookingRecipeSimulation(Fixture());
        Assert.True(restored.RestoreCheckpoint(before).Accepted);
        Assert.Equal(before.CanonicalText(), restored.ExportCheckpoint().CanonicalText());
        var next = Receive(restored, "second");
        Assert.All(restored.ExportCheckpoint().Items.Where(i => i.Id == next.Package || next.Units.Contains(i.Id)),
            i => Assert.True(i.SupplyProvenance!.AllocationSequence > before.NextProductId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Receive_version_or_event_watermark_overflow_keeps_supply_and_physical_state(bool eventOverflow)
    {
        var sim = new CookingRecipeSimulation(Fixture());
        var delivery = Accept(sim, Cmd(CookingRecipeOperation.RequestSupply, "request", request: "r")).Supply!.DeliveryId;
        sim.AdvanceFixedTick(Level, 1); sim.AdvanceFixedTick(Level, 2);
        var checkpoint = sim.ExportCheckpoint();
        Assert.True(sim.RestoreCheckpoint(eventOverflow ? checkpoint with { EventSequence = long.MaxValue }
            : checkpoint with { StateVersion = long.MaxValue }).Accepted);
        var before = sim.ExportCheckpoint();
        Assert.Equal(CookingRecipeRejectionReason.SupplyAllocationFailed,
            sim.Submit(Cmd(CookingRecipeOperation.ReceiveSupply, "receive", delivery: delivery)).Reason);
        var after = sim.ExportCheckpoint();
        Assert.Equal(before.CanonicalText(), (after with { Deduplication = before.Deduplication }).CanonicalText());
    }
    [Fact]
    public void Restored_supply_allocator_watermark_cannot_precede_materialized_identities()
    {
        var sim = new CookingRecipeSimulation(Fixture()); Receive(sim);
        var before = sim.ExportCheckpoint();
        var result = sim.RestoreCheckpoint(before with { NextProductId = 0 });
        Assert.False(result.Accepted);
        Assert.Equal(before.CanonicalText(), sim.ExportCheckpoint().CanonicalText());
    }
    [Fact]
    public void Physical_delivery_moves_finite_objects_then_cooks_and_submits_with_tombstone_provenance()
    {
        var sim = new CookingRecipeSimulation(Fixture());
        sim.AddItem(new("plate"), Plate, ItemLocation.World("plate"));
        var physical = Receive(sim);
        Assert.Equal(3, physical.Units.Count);
        var package = physical.Package!.Value;
        Accept(sim, Cmd(CookingRecipeOperation.Pickup, "pickup-box", item: package, version: 1));
        Accept(sim, Cmd(CookingRecipeOperation.Drop, "drop-box", item: package, version: 2, world: "storage"));
        var raw = physical.Units[0];
        Accept(sim, Cmd(CookingRecipeOperation.TakeOut, "take", item: raw, container: package, version: 1));
        Assert.Equal(raw, sim.ItemInHand(Chef));
        Accept(sim, Cmd(CookingRecipeOperation.Drop, "drop-raw", item: raw, station: Stove, version: 2));
        Accept(sim, Cmd(CookingRecipeOperation.StartProcess, "cook", item: raw, station: Stove, version: 3));
        sim.AdvanceFixedTick(Level, 3);
        var product = Assert.Single(sim.Snapshot().Items, i => i.IsProduct);
        Accept(sim, Cmd(CookingRecipeOperation.Pickup, "pickup-meal", item: product.Id, version: product.Version));
        Accept(sim, Cmd(CookingRecipeOperation.PutIn, "plate-meal", item: product.Id, container: new("plate"), version: product.Version + 1));
        Assert.True(sim.OpenOrder(new("order"), new("meal-order")).Accepted);
        Accept(sim, Cmd(CookingRecipeOperation.SubmitOrder, "submit", item: product.Id, order: new("order"), version: product.Version + 2));
        Assert.Single(sim.SettlementHistory);
        Assert.Equal(2, sim.ItemsInContainer(package).Count);
        Assert.True(sim.ExportCheckpoint().Items.Single(i => i.Id == raw).Removed);
        var restored = new CookingRecipeSimulation(Fixture());
        Assert.True(restored.RestoreCheckpoint(sim.ExportCheckpoint()).Accepted);
        Assert.Equal(sim.ExportCheckpoint().CanonicalText(), restored.ExportCheckpoint().CanonicalText());
        var sameCommand = Accept(restored, Cmd(CookingRecipeOperation.ReceiveSupply, "receive-r", delivery: physical.DeliveryId));
        Assert.Equal(physical.Units, sameCommand.Supply!.Units);
        var duplicate = Accept(restored, Cmd(CookingRecipeOperation.ReceiveSupply, "another-receive", delivery: physical.DeliveryId));
        Assert.True(duplicate.IsDuplicate);
        Assert.Equal(package, duplicate.Supply!.Package);
        Assert.Equal(physical.Units, duplicate.Supply.Units);
        Accept(sim, Cmd(CookingRecipeOperation.Pickup, "move-empty-package", item: package,
            version: sim.Snapshot().Items.Single(i => i.Id == package).Version));
        var second = Receive(sim, "second");
        Assert.DoesNotContain(second.Package!.Value, physical.Units);
        Assert.Equal(sim.Snapshot().Items.Count, sim.Snapshot().Items.Select(i => i.Id).Distinct().Count());
    }

    [Fact]
    public void Reservations_arrival_full_slot_exhaustion_and_semantic_duplicates_are_atomic()
    {
        var sim = new CookingRecipeSimulation(Fixture(stock: 3));
        var delivery = Accept(sim, Cmd(CookingRecipeOperation.RequestSupply, "request", request: "r")).Supply!.DeliveryId;
        var before = sim.Snapshot().CanonicalText();
        Assert.Equal(CookingRecipeOutcome.Rejected, sim.Submit(Cmd(CookingRecipeOperation.RequestSupply, "exhausted", request: "other")).Outcome);
        Assert.Equal(before, sim.Snapshot().CanonicalText());
        Assert.True(Accept(sim, Cmd(CookingRecipeOperation.RequestSupply, "retry-request", request: "r")).IsDuplicate);
        Assert.Equal(CookingRecipeOutcome.Rejected, sim.Submit(Cmd(CookingRecipeOperation.ReceiveSupply, "early", delivery: delivery)).Outcome);
        sim.AdvanceFixedTick(Level, 1);
        Assert.Equal(1, Assert.Single(sim.SupplySnapshot()!.Ledger.Deliveries).RemainingTicks);
        sim.AdvanceFixedTick(Level, 2);
        sim.AddItem(new("occupant"), Raw, ItemLocation.World("receiving"));
        before = sim.Snapshot().CanonicalText();
        Assert.Equal(CookingRecipeRejectionReason.ContainerFull, sim.Submit(Cmd(CookingRecipeOperation.ReceiveSupply, "full", delivery: delivery)).Reason);
        Assert.Equal(before, sim.Snapshot().CanonicalText());
        Accept(sim, Cmd(CookingRecipeOperation.Pickup, "free-slot", item: new("occupant"), version: 1));
        var original = Accept(sim, Cmd(CookingRecipeOperation.ReceiveSupply, "receive", delivery: delivery)).Supply!;
        before = sim.Snapshot().CanonicalText();
        var retry = Accept(sim, Cmd(CookingRecipeOperation.ReceiveSupply, "retry-receive", delivery: delivery));
        Assert.Equal(original.Package, retry.Supply!.Package);
        Assert.Equal(before, sim.Snapshot().CanonicalText());
    }

    private sealed class FaultAllocator(int fault) : ICookingProductIdAllocator
    {
        public ItemId GetProductId(long sequence) => fault switch {
            0 => sequence == 3 ? throw new InvalidOperationException("allocator fault") : new("allocated-" + sequence),
            1 => new("duplicate"), 2 => new("tombstone"), 3 => new(""),
            5 => sequence == 4 ? throw new InvalidOperationException("last allocator fault") : new("allocated-" + sequence),
            _ => new("id-" + sequence) };
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void Allocator_faults_duplicates_tombstones_blank_and_watermark_overflow_do_not_commit(int fault)
    {
        var sim = new CookingRecipeSimulation(Fixture(), new FaultAllocator(fault));
        sim.AddItem(new("tombstone"), Raw, ItemLocation.World("storage"));
        Accept(sim, Cmd(CookingRecipeOperation.DiscardItem, "discard", item: new("tombstone"), version: 1));
        var delivery = Accept(sim, Cmd(CookingRecipeOperation.RequestSupply, "request", request: "r")).Supply!.DeliveryId;
        sim.AdvanceFixedTick(Level, 1); sim.AdvanceFixedTick(Level, 2);
        if (fault == 4) Assert.True(sim.RestoreCheckpoint(sim.ExportCheckpoint() with { NextProductId = long.MaxValue }).Accepted);
        var checkpoint = sim.ExportCheckpoint();
        var result = sim.Submit(Cmd(CookingRecipeOperation.ReceiveSupply, "receive", delivery: delivery));
        Assert.Equal(CookingRecipeRejectionReason.SupplyAllocationFailed, result.Reason);
        var after = sim.ExportCheckpoint();
        Assert.Equal(checkpoint.CanonicalText(), (after with { Deduplication = checkpoint.Deduplication }).CanonicalText());
    }

    [Fact]
    public void Same_level_closing_pending_and_success_handoff_preserve_origins_and_reopen_only_handoff()
    {
        var sim = new CookingRecipeSimulation(Fixture(stock: 9));
        var physical = Receive(sim);
        Accept(sim, Cmd(CookingRecipeOperation.RequestSupply, "pending", request: "pending"));
        sim.StopNewSupplyRequests();
        var checkpoint = sim.ExportCheckpoint();
        var restored = new CookingRecipeSimulation(Fixture(stock: 9));
        Assert.True(restored.RestoreCheckpoint(checkpoint).Accepted);
        Assert.Equal(checkpoint.CanonicalText(), restored.ExportCheckpoint().CanonicalText());
        Assert.True(restored.SupplySnapshot()!.Ledger.Closing);
        Assert.Equal(CookingRecipeOutcome.Rejected, restored.Submit(Cmd(CookingRecipeOperation.RequestSupply, "closed", request: "new")).Outcome);
        var next = new CookingRecipeSimulation(Fixture(stock: 9));
        Assert.True(next.AcceptSuccessHandoff(sim.ExportSuccessHandoff()).Accepted);
        Assert.False(next.SupplySnapshot()!.Ledger.Closing);
        Assert.Equal(physical.Units, next.SupplySnapshot()!.Origins.Single().Units);
        Assert.Equal(2, next.SupplySnapshot()!.Ledger.Deliveries.Single(d => d.Phase == CookingDeliveryPhase.Pending).RemainingTicks);
        Accept(next, Cmd(CookingRecipeOperation.RequestSupply, "new", request: "new"));
    }

    [Theory]
    [InlineData("missing")] [InlineData("alias")] [InlineData("ghost")] [InlineData("identity")] [InlineData("schema")]
    public void Corrupt_supply_checkpoint_rejects_without_mutation(string kind)
    {
        var sim = new CookingRecipeSimulation(Fixture()); Receive(sim);
        var before = sim.ExportCheckpoint();
        var origin = before.SupplyOrigins!.Single();
        var corrupt = kind switch {
            "missing" => before with { SupplyOrigins = null },
            "alias" => before with { SupplyOrigins = new[] { origin with { Units = new[] { origin.Units[0], origin.Units[0], origin.Units[2] } } } },
            "ghost" => before with { SupplyOrigins = new[] { origin with { Package = new ItemId("ghost") } } },
            "identity" => before with { Supply = before.Supply! with { ConfigurationIdentity = "foreign" } },
            _ => before with { SchemaVersion = 4 } };
        Assert.False(sim.RestoreCheckpoint(corrupt).Accepted);
        Assert.Equal(before.CanonicalText(), sim.ExportCheckpoint().CanonicalText());
        var json = JsonSerializer.Serialize(before);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CookingRecipeCheckpoint>(json.Replace("\"SupplyOrigins\":", "\"omitted\":")));
    }

    [Fact]
    public void Infinite_take_creates_one_raw_and_retries_original_identity_after_movement_and_restore()
    {
        var sim = new CookingRecipeSimulation(Fixture(infinite: true));
        sim.StopNewSupplyRequests();
        var result = Accept(sim, Cmd(CookingRecipeOperation.TakeSupply, "take", request: "r"));
        var raw = Assert.Single(result.Supply!.Units);
        Accept(sim, Cmd(CookingRecipeOperation.Drop, "drop", item: raw, version: 1, world: "storage"));
        var restored = new CookingRecipeSimulation(Fixture(infinite: true));
        Assert.True(restored.RestoreCheckpoint(sim.ExportCheckpoint()).Accepted);
        var retry = Accept(restored, Cmd(CookingRecipeOperation.TakeSupply, "retry", request: "r"));
        Assert.True(retry.IsDuplicate);
        Assert.Equal(raw, Assert.Single(retry.Supply!.Units));
        Assert.Null(restored.ItemInHand(Chef));
        Accept(restored, Cmd(CookingRecipeOperation.TakeSupply, "new", request: "new"));
        var before = restored.Snapshot().CanonicalText();
        Assert.Equal(CookingRecipeOutcome.Rejected, restored.Submit(Cmd(CookingRecipeOperation.TakeSupply, "full", request: "full")).Outcome);
        Assert.Equal(before, restored.Snapshot().CanonicalText());
    }

    [Fact]
    public void Supply_configuration_requires_compatible_package_actual_anchors_and_frozen_source()
    {
        var fixture = Fixture();
        var mutable = fixture.Supply!.Suppliers.ToList();
        var frozen = new CookingRecipeFixture(Scope, fixture.Players, fixture.Items, fixture.Appliances, fixture.Recipes,
            spatial: fixture.Spatial, supply: new(mutable));
        mutable.Clear();
        Assert.Single(frozen.Supply!.Suppliers);
        Assert.Throws<ArgumentException>(() => new CookingRecipeSimulation(fixture with { Spatial = null }));
        var items = fixture.Items.ToDictionary(p => p.Key, p => p.Value);
        items[Box] = items[Box] with { Container = new(3, new HashSet<DefinitionId> { Product }) };
        Assert.Throws<ArgumentException>(() => new CookingRecipeSimulation(fixture with { Items = items }));
        var far = fixture.Spatial! with { InitialPoses = new[] { new CookingPlayerPose(Chef, -4000, 0, 1, 0) } };
        var sim = new CookingRecipeSimulation(fixture with { Spatial = far });
        Assert.Equal(CookingRecipeRejectionReason.TargetOutOfRange, sim.Submit(Cmd(CookingRecipeOperation.RequestSupply, "far", request: "r")).Reason);
        Assert.Empty(sim.SupplySnapshot()!.Ledger.Deliveries);
        Assert.False(CookingRecipeCommandValidation.IsWellFormed(Cmd(CookingRecipeOperation.RequestSupply, "bad", request: "r") with { Recipe = Recipe }));
        Assert.False(CookingRecipeCommandValidation.IsWellFormed(Cmd(CookingRecipeOperation.Pickup, "bad", item: new("x"), version: 1) with { SupplierId = "hidden" }));
    }

    [Fact]
    public void Origins_cannot_alias_ordinary_same_definition_items_or_leave_ghost_provenance()
    {
        var sim = new CookingRecipeSimulation(Fixture());
        sim.AddItem(new("ordinary"), Raw, ItemLocation.World("storage"));
        Receive(sim);
        var before = sim.ExportCheckpoint();
        var origin = before.SupplyOrigins!.Single();
        var corrupt = before with { SupplyOrigins = new[] { origin with {
            Units = new[] { new ItemId("ordinary"), origin.Units[1], origin.Units[2] } } } };
        Assert.False(sim.RestoreCheckpoint(corrupt).Accepted);
        Assert.Equal(before.CanonicalText(), sim.ExportCheckpoint().CanonicalText());
        corrupt = before with { Items = before.Items.Select(i => i.Id == new ItemId("ordinary") ? i with {
            SupplyProvenance = new(origin.RequestId, origin.SupplierId, origin.DeliveryId, 9) } : i).ToArray() };
        Assert.False(sim.RestoreCheckpoint(corrupt).Accepted);
        Assert.Equal(before.CanonicalText(), sim.ExportCheckpoint().CanonicalText());
    }

    [Fact]
    public void Supplied_package_used_for_retain_processing_restores_and_keeps_original_provenance()
    {
        var fixture = Fixture();
        var recipe = fixture.Recipes[Recipe] with { Inputs = new[] { Raw, Raw, Raw },
            Completion = CookingRecipeCompletionKind.RetainInputs, RequiredProcessingContainerDefinition = Box };
        var sim = new CookingRecipeSimulation(fixture with { Recipes = new Dictionary<RecipeId, CookingRecipeDefinition> { [Recipe] = recipe } });
        var physical = Receive(sim);
        Accept(sim, Cmd(CookingRecipeOperation.Pickup, "box", item: physical.Package, version: 1));
        Accept(sim, Cmd(CookingRecipeOperation.Drop, "stove", item: physical.Package, version: 2, station: Stove));
        Accept(sim, Cmd(CookingRecipeOperation.StartProcess, "cook", item: physical.Package, version: 3, station: Stove));
        sim.AdvanceFixedTick(Level, 3);
        Assert.True(sim.Snapshot().Items.Single(i => i.Id == physical.Package).ContainerCompleted);
        var next = new CookingRecipeSimulation(fixture with { Recipes = new Dictionary<RecipeId, CookingRecipeDefinition> { [Recipe] = recipe } });
        Assert.True(next.RestoreCheckpoint(sim.ExportCheckpoint()).Accepted);
        Assert.Equal(sim.ExportCheckpoint().CanonicalText(), next.ExportCheckpoint().CanonicalText());
    }

    [Fact]
    public void Content_supply_is_validated_frozen_and_part_of_canonical_identity()
    {
        var fixture = Fixture();
        CookingContentDocument Document(long stock) => new(CookingConfigurationIdentity.CurrentSchema,
            new[] { "heat" }, fixture.Items.Values.Select(i => new CookingContentItem(i.Id.Value,
                i.AllowedPlayerCapabilities.ToArray(), i.Container is null ? null : new CookingContentContainer(i.Container.Capacity,
                    i.Container.AcceptedDefinitions.Select(d => d.Value).ToArray()))).ToArray(),
            new[] { new CookingContentAppliance("stove", new[] { "heat" }) },
            new[] { new CookingContentRecipe("cook", new[] { "raw" }, "meal", "heat", "heat", 1) },
            Array.Empty<CookingContentOrderTemplate>(), Array.Empty<CookingContentSupplyEntry>(), fixture.Spatial)
            { Supply = new(new[] { fixture.Supply!.Suppliers.Single() with { FiniteAvailable = stock } }) };
        var content = CookingContentCatalog.Load(Document(6));
        Assert.NotEqual(content.Identity, CookingContentCatalog.Load(Document(9)).Identity);
        var frozen = CookingContentCatalog.BuildFixture(content, Scope, fixture.Players);
        Assert.Equal(6, frozen.Supply!.Suppliers.Single().FiniteAvailable);
        Assert.Throws<ArgumentException>(() => CookingContentCatalog.Load(Document(6) with { Supply = new(new[] {
            fixture.Supply!.Suppliers.Single() with { ReceivingAnchor = "missing" } }) }));
    }

    [Fact]
    public void Restored_dedup_physical_result_is_frozen_and_nested_supply_fields_are_required()
    {
        var sim = new CookingRecipeSimulation(Fixture()); Receive(sim);
        var json = JsonSerializer.Serialize(sim.ExportCheckpoint());
        var checkpoint = JsonSerializer.Deserialize<CookingRecipeCheckpoint>(json)!;
        var next = new CookingRecipeSimulation(Fixture());
        Assert.True(next.RestoreCheckpoint(checkpoint).Accepted);
        var before = next.ExportCheckpoint().CanonicalText();
        var receipt = checkpoint.Deduplication.Single(d => d.Command.Value == "receive-r");
        Assert.IsAssignableFrom<IList<ItemId>>(receipt.Supply!.Units)[0] = new("tampered");
        var retry = Accept(next, Cmd(CookingRecipeOperation.ReceiveSupply, "receive-r", delivery: "delivery:1"));
        Assert.NotEqual(new ItemId("tampered"), retry.Supply!.Units[0]);
        Assert.Equal(before, next.ExportCheckpoint().CanonicalText());
        foreach (var field in new[] { "UnitIndex", "DeliveryId", "Closing", "Package", "AllocationSequence" })
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CookingRecipeCheckpoint>(json.Replace("\"" + field + "\":", "\"missing-" + field + "\":")));
    }

    [Fact]
    public void Kitchen_fixed_tick_allocator_failure_does_not_advance_supply_candidate()
    {
        var fixture = Fixture() with { Recipes = new Dictionary<RecipeId, CookingRecipeDefinition> {
            [Recipe] = Fixture().Recipes[Recipe] with { RequiredTicks = 2 } } };
        var sim = new CookingRecipeSimulation(fixture);
        sim.AddItem(new("raw"), Raw, ItemLocation.Station(Stove));
        Accept(sim, Cmd(CookingRecipeOperation.StartProcess, "cook", item: new("raw"), version: 1, station: Stove));
        sim.AdvanceFixedTick(Level, 1);
        Accept(sim, Cmd(CookingRecipeOperation.RequestSupply, "request", request: "pending"));
        Assert.True(sim.RestoreCheckpoint(sim.ExportCheckpoint() with { NextProductId = long.MaxValue }).Accepted);
        var before = sim.ExportCheckpoint().CanonicalText();
        Assert.Throws<OverflowException>(() => sim.AdvanceFixedTick(Level, 2));
        Assert.Equal(before, sim.ExportCheckpoint().CanonicalText());
        Assert.Equal(2, sim.SupplySnapshot()!.Ledger.Deliveries.Single().RemainingTicks);
    }

    [Fact]
    public void Distinct_request_payload_conflict_and_closed_scope_do_not_reserve_or_allocate()
    {
        var fixture = Fixture();
        fixture = fixture with { Supply = new(new[] { fixture.Supply!.Suppliers.Single(),
            fixture.Supply.Suppliers.Single() with { SupplierId = "other" } }) };
        var sim = new CookingRecipeSimulation(fixture);
        Accept(sim, Cmd(CookingRecipeOperation.RequestSupply, "request", request: "same"));
        var before = sim.Snapshot().CanonicalText();
        Assert.Equal(CookingRecipeOutcome.Rejected, sim.Submit(Cmd(CookingRecipeOperation.RequestSupply, "conflict", request: "same") with { SupplierId = "other" }).Outcome);
        Assert.Equal(before, sim.Snapshot().CanonicalText());
        Assert.Equal(CookingRecipeRejectionReason.ScopeMismatch, sim.Submit(Cmd(CookingRecipeOperation.RequestSupply, "scope", request: "new") with {
            Scope = Scope with { Match = new("foreign") } }).Reason);
        Assert.Equal(before, sim.Snapshot().CanonicalText());
        var unavailable = new CookingRecipeSimulation(fixture with { Players = new Dictionary<PlayerId, CookingPlayerConfig> {
            [Chef] = fixture.Players[Chef] with { Capabilities = new HashSet<string> { "spectator" } } } });
        Assert.Equal(CookingRecipeRejectionReason.PlayerIneligible, unavailable.Submit(Cmd(CookingRecipeOperation.RequestSupply, "permission", request: "new")).Reason);
        Assert.Empty(unavailable.SupplySnapshot()!.Ledger.Deliveries);
    }
}
