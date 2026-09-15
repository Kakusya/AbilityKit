using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AbilityKit.Game.Cooking;

public readonly record struct SessionId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct WorldId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct MatchId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct PlayerId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct ItemId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct StationSlotId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct CommandId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct DefinitionId(string Value)
{
    public override string ToString() => Value;
}

public sealed record CookingScope(SessionId Session, WorldId World, MatchId Match);

public enum LocationKind
{
    WorldPosition,
    PlayerHand,
    StationSlot,
    ContainerSlot,
}

public sealed record ItemLocation(LocationKind Kind, string? OwnerId = null, string? SlotId = null)
{
    public static ItemLocation World(string position) => new(LocationKind.WorldPosition, SlotId: position);
    public static ItemLocation Hand(PlayerId player) => new(LocationKind.PlayerHand, player.Value);
    public static ItemLocation Station(StationSlotId station) => new(LocationKind.StationSlot, SlotId: station.Value);
    public static ItemLocation Container(ItemId container, string slot) => new(LocationKind.ContainerSlot, container.Value, slot);
}

public enum CookingOperation
{
    Pickup,
    Drop,
}

public enum CommandOutcome
{
    Accepted,
    Rejected,
}

public enum RejectionReason
{
    None,
    ScopeMismatch,
    PlayerNotFound,
    PlayerUnavailable,
    ItemNotFound,
    ItemStale,
    PlayerIneligible,
    TargetOutOfRange,
    CurrentLocationMismatch,
    TargetUnavailable,
    CapacityFull,
    CommandIdentityConflict,
}

public sealed record CookingCommand(
    CookingScope Scope,
    long SimulationBatch,
    PlayerId Player,
    CommandId Command,
    CookingOperation Operation,
    ItemId Item,
    int ExpectedItemVersion,
    StationSlotId? TargetStation = null);

public sealed record CookingEvent(
    long Sequence,
    long SimulationBatch,
    PlayerId Player,
    CommandId Command,
    ItemId Item,
    CookingOperation Operation,
    ItemLocation From,
    ItemLocation To);

public sealed record CommandResult(
    CommandOutcome Outcome,
    RejectionReason Reason,
    long StateVersion,
    bool IsDuplicate,
    IReadOnlyList<CookingEvent> Events)
{
    public static CommandResult Reject(RejectionReason reason, long stateVersion) =>
        new(CommandOutcome.Rejected, reason, stateVersion, false, Array.Empty<CookingEvent>());
}

public sealed record CommandExecution(
    CookingCommand Command,
    CommandResult Result,
    CookingSnapshot Before,
    CookingSnapshot After);

public sealed record CookingItemDefinition(DefinitionId Id, IReadOnlySet<string> AllowedPlayerCapabilities);

public sealed record CookingPlayerConfig(
    PlayerId Id,
    IReadOnlySet<string> Capabilities,
    IReadOnlySet<string> ReachableStations,
    bool IsAvailable = true);

public sealed record CookingStationConfig(StationSlotId Id, int Capacity, bool IsAvailable = true);

public sealed record CookingFixture(
    CookingScope Scope,
    IReadOnlyDictionary<PlayerId, CookingPlayerConfig> Players,
    IReadOnlyDictionary<StationSlotId, CookingStationConfig> Stations,
    IReadOnlyDictionary<DefinitionId, CookingItemDefinition> Definitions);

