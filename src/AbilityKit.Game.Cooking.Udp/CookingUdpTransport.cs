using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Channels;
using LiteNetLib;
using LiteNetLib.Utils;

namespace AbilityKit.Game.Cooking.Udp;

public sealed record CookingUdpHostOptions(string ConnectionKey, int Port, PlayerId HostLocalPlayer,
    Func<ConnectionId, PlayerId> RemotePlayerAssignment, int InboundQueueCapacity = 64)
{
    public const string DefaultConnectionKey = "abilitykit-cooking-test";
}

public sealed record CookingUdpClientOptions(string ConnectionKey, string Host, int Port);

public sealed record CookingUdpTransportDiagnostic(string EventType, string CorrelationId, string? ConnectionId, string Detail,
    string TimestampUtc);

public sealed class CookingUdpHost : IAsyncDisposable
{
    private readonly CookingSessionAuthority _authority;
    private readonly CookingUdpHostOptions _options;
    private readonly EventBasedNetListener _listener = new();
    private readonly ConcurrentDictionary<NetPeer, ConnectionId> _connections = new();
    private readonly ConcurrentDictionary<ConnectionId, NetPeer> _peers = new();
    private readonly ConcurrentDictionary<ConnectionId, long> _baselineReferences = new();
    private readonly Channel<InboundWork> _inbound;
    private readonly CancellationTokenSource _stopping = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly AsyncLocal<bool> _transportCallback = new();
    private readonly object _lifecycleGate = new();
    private readonly List<CookingUdpTransportDiagnostic> _diagnostics = new();
    private NetManager? _manager;
    private Task? _dispatcher;
    private long _nextConnection;
    private int _callbackInvocationCount;
    private int _droppedInboundCount;
    private long _snapshotSequence = 1;
    private bool _disposed;

    public CookingUdpHost(CookingSessionAuthority authority, CookingUdpHostOptions options)
    {
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(options.ConnectionKey))
            throw new ArgumentException("Connection key must not be blank.", nameof(options));
        if (options.Port is <= 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(options));
        if (options.InboundQueueCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Inbound queue capacity must be positive.");
        ArgumentNullException.ThrowIfNull(options.RemotePlayerAssignment);

        _inbound = Channel.CreateBounded<InboundWork>(new BoundedChannelOptions(options.InboundQueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });
        _listener.ConnectionRequestEvent += request => request.AcceptIfKey(_options.ConnectionKey);
        _listener.PeerConnectedEvent += peer => ReceiveCallback(() => Enqueue(new ConnectionOpened(peer,
            new ConnectionId($"udp-{Interlocked.Increment(ref _nextConnection)}")), "connection-open", null));
        _listener.PeerDisconnectedEvent += (peer, _) => ReceiveCallback(() =>
        {
            if (_connections.TryGetValue(peer, out var connection))
                Enqueue(new ConnectionLost(connection), "connection-loss", connection);
        });
        _listener.NetworkReceiveEvent += (peer, reader, _, _) => ReceiveCallback(() =>
        {
            var bytes = reader.GetRemainingBytes();
            if (bytes is not null && _connections.TryGetValue(peer, out var connection))
            {
                var work = new DatagramReceived(connection, bytes.ToArray());
                if (!Enqueue(work, "datagram", connection))
                    _ = SendInboundQueueFullAsync(peer, connection, work.Bytes);
            }
        });
    }

    public int BoundPort { get; private set; }
    public int CallbackInvocationCount => _callbackInvocationCount;
    public int DroppedInboundCount => _droppedInboundCount;
    public bool AuthorityDispatcherOnly { get; private set; } = true;
    public bool IsStarted => _manager is not null;
    public IReadOnlyList<CookingUdpTransportDiagnostic> Diagnostics => _diagnostics.ToArray();

