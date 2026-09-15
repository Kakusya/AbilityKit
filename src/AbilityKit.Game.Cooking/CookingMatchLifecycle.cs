using System.Text;
using System.Text.Json;

namespace AbilityKit.Game.Cooking;

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
    IReadOnlyList<ContainerId> Containers);

public sealed record CookingMatchPreparation(
    LevelId Level,
    MapId Map,
    CookingLogicalLayout Layout,
    CookingConfigurationIdentity ConfigIdentity);

public enum CookingMatchState
{
    Preparing,
    Ready,
    Started,
    Ended,
}

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

public sealed record CookingMatchLifecycleEvent(
    long Sequence,
    long Version,
    CookingMatchState State,
    string Type,
    CookingMatchLifecycleReason Reason,
    string Summary);

public sealed record CookingMatchLifecycleResult(
    bool Accepted,
    CookingMatchLifecycleReason Reason,
    CookingMatchState State,
    long Version,
    IReadOnlyList<CookingMatchLifecycleEvent> Events)
{
    public static CookingMatchLifecycleResult Reject(CookingMatchLifecycleReason reason, CookingMatchState state, long version) =>
        new(false, reason, state, version, Array.Empty<CookingMatchLifecycleEvent>());
}

public sealed record CookingMatchRestartResult(
    bool Accepted,
    CookingMatchLifecycleReason Reason,
    CookingMatchLifecycle? NewMatch,
    CookingMatchLifecycleResult SourceResult);

public interface ICookingMatchGameplayFactory
{
    CookingRecipeSimulation Create(CookingScope scope, CookingConfigurationSnapshot configuration);
}

public sealed record CookingMatchLifecycleSnapshot(
    CookingScope Scope,
    long Epoch,
    string ConfigIdentity,
    LevelId? Level,
    MapId? Map,
    LayoutId? Layout,
    CookingMatchState State,
    long Version);

public sealed record CookingLifecycleSnapshotApplyResult(
    bool Accepted,
    CookingMatchLifecycleReason Reason,
    CookingMatchLifecycleSnapshot? Current);

public sealed class CookingLifecycleSnapshotApplier
{
    private readonly CookingScope _scope;
    private readonly long _epoch;
    private readonly string _configIdentity;

    public CookingLifecycleSnapshotApplier(CookingScope scope, long epoch, CookingConfigurationIdentity configIdentity)
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

public sealed class CookingMatchLifecycle
{
    private readonly CookingConfigurationSnapshot _configuration;
    private readonly ICookingMatchGameplayFactory _gameplayFactory;
    private readonly List<CookingMatchLifecycleEvent> _events = new();
    private long _eventSequence;
    private CookingMatchPreparation? _preparation;
    private CookingRecipeSimulation? _gameplay;

    public CookingMatchLifecycle(
        CookingScope scope,
        long epoch,
        CookingConfigurationSnapshot configuration,
        ICookingMatchGameplayFactory gameplayFactory)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(gameplayFactory);
        if (epoch <= 0)
            throw new ArgumentOutOfRangeException(nameof(epoch));

        Scope = scope;
        Epoch = epoch;
        _configuration = configuration;
        _gameplayFactory = gameplayFactory;
    }

    public CookingScope Scope { get; }
    public long Epoch { get; }
    public CookingMatchState State { get; private set; } = CookingMatchState.Preparing;
    public long Version { get; private set; }
    public IReadOnlyList<CookingMatchLifecycleEvent> EventHistory => _events.AsReadOnly();
    public bool IsGameplayAdmissionOpen => State == CookingMatchState.Started && _gameplay is not null;

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

    public CookingMatchLifecycleResult Prepare(CookingMatchPreparation preparation)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        if (State != CookingMatchState.Preparing)
            return Reject(CookingMatchLifecycleReason.InvalidState);
        var validation = ValidatePreparation(preparation);
        if (validation != CookingMatchLifecycleReason.None)
            return Reject(validation);

