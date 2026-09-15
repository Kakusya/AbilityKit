using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingRecipeLoop")]
public sealed class CookingRecipeLoopTests
{
    private static readonly SessionId Session = new("recipe-session");
    private static readonly WorldId World = new("recipe-world");
    private static readonly MatchId Match = new("recipe-match");
    private static readonly PlayerId Player = new("chef-a");
    private static readonly PlayerId OtherPlayer = new("chef-b");
    private static readonly StationSlotId Station = new("stove-a");
    private static readonly ContainerId Plate = new("plate-a");
    private static readonly OrderId Order = new("fixture-order-a");
    private static readonly DefinitionId RawIngredient = new("fixture-raw");
    private static readonly DefinitionId Product = new("fixture-product");
    private static readonly RecipeId Recipe = new("fixture-single-step");

    [Fact]
    public void R01_single_input_single_process_three_ticks_plate_and_accepted_order_complete_once()
    {
        using var evidence = CreateEvidence("R01");
        var orderPort = new RecordingOrderPort(accepted: true);
        var simulation = CreateSimulation(Recipe, RawIngredient, Product, "heat", orderPort);
        var input = new ItemId("ingredient-a");
        simulation.AddIngredient(input, RawIngredient, Player);

        var started = Submit(simulation, evidence, "R01", Command(CookingRecipeOperation.StartProcess, "start", recipe: Recipe,
            item: input, station: Station, expectedVersion: 1), "start locks the held input into the appliance process");
        AssertAccepted(started);
        Assert.Null(simulation.ItemInHand(Player));
        Assert.Single(simulation.Snapshot().Processes);

        var process = simulation.Snapshot().Processes.Single();
        var tickOne = Submit(simulation, evidence, "R01", Command(CookingRecipeOperation.AdvanceTicks, "tick-one",
            process: process.Id, ticks: 1), "first logical tick preserves in-progress process");
        AssertAccepted(tickOne);
        Assert.Equal(1, simulation.LogicalTick);
        Assert.Equal(1, simulation.Snapshot().Processes.Single().ElapsedTicks);

        var tickTwo = Submit(simulation, evidence, "R01", Command(CookingRecipeOperation.AdvanceTicks, "tick-two",
            process: process.Id, ticks: 1), "second logical tick remains below three-tick completion threshold");
        AssertAccepted(tickTwo);
        Assert.Equal(2, simulation.LogicalTick);
        Assert.Single(simulation.Snapshot().Processes);

        var completed = Submit(simulation, evidence, "R01", Command(CookingRecipeOperation.AdvanceTicks, "tick-three",
            process: process.Id, ticks: 1), "third logical tick consumes input and creates exactly one product");
        AssertAccepted(completed);
        Assert.Equal(3, simulation.LogicalTick);
        Assert.Empty(simulation.Snapshot().Processes);
        var product = Assert.Single(simulation.Snapshot().Items, item => item.IsProduct);
        Assert.Equal(Product, product.Definition);
        Assert.Equal(ItemLocation.Station(Station), product.Location);

        var plated = Submit(simulation, evidence, "R01", Command(CookingRecipeOperation.Plate, "plate", item: product.Id,
            container: Plate, expectedVersion: product.Version), "completed product is placed into the independent plate state");
        AssertAccepted(plated);
        var platedProduct = Assert.Single(simulation.Snapshot().Items, item => item.Id == product.Id);
        Assert.Equal(ItemLocation.Container(new ItemId(Plate.Value), "slot-0"), platedProduct.Location);
        Assert.Equal(new[] { product.Id }, simulation.ItemsInContainer(Plate));

        var submitted = Submit(simulation, evidence, "R01", Command(CookingRecipeOperation.SubmitOrder, "submit", item: product.Id,
            order: Order, expectedVersion: platedProduct.Version), "accepted fixture order consumes the plated product exactly once");
        AssertAccepted(submitted);
        Assert.Empty(simulation.Snapshot().Items);
        Assert.Empty(simulation.ItemsInContainer(Plate));
        Assert.Equal(new[] { Order }, simulation.Snapshot().AcceptedOrders);
        Assert.Single(orderPort.Submissions);
        AssertEvidence(evidence.Path, "R01", 6);
    }

