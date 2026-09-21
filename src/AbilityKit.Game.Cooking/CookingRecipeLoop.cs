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

/// <summary>
/// 加工完成形态。<see cref="ConsumeInputs"/> 在到点时消耗输入并在工位或容器上生成输出；
/// <see cref="RetainInputs"/> 保留输入、容器切“已完成”，成品在倒出时才生成。
/// </summary>
public enum CookingRecipeCompletionKind
{
    ConsumeInputs,
    RetainInputs,
}

public sealed record CookingRecipeDefinition(
    RecipeId Id,
    IReadOnlyList<DefinitionId> Inputs,
    DefinitionId ProductDefinition,
    ProcessId Process,
    string RequiredApplianceCapability,
    int RequiredTicks,
    IReadOnlyList<DefinitionId>? DefaultInputs = null,
    CookingRecipeCompletionKind Completion = CookingRecipeCompletionKind.ConsumeInputs,
    bool RequiresStation = true);

public sealed record CookingApplianceDefinition(
    StationSlotId Station,
    IReadOnlySet<string> Capabilities,
    bool IsAvailable = true);

public sealed record CookingRecipeFixture
{
    public CookingRecipeFixture(
        CookingScope scope,
        IReadOnlyDictionary<PlayerId, CookingPlayerConfig> players,
        IReadOnlyDictionary<DefinitionId, CookingItemDefinition> items,
        IReadOnlyDictionary<StationSlotId, CookingApplianceDefinition> appliances,
        IReadOnlyDictionary<RecipeId, CookingRecipeDefinition> recipes,
        IReadOnlySet<DefinitionId>? washableContainerDefinitions = null,
        IReadOnlyDictionary<DefinitionId, int>? cleanContainerSupply = null,
        string? cleanPoolLocation = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(appliances);
        ArgumentNullException.ThrowIfNull(recipes);
        foreach (var recipe in recipes.Values)
        {
            if (recipe.Inputs.Count == 0)
                throw new ArgumentException($"Recipe '{recipe.Id}' must declare at least one input.", nameof(recipes));
            if (recipe.Inputs.Any(input => !items.ContainsKey(input)) || !items.ContainsKey(recipe.ProductDefinition))
                throw new ArgumentException($"Recipe '{recipe.Id}' references an unknown item definition.", nameof(recipes));
            if (recipe.DefaultInputs is { } defaultInputs && defaultInputs.Any(input => !items.ContainsKey(input)))
                throw new ArgumentException($"Recipe '{recipe.Id}' references an unknown default input.", nameof(recipes));
            if (recipe.RequiredTicks <= 0)
                throw new ArgumentOutOfRangeException(nameof(recipes), $"Recipe '{recipe.Id}' must require a positive tick count.");
            if (string.IsNullOrWhiteSpace(recipe.RequiredApplianceCapability))
                throw new ArgumentException($"Recipe '{recipe.Id}' has a blank appliance capability.", nameof(recipes));
        }

        Scope = scope;
        Players = players;
        Items = items;
        Appliances = appliances;
        Recipes = recipes;
        WashableContainerDefinitions = washableContainerDefinitions ?? new HashSet<DefinitionId>();
        CleanContainerSupply = cleanContainerSupply ?? new Dictionary<DefinitionId, int>();
        CleanPoolLocation = cleanPoolLocation ?? "clean-pool";
    }

    public CookingScope Scope { get; init; }
    public IReadOnlyDictionary<PlayerId, CookingPlayerConfig> Players { get; init; }
    public IReadOnlyDictionary<DefinitionId, CookingItemDefinition> Items { get; init; }
    public IReadOnlyDictionary<StationSlotId, CookingApplianceDefinition> Appliances { get; init; }
    public IReadOnlyDictionary<RecipeId, CookingRecipeDefinition> Recipes { get; init; }
    public IReadOnlySet<DefinitionId> WashableContainerDefinitions { get; init; }
    public IReadOnlyDictionary<DefinitionId, int> CleanContainerSupply { get; init; }
    public string CleanPoolLocation { get; init; }
}

/// <summary>
/// owner 确认的七项基础动作加 fixed-tick 命令路径。拾取/放下/放入/取出/倒出是物品移动原子命令；
/// 启动加工创建进程；AdvanceTicks 是 legacy 多 tick 命令路径（fixed tick 之外由测试保护）。
/// </summary>
public enum CookingRecipeOperation
{
    Pickup,
    StartProcess,
    AdvanceTicks,
    SubmitOrder,
    Drop,
    PutIn,
    TakeOut,
    Pour,
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
    IngredientAlreadyCollected,
    OrderRejected,
    LifecycleClosed,
    CommandIdentityConflict,
    MalformedCommand,
    QueueFull,
    RecipeNotMatched,
    RecipeAmbiguous,
    ContainerRejectsItem,
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
    ItemId? Container = null,
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

public sealed record CookingRecipeExecution(
    CookingRecipeCommand Command,
    CookingRecipeCommandResult Result,
    CookingRecipeSnapshot Before,
    CookingRecipeSnapshot After);

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

public sealed record CookingRecipeProcessTickResult(
    ProcessId Process,
    RecipeId Recipe,
    PlayerId Player,
    ItemId Anchor,
    StationSlotId? Station,
    int BeforeElapsedTicks,
    int AfterElapsedTicks,
    int RequiredTicks,
    bool Completed,
    ItemId? Product);

public sealed record CookingRecipeTickEvent(
    long Sequence,
    CookingLevelScope LevelScope,
    long HostFrameSequence,
    long BeforeLogicalTick,
    long AfterLogicalTick,
    long BeforeStateVersion,
    long AfterStateVersion,
    IReadOnlyList<CookingRecipeProcessTickResult> Processes);

public sealed record CookingRecipeTickResult(
    CookingLevelScope LevelScope,
    long HostFrameSequence,
    long BeforeLogicalTick,
    long AfterLogicalTick,
    long BeforeStateVersion,
    long AfterStateVersion,
    IReadOnlyList<CookingRecipeProcessTickResult> Processes,
    CookingRecipeTickEvent Event);

/// <summary>
/// Validates the complete, wire-facing shape of a recipe command before it reaches a simulation.
/// Domain validation still decides whether the referenced fixture state permits the command.
/// </summary>
public static class CookingRecipeCommandValidation
{
    public const int MaximumExpectedItemVersion = 1_000_000;
    public const int MaximumTickCount = 1_000;

    public static bool IsWellFormed(CookingRecipeCommand? command)
    {
        if (command is null || command.Scope is null || string.IsNullOrWhiteSpace(command.Scope.Session.Value) ||
            string.IsNullOrWhiteSpace(command.Scope.World.Value) || string.IsNullOrWhiteSpace(command.Scope.Match.Value) ||
            command.SimulationBatch <= 0 || string.IsNullOrWhiteSpace(command.Player.Value) ||
            string.IsNullOrWhiteSpace(command.Command.Value) || !Enum.IsDefined(command.Operation))
        {
            return false;
        }

        return command.Operation switch
        {
            CookingRecipeOperation.Pickup => HasIdentifier(command.Item) && IsExpectedVersion(command.ExpectedItemVersion) && command.TickCount == 0,
            CookingRecipeOperation.StartProcess => HasIdentifier(command.Item) &&
                IsExpectedVersion(command.ExpectedItemVersion) && command.TickCount == 0,
            CookingRecipeOperation.AdvanceTicks => HasIdentifier(command.Process) && command.ExpectedItemVersion == 0 &&
                command.TickCount is > 0 and <= MaximumTickCount,
            CookingRecipeOperation.Drop => HasIdentifier(command.Item) && HasIdentifier(command.Station) &&
                IsExpectedVersion(command.ExpectedItemVersion) && command.TickCount == 0,
            CookingRecipeOperation.PutIn => HasIdentifier(command.Item) && HasIdentifier(command.Container) &&
                IsExpectedVersion(command.ExpectedItemVersion) && command.TickCount == 0,
            CookingRecipeOperation.TakeOut => HasIdentifier(command.Item) && HasIdentifier(command.Container) &&
                IsExpectedVersion(command.ExpectedItemVersion) && command.TickCount == 0,
            CookingRecipeOperation.Pour => HasIdentifier(command.Item) && HasIdentifier(command.Container) &&
                IsExpectedVersion(command.ExpectedItemVersion) && command.TickCount == 0,
            CookingRecipeOperation.SubmitOrder => HasIdentifier(command.Item) && HasIdentifier(command.Order) &&
                IsExpectedVersion(command.ExpectedItemVersion) && command.TickCount == 0,
            _ => false,
        };
    }

