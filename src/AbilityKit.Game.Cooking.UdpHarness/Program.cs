using System.Text;
using System.Text.Json;
using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.Udp;

var arguments = ParseArguments(args);
if (arguments.TryGetValue("acceptance-root", out var acceptanceRoot))
{
    var acceptanceRunId = Required(arguments, "run-id");
    var acceptance = CookingUdpHarnessAcceptance.Validate(acceptanceRoot, acceptanceRunId, Fixture.Descriptor);
    Console.WriteLine(JsonSerializer.Serialize(acceptance));
    return;
}

var role = Required(arguments, "role");
var runId = Required(arguments, "run-id");
var port = int.Parse(Required(arguments, "port"), System.Globalization.CultureInfo.InvariantCulture);
var artifactDirectory = Path.GetFullPath(Required(arguments, "artifact-directory"));
var topology = arguments.GetValueOrDefault("topology") ?? "udp-same-machine";
if (topology != "udp-same-machine") throw new ArgumentException("This fixture only accepts --topology udp-same-machine.");
Directory.CreateDirectory(artifactDirectory);
var eventLog = new HarnessEvents(Path.Combine(artifactDirectory, "events.jsonl"), runId, role, Fixture.Descriptor);
var timeoutSeconds = int.Parse(arguments.GetValueOrDefault("timeout-seconds") ?? "15", System.Globalization.CultureInfo.InvariantCulture);

