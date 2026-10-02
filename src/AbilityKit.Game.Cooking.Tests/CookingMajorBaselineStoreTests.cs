using System.Text.Json.Nodes;
using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingMajorBaselineStoreTests
{
    [Fact]
    public void Required_host_clock_roundtrips_in_integrity_and_prior_typed_baseline_is_rejected()
    {
        using var directory = new BaselineDirectory();
        var store = new CookingMajorCheckpointStore(directory.Path);
        var payload = Payload() with { HostFrameSequence = 321 };
        Assert.Equal(3, CookingMajorCheckpointStore.CurrentBaselineFormatVersion);
        Assert.True(store.WriteBaseline(payload).Accepted);
        Assert.Equal(321, store.ReadBaseline(payload.TargetScope.MatchScope).Payload!.HostFrameSequence);
        var saved = File.ReadAllText(directory.Current);
        Assert.Equal(CookingMajorBaselineReason.InvalidBaseline,
            store.WriteBaseline(payload with { HostFrameSequence = -1 }).Reason);
        Assert.Equal(saved, File.ReadAllText(directory.Current));
        var tree = JsonNode.Parse(saved)!;
        tree["payload"]!.AsObject().Remove("hostFrameSequence");
        File.WriteAllText(directory.Current, tree.ToJsonString());
        Assert.Equal(CookingMajorBaselineReason.RecordTruncated, store.ReadBaseline(payload.TargetScope.MatchScope).Reason);
        tree = JsonNode.Parse(saved)!; tree["payload"]!["hostFrameSequence"] = 322;
        File.WriteAllText(directory.Current, tree.ToJsonString());
        Assert.Equal(CookingMajorBaselineReason.IntegrityFailure, store.ReadBaseline(payload.TargetScope.MatchScope).Reason);
        tree = JsonNode.Parse(saved)!; tree["formatVersion"] = 2;
        tree["payload"]!.AsObject().Remove("hostFrameSequence");
        File.WriteAllText(directory.Current, tree.ToJsonString());
        Assert.Equal(CookingMajorBaselineReason.UnsupportedLegacyBaseline, store.ReadBaseline(payload.TargetScope.MatchScope).Reason);
    }

    [Fact]
    public void Typed_success_baseline_survives_new_store_and_restores_actual_inventory()
    {
        using var directory = new BaselineDirectory();
        var payload = Payload();
        Assert.True(new CookingMajorCheckpointStore(directory.Path).WriteBaseline(payload).Accepted);
        var read = new CookingMajorCheckpointStore(directory.Path).ReadBaseline(payload.TargetScope.MatchScope);
        Assert.True(read.Accepted);
        Assert.Equal(payload.CanonicalText(), read.Payload!.CanonicalText());
        var content = Content();
        var restored = Simulation(content, payload.TargetScope.MatchScope);
        Assert.True(restored.AcceptSuccessHandoff(read.Payload.Kitchen).Accepted);
        Assert.Equal(payload.Kitchen.CanonicalText(), restored.ExportSuccessHandoff().CanonicalText());
        Assert.NotEmpty(restored.Snapshot().Items);
        var progress = read.Payload.Choices.CreateProgress();
        Assert.True(progress.Locked);
        Assert.True(progress.CookFaster);
        Assert.Equal(payload.Choices.Unlocks, progress.Unlocks);
    }

    [Fact]
    public void Invalid_write_preserves_last_valid_file_and_unconfirmed_choices()
    {
        using var directory = new BaselineDirectory();
        var store = new CookingMajorCheckpointStore(directory.Path);
        var payload = Payload();
        Assert.True(store.WriteBaseline(payload).Accepted);
        var saved = File.ReadAllText(directory.Current);
        foreach (var bad in new[] {
            payload with { Choices = payload.Choices with { Locked = false } },
            payload with { Kitchen = payload.Kitchen with { LogicalTick = 1 } },
            payload with { Kitchen = payload.Kitchen with { SchemaVersion = 4 } },
            payload with { TargetScope = payload.SourceScope },
            payload with { Choices = payload.Choices with { Unlocks = new[] { new DefinitionId("oven-b"), new DefinitionId("oven-b") } } }
        })
        {
            Assert.Equal(CookingMajorBaselineReason.InvalidBaseline, store.WriteBaseline(bad).Reason);
            Assert.Equal(saved, File.ReadAllText(directory.Current));
        }
        Assert.True(store.ReadBaseline(payload.TargetScope.MatchScope).Accepted);
    }

    [Fact]
    public void Typed_read_rejects_legacy_truncated_tampered_missing_required_and_wrong_match()
    {
        using var directory = new BaselineDirectory();
        var store = new CookingMajorCheckpointStore(directory.Path);
        var payload = Payload();
        Assert.True(store.WriteBaseline(payload).Accepted);
        var saved = File.ReadAllText(directory.Current);
        File.WriteAllText(directory.Current, saved[..(saved.Length / 2)]);
        Assert.Equal(CookingMajorBaselineReason.RecordTruncated, store.ReadBaseline(payload.TargetScope.MatchScope).Reason);
        File.WriteAllText(directory.Current, saved.Replace("next-level", "evil-level", StringComparison.Ordinal));
        Assert.Equal(CookingMajorBaselineReason.IntegrityFailure, store.ReadBaseline(payload.TargetScope.MatchScope).Reason);
        var tree = JsonNode.Parse(saved)!;
        tree["payload"]!.AsObject().Remove("menuPolicyIdentity");
        File.WriteAllText(directory.Current, tree.ToJsonString());
        Assert.Equal(CookingMajorBaselineReason.RecordTruncated, store.ReadBaseline(payload.TargetScope.MatchScope).Reason);
        tree = JsonNode.Parse(saved)!;
        tree["payload"]!["kitchen"]!["items"]![0]!["location"] = null;
        File.WriteAllText(directory.Current, tree.ToJsonString());
        Assert.Equal(CookingMajorBaselineReason.InvalidBaseline, store.ReadBaseline(payload.TargetScope.MatchScope).Reason);
        File.WriteAllText(directory.Current, saved);
        var other = new CookingScope(new SessionId("other"), new WorldId("world"), new MatchId("match"));
        Assert.Equal(CookingMajorBaselineReason.ScopeMismatch, store.ReadBaseline(other).Reason);
        Assert.True(store.Write(payload.TargetScope.MatchScope, payload.Choices.CreateProgress(), payload.Kitchen).Accepted);
        Assert.Equal(CookingMajorBaselineReason.UnsupportedLegacyBaseline, store.ReadBaseline(payload.TargetScope.MatchScope).Reason);
    }

    [Fact]
    public void Temporary_write_failure_keeps_current_success_file()
    {
        using var directory = new BaselineDirectory();
        var payload = Payload();
        var store = new CookingMajorCheckpointStore(directory.Path);
        Assert.True(store.WriteBaseline(payload).Accepted);
        var saved = File.ReadAllText(directory.Current);
        Directory.CreateDirectory(directory.Current + ".next");
        Assert.Equal(CookingMajorBaselineReason.WriteFailed, store.WriteBaseline(payload).Reason);
        Assert.Equal(saved, File.ReadAllText(directory.Current));
        Assert.True(new CookingMajorCheckpointStore(directory.Path).ReadBaseline(payload.TargetScope.MatchScope).Accepted);
    }

    private static CookingMajorBaselinePayload Payload()
    {
        var content = Content();
        var match = new CookingScope(new SessionId("session"), new WorldId("world"), new MatchId("match"));
        var source = new CookingLevelScope(match, new RestaurantRuntimeId(1), new LevelId("source-level"), 1);
        var target = new CookingLevelScope(match, new RestaurantRuntimeId(1), new LevelId("next-level"), 2);
        var simulation = Simulation(content, match);
        CookingContentCatalog.ApplyStandardInitialSupply(simulation, content);
        var configuration = content.Snapshot;
        var progress = new CookingMajorProgress();
        Assert.True(progress.Unlock(new DefinitionId("oven-b")).Accepted);
        progress.EnableCookFaster(); progress.Lock();
        var preparation = new CookingLevelPreparation(target.Level, new MapId("map"),
            new CookingLogicalLayout(new LayoutId("layout"), Array.Empty<StationSlotId>(), Array.Empty<DefinitionId>()),
            configuration.Identity);
        return new(source, target, preparation, configuration.Identity, null, null, null, null,
            simulation.ExportSuccessHandoff(), CookingMajorBaselineChoices.Capture(progress));
    }

    private static CookingContent Content() => CookingContentCatalog.Load(File.ReadAllText(
        System.IO.Path.Combine(AppContext.BaseDirectory, CookingContentCatalog.ContentFileName)));
    private static CookingRecipeSimulation Simulation(CookingContent content, CookingScope match)
    {
        var player = new PlayerId("chef");
        return new(CookingContentCatalog.BuildFixture(content, match,
            new Dictionary<PlayerId, CookingPlayerConfig> {
                [player] = new(player, new HashSet<string> { "cook" },
                    new HashSet<string> { "board-a", "stove-a", "oven-a", "counter-a" }) }));
    }

    private sealed class BaselineDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cooking-baseline-" + Guid.NewGuid().ToString("N"));
        public string Current => System.IO.Path.Combine(Path, "major.checkpoint.json");
        public BaselineDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
