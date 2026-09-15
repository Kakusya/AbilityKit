using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AbilityKit.Game.Cooking;

public readonly record struct RecipeId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct ProcessId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct ContainerId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct OrderId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct RecipeCommandId(string Value)
{
    public override string ToString() => Value;
}

public sealed record CookingRecipeDefinition(
    RecipeId Id,
    DefinitionId InputDefinition,
    DefinitionId ProductDefinition,
    ProcessId Process,
    string RequiredApplianceCapability,
    int RequiredTicks);

public sealed record CookingApplianceDefinition(
    StationSlotId Station,
    IReadOnlySet<string> Capabilities,
    bool IsAvailable = true);

public sealed record CookingContainerDefinition(ContainerId Id, int Capacity);

public sealed record CookingRecipeFixture
{
    public CookingRecipeFixture(
        CookingScope scope,
        IReadOnlyDictionary<PlayerId, CookingPlayerConfig> players,
        IReadOnlyDictionary<DefinitionId, CookingItemDefinition> items,
        IReadOnlyDictionary<StationSlotId, CookingApplianceDefinition> appliances,
        IReadOnlyDictionary<RecipeId, CookingRecipeDefinition> recipes,
        IReadOnlyDictionary<ContainerId, CookingContainerDefinition> containers)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(appliances);
        ArgumentNullException.ThrowIfNull(recipes);
        ArgumentNullException.ThrowIfNull(containers);
        foreach (var recipe in recipes.Values)
        {
            if (!items.ContainsKey(recipe.InputDefinition) || !items.ContainsKey(recipe.ProductDefinition))
                throw new ArgumentException($"Recipe '{recipe.Id}' references an unknown item definition.", nameof(recipes));
            if (recipe.RequiredTicks <= 0)
                throw new ArgumentOutOfRangeException(nameof(recipes), $"Recipe '{recipe.Id}' must require a positive tick count.");
            if (string.IsNullOrWhiteSpace(recipe.RequiredApplianceCapability))
                throw new ArgumentException($"Recipe '{recipe.Id}' has a blank appliance capability.", nameof(recipes));
        }
        foreach (var container in containers.Values)
        {
            if (container.Capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(containers), $"Container '{container.Id}' must have positive capacity.");
        }

        Scope = scope;
        Players = players;
        Items = items;
        Appliances = appliances;
        Recipes = recipes;
        Containers = containers;
    }

    public CookingScope Scope { get; init; }
    public IReadOnlyDictionary<PlayerId, CookingPlayerConfig> Players { get; init; }
    public IReadOnlyDictionary<DefinitionId, CookingItemDefinition> Items { get; init; }
    public IReadOnlyDictionary<StationSlotId, CookingApplianceDefinition> Appliances { get; init; }
    public IReadOnlyDictionary<RecipeId, CookingRecipeDefinition> Recipes { get; init; }
    public IReadOnlyDictionary<ContainerId, CookingContainerDefinition> Containers { get; init; }
}

public enum CookingRecipeOperation
{
    StartProcess,
    AdvanceTicks,
    Plate,
    SubmitOrder,
}

public enum CookingRecipeOutcome
{
    Accepted,
    Rejected,
}

public enum CookingRecipeRejectionReason
{
    None,
    ScopeMismatch,
    PlayerNotFound,
    PlayerUnavailable,
    RecipeNotFound,
    ProcessNotFound,
    ItemNotFound,
    ItemStale,
    CurrentLocationMismatch,
    PlayerIneligible,
    ApplianceNotFound,
    ApplianceUnavailable,
    ApplianceCapabilityMismatch,
    TargetOutOfRange,
    ProcessNotComplete,
    ContainerNotFound,
    ContainerFull,
    ProductNotFound,
    ProductAlreadyConsumed,
    ProductNotPlated,
    OrderRejected,
    CommandIdentityConflict,
}

public sealed record CookingRecipeCommand(
    CookingScope Scope,
    long SimulationBatch,
    PlayerId Player,
    RecipeCommandId Command,
    CookingRecipeOperation Operation,
    RecipeId? Recipe = null,
    ProcessId? Process = null,
    ItemId? Item = null,
    StationSlotId? Station = null,
    ContainerId? Container = null,
    OrderId? Order = null,
    int ExpectedItemVersion = 0,
    int TickCount = 0);

