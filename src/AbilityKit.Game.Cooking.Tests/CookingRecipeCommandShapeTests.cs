using System.Text.Json;
using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

/// <summary>
/// 任务 <c>09-21-cooking-kitchen-loop-contracts</c> 的命令形状契约测试：
/// StartProcess 不再强制显式 RecipeId，多输入靠“多条放入 + 一次启动”累积，
/// 匹配失败与歧义以结构化拒绝返回，不抛异常污染帧路径。
/// </summary>
[Trait("Gate", "CookingRecipeLoop")]
public sealed class CookingRecipeCommandShapeTests
{
    private static readonly SessionId Session = new("shape-session");
    private static readonly WorldId World = new("shape-world");
    private static readonly MatchId Match = new("shape-match");
    private static readonly PlayerId Player = new("chef-a");
    private static readonly StationSlotId Station = new("stove-a");
    private static readonly OrderId Order = new("shape-order");
    private static readonly DefinitionId Tomato = new("tomato");
    private static readonly DefinitionId ChoppedTomato = new("chopped-tomato");
    private static readonly DefinitionId BeatenEgg = new("beaten-egg");
    private static readonly DefinitionId Soup = new("tomato-egg-soup");
    private static readonly DefinitionId Water = new("water");
    private static readonly DefinitionId PotDefinition = new("pot");
    private static readonly ItemId Pot = new("pot-1");
    private static readonly RecipeId SoupRecipe = new("tomato-egg-soup");
    private static readonly RecipeId ChopRecipe = new("chop-tomato");

    [Theory]
    [InlineData(CookingRecipeOperation.ClaimFrontWork, 17)]
    [InlineData(CookingRecipeOperation.ContinueFrontWork, 18)]
    [InlineData(CookingRecipeOperation.StopFrontWork, 19)]
    public void Front_work_payload_has_one_work_id_and_no_kitchen_or_movement_fields(CookingRecipeOperation operation, int numeric)
    {
        Assert.Equal(numeric, (int)operation);
        var command = Command(operation, "front-shape") with { WorldAnchor = "inquiry:customer-1" };
        Assert.True(CookingRecipeCommandValidation.IsWellFormed(command));
        Assert.False(CookingRecipeCommandValidation.IsWellFormed(command with { WorldAnchor = null }));
        Assert.False(CookingRecipeCommandValidation.IsWellFormed(command with { WorldAnchor = " " }));
        Assert.False(CookingRecipeCommandValidation.IsWellFormed(command with { Item = new ItemId("item") }));
        Assert.False(CookingRecipeCommandValidation.IsWellFormed(command with { Station = Station }));
        Assert.False(CookingRecipeCommandValidation.IsWellFormed(command with { ExpectedItemVersion = 1 }));
        Assert.False(CookingRecipeCommandValidation.IsWellFormed(command with { TickCount = 1 }));
        Assert.False(CookingRecipeCommandValidation.IsWellFormed(command with { FacingX = 1 }));
    }
    [Fact]
    public void S01_well_formed_start_process_accepts_missing_recipe()
    {
        using var evidence = CreateEvidence("S01");
        var recipeLess = Command(CookingRecipeOperation.StartProcess, "no-recipe", item: new ItemId("ingredient-a"),
            station: Station, expectedVersion: 1);
        var withRecipe = Command(CookingRecipeOperation.StartProcess, "with-recipe", recipe: SoupRecipe,
            item: new ItemId("ingredient-a"), station: Station, expectedVersion: 1);

        Assert.True(CookingRecipeCommandValidation.IsWellFormed(recipeLess));
        Assert.True(CookingRecipeCommandValidation.IsWellFormed(withRecipe));

        Submit(CreateSimulation(), evidence, "S01", recipeLess,
            "StartProcess is well formed without an explicit RecipeId, preserving the explicit-recipe mode");
    }

