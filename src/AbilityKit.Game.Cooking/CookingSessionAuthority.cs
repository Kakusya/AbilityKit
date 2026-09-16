using System.Collections.Frozen;
using System.Text;
using System.Text.Json;

namespace AbilityKit.Game.Cooking;

public readonly record struct ConnectionId(string Value)
{
    public override string ToString() => Value;
}

public sealed record CookingProtocolIdentity(string Name, int MinimumVersion, int MaximumVersion)
{
    public bool Supports(string name, int version) =>
        StringComparer.Ordinal.Equals(Name, name) && version >= MinimumVersion && version <= MaximumVersion;
}

public sealed record CookingSessionDescriptor
{
    public CookingSessionDescriptor(
        CookingScope scope,
        long epoch,
        CookingProtocolIdentity protocol,
        string configIdentity,
        IReadOnlySet<string> requiredCapabilities,
        IReadOnlyDictionary<string, string> policy)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(protocol);
        ArgumentNullException.ThrowIfNull(requiredCapabilities);
        ArgumentNullException.ThrowIfNull(policy);
        if (epoch <= 0)
            throw new ArgumentOutOfRangeException(nameof(epoch), "Epoch must be positive.");
        if (string.IsNullOrWhiteSpace(configIdentity))
            throw new ArgumentException("Config identity must not be blank.", nameof(configIdentity));

        Scope = scope;
        Epoch = epoch;
        Protocol = protocol;
        ConfigIdentity = configIdentity;
        RequiredCapabilities = requiredCapabilities.ToFrozenSet(StringComparer.Ordinal);
        Policy = policy.ToFrozenDictionary(StringComparer.Ordinal);
    }

    public CookingScope Scope { get; init; }
    public long Epoch { get; init; }
    public CookingProtocolIdentity Protocol { get; init; }
    public string ConfigIdentity { get; init; }
    public IReadOnlySet<string> RequiredCapabilities { get; init; }
    public IReadOnlyDictionary<string, string> Policy { get; init; }
}

public enum CookingConnectionOrigin
{
    HostLocal,
    RemoteInProcess,
    RemoteUdp,
}

public enum CookingConnectionState
{
    Connecting,
    Handshaking,
    Bound,
    BaselinePending,
    Synchronized,
    Rejected,
    Disconnected,
    Disposed,
}

public enum CookingSessionReason
{
    None,
    SessionDisposed,
    ConnectionNotFound,
    ConnectionAlreadyExists,
    ConnectionNotBound,
    ConnectionClosed,
    ScopeMismatch,
    PlayerMismatch,
    HandshakeRequired,
    ProtocolIdentityMismatch,
    ProtocolVersionUnsupported,
    ConfigMismatch,
    CapabilityMismatch,
    InvalidHandshake,
    InvalidBaseline,
    AuthoritySnapshotMismatch,
    AssignedPlayerNotFound,
    PlayerAlreadyBound,
    MalformedCommand,
    QueueFull,
    Cancelled,
    CommandIdentityConflict,
    BaselineRequired,
    EpochMismatch,
    SnapshotSequenceDuplicate,
    SnapshotSequenceStale,
    SnapshotSequenceGap,
    BaselineReferenceMismatch,
}

public enum CookingSessionDirection
{
    Inbound,
    Outbound,
    Internal,
}

public sealed record CookingHandshake(
    CookingScope ClaimedScope,
    string ProtocolName,
    int ProtocolVersion,
    string ConfigIdentity,
    IReadOnlySet<string> Capabilities);

public sealed record CookingConnectionBinding(
    ConnectionId Connection,
    CookingScope Scope,
    PlayerId Player,
    CookingConnectionOrigin Origin,
    CookingConnectionState State);

public sealed record CookingHandshakeResult(
    bool Accepted,
    CookingSessionReason Reason,
    CookingConnectionBinding? Binding);

public enum CookingSessionCommandDisposition
{
    Queued,
    Executed,
    Rejected,
    Duplicate,
    Cancelled,
}

