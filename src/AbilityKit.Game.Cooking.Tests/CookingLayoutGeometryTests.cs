using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingLayoutGeometryTests
{
    private static readonly DefinitionId Counter = new("counter");
    private static readonly PlayerId A = new("a");
    private static readonly IReadOnlySet<DefinitionId> Allowed = new HashSet<DefinitionId> { Counter };
    private static readonly IReadOnlyDictionary<DefinitionId, CookingEquipmentFootprint> Footprints =
        new Dictionary<DefinitionId, CookingEquipmentFootprint> { [Counter] = new(Counter, 2, 1, 0, -1) };
    private static readonly CookingLayoutGeometryPolicy Policy = new(250, 1400, 700);
    private static CookingRestaurantLayout Layout() => new(new("layout"),
        new[] { new CookingFloorRegion("floor", 0, 0, 6, 6) },
        Array.Empty<CookingEquipmentPlacement>(), Array.Empty<CookingLayoutCell>(),
        new[] { new CookingLayoutTarget("player", CookingLayoutTargetKind.PlayerEntrance, new(0, 0)),
            new("customer", CookingLayoutTargetKind.CustomerEntrance, new(0, 1)),
            new("exit", CookingLayoutTargetKind.Exit, new(0, 2)) });
    private static CookingLayoutGeometryResult Project(CookingRestaurantLayout layout,
        IReadOnlyList<CookingPlayerPose>? poses = null) => CookingLayoutGeometry.Project(
        layout, Allowed, Allowed, Footprints, Policy, poses ?? new[] { new CookingPlayerPose(A, 500, 500, 1, 0) });

    [Fact]
    public void Geometry_is_valid_factory_input_while_runtime_projection_preserves_same_tick_move_limit()
    {
        var result = Project(Layout(), new[] { new CookingPlayerPose(A, 500, 500, 1, 0, 0) });
        Assert.True(result.Accepted);
        Assert.Equal(-1, Assert.Single(result.Geometry!.InitialPoses).LastMovementTick);
        Assert.Equal(0, Assert.Single(result.ProjectedPoses).LastMovementTick);
        var players = new Dictionary<PlayerId, CookingPlayerConfig> {
            [A] = new(A, new HashSet<string> { "cook" }, new HashSet<string>()) };
        var stations = new Dictionary<StationSlotId, CookingApplianceDefinition>();
        result.Geometry.Validate(players, stations);
        var scope = new CookingScope(new("s"), new("w"), new("m"));
        var simulation = new CookingRecipeSimulation(new CookingRecipeFixture(scope, players,
            new Dictionary<DefinitionId, CookingItemDefinition>(), stations,
            new Dictionary<RecipeId, CookingRecipeDefinition>(), spatial: result.Geometry));
        Assert.True(simulation.RestoreCheckpoint(simulation.ExportCheckpoint() with { Poses = result.ProjectedPoses }).Accepted);
        var before = simulation.Snapshot().CanonicalText();
        var moved = simulation.Submit(new(scope, 1, A, new("same-tick"), CookingRecipeOperation.Move, MoveX: 1));
        Assert.Equal(CookingRecipeRejectionReason.MovementBlocked, moved.Reason);
        Assert.Equal(before, simulation.Snapshot().CanonicalText());
    }
    [Fact]
    public void Floor_hole_is_a_real_S01_obstacle_and_blocks_a_continuous_move()
    {
        var layout = Layout() with { Floors = new[] {
            new CookingFloorRegion("top", 0, 0, 6, 2), new("bottom", 0, 3, 6, 3),
            new("left", 0, 2, 2, 1), new("right", 3, 2, 3, 1) } };
        var projected = Project(layout, new[] { new CookingPlayerPose(A, 1500, 2500, 1, 0) });
        Assert.True(projected.Accepted);
        var geometry = projected.Geometry!;
        Assert.False(geometry.ValidPose(new(A, 2500, 2500, 1, 0)));
        var scope = new CookingScope(new("s"), new("w"), new("m"));
        var simulation = new CookingRecipeSimulation(new CookingRecipeFixture(scope,
            new Dictionary<PlayerId, CookingPlayerConfig> { [A] = new(A, new HashSet<string> { "cook" }, new HashSet<string>()) },
            new Dictionary<DefinitionId, CookingItemDefinition>(), new Dictionary<StationSlotId, CookingApplianceDefinition>(),
            new Dictionary<RecipeId, CookingRecipeDefinition>(), spatial: geometry));
        var before = simulation.Snapshot().CanonicalText();
        var result = simulation.Submit(new(scope, 1, A, new("move"), CookingRecipeOperation.Move, MoveX: 1));
        Assert.Equal(CookingRecipeRejectionReason.MovementBlocked, result.Reason);
        Assert.Equal(before, simulation.Snapshot().CanonicalText());
    }

    [Theory]
    [InlineData(0, 2, 1, 4, 3)]
    [InlineData(90, 3, 2, 3, 4)]
    [InlineData(180, 3, 3, 4, 3)]
    [InlineData(270, 1, 3, 3, 4)]
    public void Non_square_rotation_projects_external_anchor_and_actual_obstacle(
        int rotation, int anchorX, int anchorY, int maxX, int maxY)
    {
        var layout = Layout() with { Equipment = new[] {
            new CookingEquipmentPlacement(new("station"), Counter, new(2, 2), 2, 1, rotation, 0, -1) } };
        var result = Project(layout);
        Assert.True(result.Accepted);
        var geometry = result.Geometry!;
        var anchor = Assert.Single(geometry.Anchors, a => a.Kind == LocationKind.StationSlot);
        Assert.Equal(anchorX * 1000 + 500, anchor.X);
        Assert.Equal(anchorY * 1000 + 500, anchor.Y);
        Assert.Equal(new CookingSpatialObstacle(2000, 2000, maxX * 1000, maxY * 1000), Assert.Single(geometry.Obstacles));
        Assert.True(geometry.ValidPose(new(A, anchor.X, anchor.Y, 1, 0)));
        Assert.False(geometry.ValidPose(new(A, 2500, 2500, 1, 0)));
    }

    [Fact]
    public void Sparse_remote_floors_use_bounded_gap_slabs_including_negative_coordinates()
    {
        var layout = Layout() with { Floors = new[] { new CookingFloorRegion("home", -100, -100, 6, 6),
            new("remote", 2_000_000, 2_000_000, 1, 1) }, Targets = Layout().Targets
                .Select(t => t with { Cell = new(t.Cell.X - 100, t.Cell.Y - 100) }).ToArray() };
        var result = Project(layout, new[] { new CookingPlayerPose(A, -99500, -99500, 1, 0) });
        Assert.True(result.Accepted);
        Assert.Equal(-100000, result.Geometry!.MinX);
        Assert.Equal(2_000_001_000, result.Geometry.MaxY);
        Assert.InRange(result.Geometry.Obstacles.Count, 1, 10);
        Assert.False(result.Geometry.ValidPose(new(A, 0, 0, 1, 0)));
        Assert.True(result.Geometry.ValidPose(new(A, 2_000_000_500, 2_000_000_500, 1, 0)));
    }

    [Fact]
    public void Caller_cannot_reduce_radius_change_scale_shrink_equipment_or_invent_definitions()
    {
        Assert.False(Project(Layout() with { ActorRadius = 1 }).Accepted);
        Assert.False(Project(Layout() with { CellSize = 1 }).Accepted);
        var equipment = new CookingEquipmentPlacement(new("station"), Counter, new(2, 2), 2, 1, 0, 0, -1);
        Assert.False(Project(Layout() with { Equipment = new[] { equipment with { Width = 1 } } }).Accepted);
        Assert.False(CookingLayoutGeometry.Project(Layout() with { Equipment = new[] { equipment } }, Allowed, Allowed,
            new Dictionary<DefinitionId, CookingEquipmentFootprint>(), Policy, Array.Empty<CookingPlayerPose>()).Accepted);
        Assert.False(CookingLayoutGeometry.Project(Layout() with { Equipment = new[] { equipment } }, Allowed,
            new HashSet<DefinitionId>(), Footprints, Policy, Array.Empty<CookingPlayerPose>()).Accepted);
    }

    [Fact]
    public void Relocation_preserves_legal_poses_and_watermarks_and_is_player_order_independent()
    {
        var keep = new CookingPlayerPose(new("z"), 500, 500, 0, -1, 12);
        var move = new CookingPlayerPose(A, -1000, -1000, 1, 0, 7);
        var result = Project(Layout(), new[] { move, keep });
        Assert.True(result.Accepted);
        Assert.Equal(keep, Assert.Single(result.ProjectedPoses, p => p.Player == keep.Player));
        Assert.Equal(move with { X = 1500, Y = 500 }, Assert.Single(result.ProjectedPoses, p => p.Player == A));
        Assert.Equal(result.ProjectedPoses, Project(Layout(), new[] { keep, move }).ProjectedPoses);
        Assert.All(result.Geometry!.InitialPoses, p => Assert.True(result.Geometry.ValidPose(p)));
        Assert.False(result.Geometry.Overlap(result.Geometry.InitialPoses[0], result.Geometry.InitialPoses[1]));
    }

    [Theory]
    [InlineData(8, 8500)]
    [InlineData(-9, -8500)]
    public void Geometrically_legal_old_pose_on_an_island_relocates_to_the_entrance_and_work_region(int cell, int position)
    {
        var layout = Layout() with { Floors = Layout().Floors.Append(new CookingFloorRegion("island", cell, cell, 2, 2)).ToArray(),
            Equipment = new[] { new CookingEquipmentPlacement(new("station"), Counter, new(2, 2), 2, 1, 0, 0, -1) } };
        var oldPose = new CookingPlayerPose(A, position, position, 1, 0, 3);
        var result = Project(layout, new[] { oldPose });
        Assert.True(result.Accepted);
        // Bounds/obstacles alone allow this old pose: it must still fail the entrance connectivity check.
        Assert.True(result.Geometry!.ValidPose(oldPose));
        Assert.Equal(oldPose with { X = 500, Y = 500 }, Assert.Single(result.ProjectedPoses));
        Assert.Equal(-1, Assert.Single(result.Geometry.InitialPoses).LastMovementTick);
        Assert.NotEmpty(CookingRestaurantLayoutValidator.FindPath(result.FrozenLayout!, new(0, 0), new(2, 1)));
    }
    [Fact]
    public void Invalid_pose_cannot_relocate_into_a_disconnected_room()
    {
        var layout = Layout() with { Floors = new[] { new CookingFloorRegion("home", 0, 0, 1, 1),
            new("remote", 3, 3, 2, 2) }, Targets = Layout().Targets.Select(t => t with { Cell = new(0, 0) }).ToArray() };
        var result = Project(layout, new[] { new CookingPlayerPose(A, 500, 500, 1, 0),
            new CookingPlayerPose(new("b"), -500, -500, 1, 0) });
        Assert.False(result.Accepted);
        Assert.Equal(CookingLayoutRejectionReason.UnreachableTarget, result.Validation.Reason);
        Assert.Null(result.Geometry);
        Assert.Null(result.FrozenLayout);
    }

    [Fact]
    public void Projection_freezes_source_and_keeps_trusted_reach_speed_and_world_anchors()
    {
        var floors = Layout().Floors.ToList();
        var walls = new List<CookingLayoutCell> { new(3, 3) };
        var poses = new List<CookingPlayerPose> { new(A, 500, 500, 1, 0) };
        var result = Project(Layout() with { Floors = floors, Walls = walls }, poses);
        Assert.True(result.Accepted);
        var hash = result.FrozenLayout!.Sha256();
        floors.Clear(); walls.Clear(); poses.Clear();
        Assert.Equal(hash, result.FrozenLayout.Sha256());
        Assert.Single(result.Geometry!.Obstacles);
        Assert.Single(result.Geometry.InitialPoses);
        Assert.Equal(Policy.PlayerRadius, result.Geometry.PlayerRadius);
        Assert.Equal(Policy.InteractionRadius, result.Geometry.InteractionRadius);
        Assert.Equal(Policy.MovementSpeed, result.Geometry.MovementSpeed);
        Assert.Contains(new CookingSpatialAnchor(LocationKind.WorldPosition, "exit", 500, 2500), result.Geometry.Anchors);
    }

    [Fact]
    public void Touching_a_closed_obstacle_is_invalid_but_outer_boundary_contact_is_preserved()
    {
        var layout = Layout() with { Walls = new[] { new CookingLayoutCell(2, 2) } };
        var pose = new CookingPlayerPose(A, 250, 500, 1, 0, 2);
        var result = Project(layout, new[] { pose });
        Assert.True(result.Accepted);
        Assert.Equal(pose, Assert.Single(result.ProjectedPoses));
        Assert.False(result.Geometry!.ValidPose(new(A, 1750, 2500, 1, 0)));
        Assert.True(result.Geometry.ValidPose(new(A, 1749, 2500, 1, 0)));
    }
}




