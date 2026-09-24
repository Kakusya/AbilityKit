using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests.Harness;

/// <summary>
/// 基于回环端口 UDP 真实网络传输的测试拓扑环境 (Host + Client)。
/// Host 监听回环端口并持有权威 CookingRecipeSimulation，Client 通过通用 LiteNetTransport 连接。
/// 验证跨网络命令封包传输、权威裁决与快照广播一致性。
/// </summary>
public sealed class LoopbackUdpTopology : ICookingTestTopology
{
    private readonly CookingLanHost _host;
    private readonly CookingLanClient _client;
    private readonly CookingSessionDescriptor _descriptor;
    private readonly CookingLevelScope _levelScope;
    private readonly PlayerId _hostPlayer;
    private readonly PlayerId _clientPlayer;

    public string Name => "LoopbackUdp";
    public CookingScope Scope => _descriptor.Scope;
    public CookingLevelScope LevelScope => _levelScope;

    private LoopbackUdpTopology(
        CookingLanHost host,
        CookingLanClient client,
        CookingSessionDescriptor descriptor,
        CookingLevelScope levelScope,
        PlayerId hostPlayer,
        PlayerId clientPlayer)
    {
        _host = host;
        _client = client;
        _descriptor = descriptor;
        _levelScope = levelScope;
        _hostPlayer = hostPlayer;
        _clientPlayer = clientPlayer;
    }

    public static async Task<LoopbackUdpTopology> CreateAsync(
        CookingRecipeSimulation simulation,
        CookingSessionDescriptor descriptor,
        CookingLevelScope levelScope,
        PlayerId hostPlayer,
        PlayerId clientPlayer,
        CancellationToken ct = default)
    {
        var host = new CookingLanHost(simulation, descriptor, levelScope, hostPlayer, clientPlayer);
        await host.StartAsync();

        var client = new CookingLanClient(clientPlayer);
        await client.ConnectAndHandshakeAsync("127.0.0.1", host.Port, ct);

        return new LoopbackUdpTopology(host, client, descriptor, levelScope, hostPlayer, clientPlayer);
    }

    public ICookingActor GetActor(PlayerId playerId)
    {
        if (playerId == _hostPlayer)
        {
            return new HostActor(this, playerId);
        }
        if (playerId == _clientPlayer)
        {
            return new ClientActor(this, playerId);
        }
        throw new KeyNotFoundException($"Player {playerId} is neither host ({_hostPlayer}) nor client ({_clientPlayer}).");
    }

    public async Task AdvanceTicksAsync(int tickCount, CancellationToken ct = default)
    {
        _host.AdvanceFixedTick(tickCount);
        await SyncAndDrainAsync(TimeSpan.FromSeconds(2), ct);
    }