        _preparation = new CookingMatchPreparation(preparation.Level, preparation.Map,
            new CookingLogicalLayout(preparation.Layout.Id, preparation.Layout.ApplianceStations.ToArray(),
                preparation.Layout.Containers.ToArray()), preparation.ConfigIdentity);
        return Commit(CookingMatchState.Ready, "match-prepared", "validated logical layout and configuration identity");
    }

    public CookingMatchLifecycleResult Start()
    {
        if (State != CookingMatchState.Ready)
            return Reject(CookingMatchLifecycleReason.InvalidState);

        CookingRecipeSimulation gameplay;
        try
        {
            gameplay = _gameplayFactory.Create(Scope, _configuration);
        }
        catch (Exception)
        {
            return Reject(CookingMatchLifecycleReason.GameplayInitializationFailed);
        }
        if (gameplay is null)
            return Reject(CookingMatchLifecycleReason.GameplayInitializationFailed);

        _gameplay = gameplay;
        return Commit(CookingMatchState.Started, "match-started", "created one isolated gameplay instance");
    }

    public CookingMatchLifecycleResult End()
    {
        if (State != CookingMatchState.Started)
            return Reject(CookingMatchLifecycleReason.InvalidState);
        var result = Commit(CookingMatchState.Ended, "match-ended", "closed gameplay admission without persistence or settlement");
        _gameplay!.CloseLifecycle();
        return result;
    }

    public CookingMatchRestartResult Restart(CookingScope newScope, long newEpoch)
    {
        ArgumentNullException.ThrowIfNull(newScope);
        if (State != CookingMatchState.Ended)
            return new CookingMatchRestartResult(false, CookingMatchLifecycleReason.InvalidState, null,
                Reject(CookingMatchLifecycleReason.InvalidState));
        if (newScope.Session != Scope.Session || newScope.World != Scope.World || newScope.Match == Scope.Match || newEpoch <= Epoch)
            return new CookingMatchRestartResult(false, CookingMatchLifecycleReason.InvalidState, null,
                Reject(CookingMatchLifecycleReason.InvalidState));

        var sourceResult = Commit(CookingMatchState.Ended, "match-restarted", "created a new match identity and epoch");
        var next = new CookingMatchLifecycle(newScope, newEpoch, _configuration, _gameplayFactory);
        return new CookingMatchRestartResult(true, CookingMatchLifecycleReason.None, next, sourceResult);
    }

    public CookingMatchLifecycleResult RequireOwnerDecision(CookingOwnerDecision decision) =>
        Reject(CookingMatchLifecycleReason.BlockedByOwnerDecision);

    public CookingMatchLifecycleSnapshot Snapshot() => new(
        Scope,
        Epoch,
        _configuration.Identity.ToString(),
        _preparation?.Level,
        _preparation?.Map,
        _preparation?.Layout.Id,
        State,
        Version);

    private CookingMatchLifecycleReason ValidatePreparation(CookingMatchPreparation preparation)
    {
        if (preparation.Layout is null || preparation.Layout.ApplianceStations is null || preparation.Layout.Containers is null)
            return CookingMatchLifecycleReason.LayoutMalformed;
        if (string.IsNullOrWhiteSpace(preparation.Level.Value))
            return CookingMatchLifecycleReason.LevelIdMissing;
        if (string.IsNullOrWhiteSpace(preparation.Map.Value))
            return CookingMatchLifecycleReason.MapIdMissing;
        if (string.IsNullOrWhiteSpace(preparation.Layout.Id.Value))
            return CookingMatchLifecycleReason.LayoutIdMissing;
        if (!Equals(preparation.ConfigIdentity, _configuration.Identity))
            return CookingMatchLifecycleReason.ConfigIdentityMismatch;
        if (HasDuplicates(preparation.Layout.ApplianceStations.Select(station => station.Value)) ||
            HasDuplicates(preparation.Layout.Containers.Select(container => container.Value)))
            return CookingMatchLifecycleReason.DuplicateLayoutReference;
        if (preparation.Layout.ApplianceStations.Any(station => string.IsNullOrWhiteSpace(station.Value) ||
                !_configuration.Appliances.ContainsKey(station)))
            return CookingMatchLifecycleReason.ApplianceNotFound;
        if (preparation.Layout.Containers.Any(container => string.IsNullOrWhiteSpace(container.Value) ||
                !_configuration.Containers.ContainsKey(container)))
            return CookingMatchLifecycleReason.ContainerNotFound;
        return CookingMatchLifecycleReason.None;
    }

    private static bool HasDuplicates(IEnumerable<string> values) =>
        values.GroupBy(value => value, StringComparer.Ordinal).Any(group => group.Count() > 1);

    private CookingMatchLifecycleResult Commit(CookingMatchState state, string type, string summary)
    {
        State = state;
        Version++;
        var @event = new CookingMatchLifecycleEvent(++_eventSequence, Version, State, type,
            CookingMatchLifecycleReason.None, summary);
        _events.Add(@event);
        return new CookingMatchLifecycleResult(true, CookingMatchLifecycleReason.None, State, Version, new[] { @event });
    }

    private CookingMatchLifecycleResult Reject(CookingMatchLifecycleReason reason) =>
        CookingMatchLifecycleResult.Reject(reason, State, Version);
}

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