    public Task StartAsync()
    {
        lock (_lifecycleGate)
        {
            ThrowIfDisposed();
            if (_manager is not null)
                throw new InvalidOperationException("Cooking UDP host is already started.");
            _manager = new NetManager(_listener)
            {
                UnsyncedEvents = true,
                AutoRecycle = true,
                BroadcastReceiveEnabled = false,
            };
            if (!_manager.Start(_options.Port))
            {
                _manager = null;
                throw new InvalidOperationException($"Cooking UDP host failed to bind port {_options.Port}.");
            }
            BoundPort = _manager.LocalPort;
            _dispatcher = Task.Run(DispatchAsync);
        }
        return Task.CompletedTask;
    }

    public async Task<CookingSessionCommandResult> SubmitHostLocalAsync(CookingCommand command, string correlationId,
        CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<CookingSessionCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!Enqueue(new LocalCommand(command, correlationId, cancellationToken, completion), "host-local-command", null))
            throw new InvalidOperationException("Cooking UDP dispatcher is unavailable or at bounded capacity.");
        return await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync()
    {
        Task? dispatcher;
        NetManager? manager;
        lock (_lifecycleGate)
        {
            manager = _manager;
            _manager = null;
            dispatcher = _dispatcher;
            _dispatcher = null;
            _stopping.Cancel();
            _inbound.Writer.TryComplete();
        }
        manager?.Stop();
        if (dispatcher is not null)
            await dispatcher.ConfigureAwait(false);
        _connections.Clear();
        _peers.Clear();
        _baselineReferences.Clear();
    }

    private bool Enqueue(InboundWork work, string eventType, ConnectionId? connection)
    {
        Interlocked.Increment(ref _callbackInvocationCount);
        if (_inbound.Writer.TryWrite(work))
            return true;
        Interlocked.Increment(ref _droppedInboundCount);
        RecordTransport(eventType + "-dropped", connection, "inbound-queue-full-or-closed");
        return false;
    }

