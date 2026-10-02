using System.Net;
using AbilityKit.Game.Cooking.EtRuntime;
using AbilityKit.Game.Cooking.Session;
using AbilityKit.Network.Host.InProcess;
using AbilityKit.Network.Transport.LiteNet;
using Xunit;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Game.Cooking.Tests;

public sealed class CookingNetworkSessionV3Tests
{
    private static readonly PlayerId A = new("a"), Z = new("z");
    private static readonly CookingLevelScope Scope = new(new(new("session-v3"), new("world"), new("match")), new(1), new("level"), 1);
    private static readonly DefinitionId Raw = new("raw"), Cooked = new("cooked");
    private static readonly StationSlotId Stove = new("stove");
    private static readonly ItemId Food = new("food");
    private static readonly RecipeId Recipe = new("cook");
    private sealed class Factory(CookingRecipeFixture fixture) : ICookingLevelGameplayFactory
    {
        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            var kitchen = new CookingRecipeSimulation(fixture);
            kitchen.AddWorldIngredient(Food, Raw, "spawn"); return kitchen;
        }
    }
    private static CookingLevelEtHost Host()
    {
        var items = new Dictionary<DefinitionId, CookingItemDefinition> { [Raw] = new(Raw, new HashSet<string> { "cook" }), [Cooked] = new(Cooked, new HashSet<string> { "cook" }) };
        var stations = new Dictionary<StationSlotId, CookingApplianceDefinition> { [Stove] = new(Stove, new HashSet<string> { "heat" }) };
        var recipes = new Dictionary<RecipeId, CookingRecipeDefinition> { [Recipe] = new(Recipe, new[] { Raw }, Cooked, new("cook-process"), "heat", 3) };
        var registry = new CookingConfigurationRegistry();
        Assert.True(registry.Submit(new(new[] { "heat" }, items.Values.ToArray(), stations.Values.ToArray(), recipes.Values.ToArray())).Accepted);
        var players = new[] { A, Z }.ToDictionary(p => p, p => new CookingPlayerConfig(p, new HashSet<string> { "cook" }, new HashSet<string> { "spawn", "stove" }));
        var fixture = new CookingRecipeFixture(Scope.MatchScope, players, items, stations, recipes);
        var host = new CookingLevelEtHost(new CookingLevelLifecycle(Scope, registry.Current!, new Factory(fixture)));
        Assert.True(host.Prepare(new(Scope.Level, new("map"), new(new("layout"), new[] { Stove }, Array.Empty<DefinitionId>()), registry.Current!.Identity)).Accepted);
        Assert.True(host.Start().Accepted); return host;
    }
    [Fact]
    public void Actual_ET_full_state_roundtrips_before_network_handshake()
    {
        using var host = Host();
        var state = new CookingNetworkAuthorityAdapter(host).CaptureFullState().State!;
        var baseline = new CookingNetworkBaseline(new("instance", A, 1, Scope, 1, 1, CookingNetworkWireCodec.Hash(state), "issue"), 8, 5, state);
        var bytes = CookingNetworkWireCodec.Encode(CookingNetworkMessageKind.Baseline, "roundtrip", baseline);
        Assert.True(CookingNetworkWireCodec.TryDecode(bytes, new(), out var envelope));
        var decoded = System.Text.Json.JsonSerializer.Deserialize<CookingNetworkBaseline>(envelope!.Payload.GetRawText(), CookingNetworkWireCodec.JsonOptions);
        Assert.NotNull(decoded);
        Assert.Equal(state.FullRecipe!.CanonicalText(), decoded!.State.FullRecipe!.CanonicalText());
        Assert.NotNull(CookingNetworkWireCodec.Read<CookingNetworkBaseline>(envelope));
        Assert.Throws<NotSupportedException>(() => ((IList<CookingRecipeCheckpointItem>)decoded.State.FullRecipe.Items)[0] = decoded.State.FullRecipe.Items[0]);
    }
    private static CookingRecipeCommand Pickup(PlayerId player) => new(Scope.MatchScope, 0, player, new("wire-unused"), CookingRecipeOperation.Pickup, Item: Food, ExpectedItemVersion: 1);
    private static void Pump(CookingNetworkSessionHost session, params Task[] tasks)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (tasks.Any(t => !t.IsCompleted) && DateTime.UtcNow < deadline) { session.ProcessOwnerFrame(); Thread.Sleep(5); }
        Assert.All(tasks, t => Assert.True(t.IsCompleted, "Owner loop timed out. " + session.Diagnostics.ToString()));
        Task.WhenAll(tasks).GetAwaiter().GetResult();
    }
    private static CookingNetworkSessionHost Session(CookingLevelEtHost host, bool udp = false)
        => new(new CookingNetworkAuthorityAdapter(host), udp ? new LiteNetChannelListener(IPAddress.Loopback, 0, "abilitykit-cooking-v3") : new InProcessChannelListener(),
            new Dictionary<PlayerId, string> { [A] = "join-a", [Z] = "join-z" });
    private static CookingNetworkSessionClient Local(CookingNetworkSessionHost session, PlayerId player)
        => new(player, player == A ? "join-a" : "join-z", session.CreateLocalClientTransport);

    [Fact]
    public async Task Framed_local_callback_is_readonly_and_reverse_player_arrival_wins_in_one_owner_tick()
    {
        using var host = Host(); using var session = Session(host); session.Start();
        using var a = Local(session, A); using var z = Local(session, Z);
        var joinA = a.ConnectAsync("inprocess", 1); var joinZ = z.ConnectAsync("inprocess", 1); Pump(session, joinA, joinZ);
        var before = host.Observe().CanonicalText(); var frame = host.HostFrameSequence;
        var first = z.SendCommandAsync("first", Pickup(Z)); var second = a.SendCommandAsync("second", Pickup(A));
        Assert.Equal(before, host.Observe().CanonicalText()); Assert.False(first.IsCompleted); Assert.False(second.IsCompleted);
        session.ProcessOwnerFrame(); Pump(session, first, second);
        Assert.Equal(frame + 1, host.HostFrameSequence);
        Assert.Equal(CookingRecipeOutcome.Accepted, (await first).Result!.Outcome);
        Assert.Equal(CookingRecipeOutcome.Rejected, (await second).Result!.Outcome);
        Assert.Equal(Z.Value, host.Observe().Items.Single(i => i.Id == Food).Location.OwnerId);
        var duplicate = z.SendCommandAsync("first", Pickup(Z)); Pump(session, duplicate);
        Assert.True((await duplicate).Result!.IsDuplicate);
        Assert.NotNull(z.LatestBaseline!.State.FullRecipe);
        var wire = CookingNetworkWireCodec.Encode(CookingNetworkMessageKind.Baseline, "full", z.LatestBaseline);
        Assert.True(CookingNetworkWireCodec.TryDecode(wire, new(), out var envelope));
        var decoded = CookingNetworkWireCodec.Read<CookingNetworkBaseline>(envelope!);
        Assert.Equal(z.LatestBaseline.State.FullRecipe!.CanonicalText(), decoded!.State.FullRecipe!.CanonicalText());
    }
    [Fact]
    public async Task Pending_conflicting_payload_notifies_original_and_new_caller_without_execution()
    {
        using var host = Host(); using var session = Session(host); session.Start(); using var a = Local(session, A);
        var join = a.ConnectAsync("inprocess", 1); Pump(session, join);
        var original = a.SendCommandAsync("identity", Pickup(A));
        var conflict = a.SendCommandAsync("identity", Pickup(A) with { Item = new("missing") });
        Pump(session, original, conflict);
        Assert.Equal(CookingNetworkDispositionKind.Conflicted, (await original).Disposition);
        Assert.Equal(CookingNetworkDispositionKind.Conflicted, (await conflict).Disposition);
        Assert.Equal(LocationKind.WorldPosition, host.Observe().Items.Single(i => i.Id == Food).Location.Kind);
    }
    [Fact]
    public async Task Pause_freezes_business_clocks_and_live_rebind_preserves_executed_mapping()
    {
        using var host = Host(); using var session = Session(host); session.Start(); using var a = Local(session, A);
        var join = a.ConnectAsync("inprocess", 1); Pump(session, join);
        var pickup = a.SendCommandAsync("same", Pickup(A)); Pump(session, pickup);
        Assert.True(session.ApplyControl(new(CookingNetworkControlKind.Pause, "pause")).Accepted);
        var paused = host.Observe().CanonicalText(); for (var i = 0; i < 4; i++) session.ProcessOwnerFrame();
        Assert.Equal(paused, host.Observe().CanonicalText());
        Assert.True(session.ApplyControl(new(CookingNetworkControlKind.Resume, "resume")).Accepted);
        var instance = a.ServerSessionInstance; var token = a.RebindToken;
        var reconnect = a.ReconnectAsync("inprocess", 1); Pump(session, reconnect);
        Assert.Equal(instance, a.ServerSessionInstance); Assert.Equal(token, a.RebindToken);
        var duplicate = a.SendCommandAsync("same", Pickup(A)); Pump(session, duplicate);
        Assert.True((await duplicate).Result!.IsDuplicate);
        Assert.Equal((await pickup).DomainCommandId, (await duplicate).DomainCommandId);
    }
    [Fact]
    public async Task Real_udp_remote_and_framed_local_share_the_existing_ET_owner()
    {
        using var host = Host(); using var session = Session(host, udp: true); session.Start();
        using var remote = new CookingNetworkSessionClient(Z, "join-z"); using var local = Local(session, A);
        var joinRemote = remote.ConnectAsync("127.0.0.1", session.Port); var joinLocal = local.ConnectAsync("inprocess", 1);
        Pump(session, joinRemote, joinLocal);
        var pickup = remote.SendCommandAsync("remote-first", Pickup(Z)); Pump(session, pickup);
        Assert.Equal(CookingRecipeOutcome.Accepted, (await pickup).Result!.Outcome);
        Pump(session, Task.Delay(20)); session.ProcessOwnerFrame(); Thread.Sleep(20);
        Assert.Equal(host.Observe().Items.Single(i => i.Id == Food).Location.OwnerId,
            remote.LatestBaseline!.State.Observation.Items.Single(i => i.Id == Food).Location.OwnerId);
        Assert.True(session.Diagnostics.ReceivedBytes > 0); Assert.True(session.Diagnostics.SentBytes > 0);
    }
    [Fact]
    public async Task Terminal_conflict_and_new_business_prefix_do_not_mix_old_batch_or_leave_callers_waiting()
    {
        using var host = Host(); using var session = Session(host); session.Start(); using var a = Local(session, A); using var z = Local(session, Z);
        Pump(session, a.ConnectAsync("inprocess", 1), z.ConnectAsync("inprocess", 1));
        var first = a.SendCommandAsync("executed", Pickup(A)); Pump(session, first); Assert.Equal(CookingRecipeOutcome.Accepted, (await first).Result!.Outcome);
        var conflict = a.SendCommandAsync("executed", Pickup(A) with { Item = new("missing") });
        var fresh = z.SendCommandAsync("fresh", Pickup(Z)); Pump(session, conflict, fresh);
        Assert.Equal("CommandIdentityConflict", (await conflict).Reason);
        Assert.Equal(CookingRecipeOutcome.Rejected, (await fresh).Result!.Outcome);
        var duplicate = a.SendCommandAsync("executed", Pickup(A)); Pump(session, duplicate);
        Assert.Equal(CookingRecipeOutcome.Accepted, (await duplicate).Result!.Outcome); Assert.True((await duplicate).Result!.IsDuplicate);
    }
    [Fact]
    public async Task Business_and_receipt_bounds_explicitly_reject_without_dropping_accepted_callers()
    {
        using var host = Host();
        using var session = new CookingNetworkSessionHost(new CookingNetworkAuthorityAdapter(host), new InProcessChannelListener(),
            new Dictionary<PlayerId, string> { [A] = "join-a" }, new(BusinessCapacity: 1, ControlCapacity: 1, ReceiptCapacity: 1));
        session.Start(); using var a = Local(session, A); Pump(session, a.ConnectAsync("inprocess", 1));
        var accepted = a.SendCommandAsync("one", Pickup(A)); var overflow = a.SendCommandAsync("two", Pickup(A));
        Assert.True(overflow.IsCompleted); Assert.Equal("QueueFull", (await overflow).Reason); Assert.False(accepted.IsCompleted);
        Pump(session, accepted); Assert.Equal(CookingRecipeOutcome.Accepted, (await accepted).Result!.Outcome);
        var exhausted = a.SendCommandAsync("three", Pickup(A)); Pump(session, exhausted); Assert.Equal("ReceiptCapacityExceeded", (await exhausted).Reason);
        var duplicate = a.SendCommandAsync("one", Pickup(A)); Pump(session, duplicate); Assert.True((await duplicate).Result!.IsDuplicate);
        Assert.InRange(session.Diagnostics.QueueHighWater, 1, 2); // One bounded business plus one bounded ack/control.
    }
    [Fact]
    public async Task Paused_join_controls_remain_live_and_disconnected_queued_input_cannot_run()
    {
        using var host = Host(); using var session = Session(host); session.Start(); using var a = Local(session, A); using var z = Local(session, Z);
        Pump(session, a.ConnectAsync("inprocess", 1));
        Assert.True(session.ApplyControl(new(CookingNetworkControlKind.Pause, "pause")).Accepted);
        var paused = host.Observe().CanonicalText(); Pump(session, z.ConnectAsync("inprocess", 1));
        Assert.Equal(paused, host.Observe().CanonicalText());
        Assert.Equal(CookingNetworkCheckpointUnavailableReason.LevelPaused, z.LatestBaseline!.State.CheckpointUnavailableReason);
        Assert.NotNull(z.LatestBaseline.State.FullRecipe); Assert.Null(z.LatestBaseline.State.ResumableCheckpoint);
        Assert.True(session.ApplyControl(new(CookingNetworkControlKind.Resume, "resume")).Accepted);
        var queued = a.SendCommandAsync("queued-before-close", Pickup(A)); a.Disconnect();
        await Assert.ThrowsAsync<IOException>(() => queued);
        session.ProcessOwnerFrame(); Assert.Equal(LocationKind.WorldPosition, host.Observe().Items.Single(i => i.Id == Food).Location.Kind);
        var next = z.SendCommandAsync("other-chef", Pickup(Z)); Pump(session, next); Assert.Equal(CookingRecipeOutcome.Accepted, (await next).Result!.Outcome);
    }
    [Fact]
    public void Baseline_ack_requires_the_actual_issued_identity_not_a_guessed_current_hash()
    {
        using var host = Host(); using var session = Session(host); session.Start();
        using var connection = new ConnectionManager(session.CreateLocalClientTransport, new ConnectionOptions { EnableReconnect = false });
        var packets = new List<CookingNetworkWireEnvelope>();
        connection.ServerPushReceived += (_, bytes) => { Assert.True(CookingNetworkWireCodec.TryDecode(bytes.AsSpan(), new(), out var envelope)); packets.Add(envelope!); };
        void Send<T>(CookingNetworkMessageKind kind, string correlation, T payload) => connection.Send(CookingNetworkWireCodec.OpCode,
            new ArraySegment<byte>(CookingNetworkWireCodec.Encode(kind, correlation, payload)), (ushort)NetworkPacketFlags.ServerPush);
        connection.Open("inprocess", 1); Send(CookingNetworkMessageKind.Join, "join", new CookingNetworkJoin(A, "join-a", null, null));
        session.ProcessOwnerFrame();
        var baseline = CookingNetworkWireCodec.Read<CookingNetworkBaseline>(packets.Single(p => p.Kind == CookingNetworkMessageKind.Baseline))!;
        var binding = CookingNetworkWireCodec.Read<CookingNetworkJoined>(packets.Single(p => p.Kind == CookingNetworkMessageKind.Joined))!;
        Send(CookingNetworkMessageKind.BaselineAck, "bad-ack", baseline.Identity with { IssueId = "guessed" });
        Send(CookingNetworkMessageKind.Command, "blocked", new CookingNetworkWireCommand(binding.ServerSessionInstance, binding.ConnectionGeneration, 1, "blocked", Scope, Pickup(A)));
        session.ProcessOwnerFrame();
        Assert.DoesNotContain(packets, p => p.Kind == CookingNetworkMessageKind.Ready);
        Assert.Equal("BaselineRequired", CookingNetworkWireCodec.Read<CookingNetworkWireResult>(packets.Single(p => p.CorrelationId == "blocked"))!.Reason);
        Assert.Equal(LocationKind.WorldPosition, host.Observe().Items.Single(i => i.Id == Food).Location.Kind);
        Send(CookingNetworkMessageKind.BaselineAck, "valid-ack", baseline.Identity); session.ProcessOwnerFrame();
        Assert.Contains(packets, p => p.Kind == CookingNetworkMessageKind.Ready);
    }
}






