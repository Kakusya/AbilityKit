using System.Text.Json;
using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.Udp;

var arguments = ParseArguments(args);
var role = Required(arguments, "role");
var port = int.Parse(Required(arguments, "port"), System.Globalization.CultureInfo.InvariantCulture);
var runRoot = Path.GetFullPath(arguments.GetValueOrDefault("artifact-directory") ??
    Path.Combine("artifacts", "cooking-udp", DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss")));
Directory.CreateDirectory(runRoot);
var topology = arguments.GetValueOrDefault("topology") ?? "manual-udp";
if (topology is not ("udp-loopback" or "udp-same-machine" or "udp-two-pc-lan"))
    throw new ArgumentException("--topology must be udp-loopback, udp-same-machine or udp-two-pc-lan.");
var environment = new
{
    machineName = Environment.MachineName,
    os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
    framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    nicIdentity = arguments.GetValueOrDefault("nic-identity") ?? "not-recorded",
    nicStatus = arguments.GetValueOrDefault("nic-status") ?? "not-recorded",
    firewallStatus = arguments.GetValueOrDefault("firewall-status") ?? "not-recorded",
    endpoint = arguments.GetValueOrDefault("bind") ?? "0.0.0.0",
};
var descriptor = Fixture.CreateAuthority().Descriptor;
var workload = new
{
    id = "udp-minimum-command-v1",
    protocolIdentity = $"{descriptor.Protocol.Name}:{descriptor.Protocol.MinimumVersion}-{descriptor.Protocol.MaximumVersion}",
    configIdentity = descriptor.ConfigIdentity,
};
var buildIdentity = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? Environment.GetEnvironmentVariable("BUILD_SOURCEVERSION") ?? "unknown";

try
{
    if (StringComparer.OrdinalIgnoreCase.Equals(role, "host"))
    {
        using var authority = Fixture.CreateAuthority();
        await using var host = new CookingUdpHost(authority, new CookingUdpHostOptions(
            arguments.GetValueOrDefault("key") ?? CookingUdpHostOptions.DefaultConnectionKey, port, Fixture.PlayerOne, _ => Fixture.PlayerTwo));
        await host.StartAsync();
        await WriteAsync(runRoot, "host-ready.json", new { role, port = host.BoundPort, topology, environment, workload, buildIdentity,
            protocol = descriptor.Protocol, configIdentity = descriptor.ConfigIdentity });
        await Task.Delay(TimeSpan.FromSeconds(int.Parse(arguments.GetValueOrDefault("duration-seconds") ?? "15", System.Globalization.CultureInfo.InvariantCulture)));
        await WriteAsync(runRoot, "host-result.json", new { role, stateHash = authority.Snapshot().Sha256(), diagnostics = authority.Diagnostics,
            transportDiagnostics = host.Diagnostics, topology, environment, workload, buildIdentity, protocol = descriptor.Protocol, configIdentity = descriptor.ConfigIdentity });
        return;
    }

    if (StringComparer.OrdinalIgnoreCase.Equals(role, "client"))
    {
        var hostAddress = Required(arguments, "peer");
        await using var client = new CookingUdpClient(descriptor, new CookingUdpClientOptions(
            arguments.GetValueOrDefault("key") ?? CookingUdpHostOptions.DefaultConnectionKey, hostAddress, port));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(int.Parse(arguments.GetValueOrDefault("timeout-seconds") ?? "10", System.Globalization.CultureInfo.InvariantCulture)));
        await client.ConnectAndHandshakeAsync(timeout.Token);
        var result = await client.SendCommandAsync(Fixture.Command(Fixture.PlayerTwo, "harness-remote"), timeout.Token);
        var delta = await client.WaitForDeltaAsync(timeout.Token);
        await WriteAsync(runRoot, "client-result.json", new
        {
            role,
            assignedPlayer = client.AssignedPlayerId,
            command = result,
            synchronization = delta,
            stateHash = client.Projection.Snapshot?.Sha256(),
            topology,
            environment,
            workload,
            buildIdentity,
            peer = hostAddress,
            protocol = descriptor.Protocol,
            configIdentity = descriptor.ConfigIdentity,
        });
        return;
    }

    throw new ArgumentException("--role must be host or client.");
}
catch (Exception exception)
{
    await WriteAsync(runRoot, "failure.json", new { role, error = exception.ToString() });
    Console.Error.WriteLine(exception);
    Environment.ExitCode = 1;
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
    values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new ArgumentException($"--{name} is required.");

static async Task WriteAsync(string root, string file, object content)
{
    var path = Path.Combine(root, file);
    await File.WriteAllTextAsync(path, JsonSerializer.Serialize(content, new JsonSerializerOptions { WriteIndented = true }));
}

static class Fixture
{
    internal static readonly SessionId Session = new("udp-session");
    internal static readonly WorldId World = new("udp-world");
    internal static readonly MatchId Match = new("udp-match");
    internal static readonly PlayerId PlayerOne = new("udp-player-one");
    internal static readonly PlayerId PlayerTwo = new("udp-player-two");
    private static readonly ItemId Item = new("udp-item");
    private static readonly DefinitionId Definition = new("udp-ingredient");
    private static readonly StationSlotId Station = new("udp-station");

    internal static CookingSessionAuthority CreateAuthority()
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
        return new CookingSessionAuthority(simulation, new CookingSessionDescriptor(scope, 1,
            new CookingProtocolIdentity("cooking-session", 1, 1), "udp-config",
            new HashSet<string>(StringComparer.Ordinal) { "cook" }, new Dictionary<string, string>()), 8);
    }

    internal static CookingCommand Command(PlayerId player, string id) => new(new CookingScope(Session, World, Match), 10,
        player, new CommandId(id), CookingOperation.Pickup, Item, 1);
}
