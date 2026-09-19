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
    IReadOnlyList<CookingLevelPendingDisposition> Dispositions);

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

    public CookingLevelEtHost(
        CookingLevelLifecycle lifecycle,
        int queueCapacity = 256,
        ICookingLevelEtHostFailureInjector? failureInjector = null)
    {
        _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
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
    public long HostFrameSequence { get; private set; }
    public long LastCommittedSimulationBatch { get; private set; }
    public int PendingCommandIdentityCount => _pending.Count;
    public bool IsFaulted => _tickFailure is not null;
    public IReadOnlyList<CookingLevelPendingDisposition> DispositionHistory =>
        new ReadOnlyCollection<CookingLevelPendingDisposition>(_history.ToArray());
    public IReadOnlyList<CookingLevelPendingDisposition> FinalDispositionHistory => _finalHistory;

    public CookingLevelHostOperationResult Prepare(CookingLevelPreparation preparation)
    {
        Check();
        ArgumentNullException.ThrowIfNull(preparation);
        var begin = RunLifecycleOperation(() => _lifecycle.BeginPreparation(preparation));
        if (!begin.Accepted)
            return Operation(begin);
        return Operation(RunLifecycleOperation(_lifecycle.CompletePreparation));
    }

    public CookingLevelHostOperationResult Start()
    {
        Check();
        var result = RunLifecycleOperation(() => _lifecycle.Start(_gameplayPublicationGuard));
        if (!result.Accepted)
            return Operation(result);

        if (!_lifecycle.TryGetGameplay(out var simulation))
            throw Fault(new InvalidOperationException("A running CookingLevelLifecycle did not expose its gameplay simulation."));

        try
        {
            _failureInjector?.ThrowIfRequested(CookingLevelEtHostFailurePoint.SimulationOwnershipAcquired);
            simulation.BindAuthorityGate(_authorityGate);
            _failureInjector?.ThrowIfRequested(CookingLevelEtHostFailurePoint.BeforeSimulationPublish);
            _ownedSimulation = simulation;
            Driver.Simulation = simulation;
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

        return Operation(result);
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
        if (state != CookingLevelState.Running || _ownedSimulation is null)
            return CookingLevelAdmissionResult.Reject(CookingLevelAdmissionReason.LevelNotRunning);
        if (!Equals(envelope.LevelScope, Binding.LevelScope) ||
            !Equals(envelope.Command.Scope, Binding.LegacySimulationScope))
        {
            return CookingLevelAdmissionResult.Reject(CookingLevelAdmissionReason.ScopeMismatch);
        }
        if (!CookingRecipeCommandValidation.IsWellFormed(envelope.Command))
            return CookingLevelAdmissionResult.Reject(CookingLevelAdmissionReason.MalformedCommand);
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
        if (state != CookingLevelState.Running || _ownedSimulation is null)
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

    public CookingLevelHostOperationResult BeginEnd(CookingLevelOutcome outcome)
    {
        Check();
        var result = RunLifecycleOperation(() => _lifecycle.BeginEnd(outcome));
        var dispositions = result.Accepted ? CancelPending("level-ending") : Array.Empty<CookingLevelPendingDisposition>();
        return Operation(result, dispositions);
    }

    public CookingLevelHostOperationResult CompleteEnd()
    {
        Check();
        var result = RunLifecycleOperation(_lifecycle.CompleteEnd);
        if (result.Accepted)
        {
            ReleaseSimulationOwnership();
            RemoveLevel();
        }
        return Operation(result);
    }

    public CookingLevelHostGenerationResult CreateRetry(long newEpoch)
    {
        Check();
        var candidateResult = RunLifecycleOperation(() =>
        {
            var reason = _lifecycle.TryCreateRetryCandidate(newEpoch, out var candidate);
            return new CandidateResult(reason, candidate);
        });
        return candidateResult.Reason == CookingLevelLifecycleReason.None
            ? InstallGeneration(candidateResult.Candidate!, isRetry: true)
            : RejectGeneration(candidateResult.Reason);
    }

    public CookingLevelHostGenerationResult CreateSuccessor(LevelId newLevelId, long newEpoch)
    {
        Check();
        var candidateResult = RunLifecycleOperation(() =>
        {
            var reason = _lifecycle.TryCreateSuccessorCandidate(newLevelId, newEpoch, out var candidate);
            return new CandidateResult(reason, candidate);
        });
        return candidateResult.Reason == CookingLevelLifecycleReason.None
            ? InstallGeneration(candidateResult.Candidate!, isRetry: false)
            : RejectGeneration(candidateResult.Reason);
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

        try
        {
            _runtime.Run(SceneId, _ =>
            {
                if (oldLevel is not null && !oldLevel.IsDisposed)
                    RestaurantRuntime.RemoveComponent<CookingLevelComponent>();
                InstallLevel(candidate);
            });
        }
        catch (Exception exception)
        {
            _lifecycle = sourceLifecycle;
            Binding = sourceBinding;
            throw Fault(new InvalidOperationException("The replacement level generation could not be installed.", exception));
        }

        var committed = RunLifecycleOperation(() => isRetry
            ? sourceLifecycle.CommitRetryCandidate(candidate)
            : sourceLifecycle.CommitSuccessorCandidate(candidate));
        if (!committed.Accepted)
        {
            RemoveLevel();
            throw Fault(new InvalidOperationException(
                $"The installed replacement level generation could not be committed: {committed.Reason}."));
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
        if (_ticking)
            throw new InvalidOperationException("Cooking level host operations cannot be reentered.");
        if (_tickFailure is not null)
            throw new InvalidOperationException("Cooking level host is faulted.", _tickFailure);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        if (Environment.CurrentManagedThreadId != _ownerThread || _ticking)
            throw new InvalidOperationException("Dispose requires the idle owner thread.");

        CancelPending("host-disposed");
        _finalHistory = new ReadOnlyCollection<CookingLevelPendingDisposition>(Sort(_history).ToArray());
        try
        {
            if (_lifecycle.State == CookingLevelState.Ending)
                RunLifecycleOperation(_lifecycle.CompleteEnd);
            else if (_lifecycle.State != CookingLevelState.Ended)
                BestEffortAbortLifecycle();
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

    private sealed class HostAuthorityGate : ICookingRecipeAuthorityGate
    {
        private readonly CookingLevelEtHost _host;

        public HostAuthorityGate(CookingLevelEtHost host) => _host = host;

        public bool IsAuthorityMutationOpen =>
            _host._ticking && _host._executingAuthorityMutation && _host._tickFailure is null && !_host._disposed;
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