public sealed record CookingRecipeEvent(
    long Sequence,
    long SimulationBatch,
    PlayerId Player,
    RecipeCommandId Command,
    CookingRecipeOperation Operation,
    RecipeId? Recipe,
    ProcessId? Process,
    ItemId? Item,
    string Summary);

public sealed record CookingRecipeCommandResult(
    CookingRecipeOutcome Outcome,
    CookingRecipeRejectionReason Reason,
    long StateVersion,
    bool IsDuplicate,
    IReadOnlyList<CookingRecipeEvent> Events)
{
    public static CookingRecipeCommandResult Reject(CookingRecipeRejectionReason reason, long stateVersion) =>
        new(CookingRecipeOutcome.Rejected, reason, stateVersion, false, Array.Empty<CookingRecipeEvent>());
}

public sealed record CookingOrderSubmission(
    OrderId Order,
    RecipeId Recipe,
    ItemId Product,
    PlayerId Player);

public sealed record CookingOrderAcceptance(bool Accepted, string ReasonCode = "None");

public interface ICookingOrderPort
{
    CookingOrderAcceptance Submit(CookingOrderSubmission submission);
}

public sealed class CookingRecipeSimulation
{
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly CookingRecipeFixture _fixture;
    private readonly ICookingOrderPort _orderPort;
    private readonly Dictionary<ItemId, ItemState> _items = new();
    private readonly Dictionary<PlayerId, ItemId?> _hands = new();
    private readonly Dictionary<StationSlotId, ProcessState> _processesByStation = new();
    private readonly Dictionary<ProcessId, StationSlotId> _stationsByProcess = new();
    private readonly Dictionary<ContainerId, List<ItemId>> _containerItems = new();
    private readonly HashSet<ItemId> _consumedProducts = new();
    private readonly HashSet<OrderId> _acceptedOrders = new();
    private readonly Dictionary<RecipeCommandKey, ProcessedCommand> _processedCommands = new();
    private readonly List<CookingRecipeEvent> _events = new();
    private long _stateVersion;
    private long _eventSequence;
    private long _nextProcessId;
    private long _nextProductId;