try
{
    if (role.Equals("host", StringComparison.OrdinalIgnoreCase))
    {
        var simulation = Fixture.CreateSimulation();
        await using var host = new CookingUdpRecipeHost(simulation, Fixture.Descriptor,
            new CookingUdpRecipeHostOptions(arguments.GetValueOrDefault("key") ?? CookingUdpRecipeHostOptions.DefaultConnectionKey,
                port, Fixture.HostPlayer, _ => Fixture.RemotePlayer));
        await host.StartAsync();
        await eventLog.AppendAsync("host-started", "accepted", correlationId: "host-start", stateHash: host.Snapshot.Sha256());

        var pickup = await HostCommand(host, eventLog, Fixture.Pickup(), "host-pickup");
        var start = await HostCommand(host, eventLog, Fixture.Start(), "host-start-process");
        var tick1 = await HostCommand(host, eventLog, Fixture.Tick("host-tick-1"), "host-tick-1");
        var tick2 = await HostCommand(host, eventLog, Fixture.Tick("host-tick-2"), "host-tick-2");
        var tick3 = await HostCommand(host, eventLog, Fixture.Tick("host-tick-3"), "host-tick-3");
        if (new[] { pickup, start, tick1, tick2, tick3 }.Any(result => result.Outcome != CookingRecipeOutcome.Accepted))
            throw new InvalidOperationException("Host fixture preparation command was rejected.");

        await WriteJsonAsync(Path.Combine(artifactDirectory, "ready.json"), new
        {
            schema = "abilitykit.cooking-harness-ready.v1", runId, role, topology, port = host.BoundPort,
            fixtureId = Fixture.Id, protocol = Fixture.Descriptor.Protocol, configIdentity = Fixture.Descriptor.ConfigIdentity,
        });
        await eventLog.AppendAsync("host-ready", "accepted", correlationId: "host-ready", orderId: Fixture.Order.Value,
            stateHash: host.Snapshot.Sha256());

        await Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
        var final = host.Snapshot;
        await eventLog.AppendAsync("baseline-installed", "accepted", correlationId: "host-baseline",
            stateHash: final.Sha256(), snapshotSequence: 1, baselineReference: 1);
        await eventLog.AppendAsync("role-result", final.AcceptedOrders.Contains(Fixture.Order) ? "accepted" : "incomplete",
            correlationId: "host-result", orderId: Fixture.Order.Value, stateHash: final.Sha256());
        await WriteJsonAsync(Path.Combine(artifactDirectory, "result.json"), new
        {
            schema = CookingUdpHarnessAcceptance.ResultSchema, runId, role, topology, fixtureId = Fixture.Id,
            configIdentity = Fixture.Descriptor.ConfigIdentity, scope = CookingUdpScope.FromDomain(Fixture.Descriptor.Scope), epoch = Fixture.Descriptor.Epoch,
            stateHash = final.Sha256(), orderId = Fixture.Order.Value, orderAccepted = final.AcceptedOrders.Contains(Fixture.Order),
            settlement = final.AcceptedOrders.Contains(Fixture.Order) ? new { orderId = Fixture.Order.Value, completion = "fixture-completed" } : null,
            authorityDispatcherOnly = host.AuthorityDispatcherOnly, callbackInvocationCount = host.CallbackInvocationCount,
            transportDiagnostics = host.Diagnostics,
        });
        return;
    }

    if (role.Equals("client", StringComparison.OrdinalIgnoreCase))
    {
        var peer = Required(arguments, "peer");
        await using var client = new CookingUdpRecipeClient(Fixture.Descriptor,
            new CookingUdpClientOptions(arguments.GetValueOrDefault("key") ?? CookingUdpRecipeHostOptions.DefaultConnectionKey, peer, port));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        await client.ConnectAndHandshakeAsync(timeout.Token);
        await eventLog.AppendAsync("baseline-installed", "accepted", correlationId: "client-handshake",
            stateHash: client.Projection.RecipeSnapshot?.Sha256(), snapshotSequence: client.Projection.Sequence,
            baselineReference: client.Projection.BaselineReference);

        var product = Fixture.Product;
        var plated = await ClientCommand(client, eventLog, Fixture.Plate(product, 1), timeout.Token);
        var plateDelta = await client.WaitForDeltaAsync(timeout.Token);
        await eventLog.AppendAsync("snapshot-delta", plateDelta.Accepted ? "accepted" : "rejected", correlationId: "recipe-udp-command-remote-plate",
            stateHash: client.Projection.RecipeSnapshot?.Sha256(), snapshotSequence: client.Projection.Sequence,
            baselineReference: client.Projection.BaselineReference);
        if (plated.Outcome != CookingRecipeOutcome.Accepted || !plateDelta.Accepted) throw new InvalidOperationException("Remote plate command failed.");

        var rejected = await ClientCommand(client, eventLog, Fixture.Submit(product, 2, "remote-reject", Fixture.RejectedOrder), timeout.Token);
        if (rejected.Outcome != CookingRecipeOutcome.Rejected || rejected.Reason != CookingRecipeRejectionReason.OrderRejected)
            throw new InvalidOperationException("Expected fixture rejection did not occur.");

        var submitted = await ClientCommand(client, eventLog, Fixture.Submit(product, 2, "remote-submit", Fixture.Order), timeout.Token);
        var submitDelta = await client.WaitForDeltaAsync(timeout.Token);
        await eventLog.AppendAsync("snapshot-delta", submitDelta.Accepted ? "accepted" : "rejected", correlationId: "recipe-udp-command-remote-submit",
            orderId: Fixture.Order.Value, stateHash: client.Projection.RecipeSnapshot?.Sha256(), snapshotSequence: client.Projection.Sequence,
            baselineReference: client.Projection.BaselineReference);
        var duplicate = await ClientCommand(client, eventLog, Fixture.Submit(product, 2, "remote-submit", Fixture.Order), timeout.Token);
        if (submitted.Outcome != CookingRecipeOutcome.Accepted || !submitDelta.Accepted || !duplicate.IsDuplicate)
            throw new InvalidOperationException("Remote submission or duplicate contract failed.");

        var final = client.Projection.RecipeSnapshot ?? throw new InvalidOperationException("Recipe projection was empty.");
        await eventLog.AppendAsync("role-result", "accepted", correlationId: "client-result", orderId: Fixture.Order.Value,
            stateHash: final.Sha256(), snapshotSequence: client.Projection.Sequence, baselineReference: client.Projection.BaselineReference);
        await WriteJsonAsync(Path.Combine(artifactDirectory, "result.json"), new
        {
            schema = CookingUdpHarnessAcceptance.ResultSchema, runId, role, topology, fixtureId = Fixture.Id,
            configIdentity = Fixture.Descriptor.ConfigIdentity, scope = CookingUdpScope.FromDomain(Fixture.Descriptor.Scope), epoch = Fixture.Descriptor.Epoch,
            assignedPlayer = client.AssignedPlayerId, stateHash = final.Sha256(), orderId = Fixture.Order.Value,
            orderAccepted = final.AcceptedOrders.Contains(Fixture.Order),
            settlement = final.AcceptedOrders.Contains(Fixture.Order) ? new { orderId = Fixture.Order.Value, completion = "fixture-completed" } : null,
            snapshotSequence = client.Projection.Sequence, baselineReference = client.Projection.BaselineReference,
        });
        return;
    }

    throw new ArgumentException("--role must be host or client.");
}
catch (Exception exception)
{
    await eventLog.AppendAsync("role-failure", "failed", correlationId: "failure", reason: exception.GetType().Name);
    await WriteJsonAsync(Path.Combine(artifactDirectory, "failure.json"), new
    {
        schema = "abilitykit.cooking-harness-failure.v1", runId, role, topology, error = exception.ToString(),
    });
    Console.Error.WriteLine(exception);
    Environment.ExitCode = 1;
}

