using System.Collections.Concurrent;
using AbilityKit.Game.Cooking;
using AbilityKit.Network.Transport.LiteNet;

namespace AbilityKit.Game.Cooking.Tests.Harness;

public sealed class CookingLanClient : IAsyncDisposable
{
    private readonly LiteNetTransport _transport;
    private readonly PlayerId _expectedPlayer;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<CookingLanRecipeCommandResultPacket>> _pendingCommands = new();
    private readonly TaskCompletionSource<bool> _connected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<string> _handshake = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _firstSnapshot = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _commandCounter;
    private bool _disposed;

    public CookingRecipeSnapshot? LatestProjection { get; private set; }

    public CookingLanClient(PlayerId expectedPlayer, string connectionKey = "abilitykit-cooking-lan")
    {
        _expectedPlayer = expectedPlayer;
        _transport = new LiteNetTransport(connectionKey);

        _transport.Connected += () => _connected.TrySetResult(true);
        _transport.BytesReceived += OnBytesReceived;
        _transport.Disconnected += () =>
        {
            foreach (var pending in _pendingCommands.Values)
            {
                pending.TrySetException(new IOException("Transport disconnected."));
            }
        };
    }

    public async Task ConnectAndHandshakeAsync(string host, int port, CancellationToken ct = default)
    {
        _transport.Connect(host, port);
        await _connected.Task.WaitAsync(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);

        var handshakePayload = new CookingLanHandshakeRequest(_expectedPlayer.Value);
        var bytes = CookingLanCodec.Encode(CookingLanMessageKind.HandshakeRequest, "client-handshake", handshakePayload);
        _transport.Send(new ArraySegment<byte>(bytes));

        await _handshake.Task.WaitAsync(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
        await _firstSnapshot.Task.WaitAsync(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
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
                    LatestProjection = snapPacket.Snapshot;
                    _firstSnapshot.TrySetResult(true);
                }
                break;
        }
    }

    public async Task<CookingRecipeCommandResult> SendCommandAsync(
        CookingRecipeOperation operation,
        ItemId? item,
        StationSlotId? station = null,
        ItemId? container = null,
        RecipeId? recipe = null,
        OrderId? order = null,
        CancellationToken ct = default)
    {
        var seq = Interlocked.Increment(ref _commandCounter);
        var correlationId = $"cmd-{_expectedPlayer.Value}-{seq}";
        var tcs = new TaskCompletionSource<CookingLanRecipeCommandResultPacket>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingCommands[correlationId] = tcs;

        var packet = new CookingLanRecipeCommandPacket(operation, item, station, container, recipe, order);
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
