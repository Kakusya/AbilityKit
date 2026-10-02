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
public sealed class CookingNetworkLiveRecoveryEtTests
{
    private static readonly PlayerId A = new("a"), B = new("b");
    private static readonly CookingScope Match = new(new("live-recovery"), new("world"), new("match"));
    private static readonly CookingLevelScope First = new(Match, new(1), new("first"), 1);
    private static readonly StationSlotId Board = new("board"), Counter = new("counter");
    private static readonly ItemId Input = new("input"), Cup = new("cup");
    private static readonly DefinitionId Raw = new("raw"), Drink = new("drink"), CupType = new("cup-type");
    private static readonly RecipeId Recipe = new("drink-recipe");
    private static readonly OrderTemplateId Template = new("template");
    private static readonly OrderId Order = new("order");

    private sealed class Factory : ICookingLevelGameplayFactory
    {
        public CookingConfigurationSnapshot Config { get; }
        private readonly CookingRecipeFixture _fixture;
        public Factory(bool manual = false)
        {
            var caps = new HashSet<string> { "cook" };
            var items = new[] { new CookingItemDefinition(Raw, caps), new CookingItemDefinition(Drink, caps),
                new CookingItemDefinition(CupType, caps, new(1, new HashSet<DefinitionId> { Drink }, DisposableOnSubmission: true)) };
            var appliances = new[] { new CookingApplianceDefinition(Board, new HashSet<string> { "mix" }),
                new CookingApplianceDefinition(Counter, new HashSet<string>()) };
            var recipes = new[] { new CookingRecipeDefinition(Recipe, new[] { Raw }, Drink, new("mix"), "mix", 8,
                Execution: manual ? CookingRecipeExecutionKind.Manual : CookingRecipeExecutionKind.Automatic) };
            var orders = new[] { new CookingOrderTemplateDefinition(Template, Recipe, CupType, RequiresBinding: true) };
            var registry = new CookingConfigurationRegistry();
            Assert.True(registry.Submit(new(new[] { "mix" }, items, appliances, recipes, OrderTemplates: orders)).Accepted);
            Config = registry.Current!;
            _fixture = new(Match, new[] { A, B }.ToDictionary(p => p,
                p => new CookingPlayerConfig(p, caps, new HashSet<string> { Board.Value, Counter.Value })),
                items.ToDictionary(i => i.Id), appliances.ToDictionary(i => i.Station), recipes.ToDictionary(i => i.Id),
                orderTemplates: orders.ToDictionary(i => i.Id));
        }
        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            var kitchen = new CookingRecipeSimulation(_fixture);
            kitchen.AddItem(Input, Raw, ItemLocation.Station(Board)); kitchen.AddItem(Cup, CupType, ItemLocation.Station(Counter));
            Assert.True(kitchen.OpenOrder(Order, Template).Accepted); return kitchen;
        }
        public CookingLevelPreparation Preparation(LevelId level) => new(level, new("map"),
            new(new("layout"), new[] { Board, Counter }, new[] { CupType }), Config.Identity);
        public CookingLevelEtHost Host(bool service = false)
        {
            var host = new CookingLevelEtHost(new CookingLevelLifecycle(First, Config, this));
            if (service) host.UseFrontOfHouse(new(new(1, 30, 5, 1, 1, 1, 3)), new CookingFrontOfHouseMenu(new[] { Template }));
            Assert.True(host.Prepare(Preparation(First.Level)).Accepted); Assert.True(host.Start().Accepted); return host;
        }
    }
    private sealed class Peer : IDisposable
    {
        private readonly ConnectionManager _connection;
        private readonly ConcurrentQueue<CookingNetworkWireEnvelope> _packets = new();
        private CookingNetworkBaselineIdentity? _acked;
        public PlayerId Player { get; }
        public CookingNetworkJoined Binding { get; private set; } = null!;
        public long Sequence { get; private set; }
        public string? DropReplyCorrelation { get; set; }
        public int DroppedReplies { get; private set; }
        public Peer(CookingNetworkSessionHost session, PlayerId player)
        {
            Player = player;
            _connection = new(session.CreateLocalClientTransport, new ConnectionOptions { EnableReconnect = false });
            _connection.ServerPushReceived += (_, bytes) => {
                Assert.True(CookingNetworkWireCodec.TryDecode(bytes.AsSpan(), new(), out var envelope));
                if (envelope!.Kind == CookingNetworkMessageKind.CommandResult && envelope.CorrelationId == DropReplyCorrelation) {
                    DroppedReplies++; return;
                }
                _packets.Enqueue(envelope);
            };
            _connection.Open("inprocess", 1);
        }
        public CookingNetworkWireEnvelope[] Packets => _packets.ToArray();
        public bool Has(string correlation) => _packets.Any(p => p.CorrelationId == correlation);
        public CookingNetworkWireEnvelope Packet(string correlation) => Assert.Single(Packets, p => p.CorrelationId == correlation);
        public CookingNetworkBaseline Latest => CookingNetworkWireCodec.Read<CookingNetworkBaseline>(Packets.Last(p => p.Kind == CookingNetworkMessageKind.Baseline))!;
        public void Send<T>(CookingNetworkMessageKind kind, string correlation, T payload) => _connection.Send(CookingNetworkWireCodec.OpCode,
            new ArraySegment<byte>(CookingNetworkWireCodec.Encode(kind, correlation, payload)), (ushort)NetworkPacketFlags.ServerPush);
        public void Join(CookingNetworkSessionHost session, CookingNetworkJoined? previous = null)
        {
            Send(CookingNetworkMessageKind.Join, "join", new CookingNetworkJoin(Player, "credential-" + Player.Value,
                previous?.ServerSessionInstance, previous?.RebindToken)); Pump(session, () => Has("join"));
            Binding = CookingNetworkWireCodec.Read<CookingNetworkJoined>(Packet("join"))!;
            var issued = Latest.Identity; Send(CookingNetworkMessageKind.BaselineAck, "join-ack", issued);
            Pump(session, () => Has("join-ack")); Assert.Equal(CookingNetworkMessageKind.Ready, Packet("join-ack").Kind); _acked = issued;
        }
        public void AckLatest()
        {
            var issued = Latest.Identity;
            if (_acked == issued) return;
            Send(CookingNetworkMessageKind.BaselineAck, "display-ack-" + issued.SnapshotSequence, issued); _acked = issued;
        }
        public CookingNetworkWireCommand Queue(CookingNetworkSessionHost session, CookingRecipeCommand command, string stable, string correlation,
            CookingLevelScope? scope = null, long? sequence = null, bool acknowledge = true)
        {
            if (acknowledge) AckLatest(); var seq = sequence ?? ++Sequence;
            var wire = new CookingNetworkWireCommand(Binding.ServerSessionInstance, Binding.ConnectionGeneration, seq, stable,
                scope ?? session.LatestCapture!.Observation.Scope, command);
            Send(CookingNetworkMessageKind.Command, correlation, wire); return wire;
        }
        public CookingNetworkWireResult Execute(CookingNetworkSessionHost session, CookingRecipeCommand command, string stable)
        {
            Queue(session, command, stable, stable); Pump(session, () => Has(stable));
            var result = CookingNetworkWireCodec.Read<CookingNetworkWireResult>(Packet(stable))!;
            Assert.Equal(CookingRecipeOutcome.Accepted, result.Result!.Outcome); return result;
        }
        public void Dispose() => _connection.Dispose();
    }
    private static void Pump(CookingNetworkSessionHost session, Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline) { session.ProcessOwnerFrame(); Thread.Sleep(1); }
        Assert.True(condition(), "Bounded owner response was not produced.");
    }
    private static CookingNetworkSessionHost Session(CookingLevelEtHost host, Factory factory) => new(new CookingNetworkAuthorityAdapter(host, request => {
        if (request == "successor") {
            var finish = host.TryFinishService(); if (!finish.Accepted) return finish;
            var ended = host.CompleteEnd(); if (!ended.Accepted) return ended;
            var next = host.CreateSuccessor(new("next"), 2);
            return new(next.Accepted, next.Reason.ToString(), host.Lifecycle.State.ToString(), host.Lifecycle.Version, Array.Empty<CookingLevelPendingDisposition>());
        }
        if (request == "start-next") {
            var prepared = host.Prepare(factory.Preparation(host.Binding.LevelScope.Level)); return prepared.Accepted ? host.Start() : prepared;
        }
        return new(false, "Unauthorized", host.Lifecycle.State.ToString(), host.Lifecycle.Version, Array.Empty<CookingLevelPendingDisposition>());
    }), new InProcessChannelListener(), new Dictionary<PlayerId, string> { [A] = "credential-a", [B] = "credential-b" });
    private static CookingRecipeCommand Command(CookingLevelEtHost host, PlayerId player, CookingRecipeOperation operation,
        ItemId? item = null, StationSlotId? station = null, ItemId? container = null, ProcessId? process = null, OrderId? order = null) =>
        new(Match, 0, player, new("ignored-client-domain-id"), operation, Item: item, Station: station, Container: container,
            Process: process, Order: order, ExpectedItemVersion: item is null ? 0 : host.Observe().Recipe!.Items.Single(i => i.Id == item).Version);
    private static ItemId FinishAndPlate(CookingLevelEtHost host, CookingNetworkSessionHost session, Peer peer)
    {
        for (var i = 0; i < 20 && host.Observe().Recipe!.Processes.Count != 0; i++) session.ProcessOwnerFrame();
        Assert.Empty(host.Observe().Recipe!.Processes);
        var product = Assert.Single(host.Observe().Recipe!.Items, i => i.IsProduct).Id;
        peer.Execute(session, Command(host, peer.Player, CookingRecipeOperation.Pickup, product), "pickup-product");
        peer.Execute(session, Command(host, peer.Player, CookingRecipeOperation.PutIn, product, container: Cup), "plate"); return product;
    }

    [Fact]
    public void Framed_paused_disconnect_cleanup_precedes_partner_continue_and_completes_manual_product_once()
    {
        var factory = new Factory(manual: true); using var host = factory.Host(); using var session = Session(host, factory); session.Start();
        using var a = new Peer(session, A); using var b = new Peer(session, B); a.Join(session); b.Join(session);
        a.Execute(session, Command(host, A, CookingRecipeOperation.StartProcess, Input, Board), "start-manual");
        var process = Assert.Single(host.Observe().Recipe!.Processes); Assert.Equal(1, process.ElapsedTicks);
        Assert.True(session.ApplyControl(new(CookingNetworkControlKind.Pause, "pause")).Accepted);
        var paused = host.Observe().CanonicalText(); var frame = host.HostFrameSequence; a.Dispose();
        for (var i = 0; i < 3; i++) session.ProcessOwnerFrame();
        Assert.Equal(paused, host.Observe().CanonicalText()); Assert.Equal(frame, host.HostFrameSequence);
        Assert.True(session.LatestSessionProjection.Participants.Single(p => p.Participant == A).CleanupPending);
        Assert.True(session.ApplyControl(new(CookingNetworkControlKind.Resume, "resume")).Accepted);
        b.Execute(session, Command(host, B, CookingRecipeOperation.ContinueProcess, process: process.Id), "partner-continue");
        process = Assert.Single(host.Observe().Recipe!.Processes); Assert.Equal(B, process.ActiveWorker); Assert.Equal(2, process.ElapsedTicks);
        var product = FinishAndPlate(host, session, b); Assert.Single(host.Observe().Recipe!.Items, i => i.IsProduct);
        Assert.Equal(product, Assert.Single(host.Observe().Recipe!.Items, i => i.IsProduct).Id);
        Assert.False(session.LatestSessionProjection.Participants.Single(p => p.Participant == A).CleanupPending);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Framed_disconnect_at_automatic_work_or_unbound_cup_retains_product_and_partner_delivers(bool afterCup)
    {
        var factory = new Factory(); using var host = factory.Host(); using var session = Session(host, factory); session.Start();
        using var a = new Peer(session, A); using var b = new Peer(session, B); a.Join(session); b.Join(session);
        a.Execute(session, Command(host, A, CookingRecipeOperation.StartProcess, Input, Board), "start-auto");
        ItemId product;
        if (afterCup) product = FinishAndPlate(host, session, a);
        else {
            var active = Assert.Single(host.Observe().Recipe!.Processes); Assert.Equal(1, active.ElapsedTicks); Assert.Null(active.ActiveWorker);
            a.Dispose(); session.ProcessOwnerFrame();
            Assert.Equal(2, Assert.Single(host.Observe().Recipe!.Processes).ElapsedTicks);
            product = FinishAndPlate(host, session, b);
        }
        Assert.Null(host.Observe().Recipe!.Items.Single(i => i.Id == product).BoundOrder);
        var contents = host.Observe().Recipe!.Items.Where(i => i.Location.Kind == LocationKind.ContainerSlot && i.Location.OwnerId == Cup.Value).Select(i => i.Id).ToArray();
        if (afterCup) { a.Dispose(); session.ProcessOwnerFrame(); }
        Assert.Equal(contents, host.Observe().Recipe!.Items.Where(i => i.Location.Kind == LocationKind.ContainerSlot && i.Location.OwnerId == Cup.Value).Select(i => i.Id).ToArray());
        b.Execute(session, Command(host, B, CookingRecipeOperation.Pickup, Cup), "partner-cup");
        b.Execute(session, Command(host, B, CookingRecipeOperation.BindOrder, product, order: Order), "partner-bind");
        b.Execute(session, Command(host, B, CookingRecipeOperation.SubmitOrder, product, order: Order), "partner-submit");
        Assert.Single(host.Observe().Recipe!.Settlements); Assert.DoesNotContain(host.Observe().Items, i => i.Id == Cup || i.Id == product);
    }

    [Fact]
    public void Real_submit_reply_is_dropped_full_state_arrives_and_live_rebind_returns_cached_terminal_once()
    {
        var factory = new Factory(); using var host = factory.Host(); using var session = Session(host, factory); session.Start();
        using var a = new Peer(session, A); a.Join(session);
        a.Execute(session, Command(host, A, CookingRecipeOperation.StartProcess, Input, Board), "start");
        var product = FinishAndPlate(host, session, a);
        a.Execute(session, Command(host, A, CookingRecipeOperation.Pickup, Cup), "cup");
        a.Execute(session, Command(host, A, CookingRecipeOperation.BindOrder, product, order: Order), "bind");
        var submit = Command(host, A, CookingRecipeOperation.SubmitOrder, product, order: Order);
        a.DropReplyCorrelation = "lost-submit";
        var original = a.Queue(session, submit, "stable-submit", "lost-submit"); session.ProcessOwnerFrame();
        Assert.Equal(1, a.DroppedReplies); Assert.False(a.Has("lost-submit"));
        Assert.Single(host.Observe().Recipe!.Settlements); Assert.Single(a.Latest.State.FullRecipe!.Settlements);
        Assert.DoesNotContain(a.Latest.State.Observation.Items, i => i.Id == Cup || i.Id == product);
        var committed = session.LatestCapture!.FullRecipe!;
        var consumedItems = committed.Items.Where(i => i.Id == Cup || i.Id == product).ToArray();
        Assert.All(consumedItems, i => Assert.True(i.Removed));
        var binding = a.Binding; a.Dispose(); session.ProcessOwnerFrame();
        using var rebound = new Peer(session, A); rebound.Join(session, binding);
        Assert.NotEqual(binding.RebindToken, rebound.Binding.RebindToken);
        var replay = rebound.Execute(session, submit, "stable-submit"); Assert.True(replay.Result!.IsDuplicate);
        Assert.Equal(CookingNetworkWireCodec.DomainId(original.ServerSessionInstance, original.Scope, A, original.StableCommandId), replay.DomainCommandId!.Value);
        var afterReplay = session.LatestCapture!.FullRecipe!;
        Assert.Equal(consumedItems, afterReplay.Items.Where(i => i.Id == Cup || i.Id == product).ToArray());
        Assert.Equal(committed.Settlements.ToArray(), afterReplay.Settlements.ToArray());
        Assert.Equal(committed.Deduplication.Count, afterReplay.Deduplication.Count);
        Assert.Single(host.Observe().Recipe!.Settlements);
    }

    [Fact]
    public void Queued_duplicate_callers_closed_before_owner_mapping_do_not_poison_same_stable_id_after_rebind()
    {
        var factory = new Factory(); using var host = factory.Host(); using var session = Session(host, factory); session.Start();
        using var a = new Peer(session, A); a.Join(session); var binding = a.Binding;
        var pickup = Command(host, A, CookingRecipeOperation.Pickup, Cup);
        a.Queue(session, pickup, "pending", "caller-one"); a.Queue(session, pickup, "pending", "caller-two");
        Assert.Equal(LocationKind.StationSlot, host.Observe().Items.Single(i => i.Id == Cup).Location.Kind);
        a.Dispose(); session.ProcessOwnerFrame(); Assert.Equal(0, host.PendingCommandIdentityCount);
        Assert.Empty(session.LatestCapture!.FullRecipe!.Deduplication);
        using var rebound = new Peer(session, A); rebound.Join(session, binding);
        var fresh = rebound.Execute(session, pickup, "pending"); Assert.False(fresh.Result!.IsDuplicate);
        Assert.Equal(A.Value, host.Observe().Items.Single(i => i.Id == Cup).Location.OwnerId);
    }

    [Fact]
    public void Real_successor_retires_old_scope_preserves_sequence_until_rebind_and_accepts_current_scope_work()
    {
        var factory = new Factory(); using var host = factory.Host(service: true); using var session = Session(host, factory); session.Start();
        using var a = new Peer(session, A); a.Join(session);
        var oldScope = host.Binding.LevelScope;
        a.Execute(session, Command(host, A, CookingRecipeOperation.Pickup, Cup), "old-pickup");
        a.Execute(session, Command(host, A, CookingRecipeOperation.Drop, Cup, Counter), "old-drop"); Assert.Equal(2, a.Sequence);
        CookingNetworkControlResult? transitioned = null;
        for (var i = 0; i < 120 && transitioned?.Accepted != true; i++) {
            transitioned = session.ApplyControl(new(CookingNetworkControlKind.ExecuteAuthorizedTransition, "successor"));
            if (!transitioned.Accepted) session.ProcessOwnerFrame();
        }
        Assert.True(transitioned?.Accepted == true, "Natural service did not finish within the fixture budget.");
        Assert.Equal(CookingLevelState.Created, host.Lifecycle.State); Assert.NotEqual(oldScope, host.Binding.LevelScope);
        var before = host.Observe().CanonicalText();
        a.Queue(session, Command(host, A, CookingRecipeOperation.Pickup, Cup), "pre-ack", "pre-ack", acknowledge: false);
        session.ProcessOwnerFrame(); Assert.Equal("BaselineRequired", CookingNetworkWireCodec.Read<CookingNetworkWireResult>(a.Packet("pre-ack"))!.Reason);
        Assert.Equal(before, host.Observe().CanonicalText());
        // Exact new scope ACK restores Ready; same physical generation retains its validated sequence2.
        a.AckLatest(); session.ProcessOwnerFrame();
        Assert.Equal(2, session.LatestSessionProjection.Participants.Single(p => p.Participant == A).LastValidatedClientSequence);
        Assert.True(session.ApplyControl(new(CookingNetworkControlKind.ExecuteAuthorizedTransition, "start-next")).Accepted);
        a.Queue(session, Command(host, A, CookingRecipeOperation.Pickup, Cup), "delayed-old", "delayed-old", oldScope, 999);
        session.ProcessOwnerFrame(); Assert.Equal("ScopeMismatch", CookingNetworkWireCodec.Read<CookingNetworkWireResult>(a.Packet("delayed-old"))!.Reason);
        Assert.Equal(2, session.LatestSessionProjection.Participants.Single(p => p.Participant == A).LastValidatedClientSequence);
        a.Execute(session, Command(host, A, CookingRecipeOperation.Pickup, Cup), "next-pickup");
        a.Execute(session, Command(host, A, CookingRecipeOperation.Drop, Cup, Counter), "next-drop");
        var binding = a.Binding; a.Dispose(); session.ProcessOwnerFrame(); using var rebound = new Peer(session, A); rebound.Join(session, binding);
        Assert.Equal(0, session.LatestSessionProjection.Participants.Single(p => p.Participant == A).LastValidatedClientSequence);
        rebound.Execute(session, Command(host, A, CookingRecipeOperation.Pickup, Cup), "after-rebind"); Assert.Equal(1, rebound.Sequence);
    }
}