    private async Task DispatchAsync()
    {
        var descriptor = _authority.Descriptor;
        var local = new ConnectionId("host-local");
        InvokeAuthority(() =>
        {
            _authority.OpenConnection(local, CookingConnectionOrigin.HostLocal, "udp-host-local-open");
            _authority.CompleteHandshake(local, _options.HostLocalPlayer, CreateHandshake(descriptor), "udp-host-local-handshake");
        });

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
                            InvokeAuthority(() => _authority.OpenConnection(opened.OpenedConnection, CookingConnectionOrigin.RemoteUdp,
                                $"udp-open-{opened.OpenedConnection.Value}"));
                            break;
                        case ConnectionLost lost:
                            MarkTransportLost(lost.LostConnection, $"udp-loss-{lost.LostConnection.Value}", "peer-disconnected");
                            break;
                        case DatagramReceived datagram:
                            await HandleDatagramAsync(datagram, descriptor).ConfigureAwait(false);
                            break;
                        case LocalCommand localCommand:
                            await HandleLocalCommandAsync(local, localCommand).ConfigureAwait(false);
                            break;
                    }
                }
                catch (Exception exception)
                {
                    RecordTransport("dispatcher-work-failed", work.Connection, exception.GetType().Name);
                    if (work is LocalCommand localCommand)
                        localCommand.Completion.TrySetException(exception);
                    else if (work.Connection is { } connection)
                        MarkTransportLost(connection, $"udp-dispatch-failure-{Guid.NewGuid():N}", exception.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
        }
    }

    private async Task HandleDatagramAsync(DatagramReceived datagram, CookingSessionDescriptor descriptor)
    {
        if (!CookingUdpCodec.TryDecode(datagram.Bytes, out var envelope, out var decodeReason) || envelope is null)
        {
            await SendTransportClosedAsync(datagram.DatagramConnection, decodeReason).ConfigureAwait(false);
            return;
        }
        if (!IsCompatible(envelope, descriptor, out var compatibilityReason))
        {
            await SendAsync(datagram.DatagramConnection, CookingUdpMessageKind.HandshakeRejected, descriptor, envelope.CorrelationId,
                new CookingUdpHandshakeRejected(compatibilityReason)).ConfigureAwait(false);
            return;
        }

        switch (envelope.Kind)
        {
            case CookingUdpMessageKind.HandshakeRequest:
                if (!CookingUdpCodec.TryReadPayload<CookingUdpHandshakeRequest>(envelope, out var request, out var payloadReason))
                {
                    await SendTransportClosedAsync(datagram.DatagramConnection, payloadReason).ConfigureAwait(false);
                    return;
                }
                var handshake = new CookingHandshake(descriptor.Scope, envelope.ProtocolName, envelope.ProtocolVersion,
                    envelope.ConfigIdentity, request!.Capabilities.ToHashSet(StringComparer.Ordinal));
                var binding = InvokeAuthority(() => _authority.CompleteHandshake(datagram.DatagramConnection,
                    _options.RemotePlayerAssignment(datagram.DatagramConnection), handshake, envelope.CorrelationId));
                if (!binding.Accepted || binding.Binding is null)
                {
                    await SendAsync(datagram.DatagramConnection, CookingUdpMessageKind.HandshakeRejected, descriptor, envelope.CorrelationId,
                        new CookingUdpHandshakeRejected(binding.Reason)).ConfigureAwait(false);
                    return;
                }
                if (!await SendAsync(datagram.DatagramConnection, CookingUdpMessageKind.HandshakeAccepted, descriptor, envelope.CorrelationId,
                        new CookingUdpHandshakeAccepted(binding.Binding.Player.Value)).ConfigureAwait(false))
                    return;
                await SendBaselineAsync(datagram.DatagramConnection, descriptor, envelope.CorrelationId).ConfigureAwait(false);
                break;
            case CookingUdpMessageKind.Command:
                if (!_baselineReferences.ContainsKey(datagram.DatagramConnection))
                {
                    await SendAsync(datagram.DatagramConnection, CookingUdpMessageKind.SynchronizationRejected, descriptor, envelope.CorrelationId,
                        new CookingUdpSynchronizationRejected(CookingSessionReason.BaselineRequired, null, null)).ConfigureAwait(false);
                    return;
                }
                if (!CookingUdpCodec.TryReadPayload<CookingUdpCommandMessage>(envelope, out var commandMessage, out payloadReason))
                {
                    await SendTransportClosedAsync(datagram.DatagramConnection, payloadReason).ConfigureAwait(false);
                    return;
                }
                var ingress = InvokeAuthority(() => _authority.EnqueueCommand(datagram.DatagramConnection, commandMessage!.Command,
                    envelope.CorrelationId));
                var executions = ingress.Disposition == CookingSessionCommandDisposition.Queued
                    ? InvokeAuthority(_authority.ExecuteNextBatch)
                    : Array.Empty<CookingSessionCommandResult>();
                var result = executions.FirstOrDefault() ?? ingress;
                await SendAsync(datagram.DatagramConnection, CookingUdpMessageKind.CommandResult, descriptor, envelope.CorrelationId,
                    new CookingUdpCommandResultMessage(result.Disposition, result.Reason, result.QueueDepth, result.IsDuplicate,
                        result.AuthorityResult)).ConfigureAwait(false);
                if (result.AuthorityResult?.Outcome == CommandOutcome.Accepted)
                    await BroadcastDeltaAsync(descriptor, envelope.CorrelationId).ConfigureAwait(false);
                break;
            default:
                await SendTransportClosedAsync(datagram.DatagramConnection, "message-kind-not-accepted").ConfigureAwait(false);
                break;
        }
    }

    private async Task HandleLocalCommandAsync(ConnectionId local, LocalCommand work)
    {
        try
        {
            var ingress = InvokeAuthority(() => _authority.EnqueueCommand(local, work.Command, work.CorrelationId, work.CancellationToken));
            if (ingress.Disposition != CookingSessionCommandDisposition.Queued)
            {
                work.Completion.TrySetResult(ingress);
                return;
            }

            var executions = InvokeAuthority(_authority.ExecuteNextBatch);
            var result = executions.FirstOrDefault() ?? ingress;
            if (result.AuthorityResult?.Outcome == CommandOutcome.Accepted)
                await BroadcastDeltaAsync(_authority.Descriptor, work.CorrelationId).ConfigureAwait(false);
            work.Completion.TrySetResult(result);
        }
        catch (Exception exception)
        {
            work.Completion.TrySetException(exception);
        }
    }

    private async Task SendBaselineAsync(ConnectionId connection, CookingSessionDescriptor descriptor, string correlationId)
    {
        var sequence = Interlocked.Read(ref _snapshotSequence);
        var snapshot = GetSnapshot();
        if (!TryEncode(CookingUdpMessageKind.Baseline, descriptor, correlationId,
                new CookingUdpSnapshotMessage(sequence, sequence, snapshot.Sha256(), snapshot), out var bytes, out var failure))
        {
            RecordTransport("baseline-rejected", connection, failure);
            await SendTransportClosedAsync(connection, failure).ConfigureAwait(false);
            MarkTransportLost(connection, correlationId, failure);
            return;
        }

        var metadata = InvokeAuthority(() => _authority.CreateBaseline(sequence));
        var installed = InvokeAuthority(() => _authority.InstallBaseline(connection, metadata, correlationId));
        if (!installed.Accepted)
        {
            await SendAsync(connection, CookingUdpMessageKind.SynchronizationRejected, descriptor, correlationId,
                new CookingUdpSynchronizationRejected(installed.Reason, installed.SnapshotSequence, installed.BaselineReference)).ConfigureAwait(false);
            return;
        }
        _baselineReferences[connection] = sequence;
        if (!await SendBytesAsync(connection, bytes!, correlationId).ConfigureAwait(false))
            MarkTransportLost(connection, correlationId, "baseline-send-failed");
    }

    private async Task BroadcastDeltaAsync(CookingSessionDescriptor descriptor, string correlationId)
    {
        var sequence = Interlocked.Increment(ref _snapshotSequence);
        var snapshot = GetSnapshot();
        foreach (var (connection, baselineReference) in _baselineReferences.ToArray())
        {
            if (!_peers.ContainsKey(connection))
                continue;
            if (!TryEncode(CookingUdpMessageKind.Delta, descriptor, correlationId,
                    new CookingUdpSnapshotMessage(sequence, baselineReference, snapshot.Sha256(), snapshot), out var bytes, out var failure))
            {
                RecordTransport("delta-rejected", connection, failure);
                await SendTransportClosedAsync(connection, failure).ConfigureAwait(false);
                MarkTransportLost(connection, correlationId, failure);
                continue;
            }
            if (!await SendBytesAsync(connection, bytes!, correlationId).ConfigureAwait(false))
                MarkTransportLost(connection, correlationId, "delta-send-failed");
        }
    }

    private CookingSnapshot GetSnapshot() => InvokeAuthority(_authority.Snapshot);

    private async Task SendTransportClosedAsync(ConnectionId connection, string reason)
    {
        await SendAsync(connection, CookingUdpMessageKind.TransportClosed, _authority.Descriptor, $"udp-reject-{Guid.NewGuid():N}",
            new CookingUdpTransportClosed(reason)).ConfigureAwait(false);
    }

    private async Task SendInboundQueueFullAsync(NetPeer peer, ConnectionId connection, byte[] datagram)
    {
        if (!CookingUdpCodec.TryDecode(datagram, out var envelope, out _) || envelope is null ||
            envelope.Kind != CookingUdpMessageKind.Command)
            return;
        if (!TryEncode(CookingUdpMessageKind.CommandResult, _authority.Descriptor, envelope.CorrelationId,
                new CookingUdpCommandResultMessage(CookingSessionCommandDisposition.Rejected, CookingSessionReason.QueueFull, 0,
                    false, null), out var bytes, out var failure))
        {
            RecordTransport("queue-full-response-failed", connection, failure);
            return;
        }
        try
        {
            await _sendGate.WaitAsync(_stopping.Token).ConfigureAwait(false);
            try { peer.Send(bytes!, DeliveryMethod.ReliableOrdered); }
            finally { _sendGate.Release(); }
            RecordTransport("queue-full-rejected", connection, envelope.CorrelationId);
        }
        catch (Exception exception) when (exception is OperationCanceledException or InvalidOperationException or SocketException)
        {
            RecordTransport("queue-full-response-failed", connection, exception.GetType().Name);
        }
    }

    private bool TryEncode<T>(CookingUdpMessageKind kind, CookingSessionDescriptor descriptor, string correlationId, T payload,
        out byte[]? bytes, out string failure)
    {
        try
        {
            bytes = CookingUdpCodec.Encode(kind, descriptor, correlationId, payload);
            failure = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or JsonException)
        {
            bytes = null;
            failure = exception.Message;
            return false;
        }
    }

    private async Task<bool> SendAsync<T>(ConnectionId connection, CookingUdpMessageKind kind, CookingSessionDescriptor descriptor,
        string correlationId, T payload)
    {
        if (!TryEncode(kind, descriptor, correlationId, payload, out var bytes, out var failure))
        {
            RecordTransport("outbound-encode-rejected", connection, failure);
            MarkTransportLost(connection, correlationId, failure);
            return false;
        }
        return await SendBytesAsync(connection, bytes!, correlationId).ConfigureAwait(false);
    }

    private async Task<bool> SendBytesAsync(ConnectionId connection, byte[] bytes, string correlationId)
    {
        if (!_peers.TryGetValue(connection, out var peer))
            return false;
        try
        {
            await _sendGate.WaitAsync(_stopping.Token).ConfigureAwait(false);
            try
            {
                peer.Send(bytes, DeliveryMethod.ReliableOrdered);
                return true;
            }
            finally
            {
                _sendGate.Release();
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or InvalidOperationException or SocketException)
        {
            RecordTransport("outbound-send-failed", connection, exception.GetType().Name);
            return false;
        }
    }

    private void MarkTransportLost(ConnectionId connection, string correlationId, string detail)
    {
        _peers.TryRemove(connection, out _);
        _baselineReferences.TryRemove(connection, out _);
        InvokeAuthority(() => _authority.ReportTransportLoss(connection, correlationId));
        RecordTransport("transport-lost", connection, detail);
    }

    private static CookingHandshake CreateHandshake(CookingSessionDescriptor descriptor) => new(descriptor.Scope,
        descriptor.Protocol.Name, descriptor.Protocol.MaximumVersion, descriptor.ConfigIdentity,
        descriptor.RequiredCapabilities.ToHashSet(StringComparer.Ordinal));

    private static bool IsCompatible(CookingUdpEnvelope envelope, CookingSessionDescriptor descriptor, out CookingSessionReason reason)
    {
        if (!Equals(envelope.Scope.ToDomain(), descriptor.Scope)) { reason = CookingSessionReason.ScopeMismatch; return false; }
        if (envelope.Epoch != descriptor.Epoch) { reason = CookingSessionReason.EpochMismatch; return false; }
        if (!StringComparer.Ordinal.Equals(envelope.ConfigIdentity, descriptor.ConfigIdentity)) { reason = CookingSessionReason.ConfigMismatch; return false; }
        if (!StringComparer.Ordinal.Equals(envelope.ProtocolName, descriptor.Protocol.Name)) { reason = CookingSessionReason.ProtocolIdentityMismatch; return false; }
        if (!descriptor.Protocol.Supports(envelope.ProtocolName, envelope.ProtocolVersion)) { reason = CookingSessionReason.ProtocolVersionUnsupported; return false; }
        reason = CookingSessionReason.None;
        return true;
    }

    private void ReceiveCallback(Action callback)
    {
        _transportCallback.Value = true;
        try { callback(); }
        finally { _transportCallback.Value = false; }
    }

    private void InvokeAuthority(Action action)
    {
        if (_transportCallback.Value)
            AuthorityDispatcherOnly = false;
        action();
    }

    private T InvokeAuthority<T>(Func<T> operation)
    {
        if (_transportCallback.Value)
            AuthorityDispatcherOnly = false;
        return operation();
    }

    private void RecordTransport(string eventType, ConnectionId? connection, string detail) =>
        _diagnostics.Add(new CookingUdpTransportDiagnostic(eventType, $"udp-{Guid.NewGuid():N}", connection?.Value, detail,
            DateTimeOffset.UtcNow.ToString("O")));

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(CookingUdpHost));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        await StopAsync().ConfigureAwait(false);
        _stopping.Dispose();
        _sendGate.Dispose();
    }

    private abstract record InboundWork
    {
        public virtual ConnectionId? Connection => null;
    }
    private sealed record ConnectionOpened(NetPeer Peer, ConnectionId OpenedConnection) : InboundWork
    {
        public override ConnectionId? Connection => OpenedConnection;
    }
    private sealed record ConnectionLost(ConnectionId LostConnection) : InboundWork
    {
        public override ConnectionId? Connection => LostConnection;
    }
    private sealed record DatagramReceived(ConnectionId DatagramConnection, byte[] Bytes) : InboundWork
    {
        public override ConnectionId? Connection => DatagramConnection;
    }
    private sealed record LocalCommand(CookingCommand Command, string CorrelationId, CancellationToken CancellationToken,
        TaskCompletionSource<CookingSessionCommandResult> Completion) : InboundWork;
}

