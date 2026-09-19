using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;

namespace AbilityKit.Game.Cooking;

[Obsolete("Use CookingLevelPreparation.")]
public sealed record CookingMatchPreparation(
    LevelId Level,
    MapId Map,
    CookingLogicalLayout Layout,
    CookingConfigurationIdentity ConfigIdentity);

[Obsolete("Use CookingLevelState.")]
public enum CookingMatchState
{
    Preparing,
    Ready,
    Started,
    Ended,
    Unsupported,
}

[Obsolete("Use CookingLevelLifecycleReason.")]
public enum CookingMatchLifecycleReason
{
    None,
    InvalidState,
    LevelIdMissing,
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
    BlockedByOwnerDecision,
}

public enum CookingOwnerDecision
{
    HostExit,
    RemoteLoss,
    Reconnect,
    HostMigration,
    SaveAndSettlement,
    RoomCapacity,
}

[Obsolete("Use CookingLevelLifecycleEvent.")]
public sealed record CookingMatchLifecycleEvent(
    long Sequence,
    long Version,
    CookingMatchState State,
    string Type,
    CookingMatchLifecycleReason Reason,
    string Summary);

[Obsolete("Use CookingLevelLifecycleResult.")]
public sealed record CookingMatchLifecycleResult(
    bool Accepted,
    CookingMatchLifecycleReason Reason,
    CookingMatchState State,
    long Version,
    IReadOnlyList<CookingMatchLifecycleEvent> Events)
{
    public static CookingMatchLifecycleResult Reject(
        CookingMatchLifecycleReason reason,
        CookingMatchState state,
        long version) =>
        new(false, reason, state, version, Array.Empty<CookingMatchLifecycleEvent>());
}

[Obsolete("Use CookingLevelSuccessorResult.")]
public sealed record CookingMatchRestartResult(
    bool Accepted,
    CookingMatchLifecycleReason Reason,
    CookingMatchLifecycle? NewMatch,
    CookingMatchLifecycleResult SourceResult);

[Obsolete("Use ICookingLevelGameplayFactory.")]
public interface ICookingMatchGameplayFactory
{
    CookingRecipeSimulation Create(CookingScope scope, CookingConfigurationSnapshot configuration);
}

[Obsolete("Use CookingLevelLifecycleSnapshot.")]
public sealed record CookingMatchLifecycleSnapshot(
    CookingScope Scope,
    long Epoch,
    string ConfigIdentity,
    LevelId? Level,
    MapId? Map,
    LayoutId? Layout,
    CookingMatchState State,
    long Version);

[Obsolete("Use CookingLevelLifecycleSnapshotApplyResult.")]
public sealed record CookingLifecycleSnapshotApplyResult(
    bool Accepted,
    CookingMatchLifecycleReason Reason,
    CookingMatchLifecycleSnapshot? Current);

[Obsolete("Use CookingLevelLifecycleSnapshotApplier.")]
public sealed class CookingLifecycleSnapshotApplier
{
    private readonly CookingScope _scope;
    private readonly long _epoch;
    private readonly string _configIdentity;

    public CookingLifecycleSnapshotApplier(
        CookingScope scope,
        long epoch,
        CookingConfigurationIdentity configIdentity)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(configIdentity);
        if (epoch <= 0)
            throw new ArgumentOutOfRangeException(nameof(epoch));

        _scope = scope;
        _epoch = epoch;
        _configIdentity = configIdentity.ToString();
    }

    public CookingMatchLifecycleSnapshot? Current { get; private set; }

    public CookingLifecycleSnapshotApplyResult Apply(CookingMatchLifecycleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!Equals(snapshot.Scope, _scope))
            return Reject(CookingMatchLifecycleReason.ScopeMismatch);
        if (snapshot.Epoch != _epoch)
            return Reject(CookingMatchLifecycleReason.EpochMismatch);
        if (!StringComparer.Ordinal.Equals(snapshot.ConfigIdentity, _configIdentity))
            return Reject(CookingMatchLifecycleReason.SnapshotIdentityMismatch);
        if (Current is null)
        {
            if (snapshot.Version != 1 || snapshot.State != CookingMatchState.Ready)
                return Reject(CookingMatchLifecycleReason.SnapshotSequenceGap);
            Current = snapshot;
            return new CookingLifecycleSnapshotApplyResult(true, CookingMatchLifecycleReason.None, Current);
        }
        if (snapshot.Version == Current.Version)
            return Reject(CookingMatchLifecycleReason.SnapshotSequenceDuplicate);
        if (snapshot.Version < Current.Version)
            return Reject(CookingMatchLifecycleReason.SnapshotSequenceStale);
        if (snapshot.Version != Current.Version + 1 || !IsValidTransition(Current.State, snapshot.State))
            return Reject(CookingMatchLifecycleReason.SnapshotSequenceGap);

        Current = snapshot;
        return new CookingLifecycleSnapshotApplyResult(true, CookingMatchLifecycleReason.None, Current);
    }

    private static bool IsValidTransition(CookingMatchState current, CookingMatchState next) =>
        (current, next) switch
        {
            (CookingMatchState.Ready, CookingMatchState.Started) => true,
            (CookingMatchState.Started, CookingMatchState.Ended) => true,
            _ => false,
        };

    private CookingLifecycleSnapshotApplyResult Reject(CookingMatchLifecycleReason reason) =>
        new(false, reason, Current);
}

