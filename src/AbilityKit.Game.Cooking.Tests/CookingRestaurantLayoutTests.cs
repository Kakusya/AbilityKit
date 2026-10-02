using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingRestaurantLayoutTests
{
    private static readonly DefinitionId Counter = new("counter");
    private static readonly IReadOnlySet<DefinitionId> Allowed = new HashSet<DefinitionId> { Counter };

    [Fact]
    public void A_furniture_unlock_does_not_override_the_current_level_allow_list()
    {
        var result = CookingRestaurantLayoutValidator.Validate(Layout(), Allowed, new HashSet<DefinitionId>());
        Assert.False(result.Accepted);
        Assert.Equal(CookingLayoutRejectionReason.DefinitionUnavailable, result.Reason);
        Assert.Equal("counter", result.Target);
    }

    [Fact]
    public void Blocking_the_only_customer_exit_rejects_the_whole_layout()
    {
        var layout = Layout() with { Walls = Enumerable.Range(0, 6).Select(y => new CookingLayoutCell(3, y)).ToArray() };
        var result = CookingRestaurantLayoutValidator.Validate(layout, Allowed, Allowed);
        Assert.False(result.Accepted);
        Assert.Equal(CookingLayoutRejectionReason.UnreachableTarget, result.Reason);
        Assert.Null(result.Layout);
    }

    [Theory]
    [InlineData(0, 2, 1)]
    [InlineData(90, 3, 2)]
    [InlineData(180, 3, 3)]
    [InlineData(270, 1, 3)]
    public void Rotation_changes_a_non_square_devices_interaction_side(int rotation, int x, int y)
    {
        var equipment = new CookingEquipmentPlacement(new("stove"), Counter, new(2, 2), 2, 1, rotation, 0, -1);
        Assert.Equal(new CookingLayoutCell(x, y), CookingRestaurantLayoutValidator.InteractionCell(equipment));
    }

    [Fact]
    public void Overlapping_footprints_and_hidden_interaction_faces_are_rejected()
    {
        var layout = Layout();
        var second = layout.Equipment[0] with { Station = new("other") };
        Assert.Equal(CookingLayoutRejectionReason.OccupancyConflict,
            CookingRestaurantLayoutValidator.Validate(layout with { Equipment = new[] { layout.Equipment[0], second } }, Allowed, Allowed).Reason);
        Assert.Equal(CookingLayoutRejectionReason.UnreachableTarget,
            CookingRestaurantLayoutValidator.Validate(layout with { Walls = new[] { new CookingLayoutCell(2, 1) } }, Allowed, Allowed).Reason);
    }

    [Fact]
    public void Expansion_can_reconnect_a_disconnected_service_area_without_moving_inventory()
    {
        var split = Layout() with
        {
            Floors = new[] { new CookingFloorRegion("main", 0, 0, 3, 6), new CookingFloorRegion("service", 4, 0, 2, 6) },
        };
        Assert.Equal(CookingLayoutRejectionReason.UnreachableTarget,
            CookingRestaurantLayoutValidator.Validate(split, Allowed, Allowed).Reason);
        var expanded = split with { Floors = split.Floors.Append(new CookingFloorRegion("bridge", 3, 0, 1, 6)).ToArray() };
        var result = CookingRestaurantLayoutValidator.Validate(expanded, Allowed, Allowed);
        Assert.True(result.Accepted);
        Assert.NotEmpty(CookingRestaurantLayoutValidator.FindPath(result.Layout!, new(0, 0), new(5, 5)));
    }

    [Fact]
    public void Accepted_layout_is_frozen_and_canonical_order_does_not_depend_on_collection_order()
    {
        var layout = Layout();
        var walls = new List<CookingLayoutCell> { new(4, 1), new(4, 2) };
        var candidate = layout with { Walls = walls };
        var result = CookingRestaurantLayoutValidator.Validate(candidate, Allowed, Allowed);
        Assert.True(result.Accepted);
        var hash = result.Layout!.Sha256();
        walls.Clear();
        Assert.Equal(hash, result.Layout.Sha256());
        Assert.Equal(hash, (result.Layout with { Walls = result.Layout.Walls.Reverse().ToArray(), Targets = result.Layout.Targets.Reverse().ToArray() }).Sha256());
    }

    [Fact]
    public void Shortest_routes_are_stable_and_never_walk_through_equipment()
    {
        var layout = Layout();
        Assert.True(CookingRestaurantLayoutValidator.Validate(layout, Allowed, Allowed).Accepted);
        var path = CookingRestaurantLayoutValidator.FindPath(layout, new(0, 0), new(5, 5));
        Assert.Equal(new CookingLayoutCell(0, 0), path[0]);
        Assert.Equal(new CookingLayoutCell(5, 5), path[^1]);
        Assert.Equal(11, path.Count);
        Assert.DoesNotContain(new CookingLayoutCell(2, 2), path);
        Assert.Equal(path.ToArray(), CookingRestaurantLayoutValidator.FindPath(layout, new(0, 0), new(5, 5)).ToArray());
    }

    [Fact]
    public void Placement_cannot_shrink_the_content_definitions_footprint()
    {
        var definitions = new Dictionary<DefinitionId, CookingEquipmentFootprint>
        {
            [Counter] = new(Counter, 2, 1, 0, -1),
        };
        var layout = Layout();
        var rejected = CookingRestaurantLayoutValidator.Validate(layout, Allowed, Allowed, definitions);
        Assert.False(rejected.Accepted);
        Assert.Equal(CookingLayoutRejectionReason.InvalidLayout, rejected.Reason);
        var correct = layout with { Equipment = new[] { layout.Equipment[0] with { Width = 2 } } };
        Assert.True(CookingRestaurantLayoutValidator.Validate(correct, Allowed, Allowed, definitions).Accepted);
    }

    [Fact]
    public void A_cell_center_route_is_rejected_when_the_actor_cannot_fit_through_it()
    {
        var layout = Layout() with
        {
            Floors = new[] { new CookingFloorRegion("floor", 0, 0, 7, 7) },
            Equipment = Array.Empty<CookingEquipmentPlacement>(),
            Walls = Enumerable.Range(0, 7).Where(y => y != 3).Select(y => new CookingLayoutCell(3, y)).ToArray(),
            Targets = new[]
            {
                new CookingLayoutTarget("player", CookingLayoutTargetKind.PlayerEntrance, new(1, 3)),
                new CookingLayoutTarget("customer", CookingLayoutTargetKind.CustomerEntrance, new(1, 1)),
                new CookingLayoutTarget("table", CookingLayoutTargetKind.Table, new(5, 3)),
                new CookingLayoutTarget("exit", CookingLayoutTargetKind.Exit, new(5, 5)),
            },
        };
        Assert.True(CookingRestaurantLayoutValidator.Validate(layout, Allowed, Allowed).Accepted);
        Assert.NotEmpty(CookingRestaurantLayoutValidator.FindPath(layout, new(1, 3), new(5, 3)));
        var larger = layout with { ActorRadius = 600 };
        Assert.Equal(CookingLayoutRejectionReason.UnreachableTarget,
            CookingRestaurantLayoutValidator.Validate(larger, Allowed, Allowed).Reason);
        Assert.Empty(CookingRestaurantLayoutValidator.FindPath(larger, new(1, 3), new(5, 3)));
        Assert.NotEqual(layout.Sha256(), larger.Sha256());
    }

    [Fact]
    public void Clearance_uses_floor_division_for_negative_coordinates()
    {
        var layout = Layout();
        var shifted = layout with
        {
            Floors = new[] { new CookingFloorRegion("floor", -6, -6, 6, 6) },
            Equipment = layout.Equipment.Select(item => item with { Cell = new(item.Cell.X - 6, item.Cell.Y - 6) }).ToArray(),
            Targets = layout.Targets.Select(item => item with { Cell = new(item.Cell.X - 6, item.Cell.Y - 6) }).ToArray(),
        };
        Assert.True(CookingRestaurantLayoutValidator.Validate(shifted, Allowed, Allowed).Accepted);
        Assert.NotEmpty(CookingRestaurantLayoutValidator.FindPath(shifted, new(-6, -6), new(-1, -1)));
    }

    private static CookingRestaurantLayout Layout() => new(new("restaurant"),
        new[] { new CookingFloorRegion("floor", 0, 0, 6, 6) },
        new[] { new CookingEquipmentPlacement(new("counter"), Counter, new(2, 2), 1, 1, 0, 0, -1) },
        Array.Empty<CookingLayoutCell>(),
        new[]
        {
            new CookingLayoutTarget("player", CookingLayoutTargetKind.PlayerEntrance, new(0, 0)),
            new CookingLayoutTarget("customer", CookingLayoutTargetKind.CustomerEntrance, new(5, 0)),
            new CookingLayoutTarget("table", CookingLayoutTargetKind.Table, new(5, 3)),
            new CookingLayoutTarget("exit", CookingLayoutTargetKind.Exit, new(5, 5)),
            new CookingLayoutTarget("receiving", CookingLayoutTargetKind.Receiving, new(0, 5)),
        });
}
