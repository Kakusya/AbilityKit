using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests.Harness;

/// <summary>
/// 纯本地内存 Simulation 拓扑环境。
/// 所有命令在本地同步提交并由权威 Simulation 实时处理，零传输开销。
/// </summary>
public sealed class DirectMemoryTopology : ICookingTestTopology
{
    private readonly CookingRecipeSimulation _simulation;
    private readonly CookingSessionDescriptor _descriptor;
    private readonly CookingLevelScope _levelScope;
    private readonly Dictionary<PlayerId, DirectMemoryActor> _actors = new();
    private int _commandSequence;
    private long _hostFrameSequence;

    public string Name => "DirectMemory";
    public CookingScope Scope => _descriptor.Scope;
    public CookingLevelScope LevelScope => _levelScope;

    public DirectMemoryTopology(CookingRecipeSimulation simulation, CookingSessionDescriptor descriptor, CookingLevelScope levelScope, IEnumerable<PlayerId> players)
    {
        _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        _levelScope = levelScope ?? throw new ArgumentNullException(nameof(levelScope));

        foreach (var player in players)
        {
            _actors[player] = new DirectMemoryActor(this, player);
        }
    }

    public ICookingActor GetActor(PlayerId playerId)
    {
        if (!_actors.TryGetValue(playerId, out var actor))
        {
            throw new KeyNotFoundException($"Player {playerId} is not registered in this DirectMemoryTopology.");
        }
        return actor;
    }

    public Task AdvanceTicksAsync(int tickCount, CancellationToken ct = default)
    {
        for (var i = 0; i < tickCount; i++)
        {
            var frame = Interlocked.Increment(ref _hostFrameSequence);
            _simulation.AdvanceFixedTick(_levelScope, frame);
        }
        return Task.CompletedTask;
    }

    public Task SyncAndDrainAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public void AssertStateHashConsensus()
    {
        var snapshot = _simulation.Snapshot();
        Assert.NotNull(snapshot);
        var hash = snapshot.Sha256();
        Assert.False(string.IsNullOrWhiteSpace(hash));
    }

    public CookingRecipeSnapshot GetAuthoritySnapshot() => _simulation.Snapshot();

    internal CookingRecipeCommandResult SubmitCommand(PlayerId player, CookingRecipeOperation operation, ItemId? item,
        RecipeId? recipe = null, ProcessId? process = null, StationSlotId? station = null, ItemId? container = null, OrderId? order = null, int ticks = 0)
    {
        var seq = Interlocked.Increment(ref _commandSequence);
        var commandId = new RecipeCommandId($"{player.Value}-cmd-{seq}");

        // 自动查询 Item 的期望版本，提供便利的 Actor 操作
        var expectedVersion = 0;
        if (item is not null)
        {
            var matched = _simulation.Snapshot().Items.FirstOrDefault(i => i.Id == item);
            if (matched is not null)
            {
                expectedVersion = matched.Version;
            }
        }

        var cmd = new CookingRecipeCommand(Scope, _descriptor.Epoch, player, commandId, operation,
            recipe, process, item, station, container, order, expectedVersion, ticks);

        return _simulation.Submit(cmd);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed class DirectMemoryActor : ICookingActor
    {
        private readonly DirectMemoryTopology _owner;
        public PlayerId PlayerId { get; }

        public DirectMemoryActor(DirectMemoryTopology owner, PlayerId playerId)
        {
            _owner = owner;
            PlayerId = playerId;
        }

        public Task<CookingRecipeCommandResult> PickupAsync(ItemId item, CancellationToken ct = default) =>
            Task.FromResult(_owner.SubmitCommand(PlayerId, CookingRecipeOperation.Pickup, item));

        public Task<CookingRecipeCommandResult> DropAsync(ItemId item, StationSlotId station, CancellationToken ct = default) =>
            Task.FromResult(_owner.SubmitCommand(PlayerId, CookingRecipeOperation.Drop, item, station: station));

        public Task<CookingRecipeCommandResult> PutInContainerAsync(ItemId item, ItemId container, CancellationToken ct = default) =>
            Task.FromResult(_owner.SubmitCommand(PlayerId, CookingRecipeOperation.PutIn, item, container: container));

        public Task<CookingRecipeCommandResult> StartProcessAsync(RecipeId recipe, ItemId item, StationSlotId? station = null, CancellationToken ct = default) =>
            Task.FromResult(_owner.SubmitCommand(PlayerId, CookingRecipeOperation.StartProcess, item, recipe: recipe, station: station));

        public Task<CookingRecipeCommandResult> PlateAsync(ItemId product, ItemId container, CancellationToken ct = default) =>
            Task.FromResult(_owner.SubmitCommand(PlayerId, CookingRecipeOperation.Pour, product, container: container));

        public Task<CookingRecipeCommandResult> SubmitOrderAsync(ItemId item, OrderId order, CancellationToken ct = default) =>
            Task.FromResult(_owner.SubmitCommand(PlayerId, CookingRecipeOperation.SubmitOrder, item, order: order));

        public CookingRecipeSnapshot GetCurrentSnapshot() => _owner.GetAuthoritySnapshot();
    }
}