public sealed record CookingSessionCommandResult(
    CookingSessionCommandDisposition Disposition,
    CookingSessionReason Reason,
    CommandResult? AuthorityResult,
    int QueueDepth,
    bool IsDuplicate);

public enum CookingSynchronizationState
{
    WaitingForBaseline,
    Synchronized,
    Unsynchronized,
}

public sealed record CookingSnapshotBaseline(
    CookingScope Scope,
    long Epoch,
    long SnapshotSequence,
    string ConfigIdentity,
    string SnapshotHash);

public sealed record CookingSnapshotDelta(
    CookingScope Scope,
    long Epoch,
    long SnapshotSequence,
    long BaselineReference,
    string ConfigIdentity,
    string SnapshotHash);

public sealed record CookingSynchronizationResult(
    bool Accepted,
    CookingSessionReason Reason,
    CookingSynchronizationState State,
    long? SnapshotSequence,
    long? BaselineReference);

public sealed record CookingSessionDiagnostic(
    string EventType,
    string TimestampUtc,
    string CorrelationId,
    string SessionId,
    string? ConnectionId,
    string? PlayerId,
    string? CommandId,
    long Epoch,
    long? SnapshotSequence,
    long? BaselineReference,
    string State,
    string ReasonCode,
    int QueueDepth,
    string Direction,
    string? BeforeStateHash = null,
    string? AfterStateHash = null,
    int MutationCount = 0,
    int EventCount = 0,
    int DedupCount = 0);

public static class CookingSessionDiagnosticWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static void Append(string path, CookingSessionDiagnostic diagnostic)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new ArgumentException("Diagnostic path has no directory.", nameof(path)));
        File.AppendAllText(path, JsonSerializer.Serialize(diagnostic, Options) + Environment.NewLine, Encoding.UTF8);
    }

    public static IReadOnlyList<CookingSessionDiagnostic> ReadAll(string path) =>
        File.ReadLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<CookingSessionDiagnostic>(line, Options)
                ?? throw new InvalidDataException("Invalid cooking session diagnostic line."))
            .ToArray();
}

public sealed record CookingTransportSpikeCandidate(
    string Name,
    string Version,
    string License,
    string Platform,
    IReadOnlyDictionary<string, string> Configuration);

public sealed record CookingTransportSpikeObservation(
    string Area,
    string Result,
    string? Limitation = null,
    string? FailureReason = null);

public sealed record CookingTransportSpikeReport
{
    public CookingTransportSpikeReport(
        string workloadId,
        CookingTransportSpikeCandidate candidate,
        IReadOnlyList<CookingTransportSpikeObservation> observations,
        string generatedAtUtc,
        bool isProductionDecision = false)
    {
        if (string.IsNullOrWhiteSpace(workloadId))
            throw new ArgumentException("Workload ID must not be blank.", nameof(workloadId));
        if (isProductionDecision)
            throw new ArgumentException("A D1 spike report cannot record a production transport decision.", nameof(isProductionDecision));

        WorkloadId = workloadId;
        Candidate = candidate;
        Observations = observations;
        GeneratedAtUtc = generatedAtUtc;
        IsProductionDecision = isProductionDecision;
    }

    public string WorkloadId { get; init; }
    public CookingTransportSpikeCandidate Candidate { get; init; }
    public IReadOnlyList<CookingTransportSpikeObservation> Observations { get; init; }
    public string GeneratedAtUtc { get; init; }
    public bool IsProductionDecision { get; init; }
}

public sealed class CookingSessionAuthority : IDisposable
{
    private readonly CookingSimulation _simulation;
    private readonly CookingSessionDescriptor _descriptor;
    private readonly int _queueCapacity;
    private readonly Dictionary<ConnectionId, ConnectionRecord> _connections = new();
    private readonly List<QueuedCommand> _queue = new();
    private readonly Dictionary<SessionCommandKey, CommandLedgerEntry> _commandLedger = new();
    private readonly List<CookingSessionDiagnostic> _diagnostics = new();
    private bool _disposed;
    private int _dedupCount;

