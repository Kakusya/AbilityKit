namespace AbilityKit.Game.Cooking;

public readonly record struct RestaurantRuntimeId(long Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct LevelId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct MapId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct LayoutId(string Value)
{
    public override string ToString() => Value;
}

public sealed record CookingLogicalLayout(
    LayoutId Id,
    IReadOnlyList<StationSlotId> ApplianceStations,
    IReadOnlyList<DefinitionId> Containers);

public sealed record CookingLevelScope
{
    public CookingLevelScope(
        CookingScope matchScope,
        RestaurantRuntimeId restaurantRuntime,
        LevelId level,
        long levelEpoch)
    {
        ArgumentNullException.ThrowIfNull(matchScope);
        if (string.IsNullOrWhiteSpace(matchScope.Session.Value))
            throw new ArgumentException("The parent match session identity is required.", nameof(matchScope));
        if (string.IsNullOrWhiteSpace(matchScope.World.Value))
            throw new ArgumentException("The parent match world identity is required.", nameof(matchScope));
        if (string.IsNullOrWhiteSpace(matchScope.Match.Value))
            throw new ArgumentException("The parent match identity is required.", nameof(matchScope));
        if (restaurantRuntime.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(restaurantRuntime));
        if (string.IsNullOrWhiteSpace(level.Value))
            throw new ArgumentException("The level identity is required.", nameof(level));
        if (levelEpoch <= 0)
            throw new ArgumentOutOfRangeException(nameof(levelEpoch));

        MatchScope = matchScope;
        RestaurantRuntime = restaurantRuntime;
        Level = level;
        LevelEpoch = levelEpoch;
    }

    public CookingScope MatchScope { get; }
    public RestaurantRuntimeId RestaurantRuntime { get; }
    public LevelId Level { get; }
    public long LevelEpoch { get; }
}

public sealed record CookingLevelPreparation(
    LevelId Level,
    MapId Map,
    CookingLogicalLayout Layout,
    CookingConfigurationIdentity ConfigIdentity);

public enum CookingLevelState
{
    Created,
    Preparing,
    Ready,
    Running,
    Paused,
    Ending,
    Ended,
}

public enum CookingLevelOutcome
{
    Success,
    Failed,
    Aborted,
}

public enum CookingLevelLifecycleReason
{
    None,
    InvalidState,
    InvalidOutcome,
    LevelIdMissing,
    LevelIdMismatch,
    MapIdMissing,
    LayoutIdMissing,
    LayoutMalformed,
    ConfigIdentityMismatch,
    DuplicateLayoutReference,
    ApplianceNotFound,
    ContainerNotFound,
    GameplayInitializationFailed,
    GameplayUnavailable,
    ScopeMismatch,
    EpochMismatch,
    SnapshotIdentityMismatch,
    SnapshotSequenceDuplicate,
    SnapshotSequenceStale,
    SnapshotSequenceGap,
    RetryRequiresFailedOutcome,
    SuccessorRequiresSuccessOutcome,
    EpochNotAdvanced,
    SuccessorLevelIdMissing,
    SuccessorLevelIdUnchanged,
    GenerationAlreadyCreated,
    BlockedByOwnerDecision,
}

public sealed record CookingLevelLifecycleEvent(
    long Sequence,
    long Version,
    CookingLevelState State,
    CookingLevelOutcome? Outcome,
    string Type,
    CookingLevelLifecycleReason Reason,
    string Summary);

public sealed record CookingLevelLifecycleResult(
    bool Accepted,
    CookingLevelLifecycleReason Reason,
    CookingLevelState State,
    CookingLevelOutcome? Outcome,
    long Version,
    IReadOnlyList<CookingLevelLifecycleEvent> Events)
{
    public static CookingLevelLifecycleResult Reject(
        CookingLevelLifecycleReason reason,
        CookingLevelState state,
        CookingLevelOutcome? outcome,
        long version) =>
        new(false, reason, state, outcome, version, Array.Empty<CookingLevelLifecycleEvent>());

    internal static IReadOnlyList<CookingLevelLifecycleEvent> ReadOnlyEvents(
        IEnumerable<CookingLevelLifecycleEvent> events) =>
        Array.AsReadOnly(events.ToArray());
}

public sealed record CookingLevelSuccessorResult(
    bool Accepted,
    CookingLevelLifecycleReason Reason,
    CookingLevelLifecycle? NewLevel,
    CookingLevelScope SourceScope,
    CookingLevelOutcome? SourceOutcome,
    CookingLevelScope? NewScope,
    long SourceVersionBefore,
    long SourceVersionAfter,
    bool SourceGameplayClosed,
    CookingLevelLifecycleResult SourceResult);

public interface ICookingLevelGameplayFactory
{
    CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration);
}

/// <summary>Optional trusted front-of-house configuration on the existing factory; legacy factories are unchanged.</summary>
public interface ICookingFrontOfHouseGameplayFactory : ICookingLevelGameplayFactory
{
    CookingFrontOfHouseConfiguration FrontOfHouseConfiguration { get; }
}

internal interface ICookingLevelGameplayPublicationGuard
{
    bool TryAcquire(CookingRecipeSimulation gameplay);
    void Release(CookingRecipeSimulation gameplay);
}

internal interface ICookingLevelOperationGate
{
    bool IsLifecycleOperationOpen { get; }
}

