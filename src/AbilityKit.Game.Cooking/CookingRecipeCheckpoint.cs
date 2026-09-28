using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AbilityKit.Game.Cooking;

/// <summary>
/// 恢复载荷中的物品条目：复刻 <c>CookingRecipeSimulation</c> 内部物品状态的全字段，
/// <paramref name="Removed"/> 为 true 时即 tombstone——同步快照按定义过滤墓碑，恢复载荷必须保留。
/// </summary>
public sealed record CookingRecipeCheckpointItem(
    ItemId Id,
    DefinitionId Definition,
    int Version,
    ItemLocation Location,
    bool Removed,
    RecipeId? Recipe,
    bool IsProduct,
    StationSlotId? OriginStation,
    bool ContainerCompleted = false,
    bool IsDirty = false);

/// <summary>恢复载荷中的活动加工：含完成形态、容器锚点与被锁输入集合。</summary>
public sealed record CookingRecipeCheckpointProcess(
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

/// <summary>
/// 恢复载荷中的容器内容：列表有序。空槽名按各物品自己的 <c>Location.SlotId</c> 占用集分配，
/// 不按下标分配；但按列表顺序遍历的路径会随顺序分叉，因此载荷保序、canonical 也保序、恢复保序。
/// </summary>
public sealed record CookingRecipeCheckpointContainer(ItemId Id, IReadOnlyList<ItemId> ItemIds);

/// <summary>恢复载荷中的订单条目：与订单簿状态一致（模板、要求、状态、完成 tick）。</summary>
public sealed record CookingRecipeCheckpointOrder(
    OrderId Id,
    OrderTemplateId Template,
    RecipeId RequiredRecipe,
    DefinitionId RequiredContainerDefinition,
    CookingOrderStatus Status,
    long? CompletedAtLogicalTick);

/// <summary>恢复载荷中的干净容器池计数：与“在册干净容器”不是同一集合，必须显式入账。</summary>
public sealed record CookingRecipeCheckpointCleanPool(DefinitionId Definition, int Count);

/// <summary>
/// 恢复载荷中的去重账本条目：键为 (session, player, command)——session 来自命令自身 scope，
/// 与仿真 scope 不同的外来命令其拒绝结果同样入账，因此 session 逐条携带。
/// 缓存结果只带 Outcome/Reason/StateVersion：重放路径本就丢弃事件，不随账本搬运。
/// </summary>
public sealed record CookingRecipeCheckpointDeduplication(
    SessionId Session,
    PlayerId Player,
    RecipeCommandId Command,
    string Fingerprint,
    CookingRecipeOutcome Outcome,
    CookingRecipeRejectionReason Reason,
    long StateVersion);

/// <summary>
/// 仿真权威状态的恢复载荷。与 <see cref="CookingRecipeSnapshot"/>（每帧同步投影）分工不同：
/// 快照按定义过滤墓碑且不含去重账本、事件/tick 历史、ID 计数器、干净池计数与消耗产物账，
/// 不能承担恢复职责；本记录覆盖继续运行所需的全部权威状态，可脱离宿主自包含存在。
/// </summary>
public sealed record CookingRecipeCheckpoint(
    CookingScope Scope,
    long StateVersion,
    long LogicalTick,
    IReadOnlyList<CookingRecipeCheckpointItem> Items,
    IReadOnlyList<CookingRecipeCheckpointProcess> Processes,
    IReadOnlyList<CookingRecipeCheckpointContainer> Containers,
    IReadOnlyList<CookingRecipeCheckpointOrder> Orders,
    IReadOnlyList<CookingOrderSettlement> Settlements,
    IReadOnlyList<ItemId> ConsumedProducts,
    IReadOnlyList<CookingRecipeCheckpointCleanPool> CleanContainerCounts,
    IReadOnlyList<CookingRecipeCheckpointDeduplication> Deduplication,
    IReadOnlyList<CookingRecipeEvent> Events,
    long EventSequence,
    IReadOnlyList<CookingRecipeTickEvent> TickEvents,
    long NextProcessId,
    long NextProductId,
    long NextSettlementSequence,
    CookingLevelScope? LevelScope = null)
{
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public string CanonicalText() => JsonSerializer.Serialize(new CanonicalCheckpoint(
        Scope.Session.Value,
        Scope.World.Value,
        Scope.Match.Value,
        StateVersion,
        LogicalTick,
        Items.OrderBy(item => item.Id.Value, StringComparer.Ordinal)
            .Select(item => new CanonicalItem(item.Id.Value, item.Definition.Value, item.Version, item.Location.Kind.ToString(),
                item.Location.OwnerId, item.Location.SlotId, item.Removed, item.Recipe?.Value, item.IsProduct,
                item.OriginStation?.Value, item.ContainerCompleted, item.IsDirty)).ToArray(),
        Processes.OrderBy(process => process.Id.Value, StringComparer.Ordinal)
            .Select(process => new CanonicalProcess(process.Id.Value, process.Recipe.Value, process.Player.Value,
                process.Anchor.Value, process.Station?.Value, process.ElapsedTicks, process.RequiredTicks,
                process.Completion.ToString(), process.Container?.Value,
                process.LockedInputs.OrderBy(input => input.Value, StringComparer.Ordinal).Select(input => input.Value).ToArray()))
            .ToArray(),
        Containers.OrderBy(container => container.Id.Value, StringComparer.Ordinal)
            .Select(container => new CanonicalContainer(container.Id.Value,
                container.ItemIds.Select(item => item.Value).ToArray()))
            .ToArray(),
        Orders.OrderBy(order => order.Id.Value, StringComparer.Ordinal)
            .Select(order => new CanonicalOrder(order.Id.Value, order.Template.Value, order.RequiredRecipe.Value,
                order.RequiredContainerDefinition.Value, order.Status.ToString(), order.CompletedAtLogicalTick)).ToArray(),
        Settlements.OrderBy(settlement => settlement.Sequence)
            .Select(settlement => new CanonicalSettlement(settlement.Sequence, settlement.Order.Value,
                settlement.Template.Value, settlement.Recipe.Value, settlement.Product.Value, settlement.Player.Value,
                settlement.Container.Value, settlement.LogicalTick)).ToArray(),
        ConsumedProducts.OrderBy(product => product.Value, StringComparer.Ordinal).Select(product => product.Value).ToArray(),
        CleanContainerCounts.OrderBy(pool => pool.Definition.Value, StringComparer.Ordinal)
            .Select(pool => new CanonicalCleanPool(pool.Definition.Value, pool.Count)).ToArray(),
        Deduplication.OrderBy(entry => entry.Session.Value, StringComparer.Ordinal)
            .ThenBy(entry => entry.Player.Value, StringComparer.Ordinal)
            .ThenBy(entry => entry.Command.Value, StringComparer.Ordinal)
            .Select(entry => new CanonicalDeduplication(entry.Session.Value, entry.Player.Value, entry.Command.Value,
                entry.Fingerprint, entry.Outcome.ToString(), entry.Reason.ToString(), entry.StateVersion)).ToArray(),
        Events.Select(entry => new CanonicalEvent(entry.Sequence, entry.SimulationBatch, entry.Player.Value,
            entry.Command.Value, entry.Operation.ToString(), entry.Recipe?.Value, entry.Process?.Value, entry.Item?.Value,
            entry.Summary)).ToArray(),
        EventSequence,
        TickEvents.Select(entry => new CanonicalTickEvent(entry.Sequence, entry.LevelScope.MatchScope.Session.Value,
            entry.LevelScope.MatchScope.World.Value, entry.LevelScope.MatchScope.Match.Value,
            entry.LevelScope.RestaurantRuntime.Value, entry.LevelScope.Level.Value, entry.LevelScope.LevelEpoch,
            entry.HostFrameSequence, entry.BeforeLogicalTick, entry.AfterLogicalTick, entry.BeforeStateVersion,
            entry.AfterStateVersion, entry.Processes.Count)).ToArray(),
        LevelScope is null ? null : new CanonicalLevelBinding(LevelScope.MatchScope.Session.Value,
            LevelScope.MatchScope.World.Value, LevelScope.MatchScope.Match.Value,
            LevelScope.RestaurantRuntime.Value, LevelScope.Level.Value, LevelScope.LevelEpoch),
        NextProcessId,
        NextProductId,
        NextSettlementSequence),
        CanonicalJsonOptions);

    public string Sha256() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalText())));

    private sealed record CanonicalCheckpoint(string SessionId, string WorldId, string MatchId, long StateVersion,
        long LogicalTick, IReadOnlyList<CanonicalItem> Items, IReadOnlyList<CanonicalProcess> Processes,
        IReadOnlyList<CanonicalContainer> Containers, IReadOnlyList<CanonicalOrder> Orders,
        IReadOnlyList<CanonicalSettlement> Settlements, IReadOnlyList<string> ConsumedProducts,
        IReadOnlyList<CanonicalCleanPool> CleanContainerCounts, IReadOnlyList<CanonicalDeduplication> Deduplication,
        IReadOnlyList<CanonicalEvent> Events, long EventSequence, IReadOnlyList<CanonicalTickEvent> TickEvents,
        CanonicalLevelBinding? LevelScope, long NextProcessId, long NextProductId, long NextSettlementSequence);
    private sealed record CanonicalLevelBinding(string SessionId, string WorldId, string MatchId,
        long RestaurantRuntimeId, string LevelId, long LevelEpoch);
    private sealed record CanonicalTickEvent(long Sequence, string SessionId, string WorldId, string MatchId,
        long RestaurantRuntimeId, string LevelId, long LevelEpoch, long HostFrameSequence, long BeforeLogicalTick,
        long AfterLogicalTick, long BeforeStateVersion, long AfterStateVersion, int ProcessCount);
    private sealed record CanonicalItem(string ItemId, string DefinitionId, int Version, string LocationKind,
        string? OwnerId, string? SlotId, bool Removed, string? RecipeId, bool IsProduct, string? OriginStation,
        bool ContainerCompleted, bool IsDirty);
    private sealed record CanonicalProcess(string ProcessId, string RecipeId, string PlayerId, string AnchorItemId,
        string? StationId, int ElapsedTicks, int RequiredTicks, string Completion, string? ContainerId,
        IReadOnlyList<string> LockedInputs);
    private sealed record CanonicalContainer(string ContainerId, IReadOnlyList<string> ItemIds);
    private sealed record CanonicalOrder(string OrderId, string TemplateId, string RequiredRecipeId,
        string RequiredContainerDefinitionId, string Status, long? CompletedAtLogicalTick);
    private sealed record CanonicalSettlement(long Sequence, string OrderId, string TemplateId, string RecipeId,
        string ProductId, string PlayerId, string ContainerId, long LogicalTick);
    private sealed record CanonicalCleanPool(string DefinitionId, int Count);
    private sealed record CanonicalDeduplication(string SessionId, string PlayerId, string CommandId,
        string Fingerprint, string Outcome, string Reason, long StateVersion);
    private sealed record CanonicalEvent(long Sequence, long SimulationBatch, string PlayerId, string CommandId,
        string Operation, string? RecipeId, string? ProcessId, string? ItemId, string Summary);
}

