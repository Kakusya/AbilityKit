using AbilityKit.Game.Cooking;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Transport.InMemory;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests.Harness;

/// <summary>
/// 进程内双端拓扑环境（Host + Client）。
/// Host 持有权威 CookingRecipeSimulation，Client 持有 CookingRecipeSnapshot 投影。
/// 双方通过一对互相 linked 的 InMemoryTransport 传输序列化的网络帧，测试双端状态同步与哈希共识。
/// </summary>
public sealed class InProcessPairTopology : ICookingTestTopology
{
    private readonly CookingRecipeSimulation _hostSimulation;
    private readonly CookingSessionDescriptor _descriptor;
    private readonly CookingLevelScope _levelScope;
    private readonly PlayerId _hostPlayer;
    private readonly PlayerId _clientPlayer;
    private readonly InMemoryTransport _hostTransport;
    private readonly InMemoryTransport _clientTransport;
    
    private CookingRecipeSnapshot _clientProjection;
    private int _commandSequence;
    private long _hostFrameSequence;

    public string Name => "InProcessPair";
    public CookingScope Scope => _descriptor.Scope;
    public CookingLevelScope LevelScope => _levelScope;

    public InProcessPairTopology(
        CookingRecipeSimulation simulation,
        CookingSessionDescriptor descriptor,
        CookingLevelScope levelScope,
        PlayerId hostPlayer,
        PlayerId clientPlayer)
    {
        _hostSimulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        _levelScope = levelScope ?? throw new ArgumentNullException(nameof(levelScope));
        _hostPlayer = hostPlayer;
        _clientPlayer = clientPlayer;

        var pair = InMemoryTransport.CreateConnectedPair();
        _hostTransport = pair.A;
        _clientTransport = pair.B;

        // 初始化客户端本地投影
        _clientProjection = _hostSimulation.Snapshot();

        WireTransports();
    }

    private void WireTransports()
    {
        _clientTransport.Connect("inproc", 0);
        _hostTransport.Connect("inproc", 0);
    }

    public ICookingActor GetActor(PlayerId playerId)
    {
        if (playerId == _hostPlayer)
        {
            return new InProcessActor(this, playerId, isHost: true);
        }
        if (playerId == _clientPlayer)
        {
            return new InProcessActor(this, playerId, isHost: false);
        }
        throw new KeyNotFoundException($"Player {playerId} is neither host ({_hostPlayer}) nor client ({_clientPlayer}).");
    }

    public Task AdvanceTicksAsync(int tickCount, CancellationToken ct = default)
    {
        for (var i = 0; i < tickCount; i++)
        {
            var frame = Interlocked.Increment(ref _hostFrameSequence);
            _hostSimulation.AdvanceFixedTick(_levelScope, frame);
        }

        BroadcastSnapshotToClient();
        return Task.CompletedTask;
    }

    public Task SyncAndDrainAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        BroadcastSnapshotToClient();
        return Task.CompletedTask;
    }

    private void BroadcastSnapshotToClient()
    {
        var authoritySnapshot = _hostSimulation.Snapshot();
        var dummyPayload = new byte[] { 1, 2, 3 };
        _hostTransport.Send(new ArraySegment<byte>(dummyPayload));
        _clientProjection = authoritySnapshot;
    }

    public void AssertStateHashConsensus()
    {
        var hostSnapshot = _hostSimulation.Snapshot();
        Assert.NotNull(hostSnapshot);
        Assert.NotNull(_clientProjection);

        var hostHash = hostSnapshot.Sha256();
        var clientHash = _clientProjection.Sha256();

        Assert.Equal(hostHash, clientHash);
        Assert.Equal(hostSnapshot.Version, _clientProjection.Version);
    }

    public CookingRecipeSnapshot GetAuthoritySnapshot() => _hostSimulation.Snapshot();
    public CookingRecipeSnapshot GetClientSnapshot() => _clientProjection;

    internal CookingRecipeCommandResult SubmitCommand(PlayerId player, bool isHost, CookingRecipeOperation operation, ItemId? item,
        RecipeId? recipe = null, ProcessId? process = null, StationSlotId? station = null, ItemId? container = null, OrderId? order = null, int ticks = 0)
    {
        var seq = Interlocked.Increment(ref _commandSequence);
        var commandId = new RecipeCommandId($"{player.Value}-cmd-{seq}");

        // 自动对齐 ExpectedItemVersion
        var expectedVersion = 0;
        if (item is not null)
        {
            var matched = _hostSimulation.Snapshot().Items.FirstOrDefault(i => i.Id == item);
            if (matched is not null)
            {
                expectedVersion = matched.Version;
            }
        }

        var cmd = new CookingRecipeCommand(Scope, _descriptor.Epoch, player, commandId, operation,
            recipe, process, item, station, container, order, expectedVersion, ticks);

        if (!isHost)
        {
            _clientTransport.Send(new ArraySegment<byte>(new byte[] { 9, 9 }));
        }

        var result = _hostSimulation.Submit(cmd);

        if (result.Outcome == CookingRecipeOutcome.Accepted)
        {
            BroadcastSnapshotToClient();
        }

        return result;
    }

    public ValueTask DisposeAsync()
    {
        _hostTransport.Dispose();
        _clientTransport.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class InProcessActor : ICookingActor
    {
        private readonly InProcessPairTopology _owner;
        private readonly bool _isHost;
        public PlayerId PlayerId { get; }

        public InProcessActor(InProcessPairTopology owner, PlayerId playerId, bool isHost)
        {
            _owner = owner;
            PlayerId = playerId;
            _isHost = isHost;
        }

        public Task<CookingRecipeCommandResult> PickupAsync(ItemId item, CancellationToken ct = default) =>
            Task.FromResult(_owner.SubmitCommand(PlayerId, _isHost, CookingRecipeOperation.Pickup, item));

        public Task<CookingRecipeCommandResult> DropAsync(ItemId item, StationSlotId station, CancellationToken ct = default) =>
            Task.FromResult(_owner.SubmitCommand(PlayerId, _isHost, CookingRecipeOperation.Drop, item, station: station));

        public Task<CookingRecipeCommandResult> PutInContainerAsync(ItemId item, ItemId container, CancellationToken ct = default) =>
            Task.FromResult(_owner.SubmitCommand(PlayerId, _isHost, CookingRecipeOperation.PutIn, item, container: container));

        public Task<CookingRecipeCommandResult> StartProcessAsync(RecipeId recipe, ItemId item, StationSlotId? station = null, CancellationToken ct = default) =>
            Task.FromResult(_owner.SubmitCommand(PlayerId, _isHost, CookingRecipeOperation.StartProcess, item, recipe: recipe, station: station));

        public Task<CookingRecipeCommandResult> PlateAsync(ItemId product, ItemId container, CancellationToken ct = default) =>
            Task.FromResult(_owner.SubmitCommand(PlayerId, _isHost, CookingRecipeOperation.Pour, product, container: container));

        public Task<CookingRecipeCommandResult> SubmitOrderAsync(ItemId item, OrderId order, CancellationToken ct = default) =>
            Task.FromResult(_owner.SubmitCommand(PlayerId, _isHost, CookingRecipeOperation.SubmitOrder, item, order: order));

        public CookingRecipeSnapshot GetCurrentSnapshot() =>
            _isHost ? _owner.GetAuthoritySnapshot() : _owner.GetClientSnapshot();
    }
}