static async Task<CookingRecipeCommandResult> HostCommand(CookingUdpRecipeHost host, HarnessEvents events,
    CookingRecipeCommand command, string correlation)
{
    await events.AppendAsync("gameplay-command", "submitted", correlation, command.Command.Value, orderId: command.Order?.Value,
        stateHash: host.Snapshot.Sha256());
    var result = await host.SubmitHostLocalAsync(command, correlation);
    await events.AppendAsync("command-result", result.Outcome.ToString().ToLowerInvariant(), correlation, command.Command.Value,
        command.Order?.Value, result.Reason.ToString(), host.Snapshot.Sha256());
    return result;
}

static async Task<CookingUdpRecipeCommandResultMessage> ClientCommand(CookingUdpRecipeClient client, HarnessEvents events,
    CookingRecipeCommand command, CancellationToken cancellationToken)
{
    var correlation = $"recipe-udp-command-{command.Command.Value}";
    await events.AppendAsync("gameplay-command", "submitted", correlation, command.Command.Value, command.Order?.Value,
        stateHash: client.Projection.RecipeSnapshot?.Sha256());
    var result = await client.SendCommandAsync(command, cancellationToken);
    await events.AppendAsync("command-result", result.Outcome.ToString().ToLowerInvariant(), correlation, command.Command.Value,
        command.Order?.Value, result.IsDuplicate ? "Duplicate" : result.Reason.ToString(), client.Projection.RecipeSnapshot?.Sha256());
    return result;
}

static Dictionary<string, string> ParseArguments(string[] values)
{
    var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var index = 0; index < values.Length; index += 2)
    {
        if (!values[index].StartsWith("--", StringComparison.Ordinal) || index + 1 >= values.Length)
            throw new ArgumentException("Arguments must be --name value pairs.");
        parsed.Add(values[index][2..], values[index + 1]);
    }
    return parsed;
}

static string Required(IReadOnlyDictionary<string, string> values, string name) =>
    values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException($"--{name} is required.");