    public CookingSessionAuthority(CookingSimulation simulation, CookingSessionDescriptor descriptor, int queueCapacity)
    {
        _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        if (queueCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(queueCapacity), "Queue capacity must be positive.");
        if (!Equals(simulation.Snapshot().Scope, descriptor.Scope))
            throw new ArgumentException("Session descriptor scope must match the simulation scope.", nameof(descriptor));
        _queueCapacity = queueCapacity;
    }

    public CookingSessionDescriptor Descriptor => _descriptor;

    public int QueueDepth => _queue.Count;

    public int DeduplicationCount => _dedupCount;

    public IReadOnlyList<CookingSessionDiagnostic> Diagnostics => _diagnostics;

    public CookingSnapshot Snapshot() => _simulation.Snapshot();

    public bool OpenConnection(ConnectionId connection, CookingConnectionOrigin origin, string correlationId)
    {
        if (_disposed)
        {
            Record("connection-open-rejected", connection, null, null, null, null, CookingConnectionState.Disposed,
                CookingSessionReason.SessionDisposed, null, CookingSessionDirection.Inbound, correlationId);
            return false;
        }
        if (_connections.ContainsKey(connection))
        {
            Record("connection-open-rejected", connection, null, null, null, null, CookingConnectionState.Rejected,
                CookingSessionReason.ConnectionAlreadyExists, null, CookingSessionDirection.Inbound, correlationId);
            return false;
        }

        _connections.Add(connection, new ConnectionRecord(origin, CookingConnectionState.Handshaking, null, null));
        Record("connection-opened", connection, null, null, null, null, CookingConnectionState.Handshaking,
            CookingSessionReason.None, null, CookingSessionDirection.Internal, correlationId);
        return true;
    }

    public CookingHandshakeResult CompleteHandshake(ConnectionId connection, PlayerId assignedPlayer, CookingHandshake handshake,
        string correlationId)
    {
        if (_disposed)
            return RejectHandshake(connection, CookingSessionReason.SessionDisposed, correlationId);
        if (!_connections.TryGetValue(connection, out var record))
            return RejectHandshake(connection, CookingSessionReason.ConnectionNotFound, correlationId);
        if (record.State != CookingConnectionState.Handshaking)
            return RejectHandshake(connection, CookingSessionReason.HandshakeRequired, correlationId);
        if (handshake is null)
        {
            _connections[connection] = record with { State = CookingConnectionState.Rejected };
            return RejectHandshake(connection, CookingSessionReason.InvalidHandshake, correlationId);
        }
        if (!_simulation.HasPlayer(assignedPlayer))
        {
            _connections[connection] = record with { State = CookingConnectionState.Rejected };
            return RejectHandshake(connection, CookingSessionReason.AssignedPlayerNotFound, correlationId);
        }
        if (_connections.Any(pair => pair.Key != connection && pair.Value.Binding?.Player == assignedPlayer &&
            pair.Value.State is not (CookingConnectionState.Disconnected or CookingConnectionState.Disposed or CookingConnectionState.Rejected)))
        {
            _connections[connection] = record with { State = CookingConnectionState.Rejected };
            return RejectHandshake(connection, CookingSessionReason.PlayerAlreadyBound, correlationId);
        }

        var rejection = ValidateHandshake(handshake);
        if (rejection != CookingSessionReason.None)
        {
            _connections[connection] = record with { State = CookingConnectionState.Rejected };
            return RejectHandshake(connection, rejection, correlationId);
        }

        var binding = new CookingConnectionBinding(connection, _descriptor.Scope, assignedPlayer, record.Origin,
            CookingConnectionState.Bound);
        _connections[connection] = record with { State = CookingConnectionState.Bound, Binding = binding };
        Record("handshake-accepted", connection, assignedPlayer, null, null, null, CookingConnectionState.Bound,
            CookingSessionReason.None, null, CookingSessionDirection.Inbound, correlationId);
        return new CookingHandshakeResult(true, CookingSessionReason.None, binding);
    }

