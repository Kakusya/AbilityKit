using System.Net;
using AbilityKit.Game.Cooking.EtRuntime;
using AbilityKit.Game.Cooking.Session;
using AbilityKit.Network.Host.InProcess;
using AbilityKit.Network.Transport.LiteNet;
using Xunit;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Game.Cooking.Tests;

[Collection(CookingEtHostTestCollection.Name)]
public sealed class CookingNetworkSessionV3Tests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;
    public CookingNetworkSessionV3Tests(Xunit.Abstractions.ITestOutputHelper output) => _output = output;
    private static readonly PlayerId A = new("a"), Z = new("z");
    private static readonly CookingLevelScope Scope = new(new(new("session-v3"), new("world"), new("match")), new(1), new("level"), 1);
    private static readonly DefinitionId Raw = new("raw"), Cooked = new("cooked");
    private static readonly StationSlotId Stove = new("stove");
    private static readonly ItemId Food = new("food");
    private static readonly RecipeId Recipe = new("cook");
    private sealed class Factory(CookingRecipeFixture fixture, CookingMajorProgress? progress = null, ICookingProductIdAllocator? allocator = null) : ICookingLevelGameplayFactory
    {
        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration)
        {
            var kitchen = new CookingRecipeSimulation(fixture, allocator);
            if (allocator is null) kitchen.AddWorldIngredient(Food, Raw, "spawn"); else kitchen.AddItem(Food, Raw, ItemLocation.Station(Stove)); if (progress is not null) kitchen.UseMajorProgress(progress); return kitchen;
        }
    }
    private static CookingLevelEtHost Host(CookingMajorProgress? progress = null, ICookingProductIdAllocator? allocator = null, bool initialCreated = false)
    {
        var items = new Dictionary<DefinitionId, CookingItemDefinition> { [Raw] = new(Raw, new HashSet<string> { "cook" }), [Cooked] = new(Cooked, new HashSet<string> { "cook" }) };
        var stations = new Dictionary<StationSlotId, CookingApplianceDefinition> { [Stove] = new(Stove, new HashSet<string> { "heat" }) };
        var recipes = new Dictionary<RecipeId, CookingRecipeDefinition> { [Recipe] = new(Recipe, new[] { Raw }, Cooked, new("cook-process"), "heat", 3) };
        var registry = new CookingConfigurationRegistry();
        Assert.True(registry.Submit(new(new[] { "heat" }, items.Values.ToArray(), stations.Values.ToArray(), recipes.Values.ToArray())).Accepted);
        var players = new[] { A, Z }.ToDictionary(p => p, p => new CookingPlayerConfig(p, new HashSet<string> { "cook" }, new HashSet<string> { "spawn", "stove" }));
        var fixture = new CookingRecipeFixture(Scope.MatchScope, players, items, stations, recipes);
        var host = new CookingLevelEtHost(new CookingLevelLifecycle(Scope, registry.Current!, new Factory(fixture, progress, allocator)));
        if (initialCreated) return host;
        Assert.True(host.Prepare(new(Scope.Level, new("map"), new(new("layout"), new[] { Stove }, Array.Empty<DefinitionId>()), registry.Current!.Identity)).Accepted);
        Assert.True(host.Start().Accepted); return host;
    }
    [Fact]
    public void Actual_ET_full_state_roundtrips_before_network_handshake()
    {
        using var host = Host();
        var state = new CookingNetworkAuthorityAdapter(host).CaptureFullState().State!;
        var sessionView = new CookingNetworkSessionProjection("instance", Array.AsReadOnly(new[] { new CookingNetworkParticipantProjection(A, false, 0, 0, 0, false, false), new CookingNetworkParticipantProjection(Z, false, 0, 0, 0, false, false) }));
        var baseline = new CookingNetworkBaseline(new("instance", A, 1, Scope, 1, 1, CookingNetworkWireCodec.BaselineHash(state, sessionView), "issue"), 8, 5, state, sessionView);
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
        Assert.Equal(instance, a.ServerSessionInstance); Assert.NotEqual(token, a.RebindToken);
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
        // Cancellation completes off-thread. Wait without yielding the ET owner thread;
        // the assertion below then awaits an already-terminal task and does not advance a frame.
        Assert.True(SpinWait.SpinUntil(() => queued.IsCompleted, TimeSpan.FromSeconds(10)), "Disconnected caller cancellation timed out.");
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
        Send(CookingNetworkMessageKind.Command, "out-of-order-reject", new CookingNetworkWireCommand(binding.ServerSessionInstance, binding.ConnectionGeneration, 100, "invalid-scope", new CookingLevelScope(Scope.MatchScope, Scope.RestaurantRuntime, Scope.Level, 2), Pickup(A)));
        session.ProcessOwnerFrame(); var view = session.LatestSessionProjection.Participants.Single(p => p.Participant == A);
        Assert.Equal(0, view.LastValidatedClientSequence); Assert.Equal(100, view.LastTerminalClientSequence);
        Send(CookingNetworkMessageKind.Command, "valid-low-sequence", new CookingNetworkWireCommand(binding.ServerSessionInstance, binding.ConnectionGeneration, 1, "valid-low", Scope, Pickup(A)));
        session.ProcessOwnerFrame(); view = session.LatestSessionProjection.Participants.Single(p => p.Participant == A);
        Assert.Equal(1, view.LastValidatedClientSequence); Assert.Equal(100, view.LastTerminalClientSequence); // Maximum reply sequence is not a contiguous admission/domain commit watermark.
    }
    [Fact]
    public async Task Session_projection_is_sorted_frozen_and_only_owner_commits_connection_and_sequence_changes()
    {
        using var host = Host(); using var session = Session(host); session.Start(); using var a = Local(session, A);
        var initial = session.LatestSessionProjection;
        var joining = a.ConnectAsync("inprocess", 1);
        Assert.Equal(initial, session.LatestSessionProjection); Assert.All(initial.Participants, p => Assert.False(p.ConnectedOwnerBinding));
        Pump(session, joining);
        Assert.Equal(new[] { A, Z }, session.LatestSessionProjection.Participants.Select(p => p.Participant));
        var active = session.LatestSessionProjection.Participants.Single(p => p.Participant == A);
        Assert.True(active.Ready); Assert.True(active.ConnectedOwnerBinding); Assert.Equal(0, active.LastValidatedClientSequence);
        var projectionBeforePacket = session.LatestSessionProjection;
        var pickup = a.SendCommandAsync("seq-first", Pickup(A));
        Assert.Equal(projectionBeforePacket, session.LatestSessionProjection);
        Pump(session, pickup); Assert.Equal(CookingRecipeOutcome.Accepted, (await pickup).Result!.Outcome);
        active = session.LatestSessionProjection.Participants.Single(p => p.Participant == A);
        Assert.Equal(1, active.LastValidatedClientSequence); Assert.Equal(1, active.LastTerminalClientSequence);
        var duplicate = a.SendCommandAsync("seq-first", Pickup(A)); Pump(session, duplicate);
        active = session.LatestSessionProjection.Participants.Single(p => p.Participant == A);
        Assert.Equal(2, active.LastValidatedClientSequence); Assert.Equal(2, active.LastTerminalClientSequence);
        Assert.True((await duplicate).Result!.IsDuplicate);
        Assert.Throws<NotSupportedException>(() => ((IList<CookingNetworkParticipantProjection>)a.LatestBaseline!.Session.Participants)[0] = active);
        var beforeClose = session.LatestSessionProjection; a.Disconnect();
        Assert.Equal(beforeClose, session.LatestSessionProjection); Assert.True(session.LatestSessionProjection.Participants.Single(p => p.Participant == A).ConnectedOwnerBinding);
        session.ProcessOwnerFrame(); Assert.False(session.LatestSessionProjection.Participants.Single(p => p.Participant == A).ConnectedOwnerBinding);
        Pump(session, a.ReconnectAsync("inprocess", 1)); active = session.LatestSessionProjection.Participants.Single(p => p.Participant == A);
        Assert.Equal(2, active.ConnectionGeneration); Assert.Equal(0, active.LastValidatedClientSequence); Assert.Equal(0, active.LastTerminalClientSequence);
        var afterRebind = a.SendCommandAsync("seq-first", Pickup(A)); Pump(session, afterRebind);
        Assert.Equal((await pickup).DomainCommandId, (await afterRebind).DomainCommandId); Assert.True((await afterRebind).Result!.IsDuplicate);
        Assert.Equal(1, session.LatestSessionProjection.Participants.Single(p => p.Participant == A).LastTerminalClientSequence);
    }
    [Fact]
    public void Session_projection_is_required_hash_covered_and_confers_no_authority_permissions()
    {
        using var host = Host(); using var session = Session(host); session.Start(); using var a = Local(session, A);
        Pump(session, a.ConnectAsync("inprocess", 1)); var baseline = a.LatestBaseline!;
        var bytes = CookingNetworkWireCodec.Encode(CookingNetworkMessageKind.Baseline, "b", baseline);
        var missing = System.Text.Json.Nodes.JsonNode.Parse(bytes)!;
        Assert.True(missing["payload"]!.AsObject().Remove("session"));
        Assert.True(CookingNetworkWireCodec.TryDecode(System.Text.Encoding.UTF8.GetBytes(missing.ToJsonString()), new(), out var envelope));
        Assert.Null(CookingNetworkWireCodec.Read<CookingNetworkBaseline>(envelope!));
        var participant = baseline.Session.Participants.Single(p => p.Participant == A);
        var tamperedSession = baseline.Session with { Participants = baseline.Session.Participants.Select(p => p.Participant == A ? p with { LastTerminalClientSequence = 999 } : p).ToArray() };
        Assert.NotEqual(baseline.Identity.StateHash, CookingNetworkWireCodec.BaselineHash(baseline.State, tamperedSession));
        var before = host.Observe().CanonicalText();
        Assert.False(a.TryInstallBaseline(baseline with { Identity = baseline.Identity with { SnapshotSequence = baseline.Identity.SnapshotSequence + 1 }, Session = tamperedSession }));
        Assert.Equal(baseline, a.LatestBaseline); Assert.False(a.IsSynchronized);
        Assert.Throws<InvalidOperationException>(() => { _ = a.SendCommandAsync("no-grant", Pickup(A)); });
        Assert.Equal(before, host.Observe().CanonicalText());
        var nestedMissing = System.Text.Json.Nodes.JsonNode.Parse(bytes)!;
        Assert.True(nestedMissing["payload"]!["session"]!["participants"]![0]!.AsObject().Remove("cleanupPending"));
        Assert.True(CookingNetworkWireCodec.TryDecode(System.Text.Encoding.UTF8.GetBytes(nestedMissing.ToJsonString()), new(), out envelope));
        Assert.Null(CookingNetworkWireCodec.Read<CookingNetworkBaseline>(envelope!));
    }
    [Fact]
    public void Real_trusted_global_major_progress_is_complete_frozen_display_without_spawning_future_unlock()
    {
        var progress = new CookingMajorProgress(); Assert.True(progress.EnableCookFaster().Accepted);
        Assert.True(progress.Unlock(new("future-global-material")).Accepted);
        Assert.True(progress.ChooseDecoration(new[] { new CookingStationReplacement(Stove, new("future-stove")) }).Accepted); progress.Lock();
        using var host = Host(progress); using var session = Session(host); session.Start(); using var a = Local(session, A);
        Pump(session, a.ConnectAsync("inprocess", 1)); var baseline = a.LatestBaseline!;
        Assert.True(baseline.State.MajorProgress!.Locked); Assert.True(baseline.State.MajorProgress.CookFaster);
        Assert.Equal("future-global-material", Assert.Single(baseline.State.MajorProgress.Unlocks).Value);
        Assert.Equal(Stove, Assert.Single(baseline.State.MajorProgress.Decoration).From);
        Assert.DoesNotContain(host.Observe().Items, i => i.DefinitionKey == "future-global-material");
        Assert.DoesNotContain(baseline.State.FullRecipe!.Items, i => i.Definition == new DefinitionId("future-global-material"));
        var bytes = CookingNetworkWireCodec.Encode(CookingNetworkMessageKind.Baseline, "major", baseline);
        Assert.True(CookingNetworkWireCodec.TryDecode(bytes, new(), out var envelope));
        var decoded = CookingNetworkWireCodec.Read<CookingNetworkBaseline>(envelope!)!;
        Assert.Equal(CookingNetworkWireCodec.BaselineHash(baseline.State, baseline.Session), CookingNetworkWireCodec.BaselineHash(decoded.State, decoded.Session));
        var decodedMajor = Assert.IsType<CookingNetworkMajorProgressProjection>(decoded.State.MajorProgress);
        Assert.Equal(baseline.State.MajorProgress.Locked, decodedMajor.Locked); Assert.Equal(baseline.State.MajorProgress.CookFaster, decodedMajor.CookFaster);
        Assert.Equal(baseline.State.MajorProgress.Decoration.ToArray(), decodedMajor.Decoration.ToArray()); Assert.Equal(baseline.State.MajorProgress.Unlocks.ToArray(), decodedMajor.Unlocks.ToArray());
        var missing = System.Text.Json.Nodes.JsonNode.Parse(bytes)!; missing["payload"]!["state"]!.AsObject().Remove("majorProgress");
        Assert.True(CookingNetworkWireCodec.TryDecode(System.Text.Encoding.UTF8.GetBytes(missing.ToJsonString()), new(), out envelope));
        Assert.Null(CookingNetworkWireCodec.Read<CookingNetworkBaseline>(envelope!));
    }
    [Fact]
    public void Bound_physical_connection_rejects_repeated_or_different_participant_join_without_ghost()
    {
        using var host = Host(); using var session = Session(host); session.Start();
        using var connection = new ConnectionManager(session.CreateLocalClientTransport, new ConnectionOptions { EnableReconnect = false });
        var packets = new List<CookingNetworkWireEnvelope>();
        connection.ServerPushReceived += (_, bytes) => { Assert.True(CookingNetworkWireCodec.TryDecode(bytes.AsSpan(), new(), out var e)); packets.Add(e!); };
        void Join(string correlation, PlayerId p, string credential) => connection.Send(CookingNetworkWireCodec.OpCode,
            new ArraySegment<byte>(CookingNetworkWireCodec.Encode(CookingNetworkMessageKind.Join, correlation, new CookingNetworkJoin(p, credential, null, null))), (ushort)NetworkPacketFlags.ServerPush);
        connection.Open("inprocess", 1); Join("join", A, "join-a"); session.ProcessOwnerFrame();
        var before = session.LatestSessionProjection;
        Join("repeat", A, "join-a"); Join("switch", Z, "join-z"); session.ProcessOwnerFrame();
        Assert.Single(packets, p => p.Kind == CookingNetworkMessageKind.Joined);
        foreach (var id in new[] { "repeat", "switch" }) Assert.Equal("AlreadyBound", CookingNetworkWireCodec.Read<CookingNetworkWireResult>(packets.Single(p => p.CorrelationId == id))!.Reason);
        Assert.Equal(before.Participants.ToArray(), session.LatestSessionProjection.Participants.ToArray());
        connection.Dispose(); session.ProcessOwnerFrame();
        Assert.All(session.LatestSessionProjection.Participants, p => Assert.False(p.ConnectedOwnerBinding));
    }
    [Fact]
    public void Rebind_rotates_token_and_old_token_cannot_supersede_the_current_generation()
    {
        using var host = Host(); using var session = Session(host); session.Start(); using var a = Local(session, A);
        Pump(session, a.ConnectAsync("inprocess", 1)); var old = a.RebindToken;
        Pump(session, a.ReconnectAsync("inprocess", 1)); Assert.NotEqual(old, a.RebindToken);
        using var stale = new ConnectionManager(session.CreateLocalClientTransport, new ConnectionOptions { EnableReconnect = false });
        var packets = new List<CookingNetworkWireEnvelope>();
        stale.ServerPushReceived += (_, bytes) => { Assert.True(CookingNetworkWireCodec.TryDecode(bytes.AsSpan(), new(), out var e)); packets.Add(e!); };
        stale.Open("inprocess", 1); stale.Send(CookingNetworkWireCodec.OpCode,
            new ArraySegment<byte>(CookingNetworkWireCodec.Encode(CookingNetworkMessageKind.Join, "stale", new CookingNetworkJoin(A, "join-a", a.ServerSessionInstance, old))), (ushort)NetworkPacketFlags.ServerPush);
        session.ProcessOwnerFrame();
        Assert.Equal("Unauthorized", CookingNetworkWireCodec.Read<CookingNetworkWireResult>(Assert.Single(packets, p => p.Kind == CookingNetworkMessageKind.Rejected))!.Reason);
        Assert.True(a.IsSynchronized); Assert.Equal(2, session.LatestSessionProjection.Participants.Single(p => p.Participant == A).ConnectionGeneration);
    }
    [Fact]
    public void Rehashed_wrong_match_runtime_level_epoch_or_nested_scope_never_replaces_passive_projection()
    {
        using var host = Host(); using var session = Session(host); session.Start(); using var a = Local(session, A);
        Pump(session, a.ConnectAsync("inprocess", 1)); var baseline = a.LatestBaseline!;
        var otherMatch = new CookingScope(new("other"), new("world"), new("match"));
        var variants = new[] {
            new CookingLevelScope(otherMatch, Scope.RestaurantRuntime, Scope.Level, 2),
            new CookingLevelScope(Scope.MatchScope, new(2), Scope.Level, 2),
            new CookingLevelScope(Scope.MatchScope, Scope.RestaurantRuntime, new("other-level"), 1)
        };
        foreach (var scope in variants) {
            var state = baseline.State with { Observation = baseline.State.Observation with {
                Scope = scope, Lifecycle = baseline.State.Observation.Lifecycle with { Scope = scope },
                Recipe = baseline.State.Observation.Recipe! with { Scope = scope.MatchScope } },
                FullRecipe = baseline.State.FullRecipe! with { Scope = scope.MatchScope, LevelScope = scope },
                ResumableCheckpoint = baseline.State.ResumableCheckpoint! with { Scope = scope,
                    Recipe = baseline.State.ResumableCheckpoint.Recipe with { Scope = scope.MatchScope, LevelScope = scope } } };
            var candidate = baseline with { State = state, Identity = baseline.Identity with {
                Scope = scope, Epoch = scope.LevelEpoch, SnapshotSequence = baseline.Identity.SnapshotSequence + 1,
                StateHash = CookingNetworkWireCodec.BaselineHash(state, baseline.Session) } };
            Assert.False(a.TryInstallBaseline(candidate)); Assert.Same(baseline, a.LatestBaseline); Assert.False(a.IsSynchronized);
        }
        var wrongNested = baseline.State with { FullRecipe = baseline.State.FullRecipe! with { Scope = otherMatch } };
        Assert.False(a.TryInstallBaseline(baseline with { State = wrongNested, Identity = baseline.Identity with {
            SnapshotSequence = baseline.Identity.SnapshotSequence + 1, StateHash = CookingNetworkWireCodec.BaselineHash(wrongNested, baseline.Session) } }));
        Assert.Same(baseline, a.LatestBaseline);
        var nextScope = new CookingLevelScope(Scope.MatchScope, Scope.RestaurantRuntime, Scope.Level, 2);
        var nextState = baseline.State with { Observation = baseline.State.Observation with { Scope = nextScope,
            Lifecycle = baseline.State.Observation.Lifecycle with { Scope = nextScope } },
            FullRecipe = baseline.State.FullRecipe! with { LevelScope = nextScope },
            ResumableCheckpoint = baseline.State.ResumableCheckpoint! with { Scope = nextScope,
                Recipe = baseline.State.ResumableCheckpoint.Recipe with { LevelScope = nextScope } } };
        var next = baseline with { State = nextState, Identity = baseline.Identity with { Scope = nextScope, Epoch = 2,
            SnapshotSequence = baseline.Identity.SnapshotSequence + 2, StateHash = CookingNetworkWireCodec.BaselineHash(nextState, baseline.Session) } };
        Assert.True(a.TryInstallBaseline(next)); var installed = a.LatestBaseline;
        Assert.False(a.TryInstallBaseline(baseline with { Identity = baseline.Identity with { SnapshotSequence = next.Identity.SnapshotSequence + 1 } }));
        Assert.Same(installed, a.LatestBaseline); Assert.False(a.IsSynchronized);
    }
    private sealed class ThrowingAllocator : ICookingProductIdAllocator
    {
        public ItemId GetProductId(long productSequence) => throw new IOException("real Session allocation fault");
    }
    [Fact]
    public async Task Actual_allocator_fault_revokes_existing_ready_rejects_new_join_and_retains_only_unsynchronized_display()
    {
        using var host = Host(allocator: new ThrowingAllocator()); using var session = Session(host); session.Start(); using var a = Local(session, A);
        Pump(session, a.ConnectAsync("inprocess", 1));
        var start = a.SendCommandAsync("start", new(Scope.MatchScope, 0, A, new("unused"), CookingRecipeOperation.StartProcess,
            Recipe: Recipe, Item: Food, Station: Stove, ExpectedItemVersion: 1)); Pump(session, start);
        Assert.Equal(CookingRecipeOutcome.Accepted, (await start).Result!.Outcome);
        for (var i = 0; i < 4 && !host.IsFaulted; i++) session.ProcessOwnerFrame();
        Assert.True(host.IsFaulted); Assert.False(a.IsSynchronized); Assert.NotNull(a.LatestBaseline);
        Assert.All(session.LatestSessionProjection.Participants, p => Assert.False(p.Ready));
        Assert.Throws<InvalidOperationException>(() => { _ = a.SendCommandAsync("after-fault", Pickup(A)); });
        using var z = Local(session, Z); var joining = z.ConnectAsync("inprocess", 1);
        for (var i = 0; i < 100 && !joining.IsCompleted; i++) { session.ProcessOwnerFrame(); Thread.Sleep(2); }
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => joining);
        Assert.Contains("AuthorityFaulted", error.Message); Assert.Null(z.LatestBaseline); Assert.False(z.IsSynchronized);
    }

    private static int TokenCount(byte[] bytes)
    {
        var reader = new System.Text.Json.Utf8JsonReader(bytes); var count = 0;
        while (reader.Read()) count++; return count;
    }
    [Fact]
    public void Actual_receipt_rich_full_baseline_exceeding_old_token_limit_roundtrips_under_explicit_baseline_bounds()
    {
        using var host = Host();
        for (var batch = 1; batch <= 16; batch++) {
            for (var i = 0; i < 100; i++) Assert.True(host.TryEnqueue(new(Scope,
                Pickup(A) with { Command = new("rich-" + batch + "-" + i.ToString("D4")), SimulationBatch = batch,
                    Operation = i % 2 == 0 ? CookingRecipeOperation.Pickup : CookingRecipeOperation.Drop,
                    Station = i % 2 == 0 ? null : Stove, ExpectedItemVersion = (batch - 1) * 100 + i + 1 }, "trusted-local", "c-" + batch + "-" + i)).Accepted);
            Assert.All(host.Tick().Dispositions, d => Assert.Equal(CookingRecipeOutcome.Accepted, d.Result!.Outcome));
        }
        var state = new CookingNetworkAuthorityAdapter(host).CaptureFullState().State!;
        var view = new CookingNetworkSessionProjection("instance", Array.AsReadOnly(new[] { new CookingNetworkParticipantProjection(A, true, 1, 0, 0, false, false) }));
        var baseline = new CookingNetworkBaseline(new("instance", A, 1, Scope, 1, 1, CookingNetworkWireCodec.BaselineHash(state, view), "issued"), 8, 5, state, view);
        var bytes = CookingNetworkWireCodec.Encode(CookingNetworkMessageKind.Baseline, "baseline", baseline);
        Assert.True(TokenCount(bytes) > 65536, "Actual tokens: " + TokenCount(bytes) + "; receipts: " + state.FullRecipe!.Deduplication.Count); Assert.True(bytes.Length < 8 * 1024 * 1024);
        Assert.False(CookingNetworkWireCodec.TryDecode(bytes, new(BaselineTokenLimit: 65536), out _));
        Assert.True(CookingNetworkWireCodec.TryDecode(bytes, new(), out var envelope));
        var decoded = CookingNetworkWireCodec.Read<CookingNetworkBaseline>(envelope!)!;
        Assert.Equal(state.FullRecipe!.CanonicalText(), decoded.State.FullRecipe!.CanonicalText());
        Assert.Equal(state.ResumableCheckpoint!.CanonicalText(), decoded.State.ResumableCheckpoint!.CanonicalText());
        // Diagnostic only: no timing assertion or acceptance threshold. Same actual rich image in both source runs.
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 5; i++) Assert.NotNull(CookingNetworkWireCodec.Read<CookingNetworkBaseline>(envelope!));
        watch.Stop(); var readMilliseconds = watch.Elapsed.TotalMilliseconds;
        watch.Restart(); for (var i = 0; i < 5; i++) _ = CookingNetworkWireCodec.Freeze(decoded); watch.Stop();
        _output.WriteLine("Rich baseline diagnostics: bytes={0}; tokens={1}; repeatedReads=5; readTotalMs={2:F3}; freezes=5; freezeTotalMs={3:F3}",
            bytes.Length, TokenCount(bytes), readMilliseconds, watch.Elapsed.TotalMilliseconds);
    }
    [Fact]
    public async Task Actual_outbound_full_state_excess_is_explicit_before_issued_ack_or_new_join_permission()
    {
        using var host = Host(); using var session = new CookingNetworkSessionHost(new CookingNetworkAuthorityAdapter(host),
            new InProcessChannelListener(), new Dictionary<PlayerId, string> { [A] = "join-a", [Z] = "join-z" }, new(BaselineTokenLimit: 1));
        session.Start(); using var a = Local(session, A); var first = a.ConnectAsync("inprocess", 1);
        for (var i = 0; i < 100 && !first.IsCompleted; i++) { session.ProcessOwnerFrame(); Thread.Sleep(2); }
        Assert.Contains("FullStateExceedsWireBounds", (await Assert.ThrowsAsync<InvalidOperationException>(() => first)).Message);
        Assert.Null(a.LatestBaseline); Assert.False(a.IsSynchronized);
        Assert.All(session.LatestSessionProjection.Participants, p => Assert.False(p.Ready));
        using var z = Local(session, Z); var next = z.ConnectAsync("inprocess", 1);
        for (var i = 0; i < 100 && !next.IsCompleted; i++) { session.ProcessOwnerFrame(); Thread.Sleep(2); }
        Assert.Contains("FullStateExceedsWireBounds", (await Assert.ThrowsAsync<InvalidOperationException>(() => next)).Message);
        Assert.Null(z.LatestBaseline); Assert.False(z.IsSynchronized); Assert.False(host.IsFaulted);
    }

    [Fact]
    public async Task Growth_past_injected_full_state_bound_unsynchronizes_ready_client_without_truncating_receipts()
    {
        using var host = Host(); var state = new CookingNetworkAuthorityAdapter(host).CaptureFullState().State!;
        var view = new CookingNetworkSessionProjection("measurement", Array.AsReadOnly(new[] {
            new CookingNetworkParticipantProjection(A, true, 1, 0, 0, false, false),
            new CookingNetworkParticipantProjection(Z, false, 0, 0, 0, false, false) }));
        var initial = new CookingNetworkBaseline(new("measurement", A, 1, Scope, 1, 1, CookingNetworkWireCodec.BaselineHash(state, view), "i"), 8, 5, state, view);
        var limit = TokenCount(CookingNetworkWireCodec.Encode(CookingNetworkMessageKind.Baseline, "b", initial)) + 1000;
        using var session = new CookingNetworkSessionHost(new CookingNetworkAuthorityAdapter(host), new InProcessChannelListener(),
            new Dictionary<PlayerId, string> { [A] = "join-a", [Z] = "join-z" }, new(BaselineTokenLimit: limit));
        session.Start(); using var a = Local(session, A); Pump(session, a.ConnectAsync("inprocess", 1)); Assert.True(a.IsSynchronized);
        for (var i = 0; i < 16 && a.IsSynchronized; i++) {
            var command = Pickup(A) with { Operation = i % 2 == 0 ? CookingRecipeOperation.Pickup : CookingRecipeOperation.Drop,
                Station = i % 2 == 0 ? null : Stove, ExpectedItemVersion = i + 1 };
            var result = a.SendCommandAsync("growth-" + i, command); Pump(session, result);
            Assert.Equal(CookingRecipeOutcome.Accepted, (await result).Result!.Outcome);
        }
        Assert.False(a.IsSynchronized); Assert.NotNull(a.LatestBaseline); Assert.False(host.IsFaulted);
        Assert.NotEmpty(session.LatestCapture!.FullRecipe!.Deduplication); // Nothing is removed to fit the wire.
        var before = host.Observe().CanonicalText(); session.ProcessOwnerFrame(); Assert.Equal(before, host.Observe().CanonicalText());
        using var z = Local(session, Z); var joining = z.ConnectAsync("inprocess", 1);
        for (var i = 0; i < 100 && !joining.IsCompleted; i++) { session.ProcessOwnerFrame(); Thread.Sleep(2); }
        Assert.Contains("FullStateExceedsWireBounds", (await Assert.ThrowsAsync<InvalidOperationException>(() => joining)).Message);
    }

    [Fact]
    public void Ready_connection_has_one_awaiting_ack_full_baseline_while_commands_reply_and_next_ack_publishes_latest()
    {
        using var host = Host(); using var session = Session(host); session.Start();
        using var connection = new ConnectionManager(session.CreateLocalClientTransport, new ConnectionOptions { EnableReconnect = false });
        var packets = new List<CookingNetworkWireEnvelope>();
        connection.ServerPushReceived += (_, bytes) => { Assert.True(CookingNetworkWireCodec.TryDecode(bytes.AsSpan(), new(), out var e)); packets.Add(e!); };
        void Send<T>(CookingNetworkMessageKind kind, string correlation, T payload) => connection.Send(CookingNetworkWireCodec.OpCode,
            new ArraySegment<byte>(CookingNetworkWireCodec.Encode(kind, correlation, payload)), (ushort)NetworkPacketFlags.ServerPush);
        CookingNetworkBaseline[] Baselines() => packets.Where(p => p.Kind == CookingNetworkMessageKind.Baseline)
            .Select(p => CookingNetworkWireCodec.Read<CookingNetworkBaseline>(p)!).ToArray();
        connection.Open("inprocess", 1); Send(CookingNetworkMessageKind.Join, "join", new CookingNetworkJoin(A, "join-a", null, null));
        session.ProcessOwnerFrame(); var first = Assert.Single(Baselines());
        var binding = CookingNetworkWireCodec.Read<CookingNetworkJoined>(Assert.Single(packets, p => p.Kind == CookingNetworkMessageKind.Joined))!;
        Send(CookingNetworkMessageKind.BaselineAck, "initial-ack", first.Identity); session.ProcessOwnerFrame();
        Assert.Equal(2, Baselines().Length); var pending = Baselines()[1];
        Assert.True(session.LatestSessionProjection.Participants.Single(p => p.Participant == A).Ready);
        for (var i = 0; i < 12; i++) session.ProcessOwnerFrame();
        Assert.Equal(2, Baselines().Length); // Drop/delay the second actual ACK; owner continues without more full frames.
        Send(CookingNetworkMessageKind.Command, "during-backpressure", new CookingNetworkWireCommand(binding.ServerSessionInstance,
            binding.ConnectionGeneration, 1, "pickup-during-backpressure", Scope, Pickup(A)));
        session.ProcessOwnerFrame();
        Assert.Equal(CookingRecipeOutcome.Accepted, CookingNetworkWireCodec.Read<CookingNetworkWireResult>(
            Assert.Single(packets, p => p.CorrelationId == "during-backpressure"))!.Result!.Outcome);
        Assert.Equal(2, Baselines().Length);
        Send(CookingNetworkMessageKind.BaselineAck, "stale-ack", first.Identity); session.ProcessOwnerFrame();
        Assert.Equal(2, Baselines().Length); Assert.Equal("BaselineRequired", CookingNetworkWireCodec.Read<CookingNetworkWireResult>(
            Assert.Single(packets, p => p.CorrelationId == "stale-ack"))!.Reason);
        Send(CookingNetworkMessageKind.BaselineAck, "release-pending", pending.Identity); session.ProcessOwnerFrame();
        Assert.Equal(3, Baselines().Length); var latest = Baselines()[2];
        Assert.Equal(pending.Identity.SnapshotSequence + 1, latest.Identity.SnapshotSequence);
        Assert.Equal(session.LatestCapture!.Observation.CanonicalText(), latest.State.Observation.CanonicalText());
        Assert.Equal(session.LatestCapture.FullRecipe!.CanonicalText(), latest.State.FullRecipe!.CanonicalText());
        Assert.Equal(A.Value, latest.State.Observation.Items.Single(i => i.Id == Food).Location.OwnerId);
        Send(CookingNetworkMessageKind.BaselineAck, "duplicate-ack", pending.Identity); session.ProcessOwnerFrame();
        Assert.Equal(3, Baselines().Length); // Duplicate old ACK cannot release the new issued snapshot.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Created_readonly_join_disconnect_rebind_finishes_without_clock_advance_or_carried_hand_loss(bool successor)
    {
        using var host = Host(initialCreated: !successor);
        if (successor) {
            Assert.True(host.TryEnqueue(new(Scope, Pickup(A) with { Command = new("carry-pickup"), SimulationBatch = 1 }, "trusted-local", "carry-pickup")).Accepted);
            Assert.Equal(CookingRecipeOutcome.Accepted, Assert.Single(host.Tick().Dispositions).Result!.Outcome);
            Assert.True(host.BeginEnd(CookingLevelOutcome.Success).Accepted); Assert.True(host.CompleteEnd().Accepted);
            var next = new LevelId("successor");
            Assert.True(host.CreateSuccessor(next, 2).Accepted);
        }
        Assert.Equal(CookingLevelState.Created, host.Observe().Lifecycle.State);
        var before = host.Observe().CanonicalText(); var frame = host.HostFrameSequence;
        using var session = Session(host); session.Start(); using var a = Local(session, A);
        var capture = session.LatestCapture!; var recipe = capture.FullRecipe?.CanonicalText();
        Assert.Equal(!successor, capture.FullRecipe is null);
        Pump(session, a.ConnectAsync("inprocess", 1)); Assert.True(a.IsSynchronized);
        var denied = a.SendCommandAsync("created-no-business", Pickup(A) with { Scope = host.Binding.LevelScope.MatchScope });
        Pump(session, denied); var rejected = await denied;
        Assert.Equal("LevelNotRunning", rejected.Reason); Assert.Null(rejected.Result);
        Assert.Equal(before, host.Observe().CanonicalText());
        a.Disconnect(); session.ProcessOwnerFrame();
        var rebound = a.ReconnectAsync("inprocess", 1);
        for (var i = 0; i < 100 && !rebound.IsCompleted; i++) { session.ProcessOwnerFrame(); Thread.Sleep(2); }
        Assert.True(rebound.IsCompleted, "Created rebind remained blocked by cleanup although no live work exists."); await rebound;
        Assert.True(a.IsSynchronized); Assert.Equal(2, session.LatestSessionProjection.Participants.Single(p => p.Participant == A).ConnectionGeneration);
        Assert.False(session.LatestSessionProjection.Participants.Single(p => p.Participant == A).CleanupPending);
        Assert.Equal(frame, host.HostFrameSequence); Assert.Equal(before, host.Observe().CanonicalText());
        Assert.Equal(recipe, session.LatestCapture!.FullRecipe?.CanonicalText());
        if (successor) Assert.Equal(A.Value, a.LatestBaseline!.State.FullRecipe!.Items.Single(i => i.Id == Food).Location.OwnerId);
        else Assert.Null(a.LatestBaseline!.State.FullRecipe);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void First_join_rejects_supplied_old_identity_or_token_without_binding_then_accepts_only_null_pair(int combination)
    {
        string oldInstance, oldToken;
        using (var oldHost = Host(initialCreated: true))
        using (var oldSession = Session(oldHost))
        using (var oldClient = Local(oldSession, A)) {
            oldSession.Start(); Pump(oldSession, oldClient.ConnectAsync("inprocess", 1));
            oldInstance = oldClient.ServerSessionInstance!; oldToken = oldClient.RebindToken!;
        }
        // A genuine new Host and Session after disposal; no durable-store recovery claim is made here.
        using var host = Host(initialCreated: true); using var session = Session(host); session.Start();
        Assert.NotEqual(oldInstance, session.ServerSessionInstance);
        using var connection = new ConnectionManager(session.CreateLocalClientTransport, new ConnectionOptions { EnableReconnect = false });
        var packets = new List<CookingNetworkWireEnvelope>();
        connection.ServerPushReceived += (_, bytes) => { Assert.True(CookingNetworkWireCodec.TryDecode(bytes.AsSpan(), new(), out var e)); packets.Add(e!); };
        void Send<T>(CookingNetworkMessageKind kind, string correlation, T payload) => connection.Send(CookingNetworkWireCodec.OpCode,
            new ArraySegment<byte>(CookingNetworkWireCodec.Encode(kind, correlation, payload)), (ushort)NetworkPacketFlags.ServerPush);
        var instance = combination switch { 0 or 2 => oldInstance, 1 => session.ServerSessionInstance, _ => null };
        var token = combination == 2 ? null : oldToken;
        var before = host.Observe().CanonicalText(); var projection = session.LatestSessionProjection;
        connection.Open("inprocess", 1); Send(CookingNetworkMessageKind.Join, "old-identity", new CookingNetworkJoin(A, "join-a", instance, token));
        session.ProcessOwnerFrame();
        Assert.DoesNotContain(packets, p => p.Kind is CookingNetworkMessageKind.Joined or CookingNetworkMessageKind.Baseline or CookingNetworkMessageKind.Ready);
        Assert.Equal("Unauthorized", CookingNetworkWireCodec.Read<CookingNetworkWireResult>(Assert.Single(packets, p => p.CorrelationId == "old-identity"))!.Reason);
        Assert.Equal(projection.Participants.ToArray(), session.LatestSessionProjection.Participants.ToArray());
        Assert.All(session.LatestSessionProjection.Participants, p => { Assert.Equal(0, p.ConnectionGeneration); Assert.False(p.ConnectedOwnerBinding); Assert.False(p.Ready); });
        Assert.Equal(before, host.Observe().CanonicalText()); Assert.Equal(0, host.HostFrameSequence);
        Send(CookingNetworkMessageKind.Join, "fresh", new CookingNetworkJoin(A, "join-a", null, null)); session.ProcessOwnerFrame();
        var binding = CookingNetworkWireCodec.Read<CookingNetworkJoined>(Assert.Single(packets, p => p.Kind == CookingNetworkMessageKind.Joined))!;
        Assert.Equal(1, binding.ConnectionGeneration); Assert.Equal(session.ServerSessionInstance, binding.ServerSessionInstance); Assert.NotEqual(oldToken, binding.RebindToken);
        var issued = CookingNetworkWireCodec.Read<CookingNetworkBaseline>(Assert.Single(packets, p => p.Kind == CookingNetworkMessageKind.Baseline))!;
        Send(CookingNetworkMessageKind.BaselineAck, "fresh-ack", issued.Identity); session.ProcessOwnerFrame();
        Assert.Contains(packets, p => p.Kind == CookingNetworkMessageKind.Ready);
        Assert.Equal(before, host.Observe().CanonicalText()); Assert.Equal(0, host.HostFrameSequence);
    }

}