static Task WriteJsonAsync(string path, object value) => File.WriteAllTextAsync(path,
    JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

sealed class HarnessEvents(string path, string runId, string role, CookingSessionDescriptor descriptor)
{
    private long _sequence;

    public Task AppendAsync(string eventType, string outcome, string correlationId, string? commandId = null, string? orderId = null,
        string? reason = null, string? stateHash = null, long? snapshotSequence = null, long? baselineReference = null) =>
        CookingUdpHarnessAcceptance.AppendEventAsync(path, new CookingUdpHarnessAcceptance.CookingHarnessEvent(
            CookingUdpHarnessAcceptance.EventSchema, runId, role, Interlocked.Increment(ref _sequence),
            DateTimeOffset.UtcNow.ToString("O"), eventType, CookingUdpScope.FromDomain(descriptor.Scope), descriptor.Epoch,
            correlationId, commandId, orderId, outcome, reason ?? "None", stateHash, snapshotSequence, baselineReference));
}

static class Fixture
{
    internal const string Id = "same-machine-udp-collaboration-v1";
    internal static readonly SessionId Session = new("recipe-udp-session");
    internal static readonly WorldId World = new("recipe-udp-world");
    internal static readonly MatchId Match = new("recipe-udp-match");
    internal static readonly PlayerId HostPlayer = new("recipe-host");
    internal static readonly PlayerId RemotePlayer = new("recipe-remote");
    internal static readonly ItemId Ingredient = new("fixture-ingredient-a");
    internal static readonly ItemId Product = new("product-1");
    internal static readonly DefinitionId Raw = new("fixture-raw");
    internal static readonly DefinitionId Cooked = new("fixture-cooked");
    internal static readonly RecipeId Recipe = new("fixture-single-step");
    internal static readonly ProcessId Process = new("process-1");
    internal static readonly StationSlotId Station = new("fixture-stove");
    internal static readonly ContainerId PlateId = new("fixture-plate");
    internal static readonly OrderId Order = new("fixture-order-a");
    internal static readonly OrderId RejectedOrder = new("fixture-order-rejected");
    internal static readonly CookingSessionDescriptor Descriptor = new(new CookingScope(Session, World, Match), 1,
        new CookingProtocolIdentity("cooking-session", 1, 1), "recipe-udp-config-v1",
        new HashSet<string>(StringComparer.Ordinal) { "cook" }, new Dictionary<string, string>());

    internal static CookingRecipeSimulation CreateSimulation()
    {
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [HostPlayer] = new(HostPlayer, new HashSet<string>(StringComparer.Ordinal) { "cook" }, new HashSet<string>(StringComparer.Ordinal) { Station.Value }),
            [RemotePlayer] = new(RemotePlayer, new HashSet<string>(StringComparer.Ordinal) { "cook" }, new HashSet<string>(StringComparer.Ordinal) { Station.Value }),
        };
        var fixture = new CookingRecipeFixture(Descriptor.Scope, players,
            new Dictionary<DefinitionId, CookingItemDefinition>
            {
                [Raw] = new(Raw, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
                [Cooked] = new(Cooked, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            },
            new Dictionary<StationSlotId, CookingApplianceDefinition>
            { [Station] = new(Station, new HashSet<string>(StringComparer.Ordinal) { "heat" }) },
            new Dictionary<RecipeId, CookingRecipeDefinition>
            { [Recipe] = new(Recipe, Raw, Cooked, new ProcessId("fixture-process"), "heat", 3) },
            new Dictionary<ContainerId, CookingContainerDefinition> { [PlateId] = new(PlateId, 1) });
        var simulation = new CookingRecipeSimulation(fixture, new FixtureOrderPort());
        simulation.AddWorldIngredient(Ingredient, Raw, "fixture-counter");
        return simulation;
    }

    internal static CookingRecipeCommand Pickup() => Command("host-pickup", HostPlayer, CookingRecipeOperation.Pickup, Ingredient, 1);
    internal static CookingRecipeCommand Start() => Command("host-start", HostPlayer, CookingRecipeOperation.StartProcess, Ingredient, 2,
        recipe: Recipe, station: Station);
    internal static CookingRecipeCommand Tick(string id) => Command(id, HostPlayer, CookingRecipeOperation.AdvanceTicks, default, 0,
        process: new ProcessId("process-1"), ticks: 1);
    internal static CookingRecipeCommand Plate(ItemId product, int version) => Command("remote-plate", RemotePlayer, CookingRecipeOperation.Plate,
        product, version, container: PlateId);
    internal static CookingRecipeCommand Submit(ItemId product, int version, string id, OrderId order) => Command(id, RemotePlayer,
        CookingRecipeOperation.SubmitOrder, product, version, order: order);

    private static CookingRecipeCommand Command(string id, PlayerId player, CookingRecipeOperation operation, ItemId item, int version,
        RecipeId? recipe = null, ProcessId? process = null, StationSlotId? station = null, ContainerId? container = null, OrderId? order = null,
        int ticks = 0) => new(Descriptor.Scope, 1, player, new RecipeCommandId(id), operation, recipe, process, item, station, container, order, version, ticks);

    private sealed class FixtureOrderPort : ICookingOrderPort
    {
        public CookingOrderAcceptance Submit(CookingOrderSubmission submission) =>
            submission.Order == Order && submission.Recipe == Recipe ? new(true, "fixture-accepted") : new(false, "fixture-rejected");
    }
}
