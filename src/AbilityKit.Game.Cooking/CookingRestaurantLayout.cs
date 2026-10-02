using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AbilityKit.Game.Cooking;

/// <summary>A placement cell; movement remains continuous in the kitchen spatial simulation.</summary>
public readonly record struct CookingLayoutCell(int X, int Y);

public sealed record CookingFloorRegion(string Id, int X, int Y, int Width, int Height);

public enum CookingLayoutTargetKind
{
    PlayerEntrance,
    CustomerEntrance,
    Queue,
    Table,
    Exit,
    Serving,
    Receiving,
    Storage,
    Washing,
}

public sealed record CookingLayoutTarget(string Id, CookingLayoutTargetKind Kind, CookingLayoutCell Cell);

/// <summary>Interaction offsets are expressed in the unrotated footprint, including cells outside it.</summary>
public sealed record CookingEquipmentPlacement(
    StationSlotId Station, DefinitionId Definition, CookingLayoutCell Cell,
    int Width, int Height, int Rotation, int InteractionX, int InteractionY);

/// <summary>Trusted content dimensions; placement requests cannot shrink the occupied footprint.</summary>
public sealed record CookingEquipmentFootprint(
    DefinitionId Definition, int Width, int Height, int InteractionX, int InteractionY);

public sealed record CookingRestaurantLayout(
    LayoutId Id,
    IReadOnlyList<CookingFloorRegion> Floors,
    IReadOnlyList<CookingEquipmentPlacement> Equipment,
    IReadOnlyList<CookingLayoutCell> Walls,
    IReadOnlyList<CookingLayoutTarget> Targets)
{
    public int CellSize { get; init; } = 1000;
    public int ActorRadius { get; init; } = 250;

    public string CanonicalText() => JsonSerializer.Serialize(new
    {
        id = Id.Value,
        cellSize = CellSize,
        actorRadius = ActorRadius,
        floors = Floors.OrderBy(floor => floor.Id, StringComparer.Ordinal),
        equipment = Equipment.OrderBy(item => item.Station.Value, StringComparer.Ordinal),
        walls = Walls.OrderBy(cell => cell.X).ThenBy(cell => cell.Y),
        targets = Targets.OrderBy(target => target.Id, StringComparer.Ordinal),
    });

    public string Sha256() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalText())));
}

public enum CookingLayoutRejectionReason
{
    None,
    InvalidLayout,
    DuplicateIdentity,
    DefinitionUnavailable,
    OccupancyConflict,
    OutsideFloor,
    EntranceMissing,
    UnreachableTarget,
}

public sealed record CookingLayoutValidationResult(
    bool Accepted,
    CookingLayoutRejectionReason Reason,
    string? Target = null,
    CookingRestaurantLayout? Layout = null);

/// <summary>
/// Validates the complete proposed restaurant before the existing preparation owner installs it.
/// Permission is the intersection of unlocked content and this Level's permitted content.
/// </summary>
public static class CookingRestaurantLayoutValidator
{
    public const int MaximumCells = 65536;

    public static CookingLayoutValidationResult Validate(
        CookingRestaurantLayout? candidate,
        IReadOnlySet<DefinitionId> unlocked,
        IReadOnlySet<DefinitionId> levelAllowed,
        IReadOnlyDictionary<DefinitionId, CookingEquipmentFootprint>? definitions = null)
    {
        ArgumentNullException.ThrowIfNull(unlocked);
        ArgumentNullException.ThrowIfNull(levelAllowed);
        if (candidate is null || string.IsNullOrWhiteSpace(candidate.Id.Value) ||
            candidate.Floors is null || candidate.Equipment is null || candidate.Walls is null ||
            candidate.Targets is null || candidate.Floors.Count == 0 ||
            candidate.CellSize <= 0 || candidate.ActorRadius <= 0 || candidate.ActorRadius > candidate.CellSize)
            return Reject(CookingLayoutRejectionReason.InvalidLayout);

        var floors = new HashSet<CookingLayoutCell>();
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var floor in candidate.Floors)
        {
            if (floor is null || string.IsNullOrWhiteSpace(floor.Id) || floor.Width <= 0 || floor.Height <= 0 ||
                (long)floor.Width * floor.Height > MaximumCells ||
                (long)floor.X + floor.Width - 1 > int.MaxValue || (long)floor.Y + floor.Height - 1 > int.MaxValue)
                return Reject(CookingLayoutRejectionReason.InvalidLayout);
            if (!identities.Add(floor.Id))
                return Reject(CookingLayoutRejectionReason.DuplicateIdentity, floor.Id);
            for (var x = 0; x < floor.Width; x++)
                for (var y = 0; y < floor.Height; y++)
                    floors.Add(new CookingLayoutCell(floor.X + x, floor.Y + y));
            if (floors.Count > MaximumCells)
                return Reject(CookingLayoutRejectionReason.InvalidLayout);
        }