public sealed record CookingSnapshot(
    CookingScope Scope,
    long Version,
    IReadOnlyList<CookingSnapshotItem> Items,
    IReadOnlyList<CookingSnapshotSlot> Stations)
{
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public string CanonicalText()
    {
        var canonical = new CanonicalSnapshot(
            Scope.Session.Value,
            Scope.World.Value,
            Scope.Match.Value,
            Version,
            Items.OrderBy(item => item.Id.Value, StringComparer.Ordinal)
                .Select(item => new CanonicalItem(item.Id.Value, item.Definition.Value, item.Version,
                    item.Location.Kind.ToString(), item.Location.OwnerId, item.Location.SlotId))
                .ToArray(),
            Stations.OrderBy(station => station.Id.Value, StringComparer.Ordinal)
                .Select(station => new CanonicalStation(station.Id.Value, station.Capacity,
                    station.ItemIds.OrderBy(item => item.Value, StringComparer.Ordinal).Select(item => item.Value).ToArray()))
                .ToArray());
        return JsonSerializer.Serialize(canonical, CanonicalJsonOptions);
    }

    public string Sha256() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalText())));

    private sealed record CanonicalSnapshot(string SessionId, string WorldId, string MatchId, long Version,
        IReadOnlyList<CanonicalItem> Items, IReadOnlyList<CanonicalStation> Stations);
    private sealed record CanonicalItem(string ItemId, string DefinitionId, int Version, string LocationKind,
        string? OwnerId, string? SlotId);
    private sealed record CanonicalStation(string StationId, int Capacity, IReadOnlyList<string> ItemIds);
}

public sealed record CookingSnapshotItem(ItemId Id, DefinitionId Definition, int Version, ItemLocation Location);

public sealed record CookingSnapshotSlot(StationSlotId Id, int Capacity, IReadOnlyList<ItemId> ItemIds);

public sealed class CookingSimulation
{
    private readonly CookingFixture _fixture;
    private readonly Dictionary<ItemId, ItemState> _items = new();
    private readonly Dictionary<PlayerId, ItemId?> _hands = new();
    private readonly Dictionary<StationSlotId, List<ItemId>> _stationItems = new();
    private readonly Dictionary<string, ItemId> _worldItems = new(StringComparer.Ordinal);
    private readonly Dictionary<ContainerSlotKey, ItemId> _containerItems = new();
    private readonly Dictionary<ProcessedCommandKey, ProcessedCommand> _processedCommands = new();
    private readonly List<CookingEvent> _events = new();
    private long _stateVersion;
    private long _eventSequence;

    public CookingSimulation(CookingFixture fixture)
    {
        _fixture = fixture;
        foreach (var player in fixture.Players.Keys)
            _hands.Add(player, null);
        foreach (var station in fixture.Stations)
        {
            if (station.Value.Capacity < 0)
                throw new ArgumentException($"Station '{station.Key}' has negative capacity.", nameof(fixture));
            _stationItems.Add(station.Key, new List<ItemId>());
        }
    }

    public int SubmittedEnvelopeCount { get; private set; }

    public IReadOnlyList<CookingEvent> EventHistory => _events;

    public bool HasPlayer(PlayerId player) => _fixture.Players.ContainsKey(player);

    public ItemId? ItemInHand(PlayerId player) => _hands.TryGetValue(player, out var item) ? item : null;

    public IReadOnlyList<ItemId> ItemsAtStation(StationSlotId station) =>
        _stationItems.TryGetValue(station, out var items) ? items.OrderBy(item => item.Value, StringComparer.Ordinal).ToArray() : Array.Empty<ItemId>();

    public ItemId? ItemAtWorldPosition(string position) => _worldItems.TryGetValue(position, out var item) ? item : null;

    public int OccupancyCount(ItemId item) =>
        _hands.Values.Count(value => value == item) +
        _stationItems.Values.Sum(items => items.Count(value => value == item)) +
        _worldItems.Values.Count(value => value == item) +
        _containerItems.Values.Count(value => value == item);

    public void AddItem(ItemId id, DefinitionId definition, ItemLocation location, int version = 1)
    {
        if (!_fixture.Definitions.ContainsKey(definition))
            throw new ArgumentException($"Unknown definition '{definition}'.", nameof(definition));
        if (_items.ContainsKey(id))
            throw new ArgumentException($"Item '{id}' already exists.", nameof(id));
        if (version <= 0)
            throw new ArgumentOutOfRangeException(nameof(version), "Item version must be positive.");

        ValidateVacantLocation(location);
        _items.Add(id, new ItemState(definition, version, location, false));
        OccupyLocation(id, location);
    }

