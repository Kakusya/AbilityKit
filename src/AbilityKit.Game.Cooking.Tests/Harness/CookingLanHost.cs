using System.Collections.Concurrent;
using AbilityKit.Game.Cooking;
using LiteNetLib;

namespace AbilityKit.Game.Cooking.Tests.Harness;

public sealed class CookingLanHost : IAsyncDisposable
{
    private readonly CookingRecipeSimulation _simulation;
    private readonly CookingSessionDescriptor _descriptor;
    private readonly CookingLevelScope _levelScope;
    private readonly PlayerId _hostPlayer;
    private readonly PlayerId _clientPlayer;
    private readonly string _connectionKey;
    private readonly EventBasedNetListener _listener = new();
    private readonly ConcurrentDictionary<NetPeer, PlayerId> _peers = new();
    private readonly object _simulationGate = new();

    private NetManager? _manager;
    private long _snapshotSequence;
    private int _commandSequence;
    private long _hostFrameSequence;
    private bool _disposed;

    public int Port { get; private set; }
    public CookingRecipeSnapshot LatestSnapshot { get; private set; }

    public CookingLanHost(
        CookingRecipeSimulation simulation,
        CookingSessionDescriptor descriptor,
        CookingLevelScope levelScope,
        PlayerId hostPlayer,
        PlayerId clientPlayer,
        string connectionKey = "abilitykit-cooking-lan")
    {
        _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        _levelScope = levelScope ?? throw new ArgumentNullException(nameof(levelScope));
        _hostPlayer = hostPlayer;
        _clientPlayer = clientPlayer;
        _connectionKey = connectionKey;

        LatestSnapshot = _simulation.Snapshot();

        _listener.ConnectionRequestEvent += request => request.AcceptIfKey(_connectionKey);
        _listener.PeerConnectedEvent += OnPeerConnected;
        _listener.PeerDisconnectedEvent += (peer, info) => _peers.TryRemove(peer, out _);
        _listener.NetworkReceiveEvent += OnNetworkReceive;
    }

    public Task StartAsync()
    {
        if (_manager != null) throw new InvalidOperationException("Host already started.");

        _manager = new NetManager(_listener)
        {
            UnsyncedEvents = true,
            AutoRecycle = true,
            BroadcastReceiveEnabled = false
        };

        var bound = false;
        var startPort = 19200;
        for (var p = startPort; p < startPort + 200; p++)
        {
            if (_manager.Start(p))
            {
                Port = p;
                bound = true;
                break;
            }
        }

        if (!bound)
        {
            _manager = null;
            throw new InvalidOperationException("Failed to find available UDP port for CookingLanHost.");
        }

        return Task.CompletedTask;
    }

    private void OnPeerConnected(NetPeer peer)
    {
        _peers[peer] = _clientPlayer;
    }

    private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        var bytes = reader.GetRemainingBytes();
        if (bytes == null || bytes.Length == 0) return;

        if (!CookingLanCodec.TryDecode(bytes, out var envelope) || envelope == null) return;

        switch (envelope.Kind)
        {
            case CookingLanMessageKind.HandshakeRequest:
                HandleHandshake(peer, envelope);
                break;
            case CookingLanMessageKind.RecipeCommand:
                HandleCommand(peer, envelope);
                break;
        }
    }

    private void HandleHandshake(NetPeer peer, CookingLanEnvelope envelope)
    {
        var responsePayload = new CookingLanHandshakeAccepted(_clientPlayer.Value);
        var bytes = CookingLanCodec.Encode(CookingLanMessageKind.HandshakeAccepted, envelope.CorrelationId, responsePayload);
        peer.Send(bytes, DeliveryMethod.ReliableOrdered);

        BroadcastSnapshot();
    }

    private void HandleCommand(NetPeer peer, CookingLanEnvelope envelope)
    {
        if (!CookingLanCodec.TryReadPayload<CookingLanRecipeCommandPacket>(envelope, out var packet) || packet == null)
            return;

        CookingRecipeCommandResult result;
        lock (_simulationGate)
        {
            result = ExecuteInternalCommand(_clientPlayer, packet.Operation, packet.Item,
                packet.Recipe, null, packet.Station, packet.Container, packet.Order, 0);
        }

        var resultPayload = new CookingLanRecipeCommandResultPacket(
            result.Outcome,
            result.Reason,
            result.StateVersion,
            result.IsDuplicate,
            result.Events);

        var resultBytes = CookingLanCodec.Encode(CookingLanMessageKind.RecipeCommandResult, envelope.CorrelationId, resultPayload);
        peer.Send(resultBytes, DeliveryMethod.ReliableOrdered);

        if (result.Outcome == CookingRecipeOutcome.Accepted)
        {
            BroadcastSnapshot();
        }
    }

    public CookingRecipeCommandResult ExecuteHostLocalCommand(
        PlayerId player,
        CookingRecipeOperation operation,
        ItemId? item,
        StationSlotId? station = null,
        ItemId? container = null,
        RecipeId? recipe = null,
        OrderId? order = null)
    {
        CookingRecipeCommandResult result;
        lock (_simulationGate)
        {
            result = ExecuteInternalCommand(player, operation, item, recipe, null, station, container, order, 0);
        }

        if (result.Outcome == CookingRecipeOutcome.Accepted)
        {
            BroadcastSnapshot();
        }

        return result;
    }

    private CookingRecipeCommandResult ExecuteInternalCommand(
        PlayerId player,
        CookingRecipeOperation operation,
        ItemId? item,
        RecipeId? recipe,
        ProcessId? process,
        StationSlotId? station,
        ItemId? container,
        OrderId? order,
        int ticks)
    {
        var seq = Interlocked.Increment(ref _commandSequence);
        var commandId = new RecipeCommandId($"{player.Value}-cmd-{seq}");

        var expectedVersion = 0;
        if (item is not null)
        {
            var matched = _simulation.Snapshot().Items.FirstOrDefault(i => i.Id == item);
            if (matched is not null)
            {
                expectedVersion = matched.Version;
            }
        }

        var cmd = new CookingRecipeCommand(
            _descriptor.Scope,
            _descriptor.Epoch,
            player,
            commandId,
            operation,
            recipe,
            process,
            item,
            station,
            container,
            order,
            expectedVersion,
            ticks);

        var result = _simulation.Submit(cmd);
        LatestSnapshot = _simulation.Snapshot();
        return result;
    }

    public void AdvanceFixedTick(int tickCount)
    {
        lock (_simulationGate)
        {
            for (var i = 0; i < tickCount; i++)
            {
                var frame = Interlocked.Increment(ref _hostFrameSequence);
                _simulation.AdvanceFixedTick(_levelScope, frame);
            }
            LatestSnapshot = _simulation.Snapshot();
        }

        BroadcastSnapshot();
    }

    public void BroadcastSnapshot()
    {
        CookingRecipeSnapshot snapshot;
        lock (_simulationGate)
        {
            snapshot = LatestSnapshot;
        }

        var seq = Interlocked.Increment(ref _snapshotSequence);
        var packet = new CookingLanSnapshotPacket(seq, snapshot);
        var bytes = CookingLanCodec.Encode(CookingLanMessageKind.RecipeSnapshot, $"snap-{seq}", packet);

        foreach (var peer in _peers.Keys)
        {
            try
            {
                peer.Send(bytes, DeliveryMethod.ReliableOrdered);
            }
            catch
            {
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;

        _manager?.Stop();
        _manager = null;
        _peers.Clear();
        return ValueTask.CompletedTask;
    }
}
