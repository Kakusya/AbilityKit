using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using AbilityKit.Game.Cooking.EtRuntime;
using AbilityKit.Game.Cooking.Session;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Host.InProcess;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Collection(CookingEtHostTestCollection.Name)]
public sealed class CookingNetworkSessionBoundaryFollowupTests
{
    private static readonly PlayerId Player = new("boundary-player");
    private static readonly DefinitionId Raw = new("boundary-raw"), Product = new("boundary-product");
    private static readonly StationSlotId Station = new("boundary-station");
    private static readonly CookingLevelScope Scope = new(new(new("boundary-session"), new("world"), new("match")), new(1), new("level"), 1);
    private static ItemId Food(int index = 0) => new("boundary-food-" + index);

    private sealed class Factory : ICookingLevelGameplayFactory
    {
        public Action? OnCreate { get; set; }
        public int CreateCount { get; private set; }
        public CookingConfigurationSnapshot Configuration { get; }
        private readonly CookingRecipeFixture _fixture;
        public Factory()
        {
            var tags = new HashSet<string> { "cook" };
            var items = new[] { new CookingItemDefinition(Raw, tags), new CookingItemDefinition(Product, tags) };
            var appliances = new[] { new CookingApplianceDefinition(Station, new HashSet<string> { "heat" }) };
            var recipes = new[] { new CookingRecipeDefinition(new("boundary-recipe"), new[] { Raw }, Product, new("boundary-process"), "heat", 3) };
            var registry = new CookingConfigurationRegistry();
            Assert.True(registry.Submit(new(new[] { "heat" }, items, appliances, recipes)).Accepted);
            Configuration = registry.Current!;
            _fixture = new(Scope.MatchScope, new Dictionary<PlayerId, CookingPlayerConfig> {
                [Player] = new(Player, tags, Enumerable.Range(0, 6).Select(i => "spawn-" + i).Append(Station.Value).ToHashSet())
            }, items.ToDictionary(i => i.Id), appliances.ToDictionary(a => a.Station), recipes.ToDictionary(r => r.Id));
        }
        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            CreateCount++; OnCreate?.Invoke();
            var kitchen = new CookingRecipeSimulation(_fixture);
            for (var i = 0; i < 6; i++) kitchen.AddWorldIngredient(Food(i), Raw, "spawn-" + i);
            return kitchen;
        }
        public CookingLevelEtHost Host(bool start = true)
        {
            var host = new CookingLevelEtHost(new CookingLevelLifecycle(Scope, Configuration, this));
            if (start) Start(host);
            return host;
        }
        public void Start(CookingLevelEtHost host)
        {
            Assert.True(host.Prepare(new(Scope.Level, new("boundary-map"), new(new("boundary-layout"), new[] { Station }, Array.Empty<DefinitionId>()), Configuration.Identity)).Accepted);
            Assert.True(host.Start().Accepted);
        }
    }

    private sealed class Peer : IDisposable
    {
        private readonly ConnectionManager _connection;
        private readonly ConcurrentQueue<(CookingNetworkWireEnvelope Envelope, byte[] Bytes)> _packets = new();
        private long _sequence;
        public Peer(CookingNetworkSessionHost session, Func<ITransport>? transport = null, bool expectCapacityRejection = false)
        {
            _connection = new(transport ?? session.CreateLocalClientTransport, new ConnectionOptions { EnableReconnect = false });
            _connection.ServerPushReceived += (_, bytes) => {
                var owned = bytes.ToArray();
                Assert.True(CookingNetworkWireCodec.TryDecode(owned, new(), out var envelope));
                _packets.Enqueue((envelope!, owned));
            };
            try { _connection.Open("inprocess", 1); }
            catch (InvalidOperationException error) when (expectCapacityRejection && error.Message == "In-process host channel is closed.") { }
        }
        public bool IsConnected => _connection.IsConnected;
        public CookingNetworkWireEnvelope[] Packets => _packets.Select(p => p.Envelope).ToArray();
        public byte[] LastBaselineBytes => _packets.Last(p => p.Envelope.Kind == CookingNetworkMessageKind.Baseline).Bytes;
        public CookingNetworkBaseline Baseline => CookingNetworkWireCodec.Read<CookingNetworkBaseline>(Packets.Last(p => p.Kind == CookingNetworkMessageKind.Baseline))!;
        public CookingNetworkJoined Binding => CookingNetworkWireCodec.Read<CookingNetworkJoined>(Packets.Last(p => p.Kind == CookingNetworkMessageKind.Joined))!;
        public void Send<T>(CookingNetworkMessageKind kind, string correlation, T value) => _connection.Send(CookingNetworkWireCodec.OpCode,
            new ArraySegment<byte>(CookingNetworkWireCodec.Encode(kind, correlation, value)), (ushort)NetworkPacketFlags.ServerPush);
        public void Join(string correlation = "join") => Send(CookingNetworkMessageKind.Join, correlation, new CookingNetworkJoin(Player, "boundary-credential", null, null));
        public void Ack(string correlation = "ack") => Send(CookingNetworkMessageKind.BaselineAck, correlation, Baseline.Identity);
        public void Command(CookingRecipeCommand command, string stable, string? correlation = null) => Send(CookingNetworkMessageKind.Command, correlation ?? stable,
            new CookingNetworkWireCommand(Binding.ServerSessionInstance, Binding.ConnectionGeneration, ++_sequence, stable, Scope, command));
        public bool Has(string correlation) => Packets.Any(p => p.CorrelationId == correlation && p.Kind is CookingNetworkMessageKind.CommandResult or CookingNetworkMessageKind.Rejected);
        public CookingNetworkWireResult Result(string correlation) => CookingNetworkWireCodec.Read<CookingNetworkWireResult>(Packets.Last(p => p.CorrelationId == correlation && p.Kind is CookingNetworkMessageKind.CommandResult or CookingNetworkMessageKind.Rejected))!;
        public void Dispose() => _connection.Dispose();
    }

    private static CookingNetworkSessionHost Session(CookingLevelEtHost host, CookingNetworkSessionOptions? options = null, InProcessChannelListener? remote = null)
    {
        var session = new CookingNetworkSessionHost(new CookingNetworkAuthorityAdapter(host), remote ?? new InProcessChannelListener(),
            new Dictionary<PlayerId, string> { [Player] = "boundary-credential" }, options);
        session.Start(); return session;
    }
    private static void Pump(CookingNetworkSessionHost session, Func<bool> done)
    {
        var until = Environment.TickCount64 + 10000;
        while (!done() && Environment.TickCount64 < until) { session.ProcessOwnerFrame(); Thread.Sleep(1); }
        Assert.True(done(), "Bounded boundary-control pump expired.");
    }
    private static void Ready(CookingNetworkSessionHost session, Peer peer)
    {
        peer.Join(); Pump(session, () => peer.Packets.Any(p => p.Kind == CookingNetworkMessageKind.Baseline));
        peer.Ack(); Pump(session, () => peer.Packets.Any(p => p.Kind == CookingNetworkMessageKind.Ready));
    }
    private static CookingRecipeCommand Pickup(int index = 0) => new(Scope.MatchScope, 0, Player, new("placeholder"), CookingRecipeOperation.Pickup, Item: Food(index), ExpectedItemVersion: 1);
    private static CookingRecipeCommand LegalNext(CookingNetworkSessionHost session)
    {
        var item = session.LatestCapture!.FullRecipe!.Items.Single(i => i.Id == Food());
        return new(Scope.MatchScope, 0, Player, new("placeholder"), item.Location.Kind == LocationKind.PlayerHand ? CookingRecipeOperation.Drop : CookingRecipeOperation.Pickup,
            Item: Food(), Station: item.Location.Kind == LocationKind.PlayerHand ? Station : null, ExpectedItemVersion: item.Version);
    }

    [Fact]
    public void Real_factory_callback_Busy_rejects_unmapped_join_then_fresh_join_ack_and_command_recover()
    {
        var factory = new Factory(); using var host = factory.Host(start: false); var port = new CookingNetworkAuthorityAdapter(host);
        using var session = Session(host); using var peer = new Peer(session);
        var retained = session.LatestCapture; var before = retained!.Observation.CanonicalText(); var frame = host.HostFrameSequence;
        CookingNetworkCaptureResult? busy = null; var calls = 0;
        peer.Join("during-start");
        // Explicit trusted initialization callback reentry, not an ordinary network callback
        // or an admitted waiter spanning frames. No allocator nested-frame probe is used.
        factory.OnCreate = () => {
            calls++; busy = port.CaptureFullState();
            Assert.False(busy.Accepted); Assert.Equal(CookingNetworkCaptureReason.Busy, busy.Reason); Assert.Null(busy.State);
            Assert.Null(session.ProcessOwnerFrame());
            Assert.Same(retained, session.LatestCapture); Assert.Equal(before, retained.Observation.CanonicalText());
            Assert.Equal(frame, host.HostFrameSequence);
            Assert.DoesNotContain(peer.Packets, p => p.Kind is CookingNetworkMessageKind.Joined or CookingNetworkMessageKind.Ready or CookingNetworkMessageKind.Baseline);
        };
        factory.Start(host); Assert.Equal(1, calls); Assert.Equal(1, factory.CreateCount);
        Assert.Equal("Busy", peer.Result("during-start").Reason);
        Assert.True(port.CaptureFullState().Accepted);
        peer.Join("healthy-join"); Pump(session, () => peer.Packets.Any(p => p.Kind == CookingNetworkMessageKind.Baseline));
        peer.Ack("healthy-ack"); Pump(session, () => peer.Packets.Any(p => p.Kind == CookingNetworkMessageKind.Ready));
        var command = Pickup(); peer.Command(command, "first"); Pump(session, () => peer.Has("first"));
        Assert.Equal(CookingRecipeOutcome.Accepted, peer.Result("first").Result!.Outcome);
        var committed = session.LatestCapture!.FullRecipe!;
        peer.Command(command, "first", "cached"); Pump(session, () => peer.Has("cached"));
        Assert.True(peer.Result("cached").Result!.IsDuplicate); Assert.Equal(peer.Result("first").DomainCommandId, peer.Result("cached").DomainCommandId);
        Assert.Equal(committed.Items, session.LatestCapture!.FullRecipe!.Items); Assert.Equal(committed.Deduplication, session.LatestCapture.FullRecipe.Deduplication);
        Assert.Equal(committed.NextProductId, session.LatestCapture.FullRecipe.NextProductId);
    }

    [Fact]
    public void Disposed_real_port_returns_null_and_fresh_join_cannot_receive_ready_or_export()
    {
        using var host = new Factory().Host(); var port = new CookingNetworkAuthorityAdapter(host);
        using var session = Session(host); using var ready = new Peer(session); Ready(session, ready);
        var retained = session.LatestCapture; var canonical = retained!.FullRecipe!.CanonicalText(); host.Dispose();
        var disposed = port.CaptureFullState(); Assert.False(disposed.Accepted); Assert.Null(disposed.State); Assert.Equal(CookingNetworkCaptureReason.Disposed, disposed.Reason);
        session.ProcessOwnerFrame(); Assert.All(session.LatestSessionProjection.Participants, p => Assert.False(p.Ready));
        using var fresh = new Peer(session); fresh.Join("fresh-disposed"); Pump(session, () => fresh.Has("fresh-disposed"));
        Assert.Equal("Disposed", fresh.Result("fresh-disposed").Reason);
        Assert.DoesNotContain(fresh.Packets, p => p.Kind is CookingNetworkMessageKind.Joined or CookingNetworkMessageKind.Ready or CookingNetworkMessageKind.Baseline);
        Assert.Same(retained, session.LatestCapture); Assert.Equal(canonical, retained.FullRecipe.CanonicalText());
        Assert.Throws<ObjectDisposedException>(() => Session(host));
    }

    [Theory]
    [InlineData(false, 2)] [InlineData(false, 3)] [InlineData(false, 4)]
    [InlineData(true, 2)] [InlineData(true, 3)] [InlineData(true, 4)]
    public void Business_and_perconnection_three_slot_bounds_terminalize_all_accepted_callers(bool perConnection, int count)
    {
        using var host = new Factory().Host(); using var session = Session(host,
            new(BusinessCapacity: perConnection ? 8 : 3, PerConnectionCapacity: perConnection ? 3 : 8));
        using var peer = new Peer(session); Ready(session, peer); var before = host.Observe().CanonicalText();
        for (var i = 0; i < count; i++) peer.Command(Pickup(i), "queued-" + i);
        Assert.Equal(before, host.Observe().CanonicalText()); Assert.Equal(Math.Min(count, 3), session.Diagnostics.Pending);
        if (count == 4) Assert.Equal("QueueFull", peer.Result("queued-3").Reason);
        Pump(session, () => Enumerable.Range(0, count).All(i => peer.Has("queued-" + i)));
        Assert.All(Enumerable.Range(0, Math.Min(count, 3)), i => Assert.NotNull(peer.Result("queued-" + i).Result));
        Assert.Equal(Math.Min(count, 3), session.LatestCapture!.FullRecipe!.Deduplication.Count);
        Assert.False(session.LatestCapture.FullRecipe.Items.Single(i => i.Id == Food(3)).Removed);
    }

    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void Control_three_slot_flood_preserves_close_marker_until_owner_releases_binding(int count)
    {
        using var host = new Factory().Host(start: false); using var session = Session(host, new(ControlCapacity: 3, PerConnectionCapacity: 8));
        using var peer = new Peer(session); Ready(session, peer); var before = host.Observe().CanonicalText(); var frame = host.HostFrameSequence;
        for (var i = 0; i < count; i++) peer.Ack("control-" + i);
        if (count == 4) Assert.Equal("QueueFull", peer.Result("control-3").Reason);
        peer.Dispose(); session.ProcessOwnerFrame();
        Assert.False(session.LatestSessionProjection.Participants.Single().ConnectedOwnerBinding);
        Assert.Equal(before, host.Observe().CanonicalText()); Assert.Equal(frame, host.HostFrameSequence);
        Assert.Equal(0, session.Diagnostics.Pending);
    }

    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void Shared_connection_slots_include_unbound_and_closed_entries_until_owner_drain(int count)
    {
        using var host = new Factory().Host(start: false); var remote = new InProcessChannelListener(); using var session = Session(host, new(ConnectionCapacity: 3), remote);
        var peers = new List<Peer>();
        try {
            for (var i = 0; i < count; i++) peers.Add(new Peer(session, i % 2 == 0 ? null : remote.CreateClientTransport, expectCapacityRejection: i >= 3));
            Assert.All(peers.Take(Math.Min(count, 3)), p => Assert.True(p.IsConnected));
            if (count == 4) Assert.False(peers[3].IsConnected);
            // A physical fourth local connection is rejected. Join provides an observable
            // response for admitted unbound peers without reading private connection slots.
            peers[0].Join("one"); session.ProcessOwnerFrame(); Assert.Contains(peers[0].Packets, p => p.Kind == CookingNetworkMessageKind.Joined);
            if (count < 3) return;
            peers[1].Dispose(); using var beforeDrain = new Peer(session, expectCapacityRejection: true); Assert.False(beforeDrain.IsConnected);
            Assert.Empty(beforeDrain.Packets); session.ProcessOwnerFrame();
            using var replacement = new Peer(session); replacement.Join("replacement"); Pump(session, () => replacement.Has("replacement") || replacement.Packets.Any(p => p.Kind == CookingNetworkMessageKind.Joined));
            // Participant already has a generation; null/null is explicitly rejected as Unauthorized,
            // proving a new transport slot was admitted rather than silently accepting a ghost binding.
            Assert.Equal("Unauthorized", replacement.Result("replacement").Reason);
        }
        finally { foreach (var peer in peers) peer.Dispose(); }
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Two_duplicate_waiters_execute_one_identity_and_excess_caller_is_explicit(int count)
    {
        using var host = new Factory().Host(); using var session = Session(host, new(DuplicateWaiterCapacity: 2));
        using var peer = new Peer(session); Ready(session, peer);
        for (var i = 0; i < count; i++) peer.Command(Pickup(), "same", "caller-" + i);
        Pump(session, () => Enumerable.Range(0, count).All(i => peer.Has("caller-" + i)));
        Assert.Equal(CookingRecipeOutcome.Accepted, peer.Result("caller-0").Result!.Outcome);
        if (count >= 2) Assert.True(peer.Result("caller-1").Result!.IsDuplicate);
        if (count == 3) Assert.Equal("QueueFull", peer.Result("caller-2").Reason);
        Assert.Single(session.LatestCapture!.FullRecipe!.Deduplication);
        var captured = session.LatestCapture.FullRecipe;
        peer.Command(Pickup(), "same", "cached-after-waiters"); Pump(session, () => peer.Has("cached-after-waiters"));
        Assert.True(peer.Result("cached-after-waiters").Result!.IsDuplicate); Assert.Equal(captured.Items, session.LatestCapture.FullRecipe!.Items);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Prefix_two_defers_third_command_without_queue_rejection_or_batch_mix(int count)
    {
        using var host = new Factory().Host(); using var session = Session(host, new(MaximumPrefix: 2));
        using var peer = new Peer(session); Ready(session, peer);
        for (var i = 0; i < count; i++) peer.Command(Pickup(i), "prefix-" + i);
        var first = session.ProcessOwnerFrame()!;
        Assert.Equal(Math.Min(count, 2), first.Admissions.Count); Assert.Equal(Math.Max(0, count - 2), session.Diagnostics.Pending);
        Assert.Equal(Math.Min(count, 2), session.LatestSessionProjection.Participants.Single().LastValidatedClientSequence);
        if (count == 3) { Assert.False(peer.Has("prefix-2")); var next = session.ProcessOwnerFrame()!; Assert.Single(next.Admissions); Assert.True(next.Accepted); }
        Assert.All(Enumerable.Range(0, count), i => Assert.True(peer.Has("prefix-" + i)));
        Assert.Equal(count, session.LatestCapture!.FullRecipe!.Deduplication.Count);
    }

    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void Three_receipts_keep_cached_terminal_when_fourth_identity_exhausts_bound(int count)
    {
        using var host = new Factory().Host(); using var session = Session(host, new(ReceiptCapacity: 3));
        using var peer = new Peer(session); Ready(session, peer); var first = LegalNext(session);
        for (var i = 0; i < count; i++) { peer.Command(LegalNext(session), "receipt-" + i); Pump(session, () => peer.Has("receipt-" + i)); }
        if (count == 4) Assert.Equal("ReceiptCapacityExceeded", peer.Result("receipt-3").Reason);
        Assert.Equal(Math.Min(count, 3), session.LatestCapture!.FullRecipe!.Deduplication.Count);
        var kept = session.LatestCapture.FullRecipe;
        peer.Command(first, "receipt-0", "retained"); Pump(session, () => peer.Has("retained"));
        Assert.True(peer.Result("retained").Result!.IsDuplicate); Assert.Equal(kept.Items, session.LatestCapture.FullRecipe!.Items);
        Assert.Equal(kept.Deduplication, session.LatestCapture.FullRecipe.Deduplication); Assert.Equal(kept.NextProductId, session.LatestCapture.FullRecipe.NextProductId);
    }

    private static byte[] Envelope(string kind, string payload) => Encoding.UTF8.GetBytes("{\"protocolVersion\":3,\"kind\":\"" + kind + "\",\"correlationId\":\"boundary\",\"payload\":" + payload + "}");
    private static int Tokens(byte[] bytes) { var reader = new Utf8JsonReader(bytes); var count = 0; while (reader.Read()) count++; return count; }
    private static int LargestCollection(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        int Visit(JsonElement value) => value.ValueKind switch {
            JsonValueKind.Object => Math.Max(value.EnumerateObject().Count(), value.EnumerateObject().Select(p => Visit(p.Value)).DefaultIfEmpty().Max()),
            JsonValueKind.Array => Math.Max(value.GetArrayLength(), value.EnumerateArray().Select(Visit).DefaultIfEmpty().Max()), _ => 0 };
        return Visit(document.RootElement);
    }

    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(1)]
    public void Actual_wire_bytes_tokens_and_collections_obey_exact_injected_intersections(int offset)
    {
        var bytes = Envelope("Baseline", "[0,0,0,0,0,0,0,0]");
        Assert.Equal(offset >= 0, CookingNetworkWireCodec.TryDecode(bytes, new(FrameBytes: bytes.Length + offset), out _));
        Assert.Equal(offset >= 0, CookingNetworkWireCodec.TryDecode(bytes, new(BaselineTokenLimit: Tokens(bytes) + offset), out _));
        Assert.Equal(offset >= 0, CookingNetworkWireCodec.TryDecode(bytes, new(BaselineCollectionLimit: 8 + offset), out _));
        foreach (var kind in new[] { "Join", "Command" }) {
            var plain = Envelope(kind, "{}");
            var bound = kind == "Join" ? new CookingNetworkSessionOptions(ControlBytes: plain.Length + offset) : new(CommandBytes: plain.Length + offset);
            Assert.Equal(offset >= 0, CookingNetworkWireCodec.TryDecode(plain, bound, out _));
            var collection = Envelope(kind, "[" + string.Join(',', Enumerable.Repeat("0", 4096 + offset)) + "]");
            Assert.Equal(offset <= 0, CookingNetworkWireCodec.TryDecode(collection, new(ControlBytes: 65536), out _));
        }
        // Scanner-only synthetic JSON is not a typed legal gameplay command or handshake.
        // Typed validation/authorization are exercised through actual peers in the other cases.
    }

    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(1)]
    public void Fixed_nonbaseline_token_and_depth_limits_are_checked_independently_of_bytes(int offset)
    {
        foreach (var kind in new[] { "Join", "Command" }) {
            var target = 65536 + offset;
            var remaining = target - Tokens(Envelope(kind, "[]")) - 32;
            var groups = new List<string>();
            for (var i = 0; i < 16; i++) {
                var size = Math.Min(4096, remaining); remaining -= size;
                groups.Add("[" + string.Join(',', Enumerable.Repeat("0", size)) + "]");
            }
            Assert.Equal(0, remaining);
            var bytes = Envelope(kind, "[" + string.Join(',', groups) + "]");
            Assert.Equal(target, Tokens(bytes));
            Assert.Equal(offset <= 0, CookingNetworkWireCodec.TryDecode(bytes, new(CommandBytes: 256 * 1024, ControlBytes: 256 * 1024), out _));
        }
        var depth = 32 + offset;
        var nested = Envelope("Baseline", new string('[', depth - 1) + "0" + new string(']', depth - 1));
        Assert.Equal(offset <= 0, CookingNetworkWireCodec.TryDecode(nested, new(), out _));
    }
    [Theory]
    [InlineData("value", 128)] [InlineData("joinCredential", 256)] [InlineData("text", 1024)]
    public void Fixed_string_thresholds_are_not_invented_configurable_knobs(string property, int limit)
    {
        foreach (var length in new[] { limit - 1, limit, limit + 1 }) {
            var bytes = Envelope("Baseline", "{\"" + property + "\":\"" + new string('x', length) + "\"}");
            Assert.Equal(length <= limit, CookingNetworkWireCodec.TryDecode(bytes, new(), out _));
        }
    }

    [Theory]
    [InlineData("bytes", -1)] [InlineData("bytes", 0)] [InlineData("bytes", 1)]
    [InlineData("tokens", -1)] [InlineData("tokens", 0)] [InlineData("tokens", 1)]
    [InlineData("collection", -1)] [InlineData("collection", 0)] [InlineData("collection", 1)]
    public void First_and_new_join_outbound_baseline_enforces_measured_limit_without_phony_ready(string dimension, int offset)
    {
        byte[] image;
        using (var referenceHost = new Factory().Host(start: false))
        using (var reference = Session(referenceHost))
        using (var peer = new Peer(reference)) { peer.Join(); Pump(reference, () => peer.Packets.Any(p => p.Kind == CookingNetworkMessageKind.Baseline)); image = peer.LastBaselineBytes; }
        var options = dimension switch { "bytes" => new CookingNetworkSessionOptions(FrameBytes: image.Length + offset),
            "tokens" => new(BaselineTokenLimit: Tokens(image) + offset), _ => new(BaselineCollectionLimit: LargestCollection(image) + offset) };
        using var host = new Factory().Host(start: false); using var session = Session(host, options); using var first = new Peer(session);
        var canonical = host.Observe().CanonicalText(); var frame = host.HostFrameSequence;
        first.Join("bounded-first"); session.ProcessOwnerFrame();
        Assert.Equal(canonical, host.Observe().CanonicalText()); Assert.Equal(frame, host.HostFrameSequence);
        if (offset < 0) {
            Assert.Contains(first.Packets, p => p.Kind == CookingNetworkMessageKind.Rejected && CookingNetworkWireCodec.Read<CookingNetworkWireResult>(p)!.Reason == "FullStateExceedsWireBounds");
            Assert.DoesNotContain(first.Packets, p => p.Kind is CookingNetworkMessageKind.Baseline or CookingNetworkMessageKind.Ready);
            using var next = new Peer(session); next.Join("bounded-new"); session.ProcessOwnerFrame();
            Assert.Equal("FullStateExceedsWireBounds", next.Result("bounded-new").Reason);
            Assert.DoesNotContain(next.Packets, p => p.Kind is CookingNetworkMessageKind.Joined or CookingNetworkMessageKind.Baseline or CookingNetworkMessageKind.Ready);
        } else {
            Assert.Contains(first.Packets, p => p.Kind == CookingNetworkMessageKind.Baseline);
            first.Ack("bounded-exact-ack"); Pump(session, () => first.Packets.Any(p => p.Kind == CookingNetworkMessageKind.Ready));
        }
    }

    [Theory]
    [InlineData("bytes")] [InlineData("tokens")] [InlineData("collection")]
    public void Ready_growth_budget_exhaustion_preserves_history_and_never_recovers_by_pruning(string dimension)
    {
        byte[] image;
        using (var calibrationHost = new Factory().Host())
        using (var calibration = Session(calibrationHost))
        using (var peer = new Peer(calibration)) {
            Ready(calibration, peer); peer.Ack("calibration-ack"); calibration.ProcessOwnerFrame();
            peer.Command(Pickup(), "calibration-first"); Pump(calibration, () => peer.Has("calibration-first"));
            Assert.NotEmpty(calibration.LatestCapture!.FullRecipe!.Deduplication); image = peer.LastBaselineBytes;
        }
        var options = dimension switch { "bytes" => new CookingNetworkSessionOptions(FrameBytes: image.Length + 256),
            "tokens" => new(BaselineTokenLimit: Tokens(image) + 64), _ => new(BaselineCollectionLimit: LargestCollection(image) + 1) };
        using var host = new Factory().Host(); using var session = Session(host, options); using var ready = new Peer(session); Ready(session, ready);
        for (var i = 0; i < 16 && session.LatestSessionProjection.Participants.Single().Ready; i++) {
            ready.Ack("growth-ack-" + i); session.ProcessOwnerFrame();
            if (!session.LatestSessionProjection.Participants.Single().Ready) break;
            ready.Command(LegalNext(session), "growth-" + i); Pump(session, () => ready.Has("growth-" + i));
        }
        Assert.False(session.LatestSessionProjection.Participants.Single().Ready); Assert.False(host.IsFaulted);
        Assert.NotEmpty(session.LatestCapture!.FullRecipe!.Deduplication);
        var kept = host.Observe().CanonicalText(); var receipts = session.LatestCapture.FullRecipe.Deduplication.ToArray();
        session.ProcessOwnerFrame(); Assert.Equal(kept, host.Observe().CanonicalText()); Assert.Equal(receipts, session.LatestCapture.FullRecipe!.Deduplication.ToArray());
        using var fresh = new Peer(session); fresh.Join("growth-new"); Pump(session, () => fresh.Has("growth-new"));
        Assert.Equal("FullStateExceedsWireBounds", fresh.Result("growth-new").Reason);
        Assert.DoesNotContain(fresh.Packets, p => p.Kind is CookingNetworkMessageKind.Baseline or CookingNetworkMessageKind.Ready);
    }
}