public enum CookingCheckpointRestoreReason
{
    None,
    CheckpointNull,
    LifecycleClosed,
    ScopeMismatch,
    DuplicateItemIdentity,
    ItemDefinitionNotFound,
    HandOwnerNotPlayer,
    HandOccupiedTwice,
    DuplicateProcessIdentity,
    ProcessRecipeNotFound,
    ProcessTicksMismatch,
    ProcessCompletionMismatch,
    ProcessStationBindingMismatch,
    ProcessStationNotFound,
    ProcessProgressInvalid,
    ProcessAnchorUnavailable,
    ProcessInputUnavailable,
    ProcessInputNotInRecipe,
    ProcessInputLeftContainer,
    ProcessInputLocationUnsupported,
    ProcessContainerUnavailable,
    DuplicateContainerEntry,
    ContainerContentUnavailable,
    ContainerContentForeignSlot,
    DuplicateOrderIdentity,
    OrderTemplateNotFound,
    OrderRequirementMismatch,
    SettlementSequenceInvalid,
    ConsumedProductNotRegistered,
    CleanPoolUnknownDefinition,
    CleanPoolOutOfRange,
    DeduplicationEntryInvalid,
    EventSequenceInvalid,
    CounterInvalid,
    NotSuccessHandoff,
}

public sealed record CookingRecipeCheckpointRestoreResult(
    bool Accepted,
    CookingCheckpointRestoreReason Reason,
    CookingRecipeCheckpoint? Checkpoint = null)
{
    public static CookingRecipeCheckpointRestoreResult Reject(CookingCheckpointRestoreReason reason) =>
        new(false, reason, null);
}

