using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Frozen;

namespace AbilityKit.Game.Cooking;

/// <summary>Trusted per-Level preparation policy on the existing kitchen factory.</summary>
public interface ICookingPreparationGameplayFactory : ICookingLevelGameplayFactory
{
    CookingPreparationConfiguration CreatePreparationConfiguration(CookingLevelScope scope, CookingConfigurationSnapshot configuration);
}

/// <summary>Content policy only. Projection does not install geometry or advance any clock.</summary>
public sealed record CookingPreparationConfiguration(
    CookingRestaurantLayout InitialLayout,
    IReadOnlyDictionary<DefinitionId, CookingEquipmentFootprint> TrustedFootprints,
    CookingLayoutGeometryPolicy GeometryPolicy,
    IReadOnlySet<DefinitionId> BaseAuthorized,
    IReadOnlySet<DefinitionId> LevelAllowed,
    IReadOnlyDictionary<StationSlotId, DefinitionId> StationBindings,
    int FrontQueueCapacity, int FrontClearTableTicks)
{
    public CookingPreparationConfiguration Freeze()
    {
        if (InitialLayout is null || TrustedFootprints is null || GeometryPolicy is null || BaseAuthorized is null
            || LevelAllowed is null || StationBindings is null || FrontQueueCapacity <= 0 || FrontClearTableTicks <= 0
            || GeometryPolicy.PlayerRadius <= 0 || GeometryPolicy.InteractionRadius <= 0 || GeometryPolicy.MovementSpeed <= 0)
            throw new ArgumentException("Preparation policy is incomplete.");
        var footprints = TrustedFootprints.ToFrozenDictionary();
        if (footprints.Any(p => string.IsNullOrWhiteSpace(p.Key.Value) || p.Value is null || p.Value.Definition != p.Key
            || p.Value.Width <= 0 || p.Value.Height <= 0 || (long)p.Value.Width * p.Value.Height > CookingRestaurantLayoutValidator.MaximumCells
            || !AdjacentFace(p.Value)))
            throw new ArgumentException("Trusted equipment footprints are invalid.");
        var basic = BaseAuthorized.ToFrozenSet(); var allowed = LevelAllowed.ToFrozenSet();
        var bindings = StationBindings.ToFrozenDictionary();
        if (basic.Concat(allowed).Any(d => string.IsNullOrWhiteSpace(d.Value) || !footprints.ContainsKey(d))
            || bindings.Any(p => string.IsNullOrWhiteSpace(p.Key.Value) || !footprints.ContainsKey(p.Value)))
            throw new ArgumentException("Preparation permissions or station bindings are invalid.");
        var projection = CookingLayoutGeometry.Project(InitialLayout, basic, allowed, footprints, GeometryPolicy, Array.Empty<CookingPlayerPose>());
        if (!projection.Accepted || projection.FrozenLayout!.Equipment.Any(e => !bindings.TryGetValue(e.Station, out var definition) || definition != e.Definition))
            throw new ArgumentException("Initial layout must use legal basic equipment and trusted station bindings.");
        return this with { InitialLayout = projection.FrozenLayout, TrustedFootprints = footprints, GeometryPolicy = GeometryPolicy with { },
            BaseAuthorized = basic, LevelAllowed = allowed, StationBindings = bindings };
    }

    private static bool AdjacentFace(CookingEquipmentFootprint footprint) =>
        ((footprint.InteractionX == -1 || footprint.InteractionX == footprint.Width)
            && footprint.InteractionY >= 0 && footprint.InteractionY < footprint.Height)
        || ((footprint.InteractionY == -1 || footprint.InteractionY == footprint.Height)
            && footprint.InteractionX >= 0 && footprint.InteractionX < footprint.Width);

    public string CanonicalText()
    {
        var policy = Freeze();
        return JsonSerializer.Serialize(new {
            initialLayout = policy.InitialLayout.CanonicalText(),
            initialLayoutIdentity = policy.InitialLayout.Sha256(),
            geometryPolicy = policy.GeometryPolicy,
            footprints = policy.TrustedFootprints.OrderBy(p => p.Key.Value, StringComparer.Ordinal)
                .Select(p => new { definition = p.Key.Value, p.Value.Width, p.Value.Height, p.Value.InteractionX, p.Value.InteractionY }).ToArray(),
            baseAuthorized = policy.BaseAuthorized.Select(d => d.Value).Order(StringComparer.Ordinal).ToArray(),
            levelAllowed = policy.LevelAllowed.Select(d => d.Value).Order(StringComparer.Ordinal).ToArray(),
            stationBindings = policy.StationBindings.OrderBy(p => p.Key.Value, StringComparer.Ordinal)
                .Select(p => new { station = p.Key.Value, definition = p.Value.Value }).ToArray(),
            queueCapacity = policy.FrontQueueCapacity,
            clearTableTicks = policy.FrontClearTableTicks
        });
    }

    public string Identity() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalText())));
    public CookingLayoutGeometryResult Project(CookingRestaurantLayout? candidate,
        IReadOnlyList<CookingPlayerPose> currentPoses,
        IReadOnlyDictionary<StationSlotId, CookingApplianceDefinition> configuredAppliances,
        IReadOnlySet<DefinitionId>? unlocked = null)
    {
        ArgumentNullException.ThrowIfNull(configuredAppliances);
        var policy = Freeze();
        if (candidate?.Equipment is null || candidate.Equipment.Any(e => e is null
            || !configuredAppliances.TryGetValue(e.Station, out var appliance) || appliance is null || appliance.Station != e.Station
            || !policy.StationBindings.TryGetValue(e.Station, out var definition) || definition != e.Definition)
            || !configuredAppliances.Keys.ToHashSet().SetEquals(candidate.Equipment.Select(e => e.Station)))
            return new(new(false, CookingLayoutRejectionReason.DefinitionUnavailable), null);
        var authorized = policy.BaseAuthorized.ToHashSet();
        if (unlocked is not null) authorized.UnionWith(unlocked);
        return CookingLayoutGeometry.Project(candidate, authorized, policy.LevelAllowed, policy.TrustedFootprints, policy.GeometryPolicy, currentPoses);
    }

    public CookingFrontOfHouseConfiguration DerivedFrontConfiguration(CookingLayoutGeometryResult projection,
        CookingFrontOfHouseConfiguration trustedFront)
    {
        ArgumentNullException.ThrowIfNull(projection); ArgumentNullException.ThrowIfNull(trustedFront);
        var policy = Freeze(); var front = trustedFront.Freeze();
        if (!projection.Accepted || projection.FrozenLayout is not { } layout || projection.Geometry is not { } geometry)
            throw new ArgumentException("Accepted geometry projection is required.");
        var verified = CookingLayoutGeometry.Project(layout, policy.LevelAllowed, policy.LevelAllowed, policy.TrustedFootprints,
            policy.GeometryPolicy, geometry.InitialPoses);
        if (!verified.Accepted || CookingFrontOfHouseFlow.SpatialIdentity(verified.Geometry!) != CookingFrontOfHouseFlow.SpatialIdentity(geometry)
            || layout.Equipment.Any(e => !policy.StationBindings.TryGetValue(e.Station, out var d) || d != e.Definition))
            throw new ArgumentException("Projection differs from trusted geometry policy.");
        CookingLayoutCell Target(string id, CookingLayoutTargetKind kind)
        {
            var matches = layout.Targets.Where(t => t.Id == id).ToArray();
            if (matches.Length != 1 || matches[0].Kind != kind) throw new ArgumentException("Front target is missing or has the wrong kind.");
            return matches[0].Cell;
        }
        var entrance = Target(front.EntranceAnchor, CookingLayoutTargetKind.CustomerEntrance);
        var queue = Target(front.QueueAnchor, CookingLayoutTargetKind.Queue);
        var exit = Target(front.ExitAnchor, CookingLayoutTargetKind.Exit);
        IReadOnlyList<CookingFrontPoint> Path(CookingLayoutCell from, CookingLayoutCell to)
        {
            var path = CookingRestaurantLayoutValidator.FindPath(layout, from, to);
            if (path.Count == 0) throw new ArgumentException("Front target is unreachable.");
            return Array.AsReadOnly(path.Select(c => new CookingFrontPoint(c.X, c.Y)).ToArray());
        }
        if (layout.Targets.Count(t => t.Kind == CookingLayoutTargetKind.Table) != front.Schedule.TableCount)
            throw new ArgumentException("Layout tables differ from trusted schedule.");
        var tables = Enumerable.Range(1, front.Schedule.TableCount).Select(n => {
            var id = $"table-{n}"; var table = Target(id, CookingLayoutTargetKind.Table);
            return new CookingFrontTableRoute(id, Path(queue, table), Path(table, exit));
        }).ToArray();
        var entrancePath = Path(entrance, queue); var exitPath = Path(queue, exit);
        var walkable = entrancePath.Concat(exitPath).Concat(tables.SelectMany(t => t.QueueToTable.Concat(t.TableToExit)))
            .Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToArray();
        var flow = new CookingFrontOfHouseFlow(CookingFrontOfHouseFlow.SpatialIdentity(geometry), Array.AsReadOnly(walkable),
            entrancePath, exitPath, Array.AsReadOnly(tables), policy.FrontQueueCapacity, policy.FrontClearTableTicks)
            { Spatial = geometry.Freeze(), CellSize = layout.CellSize };
        return (front with { Flow = flow }).Freeze();
    }
}