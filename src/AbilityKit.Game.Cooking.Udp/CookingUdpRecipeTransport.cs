using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Channels;
using AbilityKit.Game.Cooking;
using LiteNetLib;
using LiteNetLib.Utils;

namespace AbilityKit.Game.Cooking.Udp;

/// <summary>
/// Cooking-owned fixture transport. It deliberately shares no rule implementation with the interaction adapter:
/// both local and UDP commands are decoded/enqueued and executed only by this host's single reader.
/// </summary>
public sealed class CookingUdpRecipeHost : IAsyncDisposable
{
    private readonly CookingRecipeSimulation _simulation;
    private readonly CookingSessionDescriptor _descriptor;
    private readonly CookingUdpRecipeHostOptions _options;
    private readonly EventBasedNetListener _listener = new();
    private readonly ConcurrentDictionary<NetPeer, ConnectionId> _connections = new();
    private readonly ConcurrentDictionary<ConnectionId, NetPeer> _peers = new();
    private readonly ConcurrentDictionary<ConnectionId, PlayerId> _bindings = new();
    private readonly ConcurrentDictionary<ConnectionId, long> _baselineReferences = new();
    private readonly Channel<RecipeWork> _inbound;
    private readonly CancellationTokenSource _stopping = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly AsyncLocal<bool> _transportCallback = new();
    private readonly List<CookingUdpTransportDiagnostic> _diagnostics = new();
    private NetManager? _manager;
    private Task? _dispatcher;
    private long _nextConnection;
    private long _snapshotSequence = 1;
    private long _publishedStateVersion;
    private int _droppedInboundCount;
    private bool _disposed;

