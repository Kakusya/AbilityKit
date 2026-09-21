using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

/// <summary>
/// 任务 <c>09-21-cooking-kitchen-loop-simulation</c> 的进程锚点契约测试：
/// 容器锚定多输入匹配、免工位加工、锁输入、端走继续与 fixed-tick 白名单腐败检测。
/// </summary>
[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingProcessAnchorTests
{
    private static readonly SessionId Session = new("anchor-session");
    private static readonly WorldId World = new("anchor-world");
    private static readonly MatchId Match = new("anchor-match");
    private static readonly PlayerId Player = new("chef-a");
    private static readonly StationSlotId Stove = new("stove-a");
    private static readonly StationSlotId Board = new("board-a");
    private static readonly StationSlotId Counter = new("counter-a");
    private static readonly DefinitionId Tomato = new("tomato");
    private static readonly DefinitionId ChoppedTomato = new("chopped-tomato");
    private static readonly DefinitionId Egg = new("egg");
    private static readonly DefinitionId BeatenEgg = new("beaten-egg");
    private static readonly DefinitionId Water = new("water");
    private static readonly DefinitionId Soup = new("tomato-egg-soup");
    private static readonly DefinitionId BowlDefinition = new("bowl");
    private static readonly DefinitionId PotDefinition = new("pot");
    private static readonly RecipeId ChopRecipe = new("chop-tomato");
    private static readonly RecipeId BeatRecipe = new("beat-egg");
    private static readonly RecipeId SoupRecipe = new("tomato-egg-soup");
    private static readonly ItemId Pot = new("pot-1");
    private static readonly ItemId Bowl = new("bowl-1");

    [Fact]
    public void P01_container_anchored_start_matches_the_contents_set_and_rejects_ambiguity()
    {
        var simulation = CreateSimulation();
        PutIn(simulation, new ItemId("chopped-1"), ChoppedTomato, Pot);
        PutIn(simulation, new ItemId("beaten-1"), BeatenEgg, Pot);
        var potState = simulation.Snapshot().Items.Single(item => item.Id == Pot);

        var started = simulation.Submit(Command(CookingRecipeOperation.StartProcess, "soup-start", item: Pot,
            station: Stove, expectedVersion: potState.Version));

        AssertAccepted(started);
        var process = Assert.Single(simulation.Snapshot().Processes);
        Assert.Equal(SoupRecipe, process.Recipe);
        Assert.Equal(Stove, process.Station);
        Assert.Equal(Pot, process.Anchor);
        Assert.True(process.ElapsedTicks == 0);
    }

    [Fact]
    public void P02_station_less_beating_starts_from_a_held_bowl_and_keeps_the_liquid_in_the_bowl()
    {
        using var evidence = CreateEvidence("P02");
        var simulation = CreateSimulation();
        PutIn(simulation, new ItemId("egg-1"), Egg, Bowl);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup-bowl", item: Bowl,
            expectedVersion: 1)));
        var heldBowl = simulation.Snapshot().Items.Single(item => item.Id == Bowl);

        var started = Submit(simulation, evidence, "P02", Command(CookingRecipeOperation.StartProcess, "beat-start",
            item: Bowl, expectedVersion: heldBowl.Version),
            "beating eggs needs no station: the held bowl anchors the process");

        AssertAccepted(started);
        var process = Assert.Single(simulation.Snapshot().Processes);
        Assert.Equal(BeatRecipe, process.Recipe);
        Assert.Null(process.Station);

        var first = simulation.AdvanceFixedTick(LevelScope(), 1);
        Assert.Single(first.Processes);
        Assert.False(first.Processes[0].Completed);
        var second = simulation.AdvanceFixedTick(LevelScope(), 2);
        Assert.True(Assert.Single(second.Processes).Completed);

        Assert.Empty(simulation.Snapshot().Processes);
        var liquid = Assert.Single(simulation.Snapshot().Items, item => item.IsProduct);
        Assert.Equal(BeatenEgg, liquid.Definition);
        Assert.Equal(ItemLocation.Container(Bowl, "slot-0"), liquid.Location);
        Assert.DoesNotContain(simulation.Snapshot().Items, item => item.Id == new ItemId("egg-1"));
        AssertEvidence(evidence.Path, "P02", 1);
    }

    [Fact]
    public void P03_station_requirement_comes_from_the_recipe_definition()
    {
        var simulation = CreateSimulation();
        PutIn(simulation, new ItemId("egg-1"), Egg, Bowl);
        var bowlState = simulation.Snapshot().Items.Single(item => item.Id == Bowl);

        var withStation = simulation.Submit(Command(CookingRecipeOperation.StartProcess, "beat-with-station",
            item: Bowl, station: Stove, expectedVersion: bowlState.Version));
        AssertRejected(withStation, CookingRecipeRejectionReason.ApplianceCapabilityMismatch);

        simulation.AddItem(new ItemId("tomato-1"), Tomato, ItemLocation.Station(Board));
        var tomatoState = simulation.Snapshot().Items.Single(item => item.Id == new ItemId("tomato-1"));
        var withoutStation = simulation.Submit(Command(CookingRecipeOperation.StartProcess, "chop-without-station",
            item: new ItemId("tomato-1"), expectedVersion: tomatoState.Version));
        AssertRejected(withoutStation, CookingRecipeRejectionReason.ApplianceNotFound);
    }

    [Fact]
    public void P04_cooking_pot_can_be_carried_away_and_the_process_keeps_its_progress()
    {
        using var evidence = CreateEvidence("P04");
        var simulation = CreateSimulation();
        PutIn(simulation, new ItemId("chopped-1"), ChoppedTomato, Pot);
        PutIn(simulation, new ItemId("beaten-1"), BeatenEgg, Pot);
        var potState = simulation.Snapshot().Items.Single(item => item.Id == Pot);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.StartProcess, "soup-start", item: Pot,
            station: Stove, expectedVersion: potState.Version)));

        simulation.AdvanceFixedTick(LevelScope(), 1);
        Assert.Equal(1, simulation.Snapshot().Processes.Single().ElapsedTicks);

        var carried = Submit(simulation, evidence, "P04", Command(CookingRecipeOperation.Pickup, "carry-pot",
            item: Pot, expectedVersion: potState.Version),
            "the cooking pot is the process container anchor and may be carried away");

        AssertAccepted(carried);
        Assert.Equal(Pot, simulation.ItemInHand(Player));
        Assert.Equal(1, simulation.Snapshot().Processes.Single().ElapsedTicks);

        simulation.AdvanceFixedTick(LevelScope(), 2);
        simulation.AdvanceFixedTick(LevelScope(), 3);
        simulation.AdvanceFixedTick(LevelScope(), 4);
        Assert.Equal(4, simulation.Snapshot().Processes.Single().ElapsedTicks);
        AssertEvidence(evidence.Path, "P04", 1);
    }

    [Fact]
    public void P05_pot_contents_stay_locked_while_the_process_runs()
    {
        using var evidence = CreateEvidence("P05");
        var simulation = CreateSimulation();
        PutIn(simulation, new ItemId("chopped-1"), ChoppedTomato, Pot);
        PutIn(simulation, new ItemId("beaten-1"), BeatenEgg, Pot);
        var potState = simulation.Snapshot().Items.Single(item => item.Id == Pot);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.StartProcess, "soup-start", item: Pot,
            station: Stove, expectedVersion: potState.Version)));
        var before = simulation.Snapshot().CanonicalText();

        var takeOut = Submit(simulation, evidence, "P05", Command(CookingRecipeOperation.TakeOut, "take-out",
            item: new ItemId("chopped-1"), container: Pot, expectedVersion: 2),
            "a locked pot content cannot be taken out while cooking runs");
        AssertRejected(takeOut, CookingRecipeRejectionReason.ItemStale);

        var pour = Submit(simulation, evidence, "P05", Command(CookingRecipeOperation.Pour, "pour",
            item: Pot, container: Bowl, expectedVersion: potState.Version),
            "a cooking pot cannot be poured while the process runs");
        AssertRejected(pour, CookingRecipeRejectionReason.ItemStale);

        Assert.Equal(before, simulation.Snapshot().CanonicalText());
        Assert.Single(simulation.Snapshot().Processes);
        AssertEvidence(evidence.Path, "P05", 2);
    }

    [Fact]
    public void P06_locked_input_relocated_outside_the_whitelist_is_detected_by_fixed_tick()
    {
        var simulation = CreateSimulation();
        simulation.AddItem(new ItemId("tomato-1"), Tomato, ItemLocation.Station(Board));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.StartProcess, "chop-start",
            item: new ItemId("tomato-1"), station: Board, expectedVersion: 1)));
        simulation.RelocateLockedInputForTesting(new ItemId("tomato-1"), ItemLocation.Hand(Player));
        var before = simulation.Snapshot().CanonicalText();
        var eventsBefore = simulation.EventHistory.ToArray();

        var error = Assert.Throws<InvalidOperationException>(() => simulation.AdvanceFixedTick(LevelScope(), 1));

        Assert.Contains("unsupported location", error.Message, StringComparison.Ordinal);
        Assert.Equal(before, simulation.Snapshot().CanonicalText());
        Assert.Equal(eventsBefore, simulation.EventHistory);
        Assert.Empty(simulation.TickEventHistory);
    }

    [Fact]
    public void P07_removed_locked_input_is_detected_by_fixed_tick()
    {
        var simulation = CreateSimulation();
        simulation.AddItem(new ItemId("tomato-1"), Tomato, ItemLocation.Station(Board));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.StartProcess, "chop-start",
            item: new ItemId("tomato-1"), station: Board, expectedVersion: 1)));
        simulation.RemoveLockedInputForTesting(new ItemId("tomato-1"));
        var before = simulation.Snapshot().CanonicalText();

        var error = Assert.Throws<InvalidOperationException>(() => simulation.AdvanceFixedTick(LevelScope(), 1));

        Assert.Contains("is unavailable", error.Message, StringComparison.Ordinal);
        Assert.Equal(before, simulation.Snapshot().CanonicalText());
        Assert.Empty(simulation.TickEventHistory);
    }

    [Fact]
    public void P08_pot_content_that_left_its_container_is_detected_by_fixed_tick()
    {
        var simulation = CreateSimulation();
        PutIn(simulation, new ItemId("chopped-1"), ChoppedTomato, Pot);
        PutIn(simulation, new ItemId("beaten-1"), BeatenEgg, Pot);
        var potState = simulation.Snapshot().Items.Single(item => item.Id == Pot);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.StartProcess, "soup-start", item: Pot,
            station: Stove, expectedVersion: potState.Version)));
        simulation.RelocateLockedInputForTesting(new ItemId("chopped-1"), ItemLocation.World("floor"));
        var before = simulation.Snapshot().CanonicalText();

        var error = Assert.Throws<InvalidOperationException>(() => simulation.AdvanceFixedTick(LevelScope(), 1));

        Assert.Contains("left container", error.Message, StringComparison.Ordinal);
        Assert.Equal(before, simulation.Snapshot().CanonicalText());
        Assert.Empty(simulation.TickEventHistory);
    }

    [Fact]
    public void P09_explicit_recipe_validates_the_whole_container_contents()
    {
        var simulation = CreateSimulation();
        PutIn(simulation, new ItemId("chopped-1"), ChoppedTomato, Pot);
        PutIn(simulation, new ItemId("chopped-2"), ChoppedTomato, Pot);
        var potState = simulation.Snapshot().Items.Single(item => item.Id == Pot);

        var rejected = simulation.Submit(Command(CookingRecipeOperation.StartProcess, "soup-explicit",
            recipe: SoupRecipe, item: Pot, station: Stove, expectedVersion: potState.Version));

        AssertRejected(rejected, CookingRecipeRejectionReason.RecipeNotMatched);
        Assert.Empty(simulation.Snapshot().Processes);
    }

    private static void PutIn(CookingRecipeSimulation simulation, ItemId item, DefinitionId definition, ItemId container)
    {
        simulation.AddWorldIngredient(item, definition, $"spawn-{item.Value}");
        var state = simulation.Snapshot().Items.Single(candidate => candidate.Id == item);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, $"pickup-{item.Value}",
            item: item, expectedVersion: state.Version)));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.PutIn, $"put-in-{item.Value}",
            item: item, container: container, expectedVersion: state.Version + 1)));
    }

    private static CookingRecipeSimulation CreateSimulation()
    {
        var scope = new CookingScope(Session, World, Match);
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [Player] = new(Player, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new HashSet<string>(StringComparer.Ordinal) { Stove.Value, Board.Value, Counter.Value }),
        };
        var items = new Dictionary<DefinitionId, CookingItemDefinition>
        {
            [Tomato] = new(Tomato, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [ChoppedTomato] = new(ChoppedTomato, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [Egg] = new(Egg, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [BeatenEgg] = new(BeatenEgg, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [Water] = new(Water, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [Soup] = new(Soup, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [BowlDefinition] = new(BowlDefinition, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new CookingItemContainerCapability(2, new HashSet<DefinitionId> { Egg, BeatenEgg, ChoppedTomato, Soup })),
            [PotDefinition] = new(PotDefinition, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new CookingItemContainerCapability(4, new HashSet<DefinitionId> { ChoppedTomato, BeatenEgg, Soup })),
        };
        var appliances = new Dictionary<StationSlotId, CookingApplianceDefinition>
        {
            [Stove] = new(Stove, new HashSet<string>(StringComparer.Ordinal) { "heat" }),
            [Board] = new(Board, new HashSet<string>(StringComparer.Ordinal) { "cut" }),
            [Counter] = new(Counter, new HashSet<string>(StringComparer.Ordinal)),
        };
        var recipes = new Dictionary<RecipeId, CookingRecipeDefinition>
        {
            [ChopRecipe] = new(ChopRecipe, new[] { Tomato }, ChoppedTomato, new ProcessId("chop-process"), "cut", 2),
            [BeatRecipe] = new(BeatRecipe, new[] { Egg }, BeatenEgg, new ProcessId("beat-process"), "beat", 2,
                Completion: CookingRecipeCompletionKind.ConsumeInputs, RequiresStation: false),
            [SoupRecipe] = new(SoupRecipe, new[] { ChoppedTomato, BeatenEgg }, Soup, new ProcessId("soup-process"),
                "heat", 6, new[] { Water }, CookingRecipeCompletionKind.RetainInputs),
        };
        var simulation = new CookingRecipeSimulation(
            new CookingRecipeFixture(scope, players, items, appliances, recipes));
        simulation.AddItem(Pot, PotDefinition, ItemLocation.Station(Stove));
        simulation.AddItem(Bowl, BowlDefinition, ItemLocation.Station(Counter));
        return simulation;
    }

    private static CookingLevelScope LevelScope() => new(
        new CookingScope(Session, World, Match), new RestaurantRuntimeId(1), new LevelId("anchor-level"), 1);

    private static CookingRecipeCommand Command(CookingRecipeOperation operation, string commandId, RecipeId? recipe = null,
        ItemId? item = null, StationSlotId? station = null, ItemId? container = null, int expectedVersion = 0) =>
        new(new CookingScope(Session, World, Match), 10, Player, new RecipeCommandId(commandId), operation,
            recipe, null, item, station, container, null, expectedVersion, 0);

    private static CookingRecipeCommandResult Submit(CookingRecipeSimulation simulation, EvidenceScope evidence, string testId,
        CookingRecipeCommand command, string assertionSummary)
    {
        var before = simulation.Snapshot();
        var result = simulation.Submit(command);
        CookingRecipeAcceptanceEvidenceWriter.Append(evidence.Path, new CookingRecipeAcceptanceEvidence(
            testId, "p2-process-anchor", command, simulation.LogicalTick, result.Outcome.ToString(), result.Reason.ToString(),
            result.IsDuplicate, result.Events, before.Sha256(), simulation.Snapshot().Sha256(), assertionSummary,
            "dotnet test AbilityKit.Game.Cooking.Tests", DateTimeOffset.UtcNow.ToString("O")));
        if (result.Outcome == CookingRecipeOutcome.Rejected)
            Assert.Equal(before.CanonicalText(), simulation.Snapshot().CanonicalText());
        return result;
    }

    private static void AssertAccepted(CookingRecipeCommandResult result)
    {
        Assert.Equal(CookingRecipeOutcome.Accepted, result.Outcome);
        Assert.Equal(CookingRecipeRejectionReason.None, result.Reason);
        Assert.Single(result.Events);
    }

    private static void AssertRejected(CookingRecipeCommandResult result, CookingRecipeRejectionReason reason)
    {
        Assert.Equal(CookingRecipeOutcome.Rejected, result.Outcome);
        Assert.Equal(reason, result.Reason);
        Assert.Empty(result.Events);
    }

    private static void AssertEvidence(string path, string testId, int recordCount)
    {
        var records = CookingRecipeAcceptanceEvidenceWriter.ReadAll(path);
        Assert.Equal(recordCount, records.Count);
        Assert.All(records, record =>
        {
            Assert.Equal(testId, record.TestId);
            Assert.False(string.IsNullOrWhiteSpace(record.BeforeStateHash));
            Assert.False(string.IsNullOrWhiteSpace(record.AfterStateHash));
        });
    }


    private sealed class EvidenceScope : IDisposable
    {
        private readonly string _directory;
        private readonly bool _keepArtifacts;

        public EvidenceScope(string testId)
        {
            var requestedRoot = Environment.GetEnvironmentVariable("COOKING_RECIPE_EVIDENCE_DIRECTORY");
            _keepArtifacts = !string.IsNullOrWhiteSpace(requestedRoot);
            var root = _keepArtifacts ? System.IO.Path.GetFullPath(requestedRoot!) :
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AbilityKit.Game.Cooking.Tests", "anchor");
            _directory = System.IO.Path.Combine(root, testId, Guid.NewGuid().ToString("N"));
            Path = System.IO.Path.Combine(_directory, "anchor-acceptance.jsonl");
        }

        public string Path { get; }

        public void Dispose()
        {
            if (!_keepArtifacts && Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }
    }

    private static EvidenceScope CreateEvidence(string testId) => new(testId);
}