    public CookingRecipeSimulation(CookingRecipeFixture fixture, ICookingOrderPort orderPort)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        _orderPort = orderPort ?? throw new ArgumentNullException(nameof(orderPort));
        foreach (var player in fixture.Players.Keys)
            _hands.Add(player, null);
        foreach (var container in fixture.Containers.Keys)
            _containerItems.Add(container, new List<ItemId>());
    }

    public long LogicalTick { get; private set; }

    public IReadOnlyList<CookingRecipeEvent> EventHistory => _events;

    public ItemId? ItemInHand(PlayerId player) => _hands.TryGetValue(player, out var item) ? item : null;

    public IReadOnlyList<ItemId> ItemsInContainer(ContainerId container) =>
        _containerItems.TryGetValue(container, out var items)
            ? items.OrderBy(item => item.Value, StringComparer.Ordinal).ToArray()
            : Array.Empty<ItemId>();

    public CookingRecipeSnapshot Snapshot() => new(
        _fixture.Scope,
        _stateVersion,
        LogicalTick,
        _items.Where(pair => !pair.Value.Removed)
            .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
            .Select(pair => new CookingRecipeSnapshotItem(pair.Key, pair.Value.Definition, pair.Value.Version, pair.Value.Location,
                pair.Value.Recipe, pair.Value.IsProduct, pair.Value.OriginStation))
            .ToArray(),
        _processesByStation.Values
            .OrderBy(process => process.Id.Value, StringComparer.Ordinal)
            .Select(process => new CookingRecipeSnapshotProcess(process.Id, process.Recipe, process.Player, process.Input,
                process.Station, process.ElapsedTicks, process.RequiredTicks))
            .ToArray(),
        _containerItems.OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
            .Select(pair => new CookingRecipeSnapshotContainer(pair.Key, _fixture.Containers[pair.Key].Capacity,
                pair.Value.OrderBy(item => item.Value, StringComparer.Ordinal).ToArray()))
            .ToArray(),
        _acceptedOrders.OrderBy(order => order.Value, StringComparer.Ordinal).ToArray());

    public void AddIngredient(ItemId id, DefinitionId definition, PlayerId player, int version = 1)
    {
        if (!_fixture.Items.ContainsKey(definition))
            throw new ArgumentException($"Unknown item definition '{definition}'.", nameof(definition));
        if (!_fixture.Players.ContainsKey(player))
            throw new ArgumentException($"Unknown player '{player}'.", nameof(player));
        if (_items.ContainsKey(id))
            throw new ArgumentException($"Item '{id}' already exists.", nameof(id));
        if (version <= 0)
            throw new ArgumentOutOfRangeException(nameof(version));
        if (_hands[player] is not null)
            throw new InvalidOperationException($"Player '{player}' already holds an item.");

        _items.Add(id, new ItemState(definition, version, ItemLocation.Hand(player), false, null, false, null));
        _hands[player] = id;
    }

    public CookingRecipeCommandResult Submit(CookingRecipeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var key = new RecipeCommandKey(command.Scope.Session, command.Player, command.Command);
        var fingerprint = JsonSerializer.Serialize(command, CanonicalJsonOptions);
        if (_processedCommands.TryGetValue(key, out var processed))
        {
            if (!StringComparer.Ordinal.Equals(processed.Fingerprint, fingerprint))
                return CookingRecipeCommandResult.Reject(CookingRecipeRejectionReason.CommandIdentityConflict, _stateVersion);
            return processed.Result with { IsDuplicate = true, Events = Array.Empty<CookingRecipeEvent>() };
        }

        var result = command.Operation switch
        {
            CookingRecipeOperation.StartProcess => StartProcess(command),
            CookingRecipeOperation.AdvanceTicks => AdvanceTicks(command),
            CookingRecipeOperation.Plate => Plate(command),
            CookingRecipeOperation.SubmitOrder => SubmitOrder(command),
            _ => throw new ArgumentOutOfRangeException(nameof(command.Operation), command.Operation, null),
        };
        _processedCommands.Add(key, new ProcessedCommand(fingerprint, result));
        return result;
    }

    private CookingRecipeCommandResult StartProcess(CookingRecipeCommand command)
    {
        if (!TryValidateCommandScopeAndPlayer(command, out var player, out var rejection))
            return Reject(rejection);
        if (command.Recipe is not { } recipeId || !_fixture.Recipes.TryGetValue(recipeId, out var recipe))
            return Reject(CookingRecipeRejectionReason.RecipeNotFound);
        if (command.Item is not { } itemId || !_items.TryGetValue(itemId, out var item) || item.Removed)
            return Reject(CookingRecipeRejectionReason.ItemNotFound);
        if (item.Version != command.ExpectedItemVersion)
            return Reject(CookingRecipeRejectionReason.ItemStale);
        if (item.Location != ItemLocation.Hand(command.Player))
            return Reject(CookingRecipeRejectionReason.CurrentLocationMismatch);
        if (item.Definition != recipe.InputDefinition)
            return Reject(CookingRecipeRejectionReason.ItemNotFound);
        if (command.Station is not { } stationId || !_fixture.Appliances.TryGetValue(stationId, out var appliance))
            return Reject(CookingRecipeRejectionReason.ApplianceNotFound);
        if (!appliance.IsAvailable)
            return Reject(CookingRecipeRejectionReason.ApplianceUnavailable);
        if (!appliance.Capabilities.Contains(recipe.RequiredApplianceCapability))
            return Reject(CookingRecipeRejectionReason.ApplianceCapabilityMismatch);
        if (!player.ReachableStations.Contains(stationId.Value))
            return Reject(CookingRecipeRejectionReason.TargetOutOfRange);
        if (_processesByStation.ContainsKey(stationId))
            return Reject(CookingRecipeRejectionReason.ApplianceUnavailable);
        if (!_fixture.Items[item.Definition].AllowedPlayerCapabilities.Overlaps(player.Capabilities))
            return Reject(CookingRecipeRejectionReason.PlayerIneligible);

        var processId = new ProcessId($"process-{++_nextProcessId}");
        _hands[command.Player] = null;
        _items[itemId] = item with { Location = ItemLocation.Station(stationId), Version = item.Version + 1 };
        var process = new ProcessState(processId, recipe.Id, command.Player, itemId, stationId, 0, recipe.RequiredTicks);
        _processesByStation.Add(stationId, process);
        _stationsByProcess.Add(processId, stationId);
        return Commit(command, recipe.Id, processId, itemId, "process-started");
    }

    private CookingRecipeCommandResult AdvanceTicks(CookingRecipeCommand command)
    {
        if (!TryValidateCommandScopeAndPlayer(command, out _, out var rejection))
            return Reject(rejection);
        if (command.Process is not { } processId || !_stationsByProcess.TryGetValue(processId, out var stationId) ||
            !_processesByStation.TryGetValue(stationId, out var process))
            return Reject(CookingRecipeRejectionReason.ProcessNotFound);
        if (command.TickCount <= 0)
            return Reject(CookingRecipeRejectionReason.ProcessNotComplete);
        if (process.Player != command.Player)
            return Reject(CookingRecipeRejectionReason.PlayerIneligible);

        LogicalTick += command.TickCount;
        var progressed = process with { ElapsedTicks = Math.Min(process.RequiredTicks, process.ElapsedTicks + command.TickCount) };
        if (progressed.ElapsedTicks < progressed.RequiredTicks)
        {
            _processesByStation[stationId] = progressed;
            return Commit(command, progressed.Recipe, progressed.Id, progressed.Input, "process-progressed");
        }

        var recipe = _fixture.Recipes[process.Recipe];
        var input = _items[process.Input];
        _items[process.Input] = input with { Removed = true, Version = input.Version + 1 };
        _processesByStation.Remove(stationId);
        _stationsByProcess.Remove(progressed.Id);
        var productId = new ItemId($"product-{++_nextProductId}");
        _items.Add(productId, new ItemState(recipe.ProductDefinition, 1, ItemLocation.Station(stationId), false, recipe.Id, true, stationId));
        return Commit(command, recipe.Id, progressed.Id, productId, "process-completed");
    }

    private CookingRecipeCommandResult Plate(CookingRecipeCommand command)
    {
        if (!TryValidateCommandScopeAndPlayer(command, out var player, out var rejection))
            return Reject(rejection);
        if (command.Item is not { } productId || !_items.TryGetValue(productId, out var product) || product.Removed || !product.IsProduct)
            return Reject(_consumedProducts.Contains(command.Item ?? default) ? CookingRecipeRejectionReason.ProductAlreadyConsumed : CookingRecipeRejectionReason.ProductNotFound);
        if (product.Version != command.ExpectedItemVersion)
            return Reject(CookingRecipeRejectionReason.ItemStale);
        if (product.Location.Kind != LocationKind.StationSlot)
            return Reject(CookingRecipeRejectionReason.CurrentLocationMismatch);
        if (product.Location.SlotId is null || !player.ReachableStations.Contains(product.Location.SlotId))
            return Reject(CookingRecipeRejectionReason.TargetOutOfRange);
        if (command.Container is not { } containerId || !_fixture.Containers.TryGetValue(containerId, out var container))
            return Reject(CookingRecipeRejectionReason.ContainerNotFound);
        if (_containerItems[containerId].Count >= container.Capacity)
            return Reject(CookingRecipeRejectionReason.ContainerFull);

        var slot = NextVacantContainerSlot(containerId);
        _containerItems[containerId].Add(productId);
        _items[productId] = product with { Location = ItemLocation.Container(new ItemId(containerId.Value), slot), Version = product.Version + 1 };
        return Commit(command, product.Recipe, null, productId, "product-plated");
    }

    private CookingRecipeCommandResult SubmitOrder(CookingRecipeCommand command)
    {
        if (!TryValidateCommandScopeAndPlayer(command, out var player, out var rejection))
            return Reject(rejection);
        if (command.Item is not { } productId || !_items.TryGetValue(productId, out var product) || product.Removed || !product.IsProduct)
            return Reject(_consumedProducts.Contains(command.Item ?? default) ? CookingRecipeRejectionReason.ProductAlreadyConsumed : CookingRecipeRejectionReason.ProductNotFound);
        if (command.Order is not { } orderId)
            return Reject(CookingRecipeRejectionReason.OrderRejected);
        if (_acceptedOrders.Contains(orderId))
            return Reject(CookingRecipeRejectionReason.OrderRejected);
        if (product.Version != command.ExpectedItemVersion)
            return Reject(CookingRecipeRejectionReason.ItemStale);
        if (product.Location.Kind != LocationKind.ContainerSlot)
            return Reject(CookingRecipeRejectionReason.ProductNotPlated);
        if (product.Location.OwnerId is null || !ContainerIsReachableFromProductStation(product, player))
            return Reject(CookingRecipeRejectionReason.TargetOutOfRange);
        if (product.Recipe is not { } recipe)
            return Reject(CookingRecipeRejectionReason.ProductNotFound);

        var acceptance = _orderPort.Submit(new CookingOrderSubmission(orderId, recipe, productId, command.Player));
        if (!acceptance.Accepted)
            return Reject(CookingRecipeRejectionReason.OrderRejected);

        var container = new ContainerId(product.Location.OwnerId!);
        _containerItems[container].Remove(productId);
        _items[productId] = product with { Removed = true, Version = product.Version + 1 };
        _consumedProducts.Add(productId);
        _acceptedOrders.Add(orderId);
        return Commit(command, recipe, null, productId, "order-submitted");
    }

    private static bool ContainerIsReachableFromProductStation(ItemState product, CookingPlayerConfig player) =>
        product.OriginStation is { } station && player.ReachableStations.Contains(station.Value);

    private string NextVacantContainerSlot(ContainerId container)
    {
        var occupied = _containerItems[container]
            .Select(item => _items[item].Location.SlotId)
            .Where(slot => slot is not null)
            .ToHashSet(StringComparer.Ordinal);
        for (var index = 0; ; index++)
        {
            var slot = $"slot-{index}";
            if (!occupied.Contains(slot))
                return slot;
        }
    }

    private bool TryValidateCommandScopeAndPlayer(CookingRecipeCommand command, out CookingPlayerConfig player,
        out CookingRecipeRejectionReason rejection)
    {
        if (!Equals(command.Scope, _fixture.Scope))
        {
            player = default!;
            rejection = CookingRecipeRejectionReason.ScopeMismatch;
            return false;
        }
        if (!_fixture.Players.TryGetValue(command.Player, out player!))
        {
            rejection = CookingRecipeRejectionReason.PlayerNotFound;
            return false;
        }
        if (!player.IsAvailable)
        {
            rejection = CookingRecipeRejectionReason.PlayerUnavailable;
            return false;
        }
        rejection = CookingRecipeRejectionReason.None;
        return true;
    }

    private CookingRecipeCommandResult Commit(CookingRecipeCommand command, RecipeId? recipe, ProcessId? process, ItemId? item,
        string summary)
    {
        _stateVersion++;
        var @event = new CookingRecipeEvent(++_eventSequence, command.SimulationBatch, command.Player, command.Command,
            command.Operation, recipe, process, item, summary);
        _events.Add(@event);
        return new CookingRecipeCommandResult(CookingRecipeOutcome.Accepted, CookingRecipeRejectionReason.None, _stateVersion,
            false, new[] { @event });
    }

    private CookingRecipeCommandResult Reject(CookingRecipeRejectionReason reason) =>
        CookingRecipeCommandResult.Reject(reason, _stateVersion);

    private sealed record ItemState(
        DefinitionId Definition,
        int Version,
        ItemLocation Location,
        bool Removed,
        RecipeId? Recipe,
        bool IsProduct,
        StationSlotId? OriginStation);

    private sealed record ProcessState(
        ProcessId Id,
        RecipeId Recipe,
        PlayerId Player,
        ItemId Input,
        StationSlotId Station,
        int ElapsedTicks,
        int RequiredTicks);

    private sealed record RecipeCommandKey(SessionId Session, PlayerId Player, RecipeCommandId Command);

    private sealed record ProcessedCommand(string Fingerprint, CookingRecipeCommandResult Result);
}

