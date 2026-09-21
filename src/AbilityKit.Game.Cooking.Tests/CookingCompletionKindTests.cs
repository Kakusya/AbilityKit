using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

/// <summary>
/// 任务 <c>09-21-cooking-kitchen-loop-simulation</c> 的完成形态契约测试：
/// ConsumeInputs 与 RetainInputs 都经 fixed tick 原子提交，倒出至多生成一次产物，
/// 命令路径与 fixed-tick 路径共享同一产品 ID allocator。
/// </summary>
[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingCompletionKindTests
{
    private static readonly SessionId Session = new("completion-session");
    private static readonly WorldId World = new("completion-world");
    private static readonly MatchId Match = new("completion-match");
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
    public void F01_retain_inputs_completion_keeps_inputs_and_marks_the_pot_completed()
    {
        var simulation = CreateSimulation();
        var process = StartSoup(simulation);

        for (var frame = 1; frame <= 6; frame++)
            simulation.AdvanceFixedTick(LevelScope(), frame);

        Assert.Empty(simulation.Snapshot().Processes);
        var pot = Assert.Single(simulation.Snapshot().Items, item => item.Id == Pot);
        Assert.True(pot.ContainerCompleted);
        Assert.Equal(new[] { "beaten-1", "chopped-1" },
            simulation.ItemsInContainer(Pot).Select(item => item.Value).OrderBy(value => value, StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(simulation.Snapshot().Items, item => item.IsProduct);
    }

    [Fact]
    public void F02_pour_from_completed_pot_generates_the_product_exactly_once()
    {
        using var evidence = CreateEvidence("F02");
        var simulation = CreateSimulation();
        var process = StartSoup(simulation);
        for (var frame = 1; frame <= 6; frame++)
            simulation.AdvanceFixedTick(LevelScope(), frame);
        var completedPot = simulation.Snapshot().Items.Single(item => item.Id == Pot);

        var poured = Submit(simulation, evidence, "F02", Command(CookingRecipeOperation.Pour, "pour", item: Pot,
            container: Bowl, expectedVersion: completedPot.Version),
            "pouring a completed pot generates the product into the bowl exactly once");

        AssertAccepted(poured);
        var product = Assert.Single(simulation.Snapshot().Items, item => item.IsProduct);
        Assert.Equal(Soup, product.Definition);
        Assert.Equal(ItemLocation.Container(Bowl, "slot-0"), product.Location);
        Assert.Equal(new[] { product.Id }, simulation.ItemsInContainer(Bowl));
        var potAfter = Assert.Single(simulation.Snapshot().Items, item => item.Id == Pot);
        Assert.False(potAfter.ContainerCompleted);
        Assert.Empty(simulation.ItemsInContainer(Pot));

        var secondPour = Submit(simulation, evidence, "F02", Command(CookingRecipeOperation.Pour, "pour-again",
            item: Pot, container: Bowl, expectedVersion: potAfter.Version),
            "a second pour from the emptied pot cannot generate another product");
        AssertRejected(secondPour, CookingRecipeRejectionReason.ProductNotFound);
        Assert.Single(simulation.Snapshot().Items, item => item.IsProduct);
        AssertEvidence(evidence.Path, "F02", 2);
    }

    [Fact]
    public void F03_replayed_pour_identity_returns_the_cached_result_without_a_second_product()
    {
        var simulation = CreateSimulation();
        StartSoup(simulation);
        for (var frame = 1; frame <= 6; frame++)
            simulation.AdvanceFixedTick(LevelScope(), frame);
        var completedPot = simulation.Snapshot().Items.Single(item => item.Id == Pot);
        var command = Command(CookingRecipeOperation.Pour, "pour-once", item: Pot, container: Bowl,
            expectedVersion: completedPot.Version);

        var first = simulation.Submit(command);
        var replay = simulation.Submit(command);

        AssertAccepted(first);
        Assert.True(replay.IsDuplicate);
        Assert.Empty(replay.Events);
        Assert.Single(simulation.Snapshot().Items, item => item.IsProduct);
    }

    [Fact]
    public void F04_pour_rejects_a_target_that_does_not_accept_the_product()
    {
        using var evidence = CreateEvidence("F04");
        var simulation = CreateSimulation(targetAccepts: new HashSet<DefinitionId>());
        StartSoup(simulation);
        for (var frame = 1; frame <= 6; frame++)
            simulation.AdvanceFixedTick(LevelScope(), frame);
        var completedPot = simulation.Snapshot().Items.Single(item => item.Id == Pot);
        var before = simulation.Snapshot().CanonicalText();

        var rejected = Submit(simulation, evidence, "F04", Command(CookingRecipeOperation.Pour, "pour-rejected",
            item: Pot, container: Bowl, expectedVersion: completedPot.Version),
            "a bowl that does not declare the soup definition rejects the pour and keeps the pot completed");

        AssertRejected(rejected, CookingRecipeRejectionReason.ContainerRejectsItem);
        Assert.Equal(before, simulation.Snapshot().CanonicalText());
        Assert.True(simulation.Snapshot().Items.Single(item => item.Id == Pot).ContainerCompleted);
        AssertEvidence(evidence.Path, "F04", 1);
    }

    [Fact]
    public void F05_command_path_and_fixed_tick_path_share_one_product_allocator()
    {
        var allocator = new RecordingAllocator();
        var recording = CreateSimulation(productIdAllocator: allocator);

        // 命令路径完成一个切番茄工序。
        recording.AddItem(new ItemId("tomato-1"), Tomato, ItemLocation.Station(Board));
        AssertAccepted(recording.Submit(Command(CookingRecipeOperation.StartProcess, "chop-start",
            item: new ItemId("tomato-1"), station: Board, expectedVersion: 1)));
        var chopProcess = recording.Snapshot().Processes.Single();
        AssertAccepted(recording.Submit(Command(CookingRecipeOperation.AdvanceTicks, "chop-complete",
            process: chopProcess.Id, ticks: 2)));

        // fixed-tick 路径完成一个打蛋工序。
        PutIn(recording, new ItemId("egg-1"), Egg, Bowl);
        var bowlState = recording.Snapshot().Items.Single(item => item.Id == Bowl);
        AssertAccepted(recording.Submit(Command(CookingRecipeOperation.StartProcess, "beat-start",
            item: Bowl, expectedVersion: bowlState.Version)));
        recording.AdvanceFixedTick(LevelScope(), 1);
        recording.AdvanceFixedTick(LevelScope(), 2);

        // 倒出路径再生成一个成品。
        StartSoup(recording);
        for (var frame = 3; frame <= 8; frame++)
            recording.AdvanceFixedTick(LevelScope(), frame);
        var completedPot = recording.Snapshot().Items.Single(item => item.Id == Pot);
        AssertAccepted(recording.Submit(Command(CookingRecipeOperation.Pour, "soup-pour", item: Pot,
            container: Bowl, expectedVersion: completedPot.Version)));

        Assert.Equal(new long[] { 1, 2, 3 }, allocator.Requests);
        Assert.Equal(
            new[] { "product-1", "product-2", "product-3" },
            allocator.RequestedIds);
        Assert.Equal(3, recording.Snapshot().Items.Count(item => item.IsProduct));
    }

    [Fact]
    public void F06_allocator_fault_on_the_command_path_is_mutation_safe()
    {
        var simulation = CreateSimulation(productIdAllocator: new ThrowingAllocator());
        simulation.AddItem(new ItemId("tomato-1"), Tomato, ItemLocation.Station(Board));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.StartProcess, "chop-start",
            item: new ItemId("tomato-1"), station: Board, expectedVersion: 1)));
        var process = simulation.Snapshot().Processes.Single();
        var before = simulation.Snapshot().CanonicalText();
        var eventsBefore = simulation.EventHistory.ToArray();

        var error = Assert.Throws<InvalidOperationException>(() => simulation.Submit(
            Command(CookingRecipeOperation.AdvanceTicks, "chop-complete", process: process.Id, ticks: 2)));

        Assert.Equal("allocator-fault", error.Message);
        Assert.Equal(before, simulation.Snapshot().CanonicalText());
        Assert.Equal(eventsBefore, simulation.EventHistory);
    }

    private static ProcessId StartSoup(CookingRecipeSimulation simulation)
    {
        PutIn(simulation, new ItemId("chopped-1"), ChoppedTomato, Pot);
        PutIn(simulation, new ItemId("beaten-1"), BeatenEgg, Pot);
        var potState = simulation.Snapshot().Items.Single(item => item.Id == Pot);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.StartProcess, "soup-start", item: Pot,
            station: Stove, expectedVersion: potState.Version)));
        return simulation.Snapshot().Processes.Single().Id;
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

    private static CookingRecipeSimulation CreateSimulation(
        IReadOnlySet<DefinitionId>? targetAccepts = null,
        ICookingProductIdAllocator? productIdAllocator = null)
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
                new CookingItemContainerCapability(2, targetAccepts ?? new HashSet<DefinitionId> { Soup, Egg, BeatenEgg })),
            [PotDefinition] = new(PotDefinition, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new CookingItemContainerCapability(4, new HashSet<DefinitionId> { ChoppedTomato, BeatenEgg })),
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
            new CookingRecipeFixture(scope, players, items, appliances, recipes), productIdAllocator);
        simulation.AddItem(Pot, PotDefinition, ItemLocation.Station(Stove));
        simulation.AddItem(Bowl, BowlDefinition, ItemLocation.Station(Counter));
        return simulation;
    }

    private static CookingLevelScope LevelScope() => new(
        new CookingScope(Session, World, Match), new RestaurantRuntimeId(1), new LevelId("completion-level"), 1);

    private static CookingRecipeCommand Command(CookingRecipeOperation operation, string commandId, RecipeId? recipe = null,
        ProcessId? process = null, ItemId? item = null, StationSlotId? station = null, ItemId? container = null,
        int expectedVersion = 0, int ticks = 0) =>
        new(new CookingScope(Session, World, Match), 10, Player, new RecipeCommandId(commandId), operation,
            recipe, process, item, station, container, null, expectedVersion, ticks);

    private static CookingRecipeCommandResult Submit(CookingRecipeSimulation simulation, EvidenceScope evidence, string testId,
        CookingRecipeCommand command, string assertionSummary)
    {
        var before = simulation.Snapshot();
        var result = simulation.Submit(command);
        CookingRecipeAcceptanceEvidenceWriter.Append(evidence.Path, new CookingRecipeAcceptanceEvidence(
            testId, "p2-completion-kinds", command, simulation.LogicalTick, result.Outcome.ToString(), result.Reason.ToString(),
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
        Assert.All(records, record => Assert.Equal(testId, record.TestId));
    }


    private sealed class RecordingAllocator : ICookingProductIdAllocator
    {
        public List<long> Requests { get; } = new();
        public List<string> RequestedIds { get; } = new();

        public ItemId GetProductId(long productSequence)
        {
            Requests.Add(productSequence);
            var id = new ItemId($"product-{productSequence}");
            RequestedIds.Add(id.Value);
            return id;
        }
    }

    private sealed class ThrowingAllocator : ICookingProductIdAllocator
    {
        public ItemId GetProductId(long productSequence) => throw new InvalidOperationException("allocator-fault");
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
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AbilityKit.Game.Cooking.Tests", "completion");
            _directory = System.IO.Path.Combine(root, testId, Guid.NewGuid().ToString("N"));
            Path = System.IO.Path.Combine(_directory, "completion-acceptance.jsonl");
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
