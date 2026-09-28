using System.Collections.Concurrent;
using AbilityKit.Network.Transport.LiteNet;

namespace AbilityKit.Game.Cooking.Session;

public sealed class CookingSessionClient : IAsyncDisposable
{
    private LiteNetTransport _transport;
    private readonly PlayerId _expectedPlayer;
    private readonly string _connectionKey;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<CookingLanRecipeCommandResultPacket>> _pendingCommands = new();
    
    private TaskCompletionSource<bool> _connected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource<string> _handshake = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource<bool> _firstSnapshot = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private long _commandCounter;
    private bool _disposed;

    public string? ReconnectToken { get; private set; }
    public CookingRecipeSnapshot? LatestProjection { get; private set; }
    public CookingSessionSnapshot? LatestSessionProjection { get; private set; }
    public CookingLevelScope? CurrentLevelScope => LatestSessionProjection?.LevelScope;

    public CookingSessionClient(PlayerId expectedPlayer, string connectionKey = "abilitykit-cooking-lan")
    {
        _expectedPlayer = expectedPlayer;
        _connectionKey = connectionKey;
        _transport = CreateTransport();
    }

    private LiteNetTransport CreateTransport()
    {
        var transport = new LiteNetTransport(_connectionKey);
        transport.Connected += () => _connected.TrySetResult(true);
        transport.BytesReceived += OnBytesReceived;
        transport.Disconnected += () =>
        {
            foreach (var pending in _pendingCommands.Values)
            {
                pending.TrySetException(new IOException("Transport disconnected."));
            }
        };
        return transport;
    }

    public async Task ConnectAndHandshakeAsync(string host, int port, CancellationToken ct = default)
    {
        _transport.Connect(host, port);
        await _connected.Task.WaitAsync(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);

        var handshakePayload = new CookingLanHandshakeRequest(_expectedPlayer.Value, ReconnectToken);
        var bytes = CookingLanCodec.Encode(CookingLanMessageKind.HandshakeRequest, "client-handshake", handshakePayload);
        _transport.Send(new ArraySegment<byte>(bytes));

        await _handshake.Task.WaitAsync(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
        await _firstSnapshot.Task.WaitAsync(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
    }

    public async Task ReconnectAsync(string host, int port, CancellationToken ct = default)
    {
        _transport.Dispose();

        _connected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _handshake = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _firstSnapshot = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingCommands.Clear();

        _transport = CreateTransport();
        await ConnectAndHandshakeAsync(host, port, ct);
    }

    public void Disconnect()
    {
        _transport.Dispose();
    }

    private void OnBytesReceived(ArraySegment<byte> segment)
    {
        if (segment.Array == null || segment.Count == 0) return;

        var span = segment.AsSpan();
        if (!CookingLanCodec.TryDecode(span, out var envelope) || envelope == null) return;

        switch (envelope.Kind)
        {
            case CookingLanMessageKind.HandshakeAccepted:
                if (CookingLanCodec.TryReadPayload<CookingLanHandshakeAccepted>(envelope, out var accepted) && accepted != null)
                {
                    ReconnectToken = accepted.ReconnectToken;
                    _handshake.TrySetResult(accepted.AssignedPlayerId);
                }
                break;

            case CookingLanMessageKind.RecipeCommandResult:
                if (_pendingCommands.TryRemove(envelope.CorrelationId, out var tcs))
                {
                    if (CookingLanCodec.TryReadPayload<CookingLanRecipeCommandResultPacket>(envelope, out var result) && result != null)
                    {
                        tcs.TrySetResult(result);
                    }
                    else
                    {
                        tcs.TrySetException(new InvalidOperationException("Failed to decode command result payload."));
                    }
                }
                break;

            case CookingLanMessageKind.RecipeSnapshot:
                if (CookingLanCodec.TryReadPayload<CookingLanSnapshotPacket>(envelope, out var snapPacket) && snapPacket != null)
                {
                    if (LatestSessionProjection is null)
                    {
                        LatestProjection = snapPacket.Snapshot;
                        _firstSnapshot.TrySetResult(true);
                    }
                }
                break;

            case CookingLanMessageKind.SessionSnapshot:
                if (CookingLanCodec.TryReadPayload<CookingLanSessionSnapshotPacket>(envelope, out var sessionPacket) &&
                    sessionPacket != null &&
                    TryApplySessionSnapshot(sessionPacket.Snapshot, sessionPacket.Sha256))
                {
                    _firstSnapshot.TrySetResult(true);
                }
                break;
        }
    }

    public bool TryApplySessionSnapshot(CookingSessionSnapshot snapshot, string? expectedSha256 = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!string.IsNullOrWhiteSpace(expectedSha256) &&
            !StringComparer.Ordinal.Equals(expectedSha256, snapshot.Sha256()))
        {
            return false;
        }

        var current = LatestSessionProjection;
        if (current is not null)
        {
            if (snapshot.LevelScope.MatchScope != current.LevelScope.MatchScope ||
                snapshot.LevelScope.RestaurantRuntime != current.LevelScope.RestaurantRuntime ||
                snapshot.Generation < current.Generation)
            {
                return false;
            }

            if (snapshot.Generation == current.Generation)
            {
                if (snapshot.LevelScope != current.LevelScope || snapshot.Sequence <= current.Sequence)
                    return false;
            }
            else
            {
                if (snapshot.LevelScope.LevelEpoch <= current.LevelScope.LevelEpoch)
                    return false;
                foreach (var pending in _pendingCommands.Values)
                    pending.TrySetException(new InvalidOperationException("The command belongs to an ended Cooking Level."));
                _pendingCommands.Clear();
                Interlocked.Exchange(ref _commandCounter, 0);
            }
        }

        LatestSessionProjection = snapshot;
        LatestProjection = snapshot.Recipe;
        return true;
    }

    public async Task<CookingRecipeCommandResult> SendCommandAsync(
        CookingRecipeOperation operation,
        ItemId? item,
        StationSlotId? station = null,
        ItemId? container = null,
        RecipeId? recipe = null,
        OrderId? order = null,
        long? explicitCommandId = null,
        CookingLevelScope? levelScope = null,
        CancellationToken ct = default)
    {
        var commandId = explicitCommandId ?? Interlocked.Increment(ref _commandCounter);
        var correlationId = $"cmd-{_expectedPlayer.Value}-{commandId}";
        var tcs = new TaskCompletionSource<CookingLanRecipeCommandResultPacket>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingCommands[correlationId] = tcs;

        var packet = new CookingLanRecipeCommandPacket(
            commandId,
            operation,
            item,
            station,
            container,
            recipe,
            order,
            levelScope ?? LatestSessionProjection?.LevelScope);
        var bytes = CookingLanCodec.Encode(CookingLanMessageKind.RecipeCommand, correlationId, packet);
        _transport.Send(new ArraySegment<byte>(bytes));

        using var reg = ct.Register(() => tcs.TrySetCanceled(ct));
        var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);

        return new CookingRecipeCommandResult(
            result.Outcome,
            result.Reason,
            result.StateVersion,
            result.IsDuplicate,
            result.Events);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;

        _transport.Dispose();
        return ValueTask.CompletedTask;
    }
}