    private static bool IsExpectedVersion(int version) => version is > 0 and <= MaximumExpectedItemVersion;
    private static bool HasIdentifier(ItemId? value) => value is { Value: { } id } && !string.IsNullOrWhiteSpace(id);
    private static bool HasIdentifier(RecipeId? value) => value is { Value: { } id } && !string.IsNullOrWhiteSpace(id);
    private static bool HasIdentifier(ProcessId? value) => value is { Value: { } id } && !string.IsNullOrWhiteSpace(id);
    private static bool HasIdentifier(StationSlotId? value) => value is { Value: { } id } && !string.IsNullOrWhiteSpace(id);
    private static bool HasIdentifier(OrderId? value) => value is { Value: { } id } && !string.IsNullOrWhiteSpace(id);
}

public sealed record CookingOrderSubmission(
    OrderId Order,
    RecipeId Recipe,
    ItemId Product,
    PlayerId Player,
    ItemId? Container = null);

public sealed record CookingOrderAcceptance(bool Accepted, string ReasonCode = "None", bool OrderCompleted = false);

/// <summary>
/// NPC 洗碗端口：领域只在提交成功时发出“这个容器脏了、交给 NPC”的请求；
/// 走到池边与占用若干 tick 清洗属前厅/NPC 范围，由测试直接注入清洗完成。
/// </summary>
public interface ICookingBowlWashingPort
{
    void RequestWash(ItemId bowl, DefinitionId definition);
}

public sealed record CookingWashCompletionResult(bool Accepted, string ReasonCode);

public interface ICookingOrderPort
{
    CookingOrderAcceptance Submit(CookingOrderSubmission submission);
}

public interface ICookingProductIdAllocator
{
    ItemId GetProductId(long productSequence);
}

internal interface ICookingRecipeLifecycleGate
{
    bool IsGameplayMutationOpen { get; }
}

internal interface ICookingRecipeAuthorityGate
{
    bool IsAuthorityMutationOpen { get; }
}

public sealed class SequentialCookingProductIdAllocator : ICookingProductIdAllocator
{
    public static SequentialCookingProductIdAllocator Instance { get; } = new();

    private SequentialCookingProductIdAllocator()
    {
    }

    public ItemId GetProductId(long productSequence)
    {
        if (productSequence <= 0)
            throw new ArgumentOutOfRangeException(nameof(productSequence));
        return new ItemId($"product-{productSequence}");
    }
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
    private readonly ICookingProductIdAllocator _productIdAllocator;
    private readonly ICookingBowlWashingPort? _bowlWashingPort;
    private readonly Dictionary<DefinitionId, int> _cleanContainerCount = new();

    private Dictionary<ItemId, ItemState> _items = new();
    private readonly Dictionary<PlayerId, ItemId?> _hands = new();
    private Dictionary<StationSlotId, ProcessState> _processesByStation = new();
    private Dictionary<ProcessId, StationSlotId> _stationsByProcess = new();
    private Dictionary<ItemId, ProcessState> _processesByAnchorItem = new();
    private Dictionary<ProcessId, ItemId> _anchorItemsByProcess = new();
    private Dictionary<ItemId, List<ItemId>> _containerItems = new();
    private readonly Dictionary<ItemId, ProcessId> _inputsByProcessItem = new();
    private readonly Dictionary<ProcessId, IReadOnlyList<ItemId>> _lockedInputsByProcess = new();
    private readonly HashSet<ItemId> _consumedProducts = new();
    private readonly HashSet<OrderId> _acceptedOrders = new();
    private readonly Dictionary<RecipeCommandKey, ProcessedCommand> _processedCommands = new();
    private readonly List<CookingRecipeEvent> _events = new();
    private List<CookingRecipeTickEvent> _tickEvents = new();
    private long _stateVersion;
    private long _eventSequence;
    private long _nextProcessId;
    private long _nextProductId;
    private bool _lifecycleClosed;
    private bool _mutationInProgress;
    private ICookingRecipeLifecycleGate? _lifecycleGate;
    private ICookingRecipeAuthorityGate? _authorityGate;

    public CookingRecipeSimulation(
        CookingRecipeFixture fixture,
        ICookingOrderPort orderPort,
        ICookingProductIdAllocator? productIdAllocator = null,
        ICookingBowlWashingPort? bowlWashingPort = null)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        _orderPort = orderPort ?? throw new ArgumentNullException(nameof(orderPort));
        _productIdAllocator = productIdAllocator ?? SequentialCookingProductIdAllocator.Instance;
        _bowlWashingPort = bowlWashingPort;
        foreach (var player in fixture.Players.Keys)
            _hands.Add(player, null);
        foreach (var (definition, supply) in fixture.CleanContainerSupply)
        {
            if (supply <= 0)
                throw new ArgumentOutOfRangeException(nameof(fixture),
                    $"Clean container supply for '{definition}' must be positive.");
            _cleanContainerCount[definition] = supply;
            for (var index = 1; index <= supply; index++)
            {
                var pooled = new ItemId($"pool-{definition.Value}-{index}");
                _items.Add(pooled, new ItemState(definition, 1, ItemLocation.World(fixture.CleanPoolLocation),
                    false, null, false, null));
                EnsureContainerList(pooled);
            }
        }
    }

    /// <summary>
    /// 注入“清洗完成”：脏碗复位为干净并回到干净碗池位置；在册干净数达到配置上限时拒绝。
    /// </summary>
    public CookingWashCompletionResult CompleteWash(ItemId bowl)
    {
        if (!_items.TryGetValue(bowl, out var state) || !state.Removed || !state.IsDirty)
            return new CookingWashCompletionResult(false, "BowlNotAwaitingWash");
        if (!_fixture.WashableContainerDefinitions.Contains(state.Definition))
            return new CookingWashCompletionResult(false, "DefinitionNotWashable");
        if (!_cleanContainerCount.TryGetValue(state.Definition, out var count) ||
            count >= _fixture.CleanContainerSupply[state.Definition])
            return new CookingWashCompletionResult(false, "CleanPoolAtCapacity");

        _cleanContainerCount[state.Definition] = count + 1;
        _items[bowl] = state with
        {
            Removed = false,
            IsDirty = false,
            Version = state.Version + 1,
            Location = ItemLocation.World(_fixture.CleanPoolLocation),
        };
        _stateVersion++;
        return new CookingWashCompletionResult(true, "Washed");
    }

    public int CleanContainerCount(DefinitionId definition) =>
        _cleanContainerCount.TryGetValue(definition, out var count) ? count : 0;

    public long LogicalTick { get; private set; }

    public IReadOnlyList<CookingRecipeEvent> EventHistory => _events;

    public IReadOnlyList<CookingRecipeTickEvent> TickEventHistory => _tickEvents.AsReadOnly();

    public ItemId? ItemInHand(PlayerId player) => _hands.TryGetValue(player, out var item) ? item : null;