        var occupied = new HashSet<CookingLayoutCell>();
        foreach (var wall in candidate.Walls)
        {
            if (!floors.Contains(wall))
                return Reject(CookingLayoutRejectionReason.OutsideFloor);
            if (!occupied.Add(wall))
                return Reject(CookingLayoutRejectionReason.OccupancyConflict);
        }

        identities.Clear();
        var interactionCells = new Dictionary<string, CookingLayoutCell>(StringComparer.Ordinal);
        foreach (var equipment in candidate.Equipment)
        {
            if (equipment is null || string.IsNullOrWhiteSpace(equipment.Station.Value) ||
                string.IsNullOrWhiteSpace(equipment.Definition.Value) || equipment.Width <= 0 || equipment.Height <= 0 ||
                (long)equipment.Width * equipment.Height > MaximumCells ||
                equipment.Rotation is not (0 or 90 or 180 or 270))
                return Reject(CookingLayoutRejectionReason.InvalidLayout);
            if (!identities.Add(equipment.Station.Value))
                return Reject(CookingLayoutRejectionReason.DuplicateIdentity, equipment.Station.Value);
            if (!unlocked.Contains(equipment.Definition) || !levelAllowed.Contains(equipment.Definition))
                return Reject(CookingLayoutRejectionReason.DefinitionUnavailable, equipment.Definition.Value);
            if (definitions is not null &&
                (!definitions.TryGetValue(equipment.Definition, out var footprint) || footprint is null ||
                 footprint.Definition != equipment.Definition || footprint.Width != equipment.Width ||
                 footprint.Height != equipment.Height || footprint.InteractionX != equipment.InteractionX ||
                 footprint.InteractionY != equipment.InteractionY))
                return Reject(CookingLayoutRejectionReason.InvalidLayout, equipment.Station.Value);
            for (var x = 0; x < equipment.Width; x++)
            {
                for (var y = 0; y < equipment.Height; y++)
                {
                    if (!TryRotate(equipment, x, y, out var cell))
                        return Reject(CookingLayoutRejectionReason.InvalidLayout, equipment.Station.Value);
                    if (!floors.Contains(cell))
                        return Reject(CookingLayoutRejectionReason.OutsideFloor, equipment.Station.Value);
                    if (!occupied.Add(cell))
                        return Reject(CookingLayoutRejectionReason.OccupancyConflict, equipment.Station.Value);
                }
            }
            if (!TryRotate(equipment, equipment.InteractionX, equipment.InteractionY, out var interaction))
                return Reject(CookingLayoutRejectionReason.InvalidLayout, equipment.Station.Value);
            interactionCells.Add(equipment.Station.Value, interaction);
        }

        identities.Clear();
        foreach (var target in candidate.Targets)
        {
            if (target is null || string.IsNullOrWhiteSpace(target.Id) || !Enum.IsDefined(target.Kind))
                return Reject(CookingLayoutRejectionReason.InvalidLayout);
            if (!identities.Add(target.Id) || interactionCells.ContainsKey(target.Id))
                return Reject(CookingLayoutRejectionReason.DuplicateIdentity, target.Id);
            if (!floors.Contains(target.Cell) || occupied.Contains(target.Cell))
                return Reject(CookingLayoutRejectionReason.UnreachableTarget, target.Id);
        }

