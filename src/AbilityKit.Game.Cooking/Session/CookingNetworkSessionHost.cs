using System.Diagnostics;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Host;
using AbilityKit.Network.Host.InProcess;

namespace AbilityKit.Game.Cooking.Session;

public sealed record CookingNetworkSessionTiming(long Ordinal, string Source, string Correlation,
    long ReceivedTimestamp, long ConsumedTimestamp, long CommittedTimestamp, string Reason);
public sealed record CookingNetworkSessionDiagnostics(int Pending, int QueueHighWater, long Rejected, long ReceivedBytes,
    long SentBytes, IReadOnlyList<CookingNetworkSessionTiming> Timings);

/// <summary>Current v3 composition. No simulator is constructed or executed by this Session.</summary>
public sealed class CookingNetworkSessionHost : IDisposable
{
    private sealed class Connection(IServerNetworkSession peer, bool local)
    {
        public IServerNetworkSession Peer { get; } = peer;
        public string PhysicalId { get; } = Guid.NewGuid().ToString("N");
        public bool Local { get; } = local;
        public long ClosedOrdinal;
        public PlayerId? Participant;
        public long Generation;
        public long Sequence;
        public long LastTerminalSequence;
        public (long Generation, long Sequence)? TransportRepliedWatermark;
        public bool Ready;
        public CookingNetworkBaselineIdentity? Issued;
        public bool BaselineAwaitingAck;
        public string Source = "";
    }
    private sealed class Participant(string credential)
    {
        public string Credential { get; } = credential;
        public string Token { get; set; } = Guid.NewGuid().ToString("N");
        public long Generation;
        public long LastValidatedSequence;
        public long LastTerminalSequence;
        public Connection? Active;
    }
    private sealed record Ingress(long Ordinal, Connection Connection, CookingNetworkWireEnvelope Envelope, long Received, long Consumed = 0);
    private sealed class Mapping(string stableId, PlayerId player, CookingRecipeCommand command, long ordinal, string fingerprint)
    {
        public string StableId { get; } = stableId;
        public PlayerId Player { get; } = player;
        public CookingRecipeCommand Command { get; } = command;
        public long Ordinal { get; } = ordinal;
        public string Fingerprint { get; } = fingerprint;
        public CookingNetworkWireResult? Terminal;
        public bool Cancelled;
        public int Waiters;
    }
    private readonly object _gate = new();
    private readonly ICookingNetworkAuthorityPort _authority;
    private readonly CookingNetworkSessionOptions _options;
    private readonly InProcessChannelListener _localListener = new();
    private readonly NetworkHost _remote;
    private readonly NetworkHost _local;
    private readonly Dictionary<IServerNetworkSession, Connection> _connections = new();
    private readonly Dictionary<PlayerId, Participant> _participants;
    private readonly Queue<Ingress> _ingress = new();
    private readonly Dictionary<(CookingLevelScope Scope, PlayerId Player, string Stable), Mapping> _mapping = new();
    private readonly Dictionary<RecipeCommandId, Mapping> _domain = new();
    private readonly Dictionary<(string Source, string Correlation), Ingress> _waiting = new();
    private readonly HashSet<PlayerId> _cleanup = new();
    private readonly List<CookingNetworkSessionTiming> _timings = new();
    private long _ordinal, _snapshotSequence, _rejected, _receivedBytes, _sentBytes;
    private int _highWater;
    private bool _disposed;
    private string? _captureUnavailable;
    public string ServerSessionInstance { get; } = Guid.NewGuid().ToString("N");
    public CookingNetworkAuthorityCapture? LatestCapture { get; private set; }
    public CookingNetworkSessionProjection LatestSessionProjection { get; private set; } = null!;
    public string Endpoint => _remote.Endpoint;
    public int Port => int.TryParse(Endpoint.Split(':').Last(), out var port) ? port : 0;