/// <summary>
/// 仿真恢复出口：导出整册权威状态，或把一份载荷整册换入本实例。
/// 与 <see cref="CookingRecipeSimulation.Snapshot"/>（同步投影）分工见 <see cref="CookingRecipeCheckpoint"/>。
/// </summary>
public sealed partial class CookingRecipeSimulation
{
    /// <summary>
    /// 导出恢复载荷：墓碑、活动加工、容器有序内容、订单、结算、消耗产物账、干净池计数、
    /// 去重账本、事件/tick 历史与 ID 计数器全部入账；可派生索引（持物、进程索引、锁输入反查）不入账。
    /// </summary>
    public CookingRecipeCheckpoint ExportCheckpoint() => new(
        _fixture.Scope,
        _stateVersion,
        LogicalTick,
        _items.OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
            .Select(pair => new CookingRecipeCheckpointItem(pair.Key, pair.Value.Definition, pair.Value.Version,
                pair.Value.Location, pair.Value.Removed, pair.Value.Recipe, pair.Value.IsProduct,
                pair.Value.OriginStation, pair.Value.ContainerCompleted, pair.Value.IsDirty))
            .ToArray(),
        AllProcesses()
            .OrderBy(process => process.Id.Value, StringComparer.Ordinal)
            .Select(process => new CookingRecipeCheckpointProcess(process.Id, process.Recipe, process.Player,
                process.Anchor, process.Station, process.ElapsedTicks, process.RequiredTicks, process.Completion,
                process.Container, process.LockedInputs.ToArray()))
            .ToArray(),
        _containerItems.OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
            .Select(pair => new CookingRecipeCheckpointContainer(pair.Key, pair.Value.ToArray()))
            .ToArray(),
        _orders.Values
            .OrderBy(order => order.Id.Value, StringComparer.Ordinal)
            .Select(order => new CookingRecipeCheckpointOrder(order.Id, order.Template, order.RequiredRecipe,
                order.RequiredContainerDefinition, order.Status, order.CompletedAtLogicalTick))
            .ToArray(),
        _settlements.ToArray(),
        _consumedProducts
            .OrderBy(product => product.Value, StringComparer.Ordinal)
            .ToArray(),
        _cleanContainerCount
            .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
            .Select(pair => new CookingRecipeCheckpointCleanPool(pair.Key, pair.Value))
            .ToArray(),
        _processedCommands
            .OrderBy(pair => pair.Key.Session.Value, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.Player.Value, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.Command.Value, StringComparer.Ordinal)
            .Select(pair => new CookingRecipeCheckpointDeduplication(pair.Key.Session, pair.Key.Player, pair.Key.Command,
                pair.Value.Fingerprint, pair.Value.Result.Outcome, pair.Value.Result.Reason, pair.Value.Result.StateVersion))
            .ToArray(),
        _events.ToArray(),
        _eventSequence,
        _tickEvents.ToArray(),
        _nextProcessId,
        _nextProductId,
        _nextSettlementSequence,
        _levelScope);

