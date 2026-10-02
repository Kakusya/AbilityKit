using System.Reflection;
using System.Text.Json;
using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingPreparedGenerationCommitTests
{
    [Fact]
    public void Preparation_is_unpublished_and_commit_matches_legacy_transition_once()
    {
        var source = Ended(); var legacy = Ended();
        Assert.Equal(CookingLevelLifecycleReason.None,
            source.TryCreateSuccessorCandidate(new("next"), 2, out var candidate));
        var before = source.Snapshot(); var events = JsonSerializer.Serialize(source.EventHistory);
        Assert.True(source.PrepareSuccessorCandidateCommit(candidate!, out var prepared, out var reason));
        Assert.Equal(CookingLevelLifecycleReason.None, reason);
        Assert.Equal(before, source.Snapshot());
        Assert.Equal(events, JsonSerializer.Serialize(source.EventHistory));
        Assert.Equal(CookingLevelState.Created, candidate!.State);
        Assert.Equal(before.Version + 1, prepared!.Result.SourceVersionAfter);
        var expected = legacy.CreateSuccessor(new("next"), 2);
        Assert.True(expected.Accepted);
        prepared.Commit();
        Assert.Equal(legacy.Snapshot(), source.Snapshot());
        Assert.Equal(JsonSerializer.Serialize(legacy.EventHistory), JsonSerializer.Serialize(source.EventHistory));
        Assert.Equal(expected.SourceResult, prepared.Result.SourceResult with { Events = expected.SourceResult.Events });
        var committed = source.Snapshot();
        prepared.Commit();
        Assert.Equal(committed, source.Snapshot());
        Assert.Equal(CookingLevelLifecycleReason.GenerationAlreadyCreated,
            source.CreateSuccessor(new("another"), 3).Reason);
    }

    [Fact]
    public void Failed_source_and_foreign_candidate_are_rejected_without_publication()
    {
        var source = Ended();
        var candidate = new CookingLevelLifecycle(new(new(new("foreign"), new("world"), new("match")),
            new(1), new("next"), 2), source.Configuration, source.GameplayFactory);
        var before = source.Snapshot();
        Assert.False(source.PrepareSuccessorCandidateCommit(candidate, out var prepared, out var reason));
        Assert.Null(prepared); Assert.Equal(CookingLevelLifecycleReason.InvalidState, reason);
        Assert.Equal(before, source.Snapshot());
        var failed = Ended(CookingLevelOutcome.Failed); before = failed.Snapshot();
        Assert.False(failed.PrepareSuccessorCandidateCommit(candidate, out prepared, out reason));
        Assert.Null(prepared); Assert.Equal(CookingLevelLifecycleReason.SuccessorRequiresSuccessOutcome, reason);
        Assert.Equal(before, failed.Snapshot());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Counter_overflow_is_rejected_before_the_durable_commit_point(bool version)
    {
        var source = Ended();
        Assert.Equal(CookingLevelLifecycleReason.None,
            source.TryCreateSuccessorCandidate(new("next"), 2, out var candidate));
        var property = typeof(CookingLevelLifecycle).GetProperty("Version")!;
        var field = typeof(CookingLevelLifecycle).GetField("_eventSequence", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var original = version ? source.Version : (long)field.GetValue(source)!;
        if (version) property.SetValue(source, long.MaxValue); else field.SetValue(source, long.MaxValue);
        var before = source.Snapshot(); var events = JsonSerializer.Serialize(source.EventHistory);
        Assert.False(source.PrepareSuccessorCandidateCommit(candidate!, out var prepared, out var reason));
        Assert.Null(prepared); Assert.Equal(CookingLevelLifecycleReason.GameplayInitializationFailed, reason);
        Assert.Equal(before, source.Snapshot()); Assert.Equal(events, JsonSerializer.Serialize(source.EventHistory));
        if (version) property.SetValue(source, original); else field.SetValue(source, original);
        Assert.True(source.PrepareSuccessorCandidateCommit(candidate!, out prepared, out reason));
        prepared!.Commit(); Assert.Equal(original + 1, version ? source.Version : (long)field.GetValue(source)!);
    }

    private static CookingLevelLifecycle Ended(CookingLevelOutcome outcome = CookingLevelOutcome.Success)
    {
        var content = CookingContentCatalog.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, CookingContentCatalog.ContentFileName)));
        var scope = new CookingLevelScope(new(new("prepared"), new("world"), new("match")), new(1), new("source"), 1);
        var source = new CookingLevelLifecycle(scope, content.Snapshot, new Factory(content));
        Assert.True(source.BeginPreparation(new(scope.Level, new("map"),
            new(new("layout"), content.Appliances.Keys.ToArray(), Array.Empty<DefinitionId>()), content.Identity)).Accepted);
        Assert.True(source.CompletePreparation().Accepted); Assert.True(source.Start().Accepted);
        Assert.True(source.BeginEnd(outcome).Accepted); Assert.True(source.CompleteEnd().Accepted);
        return source;
    }
    private sealed class Factory(CookingContent content) : ICookingLevelGameplayFactory
    {
        public CookingRecipeSimulation Create(CookingLevelScope scope, CookingConfigurationSnapshot configuration) =>
            new(CookingContentCatalog.BuildFixture(content, scope.MatchScope,
                new Dictionary<PlayerId, CookingPlayerConfig> { [new("chef")] = new(new("chef"),
                    new HashSet<string> { "cook" }, content.Appliances.Keys.Select(s => s.Value).ToHashSet()) }));
    }
}
