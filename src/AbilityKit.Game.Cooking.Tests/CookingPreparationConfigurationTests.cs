using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingPreparationConfigurationTests
{
    private static readonly DefinitionId Counter = new("counter"), Other = new("other");
    private static readonly StationSlotId Board = new("board");
    private static readonly PlayerId Player = new("player");
    private static CookingRestaurantLayout Layout() => new(new("layout"), new[] { new CookingFloorRegion("floor", 0, 0, 8, 6) },
        new[] { new CookingEquipmentPlacement(Board, Counter, new(3, 3), 1, 1, 0, 0, -1) }, Array.Empty<CookingLayoutCell>(),
        new[] { new CookingLayoutTarget("player", CookingLayoutTargetKind.PlayerEntrance, new(0, 0)),
            new("customer-entrance", CookingLayoutTargetKind.CustomerEntrance, new(0, 1)), new("queue", CookingLayoutTargetKind.Queue, new(1, 1)),
            new("table-1", CookingLayoutTargetKind.Table, new(5, 1)), new("exit", CookingLayoutTargetKind.Exit, new(7, 1)),
            new("washing", CookingLayoutTargetKind.Washing, new(2, 1)) }) { ActorRadius = 100 };
    private static CookingPreparationConfiguration Policy() => new(Layout(), new Dictionary<DefinitionId, CookingEquipmentFootprint> {
        [Counter] = new(Counter, 1, 1, 0, -1), [Other] = new(Other, 1, 1, 0, -1) }, new(100, 1500, 800),
        new HashSet<DefinitionId> { Counter }, new HashSet<DefinitionId> { Counter, Other },
        new Dictionary<StationSlotId, DefinitionId> { [Board] = Counter }, 2, 5);
    private static IReadOnlyDictionary<StationSlotId, CookingApplianceDefinition> Appliances() =>
        new Dictionary<StationSlotId, CookingApplianceDefinition> { [Board] = new(Board, new HashSet<string> { "work" }) };
    private static CookingLayoutGeometryResult Project(CookingPreparationConfiguration policy, CookingRestaurantLayout? layout = null) =>
        policy.Project(layout ?? policy.InitialLayout, new[] { new CookingPlayerPose(Player, 500, 500, 1, 0) }, Appliances());
    private static CookingFrontOfHouseConfiguration Front() => new(new(1, 20, 4, 2, 2, 3, 10), new[] { new OrderTemplateId("meal") },
        ManualPolicyIdentity: "manual-policy", DeliveryPolicy: new(CookingFrontDeliveryMode.CustomerTable));

    [Fact]
    public void Frozen_policy_deep_copies_permissions_bindings_and_initial_layout()
    {
        var policy = Policy(); var floors = policy.InitialLayout.Floors.ToList();
        var footprints = policy.TrustedFootprints.ToDictionary(x => x.Key, x => x.Value);
        var basic = policy.BaseAuthorized.ToHashSet(); var bindings = policy.StationBindings.ToDictionary(x => x.Key, x => x.Value);
        var frozen = (policy with { InitialLayout = policy.InitialLayout with { Floors = floors }, TrustedFootprints = footprints,
            BaseAuthorized = basic, StationBindings = bindings }).Freeze();
        var before = frozen.InitialLayout.CanonicalText();
        floors.Clear(); footprints.Clear(); basic.Clear(); bindings.Clear();
        Assert.Equal(before, frozen.InitialLayout.CanonicalText()); Assert.True(Project(frozen).Accepted);
    }
    [Fact]
    public void Legal_rotation_changes_only_geometry_and_keeps_configured_capabilities()
    {
        var policy = Policy().Freeze(); var appliances = Appliances();
        var moved = policy.InitialLayout with { Equipment = policy.InitialLayout.Equipment.Select(e => e with { Cell = new(4, 3), Rotation = 90 }).ToArray() };
        var projected = policy.Project(moved, new[] { new CookingPlayerPose(Player, 500, 500, 1, 0) }, appliances);
        Assert.True(projected.Accepted); Assert.Contains(projected.Geometry!.Anchors, a => a.Kind == LocationKind.StationSlot && a.Id == Board.Value);
        Assert.Equal(new[] { "work" }, appliances[Board].Capabilities);
    }
    [Theory]
    [InlineData("definition")]
    [InlineData("unknown-station")]
    [InlineData("missing-station")]
    [InlineData("footprint")]
    [InlineData("radius")]
    public void Layout_requests_cannot_forge_equipment_binding_or_geometry_policy(string poison)
    {
        var policy = Policy(); var layout = Layout();
        layout = poison switch {
            "definition" => layout with { Equipment = layout.Equipment.Select(e => e with { Definition = Other }).ToArray() },
            "unknown-station" => layout with { Equipment = layout.Equipment.Select(e => e with { Station = new("fake") }).ToArray() },
            "missing-station" => layout with { Equipment = Array.Empty<CookingEquipmentPlacement>() },
            "footprint" => layout with { Equipment = layout.Equipment.Select(e => e with { Width = 2 }).ToArray() },
            _ => layout with { ActorRadius = 1 } };
        Assert.False(Project(policy, layout).Accepted); Assert.True(Project(policy).Accepted);
    }
    [Fact]
    public void Initial_layout_must_be_basic_authorized_and_unlock_never_overrides_level_allowed()
    {
        var policy = Policy(); Assert.Throws<ArgumentException>(() => (policy with { BaseAuthorized = new HashSet<DefinitionId>() }).Freeze());
        var candidate = policy.InitialLayout with { Equipment = policy.InitialLayout.Equipment.Select(e => e with { Definition = Other }).ToArray() };
        var alternate = (policy with { StationBindings = new Dictionary<StationSlotId, DefinitionId> { [Board] = Other },
            InitialLayout = candidate, BaseAuthorized = new HashSet<DefinitionId> { Other }, LevelAllowed = new HashSet<DefinitionId> { Other } }).Freeze();
        Assert.True(Project(alternate).Accepted);
        Assert.Throws<ArgumentException>(() => (alternate with { LevelAllowed = new HashSet<DefinitionId> { Counter } }).Freeze());
    }
    [Fact]
    public void Derived_front_routes_share_geometry_and_preserve_all_trusted_service_policy()
    {
        var policy = Policy(); var projection = Project(policy); var front = Front();
        var derived = policy.DerivedFrontConfiguration(projection, front);
        Assert.Equal(front.Schedule, derived.Schedule); Assert.Equal(front.Menu, derived.Menu);
        Assert.Equal(front.DeliveryPolicy, derived.DeliveryPolicy); Assert.Equal(front.ManualPolicyIdentity, derived.ManualPolicyIdentity);
        Assert.Equal(CookingFrontOfHouseFlow.SpatialIdentity(projection.Geometry!), derived.Flow!.GeometryIdentity);
        Assert.Equal(2, derived.Flow.QueueCapacity); Assert.Equal(5, derived.Flow.ClearTableTicks);
        Assert.Equal(new CookingFrontPoint(0, 1), derived.Flow.EntranceToQueue[0]);
        Assert.Equal(new CookingFrontPoint(5, 1), derived.Flow.Tables.Single().QueueToTable[^1]);
        Assert.Equal(new CookingFrontPoint(7, 1), derived.Flow.Tables.Single().TableToExit[^1]);
    }
    [Theory]
    [InlineData("missing")]
    [InlineData("kind")]
    [InlineData("geometry")]
    public void Front_derivation_rejects_missing_wrong_kind_and_forged_geometry(string poison)
    {
        var policy = Policy(); var layout = Layout();
        if (poison == "missing") layout = layout with { Targets = layout.Targets.Where(t => t.Id != "queue").ToArray() };
        if (poison == "kind") layout = layout with { Targets = layout.Targets.Select(t => t.Id == "queue" ? t with { Kind = CookingLayoutTargetKind.Storage } : t).ToArray() };
        var projection = Project(policy, layout);
        if (poison == "geometry") projection = projection with { Geometry = projection.Geometry! with { InteractionRadius = 9999 } };
        Assert.Throws<ArgumentException>(() => policy.DerivedFrontConfiguration(projection, Front()));
    }
    [Fact]
    public void Additional_unlock_combines_with_basic_permissions_but_does_not_bypass_level_limits()
    {
        var extra = new StationSlotId("extra");
        var policy = Policy() with { StationBindings = new Dictionary<StationSlotId, DefinitionId> { [Board] = Counter, [extra] = Other } };
        var candidate = Layout() with { Equipment = Layout().Equipment.Append(new CookingEquipmentPlacement(extra, Other, new(5, 3), 1, 1, 0, 0, -1)).ToArray() };
        var appliances = Appliances().ToDictionary(p => p.Key, p => p.Value);
        appliances.Add(extra, new(extra, new HashSet<string> { "trusted-other" }));
        var poses = new[] { new CookingPlayerPose(Player, 500, 500, 1, 0) };
        Assert.False(policy.Project(candidate, poses, appliances).Accepted);
        Assert.True(policy.Project(candidate, poses, appliances, new HashSet<DefinitionId> { Other }).Accepted);
        Assert.False((policy with { LevelAllowed = new HashSet<DefinitionId> { Counter } })
            .Project(candidate, poses, appliances, new HashSet<DefinitionId> { Other }).Accepted);
    }    [Fact]
    public void Trusted_identity_ignores_collection_order_but_covers_actual_policy_changes()
    {
        var policy = Policy() with { BaseAuthorized = new HashSet<DefinitionId> { Counter, Other },
            StationBindings = new Dictionary<StationSlotId, DefinitionId> { [Board] = Counter, [new("unused")] = Other } };
        var reordered = policy with {
            TrustedFootprints = policy.TrustedFootprints.Reverse().ToDictionary(p => p.Key, p => p.Value),
            BaseAuthorized = policy.BaseAuthorized.Reverse().ToHashSet(), LevelAllowed = policy.LevelAllowed.Reverse().ToHashSet(),
            StationBindings = policy.StationBindings.Reverse().ToDictionary(p => p.Key, p => p.Value),
            InitialLayout = policy.InitialLayout with { Targets = policy.InitialLayout.Targets.Reverse().ToArray() } };
        Assert.Equal(policy.CanonicalText(), reordered.CanonicalText()); Assert.Equal(policy.Identity(), reordered.Identity());
        Assert.NotEqual(policy.Identity(), (policy with { BaseAuthorized = new HashSet<DefinitionId> { Counter } }).Identity());
        Assert.NotEqual(policy.Identity(), (policy with { GeometryPolicy = policy.GeometryPolicy with { InteractionRadius = 1700 } }).Identity());
        Assert.NotEqual(policy.Identity(), (policy with { FrontQueueCapacity = 3 }).Identity());
        var footprints = policy.TrustedFootprints.ToDictionary(p => p.Key, p => p.Value);
        footprints[Other] = footprints[Other] with { Width = 2 };
        Assert.NotEqual(policy.Identity(), (policy with { TrustedFootprints = footprints }).Identity());
    }
    [Theory]
    [InlineData(-1, 0, true)]
    [InlineData(2, 1, true)]
    [InlineData(1, -1, true)]
    [InlineData(0, 3, true)]
    [InlineData(-2, 0, false)]
    [InlineData(3, 1, false)]
    [InlineData(1, -2, false)]
    [InlineData(0, 4, false)]
    [InlineData(-1, -1, false)]
    [InlineData(0, 0, false)]
    public void Trusted_interaction_offset_is_one_adjacent_external_face_cell(int x, int y, bool valid)
    {
        var policy = Policy(); var footprints = policy.TrustedFootprints.ToDictionary(p => p.Key, p => p.Value);
        footprints[Other] = new(Other, 2, 3, x, y);
        policy = policy with { TrustedFootprints = footprints };
        if (valid) Assert.NotNull(policy.Freeze());
        else Assert.Throws<ArgumentException>(() => policy.Freeze());
    }}