    /// <summary>
    /// 把一份恢复载荷整册换入本实例：构造期状态被完全替换，替换是原子的（先构建全部新字典/列表再整体赋值）。
    /// 校验失败返回结构化诊断且本实例保持调用前状态；恢复后既有 fixed-tick 腐败检测器在下一 tick 兜底复核。
    /// </summary>
    public CookingRecipeCheckpointRestoreResult RestoreCheckpoint(CookingRecipeCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (_lifecycleClosed)
            return CookingRecipeCheckpointRestoreResult.Reject(CookingCheckpointRestoreReason.LifecycleClosed);
        if (!IsGameplayMutationOpen)
            return CookingRecipeCheckpointRestoreResult.Reject(CookingCheckpointRestoreReason.LifecycleClosed);

        var validation = ValidateCheckpoint(checkpoint);
        if (validation != CookingCheckpointRestoreReason.None)
            return CookingRecipeCheckpointRestoreResult.Reject(validation);

        _mutationInProgress = true;
        try
        {
            InstallCheckpointState(checkpoint);
        }
        finally
        {
            _mutationInProgress = false;
        }

        return new CookingRecipeCheckpointRestoreResult(true, CookingCheckpointRestoreReason.None, checkpoint);
    }

    /// <summary>
    /// 成功交接载荷：保留厨房现场与三个 ID 计数器，清除本关订单、结算、去重账本、
    /// 命令/tick 事件，并把逻辑 Tick、事件序号、状态版本和 Level 绑定归零。
    /// 这不是同代际恢复；<see cref="ExportCheckpoint"/> 仍整册导出。
    /// </summary>
    public CookingRecipeCheckpoint ExportSuccessHandoff()
    {
        var checkpoint = ExportCheckpoint();
        return checkpoint with
        {
            StateVersion = 0,
            LogicalTick = 0,
            Orders = Array.Empty<CookingRecipeCheckpointOrder>(),
            Settlements = Array.Empty<CookingOrderSettlement>(),
            NextSettlementSequence = 0,
            Deduplication = Array.Empty<CookingRecipeCheckpointDeduplication>(),
            Events = Array.Empty<CookingRecipeEvent>(),
            EventSequence = 0,
            TickEvents = Array.Empty<CookingRecipeTickEvent>(),
            LevelScope = null,
        };
    }