    public CookingSessionCommandResult EnqueueCommand(ConnectionId connection, CookingCommand claimedCommand, string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (claimedCommand is null)
            return RejectMalformedCommand(connection, correlationId);
        if (_disposed)
            return RejectCommand(connection, claimedCommand, CookingSessionReason.SessionDisposed, correlationId);
        if (cancellationToken.IsCancellationRequested)
            return RejectCommand(connection, claimedCommand, CookingSessionReason.Cancelled, correlationId);
        if (!_connections.TryGetValue(connection, out var record))
            return RejectCommand(connection, claimedCommand, CookingSessionReason.ConnectionNotFound, correlationId);
        if (record.State is CookingConnectionState.Disconnected or CookingConnectionState.Disposed)
            return RejectCommand(connection, claimedCommand, CookingSessionReason.ConnectionClosed, correlationId);
        if (record.Binding is not { } binding)
            return RejectCommand(connection, claimedCommand, CookingSessionReason.ConnectionNotBound, correlationId);
        if (!Equals(claimedCommand.Scope, binding.Scope))
            return RejectCommand(connection, claimedCommand, CookingSessionReason.ScopeMismatch, correlationId);
        if (claimedCommand.Player != binding.Player)
            return RejectCommand(connection, claimedCommand, CookingSessionReason.PlayerMismatch, correlationId);

        var effectiveCommand = claimedCommand with { Scope = binding.Scope, Player = binding.Player };
        var key = new SessionCommandKey(binding.Scope.Session, binding.Player, effectiveCommand.Command);
        var fingerprint = JsonSerializer.Serialize(effectiveCommand);
        if (_commandLedger.TryGetValue(key, out var ledger))
        {
            _dedupCount++;
            if (!StringComparer.Ordinal.Equals(ledger.Fingerprint, fingerprint))
                return RejectCommand(connection, effectiveCommand, CookingSessionReason.CommandIdentityConflict, correlationId);

            var duplicateResult = ledger.Result is null
                ? new CookingSessionCommandResult(CookingSessionCommandDisposition.Duplicate, CookingSessionReason.None, null,
                    QueueDepth, true)
                : new CookingSessionCommandResult(CookingSessionCommandDisposition.Duplicate, CookingSessionReason.None,
                    ledger.Result with { IsDuplicate = true, Events = Array.Empty<CookingEvent>() }, QueueDepth, true);
            Record("command-deduplicated", connection, binding.Player, effectiveCommand.Command, null, null, record.State,
                CookingSessionReason.None, null, CookingSessionDirection.Inbound, correlationId, dedupCount: _dedupCount);
            return duplicateResult;
        }
        if (_queue.Count >= _queueCapacity)
            return RejectCommand(connection, effectiveCommand, CookingSessionReason.QueueFull, correlationId);

        _commandLedger.Add(key, new CommandLedgerEntry(fingerprint, null));
        _queue.Add(new QueuedCommand(connection, effectiveCommand, key, correlationId));
        Record("command-enqueued", connection, binding.Player, effectiveCommand.Command, null, null, record.State,
            CookingSessionReason.None, null, CookingSessionDirection.Inbound, correlationId);
        return new CookingSessionCommandResult(CookingSessionCommandDisposition.Queued, CookingSessionReason.None, null, QueueDepth, false);
    }

    public IReadOnlyList<CookingSessionCommandResult> ExecuteNextBatch()
    {
        if (_queue.Count == 0)
            return Array.Empty<CookingSessionCommandResult>();

        var batch = _queue.Min(command => command.Command.SimulationBatch);
        var commands = _queue.Where(command => command.Command.SimulationBatch == batch).ToArray();
        _queue.RemoveAll(command => command.Command.SimulationBatch == batch);

        var executions = _simulation.ExecuteBatch(commands.Select(command => command.Command));
        var results = new List<CookingSessionCommandResult>(executions.Count);
        foreach (var execution in executions)
        {
            var queued = commands.Single(command => command.Command == execution.Command);
            _commandLedger[queued.Key] = _commandLedger[queued.Key] with { Result = execution.Result };
            var record = _connections.GetValueOrDefault(queued.Connection);
            Record("command-executed", queued.Connection, execution.Command.Player, execution.Command.Command, null, null,
                record?.State ?? CookingConnectionState.Disconnected, CookingSessionReason.None, execution.Before.Sha256(),
                CookingSessionDirection.Internal, queued.CorrelationId, execution.After.Sha256(), execution.Result.Events.Count,
                execution.Result.Events.Count);
            results.Add(new CookingSessionCommandResult(CookingSessionCommandDisposition.Executed, CookingSessionReason.None,
                execution.Result, QueueDepth, false));
        }

        return results;
    }