    public CookingNetworkSessionHost(ICookingNetworkAuthorityPort authority, IChannelListener listener,
        IReadOnlyDictionary<PlayerId, string> joinCredentials, CookingNetworkSessionOptions? options = null)
    {
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        _options = options ?? new();
        if (joinCredentials.Count is < 1 or > 4 || joinCredentials.Any(x => !CookingNetworkWireCodec.Identifier(x.Key.Value) ||
            !CookingNetworkWireCodec.Identifier(x.Value)) || _options.BusinessCapacity <= 0 || _options.ControlCapacity <= 0 ||
            _options.ConnectionCapacity <= 0 || _options.ReceiptCapacity <= 0 || _options.MaximumPrefix <= 0 ||
            _options.DuplicateWaiterCapacity <= 0 || _options.PerConnectionCapacity <= 0 ||
            _options.BaselineTokenLimit <= 0 || _options.BaselineCollectionLimit <= 0) throw new ArgumentException("Invalid Session bounds/participants.");
        _participants = joinCredentials.ToDictionary(x => x.Key, x => new Participant(x.Value));
        var capture = _authority.CaptureFullState();
        if (!capture.Accepted || capture.State is null) throw new InvalidOperationException("Authority capture unavailable.");
        LatestCapture = capture.State; RefreshSessionProjection();
        _remote = CreateHost(listener, false); _local = CreateHost(_localListener, true);
    }
    public void Start() { _remote.Start(); _local.Start(); }
    public ITransport CreateLocalClientTransport() => _localListener.CreateClientTransport();
    public CookingNetworkSessionDiagnostics Diagnostics { get { lock (_gate) return new(_ingress.Count, _highWater, _rejected,
        _receivedBytes, _sentBytes, _timings.ToArray()); } }
    private NetworkHost CreateHost(IChannelListener listener, bool local)
    {
        var host = new NetworkHost(listener, new NetworkHostOptions { MaxConnections = _options.ConnectionCapacity,
            FrameCodec = new CookingNetworkFrameCodec(_options.FrameBytes + 64) });
        host.SessionOpened += peer => { lock (_gate) {
            if (_connections.Count >= _options.ConnectionCapacity) peer.Close();
            else _connections.Add(peer, new Connection(peer, local));
        } };
        host.PacketReceived += (peer, header, bytes) => { lock (_gate) {
            if (!_connections.TryGetValue(peer, out var connection) || connection.ClosedOrdinal != 0) return;
            _receivedBytes += bytes.Count;
            if (header.OpCode != CookingNetworkWireCodec.OpCode || !CookingNetworkWireCodec.TryDecode(bytes.AsSpan(), _options, out var envelope) || envelope is null) {
                Reject(connection, "invalid-frame", "ProtocolMismatch"); return;
            }
            var business = envelope.Kind == CookingNetworkMessageKind.Command;
            var count = _ingress.Count(i => (i.Envelope.Kind == CookingNetworkMessageKind.Command) == business);
            if (count >= (business ? _options.BusinessCapacity : _options.ControlCapacity) ||
                _ingress.Count(i => i.Connection == connection) >= _options.PerConnectionCapacity) {
                Reject(connection, envelope.CorrelationId, "QueueFull");
                if (business && CookingNetworkWireCodec.Read<CookingNetworkWireCommand>(envelope) is { ClientSequence: > 0 } rejected) { var prior = connection.TransportRepliedWatermark; connection.TransportRepliedWatermark = (connection.Generation, Math.Max(prior?.Generation == connection.Generation ? prior.Value.Sequence : 0, rejected.ClientSequence)); }
                if (!business) connection.Peer.Close(); return;
            }
            _ingress.Enqueue(new Ingress(checked(++_ordinal), connection, envelope, Stopwatch.GetTimestamp()));
            _highWater = Math.Max(_highWater, _ingress.Count);
        } };
        host.SessionClosed += peer => { lock (_gate) {
            if (_connections.TryGetValue(peer, out var connection) && connection.ClosedOrdinal == 0)
                connection.ClosedOrdinal = checked(++_ordinal);
        } };
        return host;
    }
    private void Send<T>(Connection connection, CookingNetworkMessageKind kind, string correlation, T value)
    {
        if (!connection.Peer.IsConnected) return;
        var bytes = CookingNetworkWireCodec.Encode(kind, correlation, value, _options);
        if (bytes.Length > _options.FrameBytes) throw new InvalidOperationException("Full state exceeds configured wire bound.");
        _sentBytes += bytes.Length;
        connection.Peer.SendPush(CookingNetworkWireCodec.OpCode, new ArraySegment<byte>(bytes));
    }
    private void Reject(Connection connection, string correlation, string reason)
    {
        _rejected++;
        Send(connection, CookingNetworkMessageKind.Rejected, correlation, new CookingNetworkWireResult("", null, reason, null, null));
    }
    private void Publish(Connection connection)
    {
        if (_captureUnavailable is not null || LatestCapture is null || connection.Participant is null || connection.BaselineAwaitingAck) return;
        RefreshSessionProjection();
        var identity = new CookingNetworkBaselineIdentity(ServerSessionInstance, connection.Participant.Value, connection.Generation,
            LatestCapture.Observation.Scope, LatestCapture.Observation.Scope.LevelEpoch, checked(_snapshotSequence + 1),
            CookingNetworkWireCodec.BaselineHash(LatestCapture, LatestSessionProjection), Guid.NewGuid().ToString("N"));
        var baseline = new CookingNetworkBaseline(identity, CookingLevelCheckpointCodec.CurrentFormatVersion, 5, LatestCapture, LatestSessionProjection);
        byte[] bytes;
        try { bytes = CookingNetworkWireCodec.Encode(CookingNetworkMessageKind.Baseline, "baseline-" + identity.SnapshotSequence, baseline, _options); }
        catch (ArgumentException) { MakeUnavailable("FullStateExceedsWireBounds"); return; }
        // Issued identity and sequence become visible only after full outbound validation.
        _snapshotSequence = identity.SnapshotSequence; connection.Issued = identity; connection.BaselineAwaitingAck = true;
        if (connection.Peer.IsConnected) { _sentBytes += bytes.Length; connection.Peer.SendPush(CookingNetworkWireCodec.OpCode, new ArraySegment<byte>(bytes)); }
    }
    private void Join(Ingress input)
    {
        if (input.Connection.Participant is not null) { Reject(input.Connection, input.Envelope.CorrelationId, "AlreadyBound"); return; }
        if (_captureUnavailable is not null) { Reject(input.Connection, input.Envelope.CorrelationId, _captureUnavailable); return; }
        var join = CookingNetworkWireCodec.Read<CookingNetworkJoin>(input.Envelope);
        if (join is null || !_participants.TryGetValue(join.Participant, out var participant) ||
            join.JoinCredential != participant.Credential ||
            (participant.Generation == 0 && (join.ServerSessionInstance is not null || join.RebindToken is not null)) ||
            (join.RebindToken is not null && join.ServerSessionInstance != ServerSessionInstance) ||
            (participant.Generation > 0 && (join.ServerSessionInstance != ServerSessionInstance || join.RebindToken != participant.Token))) {
            Reject(input.Connection, input.Envelope.CorrelationId, "Unauthorized"); return;
        }
        if (participant.Active is { } old && old != input.Connection) {
            CloseOwner(old, CookingNetworkCallerCancellationReason.ConnectionSuperseded); old.Peer.Close();
        }
        var connection = input.Connection;
        connection.Participant = join.Participant; connection.Generation = checked(++participant.Generation);
        participant.Token = Guid.NewGuid().ToString("N");
        connection.Source = ServerSessionInstance + ":" + connection.PhysicalId + ":" + connection.Generation;
        connection.Sequence = 0; connection.LastTerminalSequence = 0; connection.TransportRepliedWatermark = null;
        participant.LastValidatedSequence = 0; participant.LastTerminalSequence = 0; connection.Ready = false; participant.Active = connection;
        Send(connection, CookingNetworkMessageKind.Joined, input.Envelope.CorrelationId,
            new CookingNetworkJoined(ServerSessionInstance, join.Participant, connection.Generation, participant.Token));
        Publish(connection);
    }
    private void CloseOwner(Connection connection, CookingNetworkCallerCancellationReason reason)
    {
        connection.Ready = false; connection.Issued = null; connection.BaselineAwaitingAck = false;
        if (connection.Participant is { } player) {
            if (_participants[player].Active == connection) _participants[player].Active = null;
            _cleanup.Add(player);
        }
        if (connection.Source.Length > 0) {
            var cancelled = _authority.CancelSources(new[] { new CookingNetworkCancellation(connection.Source, connection.Generation, reason) });
            foreach (var caller in cancelled.CancelledCallers) CancelCaller(caller);
        }
        foreach (var waiting in _waiting.Where(p => p.Key.Source == connection.Source).ToArray()) {
            if (_domain.TryGetValue(CookingNetworkWireCodec.DomainId(ServerSessionInstance,
                CookingNetworkWireCodec.Read<CookingNetworkWireCommand>(waiting.Value.Envelope)!.Scope,
                connection.Participant!.Value, CookingNetworkWireCodec.Read<CookingNetworkWireCommand>(waiting.Value.Envelope)!.StableCommandId), out var mapping)) {
                mapping.Waiters--; if (mapping.Waiters == 0 && mapping.Terminal is null) mapping.Cancelled = true;
            }
            _waiting.Remove(waiting.Key);
        }
    }
    private void CancelCaller(CookingNetworkCallerCancellation caller)
    {
        if (_waiting.Remove((caller.SourceConnectionId, caller.CorrelationId), out var input) && _domain.TryGetValue(caller.CommandId, out var mapping)) {
            mapping.Waiters--; if (mapping.Waiters == 0 && mapping.Terminal is null) mapping.Cancelled = true;
            if (CookingNetworkWireCodec.Read<CookingNetworkWireCommand>(input.Envelope) is { } cancelledWire) MarkTerminal(input.Connection, cancelledWire.ClientSequence);
            Send(input.Connection, CookingNetworkMessageKind.CommandResult, caller.CorrelationId,
                new CookingNetworkWireResult(mapping.StableId, caller.CommandId, caller.Reason.ToString(), null, CookingNetworkDispositionKind.Cancelled));
        }
    }
    public CookingNetworkOwnerFrameResult? ProcessOwnerFrame()
    {
        lock (_gate) {
            ObjectDisposedException.ThrowIf(_disposed, this);
            foreach (var c in _connections.Values) { if (c.TransportRepliedWatermark is { } watermark && watermark.Generation == c.Generation) MarkTerminal(c, watermark.Sequence); c.TransportRepliedWatermark = null; }
            foreach (var connection in _connections.Values.Where(c => c.ClosedOrdinal != 0).OrderBy(c => c.ClosedOrdinal).ToArray()) {
                CloseOwner(connection, CookingNetworkCallerCancellationReason.ConnectionClosed); _connections.Remove(connection.Peer);
            }
            var prefix = new List<Ingress>();
            while (prefix.Count < _options.MaximumPrefix && _ingress.TryDequeue(out var input)) prefix.Add(input);
            AcceptCapture(_authority.CaptureFullState());
            if (_captureUnavailable is not null) {
                foreach (var input in prefix.Where(i => i.Connection.ClosedOrdinal == 0)) Reject(input.Connection, input.Envelope.CorrelationId, _captureUnavailable);
                RefreshSessionProjection(); return null;
            }
            foreach (var input in prefix.Where(i => i.Envelope.Kind != CookingNetworkMessageKind.Command)) {
                if (input.Connection.ClosedOrdinal != 0) continue;
                if (input.Envelope.Kind == CookingNetworkMessageKind.Join) Join(input);
                else if (input.Envelope.Kind == CookingNetworkMessageKind.BaselineAck) {
                    var ack = CookingNetworkWireCodec.Read<CookingNetworkBaselineIdentity>(input.Envelope);
                    if (ack is not null && input.Connection.BaselineAwaitingAck && ack == input.Connection.Issued && _cleanup.Count == 0) {
                        input.Connection.BaselineAwaitingAck = false; input.Connection.Ready = true; Send(input.Connection, CookingNetworkMessageKind.Ready, input.Envelope.CorrelationId, ack);
                    } else Reject(input.Connection, input.Envelope.CorrelationId, "BaselineRequired");
                } else Reject(input.Connection, input.Envelope.CorrelationId, "MalformedCommand");
            }
            if (LatestCapture!.Observation.Lifecycle.State == CookingLevelState.Paused) {
                foreach (var input in prefix.Where(i => i.Envelope.Kind == CookingNetworkMessageKind.Command)) Reject(input.Connection, input.Envelope.CorrelationId, "LevelPaused");
                RefreshSessionProjection(); return null;
            }
            var batch = checked(Math.Max(LatestCapture.Observation.HostFrameSequence, LatestCapture.LastCommittedSimulationBatch) + 1);
            var mapped = new List<CookingNetworkMappedCommand>();
            foreach (var input in prefix.Where(i => i.Envelope.Kind == CookingNetworkMessageKind.Command)) Map(input, batch, mapped);
            var result = _authority.ConsumeFrame(mapped, _cleanup.ToArray());

            if (result.Accepted) _cleanup.Clear();
            foreach (var admission in result.Admissions) if (!admission.Accepted) Complete(admission.Disposition ??
                new CookingNetworkDisposition(admission.Scope, admission.Participant, admission.CommandId, admission.SourceConnectionId,
                    admission.CorrelationId, CookingNetworkDispositionKind.Stale, admission.Reason.ToString(), null));
            foreach (var disposition in result.Dispositions) Complete(disposition);
            foreach (var caller in result.CancelledCallers) CancelCaller(caller);
            if (!result.Accepted) foreach (var entry in mapped) Complete(new CookingNetworkDisposition(entry.Envelope.LevelScope, entry.Envelope.Command.Player, entry.Envelope.Command.Command, entry.Envelope.SourceConnectionId, entry.Envelope.CorrelationId, CookingNetworkDispositionKind.Cancelled, result.Reason.ToString(), null));
            if (AcceptCapture(result.Capture)) {
                foreach (var connection in _connections.Values.Where(c => c.Participant is not null && c.ClosedOrdinal == 0)) Publish(connection);
            }
            RefreshSessionProjection(); return result;
        }
    }
    private void Map(Ingress input, long batch, ICollection<CookingNetworkMappedCommand> output)
    {
        var connection = input.Connection;
        var wire = CookingNetworkWireCodec.Read<CookingNetworkWireCommand>(input.Envelope);

        void RejectMapped(string reason) { if (wire is not null) MarkTerminal(connection, wire.ClientSequence); Reject(connection, input.Envelope.CorrelationId, reason); }
        void ReplyMapped(CookingNetworkWireResult result) { if (wire is not null) MarkTerminal(connection, wire.ClientSequence); Send(connection, CookingNetworkMessageKind.CommandResult, input.Envelope.CorrelationId, result); }
        if (connection.ClosedOrdinal != 0 || connection.Participant is null || !connection.Ready) { RejectMapped("BaselineRequired"); return; }
        if (wire is null || wire.ServerSessionInstance != ServerSessionInstance || wire.ConnectionGeneration != connection.Generation) { RejectMapped("ServerInstanceMismatch"); return; }
        if (wire.Scope != LatestCapture!.Observation.Scope || wire.Command.Scope != wire.Scope.MatchScope || wire.Command.Player != connection.Participant) { RejectMapped("ScopeMismatch"); return; }
        if (wire.ClientSequence <= connection.Sequence || !CookingNetworkWireCodec.Identifier(wire.StableCommandId) || wire.Command.SimulationBatch != 0 || !CookingRecipeCommandValidation.IsWellFormed(wire.Command with { SimulationBatch = 1 })) { RejectMapped("MalformedCommand"); return; }
        connection.Sequence = wire.ClientSequence; _participants[connection.Participant.Value].LastValidatedSequence = wire.ClientSequence;
        var key = (wire.Scope, connection.Participant.Value, wire.StableCommandId);
        var fingerprint = CookingNetworkWireCodec.Hash(wire.Command with { Command = new RecipeCommandId(wire.StableCommandId), SimulationBatch = 0 });
        if (!_mapping.TryGetValue(key, out var mapping)) {
            if (_mapping.Count >= _options.ReceiptCapacity) { RejectMapped("ReceiptCapacityExceeded"); return; }
            var domain = CookingNetworkWireCodec.DomainId(ServerSessionInstance, wire.Scope, connection.Participant.Value, wire.StableCommandId);
            mapping = new Mapping(wire.StableCommandId, connection.Participant.Value, wire.Command with { Command = domain, SimulationBatch = batch }, input.Ordinal, fingerprint);
            _mapping.Add(key, mapping); _domain.Add(domain, mapping);
        }
        if (mapping.Cancelled) { ReplyMapped(
            new CookingNetworkWireResult(mapping.StableId, mapping.Command.Command, "CancelledNoExecution", null, CookingNetworkDispositionKind.Cancelled)); return; }
        if (mapping.Terminal is not null && fingerprint == mapping.Fingerprint) {
            ReplyMapped( mapping.Terminal with {
                Result = mapping.Terminal.Result is null ? null : mapping.Terminal.Result with { IsDuplicate = true, Events = Array.Empty<CookingRecipeEvent>() } }); return;
        }
        if (mapping.Terminal is not null) { ReplyMapped( new CookingNetworkWireResult(mapping.StableId, mapping.Command.Command, "CommandIdentityConflict", CookingRecipeCommandResult.Reject(CookingRecipeRejectionReason.CommandIdentityConflict, LatestCapture.Observation.Recipe?.Version ?? 0), CookingNetworkDispositionKind.Conflicted)); return; }
        if (mapping.Waiters >= _options.DuplicateWaiterCapacity || _waiting.ContainsKey((connection.Source, input.Envelope.CorrelationId))) { RejectMapped("QueueFull"); return; }
        var command = fingerprint == mapping.Fingerprint ? mapping.Command : wire.Command with { Command = mapping.Command.Command, SimulationBatch = mapping.Command.SimulationBatch };
        output.Add(new CookingNetworkMappedCommand(new(wire.Scope, command, connection.Source, input.Envelope.CorrelationId), mapping.Ordinal, fingerprint));
        mapping.Waiters++; _waiting.Add((connection.Source, input.Envelope.CorrelationId), input with { Consumed = Stopwatch.GetTimestamp() });
    }
    private void Complete(CookingNetworkDisposition disposition)
    {
        if (!_waiting.Remove((disposition.SourceConnectionId, disposition.CorrelationId), out var input) || !_domain.TryGetValue(disposition.CommandId, out var mapping)) return;
        mapping.Waiters--;
        if (CookingNetworkWireCodec.Read<CookingNetworkWireCommand>(input.Envelope) is { } terminalWire) MarkTerminal(input.Connection, terminalWire.ClientSequence);
        var response = new CookingNetworkWireResult(mapping.StableId, disposition.CommandId, disposition.Reason, disposition.Result, disposition.Kind);
        if (mapping.Terminal is null && disposition.Kind != CookingNetworkDispositionKind.Duplicate) mapping.Terminal = response;
        Send(input.Connection, CookingNetworkMessageKind.CommandResult, disposition.CorrelationId, response);
        if (_timings.Count < _options.ReceiptCapacity * _options.DuplicateWaiterCapacity)
            _timings.Add(new(input.Ordinal, disposition.SourceConnectionId, disposition.CorrelationId, input.Received,
                input.Consumed, Stopwatch.GetTimestamp(), disposition.Reason));
    }
    public CookingNetworkControlResult ApplyControl(CookingNetworkOwnerControl control)
    {
        lock (_gate) {
            var result = _authority.ApplyControl(control);
            foreach (var disposition in result.Dispositions) Complete(disposition);
            AcceptCapture(result.Capture);
            if (result.Accepted && control.Kind == CookingNetworkControlKind.Pause) {
                foreach (var c in _connections.Values.Where(c => c.Participant is not null)) { var cancelled = _authority.CancelSources(new[] { new CookingNetworkCancellation(c.Source, c.Generation, CookingNetworkCallerCancellationReason.Paused) }); foreach (var caller in cancelled.CancelledCallers) CancelCaller(caller); }
                var controlInputs = new List<Ingress>(); while (_ingress.TryDequeue(out var input)) { if (input.Envelope.Kind == CookingNetworkMessageKind.Command) Reject(input.Connection, input.Envelope.CorrelationId, "LevelPaused"); else controlInputs.Add(input); } foreach (var input in controlInputs) _ingress.Enqueue(input);
            }
            foreach (var c in _connections.Values.Where(c => c.Participant is not null && c.ClosedOrdinal == 0)) Publish(c);
            RefreshSessionProjection(); return result;
        }
    }
    private bool AcceptCapture(CookingNetworkCaptureResult capture)
    {
        // Fault/dispose is terminal for this Session. Busy is temporary and never republishes stale state.
        if (_captureUnavailable is "AuthorityFaulted" or "Disposed" or "FullStateExceedsWireBounds") return false;
        if (capture.Accepted && capture.State is { } state) { _captureUnavailable = null; InstallCapture(state); return true; }
        MakeUnavailable(capture.Reason.ToString()); return false;
    }
    private void MakeUnavailable(string reason)
    {
        _captureUnavailable = reason;
        foreach (var entry in _waiting.ToArray()) {
            var wire = CookingNetworkWireCodec.Read<CookingNetworkWireCommand>(entry.Value.Envelope)!;
            var id = CookingNetworkWireCodec.DomainId(ServerSessionInstance, wire.Scope, wire.Command.Player, wire.StableCommandId);
            Complete(new(wire.Scope, wire.Command.Player, id, entry.Key.Source, entry.Key.Correlation,
                CookingNetworkDispositionKind.Cancelled, _captureUnavailable, null));
        }
        foreach (var connection in _connections.Values.Where(c => c.ClosedOrdinal == 0 && c.Participant is not null)) {
            connection.Ready = false; connection.Issued = null; connection.BaselineAwaitingAck = false;
            Reject(connection, "authority", _captureUnavailable);
        }
        RefreshSessionProjection();
    }
    private void MarkTerminal(Connection connection, long sequence)
    {
        if (connection.Participant is not { } player || sequence <= 0 || _participants[player].Generation != connection.Generation) return;
        connection.LastTerminalSequence = Math.Max(connection.LastTerminalSequence, sequence);
        _participants[player].LastTerminalSequence = Math.Max(_participants[player].LastTerminalSequence, sequence);
    }
    private void RefreshSessionProjection()
    {
        LatestSessionProjection = new(ServerSessionInstance, Array.AsReadOnly(_participants
            .OrderBy(p => p.Key.Value, StringComparer.Ordinal)
            .Select(p => new CookingNetworkParticipantProjection(p.Key, p.Value.Active is not null,
                p.Value.Generation, p.Value.LastValidatedSequence, p.Value.LastTerminalSequence,
                p.Value.Active?.Ready ?? false, _cleanup.Contains(p.Key))).ToArray()));
    }
    private void InstallCapture(CookingNetworkAuthorityCapture capture)
    {
        if (LatestCapture is not null && capture.Observation.Scope != LatestCapture.Observation.Scope) {
            foreach (var pair in _waiting.ToArray()) {
                var wire = CookingNetworkWireCodec.Read<CookingNetworkWireCommand>(pair.Value.Envelope)!;
                var domain = CookingNetworkWireCodec.DomainId(ServerSessionInstance, wire.Scope, pair.Value.Connection.Participant!.Value, wire.StableCommandId);
                Complete(new CookingNetworkDisposition(wire.Scope, pair.Value.Connection.Participant.Value, domain, pair.Key.Source, pair.Key.Correlation, CookingNetworkDispositionKind.Cancelled, "ScopeRetired", null));
            }
            _mapping.Clear(); _domain.Clear(); _cleanup.Clear();
            foreach (var c in _connections.Values) { c.Ready = false; c.Issued = null; c.BaselineAwaitingAck = false; }
        }
        LatestCapture = capture;
    }
    public void Dispose() { lock (_gate) { if (_disposed) return; _disposed = true; _remote.Dispose(); _local.Dispose(); _ingress.Clear(); _waiting.Clear(); } }
}
