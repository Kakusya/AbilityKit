using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

/// <summary>
/// 任务 <c>09-22-cooking-level-progression</c>：装修换工位、解锁工位、煮制加速，
/// 以及只有锁定之后才写的大关检查点。
/// </summary>
[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingMajorProgressTests
{
    [Fact]
    public void P01_cook_faster_shortens_only_a_new_soup_and_does_not_stack()
    {
        var progress = new CookingMajorProgress();
        var soup = new RecipeId("tomato-egg-soup");
        var chop = new RecipeId("chop-tomato");

        Assert.Equal(6, progress.CookTicks(soup, 6));
        Assert.Equal(CookingMajorProgressReason.None, progress.EnableCookFaster().Reason);
        Assert.Equal(CookingMajorProgressReason.Duplicate, progress.EnableCookFaster().Reason);
        Assert.Equal(3, progress.CookTicks(soup, 6));
        Assert.Equal(1, progress.CookTicks(soup, 1));
        Assert.Equal(2, progress.CookTicks(chop, 2));
    }

    [Fact]
    public void P02_a_success_checkpoint_is_reread_and_a_tampered_file_is_rejected()
    {
        using var directory = new TempDirectory();
        var match = new CookingScope(new SessionId("session"), new WorldId("world"), new MatchId("match"));
        var progress = new CookingMajorProgress();
        Assert.True(progress.ChooseDecoration(new[]
        {
            new CookingStationReplacement(new StationSlotId("stove-a"), new StationSlotId("stove-b")),
        }).Accepted);
        Assert.True(progress.Unlock(new DefinitionId("oven-b")).Accepted);
        Assert.True(progress.EnableCookFaster().Accepted);
        progress.Lock();
        var kitchen = Kitchen();
        var store = new CookingMajorCheckpointStore(directory.Path);

        Assert.True(store.Write(match, progress, kitchen).Accepted);
        var read = new CookingMajorCheckpointStore(directory.Path).Read(match);
        Assert.True(read.Accepted);
        Assert.True(read.Progress!.CookFaster);
        Assert.Equal(new[] { new DefinitionId("oven-b") }, read.Progress.Unlocks);
        Assert.Equal(kitchen.CanonicalText(), read.KitchenCanonical);

        var path = Assert.Single(Directory.GetFiles(directory.Path, "major.checkpoint.json"));
        var original = File.ReadAllText(path);
        File.WriteAllText(path, original.Replace("oven-b", "oven-x", StringComparison.Ordinal));
        var tampered = new CookingMajorCheckpointStore(directory.Path).Read(match);
        Assert.Equal(CookingMajorProgressReason.IntegrityFailure, tampered.Reason);
        File.WriteAllText(path, original);
        Assert.Equal(kitchen.CanonicalText(), new CookingMajorCheckpointStore(directory.Path).Read(match).KitchenCanonical);
    }

    [Fact]
    public void P03_an_unlocked_progress_cannot_be_written()
    {
        using var directory = new TempDirectory();
        var match = new CookingScope(new SessionId("session"), new WorldId("world"), new MatchId("match"));
        var store = new CookingMajorCheckpointStore(directory.Path);
        var result = store.Write(match, new CookingMajorProgress(), Kitchen());
        Assert.Equal(CookingMajorProgressReason.InvalidState, result.Reason);
        Assert.Equal(CookingMajorProgressReason.Missing, store.Read(match).Reason);
    }

    [Fact]
    public void P04_station_migration_keeps_elapsed_ticks_and_a_conflict_changes_nothing()
    {
        var content = CookingContentCatalog.Load(File.ReadAllText(ContentPath()));
        var scope = new CookingScope(new SessionId("session"), new WorldId("world"), new MatchId("match"));
        var player = new PlayerId("chef");
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [player] = new(player, new HashSet<string> { "cook" },
                new HashSet<string> { "board-a", "stove-a", "oven-a", "counter-a" }),
        };
        var simulation = new CookingRecipeSimulation(CookingContentCatalog.BuildFixture(content, scope, players));
        CookingContentCatalog.ApplyStandardInitialSupply(simulation, content);
        var slice = new ItemId("bread-slice-1");
        var version = simulation.Snapshot().Items.Single(item => item.Id == slice).Version;
        Assert.Equal(CookingRecipeOutcome.Accepted, simulation.Submit(new CookingRecipeCommand(
            scope, 1, player, new RecipeCommandId("pickup"), CookingRecipeOperation.Pickup,
            null, null, slice, null, null, null, version, 0)).Outcome);
        version = simulation.Snapshot().Items.Single(item => item.Id == slice).Version;
        Assert.Equal(CookingRecipeOutcome.Accepted, simulation.Submit(new CookingRecipeCommand(
            scope, 2, player, new RecipeCommandId("drop"), CookingRecipeOperation.Drop,
            null, null, slice, new StationSlotId("oven-a"), null, null, version, 0)).Outcome);
        version = simulation.Snapshot().Items.Single(item => item.Id == slice).Version;
        Assert.Equal(CookingRecipeOutcome.Accepted, simulation.Submit(new CookingRecipeCommand(
            scope, 3, player, new RecipeCommandId("bake"), CookingRecipeOperation.StartProcess,
            new RecipeId("bake-bread"), null, slice, new StationSlotId("oven-a"), null, null, version, 0)).Outcome);
        var before = simulation.Snapshot().Processes.Single();

        var conflict = simulation.MigrateStations(new[]
        {
            new CookingStationReplacement(new StationSlotId("oven-a"), new StationSlotId("missing")),
        });
        Assert.Equal(CookingMajorProgressReason.UnknownChoice, conflict.Reason);
        Assert.Equal(before.Station, simulation.Snapshot().Processes.Single().Station);

        var moved = simulation.MigrateStations(new[]
        {
            new CookingStationReplacement(new StationSlotId("oven-a"), new StationSlotId("counter-a")),
        });
        var after = simulation.Snapshot().Processes.Single();
        Assert.True(moved.Accepted);
        Assert.Equal(new StationSlotId("counter-a"), after.Station);
        Assert.Equal(before.ElapsedTicks, after.ElapsedTicks);
        Assert.Equal(before.RequiredTicks, after.RequiredTicks);

        var placed = simulation.PlaceUnlock(content, new DefinitionId("bread-slice"));
        Assert.True(placed.Accepted);
        Assert.Contains(simulation.Snapshot().Items, item => item.Id == new ItemId("bread-slice-unlock-1"));
    }

    private static string ContentPath() =>
        Path.Combine(AppContext.BaseDirectory, CookingContentCatalog.ContentFileName);

    private static CookingRecipeCheckpoint Kitchen()
    {
        var scope = new CookingScope(new SessionId("session"), new WorldId("world"), new MatchId("match"));
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [new PlayerId("chef")] = new(new PlayerId("chef"), new HashSet<string> { "cook" }, new HashSet<string>()),
        };
        var simulation = new CookingRecipeSimulation(new CookingRecipeFixture(
            scope, players,
            new Dictionary<DefinitionId, CookingItemDefinition>(),
            new Dictionary<StationSlotId, CookingApplianceDefinition>(),
            new Dictionary<RecipeId, CookingRecipeDefinition>()));
        return simulation.ExportCheckpoint();
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cooking-major-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
