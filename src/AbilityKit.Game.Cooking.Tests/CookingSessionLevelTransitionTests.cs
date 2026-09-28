using AbilityKit.Game.Cooking.Session;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingSessionLevelTransitionTests
{
    private static readonly SessionId Session = new("transition-session");
    private static readonly WorldId World = new("transition-world");
    private static readonly MatchId Match = new("transition-match");
    private static readonly PlayerId HostPlayer = new("chef-a");
    private static readonly PlayerId ClientPlayer = new("chef-b");

    [Fact]
    public async Task T01_successful_transition_persists_choices_and_reaches_next_level_consensus()
    {
        var environment = CreateEnvironment();
        environment.Simulation.UpdateFrontOfHouseState(isClosing: true, isCompleted: true);
        using var directory = new TempDirectory();
        await using var host = new CookingSessionHost(
            environment.Simulation,
            environment.Descriptor,
            environment.Source,
            HostPlayer,
            ClientPlayer,
            content: environment.Content);
        await host.StartAsync();
        await using var client = new CookingSessionClient(ClientPlayer);
        await client.ConnectAndHandshakeAsync("127.0.0.1", host.Port);

        var request = TransitionRequest(environment.Source);
        var result = host.TransitionToNextLevel(request, new CookingMajorCheckpointStore(directory.Path));
        Assert.True(result.Accepted);
        await WaitForGeneration(client, result.Snapshot.Generation);

        Assert.Equal(request.TargetLevel, host.CurrentLevelScope);
        Assert.Equal(request.TargetLevel, client.CurrentLevelScope);
        Assert.Equal(host.LatestSessionSnapshot.Sha256(), client.LatestSessionProjection!.Sha256());
        Assert.True(client.LatestSessionProjection.Progress.CookFaster);
        Assert.Contains(new DefinitionId("bread-slice"), client.LatestSessionProjection.Progress.Unlocks);
        Assert.Equal(0, client.LatestProjection!.LogicalTick);
        Assert.Empty(client.LatestProjection.Orders);
        Assert.False(client.LatestProjection.IsClosing);
        Assert.False(client.LatestProjection.IsCompleted);
        Assert.Contains(client.LatestProjection.Items, item => item.Id == new ItemId("bread-slice-unlock-1"));
        Assert.True(new CookingMajorCheckpointStore(directory.Path).Read(environment.Source.MatchScope).Accepted);

        var duplicate = host.TransitionToNextLevel(request, new CookingMajorCheckpointStore(directory.Path));
        Assert.False(duplicate.Accepted);
        Assert.Equal(CookingLevelTransitionReason.Duplicate, duplicate.Reason);
    }

    [Fact]
    public async Task T02_stale_level_command_and_snapshot_do_not_mutate_the_next_level()
    {
        var environment = CreateEnvironment();
        environment.Simulation.UpdateFrontOfHouseState(isClosing: true, isCompleted: true);
        using var directory = new TempDirectory();
        await using var host = new CookingSessionHost(
            environment.Simulation,
            environment.Descriptor,
            environment.Source,
            HostPlayer,
            ClientPlayer,
            content: environment.Content);
        await host.StartAsync();
        await using var client = new CookingSessionClient(ClientPlayer);
        await client.ConnectAndHandshakeAsync("127.0.0.1", host.Port);
        var oldProjection = client.LatestSessionProjection!;

        var transitioned = host.TransitionToNextLevel(
            TransitionRequest(environment.Source),
            new CookingMajorCheckpointStore(directory.Path));
        Assert.True(transitioned.Accepted);
        await WaitForGeneration(client, transitioned.Snapshot.Generation);
        var before = host.LatestSessionSnapshot.Sha256();

        var staleCommand = await client.SendCommandAsync(
            CookingRecipeOperation.Pickup,
            new ItemId("tomato-1"),
            levelScope: environment.Source);
        Assert.Equal(CookingRecipeOutcome.Rejected, staleCommand.Outcome);
        Assert.Equal(CookingRecipeRejectionReason.ScopeMismatch, staleCommand.Reason);
        Assert.Equal(before, host.LatestSessionSnapshot.Sha256());

        var staleSnapshot = oldProjection with { Sequence = host.LatestSessionSnapshot.Sequence + 100 };
        Assert.False(client.TryApplySessionSnapshot(staleSnapshot, staleSnapshot.Sha256()));
        Assert.False(client.TryApplySessionSnapshot(
            client.LatestSessionProjection!,
            client.LatestSessionProjection!.Sha256()));
        Assert.Equal(before, client.LatestSessionProjection!.Sha256());
    }

    [Fact]
    public async Task T03_checkpoint_failure_keeps_source_level_and_client_projection()
    {
        var environment = CreateEnvironment();
        environment.Simulation.UpdateFrontOfHouseState(isClosing: true, isCompleted: true);
        using var directory = new TempDirectory();
        var invalidRoot = Path.Combine(directory.Path, "not-a-directory");
        File.WriteAllText(invalidRoot, "occupied");
        await using var host = new CookingSessionHost(
            environment.Simulation,
            environment.Descriptor,
            environment.Source,
            HostPlayer,
            ClientPlayer,
            content: environment.Content);
        await host.StartAsync();
        await using var client = new CookingSessionClient(ClientPlayer);
        await client.ConnectAndHandshakeAsync("127.0.0.1", host.Port);
        var beforeHost = host.LatestSessionSnapshot.Sha256();
        var beforeClient = client.LatestSessionProjection!.Sha256();

        var result = host.TransitionToNextLevel(
            TransitionRequest(environment.Source),
            new CookingMajorCheckpointStore(invalidRoot));

        Assert.False(result.Accepted);
        Assert.Equal(CookingLevelTransitionReason.CheckpointWriteFailed, result.Reason);
        Assert.Equal(environment.Source, host.CurrentLevelScope);
        Assert.Equal(beforeHost, host.LatestSessionSnapshot.Sha256());
        Assert.Equal(beforeClient, client.LatestSessionProjection!.Sha256());
        Assert.DoesNotContain(host.LatestSnapshot.Items, item => item.Id == new ItemId("bread-slice-unlock-1"));
    }

    [Fact]
    public async Task T04_reconnect_in_next_level_receives_current_full_baseline()
    {
        var environment = CreateEnvironment();
        environment.Simulation.UpdateFrontOfHouseState(isClosing: true, isCompleted: true);
        using var directory = new TempDirectory();
        await using var host = new CookingSessionHost(
            environment.Simulation,
            environment.Descriptor,
            environment.Source,
            HostPlayer,
            ClientPlayer,
            content: environment.Content);
        await host.StartAsync();
        await using var client = new CookingSessionClient(ClientPlayer);
        await client.ConnectAndHandshakeAsync("127.0.0.1", host.Port);
        var token = client.ReconnectToken;

        var transitioned = host.TransitionToNextLevel(
            TransitionRequest(environment.Source),
            new CookingMajorCheckpointStore(directory.Path));
        Assert.True(transitioned.Accepted);
        await WaitForGeneration(client, transitioned.Snapshot.Generation);
        client.Disconnect();
        await client.ReconnectAsync("127.0.0.1", host.Port);

        Assert.Equal(token, client.ReconnectToken);
        Assert.Equal(host.CurrentLevelScope, client.CurrentLevelScope);
        Assert.Equal(host.LatestSessionSnapshot.Sha256(), client.LatestSessionProjection!.Sha256());
    }

    private static CookingLevelTransitionRequest TransitionRequest(CookingLevelScope source) => new(
        "transition-1",
        source,
        new CookingLevelScope(source.MatchScope, source.RestaurantRuntime, new LevelId("transition-level-2"), 2),
        new[] { new CookingStationReplacement(new StationSlotId("oven-a"), new StationSlotId("counter-a")) },
        new[] { new DefinitionId("bread-slice") },
        EnableCookFaster: true);

    private static async Task WaitForGeneration(CookingSessionClient client, long generation)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            if (client.LatestSessionProjection?.Generation == generation)
                return;
            await Task.Delay(20);
        }
        Assert.Equal(generation, client.LatestSessionProjection?.Generation);
    }

    private static TestEnvironment CreateEnvironment()
    {
        var scope = new CookingScope(Session, World, Match);
        var source = new CookingLevelScope(scope, new RestaurantRuntimeId(1), new LevelId("transition-level-1"), 1);
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [HostPlayer] = Player(HostPlayer),
            [ClientPlayer] = Player(ClientPlayer),
        };
        var content = CookingContentCatalog.Load(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, CookingContentCatalog.ContentFileName)));
        var simulation = new CookingRecipeSimulation(
            CookingContentCatalog.BuildFixture(content, scope, players, "clean-pool"), null, null);
        CookingContentCatalog.ApplyStandardInitialSupply(simulation, content);
        var descriptor = new CookingSessionDescriptor(
            scope,
            1,
            new CookingProtocolIdentity("cooking-session", 1, 1),
            "transition-config-v1",
            new HashSet<string>(StringComparer.Ordinal) { "cook" },
            new Dictionary<string, string>());
        return new TestEnvironment(simulation, content, descriptor, source);
    }

    private static CookingPlayerConfig Player(PlayerId player) => new(
        player,
        new HashSet<string>(StringComparer.Ordinal) { "cook" },
        new HashSet<string>(StringComparer.Ordinal) { "board-a", "board-b", "stove-a", "oven-a", "counter-a" });

    private sealed record TestEnvironment(
        CookingRecipeSimulation Simulation,
        CookingContent Content,
        CookingSessionDescriptor Descriptor,
        CookingLevelScope Source);

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "abilitykit-cooking-transition-" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
