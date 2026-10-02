using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingPreparedGeometryTests
{
    private static readonly PlayerId Player = new("a");
    private static readonly DefinitionId Raw = new("raw");
    private static CookingLayoutGeometryResult Project(int sourceX, bool includeSource = true, string sourceId = "source") =>
        CookingLayoutGeometry.Project(new(new("layout"), new[] { new CookingFloorRegion("floor", 0, 0, 6, 6) },
            Array.Empty<CookingEquipmentPlacement>(), Array.Empty<CookingLayoutCell>(),
            new[] { new CookingLayoutTarget("entrance", CookingLayoutTargetKind.PlayerEntrance, new(0, 0)),
                new("customer", CookingLayoutTargetKind.CustomerEntrance, new(0, 1)),
                new("exit", CookingLayoutTargetKind.Exit, new(0, 2)) }
                .Concat(includeSource ? new[] { new CookingLayoutTarget(sourceId, CookingLayoutTargetKind.Storage, new(sourceX, 0)) } : Array.Empty<CookingLayoutTarget>()).ToArray()),
            new HashSet<DefinitionId>(), new HashSet<DefinitionId>(), new Dictionary<DefinitionId, CookingEquipmentFootprint>(),
            new(250, 1400, 700), new[] { new CookingPlayerPose(Player, 500, 500, 1, 0) });

    private static CookingRecipeSimulation Kitchen(bool includeEmptyStation = false)
    {
        var projection = Project(3);
        Assert.True(projection.Accepted);
        var geometry = projection.Geometry!;
        var stations = new Dictionary<StationSlotId, CookingApplianceDefinition>();
        if (includeEmptyStation)
        {
            var station = new StationSlotId("empty-station");
            stations.Add(station, new(station, new HashSet<string> { "cook" }));
            geometry = geometry with { Anchors = geometry.Anchors.Append(new(LocationKind.StationSlot, station.Value, 4500, 1500)).ToArray() };
        }
        var simulation = new CookingRecipeSimulation(new CookingRecipeFixture(new(new("s"), new("w"), new("m")),
            new Dictionary<PlayerId, CookingPlayerConfig> { [Player] = new(Player, new HashSet<string> { "cook" }, new HashSet<string>()) },
            new Dictionary<DefinitionId, CookingItemDefinition> { [Raw] = new(Raw, new HashSet<string> { "cook" }) },
            stations, new Dictionary<RecipeId, CookingRecipeDefinition>(), spatial: geometry));
        simulation.AddWorldIngredient(new("raw-1"), Raw, "source");
        return simulation;
    }

    [Fact]
    public void Installed_geometry_changes_actual_pickup_reach_without_replacing_the_object()
    {
        var kitchen = Kitchen();
        var command = new CookingRecipeCommand(new CookingScope(new("s"), new("w"), new("m")), 1, Player, new("far"), CookingRecipeOperation.Pickup,
            Item: new("raw-1"), ExpectedItemVersion: 1);
        Assert.Equal(CookingRecipeRejectionReason.TargetOutOfRange, kitchen.Submit(command).Reason);
        Assert.True(kitchen.InstallPreparedGeometry(Project(1)));
        Assert.Equal(1500, kitchen.SpatialConfiguration!.Anchors.Single(a => a.Id == "source").X);
        Assert.Equal(CookingRecipeOutcome.Accepted, kitchen.Submit(command with { Command = new("near") }).Outcome);
        Assert.Equal(new ItemId("raw-1"), kitchen.ItemInHand(Player));
    }

    [Fact]
    public void Missing_live_object_anchor_rejects_the_whole_installation_without_moving_pose_or_object()
    {
        var kitchen = Kitchen();
        var before = kitchen.ExportCheckpoint().CanonicalText();
        Assert.False(kitchen.InstallPreparedGeometry(Project(1, false)));
        Assert.Equal(before, kitchen.ExportCheckpoint().CanonicalText());
        Assert.Equal(3500, kitchen.SpatialConfiguration!.Anchors.Single(a => a.Id == "source").X);
    }

    [Fact]
    public void Missing_an_empty_configured_appliance_anchor_also_rejects_without_mutation()
    {
        var kitchen = Kitchen(includeEmptyStation: true);
        var before = kitchen.ExportCheckpoint().CanonicalText();
        Assert.False(kitchen.InstallPreparedGeometry(Project(1)));
        Assert.Equal(before, kitchen.ExportCheckpoint().CanonicalText());
    }

    [Fact]
    public void Restore_projection_validates_saved_references_instead_of_discarded_factory_items()
    {
        var kitchen = Kitchen();
        var saved = kitchen.ExportCheckpoint();
        saved = saved with { Items = saved.Items.Select(i => i with { Location = ItemLocation.World("new-source") }).ToArray() };
        var geometry = Project(1, sourceId: "new-source");
        Assert.False(kitchen.CanInstallPreparedGeometry(geometry));
        Assert.True(kitchen.InstallPreparedGeometry(geometry, saved));
        Assert.True(kitchen.RestoreCheckpoint(saved).Accepted);
        Assert.Equal("new-source", Assert.Single(kitchen.Snapshot().Items).Location.SlotId);
        Assert.Equal(saved.CanonicalText(), kitchen.ExportCheckpoint().CanonicalText());
    }

    [Fact]
    public void Explicit_layout_gate_allows_preparing_gameplay_but_rejects_a_closed_layout_phase()
    {
        var kitchen = Kitchen();
        var gate = new LayoutGate();
        kitchen.BindLifecycleGate(gate);
        Assert.True(kitchen.InstallPreparedGeometry(Project(1)));
        gate.IsLayoutInstallationOpen = false;
        var before = kitchen.ExportCheckpoint().CanonicalText();
        Assert.False(kitchen.InstallPreparedGeometry(Project(2)));
        Assert.Equal(before, kitchen.ExportCheckpoint().CanonicalText());
    }

    private sealed class LayoutGate : ICookingRecipeLifecycleGate
    {
        public bool IsGameplayMutationOpen => true;
        public bool IsLayoutInstallationOpen { get; set; } = true;
    }
}