    public CookingSnapshotBaseline CreateBaseline(long snapshotSequence)
    {
        if (snapshotSequence <= 0)
            throw new ArgumentOutOfRangeException(nameof(snapshotSequence), "Snapshot sequence must be positive.");

        return new CookingSnapshotBaseline(_descriptor.Scope, _descriptor.Epoch, snapshotSequence,
            _descriptor.ConfigIdentity, _simulation.Snapshot().Sha256());
    }

    public CookingSynchronizationResult InstallBaseline(ConnectionId connection, CookingSnapshotBaseline baseline, string correlationId)
    {
        if (baseline is null || baseline.SnapshotSequence <= 0 || string.IsNullOrWhiteSpace(baseline.SnapshotHash))
            return RejectSynchronization(connection, _descriptor.Scope, CookingSessionReason.InvalidBaseline, correlationId);
        if (!TryGetBoundConnection(connection, out var record, out var reason))
            return RejectSynchronization(connection, baseline.Scope, reason, correlationId);
        if (!Equals(baseline.Scope, _descriptor.Scope))
            return RejectSynchronization(connection, baseline.Scope, CookingSessionReason.ScopeMismatch, correlationId);
        if (baseline.Epoch != _descriptor.Epoch)
            return RejectSynchronization(connection, baseline.Scope, CookingSessionReason.EpochMismatch, correlationId);
        if (!StringComparer.Ordinal.Equals(baseline.ConfigIdentity, _descriptor.ConfigIdentity))
            return RejectSynchronization(connection, baseline.Scope, CookingSessionReason.ConfigMismatch, correlationId);
        if (!StringComparer.Ordinal.Equals(baseline.SnapshotHash, _simulation.Snapshot().Sha256()))
            return RejectSynchronization(connection, baseline.Scope, CookingSessionReason.AuthoritySnapshotMismatch, correlationId);
        if (record.Synchronization is { } current && baseline.SnapshotSequence < current.Sequence)
            return RejectSynchronization(connection, baseline.Scope, CookingSessionReason.SnapshotSequenceStale, correlationId);

        _connections[connection] = record with
        {
            State = CookingConnectionState.Synchronized,
            Synchronization = new SynchronizationRecord(CookingSynchronizationState.Synchronized, baseline.SnapshotSequence,
                baseline.SnapshotSequence, baseline.SnapshotHash),
        };
        Record("baseline-installed", connection, record.Binding!.Player, null, baseline.SnapshotSequence, baseline.SnapshotSequence,
            CookingConnectionState.Synchronized, CookingSessionReason.None, null, CookingSessionDirection.Outbound, correlationId);
        return new CookingSynchronizationResult(true, CookingSessionReason.None, CookingSynchronizationState.Synchronized,
            baseline.SnapshotSequence, baseline.SnapshotSequence);
    }