    public CookingUdpRecipeHost(CookingRecipeSimulation simulation, CookingSessionDescriptor descriptor,
        CookingUdpRecipeHostOptions options)
    {
        _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(options.ConnectionKey)) throw new ArgumentException("Connection key must not be blank.", nameof(options));
        if (options.Port is <= 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(options));
        if (options.InboundQueueCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(options));
        ArgumentNullException.ThrowIfNull(options.RemotePlayerAssignment);
        _inbound = Channel.CreateBounded<RecipeWork>(new BoundedChannelOptions(options.InboundQueueCapacity)
        {
            SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait,
        });
        _publishedStateVersion = _simulation.Snapshot().Version;
        _listener.ConnectionRequestEvent += request => request.AcceptIfKey(options.ConnectionKey);
        _listener.PeerConnectedEvent += peer => ReceiveCallback(() => Enqueue(new ConnectionOpened(peer,
            new ConnectionId($"recipe-udp-{Interlocked.Increment(ref _nextConnection)}")), "connection-open", null));
        _listener.PeerDisconnectedEvent += (peer, _) => ReceiveCallback(() =>
        {
            if (_connections.TryGetValue(peer, out var connection)) Enqueue(new ConnectionLost(connection), "connection-loss", connection);
        });
        _listener.NetworkReceiveEvent += (peer, reader, _, _) => ReceiveCallback(() =>
        {
            if (reader.GetRemainingBytes() is { } bytes && _connections.TryGetValue(peer, out var connection))
            {
                var datagram = new DatagramReceived(connection, bytes.ToArray());
                if (!Enqueue(datagram, "datagram", connection))
                    _ = SendInboundQueueFullAsync(peer, connection, datagram.Bytes);
            }
        });
    }

    public int BoundPort { get; private set; }
    public bool AuthorityDispatcherOnly { get; private set; } = true;
    public int CallbackInvocationCount { get; private set; }
    public int DroppedInboundCount { get; private set; }
    public IReadOnlyList<CookingUdpTransportDiagnostic> Diagnostics => _diagnostics.ToArray();
    public CookingRecipeSnapshot Snapshot => _simulation.Snapshot();

    public Task StartAsync()
    {
        ThrowIfDisposed();
        if (_manager is not null) throw new InvalidOperationException("Cooking recipe UDP host is already started.");
        _manager = new NetManager(_listener) { UnsyncedEvents = true, AutoRecycle = true, BroadcastReceiveEnabled = false };
        if (!_manager.Start(_options.Port)) { _manager = null; throw new InvalidOperationException($"Failed to bind UDP port {_options.Port}."); }
        BoundPort = _manager.LocalPort;
        _dispatcher = Task.Run(DispatchAsync);
        return Task.CompletedTask;
    }

    public async Task<CookingRecipeCommandResult> SubmitHostLocalAsync(CookingRecipeCommand command, string correlationId,
        CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<CookingRecipeCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!Enqueue(new LocalCommand(command, correlationId, cancellationToken, completion), "host-local-command", null))
            throw new InvalidOperationException("Cooking recipe UDP dispatcher is unavailable or at capacity.");
        return await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private bool Enqueue(RecipeWork work, string eventType, ConnectionId? connection)
    {
        CallbackInvocationCount++;
        if (_inbound.Writer.TryWrite(work)) return true;
        Interlocked.Increment(ref _droppedInboundCount);
        Record(eventType + "-dropped", connection, "inbound-queue-full-or-closed");
        return false;
    }

    private async Task DispatchAsync()
    {
        var local = new ConnectionId("recipe-host-local");
        _bindings[local] = _options.HostLocalPlayer;
        try
        {
            await foreach (var work in _inbound.Reader.ReadAllAsync(_stopping.Token).ConfigureAwait(false))
            {
                try
                {
                    switch (work)
                    {
                        case ConnectionOpened opened:
                            _connections[opened.Peer] = opened.OpenedConnection;
                            _peers[opened.OpenedConnection] = opened.Peer;
                            break;
                        case ConnectionLost lost:
                            RemoveConnection(lost.LostConnection, "peer-disconnected");
                            break;
                        case DatagramReceived datagram:
                            await HandleDatagramAsync(datagram).ConfigureAwait(false);
                            break;
                        case LocalCommand command:
                            await HandleLocalAsync(local, command).ConfigureAwait(false);
                            break;
                    }
                }
                catch (Exception exception)
                {
                    Record("dispatcher-work-failed", work.Connection, exception.GetType().Name);
                    if (work is LocalCommand localCommand) localCommand.Completion.TrySetException(exception);
                }
            }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
    }

    private async Task HandleDatagramAsync(DatagramReceived datagram)
    {
        if (!CookingUdpCodec.TryDecode(datagram.Bytes, out var envelope, out var reason) || envelope is null)
        {
            await SendClosedAsync(datagram.DatagramConnection, reason).ConfigureAwait(false);
            return;
        }
        if (!IsCompatible(envelope, out var compatibility))
        {
            await SendAsync(datagram.DatagramConnection, CookingUdpMessageKind.HandshakeRejected, envelope.CorrelationId,
                new CookingUdpHandshakeRejected(compatibility)).ConfigureAwait(false);
            return;
        }

        switch (envelope.Kind)
        {
            case CookingUdpMessageKind.HandshakeRequest:
                if (!CookingUdpCodec.TryReadPayload<CookingUdpHandshakeRequest>(envelope, out var request, out var requestReason) ||
                    request!.Capabilities is null || !_descriptor.RequiredCapabilities.All(request.Capabilities.Contains))
                {
                    await SendAsync(datagram.DatagramConnection, CookingUdpMessageKind.HandshakeRejected, envelope.CorrelationId,
                        new CookingUdpHandshakeRejected(CookingSessionReason.CapabilityMismatch)).ConfigureAwait(false);
                    return;
                }
                var assigned = _options.RemotePlayerAssignment(datagram.DatagramConnection);
                _bindings[datagram.DatagramConnection] = assigned;
                await SendAsync(datagram.DatagramConnection, CookingUdpMessageKind.HandshakeAccepted, envelope.CorrelationId,
                    new CookingUdpHandshakeAccepted(assigned.Value)).ConfigureAwait(false);
                await SendBaselineAsync(datagram.DatagramConnection, envelope.CorrelationId).ConfigureAwait(false);
                break;
            case CookingUdpMessageKind.RecipeCommand:
                if (!_bindings.TryGetValue(datagram.DatagramConnection, out var bound))
                {
                    await SendRecipeRejectionAsync(datagram.DatagramConnection, envelope.CorrelationId,
                        CookingRecipeRejectionReason.PlayerNotFound).ConfigureAwait(false);
                    return;
                }
                if (!_baselineReferences.ContainsKey(datagram.DatagramConnection))
                {
                    await SendAsync(datagram.DatagramConnection, CookingUdpMessageKind.SynchronizationRejected, envelope.CorrelationId,
                        new CookingUdpSynchronizationRejected(CookingSessionReason.BaselineRequired, null, null)).ConfigureAwait(false);
                    return;
                }
                if (!CookingUdpCodec.TryReadPayload<CookingUdpRecipeCommandMessage>(envelope, out var recipeMessage, out _) ||
                    !CookingRecipeCommandValidation.IsWellFormed(recipeMessage?.Command))
                {
                    await SendRecipeRejectionAsync(datagram.DatagramConnection, envelope.CorrelationId,
                        CookingRecipeRejectionReason.MalformedCommand).ConfigureAwait(false);
                    return;
                }
                await ExecuteRemoteAsync(datagram.DatagramConnection, bound, recipeMessage!.Command, envelope.CorrelationId).ConfigureAwait(false);
                break;
            default:
                await SendClosedAsync(datagram.DatagramConnection, "message-kind-not-accepted").ConfigureAwait(false);
                break;
        }
    }

    private async Task HandleLocalAsync(ConnectionId local, LocalCommand work)
    {
        try
        {
            if (!CookingRecipeCommandValidation.IsWellFormed(work.Command))
            {
                work.Completion.TrySetResult(CookingRecipeCommandResult.Reject(CookingRecipeRejectionReason.MalformedCommand,
                    Interlocked.Read(ref _publishedStateVersion)));
                return;
            }
            if (work.CancellationToken.IsCancellationRequested) { work.Completion.TrySetCanceled(work.CancellationToken); return; }
            var result = InvokeAuthority(() => _simulation.Submit(work.Command with { Player = _options.HostLocalPlayer }));
            PublishStateVersion(result.StateVersion);
            if (result.Outcome == CookingRecipeOutcome.Accepted) await BroadcastDeltaAsync(work.CorrelationId).ConfigureAwait(false);
            work.Completion.TrySetResult(result);
        }
        catch (Exception exception) { work.Completion.TrySetException(exception); }
    }

    private async Task ExecuteRemoteAsync(ConnectionId connection, PlayerId bound, CookingRecipeCommand claimed, string correlationId)
    {
        var before = Snapshot.Sha256();
        CookingRecipeCommandResult result;
        try
        {
            if (claimed is null || !CookingRecipeCommandValidation.IsWellFormed(claimed))
                result = CookingRecipeCommandResult.Reject(CookingRecipeRejectionReason.MalformedCommand, Interlocked.Read(ref _publishedStateVersion));
            else if (claimed.Player != bound)
                result = CookingRecipeCommandResult.Reject(CookingRecipeRejectionReason.PlayerNotFound, Interlocked.Read(ref _publishedStateVersion));
            else
                result = InvokeAuthority(() => _simulation.Submit(claimed with { Scope = _descriptor.Scope, Player = bound }));
            PublishStateVersion(result.StateVersion);
        }
        catch (ArgumentException)
        {
            result = CookingRecipeCommandResult.Reject(CookingRecipeRejectionReason.MalformedCommand, Interlocked.Read(ref _publishedStateVersion));
        }
        catch (JsonException)
        {
            result = CookingRecipeCommandResult.Reject(CookingRecipeRejectionReason.MalformedCommand, Interlocked.Read(ref _publishedStateVersion));
        }
        await SendAsync(connection, CookingUdpMessageKind.RecipeCommandResult, correlationId,
            new CookingUdpRecipeCommandResultMessage(result.Outcome, result.Reason, result.StateVersion, result.IsDuplicate, result.Events)).ConfigureAwait(false);
        Record("recipe-command-executed", connection, result.Outcome.ToString() + ":" + result.Reason + ":" + before + ":" + Snapshot.Sha256());
        if (result.Outcome == CookingRecipeOutcome.Accepted) await BroadcastDeltaAsync(correlationId).ConfigureAwait(false);
    }

    private async Task SendBaselineAsync(ConnectionId connection, string correlationId)
    {
        var sequence = Interlocked.Read(ref _snapshotSequence);
        var snapshot = Snapshot;
        _baselineReferences[connection] = sequence;
        await SendAsync(connection, CookingUdpMessageKind.RecipeBaseline, correlationId,
            new CookingUdpRecipeSnapshotMessage(sequence, sequence, snapshot.Sha256(), snapshot)).ConfigureAwait(false);
    }

    private async Task BroadcastDeltaAsync(string correlationId)
    {
        var sequence = Interlocked.Increment(ref _snapshotSequence);
        var snapshot = Snapshot;
        foreach (var (connection, baseline) in _baselineReferences.ToArray())
        {
            if (_peers.ContainsKey(connection))
                await SendAsync(connection, CookingUdpMessageKind.RecipeDelta, correlationId,
                    new CookingUdpRecipeSnapshotMessage(sequence, baseline, snapshot.Sha256(), snapshot)).ConfigureAwait(false);
        }
    }

    private bool IsCompatible(CookingUdpEnvelope envelope, out CookingSessionReason reason)
    {
        if (!Equals(envelope.Scope.ToDomain(), _descriptor.Scope)) { reason = CookingSessionReason.ScopeMismatch; return false; }
        if (envelope.Epoch != _descriptor.Epoch) { reason = CookingSessionReason.EpochMismatch; return false; }
        if (!StringComparer.Ordinal.Equals(envelope.ConfigIdentity, _descriptor.ConfigIdentity)) { reason = CookingSessionReason.ConfigMismatch; return false; }
        if (!StringComparer.Ordinal.Equals(envelope.ProtocolName, _descriptor.Protocol.Name)) { reason = CookingSessionReason.ProtocolIdentityMismatch; return false; }
        if (!_descriptor.Protocol.Supports(envelope.ProtocolName, envelope.ProtocolVersion)) { reason = CookingSessionReason.ProtocolVersionUnsupported; return false; }
        reason = CookingSessionReason.None;
        return true;
    }

    private async Task SendRecipeRejectionAsync(ConnectionId connection, string correlationId, CookingRecipeRejectionReason reason) =>
        await SendAsync(connection, CookingUdpMessageKind.RecipeCommandResult, correlationId,
            reason == CookingRecipeRejectionReason.MalformedCommand
                ? CreateMalformedCommandResult(Interlocked.Read(ref _publishedStateVersion))
                : new CookingUdpRecipeCommandResultMessage(CookingRecipeOutcome.Rejected, reason,
                    Interlocked.Read(ref _publishedStateVersion), false, Array.Empty<CookingRecipeEvent>())).ConfigureAwait(false);

    private async Task SendInboundQueueFullAsync(NetPeer peer, ConnectionId connection, byte[] datagram)
    {
        if (!CookingUdpCodec.TryDecode(datagram, out var envelope, out _) || envelope is null ||
            envelope.Kind != CookingUdpMessageKind.RecipeCommand)
        {
            return;
        }

        try
        {
            var bytes = CookingUdpCodec.Encode(CookingUdpMessageKind.RecipeCommandResult, _descriptor, envelope.CorrelationId,
                CreateQueueFullResult(Interlocked.Read(ref _publishedStateVersion)));
            await _sendGate.WaitAsync(_stopping.Token).ConfigureAwait(false);
            try { peer.Send(bytes, DeliveryMethod.ReliableOrdered); }
            finally { _sendGate.Release(); }
            Record("queue-full-rejected", connection, envelope.CorrelationId);
        }
        catch (Exception exception) when (exception is OperationCanceledException or InvalidOperationException or ArgumentException or SocketException)
        {
            Record("queue-full-response-failed", connection, exception.GetType().Name);
        }
    }

    private async Task SendClosedAsync(ConnectionId connection, string reason) =>
        await SendAsync(connection, CookingUdpMessageKind.TransportClosed, $"recipe-udp-reject-{Guid.NewGuid():N}",
            new CookingUdpTransportClosed(reason)).ConfigureAwait(false);

    private async Task SendAsync<T>(ConnectionId connection, CookingUdpMessageKind kind, string correlationId, T payload)
    {
        if (!_peers.TryGetValue(connection, out var peer)) return;
        byte[] bytes;
        try { bytes = CookingUdpCodec.Encode(kind, _descriptor, correlationId, payload); }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or JsonException)
        {
            Record("outbound-encode-rejected", connection, exception.Message);
            return;
        }
        try
        {
            await _sendGate.WaitAsync(_stopping.Token).ConfigureAwait(false);
            try { peer.Send(bytes, DeliveryMethod.ReliableOrdered); }
            finally { _sendGate.Release(); }
        }
        catch (Exception exception) when (exception is OperationCanceledException or InvalidOperationException or SocketException)
        {
            Record("outbound-send-failed", connection, exception.GetType().Name);
        }
    }

    private void PublishStateVersion(long stateVersion) => Interlocked.Exchange(ref _publishedStateVersion, stateVersion);

    private void RemoveConnection(ConnectionId connection, string detail)
    {
        _peers.TryRemove(connection, out _);
        _baselineReferences.TryRemove(connection, out _);
        _bindings.TryRemove(connection, out _);
        Record("transport-lost", connection, detail);
    }

    private void ReceiveCallback(Action action)
    {
        _transportCallback.Value = true;
        try { action(); }
        finally { _transportCallback.Value = false; }
    }

    private T InvokeAuthority<T>(Func<T> action)
    {
        if (_transportCallback.Value) AuthorityDispatcherOnly = false;
        return action();
    }

    private void Record(string eventType, ConnectionId? connection, string detail) =>
        _diagnostics.Add(new CookingUdpTransportDiagnostic(eventType, $"recipe-udp-{Guid.NewGuid():N}", connection?.Value,
            detail, DateTimeOffset.UtcNow.ToString("O")));

    public static CookingUdpRecipeCommandResultMessage CreateQueueFullResult(long stateVersion) =>
        new(CookingRecipeOutcome.Rejected, CookingRecipeRejectionReason.QueueFull, stateVersion, false,
            Array.Empty<CookingRecipeEvent>());

    public static CookingUdpRecipeCommandResultMessage CreateMalformedCommandResult(long stateVersion) =>
        new(CookingRecipeOutcome.Rejected, CookingRecipeRejectionReason.MalformedCommand, stateVersion, false,
            Array.Empty<CookingRecipeEvent>());

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(CookingUdpRecipeHost));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _stopping.Cancel();
        _inbound.Writer.TryComplete();
        _manager?.Stop();
        if (_dispatcher is not null) await _dispatcher.ConfigureAwait(false);
        _stopping.Dispose();
        _sendGate.Dispose();
    }

    private abstract record RecipeWork { public virtual ConnectionId? Connection => null; }
    private sealed record ConnectionOpened(NetPeer Peer, ConnectionId OpenedConnection) : RecipeWork
    { public override ConnectionId? Connection => OpenedConnection; }
    private sealed record ConnectionLost(ConnectionId LostConnection) : RecipeWork { public override ConnectionId? Connection => LostConnection; }
    private sealed record DatagramReceived(ConnectionId DatagramConnection, byte[] Bytes) : RecipeWork { public override ConnectionId? Connection => DatagramConnection; }
    private sealed record LocalCommand(CookingRecipeCommand Command, string CorrelationId, CancellationToken CancellationToken,
        TaskCompletionSource<CookingRecipeCommandResult> Completion) : RecipeWork;
}

