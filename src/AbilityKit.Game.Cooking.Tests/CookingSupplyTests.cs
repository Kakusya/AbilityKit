using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingSupplyTests
{
    private static CookingSupplyConfiguration Configuration(long available = 6, int ticks = 2) => new(new[]
    {
        new CookingSupplierDefinition("vegetables", "stock", "receiving", new("tomato"), new("box"), 3, ticks, available),
        new CookingSupplierDefinition("water", "tap", "receiving", new("water"), new("jug"), 1, 0, Infinite: true)
    });

    [Fact]
    public void Requests_reserve_units_once_and_conflicts_or_exhaustion_change_nothing()
    {
        var state = new CookingSupplyState(Configuration());
        var first = state.Request("one", "vegetables");
        Assert.True(first.Accepted);
        Assert.Equal("delivery:1", first.DeliveryId);
        var canonical = state.CanonicalText();
        Assert.True(state.Request("one", "vegetables").Duplicate);
        Assert.Equal(CookingSupplyRejection.PayloadConflict, state.Request("one", "water").Reason);
        Assert.Equal(CookingSupplyRejection.InvalidRequest, state.Request("multi", "vegetables", 2).Reason);
        Assert.Equal(canonical, state.CanonicalText());
        Assert.True(state.Request("two", "vegetables").Accepted);
        canonical = state.CanonicalText();
        Assert.Equal(CookingSupplyRejection.InsufficientSupply, state.Request("three", "vegetables").Reason);
        Assert.Equal(canonical, state.CanonicalText());
        Assert.Equal(3, state.ExportCheckpoint().NextDeliverySequence);
        Assert.Equal(0, state.ExportCheckpoint().Balances.Single(x => x.SupplierId == "vegetables").AvailableUnits);
    }

    [Fact]
    public void Arrival_uses_only_explicit_ticks_and_preview_does_not_receive_a_full_slot()
    {
        var state = new CookingSupplyState(Configuration());
        var first = state.Request("one", "vegetables").DeliveryId!;
        var second = state.Request("two", "vegetables").DeliveryId!;
        Assert.Equal(CookingSupplyRejection.NotArrived, state.PreviewReceive(first, out _).Reason);
        state.AdvanceFixedTick();
        Assert.Equal(1, state.ExportCheckpoint().Deliveries[0].RemainingTicks);
        state.AdvanceFixedTick();
        Assert.True(state.PreviewReceive(first, out var firstPlan).Accepted);
        Assert.Equal(1, firstPlan!.PackageCount);
        Assert.Equal(3, firstPlan.UnitCount);
        Assert.True(state.CommitReceive(firstPlan).Accepted);
        var beforeFullSlot = state.CanonicalText();
        Assert.True(state.PreviewReceive(second, out var secondPlan).Accepted);
        Assert.Equal(beforeFullSlot, state.CanonicalText()); // Kitchen rejects occupied slot; no commit.
        Assert.Equal(CookingDeliveryPhase.Arrived, state.ExportCheckpoint().Deliveries[1].Phase);
        Assert.True(state.CommitReceive(secondPlan).Accepted); // Owner cleared its real slot first.
        var after = state.CanonicalText();
        Assert.Equal(CookingSupplyRejection.AlreadyReceived, state.CommitReceive(firstPlan).Reason);
        Assert.Equal(CookingSupplyRejection.AlreadyReceived, state.PreviewReceive(first, out _).Reason);
        Assert.Equal(after, state.CanonicalText());
    }

    [Fact]
    public void Closing_blocks_new_procurement_but_existing_deliveries_arrive_and_receive()
    {
        var state = new CookingSupplyState(Configuration());
        var id = state.Request("one", "vegetables").DeliveryId!;
        state.StopNewRequests();
        var before = state.CanonicalText();
        Assert.Equal(CookingSupplyRejection.Closing, state.Request("two", "vegetables").Reason);
        Assert.True(state.Request("one", "vegetables").Duplicate);
        Assert.Equal(before, state.CanonicalText());
        state.AdvanceFixedTick();
        state.AdvanceFixedTick();
        Assert.True(state.PreviewReceive(id, out var plan).Accepted);
        Assert.True(state.CommitReceive(plan).Accepted);
    }

    [Fact]
    public void Infinite_source_is_explicit_one_unit_and_allocates_only_after_owner_commit()
    {
        var state = new CookingSupplyState(Configuration());
        var before = state.CanonicalText();
        Assert.Equal(CookingSupplyRejection.InvalidRequest, state.Request("water-box", "water").Reason);
        Assert.Equal(CookingSupplyRejection.NotInfinite, state.PreviewInfiniteTake("tomato", "vegetables", out _).Reason);
        Assert.True(state.PreviewInfiniteTake("water-1", "water", out var plan).Accepted);
        Assert.Equal(1, plan!.UnitCount);
        Assert.Equal(before, state.CanonicalText()); // A full hand rejects creation without committing.
        Assert.True(state.CommitInfiniteTake(plan).Accepted);
        before = state.CanonicalText();
        Assert.True(state.CommitInfiniteTake(plan).Duplicate);
        Assert.True(state.PreviewInfiniteTake("water-1", "water", out var duplicate).Duplicate);
        Assert.Null(duplicate);
        Assert.Equal(CookingSupplyRejection.PayloadConflict, state.Request("water-1", "vegetables").Reason);
        Assert.Equal(before, state.CanonicalText());
        Assert.Equal(2, state.ExportCheckpoint().NextInfiniteSequence);
    }

    [Fact]
    public void Receipt_plan_cannot_be_committed_into_a_different_kitchen_or_restored_state()
    {
        var state = new CookingSupplyState(Configuration(ticks: 0));
        var id = state.Request("one", "vegetables").DeliveryId!;
        state.PreviewReceive(id, out var plan);
        Assert.True(CookingSupplyState.TryRestore(Configuration(ticks: 0), state.ExportCheckpoint(), out var restored));
        var before = restored!.CanonicalText();
        Assert.Equal(CookingSupplyRejection.InvalidPlan, restored.CommitReceive(plan).Reason);
        Assert.Equal(before, restored.CanonicalText());
        state.PreviewInfiniteTake("water", "water", out var water);
        Assert.Equal(CookingSupplyRejection.InvalidPlan, restored.CommitInfiniteTake(water).Reason);
    }

    [Fact]
    public void Pending_and_arrived_restore_then_continue_exactly_like_an_uninterrupted_ledger()
    {
        var state = new CookingSupplyState(Configuration());
        state.Request("one", "vegetables");
        state.AdvanceFixedTick();
        state.Request("two", "vegetables");
        state.PreviewInfiniteTake("water", "water", out var water);
        state.CommitInfiniteTake(water);
        Assert.True(CookingSupplyState.TryRestore(Configuration(), state.ExportCheckpoint(), out var restored));
        Assert.Equal(state.CanonicalText(), restored!.CanonicalText());
        foreach (var current in new[] { state, restored })
        {
            current.AdvanceFixedTick();
            current.PreviewReceive("delivery:1", out var first);
            current.CommitReceive(first);
            current.StopNewRequests();
            current.AdvanceFixedTick();
            current.PreviewReceive("delivery:2", out var second);
            current.CommitReceive(second);
        }
        Assert.Equal(state.CanonicalText(), restored.CanonicalText());
        Assert.True(CookingSupplyState.TryRestore(Configuration(), state.ExportCheckpoint(), out var received));
        Assert.Equal(state.Sha256(), received!.Sha256());
    }

    [Fact]
    public void Restore_rejects_forged_balances_sequences_receipts_and_configuration_changes()
    {
        var config = Configuration();
        var state = new CookingSupplyState(config);
        state.Request("one", "vegetables");
        var valid = state.ExportCheckpoint();
        var originalCanonical = state.CanonicalText();
        var invalid = new[]
        {
            valid with { ConfigurationIdentity = "wrong" },
            valid with { NextDeliverySequence = 1 },
            valid with { NextDeliverySequence = 3 },
            valid with { Balances = new[] { new CookingSupplierBalance("vegetables", 6), new CookingSupplierBalance("water", 0) } },
            valid with { Balances = new[] { new CookingSupplierBalance("vegetables", 3), new CookingSupplierBalance("water", 1) } },
            valid with { Deliveries = valid.Deliveries.Concat(valid.Deliveries).ToArray() },
            valid with { Deliveries = new[] { valid.Deliveries[0] with { Phase = CookingDeliveryPhase.Arrived } } },
            valid with { Deliveries = new[] { valid.Deliveries[0] with { PackageCount = 2 } } },
            valid with { Requests = Array.Empty<CookingSupplyRequestReceipt>() },
            valid with { Requests = new[] { valid.Requests[0] with { DeliveryId = "delivery:999" } } },
            valid with { NextInfiniteSequence = 2 },
            valid with { Requests = valid.Requests.Concat(new[] { new CookingSupplyRequestReceipt("ghost", "water", 0, null, 1) }).ToArray() }
        };
        foreach (var checkpoint in invalid)
        {
            Assert.False(CookingSupplyState.TryRestore(config, checkpoint, out var restored));
            Assert.Null(restored);
        }
        Assert.False(CookingSupplyState.TryRestore(Configuration(available: 9), valid, out _));
        Assert.Equal(originalCanonical, state.CanonicalText());
    }

    [Fact]
    public void Restore_rejects_two_request_receipts_aliasing_the_same_delivery()
    {
        var configuration = Configuration();
        var state = new CookingSupplyState(configuration);
        state.Request("one", "vegetables");
        var checkpoint = state.ExportCheckpoint();
        var before = state.CanonicalText();
        var forged = checkpoint with
        {
            Requests = checkpoint.Requests.Append(checkpoint.Requests[0] with { RequestId = "alias" }).ToArray(),
        };

        Assert.False(CookingSupplyState.TryRestore(configuration, forged, out var restored));
        Assert.Null(restored);
        Assert.Equal(before, state.CanonicalText());
        Assert.True(CookingSupplyState.TryRestore(configuration, checkpoint, out var valid));
        Assert.Equal(before, valid!.CanonicalText());
    }

    [Fact]
    public void Configuration_is_frozen_sorted_and_requires_authoritative_compatible_definitions()
    {
        var source = Configuration().Suppliers.ToList();
        var state = new CookingSupplyState(new(source));
        var hash = state.ConfigurationIdentity;
        source.Clear();
        Assert.Equal(2, state.Configuration.Suppliers.Count);
        Assert.Equal(hash, state.ConfigurationIdentity);
        Assert.Equal(hash, new CookingSupplyState(new(Configuration().Suppliers.Reverse().ToArray())).ConfigurationIdentity);
        var units = new HashSet<DefinitionId> { new("tomato"), new("water") };
        var packages = new Dictionary<DefinitionId, int> { [new("box")] = 3, [new("jug")] = 1 };
        var anchors = new HashSet<string> { "stock", "receiving", "tap" };
        Assert.True(state.ValidateDefinitions(units, packages, anchors));
        packages[new("box")] = 2;
        Assert.False(state.ValidateDefinitions(units, packages, anchors));
        packages[new("box")] = 3;
        anchors.Remove("receiving");
        Assert.False(state.ValidateDefinitions(units, packages, anchors));
        Assert.Throws<ArgumentException>(() => new CookingSupplyState(new(new[] { Configuration().Suppliers[0] with { UnitsPerPackage = 0 } })));
        Assert.Throws<ArgumentException>(() => new CookingSupplyState(new(new[] { Configuration().Suppliers[1] with { FiniteAvailable = 1 } })));
        Assert.Throws<ArgumentException>(() => new CookingSupplyState(new(new[] { Configuration().Suppliers[0], Configuration().Suppliers[0] })));
    }
}
