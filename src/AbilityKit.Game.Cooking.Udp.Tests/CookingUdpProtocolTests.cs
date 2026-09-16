using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.Udp;
using Xunit;

namespace AbilityKit.Game.Cooking.Udp.Tests;

[Trait("Gate", "CookingUdp")]
public sealed class CookingUdpProtocolTests
{
    [Fact]
    public void Codec_round_trips_a_command_envelope_and_rejects_invalid_datagrams()
    {
        var descriptor = Fixture.CreateAuthority().Descriptor;
        var command = Fixture.Command(Fixture.PlayerTwo, "codec-command");
        var bytes = CookingUdpCodec.Encode(CookingUdpMessageKind.Command, descriptor, "codec-1",
            new CookingUdpCommandMessage(command));

        Assert.True(CookingUdpCodec.TryDecode(bytes, out var envelope, out var reason), reason);
        Assert.Equal(CookingUdpMessageKind.Command, envelope!.Kind);
        Assert.Equal(descriptor.Scope, envelope.Scope.ToDomain());
        Assert.True(CookingUdpCodec.TryReadPayload<CookingUdpCommandMessage>(envelope, out var payload, out reason), reason);
        Assert.Equal(command, payload!.Command);

        Assert.False(CookingUdpCodec.TryDecode("not-json"u8, out _, out reason));
        Assert.Equal("json-invalid", reason);
        Assert.False(CookingUdpCodec.TryDecode(new byte[CookingUdpCodec.MaximumDatagramBytes + 1], out _, out reason));
        Assert.Equal("datagram-size", reason);
    }

    [Fact]
    public void Projection_requires_full_hash_validated_baseline_and_continuous_deltas()
    {
        using var authority = Fixture.CreateAuthority();
        var projection = new CookingUdpClientProjection(authority.Descriptor);
        var baselineSnapshot = authority.Snapshot();
        var baseline = new CookingUdpSnapshotMessage(1, 1, baselineSnapshot.Sha256(), baselineSnapshot);

        Assert.True(projection.InstallBaseline(baseline).Accepted);
        var invalidHash = projection.ApplyDelta(new CookingUdpSnapshotMessage(2, 1, "forged", baselineSnapshot));
        Assert.Equal(CookingSessionReason.AuthoritySnapshotMismatch, invalidHash.Reason);
        Assert.Equal(CookingSynchronizationState.Unsynchronized, projection.State);

        var invalidBaselineReference = projection.InstallBaseline(baseline with { BaselineReference = 0 });
        Assert.Equal(CookingSessionReason.BaselineReferenceMismatch, invalidBaselineReference.Reason);
        Assert.True(projection.InstallBaseline(baseline with { Sequence = 10, BaselineReference = 10 }).Accepted);
        var gap = projection.ApplyDelta(baseline with { Sequence = 12, BaselineReference = 10 });
        Assert.Equal(CookingSessionReason.SnapshotSequenceGap, gap.Reason);
    }

    [Fact]
    public async Task Loopback_host_local_and_remote_udp_share_authority_and_converge_snapshot()
    {
        var simulation = Fixture.CreateSimulation();
        using var authority = Fixture.CreateAuthority(simulation);
        var port = Fixture.ReserveUdpPort();
        await using var host = new CookingUdpHost(authority, new CookingUdpHostOptions(CookingUdpHostOptions.DefaultConnectionKey,
            port, Fixture.PlayerOne, _ => Fixture.PlayerTwo));
        await host.StartAsync();
        await using var client = new CookingUdpClient(authority.Descriptor,
            new CookingUdpClientOptions(CookingUdpHostOptions.DefaultConnectionKey, "127.0.0.1", host.BoundPort));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await client.ConnectAndHandshakeAsync(timeout.Token);
        Assert.Equal(Fixture.PlayerTwo.Value, client.AssignedPlayerId);
        Assert.Equal(CookingSynchronizationState.Synchronized, client.Projection.State);

        var local = await host.SubmitHostLocalAsync(Fixture.Command(Fixture.PlayerOne, "host-pickup"), "host-pickup", timeout.Token);
        Assert.Equal(CommandOutcome.Accepted, local.AuthorityResult?.Outcome);
        var remote = await client.SendCommandAsync(Fixture.Command(Fixture.PlayerTwo, "remote-loses-race"), timeout.Token);
        Assert.Equal(CookingSessionCommandDisposition.Executed, remote.Disposition);
        var delta = await client.WaitForDeltaAsync(timeout.Token);

        Assert.True(delta.Accepted);
        Assert.Equal(authority.Snapshot().Sha256(), client.Projection.Snapshot?.Sha256());
        Assert.True(host.AuthorityDispatcherOnly);
        Assert.True(host.CallbackInvocationCount > 0);
    }