public sealed class CookingUdpRecipeClient : IAsyncDisposable
{
    private readonly CookingSessionDescriptor _descriptor;
    private readonly CookingUdpClientOptions _options;
    private readonly EventBasedNetListener _listener = new();
    private readonly TaskCompletionSource<bool> _connected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<CookingUdpHandshakeAccepted> _handshake = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<CookingUdpRecipeSnapshotMessage> _baseline = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<CookingUdpRecipeCommandResultMessage>> _results = new(StringComparer.Ordinal);
    private readonly Channel<CookingUdpRecipeSnapshotMessage> _deltas = Channel.CreateUnbounded<CookingUdpRecipeSnapshotMessage>();
    private NetManager? _manager;
    private NetPeer? _peer;

    public CookingUdpRecipeClient(CookingSessionDescriptor descriptor, CookingUdpClientOptions options)
    {
        _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        Projection = new CookingUdpClientProjection(descriptor);
        _listener.PeerConnectedEvent += peer => { _peer = peer; _connected.TrySetResult(true); };
        _listener.PeerDisconnectedEvent += (_, _) => Projection.ReportTransportLoss();
        _listener.NetworkReceiveEvent += (_, reader, _, _) => Receive(reader.GetRemainingBytes());
    }

    public CookingUdpClientProjection Projection { get; }
    public string? AssignedPlayerId { get; private set; }