public sealed record CookingRecipeSnapshot(
    CookingScope Scope,
    long Version,
    long LogicalTick,
    IReadOnlyList<CookingRecipeSnapshotItem> Items,
    IReadOnlyList<CookingRecipeSnapshotProcess> Processes,
    IReadOnlyList<CookingRecipeSnapshotContainer> Containers,
    IReadOnlyList<OrderId> AcceptedOrders)
{
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public string CanonicalText() => JsonSerializer.Serialize(new CanonicalSnapshot(
        Scope.Session.Value,
        Scope.World.Value,
        Scope.Match.Value,
        Version,
        LogicalTick,
        Items.OrderBy(item => item.Id.Value, StringComparer.Ordinal)
            .Select(item => new CanonicalItem(item.Id.Value, item.Definition.Value, item.Version, item.Location.Kind.ToString(),
                item.Location.OwnerId, item.Location.SlotId, item.Recipe?.Value, item.IsProduct, item.OriginStation?.Value)).ToArray(),
        Processes.OrderBy(process => process.Id.Value, StringComparer.Ordinal)
            .Select(process => new CanonicalProcess(process.Id.Value, process.Recipe.Value, process.Player.Value,
                process.Input.Value, process.Station.Value, process.ElapsedTicks, process.RequiredTicks)).ToArray(),
        Containers.OrderBy(container => container.Id.Value, StringComparer.Ordinal)
            .Select(container => new CanonicalContainer(container.Id.Value, container.Capacity,
                container.ItemIds.OrderBy(item => item.Value, StringComparer.Ordinal).Select(item => item.Value).ToArray())).ToArray(),
        AcceptedOrders.OrderBy(order => order.Value, StringComparer.Ordinal).Select(order => order.Value).ToArray()),
        CanonicalJsonOptions);

    public string Sha256() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalText())));

    private sealed record CanonicalSnapshot(string SessionId, string WorldId, string MatchId, long Version, long LogicalTick,
        IReadOnlyList<CanonicalItem> Items, IReadOnlyList<CanonicalProcess> Processes,
        IReadOnlyList<CanonicalContainer> Containers, IReadOnlyList<string> AcceptedOrders);
    private sealed record CanonicalItem(string ItemId, string DefinitionId, int Version, string LocationKind, string? OwnerId,
        string? SlotId, string? RecipeId, bool IsProduct, string? OriginStation);
    private sealed record CanonicalProcess(string ProcessId, string RecipeId, string PlayerId, string InputItemId,
        string StationId, int ElapsedTicks, int RequiredTicks);
    private sealed record CanonicalContainer(string ContainerId, int Capacity, IReadOnlyList<string> ItemIds);
}

