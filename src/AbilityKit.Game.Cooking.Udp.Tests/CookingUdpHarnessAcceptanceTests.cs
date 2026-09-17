using System.Text.Json;
using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.Udp;
using Xunit;

namespace AbilityKit.Game.Cooking.Udp.Tests;

[Trait("Gate", "CookingUdp")]
public sealed class CookingUdpHarnessAcceptanceTests
{
    [Fact]
    public void Acceptance_uses_role_isolated_paths_and_streaming_reducer_for_valid_contract()
    {
        using var fixture = new ArtifactFixture();
        fixture.WriteValidRun();

        var accepted = CookingUdpHarnessAcceptance.Validate(fixture.Root, fixture.RunId, fixture.Descriptor);

        Assert.Equal(fixture.RunId, accepted.RunId);
        Assert.Equal(13, accepted.HostEventCount);
        Assert.Equal(12, accepted.ClientEventCount);
        Assert.Equal(fixture.Hash, accepted.StateHash);
    }

    [Theory]
    [InlineData("wrong-run")]
    [InlineData("wrong-role")]
    [InlineData("wrong-schema")]
    [InlineData("wrong-scope")]
    [InlineData("wrong-epoch")]
    [InlineData("unmatched-result")]
    [InlineData("delta-gap")]
    [InlineData("unmatched-delta")]
    [InlineData("truncated")]
    public void Acceptance_rejects_negative_log_contracts(string mutation)
    {
        using var fixture = new ArtifactFixture();
        fixture.WriteValidRun();
        fixture.MutateClientLog(mutation);

        Assert.Throws<InvalidDataException>(() => CookingUdpHarnessAcceptance.Validate(fixture.Root, fixture.RunId, fixture.Descriptor));
    }

    [Fact]
    public void Recipe_transport_rejections_are_structured_and_preserve_command_correlation()
    {
        var queueFull = CookingUdpRecipeHost.CreateQueueFullResult(4);
        var malformed = CookingUdpRecipeHost.CreateMalformedCommandResult(4);

        Assert.Equal(CookingRecipeOutcome.Rejected, queueFull.Outcome);
        Assert.Equal(CookingRecipeRejectionReason.QueueFull, queueFull.Reason);
        Assert.Equal(4, queueFull.StateVersion);
        Assert.Empty(queueFull.Events);
        Assert.Equal(CookingRecipeOutcome.Rejected, malformed.Outcome);
        Assert.Equal(CookingRecipeRejectionReason.MalformedCommand, malformed.Reason);
        Assert.Equal(4, malformed.StateVersion);
        Assert.Empty(malformed.Events);
    }

    private sealed class ArtifactFixture : IDisposable
    {
        private static readonly CookingScope Scope = new(new SessionId("harness-session"), new WorldId("harness-world"), new MatchId("harness-match"));
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "abilitykit-cooking-harness-" + Guid.NewGuid().ToString("N"));
        public string RunId { get; } = "run-" + Guid.NewGuid().ToString("N");
        public string Hash { get; } = "CANONICAL-HASH";
        public CookingSessionDescriptor Descriptor { get; } = new(Scope, 7,
            new CookingProtocolIdentity("cooking-session", 1, 1), "harness-config",
            new HashSet<string>(StringComparer.Ordinal) { "cook" }, new Dictionary<string, string>());

        public void WriteValidRun()
        {
            Directory.CreateDirectory(Path.Combine(Root, "host"));
            Directory.CreateDirectory(Path.Combine(Root, "client"));
            WriteResult("host", authorityDispatcherOnly: true);
            WriteResult("client", authorityDispatcherOnly: null);
            WriteEvents("host", new[]
            {
                Event("host-started", "accepted", "host-start"),
                Event("baseline-installed", "accepted", "host-baseline", stateHash: Hash, sequence: 1, baseline: 1),
                Event("gameplay-command", "submitted", "host-pickup", "host-pickup"),
                Event("command-result", "accepted", "host-pickup", "host-pickup"),
                Event("gameplay-command", "submitted", "host-start", "host-start"),
                Event("command-result", "accepted", "host-start", "host-start"),
                Event("gameplay-command", "submitted", "host-tick-1", "host-tick-1"),
                Event("command-result", "accepted", "host-tick-1", "host-tick-1"),
                Event("gameplay-command", "submitted", "host-tick-2", "host-tick-2"),
                Event("command-result", "accepted", "host-tick-2", "host-tick-2"),
                Event("gameplay-command", "submitted", "host-tick-3", "host-tick-3"),
                Event("command-result", "accepted", "host-tick-3", "host-tick-3"),
                Event("role-result", "accepted", "host-result", stateHash: Hash),
            });
            WriteEvents("client", new[]
            {
                Event("baseline-installed", "accepted", "client-handshake", stateHash: Hash, sequence: 1, baseline: 1),
                Event("gameplay-command", "submitted", "plate-correlation", "remote-plate"),
                Event("command-result", "accepted", "plate-correlation", "remote-plate"),
                Event("snapshot-delta", "accepted", "plate-correlation", stateHash: Hash, sequence: 2, baseline: 1),
                Event("gameplay-command", "submitted", "reject-correlation", "remote-reject", "fixture-order-rejected"),
                Event("command-result", "rejected", "reject-correlation", "remote-reject", "fixture-order-rejected", "OrderRejected"),
                Event("gameplay-command", "submitted", "submit-correlation", "remote-submit", "fixture-order-a"),
                Event("command-result", "accepted", "submit-correlation", "remote-submit", "fixture-order-a"),
                Event("gameplay-command", "submitted", "submit-correlation", "remote-submit", "fixture-order-a"),
                Event("command-result", "accepted", "submit-correlation", "remote-submit", "fixture-order-a", "Duplicate"),
                Event("snapshot-delta", "accepted", "submit-correlation", stateHash: Hash, sequence: 3, baseline: 1),
                Event("role-result", "accepted", "client-result", stateHash: Hash),
            });
        }