    public async Task ConnectAndHandshakeAsync(CancellationToken cancellationToken = default)
    {
        _manager = new NetManager(_listener) { UnsyncedEvents = true, AutoRecycle = true };
        if (!_manager.Start()) throw new InvalidOperationException("Cooking recipe UDP client failed to start.");
        _manager.Connect(_options.Host, _options.Port, _options.ConnectionKey);
        await _connected.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        Send(CookingUdpMessageKind.HandshakeRequest, "recipe-udp-handshake",
            new CookingUdpHandshakeRequest(_descriptor.RequiredCapabilities.OrderBy(value => value, StringComparer.Ordinal).ToArray()));
        AssignedPlayerId = (await _handshake.Task.WaitAsync(cancellationToken).ConfigureAwait(false)).PlayerId;
        var baseline = await _baseline.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        var installed = Projection.InstallRecipeBaseline(baseline);
        if (!installed.Accepted) throw new InvalidOperationException($"Cooking recipe baseline rejected: {installed.Reason}.");
    }

    public async Task<CookingUdpRecipeCommandResultMessage> SendCommandAsync(CookingRecipeCommand command,
        CancellationToken cancellationToken = default)
    {
        var correlation = $"recipe-udp-command-{command.Command.Value}";
        var completion = new TaskCompletionSource<CookingUdpRecipeCommandResultMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_results.TryAdd(correlation, completion)) throw new InvalidOperationException($"Duplicate pending command '{correlation}'.");
        try
        {
            Send(CookingUdpMessageKind.RecipeCommand, correlation, new CookingUdpRecipeCommandMessage(command));
            return await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { _results.TryRemove(correlation, out _); }
    }