    [Fact]
    public void R02_invalid_input_capability_range_availability_and_incomplete_product_are_mutation_safe()
    {
        using var evidence = CreateEvidence("R02");
        var input = new ItemId("ingredient-a");

        var missing = CreateSimulation(Recipe, RawIngredient, Product, "heat", new RecordingOrderPort(true));
        AssertRejected(Submit(missing, evidence, "R02", Command(CookingRecipeOperation.StartProcess, "missing", recipe: Recipe,
            item: input, station: Station, expectedVersion: 1), "missing input rejects without process"), CookingRecipeRejectionReason.ItemNotFound);

        var wrongCapability = CreateSimulation(Recipe, RawIngredient, Product, "heat", new RecordingOrderPort(true), applianceCapabilities: new HashSet<string>());
        wrongCapability.AddIngredient(input, RawIngredient, Player);
        AssertRejected(Submit(wrongCapability, evidence, "R02", Command(CookingRecipeOperation.StartProcess, "wrong-capability", recipe: Recipe,
            item: input, station: Station, expectedVersion: 1), "appliance capability mismatch is mutation-free"), CookingRecipeRejectionReason.ApplianceCapabilityMismatch);

        var unreachable = CreateSimulation(Recipe, RawIngredient, Product, "heat", new RecordingOrderPort(true), reachable: new HashSet<string>());
        unreachable.AddIngredient(input, RawIngredient, Player);
        AssertRejected(Submit(unreachable, evidence, "R02", Command(CookingRecipeOperation.StartProcess, "unreachable", recipe: Recipe,
            item: input, station: Station, expectedVersion: 1), "unreachable appliance is mutation-free"), CookingRecipeRejectionReason.TargetOutOfRange);

        var unavailable = CreateSimulation(Recipe, RawIngredient, Product, "heat", new RecordingOrderPort(true), applianceAvailable: false);
        unavailable.AddIngredient(input, RawIngredient, Player);
        AssertRejected(Submit(unavailable, evidence, "R02", Command(CookingRecipeOperation.StartProcess, "unavailable", recipe: Recipe,
            item: input, station: Station, expectedVersion: 1), "unavailable appliance is mutation-free"), CookingRecipeRejectionReason.ApplianceUnavailable);

        var incomplete = CreateSimulation(Recipe, RawIngredient, Product, "heat", new RecordingOrderPort(true));
        incomplete.AddIngredient(input, RawIngredient, Player);
        AssertAccepted(incomplete.Submit(Command(CookingRecipeOperation.StartProcess, "setup", recipe: Recipe, item: input, station: Station, expectedVersion: 1)));
        var process = incomplete.Snapshot().Processes.Single();
        AssertRejected(Submit(incomplete, evidence, "R02", Command(CookingRecipeOperation.Plate, "incomplete-plate", item: input,
            container: Plate, expectedVersion: 2), "processing input cannot be plated as completed product"), CookingRecipeRejectionReason.ProductNotFound);
        Assert.Equal(process.Id, incomplete.Snapshot().Processes.Single().Id);
        AssertEvidence(evidence.Path, "R02", 5);
    }

    [Fact]
    public void R03_rejected_order_preserves_successfully_plated_product()
    {
        using var evidence = CreateEvidence("R03");
        var simulation = CompleteAndPlate(new RecordingOrderPort(accepted: false), out var product);
        var before = simulation.Snapshot().Sha256();
        var plated = simulation.Snapshot().Items.Single(item => item.Id == product);

        var rejected = Submit(simulation, evidence, "R03", Command(CookingRecipeOperation.SubmitOrder, "reject", item: product,
            order: Order, expectedVersion: plated.Version), "order port reject preserves independently plated product");

        AssertRejected(rejected, CookingRecipeRejectionReason.OrderRejected);
        Assert.Equal(before, simulation.Snapshot().Sha256());
        Assert.Equal(new[] { product }, simulation.ItemsInContainer(Plate));
        Assert.Empty(simulation.Snapshot().AcceptedOrders);
        AssertEvidence(evidence.Path, "R03", 1);
    }