    [Fact]
    public async Task Loopback_late_join_client_uses_its_own_baseline_reference_for_future_delta()
    {
        var simulation = Fixture.CreateSimulation();
        using var authority = Fixture.CreateAuthority(simulation);
        var port = Fixture.ReserveUdpPort();
        await using var host = new CookingUdpHost(authority, new CookingUdpHostOptions(CookingUdpHostOptions.DefaultConnectionKey,
            port, Fixture.PlayerOne, _ => Fixture.PlayerTwo));
        await host.StartAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var pickup = await host.SubmitHostLocalAsync(Fixture.Command(Fixture.PlayerOne, "before-late-join-pickup"), "before-late-join-pickup", timeout.Token);
        Assert.Equal(CommandOutcome.Accepted, pickup.AuthorityResult?.Outcome);
        var drop = await host.SubmitHostLocalAsync(Fixture.Command(Fixture.PlayerOne, "before-late-join-drop", CookingOperation.Drop, 2, Fixture.Station), "before-late-join-drop", timeout.Token);
        Assert.Equal(CommandOutcome.Accepted, drop.AuthorityResult?.Outcome);
        await using var client = new CookingUdpClient(authority.Descriptor,
            new CookingUdpClientOptions(CookingUdpHostOptions.DefaultConnectionKey, "127.0.0.1", host.BoundPort));
        await client.ConnectAndHandshakeAsync(timeout.Token);

        var duplicate = await client.SendCommandAsync(Fixture.Command(Fixture.PlayerTwo, "late-join-command", expectedItemVersion: 3), timeout.Token);
        Assert.Equal(CookingSessionCommandDisposition.Executed, duplicate.Disposition);
        var delta = await client.WaitForDeltaAsync(timeout.Token);

        Assert.True(delta.Accepted);
        Assert.Equal(authority.Snapshot().Sha256(), client.Projection.Snapshot?.Sha256());
    }

    [Fact]
    public async Task Loopback_host_local_broadcast_completes_before_following_remote_command_is_dispatched()
    {
        var simulation = Fixture.CreateSimulation();
        using var authority = Fixture.CreateAuthority(simulation);
        var port = Fixture.ReserveUdpPort();
        await using var host = new CookingUdpHost(authority, new CookingUdpHostOptions(CookingUdpHostOptions.DefaultConnectionKey,
            port, Fixture.PlayerOne, _ => Fixture.PlayerTwo));
        await host.StartAsync();
        await using var client = new CookingUdpClient(authority.Descriptor,
            new CookingUdpClientOptions(CookingUdpHostOptions.DefaultConnectionKey, "127.0.0.1", host.BoundPort));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await client.ConnectAndHandshakeAsync(timeout.Token);

        var local = await host.SubmitHostLocalAsync(Fixture.Command(Fixture.PlayerOne, "local-before-remote"), "local-before-remote", timeout.Token);
        Assert.Equal(CommandOutcome.Accepted, local.AuthorityResult?.Outcome);
        var remote = await client.SendCommandAsync(Fixture.Command(Fixture.PlayerTwo, "remote-after-local"), timeout.Token);
        Assert.Equal(CookingSessionCommandDisposition.Executed, remote.Disposition);
        var firstDelta = await client.WaitForDeltaAsync(timeout.Token);

        Assert.True(firstDelta.Accepted);
        Assert.Equal(authority.Snapshot().Sha256(), client.Projection.Snapshot?.Sha256());
    }