    [Fact]
    public void S02_well_formed_start_process_requires_item_and_version_but_not_station()
    {
        using var evidence = CreateEvidence("S02");
        var missingItem = Command(CookingRecipeOperation.StartProcess, "no-item", station: Station, expectedVersion: 1);
        var missingVersion = Command(CookingRecipeOperation.StartProcess, "no-version", item: new ItemId("ingredient-a"),
            station: Station);
        var withTicks = Command(CookingRecipeOperation.StartProcess, "with-ticks", item: new ItemId("ingredient-a"),
            station: Station, expectedVersion: 1, ticks: 1);
        var stationLess = Command(CookingRecipeOperation.StartProcess, "station-less", item: new ItemId("ingredient-a"),
            expectedVersion: 1);

        Assert.False(CookingRecipeCommandValidation.IsWellFormed(missingItem));
        Assert.False(CookingRecipeCommandValidation.IsWellFormed(missingVersion));
        Assert.False(CookingRecipeCommandValidation.IsWellFormed(withTicks));
        // 免工位加工（打蛋）在 wire 上不携带工位；是否必须带工位由配方的 RequiresStation 在域内决定。
        Assert.True(CookingRecipeCommandValidation.IsWellFormed(stationLess));

        Submit(CreateSimulation(), evidence, "S02", missingItem,
            "relaxing Recipe does not relax Item, Station, ExpectedItemVersion or TickCount");
    }

    [Fact]
    public void S03_start_process_without_recipe_returns_structured_rejection_without_mutation()
    {
        using var evidence = CreateEvidence("S03");
        var simulation = CreateSimulation();
        var input = new ItemId("ingredient-a");
        simulation.AddItem(input, ChoppedTomato, ItemLocation.Station(Station));
        var before = simulation.Snapshot().CanonicalText();
        var eventsBefore = simulation.EventHistory.Count;

        var rejected = Submit(simulation, evidence, "S03", Command(CookingRecipeOperation.StartProcess, "no-recipe", item: input,
            station: Station, expectedVersion: 1), "recipe-less StartProcess is a structured rejection with no mutation and no exception");

        Assert.Equal(CookingRecipeOutcome.Rejected, rejected.Outcome);
        Assert.Equal(CookingRecipeRejectionReason.RecipeNotMatched, rejected.Reason);
        Assert.Empty(rejected.Events);
        Assert.Equal(before, simulation.Snapshot().CanonicalText());
        Assert.Equal(eventsBefore, simulation.EventHistory.Count);
        Assert.Empty(simulation.Snapshot().Processes);
    }

    [Fact]
    public void S04_explicit_recipe_accepts_only_declared_input_definitions()
    {
        using var evidence = CreateEvidence("S04");
        var simulation = CreateSimulation();
        var tomato = new ItemId("ingredient-tomato");
        simulation.AddItem(tomato, Tomato, ItemLocation.Station(Station));

        var accepted = Submit(simulation, evidence, "S04", Command(CookingRecipeOperation.StartProcess, "chop-accepted", recipe: ChopRecipe,
            item: tomato, station: Station, expectedVersion: 1), "explicit recipe accepts an input inside the declared input set");

        Assert.Equal(CookingRecipeOutcome.Accepted, accepted.Outcome);
        Assert.Single(simulation.Snapshot().Processes);
    }

    [Fact]
    public void S05_explicit_recipe_rejects_an_input_outside_the_declared_input_set()
    {
        using var evidence = CreateEvidence("S05");
        var simulation = CreateSimulation();
        var beaten = new ItemId("ingredient-beaten");
        simulation.AddItem(beaten, BeatenEgg, ItemLocation.Station(Station));
        var before = simulation.Snapshot().CanonicalText();

        var rejected = Submit(simulation, evidence, "S05", Command(CookingRecipeOperation.StartProcess, "chop-rejected", recipe: ChopRecipe,
            item: beaten, station: Station, expectedVersion: 1), "explicit recipe rejects an input outside the declared input set");

        Assert.Equal(CookingRecipeOutcome.Rejected, rejected.Outcome);
        Assert.Equal(CookingRecipeRejectionReason.RecipeNotMatched, rejected.Reason);
        Assert.Equal(before, simulation.Snapshot().CanonicalText());
        Assert.Empty(simulation.Snapshot().Processes);
    }