public sealed record CookingLevelLifecycleSnapshot(
    CookingLevelScope Scope,
    string ConfigIdentity,
    MapId? Map,
    LayoutId? Layout,
    CookingLevelState State,
    CookingLevelOutcome? Outcome,
    long Version,
    bool HasCreatedNextGeneration);

public sealed record CookingLevelLifecycleSnapshotApplyResult(
    bool Accepted,
    CookingLevelLifecycleReason Reason,
    CookingLevelLifecycleSnapshot? Current);

public sealed class CookingLevelLifecycleSnapshotApplier
{
    private readonly CookingLevelScope _scope;
    private readonly string _configIdentity;

    public CookingLevelLifecycleSnapshotApplier(
        CookingLevelScope scope,
        CookingConfigurationIdentity configIdentity)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        ArgumentNullException.ThrowIfNull(configIdentity);
        _configIdentity = configIdentity.ToString();
    }

    public CookingLevelLifecycleSnapshot? Current { get; private set; }

    public CookingLevelLifecycleSnapshotApplyResult Apply(CookingLevelLifecycleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!Equals(snapshot.Scope, _scope))
            return Reject(CookingLevelLifecycleReason.ScopeMismatch);
        if (!StringComparer.Ordinal.Equals(snapshot.ConfigIdentity, _configIdentity))
            return Reject(CookingLevelLifecycleReason.SnapshotIdentityMismatch);
        if (!IsSnapshotConsistent(snapshot))
            return Reject(CookingLevelLifecycleReason.SnapshotSequenceGap);

        if (Current is null)
        {
            if (snapshot.Version < 0)
                return Reject(CookingLevelLifecycleReason.SnapshotSequenceGap);
            Current = snapshot;
            return Accept();
        }

        if (snapshot.Version == Current.Version)
            return Reject(CookingLevelLifecycleReason.SnapshotSequenceDuplicate);
        if (snapshot.Version < Current.Version)
            return Reject(CookingLevelLifecycleReason.SnapshotSequenceStale);
        if (snapshot.Version != Current.Version + 1 || !IsValidTransition(Current, snapshot))
            return Reject(CookingLevelLifecycleReason.SnapshotSequenceGap);

        Current = snapshot;
        return Accept();
    }

    private CookingLevelLifecycleSnapshotApplyResult Accept() =>
        new(true, CookingLevelLifecycleReason.None, Current);

    private CookingLevelLifecycleSnapshotApplyResult Reject(CookingLevelLifecycleReason reason) =>
        new(false, reason, Current);

    private static bool IsSnapshotConsistent(CookingLevelLifecycleSnapshot snapshot)
    {
        if (snapshot.Version < 0)
            return false;
        if (snapshot.HasCreatedNextGeneration && snapshot.State != CookingLevelState.Ended)
            return false;

        var hasPreparationIdentity = snapshot.Map is not null && snapshot.Layout is not null;
        var hasAnyPreparationIdentity = snapshot.Map is not null || snapshot.Layout is not null;
        if (hasAnyPreparationIdentity && !hasPreparationIdentity)
            return false;
        if (snapshot.Outcome is not null && !Enum.IsDefined(snapshot.Outcome.Value))
            return false;
        if (snapshot.Outcome is CookingLevelOutcome.Success or CookingLevelOutcome.Failed && !hasPreparationIdentity)
            return false;

        var fieldsConsistent = snapshot.State switch
        {
            CookingLevelState.Created => snapshot.Outcome is null && !hasAnyPreparationIdentity,
            CookingLevelState.Preparing or CookingLevelState.Ready or CookingLevelState.Running or CookingLevelState.Paused =>
                snapshot.Outcome is null && hasPreparationIdentity,
            CookingLevelState.Ending or CookingLevelState.Ended =>
                snapshot.Outcome is not null &&
                (hasPreparationIdentity || snapshot.Outcome == CookingLevelOutcome.Aborted),
            _ => false,
        };
        if (!fieldsConsistent)
            return false;

        var reachableVersion = snapshot.State switch
        {
            CookingLevelState.Created => snapshot.Version == 0,
            CookingLevelState.Preparing => snapshot.Version == 1,
            CookingLevelState.Ready => snapshot.Version == 2,
            CookingLevelState.Running => snapshot.Version >= 3 && snapshot.Version % 2 == 1,
            CookingLevelState.Paused => snapshot.Version >= 4 && snapshot.Version % 2 == 0,
            CookingLevelState.Ending when snapshot.Outcome is CookingLevelOutcome.Success or CookingLevelOutcome.Failed =>
                snapshot.Version >= 4 && snapshot.Version % 2 == 0,
            CookingLevelState.Ending when hasPreparationIdentity => snapshot.Version >= 2,
            CookingLevelState.Ending => snapshot.Version == 1,
            CookingLevelState.Ended when snapshot.Outcome is CookingLevelOutcome.Success or CookingLevelOutcome.Failed =>
                snapshot.Version >= 5 && snapshot.Version % 2 == 1,
            CookingLevelState.Ended when hasPreparationIdentity => snapshot.Version >= 3,
            CookingLevelState.Ended => snapshot.Version == 2,
            _ => false,
        };
        if (!reachableVersion)
            return false;
        if (!snapshot.HasCreatedNextGeneration)
            return true;

        return snapshot.State == CookingLevelState.Ended &&
            snapshot.Outcome is CookingLevelOutcome.Success or CookingLevelOutcome.Failed &&
            snapshot.Version >= 6 && snapshot.Version % 2 == 0;
    }

    private static bool IsValidTransition(
        CookingLevelLifecycleSnapshot current,
        CookingLevelLifecycleSnapshot next)
    {
        if (current.HasCreatedNextGeneration || !HasStableFields(current, next))
            return false;

        return (current.State, next.State) switch
        {
            (CookingLevelState.Created, CookingLevelState.Preparing) =>
                next.Outcome is null && next.Map is not null && next.Layout is not null,
            (CookingLevelState.Preparing, CookingLevelState.Ready) =>
                next.Outcome is null && HasSamePreparationFields(current, next),
            (CookingLevelState.Ready, CookingLevelState.Running) =>
                next.Outcome is null && HasSamePreparationFields(current, next),
            (CookingLevelState.Running, CookingLevelState.Paused) =>
                next.Outcome is null && HasSamePreparationFields(current, next),
            (CookingLevelState.Paused, CookingLevelState.Running) =>
                next.Outcome is null && HasSamePreparationFields(current, next),
            (CookingLevelState.Running, CookingLevelState.Ending) =>
                next.Outcome is CookingLevelOutcome.Success or CookingLevelOutcome.Failed or CookingLevelOutcome.Aborted &&
                HasSamePreparationFields(current, next),
            (CookingLevelState.Created, CookingLevelState.Ending) =>
                next.Outcome == CookingLevelOutcome.Aborted && !HasAnyPreparationFields(next),
            (CookingLevelState.Preparing or CookingLevelState.Ready or CookingLevelState.Paused,
                CookingLevelState.Ending) =>
                next.Outcome == CookingLevelOutcome.Aborted && HasSamePreparationFields(current, next),
            (CookingLevelState.Ending, CookingLevelState.Ended) =>
                next.Outcome == current.Outcome && HasSamePreparationFields(current, next),
            (CookingLevelState.Ended, CookingLevelState.Ended) =>
                next.Outcome == current.Outcome &&
                HasSamePreparationFields(current, next) &&
                !current.HasCreatedNextGeneration &&
                next.HasCreatedNextGeneration,
            _ => false,
        };
    }

    private static bool HasStableFields(
        CookingLevelLifecycleSnapshot current,
        CookingLevelLifecycleSnapshot next) =>
        Equals(current.Scope, next.Scope) &&
        StringComparer.Ordinal.Equals(current.ConfigIdentity, next.ConfigIdentity);

    private static bool HasSamePreparationFields(
        CookingLevelLifecycleSnapshot current,
        CookingLevelLifecycleSnapshot next) =>
        current.Map == next.Map && current.Layout == next.Layout;

    private static bool HasAnyPreparationFields(CookingLevelLifecycleSnapshot snapshot) =>
        snapshot.Map is not null || snapshot.Layout is not null;
}

