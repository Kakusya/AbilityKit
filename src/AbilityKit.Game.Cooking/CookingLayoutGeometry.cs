namespace AbilityKit.Game.Cooking;

/// <summary>Content-owned movement policy. Layout requests cannot change these values.</summary>
public sealed record CookingLayoutGeometryPolicy(int PlayerRadius, int InteractionRadius, int MovementSpeed)
{
    public const int CellSize = 1000;
}

/// <summary>A frozen candidate only; installation and lifecycle admission belong to the Level owner.</summary>
public sealed record CookingLayoutGeometryResult(
    CookingLayoutValidationResult Validation, CookingSpatialConfiguration? Geometry)
{
    public IReadOnlyList<CookingPlayerPose> ProjectedPoses { get; init; } = Array.Empty<CookingPlayerPose>();
    public bool Accepted => Validation.Accepted;
    public CookingRestaurantLayout? FrozenLayout => Validation.Layout;
}

public static class CookingLayoutGeometry
{
    public static CookingLayoutGeometryResult Project(
        CookingRestaurantLayout? candidate,
        IReadOnlySet<DefinitionId> unlocked,
        IReadOnlySet<DefinitionId> levelAllowed,
        IReadOnlyDictionary<DefinitionId, CookingEquipmentFootprint> trustedFootprints,
        CookingLayoutGeometryPolicy trustedPolicy,
        IReadOnlyList<CookingPlayerPose> currentPoses)
    {
        ArgumentNullException.ThrowIfNull(trustedPolicy);
        ArgumentNullException.ThrowIfNull(currentPoses);
        if (candidate is null || candidate.CellSize != CookingLayoutGeometryPolicy.CellSize ||
            candidate.ActorRadius != trustedPolicy.PlayerRadius || trustedPolicy.PlayerRadius <= 0 ||
            trustedPolicy.InteractionRadius <= 0 || trustedPolicy.MovementSpeed <= 0 ||
            currentPoses.Any(p => p is null || string.IsNullOrWhiteSpace(p.Player.Value) ||
                Math.Abs((long)p.FacingX) > 1 || Math.Abs((long)p.FacingY) > 1 ||
                (p.FacingX == 0 && p.FacingY == 0) || p.LastMovementTick < -1) ||
            currentPoses.Select(p => p.Player).Distinct().Count() != currentPoses.Count)
            return Reject(CookingLayoutRejectionReason.InvalidLayout);

        var validation = CookingRestaurantLayoutValidator.ValidateForInstallation(
            candidate, unlocked, levelAllowed, trustedFootprints);
        if (!validation.Accepted) return new(validation, null);
        var layout = validation.Layout!;
        var cells = new HashSet<CookingLayoutCell>();
        foreach (var floor in layout.Floors)
            for (var x = 0; x < floor.Width; x++)
                for (var y = 0; y < floor.Height; y++)
                    cells.Add(new(floor.X + x, floor.Y + y));
        var minX = cells.Min(c => c.X);
        var minY = cells.Min(c => c.Y);
        var maxX = cells.Max(c => c.X) + 1;
        var maxY = cells.Max(c => c.Y) + 1;
        var obstacles = Complement(cells, minX, minY, maxX, maxY);
        foreach (var wall in layout.Walls.OrderBy(c => c.Y).ThenBy(c => c.X))
            obstacles.Add(Rectangle(wall.X, wall.Y, wall.X + 1, wall.Y + 1));
        var anchors = new List<CookingSpatialAnchor>();
        foreach (var equipment in layout.Equipment.OrderBy(e => e.Station.Value, StringComparer.Ordinal))
        {
            var width = equipment.Rotation is 90 or 270 ? equipment.Height : equipment.Width;
            var height = equipment.Rotation is 90 or 270 ? equipment.Width : equipment.Height;
            obstacles.Add(Rectangle(equipment.Cell.X, equipment.Cell.Y,
                equipment.Cell.X + width, equipment.Cell.Y + height));
            var interaction = CookingRestaurantLayoutValidator.InteractionCell(equipment);
            anchors.Add(new(LocationKind.StationSlot, equipment.Station.Value, Center(interaction.X), Center(interaction.Y)));
        }
        foreach (var target in layout.Targets.OrderBy(t => t.Id, StringComparer.Ordinal))
            anchors.Add(new(LocationKind.WorldPosition, target.Id, Center(target.Cell.X), Center(target.Cell.Y)));
        var geometry = new CookingSpatialConfiguration(Scale(minX), Scale(minY), Scale(maxX), Scale(maxY),
            trustedPolicy.PlayerRadius, trustedPolicy.InteractionRadius, Array.Empty<CookingPlayerPose>(),
            Array.AsReadOnly(anchors.ToArray()), Array.AsReadOnly(obstacles.ToArray()), trustedPolicy.MovementSpeed);

        var available = EntranceConnectedCells(layout, cells);
        var connected = available.ToHashSet();
        // Preserve all legal, entrance-connected poses before relocating anyone.
        var ordered = currentPoses.OrderBy(p => p.Player.Value, StringComparer.Ordinal).ToArray();
        var preserved = new Dictionary<PlayerId, CookingPlayerPose>();
        foreach (var pose in ordered)
            if (geometry.ValidPose(pose) &&
                connected.Contains(new CookingLayoutCell((int)FloorDivide(pose.X), (int)FloorDivide(pose.Y))) &&
                !preserved.Values.Any(other => geometry.Overlap(pose, other)))
                preserved.Add(pose.Player, pose);
        var needsRelocation = ordered.Where(p => !preserved.ContainsKey(p.Player)).ToArray();
        if (needsRelocation.Length != 0)
        {
            foreach (var pose in needsRelocation)
            {
                CookingPlayerPose? placement = null;
                foreach (var cell in available)
                {
                    var proposed = pose with { X = Center(cell.X), Y = Center(cell.Y) };
                    if (preserved.Values.Any(other => geometry.Overlap(proposed, other))) continue;
                    // Reachable centres have already passed the same closed-cell clearance rule;
                    // verify the selected centre against the actual S01 representation as well.
                    if (!geometry.ValidPose(proposed)) continue;
                    placement = proposed;
                    break;
                }
                if (placement is null)
                    return Reject(CookingLayoutRejectionReason.UnreachableTarget, pose.Player.Value);
                preserved.Add(pose.Player, placement);
            }
        }
        var projectedPoses = Array.AsReadOnly(ordered.Select(p => preserved[p.Player]).ToArray());
        return new(validation, geometry with {
            InitialPoses = Array.AsReadOnly(projectedPoses.Select(p => p with { LastMovementTick = -1 }).ToArray()) })
        { ProjectedPoses = projectedPoses };
    }