    [Fact]
    public void S06_unknown_explicit_recipe_still_reports_recipe_not_found()
    {
        var simulation = CreateSimulation();
        var chopped = new ItemId("ingredient-chopped");
        simulation.AddItem(chopped, ChoppedTomato, ItemLocation.Station(Station));

        var rejected = simulation.Submit(Command(CookingRecipeOperation.StartProcess, "unknown-recipe",
            recipe: new RecipeId("absent-recipe"), item: chopped, station: Station, expectedVersion: 1));

        Assert.Equal(CookingRecipeOutcome.Rejected, rejected.Outcome);
        Assert.Equal(CookingRecipeRejectionReason.RecipeNotFound, rejected.Reason);
    }

    [Fact]
    public void S07_the_two_command_modes_have_distinct_fingerprints_and_are_each_idempotent()
    {
        using var evidence = CreateEvidence("S07");
        var explicitRecipe = Command(CookingRecipeOperation.StartProcess, "fingerprint-a", recipe: ChopRecipe,
            item: new ItemId("ingredient-a"), station: Station, expectedVersion: 1);
        var matchedRecipe = Command(CookingRecipeOperation.StartProcess, "fingerprint-a", item: new ItemId("ingredient-a"),
            station: Station, expectedVersion: 1);

        Assert.NotEqual(
            System.Text.Json.JsonSerializer.Serialize(explicitRecipe),
            System.Text.Json.JsonSerializer.Serialize(matchedRecipe));

        var explicitSimulation = CreateSimulation();
        explicitSimulation.AddItem(new ItemId("ingredient-a"), Tomato, ItemLocation.Station(Station));
        var explicitBefore = explicitSimulation.Snapshot();
        var firstExplicit = explicitSimulation.Submit(explicitRecipe);
        var replayExplicit = explicitSimulation.Submit(explicitRecipe);
        var explicitAfter = explicitSimulation.Snapshot();

        Assert.Equal(CookingRecipeOutcome.Accepted, firstExplicit.Outcome);
        Assert.True(replayExplicit.IsDuplicate);
        Assert.Empty(replayExplicit.Events);
        Assert.Single(explicitSimulation.Snapshot().Processes);

        var matchedSimulation = CreateSimulation();
        matchedSimulation.AddItem(new ItemId("ingredient-a"), BeatenEgg, ItemLocation.Station(Station));
        var firstMatched = matchedSimulation.Submit(matchedRecipe);
        var replayMatched = matchedSimulation.Submit(matchedRecipe);

        Assert.Equal(CookingRecipeOutcome.Rejected, firstMatched.Outcome);
        Assert.Equal(CookingRecipeRejectionReason.RecipeNotMatched, firstMatched.Reason);
        Assert.True(replayMatched.IsDuplicate);
        Assert.Empty(replayMatched.Events);
        Assert.Empty(matchedSimulation.Snapshot().Processes);

        AppendEvidence(evidence, explicitRecipe, firstExplicit, explicitBefore.Sha256(), explicitAfter.Sha256());
    }