    public CookingSynchronizationResult ApplyDelta(ConnectionId connection, CookingSnapshotDelta delta, string correlationId)
    {
        if (delta is null || delta.SnapshotSequence <= 0 || delta.BaselineReference <= 0 || string.IsNullOrWhiteSpace(delta.SnapshotHash))
            return RejectSynchronization(connection, _descriptor.Scope, CookingSessionReason.InvalidBaseline, correlationId);
        if (!TryGetBoundConnection(connection, out var record, out var reason))
            return RejectSynchronization(connection, delta.Scope, reason, correlationId);
        if (!Equals(delta.Scope, _descriptor.Scope))
            return RejectSynchronization(connection, delta.Scope, CookingSessionReason.ScopeMismatch, correlationId);
        if (delta.Epoch != _descriptor.Epoch)
            return RejectSynchronization(connection, delta.Scope, CookingSessionReason.EpochMismatch, correlationId);
        if (!StringComparer.Ordinal.Equals(delta.ConfigIdentity, _descriptor.ConfigIdentity))
            return RejectSynchronization(connection, delta.Scope, CookingSessionReason.ConfigMismatch, correlationId);
        if (record.Synchronization is not { } synchronization)
            return MarkUnsynchronized(connection, record, delta, CookingSessionReason.BaselineRequired, correlationId);
        if (delta.BaselineReference != synchronization.BaselineReference)
            return MarkUnsynchronized(connection, record, delta, CookingSessionReason.BaselineReferenceMismatch, correlationId);
        if (delta.SnapshotSequence == synchronization.Sequence)
            return MarkUnsynchronized(connection, record, delta, CookingSessionReason.SnapshotSequenceDuplicate, correlationId);
        if (delta.SnapshotSequence < synchronization.Sequence)
            return MarkUnsynchronized(connection, record, delta, CookingSessionReason.SnapshotSequenceStale, correlationId);
        if (delta.SnapshotSequence > synchronization.Sequence + 1)
            return MarkUnsynchronized(connection, record, delta, CookingSessionReason.SnapshotSequenceGap, correlationId);

        _connections[connection] = record with
        {
            State = CookingConnectionState.Synchronized,
            Synchronization = synchronization with { State = CookingSynchronizationState.Synchronized, Sequence = delta.SnapshotSequence, SnapshotHash = delta.SnapshotHash },
        };
        Record("delta-applied", connection, record.Binding!.Player, null, delta.SnapshotSequence, delta.BaselineReference,
            CookingConnectionState.Synchronized, CookingSessionReason.None, null, CookingSessionDirection.Outbound, correlationId);
        return new CookingSynchronizationResult(true, CookingSessionReason.None, CookingSynchronizationState.Synchronized,
            delta.SnapshotSequence, delta.BaselineReference);
    }

    public CookingSynchronizationResult ReportTransportLoss(ConnectionId connection, string correlationId)
    {
        if (!_connections.TryGetValue(connection, out var record))
            return RejectSynchronization(connection, _descriptor.Scope, CookingSessionReason.ConnectionNotFound, correlationId);

        var synchronization = record.Synchronization ?? new SynchronizationRecord(
            CookingSynchronizationState.WaitingForBaseline, 0, 0, null);
        CancelQueued(connection, CookingSessionReason.ConnectionClosed, correlationId);
        _connections[connection] = record with
        {
            State = CookingConnectionState.Disconnected,
            Binding = null,
            Synchronization = synchronization with { State = CookingSynchronizationState.Unsynchronized },
        };
        Record("transport-loss", connection, record.Binding?.Player, null, synchronization.Sequence,
            synchronization.BaselineReference, CookingConnectionState.Disconnected, CookingSessionReason.ConnectionClosed,
            null, CookingSessionDirection.Internal, correlationId);
        Record("resource-unbound", connection, record.Binding?.Player, null, synchronization.Sequence,
            synchronization.BaselineReference, CookingConnectionState.Disconnected, CookingSessionReason.None,
            null, CookingSessionDirection.Internal, correlationId);
        return new CookingSynchronizationResult(false, CookingSessionReason.ConnectionClosed,
            CookingSynchronizationState.Unsynchronized, synchronization.Sequence, synchronization.BaselineReference);
    }