    public async Task SyncAndDrainAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        _host.BroadcastSnapshot();

        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout)
        {
            if (_client.LatestProjection != null &&
                _client.LatestProjection.Version >= _host.LatestSnapshot.Version)
            {
                return;
            }
            await Task.Delay(10, ct);
        }
    }

    public void AssertStateHashConsensus()
    {
        var hostSnapshot = _host.LatestSnapshot;
        var clientSnapshot = _client.LatestProjection;

        Assert.NotNull(hostSnapshot);
        Assert.NotNull(clientSnapshot);

        var hostHash = hostSnapshot.Sha256();
        var clientHash = clientSnapshot.Sha256();

        Assert.False(string.IsNullOrWhiteSpace(hostHash), "Host state hash must not be empty.");
        Assert.False(string.IsNullOrWhiteSpace(clientHash), "Client state hash must not be empty.");
        Assert.Equal(hostHash, clientHash);
        Assert.Equal(hostSnapshot.Version, clientSnapshot.Version);
    }

    public CookingRecipeSnapshot GetAuthoritySnapshot() => _host.LatestSnapshot;
    public CookingRecipeSnapshot? GetClientSnapshot() => _client.LatestProjection;

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
        await _host.DisposeAsync();
    }

    private sealed class HostActor : ICookingActor
    {
        private readonly LoopbackUdpTopology _owner;
        public PlayerId PlayerId { get; }

        public HostActor(LoopbackUdpTopology owner, PlayerId playerId)
        {
            _owner = owner;
            PlayerId = playerId;
        }

        public async Task<CookingRecipeCommandResult> PickupAsync(ItemId item, CancellationToken ct = default)
        {
            var res = _owner._host.ExecuteHostLocalCommand(PlayerId, CookingRecipeOperation.Pickup, item);
            await _owner.SyncAndDrainAsync(TimeSpan.FromSeconds(2), ct);
            return res;
        }

        public async Task<CookingRecipeCommandResult> DropAsync(ItemId item, StationSlotId station, CancellationToken ct = default)
        {
            var res = _owner._host.ExecuteHostLocalCommand(PlayerId, CookingRecipeOperation.Drop, item, station: station);
            await _owner.SyncAndDrainAsync(TimeSpan.FromSeconds(2), ct);
            return res;
        }

        public async Task<CookingRecipeCommandResult> PutInContainerAsync(ItemId item, ItemId container, CancellationToken ct = default)
        {
            var res = _owner._host.ExecuteHostLocalCommand(PlayerId, CookingRecipeOperation.PutIn, item, container: container);
            await _owner.SyncAndDrainAsync(TimeSpan.FromSeconds(2), ct);
            return res;
        }

        public async Task<CookingRecipeCommandResult> StartProcessAsync(RecipeId recipe, ItemId item, StationSlotId? station = null, CancellationToken ct = default)
        {
            var res = _owner._host.ExecuteHostLocalCommand(PlayerId, CookingRecipeOperation.StartProcess, item, recipe: recipe, station: station);
            await _owner.SyncAndDrainAsync(TimeSpan.FromSeconds(2), ct);
            return res;
        }

        public async Task<CookingRecipeCommandResult> PlateAsync(ItemId product, ItemId container, CancellationToken ct = default)
        {
            var res = _owner._host.ExecuteHostLocalCommand(PlayerId, CookingRecipeOperation.Pour, product, container: container);
            await _owner.SyncAndDrainAsync(TimeSpan.FromSeconds(2), ct);
            return res;
        }

        public async Task<CookingRecipeCommandResult> SubmitOrderAsync(ItemId item, OrderId order, CancellationToken ct = default)
        {
            var res = _owner._host.ExecuteHostLocalCommand(PlayerId, CookingRecipeOperation.SubmitOrder, item, order: order);
            await _owner.SyncAndDrainAsync(TimeSpan.FromSeconds(2), ct);
            return res;
        }

        public CookingRecipeSnapshot GetCurrentSnapshot() => _owner.GetAuthoritySnapshot();
    }

    private sealed class ClientActor : ICookingActor
    {
        private readonly LoopbackUdpTopology _owner;
        public PlayerId PlayerId { get; }

        public ClientActor(LoopbackUdpTopology owner, PlayerId playerId)
        {
            _owner = owner;
            PlayerId = playerId;
        }

        public async Task<CookingRecipeCommandResult> PickupAsync(ItemId item, CancellationToken ct = default)
        {
            var res = await _owner._client.SendCommandAsync(CookingRecipeOperation.Pickup, item, ct: ct);
            await _owner.SyncAndDrainAsync(TimeSpan.FromSeconds(2), ct);
            return res;
        }

        public async Task<CookingRecipeCommandResult> DropAsync(ItemId item, StationSlotId station, CancellationToken ct = default)
        {
            var res = await _owner._client.SendCommandAsync(CookingRecipeOperation.Drop, item, station: station, ct: ct);
            await _owner.SyncAndDrainAsync(TimeSpan.FromSeconds(2), ct);
            return res;
        }

        public async Task<CookingRecipeCommandResult> PutInContainerAsync(ItemId item, ItemId container, CancellationToken ct = default)
        {
            var res = await _owner._client.SendCommandAsync(CookingRecipeOperation.PutIn, item, container: container, ct: ct);
            await _owner.SyncAndDrainAsync(TimeSpan.FromSeconds(2), ct);
            return res;
        }

        public async Task<CookingRecipeCommandResult> StartProcessAsync(RecipeId recipe, ItemId item, StationSlotId? station = null, CancellationToken ct = default)
        {
            var res = await _owner._client.SendCommandAsync(CookingRecipeOperation.StartProcess, item, station: station, recipe: recipe, ct: ct);
            await _owner.SyncAndDrainAsync(TimeSpan.FromSeconds(2), ct);
            return res;
        }

        public async Task<CookingRecipeCommandResult> PlateAsync(ItemId product, ItemId container, CancellationToken ct = default)
        {
            var res = await _owner._client.SendCommandAsync(CookingRecipeOperation.Pour, product, container: container, ct: ct);
            await _owner.SyncAndDrainAsync(TimeSpan.FromSeconds(2), ct);
            return res;
        }

        public async Task<CookingRecipeCommandResult> SubmitOrderAsync(ItemId item, OrderId order, CancellationToken ct = default)
        {
            var res = await _owner._client.SendCommandAsync(CookingRecipeOperation.SubmitOrder, item, order: order, ct: ct);
            await _owner.SyncAndDrainAsync(TimeSpan.FromSeconds(2), ct);
            return res;
        }

        public CookingRecipeSnapshot GetCurrentSnapshot() => _owner.GetClientSnapshot() ?? _owner.GetAuthoritySnapshot();
    }
}