    [Fact]
    public void S08_container_slot_and_recipe_rejection_reasons_exist_as_tail_appended_enum_values()
    {
        Assert.True(Enum.IsDefined(CookingRecipeRejectionReason.RecipeNotMatched));
        Assert.True(Enum.IsDefined(CookingRecipeRejectionReason.RecipeAmbiguous));
        Assert.True(Enum.IsDefined(CookingRecipeRejectionReason.ContainerRejectsItem));
        Assert.True(Enum.IsDefined(LocationKind.ContainerSlot));
        Assert.Equal(LocationKind.ContainerSlot, (LocationKind)3);

        // The three new rejection reasons must stay tail-appended after the last pre-existing
        // value: inserting a value in the middle of the enum would silently renumber every
        // reason after it, and these values are part of the command contract.
        var queueFull = (int)CookingRecipeRejectionReason.QueueFull;
        Assert.Equal(CookingRecipeRejectionReason.RecipeNotMatched, (CookingRecipeRejectionReason)(queueFull + 1));
        Assert.Equal(CookingRecipeRejectionReason.RecipeAmbiguous, (CookingRecipeRejectionReason)(queueFull + 2));
        Assert.Equal(CookingRecipeRejectionReason.ContainerRejectsItem, (CookingRecipeRejectionReason)(queueFull + 3));

        // 订单簿契约的三个新 reason 同样只能尾插（order owner 移入领域时追加）。
        Assert.Equal(CookingRecipeRejectionReason.OrderNotFound, (CookingRecipeRejectionReason)(queueFull + 4));
        Assert.Equal(CookingRecipeRejectionReason.OrderAlreadyCompleted, (CookingRecipeRejectionReason)(queueFull + 5));
        Assert.Equal(CookingRecipeRejectionReason.OrderRequirementMismatch, (CookingRecipeRejectionReason)(queueFull + 6));
        Assert.Equal(CookingRecipeRejectionReason.BindingRequired, (CookingRecipeRejectionReason)(queueFull + 10));
        Assert.Equal(CookingRecipeRejectionReason.BindingConflict, (CookingRecipeRejectionReason)(queueFull + 11));
        Assert.Equal(CookingRecipeRejectionReason.BindingNotFound, (CookingRecipeRejectionReason)(queueFull + 12));
        Assert.Equal(CookingRecipeRejectionReason.FrontOfHouseUnavailable, (CookingRecipeRejectionReason)(queueFull + 13));
        Assert.Equal(CookingRecipeRejectionReason.FrontWorkNotFound, (CookingRecipeRejectionReason)(queueFull + 14));
        Assert.Equal(CookingRecipeRejectionReason.SupplyRejected, (CookingRecipeRejectionReason)(queueFull + 15));
        Assert.Equal(CookingRecipeRejectionReason.SupplyAllocationFailed, (CookingRecipeRejectionReason)(queueFull + 16));
        Assert.Equal(CookingRecipeRejectionReason.MenuNotAuthorized, (CookingRecipeRejectionReason)(queueFull + 17));
        Assert.Equal(queueFull + 18, Enum.GetValues<CookingRecipeRejectionReason>().Length);
    }

    [Fact]
    public void S09_multi_input_recipe_starts_only_when_the_container_holds_the_full_declared_set()
    {
        using var evidence = CreateEvidence("S09");
        var partial = CreateSimulation();
        PutInSimulation(partial, new ItemId("chopped-only"), ChoppedTomato);
        var partialState = partial.Snapshot().Items.Single(item => item.Id == Pot);

        var partialStart = Submit(partial, evidence, "S09",
            Command(CookingRecipeOperation.StartProcess, "soup-partial", item: Pot, station: Station,
                expectedVersion: partialState.Version),
            "a partial input set does not start the multi-input recipe");

        Assert.Equal(CookingRecipeOutcome.Rejected, partialStart.Outcome);
        Assert.Equal(CookingRecipeRejectionReason.RecipeNotMatched, partialStart.Reason);
        Assert.Empty(partial.Snapshot().Processes);

        var simulation = CreateSimulation();
        PutInSimulation(simulation, new ItemId("chopped-1"), ChoppedTomato);
        PutInSimulation(simulation, new ItemId("beaten-1"), BeatenEgg);
        var potState = simulation.Snapshot().Items.Single(item => item.Id == Pot);

        var accepted = Submit(simulation, evidence, "S09",
            Command(CookingRecipeOperation.StartProcess, "soup-start", item: Pot, station: Station,
                expectedVersion: potState.Version),
            "the full declared input set in the container starts the multi-input recipe; water stays a default supply");

        Assert.Equal(CookingRecipeOutcome.Accepted, accepted.Outcome);
        var process = Assert.Single(simulation.Snapshot().Processes);
        Assert.Equal(SoupRecipe, process.Recipe);
    }

