using System.Numerics;

namespace AbilityKit.Game.Cooking;

public enum CookingRecipeExecutionKind { Automatic, Manual }

/// <summary>Integer subunits: 1000 represent one logical unit; facing components are -1, 0 or 1.</summary>
public sealed record CookingPlayerPose(PlayerId Player, int X, int Y, int FacingX, int FacingY, [property: System.Text.Json.Serialization.JsonRequired] long LastMovementTick = -1);
public sealed record CookingSpatialAnchor(LocationKind Kind, string Id, int X, int Y);
public sealed record CookingSpatialObstacle(int MinX, int MinY, int MaxX, int MaxY);
public sealed record CookingSpatialConfiguration(
    int MinX, int MinY, int MaxX, int MaxY, int PlayerRadius, int InteractionRadius,
    IReadOnlyList<CookingPlayerPose> InitialPoses,
    IReadOnlyList<CookingSpatialAnchor> Anchors,
    IReadOnlyList<CookingSpatialObstacle> Obstacles,
    int MovementSpeed = 1000)
{
    internal CookingSpatialConfiguration Freeze() => this with {
        InitialPoses = Array.AsReadOnly(InitialPoses.ToArray()),
        Anchors = Array.AsReadOnly(Anchors.ToArray()),
        Obstacles = Array.AsReadOnly(Obstacles.ToArray()) };

    public void Validate(IReadOnlyDictionary<PlayerId, CookingPlayerConfig> players,
        IReadOnlyDictionary<StationSlotId, CookingApplianceDefinition> stations)
    {
        if (MinX >= MaxX || MinY >= MaxY || PlayerRadius <= 0 || InteractionRadius <= 0 || MovementSpeed <= 0 ||
            InitialPoses.Count != players.Count || InitialPoses.Select(p => p.Player).Distinct().Count() != players.Count ||
            InitialPoses.Any(p => !players.ContainsKey(p.Player) || !ValidPose(p) || p.LastMovementTick != -1) ||
            Anchors.Any(a => a.Kind is not (LocationKind.WorldPosition or LocationKind.StationSlot) ||
                string.IsNullOrWhiteSpace(a.Id) || a.X < MinX || a.X > MaxX || a.Y < MinY || a.Y > MaxY) ||
            Anchors.Select(a => (a.Kind, a.Id)).Distinct().Count() != Anchors.Count ||
            stations.Keys.Any(s => !Anchors.Any(a => a.Kind == LocationKind.StationSlot && a.Id == s.Value)) ||
            Obstacles.Any(o => o.MinX >= o.MaxX || o.MinY >= o.MaxY))
            throw new ArgumentException("Invalid spatial configuration or missing anchor.");
        foreach (var a in InitialPoses)
            if (InitialPoses.Any(b => a.Player != b.Player && Overlap(a, b)))
                throw new ArgumentException("Initial player poses overlap.");
    }

    internal bool ValidPose(CookingPlayerPose p) =>
        Math.Abs((long)p.FacingX) <= 1 && Math.Abs((long)p.FacingY) <= 1 && (p.FacingX != 0 || p.FacingY != 0) &&
        (long)p.X - PlayerRadius >= MinX && (long)p.X + PlayerRadius <= MaxX &&
        (long)p.Y - PlayerRadius >= MinY && (long)p.Y + PlayerRadius <= MaxY &&
        !Obstacles.Any(o => SegmentHits(p.X, p.Y, p.X, p.Y,
            (long)o.MinX - PlayerRadius, (long)o.MinY - PlayerRadius, (long)o.MaxX + PlayerRadius, (long)o.MaxY + PlayerRadius));

    internal bool Overlap(CookingPlayerPose a, CookingPlayerPose b) =>
        Math.Abs((long)a.X - b.X) <= 2L * PlayerRadius && Math.Abs((long)a.Y - b.Y) <= 2L * PlayerRadius;

    // Closed segment/rectangle intersection with exact integer orientation, including tangency.
    internal static bool SegmentHits(long ax, long ay, long bx, long by, long minX, long minY, long maxX, long maxY)
    {
        if ((ax >= minX && ax <= maxX && ay >= minY && ay <= maxY) ||
            (bx >= minX && bx <= maxX && by >= minY && by <= maxY)) return true;
        return Crosses(ax, ay, bx, by, minX, minY, maxX, minY) ||
            Crosses(ax, ay, bx, by, maxX, minY, maxX, maxY) ||
            Crosses(ax, ay, bx, by, maxX, maxY, minX, maxY) ||
            Crosses(ax, ay, bx, by, minX, maxY, minX, minY);
    }

    private static bool Crosses(long ax, long ay, long bx, long by, long cx, long cy, long dx, long dy)
    {
        static BigInteger Orient(long x, long y, long x2, long y2, long x3, long y3) =>
            (BigInteger)(x2 - x) * (y3 - y) - (BigInteger)(y2 - y) * (x3 - x);
        if (Math.Max(ax, bx) < Math.Min(cx, dx) || Math.Max(cx, dx) < Math.Min(ax, bx) ||
            Math.Max(ay, by) < Math.Min(cy, dy) || Math.Max(cy, dy) < Math.Min(ay, by)) return false;
        return Orient(ax, ay, bx, by, cx, cy).Sign * Orient(ax, ay, bx, by, dx, dy).Sign <= 0 &&
            Orient(cx, cy, dx, dy, ax, ay).Sign * Orient(cx, cy, dx, dy, bx, by).Sign <= 0;
    }
}

