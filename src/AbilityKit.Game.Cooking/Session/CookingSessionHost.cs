using System.Collections.Concurrent;
using LiteNetLib;

namespace AbilityKit.Game.Cooking.Session;

public sealed class CookingSessionHost : IAsyncDisposable
{
    private sealed class PlayerSession
    {
        public PlayerId PlayerId { get; }
        public string ReconnectToken { get; }
        public NetPeer? CurrentPeer { get; set; }
        public bool IsConnected => CurrentPeer != null;
        public ConcurrentDictionary<long, CookingLanRecipeCommandResultPacket> ExecutedCommands { get; } = new();

        public PlayerSession(PlayerId playerId, string reconnectToken)
        {
            PlayerId = playerId;
            ReconnectToken = reconnectToken;
        }
    }

    private readonly CookingRecipeSimulation _simulation;
    private readonly CookingSessionDescriptor _descriptor;
    private readonly CookingLevelScope _levelScope;
    private readonly PlayerId _hostPlayer;
    private readonly PlayerId _clientPlayer;
    private readonly string _connectionKey;
    private readonly EventBasedNetListener _listener = new();
    private readonly ConcurrentDictionary<NetPeer, PlayerId> _peerToPlayer = new();
    private readonly ConcurrentDictionary<PlayerId, PlayerSession> _sessions = new();
    private readonly object _simulationGate = new();

    private NetManager? _manager;
    private long _snapshotSequence;
    private int _commandSequence;
    private long _hostFrameSequence;
    private bool _disposed;

    public int Port { get; private set; }
    public CookingRecipeSnapshot LatestSnapshot { get; private set; }

    public CookingSessionHost(
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

        _sessions[_hostPlayer] = new PlayerSession(_hostPlayer, Guid.NewGuid().ToString("N"));
        _sessions[_clientPlayer] = new PlayerSession(_clientPlayer, Guid.NewGuid().ToString("N"));

        LatestSnapshot = _simulation.Snapshot();

        _listener.ConnectionRequestEvent += request => request.AcceptIfKey(_connectionKey);
        _listener.PeerConnectedEvent += OnPeerConnected;
        _listener.PeerDisconnectedEvent += OnPeerDisconnected;
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
            throw new InvalidOperationException("Failed to find available UDP port for CookingSessionHost.");
        }

        return Task.CompletedTask;
    }

    private void OnPeerConnected(NetPeer peer)
    {
    }

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        if (_peerToPlayer.TryRemove(peer, out var playerId))
        {
            if (_sessions.TryGetValue(playerId, out var session) && session.CurrentPeer == peer)
            {
                session.CurrentPeer = null;
            }

            HandlePlayerDisconnected(playerId);
        }
    }

    private void HandlePlayerDisconnected(PlayerId playerId)
    {
        lock (_simulationGate)
        {
            var heldItem = _simulation.ItemInHand(playerId);
            if (heldItem != null)
            {
                var snap = _simulation.Snapshot();
                var occupiedStations = snap.Items
                    .Where(i => i.Location.Kind == LocationKind.StationSlot && i.Location.SlotId != null)
                    .Select(i => i.Location.SlotId!)
                    .ToHashSet();

                var defaultStations = new[]
                {
                    new StationSlotId("counter-a"),
                    new StationSlotId("board-a"),
                    new StationSlotId("board-b"),
                    new StationSlotId("stove-a")
                };

                StationSlotId? targetStation = null;
                foreach (var station in defaultStations)
                {
                    if (!occupiedStations.Contains(station.Value))
                    {
                        targetStation = station;
                        break;
                    }
                }

                if (targetStation != null)
                {
                    ExecuteInternalCommand(playerId, CookingRecipeOperation.Drop, heldItem,
                        null, null, targetStation, null, null, 0);
                }
            }

            LatestSnapshot = _simulation.Snapshot();
        }

        BroadcastSnapshot();
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
        if (!CookingLanCodec.TryReadPayload<CookingLanHandshakeRequest>(envelope, out var request) || request == null)
            return;

        var targetPlayer = _clientPlayer;
        if (!_sessions.TryGetValue(targetPlayer, out var session))
        {
            session = new PlayerSession(targetPlayer, Guid.NewGuid().ToString("N"));
            _sessions[targetPlayer] = session;
        }

        if (!string.IsNullOrEmpty(request.ReconnectToken))
        {
            if (session.ReconnectToken != request.ReconnectToken)
            {
                return;
            }
        }

        session.CurrentPeer = peer;
        _peerToPlayer[peer] = targetPlayer;

        var responsePayload = new CookingLanHandshakeAccepted(targetPlayer.Value, session.ReconnectToken);
        var bytes = CookingLanCodec.Encode(CookingLanMessageKind.HandshakeAccepted, envelope.CorrelationId, responsePayload);
        peer.Send(bytes, DeliveryMethod.ReliableOrdered);

        SendSnapshotToPeer(peer);
    }

    private void HandleCommand(NetPeer peer, CookingLanEnvelope envelope)
    {
        if (!CookingLanCodec.TryReadPayload<CookingLanRecipeCommandPacket>(envelope, out var packet) || packet == null)
            return;

        if (!_peerToPlayer.TryGetValue(peer, out var player))
        {
            player = _clientPlayer;
        }

        if (!_sessions.TryGetValue(player, out var session))
        {
            session = new PlayerSession(player, Guid.NewGuid().ToString("N"));
            _sessions[player] = session;
        }

        if (packet.CommandId > 0 && session.ExecutedCommands.TryGetValue(packet.CommandId, out var cachedResult))
        {
            var cachedBytes = CookingLanCodec.Encode(CookingLanMessageKind.RecipeCommandResult, envelope.CorrelationId, cachedResult);
            peer.Send(cachedBytes, DeliveryMethod.ReliableOrdered);
            return;
        }

        CookingRecipeCommandResult result;
        lock (_simulationGate)
        {
            result = ExecuteInternalCommand(player, packet.Operation, packet.Item,
                packet.Recipe, null, packet.Station, packet.Container, packet.Order, 0);
        }

        var resultPayload = new CookingLanRecipeCommandResultPacket(
            packet.CommandId,
            result.Outcome,
            result.Reason,
            result.StateVersion,
            result.IsDuplicate,
            result.Events);

        if (packet.CommandId > 0)
        {
            session.ExecutedCommands[packet.CommandId] = resultPayload;
        }

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

    private void SendSnapshotToPeer(NetPeer peer)
    {
        CookingRecipeSnapshot snapshot;
        lock (_simulationGate)
        {
            snapshot = LatestSnapshot;
        }

        var seq = Interlocked.Increment(ref _snapshotSequence);
        var packet = new CookingLanSnapshotPacket(seq, snapshot);
        var bytes = CookingLanCodec.Encode(CookingLanMessageKind.RecipeSnapshot, $"snap-{seq}", packet);

        try
        {
            peer.Send(bytes, DeliveryMethod.ReliableOrdered);
        }
        catch
        {
        }
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

        foreach (var peer in _peerToPlayer.Keys)
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
        _peerToPlayer.Clear();
        _sessions.Clear();
        return ValueTask.CompletedTask;
    }
}