    [Fact]
    public void S10_movement_operations_require_their_own_identifiers_on_the_wire()
    {
        using var evidence = CreateEvidence("S10");
        var item = new ItemId("ingredient-a");
        var bowl = new ItemId("bowl-a");

        Assert.True(CookingRecipeCommandValidation.IsWellFormed(
            Command(CookingRecipeOperation.Drop, "drop", item: item, station: Station, expectedVersion: 1)));
        Assert.True(CookingRecipeCommandValidation.IsWellFormed(
            Command(CookingRecipeOperation.PutIn, "put-in", item: item, container: bowl, expectedVersion: 1)));
        Assert.True(CookingRecipeCommandValidation.IsWellFormed(
            Command(CookingRecipeOperation.TakeOut, "take-out", item: item, container: bowl, expectedVersion: 1)));
        Assert.True(CookingRecipeCommandValidation.IsWellFormed(
            Command(CookingRecipeOperation.Pour, "pour", item: bowl, container: bowl, expectedVersion: 1)));

        Assert.False(CookingRecipeCommandValidation.IsWellFormed(
            Command(CookingRecipeOperation.Drop, "drop-no-station", item: item, expectedVersion: 1)));
        Assert.False(CookingRecipeCommandValidation.IsWellFormed(
            Command(CookingRecipeOperation.PutIn, "put-in-no-container", item: item, expectedVersion: 1)));
        Assert.False(CookingRecipeCommandValidation.IsWellFormed(
            Command(CookingRecipeOperation.TakeOut, "take-out-no-container", item: item, expectedVersion: 1)));
        Assert.False(CookingRecipeCommandValidation.IsWellFormed(
            Command(CookingRecipeOperation.Pour, "pour-no-target", container: bowl, expectedVersion: 1)));
        Assert.False(CookingRecipeCommandValidation.IsWellFormed(
            Command(CookingRecipeOperation.Pour, "pour-no-version", item: bowl, container: bowl)));
        Assert.False(CookingRecipeCommandValidation.IsWellFormed(
            Command(CookingRecipeOperation.Drop, "drop-with-ticks", item: item, station: Station, expectedVersion: 1, ticks: 1)));

        // Plate 已被 放入/倒出 取代：枚举只保留七项动作加 fixed-tick 命令路径，且新值尾插。
        Assert.Equal(
            new[]
            {
                CookingRecipeOperation.Pickup, CookingRecipeOperation.StartProcess, CookingRecipeOperation.AdvanceTicks,
                CookingRecipeOperation.SubmitOrder, CookingRecipeOperation.Drop, CookingRecipeOperation.PutIn,
                CookingRecipeOperation.TakeOut, CookingRecipeOperation.Pour, CookingRecipeOperation.Move,
                CookingRecipeOperation.ContinueProcess, CookingRecipeOperation.StopProcess, CookingRecipeOperation.ServePortion,
                CookingRecipeOperation.ClearContents, CookingRecipeOperation.DiscardItem,
                CookingRecipeOperation.BindOrder, CookingRecipeOperation.UnbindOrder, CookingRecipeOperation.RebindOrder,
                CookingRecipeOperation.ClaimFrontWork, CookingRecipeOperation.ContinueFrontWork, CookingRecipeOperation.StopFrontWork,
                CookingRecipeOperation.RequestSupply, CookingRecipeOperation.ReceiveSupply, CookingRecipeOperation.TakeSupply,
            },
            Enum.GetValues<CookingRecipeOperation>());

        var simulation = CreateSimulation();
        simulation.AddWorldIngredient(new ItemId("shape-item"), Tomato, "spawn");
        var simulationItem = simulation.Snapshot().Items.First();
        var accepted = Submit(simulation, evidence, "S10",
            Command(CookingRecipeOperation.Drop, "drop-shape", item: simulationItem.Id, station: Station,
                expectedVersion: simulationItem.Version),
            "a well-formed drop reaches the domain and is rejected only by fixture state");
        Assert.Equal(CookingRecipeOutcome.Rejected, accepted.Outcome);
        Assert.Equal(CookingRecipeRejectionReason.CurrentLocationMismatch, accepted.Reason);
    }

