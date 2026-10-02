using System.Collections.Concurrent;
using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using AbilityKit.Game.Cooking.Session;
using AbilityKit.Network.Host.InProcess;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

[Trait("Gate", "CookingLevelRuntime")]
public sealed class CookingNetworkColdRecoveryEtTests
{
    private static readonly CookingScope Match = new(new("cold-network"), new("world"), new("match"));
    private static readonly CookingLevelScope First = new(Match, new(1), new("first"), 1);
    private static readonly LevelId Next = new("next");
    private static readonly PlayerId Chef = new("chef");
    private static readonly DefinitionId Raw = new("raw"), Cooked = new("cooked"), Plate = new("plate");
    private static readonly ItemId Food = new("food");
    private static readonly StationSlotId Stove = new("stove");
    private static readonly RecipeId Recipe = new("cook");
    private static readonly OrderTemplateId Template = new("cold-order");
    private const string Credential = "cold-test-credential", Stable = "same-wire-pickup";

    private sealed class Factory : ICookingFrontOfHouseGameplayFactory
    {
        private readonly CookingRecipeFixture _fixture;
        public CookingConfigurationSnapshot Config { get; }
        public int CreateCount { get; private set; }
        public CookingFrontOfHouseConfiguration FrontOfHouseConfiguration { get; } = new(new(1, 4, 2, 1, 1, 1, 3), new[] { Template });
        public Factory()
        {
            var capability = new HashSet<string> { "cook" };
            var items = new[] { new CookingItemDefinition(Raw, capability), new CookingItemDefinition(Cooked, capability),
                new CookingItemDefinition(Plate, capability, new(1, new HashSet<DefinitionId> { Cooked })) };
            var stations = new[] { new CookingApplianceDefinition(Stove, new HashSet<string> { "heat" }) };
            var recipes = new[] { new CookingRecipeDefinition(Recipe, new[] { Raw }, Cooked, new("process"), "heat", 3) };
            var templates = new[] { new CookingOrderTemplateDefinition(Template, Recipe, Plate) };
            var registry = new CookingConfigurationRegistry();
            Assert.True(registry.Submit(new(new[] { "heat" }, items, stations, recipes, OrderTemplates: templates)).Accepted);
            Config = registry.Current!;
            _fixture = new(Match, new Dictionary<PlayerId, CookingPlayerConfig> {
                [Chef] = new(Chef, capability, new HashSet<string> { "spawn", Stove.Value }) },
                items.ToDictionary(i => i.Id), stations.ToDictionary(s => s.Station), recipes.ToDictionary(r => r.Id),
                orderTemplates: templates.ToDictionary(t => t.Id));
        }
        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            CreateCount++;
            var kitchen = new CookingRecipeSimulation(_fixture); kitchen.AddWorldIngredient(Food, Raw, "spawn"); return kitchen;
        }
        public CookingLevelPreparation Preparation(LevelId level) => new(level, new("map"),
            new(new("layout"), new[] { Stove }, new[] { Plate }), Config.Identity);
        public CookingLevelEtHost Host() => new(new CookingLevelLifecycle(First, Config, this));
    }
    private sealed class Files : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "network-cold-" + Guid.NewGuid().ToString("N"));
        public CookingMajorCheckpointStore NewStore() => new(Root);
        public byte[] Bytes => File.ReadAllBytes(Path.Combine(Root, "major.checkpoint.json"));
        public void Dispose()
        {
            var resolved = Path.GetFullPath(Root);
            var temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(temporary, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(resolved).StartsWith("network-cold-", StringComparison.Ordinal))
                throw new InvalidOperationException("Cold test cleanup escaped its owned temporary directory.");
            if (Directory.Exists(resolved)) Directory.Delete(resolved, recursive: true);
        }
    }
    private sealed class Peer : IDisposable
    {
        private readonly ConnectionManager _connection;
        private readonly ConcurrentQueue<CookingNetworkWireEnvelope> _packets = new();
        public Peer(CookingNetworkSessionHost session)
        {
            _connection = new ConnectionManager(session.CreateLocalClientTransport, new ConnectionOptions { EnableReconnect = false });
            _connection.ServerPushReceived += (_, bytes) => {
                Assert.True(CookingNetworkWireCodec.TryDecode(bytes.AsSpan(), new(), out var envelope)); _packets.Enqueue(envelope!);
            };
            _connection.Open("inprocess", 1);
        }
        public void Send<T>(CookingNetworkMessageKind kind, string correlation, T payload) => _connection.Send(CookingNetworkWireCodec.OpCode,
            new ArraySegment<byte>(CookingNetworkWireCodec.Encode(kind, correlation, payload)), (ushort)NetworkPacketFlags.ServerPush);
        public CookingNetworkWireEnvelope Packet(string correlation) => Assert.Single(_packets.ToArray(), p => p.CorrelationId == correlation);
        public bool Has(string correlation) => _packets.Any(p => p.CorrelationId == correlation);
        public CookingNetworkWireEnvelope[] Packets => _packets.ToArray();
        public CookingNetworkJoined Join(CookingNetworkSessionHost session)
        {
            Send(CookingNetworkMessageKind.Join, "join", new CookingNetworkJoin(Chef, Credential, null, null));
            Pump(session, () => Has("join"));
            var joined = CookingNetworkWireCodec.Read<CookingNetworkJoined>(Packet("join"))!;
            var baseline = CookingNetworkWireCodec.Read<CookingNetworkBaseline>(Assert.Single(Packets, p => p.Kind == CookingNetworkMessageKind.Baseline))!;
            Assert.Equal(joined.ServerSessionInstance, baseline.Identity.ServerSessionInstance);
            Send(CookingNetworkMessageKind.BaselineAck, "ack", baseline.Identity); Pump(session, () => Has("ack"));
            Assert.Equal(CookingNetworkMessageKind.Ready, Packet("ack").Kind);
            return joined;
        }
        public void Dispose() => _connection.Dispose();
    }
    private static void Pump(CookingNetworkSessionHost session, Func<bool> completed)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!completed() && DateTime.UtcNow < deadline) { session.ProcessOwnerFrame(); Thread.Sleep(1); }
        Assert.True(completed(), "Owner did not produce the expected bounded protocol response.");
    }
    private static CookingNetworkSessionHost Session(CookingLevelEtHost host, Factory factory) => new(
        new CookingNetworkAuthorityAdapter(host, request => {
            if (request != "start-recovered") return new(false, "Unauthorized", host.Lifecycle.State.ToString(),
                host.Lifecycle.Version, Array.Empty<CookingLevelPendingDisposition>());
            var prepared = host.Prepare(factory.Preparation(host.Binding.LevelScope.Level));
            return prepared.Accepted ? host.Start() : prepared;
        }), new InProcessChannelListener(), new Dictionary<PlayerId, string> { [Chef] = Credential });
    private static CookingRecipeCommand Pickup() => new(Match, 0, Chef, new("untrusted-placeholder"),
        CookingRecipeOperation.Pickup, Item: Food, ExpectedItemVersion: 1);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Actual_success_store_dispose_load_new_session_rejects_old_identity_and_packet_then_executes_same_wire_id_fresh(int combination)
    {
        using var files = new Files();
        string oldInstance, oldToken, createdCanonical, captureHash;
        CookingNetworkBaselineIdentity oldIssued;
        CookingNetworkWireCommand oldPacket;
        RecipeCommandId oldDomainId;
        CookingMajorBaselinePayload payload;
        byte[] bytes;
        var oldFactory = new Factory();
        using (var oldHost = oldFactory.Host())
        {
            Assert.True(oldHost.Prepare(oldFactory.Preparation(First.Level)).Accepted); Assert.True(oldHost.Start().Accepted);
            for (var i = 0; i < 100 && oldHost.Lifecycle.State == CookingLevelState.Running; i++) {
                oldHost.Tick(); _ = oldHost.TryFinishService();
            }
            Assert.Equal(CookingLevelState.Ending, oldHost.Lifecycle.State);
            Assert.Equal(CookingLevelOutcome.Success, oldHost.Lifecycle.Outcome);
            Assert.True(oldHost.FrontOfHouseSnapshot!.Closing); Assert.NotEmpty(oldHost.FrontOfHouseSnapshot.UnsatisfiedOrders);
            Assert.True(oldHost.CompleteEnd().Accepted);
            var progress = new CookingMajorProgress(); progress.Lock();
            var writtenStore = files.NewStore();
            Assert.True(oldHost.CreateSuccessor(Next, 2, oldFactory.Preparation(Next), progress, writtenStore).Accepted);
            payload = writtenStore.ReadBaseline(Match).Payload!; bytes = files.Bytes;
            Assert.Equal(CookingLevelState.Created, oldHost.Lifecycle.State);
            Assert.Empty(payload.Kitchen.Deduplication); // Major handoff is not network mapping persistence.
            createdCanonical = oldHost.Observe().CanonicalText();
            captureHash = CookingNetworkWireCodec.Hash(new CookingNetworkAuthorityAdapter(oldHost).CaptureFullState().State!);
            using var oldSession = Session(oldHost, oldFactory); oldSession.Start(); using var oldPeer = new Peer(oldSession);
            var binding = oldPeer.Join(oldSession); oldInstance = binding.ServerSessionInstance; oldToken = binding.RebindToken;
            oldIssued = CookingNetworkWireCodec.Read<CookingNetworkBaseline>(Assert.Single(oldPeer.Packets,
                p => p.Kind == CookingNetworkMessageKind.Baseline && p.CorrelationId == "baseline-1"))!.Identity;
            Assert.True(oldSession.ApplyControl(new(CookingNetworkControlKind.ExecuteAuthorizedTransition, "start-recovered")).Accepted);
            oldPacket = new(oldInstance, binding.ConnectionGeneration, 1, Stable, oldHost.Binding.LevelScope, Pickup());
            oldPeer.Send(CookingNetworkMessageKind.Command, "old-action", oldPacket); Pump(oldSession, () => oldPeer.Has("old-action"));
            var result = CookingNetworkWireCodec.Read<CookingNetworkWireResult>(oldPeer.Packet("old-action"))!;
            Assert.Equal(CookingRecipeOutcome.Accepted, result.Result!.Outcome); oldDomainId = result.DomainCommandId!.Value;
            Assert.Equal(Chef.Value, oldHost.Observe().Items.Single(i => i.Id == Food).Location.OwnerId);
            Assert.Equal(bytes, files.Bytes); // Post-baseline work was not saved as a new success.
        } // Old peer, Session and Host genuinely disposed before constructing any replacement.
        var newStore = files.NewStore(); var read = newStore.ReadBaseline(Match);
        Assert.True(read.Accepted); Assert.Equal(payload.CanonicalText(), read.Payload!.CanonicalText());
        var trusted = new Factory(); var loaded = CookingLevelEtHost.LoadMajorBaseline(newStore, Match, trusted.Config, trusted);
        Assert.True(loaded.Accepted, loaded.ToString()); Assert.Equal(1, trusted.CreateCount); Assert.True(loaded.Progress!.Locked);
        using var recovered = loaded.Host!;
        Assert.Equal(CookingLevelState.Created, recovered.Lifecycle.State);
        Assert.Equal(createdCanonical, recovered.Observe().CanonicalText()); Assert.Equal(payload.HostFrameSequence, recovered.HostFrameSequence);
        Assert.Equal(LocationKind.WorldPosition, recovered.Observe().Items.Single(i => i.Id == Food).Location.Kind);
        Assert.Equal(captureHash, CookingNetworkWireCodec.Hash(new CookingNetworkAuthorityAdapter(recovered).CaptureFullState().State!));
        using var session = Session(recovered, trusted); session.Start(); using var peer = new Peer(session);
        Assert.NotEqual(oldInstance, session.ServerSessionInstance);
        var suppliedInstance = combination switch { 0 or 2 => oldInstance, 1 => session.ServerSessionInstance, _ => null };
        var suppliedToken = combination == 2 ? null : oldToken;
        var before = recovered.Observe().CanonicalText(); var beforeView = session.LatestSessionProjection;
        peer.Send(CookingNetworkMessageKind.Join, "old-join", new CookingNetworkJoin(Chef, Credential, suppliedInstance, suppliedToken));
        Pump(session, () => peer.Has("old-join"));
        Assert.Equal("Unauthorized", CookingNetworkWireCodec.Read<CookingNetworkWireResult>(peer.Packet("old-join"))!.Reason);
        Assert.DoesNotContain(peer.Packets, p => p.Kind is CookingNetworkMessageKind.Joined or CookingNetworkMessageKind.Baseline or CookingNetworkMessageKind.Ready);
        Assert.Equal(beforeView.Participants.ToArray(), session.LatestSessionProjection.Participants.ToArray());
        Assert.Equal(before, recovered.Observe().CanonicalText()); Assert.Equal(bytes, files.Bytes);
        var newBinding = peer.Join(session); Assert.Equal(1, newBinding.ConnectionGeneration); Assert.NotEqual(oldToken, newBinding.RebindToken);
        Assert.Equal(before, recovered.Observe().CanonicalText());
        peer.Send(CookingNetworkMessageKind.BaselineAck, "old-ack", oldIssued); Pump(session, () => peer.Has("old-ack"));
        Assert.Equal("BaselineRequired", CookingNetworkWireCodec.Read<CookingNetworkWireResult>(peer.Packet("old-ack"))!.Reason);
        Assert.Equal(before, recovered.Observe().CanonicalText());
        peer.Send(CookingNetworkMessageKind.Command, "delayed-old", oldPacket); Pump(session, () => peer.Has("delayed-old"));
        Assert.Equal("ServerInstanceMismatch", CookingNetworkWireCodec.Read<CookingNetworkWireResult>(peer.Packet("delayed-old"))!.Reason);
        Assert.Equal(before, recovered.Observe().CanonicalText()); Assert.Equal(bytes, files.Bytes);
        Assert.True(session.ApplyControl(new(CookingNetworkControlKind.ExecuteAuthorizedTransition, "start-recovered")).Accepted);
        var newPacket = new CookingNetworkWireCommand(newBinding.ServerSessionInstance, newBinding.ConnectionGeneration, 1,
            Stable, recovered.Binding.LevelScope, Pickup());
        peer.Send(CookingNetworkMessageKind.Command, "new-action", newPacket); Pump(session, () => peer.Has("new-action"));
        var fresh = CookingNetworkWireCodec.Read<CookingNetworkWireResult>(peer.Packet("new-action"))!;
        Assert.Equal(CookingRecipeOutcome.Accepted, fresh.Result!.Outcome); Assert.False(fresh.Result.IsDuplicate);
        Assert.NotEqual(oldDomainId, fresh.DomainCommandId!.Value);
        Assert.Equal(CookingNetworkWireCodec.DomainId(session.ServerSessionInstance, recovered.Binding.LevelScope, Chef, Stable), fresh.DomainCommandId.Value);
        Assert.Equal(Chef.Value, recovered.Observe().Items.Single(i => i.Id == Food).Location.OwnerId);
        Assert.Equal(bytes, files.Bytes);
    }
}