[Obsolete("Use CookingLevelLifecycle. CookingMatchLifecycle is a compatibility facade over the canonical Level lifecycle.")]
public sealed class CookingMatchLifecycle
{
    private readonly CookingScope _scope;
    private readonly long _epoch;
    private readonly CookingConfigurationSnapshot _configuration;
    private readonly ICookingLevelGameplayFactory _gameplayFactory;
    private CookingLevelLifecycle? _inner;

    public CookingMatchLifecycle(
        CookingScope scope,
        long epoch,
        CookingConfigurationSnapshot configuration,
        ICookingMatchGameplayFactory gameplayFactory)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        if (epoch <= 0)
            throw new ArgumentOutOfRangeException(nameof(epoch));
        _epoch = epoch;
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _gameplayFactory = new MatchGameplayFactoryAdapter(gameplayFactory);
    }

    private CookingMatchLifecycle(CookingLevelLifecycle inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _scope = inner.Scope.MatchScope;
        _epoch = inner.Scope.LevelEpoch;
        _configuration = inner.Configuration;
        _gameplayFactory = inner.GameplayFactory;
    }

    public CookingScope Scope => _scope;
    public long Epoch => _epoch;
    public CookingMatchState State => _inner is null ? CookingMatchState.Preparing : ProjectState(_inner.State);
    public long Version => CountLegacyVisibleEvents();
    public IReadOnlyList<CookingMatchLifecycleEvent> EventHistory =>
        _inner is null ? Array.Empty<CookingMatchLifecycleEvent>() : ProjectEvents(_inner.EventHistory);
    public bool IsGameplayAdmissionOpen => _inner?.IsGameplayAdmissionOpen == true;

    internal CookingLevelLifecycle? CanonicalLifecycle => _inner;

    public bool TryGetGameplay(out CookingRecipeSimulation gameplay)
    {
        if (_inner is null)
        {
            gameplay = default!;
            return false;
        }

        return _inner.TryGetGameplay(out gameplay);
    }

    public CookingMatchLifecycleResult Prepare(CookingMatchPreparation preparation)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        if (_inner is not null && _inner.State != CookingLevelState.Created)
            return Reject(CookingMatchLifecycleReason.InvalidState);

        var candidate = ToLevelPreparation(preparation);
        var lifecycle = _inner ?? new CookingLevelLifecycle(
            CreateCompatibilityScope(_scope, preparation.Level, _epoch),
            _configuration,
            _gameplayFactory);
        var validation = CookingLevelLifecycle.ValidatePreparation(lifecycle.Scope, _configuration, candidate);
        if (validation != CookingLevelLifecycleReason.None)
            return Reject(ProjectReason(validation));

        var begin = lifecycle.BeginPreparation(candidate);
        if (!begin.Accepted)
            return Reject(ProjectReason(begin.Reason));
        var complete = lifecycle.CompletePreparation();
        if (!complete.Accepted)
            return Reject(ProjectReason(complete.Reason));

        _inner = lifecycle;
        return ProjectResult(complete, "match-prepared", "validated logical layout and configuration identity");
    }

    public CookingMatchLifecycleResult Start()
    {
        if (_inner?.State != CookingLevelState.Ready)
            return Reject(CookingMatchLifecycleReason.InvalidState);
        return ProjectResult(_inner.Start(), "match-started", "created one isolated gameplay instance");
    }

    public CookingMatchLifecycleResult End()
    {
        if (_inner?.State != CookingLevelState.Running)
            return Reject(CookingMatchLifecycleReason.InvalidState);

        return ProjectResult(_inner.CompleteEndForCompatibility(CookingLevelOutcome.Success),
            "match-ended", "closed gameplay admission without persistence or settlement");
    }

    public CookingMatchRestartResult Restart(CookingScope newScope, long newEpoch)
    {
        ArgumentNullException.ThrowIfNull(newScope);
        if (_inner?.State != CookingLevelState.Ended)
            return RejectRestart(CookingMatchLifecycleReason.InvalidState);
        if (string.IsNullOrWhiteSpace(newScope.Match.Value) ||
            newScope.Session != Scope.Session ||
            newScope.World != Scope.World ||
            newScope.Match == Scope.Match ||
            newEpoch <= Epoch)
        {
            return RejectRestart(CookingMatchLifecycleReason.InvalidState);
        }

        var successor = _inner.CreateCompatibilityRestart(newScope, newEpoch);
        if (!successor.Accepted || successor.NewLevel is null)
            return RejectRestart(ProjectReason(successor.Reason));

        var next = new CookingMatchLifecycle(successor.NewLevel);
        var sourceResult = ProjectResult(
            successor.SourceResult,
            "match-restarted",
            "created a new match identity and epoch");
        return new CookingMatchRestartResult(true, CookingMatchLifecycleReason.None, next, sourceResult);
    }

    public CookingMatchLifecycleResult RequireOwnerDecision(CookingOwnerDecision decision) =>
        _inner is null
            ? Reject(CookingMatchLifecycleReason.BlockedByOwnerDecision)
            : ProjectResult(_inner.RequireOwnerDecision(decision));

    public CookingMatchLifecycleSnapshot Snapshot()
    {
        if (_inner is null)
        {
            return new CookingMatchLifecycleSnapshot(
                Scope,
                Epoch,
                _configuration.Identity.ToString(),
                null,
                null,
                null,
                CookingMatchState.Preparing,
                0);
        }

        var snapshot = _inner.Snapshot();
        return new CookingMatchLifecycleSnapshot(
            Scope,
            Epoch,
            snapshot.ConfigIdentity,
            _inner.Preparation?.Level,
            snapshot.Map,
            snapshot.Layout,
            ProjectState(snapshot.State),
            Version);
    }

    private CookingMatchRestartResult RejectRestart(CookingMatchLifecycleReason reason) =>
        new(false, reason, null, Reject(reason));

    private CookingMatchLifecycleResult Reject(CookingMatchLifecycleReason reason) =>
        CookingMatchLifecycleResult.Reject(reason, State, Version);

    private CookingMatchLifecycleResult ProjectResult(
        CookingLevelLifecycleResult result,
        string? acceptedType = null,
        string? acceptedSummary = null)
    {
        if (!result.Accepted)
            return Reject(ProjectReason(result.Reason));

        var projectedEvents = result.Events
            .Where(@event => IsLegacyVisibleEvent(@event.Type, acceptedType))
            .Select(@event => ProjectEvent(@event, acceptedType, acceptedSummary, Version));
        return new CookingMatchLifecycleResult(
            true,
            CookingMatchLifecycleReason.None,
            State,
            Version,
            ReadOnlyEvents(projectedEvents));
    }

    private static IReadOnlyList<CookingMatchLifecycleEvent> ReadOnlyEvents(
        IEnumerable<CookingMatchLifecycleEvent> events) =>
        new ReadOnlyCollection<CookingMatchLifecycleEvent>(events.ToArray());

    private IReadOnlyList<CookingMatchLifecycleEvent> ProjectEvents(IEnumerable<CookingLevelLifecycleEvent> events)
    {
        var visible = events.Where(@event => IsLegacyVisibleEvent(@event.Type, null)).ToArray();
        return ReadOnlyEvents(visible.Select((@event, index) => ProjectEvent(@event, null, null, index + 1)));
    }

    private CookingMatchLifecycleEvent ProjectEvent(
        CookingLevelLifecycleEvent @event,
        string? acceptedType,
        string? acceptedSummary,
        long legacyVersion)
    {
        var type = acceptedType ?? @event.Type switch
        {
            "level-prepared" => "match-prepared",
            "level-started" => "match-started",
            "level-ended" => "match-ended",
            "level-successor-created" or "legacy-match-restarted" => "match-restarted",
            _ => @event.Type,
        };
        var summary = acceptedSummary ?? @event.Summary;
        return new CookingMatchLifecycleEvent(
            legacyVersion,
            legacyVersion,
            ProjectState(@event.State),
            type,
            ProjectReason(@event.Reason),
            summary);
    }

    private static bool IsLegacyVisibleEvent(string eventType, string? acceptedType) =>
        acceptedType is not null || eventType is "level-prepared" or "level-started" or "level-ended" or
            "level-successor-created" or "legacy-match-restarted";

    private long CountLegacyVisibleEvents() =>
        _inner?.EventHistory.LongCount(@event => IsLegacyVisibleEvent(@event.Type, null)) ?? 0;

    private static CookingMatchState ProjectState(CookingLevelState state) => state switch
    {
        CookingLevelState.Created or CookingLevelState.Preparing => CookingMatchState.Preparing,
        CookingLevelState.Ready => CookingMatchState.Ready,
        CookingLevelState.Running => CookingMatchState.Started,
        CookingLevelState.Ended => CookingMatchState.Ended,
        CookingLevelState.Paused or CookingLevelState.Ending => CookingMatchState.Unsupported,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    private static CookingMatchLifecycleReason ProjectReason(CookingLevelLifecycleReason reason) => reason switch
    {
        CookingLevelLifecycleReason.None => CookingMatchLifecycleReason.None,
        CookingLevelLifecycleReason.LevelIdMissing => CookingMatchLifecycleReason.LevelIdMissing,
        CookingLevelLifecycleReason.MapIdMissing => CookingMatchLifecycleReason.MapIdMissing,
        CookingLevelLifecycleReason.LayoutIdMissing => CookingMatchLifecycleReason.LayoutIdMissing,
        CookingLevelLifecycleReason.LayoutMalformed => CookingMatchLifecycleReason.LayoutMalformed,
        CookingLevelLifecycleReason.ConfigIdentityMismatch => CookingMatchLifecycleReason.ConfigIdentityMismatch,
        CookingLevelLifecycleReason.DuplicateLayoutReference => CookingMatchLifecycleReason.DuplicateLayoutReference,
        CookingLevelLifecycleReason.ApplianceNotFound => CookingMatchLifecycleReason.ApplianceNotFound,
        CookingLevelLifecycleReason.ContainerNotFound => CookingMatchLifecycleReason.ContainerNotFound,
        CookingLevelLifecycleReason.GameplayInitializationFailed => CookingMatchLifecycleReason.GameplayInitializationFailed,
        CookingLevelLifecycleReason.GameplayUnavailable => CookingMatchLifecycleReason.GameplayUnavailable,
        CookingLevelLifecycleReason.ScopeMismatch => CookingMatchLifecycleReason.ScopeMismatch,
        CookingLevelLifecycleReason.EpochMismatch => CookingMatchLifecycleReason.EpochMismatch,
        CookingLevelLifecycleReason.SnapshotIdentityMismatch => CookingMatchLifecycleReason.SnapshotIdentityMismatch,
        CookingLevelLifecycleReason.SnapshotSequenceDuplicate => CookingMatchLifecycleReason.SnapshotSequenceDuplicate,
        CookingLevelLifecycleReason.SnapshotSequenceStale => CookingMatchLifecycleReason.SnapshotSequenceStale,
        CookingLevelLifecycleReason.SnapshotSequenceGap => CookingMatchLifecycleReason.SnapshotSequenceGap,
        CookingLevelLifecycleReason.BlockedByOwnerDecision => CookingMatchLifecycleReason.BlockedByOwnerDecision,
        _ => CookingMatchLifecycleReason.InvalidState,
    };

    private static CookingLevelPreparation ToLevelPreparation(CookingMatchPreparation preparation) =>
        new(preparation.Level, preparation.Map, preparation.Layout, preparation.ConfigIdentity);

    private static CookingLevelScope CreateCompatibilityScope(CookingScope scope, LevelId level, long epoch) =>
        new(scope, new RestaurantRuntimeId(1), level, epoch);

    private sealed class MatchGameplayFactoryAdapter : ICookingLevelGameplayFactory
    {
        private readonly ICookingMatchGameplayFactory _inner;

        public MatchGameplayFactoryAdapter(ICookingMatchGameplayFactory inner) =>
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration) =>
            _inner.Create(scope.MatchScope, configuration);
    }
}

[Obsolete("Compatibility evidence for CookingMatchLifecycle. Prefer Level lifecycle test artifacts.")]
public sealed record CookingMatchLifecycleAcceptanceEvidence(
    string TestId,
    string Scope,
    long Epoch,
    string Operation,
    bool Accepted,
    string Reason,
    string BeforeSnapshot,
    string AfterSnapshot,
    string AssertionSummary,
    string Runner,
    string TimestampUtc);

[Obsolete("Compatibility evidence for CookingMatchLifecycle. Prefer Level lifecycle test artifacts.")]
public static class CookingMatchLifecycleAcceptanceEvidenceWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static void Append(string path, CookingMatchLifecycleAcceptanceEvidence evidence)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new ArgumentException("Evidence path has no directory.", nameof(path)));
        File.AppendAllText(path, JsonSerializer.Serialize(evidence, Options) + Environment.NewLine, Encoding.UTF8);
    }

    public static IReadOnlyList<CookingMatchLifecycleAcceptanceEvidence> ReadAll(string path) =>
        File.ReadLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<CookingMatchLifecycleAcceptanceEvidence>(line, Options)
                ?? throw new InvalidDataException("Invalid cooking match lifecycle evidence line."))
            .ToArray();
}