    public void RemoveItem(ItemId id)
    {
        if (!_items.TryGetValue(id, out var item) || item.Removed)
            return;

        VacateLocation(id, item.Location);
        _items[id] = item with { Removed = true, Version = item.Version + 1 };
        _stateVersion++;
    }

    public CommandResult Submit(CookingCommand command)
    {
        SubmittedEnvelopeCount++;
        return Execute(command);
    }

    public IReadOnlyList<CommandResult> SubmitBatch(IEnumerable<CookingCommand> commands) =>
        ExecuteBatch(commands).Select(execution => execution.Result).ToArray();

    public IReadOnlyList<CommandExecution> ExecuteBatch(IEnumerable<CookingCommand> commands)
    {
        var closedBatch = commands.ToArray();
        if (closedBatch.Length == 0)
            return Array.Empty<CommandExecution>();

        var batch = closedBatch[0].SimulationBatch;
        if (closedBatch.Any(command => command.SimulationBatch != batch))
            throw new ArgumentException("A closed batch must contain exactly one simulation batch value.", nameof(commands));

        var executions = new List<CommandExecution>(closedBatch.Length);
        foreach (var command in closedBatch
                     .OrderBy(command => command.Player.Value, StringComparer.Ordinal)
                     .ThenBy(command => command.Command.Value, StringComparer.Ordinal))
        {
            var before = Snapshot();
            var result = Execute(command);
            executions.Add(new CommandExecution(command, result, before, Snapshot()));
        }

        return executions;
    }

    public CookingSnapshot Snapshot() => new(
        _fixture.Scope,
        _stateVersion,
        _items.Where(pair => !pair.Value.Removed)
            .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
            .Select(pair => new CookingSnapshotItem(pair.Key, pair.Value.Definition, pair.Value.Version, pair.Value.Location))
            .ToArray(),
        _stationItems.OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
            .Select(pair => new CookingSnapshotSlot(pair.Key, _fixture.Stations[pair.Key].Capacity,
                pair.Value.OrderBy(id => id.Value, StringComparer.Ordinal).ToArray()))
            .ToArray());

    private CommandResult Execute(CookingCommand command)
    {
        var key = new ProcessedCommandKey(command.Scope.Session, command.Player, command.Command);
        var fingerprint = Fingerprint(command);
        if (_processedCommands.TryGetValue(key, out var processed))
        {
            if (!StringComparer.Ordinal.Equals(processed.Fingerprint, fingerprint))
                return CommandResult.Reject(RejectionReason.CommandIdentityConflict, _stateVersion);

            return processed.Result with { IsDuplicate = true, Events = Array.Empty<CookingEvent>() };
        }

        var result = ValidateAndCommit(command);
        _processedCommands.Add(key, new ProcessedCommand(fingerprint, result));
        return result;
    }

    private CommandResult ValidateAndCommit(CookingCommand command)
    {
        if (!Equals(command.Scope, _fixture.Scope))
            return CommandResult.Reject(RejectionReason.ScopeMismatch, _stateVersion);
        if (!_fixture.Players.TryGetValue(command.Player, out var player))
            return CommandResult.Reject(RejectionReason.PlayerNotFound, _stateVersion);
        if (!player.IsAvailable)
            return CommandResult.Reject(RejectionReason.PlayerUnavailable, _stateVersion);
        if (!_items.TryGetValue(command.Item, out var item) || item.Removed)
            return CommandResult.Reject(RejectionReason.ItemNotFound, _stateVersion);
        if (item.Version != command.ExpectedItemVersion)
            return CommandResult.Reject(RejectionReason.ItemStale, _stateVersion);
        if (!_fixture.Definitions.TryGetValue(item.Definition, out var definition) ||
            !definition.AllowedPlayerCapabilities.Overlaps(player.Capabilities))
            return CommandResult.Reject(RejectionReason.PlayerIneligible, _stateVersion);

        return command.Operation switch
        {
            CookingOperation.Pickup => CommitPickup(command, item),
            CookingOperation.Drop => CommitDrop(command, item, player),
            _ => throw new ArgumentOutOfRangeException(nameof(command.Operation), command.Operation, null),
        };
    }