    public IReadOnlyList<ItemId> ItemsInContainer(ItemId container) =>
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
                pair.Value.Recipe, pair.Value.IsProduct, pair.Value.OriginStation, pair.Value.ContainerCompleted, pair.Value.IsDirty))
            .ToArray(),
        AllProcesses()
            .OrderBy(process => process.Id.Value, StringComparer.Ordinal)
            .Select(process => new CookingRecipeSnapshotProcess(process.Id, process.Recipe, process.Player, process.Anchor,
                process.Station, process.ElapsedTicks, process.RequiredTicks))
            .ToArray(),
        _containerItems.OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
            .Select(pair => new CookingRecipeSnapshotContainer(pair.Key, ContainerCapacity(pair.Key),
                pair.Value.OrderBy(item => item.Value, StringComparer.Ordinal).ToArray()))
            .ToArray(),
        _acceptedOrders.OrderBy(order => order.Value, StringComparer.Ordinal).ToArray());

    public void AddIngredient(ItemId id, DefinitionId definition, PlayerId player, int version = 1)
    {
        AddItem(id, definition, ItemLocation.Hand(player), version);
    }

    public void AddWorldIngredient(ItemId id, DefinitionId definition, string location, int version = 1) =>
        AddItem(id, definition, ItemLocation.World(location), version);

    /// <summary>
    /// 按权威位置放入一个物品实例；容器能力的物品同时建立其内容物索引。
    /// </summary>
    public void AddItem(ItemId id, DefinitionId definition, ItemLocation location, int version = 1) =>
        AddItemCore(id, definition, location, version);

    private void AddItemCore(ItemId id, DefinitionId definition, ItemLocation location, int version)
    {
        EnsureGameplayMutationOpen();
        if (!_fixture.Items.ContainsKey(definition))
            throw new ArgumentException($"Unknown item definition '{definition}'.", nameof(definition));
        if (_items.ContainsKey(id))
            throw new ArgumentException($"Item '{id}' already exists.", nameof(id));
        if (version <= 0)
            throw new ArgumentOutOfRangeException(nameof(version));
        if (location.Kind == LocationKind.PlayerHand && location.OwnerId is { } owner)
        {
            var player = new PlayerId(owner);
            if (!_fixture.Players.ContainsKey(player))
                throw new ArgumentException($"Unknown player '{player}'.", nameof(location));
            if (_hands[player] is not null)
                throw new InvalidOperationException($"Player '{player}' already holds an item.");
        }

        _items.Add(id, new ItemState(definition, version, location, false, null, false, null));
        if (location.Kind == LocationKind.PlayerHand)
            _hands[new PlayerId(location.OwnerId!)] = id;
        if (_fixture.Items.TryGetValue(definition, out var added) && added.Container is not null)
            EnsureContainerList(id);
    }

    internal void CloseLifecycle() => _lifecycleClosed = true;

    private bool IsGameplayMutationOpen =>
        !_lifecycleClosed && (_lifecycleGate?.IsGameplayMutationOpen ?? true);

    private bool IsAuthorityMutationOpen => _authorityGate?.IsAuthorityMutationOpen ?? true;

    private void EnsureGameplayMutationOpen()
    {
        if (!IsGameplayMutationOpen || !IsAuthorityMutationOpen || _mutationInProgress)
            throw new InvalidOperationException("The recipe simulation is not open for authoritative mutation.");
    }

    internal void BindLifecycleGate(ICookingRecipeLifecycleGate lifecycleGate)
    {
        ArgumentNullException.ThrowIfNull(lifecycleGate);
        if (_lifecycleClosed)
            throw new InvalidOperationException("A closed recipe simulation cannot be bound to a level lifecycle.");
        if (_lifecycleGate is not null && !ReferenceEquals(_lifecycleGate, lifecycleGate))
            throw new InvalidOperationException("The recipe simulation is already bound to a different level lifecycle.");
        _lifecycleGate = lifecycleGate;
    }

    internal void BindAuthorityGate(ICookingRecipeAuthorityGate authorityGate)
    {
        ArgumentNullException.ThrowIfNull(authorityGate);
        if (_authorityGate is not null && !ReferenceEquals(_authorityGate, authorityGate))
            throw new InvalidOperationException("The recipe simulation is already bound to a different authority gate.");
        _authorityGate = authorityGate;
    }

    public CookingRecipeCommandResult Submit(CookingRecipeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!CookingRecipeCommandValidation.IsWellFormed(command))
            return CookingRecipeCommandResult.Reject(CookingRecipeRejectionReason.MalformedCommand, _stateVersion);
        if (!IsGameplayMutationOpen || !IsAuthorityMutationOpen || _mutationInProgress)
            return CookingRecipeCommandResult.Reject(CookingRecipeRejectionReason.LifecycleClosed, _stateVersion);

        _mutationInProgress = true;
        try
        {
            return SubmitCore(command);
        }
        finally
        {
            _mutationInProgress = false;
        }
    }

    /// <summary>
    /// 封闭批次提交：同一批次内对同一物品/工位/容器的争抢按 (LogicalTick, 玩家 ID, 命令 ID)
    /// 稳定排序裁决，不依赖抵达顺序、线程调度或字典枚举顺序。乱序传入与重复投放结果一致。
    /// </summary>
    public IReadOnlyList<CookingRecipeCommandResult> SubmitBatch(IEnumerable<CookingRecipeCommand> commands) =>
        ExecuteBatch(commands).Select(execution => execution.Result).ToArray();

    public IReadOnlyList<CookingRecipeExecution> ExecuteBatch(IEnumerable<CookingRecipeCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var closedBatch = commands.ToArray();
        if (closedBatch.Length == 0)
            return Array.Empty<CookingRecipeExecution>();
        if (closedBatch.Any(command => command is null))
            throw new ArgumentException("A closed batch cannot contain null commands.", nameof(commands));
        if (closedBatch.Any(command => command.SimulationBatch != closedBatch[0].SimulationBatch))
            throw new ArgumentException("A closed batch must contain exactly one simulation batch value.", nameof(commands));

        var executions = new List<CookingRecipeExecution>(closedBatch.Length);
        foreach (var command in closedBatch
                     .OrderBy(command => LogicalTick)
                     .ThenBy(command => command.Player.Value, StringComparer.Ordinal)
                     .ThenBy(command => command.Command.Value, StringComparer.Ordinal))
        {
            var before = Snapshot();
            var result = Submit(command);
            executions.Add(new CookingRecipeExecution(command, result, before, Snapshot()));
        }

        return executions;
    }

    private CookingRecipeCommandResult SubmitCore(CookingRecipeCommand command)
    {
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
            CookingRecipeOperation.Pickup => Pickup(command),
            CookingRecipeOperation.StartProcess => StartProcess(command),
            CookingRecipeOperation.AdvanceTicks => AdvanceTicks(command),
            CookingRecipeOperation.Drop => Drop(command),
            CookingRecipeOperation.PutIn => PutIn(command),
            CookingRecipeOperation.TakeOut => TakeOut(command),
            CookingRecipeOperation.Pour => Pour(command),
            CookingRecipeOperation.SubmitOrder => SubmitOrder(command),
            _ => throw new ArgumentOutOfRangeException(nameof(command.Operation), command.Operation, null),
        };
        _processedCommands.Add(key, new ProcessedCommand(fingerprint, result));
        return result;
    }

    public CookingRecipeTickResult AdvanceFixedTick(CookingLevelScope levelScope, long hostFrameSequence)
    {
        ArgumentNullException.ThrowIfNull(levelScope);
        EnsureGameplayMutationOpen();
        if (_mutationInProgress)
            throw new InvalidOperationException("The recipe simulation mutation cannot be reentered.");
        if (hostFrameSequence <= 0)
            throw new ArgumentOutOfRangeException(nameof(hostFrameSequence));
        if (!Equals(levelScope.MatchScope, _fixture.Scope))
            throw new ArgumentException("The level scope does not match the recipe simulation scope.", nameof(levelScope));

        _mutationInProgress = true;
        try
        {
            var plan = BuildFixedTickPlan(levelScope, hostFrameSequence);
            CommitFixedTick(plan);
            return plan.Result;
        }
        finally
        {
            _mutationInProgress = false;
        }
    }

    private IEnumerable<ProcessState> AllProcesses() =>
        _processesByStation.Values.Concat(_processesByAnchorItem.Values);

    private FixedTickPlan BuildFixedTickPlan(CookingLevelScope levelScope, long hostFrameSequence)
    {
        var beforeLogicalTick = LogicalTick;
        var afterLogicalTick = checked(beforeLogicalTick + 1);
        var beforeStateVersion = _stateVersion;
        var afterStateVersion = checked(beforeStateVersion + 1);
        var afterEventSequence = checked(_eventSequence + 1);
        var nextProductSequence = _nextProductId;
        var replacementItems = new Dictionary<ItemId, ItemState>(_items);
        var replacementProcessesByStation = new Dictionary<StationSlotId, ProcessState>(_processesByStation);
        var replacementStationsByProcess = new Dictionary<ProcessId, StationSlotId>(_stationsByProcess);
        var replacementProcessesByAnchorItem = new Dictionary<ItemId, ProcessState>(_processesByAnchorItem);
        var replacementAnchorItemsByProcess = new Dictionary<ProcessId, ItemId>(_anchorItemsByProcess);
        var replacementContainerItems = _containerItems.ToDictionary(
            pair => pair.Key, pair => new List<ItemId>(pair.Value));
        var completedProcesses = new List<ProcessId>();
        var processResults = new List<CookingRecipeProcessTickResult>();

        ValidateProcessIndexesForFixedTick();
        foreach (var process in AllProcesses().OrderBy(value => value.Id.Value, StringComparer.Ordinal))
        {
            ValidateProcessForFixedTick(process);
            var afterElapsedTicks = checked(process.ElapsedTicks + 1);
            if (afterElapsedTicks < process.RequiredTicks)
            {
                var progressed = process with { ElapsedTicks = afterElapsedTicks };
                StageProcess(progressed, replacementProcessesByStation, replacementProcessesByAnchorItem);
                processResults.Add(new CookingRecipeProcessTickResult(
                    process.Id,
                    process.Recipe,
                    process.Player,
                    process.Anchor,
                    process.Station,
                    process.ElapsedTicks,
                    afterElapsedTicks,
                    process.RequiredTicks,
                    false,
                    null));
                continue;
            }

            var recipe = _fixture.Recipes[process.Recipe];
            ItemId? productId = null;
            if (recipe.Completion == CookingRecipeCompletionKind.ConsumeInputs)
            {
                var productSequence = checked(nextProductSequence + 1);
                productId = _productIdAllocator.GetProductId(productSequence);
                if (string.IsNullOrWhiteSpace(productId.Value.Value))
                    throw new InvalidOperationException($"Product allocator returned a blank identity for sequence '{productSequence}'.");
                if (replacementItems.ContainsKey(productId.Value))
                    throw new InvalidOperationException($"Product allocator returned duplicate item identity '{productId.Value}'.");

                nextProductSequence = productSequence;
                foreach (var input in ConsumedInputs(process))
                {
                    var inputState = replacementItems[input];
                    replacementItems[input] = inputState with { Removed = true, Version = checked(inputState.Version + 1) };
                    foreach (var contents in replacementContainerItems.Values)
                        contents.Remove(input);
                }

                var productLocation = ProductLocationFor(process, replacementContainerItems);
                replacementItems.Add(productId.Value, new ItemState(
                    recipe.ProductDefinition,
                    1,
                    productLocation,
                    false,
                    recipe.Id,
                    true,
                    process.Station));
                if (productLocation is { Kind: LocationKind.ContainerSlot, OwnerId: { } productOwner })
                    StageContainerContents(replacementContainerItems, new ItemId(productOwner), productId.Value);
            }
            else
            {
                // RetainInputs：输入保留，容器锚点切“已完成”，成品在倒出时才生成。
                var anchorState = replacementItems[process.Anchor];
                replacementItems[process.Anchor] = anchorState with { ContainerCompleted = true, Recipe = recipe.Id };
            }

            if (!RemoveStagedProcess(process, replacementProcessesByStation, replacementStationsByProcess,
                    replacementProcessesByAnchorItem, replacementAnchorItemsByProcess))
            {
                throw new InvalidOperationException($"Process '{process.Id}' could not be staged for completion.");
            }
            completedProcesses.Add(process.Id);
            processResults.Add(new CookingRecipeProcessTickResult(
                process.Id,
                process.Recipe,
                process.Player,
                process.Anchor,
                process.Station,
                process.ElapsedTicks,
                afterElapsedTicks,
                process.RequiredTicks,
                true,
                productId));
        }

        var immutableResults = Array.AsReadOnly(processResults.ToArray());
        var tickEvent = new CookingRecipeTickEvent(
            afterEventSequence,
            levelScope,
            hostFrameSequence,
            beforeLogicalTick,
            afterLogicalTick,
            beforeStateVersion,
            afterStateVersion,
            immutableResults);
        var replacementTickEvents = new List<CookingRecipeTickEvent>(_tickEvents.Count + 1);
        replacementTickEvents.AddRange(_tickEvents);
        replacementTickEvents.Add(tickEvent);
        var result = new CookingRecipeTickResult(
            levelScope,
            hostFrameSequence,
            beforeLogicalTick,
            afterLogicalTick,
            beforeStateVersion,
            afterStateVersion,
            immutableResults,
            tickEvent);
        return new FixedTickPlan(
            beforeLogicalTick,
            afterLogicalTick,
            beforeStateVersion,
            afterStateVersion,
            afterEventSequence,
            nextProductSequence,
            replacementItems,
            replacementProcessesByStation,
            replacementStationsByProcess,
            replacementProcessesByAnchorItem,
            replacementAnchorItemsByProcess,
            replacementContainerItems,
            replacementTickEvents,
            immutableResults,
            tickEvent,
            result,
            completedProcesses);
    }

    /// <summary>
    /// ConsumeInputs 的产物位置：输入在工位槽则生成在同一工位；输入在容器槽则生成在同一容器的空缺槽。
    /// </summary>
    private ItemLocation ProductLocationFor(ProcessState process,
        IReadOnlyDictionary<ItemId, List<ItemId>>? stagedContainers = null)
    {
        if (process.Container is { } container)
            return ItemLocation.Container(container, NextVacantContainerSlot(container, stagedContainers));
        var inputLocation = _items[process.Anchor].Location;
        if (inputLocation is { Kind: LocationKind.ContainerSlot, OwnerId: { } owner })
            return ItemLocation.Container(new ItemId(owner), NextVacantContainerSlot(new ItemId(owner), stagedContainers));
        return ItemLocation.Station(process.Station ?? throw new InvalidOperationException(
            $"Process '{process.Id}' has no station and no container for its product location."));
    }

    private static void StageContainerContents(Dictionary<ItemId, List<ItemId>> stagedContainers, ItemId container,
        ItemId item)
    {
        if (!stagedContainers.TryGetValue(container, out var contents))
            contents = stagedContainers[container] = new List<ItemId>();
        contents.Add(item);
    }

    private static void StageProcess(ProcessState process,
        Dictionary<StationSlotId, ProcessState> byStation, Dictionary<ItemId, ProcessState> byAnchor)
    {
        if (process.Station is { } station)
            byStation[station] = process;
        else
            byAnchor[process.Anchor] = process;
    }

    private static bool RemoveStagedProcess(ProcessState process,
        Dictionary<StationSlotId, ProcessState> byStation, Dictionary<ProcessId, StationSlotId> stationsByProcess,
        Dictionary<ItemId, ProcessState> byAnchor, Dictionary<ProcessId, ItemId> anchorsByProcess)
    {
        if (process.Station is { } station)
            return byStation.Remove(station) && stationsByProcess.Remove(process.Id);
        return byAnchor.Remove(process.Anchor) && anchorsByProcess.Remove(process.Id);
    }

    private void ValidateProcessIndexesForFixedTick()
    {
        if (_stationsByProcess.Count != _processesByStation.Count)
            throw new InvalidOperationException("The process reverse index count does not match the station process count.");

        foreach (var (processId, stationId) in _stationsByProcess)
        {
            if (!_processesByStation.TryGetValue(stationId, out var process) ||
                process.Id != processId ||
                process.Station != stationId)
            {
                throw new InvalidOperationException(
                    $"Process reverse index '{processId}' -> '{stationId}' does not match an authoritative station process.");
            }
        }

        if (_anchorItemsByProcess.Count != _processesByAnchorItem.Count)
            throw new InvalidOperationException("The anchor reverse index count does not match the anchor process count.");

        foreach (var (processId, anchorId) in _anchorItemsByProcess)
        {
            if (!_processesByAnchorItem.TryGetValue(anchorId, out var process) ||
                process.Id != processId ||
                process.Anchor != anchorId ||
                process.Station is not null)
            {
                throw new InvalidOperationException(
                    $"Process reverse index '{processId}' -> '{anchorId}' does not match an authoritative anchor process.");
            }
        }

        if (_inputsByProcessItem.Count != _lockedInputsByProcess.Values.Sum(inputs => inputs.Count))
            throw new InvalidOperationException("The locked-input reverse index count does not match the process lock table.");
        foreach (var (processId, inputs) in _lockedInputsByProcess)
        {
            if (inputs.Count == 0)
                throw new InvalidOperationException($"Process '{processId}' locks no input.");
            foreach (var input in inputs)
            {
                if (!_inputsByProcessItem.TryGetValue(input, out var owner) || owner != processId)
                    throw new InvalidOperationException(
                        $"Locked input '{input}' does not map back to process '{processId}'.");
                if (!_items.TryGetValue(input, out var state) || state.Removed)
                    throw new InvalidOperationException(
                        $"Locked input '{input}' of process '{processId}' is unavailable.");
            }
        }
    }

    private void ValidateProcessForFixedTick(ProcessState process)
    {
        if (string.IsNullOrWhiteSpace(process.Id.Value))
            throw new InvalidOperationException("An active process has a blank identity.");
        if (process.Station is { } station)
        {
            if (!_stationsByProcess.TryGetValue(process.Id, out var indexedStation) || indexedStation != station)
                throw new InvalidOperationException($"Process '{process.Id}' has inconsistent station indexes.");
            if (!_processesByStation.TryGetValue(station, out var indexedProcess) || indexedProcess != process)
                throw new InvalidOperationException($"Process '{process.Id}' is not the authoritative station process.");
            if (!_fixture.Appliances.ContainsKey(station))
                throw new InvalidOperationException($"Process '{process.Id}' references unknown station '{station}'.");
        }
        else
        {
            if (!_anchorItemsByProcess.TryGetValue(process.Id, out var indexedAnchor) || indexedAnchor != process.Anchor)
                throw new InvalidOperationException($"Process '{process.Id}' has inconsistent anchor indexes.");
            if (!_processesByAnchorItem.TryGetValue(process.Anchor, out var indexedAnchorProcess) || indexedAnchorProcess != process)
                throw new InvalidOperationException($"Process '{process.Id}' is not the authoritative anchor process.");
        }

        if (!_fixture.Recipes.TryGetValue(process.Recipe, out var recipe))
            throw new InvalidOperationException($"Process '{process.Id}' references unknown recipe '{process.Recipe}'.");
        if (process.RequiredTicks != recipe.RequiredTicks || process.ElapsedTicks < 0 || process.ElapsedTicks >= process.RequiredTicks)
            throw new InvalidOperationException($"Process '{process.Id}' has invalid progress invariants.");
        if (recipe.RequiresStation != (process.Station is not null))
            throw new InvalidOperationException($"Process '{process.Id}' station binding disagrees with its recipe.");

        foreach (var input in process.LockedInputs)
        {
            if (!_inputsByProcessItem.TryGetValue(input, out var owner) || owner != process.Id)
                throw new InvalidOperationException($"Process '{process.Id}' lost ownership of locked input '{input}'.");
            if (!_items.TryGetValue(input, out var inputState) || inputState.Removed)
                throw new InvalidOperationException($"Process '{process.Id}' references unavailable input '{input}'.");
            if (!recipe.Inputs.Contains(inputState.Definition) && input != process.Anchor)
                throw new InvalidOperationException($"Process '{process.Id}' input '{input}' is inconsistent with its recipe.");
            if (process.Container is { } container)
            {
                // 容器锚定加工：内容物必须仍留在该容器的容器槽中；锚点（锅/碗）自身可在任意权威位置。
                if (input != process.Anchor &&
                    (inputState.Location is not { Kind: LocationKind.ContainerSlot, OwnerId: { } ownerId } ||
                     ownerId != container.Value))
                    throw new InvalidOperationException(
                        $"Process '{process.Id}' input '{input}' left container '{container}'.");
            }
            else if (inputState.Location.Kind is not (LocationKind.WorldPosition or LocationKind.StationSlot or LocationKind.ContainerSlot))
            {
                // 单物品加工：输入位置白名单为世界/工位/容器槽，手持不可。
                throw new InvalidOperationException(
                    $"Process '{process.Id}' input '{input}' is at unsupported location '{inputState.Location.Kind}'.");
            }
        }
    }

    private void CommitFixedTick(FixedTickPlan plan)
    {
        _items = plan.ReplacementItems;
        _processesByStation = plan.ReplacementProcessesByStation;
        _stationsByProcess = plan.ReplacementStationsByProcess;
        _processesByAnchorItem = plan.ReplacementProcessesByAnchorItem;
        _anchorItemsByProcess = plan.ReplacementAnchorItemsByProcess;
        _containerItems = plan.ReplacementContainerItems;
        _tickEvents = plan.ReplacementTickEvents;
        LogicalTick = plan.AfterLogicalTick;
        _stateVersion = plan.AfterStateVersion;
        _eventSequence = plan.AfterEventSequence;
        _nextProductId = plan.NextProductSequence;
        foreach (var process in plan.CompletedProcesses)
            ReleaseProcessLocks(process);
    }

    private CookingRecipeCommandResult Pickup(CookingRecipeCommand command)
    {
        if (!TryValidateCommandScopeAndPlayer(command, out var player, out var rejection))
            return Reject(rejection);
        if (command.Item is not { } itemId || !_items.TryGetValue(itemId, out var item) || item.Removed)
            return Reject(CookingRecipeRejectionReason.ItemNotFound);
        if (item.Version != command.ExpectedItemVersion)
            return Reject(CookingRecipeRejectionReason.ItemStale);
        if (item.Location.Kind == LocationKind.ContainerSlot)
            return Reject(CookingRecipeRejectionReason.CurrentLocationMismatch);
        if (item.Location.Kind == LocationKind.PlayerHand)
            return Reject(CookingRecipeRejectionReason.IngredientAlreadyCollected);
        if (item.Location.Kind == LocationKind.StationSlot &&
            (item.Location.SlotId is null || !player.ReachableStations.Contains(item.Location.SlotId)))
            return Reject(CookingRecipeRejectionReason.TargetOutOfRange);
        if (_hands[command.Player] is not null)
            return Reject(CookingRecipeRejectionReason.IngredientAlreadyCollected);
        if (IsLockedInput(itemId) && !IsActiveProcessAnchor(itemId))
            return Reject(CookingRecipeRejectionReason.ItemStale);
        if (!_fixture.Items[item.Definition].AllowedPlayerCapabilities.Overlaps(player.Capabilities))
            return Reject(CookingRecipeRejectionReason.PlayerIneligible);

        _items[itemId] = item with { Location = ItemLocation.Hand(command.Player), Version = item.Version + 1 };
        _hands[command.Player] = itemId;
        return Commit(command, null, null, itemId, "ingredient-picked-up");
    }

    /// <summary>
    /// 启动加工：锚点是命令引用的物品。锚点带容器能力时按容器内容集合匹配配方（多输入煮制、手持碗打蛋）；
    /// 否则按单物品匹配（砧板切番茄）。工位绑定由加工定义的 RequiresStation 声明，免工位加工不得携带工位。
    /// </summary>
    private CookingRecipeCommandResult StartProcess(CookingRecipeCommand command)
    {
        if (!TryValidateCommandScopeAndPlayer(command, out var player, out var rejection))
            return Reject(rejection);
        if (command.Item is not { } anchorId || !_items.TryGetValue(anchorId, out var anchorItem) || anchorItem.Removed)
            return Reject(CookingRecipeRejectionReason.ItemNotFound);
        if (anchorItem.Version != command.ExpectedItemVersion)
            return Reject(CookingRecipeRejectionReason.ItemStale);
        if (IsLockedInput(anchorId))
            return Reject(CookingRecipeRejectionReason.ItemStale);

        var containerCapability = _fixture.Items.TryGetValue(anchorItem.Definition, out var anchorDefinition) &&
            anchorDefinition.Container is { } declared ? declared : null;
        IReadOnlyList<DefinitionId> presentInputs;
        ItemId? containerId = null;
        if (containerCapability is not null)
        {
            containerId = anchorId;
            presentInputs = ItemsInContainer(anchorId)
                .Select(item => _items[item].Definition)
                .Distinct()
                .ToArray();
        }
        else
        {
            if (anchorItem.Location.Kind == LocationKind.PlayerHand)
            {
                if (anchorItem.Location.OwnerId != command.Player.Value)
                    return Reject(CookingRecipeRejectionReason.CurrentLocationMismatch);
            }
            else if (anchorItem.Location.Kind != LocationKind.StationSlot)
            {
                return Reject(CookingRecipeRejectionReason.CurrentLocationMismatch);
            }
            presentInputs = new[] { anchorItem.Definition };
        }

        CookingApplianceDefinition? appliance = null;
        StationSlotId? stationId = null;
        if (command.Station is { } requestedStation)
        {
            if (!_fixture.Appliances.TryGetValue(requestedStation, out var declaredAppliance))
                return Reject(CookingRecipeRejectionReason.ApplianceNotFound);
            appliance = declaredAppliance;
            stationId = requestedStation;
        }

        if (!TryResolveStartRecipe(command, presentInputs, appliance, out var recipe, out rejection))
            return Reject(rejection);

        if (recipe.RequiresStation)
        {
            if (stationId is not { } requiredStation)
                return Reject(CookingRecipeRejectionReason.ApplianceNotFound);
            if (!appliance!.IsAvailable)
                return Reject(CookingRecipeRejectionReason.ApplianceUnavailable);
            if (!appliance.Capabilities.Contains(recipe.RequiredApplianceCapability))
                return Reject(CookingRecipeRejectionReason.ApplianceCapabilityMismatch);
            if (!player.ReachableStations.Contains(requiredStation.Value))
                return Reject(CookingRecipeRejectionReason.TargetOutOfRange);
            if (_processesByStation.ContainsKey(requiredStation))
                return Reject(CookingRecipeRejectionReason.ApplianceUnavailable);
        }
        else if (stationId is not null)
        {
            return Reject(CookingRecipeRejectionReason.ApplianceCapabilityMismatch);
        }

        if (containerId is not null)
        {
            if (_processesByAnchorItem.ContainsKey(containerId.Value))
                return Reject(CookingRecipeRejectionReason.ApplianceUnavailable);
            if (!ContainerIsReachable(containerId.Value, player))
                return Reject(CookingRecipeRejectionReason.TargetOutOfRange);
        }
        else if (anchorItem.Location.Kind == LocationKind.StationSlot &&
                 anchorItem.Location.SlotId != stationId?.Value)
        {
            return Reject(CookingRecipeRejectionReason.CurrentLocationMismatch);
        }

        var lockedInputs = new List<ItemId> { anchorId };
        lockedInputs.AddRange(ItemsInContainer(anchorId));
        foreach (var input in lockedInputs)
        {
            if (!_fixture.Items[_items[input].Definition].AllowedPlayerCapabilities.Overlaps(player.Capabilities))
                return Reject(CookingRecipeRejectionReason.PlayerIneligible);
        }

        if (containerId is null && anchorItem.Location.Kind == LocationKind.PlayerHand && stationId is { } moveStation)
        {
            _hands[command.Player] = null;
            _items[anchorId] = anchorItem with { Location = ItemLocation.Station(moveStation), Version = anchorItem.Version + 1 };
        }

        var processId = new ProcessId($"process-{++_nextProcessId}");
        var process = new ProcessState(processId, recipe.Id, command.Player, anchorId, stationId, 0, recipe.RequiredTicks,
            recipe.Completion, containerId, lockedInputs);
        if (stationId is { } stationProcessKey)
        {
            _processesByStation.Add(stationProcessKey, process);
            _stationsByProcess.Add(processId, stationProcessKey);
        }
        else
        {
            _processesByAnchorItem.Add(anchorId, process);
            _anchorItemsByProcess.Add(processId, anchorId);
        }

        LockProcessInputs(processId, lockedInputs);
        return Commit(command, recipe.Id, processId, anchorId, "process-started");
    }

    /// <summary>
    /// 显式 Recipe 直接采用并校验输入集合；缺省时按内容集合自动匹配，命中多个返回 RecipeAmbiguous。
    /// </summary>
    private bool TryResolveStartRecipe(CookingRecipeCommand command, IReadOnlyList<DefinitionId> presentInputs,
        CookingApplianceDefinition? appliance, out CookingRecipeDefinition recipe,
        out CookingRecipeRejectionReason rejection)
    {
        if (command.Recipe is { } explicitRecipeId)
        {
            if (!_fixture.Recipes.TryGetValue(explicitRecipeId, out var explicitRecipe))
            {
                recipe = null!;
                rejection = CookingRecipeRejectionReason.RecipeNotFound;
                return false;
            }
            var declared = new HashSet<DefinitionId>(explicitRecipe.Inputs);
            if (!declared.SetEquals(presentInputs))
            {
                recipe = null!;
                rejection = CookingRecipeRejectionReason.RecipeNotMatched;
                return false;
            }
            recipe = explicitRecipe;
            rejection = CookingRecipeRejectionReason.None;
            return true;
        }

        var stationBound = _fixture.Recipes.Values
            .Where(candidate => candidate.RequiresStation == (appliance is not null))
            .ToArray();
        var match = CookingRecipeMatcher.Match(presentInputs, appliance?.Capabilities, stationBound);
        switch (match.Outcome)
        {
            case CookingRecipeMatchOutcome.Matched:
                recipe = _fixture.Recipes[match.Recipe!.Value];
                rejection = CookingRecipeRejectionReason.None;
                return true;
            case CookingRecipeMatchOutcome.Ambiguous:
                recipe = null!;
                rejection = CookingRecipeRejectionReason.RecipeAmbiguous;
                return false;
        }

        // 未命中时分两级诊断：同侧候选忽略能力后能唯一命中，说明配方存在但该工位缺能力；
        // 对侧候选（工位绑定分歧）能唯一命中，说明配方存在但绑定与命令分歧。
        var sameBinding = CookingRecipeMatcher.Match(presentInputs, null, stationBound);
        if (sameBinding.Outcome == CookingRecipeMatchOutcome.Ambiguous)
        {
            recipe = null!;
            rejection = CookingRecipeRejectionReason.RecipeAmbiguous;
            return false;
        }
        if (sameBinding.Outcome == CookingRecipeMatchOutcome.Matched)
        {
            recipe = null!;
            rejection = appliance is null
                ? CookingRecipeRejectionReason.ApplianceNotFound
                : CookingRecipeRejectionReason.ApplianceCapabilityMismatch;
            return false;
        }

        var oppositeBound = _fixture.Recipes.Values
            .Where(candidate => candidate.RequiresStation != (appliance is not null))
            .ToArray();
        var opposite = CookingRecipeMatcher.Match(presentInputs, null, oppositeBound);
        if (opposite.Outcome == CookingRecipeMatchOutcome.Matched)
        {
            recipe = null!;
            rejection = appliance is null
                ? CookingRecipeRejectionReason.ApplianceNotFound
                : CookingRecipeRejectionReason.ApplianceCapabilityMismatch;
            return false;
        }

        recipe = null!;
        rejection = CookingRecipeRejectionReason.RecipeNotMatched;
        return false;
    }

    private CookingRecipeCommandResult AdvanceTicks(CookingRecipeCommand command)
    {
        if (!TryValidateCommandScopeAndPlayer(command, out _, out var rejection))
            return Reject(rejection);
        if (command.Process is not { } processId || !TryGetProcess(processId, out var process))
            return Reject(CookingRecipeRejectionReason.ProcessNotFound);
        if (command.TickCount <= 0)
            return Reject(CookingRecipeRejectionReason.ProcessNotComplete);
        if (process.Player != command.Player)
            return Reject(CookingRecipeRejectionReason.PlayerIneligible);

        var progressed = process with { ElapsedTicks = Math.Min(process.RequiredTicks, process.ElapsedTicks + command.TickCount) };
        if (progressed.ElapsedTicks < progressed.RequiredTicks)
        {
            StageProcess(progressed, _processesByStation, _processesByAnchorItem);
            LogicalTick += command.TickCount;
            return Commit(command, progressed.Recipe, progressed.Id, progressed.Anchor, "process-progressed");
        }

        var recipe = _fixture.Recipes[process.Recipe];
        ItemId? productId = null;
        if (recipe.Completion == CookingRecipeCompletionKind.ConsumeInputs)
        {
            var allocated = _productIdAllocator.GetProductId(checked(++_nextProductId));
            productId = allocated;
            foreach (var input in ConsumedInputs(process))
            {
                var inputState = _items[input];
                _items[input] = inputState with { Removed = true, Version = inputState.Version + 1 };
                foreach (var contents in _containerItems.Values)
                    contents.Remove(input);
            }

            var productLocation = ProductLocationFor(process);
            _items.Add(allocated, new ItemState(recipe.ProductDefinition, 1, productLocation, false,
                recipe.Id, true, process.Station));
            if (productLocation is { Kind: LocationKind.ContainerSlot, OwnerId: { } productOwner })
                EnsureContainerList(new ItemId(productOwner)).Add(allocated);
        }
        else
        {
            var anchorState = _items[process.Anchor];
            _items[process.Anchor] = anchorState with { ContainerCompleted = true, Recipe = recipe.Id };
        }

        RemoveProcess(process);
        ReleaseProcessLocks(progressed.Id);
        LogicalTick += command.TickCount;
        return Commit(command, recipe.Id, progressed.Id, productId ?? progressed.Anchor,
            recipe.Completion == CookingRecipeCompletionKind.ConsumeInputs ? "process-completed" : "process-retained");
    }

    private bool TryGetProcess(ProcessId processId, out ProcessState process)
    {
        if (_stationsByProcess.TryGetValue(processId, out var stationId) &&
            _processesByStation.TryGetValue(stationId, out var stationProcess))
        {
            process = stationProcess;
            return true;
        }
        if (_anchorItemsByProcess.TryGetValue(processId, out var anchorId) &&
            _processesByAnchorItem.TryGetValue(anchorId, out var anchorProcess))
        {
            process = anchorProcess;
            return true;
        }

        process = null!;
        return false;
    }

    private void RemoveProcess(ProcessState process)
    {
        if (process.Station is { } station)
        {
            _processesByStation.Remove(station);
            _stationsByProcess.Remove(process.Id);
        }
        else
        {
            _processesByAnchorItem.Remove(process.Anchor);
            _anchorItemsByProcess.Remove(process.Id);
        }
    }

    private CookingRecipeCommandResult Drop(CookingRecipeCommand command)
    {
        if (!TryValidateCommandScopeAndPlayer(command, out var player, out var rejection))
            return Reject(rejection);
        if (command.Item is not { } itemId || !_items.TryGetValue(itemId, out var item) || item.Removed)
            return Reject(CookingRecipeRejectionReason.ItemNotFound);
        if (item.Version != command.ExpectedItemVersion)
            return Reject(CookingRecipeRejectionReason.ItemStale);
        if (IsLockedInput(itemId))
            return Reject(CookingRecipeRejectionReason.ItemStale);
        if (item.Location != ItemLocation.Hand(command.Player))
            return Reject(CookingRecipeRejectionReason.CurrentLocationMismatch);
        if (command.Station is not { } stationId || !_fixture.Appliances.TryGetValue(stationId, out var appliance) ||
            !appliance.IsAvailable)
            return Reject(CookingRecipeRejectionReason.ApplianceUnavailable);
        if (!player.ReachableStations.Contains(stationId.Value))
            return Reject(CookingRecipeRejectionReason.TargetOutOfRange);

        return CommitMoveToStation(command, itemId, item, stationId);
    }

    private CookingRecipeCommandResult PutIn(CookingRecipeCommand command)
    {
        if (!TryValidateCommandScopeAndPlayer(command, out var player, out var rejection))
            return Reject(rejection);
        if (command.Item is not { } itemId || !_items.TryGetValue(itemId, out var item) || item.Removed)
            return Reject(CookingRecipeRejectionReason.ItemNotFound);
        if (item.Version != command.ExpectedItemVersion)
            return Reject(CookingRecipeRejectionReason.ItemStale);
        if (IsLockedInput(itemId))
            return Reject(CookingRecipeRejectionReason.ItemStale);
        if (item.Location != ItemLocation.Hand(command.Player))
            return Reject(CookingRecipeRejectionReason.CurrentLocationMismatch);
        if (command.Container is not { } containerId || !TryGetContainerCapability(containerId, out var container))
            return Reject(CookingRecipeRejectionReason.ContainerNotFound);
        if (WouldCreateContainmentCycle(containerId, itemId))
            return Reject(CookingRecipeRejectionReason.ContainerRejectsItem);
        if (!ContainerIsReachable(containerId, player))
            return Reject(CookingRecipeRejectionReason.TargetOutOfRange);
        if (!container.AcceptedDefinitions.Contains(item.Definition))
            return Reject(CookingRecipeRejectionReason.ContainerRejectsItem);
        if (ItemsInContainer(containerId).Count >= container.Capacity)
            return Reject(CookingRecipeRejectionReason.ContainerFull);

        var slot = NextVacantContainerSlot(containerId);
        _hands[command.Player] = null;
        EnsureContainerList(containerId).Add(itemId);
        _items[itemId] = item with { Location = ItemLocation.Container(containerId, slot), Version = item.Version + 1 };
        return Commit(command, null, null, itemId, "item-put-in");
    }

    private CookingRecipeCommandResult TakeOut(CookingRecipeCommand command)
    {
        if (!TryValidateCommandScopeAndPlayer(command, out var player, out var rejection))
            return Reject(rejection);
        if (command.Item is not { } itemId || !_items.TryGetValue(itemId, out var item) || item.Removed)
            return Reject(CookingRecipeRejectionReason.ItemNotFound);
        if (item.Version != command.ExpectedItemVersion)
            return Reject(CookingRecipeRejectionReason.ItemStale);
        if (command.Container is not { } containerId || !TryGetContainerCapability(containerId, out _))
            return Reject(CookingRecipeRejectionReason.ContainerNotFound);
        if (item.Location is not { Kind: LocationKind.ContainerSlot, OwnerId: { } owner } || owner != containerId.Value)
            return Reject(CookingRecipeRejectionReason.CurrentLocationMismatch);
        if (IsLockedInput(itemId))
            return Reject(CookingRecipeRejectionReason.ItemStale);
        if (!ContainerIsReachable(containerId, player))
            return Reject(CookingRecipeRejectionReason.TargetOutOfRange);
        if (_hands[command.Player] is not null)
            return Reject(CookingRecipeRejectionReason.IngredientAlreadyCollected);

        var contents = EnsureContainerList(containerId);
        contents.Remove(itemId);
        _hands[command.Player] = itemId;
        _items[itemId] = item with { Location = ItemLocation.Hand(command.Player), Version = item.Version + 1 };
        return Commit(command, null, null, itemId, "item-taken-out");
    }

    private CookingRecipeCommandResult Pour(CookingRecipeCommand command)
    {
        if (!TryValidateCommandScopeAndPlayer(command, out var player, out var rejection))
            return Reject(rejection);
        if (command.Item is not { } sourceId || !TryGetContainerCapability(sourceId, out var sourceCapability))
            return Reject(CookingRecipeRejectionReason.ContainerNotFound);
        var source = _items[sourceId];
        if (source.Version != command.ExpectedItemVersion)
            return Reject(CookingRecipeRejectionReason.ItemStale);
        if (command.Container is not { } targetId || !TryGetContainerCapability(targetId, out var targetCapability))
            return Reject(CookingRecipeRejectionReason.ContainerNotFound);
        if (targetId == sourceId)
            return Reject(CookingRecipeRejectionReason.CurrentLocationMismatch);
        if (!ContainerIsReachable(sourceId, player) || !ContainerIsReachable(targetId, player))
            return Reject(CookingRecipeRejectionReason.TargetOutOfRange);

        var contents = ItemsInContainer(sourceId);
        if (contents.Count == 0)
            return Reject(CookingRecipeRejectionReason.ProductNotFound);
        if (contents.Any(item => IsLockedInput(item)))
            return Reject(CookingRecipeRejectionReason.ItemStale);

        if (source.ContainerCompleted)
            return PourCompletedContainer(command, sourceId, source, targetId, targetCapability, contents);

        var vacant = targetCapability.Capacity - ItemsInContainer(targetId).Count;
        if (contents.Count > vacant)
            return Reject(CookingRecipeRejectionReason.ContainerFull);
        foreach (var contentId in contents)
        {
            var content = _items[contentId];
            if (!targetCapability.AcceptedDefinitions.Contains(content.Definition))
                return Reject(CookingRecipeRejectionReason.ContainerRejectsItem);
        }

        var targetContents = EnsureContainerList(targetId);
        foreach (var contentId in contents)
        {
            var content = _items[contentId];
            var slot = NextVacantContainerSlot(targetId);
            targetContents.Add(contentId);
            EnsureContainerList(sourceId).Remove(contentId);
            _items[contentId] = content with { Location = ItemLocation.Container(targetId, slot), Version = content.Version + 1 };
        }
        _items[sourceId] = source with { Version = source.Version + 1 };
        return Commit(command, null, null, sourceId, "container-poured");
    }

    /// <summary>
    /// 从“已完成”的 RetainInputs 容器倒出：经统一 allocator 生成成品进入目标容器空缺槽，
    /// 保留的输入被消耗，容器复位为未完成（可复用）；重复倒出因容器已空且未完成而被拒绝，
    /// 因此“倒出至多生成一次产物”。
    /// </summary>
    private CookingRecipeCommandResult PourCompletedContainer(CookingRecipeCommand command, ItemId sourceId, ItemState source,
        ItemId targetId, CookingItemContainerCapability targetCapability, IReadOnlyList<ItemId> contents)
    {
        if (source.Recipe is not { } recipeId || !_fixture.Recipes.TryGetValue(recipeId, out var recipe))
            return Reject(CookingRecipeRejectionReason.ProductNotFound);
        if (ItemsInContainer(targetId).Count >= targetCapability.Capacity)
            return Reject(CookingRecipeRejectionReason.ContainerFull);
        if (!targetCapability.AcceptedDefinitions.Contains(recipe.ProductDefinition))
            return Reject(CookingRecipeRejectionReason.ContainerRejectsItem);

        var productId = _productIdAllocator.GetProductId(checked(++_nextProductId));
        if (string.IsNullOrWhiteSpace(productId.Value))
            throw new InvalidOperationException(
                $"Product allocator returned a blank identity for sequence '{_nextProductId}'.");
        if (_items.ContainsKey(productId))
            throw new InvalidOperationException($"Product allocator returned duplicate item identity '{productId}'.");

        var slot = NextVacantContainerSlot(targetId);
        foreach (var contentId in contents)
        {
            var content = _items[contentId];
            _items[contentId] = content with { Removed = true, Version = content.Version + 1 };
        }

        EnsureContainerList(targetId).Add(productId);
        EnsureContainerList(sourceId).Clear();
        _items.Add(productId, new ItemState(recipe.ProductDefinition, 1, ItemLocation.Container(targetId, slot), false,
            recipe.Id, true, null));
        _items[sourceId] = source with { Version = source.Version + 1, ContainerCompleted = false, Recipe = null };
        return Commit(command, recipe.Id, null, productId, "product-poured");
    }

    private bool ContainerIsReachable(ItemId container, CookingPlayerConfig player)
    {
        var location = _items[container].Location;
        return location.Kind switch
        {
            LocationKind.PlayerHand => location.OwnerId == player.Id.Value,
            LocationKind.StationSlot => location.SlotId is { } station && player.ReachableStations.Contains(station),
            _ => false,
        };
    }

    /// <summary>
    /// 沿容器槽所有者链上走，若目标容器是待放入物品的后裔则形成包含环。
    /// </summary>
    private bool WouldCreateContainmentCycle(ItemId container, ItemId item)
    {
        var current = container;
        var visited = new HashSet<ItemId>();
        while (true)
        {
            if (current == item)
                return true;
            if (!visited.Add(current) || !_items.TryGetValue(current, out var state) || state.Removed)
                return false;
            if (state.Location is not { Kind: LocationKind.ContainerSlot, OwnerId: { } owner })
                return false;
            current = new ItemId(owner);
        }
    }

    private bool IsLockedInput(ItemId item) => _inputsByProcessItem.ContainsKey(item);

    /// <summary>
    /// ConsumeInputs 实际消耗的输入：单物品加工消耗锚点自身；容器锚定加工只消耗内容物，容器保留。
    /// </summary>
    private static IEnumerable<ItemId> ConsumedInputs(ProcessState process) =>
        process.Container is null
            ? process.LockedInputs
            : process.LockedInputs.Where(input => input != process.Anchor);

    /// <summary>
    /// 活动进程的容器锚点（锅）允许被拾取携带——端走继续；其余锁定输入不可移动。
    /// </summary>
    private bool IsActiveProcessAnchor(ItemId item) =>
        _processesByAnchorItem.ContainsKey(item) ||
        _processesByStation.Values.Any(process => process.Container == item);

    private void LockProcessInputs(ProcessId process, IReadOnlyList<ItemId> inputs)
    {
        _lockedInputsByProcess[process] = inputs;
        foreach (var input in inputs)
            _inputsByProcessItem[input] = process;
    }

    private void ReleaseProcessLocks(ProcessId process)
    {
        if (!_lockedInputsByProcess.Remove(process, out var inputs))
            return;
        foreach (var input in inputs)
            _inputsByProcessItem.Remove(input);
    }

    private CookingRecipeCommandResult CommitMoveToStation(CookingRecipeCommand command, ItemId itemId, ItemState item,
        StationSlotId stationId)
    {
        _hands[command.Player] = null;
        _items[itemId] = item with { Location = ItemLocation.Station(stationId), Version = item.Version + 1 };
        return Commit(command, null, null, itemId, "item-dropped");
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
        if (product.Location.OwnerId is null ||
            !ContainerIsReachable(new ItemId(product.Location.OwnerId), player))
            return Reject(CookingRecipeRejectionReason.TargetOutOfRange);
        if (product.Recipe is not { } recipe)
            return Reject(CookingRecipeRejectionReason.ProductNotFound);

        var container = new ItemId(product.Location.OwnerId!);
        var acceptance = _orderPort.Submit(new CookingOrderSubmission(orderId, recipe, productId, command.Player, container));
        if (!acceptance.Accepted)
            return Reject(CookingRecipeRejectionReason.OrderRejected);

        if (_containerItems.TryGetValue(container, out var contents))
            contents.Remove(productId);
        _items[productId] = product with { Removed = true, Version = product.Version + 1 };
        _consumedProducts.Add(productId);
        _acceptedOrders.Add(orderId);

        // 提交成功后容器变脏并交给 NPC 清洗：脏碗离开厨房，在册干净数下降。
        var containerState = _items[container];
        if (_fixture.WashableContainerDefinitions.Contains(containerState.Definition))
        {
            _items[container] = containerState with { IsDirty = true, Removed = true, Version = containerState.Version + 1 };
            foreach (var (playerId, held) in _hands.Where(pair => pair.Value == container).ToArray())
                _hands[playerId] = null;
            if (_cleanContainerCount.TryGetValue(containerState.Definition, out var cleanCount))
                _cleanContainerCount[containerState.Definition] = cleanCount - 1;
            _bowlWashingPort?.RequestWash(container, containerState.Definition);
        }

        return Commit(command, recipe, null, productId, "order-submitted");
    }

    private string NextVacantContainerSlot(ItemId container,
        IReadOnlyDictionary<ItemId, List<ItemId>>? stagedContainers = null)
    {
        var occupied = new HashSet<string>(StringComparer.Ordinal);
        List<ItemId>? contents = null;
        if (stagedContainers is not null)
            stagedContainers.TryGetValue(container, out contents);
        else
            _containerItems.TryGetValue(container, out contents);
        if (contents is not null)
        {
            foreach (var item in contents)
            {
                if (_items[item].Location.SlotId is { } slot)
                    occupied.Add(slot);
            }
        }
        for (var index = 0; ; index++)
        {
            var slot = $"slot-{index}";
            if (!occupied.Contains(slot))
                return slot;
        }
    }

    /// <summary>
    /// 解析容器物品的容器能力；容器必须是活动物品且定义声明了容器能力。
    /// </summary>
    private bool TryGetContainerCapability(ItemId container, out CookingItemContainerCapability capability)
    {
        if (_items.TryGetValue(container, out var item) && !item.Removed &&
            _fixture.Items.TryGetValue(item.Definition, out var definition) &&
            definition.Container is { } declared)
        {
            capability = declared;
            return true;
        }

        capability = null!;
        return false;
    }

    private int ContainerCapacity(ItemId container) =>
        TryGetContainerCapability(container, out var capability) ? capability.Capacity : 0;

    private List<ItemId> EnsureContainerList(ItemId container) =>
        _containerItems.TryGetValue(container, out var contents)
            ? contents
            : _containerItems[container] = new List<ItemId>();

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

    internal void SetFixedTickCountersForTesting(
        long logicalTick,
        long stateVersion,
        long eventSequence,
        long productSequence)
    {
        LogicalTick = logicalTick;
        _stateVersion = stateVersion;
        _eventSequence = eventSequence;
        _nextProductId = productSequence;
    }

    internal void AddFixedTickReverseIndexEntryForTesting(ProcessId process, StationSlotId station) =>
        _stationsByProcess.Add(process, station);

    /// <summary>
    /// 测试专用：把锁定输入搬到任意权威外位置，用于证明 fixed-tick 白名单腐败检测器可达。
    /// 正常命令路径永远走不到这个状态。
    /// </summary>
    internal void RelocateLockedInputForTesting(ItemId item, ItemLocation location)
    {
        var state = _items[item];
        _items[item] = state with { Location = location, Version = state.Version + 1 };
    }

    internal void RemoveLockedInputForTesting(ItemId item)
    {
        var state = _items[item];
        _items[item] = state with { Removed = true, Version = state.Version + 1 };
    }

    internal void SetFixedTickReverseIndexEntryForTesting(ProcessId process, StationSlotId station) =>
        _stationsByProcess[process] = station;

    internal bool ContainsFixedTickReverseIndexForTesting(ProcessId process) =>
        _stationsByProcess.ContainsKey(process);

    private sealed record ItemState(
        DefinitionId Definition,
        int Version,
        ItemLocation Location,
        bool Removed,
        RecipeId? Recipe,
        bool IsProduct,
        StationSlotId? OriginStation,
        bool ContainerCompleted = false,
        bool IsDirty = false);

    private sealed record ProcessState(
        ProcessId Id,
        RecipeId Recipe,
        PlayerId Player,
        ItemId Anchor,
        StationSlotId? Station,
        int ElapsedTicks,
        int RequiredTicks,
        CookingRecipeCompletionKind Completion,
        ItemId? Container,
        IReadOnlyList<ItemId> LockedInputs);

    private sealed record FixedTickPlan(
        long BeforeLogicalTick,
        long AfterLogicalTick,
        long BeforeStateVersion,
        long AfterStateVersion,
        long AfterEventSequence,
        long NextProductSequence,
        Dictionary<ItemId, ItemState> ReplacementItems,
        Dictionary<StationSlotId, ProcessState> ReplacementProcessesByStation,
        Dictionary<ProcessId, StationSlotId> ReplacementStationsByProcess,
        Dictionary<ItemId, ProcessState> ReplacementProcessesByAnchorItem,
        Dictionary<ProcessId, ItemId> ReplacementAnchorItemsByProcess,
        Dictionary<ItemId, List<ItemId>> ReplacementContainerItems,
        List<CookingRecipeTickEvent> ReplacementTickEvents,
        IReadOnlyList<CookingRecipeProcessTickResult> ProcessResults,
        CookingRecipeTickEvent TickEvent,
        CookingRecipeTickResult Result,
        IReadOnlyList<ProcessId> CompletedProcesses);

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
                item.Location.OwnerId, item.Location.SlotId, item.Recipe?.Value, item.IsProduct, item.OriginStation?.Value,
                item.ContainerCompleted, item.IsDirty)).ToArray(),
        Processes.OrderBy(process => process.Id.Value, StringComparer.Ordinal)
            .Select(process => new CanonicalProcess(process.Id.Value, process.Recipe.Value, process.Player.Value,
                process.Anchor.Value, process.Station?.Value, process.ElapsedTicks, process.RequiredTicks)).ToArray(),
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
        string? SlotId, string? RecipeId, bool IsProduct, string? OriginStation, bool ContainerCompleted, bool IsDirty);
    private sealed record CanonicalProcess(string ProcessId, string RecipeId, string PlayerId, string AnchorItemId,
        string? StationId, int ElapsedTicks, int RequiredTicks);
    private sealed record CanonicalContainer(string ContainerId, int Capacity, IReadOnlyList<string> ItemIds);
}

public sealed record CookingRecipeSnapshotItem(ItemId Id, DefinitionId Definition, int Version, ItemLocation Location,
    RecipeId? Recipe, bool IsProduct, StationSlotId? OriginStation, bool ContainerCompleted = false, bool IsDirty = false);

public sealed record CookingRecipeSnapshotProcess(ProcessId Id, RecipeId Recipe, PlayerId Player, ItemId Anchor,
    StationSlotId? Station, int ElapsedTicks, int RequiredTicks);

public sealed record CookingRecipeSnapshotContainer(ItemId Id, int Capacity, IReadOnlyList<ItemId> ItemIds);

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