        var playerEntrances = candidate.Targets.Where(target => target.Kind == CookingLayoutTargetKind.PlayerEntrance).ToArray();
        var customerEntrances = candidate.Targets.Where(target => target.Kind == CookingLayoutTargetKind.CustomerEntrance).ToArray();
        if (playerEntrances.Length == 0 || customerEntrances.Length == 0 ||
            !candidate.Targets.Any(target => target.Kind == CookingLayoutTargetKind.Exit))
            return Reject(CookingLayoutRejectionReason.EntranceMissing);

        var walkable = new HashSet<CookingLayoutCell>(floors);
        walkable.ExceptWith(occupied);
        walkable.RemoveWhere(cell => !HasClearance(cell, floors, occupied, candidate.CellSize, candidate.ActorRadius));
        foreach (var target in candidate.Targets)
            if (!walkable.Contains(target.Cell))
                return Reject(CookingLayoutRejectionReason.UnreachableTarget, target.Id);
        // Each spawn entrance must reach every relevant target; disconnected rooms cannot hide a blocked entrance.
        foreach (var entrance in playerEntrances)
        {
            var reached = Reachable(walkable, entrance.Cell);
            foreach (var (id, cell) in interactionCells)
                if (!reached.Contains(cell))
                    return Reject(CookingLayoutRejectionReason.UnreachableTarget, id);
            foreach (var target in candidate.Targets.Where(target => target.Kind != CookingLayoutTargetKind.CustomerEntrance))
                if (!reached.Contains(target.Cell))
                    return Reject(CookingLayoutRejectionReason.UnreachableTarget, target.Id);
        }
        foreach (var entrance in customerEntrances)
        {
            var reached = Reachable(walkable, entrance.Cell);
            foreach (var target in candidate.Targets.Where(target => target.Kind is
                         CookingLayoutTargetKind.Table or CookingLayoutTargetKind.Queue or CookingLayoutTargetKind.Exit))
                if (!reached.Contains(target.Cell))
                    return Reject(CookingLayoutRejectionReason.UnreachableTarget, target.Id);
        }