public sealed record CookingRecipeSnapshotItem(ItemId Id, DefinitionId Definition, int Version, ItemLocation Location,
    RecipeId? Recipe, bool IsProduct, StationSlotId? OriginStation);

public sealed record CookingRecipeSnapshotProcess(ProcessId Id, RecipeId Recipe, PlayerId Player, ItemId Input,
    StationSlotId Station, int ElapsedTicks, int RequiredTicks);

public sealed record CookingRecipeSnapshotContainer(ContainerId Id, int Capacity, IReadOnlyList<ItemId> ItemIds);

public sealed record CookingRecipeAcceptanceEvidence(
    string TestId,
    string FixtureId,
    CookingRecipeCommand Command,
    long LogicalTick,
    string Outcome,
    string RejectionReason,
    bool IsDuplicate,
    IReadOnlyList<CookingRecipeEvent> Events,
    string BeforeStateHash,
    string AfterStateHash,
    string AssertionSummary,
    string Runner,
    string TimestampUtc);

public static class CookingRecipeAcceptanceEvidenceWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static void Append(string path, CookingRecipeAcceptanceEvidence evidence)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new ArgumentException("Evidence path has no directory.", nameof(path)));
        File.AppendAllText(path, JsonSerializer.Serialize(evidence, Options) + Environment.NewLine, Encoding.UTF8);
    }

    public static IReadOnlyList<CookingRecipeAcceptanceEvidence> ReadAll(string path) =>
        File.ReadLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<CookingRecipeAcceptanceEvidence>(line, Options)
                ?? throw new InvalidDataException("Invalid cooking recipe evidence line."))
            .ToArray();
}