    /// <summary>
    /// 把一份成功交接载荷整册换入。形状必须已经是交接裁剪（本关上下文为空、水位归零、未绑定 Level）；
    /// 其它形状结构化拒绝且零变更。接受后走 <see cref="RestoreCheckpoint"/> 的外键校验。
    /// </summary>
    public CookingRecipeCheckpointRestoreResult AcceptSuccessHandoff(CookingRecipeCheckpoint handoff)
    {
        ArgumentNullException.ThrowIfNull(handoff);
        if (!IsSuccessHandoffShape(handoff))
            return CookingRecipeCheckpointRestoreResult.Reject(CookingCheckpointRestoreReason.NotSuccessHandoff);
        if (handoff.NextProcessId < _nextProcessId || handoff.NextProductId < _nextProductId ||
            handoff.NextSettlementSequence < _nextSettlementSequence)
        {
            return CookingRecipeCheckpointRestoreResult.Reject(CookingCheckpointRestoreReason.CounterInvalid);
        }

        // 源代际 CompleteEnd 会关掉 gameplay mutation。成功交接是这次关闭之后唯一允许的换入，
        // 而且只在厨房还没有绑到下一代时发生一次。换入后重新打开，交给下一代 Start 绑定。
        var closedForHandoff = _lifecycleClosed;
        var detachedGate = _lifecycleGate;
        if (closedForHandoff)
        {
            _lifecycleClosed = false;
            _lifecycleGate = null;
        }
        var restored = RestoreCheckpoint(handoff);
        if (!restored.Accepted && closedForHandoff)
        {
            _lifecycleClosed = true;
            _lifecycleGate = detachedGate;
        }
        else if (restored.Accepted)
        {
            _lifecycleClosed = false;
            _lifecycleGate = null;
            _levelScope = null;
            _isClosing = false;
            _isCompleted = false;
        }
        return restored;
    }

    internal void RestoreFrontOfHouseState(bool isClosing, bool isCompleted, long stateVersion)
    {
        _isClosing = isClosing;
        _isCompleted = isCompleted;
        _stateVersion = stateVersion;
    }

    /// <summary>
    /// 交接换入失败后的内部装回。不走对外的生命周期开关：调用前的关闭状态保持不变。
    /// </summary>
    internal CookingCheckpointRestoreReason RestoreExportedCheckpoint(CookingRecipeCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        var validation = ValidateCheckpoint(checkpoint);
        if (validation != CookingCheckpointRestoreReason.None)
            return validation;
        InstallCheckpointState(checkpoint);
        return CookingCheckpointRestoreReason.None;
    }

    private static bool IsSuccessHandoffShape(CookingRecipeCheckpoint handoff) =>
        handoff.StateVersion == 0 &&
        handoff.LogicalTick == 0 &&
        handoff.EventSequence == 0 &&
        handoff.LevelScope is null &&
        handoff.Orders.Count == 0 &&
        handoff.Settlements.Count == 0 &&
        handoff.Deduplication.Count == 0 &&
        handoff.Events.Count == 0 &&
        handoff.TickEvents.Count == 0;