public sealed class CookingLevelLifecycle
{
    private readonly CookingConfigurationSnapshot _configuration;
    private readonly ICookingLevelGameplayFactory _gameplayFactory;
    private readonly ICookingRecipeLifecycleGate _gameplayGate;
    private ICookingRecipeLifecycleGate? _ownerGameplayGate;
    private ICookingLevelOperationGate? _operationGate;
    private readonly bool _allowPreparationLevelMismatch;
    private readonly List<CookingLevelLifecycleEvent> _events = new();
    private long _eventSequence;
    private CookingLevelPreparation? _preparation;
    private CookingRecipeSimulation? _gameplay;
    private bool _gameplayClosed;
    private bool _hasCreatedNextGeneration;
    private bool _receivesSuccessorKitchen;
    private bool _preparationKitchenInitialized;
    private bool _preparationGameplayEnabled;
    private bool _isStarting;
    private bool _startReentered;

    public CookingLevelLifecycle(
        CookingLevelScope scope,
        CookingConfigurationSnapshot configuration,
        ICookingLevelGameplayFactory gameplayFactory)
        : this(scope, configuration, gameplayFactory, allowPreparationLevelMismatch: false)
    {
    }

    internal CookingLevelLifecycle(
        CookingLevelScope scope,
        CookingConfigurationSnapshot configuration,
        ICookingLevelGameplayFactory gameplayFactory,
        bool allowPreparationLevelMismatch)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _gameplayFactory = gameplayFactory ?? throw new ArgumentNullException(nameof(gameplayFactory));
        _gameplayGate = new LifecycleGameplayGate(this);
        _allowPreparationLevelMismatch = allowPreparationLevelMismatch;
    }

    public CookingLevelScope Scope { get; }
    public CookingLevelState State { get; private set; } = CookingLevelState.Created;
    public CookingLevelOutcome? Outcome { get; private set; }
    public long Version { get; private set; }
    public IReadOnlyList<CookingLevelLifecycleEvent> EventHistory => _events.AsReadOnly();
    public bool IsGameplayAdmissionOpen => State == CookingLevelState.Running && _gameplay is not null && !_gameplayClosed;
    internal bool IsPreparationAdmissionOpen => _preparationGameplayEnabled && State == CookingLevelState.Preparing &&
        _gameplay is not null && !_gameplayClosed;

    internal void EnablePreparationGameplay()
    {
        if (State != CookingLevelState.Created || _gameplay is null || _gameplayClosed)
            throw new InvalidOperationException("Preparation gameplay requires an initialized Created kitchen.");
        _preparationGameplayEnabled = true;
    }
    public bool HasCreatedNextGeneration => _hasCreatedNextGeneration;

    /// <summary>
    /// 这一代是成功交接的下一小关：厨房已经换入，<see cref="Start"/> 不得再向工厂要一份空仿真。
    /// 普通新开局保持 false。
    /// </summary>
    internal bool ReceivesSuccessorKitchen => _receivesSuccessorKitchen;

    internal CookingConfigurationSnapshot Configuration => _configuration;
    internal ICookingLevelGameplayFactory GameplayFactory => _gameplayFactory;
    internal CookingLevelPreparation? Preparation => _preparation;

    internal void BindOperationGate(ICookingLevelOperationGate operationGate)
    {
        ArgumentNullException.ThrowIfNull(operationGate);
        if (_operationGate is not null && !ReferenceEquals(_operationGate, operationGate))
            throw new InvalidOperationException("The level lifecycle is already bound to another operation gate.");
        _operationGate = operationGate;
    }

    internal void BindOwnerGameplayGate(ICookingRecipeLifecycleGate ownerGameplayGate)
    {
        ArgumentNullException.ThrowIfNull(ownerGameplayGate);
        if (_ownerGameplayGate is not null && !ReferenceEquals(_ownerGameplayGate, ownerGameplayGate))
            throw new InvalidOperationException("The level lifecycle is already bound to another gameplay owner gate.");
        _ownerGameplayGate = ownerGameplayGate;
    }

    public bool TryGetGameplay(out CookingRecipeSimulation gameplay)
    {
        if (!IsGameplayAdmissionOpen)
        {
            gameplay = default!;
            return false;
        }

        gameplay = _gameplay!;
        return true;
    }

    /// <summary>
    /// 准备态只读观察。成功交接和失败重开都会在 <see cref="CookingLevelState.Created"/> 挂上厨房，
    /// 此时玩法入口仍关闭。<see cref="Start"/> 之后与 <see cref="TryGetGameplay"/> 同一份对象。
    /// </summary>
    internal bool TryPeekBoundKitchen(out CookingRecipeSimulation? kitchen)
    {
        kitchen = _gameplay;
        return kitchen is not null;
    }

    internal CookingLevelLifecycleResult InitializePreparationKitchen(ICookingLevelGameplayPublicationGuard publicationGuard)
    {
        ArgumentNullException.ThrowIfNull(publicationGuard);
        if (RejectBlockedLifecycleOperation() is { } blocked) return blocked;
        if (State != CookingLevelState.Created) return Reject(CookingLevelLifecycleReason.InvalidState);
        if (_gameplay is not null)
            return new(true, CookingLevelLifecycleReason.None, State, Outcome, Version, Array.Empty<CookingLevelLifecycleEvent>());
        CookingRecipeSimulation? candidate = null;
        _startReentered = false;
        _isStarting = true;
        try { candidate = _gameplayFactory.Create(Scope, _configuration); }
        catch (Exception) { return Reject(CookingLevelLifecycleReason.GameplayInitializationFailed); }
        finally { _isStarting = false; }
        if (candidate is null || _startReentered || State != CookingLevelState.Created || _gameplay is not null)
            return Reject(CookingLevelLifecycleReason.GameplayInitializationFailed);
        var acquired = false;
        try
        {
            if (!publicationGuard.TryAcquire(candidate)) return Reject(CookingLevelLifecycleReason.GameplayInitializationFailed);
            acquired = true;
            candidate.BindLifecycleGate(_gameplayGate);
        }
        catch (Exception)
        {
            if (acquired) publicationGuard.Release(candidate);
            return Reject(CookingLevelLifecycleReason.GameplayInitializationFailed);
        }
        _gameplay = candidate;
        _gameplayClosed = false;
        _preparationKitchenInitialized = true;
        return new(true, CookingLevelLifecycleReason.None, State, Outcome, Version, Array.Empty<CookingLevelLifecycleEvent>());
    }

    public CookingLevelLifecycleResult BeginPreparation(CookingLevelPreparation preparation)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        if (RejectBlockedLifecycleOperation() is { } reentrant)
            return reentrant;
        if (State != CookingLevelState.Created)
            return Reject(CookingLevelLifecycleReason.InvalidState);

        _preparation = CopyPreparation(preparation);
        return Commit(CookingLevelState.Preparing, "level-preparation-began", "stored an immutable level preparation candidate");
    }

    internal CookingLevelLifecycleReason ValidatePreparationCandidate(CookingLevelPreparation preparation) =>
        ValidatePreparation(Scope, _configuration, preparation, _allowPreparationLevelMismatch);

    public CookingLevelLifecycleResult CompletePreparation()
    {
        if (RejectBlockedLifecycleOperation() is { } reentrant)
            return reentrant;
        if (State != CookingLevelState.Preparing)
            return Reject(CookingLevelLifecycleReason.InvalidState);
        if (_preparation is null)
            return Reject(CookingLevelLifecycleReason.InvalidState);

        var validation = ValidatePreparation(Scope, _configuration, _preparation, _allowPreparationLevelMismatch);
        if (validation != CookingLevelLifecycleReason.None)
            return Reject(validation);

        return Commit(CookingLevelState.Ready, "level-prepared", "validated level, map, layout, and configuration identity");
    }

    public CookingLevelLifecycleResult Start() => Start(publicationGuard: null);

    internal CookingLevelLifecycleResult Start(ICookingLevelGameplayPublicationGuard? publicationGuard)
    {
        if (RejectBlockedLifecycleOperation() is { } blocked)
            return blocked;
        if (State != CookingLevelState.Ready)
            return Reject(CookingLevelLifecycleReason.InvalidState);
        if (_isStarting)
        {
            _startReentered = true;
            return Reject(CookingLevelLifecycleReason.InvalidState);
        }

        if (_receivesSuccessorKitchen && _gameplay is null)
            return Reject(CookingLevelLifecycleReason.GameplayUnavailable);

        var reuseKitchen = _receivesSuccessorKitchen || _preparationKitchenInitialized;
        CookingRecipeSimulation? gameplay = reuseKitchen ? _gameplay : null;
        _startReentered = false;
        _isStarting = true;
        try
        {
            if (!reuseKitchen)
                gameplay = _gameplayFactory.Create(Scope, _configuration);
        }
        catch (Exception)
        {
            return Reject(CookingLevelLifecycleReason.GameplayInitializationFailed);
        }
        finally
        {
            _isStarting = false;
        }

        if (gameplay is null)
            return Reject(CookingLevelLifecycleReason.GameplayInitializationFailed);

        var publicationAcquired = false;
        try
        {
            if (publicationGuard is not null && !publicationGuard.TryAcquire(gameplay))
                return Reject(CookingLevelLifecycleReason.GameplayInitializationFailed);
            publicationAcquired = publicationGuard is not null;
            gameplay.BindLifecycleGate(_gameplayGate);
        }
        catch (Exception)
        {
            if (publicationAcquired)
                publicationGuard!.Release(gameplay);
            return Reject(CookingLevelLifecycleReason.GameplayInitializationFailed);
        }

        if (_startReentered || State != CookingLevelState.Ready ||
            (!reuseKitchen && _gameplay is not null) ||
            (reuseKitchen && !ReferenceEquals(_gameplay, gameplay)))
        {
            if (publicationAcquired)
                publicationGuard!.Release(gameplay);
            if (!reuseKitchen)
                gameplay.CloseLifecycle();
            return Reject(CookingLevelLifecycleReason.GameplayInitializationFailed);
        }

        _gameplay = gameplay;
        _gameplayClosed = false;
        return Commit(CookingLevelState.Running, "level-started", "created the only gameplay simulation for this level generation");
    }

    public CookingLevelLifecycleResult Pause()
    {
        if (RejectBlockedLifecycleOperation() is { } reentrant)
            return reentrant;
        if (State != CookingLevelState.Running)
            return Reject(CookingLevelLifecycleReason.InvalidState);
        return Commit(CookingLevelState.Paused, "level-paused", "closed gameplay admission while preserving the level generation");
    }

    public CookingLevelLifecycleResult Resume()
    {
        if (RejectBlockedLifecycleOperation() is { } reentrant)
            return reentrant;
        if (State != CookingLevelState.Paused)
            return Reject(CookingLevelLifecycleReason.InvalidState);
        return Commit(CookingLevelState.Running, "level-resumed", "reopened gameplay admission for the existing level generation");
    }

    public CookingLevelLifecycleResult BeginEnd(CookingLevelOutcome outcome)
    {
        if (RejectBlockedLifecycleOperation() is { } reentrant)
            return reentrant;
        if (!Enum.IsDefined(outcome))
            return Reject(CookingLevelLifecycleReason.InvalidOutcome);

        var accepted = outcome switch
        {
            CookingLevelOutcome.Success or CookingLevelOutcome.Failed => State == CookingLevelState.Running,
            CookingLevelOutcome.Aborted => State is CookingLevelState.Created or CookingLevelState.Preparing or
                CookingLevelState.Ready or CookingLevelState.Running or CookingLevelState.Paused,
            _ => false,
        };
        if (!accepted)
            return Reject(CookingLevelLifecycleReason.InvalidState);

        Outcome = outcome;
        return Commit(CookingLevelState.Ending, "level-ending", $"locked the immutable level outcome '{outcome}'");
    }

    public CookingLevelLifecycleResult CompleteEnd()
    {
        if (RejectBlockedLifecycleOperation() is { } reentrant)
            return reentrant;
        if (State != CookingLevelState.Ending || Outcome is null)
            return Reject(CookingLevelLifecycleReason.InvalidState);

        if (_gameplay is not null && !_gameplayClosed)
        {
            _gameplay.CloseLifecycle();
            _gameplayClosed = true;
        }

        return Commit(CookingLevelState.Ended, "level-ended", "closed gameplay resources and completed the level generation");
    }

    public CookingLevelSuccessorResult CreateRetry(long newEpoch)
    {
        var before = Version;
        if (_operationGate is not null && !_operationGate.IsLifecycleOperationOpen)
            return RejectSuccessor(CookingLevelLifecycleReason.InvalidState, before);
        if (_isStarting)
        {
            _startReentered = true;
            return RejectSuccessor(CookingLevelLifecycleReason.InvalidState, before);
        }
        if (State != CookingLevelState.Ended)
            return RejectSuccessor(CookingLevelLifecycleReason.InvalidState, before);
        if (Outcome != CookingLevelOutcome.Failed)
            return RejectSuccessor(CookingLevelLifecycleReason.RetryRequiresFailedOutcome, before);
        if (_hasCreatedNextGeneration)
            return RejectSuccessor(CookingLevelLifecycleReason.GenerationAlreadyCreated, before);
        if (newEpoch <= Scope.LevelEpoch)
            return RejectSuccessor(CookingLevelLifecycleReason.EpochNotAdvanced, before);

        var nextScope = new CookingLevelScope(Scope.MatchScope, Scope.RestaurantRuntime, Scope.Level, newEpoch);
        return CreateNextGeneration(nextScope, "level-retry-created", "created a failed-level retry generation", before);
    }

    public CookingLevelSuccessorResult CreateSuccessor(LevelId newLevelId, long newEpoch)
    {
        var before = Version;
        if (_operationGate is not null && !_operationGate.IsLifecycleOperationOpen)
            return RejectSuccessor(CookingLevelLifecycleReason.InvalidState, before);
        if (_isStarting)
        {
            _startReentered = true;
            return RejectSuccessor(CookingLevelLifecycleReason.InvalidState, before);
        }
        if (State != CookingLevelState.Ended)
            return RejectSuccessor(CookingLevelLifecycleReason.InvalidState, before);
        if (Outcome != CookingLevelOutcome.Success)
            return RejectSuccessor(CookingLevelLifecycleReason.SuccessorRequiresSuccessOutcome, before);
        if (_hasCreatedNextGeneration)
            return RejectSuccessor(CookingLevelLifecycleReason.GenerationAlreadyCreated, before);
        if (string.IsNullOrWhiteSpace(newLevelId.Value))
            return RejectSuccessor(CookingLevelLifecycleReason.SuccessorLevelIdMissing, before);
        if (newLevelId == Scope.Level)
            return RejectSuccessor(CookingLevelLifecycleReason.SuccessorLevelIdUnchanged, before);
        if (newEpoch <= Scope.LevelEpoch)
            return RejectSuccessor(CookingLevelLifecycleReason.EpochNotAdvanced, before);

        var nextScope = new CookingLevelScope(Scope.MatchScope, Scope.RestaurantRuntime, newLevelId, newEpoch);
        return CreateNextGeneration(nextScope, "level-successor-created", "created the next successful level generation", before);
    }

    internal CookingLevelSuccessorResult CreateCompatibilityRestart(CookingScope newMatchScope, long newEpoch)
    {
        ArgumentNullException.ThrowIfNull(newMatchScope);
        var before = Version;
        if (_operationGate is not null && !_operationGate.IsLifecycleOperationOpen)
            return RejectSuccessor(CookingLevelLifecycleReason.InvalidState, before);
        if (_isStarting)
        {
            _startReentered = true;
            return RejectSuccessor(CookingLevelLifecycleReason.InvalidState, before);
        }
        if (State != CookingLevelState.Ended)
            return RejectSuccessor(CookingLevelLifecycleReason.InvalidState, before);
        if (Outcome != CookingLevelOutcome.Success)
            return RejectSuccessor(CookingLevelLifecycleReason.SuccessorRequiresSuccessOutcome, before);
        if (_hasCreatedNextGeneration)
            return RejectSuccessor(CookingLevelLifecycleReason.GenerationAlreadyCreated, before);
        if (newEpoch <= Scope.LevelEpoch)
            return RejectSuccessor(CookingLevelLifecycleReason.EpochNotAdvanced, before);

        var nextScope = new CookingLevelScope(newMatchScope, Scope.RestaurantRuntime, Scope.Level, newEpoch);
        return CreateNextGeneration(nextScope, "legacy-match-restarted", "created a legacy match restart generation", before);
    }

    public CookingLevelLifecycleResult RequireOwnerDecision(CookingOwnerDecision decision)
    {
        if (RejectBlockedLifecycleOperation() is { } reentrant)
            return reentrant;
        return Reject(CookingLevelLifecycleReason.BlockedByOwnerDecision);
    }

    public CookingLevelLifecycleSnapshot Snapshot() => new(
        Scope,
        _configuration.Identity.ToString(),
        _preparation?.Map,
        _preparation?.Layout?.Id,
        State,
        Outcome,
        Version,
        _hasCreatedNextGeneration);

    /// <summary>
    /// 恢复接缝：跨宿主重建一代 Level 后，把 checkpoint 携带的 lifecycle 版本续到新宿主对象上。
    /// 只推进版本计数、不改状态机语义（事件历史记录新宿主自身的操作，不搬运旧宿主的审计历史）。
    /// </summary>
    internal void AdoptRecoveredVersion(long version)
    {
        if (State is not (CookingLevelState.Preparing or CookingLevelState.Running))
            throw new InvalidOperationException("Only a preparing or running level generation can adopt a recovered lifecycle version.");
        if (version < Version)
            throw new ArgumentOutOfRangeException(nameof(version),
                "A recovered lifecycle version must not move the generation version backwards.");
        if (version > Version)
            Version = version;
    }

    internal static CookingLevelLifecycleReason ValidatePreparation(
        CookingLevelScope scope,
        CookingConfigurationSnapshot configuration,
        CookingLevelPreparation preparation,
        bool allowLevelMismatch = false)
    {
        if (preparation.Layout is null || preparation.Layout.ApplianceStations is null || preparation.Layout.Containers is null)
            return CookingLevelLifecycleReason.LayoutMalformed;
        if (string.IsNullOrWhiteSpace(preparation.Level.Value))
            return CookingLevelLifecycleReason.LevelIdMissing;
        if (!allowLevelMismatch && preparation.Level != scope.Level)
            return CookingLevelLifecycleReason.LevelIdMismatch;
        if (string.IsNullOrWhiteSpace(preparation.Map.Value))
            return CookingLevelLifecycleReason.MapIdMissing;
        if (string.IsNullOrWhiteSpace(preparation.Layout.Id.Value))
            return CookingLevelLifecycleReason.LayoutIdMissing;
        if (!Equals(preparation.ConfigIdentity, configuration.Identity))
            return CookingLevelLifecycleReason.ConfigIdentityMismatch;
        if (HasDuplicates(preparation.Layout.ApplianceStations.Select(station => station.Value)) ||
            HasDuplicates(preparation.Layout.Containers.Select(container => container.Value)))
            return CookingLevelLifecycleReason.DuplicateLayoutReference;
        if (preparation.Layout.ApplianceStations.Any(station => string.IsNullOrWhiteSpace(station.Value) ||
                !configuration.Appliances.ContainsKey(station)))
            return CookingLevelLifecycleReason.ApplianceNotFound;
        if (preparation.Layout.Containers.Any(container => string.IsNullOrWhiteSpace(container.Value) ||
                !configuration.Items.TryGetValue(container, out var containerDefinition) ||
                containerDefinition.Container is null))
            return CookingLevelLifecycleReason.ContainerNotFound;
        return CookingLevelLifecycleReason.None;
    }

    internal CookingLevelLifecycleReason TryCreateRetryCandidate(
        long newEpoch,
        out CookingLevelLifecycle? candidate)
    {
        candidate = null;
        var reason = ValidateRetryCandidate(newEpoch);
        if (reason != CookingLevelLifecycleReason.None)
            return reason;

        var nextScope = new CookingLevelScope(Scope.MatchScope, Scope.RestaurantRuntime, Scope.Level, newEpoch);
        candidate = new CookingLevelLifecycle(nextScope, _configuration, _gameplayFactory, _allowPreparationLevelMismatch);
        return CookingLevelLifecycleReason.None;
    }

    internal CookingLevelLifecycleReason TryCreateSuccessorCandidate(
        LevelId newLevelId,
        long newEpoch,
        out CookingLevelLifecycle? candidate)
    {
        candidate = null;
        var reason = ValidateSuccessorCandidate(newLevelId, newEpoch);
        if (reason != CookingLevelLifecycleReason.None)
            return reason;

        var nextScope = new CookingLevelScope(Scope.MatchScope, Scope.RestaurantRuntime, newLevelId, newEpoch);
        candidate = new CookingLevelLifecycle(nextScope, _configuration, _gameplayFactory, _allowPreparationLevelMismatch);
        return CookingLevelLifecycleReason.None;
    }

    internal CookingLevelSuccessorResult CommitRetryCandidate(CookingLevelLifecycle candidate) =>
        CommitCandidate(candidate, Scope.Level, "level-retry-created", "created a failed-level retry generation");

    internal CookingLevelSuccessorResult CommitSuccessorCandidate(CookingLevelLifecycle candidate) =>
        CommitCandidate(
            candidate,
            candidate.Scope.Level,
            "level-successor-created",
            "created the next successful level generation",
            requireDifferentLevel: true);

    /// <summary>
    /// 把已经裁好的成功交接厨房挂到这一代上。只允许还在 <see cref="CookingLevelState.Created"/>、
    /// 且尚未持有仿真的下一代调用。挂上后 <see cref="Start"/> 绑定这份厨房，不再向工厂新建。
    /// </summary>
    internal CookingLevelLifecycleReason AdoptSuccessorKitchen(CookingRecipeSimulation kitchen)
    {
        ArgumentNullException.ThrowIfNull(kitchen);
        if (State != CookingLevelState.Created || _gameplay is not null || _receivesSuccessorKitchen)
            return CookingLevelLifecycleReason.InvalidState;
        _gameplay = kitchen;
        _gameplayClosed = false;
        _receivesSuccessorKitchen = true;
        return CookingLevelLifecycleReason.None;
    }

    private CookingLevelSuccessorResult CommitCandidate(
        CookingLevelLifecycle candidate,
        LevelId expectedLevel,
        string eventType,
        string summary,
        bool requireDifferentLevel = false)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var before = Version;
        if (candidate.Scope.MatchScope != Scope.MatchScope ||
            candidate.Scope.RestaurantRuntime != Scope.RestaurantRuntime ||
            candidate.Scope.Level != expectedLevel ||
            (requireDifferentLevel && candidate.Scope.Level == Scope.Level) ||
            candidate.Scope.LevelEpoch <= Scope.LevelEpoch)
        {
            return RejectSuccessor(CookingLevelLifecycleReason.InvalidState, before);
        }
        if (_hasCreatedNextGeneration)
            return RejectSuccessor(CookingLevelLifecycleReason.GenerationAlreadyCreated, before);

        return CreateNextGeneration(candidate, eventType, summary, before);
    }

    private CookingLevelLifecycleReason ValidateRetryCandidate(long newEpoch)
    {
        if (_operationGate is not null && !_operationGate.IsLifecycleOperationOpen)
            return CookingLevelLifecycleReason.InvalidState;
        if (_isStarting)
        {
            _startReentered = true;
            return CookingLevelLifecycleReason.InvalidState;
        }
        if (State != CookingLevelState.Ended)
            return CookingLevelLifecycleReason.InvalidState;
        if (Outcome != CookingLevelOutcome.Failed)
            return CookingLevelLifecycleReason.RetryRequiresFailedOutcome;
        if (_hasCreatedNextGeneration)
            return CookingLevelLifecycleReason.GenerationAlreadyCreated;
        if (newEpoch <= Scope.LevelEpoch)
            return CookingLevelLifecycleReason.EpochNotAdvanced;
        return CookingLevelLifecycleReason.None;
    }

    private CookingLevelLifecycleReason ValidateSuccessorCandidate(LevelId newLevelId, long newEpoch)
    {
        if (_operationGate is not null && !_operationGate.IsLifecycleOperationOpen)
            return CookingLevelLifecycleReason.InvalidState;
        if (_isStarting)
        {
            _startReentered = true;
            return CookingLevelLifecycleReason.InvalidState;
        }
        if (State != CookingLevelState.Ended)
            return CookingLevelLifecycleReason.InvalidState;
        if (Outcome != CookingLevelOutcome.Success)
            return CookingLevelLifecycleReason.SuccessorRequiresSuccessOutcome;
        if (_hasCreatedNextGeneration)
            return CookingLevelLifecycleReason.GenerationAlreadyCreated;
        if (string.IsNullOrWhiteSpace(newLevelId.Value))
            return CookingLevelLifecycleReason.SuccessorLevelIdMissing;
        if (newLevelId == Scope.Level)
            return CookingLevelLifecycleReason.SuccessorLevelIdUnchanged;
        if (newEpoch <= Scope.LevelEpoch)
            return CookingLevelLifecycleReason.EpochNotAdvanced;
        return CookingLevelLifecycleReason.None;
    }

    private CookingLevelSuccessorResult CreateNextGeneration(
        CookingLevelLifecycle next,
        string eventType,
        string summary,
        long before)
    {
        _hasCreatedNextGeneration = true;
        var sourceResult = Commit(State, eventType, summary);
        return new CookingLevelSuccessorResult(
            true,
            CookingLevelLifecycleReason.None,
            next,
            Scope,
            Outcome,
            next.Scope,
            before,
            Version,
            _gameplay is null || _gameplayClosed,
            sourceResult);
    }

    private CookingLevelSuccessorResult CreateNextGeneration(
        CookingLevelScope nextScope,
        string eventType,
        string summary,
        long before) =>
        CreateNextGeneration(
            new CookingLevelLifecycle(nextScope, _configuration, _gameplayFactory, _allowPreparationLevelMismatch),
            eventType,
            summary,
            before);

    private CookingLevelSuccessorResult RejectSuccessor(CookingLevelLifecycleReason reason, long before) =>
        new(false, reason, null, Scope, Outcome, null, before, Version, _gameplay is null || _gameplayClosed, Reject(reason));

    internal static CookingLevelPreparation CopyPreparation(CookingLevelPreparation preparation) =>
        new(
            preparation.Level,
            preparation.Map,
            preparation.Layout is null
                ? null!
                : new CookingLogicalLayout(
                    preparation.Layout.Id,
                    preparation.Layout.ApplianceStations?.ToArray()!,
                    preparation.Layout.Containers?.ToArray()!),
            preparation.ConfigIdentity);

    private static bool HasDuplicates(IEnumerable<string> values) =>
        values.GroupBy(value => value, StringComparer.Ordinal).Any(group => group.Count() > 1);

    private CookingLevelLifecycleResult Commit(CookingLevelState state, string type, string summary)
    {
        State = state;
        Version++;
        var @event = new CookingLevelLifecycleEvent(
            ++_eventSequence,
            Version,
            State,
            Outcome,
            type,
            CookingLevelLifecycleReason.None,
            summary);
        _events.Add(@event);
        return new CookingLevelLifecycleResult(
            true,
            CookingLevelLifecycleReason.None,
            State,
            Outcome,
            Version,
            CookingLevelLifecycleResult.ReadOnlyEvents(new[] { @event }));
    }

    private CookingLevelLifecycleResult? RejectBlockedLifecycleOperation()
    {
        if (_operationGate is not null && !_operationGate.IsLifecycleOperationOpen)
            return Reject(CookingLevelLifecycleReason.InvalidState);
        if (!_isStarting)
            return null;
        _startReentered = true;
        return Reject(CookingLevelLifecycleReason.InvalidState);
    }

    internal CookingLevelLifecycleResult CompleteEndForCompatibility(CookingLevelOutcome outcome)
    {
        var begin = BeginEnd(outcome);
        return begin.Accepted ? CompleteEnd() : begin;
    }

    private sealed class LifecycleGameplayGate : ICookingRecipeLifecycleGate
    {
        private readonly CookingLevelLifecycle _owner;

        public LifecycleGameplayGate(CookingLevelLifecycle owner) => _owner = owner;

        public bool IsGameplayMutationOpen =>
            (_owner.IsGameplayAdmissionOpen || _owner.IsPreparationAdmissionOpen) &&
            (_owner._ownerGameplayGate?.IsGameplayMutationOpen ?? true);

        public CookingLevelScope LevelScope => _owner.Scope;

        public bool IsLayoutInstallationOpen => !_owner._gameplayClosed &&
            _owner.State is CookingLevelState.Created or CookingLevelState.Preparing;
    }

    private CookingLevelLifecycleResult Reject(CookingLevelLifecycleReason reason) =>
        CookingLevelLifecycleResult.Reject(reason, State, Outcome, Version);
}