    public int CloseConnection(ConnectionId connection, string correlationId)
    {
        if (!_connections.TryGetValue(connection, out var record))
            return 0;

        var cancelled = CancelQueued(connection, CookingSessionReason.ConnectionClosed, correlationId);
        _connections[connection] = record with { State = CookingConnectionState.Disconnected, Binding = null, Synchronization = null };
        Record("connection-closed", connection, record.Binding?.Player, null, null, null, CookingConnectionState.Disconnected,
            CookingSessionReason.ConnectionClosed, null, CookingSessionDirection.Internal, correlationId);
        Record("resource-unbound", connection, record.Binding?.Player, null, null, null, CookingConnectionState.Disconnected,
            CookingSessionReason.None, null, CookingSessionDirection.Internal, correlationId);
        return cancelled;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (var connection in _connections.Keys.ToArray())
        {
            var record = _connections[connection];
            CancelQueued(connection, CookingSessionReason.SessionDisposed, "dispose");
            _connections[connection] = record with { State = CookingConnectionState.Disposed, Binding = null, Synchronization = null };
            Record("resource-unbound", connection, record.Binding?.Player, null, null, null, CookingConnectionState.Disposed,
                CookingSessionReason.SessionDisposed, null, CookingSessionDirection.Internal, "dispose");
        }
    }

    private CookingSessionReason ValidateHandshake(CookingHandshake handshake)
    {
        if (handshake.ClaimedScope is null || string.IsNullOrWhiteSpace(handshake.ProtocolName) ||
            string.IsNullOrWhiteSpace(handshake.ConfigIdentity) || handshake.Capabilities is null)
            return CookingSessionReason.InvalidHandshake;
        if (!Equals(handshake.ClaimedScope, _descriptor.Scope))
            return CookingSessionReason.ScopeMismatch;
        if (!StringComparer.Ordinal.Equals(handshake.ProtocolName, _descriptor.Protocol.Name))
            return CookingSessionReason.ProtocolIdentityMismatch;
        if (!_descriptor.Protocol.Supports(handshake.ProtocolName, handshake.ProtocolVersion))
            return CookingSessionReason.ProtocolVersionUnsupported;
        if (!StringComparer.Ordinal.Equals(handshake.ConfigIdentity, _descriptor.ConfigIdentity))
            return CookingSessionReason.ConfigMismatch;
        if (!_descriptor.RequiredCapabilities.All(handshake.Capabilities.Contains))
            return CookingSessionReason.CapabilityMismatch;
        return CookingSessionReason.None;
    }

    private CookingHandshakeResult RejectHandshake(ConnectionId connection, CookingSessionReason reason, string correlationId)
    {
        Record("handshake-rejected", connection, null, null, null, null, CookingConnectionState.Rejected, reason, null,
            CookingSessionDirection.Inbound, correlationId);
        return new CookingHandshakeResult(false, reason, null);
    }

    private CookingSessionCommandResult RejectMalformedCommand(ConnectionId connection, string correlationId)
    {
        var record = _connections.GetValueOrDefault(connection);
        Record("command-rejected", connection, null, null, null, null,
            record?.State ?? (_disposed ? CookingConnectionState.Disposed : CookingConnectionState.Rejected),
            CookingSessionReason.MalformedCommand, null, CookingSessionDirection.Inbound, correlationId);
        return new CookingSessionCommandResult(CookingSessionCommandDisposition.Rejected, CookingSessionReason.MalformedCommand,
            null, QueueDepth, false);
    }

    private CookingSessionCommandResult RejectCommand(ConnectionId connection, CookingCommand command, CookingSessionReason reason,
        string correlationId)
    {
        var record = _connections.GetValueOrDefault(connection);
        Record("command-rejected", connection, command.Player, command.Command, null, null,
            record?.State ?? (_disposed ? CookingConnectionState.Disposed : CookingConnectionState.Rejected), reason, null,
            CookingSessionDirection.Inbound, correlationId);
        return new CookingSessionCommandResult(CookingSessionCommandDisposition.Rejected, reason, null, QueueDepth, false);
    }

    private bool TryGetBoundConnection(ConnectionId connection, out ConnectionRecord record, out CookingSessionReason reason)
    {
        if (_disposed)
        {
            record = default!;
            reason = CookingSessionReason.SessionDisposed;
            return false;
        }
        if (!_connections.TryGetValue(connection, out record!))
        {
            reason = CookingSessionReason.ConnectionNotFound;
            return false;
        }
        if (record.State is CookingConnectionState.Disconnected or CookingConnectionState.Disposed)
        {
            reason = CookingSessionReason.ConnectionClosed;
            return false;
        }
        if (record.Binding is null)
        {
            reason = CookingSessionReason.ConnectionNotBound;
            return false;
        }
        reason = CookingSessionReason.None;
        return true;
    }

