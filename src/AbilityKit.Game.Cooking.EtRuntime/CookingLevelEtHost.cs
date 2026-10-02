using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using ArgumentNullException = System.ArgumentNullException;
using global::ET;

namespace AbilityKit.Game.Cooking.EtRuntime;

public enum CookingLevelAdmissionReason
{
    None,
    LevelNotRunning,
    LevelPaused,
    ScopeMismatch,
    MalformedCommand,
    ReservedClockOperation,
    BatchStale,
    QueueFull,
    CommandIdentityConflict,
    CommandTerminal,
}

public enum CookingLevelDispositionKind
{
    Executed,
    Duplicate,
    Conflicted,
    Cancelled,
    Stale,
}

public enum CookingLevelFrameReason
{
    None,
    LevelNotRunning,
    LevelPaused,
}

public sealed record CookingLevelCommandEnvelope(
    CookingLevelScope LevelScope,
    CookingRecipeCommand Command,
    string SourceConnectionId,
    string CorrelationId);

public readonly record struct CookingLevelCommandGroupKey(
    CookingLevelScope LevelScope,
    PlayerId Player,
    RecipeCommandId Command);

public readonly record struct CookingCommandFingerprint(string Value)
{
    public static CookingCommandFingerprint Create(CookingLevelCommandEnvelope envelope) =>
        new(Convert.ToHexString(SHA256.HashData(CanonicalBytes(envelope))));

    public static byte[] CanonicalBytes(CookingLevelCommandEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(envelope.LevelScope);
        ArgumentNullException.ThrowIfNull(envelope.Command);

        using var stream = new MemoryStream();
        var writer = new CanonicalWriter(stream);
        var scope = envelope.LevelScope;
        var command = envelope.Command;

        writer.String(scope.MatchScope.Session.Value);
        writer.String(scope.MatchScope.World.Value);
        writer.String(scope.MatchScope.Match.Value);
        writer.Int64(scope.RestaurantRuntime.Value);
        writer.String(scope.Level.Value);
        writer.Int64(scope.LevelEpoch);
        writer.Int64(command.SimulationBatch);
        writer.String(command.Player.Value);
        writer.String(command.Command.Value);
        writer.Int32((int)command.Operation);
        writer.NullableString(command.Recipe?.Value);
        writer.NullableString(command.Process?.Value);
        writer.NullableString(command.Item?.Value);
        writer.NullableString(command.Station?.Value);
        writer.NullableString(command.Container?.Value);
        writer.NullableString(command.Order?.Value);
        writer.Int32(command.ExpectedItemVersion);
        writer.Int32(command.TickCount);
        writer.Int32(command.MoveX);
        writer.Int32(command.MoveY);
        writer.Int32(command.FacingX);
        writer.Int32(command.FacingY);
        writer.NullableString(command.WorldAnchor);
        // Absent extension preserves every legacy golden byte; SUPP marks presence and version.
        if (command.SupplierId is not null || command.DeliveryId is not null || command.SupplyRequestId is not null)
        {
            writer.Int32(0x53555050); writer.Int32(1);
            writer.NullableString(command.SupplierId);
            writer.NullableString(command.DeliveryId);
            writer.NullableString(command.SupplyRequestId);
        }
        return stream.ToArray();
    }

    private sealed class CanonicalWriter(Stream stream)
    {
        private readonly byte[] _buffer = new byte[8];

        public void Int32(int value)
        {
            BinaryPrimitives.WriteInt32BigEndian(_buffer, value);
            stream.Write(_buffer, 0, 4);
        }

        public void Int64(long value)
        {
            BinaryPrimitives.WriteInt64BigEndian(_buffer, value);
            stream.Write(_buffer, 0, 8);
        }

        public void String(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            var bytes = Encoding.UTF8.GetBytes(value);
            Int32(bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }

        public void NullableString(string? value)
        {
            if (value is null)
            {
                stream.WriteByte(0);
                return;
            }

            stream.WriteByte(1);
            String(value);
        }
    }
}

public sealed record CookingLevelAdmissionResult(
    bool Accepted,
    CookingLevelAdmissionReason Reason,
    CookingLevelPendingDisposition? TerminalDisposition = null)
{
    public static CookingLevelAdmissionResult Accept() => new(true, CookingLevelAdmissionReason.None);
    public static CookingLevelAdmissionResult Reject(
        CookingLevelAdmissionReason reason,
        CookingLevelPendingDisposition? disposition = null) => new(false, reason, disposition);
}

public sealed record CookingLevelPendingDisposition(
    CookingLevelCommandEnvelope Envelope,
    CookingCommandFingerprint Fingerprint,
    CookingLevelDispositionKind Kind,
    CookingRecipeCommandResult? Result,
    string Reason,
    string? RepresentativeConnectionId = null,
    string? RepresentativeCorrelationId = null);

public sealed record CookingLevelFrameResult(
    bool Accepted,
    CookingLevelFrameReason Reason,
    CookingLevelScope LevelScope,
    long HostFrameSequence,
    long? SimulationBatch,
    CookingRecipeTickResult? Tick,
    IReadOnlyList<CookingLevelPendingDisposition> Dispositions)
{
    public static CookingLevelFrameResult Reject(
        CookingLevelFrameReason reason,
        CookingLevelScope scope,
        long hostFrameSequence) =>
        new(false, reason, scope, hostFrameSequence, null, null, Array.Empty<CookingLevelPendingDisposition>());
}

public sealed record CookingLevelHostOperationResult(
    bool Accepted,
    string Reason,
    string State,
    long Version,
    IReadOnlyList<CookingLevelPendingDisposition> Dispositions);

    public sealed record CookingLevelHostGenerationResult(
    bool Accepted,
    string Reason,
    CookingLevelScope SourceScope,
    CookingLevelOutcome? SourceOutcome,
    long SourceVersionBefore,
    long SourceVersionAfter,
    bool SourceGameplayClosed,
    CookingLevelScope? NewScope,
    CookingLevelLifecycle? Lifecycle,
    IReadOnlyList<CookingLevelPendingDisposition> Dispositions,
    int RetainedProcessCount = 0,
    int ClearedOrderCount = 0,
    int ClearedSettlementCount = 0);

public enum CookingLevelCheckpointExportReason
{
    None,
    LevelNotRunning,
    LevelPaused,
    PendingCommands,
    PreparationMissing,
}

public sealed record CookingLevelCheckpointExportResult(
    bool Accepted,
    CookingLevelCheckpointExportReason Reason,
    CookingLevelCheckpoint? Checkpoint = null);

public enum CookingLevelCheckpointRestoreReason
{
    None,
    CheckpointNull,
    CheckpointScopeInvalid,
    CheckpointPayloadScopeMismatch,
    ConfigurationIdentityMismatch,
    PreparationRejected,
    LifecycleStartRejected,
    GameplayUnavailable,
    GameplayRestoreRejected,
    FrontOfHouseRestoreRejected,
    HostCreationFailed,
    TickHistoryScopeMismatch,
}

public sealed record CookingLevelCheckpointRestoreResult(
    bool Accepted,
    CookingLevelCheckpointRestoreReason Reason,
    CookingLevelEtHost? Host = null,
    CookingCheckpointRestoreReason RecipeRestoreReason = CookingCheckpointRestoreReason.None,
    string? Detail = null,
    CookingFrontOfHouseRestoreReason FrontOfHouseRestoreReason = CookingFrontOfHouseRestoreReason.None);

public enum CookingLevelEtHostFailurePoint
{
    LevelCreated,
    DriverCreated,
    BeforeLevelPublish,
    SimulationOwnershipAcquired,
    BeforeSimulationPublish,
}

public interface ICookingLevelEtHostFailureInjector
{
    void ThrowIfRequested(CookingLevelEtHostFailurePoint point);
}

public sealed record CookingLevelSimulationBinding(
    CookingLevelScope LevelScope,
    CookingScope LegacySimulationScope);

[ComponentOf(typeof(Scene))]
public sealed class CookingApplicationComponent : Entity, IAwake
{
}

[ComponentOf(typeof(CookingApplicationComponent))]
public sealed class CookingMatchRegistryComponent : Entity, IAwake
{
}

[ChildOf(typeof(CookingMatchRegistryComponent))]
public sealed class CookingMatchEntity : Entity, IAwake
{
}

[ComponentOf(typeof(CookingMatchEntity))]
public sealed class CookingMatchIdentityComponent : Entity, IAwake
{
    public CookingScope Scope { get; internal set; } = null!;
}

[ComponentOf(typeof(CookingMatchEntity))]
public sealed class CookingRestaurantRuntimeComponent : Entity, IAwake
{
    public RestaurantRuntimeId RuntimeId { get; internal set; }
}

[ComponentOf(typeof(CookingRestaurantRuntimeComponent))]
public sealed class CookingKitchenComponent : Entity, IAwake
{
}

[ComponentOf(typeof(CookingRestaurantRuntimeComponent))]
public sealed class CookingLevelComponent : Entity, IAwake
{
    public CookingLevelScope Scope { get; internal set; } = null!;
    public CookingLevelLifecycle Lifecycle { get; internal set; } = null!;
}

[ComponentOf(typeof(CookingLevelComponent))]
public sealed class CookingLevelDriverComponent : Entity, IAwake, IUpdate
{
    internal CookingLevelEtHost Host { get; set; } = null!;
    internal CookingRecipeSimulation? Simulation { get; set; }
}

[EntitySystem]
internal sealed class CookingLevelDriverUpdateSystem : UpdateSystem<CookingLevelDriverComponent>
{
    protected override void Update(CookingLevelDriverComponent self) => self.Host.ExecuteEtUpdate(self);
}

public sealed class CookingLevelEtHost : IDisposable
{
    private const int SceneId = 1;
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private readonly EtRuntimeHost _runtime;
    private readonly int _queueCapacity;
    private readonly ICookingLevelEtHostFailureInjector? _failureInjector;
    private readonly ICookingLevelGameplayPublicationGuard _gameplayPublicationGuard;
    private readonly ICookingRecipeLifecycleGate _hostGameplayGate;
    private readonly ICookingRecipeAuthorityGate _authorityGate;
    private readonly ICookingLevelOperationGate _lifecycleOperationGate;

    private readonly Dictionary<CookingLevelCommandGroupKey, PendingGroup> _pending = new();
    private readonly Dictionary<CookingLevelCommandGroupKey, TerminalCommand> _terminal = new();
    private readonly List<CookingLevelPendingDisposition> _history = new();
    private CookingLevelLifecycle _lifecycle;
    private CookingRecipeSimulation? _ownedSimulation;
    private FrozenBatch? _inFlight;
    private CookingLevelFrameResult? _completedFrame;
    private Exception? _tickFailure;
    private IReadOnlyList<CookingLevelPendingDisposition> _finalHistory = Array.Empty<CookingLevelPendingDisposition>();
    private bool _disposed;
    private bool _ticking;
    private bool _executingAuthorityMutation;
    private bool _executingLifecycleOperation;
    private bool _executingFrontOperation;
    private bool _executingPreparationMutation;
    private bool _initializingPreparation;
    private CookingPreparationConfiguration? _preparationConfiguration;
    private CookingLevelPreparation? _stagedPreparation;
    private long _serviceStartLogicalTick;
    private readonly CookingFrontOfHouseConfiguration? _frontConfiguration;
    private CookingFrontOfHouseConfiguration? _effectiveFrontConfiguration;
    private CookingInstalledLayoutCheckpoint? _installedLayout;
    private readonly string? _frontConfigurationIdentity;

    public CookingLevelEtHost(
        CookingLevelLifecycle lifecycle,
        int queueCapacity = 256,
        ICookingLevelEtHostFailureInjector? failureInjector = null)
    {
        _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        _frontConfiguration = (lifecycle.GameplayFactory as ICookingFrontOfHouseGameplayFactory)?.FrontOfHouseConfiguration.Freeze();
        _frontConfigurationIdentity = _frontConfiguration?.Identity();
        _effectiveFrontConfiguration = _frontConfiguration;
        if (queueCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(queueCapacity));
        _queueCapacity = queueCapacity;
        _failureInjector = failureInjector;
        _gameplayPublicationGuard = new GameplayPublicationGuard(this);
        _hostGameplayGate = new HostGameplayGate(this);
        _authorityGate = new HostAuthorityGate(this);
        _lifecycleOperationGate = new HostLifecycleOperationGate(this);

        var scope = lifecycle.Scope;
        Binding = new CookingLevelSimulationBinding(scope, scope.MatchScope);
        _runtime = new EtRuntimeHost(typeof(CookingLevelEtHost).Assembly);
        try
        {
            Scene = _runtime.CreateScene(SceneId, "cooking-level");
            _runtime.Run(SceneId, scene =>
            {
                Application = scene.AddComponent<CookingApplicationComponent>();
                MatchRegistry = Application.AddComponent<CookingMatchRegistryComponent>();
                Match = MatchRegistry.AddChild<CookingMatchEntity>();
                MatchIdentity = Match.AddComponent<CookingMatchIdentityComponent>();
                MatchIdentity.Scope = scope.MatchScope;
                RestaurantRuntime = Match.AddComponent<CookingRestaurantRuntimeComponent>();
                RestaurantRuntime.RuntimeId = scope.RestaurantRuntime;
                Kitchen = RestaurantRuntime.AddComponent<CookingKitchenComponent>();
                InstallLevel(lifecycle);
            });
            lifecycle.BindOwnerGameplayGate(_hostGameplayGate);
            lifecycle.BindOperationGate(_lifecycleOperationGate);
        }
        catch
        {
            _runtime.Dispose();
            throw;
        }
    }

    public Scene Scene { get; }
    public CookingApplicationComponent Application { get; private set; } = null!;
    public CookingMatchRegistryComponent MatchRegistry { get; private set; } = null!;
    public CookingMatchEntity Match { get; private set; } = null!;
    public CookingMatchIdentityComponent MatchIdentity { get; private set; } = null!;
    public CookingRestaurantRuntimeComponent RestaurantRuntime { get; private set; } = null!;
    public CookingKitchenComponent Kitchen { get; private set; } = null!;
    public CookingLevelComponent Level { get; private set; } = null!;
    public CookingLevelDriverComponent Driver { get; private set; } = null!;
    public CookingLevelSimulationBinding Binding { get; private set; }
    public CookingLevelLifecycle Lifecycle => _lifecycle;
    public CookingFrontOfHouseSnapshot? FrontOfHouseSnapshot => _frontOfHouse?.Snapshot();

    /// <summary>Reads the committed owner boundary without advancing simulation or lifecycle.</summary>
    public CookingLevelObservation Observe()
    {
        Check();
        var kitchen = _ownedSimulation;
        return CookingLevelObservationProjector.Project(Binding.LevelScope, _lifecycle.Snapshot(), HostFrameSequence,
            kitchen?.Snapshot(), _frontOfHouse?.Snapshot(), _installedLayout?.Layout, _preparationConfiguration?.Identity(),
            kitchen?.SpatialConfiguration is { } geometry ? CookingFrontOfHouseFlow.SpatialIdentity(geometry) : null,
            kitchen?.ConfiguredPlayers);
    }

    /// <summary>
    /// 准备态只读观察。成功交接和失败重开在 <c>Created</c> 就已经挂上厨房，
    /// 此时 <see cref="CookingLevelLifecycle.TryGetGameplay"/> 仍关闭。
    /// </summary>
    public bool TryPeekBoundKitchen(out CookingRecipeSimulation? kitchen) =>
        _lifecycle.TryPeekBoundKitchen(out kitchen);

    private CookingFrontOfHouse? _frontOfHouse;
    private CookingFrontOfHouseMenu _frontOfHouseMenu = CookingFrontOfHouseMenu.Single(new("unused"));

    /// <summary>运行帧结束后推进前厅。暂停帧不会调用 Tick 的成功路径。模板必须已在厨房内容里。</summary>
    public void UseFrontOfHouse(CookingFrontOfHouse house, OrderTemplateId template)
        => UseFrontOfHouse(house, CookingFrontOfHouseMenu.Single(template));

    public void UseFrontOfHouse(CookingFrontOfHouse house, CookingFrontOfHouseMenu menu)
    {
        Check();
        ArgumentNullException.ThrowIfNull(house);
        ArgumentNullException.ThrowIfNull(menu);
        if (_frontOfHouse is not null && _lifecycle.State != CookingLevelState.Created)
            throw new InvalidOperationException("An active front-of-house owner cannot be replaced.");
        ValidateFrontConfiguration(house, menu, _ownedSimulation);
        _frontOfHouse = house;
        _frontOfHouseMenu = menu;
        if (_ownedSimulation is not null) BindFrontOfHouse(_ownedSimulation);
    }

    private T RunFrontOperation<T>(Func<T> operation)
    {
        if (_executingFrontOperation) throw new InvalidOperationException("Front owner operation cannot be reentered.");
        _executingFrontOperation = true;
        try { return operation(); } finally { _executingFrontOperation = false; }
    }

    private void ValidateFrontConfiguration(CookingFrontOfHouse house, CookingFrontOfHouseMenu menu, CookingRecipeSimulation? kitchen,
        CookingFrontOfHouseConfiguration? candidateConfiguration = null, CookingSpatialConfiguration? candidateSpatial = null)
    {
        var snapshot = house.Snapshot();
        var front = candidateConfiguration ?? _effectiveFrontConfiguration;
        if (front is null)
        {
            if (snapshot.Flow is not null || snapshot.ManualPolicyIdentity is not null)
                throw new ArgumentException("New front gameplay requires trusted factory configuration.");
            return;
        }
        if (snapshot.Schedule != front.Schedule || !menu.Templates.SequenceEqual(front.Menu)
            || System.Text.Json.JsonSerializer.Serialize(snapshot.Flow) != System.Text.Json.JsonSerializer.Serialize(front.Flow)
            || (snapshot.ManualPolicyIdentity is not null && snapshot.ManualPolicyIdentity != front.ManualPolicyIdentity))
            throw new ArgumentException("Front gameplay differs from the trusted factory configuration.");
        if (kitchen is null) return;
        if (menu.Templates.Any(x => !kitchen.HasOrderTemplate(x))) throw new ArgumentException("Front menu references unavailable orders.");
        if (front.Flow is { } flow)
        {
            if ((candidateSpatial ?? kitchen.SpatialConfiguration) is not { } spatial || CookingFrontOfHouseFlow.SpatialIdentity(spatial) != flow.GeometryIdentity)
                throw new ArgumentException("Front paths must use the active kitchen geometry.");
            void Endpoint(string id, CookingFrontPoint point)
            {
                var x = (long)point.X * flow.CellSize + flow.CellSize / 2;
                var y = (long)point.Y * flow.CellSize + flow.CellSize / 2;
                if (!spatial.Anchors.Any(a => a.Id == id && a.X == x && a.Y == y))
                    throw new ArgumentException("Front path endpoint has no matching authoritative anchor.");
            }
            Endpoint(front.EntranceAnchor, flow.EntranceToQueue[0]);
            Endpoint(front.QueueAnchor, flow.EntranceToQueue[^1]);
            Endpoint(front.ExitAnchor, flow.QueueToExit[^1]);
            foreach (var table in flow.Tables) Endpoint(table.TableId, table.QueueToTable[^1]);
        }
        if (front.DeliveryPolicy is { } delivery)
        {
            var spatial = candidateSpatial ?? kitchen.SpatialConfiguration ?? throw new ArgumentException("Service delivery requires kitchen geometry.");
            var required = delivery.Mode == CookingFrontDeliveryMode.ServingAnchor
                ? new[] { delivery.ServingAnchor! }
                : Enumerable.Range(1, snapshot.Schedule.TableCount).Select(x => $"table-{x}");
            if (required.Any(id => spatial.Anchors.Count(a => a.Id == id) != 1))
                throw new ArgumentException("A unique authoritative service anchor is required.");
        }
        if (front.ManualPolicyIdentity is not null)
        {
            var spatial = candidateSpatial ?? kitchen.SpatialConfiguration ?? throw new ArgumentException("Manual front work requires kitchen geometry.");
            var required = Enumerable.Range(1, snapshot.Schedule.TableCount).Select(x => $"table-{x}").Append(front.WashingAnchor);
            if (required.Any(id => !spatial.Anchors.Any(a => a.Id == id))) throw new ArgumentException("A front work anchor is missing.");
        }
    }

    private void BindFrontOfHouse(CookingRecipeSimulation kitchen, bool prepared = false)
    {
        if (_frontOfHouse is not { } house) return;
        var snapshot = house.Snapshot();
        if (!prepared) ValidateFrontConfiguration(house, _frontOfHouseMenu, kitchen);
        if (!prepared && _effectiveFrontConfiguration?.ManualPolicyIdentity is { } policy)
            RunFrontOperation(() => { house.ConfigureManualWork(policy, (player, target) => CanFrontWork(kitchen, player, target)); return true; });
        kitchen.BindFrontDeliveryAuthority(_effectiveFrontConfiguration?.DeliveryPolicy is { } delivery
            ? (player, order) => ValidateFrontDelivery(kitchen, house, delivery, player, order)
            : null);
        house.BindAuthorityGate(new FrontAuthorityGate(this));
        kitchen.BindFrontWorkAuthority(command => command.Operation switch
        {
            CookingRecipeOperation.ClaimFrontWork => house.ClaimFrontWork(kitchen, command.Player, command.WorldAnchor!),
            CookingRecipeOperation.ContinueFrontWork => house.ContinueFrontWork(command.Player, command.WorldAnchor!),
            CookingRecipeOperation.StopFrontWork => house.StopFrontWork(command.Player, command.WorldAnchor!),
            _ => new(false, CookingFrontWorkRejection.WorkMissing)
        }, house.HasPlayerWork);
    }

    private static CookingRecipeRejectionReason ValidateFrontDelivery(CookingRecipeSimulation kitchen,
        CookingFrontOfHouse house, CookingFrontDeliveryPolicy policy, PlayerId player, OrderId order)
    {
        var customer = house.Snapshot().Customers.SingleOrDefault(c => c.Order == order && c.Phase == CookingTablePhase.Ordered);
        if (customer is null) return CookingRecipeRejectionReason.OrderRejected;
        var target = policy.Mode == CookingFrontDeliveryMode.ServingAnchor ? policy.ServingAnchor! : customer.TableId;
        var anchor = kitchen.SpatialConfiguration?.Anchors.SingleOrDefault(a => a.Id == target);
        return anchor is not null && kitchen.ValidateSpatialReach(player, anchor.Kind, anchor.Id)
            ? CookingRecipeRejectionReason.None : CookingRecipeRejectionReason.TargetOutOfRange;
    }

    private bool CanFrontWork(CookingRecipeSimulation kitchen, PlayerId player, string target)
    {
        if (!kitchen.IsPlayerAvailable(player) || kitchen.HasManualWork(player)) return false;
        var anchorId = target == "washing" ? _effectiveFrontConfiguration?.WashingAnchor ?? target : target;
        var anchor = kitchen.SpatialConfiguration?.Anchors.Where(x => x.Id == anchorId).OrderBy(x => x.Kind).FirstOrDefault();
        return anchor is not null && kitchen.ValidateSpatialReach(player, anchor.Kind, anchor.Id);
    }

    public long HostFrameSequence { get; private set; }
    public long LastCommittedSimulationBatch { get; private set; }
    public int PendingCommandIdentityCount => _pending.Count;
    public bool IsFaulted => _tickFailure is not null;
    public IReadOnlyList<CookingLevelPendingDisposition> DispositionHistory =>
        new ReadOnlyCollection<CookingLevelPendingDisposition>(_history.ToArray());
    public IReadOnlyList<CookingLevelPendingDisposition> FinalDispositionHistory => _finalHistory;

    public CookingLevelHostOperationResult Prepare(CookingLevelPreparation preparation)
    {
        var begun = BeginPreparation(preparation);
        return begun.Accepted ? CompletePreparation() : begun;
    }

    public CookingLevelHostOperationResult BeginPreparation(CookingLevelPreparation preparation)
        => BeginPreparationCore(preparation, null, null);

    private CookingLevelHostOperationResult BeginPreparationCore(CookingLevelPreparation preparation,
        CookingInstalledLayoutCheckpoint? restoredLayout, CookingRecipeCheckpoint? restoreReferences)
    {
        Check(); ArgumentNullException.ThrowIfNull(preparation);
        if (_lifecycle.State != CookingLevelState.Created)
            return Operation(new(false, CookingLevelLifecycleReason.InvalidState, _lifecycle.State, _lifecycle.Outcome, _lifecycle.Version, Array.Empty<CookingLevelLifecycleEvent>()));
        var validation = _lifecycle.ValidatePreparationCandidate(preparation);
        if (validation != CookingLevelLifecycleReason.None)
            return Operation(new(false, validation, _lifecycle.State, _lifecycle.Outcome, _lifecycle.Version, Array.Empty<CookingLevelLifecycleEvent>()));
        if (_lifecycle.GameplayFactory is ICookingPreparationGameplayFactory factory)
        {
            _initializingPreparation = true;
            try
            {
                var configuration = _stagedPreparation is not null && _preparationConfiguration is not null
                    ? _preparationConfiguration : factory.CreatePreparationConfiguration(Binding.LevelScope, _lifecycle.Configuration).Freeze();
                var initialized = RunLifecycleOperation(() => _lifecycle.InitializePreparationKitchen(_gameplayPublicationGuard));
                if (!initialized.Accepted) return Operation(initialized);
                if (!_lifecycle.TryPeekBoundKitchen(out var simulation) || simulation is null)
                    throw Fault(new InvalidOperationException("Preparation kitchen was not published."));
                _preparationConfiguration = configuration;
                simulation.BindAuthorityGate(_authorityGate);
                if (!InstallPreparedLayout(simulation, restoredLayout?.Layout ?? configuration.InitialLayout,
                    restoredLayout?.GeometrySeedPoses, restoreReferences))
                {
                    CookingSimulationHostOwnership.Release(simulation, this);
                    BestEffortAbortLifecycle(); RemoveLevel();
                    throw Fault(new InvalidOperationException("Preparation layout cannot be installed in this kitchen."));
                }
                PublishKitchen(simulation);
                _lifecycle.EnablePreparationGameplay();
            }
            finally { _initializingPreparation = false; }
        }
        return Operation(RunLifecycleOperation(() => _lifecycle.BeginPreparation(preparation)));
    }

    public CookingLevelHostOperationResult CompletePreparation()
    {
        Check(); return Operation(RunLifecycleOperation(_lifecycle.CompletePreparation));
    }

    private static bool AllowedDuringPreparation(CookingRecipeOperation operation) => operation is
        CookingRecipeOperation.Move or CookingRecipeOperation.Pickup or CookingRecipeOperation.Drop or
        CookingRecipeOperation.PutIn or CookingRecipeOperation.TakeOut or CookingRecipeOperation.Pour or
        CookingRecipeOperation.StartProcess or CookingRecipeOperation.ContinueProcess or CookingRecipeOperation.StopProcess or
        CookingRecipeOperation.ServePortion or CookingRecipeOperation.ClearContents or CookingRecipeOperation.DiscardItem or
        CookingRecipeOperation.RequestSupply or CookingRecipeOperation.ReceiveSupply or CookingRecipeOperation.TakeSupply;
    public CookingLevelHostOperationResult Start()
    {
        Check();
        var result = RunLifecycleOperation(() => _lifecycle.Start(_gameplayPublicationGuard));
        if (!result.Accepted)
            return Operation(result);

        if (!_lifecycle.TryGetGameplay(out var simulation))
            throw Fault(new InvalidOperationException("A running CookingLevelLifecycle did not expose its gameplay simulation."));

        PublishKitchen(simulation);
        _serviceStartLogicalTick = simulation.LogicalTick;

        return Operation(result);
    }

    private void PublishKitchen(CookingRecipeSimulation simulation)
    {
        try
        {
            _failureInjector?.ThrowIfRequested(CookingLevelEtHostFailurePoint.SimulationOwnershipAcquired);
            simulation.BindAuthorityGate(_authorityGate);
            _failureInjector?.ThrowIfRequested(CookingLevelEtHostFailurePoint.BeforeSimulationPublish);
            _ownedSimulation = simulation;
            Driver.Simulation = simulation;
            if (_effectiveFrontConfiguration is { } configured && _frontOfHouse is null)
            {
                _frontOfHouse = new(configured.Schedule);
                if (configured.Flow is not null) _frontOfHouse.ConfigureFlow(configured.Flow);
                _frontOfHouseMenu = new(configured.Menu);
            }
            BindFrontOfHouse(simulation);
        }
        catch (Exception exception)
        {
            CookingSimulationHostOwnership.Release(simulation, this);
            _ownedSimulation = null;
            Driver.Simulation = null;
            BestEffortAbortLifecycle();
            RemoveLevel();
            throw Fault(new InvalidOperationException("The level gameplay simulation could not be bound to its ET driver.", exception));
        }

    }

    /// <summary>Atomically changes the initialized kitchen layout in Created or Preparing.</summary>
    public bool TryInstallPreparedLayout(CookingRestaurantLayout layout)
    {
        Check(); ArgumentNullException.ThrowIfNull(layout);
        if (_ownedSimulation is null || _preparationConfiguration is null || _pending.Count != 0 || _inFlight is not null ||
            _lifecycle.State is not CookingLevelState.Created and not CookingLevelState.Preparing)
            return false;
        return InstallPreparedLayout(_ownedSimulation, layout, null, null);
    }

    private static CookingFrontOfHouse CreateInitialFront(CookingFrontOfHouseConfiguration configuration)
    {
        var house = new CookingFrontOfHouse(configuration.Schedule);
        if (configuration.Flow is not null) house.ConfigureFlow(configuration.Flow);
        if (configuration.ManualPolicyIdentity is { } policy) house.ConfigureManualWork(policy, (_, _) => false);
        house.ExportCheckpoint(new CookingFrontOfHouseMenu(configuration.Menu));
        return house;
    }

    private bool InstallPreparedLayout(CookingRecipeSimulation kitchen, CookingRestaurantLayout layout,
        IReadOnlyList<CookingPlayerPose>? seeds, CookingRecipeCheckpoint? restoreReferences)
    {
        if (_preparationConfiguration is not { } policy) return false;
        try
        {
            if (seeds is not null && seeds.Any(p => p is null || p.LastMovementTick != -1)) return false;
            var projection = policy.Project(layout, seeds ?? kitchen.Snapshot().Poses ?? Array.Empty<CookingPlayerPose>(), kitchen.ConfiguredAppliances);
            if (!projection.Accepted || !kitchen.CanInstallPreparedGeometry(projection, restoreReferences)) return false;
            if (restoreReferences is null && _installedLayout?.Layout.CanonicalText() == projection.FrozenLayout!.CanonicalText())
                return true;
            if (seeds is not null && !seeds.OrderBy(p => p.Player.Value, StringComparer.Ordinal)
                .SequenceEqual(projection.Geometry!.InitialPoses.OrderBy(p => p.Player.Value, StringComparer.Ordinal))) return false;
            CookingFrontOfHouse? house = null;
            CookingFrontOfHouseConfiguration? effective = null;
            var menu = _frontOfHouseMenu;
            if (_frontConfiguration is { } trusted)
            {
                effective = policy.DerivedFrontConfiguration(projection, trusted);
                house = CreateInitialFront(effective); menu = new(effective.Menu);
                if (effective.ManualPolicyIdentity is { } manual)
                    house.ConfigureManualWork(manual, (player, target) => CanFrontWork(kitchen, player, target));
                ValidateFrontConfiguration(house, menu, kitchen, effective, projection.Geometry);
                if (restoreReferences is null && _frontOfHouse is { } oldHouse && _effectiveFrontConfiguration is { } oldConfiguration &&
                    oldHouse.Snapshot().CanonicalText() != CreateInitialFront(oldConfiguration).ExportCheckpoint(new CookingFrontOfHouseMenu(oldConfiguration.Menu)).State.CanonicalText())
                    return false;
            }
            else if (_frontOfHouse is not null) return false;
            var installed = new CookingInstalledLayoutCheckpoint(projection.FrozenLayout!,
                Array.AsReadOnly(projection.Geometry!.InitialPoses.ToArray()));
            if (!RunPreparationMutation(() => kitchen.InstallPreparedGeometry(projection, restoreReferences))) return false;
            _installedLayout = installed;
            _effectiveFrontConfiguration = effective;
            _frontOfHouse = house;
            _frontOfHouseMenu = menu;
            BindFrontOfHouse(kitchen, prepared: true);
            return true;
        }
        catch (ArgumentException) { return false; }
        catch (OverflowException) { return false; }
    }

    public CookingLevelAdmissionResult TryEnqueue(CookingLevelCommandEnvelope envelope)
    {
        Check();
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(envelope.LevelScope);
        ArgumentNullException.ThrowIfNull(envelope.Command);

        var state = _lifecycle.State;
        if (state == CookingLevelState.Paused)
            return CookingLevelAdmissionResult.Reject(CookingLevelAdmissionReason.LevelPaused);
        if ((state != CookingLevelState.Running && !_lifecycle.IsPreparationAdmissionOpen) || _ownedSimulation is null)
            return CookingLevelAdmissionResult.Reject(CookingLevelAdmissionReason.LevelNotRunning);
        if (!Equals(envelope.LevelScope, Binding.LevelScope) ||
            !Equals(envelope.Command.Scope, Binding.LegacySimulationScope))
        {
            return CookingLevelAdmissionResult.Reject(CookingLevelAdmissionReason.ScopeMismatch);
        }
        if (!CookingRecipeCommandValidation.IsWellFormed(envelope.Command))
            return CookingLevelAdmissionResult.Reject(CookingLevelAdmissionReason.MalformedCommand);
        if (_lifecycle.IsPreparationAdmissionOpen && !AllowedDuringPreparation(envelope.Command.Operation))
            return CookingLevelAdmissionResult.Reject(CookingLevelAdmissionReason.LevelNotRunning);
        if (envelope.Command.Operation == CookingRecipeOperation.AdvanceTicks)
            return CookingLevelAdmissionResult.Reject(CookingLevelAdmissionReason.ReservedClockOperation);

        var fingerprint = CookingCommandFingerprint.Create(envelope);
        var pendingKey = PendingKey(envelope);
        var terminalKey = TerminalKey(envelope);
        if (_terminal.TryGetValue(terminalKey, out var terminal))
            return TerminalAdmission(envelope, fingerprint, terminal);
        if (LastCommittedSimulationBatch > 0 && envelope.Command.SimulationBatch <= LastCommittedSimulationBatch)
        {
            var stale = Disposition(envelope, fingerprint, CookingLevelDispositionKind.Stale, null,
                "simulation-batch-stale");
            Terminalize(terminalKey, new[] { stale });
            return CookingLevelAdmissionResult.Reject(CookingLevelAdmissionReason.BatchStale, stale);
        }

        if (_pending.TryGetValue(pendingKey, out var existing))
        {
            if (existing.Envelopes.Any(value => value.Fingerprint != fingerprint))
            {
                var admitted = new PendingEnvelope(envelope, fingerprint);
                existing.Envelopes.Add(admitted);
                var conflicts = existing.Envelopes.Select(value => Disposition(value.Envelope, value.Fingerprint,
                    CookingLevelDispositionKind.Conflicted, null, "command-identity-conflict")).ToArray();
                _pending.Remove(pendingKey);
                Terminalize(pendingKey, conflicts);
                var current = conflicts[^1];
                return CookingLevelAdmissionResult.Reject(CookingLevelAdmissionReason.CommandIdentityConflict, current);
            }

            existing.Envelopes.Add(new PendingEnvelope(envelope, fingerprint));
            return CookingLevelAdmissionResult.Accept();
        }
        if (_pending.Count >= _queueCapacity)
            return CookingLevelAdmissionResult.Reject(CookingLevelAdmissionReason.QueueFull);

        _pending.Add(pendingKey, new PendingGroup(pendingKey, new List<PendingEnvelope> { new(envelope, fingerprint) }));
        return CookingLevelAdmissionResult.Accept();
    }

    public CookingLevelFrameResult Tick()
    {
        Check();
        var state = _lifecycle.State;
        if (state == CookingLevelState.Paused)
            return CookingLevelFrameResult.Reject(CookingLevelFrameReason.LevelPaused, Binding.LevelScope, HostFrameSequence);
        if ((state != CookingLevelState.Running && !_lifecycle.IsPreparationAdmissionOpen) || _ownedSimulation is null)
            return CookingLevelFrameResult.Reject(CookingLevelFrameReason.LevelNotRunning, Binding.LevelScope, HostFrameSequence);

        _ticking = true;
        _completedFrame = null;
        try
        {
            _runtime.Tick();
            if (_tickFailure is not null)
                throw new InvalidOperationException("Cooking level authority failed during the ET tick; the host is faulted.", _tickFailure);
            return _completedFrame ?? throw Fault(new InvalidOperationException("The Cooking Level ET driver did not produce a frame result."));
        }
        finally
        {
            _ticking = false;
        }
    }

    public CookingLevelHostOperationResult Pause()
    {
        Check();
        return Operation(RunLifecycleOperation(_lifecycle.Pause));
    }

    public CookingLevelHostOperationResult Resume()
    {
        Check();
        return Operation(RunLifecycleOperation(_lifecycle.Resume));
    }

    /// <summary>前厅允许成功时才进入结束。没有前厅，或座位未空，都不改关卡。</summary>
    public CookingLevelHostOperationResult TryFinishService()
    {
        Check();
        if (_frontOfHouse is null || !_frontOfHouse.CanSucceed)
            return new CookingLevelHostOperationResult(false, "ServiceNotFinished", _lifecycle.State.ToString(),
                _lifecycle.Version, Array.Empty<CookingLevelPendingDisposition>());
        return BeginEnd(CookingLevelOutcome.Success);
    }

    public CookingLevelHostOperationResult BeginEnd(CookingLevelOutcome outcome)
    {
        Check();
        if (outcome == CookingLevelOutcome.Success && _frontConfiguration is not null && _frontOfHouse is { CanSucceed: false })
            return new(false, "ServiceNotFinished", _lifecycle.State.ToString(), _lifecycle.Version, Array.Empty<CookingLevelPendingDisposition>());
        var result = RunLifecycleOperation(() => _lifecycle.BeginEnd(outcome));
        var dispositions = result.Accepted ? CancelPending("level-ending") : Array.Empty<CookingLevelPendingDisposition>();
        return Operation(result, dispositions);
    }

    public CookingLevelSettlementConfirmationResult ConfirmSettlements(CookingLevelSettlementLedger ledger)
    {
        Check();
        ArgumentNullException.ThrowIfNull(ledger);
        if (_lifecycle.State != CookingLevelState.Ended || _lifecycle.Outcome != CookingLevelOutcome.Success)
        {
            return new CookingLevelSettlementConfirmationResult(
                false,
                CookingLevelSettlementConfirmationDisposition.Rejected,
                CookingLevelSettlementConfirmationReason.InvalidState,
                null);
        }

        if (_ownedSimulation is null)
        {
            return new CookingLevelSettlementConfirmationResult(
                false,
                CookingLevelSettlementConfirmationDisposition.Rejected,
                CookingLevelSettlementConfirmationReason.InvalidState,
                null);
        }

        return ledger.Confirm(_lifecycle.Scope, _ownedSimulation.SettlementHistory);
    }

    public CookingLevelSettlementStoreResult StoreSettlements(
        CookingLevelSettlementLedger ledger,
        CookingLevelSettlementStore store)
    {
        Check();
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(store);
        if (_lifecycle.State != CookingLevelState.Ended || _lifecycle.Outcome != CookingLevelOutcome.Success ||
            _ownedSimulation is null)
        {
            return new CookingLevelSettlementStoreResult(
                false,
                CookingLevelSettlementConfirmationDisposition.Rejected,
                CookingLevelSettlementStoreReason.InvalidState,
                null);
        }

        return CookingLevelSettlementStore.Commit(
            ledger, store, _lifecycle.Scope, _ownedSimulation.SettlementHistory);
    }

    public CookingMajorProgressResult ChooseDecoration(
        CookingMajorProgress progress,
        IReadOnlyList<CookingStationReplacement> replacements)
    {
        Check();
        ArgumentNullException.ThrowIfNull(progress);
        if (_lifecycle.State != CookingLevelState.Created || _ownedSimulation is null)
            return new CookingMajorProgressResult(false, CookingMajorProgressReason.InvalidState);
        var validation = progress.ValidateDecoration(replacements);
        if (!validation.Accepted) return validation;
        var moved = RunPreparationMutation(() => _ownedSimulation.MigrateStations(replacements));
        if (!moved.Accepted) return moved;
        return progress.ChooseDecoration(replacements);
    }

    public CookingMajorProgressResult Unlock(
        CookingMajorProgress progress,
        CookingContent content,
        DefinitionId definition)
    {
        Check();
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(content);
        if (_lifecycle.State != CookingLevelState.Created || _ownedSimulation is null)
            return new CookingMajorProgressResult(false, CookingMajorProgressReason.InvalidState);
        var validation = progress.ValidateUnlock(definition);
        if (!validation.Accepted || validation.Reason == CookingMajorProgressReason.Duplicate) return validation;
        var placed = RunPreparationMutation(() => _ownedSimulation.PlaceUnlock(content, definition));
        if (!placed.Accepted)
            return placed;
        return progress.Unlock(definition);
    }

    public CookingMajorProgressResult WriteMajorCheckpoint(
        CookingMajorProgress progress,
        CookingMajorCheckpointStore store)
    {
        Check();
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(store);
        if (_lifecycle.State != CookingLevelState.Created || _ownedSimulation is null || !progress.Locked)
            return new CookingMajorProgressResult(false, CookingMajorProgressReason.InvalidState);
        return store.Write(_lifecycle.Scope.MatchScope, progress, _ownedSimulation.ExportSuccessHandoff());
    }

    public CookingLevelHostOperationResult CompleteEnd()
    {
        Check();
        var result = RunLifecycleOperation(_lifecycle.CompleteEnd);
        if (result.Accepted)
        {
            if (_lifecycle.Outcome == CookingLevelOutcome.Success)
                DetachEndedSimulation();
            else
                ReleaseSimulationOwnership();
            RemoveLevel();
        }
        return Operation(result);
    }

    public CookingLevelHostGenerationResult CreateRetry(long newEpoch, CookingContent content, CookingMajorProgress? progress = null)
    {
        Check(); ArgumentNullException.ThrowIfNull(content);
        var result = RunLifecycleOperation(() => {
            var reason = _lifecycle.TryCreateRetryCandidate(newEpoch, out var candidate);
            return new CandidateResult(reason, candidate);
        });
        if (result.Reason != CookingLevelLifecycleReason.None) return RejectGeneration(result.Reason);
        return BuildAndCommitGeneration(result.Candidate!, true, _lifecycle.Preparation, content, progress);
    }

    public CookingLevelHostGenerationResult CreateSuccessor(LevelId newLevelId, long newEpoch) =>
        CreateSuccessorCore(newLevelId, newEpoch, null);

    public CookingLevelHostGenerationResult CreateSuccessor(LevelId newLevelId, long newEpoch, CookingLevelPreparation nextPreparation)
    {
        ArgumentNullException.ThrowIfNull(nextPreparation);
        return CreateSuccessorCore(newLevelId, newEpoch, nextPreparation);
    }

    private CookingLevelHostGenerationResult CreateSuccessorCore(LevelId newLevelId, long newEpoch, CookingLevelPreparation? nextPreparation)
    {
        Check();
        var result = RunLifecycleOperation(() => {
            var reason = _lifecycle.TryCreateSuccessorCandidate(newLevelId, newEpoch, out var candidate);
            return new CandidateResult(reason, candidate);
        });
        if (result.Reason != CookingLevelLifecycleReason.None) return RejectGeneration(result.Reason);
        if (_ownedSimulation is null) return RejectGeneration(CookingLevelLifecycleReason.GameplayUnavailable);
        if (_lifecycle.GameplayFactory is ICookingPreparationGameplayFactory && nextPreparation is null)
            return RejectGeneration(CookingLevelLifecycleReason.InvalidState);
        return BuildAndCommitGeneration(result.Candidate!, false, nextPreparation, null, null);
    }

    private CookingLevelHostGenerationResult BuildAndCommitGeneration(CookingLevelLifecycle candidate, bool retry,
        CookingLevelPreparation? preparation, CookingContent? content, CookingMajorProgress? progress)
    {
        CookingRecipeSimulation? kitchen = null;
        var acquired = false;
        _initializingPreparation = true;
        try
        {
            var frozenPreparation = preparation is null ? null : CookingLevelLifecycle.CopyPreparation(preparation);
            if (frozenPreparation is not null && candidate.ValidatePreparationCandidate(frozenPreparation) != CookingLevelLifecycleReason.None)
                return RejectGeneration(CookingLevelLifecycleReason.InvalidState);
            var policy = (candidate.GameplayFactory as ICookingPreparationGameplayFactory)?
                .CreatePreparationConfiguration(candidate.Scope, candidate.Configuration).Freeze();
            CookingRecipeCheckpoint? handoff = null;
            CookingFrontOfHouse? stagedSourceFront = null;
            var clearedOrders = 0; var clearedSettlements = 0;
            if (!retry)
            {
                var source = _ownedSimulation!.CreateGenerationTransactionCopy();
                clearedSettlements = source.SettlementHistory.Count;
                if (_frontOfHouse is { } oldFront)
                {
                    var restored = CookingFrontOfHouse.Restore(new(_frontOfHouseMenu.Templates[0], oldFront.Snapshot()), source);
                    if (!restored.Accepted || restored.FrontOfHouse is null) return RejectGeneration(CookingLevelLifecycleReason.InvalidState);
                    stagedSourceFront = restored.FrontOfHouse;
                    stagedSourceFront.FinishInProgress(source, _frontOfHouseMenu);
                }
                clearedOrders = source.Orders.Count;
                if (stagedSourceFront is not null) stagedSourceFront.ResetForNextLevel(source, _frontOfHouseMenu);
                handoff = source.ExportSuccessHandoff();
            }
            kitchen = candidate.GameplayFactory.Create(candidate.Scope, candidate.Configuration);
            if (kitchen is null || ReferenceEquals(kitchen, _ownedSimulation))
                return RejectGeneration(CookingLevelLifecycleReason.GameplayInitializationFailed);
            if (!_gameplayPublicationGuard.TryAcquire(kitchen)) return RejectGeneration(CookingLevelLifecycleReason.GameplayInitializationFailed);
            acquired = true;
            if (!Equals(kitchen.Snapshot().Scope, candidate.Scope.MatchScope))
                return RejectGeneration(CookingLevelLifecycleReason.GameplayInitializationFailed);
            if (retry)
            {
                CookingContentCatalog.ApplyStandardInitialSupply(kitchen, content!);
                if (progress is not null && !kitchen.ApplyRetryChoices(content!, progress).Accepted)
                    return RejectGeneration(CookingLevelLifecycleReason.InvalidState);
            }
            CookingInstalledLayoutCheckpoint? installed = null;
            var effective = _frontConfiguration;
            if (policy is not null)
            {
                var seeds = kitchen.SpatialConfiguration?.InitialPoses ?? Array.Empty<CookingPlayerPose>();
                var projection = policy.Project(policy.InitialLayout, seeds, kitchen.ConfiguredAppliances);
                var references = handoff is null ? null : handoff with { Poses = projection.Geometry?.InitialPoses };
                if (!projection.Accepted || !kitchen.CanInstallPreparedGeometry(projection, references))
                    return RejectGeneration(CookingLevelLifecycleReason.InvalidState);
                if (_frontConfiguration is { } trusted) effective = policy.DerivedFrontConfiguration(projection, trusted);
                if (!kitchen.InstallPreparedGeometry(projection, references)) return RejectGeneration(CookingLevelLifecycleReason.InvalidState);
                installed = new(projection.FrozenLayout!, Array.AsReadOnly(projection.Geometry!.InitialPoses.ToArray()));
            }
            if (handoff is not null && !kitchen.AcceptSuccessHandoff(handoff).Accepted)
                return RejectGeneration(CookingLevelLifecycleReason.InvalidState);
            CookingFrontOfHouse? front = effective is not null ? CreateInitialFront(effective) : stagedSourceFront;
            if (retry && effective is null && _frontOfHouse is { } legacy)
                front = new CookingFrontOfHouse(legacy.Snapshot().Schedule, legacy.Snapshot().Companion.Id);
            var menu = effective is null ? _frontOfHouseMenu : new CookingFrontOfHouseMenu(effective.Menu);
            Action? adoptLegacyFront = null;
            if (front is not null)
            {
                if (effective?.ManualPolicyIdentity is { } manual)
                {
                    var stagedKitchen = kitchen;
                    front.ConfigureManualWork(manual, (player, target) => CanFrontWork(stagedKitchen, player, target));
                }
                ValidateFrontConfiguration(front, menu, kitchen, effective, kitchen.SpatialConfiguration);
                if (_frontConfiguration is null && _frontOfHouse is { } original)
                    adoptLegacyFront = RunFrontOperation(() => original.PrepareGenerationStateAdoption(front));
            }
            if (candidate.AdoptSuccessorKitchen(kitchen) != CookingLevelLifecycleReason.None)
                return RejectGeneration(CookingLevelLifecycleReason.InvalidState);
            kitchen.BindAuthorityGate(_authorityGate);
            _failureInjector?.ThrowIfRequested(CookingLevelEtHostFailurePoint.SimulationOwnershipAcquired);
            _failureInjector?.ThrowIfRequested(CookingLevelEtHostFailurePoint.BeforeSimulationPublish);
            var result = InstallGeneration(candidate, retry);
            if (!result.Accepted) return result;
            var previous = _ownedSimulation;
            _ownedSimulation = kitchen; Driver.Simulation = kitchen;
            _preparationConfiguration = policy; _installedLayout = installed;
            _effectiveFrontConfiguration = effective; _serviceStartLogicalTick = 0;
            _stagedPreparation = frozenPreparation;
            if (adoptLegacyFront is not null) adoptLegacyFront(); else _frontOfHouse = front;
            _frontOfHouseMenu = menu;
            BindFrontOfHouse(kitchen, prepared: true);
            if (previous is not null) { CookingSimulationHostOwnership.Release(previous, this); previous.CloseLifecycle(); }
            acquired = false;
            kitchen = null;
            return result with { RetainedProcessCount = handoff?.Processes.Count ?? 0,
                ClearedOrderCount = clearedOrders, ClearedSettlementCount = clearedSettlements };
        }
        catch (Exception)
        {
            return RejectGeneration(CookingLevelLifecycleReason.GameplayInitializationFailed);
        }
        finally
        {
            if (acquired && kitchen is not null)
            {
                _gameplayPublicationGuard.Release(kitchen);
                if (!ReferenceEquals(kitchen, _ownedSimulation)) kitchen.CloseLifecycle();
            }
            _initializingPreparation = false;
        }
    }

    /// <summary>
    /// 导出宿主级恢复 checkpoint：本代际的 Level scope/epoch、config identity、preparation、
    /// lifecycle 状态与版本、HostFrameSequence、命令水位与整册仿真载荷。
    /// 前置条件：Level Running 或已初始化的 Preparing，且 admitted 命令已全部有终态（pending 与 in-flight 皆空）——
    /// 宿主 Dispose 对未决命令的既有语义是取消并返回有序 disposition，半途点不构成权威态记录。
    /// </summary>
    public CookingLevelCheckpointExportResult ExportCheckpoint()
    {
        Check();
        var state = _lifecycle.State;
        if (state == CookingLevelState.Paused)
            return new CookingLevelCheckpointExportResult(false, CookingLevelCheckpointExportReason.LevelPaused);
        if ((state != CookingLevelState.Running && !_lifecycle.IsPreparationAdmissionOpen) || _ownedSimulation is null)
            return new CookingLevelCheckpointExportResult(false, CookingLevelCheckpointExportReason.LevelNotRunning);
        if (_pending.Count > 0 || _inFlight is not null)
            return new CookingLevelCheckpointExportResult(false, CookingLevelCheckpointExportReason.PendingCommands);
        if (_lifecycle.Preparation is not { } preparation)
            return new CookingLevelCheckpointExportResult(false, CookingLevelCheckpointExportReason.PreparationMissing);
        // Publication must bind the payload to this generation, including before its first fixed tick.
        var recipeCheckpoint = _ownedSimulation.ExportCheckpoint();
        if (!Equals(recipeCheckpoint.LevelScope, Binding.LevelScope))
            return new CookingLevelCheckpointExportResult(false, CookingLevelCheckpointExportReason.LevelNotRunning);

        return new CookingLevelCheckpointExportResult(true, CookingLevelCheckpointExportReason.None,
            new CookingLevelCheckpoint(
                Binding.LevelScope,
                _lifecycle.Configuration.Identity,
                CookingLevelLifecycle.CopyPreparation(preparation),
                state,
                null,
                _lifecycle.Version,
                HostFrameSequence,
                LastCommittedSimulationBatch,
                recipeCheckpoint,
                _frontOfHouse is null ? null : RunFrontOperation(() => _frontOfHouse.ExportCheckpoint(_frontOfHouseMenu)),
                _frontConfigurationIdentity, _serviceStartLogicalTick, _preparationConfiguration?.Identity(), _installedLayout));
    }

    /// <summary>
    /// 按 checkpoint 重建一代 Level 宿主并交还调用方：同一 scope/epoch 与 config identity，
    /// 仿真由工厂创建后整册换入载荷，HostFrameSequence 单调不重置（reference/product-lifetimes §4.1），
    /// Level-local 命令水位按 checkpoint 恢复。任一步失败都会释放已创建宿主（ET 宿主是进程级单例），
    /// 并以结构化 reason 报告，不残留半恢复状态。
    /// </summary>
    public static CookingLevelCheckpointRestoreResult Restore(
        CookingLevelCheckpoint checkpoint,
        CookingConfigurationSnapshot configuration,
        ICookingLevelGameplayFactory factory)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(factory);
        if ((checkpoint.State != CookingLevelState.Running && checkpoint.State != CookingLevelState.Preparing)
            || checkpoint.Outcome is not null)
            return new(false, CookingLevelCheckpointRestoreReason.CheckpointScopeInvalid);
        if (!Equals(configuration.Identity, checkpoint.ConfigIdentity))
            return new CookingLevelCheckpointRestoreResult(false,
                CookingLevelCheckpointRestoreReason.ConfigurationIdentityMismatch);
        if (checkpoint.Recipe is null || !Equals(checkpoint.Recipe.Scope, checkpoint.Scope.MatchScope))
            return new CookingLevelCheckpointRestoreResult(false,
                CookingLevelCheckpointRestoreReason.CheckpointPayloadScopeMismatch);
        CookingPreparationConfiguration? trustedPreparation;
        try { trustedPreparation = (factory as ICookingPreparationGameplayFactory)?.CreatePreparationConfiguration(checkpoint.Scope, configuration).Freeze(); }
        catch (ArgumentException) { return new(false, CookingLevelCheckpointRestoreReason.ConfigurationIdentityMismatch); }
        if (checkpoint.PreparationConfigurationIdentity != trustedPreparation?.Identity())
            return new(false, CookingLevelCheckpointRestoreReason.ConfigurationIdentityMismatch);
        var preparing = checkpoint.State == CookingLevelState.Preparing;
        if (preparing && trustedPreparation is null)
            return new(false, CookingLevelCheckpointRestoreReason.ConfigurationIdentityMismatch);
        if (checkpoint.ServiceStartLogicalTick < 0 ||
            checkpoint.ServiceStartLogicalTick > checkpoint.Recipe.LogicalTick ||
            (preparing && checkpoint.ServiceStartLogicalTick != 0) ||
            (trustedPreparation is null && checkpoint.ServiceStartLogicalTick != 0))
            return new(false, CookingLevelCheckpointRestoreReason.GameplayRestoreRejected,
                RecipeRestoreReason: CookingCheckpointRestoreReason.CounterInvalid);
        var trustedFront = (factory as ICookingFrontOfHouseGameplayFactory)?.FrontOfHouseConfiguration;
        var effectiveFront = trustedFront;
        if ((trustedPreparation is null) != (checkpoint.InstalledLayout is null))
            return new(false, CookingLevelCheckpointRestoreReason.ConfigurationIdentityMismatch);
        if (checkpoint.InstalledLayout is { } installed)
        {
            try
            {
                if (installed.GeometrySeedPoses is null || installed.GeometrySeedPoses.Any(p => p is null || p.LastMovementTick != -1))
                    return new(false, CookingLevelCheckpointRestoreReason.ConfigurationIdentityMismatch);
                var projection = trustedPreparation!.Project(installed.Layout, installed.GeometrySeedPoses, configuration.Appliances);
                if (!projection.Accepted || !installed.GeometrySeedPoses.OrderBy(p => p.Player.Value, StringComparer.Ordinal)
                    .SequenceEqual(projection.Geometry!.InitialPoses.OrderBy(p => p.Player.Value, StringComparer.Ordinal)))
                    return new(false, CookingLevelCheckpointRestoreReason.ConfigurationIdentityMismatch);
                if (trustedFront is not null) effectiveFront = trustedPreparation.DerivedFrontConfiguration(projection, trustedFront);
            }
            catch (ArgumentException) { return new(false, CookingLevelCheckpointRestoreReason.ConfigurationIdentityMismatch); }
            catch (OverflowException) { return new(false, CookingLevelCheckpointRestoreReason.ConfigurationIdentityMismatch); }
        }
        if (trustedFront is not null && checkpoint.FrontOfHouse is { } clock &&
            clock.State.ServiceTicks != (preparing ? 0 : Math.Min(checkpoint.Recipe.LogicalTick - checkpoint.ServiceStartLogicalTick, trustedFront.Schedule.ServiceTicks)))
            return new(false, CookingLevelCheckpointRestoreReason.FrontOfHouseRestoreRejected,
                FrontOfHouseRestoreReason: CookingFrontOfHouseRestoreReason.ConfigurationMismatch);
        if (checkpoint.FrontOfHouseConfigurationIdentity != trustedFront?.Identity()
            || (trustedFront is not null && checkpoint.FrontOfHouse is null)
            || (trustedFront is null && checkpoint.FrontOfHouse?.State is { } untrusted && (untrusted.Flow is not null || untrusted.ManualPolicyIdentity is not null)))
            return new(false, CookingLevelCheckpointRestoreReason.FrontOfHouseRestoreRejected,
                FrontOfHouseRestoreReason: CookingFrontOfHouseRestoreReason.ConfigurationMismatch);
        if (preparing)
        {
            // Preparing has never advanced service. Compare every business field against
            // the same trusted initial configuration used by PublishKitchen/BindFrontOfHouse.
            CookingFrontOfHouseCheckpoint? pristine = null;
            if (effectiveFront is not null)
            {
                var initial = CreateInitialFront(effectiveFront);
                pristine = initial.ExportCheckpoint(new CookingFrontOfHouseMenu(effectiveFront.Menu));
            }
            if (checkpoint.FrontOfHouse?.CanonicalText() != pristine?.CanonicalText())
                return new(false, CookingLevelCheckpointRestoreReason.FrontOfHouseRestoreRejected,
                    FrontOfHouseRestoreReason: CookingFrontOfHouseRestoreReason.ConfigurationMismatch);
        }
        if (checkpoint.FrontOfHouse?.State.Closing == true && checkpoint.Recipe.Supply is { Closing: false })
            return new(false, CookingLevelCheckpointRestoreReason.GameplayRestoreRejected,
                RecipeRestoreReason: CookingCheckpointRestoreReason.SupplyStateInvalid);
        // 载荷必须属于信封声明的同一 match，且已经绑定的 Level scope（含 epoch）必须就是本代际。
        // 只改 epoch、载荷 match 不变的 checkpoint 不能绕过这里被建成另一代宿主。
        // 尚未推进 fixed tick 的载荷没有代际绑定，同样拒绝：宿主恢复要求这一代已经被记录。

        if (checkpoint.Recipe.LevelScope is not { } levelScope || !Equals(levelScope, checkpoint.Scope))
            return new CookingLevelCheckpointRestoreResult(false,
                CookingLevelCheckpointRestoreReason.TickHistoryScopeMismatch);

        CookingLevelLifecycle? lifecycle = null;
        CookingLevelEtHost? host = null;
        try
        {
            lifecycle = new CookingLevelLifecycle(checkpoint.Scope, configuration, factory);
        }
        catch (Exception exception) when (exception is ArgumentException)
        {
            return new CookingLevelCheckpointRestoreResult(false,
                CookingLevelCheckpointRestoreReason.CheckpointScopeInvalid, null,
                CookingCheckpointRestoreReason.None, exception.Message);
        }

        try
        {
            host = new CookingLevelEtHost(lifecycle);
            var prepared = host.BeginPreparationCore(checkpoint.Preparation, checkpoint.InstalledLayout, checkpoint.Recipe);
            if (prepared.Accepted && !preparing) prepared = host.CompletePreparation();
            if (!prepared.Accepted)
            {
                host.Dispose();
                return new CookingLevelCheckpointRestoreResult(false,
                    CookingLevelCheckpointRestoreReason.PreparationRejected, null,
                    CookingCheckpointRestoreReason.None, prepared.Reason);
            }

            if (!preparing)
            {
                var started = host.Start();
                if (!started.Accepted)
                {
                    host.Dispose();
                    return new CookingLevelCheckpointRestoreResult(false,
                        CookingLevelCheckpointRestoreReason.LifecycleStartRejected, null,
                        CookingCheckpointRestoreReason.None, started.Reason);
                }
            }

            if (!lifecycle.TryPeekBoundKitchen(out var simulation) || simulation is null)
            {
                host.Dispose();
                return new CookingLevelCheckpointRestoreResult(false,
                    CookingLevelCheckpointRestoreReason.GameplayUnavailable);
            }

            var restored = simulation.RestoreCheckpoint(checkpoint.Recipe);
            if (!restored.Accepted)
            {
                host.Dispose();
                return new CookingLevelCheckpointRestoreResult(false,
                    CookingLevelCheckpointRestoreReason.GameplayRestoreRejected, null, restored.Reason);
            }

            if (checkpoint.FrontOfHouse is { } frontOfHouseCheckpoint)
            {
                var frontOfHouse = CookingFrontOfHouse.Restore(frontOfHouseCheckpoint, simulation);
                if (!frontOfHouse.Accepted || frontOfHouse.FrontOfHouse is null ||
                    frontOfHouse.ActiveOrderTemplate is null)
                {
                    host.Dispose();
                    return new CookingLevelCheckpointRestoreResult(false,
                        CookingLevelCheckpointRestoreReason.FrontOfHouseRestoreRejected,
                        FrontOfHouseRestoreReason: frontOfHouse.Reason);
                }

                host._frontOfHouse = frontOfHouse.FrontOfHouse;
                host._frontOfHouseMenu = new(frontOfHouse.FrontOfHouse.Snapshot().OrderMenu);
                var frontState = host._frontOfHouse.Snapshot();
                if (frontState.Work.Where(x => x.Player is not null).Any(x => !host.CanFrontWork(simulation, x.Player!.Value, x.Target))
                    || frontState.ManualPolicyIdentity != trustedFront?.ManualPolicyIdentity
                    || (trustedFront is not null && frontState.ServiceTicks != (preparing ? 0 : Math.Min(checkpoint.Recipe.LogicalTick - checkpoint.ServiceStartLogicalTick, trustedFront.Schedule.ServiceTicks))))
                {
                    host.Dispose();
                    return new(false, CookingLevelCheckpointRestoreReason.FrontOfHouseRestoreRejected,
                        FrontOfHouseRestoreReason: CookingFrontOfHouseRestoreReason.WorkInvalid);
                }
                host.BindFrontOfHouse(simulation);
                if (trustedFront is not null)
                    simulation.RestoreFrontOfHouseState(frontState.Closing, host._frontOfHouse.CanSucceed, checkpoint.Recipe.StateVersion);
            }

            host.AdoptRecoveredCheckpoint(checkpoint);
            return new CookingLevelCheckpointRestoreResult(true, CookingLevelCheckpointRestoreReason.None, host);
        }
        catch (Exception exception)
        {
            host?.Dispose();
            return new CookingLevelCheckpointRestoreResult(false,
                CookingLevelCheckpointRestoreReason.HostCreationFailed, null,
                CookingCheckpointRestoreReason.None, exception.Message);
        }
    }

    internal void AdoptRecoveredCheckpoint(CookingLevelCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (_lifecycle.State != checkpoint.State ||
            (_lifecycle.State != CookingLevelState.Running && !_lifecycle.IsPreparationAdmissionOpen))
            throw new InvalidOperationException("Only a matching running or preparing level host can adopt a recovered checkpoint.");
        if (HostFrameSequence > checkpoint.HostFrameSequence)
            throw new InvalidOperationException("A recovered checkpoint must not move the host frame sequence backwards.");

        _serviceStartLogicalTick = checkpoint.ServiceStartLogicalTick;
        HostFrameSequence = checkpoint.HostFrameSequence;
        LastCommittedSimulationBatch = checkpoint.LastCommittedSimulationBatch;
        _lifecycle.AdoptRecoveredVersion(checkpoint.LifecycleVersion);
    }

    internal void ExecuteEtUpdate(CookingLevelDriverComponent driver)
    {
        if (!_ticking || !ReferenceEquals(driver, Driver) || !ReferenceEquals(driver.Simulation, _ownedSimulation))
            return;

        try
        {
            ExecuteFrame(driver.Simulation!);
        }
        catch (Exception exception)
        {
            _tickFailure = exception;
        }
    }

    private void ExecuteFrame(CookingRecipeSimulation simulation)
    {
        if (_executingAuthorityMutation)
            throw new InvalidOperationException("Cooking authority mutation cannot be reentered.");

        _executingAuthorityMutation = true;
        try
        {
            ExecuteFrameCore(simulation);
        }
        finally
        {
            _executingAuthorityMutation = false;
        }
    }

    private void ExecuteFrameCore(CookingRecipeSimulation simulation)
    {
        var candidateFrame = checked(HostFrameSequence + 1);
        _inFlight = FreezeNextBatch();
        var dispositions = new List<CookingLevelPendingDisposition>();

        if (_inFlight is not null)
        {
            foreach (var group in _inFlight.Groups)
            {
                var fingerprints = group.Envelopes.Select(envelope => envelope.Fingerprint).Distinct().ToArray();
                if (fingerprints.Length != 1)
                {
                    var conflicts = group.Envelopes.Select(envelope =>
                        Disposition(envelope.Envelope, envelope.Fingerprint, CookingLevelDispositionKind.Conflicted, null,
                            "command-identity-conflict")).ToArray();
                    dispositions.AddRange(conflicts);
                    Terminalize(group.Key, conflicts);
                    TerminalizeRemainingIdentity(group.Key, dispositions);
                    continue;
                }

                var ordered = group.Envelopes
                    .OrderBy(envelope => envelope.Envelope.SourceConnectionId, StringComparer.Ordinal)
                    .ThenBy(envelope => envelope.Envelope.CorrelationId, StringComparer.Ordinal)
                    .ToArray();
                var representative = ordered[0];
                CookingRecipeCommandResult result;
                try
                {
                    result = simulation.Submit(representative.Envelope.Command);
                }
                catch (Exception exception)
                {
                    var failed = group.Envelopes.Select(envelope =>
                        Disposition(envelope.Envelope, envelope.Fingerprint, CookingLevelDispositionKind.Cancelled, null,
                            "command-submission-fault")).ToArray();
                    dispositions.AddRange(failed);
                    Terminalize(group.Key, failed);
                    CancelAdmittedAfterFault("command-lane-fault", dispositions);
                    throw new InvalidOperationException("Cooking command submission failed.", exception);
                }

                var executed = Disposition(representative.Envelope, representative.Fingerprint,
                    CookingLevelDispositionKind.Executed, result, "executed",
                    representative.Envelope.SourceConnectionId, representative.Envelope.CorrelationId);
                dispositions.Add(executed);
                var terminalized = new List<CookingLevelPendingDisposition> { executed };
                foreach (var duplicate in ordered.Skip(1))
                {
                    var duplicateResult = result with { IsDuplicate = true, Events = Array.Empty<CookingRecipeEvent>() };
                    var disposition = Disposition(duplicate.Envelope, duplicate.Fingerprint,
                        CookingLevelDispositionKind.Duplicate, duplicateResult, "duplicate-collapsed",
                        representative.Envelope.SourceConnectionId, representative.Envelope.CorrelationId);
                    dispositions.Add(disposition);
                    terminalized.Add(disposition);
                }
                Terminalize(group.Key, terminalized);
                TerminalizeRemainingIdentity(group.Key, dispositions);
            }
        }

        CookingRecipeTickResult tick;
        try
        {
            tick = simulation.AdvanceFixedTick(Binding.LevelScope, candidateFrame);
        }
        catch
        {
            CancelAdmittedAfterFault("fixed-step-fault", dispositions);
            throw;
        }

        HostFrameSequence = candidateFrame;
        if (_lifecycle.State == CookingLevelState.Running) _frontOfHouse?.Step(simulation, _frontOfHouseMenu);
        if (_lifecycle.State == CookingLevelState.Running && _frontOfHouse?.IsClosing == true) simulation.StopNewSupplyRequests();
        if (_lifecycle.State == CookingLevelState.Running && _frontConfiguration is not null && _frontOfHouse is not null)
            simulation.UpdateFrontOfHouseState(_frontOfHouse.IsClosing, _frontOfHouse.CanSucceed);
        if (_inFlight is not null)
            LastCommittedSimulationBatch = _inFlight.SimulationBatch;
        _inFlight = null;
        MaterializeStalePending();
        _completedFrame = new CookingLevelFrameResult(true, CookingLevelFrameReason.None, Binding.LevelScope,
            HostFrameSequence, LastCommittedSimulationBatch > 0 ? LastCommittedSimulationBatch : null, tick, Sort(dispositions));
    }

    private FrozenBatch? FreezeNextBatch()
    {
        if (_pending.Count == 0)
            return null;

        var batch = _pending.Values.SelectMany(group => group.Envelopes)
            .Min(envelope => envelope.Envelope.Command.SimulationBatch);
        var frozenGroups = new List<PendingGroup>();
        foreach (var group in _pending.Values.ToArray())
        {
            var frozen = group.Envelopes
                .Where(envelope => envelope.Envelope.Command.SimulationBatch == batch)
                .ToList();
            if (frozen.Count == 0)
                continue;

            var remaining = group.Envelopes
                .Where(envelope => envelope.Envelope.Command.SimulationBatch != batch)
                .ToList();
            if (remaining.Count == 0)
                _pending.Remove(group.Key);
            else
                _pending[group.Key] = new PendingGroup(group.Key, remaining);
            frozenGroups.Add(new PendingGroup(group.Key, frozen));
        }

        return new FrozenBatch(batch, frozenGroups
            .OrderBy(group => group.Key.Player.Value, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Command.Value, StringComparer.Ordinal)
            .ToArray());
    }

    private void TerminalizeRemainingIdentity(
        CookingLevelCommandGroupKey key,
        ICollection<CookingLevelPendingDisposition> currentDispositions)
    {
        if (!_pending.Remove(key, out var remaining))
            return;

        var terminal = _terminal[key];
        var materialized = new List<CookingLevelPendingDisposition>();
        foreach (var envelope in remaining.Envelopes)
        {
            var matching = terminal.Dispositions.FirstOrDefault(value => value.Fingerprint == envelope.Fingerprint);
            CookingLevelPendingDisposition disposition;
            if (matching is not null && matching.Kind is CookingLevelDispositionKind.Executed or CookingLevelDispositionKind.Duplicate &&
                matching.Result is not null)
            {
                var representative = terminal.Dispositions.First(value => value.Kind == CookingLevelDispositionKind.Executed);
                disposition = Disposition(envelope.Envelope, envelope.Fingerprint, CookingLevelDispositionKind.Duplicate,
                    matching.Result with { IsDuplicate = true, Events = Array.Empty<CookingRecipeEvent>() },
                    "duplicate-terminal", representative.Envelope.SourceConnectionId,
                    representative.Envelope.CorrelationId);
            }
            else if (terminal.Dispositions.Any(value => value.Kind == CookingLevelDispositionKind.Conflicted))
            {
                disposition = Disposition(envelope.Envelope, envelope.Fingerprint,
                    CookingLevelDispositionKind.Conflicted, null, "command-identity-conflict");
            }
            else
            {
                disposition = Disposition(envelope.Envelope, envelope.Fingerprint,
                    matching?.Kind ?? CookingLevelDispositionKind.Conflicted, matching?.Result,
                    matching?.Reason ?? "command-identity-conflict",
                    matching?.RepresentativeConnectionId, matching?.RepresentativeCorrelationId);
            }
            materialized.Add(disposition);
            currentDispositions.Add(disposition);
        }

        if (materialized.Count > 0)
        {
            _history.AddRange(materialized);
            _terminal[key] = new TerminalCommand(terminal.Dispositions.Concat(materialized).ToArray());
        }
    }

    private void CancelAdmittedAfterFault(
        string reason,
        ICollection<CookingLevelPendingDisposition> currentDispositions)
    {
        if (_inFlight is not null)
        {
            foreach (var group in _inFlight.Groups.Where(group => !_terminal.ContainsKey(group.Key)))
            {
                var cancelled = group.Envelopes.Select(envelope => Disposition(envelope.Envelope, envelope.Fingerprint,
                    CookingLevelDispositionKind.Cancelled, null, reason)).ToArray();
                foreach (var disposition in cancelled)
                    currentDispositions.Add(disposition);
                Terminalize(group.Key, cancelled);
            }
        }

        foreach (var group in _pending.Values.ToArray())
        {
            var cancelled = group.Envelopes.Select(envelope => Disposition(envelope.Envelope, envelope.Fingerprint,
                CookingLevelDispositionKind.Cancelled, null, reason)).ToArray();
            foreach (var disposition in cancelled)
                currentDispositions.Add(disposition);
            Terminalize(group.Key, cancelled);
        }
        _pending.Clear();
        _inFlight = null;
        _finalHistory = new ReadOnlyCollection<CookingLevelPendingDisposition>(Sort(_history).ToArray());
    }

    private void MaterializeStalePending()
    {
        if (LastCommittedSimulationBatch <= 0)
            return;
        var staleGroups = _pending.Values
            .Where(group => group.Envelopes.All(envelope =>
                envelope.Envelope.Command.SimulationBatch <= LastCommittedSimulationBatch))
            .ToArray();
        foreach (var group in staleGroups)
        {
            _pending.Remove(group.Key);
            var stale = group.Envelopes.Select(envelope =>
                Disposition(envelope.Envelope, envelope.Fingerprint, CookingLevelDispositionKind.Stale, null,
                    "simulation-batch-stale")).ToArray();
            Terminalize(group.Key, stale);
        }
    }

    private CookingLevelHostGenerationResult RejectGeneration(CookingLevelLifecycleReason reason) =>
        new(
            false,
            reason.ToString(),
            Binding.LevelScope,
            _lifecycle.Outcome,
            _lifecycle.Version,
            _lifecycle.Version,
            _lifecycle.State == CookingLevelState.Ended,
            null,
            null,
            Array.Empty<CookingLevelPendingDisposition>());

    private CookingLevelHostGenerationResult InstallGeneration(
        CookingLevelLifecycle candidate,
        bool isRetry)
    {
        var sourceScope = Binding.LevelScope;
        var sourceLifecycle = _lifecycle;
        var sourceBinding = Binding;
        candidate.BindOwnerGameplayGate(_hostGameplayGate);
        candidate.BindOperationGate(_lifecycleOperationGate);
        var oldLevel = Level;
        var oldDriver = Driver;
        var oldLevelAlive = oldLevel is not null && !oldLevel.IsDisposed;
        var oldSimulation = oldDriver?.Simulation;
        void RestoreSourceTree()
        {
            _lifecycle = sourceLifecycle; Binding = sourceBinding;
            _runtime.Run(SceneId, _ => {
                if (RestaurantRuntime.GetComponent<CookingLevelComponent>() is { IsDisposed: false })
                    RestaurantRuntime.RemoveComponent<CookingLevelComponent>();
                if (oldLevelAlive) { InstallLevel(sourceLifecycle, injectFailures: false); Driver.Simulation = oldSimulation; }
                else { Level = oldLevel!; Driver = oldDriver!; }
            });
        }

        try
        {
            _runtime.Run(SceneId, _ =>
            {
                if (oldLevel is not null && !oldLevel.IsDisposed)
                    RestaurantRuntime.RemoveComponent<CookingLevelComponent>();
                InstallLevel(candidate);
            });
        }
        catch (Exception)
        {
            RestoreSourceTree();
            return RejectGeneration(CookingLevelLifecycleReason.GameplayInitializationFailed);
        }

        var committed = RunLifecycleOperation(() => isRetry
            ? sourceLifecycle.CommitRetryCandidate(candidate)
            : sourceLifecycle.CommitSuccessorCandidate(candidate));
        if (!committed.Accepted)
        {
            RestoreSourceTree();
            return RejectGeneration(committed.Reason);
        }

        var cancelled = CancelPending("level-generation-replaced");
        _lifecycle = candidate;
        Binding = new CookingLevelSimulationBinding(candidate.Scope, candidate.Scope.MatchScope);
        LastCommittedSimulationBatch = 0;
        _pending.Clear();
        _terminal.Clear();
        return new CookingLevelHostGenerationResult(
            true,
            committed.Reason.ToString(),
            sourceScope,
            committed.SourceOutcome,
            committed.SourceVersionBefore,
            committed.SourceVersionAfter,
            committed.SourceGameplayClosed,
            Binding.LevelScope,
            candidate,
            cancelled);
    }

    private void InstallLevel(CookingLevelLifecycle lifecycle, bool injectFailures = true)
    {
        CookingLevelComponent? level = null;
        try
        {
            level = RestaurantRuntime.AddComponent<CookingLevelComponent>();
            if (injectFailures)
                _failureInjector?.ThrowIfRequested(CookingLevelEtHostFailurePoint.LevelCreated);
            level.Scope = lifecycle.Scope;
            level.Lifecycle = lifecycle;
            var driver = level.AddComponent<CookingLevelDriverComponent>();
            if (injectFailures)
                _failureInjector?.ThrowIfRequested(CookingLevelEtHostFailurePoint.DriverCreated);
            driver.Host = this;
            if (injectFailures)
                _failureInjector?.ThrowIfRequested(CookingLevelEtHostFailurePoint.BeforeLevelPublish);
            Level = level;
            Driver = driver;
        }
        catch
        {
            if (level is not null && !level.IsDisposed)
                RestaurantRuntime.RemoveComponent<CookingLevelComponent>();
            throw;
        }
    }

    private void RemoveLevel()
    {
        if (Level is null || Level.IsDisposed)
            return;
        _runtime.Run(SceneId, _ => RestaurantRuntime.RemoveComponent<CookingLevelComponent>());
    }

    private CookingLevelPendingDisposition[] CancelPending(string reason)
    {
        var cancelled = new List<CookingLevelPendingDisposition>();
        if (_inFlight is not null)
        {
            foreach (var group in _inFlight.Groups.Where(group => !_terminal.ContainsKey(group.Key)))
            {
                var groupCancelled = group.Envelopes.Select(envelope => Disposition(envelope.Envelope, envelope.Fingerprint,
                    CookingLevelDispositionKind.Cancelled, null, reason)).ToArray();
                cancelled.AddRange(groupCancelled);
                Terminalize(group.Key, groupCancelled);
            }
            _inFlight = null;
        }

        foreach (var group in _pending.Values.ToArray())
        {
            var groupCancelled = group.Envelopes.Select(envelope => Disposition(envelope.Envelope, envelope.Fingerprint,
                CookingLevelDispositionKind.Cancelled, null, reason)).ToArray();
            cancelled.AddRange(groupCancelled);
            Terminalize(group.Key, groupCancelled);
        }
        _pending.Clear();
        return Sort(cancelled).ToArray();
    }

    private void Terminalize(CookingLevelCommandGroupKey key, IEnumerable<CookingLevelPendingDisposition> dispositions)
    {
        var materialized = dispositions.ToArray();
        if (materialized.Length == 0)
            return;
        _history.AddRange(materialized);
        _terminal[key] = new TerminalCommand(materialized);
    }

    private CookingLevelAdmissionResult TerminalAdmission(
        CookingLevelCommandEnvelope envelope,
        CookingCommandFingerprint fingerprint,
        TerminalCommand terminal)
    {
        var matching = terminal.Dispositions.FirstOrDefault(value => value.Fingerprint == fingerprint);
        if (matching is not null &&
            matching.Kind is (CookingLevelDispositionKind.Executed or CookingLevelDispositionKind.Duplicate) &&
            matching.Result is not null)
        {
            var representative = terminal.Dispositions.First(value => value.Kind == CookingLevelDispositionKind.Executed);
            var duplicate = Disposition(envelope, fingerprint, CookingLevelDispositionKind.Duplicate,
                matching.Result with { IsDuplicate = true, Events = Array.Empty<CookingRecipeEvent>() },
                "duplicate-terminal", representative.Envelope.SourceConnectionId,
                representative.Envelope.CorrelationId);
            _history.Add(duplicate);
            return new CookingLevelAdmissionResult(true, CookingLevelAdmissionReason.None, duplicate);
        }

        if (terminal.Dispositions.Any(value => value.Kind == CookingLevelDispositionKind.Conflicted))
        {
            var conflict = Disposition(envelope, fingerprint, CookingLevelDispositionKind.Conflicted, null,
                "command-identity-conflict");
            _history.Add(conflict);
            return CookingLevelAdmissionResult.Reject(CookingLevelAdmissionReason.CommandIdentityConflict, conflict);
        }

        if (matching is null)
        {
            var conflict = Disposition(envelope, fingerprint, CookingLevelDispositionKind.Conflicted, null,
                "command-identity-conflict");
            _history.Add(conflict);
            return CookingLevelAdmissionResult.Reject(CookingLevelAdmissionReason.CommandIdentityConflict, conflict);
        }

        return CookingLevelAdmissionResult.Reject(CookingLevelAdmissionReason.CommandTerminal, matching);
    }

    private CookingLevelHostOperationResult Operation(
        CookingLevelLifecycleResult result,
        IReadOnlyList<CookingLevelPendingDisposition>? dispositions = null) =>
        new(result.Accepted, result.Reason.ToString(), _lifecycle.State.ToString(), _lifecycle.Version,
            dispositions ?? Array.Empty<CookingLevelPendingDisposition>());

    private void ReleaseSimulationOwnership()
    {
        if (_ownedSimulation is null)
            return;
        if (Driver is not null && !Driver.IsDisposed)
            Driver.Simulation = null;
        CookingSimulationHostOwnership.Release(_ownedSimulation, this);
        _ownedSimulation = null;
    }

    /// <summary>
    /// 成功结束只解除 ET driver 绑定，厨房对象留给随后的 <see cref="CreateSuccessor"/>。
    /// 失败和中止仍走 <see cref="ReleaseSimulationOwnership"/>，不把失败现场交到下一代。
    /// </summary>
    private void DetachEndedSimulation()
    {
        if (Driver is not null && !Driver.IsDisposed)
            Driver.Simulation = null;
    }

    private T RunPreparationMutation<T>(Func<T> operation)
    {
        if (_executingPreparationMutation || _lifecycle.State is not CookingLevelState.Created and not CookingLevelState.Preparing)
            throw new InvalidOperationException("Preparation mutation is unavailable or reentered.");
        _executingPreparationMutation = true;
        try { return operation(); } finally { _executingPreparationMutation = false; }
    }

    private T RunLifecycleOperation<T>(Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (_executingLifecycleOperation)
            throw new InvalidOperationException("Cooking level lifecycle operations cannot be reentered.");

        _executingLifecycleOperation = true;
        try
        {
            return operation();
        }
        finally
        {
            _executingLifecycleOperation = false;
        }
    }

    private void BestEffortAbortLifecycle()
    {
        try
        {
            RunLifecycleOperation(() =>
            {
                var begin = _lifecycle.BeginEnd(CookingLevelOutcome.Aborted);
                if (begin.Accepted)
                    _lifecycle.CompleteEnd();
                return begin;
            });
        }
        catch
        {
            // Preserve the original installation/binding failure.
        }
    }

    private Exception Fault(Exception exception)
    {
        _tickFailure ??= exception;
        return exception;
    }

    private void Check()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Environment.CurrentManagedThreadId != _ownerThread)
            throw new InvalidOperationException("Cooking level host operations require the owner thread.");
        if (_ticking || _executingPreparationMutation || _initializingPreparation)
            throw new InvalidOperationException("Cooking level host operations cannot be reentered.");
        if (_tickFailure is not null)
            throw new InvalidOperationException("Cooking level host is faulted.", _tickFailure);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        if (Environment.CurrentManagedThreadId != _ownerThread || _ticking || _executingPreparationMutation || _initializingPreparation)
            throw new InvalidOperationException("Dispose requires the idle owner thread.");

        CancelPending("host-disposed");
        _finalHistory = new ReadOnlyCollection<CookingLevelPendingDisposition>(Sort(_history).ToArray());
        try
        {
            if (_lifecycle.State == CookingLevelState.Ending)
            {
                RunLifecycleOperation(_lifecycle.CompleteEnd);
                if (_lifecycle.Outcome == CookingLevelOutcome.Success)
                    DetachEndedSimulation();
            }
            else if (_lifecycle.State != CookingLevelState.Ended)
            {
                BestEffortAbortLifecycle();
            }
            if (_ownedSimulation is not null)
                _ownedSimulation.CloseLifecycle();
            ReleaseSimulationOwnership();
            _runtime.Dispose();
        }
        finally
        {
            _disposed = true;
        }
    }

    private static CookingLevelCommandGroupKey PendingKey(CookingLevelCommandEnvelope envelope) =>
        new(envelope.LevelScope, envelope.Command.Player, envelope.Command.Command);

    private static CookingLevelCommandGroupKey TerminalKey(CookingLevelCommandEnvelope envelope) =>
        PendingKey(envelope);

    private static CookingLevelPendingDisposition Disposition(
        CookingLevelCommandEnvelope envelope,
        CookingCommandFingerprint fingerprint,
        CookingLevelDispositionKind kind,
        CookingRecipeCommandResult? result,
        string reason,
        string? representativeConnectionId = null,
        string? representativeCorrelationId = null) =>
        new(envelope, fingerprint, kind, result, reason, representativeConnectionId, representativeCorrelationId);

    private static IReadOnlyList<CookingLevelPendingDisposition> Sort(IEnumerable<CookingLevelPendingDisposition> values) =>
        values.OrderBy(value => value.Envelope.Command.Player.Value, StringComparer.Ordinal)
            .ThenBy(value => value.Envelope.Command.Command.Value, StringComparer.Ordinal)
            .ThenBy(value => value.Fingerprint.Value, StringComparer.Ordinal)
            .ThenBy(value => value.Envelope.SourceConnectionId, StringComparer.Ordinal)
            .ThenBy(value => value.Envelope.CorrelationId, StringComparer.Ordinal)
            .ToArray();

    private sealed class HostLifecycleOperationGate : ICookingLevelOperationGate
    {
        private readonly CookingLevelEtHost _host;

        public HostLifecycleOperationGate(CookingLevelEtHost host) => _host = host;

        public bool IsLifecycleOperationOpen =>
            _host._executingLifecycleOperation && !_host._disposed;
    }

    private sealed class FrontAuthorityGate(CookingLevelEtHost host) : ICookingRecipeAuthorityGate
    {
        public bool IsAuthorityMutationOpen => !host._disposed && host._tickFailure is null &&
            (host._executingFrontOperation || (host._ticking && host._executingAuthorityMutation));
    }

    private sealed class HostAuthorityGate : ICookingRecipeAuthorityGate
    {
        private readonly CookingLevelEtHost _host;

        public HostAuthorityGate(CookingLevelEtHost host) => _host = host;

        public bool IsAuthorityMutationOpen =>
            !_host._disposed && _host._tickFailure is null &&
            ((_host._ticking && _host._executingAuthorityMutation) ||
             (_host._executingPreparationMutation && _host._lifecycle.State is CookingLevelState.Created or CookingLevelState.Preparing));
    }

    private sealed class HostGameplayGate : ICookingRecipeLifecycleGate
    {
        private readonly CookingLevelEtHost _host;

        public HostGameplayGate(CookingLevelEtHost host) => _host = host;

        public bool IsGameplayMutationOpen => !_host._disposed && _host._tickFailure is null;
    }

    private sealed class GameplayPublicationGuard : ICookingLevelGameplayPublicationGuard
    {
        private readonly CookingLevelEtHost _host;

        public GameplayPublicationGuard(CookingLevelEtHost host) => _host = host;

        public bool TryAcquire(CookingRecipeSimulation gameplay)
        {
            try
            {
                CookingSimulationHostOwnership.Acquire(gameplay, _host);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        public void Release(CookingRecipeSimulation gameplay) =>
            CookingSimulationHostOwnership.Release(gameplay, _host);
    }

    private sealed record CandidateResult(
        CookingLevelLifecycleReason Reason,
        CookingLevelLifecycle? Candidate);

    private sealed record PendingEnvelope(
        CookingLevelCommandEnvelope Envelope,
        CookingCommandFingerprint Fingerprint);

    private sealed record PendingGroup(
        CookingLevelCommandGroupKey Key,
        List<PendingEnvelope> Envelopes);

    private sealed record FrozenBatch(
        long SimulationBatch,
        IReadOnlyList<PendingGroup> Groups);

    private sealed record TerminalCommand(
        IReadOnlyList<CookingLevelPendingDisposition> Dispositions);
}

internal static class CookingSimulationHostOwnership
{
    private static readonly object Sync = new();
    private static readonly ConditionalWeakTable<CookingRecipeSimulation, Owner> Owners = new();

    public static void Acquire(CookingRecipeSimulation simulation, object host)
    {
        lock (Sync)
        {
            if (Owners.TryGetValue(simulation, out var owner) && !ReferenceEquals(owner.Host, host))
                throw new InvalidOperationException("A CookingRecipeSimulation cannot be driven by both legacy and Level ET hosts.");
            if (!Owners.TryGetValue(simulation, out _))
                Owners.Add(simulation, new Owner(host));
        }
    }

    public static void Release(CookingRecipeSimulation simulation, object host)
    {
        lock (Sync)
        {
            if (Owners.TryGetValue(simulation, out var owner) && ReferenceEquals(owner.Host, host))
                Owners.Remove(simulation);
        }
    }

    private sealed record Owner(object Host);
}