public sealed record CookingInteractionPreview(string TargetId, CookingRecipeCommand Command, long DistanceSquared, long FacingDot);

public sealed partial class CookingRecipeSimulation
{
    private Dictionary<PlayerId, CookingPlayerPose> _poses = new();

    private bool IsManagedPoolLocation(ItemLocation location, DefinitionId definition) =>
        location == ItemLocation.World(_fixture.CleanPoolLocation) && _fixture.CleanContainerSupply.ContainsKey(definition);

    private void ValidateSpatialItemLocation(ItemLocation location, DefinitionId definition)
    {
        if (_fixture.Spatial is not { } spatial) return;
        if (location.Kind is LocationKind.WorldPosition or LocationKind.StationSlot)
        {
            if (!spatial.Anchors.Any(a => a.Kind == location.Kind && a.Id == location.SlotId))
                throw new ArgumentException("Spatial item location has no configured anchor.");
            if (!IsManagedPoolLocation(location, definition) && _items.Values.Any(i => !i.Removed && i.Location == location))
                throw new ArgumentException("Ordinary station contains one object.");
        }
    }

    private bool StationIsReachable(CookingPlayerConfig player, StationSlotId station) =>
        _fixture.Spatial is null ? player.ReachableStations.Contains(station.Value) :
        LocationIsReachable(ItemLocation.Station(station), player.Id);

    private bool ItemIsReachable(ItemId id, PlayerId player) =>
        _items.TryGetValue(id, out var item) && !item.Removed && LocationIsReachable(item.Location, player);

    private bool LocationIsReachable(ItemLocation location, PlayerId player)
    {
        if (_fixture.Spatial is null)
            return location.Kind switch
            {
                LocationKind.PlayerHand => location.OwnerId == player.Value,
                LocationKind.StationSlot => location.SlotId is { } s && _fixture.Players[player].ReachableStations.Contains(s),
                LocationKind.WorldPosition => true,
                _ => false,
            };
        if (!_poses.TryGetValue(player, out var pose) || !ResolvePosition(location, out var x, out var y, out var handOwner)) return false;
        if (handOwner is not null) return handOwner == player.Value;
        return GeometryReach(pose, x, y);
    }

    private bool ResolvePosition(ItemLocation location, out int x, out int y, out string? handOwner)
    {
        x = y = 0; handOwner = null;
        var seen = new HashSet<ItemId>();
        while (location.Kind == LocationKind.ContainerSlot)
        {
            var id = new ItemId(location.OwnerId ?? "");
            if (!seen.Add(id) || !_items.TryGetValue(id, out var item) || item.Removed) return false;
            location = item.Location;
        }
        if (location.Kind == LocationKind.PlayerHand)
        {
            handOwner = location.OwnerId;
            if (!_poses.TryGetValue(new PlayerId(handOwner ?? ""), out var p)) return false;
            x = p.X; y = p.Y; return true;
        }
        var anchor = _fixture.Spatial!.Anchors.FirstOrDefault(a => a.Kind == location.Kind && a.Id == location.SlotId);
        if (anchor is null) return false;
        x = anchor.X; y = anchor.Y; return true;
    }

    private bool GeometryReach(CookingPlayerPose pose, int x, int y)
    {
        var spatial = _fixture.Spatial!;
        var dx = (long)x - pose.X; var dy = (long)y - pose.Y;
        return (BigInteger)dx * dx + (BigInteger)dy * dy <= (BigInteger)spatial.InteractionRadius * spatial.InteractionRadius &&
            ((dx == 0 && dy == 0) || dx * pose.FacingX + dy * pose.FacingY > 0) &&
            !spatial.Obstacles.Any(o => CookingSpatialConfiguration.SegmentHits(pose.X, pose.Y, x, y, o.MinX, o.MinY, o.MaxX, o.MaxY));
    }

    private bool CommandIsReachable(CookingRecipeCommand command)
    {
        if (_fixture.Spatial is null || command.Operation is CookingRecipeOperation.Move or CookingRecipeOperation.AdvanceTicks or
            CookingRecipeOperation.StopProcess) return true;
        if (command.Item is { } id && _items.TryGetValue(id, out var item) && !item.Removed && !ItemIsReachable(id, command.Player)) return false;
        if (command.Container is { } container && _items.TryGetValue(container, out var c) && !c.Removed && !ItemIsReachable(container, command.Player)) return false;
        if (command.WorldAnchor is { } world && !LocationIsReachable(ItemLocation.World(world), command.Player)) return false;
        if (command.Station is { } station && !LocationIsReachable(ItemLocation.Station(station), command.Player)) return false;
        if (command.Operation == CookingRecipeOperation.ContinueProcess && command.Process is { } process && TryGetProcess(process, out var p))
            return WorkerCanReach(command.Player, p);
        return true;
    }