        var frozen = candidate with
        {
            Floors = Array.AsReadOnly(candidate.Floors.ToArray()),
            Equipment = Array.AsReadOnly(candidate.Equipment.ToArray()),
            Walls = Array.AsReadOnly(candidate.Walls.ToArray()),
            Targets = Array.AsReadOnly(candidate.Targets.ToArray()),
        };
        return new CookingLayoutValidationResult(true, CookingLayoutRejectionReason.None, Layout: frozen);
    }

    public static CookingLayoutCell InteractionCell(CookingEquipmentPlacement equipment)
    {
        if (!TryRotate(equipment, equipment.InteractionX, equipment.InteractionY, out var cell))
            throw new ArgumentOutOfRangeException(nameof(equipment));
        return cell;
    }

    /// <summary>Returns a stable shortest route over the validated placement floor, including both endpoints.</summary>
    public static IReadOnlyList<CookingLayoutCell> FindPath(
        CookingRestaurantLayout layout, CookingLayoutCell from, CookingLayoutCell to)
    {
        var cells = new HashSet<CookingLayoutCell>();
        foreach (var floor in layout.Floors)
            for (var x = 0; x < floor.Width; x++)
                for (var y = 0; y < floor.Height; y++)
                    cells.Add(new CookingLayoutCell(floor.X + x, floor.Y + y));
        cells.ExceptWith(layout.Walls);
        var floorCells = new HashSet<CookingLayoutCell>(cells);
        floorCells.UnionWith(layout.Walls);
        var occupied = new HashSet<CookingLayoutCell>(layout.Walls);
        foreach (var equipment in layout.Equipment)
            for (var x = 0; x < equipment.Width; x++)
                for (var y = 0; y < equipment.Height; y++)
                    if (TryRotate(equipment, x, y, out var cell)) { cells.Remove(cell); occupied.Add(cell); }
        cells.RemoveWhere(cell => !HasClearance(cell, floorCells, occupied, layout.CellSize, layout.ActorRadius));
        if (!cells.Contains(from) || !cells.Contains(to)) return Array.Empty<CookingLayoutCell>();
        var previous = new Dictionary<CookingLayoutCell, CookingLayoutCell?> { [from] = null };
        var queue = new Queue<CookingLayoutCell>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == to)
            {
                var path = new List<CookingLayoutCell>();
                CookingLayoutCell? cell = current;
                while (cell is { } value) { path.Add(value); cell = previous[value]; }
                path.Reverse();
                return Array.AsReadOnly(path.ToArray());
            }
            foreach (var next in Neighbors(current))
                if (cells.Contains(next) && previous.TryAdd(next, current)) queue.Enqueue(next);
        }
        return Array.Empty<CookingLayoutCell>();
    }

    private static HashSet<CookingLayoutCell> Reachable(HashSet<CookingLayoutCell> walkable, CookingLayoutCell from)
    {
        var reached = new HashSet<CookingLayoutCell> { from };
        var queue = new Queue<CookingLayoutCell>();
        queue.Enqueue(from);
        while (queue.Count > 0)
            foreach (var next in Neighbors(queue.Dequeue()))
                if (walkable.Contains(next) && reached.Add(next)) queue.Enqueue(next);
        return reached;
    }

    private static bool HasClearance(CookingLayoutCell cell, HashSet<CookingLayoutCell> floors,
        HashSet<CookingLayoutCell> occupied, int scale, int radius)
    {
        var centerX = (long)cell.X * scale + scale / 2;
        var centerY = (long)cell.Y * scale + scale / 2;
        // Obstacles and outer floor boundaries are closed, matching swept spatial collision.
        var minX = FloorDivide(centerX - radius - 1, scale);
        var maxX = FloorDivide(centerX + radius, scale);
        var minY = FloorDivide(centerY - radius - 1, scale);
        var maxY = FloorDivide(centerY + radius, scale);
        for (var x = minX; x <= maxX; x++)
            for (var y = minY; y <= maxY; y++)
            {
                if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue) return false;
                var part = new CookingLayoutCell((int)x, (int)y);
                if (!floors.Contains(part) || occupied.Contains(part)) return false;
            }
        return true;
    }

    private static long FloorDivide(long value, int divisor) =>
        value >= 0 ? value / divisor : (value - divisor + 1) / divisor;

    private static IEnumerable<CookingLayoutCell> Neighbors(CookingLayoutCell cell)
    {
        if (cell.X > int.MinValue) yield return new(cell.X - 1, cell.Y);
        if (cell.Y > int.MinValue) yield return new(cell.X, cell.Y - 1);
        if (cell.X < int.MaxValue) yield return new(cell.X + 1, cell.Y);
        if (cell.Y < int.MaxValue) yield return new(cell.X, cell.Y + 1);
    }

    private static bool TryRotate(CookingEquipmentPlacement item, int x, int y, out CookingLayoutCell cell)
    {
        var (offsetX, offsetY) = item.Rotation switch
        {
            90 => ((long)item.Height - 1 - y, (long)x),
            180 => ((long)item.Width - 1 - x, (long)item.Height - 1 - y),
            270 => ((long)y, (long)item.Width - 1 - x),
            _ => ((long)x, (long)y),
        };
        var resultX = (long)item.Cell.X + offsetX;
        var resultY = (long)item.Cell.Y + offsetY;
        if (resultX < int.MinValue || resultX > int.MaxValue || resultY < int.MinValue || resultY > int.MaxValue)
        { cell = default; return false; }
        cell = new((int)resultX, (int)resultY);
        return true;
    }

    private static CookingLayoutValidationResult Reject(CookingLayoutRejectionReason reason, string? target = null)
        => new(false, reason, target);
}