    // At most one row per enabled cell, plus one slab per gap. Never scan the bounding-box area.
    private static List<CookingSpatialObstacle> Complement(HashSet<CookingLayoutCell> cells,
        int minX, int minY, int maxX, int maxY)
    {
        var result = new List<CookingSpatialObstacle>();
        var previousY = minY;
        foreach (var row in cells.GroupBy(c => c.Y).OrderBy(g => g.Key))
        {
            if (previousY < row.Key) result.Add(Rectangle(minX, previousY, maxX, row.Key));
            var nextX = minX;
            foreach (var x in row.Select(c => c.X).Order())
            {
                if (nextX < x) result.Add(Rectangle(nextX, row.Key, x, row.Key + 1));
                nextX = x + 1;
            }
            if (nextX < maxX) result.Add(Rectangle(nextX, row.Key, maxX, row.Key + 1));
            previousY = row.Key + 1;
        }
        if (previousY < maxY) result.Add(Rectangle(minX, previousY, maxX, maxY));
        return result;
    }

    private static IReadOnlyList<CookingLayoutCell> EntranceConnectedCells(
        CookingRestaurantLayout layout, HashSet<CookingLayoutCell> floors)
    {
        var occupied = new HashSet<CookingLayoutCell>(layout.Walls);
        foreach (var e in layout.Equipment)
        {
            var width = e.Rotation is 90 or 270 ? e.Height : e.Width;
            var height = e.Rotation is 90 or 270 ? e.Width : e.Height;
            for (var x = 0; x < width; x++)
                for (var y = 0; y < height; y++) occupied.Add(new(e.Cell.X + x, e.Cell.Y + y));
        }
        var minX = floors.Min(c => c.X); var maxX = floors.Max(c => c.X);
        var minY = floors.Min(c => c.Y); var maxY = floors.Max(c => c.Y);
        bool Clear(CookingLayoutCell cell)
        {
            var cx = (long)Center(cell.X); var cy = (long)Center(cell.Y);
            var r = layout.ActorRadius;
            if (cx - r < (long)minX * 1000 || cx + r > ((long)maxX + 1) * 1000 ||
                cy - r < (long)minY * 1000 || cy + r > ((long)maxY + 1) * 1000) return false;
            for (var x = FloorDivide(cx - r - 1); x <= FloorDivide(cx + r); x++)
                for (var y = FloorDivide(cy - r - 1); y <= FloorDivide(cy + r); y++)
                {
                    if (x < minX || x > maxX || y < minY || y > maxY) continue;
                    var part = new CookingLayoutCell((int)x, (int)y);
                    if (!floors.Contains(part) || occupied.Contains(part)) return false;
                }
            return true;
        }
        var walkable = floors.Where(Clear).ToHashSet();
        var reached = new HashSet<CookingLayoutCell>();
        var queue = new Queue<CookingLayoutCell>();
        foreach (var entrance in layout.Targets.Where(t => t.Kind == CookingLayoutTargetKind.PlayerEntrance)
                     .OrderBy(t => t.Id, StringComparer.Ordinal))
            if (reached.Add(entrance.Cell)) queue.Enqueue(entrance.Cell);
        while (queue.TryDequeue(out var cell))
            foreach (var neighbor in new[] { new CookingLayoutCell(cell.X - 1, cell.Y), new(cell.X, cell.Y - 1),
                         new(cell.X + 1, cell.Y), new(cell.X, cell.Y + 1) })
                if (walkable.Contains(neighbor) && reached.Add(neighbor)) queue.Enqueue(neighbor);
        return reached.OrderBy(c => c.Y).ThenBy(c => c.X).ToArray();
    }

    private static long FloorDivide(long value) => value >= 0 ? value / 1000 : (value - 999) / 1000;
    private static int Scale(int cell) => checked(cell * CookingLayoutGeometryPolicy.CellSize);
    private static int Center(int cell) => checked(Scale(cell) + CookingLayoutGeometryPolicy.CellSize / 2);
    private static CookingSpatialObstacle Rectangle(int minX, int minY, int maxX, int maxY) =>
        new(Scale(minX), Scale(minY), Scale(maxX), Scale(maxY));
    private static CookingLayoutGeometryResult Reject(CookingLayoutRejectionReason reason, string? target = null) =>
        new(new(false, reason, target), null);
}