        public void MutateClientLog(string mutation)
        {
            var path = Path.Combine(Root, "client", "events.jsonl");
            var lines = File.ReadAllLines(path).ToList();
            switch (mutation)
            {
                case "wrong-run": lines[1] = Replace(lines[1], "runId", "foreign-run"); break;
                case "wrong-role": lines[1] = Replace(lines[1], "role", "host"); break;
                case "wrong-schema": lines[1] = Replace(lines[1], "schema", "unknown.v1"); break;
                case "wrong-scope": lines[1] = ReplaceNestedScope(lines[1]); break;
                case "wrong-epoch": lines[1] = Replace(lines[1], "epoch", 999L); break;
                case "unmatched-result": lines[2] = Replace(lines[2], "correlationId", "missing-command"); break;
                case "delta-gap": lines[3] = Replace(lines[3], "snapshotSequence", 3L); break;
                case "unmatched-delta": lines[3] = Replace(lines[3], "correlationId", "foreign-command"); break;
                case "truncated": lines[1] = lines[1][..^1]; break;
                default: throw new ArgumentOutOfRangeException(nameof(mutation));
            }
            File.WriteAllLines(path, lines);
        }

        private void WriteResult(string role, bool? authorityDispatcherOnly)
        {
            var value = new CookingUdpHarnessAcceptance.CookingHarnessRoleResult(
                CookingUdpHarnessAcceptance.ResultSchema, RunId, role, CookingUdpHarnessAcceptance.Topology,
                "fixture", Descriptor.ConfigIdentity, CookingUdpScope.FromDomain(Descriptor.Scope), Descriptor.Epoch, Hash,
                "fixture-order-a", true, JsonSerializer.SerializeToElement(new { orderId = "fixture-order-a", completion = "fixture-completed" }),
                authorityDispatcherOnly);
            File.WriteAllText(Path.Combine(Root, role, "result.json"), JsonSerializer.Serialize(value,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }

        private void WriteEvents(string role, IEnumerable<CookingUdpHarnessAcceptance.CookingHarnessEvent> events)
        {
            var path = Path.Combine(Root, role, "events.jsonl");
            long eventSequence = 0;
            foreach (var value in events)
            {
                var numbered = value with { Role = role, Sequence = ++eventSequence };
                CookingUdpHarnessAcceptance.AppendEventAsync(path, numbered).GetAwaiter().GetResult();
            }
        }

        private CookingUdpHarnessAcceptance.CookingHarnessEvent Event(string eventType, string outcome, string correlation,
            string? command = null, string? order = null, string reason = "None", string? stateHash = null,
            long? sequence = null, long? baseline = null) =>
            new(CookingUdpHarnessAcceptance.EventSchema, RunId, string.Empty, 0, DateTimeOffset.UtcNow.ToString("O"), eventType,
                CookingUdpScope.FromDomain(Descriptor.Scope), Descriptor.Epoch, correlation, command, order, outcome, reason,
                stateHash, sequence, baseline);

        private static string Replace(string json, string property, object value)
        {
            using var document = JsonDocument.Parse(json);
            var map = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
            map[property] = JsonSerializer.SerializeToElement(value);
            return JsonSerializer.Serialize(map, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }

        private static string ReplaceNestedScope(string json)
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
            map["scope"] = JsonSerializer.SerializeToElement(new { sessionId = "other", worldId = "harness-world", matchId = "harness-match" });
            return JsonSerializer.Serialize(map, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
