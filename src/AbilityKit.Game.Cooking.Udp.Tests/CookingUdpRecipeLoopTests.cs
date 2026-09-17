using System.Text;
using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.Udp;
using Xunit;

namespace AbilityKit.Game.Cooking.Udp.Tests;

[Trait("Gate", "CookingUdp")]
public sealed class CookingUdpRecipeLoopTests
{
    [Fact]
    public async Task Loopback_host_and_remote_execute_the_collaborative_recipe_fixture_through_typed_udp_commands()
    {
        var simulation = Fixture.CreateSimulation();
        var descriptor = Fixture.CreateDescriptor();
        var port = Fixture.ReserveUdpPort();
        await using var host = new CookingUdpRecipeHost(simulation, descriptor,
            new CookingUdpRecipeHostOptions(CookingUdpRecipeHostOptions.DefaultConnectionKey, port, Fixture.Host, _ => Fixture.Remote));
        await host.StartAsync();
        await using var client = new CookingUdpRecipeClient(descriptor,
            new CookingUdpClientOptions(CookingUdpRecipeHostOptions.DefaultConnectionKey, "127.0.0.1", host.BoundPort));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        foreach (var command in Fixture.HostCommands())
        {
            var result = await host.SubmitHostLocalAsync(command, command.Command.Value, timeout.Token);
            Assert.Equal(CookingRecipeOutcome.Accepted, result.Outcome);
        }

        await client.ConnectAndHandshakeAsync(timeout.Token);
        Assert.Equal(Fixture.Remote.Value, client.AssignedPlayerId);
        var baselineHash = client.Projection.RecipeSnapshot!.Sha256();
        var plate = await client.SendCommandAsync(Fixture.Plate(), timeout.Token);
        Assert.Equal(CookingRecipeOutcome.Accepted, plate.Outcome);
        Assert.True((await client.WaitForDeltaAsync(timeout.Token)).Accepted);
        var rejected = await client.SendCommandAsync(Fixture.Submit("reject", Fixture.RejectedOrder), timeout.Token);
        Assert.Equal(CookingRecipeOutcome.Rejected, rejected.Outcome);
        Assert.Equal(CookingRecipeRejectionReason.OrderRejected, rejected.Reason);
        Assert.Equal(client.Projection.RecipeSnapshot!.Sha256(), host.Snapshot.Sha256());
        var submit = await client.SendCommandAsync(Fixture.Submit("submit", Fixture.Order), timeout.Token);
        Assert.Equal(CookingRecipeOutcome.Accepted, submit.Outcome);
        Assert.True((await client.WaitForDeltaAsync(timeout.Token)).Accepted);
        var duplicate = await client.SendCommandAsync(Fixture.Submit("submit", Fixture.Order), timeout.Token);
        Assert.True(duplicate.IsDuplicate);
        Assert.Single(host.Snapshot.AcceptedOrders);
        Assert.Equal(host.Snapshot.Sha256(), client.Projection.RecipeSnapshot!.Sha256());
        Assert.NotEqual(baselineHash, host.Snapshot.Sha256());
        Assert.True(host.AuthorityDispatcherOnly);
        Assert.True(host.CallbackInvocationCount > 0);
    }

    [Fact]
    public void Recipe_projection_rejects_forged_hash_and_noncontinuous_delta()
    {
        var simulation = Fixture.CreateSimulation();
        var descriptor = Fixture.CreateDescriptor();
        var projection = new CookingUdpClientProjection(descriptor);
        var snapshot = simulation.Snapshot();
        Assert.True(projection.InstallRecipeBaseline(new CookingUdpRecipeSnapshotMessage(1, 1, snapshot.Sha256(), snapshot)).Accepted);
        Assert.Equal(CookingSessionReason.AuthoritySnapshotMismatch,
            projection.ApplyRecipeDelta(new CookingUdpRecipeSnapshotMessage(2, 1, "forged", snapshot)).Reason);
        Assert.True(projection.InstallRecipeBaseline(new CookingUdpRecipeSnapshotMessage(10, 10, snapshot.Sha256(), snapshot)).Accepted);
        Assert.Equal(CookingSessionReason.SnapshotSequenceGap,
            projection.ApplyRecipeDelta(new CookingUdpRecipeSnapshotMessage(12, 10, snapshot.Sha256(), snapshot)).Reason);
    }