    private CookingCheckpointRestoreReason ValidateCheckpoint(CookingRecipeCheckpoint checkpoint)
    {
        if (!Equals(checkpoint.Scope, _fixture.Scope))
            return CookingCheckpointRestoreReason.ScopeMismatch;

        var items = new Dictionary<ItemId, CookingRecipeCheckpointItem>();
        foreach (var item in checkpoint.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Id.Value) || !items.TryAdd(item.Id, item))
                return CookingCheckpointRestoreReason.DuplicateItemIdentity;
            if (item.Version <= 0 || !_fixture.Items.ContainsKey(item.Definition))
                return CookingCheckpointRestoreReason.ItemDefinitionNotFound;
            if (item.Location.Kind == LocationKind.PlayerHand)
            {
                if (item.Location.OwnerId is not { } handOwner || !_fixture.Players.ContainsKey(new PlayerId(handOwner)))
                    return CookingCheckpointRestoreReason.HandOwnerNotPlayer;
            }
        }

        foreach (var group in GroupByHandOccupancy(checkpoint.Items))
        {
            if (group.Count() > 1)
                return CookingCheckpointRestoreReason.HandOccupiedTwice;
        }

        var containerIdentities = new HashSet<ItemId>();
        foreach (var container in checkpoint.Containers)
        {
            if (!containerIdentities.Add(container.Id))
                return CookingCheckpointRestoreReason.DuplicateContainerEntry;
        }

        foreach (var container in checkpoint.Containers)
        {
            foreach (var contentId in container.ItemIds)
            {
                if (!items.TryGetValue(contentId, out var content) || content.Removed)
                    return CookingCheckpointRestoreReason.ContainerContentUnavailable;
                if (content.Location is not { Kind: LocationKind.ContainerSlot, OwnerId: { } owner } ||
                    owner != container.Id.Value)
                {
                    return CookingCheckpointRestoreReason.ContainerContentForeignSlot;
                }
            }
        }

        var processes = new Dictionary<ProcessId, CookingRecipeCheckpointProcess>();
        foreach (var process in checkpoint.Processes)
        {
            if (string.IsNullOrWhiteSpace(process.Id.Value) || !processes.TryAdd(process.Id, process))
                return CookingCheckpointRestoreReason.DuplicateProcessIdentity;
            if (!_fixture.Recipes.TryGetValue(process.Recipe, out var recipe))
                return CookingCheckpointRestoreReason.ProcessRecipeNotFound;
            if (recipe.RequiredTicks != process.RequiredTicks)
                return CookingCheckpointRestoreReason.ProcessTicksMismatch;
            if (recipe.Completion != process.Completion)
                return CookingCheckpointRestoreReason.ProcessCompletionMismatch;
            if (recipe.RequiresStation != (process.Station is not null))
                return CookingCheckpointRestoreReason.ProcessStationBindingMismatch;
            if (process.Station is { } station && !_fixture.Appliances.ContainsKey(station))
                return CookingCheckpointRestoreReason.ProcessStationNotFound;
            if (process.ElapsedTicks < 0 || process.ElapsedTicks >= process.RequiredTicks)
                return CookingCheckpointRestoreReason.ProcessProgressInvalid;

            if (!items.TryGetValue(process.Anchor, out var anchor) || anchor.Removed)
                return CookingCheckpointRestoreReason.ProcessAnchorUnavailable;
            if (process.Container is { } processContainer &&
                (!items.TryGetValue(processContainer, out var containerState) || containerState.Removed))
            {
                return CookingCheckpointRestoreReason.ProcessContainerUnavailable;
            }

            var locked = new HashSet<ItemId>();
            foreach (var input in process.LockedInputs)
            {
                if (!locked.Add(input))
                    return CookingCheckpointRestoreReason.ProcessInputUnavailable;
                if (!items.TryGetValue(input, out var inputState) || inputState.Removed)
                    return CookingCheckpointRestoreReason.ProcessInputUnavailable;
                if (input != process.Anchor && !recipe.Inputs.Contains(inputState.Definition))
                    return CookingCheckpointRestoreReason.ProcessInputNotInRecipe;

                if (process.Container is { } anchorContainer)
                {
                    if (input != process.Anchor &&
                        (inputState.Location is not { Kind: LocationKind.ContainerSlot, OwnerId: { } inputOwner } ||
                         inputOwner != anchorContainer.Value))
                    {
                        return CookingCheckpointRestoreReason.ProcessInputLeftContainer;
                    }
                }
                else if (inputState.Location.Kind is not (LocationKind.WorldPosition or LocationKind.StationSlot
                    or LocationKind.ContainerSlot))
                {
                    return CookingCheckpointRestoreReason.ProcessInputLocationUnsupported;
                }
            }

            if (!locked.Contains(process.Anchor))
                return CookingCheckpointRestoreReason.ProcessInputUnavailable;
        }

        var orders = new HashSet<OrderId>();
        foreach (var order in checkpoint.Orders)
        {
            if (!orders.Add(order.Id))
                return CookingCheckpointRestoreReason.DuplicateOrderIdentity;
            if (!_fixture.OrderTemplates.TryGetValue(order.Template, out var template))
                return CookingCheckpointRestoreReason.OrderTemplateNotFound;
            if (template.RequiredRecipe != order.RequiredRecipe ||
                template.RequiredContainerDefinition != order.RequiredContainerDefinition)
            {
                return CookingCheckpointRestoreReason.OrderRequirementMismatch;
            }

            if (!_fixture.Items.TryGetValue(order.RequiredContainerDefinition, out var requiredContainer) ||
                requiredContainer.Container is null)
            {
                return CookingCheckpointRestoreReason.OrderRequirementMismatch;
            }
        }

        for (var index = 0; index < checkpoint.Settlements.Count; index++)
        {
            var settlement = checkpoint.Settlements[index];
            if (settlement.Sequence != index + 1)
                return CookingCheckpointRestoreReason.SettlementSequenceInvalid;
        }

        if (checkpoint.NextSettlementSequence != checkpoint.Settlements.Count)
            return CookingCheckpointRestoreReason.SettlementSequenceInvalid;

        foreach (var product in checkpoint.ConsumedProducts)
        {
            if (!items.ContainsKey(product))
                return CookingCheckpointRestoreReason.ConsumedProductNotRegistered;
        }

        foreach (var pool in checkpoint.CleanContainerCounts)
        {
            if (!_fixture.CleanContainerSupply.TryGetValue(pool.Definition, out var supply))
                return CookingCheckpointRestoreReason.CleanPoolUnknownDefinition;
            if (pool.Count < 0 || pool.Count > supply)
                return CookingCheckpointRestoreReason.CleanPoolOutOfRange;
        }

        var deduplication = new HashSet<(SessionId, PlayerId, RecipeCommandId)>();
        foreach (var entry in checkpoint.Deduplication)
        {
            if (string.IsNullOrWhiteSpace(entry.Fingerprint) ||
                !deduplication.Add((entry.Session, entry.Player, entry.Command)))
            {
                return CookingCheckpointRestoreReason.DeduplicationEntryInvalid;
            }
        }

        if (checkpoint.StateVersion < 0 || checkpoint.LogicalTick < 0 ||
            checkpoint.NextProcessId < 0 || checkpoint.NextProductId < 0)
        {
            return CookingCheckpointRestoreReason.CounterInvalid;
        }

        if (ValidateEventHistory(checkpoint) is { } eventReason)
            return eventReason;

        return CookingCheckpointRestoreReason.None;
    }

    /// <summary>
    /// 命令事件与 tick 事件共用一条序号：二者序号互不重复、各自严格递增，
    /// 且最大序号不超过 <see cref="CookingRecipeCheckpoint.EventSequence"/>。
    /// tick 历史还要和逻辑 tick、宿主帧首尾相接——错位的历史恢复后，下一帧会从错误水位继续。
    /// </summary>
    private static CookingCheckpointRestoreReason? ValidateEventHistory(CookingRecipeCheckpoint checkpoint)
    {
        if (checkpoint.EventSequence < 0)
            return CookingCheckpointRestoreReason.EventSequenceInvalid;

        var seen = new HashSet<long>();
        long previousCommand = 0;
        foreach (var entry in checkpoint.Events)
        {
            if (entry.Sequence <= 0 || entry.Sequence <= previousCommand || !seen.Add(entry.Sequence))
                return CookingCheckpointRestoreReason.EventSequenceInvalid;
            previousCommand = entry.Sequence;
        }

        long previousTickSequence = 0;
        long previousLogicalTick = 0;
        long previousFrame = 0;
        for (var index = 0; index < checkpoint.TickEvents.Count; index++)
        {
            var tick = checkpoint.TickEvents[index];
            if (tick.Sequence <= 0 || tick.Sequence <= previousTickSequence || !seen.Add(tick.Sequence))
                return CookingCheckpointRestoreReason.EventSequenceInvalid;
            if (!Equals(tick.LevelScope.MatchScope, checkpoint.Scope))
                return CookingCheckpointRestoreReason.EventSequenceInvalid;
            if (tick.HostFrameSequence <= 0 || tick.AfterLogicalTick != tick.BeforeLogicalTick + 1)
                return CookingCheckpointRestoreReason.EventSequenceInvalid;
            if (index == 0)
            {
                if (tick.BeforeLogicalTick != 0)
                    return CookingCheckpointRestoreReason.EventSequenceInvalid;
            }
            else if (tick.BeforeLogicalTick != previousLogicalTick || tick.HostFrameSequence != previousFrame + 1)
            {
                return CookingCheckpointRestoreReason.EventSequenceInvalid;
            }

            previousTickSequence = tick.Sequence;
            previousLogicalTick = tick.AfterLogicalTick;
            previousFrame = tick.HostFrameSequence;
        }

        if (checkpoint.TickEvents.Count > 0 &&
            checkpoint.TickEvents[checkpoint.TickEvents.Count - 1].AfterLogicalTick != checkpoint.LogicalTick)
        {
            return CookingCheckpointRestoreReason.EventSequenceInvalid;
        }

        if (checkpoint.LevelScope is { } bound)
        {
            if (!Equals(bound.MatchScope, checkpoint.Scope))
                return CookingCheckpointRestoreReason.ScopeMismatch;
            if (checkpoint.TickEvents.Any(tick => !Equals(tick.LevelScope, bound)))
                return CookingCheckpointRestoreReason.EventSequenceInvalid;
        }
        else if (checkpoint.TickEvents.Count > 0 || checkpoint.LogicalTick != 0)
        {
            return CookingCheckpointRestoreReason.EventSequenceInvalid;
        }

        if (seen.Count > 0 && seen.Max() > checkpoint.EventSequence)
            return CookingCheckpointRestoreReason.EventSequenceInvalid;

        return null;
    }

    private static IEnumerable<IGrouping<string, CookingRecipeCheckpointItem>> GroupByHandOccupancy(
        IReadOnlyList<CookingRecipeCheckpointItem> items) =>
        items
            .Where(item => item.Location.Kind == LocationKind.PlayerHand && item.Location.OwnerId is { } owner)
            .GroupBy(item => item.Location.OwnerId!, StringComparer.Ordinal);

    private void InstallCheckpointState(CookingRecipeCheckpoint checkpoint)
    {
        var hands = new Dictionary<PlayerId, ItemId?>();
        foreach (var player in _fixture.Players.Keys)
            hands.Add(player, null);
        foreach (var item in checkpoint.Items)
        {
            if (item.Location.Kind == LocationKind.PlayerHand && item.Location.OwnerId is { } owner)
                hands[new PlayerId(owner)] = item.Id;
        }

        var processesByStation = new Dictionary<StationSlotId, ProcessState>();
        var stationsByProcess = new Dictionary<ProcessId, StationSlotId>();
        var processesByAnchorItem = new Dictionary<ItemId, ProcessState>();
        var anchorItemsByProcess = new Dictionary<ProcessId, ItemId>();
        var inputsByProcessItem = new Dictionary<ItemId, ProcessId>();
        var lockedInputsByProcess = new Dictionary<ProcessId, IReadOnlyList<ItemId>>();
        foreach (var process in checkpoint.Processes)
        {
            var state = new ProcessState(process.Id, process.Recipe, process.Player, process.Anchor, process.Station,
                process.ElapsedTicks, process.RequiredTicks, process.Completion, process.Container,
                process.LockedInputs.ToArray());
            if (process.Station is { } station)
            {
                processesByStation.Add(station, state);
                stationsByProcess.Add(process.Id, station);
            }
            else
            {
                processesByAnchorItem.Add(process.Anchor, state);
                anchorItemsByProcess.Add(process.Id, process.Anchor);
            }

            lockedInputsByProcess.Add(process.Id, state.LockedInputs);
            foreach (var input in state.LockedInputs)
                inputsByProcessItem[input] = process.Id;
        }

        _items = checkpoint.Items.ToDictionary(item => item.Id, item => new ItemState(item.Definition, item.Version,
            item.Location, item.Removed, item.Recipe, item.IsProduct, item.OriginStation,
            item.ContainerCompleted, item.IsDirty));
        _hands.Clear();
        foreach (var (player, held) in hands)
            _hands[player] = held;
        _processesByStation = processesByStation;
        _stationsByProcess = stationsByProcess;
        _processesByAnchorItem = processesByAnchorItem;
        _anchorItemsByProcess = anchorItemsByProcess;
        _inputsByProcessItem.Clear();
        foreach (var (input, owner) in inputsByProcessItem)
            _inputsByProcessItem[input] = owner;
        _lockedInputsByProcess.Clear();
        foreach (var (process, inputs) in lockedInputsByProcess)
            _lockedInputsByProcess[process] = inputs;
        _containerItems = checkpoint.Containers.ToDictionary(
            container => container.Id,
            container => new List<ItemId>(container.ItemIds));
        _consumedProducts.Clear();
        foreach (var product in checkpoint.ConsumedProducts)
            _consumedProducts.Add(product);
        _orders.Clear();
        foreach (var order in checkpoint.Orders)
        {
            _orders[order.Id] = new OrderState(order.Id, order.Template, order.RequiredRecipe,
                order.RequiredContainerDefinition, order.Status, order.CompletedAtLogicalTick);
        }

        _settlements.Clear();
        _settlements.AddRange(checkpoint.Settlements);
        _processedCommands.Clear();
        foreach (var entry in checkpoint.Deduplication)
        {
            _processedCommands[new RecipeCommandKey(entry.Session, entry.Player, entry.Command)] =
                new ProcessedCommand(entry.Fingerprint, new CookingRecipeCommandResult(
                    entry.Outcome, entry.Reason, entry.StateVersion, false, Array.Empty<CookingRecipeEvent>()));
        }

        _events.Clear();
        _events.AddRange(checkpoint.Events);
        _tickEvents.Clear();
        _tickEvents.AddRange(checkpoint.TickEvents);
        _cleanContainerCount.Clear();
        foreach (var pool in checkpoint.CleanContainerCounts)
            _cleanContainerCount[pool.Definition] = pool.Count;

        _stateVersion = checkpoint.StateVersion;
        LogicalTick = checkpoint.LogicalTick;
        _levelScope = checkpoint.LevelScope;
        _eventSequence = checkpoint.EventSequence;
        _nextProcessId = checkpoint.NextProcessId;
        _nextProductId = checkpoint.NextProductId;
        _nextSettlementSequence = checkpoint.NextSettlementSequence;
    }
}