    [Fact]
    public async Task Loopback_client_correlates_multiple_command_results_and_consumes_multiple_deltas()
    {
        var simulation = Fixture.CreateSimulation();
        using var authority = Fixture.CreateAuthority(simulation);
        var port = Fixture.ReserveUdpPort();
        await using var host = new CookingUdpHost(authority, new CookingUdpHostOptions(CookingUdpHostOptions.DefaultConnectionKey,
            port, Fixture.PlayerOne, _ => Fixture.PlayerTwo));
        await host.StartAsync();
        await using var client = new CookingUdpClient(authority.Descriptor,
            new CookingUdpClientOptions(CookingUdpHostOptions.DefaultConnectionKey, "127.0.0.1", host.BoundPort));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await client.ConnectAndHandshakeAsync(timeout.Token);

        var pickup = await client.SendCommandAsync(Fixture.Command(Fixture.PlayerTwo, "remote-pickup"), timeout.Token);
        Assert.Equal(CommandOutcome.Accepted, pickup.AuthorityResult?.Outcome);
        var pickupDelta = await client.WaitForDeltaAsync(timeout.Token);
        Assert.True(pickupDelta.Accepted);

        var drop = await client.SendCommandAsync(Fixture.Command(Fixture.PlayerTwo, "remote-drop", CookingOperation.Drop, 2, Fixture.Station), timeout.Token);
        Assert.Equal(CommandOutcome.Accepted, drop.AuthorityResult?.Outcome);
        var dropDelta = await client.WaitForDeltaAsync(timeout.Token);

        Assert.True(dropDelta.Accepted);
        Assert.Equal(3, client.Projection.Sequence);
        Assert.Equal(authority.Snapshot().Sha256(), client.Projection.Snapshot?.Sha256());
    }

    [Fact]
    public async Task Loopback_wrong_connection_key_does_not_create_remote_binding()
    {
        using var authority = Fixture.CreateAuthority();
        var port = Fixture.ReserveUdpPort();
        await using var host = new CookingUdpHost(authority, new CookingUdpHostOptions(CookingUdpHostOptions.DefaultConnectionKey,
            port, Fixture.PlayerOne, _ => Fixture.PlayerTwo));
        await host.StartAsync();
        await using var client = new CookingUdpClient(authority.Descriptor,
            new CookingUdpClientOptions("wrong-key", "127.0.0.1", host.BoundPort));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(750));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ConnectAndHandshakeAsync(timeout.Token));
        Assert.DoesNotContain(authority.Diagnostics, item => item.EventType == "handshake-accepted" && item.ConnectionId != "host-local");
    }

    private static class Fixture
    {
        internal static readonly SessionId Session = new("udp-session");
        internal static readonly WorldId World = new("udp-world");
        internal static readonly MatchId Match = new("udp-match");
        internal static readonly PlayerId PlayerOne = new("udp-player-one");
        internal static readonly PlayerId PlayerTwo = new("udp-player-two");
        private static readonly ItemId Item = new("udp-item");
        private static readonly DefinitionId Definition = new("udp-ingredient");
        internal static readonly StationSlotId Station = new("udp-station");

        internal static CookingSessionAuthority CreateAuthority(CookingSimulation? simulation = null) => new(simulation ?? CreateSimulation(),
            new CookingSessionDescriptor(new CookingScope(Session, World, Match), 1,
                new CookingProtocolIdentity("cooking-session", 1, 1), "udp-config",
                new HashSet<string>(StringComparer.Ordinal) { "cook" }, new Dictionary<string, string>()), 8);

        internal static CookingSimulation CreateSimulation()
        {
            var scope = new CookingScope(Session, World, Match);
            var players = new Dictionary<PlayerId, CookingPlayerConfig>
            {
                [PlayerOne] = new(PlayerOne, new HashSet<string>(StringComparer.Ordinal) { "cook" }, new HashSet<string>(StringComparer.Ordinal) { Station.Value }),
                [PlayerTwo] = new(PlayerTwo, new HashSet<string>(StringComparer.Ordinal) { "cook" }, new HashSet<string>(StringComparer.Ordinal) { Station.Value }),
            };
            var simulation = new CookingSimulation(new CookingFixture(scope, players,
                new Dictionary<StationSlotId, CookingStationConfig> { [Station] = new(Station, 2) },
                new Dictionary<DefinitionId, CookingItemDefinition> { [Definition] = new(Definition, new HashSet<string>(StringComparer.Ordinal) { "cook" }) }));
            simulation.AddItem(Item, Definition, ItemLocation.World("udp-counter"));
            return simulation;
        }

        internal static CookingCommand Command(PlayerId player, string id, CookingOperation operation = CookingOperation.Pickup,
            int expectedItemVersion = 1, StationSlotId? station = null) => new(new CookingScope(Session, World, Match), 10,
            player, new CommandId(id), operation, Item, expectedItemVersion, station);

        internal static int ReserveUdpPort()
        {
            using var socket = new System.Net.Sockets.UdpClient(0);
            return ((System.Net.IPEndPoint)socket.Client.LocalEndPoint!).Port;
        }
    }
}