public sealed class CookingUdpClient : IAsyncDisposable
{
    private readonly CookingSessionDescriptor _descriptor;
    private readonly CookingUdpClientOptions _options;
    private readonly EventBasedNetListener _listener = new();
    private readonly TaskCompletionSource<bool> _connected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<CookingUdpHandshakeAccepted> _handshake = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<CookingUdpSnapshotMessage> _baseline = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<CookingUdpCommandResultMessage>> _commandResults = new(StringComparer.Ordinal);
    private readonly Channel<CookingUdpSnapshotMessage> _deltas = Channel.CreateUnbounded<CookingUdpSnapshotMessage>(new UnboundedChannelOptions
    {
        SingleReader = false,
        SingleWriter = false,
    });
    private NetManager? _manager;
    private NetPeer? _peer;

    public CookingUdpClient(CookingSessionDescriptor descriptor, CookingUdpClientOptions options)
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
        if (!_manager.Start())
            throw new InvalidOperationException("Cooking UDP client failed to start.");
        _manager.Connect(_options.Host, _options.Port, _options.ConnectionKey);
        await _connected.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        Send(CookingUdpMessageKind.HandshakeRequest, "udp-handshake",
            new CookingUdpHandshakeRequest(_descriptor.RequiredCapabilities.OrderBy(value => value, StringComparer.Ordinal).ToArray()));
        var accepted = await _handshake.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        AssignedPlayerId = accepted.PlayerId;
        var baseline = await _baseline.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        var result = Projection.InstallBaseline(baseline);
        if (!result.Accepted)
            throw new InvalidOperationException($"Cooking UDP baseline rejected: {result.Reason}.");
    }

    public async Task<CookingUdpCommandResultMessage> SendCommandAsync(CookingCommand command, CancellationToken cancellationToken = default)
    {
        var correlationId = $"udp-command-{command.Command.Value}";
        var completion = new TaskCompletionSource<CookingUdpCommandResultMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_commandResults.TryAdd(correlationId, completion))
            throw new InvalidOperationException($"Cooking UDP command correlation '{correlationId}' is already pending.");
        try
        {
            Send(CookingUdpMessageKind.Command, correlationId, new CookingUdpCommandMessage(command));
            return await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _commandResults.TryRemove(correlationId, out _);
        }
    }

    public async Task<CookingSynchronizationResult> WaitForDeltaAsync(CancellationToken cancellationToken = default)
    {
        var delta = await _deltas.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        return Projection.ApplyDelta(delta);
    }

    private void Receive(byte[]? bytes)
    {
        if (bytes is null || !CookingUdpCodec.TryDecode(bytes, out var envelope, out _) || envelope is null ||
            !IsCompatible(envelope, _descriptor, out _))
        {
            Projection.ReportTransportLoss();
            return;
        }
        switch (envelope.Kind)
        {
            case CookingUdpMessageKind.HandshakeAccepted when CookingUdpCodec.TryReadPayload<CookingUdpHandshakeAccepted>(envelope, out var accepted, out _):
                _handshake.TrySetResult(accepted!);
                break;
            case CookingUdpMessageKind.HandshakeRejected when CookingUdpCodec.TryReadPayload<CookingUdpHandshakeRejected>(envelope, out var rejected, out _):
                _handshake.TrySetException(new InvalidOperationException($"Cooking UDP handshake rejected: {rejected!.Reason}."));
                break;
            case CookingUdpMessageKind.Baseline when CookingUdpCodec.TryReadPayload<CookingUdpSnapshotMessage>(envelope, out var baseline, out _):
                _baseline.TrySetResult(baseline!);
                break;
            case CookingUdpMessageKind.CommandResult when CookingUdpCodec.TryReadPayload<CookingUdpCommandResultMessage>(envelope, out var result, out _):
                if (_commandResults.TryGetValue(envelope.CorrelationId, out var completion))
                    completion.TrySetResult(result!);
                break;
            case CookingUdpMessageKind.Delta when CookingUdpCodec.TryReadPayload<CookingUdpSnapshotMessage>(envelope, out var delta, out _):
                _deltas.Writer.TryWrite(delta!);
                break;
            case CookingUdpMessageKind.TransportClosed:
                Projection.ReportTransportLoss();
                break;
        }
    }

    private static bool IsCompatible(CookingUdpEnvelope envelope, CookingSessionDescriptor descriptor, out CookingSessionReason reason)
    {
        if (!Equals(envelope.Scope.ToDomain(), descriptor.Scope)) { reason = CookingSessionReason.ScopeMismatch; return false; }
        if (envelope.Epoch != descriptor.Epoch) { reason = CookingSessionReason.EpochMismatch; return false; }
        if (!StringComparer.Ordinal.Equals(envelope.ConfigIdentity, descriptor.ConfigIdentity)) { reason = CookingSessionReason.ConfigMismatch; return false; }
        if (!StringComparer.Ordinal.Equals(envelope.ProtocolName, descriptor.Protocol.Name)) { reason = CookingSessionReason.ProtocolIdentityMismatch; return false; }
        if (!descriptor.Protocol.Supports(envelope.ProtocolName, envelope.ProtocolVersion)) { reason = CookingSessionReason.ProtocolVersionUnsupported; return false; }
        reason = CookingSessionReason.None;
        return true;
    }

    private void Send<T>(CookingUdpMessageKind kind, string correlationId, T payload)
    {
        var peer = _peer ?? throw new InvalidOperationException("Cooking UDP client is not connected.");
        var bytes = CookingUdpCodec.Encode(kind, _descriptor, correlationId, payload);
        peer.Send(bytes, DeliveryMethod.ReliableOrdered);
    }

    public ValueTask DisposeAsync()
    {
        _manager?.Stop();
        _manager = null;
        _peer = null;
        return ValueTask.CompletedTask;
    }
}