    [Fact]
    public async Task Loopback_malformed_deserialized_recipe_command_returns_correlated_rejection_without_mutation()
    {
        var simulation = Fixture.CreateSimulation();
        var descriptor = Fixture.CreateDescriptor();
        var port = Fixture.ReserveUdpPort();
        await using var host = new CookingUdpRecipeHost(simulation, descriptor,
            new CookingUdpRecipeHostOptions(CookingUdpRecipeHostOptions.DefaultConnectionKey, port, Fixture.Host, _ => Fixture.Remote));
        await host.StartAsync();
        await using var client = new CookingUdpRecipeClient(descriptor,
            new CookingUdpClientOptions(CookingUdpRecipeHostOptions.DefaultConnectionKey, "127.0.0.1", host.BoundPort));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await client.ConnectAndHandshakeAsync(timeout.Token);
        var before = host.Snapshot.CanonicalText();

        var result = await client.SendCommandAsync(Fixture.Plate() with { Command = default, Operation = default }, timeout.Token);

        Assert.Equal(CookingRecipeOutcome.Rejected, result.Outcome);
        Assert.Equal(CookingRecipeRejectionReason.MalformedCommand, result.Reason);
        Assert.Equal(before, host.Snapshot.CanonicalText());
        Assert.True(host.AuthorityDispatcherOnly);
    }

    [Fact]
    public void Recipe_command_validation_rejects_deserialized_missing_nested_fields_without_mutating_authority()
    {
        var simulation = Fixture.CreateSimulation();
        var before = simulation.Snapshot().CanonicalText();
        var malformed = Fixture.Plate() with { Command = default, Operation = default };
        var descriptor = Fixture.CreateDescriptor();
        var bytes = CookingUdpCodec.Encode(CookingUdpMessageKind.RecipeCommand, descriptor, "recipe-malformed",
            new CookingUdpRecipeCommandMessage(malformed));

        Assert.True(CookingUdpCodec.TryDecode(bytes, out var envelope, out var reason), reason);
        Assert.True(CookingUdpCodec.TryReadPayload<CookingUdpRecipeCommandMessage>(envelope!, out var payload, out reason), reason);
        Assert.False(CookingRecipeCommandValidation.IsWellFormed(payload!.Command));

        var result = simulation.Submit(payload.Command);

        Assert.Equal(CookingRecipeOutcome.Rejected, result.Outcome);
        Assert.Equal(CookingRecipeRejectionReason.MalformedCommand, result.Reason);
        Assert.Equal(before, simulation.Snapshot().CanonicalText());
    }

    [Fact]
    public void Recipe_codec_round_trips_typed_gameplay_command_without_weakening_envelope_validation()
    {
        var descriptor = Fixture.CreateDescriptor();
        var command = Fixture.Plate();
        var bytes = CookingUdpCodec.Encode(CookingUdpMessageKind.RecipeCommand, descriptor, "recipe-codec",
            new CookingUdpRecipeCommandMessage(command));
        Assert.True(CookingUdpCodec.TryDecode(bytes, out var envelope, out var reason), reason);
        Assert.Equal(CookingUdpMessageKind.RecipeCommand, envelope!.Kind);
        Assert.True(CookingUdpCodec.TryReadPayload<CookingUdpRecipeCommandMessage>(envelope, out var payload, out reason), reason);
        Assert.Equal(command, payload!.Command);
        Assert.False(CookingUdpCodec.TryDecode(new byte[CookingUdpCodec.MaximumDatagramBytes + 1], out _, out reason));
        Assert.Equal("datagram-size", reason);
    }

    private static class Fixture
    {
        internal static readonly PlayerId Host = new("recipe-host");
        internal static readonly PlayerId Remote = new("recipe-remote");
        internal static readonly ItemId Ingredient = new("ingredient-a");
        internal static readonly ItemId Product = new("product-1");
        internal static readonly RecipeId Recipe = new("recipe-a");
        internal static readonly StationSlotId Station = new("stove-a");
        internal static readonly ContainerId PlateId = new("plate-a");
        internal static readonly OrderId Order = new("fixture-order-a");
        internal static readonly OrderId RejectedOrder = new("fixture-order-rejected");
        private static readonly CookingScope Scope = new(new SessionId("recipe-udp-session"), new WorldId("recipe-udp-world"), new MatchId("recipe-udp-match"));