    public async Task<CookingSynchronizationResult> WaitForDeltaAsync(CancellationToken cancellationToken = default) =>
        Projection.ApplyRecipeDelta(await _deltas.Reader.ReadAsync(cancellationToken).ConfigureAwait(false));

    private void Receive(byte[]? bytes)
    {
        if (bytes is null || !CookingUdpCodec.TryDecode(bytes, out var envelope, out _) || envelope is null || !IsCompatible(envelope))
        { Projection.ReportTransportLoss(); return; }
        switch (envelope.Kind)
        {
            case CookingUdpMessageKind.HandshakeAccepted when CookingUdpCodec.TryReadPayload<CookingUdpHandshakeAccepted>(envelope, out var accepted, out _): _handshake.TrySetResult(accepted!); break;
            case CookingUdpMessageKind.HandshakeRejected when CookingUdpCodec.TryReadPayload<CookingUdpHandshakeRejected>(envelope, out var rejected, out _): _handshake.TrySetException(new InvalidOperationException($"Cooking recipe UDP handshake rejected: {rejected!.Reason}.")); break;
            case CookingUdpMessageKind.RecipeBaseline when CookingUdpCodec.TryReadPayload<CookingUdpRecipeSnapshotMessage>(envelope, out var baseline, out _): _baseline.TrySetResult(baseline!); break;
            case CookingUdpMessageKind.RecipeCommandResult when CookingUdpCodec.TryReadPayload<CookingUdpRecipeCommandResultMessage>(envelope, out var result, out _): if (_results.TryGetValue(envelope.CorrelationId, out var completion)) completion.TrySetResult(result!); break;
            case CookingUdpMessageKind.RecipeDelta when CookingUdpCodec.TryReadPayload<CookingUdpRecipeSnapshotMessage>(envelope, out var delta, out _): _deltas.Writer.TryWrite(delta!); break;
            case CookingUdpMessageKind.TransportClosed: Projection.ReportTransportLoss(); break;
        }
    }

    private bool IsCompatible(CookingUdpEnvelope envelope) => Equals(envelope.Scope.ToDomain(), _descriptor.Scope) &&
        envelope.Epoch == _descriptor.Epoch && StringComparer.Ordinal.Equals(envelope.ConfigIdentity, _descriptor.ConfigIdentity) &&
        StringComparer.Ordinal.Equals(envelope.ProtocolName, _descriptor.Protocol.Name) &&
        _descriptor.Protocol.Supports(envelope.ProtocolName, envelope.ProtocolVersion);

    private void Send<T>(CookingUdpMessageKind kind, string correlationId, T payload)
    {
        var peer = _peer ?? throw new InvalidOperationException("Cooking recipe UDP client is not connected.");
        peer.Send(CookingUdpCodec.Encode(kind, _descriptor, correlationId, payload), DeliveryMethod.ReliableOrdered);
    }

    public ValueTask DisposeAsync()
    {
        _manager?.Stop(); _manager = null; _peer = null; return ValueTask.CompletedTask;
    }
}