    private CommandResult CommitPickup(CookingCommand command, ItemState item)
    {
        if (item.Location.Kind is not (LocationKind.WorldPosition or LocationKind.StationSlot))
            return CommandResult.Reject(RejectionReason.CurrentLocationMismatch, _stateVersion);
        if (_hands[command.Player] is not null)
            return CommandResult.Reject(RejectionReason.CapacityFull, _stateVersion);

        return CommitMove(command, item, ItemLocation.Hand(command.Player));
    }

    private CommandResult CommitDrop(CookingCommand command, ItemState item, CookingPlayerConfig player)
    {
        if (item.Location != ItemLocation.Hand(command.Player))
            return CommandResult.Reject(RejectionReason.CurrentLocationMismatch, _stateVersion);
        if (command.TargetStation is not { } stationId || !_fixture.Stations.TryGetValue(stationId, out var station) || !station.IsAvailable)
            return CommandResult.Reject(RejectionReason.TargetUnavailable, _stateVersion);
        if (!player.ReachableStations.Contains(stationId.Value))
            return CommandResult.Reject(RejectionReason.TargetOutOfRange, _stateVersion);
        if (_stationItems[stationId].Count >= station.Capacity)
            return CommandResult.Reject(RejectionReason.CapacityFull, _stateVersion);

        return CommitMove(command, item, ItemLocation.Station(stationId));
    }

    private CommandResult CommitMove(CookingCommand command, ItemState item, ItemLocation destination)
    {
        var source = item.Location;
        VacateLocation(command.Item, source);
        OccupyLocation(command.Item, destination);
        _items[command.Item] = item with { Location = destination, Version = item.Version + 1 };
        _stateVersion++;

        var @event = new CookingEvent(++_eventSequence, command.SimulationBatch, command.Player, command.Command,
            command.Item, command.Operation, source, destination);
        _events.Add(@event);
        return new CommandResult(CommandOutcome.Accepted, RejectionReason.None, _stateVersion, false, new[] { @event });
    }