        internal static CookingSessionDescriptor CreateDescriptor() => new(Scope, 1, new CookingProtocolIdentity("cooking-session", 1, 1),
            "recipe-udp-config-v1", new HashSet<string>(StringComparer.Ordinal) { "cook" }, new Dictionary<string, string>());

        internal static CookingRecipeSimulation CreateSimulation()
        {
            var players = new Dictionary<PlayerId, CookingPlayerConfig>
            {
                [Host] = new(Host, new HashSet<string>(StringComparer.Ordinal) { "cook" }, new HashSet<string>(StringComparer.Ordinal) { Station.Value }),
                [Remote] = new(Remote, new HashSet<string>(StringComparer.Ordinal) { "cook" }, new HashSet<string>(StringComparer.Ordinal) { Station.Value }),
            };
            var fixture = new CookingRecipeFixture(Scope, players,
                new Dictionary<DefinitionId, CookingItemDefinition>
                {
                    [new DefinitionId("raw")] = new(new DefinitionId("raw"), new HashSet<string>(StringComparer.Ordinal) { "cook" }),
                    [new DefinitionId("cooked")] = new(new DefinitionId("cooked"), new HashSet<string>(StringComparer.Ordinal) { "cook" }),
                },
                new Dictionary<StationSlotId, CookingApplianceDefinition> { [Station] = new(Station, new HashSet<string>(StringComparer.Ordinal) { "heat" }) },
                new Dictionary<RecipeId, CookingRecipeDefinition> { [Recipe] = new(Recipe, new DefinitionId("raw"), new DefinitionId("cooked"), new ProcessId("definition-process"), "heat", 3) },
                new Dictionary<ContainerId, CookingContainerDefinition> { [PlateId] = new(PlateId, 1) });
            var simulation = new CookingRecipeSimulation(fixture, new OrderPort());
            simulation.AddWorldIngredient(Ingredient, new DefinitionId("raw"), "counter");
            return simulation;
        }

        internal static IEnumerable<CookingRecipeCommand> HostCommands()
        {
            yield return Command("pickup", Host, CookingRecipeOperation.Pickup, Ingredient, 1);
            yield return Command("start", Host, CookingRecipeOperation.StartProcess, Ingredient, 2, recipe: Recipe, station: Station);
            yield return Command("tick-1", Host, CookingRecipeOperation.AdvanceTicks, default, 0, process: new ProcessId("process-1"), ticks: 1);
            yield return Command("tick-2", Host, CookingRecipeOperation.AdvanceTicks, default, 0, process: new ProcessId("process-1"), ticks: 1);
            yield return Command("tick-3", Host, CookingRecipeOperation.AdvanceTicks, default, 0, process: new ProcessId("process-1"), ticks: 1);
        }

        internal static CookingRecipeCommand Plate() => Command("plate", Remote, CookingRecipeOperation.Plate, Product, 1, container: PlateId);
        internal static CookingRecipeCommand Submit(string id, OrderId order) => Command(id, Remote, CookingRecipeOperation.SubmitOrder, Product, 2, order: order);
        private static CookingRecipeCommand Command(string id, PlayerId player, CookingRecipeOperation operation, ItemId item, int version,
            RecipeId? recipe = null, ProcessId? process = null, StationSlotId? station = null, ContainerId? container = null, OrderId? order = null, int ticks = 0) =>
            new(Scope, 1, player, new RecipeCommandId(id), operation, recipe, process, item, station, container, order, version, ticks);
        internal static int ReserveUdpPort() { using var udp = new System.Net.Sockets.UdpClient(0); return ((System.Net.IPEndPoint)udp.Client.LocalEndPoint!).Port; }

        private sealed class OrderPort : ICookingOrderPort
        {
            public CookingOrderAcceptance Submit(CookingOrderSubmission submission) => submission.Order == Order
                ? new(true, "fixture-accepted") : new(false, "fixture-rejected");
        }
    }
}