    private bool WorkerCanReach(PlayerId worker, ProcessState process) =>
        _fixture.Players.TryGetValue(worker, out var player) && player.IsAvailable && ItemIsReachable(process.Anchor, worker) &&
        (process.Station is not { } station || StationIsReachable(player, station));

    private CookingRecipeCommandResult Move(CookingRecipeCommand command)
    {
        if (_fixture.Spatial is not { } spatial || !_poses.TryGetValue(command.Player, out var before))
            return Reject(CookingRecipeRejectionReason.MovementBlocked);
        var translating = command.MoveX != 0 || command.MoveY != 0;
        if (translating && before.LastMovementTick == LogicalTick) return Reject(CookingRecipeRejectionReason.MovementBlocked);
        // Exact integer normalization, floor absolute components; diagonal error is below one subunit per axis.
        var magnitudeSquared = (BigInteger)command.MoveX * command.MoveX + (BigInteger)command.MoveY * command.MoveY;
        int Component(int input)
        {
            if (input == 0) return 0;
            var numerator = (BigInteger)spatial.MovementSpeed * spatial.MovementSpeed * input * input;
            long lo = 0, hi = spatial.MovementSpeed;
            while (lo < hi) { var mid = lo + (hi - lo + 1) / 2; if ((BigInteger)mid * mid * magnitudeSquared <= numerator) lo = mid; else hi = mid - 1; }
            return checked((int)lo) * Math.Sign(input);
        }
        var dx = Component(command.MoveX); var dy = Component(command.MoveY);
        var facing = command.FacingX != 0 || command.FacingY != 0;
        CookingPlayerPose Candidate(int mx, int my) => before with {
            X = checked(before.X + mx), Y = checked(before.Y + my),
            FacingX = facing ? command.FacingX : before.FacingX, FacingY = facing ? command.FacingY : before.FacingY,
            LastMovementTick = translating ? LogicalTick : before.LastMovementTick };
        bool Legal(CookingPlayerPose after) => spatial.ValidPose(after) &&
            !spatial.Obstacles.Any(o => CookingSpatialConfiguration.SegmentHits(before.X, before.Y, after.X, after.Y,
                (long)o.MinX - spatial.PlayerRadius, (long)o.MinY - spatial.PlayerRadius, (long)o.MaxX + spatial.PlayerRadius, (long)o.MaxY + spatial.PlayerRadius)) &&
            !_poses.Values.Any(p => p.Player != command.Player && CookingSpatialConfiguration.SegmentHits(before.X, before.Y, after.X, after.Y,
                (long)p.X - 2L * spatial.PlayerRadius, (long)p.Y - 2L * spatial.PlayerRadius,
                (long)p.X + 2L * spatial.PlayerRadius, (long)p.Y + 2L * spatial.PlayerRadius));
        CookingPlayerPose after;
        try
        {
            after = Candidate(dx, dy);
            if (!Legal(after))
            {
                if (dx != 0 && dy != 0 && Legal(Candidate(dx, 0))) after = Candidate(dx, 0);
                else if (dx != 0 && dy != 0 && Legal(Candidate(0, dy))) after = Candidate(0, dy);
                else return Reject(CookingRecipeRejectionReason.MovementBlocked);
            }
        }
        catch (OverflowException) { return Reject(CookingRecipeRejectionReason.MovementBlocked); }
        _ = checked(_stateVersion + 1); _ = checked(_eventSequence + 1);
        _poses[command.Player] = after;
        foreach (var p in AllProcesses().Where(p => p.ActiveWorker == command.Player).ToArray())
            if (!WorkerCanReach(command.Player, p)) StageProcess(p with { ActiveWorker = null }, _processesByStation, _processesByAnchorItem);
        return Commit(command, null, null, null, "player-moved");
    }

    private CookingRecipeCommandResult ChangeWorker(CookingRecipeCommand command, bool claim)
    {
        if (command.Process is not { } id || !TryGetProcess(id, out var process)) return Reject(CookingRecipeRejectionReason.ProcessNotFound);
        if (_fixture.Recipes[process.Recipe].Execution != CookingRecipeExecutionKind.Manual ||
            (claim && (process.ActiveWorker is not null || AllProcesses().Any(p => p.ActiveWorker == command.Player))) ||
            (!claim && process.ActiveWorker != command.Player)) return Reject(CookingRecipeRejectionReason.WorkerUnavailable);
        if (claim && !WorkerCanReach(command.Player, process)) return Reject(CookingRecipeRejectionReason.TargetOutOfRange);
        _ = checked(_stateVersion + 1); _ = checked(_eventSequence + 1);
        StageProcess(process with { ActiveWorker = claim ? command.Player : null }, _processesByStation, _processesByAnchorItem);
        return Commit(command, process.Recipe, id, process.Anchor, claim ? "worker-claimed" : "worker-released");
    }
}