    private CookingSynchronizationResult RejectSynchronization(ConnectionId connection, CookingScope scope, CookingSessionReason reason,
        string correlationId)
    {
        var record = _connections.GetValueOrDefault(connection);
        Record("synchronization-rejected", connection, record?.Binding?.Player, null, null, null,
            record?.State ?? CookingConnectionState.Rejected, reason, null, CookingSessionDirection.Outbound, correlationId);
        return new CookingSynchronizationResult(false, reason, record?.Synchronization?.State ?? CookingSynchronizationState.WaitingForBaseline,
            record?.Synchronization?.Sequence, record?.Synchronization?.BaselineReference);
    }

    private CookingSynchronizationResult MarkUnsynchronized(ConnectionId connection, ConnectionRecord record, CookingSnapshotDelta delta,
        CookingSessionReason reason, string correlationId)
    {
        var synchronization = record.Synchronization ?? new SynchronizationRecord(CookingSynchronizationState.WaitingForBaseline, 0, 0, null);
        _connections[connection] = record with
        {
            State = CookingConnectionState.BaselinePending,
            Synchronization = synchronization with { State = CookingSynchronizationState.Unsynchronized },
        };
        Record("delta-rejected", connection, record.Binding!.Player, null, delta.SnapshotSequence, delta.BaselineReference,
            CookingConnectionState.BaselinePending, reason, null, CookingSessionDirection.Outbound, correlationId);
        return new CookingSynchronizationResult(false, reason, CookingSynchronizationState.Unsynchronized,
            synchronization.Sequence, synchronization.BaselineReference);
    }

    private int CancelQueued(ConnectionId connection, CookingSessionReason reason, string correlationId)
    {
        var cancelled = _queue.Where(queued => queued.Connection == connection).ToArray();
        foreach (var queued in cancelled)
        {
            _queue.Remove(queued);
            _commandLedger[queued.Key] = _commandLedger[queued.Key] with { Result = null };
            var record = _connections[connection];
            Record("command-cancelled", connection, queued.Command.Player, queued.Command.Command, null, null, record.State,
                reason, null, CookingSessionDirection.Internal, correlationId);
        }
        return cancelled.Length;
    }

    private void Record(string eventType, ConnectionId connection, PlayerId? player, CommandId? command, long? sequence,
        long? baselineReference, CookingConnectionState state, CookingSessionReason reason, string? beforeStateHash,
        CookingSessionDirection direction, string correlationId, string? afterStateHash = null, int mutationCount = 0,
        int eventCount = 0, int dedupCount = 0)
    {
        _diagnostics.Add(new CookingSessionDiagnostic(eventType, DateTimeOffset.UtcNow.ToString("O"), correlationId,
            _descriptor.Scope.Session.Value, connection.Value, player?.Value, command?.Value, _descriptor.Epoch, sequence,
            baselineReference, state.ToString(), reason.ToString(), QueueDepth, direction.ToString(), beforeStateHash,
            afterStateHash, mutationCount, eventCount, dedupCount));
    }

    private sealed record ConnectionRecord(
        CookingConnectionOrigin Origin,
        CookingConnectionState State,
        CookingConnectionBinding? Binding,
        SynchronizationRecord? Synchronization);

    private sealed record SynchronizationRecord(
        CookingSynchronizationState State,
        long Sequence,
        long BaselineReference,
        string? SnapshotHash);

    private sealed record QueuedCommand(ConnectionId Connection, CookingCommand Command, SessionCommandKey Key, string CorrelationId);

    private sealed record SessionCommandKey(SessionId Session, PlayerId Player, CommandId Command);

    private sealed record CommandLedgerEntry(string Fingerprint, CommandResult? Result);
}