    private void ValidateVacantLocation(ItemLocation location)
    {
        switch (location.Kind)
        {
            case LocationKind.WorldPosition:
                RequireNonBlank(location.SlotId, "World position");
                if (_worldItems.ContainsKey(location.SlotId!))
                    throw new InvalidOperationException($"World position '{location.SlotId}' is already occupied.");
                break;
            case LocationKind.PlayerHand:
                RequireNonBlank(location.OwnerId, "Hand player");
                var player = new PlayerId(location.OwnerId!);
                if (!_hands.TryGetValue(player, out var hand))
                    throw new ArgumentException($"Unknown player '{player}'.", nameof(location));
                if (hand is not null)
                    throw new InvalidOperationException($"Player '{player}' hand is already occupied.");
                break;
            case LocationKind.StationSlot:
                RequireNonBlank(location.SlotId, "Station slot");
                var station = new StationSlotId(location.SlotId!);
                if (!_stationItems.TryGetValue(station, out var stationItems))
                    throw new ArgumentException($"Unknown station '{station}'.", nameof(location));
                if (stationItems.Count >= _fixture.Stations[station].Capacity)
                    throw new InvalidOperationException($"Station '{station}' is already full.");
                break;
            case LocationKind.ContainerSlot:
                RequireNonBlank(location.OwnerId, "Container item");
                RequireNonBlank(location.SlotId, "Container slot");
                var container = new ItemId(location.OwnerId!);
                if (!_items.TryGetValue(container, out var containerItem) || containerItem.Removed)
                    throw new ArgumentException($"Unknown active container '{container}'.", nameof(location));
                if (_containerItems.ContainsKey(new ContainerSlotKey(container, location.SlotId!)))
                    throw new InvalidOperationException($"Container slot '{container}/{location.SlotId}' is already occupied.");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(location), location.Kind, null);
        }
    }

    private void OccupyLocation(ItemId item, ItemLocation location)
    {
        switch (location.Kind)
        {
            case LocationKind.WorldPosition:
                _worldItems.Add(location.SlotId!, item);
                break;
            case LocationKind.PlayerHand:
                _hands[new PlayerId(location.OwnerId!)] = item;
                break;
            case LocationKind.StationSlot:
                _stationItems[new StationSlotId(location.SlotId!)].Add(item);
                break;
            case LocationKind.ContainerSlot:
                _containerItems.Add(new ContainerSlotKey(new ItemId(location.OwnerId!), location.SlotId!), item);
                break;
        }
    }

    private void VacateLocation(ItemId item, ItemLocation location)
    {
        switch (location.Kind)
        {
            case LocationKind.WorldPosition:
                _worldItems.Remove(location.SlotId!);
                break;
            case LocationKind.PlayerHand:
                _hands[new PlayerId(location.OwnerId!)] = null;
                break;
            case LocationKind.StationSlot:
                _stationItems[new StationSlotId(location.SlotId!)].Remove(item);
                break;
            case LocationKind.ContainerSlot:
                _containerItems.Remove(new ContainerSlotKey(new ItemId(location.OwnerId!), location.SlotId!));
                break;
        }
    }

    private static string Fingerprint(CookingCommand command) =>
        JsonSerializer.Serialize(command, FingerprintJsonOptions);

    private static void RequireNonBlank(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{label} must not be blank.");
    }

    private static readonly JsonSerializerOptions FingerprintJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private sealed record ItemState(DefinitionId Definition, int Version, ItemLocation Location, bool Removed);
    private sealed record ProcessedCommandKey(SessionId Session, PlayerId Player, CommandId Command);
    private sealed record ProcessedCommand(string Fingerprint, CommandResult Result);
    private sealed record ContainerSlotKey(ItemId Container, string Slot);
}

public interface ICookingCommandAdapter
{
    CommandResult Submit(CookingCommand command);
}

public sealed class HostLocalAdapter(CookingSimulation simulation) : ICookingCommandAdapter
{
    public CommandResult Submit(CookingCommand command) => simulation.Submit(command);
}

public sealed class RemoteInProcessAdapter(CookingSimulation simulation) : ICookingCommandAdapter
{
    public CommandResult Submit(CookingCommand command) => simulation.Submit(command);
}

public sealed record CookingAcceptanceEvidence(
    string TestId,
    string FixtureId,
    string SessionId,
    string WorldId,
    string MatchId,
    long SimulationBatch,
    string SortKey,
    CookingCommand Command,
    string Outcome,
    string RejectionReason,
    bool IsDuplicate,
    IReadOnlyList<CookingEvent> Events,
    string BeforeStateHash,
    string AfterStateHash,
    string AssertionSummary,
    string Runner,
    string TimestampUtc);

public static class CookingAcceptanceEvidenceWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static void Append(string path, CookingAcceptanceEvidence evidence)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new ArgumentException("Evidence path has no directory.", nameof(path)));
        File.AppendAllText(path, JsonSerializer.Serialize(evidence, Options) + Environment.NewLine, Encoding.UTF8);
    }

    public static IReadOnlyList<CookingAcceptanceEvidence> ReadAll(string path)
    {
        return File.ReadLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<CookingAcceptanceEvidence>(line, Options)
                ?? throw new InvalidDataException("Invalid cooking acceptance evidence line."))
            .ToArray();
    }
}