    private static CookingRecipeSimulation CreateSimulation()
    {
        var scope = new CookingScope(Session, World, Match);
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [Player] = new(Player, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new HashSet<string>(StringComparer.Ordinal) { Station.Value }),
        };
        var items = new Dictionary<DefinitionId, CookingItemDefinition>
        {
            [Tomato] = new(Tomato, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [ChoppedTomato] = new(ChoppedTomato, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [BeatenEgg] = new(BeatenEgg, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [Soup] = new(Soup, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [Water] = new(Water, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [PotDefinition] = new(PotDefinition, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new CookingItemContainerCapability(4, new HashSet<DefinitionId> { ChoppedTomato, BeatenEgg })),
        };
        var appliances = new Dictionary<StationSlotId, CookingApplianceDefinition>
        {
            [Station] = new(Station, new HashSet<string>(StringComparer.Ordinal) { "cut", "heat" }),
        };
        var recipes = new Dictionary<RecipeId, CookingRecipeDefinition>
        {
            [ChopRecipe] = new(ChopRecipe, new[] { Tomato }, ChoppedTomato, new ProcessId("chop-process"), "cut", 2),
            [SoupRecipe] = new(SoupRecipe, new[] { ChoppedTomato, BeatenEgg }, Soup, new ProcessId("soup-process"),
                "heat", 6, new[] { new DefinitionId("water") }),
        };
        var simulation = new CookingRecipeSimulation(
            new CookingRecipeFixture(scope, players, items, appliances, recipes));
        simulation.AddItem(Pot, PotDefinition, ItemLocation.Station(Station));
        return simulation;
    }

    private static void PutInSimulation(CookingRecipeSimulation simulation, ItemId item, DefinitionId definition)
    {
        simulation.AddWorldIngredient(item, definition, $"spawn-{item.Value}");
        var state = simulation.Snapshot().Items.Single(candidate => candidate.Id == item);
        Assert.Equal(CookingRecipeOutcome.Accepted, simulation.Submit(Command(CookingRecipeOperation.Pickup,
            $"pickup-{item.Value}", item: item, expectedVersion: state.Version)).Outcome);
        Assert.Equal(CookingRecipeOutcome.Accepted, simulation.Submit(Command(CookingRecipeOperation.PutIn,
            $"put-in-{item.Value}", item: item, container: Pot, expectedVersion: state.Version + 1)).Outcome);
    }

    private static CookingRecipeCommand Command(CookingRecipeOperation operation, string commandId, RecipeId? recipe = null,
        ProcessId? process = null, ItemId? item = null, StationSlotId? station = null, ItemId? container = null,
        OrderId? order = null, int expectedVersion = 0, int ticks = 0, PlayerId? player = null) =>
        new(new CookingScope(Session, World, Match), 10, player ?? Player, new RecipeCommandId(commandId), operation, recipe,
            process, item, station, container, order, expectedVersion, ticks);

    private static EvidenceScope CreateEvidence(string testId) => new(testId);

    private static void AppendEvidence(EvidenceScope evidence, CookingRecipeCommand explicitRecipe,
        CookingRecipeCommandResult firstExplicit, string beforeStateHash, string afterStateHash) =>
        CookingRecipeAcceptanceEvidenceWriter.Append(evidence.Path, new CookingRecipeAcceptanceEvidence(
            "S07", "shape-multi-input-command", explicitRecipe, 0, firstExplicit.Outcome.ToString(),
            firstExplicit.Reason.ToString(), firstExplicit.IsDuplicate, firstExplicit.Events,
            beforeStateHash, afterStateHash,
            "the two command modes produce different JSON fingerprints; each replays as a duplicate with zero events",
            "dotnet test AbilityKit.Game.Cooking.Tests", DateTimeOffset.UtcNow.ToString("O")));

    private static CookingRecipeCommandResult Submit(CookingRecipeSimulation simulation, EvidenceScope evidence,
        string testId, CookingRecipeCommand command, string assertionSummary)
    {
        var before = simulation.Snapshot();
        var result = simulation.Submit(command);
        CookingRecipeAcceptanceEvidenceWriter.Append(evidence.Path, new CookingRecipeAcceptanceEvidence(
            testId, "shape-multi-input-command", command, simulation.LogicalTick, result.Outcome.ToString(),
            result.Reason.ToString(), result.IsDuplicate, result.Events, before.Sha256(), simulation.Snapshot().Sha256(),
            assertionSummary, "dotnet test AbilityKit.Game.Cooking.Tests", DateTimeOffset.UtcNow.ToString("O")));
        if (result.Outcome == CookingRecipeOutcome.Rejected)
            Assert.Equal(before.CanonicalText(), simulation.Snapshot().CanonicalText());
        return result;
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
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AbilityKit.Game.Cooking.Tests", "command-shape");
            _directory = System.IO.Path.Combine(root, testId, Guid.NewGuid().ToString("N"));
            Path = System.IO.Path.Combine(_directory, "recipe-acceptance.jsonl");
        }

        public string Path { get; }

        public void Dispose()
        {
            if (!_keepArtifacts && System.IO.Directory.Exists(_directory))
                System.IO.Directory.Delete(_directory, recursive: true);
        }
    }
}