    [Fact]
    public void R03_submission_requires_reachability_and_container_slots_are_unique()
    {
        var orderPort = new RecordingOrderPort(accepted: true);
        var simulation = CompleteAndPlate(orderPort, out var firstProduct, containerCapacity: 2);
        var firstPlated = simulation.Snapshot().Items.Single(item => item.Id == firstProduct);
        var before = simulation.Snapshot().Sha256();
        var foreignSubmit = simulation.Submit(Command(CookingRecipeOperation.SubmitOrder, "foreign-submit", item: firstProduct,
            order: Order, expectedVersion: firstPlated.Version, player: OtherPlayer));
        AssertRejected(foreignSubmit, CookingRecipeRejectionReason.TargetOutOfRange);
        Assert.Equal(before, simulation.Snapshot().Sha256());

        var secondInput = new ItemId("ingredient-b");
        simulation.AddIngredient(secondInput, RawIngredient, Player);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.StartProcess, "second-start", recipe: Recipe, item: secondInput,
            station: Station, expectedVersion: 1)));
        var secondProcess = simulation.Snapshot().Processes.Single();
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.AdvanceTicks, "second-complete", process: secondProcess.Id, ticks: 3)));
        var secondProduct = simulation.Snapshot().Items.Single(item => item.IsProduct && item.Id != firstProduct);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Plate, "second-plate", item: secondProduct.Id,
            container: Plate, expectedVersion: secondProduct.Version)));

        var slots = simulation.Snapshot().Items.Where(item => item.IsProduct)
            .Select(item => item.Location.SlotId).OrderBy(slot => slot, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "slot-0", "slot-1" }, slots);
        Assert.Equal(2, slots.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void R04_replay_is_idempotent_and_consumed_product_cannot_be_resubmitted()
    {
        using var evidence = CreateEvidence("R04");
        var orderPort = new RecordingOrderPort(accepted: true);
        var simulation = CompleteAndPlate(orderPort, out var product);
        var plated = simulation.Snapshot().Items.Single(item => item.Id == product);
        var command = Command(CookingRecipeOperation.SubmitOrder, "submit-once", item: product, order: Order, expectedVersion: plated.Version);

        var accepted = Submit(simulation, evidence, "R04", command, "initial submit consumes product once");
        var duplicate = Submit(simulation, evidence, "R04", command, "same identity returns cached result without another order port call");
        var replay = Submit(simulation, evidence, "R04", command with { Command = new RecipeCommandId("submit-again") },
            "different identity cannot submit an already consumed product");

        AssertAccepted(accepted);
        Assert.True(duplicate.IsDuplicate);
        Assert.Empty(duplicate.Events);
        AssertRejected(replay, CookingRecipeRejectionReason.ProductAlreadyConsumed);
        Assert.Single(orderPort.Submissions);
        Assert.Single(simulation.EventHistory, @event => @event.Summary == "order-submitted");
        AssertEvidence(evidence.Path, "R04", 3);
    }

    [Fact]
    public void R05_second_recipe_and_appliance_fixture_uses_the_same_rules_without_recipe_branches()
    {
        using var evidence = CreateEvidence("R05");
        var secondRecipe = new RecipeId("fixture-second-step");
        var secondInput = new DefinitionId("fixture-second-raw");
        var secondProduct = new DefinitionId("fixture-second-product");
        var orderPort = new RecordingOrderPort(accepted: true);
        var simulation = CreateSimulation(secondRecipe, secondInput, secondProduct, "blend", orderPort, requiredTicks: 3);
        var input = new ItemId("second-ingredient");
        simulation.AddIngredient(input, secondInput, Player);

        AssertAccepted(Submit(simulation, evidence, "R05", Command(CookingRecipeOperation.StartProcess, "second-start", recipe: secondRecipe,
            item: input, station: Station, expectedVersion: 1), "second recipe changes fixture data only"));
        var process = simulation.Snapshot().Processes.Single();
        AssertAccepted(Submit(simulation, evidence, "R05", Command(CookingRecipeOperation.AdvanceTicks, "second-complete", process: process.Id,
            ticks: 3), "same logical tick rule completes second fixture"));
        var product = simulation.Snapshot().Items.Single(item => item.IsProduct);
        Assert.Equal(secondProduct, product.Definition);
        AssertAccepted(Submit(simulation, evidence, "R05", Command(CookingRecipeOperation.Plate, "second-plate", item: product.Id,
            container: Plate, expectedVersion: product.Version), "same plate rule accepts second fixture product"));
        var plated = simulation.Snapshot().Items.Single(item => item.Id == product.Id);
        AssertAccepted(Submit(simulation, evidence, "R05", Command(CookingRecipeOperation.SubmitOrder, "second-submit", item: product.Id,
            order: Order, expectedVersion: plated.Version), "same order boundary accepts second fixture product"));
        Assert.Single(orderPort.Submissions);
        AssertEvidence(evidence.Path, "R05", 4);
    }

    [Fact]
    public void R06_host_local_and_remote_in_process_follow_the_same_recipe_authority_path()
    {
        var localPort = new RecordingOrderPort(true);
        var remotePort = new RecordingOrderPort(true);
        var local = CreateSimulation(Recipe, RawIngredient, Product, "heat", localPort);
        var remote = CreateSimulation(Recipe, RawIngredient, Product, "heat", remotePort);
        var input = new ItemId("shared-input");
        local.AddIngredient(input, RawIngredient, Player);
        remote.AddIngredient(input, RawIngredient, Player);

        ExecuteLoop(local, input);
        ExecuteLoop(remote, input);

        Assert.Equal(local.Snapshot().CanonicalText(), remote.Snapshot().CanonicalText());
        Assert.Equal(local.EventHistory, remote.EventHistory);
        Assert.Equal(localPort.Submissions, remotePort.Submissions);
    }

    private static CookingRecipeSimulation CompleteAndPlate(RecordingOrderPort orderPort, out ItemId product, int containerCapacity = 1)
    {
        var simulation = CreateSimulation(Recipe, RawIngredient, Product, "heat", orderPort, containerCapacity: containerCapacity);
        var input = new ItemId("ingredient-a");
        simulation.AddIngredient(input, RawIngredient, Player);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.StartProcess, "start", recipe: Recipe, item: input, station: Station, expectedVersion: 1)));
        var process = simulation.Snapshot().Processes.Single();
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.AdvanceTicks, "complete", process: process.Id, ticks: 3)));
        var completedProduct = simulation.Snapshot().Items.Single(item => item.IsProduct).Id;
        var state = simulation.Snapshot().Items.Single(item => item.Id == completedProduct);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Plate, "plate", item: completedProduct, container: Plate, expectedVersion: state.Version)));
        product = completedProduct;
        return simulation;
    }

    private static void ExecuteLoop(CookingRecipeSimulation simulation, ItemId input)
    {
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.StartProcess, "shared-start", recipe: Recipe, item: input, station: Station, expectedVersion: 1)));
        var process = simulation.Snapshot().Processes.Single();
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.AdvanceTicks, "shared-complete", process: process.Id, ticks: 3)));
        var product = simulation.Snapshot().Items.Single(item => item.IsProduct);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Plate, "shared-plate", item: product.Id, container: Plate, expectedVersion: product.Version)));
        var plated = simulation.Snapshot().Items.Single(item => item.Id == product.Id);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.SubmitOrder, "shared-submit", item: product.Id, order: Order, expectedVersion: plated.Version)));
    }

    private static CookingRecipeSimulation CreateSimulation(RecipeId recipe, DefinitionId input, DefinitionId product,
        string applianceCapability, RecordingOrderPort orderPort, int requiredTicks = 3,
        IReadOnlySet<string>? applianceCapabilities = null, IReadOnlySet<string>? reachable = null, bool applianceAvailable = true,
        int containerCapacity = 1)
    {
        var scope = new CookingScope(Session, World, Match);
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [Player] = new(Player, new HashSet<string>(StringComparer.Ordinal) { "cook" }, reachable ?? new HashSet<string>(StringComparer.Ordinal) { Station.Value }),
            [OtherPlayer] = new(OtherPlayer, new HashSet<string>(StringComparer.Ordinal) { "cook" }, new HashSet<string>(StringComparer.Ordinal)),
        };
        var items = new Dictionary<DefinitionId, CookingItemDefinition>
        {
            [input] = new(input, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [product] = new(product, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
        };
        var appliances = new Dictionary<StationSlotId, CookingApplianceDefinition>
        {
            [Station] = new(Station, applianceCapabilities ?? new HashSet<string>(StringComparer.Ordinal) { applianceCapability }, applianceAvailable),
        };
        var recipes = new Dictionary<RecipeId, CookingRecipeDefinition>
        {
            [recipe] = new(recipe, input, product, new ProcessId($"{recipe.Value}-process"), applianceCapability, requiredTicks),
        };
        var containers = new Dictionary<ContainerId, CookingContainerDefinition> { [Plate] = new(Plate, containerCapacity) };
        return new CookingRecipeSimulation(new CookingRecipeFixture(scope, players, items, appliances, recipes, containers), orderPort);
    }

    private static CookingRecipeCommand Command(CookingRecipeOperation operation, string commandId, RecipeId? recipe = null,
        ProcessId? process = null, ItemId? item = null, StationSlotId? station = null, ContainerId? container = null,
        OrderId? order = null, int expectedVersion = 0, int ticks = 0, PlayerId? player = null) =>
        new(new CookingScope(Session, World, Match), 10, player ?? Player, new RecipeCommandId(commandId), operation, recipe, process,
            item, station, container, order, expectedVersion, ticks);

    private static CookingRecipeCommandResult Submit(CookingRecipeSimulation simulation, EvidenceScope evidence, string testId,
        CookingRecipeCommand command, string assertionSummary)
    {
        var before = simulation.Snapshot();
        var result = simulation.Submit(command);
        CookingRecipeAcceptanceEvidenceWriter.Append(evidence.Path, new CookingRecipeAcceptanceEvidence(
            testId, "p2-single-input-single-process", command, simulation.LogicalTick, result.Outcome.ToString(), result.Reason.ToString(),
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
        Assert.False(result.IsDuplicate);
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
            Assert.NotNull(record.Events);
        });
    }

    private sealed class RecordingOrderPort(bool accepted) : ICookingOrderPort
    {
        public List<CookingOrderSubmission> Submissions { get; } = new();

        public CookingOrderAcceptance Submit(CookingOrderSubmission submission)
        {
            Submissions.Add(submission);
            return new CookingOrderAcceptance(accepted, accepted ? "fixture-accepted" : "fixture-rejected");
        }
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
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AbilityKit.Game.Cooking.Tests", "recipe");
            _directory = System.IO.Path.Combine(root, testId, Guid.NewGuid().ToString("N"));
            Path = System.IO.Path.Combine(_directory, "recipe-acceptance.jsonl");
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
