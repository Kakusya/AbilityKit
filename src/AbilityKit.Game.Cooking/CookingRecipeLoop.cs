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

public readonly record struct OrderTemplateId(string Value)
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
    bool RequiresStation = true,
    CookingRecipeExecutionKind Execution = CookingRecipeExecutionKind.Automatic,
    int YieldPortions = 1,
    DefinitionId? RequiredProcessingContainerDefinition = null);

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
        string? cleanPoolLocation = null,
        IReadOnlyDictionary<OrderTemplateId, CookingOrderTemplateDefinition>? orderTemplates = null,
        CookingScoreThresholds? scoreThresholds = null,
        CookingSpatialConfiguration? spatial = null)
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
            if (!Enum.IsDefined(recipe.Execution) || recipe.YieldPortions <= 0 ||
                (recipe.YieldPortions > 1 && recipe.Completion != CookingRecipeCompletionKind.RetainInputs))
                throw new ArgumentException("Invalid execution kind or portion yield.", nameof(recipes));
            if (recipe.RequiredTicks <= 0)
                throw new ArgumentOutOfRangeException(nameof(recipes), $"Recipe '{recipe.Id}' must require a positive tick count.");
            if (string.IsNullOrWhiteSpace(recipe.RequiredApplianceCapability))
                throw new ArgumentException($"Recipe '{recipe.Id}' has a blank appliance capability.", nameof(recipes));
        }

        foreach (var disposable in items.Values.Where(i => i.Container?.DisposableOnSubmission == true))
            if ((washableContainerDefinitions?.Contains(disposable.Id) ?? false) || (cleanContainerSupply?.ContainsKey(disposable.Id) ?? false))
                throw new ArgumentException("Disposable vessels cannot be supplied by the clean pool or washed.", nameof(items));
        Spatial = spatial?.Freeze();
        Spatial?.Validate(players, appliances);
        Scope = scope;
        Players = players;
        Items = items;
        Appliances = appliances;
        Recipes = recipes;
        WashableContainerDefinitions = washableContainerDefinitions ?? new HashSet<DefinitionId>();
        CleanContainerSupply = cleanContainerSupply ?? new Dictionary<DefinitionId, int>();
        CleanPoolLocation = cleanPoolLocation ?? "clean-pool";
        OrderTemplates = orderTemplates ?? new Dictionary<OrderTemplateId, CookingOrderTemplateDefinition>();
        ScoreThresholds = scoreThresholds ?? CookingScoreThresholds.Default;
    }

    public CookingSpatialConfiguration? Spatial { get; init; }
    public CookingScope Scope { get; init; }
    public IReadOnlyDictionary<PlayerId, CookingPlayerConfig> Players { get; init; }
    public IReadOnlyDictionary<DefinitionId, CookingItemDefinition> Items { get; init; }
    public IReadOnlyDictionary<StationSlotId, CookingApplianceDefinition> Appliances { get; init; }
    public IReadOnlyDictionary<RecipeId, CookingRecipeDefinition> Recipes { get; init; }
    public IReadOnlySet<DefinitionId> WashableContainerDefinitions { get; init; }
    public IReadOnlyDictionary<DefinitionId, int> CleanContainerSupply { get; init; }
    public string CleanPoolLocation { get; init; }
    public IReadOnlyDictionary<OrderTemplateId, CookingOrderTemplateDefinition> OrderTemplates { get; init; }
    public CookingScoreThresholds ScoreThresholds { get; init; }
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
    Move,
    ContinueProcess,
    StopProcess,
    ServePortion,
    ClearContents,
    DiscardItem,
    BindOrder,
    UnbindOrder,
    RebindOrder,
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
    OrderNotFound,
    OrderAlreadyCompleted,
    OrderRequirementMismatch,
    MovementBlocked,
    WorkerUnavailable,
    BatchCompleted,
    BindingRequired,
    BindingConflict,
    BindingNotFound,
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
    int TickCount = 0,
    int MoveX = 0,
    int MoveY = 0,
    int FacingX = 0,
    int FacingY = 0,
    string? WorldAnchor = null);

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

        if (command.WorldAnchor is not null && (command.Operation != CookingRecipeOperation.Drop || string.IsNullOrWhiteSpace(command.WorldAnchor) || command.Station is not null)) return false;
        if (command.Operation != CookingRecipeOperation.Move &&
            (command.MoveX != 0 || command.MoveY != 0 || command.FacingX != 0 || command.FacingY != 0))
            return false;
        return command.Operation switch
        {
            CookingRecipeOperation.Pickup => HasIdentifier(command.Item) && IsExpectedVersion(command.ExpectedItemVersion) && command.TickCount == 0,
            CookingRecipeOperation.StartProcess => HasIdentifier(command.Item) &&
                IsExpectedVersion(command.ExpectedItemVersion) && command.TickCount == 0,
            CookingRecipeOperation.AdvanceTicks => HasIdentifier(command.Process) && command.ExpectedItemVersion == 0 &&
                command.TickCount is > 0 and <= MaximumTickCount,
            CookingRecipeOperation.Drop => HasIdentifier(command.Item) && (HasIdentifier(command.Station) || !string.IsNullOrWhiteSpace(command.WorldAnchor)) &&
                IsExpectedVersion(command.ExpectedItemVersion) && command.TickCount == 0,
            CookingRecipeOperation.PutIn => HasIdentifier(command.Item) && HasIdentifier(command.Container) &&
                IsExpectedVersion(command.ExpectedItemVersion) && command.TickCount == 0,
            CookingRecipeOperation.TakeOut => HasIdentifier(command.Item) && HasIdentifier(command.Container) &&
                IsExpectedVersion(command.ExpectedItemVersion) && command.TickCount == 0,
            CookingRecipeOperation.Pour => HasIdentifier(command.Item) && HasIdentifier(command.Container) &&
                IsExpectedVersion(command.ExpectedItemVersion) && command.TickCount == 0,
            CookingRecipeOperation.SubmitOrder or CookingRecipeOperation.BindOrder or CookingRecipeOperation.RebindOrder => HasIdentifier(command.Item) && HasIdentifier(command.Order) &&
                IsExpectedVersion(command.ExpectedItemVersion) && command.TickCount == 0,
            CookingRecipeOperation.Move => command.Item is null && command.Process is null && command.Recipe is null &&
                command.Container is null && command.Station is null && command.Order is null && command.ExpectedItemVersion == 0 &&
                command.TickCount == 0 && Math.Abs((long)command.MoveX) <= 1000 && Math.Abs((long)command.MoveY) <= 1000 &&
                Math.Abs((long)command.FacingX) <= 1 && Math.Abs((long)command.FacingY) <= 1,
            CookingRecipeOperation.ContinueProcess or CookingRecipeOperation.StopProcess => HasIdentifier(command.Process) &&
                command.ExpectedItemVersion == 0 && command.TickCount == 0,
            CookingRecipeOperation.ServePortion => HasIdentifier(command.Item) && HasIdentifier(command.Container) &&
                IsExpectedVersion(command.ExpectedItemVersion) && command.TickCount == 0,
            CookingRecipeOperation.ClearContents or CookingRecipeOperation.DiscardItem or CookingRecipeOperation.UnbindOrder => HasIdentifier(command.Item) &&
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

public enum CookingOrderStatus
{
    Open,
    Completed,
    Unsatisfied,
}

/// <summary>
/// 结算记录：提交成功时追加的确定事实（订单、模板、recipe、产物、玩家、容器与逻辑 Tick）。
/// 不含评分、收益或评价字段——那些属小关结算，owner 已推迟。
/// </summary>
public sealed record CookingOrderSettlement(
    long Sequence,
    OrderId Order,
    OrderTemplateId Template,
    RecipeId Recipe,
    ItemId Product,
    PlayerId Player,
    ItemId Container,
    long LogicalTick);

/// <summary>前厅注入开单的结果（对称 <see cref="CookingWashCompletionResult"/>）。</summary>
public sealed record CookingOrderResult(bool Accepted, string ReasonCode)
{
    public const string AcceptedReason = "OrderOpened";
    public const string OrderTemplateNotFound = "OrderTemplateNotFound";
    public const string OrderIdentityConflict = "OrderIdentityConflict";
}

/// <summary>
/// NPC 洗碗端口：领域只在提交成功时发出“这个容器脏了、交给 NPC”的请求；
/// 走到池边与占用若干 tick 清洗属前厅/NPC 范围，由测试直接注入清洗完成。
/// </summary>
public interface ICookingBowlWashingPort
{
    void RequestWash(ItemId bowl, DefinitionId definition);
}

public sealed record CookingWashCompletionResult(bool Accepted, string ReasonCode);

public interface ICookingProductIdAllocator
{
    ItemId GetProductId(long productSequence);
}

internal interface ICookingRecipeLifecycleGate
{
    bool IsGameplayMutationOpen { get; }

    CookingLevelScope? LevelScope => null;
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

public sealed partial class CookingRecipeSimulation
{
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly CookingRecipeFixture _fixture;
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
    private readonly Dictionary<OrderId, OrderState> _orders = new();
    private readonly List<CookingOrderSettlement> _settlements = new();
    private readonly Dictionary<RecipeCommandKey, ProcessedCommand> _processedCommands = new();
    private readonly List<CookingRecipeEvent> _events = new();
    private List<CookingRecipeTickEvent> _tickEvents = new();
    private CookingLevelScope? _levelScope;
    private long _stateVersion;
    private long _eventSequence;
    private long _nextProcessId;
    private long _nextProductId;
    private long _nextSettlementSequence;
    private bool _lifecycleClosed;
    private bool _mutationInProgress;
    private ICookingRecipeLifecycleGate? _lifecycleGate;
    private ICookingRecipeAuthorityGate? _authorityGate;
    private bool _isClosing;
    private bool _isCompleted;

    public CookingRecipeSimulation(
        CookingRecipeFixture fixture,
        ICookingProductIdAllocator? productIdAllocator = null,
        ICookingBowlWashingPort? bowlWashingPort = null)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        if (fixture.Spatial is { } spatial)
            foreach (var pose in spatial.InitialPoses) _poses.Add(pose.Player, pose);
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
                ValidateSpatialItemLocation(ItemLocation.World(fixture.CleanPoolLocation), definition);
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

    internal bool HasOrderTemplate(OrderTemplateId template) => _fixture.OrderTemplates.ContainsKey(template);

    /// <summary>
    /// 前厅注入开单（与 <see cref="CompleteWash"/> 同一模式）：模板必须存在于 fixture，
    /// 订单身份未被使用过；开单不是玩家命令，不进命令路径、不产生命令事件。
    /// </summary>
    public CookingOrderResult OpenOrder(OrderId order, OrderTemplateId template)
    {
        if (!_fixture.OrderTemplates.TryGetValue(template, out var declared))
            return new CookingOrderResult(false, CookingOrderResult.OrderTemplateNotFound);
        if (_orders.ContainsKey(order))
            return new CookingOrderResult(false, CookingOrderResult.OrderIdentityConflict);

        _orders.Add(order, new OrderState(order, template, declared.RequiredRecipe,
            declared.RequiredContainerDefinition, CookingOrderStatus.Open, null));
        _stateVersion++;
        return new CookingOrderResult(true, CookingOrderResult.AcceptedReason);
    }

    /// <summary>
    /// 前厅在顾客超时离席时关闭仍开放的订单。不写结算，不加钱，也不把关卡标成失败。
    /// </summary>
    public CookingOrderResult MarkOrderUnsatisfied(OrderId order)
    {
        if (!_orders.TryGetValue(order, out var state))
            return new CookingOrderResult(false, "OrderNotFound");
        if (state.Status != CookingOrderStatus.Open)
            return new CookingOrderResult(false, "OrderNotOpen");

        ClearOrderBinding(order);
        _orders[order] = state with { Status = CookingOrderStatus.Unsatisfied };
        _stateVersion++;
        return new CookingOrderResult(true, "OrderUnsatisfied");
    }

    /// <summary>测试入口：把已开订单标成完成，不写结算。</summary>
    public void MarkOrderCompletedForTest(OrderId order)
    {
        if (!_orders.TryGetValue(order, out var state) || state.Status != CookingOrderStatus.Open)
            throw new ArgumentException($"Order '{order}' is not open.", nameof(order));
        ClearOrderBinding(order);
        _orders[order] = state with { Status = CookingOrderStatus.Completed, CompletedAtLogicalTick = LogicalTick };
        _stateVersion++;
    }

    /// <summary>测试入口：把一只在册干净碗标成待洗。不产生结算。</summary>
    public void MarkBowlDirtyForTest(ItemId bowl)
    {
        if (!_items.TryGetValue(bowl, out var state) || state.Removed || state.IsDirty)
            throw new ArgumentException($"Bowl '{bowl}' is not a clean registered bowl.", nameof(bowl));
        if (!_fixture.WashableContainerDefinitions.Contains(state.Definition))
            throw new ArgumentException($"Definition '{state.Definition}' is not washable.", nameof(bowl));
        if (_cleanContainerCount.TryGetValue(state.Definition, out var cleanCount))
            _cleanContainerCount[state.Definition] = cleanCount - 1;
        _items[bowl] = state with { IsDirty = true, Removed = true, Version = state.Version + 1 };
        _stateVersion++;
    }

    public IReadOnlyList<ItemId> DirtyBowlsAwaitingWash() => _items
        .Where(pair => pair.Value.Removed && pair.Value.IsDirty &&
            _fixture.WashableContainerDefinitions.Contains(pair.Value.Definition))
        .Select(pair => pair.Key)
        .OrderBy(item => item.Value, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<CookingOrderSettlement> SettlementHistory => _settlements;

    public IReadOnlyList<CookingOrderSnapshotOrder> Orders => _orders.Values
        .OrderBy(order => order.Id.Value, StringComparer.Ordinal)
        .Select(order => new CookingOrderSnapshotOrder(order.Id, order.Template, order.RequiredRecipe,
            order.RequiredContainerDefinition, order.Status.ToString(), order.CompletedAtLogicalTick))
        .ToArray();

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
                pair.Value.Recipe, pair.Value.IsProduct, pair.Value.OriginStation, pair.Value.ContainerCompleted, pair.Value.IsDirty, pair.Value.RemainingPortions, pair.Value.BoundOrder))
            .ToArray(),
        AllProcesses()
            .OrderBy(process => process.Id.Value, StringComparer.Ordinal)
            .Select(process => new CookingRecipeSnapshotProcess(process.Id, process.Recipe, process.Player, process.Anchor,
                process.Station, process.ElapsedTicks, process.RequiredTicks, process.ActiveWorker))
            .ToArray(),
        _containerItems.OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
            .Select(pair => new CookingRecipeSnapshotContainer(pair.Key, ContainerCapacity(pair.Key),
                pair.Value.OrderBy(item => item.Value, StringComparer.Ordinal).ToArray()))
            .ToArray(),
        CompletedOrderIds(),
        Orders,
        _settlements.ToArray(),
        CalculateTotalScore(),
        CalculateStars(),
        _isClosing,
        _isCompleted,
        _poses.Values.OrderBy(p => p.Player.Value, StringComparer.Ordinal).ToArray());

    public void UpdateFrontOfHouseState(bool isClosing, bool isCompleted)
    {
        if (_isClosing != isClosing || _isCompleted != isCompleted)
        {
            _isClosing = isClosing;
            _isCompleted = isCompleted;
            _stateVersion++;
        }
    }

    public int CalculateTotalScore()
    {
        var total = 0;
        foreach (var settlement in _settlements)
        {
            if (_fixture.OrderTemplates.TryGetValue(settlement.Template, out var template))
            {
                total += template.BaseScore;
            }
            else
            {
                total += 100;
            }
        }
        return total;
    }

    public int CalculateStars() => _fixture.ScoreThresholds.EvaluateStars(CalculateTotalScore());

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

        if (location.Kind == LocationKind.ContainerSlot)
        {
            if (location.OwnerId is null || location.SlotId is null || !TryGetContainerCapability(new ItemId(location.OwnerId), out var cap) ||
                !cap.AcceptedDefinitions.Contains(definition) || ItemsInContainer(new ItemId(location.OwnerId)).Count >= cap.Capacity ||
                ItemsInContainer(new ItemId(location.OwnerId)).Any(i => _items[i].Location.SlotId == location.SlotId))
                throw new ArgumentException("Invalid initial container slot.");
        }
        ValidateSpatialItemLocation(location, definition);
        _items.Add(id, new ItemState(definition, version, location, false, null, false, null));
        if (location.Kind == LocationKind.PlayerHand)
            _hands[new PlayerId(location.OwnerId!)] = id;
        if (location.Kind == LocationKind.ContainerSlot)
            EnsureContainerList(new ItemId(location.OwnerId!)).Add(id);
        if (_fixture.Items.TryGetValue(definition, out var added) && added.Container is not null)
            EnsureContainerList(id);
    }

    internal void CloseLifecycle() => _lifecycleClosed = true;

    private CookingMajorProgress? _majorProgress;

    /// <summary>
    /// 准备态换工位。没做完的工序改挂新工位，已用 tick 不变。
    /// 冲突时整次不动。不需要工位的工序不改。
    /// </summary>
    public CookingMajorProgressResult MigrateStations(IReadOnlyList<CookingStationReplacement> replacements)
    {
        ArgumentNullException.ThrowIfNull(replacements);
        if (_lifecycleGate?.IsGameplayMutationOpen == true)
            return new CookingMajorProgressResult(false, CookingMajorProgressReason.InvalidState);
        if (replacements.Count == 0)
            return new CookingMajorProgressResult(false, CookingMajorProgressReason.UnknownChoice);

        var moved = new Dictionary<ProcessId, ProcessState>();
        var occupied = new HashSet<StationSlotId>(_processesByStation.Keys);
        foreach (var replacement in replacements)
        {
            if (!_fixture.Appliances.ContainsKey(replacement.From) || !_fixture.Appliances.ContainsKey(replacement.To))
                return new CookingMajorProgressResult(false, CookingMajorProgressReason.UnknownChoice);
        }

        foreach (var replacement in replacements)
        {
            if (!_processesByStation.TryGetValue(replacement.From, out var process))
                continue;
            if (occupied.Contains(replacement.To) && replacement.From != replacement.To)
                return new CookingMajorProgressResult(false, CookingMajorProgressReason.StationConflict);
            occupied.Remove(replacement.From);
            occupied.Add(replacement.To);
            moved[process.Id] = process with { Station = replacement.To, ActiveWorker = null };
        }

        foreach (var pair in moved)
        {
            var from = _stationsByProcess[pair.Key];
            _processesByStation.Remove(from);
            _processesByStation[pair.Value.Station!.Value] = pair.Value;
            _stationsByProcess[pair.Key] = pair.Value.Station!.Value;
        }

        foreach (var process in AllProcesses().ToArray())
            StageProcess(process with { ActiveWorker = null }, _processesByStation, _processesByAnchorItem);
        return new CookingMajorProgressResult(true, CookingMajorProgressReason.None);
    }

    /// <summary>
    /// 准备态把解锁的定义按标准供应摆进延续厨房。未知定义或这个实例已经在则零变更。
    /// 不重摆整间厨房。
    /// </summary>
    public CookingMajorProgressResult PlaceUnlock(CookingContent content, DefinitionId definition)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (_lifecycleClosed || _lifecycleGate?.IsGameplayMutationOpen == true)
            return new CookingMajorProgressResult(false, CookingMajorProgressReason.InvalidState);
        var entries = content.StandardInitialSupply.Where(entry => entry.Definition == definition).ToArray();
        if (entries.Length == 0)
            return new CookingMajorProgressResult(false, CookingMajorProgressReason.UnknownChoice);

        var planned = new List<(ItemId Id, ItemLocation Location)>();
        foreach (var entry in entries)
        {
            if (StringComparer.Ordinal.Equals(entry.Location, CookingContentCatalog.CleanPoolLocation))
                continue;
            if (!_fixture.Items.ContainsKey(entry.Definition))
                return new CookingMajorProgressResult(false, CookingMajorProgressReason.UnknownChoice);
            for (var index = 1; index <= entry.Count; index++)
            {
                var id = new ItemId($"{entry.Definition.Value}-unlock-{index}");
                if (_items.ContainsKey(id))
                    return new CookingMajorProgressResult(false, CookingMajorProgressReason.StationConflict);
                if (entry.Location.StartsWith("station:", StringComparison.Ordinal))
                    planned.Add((id, ItemLocation.Station(new StationSlotId(entry.Location["station:".Length..]))));
                else if (entry.Location.StartsWith("world:", StringComparison.Ordinal))
                    planned.Add((id, ItemLocation.World(entry.Location["world:".Length..])));
                else
                    return new CookingMajorProgressResult(false, CookingMajorProgressReason.UnknownChoice);
            }
        }

        foreach (var item in planned)
        {
            if (_items.ContainsKey(item.Id))
                return new CookingMajorProgressResult(false, CookingMajorProgressReason.StationConflict);
        }

        var gate = _lifecycleGate;
        _lifecycleGate = null;
        try
        {
            foreach (var item in planned)
                AddItem(item.Id, definition, item.Location);
        }
        finally
        {
            _lifecycleGate = gate;
        }

        return new CookingMajorProgressResult(true, CookingMajorProgressReason.None);
    }

    public void UseMajorProgress(CookingMajorProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        _majorProgress = progress;
    }

    /// <summary>
    /// 失败重开的新厨房套用当前进程的选择。装修为空则跳过。
    /// 任一拒绝时厨房保持调用前的现场，进度对象也不改。
    /// 调用方必须还没把这间厨房安装到下一代。
    /// </summary>
    public CookingMajorProgressResult ApplyRetryChoices(CookingContent content, CookingMajorProgress progress)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(progress);
        if (_lifecycleClosed)
            return new CookingMajorProgressResult(false, CookingMajorProgressReason.InvalidState);

        if (progress.Decoration.Count > 0)
        {
            var moved = MigrateStations(progress.Decoration);
            if (!moved.Accepted)
                return moved;
        }

        var planned = new List<(ItemId Id, ItemLocation Location, DefinitionId Definition)>();
        var reserved = new HashSet<ItemId>();
        foreach (var definition in progress.Unlocks)
        {
            var entries = content.StandardInitialSupply.Where(entry => entry.Definition == definition).ToArray();
            if (entries.Length == 0 || !_fixture.Items.ContainsKey(definition))
                return new CookingMajorProgressResult(false, CookingMajorProgressReason.UnknownChoice);
            foreach (var entry in entries)
            {
                if (StringComparer.Ordinal.Equals(entry.Location, CookingContentCatalog.CleanPoolLocation))
                    continue;
                ItemLocation location;
                if (entry.Location.StartsWith("station:", StringComparison.Ordinal))
                    location = ItemLocation.Station(new StationSlotId(entry.Location["station:".Length..]));
                else if (entry.Location.StartsWith("world:", StringComparison.Ordinal))
                    location = ItemLocation.World(entry.Location["world:".Length..]);
                else
                    return new CookingMajorProgressResult(false, CookingMajorProgressReason.UnknownChoice);
                for (var index = 1; index <= entry.Count; index++)
                {
                    var id = new ItemId($"{definition.Value}-unlock-{index}");
                    if (_items.ContainsKey(id) || !reserved.Add(id))
                        return new CookingMajorProgressResult(false, CookingMajorProgressReason.StationConflict);
                    planned.Add((id, location, definition));
                }
            }
        }

        var gate = _lifecycleGate;
        _lifecycleGate = null;
        try
        {
            foreach (var item in planned)
                AddItem(item.Id, item.Definition, item.Location);
            UseMajorProgress(progress);
            return new CookingMajorProgressResult(true, CookingMajorProgressReason.None);
        }
        finally
        {
            _lifecycleGate = gate;
        }
    }

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
        if (lifecycleGate.LevelScope is { } levelScope)
            BindLevelScope(levelScope);
        _lifecycleGate = lifecycleGate;
    }

    private void BindLevelScope(CookingLevelScope levelScope)
    {
        ArgumentNullException.ThrowIfNull(levelScope);
        if (!Equals(levelScope.MatchScope, _fixture.Scope))
            throw new ArgumentException("The level scope does not match the recipe simulation scope.", nameof(levelScope));
        if (_levelScope is not null && !Equals(_levelScope, levelScope))
            throw new InvalidOperationException("The recipe simulation is already bound to a different level generation.");
        _levelScope = levelScope;
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

        var result = ExecuteValidatedCommand(command);
        _processedCommands.Add(key, new ProcessedCommand(fingerprint, result));
        return result;
    }

    private CookingRecipeCommandResult ExecuteValidatedCommand(CookingRecipeCommand command)
    {
        if (!TryValidateCommandScopeAndPlayer(command, out _, out var scopeReason)) return Reject(scopeReason);
        if (!CommandIsReachable(command)) return Reject(CookingRecipeRejectionReason.TargetOutOfRange);
        return command.Operation switch
        {
            CookingRecipeOperation.Pickup => Pickup(command),
            CookingRecipeOperation.StartProcess => StartProcess(command),
            CookingRecipeOperation.AdvanceTicks => AdvanceTicks(command),
            CookingRecipeOperation.Drop => Drop(command),
            CookingRecipeOperation.PutIn => PutIn(command),
            CookingRecipeOperation.TakeOut => TakeOut(command),
            CookingRecipeOperation.Pour => Pour(command),
            CookingRecipeOperation.SubmitOrder => SubmitOrder(command),
            CookingRecipeOperation.BindOrder or CookingRecipeOperation.UnbindOrder or CookingRecipeOperation.RebindOrder => MutateBinding(command),
            CookingRecipeOperation.Move => Move(command),
            CookingRecipeOperation.ContinueProcess => ChangeWorker(command, true),
            CookingRecipeOperation.StopProcess => ChangeWorker(command, false),
            CookingRecipeOperation.ServePortion => ServePortion(command),
            CookingRecipeOperation.ClearContents => ClearContents(command),
            CookingRecipeOperation.DiscardItem => DiscardItem(command),
            _ => throw new ArgumentOutOfRangeException(nameof(command.Operation), command.Operation, null),
        };
    }

    public CookingRecipeTickResult AdvanceFixedTick(CookingLevelScope levelScope, long hostFrameSequence)
    {
        ArgumentNullException.ThrowIfNull(levelScope);
        EnsureGameplayMutationOpen();
        if (_mutationInProgress)
            throw new InvalidOperationException("The recipe simulation mutation cannot be reentered.");
        if (hostFrameSequence <= 0)
            throw new ArgumentOutOfRangeException(nameof(hostFrameSequence));
        BindLevelScope(levelScope);

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
            if (_fixture.Recipes[process.Recipe].Execution == CookingRecipeExecutionKind.Manual &&
                (process.ActiveWorker is not { } worker || !WorkerCanReach(worker, process)))
            {
                StageProcess(process with { ActiveWorker = null }, replacementProcessesByStation, replacementProcessesByAnchorItem);
                continue;
            }
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
                replacementItems[process.Anchor] = anchorState with { ContainerCompleted = true, Recipe = recipe.Id, RemainingPortions = recipe.YieldPortions };
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
        if (process.Station is { } requiredStation &&
            (!_fixture.Appliances[requiredStation].IsAvailable ||
             !_fixture.Appliances[requiredStation].Capabilities.Contains(recipe.RequiredApplianceCapability)))
            throw new InvalidOperationException($"Process '{process.Id}' station cannot perform its recipe.");

        foreach (var input in process.LockedInputs)
        {
            if (!_inputsByProcessItem.TryGetValue(input, out var owner) || owner != process.Id)
                throw new InvalidOperationException($"Process '{process.Id}' lost ownership of locked input '{input}'.");
            if (!_items.TryGetValue(input, out var inputState) || inputState.Removed)
                throw new InvalidOperationException($"Process '{process.Id}' references unavailable input '{input}'.");
            if (!recipe.Inputs.Contains(inputState.Definition) &&
                !(recipe.DefaultInputs?.Contains(inputState.Definition) ?? false) && input != process.Anchor)
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
        if (!_items.TryGetValue(process.Anchor, out var anchor) || anchor.Removed)
            throw new InvalidOperationException($"Process '{process.Id}' has an unavailable anchor.");
        var physicalInputs = process.Container is { } vessel ? ItemsInContainer(vessel).ToArray() : new[] { process.Anchor };
        var expectedLocks = physicalInputs.Append(process.Anchor).ToHashSet();
        if (!expectedLocks.SetEquals(process.LockedInputs) || process.LockedInputs.Count != expectedLocks.Count)
            throw new InvalidOperationException($"Process '{process.Id}' does not lock all physical inputs exactly once.");
        if (process.Container is { } containerId && (containerId != process.Anchor || !TryGetContainerCapability(containerId, out _)))
            throw new InvalidOperationException($"Process '{process.Id}' has an invalid carrier.");
        if (CookingRecipeMatcher.Match(physicalInputs.Select(id => _items[id].Definition).ToArray(), null,
                new[] { recipe }, process.Container is null ? null : anchor.Definition).Outcome != CookingRecipeMatchOutcome.Matched)
            throw new InvalidOperationException($"Process '{process.Id}' input quantities or carrier disagree with its recipe.");
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
        _levelScope = plan.TickEvent.LevelScope;
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
            (item.Location.SlotId is null || !StationIsReachable(player, new StationSlotId(item.Location.SlotId))))
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

        if (!TryResolveStartRecipe(command, presentInputs, appliance, containerCapability is null ? null : anchorItem.Definition, out var recipe, out rejection))
            return Reject(rejection);

        if (recipe.RequiresStation)
        {
            if (stationId is not { } requiredStation)
                return Reject(CookingRecipeRejectionReason.ApplianceNotFound);
            if (!appliance!.IsAvailable)
                return Reject(CookingRecipeRejectionReason.ApplianceUnavailable);
            if (!appliance.Capabilities.Contains(recipe.RequiredApplianceCapability))
                return Reject(CookingRecipeRejectionReason.ApplianceCapabilityMismatch);
            if (!StationIsReachable(player, requiredStation))
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

        if (anchorItem.ContainerCompleted) return Reject(CookingRecipeRejectionReason.BatchCompleted);
        if (recipe.Execution == CookingRecipeExecutionKind.Manual && AllProcesses().Any(p => p.ActiveWorker == command.Player))
            return Reject(CookingRecipeRejectionReason.WorkerUnavailable);
        var lockedInputs = new List<ItemId> { anchorId };
        lockedInputs.AddRange(ItemsInContainer(anchorId));
        foreach (var input in lockedInputs)
        {
            if (_items[input].BoundOrder is not null) return Reject(CookingRecipeRejectionReason.BindingConflict);
            if (!_fixture.Items[_items[input].Definition].AllowedPlayerCapabilities.Overlaps(player.Capabilities))
                return Reject(CookingRecipeRejectionReason.PlayerIneligible);
        }

        if (_fixture.Spatial is not null && containerId is null && anchorItem.Location.Kind == LocationKind.PlayerHand &&
            stationId is { } target && _items.Values.Any(i => !i.Removed && i.Location == ItemLocation.Station(target)))
            return Reject(CookingRecipeRejectionReason.ContainerFull);
        _ = checked(_nextProcessId + 1); _ = checked(anchorItem.Version + 1); _ = checked(_stateVersion + 1); _ = checked(_eventSequence + 1);
        if (containerId is null && anchorItem.Location.Kind == LocationKind.PlayerHand && stationId is { } moveStation)
        {
            _hands[command.Player] = null;
            _items[anchorId] = anchorItem with { Location = ItemLocation.Station(moveStation), Version = anchorItem.Version + 1 };
        }

        var processId = new ProcessId($"process-{++_nextProcessId}");
        var requiredTicks = _majorProgress?.CookTicks(recipe.Id, recipe.RequiredTicks) ?? recipe.RequiredTicks;
        var process = new ProcessState(processId, recipe.Id, command.Player, anchorId, stationId, 0, requiredTicks,
            recipe.Completion, containerId, lockedInputs,
            recipe.Execution == CookingRecipeExecutionKind.Manual ? command.Player : null);
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
        CookingApplianceDefinition? appliance, DefinitionId? processingContainerDefinition, out CookingRecipeDefinition recipe,
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
            if (CookingRecipeMatcher.Match(presentInputs, null, new[] { explicitRecipe }, processingContainerDefinition).Outcome != CookingRecipeMatchOutcome.Matched)
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
        var match = CookingRecipeMatcher.Match(presentInputs, appliance?.Capabilities, stationBound, processingContainerDefinition);
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
        var sameBinding = CookingRecipeMatcher.Match(presentInputs, null, stationBound, processingContainerDefinition);
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
        var opposite = CookingRecipeMatcher.Match(presentInputs, null, oppositeBound, processingContainerDefinition);
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
        if (_fixture.Recipes[process.Recipe].Execution == CookingRecipeExecutionKind.Manual)
            return Reject(CookingRecipeRejectionReason.WorkerUnavailable);
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
            var nextSequence = checked(_nextProductId + 1);
            var allocated = _productIdAllocator.GetProductId(nextSequence);
            if (string.IsNullOrWhiteSpace(allocated.Value) || _items.ContainsKey(allocated))
                throw new InvalidOperationException("Product allocator returned invalid identity.");
            _ = checked(LogicalTick + command.TickCount); _ = checked(_stateVersion + 1); _ = checked(_eventSequence + 1);
            foreach (var input in ConsumedInputs(process)) _ = checked(_items[input].Version + 1);
            _nextProductId = nextSequence;
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
            _items[process.Anchor] = anchorState with { ContainerCompleted = true, Recipe = recipe.Id, RemainingPortions = recipe.YieldPortions };
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
        if (item.BoundOrder is not null) return Reject(CookingRecipeRejectionReason.BindingConflict);
        if (IsLockedInput(itemId))
            return Reject(CookingRecipeRejectionReason.ItemStale);
        if (item.Location != ItemLocation.Hand(command.Player))
            return Reject(CookingRecipeRejectionReason.CurrentLocationMismatch);
        if (command.WorldAnchor is { } world)
        {
            var location = ItemLocation.World(world);
            if (world == _fixture.CleanPoolLocation && _fixture.CleanContainerSupply.Count > 0) return Reject(CookingRecipeRejectionReason.ContainerRejectsItem);
            if (_fixture.Spatial is null || !LocationIsReachable(location, command.Player)) return Reject(CookingRecipeRejectionReason.TargetOutOfRange);
            if (_items.Values.Any(i => !i.Removed && i.Location == location)) return Reject(CookingRecipeRejectionReason.ContainerFull);
            var version = checked(item.Version + 1); _ = checked(_stateVersion + 1); _ = checked(_eventSequence + 1);
            _hands[command.Player] = null;
            _items[itemId] = item with { Location = location, Version = version };
            return Commit(command, null, null, itemId, "item-dropped");
        }
        if (command.Station is not { } stationId || !_fixture.Appliances.TryGetValue(stationId, out var appliance) ||
            !appliance.IsAvailable)
            return Reject(CookingRecipeRejectionReason.ApplianceUnavailable);
        if (!StationIsReachable(player, stationId))
            return Reject(CookingRecipeRejectionReason.TargetOutOfRange);

        if (_fixture.Spatial is not null && _items.Values.Any(i => !i.Removed && i.Location == ItemLocation.Station(stationId)))
            return Reject(CookingRecipeRejectionReason.ContainerFull);
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
        if (item.BoundOrder is not null) return Reject(CookingRecipeRejectionReason.BindingConflict);
        if (IsLockedInput(itemId))
            return Reject(CookingRecipeRejectionReason.ItemStale);
        if (item.Location != ItemLocation.Hand(command.Player))
            return Reject(CookingRecipeRejectionReason.CurrentLocationMismatch);
        if (command.Container is not { } containerId || !TryGetContainerCapability(containerId, out var container))
            return Reject(CookingRecipeRejectionReason.ContainerNotFound);
        if (ItemsInContainer(containerId).Any(id => _items[id].BoundOrder is not null)) return Reject(CookingRecipeRejectionReason.BindingConflict);
        if (_items[containerId].ContainerCompleted) return Reject(CookingRecipeRejectionReason.BatchCompleted);
        if (IsLockedInput(containerId)) return Reject(CookingRecipeRejectionReason.ItemStale);
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
        if (_items[containerId].ContainerCompleted) return Reject(CookingRecipeRejectionReason.BatchCompleted);
        if (item.Location is not { Kind: LocationKind.ContainerSlot, OwnerId: { } owner } || owner != containerId.Value)
            return Reject(CookingRecipeRejectionReason.CurrentLocationMismatch);
        if (item.BoundOrder is not null) return Reject(CookingRecipeRejectionReason.BindingConflict);
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
        if (contents.Any(id => _items[id].BoundOrder is not null)) return Reject(CookingRecipeRejectionReason.BindingConflict);
        if (contents.Any(item => IsLockedInput(item)))
            return Reject(CookingRecipeRejectionReason.ItemStale);

        if (source.ContainerCompleted)
            return PourCompletedContainer(command, sourceId, source, targetId, targetCapability, contents);

        if (ItemsInContainer(targetId).Any(id => _items[id].BoundOrder is not null)) return Reject(CookingRecipeRejectionReason.BindingConflict);
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
        if (source.Recipe is not { } recipe || _fixture.Recipes[recipe].YieldPortions != 1)
            return Reject(CookingRecipeRejectionReason.BatchCompleted);
        return TransferPortions(command, sourceId, source, targetId, targetCapability, 1);
    }

    private bool ContainerIsReachable(ItemId container, CookingPlayerConfig player)
    {
        return ItemIsReachable(container, player.Id);
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

    /// <summary>Completed 订单 ID 列表：保持 <see cref="CookingRecipeSnapshot.AcceptedOrders"/> 语义不变。</summary>
    private IReadOnlyList<OrderId> CompletedOrderIds() => _orders.Values
        .Where(order => order.Status == CookingOrderStatus.Completed)
        .Select(order => order.Id)
        .OrderBy(order => order.Value, StringComparer.Ordinal)
        .ToArray();

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

    /// <summary>
    /// 提交订单：领域按订单簿校验（订单存在且 Open、产物 recipe 与容器定义匹配订单要求），
    /// 成功时原子地消耗产物、订单转 Completed、追加结算记录，并按可洗碗定义把容器交给 NPC。
    /// 同 command identity 重放由 SubmitCore 缓存层返回 IsDuplicate；跨 identity 不得二次变更。
    /// </summary>
    private CookingRecipeCommandResult SubmitOrder(CookingRecipeCommand command)
    {
        if (!TryValidateCommandScopeAndPlayer(command, out var player, out var rejection))
            return Reject(rejection);
        if (command.Item is not { } productId || !_items.TryGetValue(productId, out var product) || product.Removed || !product.IsProduct)
            return Reject(_consumedProducts.Contains(command.Item ?? default) ? CookingRecipeRejectionReason.ProductAlreadyConsumed : CookingRecipeRejectionReason.ProductNotFound);
        if (command.Order is not { } orderId)
            return Reject(CookingRecipeRejectionReason.OrderNotFound);
        if (!_orders.TryGetValue(orderId, out var order))
            return Reject(CookingRecipeRejectionReason.OrderNotFound);
        if (order.Status != CookingOrderStatus.Open)
            return Reject(CookingRecipeRejectionReason.OrderAlreadyCompleted);
        if (product.Version != command.ExpectedItemVersion)
            return Reject(CookingRecipeRejectionReason.ItemStale);
        if (product.Location.Kind != LocationKind.ContainerSlot)
            return Reject(CookingRecipeRejectionReason.ProductNotPlated);
        if (product.Location.OwnerId is null ||
            !ContainerIsReachable(new ItemId(product.Location.OwnerId), player))
            return Reject(CookingRecipeRejectionReason.TargetOutOfRange);
        if (product.Recipe is not { } recipe)
            return Reject(CookingRecipeRejectionReason.ProductNotFound);
        if (recipe != order.RequiredRecipe)
            return Reject(CookingRecipeRejectionReason.OrderRequirementMismatch);

        var container = new ItemId(product.Location.OwnerId!);
        var containerState = _items[container];
        if (IsLockedInput(productId) || IsLockedInput(container) || IsActiveProcessAnchor(productId) || IsActiveProcessAnchor(container))
            return Reject(CookingRecipeRejectionReason.ItemStale);
        if (containerState.Removed || containerState.IsDirty || containerState.ContainerCompleted ||
            !TryGetContainerCapability(container, out var servingCapability) || !servingCapability.AcceptedDefinitions.Contains(product.Definition) ||
            containerState.Definition != order.RequiredContainerDefinition || IsWorkingCarrier(containerState.Definition))
            return Reject(CookingRecipeRejectionReason.OrderRequirementMismatch);

        if (!_fixture.Items[product.Definition].AllowedPlayerCapabilities.Overlaps(player.Capabilities) ||
            !_fixture.Items[containerState.Definition].AllowedPlayerCapabilities.Overlaps(player.Capabilities))
            return Reject(CookingRecipeRejectionReason.PlayerIneligible);
        if (_fixture.Items[containerState.Definition].Container?.DisposableOnSubmission == true && ItemsInContainer(container).Count != 1)
            return Reject(CookingRecipeRejectionReason.OrderRequirementMismatch);
        if (product.BoundOrder is { } bound && bound != orderId)
            return Reject(CookingRecipeRejectionReason.BindingConflict);
        if (_fixture.OrderTemplates[order.Template].RequiresBinding && product.BoundOrder != orderId)
            return Reject(CookingRecipeRejectionReason.BindingRequired);
        if (_items.Any(p => p.Value.BoundOrder == orderId && p.Key != productId))
            return Reject(CookingRecipeRejectionReason.BindingConflict);
        _ = checked(product.Version + 1); _ = checked(containerState.Version + 1);
        _ = checked(_stateVersion + 1); _ = checked(_eventSequence + 1); _ = checked(_nextSettlementSequence + 1);
        if (_containerItems.TryGetValue(container, out var contents))
            contents.Remove(productId);
        _items[productId] = product with { Removed = true, BoundOrder = null, Version = product.Version + 1 };
        _consumedProducts.Add(productId);
        _orders[orderId] = order with
        {
            Status = CookingOrderStatus.Completed,
            CompletedAtLogicalTick = LogicalTick,
        };
        _settlements.Add(new CookingOrderSettlement(
            ++_nextSettlementSequence, orderId, order.Template, recipe, productId, command.Player, container, LogicalTick));

        // 提交成功后容器变脏并交给 NPC 清洗：脏碗离开厨房，在册干净数下降。
        if (_fixture.Items[containerState.Definition].Container?.DisposableOnSubmission == true)
        {
            _items[container] = containerState with { Removed = true, Version = checked(containerState.Version + 1) };
            foreach (var playerId in _hands.Keys.ToArray())
                if (_hands[playerId] == container) _hands[playerId] = null;
        }
        else if (_fixture.WashableContainerDefinitions.Contains(containerState.Definition))
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
        bool IsDirty = false,
        int RemainingPortions = 0, OrderId? BoundOrder = null);

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
        IReadOnlyList<ItemId> LockedInputs,
        PlayerId? ActiveWorker = null);

    private sealed record OrderState(
        OrderId Id,
        OrderTemplateId Template,
        RecipeId RequiredRecipe,
        DefinitionId RequiredContainerDefinition,
        CookingOrderStatus Status,
        long? CompletedAtLogicalTick);

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
    IReadOnlyList<OrderId> AcceptedOrders,
    IReadOnlyList<CookingOrderSnapshotOrder> Orders,
    IReadOnlyList<CookingOrderSettlement> Settlements,
    int TotalScore = 0,
    int Stars = 0,
    bool IsClosing = false,
    bool IsCompleted = false,
    IReadOnlyList<CookingPlayerPose>? Poses = null)
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
        TotalScore,
        Stars,
        IsClosing,
        IsCompleted,
        Items.OrderBy(item => item.Id.Value, StringComparer.Ordinal)
            .Select(item => new CanonicalItem(item.Id.Value, item.Definition.Value, item.Version, item.Location.Kind.ToString(),
                item.Location.OwnerId, item.Location.SlotId, item.Recipe?.Value, item.IsProduct, item.OriginStation?.Value,
                item.ContainerCompleted, item.IsDirty, item.RemainingPortions, item.BoundOrder?.Value)).ToArray(),
        Processes.OrderBy(process => process.Id.Value, StringComparer.Ordinal)
            .Select(process => new CanonicalProcess(process.Id.Value, process.Recipe.Value, process.Player.Value,
                process.Anchor.Value, process.Station?.Value, process.ElapsedTicks, process.RequiredTicks, process.ActiveWorker?.Value)).ToArray(),
        Containers.OrderBy(container => container.Id.Value, StringComparer.Ordinal)
            .Select(container => new CanonicalContainer(container.Id.Value, container.Capacity,
                container.ItemIds.OrderBy(item => item.Value, StringComparer.Ordinal).Select(item => item.Value).ToArray())).ToArray(),
        AcceptedOrders.OrderBy(order => order.Value, StringComparer.Ordinal).Select(order => order.Value).ToArray(),
        Orders.OrderBy(order => order.Id.Value, StringComparer.Ordinal)
            .Select(order => new CanonicalOrder(order.Id.Value, order.Template.Value, order.RequiredRecipe.Value,
                order.RequiredContainerDefinition.Value, order.Status, order.CompletedAtLogicalTick)).ToArray(),
        Settlements.OrderBy(settlement => settlement.Sequence)
            .Select(settlement => new CanonicalSettlement(settlement.Sequence, settlement.Order.Value,
                settlement.Template.Value, settlement.Recipe.Value, settlement.Product.Value, settlement.Player.Value,
                settlement.Container.Value, settlement.LogicalTick)).ToArray(),
        (Poses ?? Array.Empty<CookingPlayerPose>()).OrderBy(p => p.Player.Value, StringComparer.Ordinal).ToArray()),
        CanonicalJsonOptions);

    public string Sha256() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalText())));

    private sealed record CanonicalSnapshot(string SessionId, string WorldId, string MatchId, long Version, long LogicalTick,
        int TotalScore, int Stars, bool IsClosing, bool IsCompleted,
        IReadOnlyList<CanonicalItem> Items, IReadOnlyList<CanonicalProcess> Processes, IReadOnlyList<CanonicalContainer> Containers,
        IReadOnlyList<string> AcceptedOrders, IReadOnlyList<CanonicalOrder> Orders, IReadOnlyList<CanonicalSettlement> Settlements, IReadOnlyList<CookingPlayerPose> Poses);
    private sealed record CanonicalItem(string ItemId, string DefinitionId, int Version, string LocationKind, string? OwnerId,
        string? SlotId, string? RecipeId, bool IsProduct, string? OriginStation, bool ContainerCompleted, bool IsDirty, int RemainingPortions, string? BoundOrder);
    private sealed record CanonicalProcess(string ProcessId, string RecipeId, string PlayerId, string AnchorItemId,
        string? StationId, int ElapsedTicks, int RequiredTicks, string? ActiveWorker);
    private sealed record CanonicalContainer(string ContainerId, int Capacity, IReadOnlyList<string> ItemIds);
    private sealed record CanonicalOrder(string OrderId, string TemplateId, string RequiredRecipeId,
        string RequiredContainerDefinitionId, string Status, long? CompletedAtLogicalTick);
    private sealed record CanonicalSettlement(long Sequence, string OrderId, string TemplateId, string RecipeId,
        string ProductId, string PlayerId, string ContainerId, long LogicalTick);
}

/// <summary>订单簿中的一条订单：模板、要求与状态对快照与哈希可观察。</summary>
public sealed record CookingOrderSnapshotOrder(
    OrderId Id,
    OrderTemplateId Template,
    RecipeId RequiredRecipe,
    DefinitionId RequiredContainerDefinition,
    string Status,
    long? CompletedAtLogicalTick);

public sealed record CookingRecipeSnapshotItem(ItemId Id, DefinitionId Definition, int Version, ItemLocation Location,
    RecipeId? Recipe, bool IsProduct, StationSlotId? OriginStation, bool ContainerCompleted = false, bool IsDirty = false,
        int RemainingPortions = 0, OrderId? BoundOrder = null);

public sealed record CookingRecipeSnapshotProcess(ProcessId Id, RecipeId Recipe, PlayerId Player, ItemId Anchor,
    StationSlotId? Station, int ElapsedTicks, int RequiredTicks, PlayerId? ActiveWorker = null);

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
