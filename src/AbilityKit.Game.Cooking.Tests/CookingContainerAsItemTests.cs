using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingContainerAsItemTests
{
    private static readonly SessionId Session = new("container-session");
    private static readonly WorldId World = new("container-world");
    private static readonly MatchId Match = new("container-match");
    private static readonly PlayerId Player = new("chef-a");
    private static readonly StationSlotId Station = new("stove-a");
    private static readonly StationSlotId Counter = new("counter-a");
    private static readonly OrderId Order = new("fixture-order");
    private static readonly OrderTemplateId OrderTemplate = new("fixture-order-template");
    private static readonly DefinitionId Raw = new("fixture-raw");
    private static readonly DefinitionId Product = new("fixture-product");
    private static readonly DefinitionId BowlDefinition = new("bowl");
    private static readonly DefinitionId PotDefinition = new("pot");
    private static readonly RecipeId Recipe = new("fixture-single-step");
    private static readonly ItemId SmallBowl = new("bowl-small");
    private static readonly ItemId LargeBowl = new("bowl-large");

    [Fact]
    public void C01_container_item_is_observable_with_capacity_and_contents_in_snapshot_and_canonical()
    {
        using var evidence = CreateEvidence("C01");
        var simulation = CreateSimulation();

        var container = Assert.Single(simulation.Snapshot().Containers, candidate => candidate.Id == SmallBowl);
        Assert.Equal(1, container.Capacity);
        Assert.Empty(container.ItemIds);

        var completed = CompleteProduct(simulation);
        AssertAccepted(Submit(simulation, evidence, "C01", Command(CookingRecipeOperation.Pickup, "pickup", item: completed,
            expectedVersion: 1), "the completed product is picked up from the appliance station"));
        AssertAccepted(Submit(simulation, evidence, "C01", Command(CookingRecipeOperation.PutIn, "put-in", item: completed,
            container: SmallBowl, expectedVersion: 2), "the held product is put into the capacity-one bowl"));

        var after = Assert.Single(simulation.Snapshot().Containers, candidate => candidate.Id == SmallBowl);
        Assert.Equal(new[] { completed }, after.ItemIds);
        var plated = Assert.Single(simulation.Snapshot().Items, item => item.Id == completed);
        Assert.Equal(ItemLocation.Container(SmallBowl, "slot-0"), plated.Location);
        Assert.Contains(SmallBowl.Value, simulation.Snapshot().CanonicalText(), StringComparison.Ordinal);
        Assert.Contains(completed.Value, simulation.Snapshot().CanonicalText(), StringComparison.Ordinal);
        AssertEvidence(evidence.Path, "C01", 2);
    }

    [Fact]
    public void C02_capacity_comes_from_the_container_item_definition_not_a_shared_container_table()
    {
        using var evidence = CreateEvidence("C02");
        var simulation = CreateSimulation(largeBowlCapacity: 2);

        var first = CompleteProduct(simulation);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup-small", item: first,
            expectedVersion: 1)));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.PutIn, "put-in-small", item: first,
            container: SmallBowl, expectedVersion: 2)));
        var second = CompleteProduct(simulation);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup-overflow", item: second,
            expectedVersion: 1)));

        var overflow = Submit(simulation, evidence, "C02", Command(CookingRecipeOperation.PutIn, "put-in-overflow",
            item: second, container: SmallBowl, expectedVersion: 2),
            "the capacity-one bowl rejects a second product while the capacity-two pot accepts it");

        AssertRejected(overflow, CookingRecipeRejectionReason.ContainerFull);
        Assert.Equal(new[] { first }, simulation.ItemsInContainer(SmallBowl));
        Assert.Equal(second, simulation.ItemInHand(Player));

        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.PutIn, "put-in-large", item: second,
            container: LargeBowl, expectedVersion: 2)));
        Assert.Equal(new[] { second }, simulation.ItemsInContainer(LargeBowl));
        var third = CompleteProduct(simulation);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup-large-second", item: third,
            expectedVersion: 1)));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.PutIn, "put-in-large-second", item: third,
            container: LargeBowl, expectedVersion: 2)));
        Assert.Equal(new[] { second, third }.OrderBy(id => id.Value, StringComparer.Ordinal).ToArray(),
            simulation.ItemsInContainer(LargeBowl));
        AssertEvidence(evidence.Path, "C02", 1);
    }

    [Fact]
    public void C03_container_item_is_a_movable_item_and_can_be_picked_up_from_the_world()
    {
        var simulation = CreateSimulation();
        var bowl = simulation.Snapshot().Items.Single(item => item.Id == SmallBowl);
        Assert.Equal(ItemLocation.Station(Counter), bowl.Location);

        var picked = simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup-bowl", item: SmallBowl,
            expectedVersion: bowl.Version));

        AssertAccepted(picked);
        Assert.Equal(SmallBowl, simulation.ItemInHand(Player));
        var held = Assert.Single(simulation.Snapshot().Items, item => item.Id == SmallBowl);
        Assert.Equal(ItemLocation.Hand(Player), held.Location);
        Assert.Empty(simulation.ItemsInContainer(SmallBowl));
    }

    [Fact]
    public void C04_item_without_container_capability_is_not_a_container()
    {
        using var evidence = CreateEvidence("C04");
        var simulation = CreateSimulation();
        var completed = CompleteProduct(simulation);

        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup", item: completed,
            expectedVersion: 1)));
        var rejected = Submit(simulation, evidence, "C04", Command(CookingRecipeOperation.PutIn, "put-in-raw",
            item: completed, container: new ItemId("ingredient-a"), expectedVersion: 2),
            "a plain ingredient item cannot serve as a container");

        AssertRejected(rejected, CookingRecipeRejectionReason.ContainerNotFound);
        var product = Assert.Single(simulation.Snapshot().Items, item => item.Id == completed);
        Assert.Equal(ItemLocation.Hand(Player), product.Location);
        AssertEvidence(evidence.Path, "C04", 1);
    }

    [Fact]
    public void C05_unknown_container_reports_no_contents_without_throwing()
    {
        var simulation = CreateSimulation();

        Assert.Empty(simulation.ItemsInContainer(new ItemId("absent-bowl")));
        Assert.DoesNotContain(simulation.Snapshot().Containers, candidate => candidate.Id == new ItemId("absent-bowl"));
    }

    [Fact]
    public void C06_vacated_container_slot_is_reused_deterministically()
    {
        var simulation = CreateSimulation();
        var first = CompleteProduct(simulation);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup-first", item: first,
            expectedVersion: 1)));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.PutIn, "put-in-first", item: first,
            container: SmallBowl, expectedVersion: 2)));
        var plated = simulation.Snapshot().Items.Single(item => item.Id == first);

        Assert.True(simulation.OpenOrder(Order, OrderTemplate).Accepted);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.SubmitOrder, "submit-first", item: first,
            order: Order, expectedVersion: plated.Version)));

        var second = CompleteProduct(simulation);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup-second", item: second,
            expectedVersion: 1)));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.PutIn, "put-in-second", item: second,
            container: SmallBowl, expectedVersion: 2)));

        var replated = Assert.Single(simulation.Snapshot().Items, item => item.Id == second);
        Assert.Equal(ItemLocation.Container(SmallBowl, "slot-0"), replated.Location);
        Assert.Equal(new[] { second }, simulation.ItemsInContainer(SmallBowl));
    }

    private static ItemId CompleteProduct(CookingRecipeSimulation simulation)
    {
        var input = new ItemId($"ingredient-{simulation.Snapshot().Version}-{simulation.LogicalTick}");
        simulation.AddItem(input, Raw, ItemLocation.Station(Station));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.StartProcess, $"start-{input.Value}",
            recipe: Recipe, item: input, station: Station, expectedVersion: 1)));
        var process = simulation.Snapshot().Processes.Single();
        var completion = simulation.Submit(Command(CookingRecipeOperation.AdvanceTicks, $"complete-{input.Value}",
            process: process.Id, ticks: 3));
        AssertAccepted(completion);
        return Assert.Single(completion.Events).Item!.Value;
    }

    private static CookingRecipeSimulation CreateSimulation(int largeBowlCapacity = 2)
    {
        var scope = new CookingScope(Session, World, Match);
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [Player] = new(Player, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new HashSet<string>(StringComparer.Ordinal) { Station.Value, Counter.Value }),
        };
        var items = new Dictionary<DefinitionId, CookingItemDefinition>
        {
            [Raw] = new(Raw, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [Product] = new(Product, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [BowlDefinition] = new(BowlDefinition, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new CookingItemContainerCapability(1, new HashSet<DefinitionId> { Product })),
            [PotDefinition] = new(PotDefinition, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new CookingItemContainerCapability(largeBowlCapacity, new HashSet<DefinitionId> { Product })),
        };
        var appliances = new Dictionary<StationSlotId, CookingApplianceDefinition>
        {
            [Station] = new(Station, new HashSet<string>(StringComparer.Ordinal) { "heat" }),
            [Counter] = new(Counter, new HashSet<string>(StringComparer.Ordinal)),
        };
        var recipes = new Dictionary<RecipeId, CookingRecipeDefinition>
        {
            [Recipe] = new(Recipe, new[] { Raw }, Product, new ProcessId("fixture-process"), "heat", 3),
        };
        var orderTemplates = new Dictionary<OrderTemplateId, CookingOrderTemplateDefinition>
        {
            [OrderTemplate] = new(OrderTemplate, Recipe, BowlDefinition),
        };
        var simulation = new CookingRecipeSimulation(
            new CookingRecipeFixture(scope, players, items, appliances, recipes, orderTemplates: orderTemplates));
        simulation.AddItem(SmallBowl, BowlDefinition, ItemLocation.Station(Counter));
        simulation.AddItem(LargeBowl, PotDefinition, ItemLocation.Station(Counter));
        return simulation;
    }

    private static CookingRecipeCommand Command(CookingRecipeOperation operation, string commandId, RecipeId? recipe = null,
        ProcessId? process = null, ItemId? item = null, StationSlotId? station = null, ItemId? container = null,
        OrderId? order = null, int expectedVersion = 0, int ticks = 0) =>
        new(new CookingScope(Session, World, Match), 10, Player, new RecipeCommandId(commandId), operation,
            recipe, process, item, station, container, order, expectedVersion, ticks);

    private static CookingRecipeCommandResult Submit(CookingRecipeSimulation simulation, EvidenceScope evidence, string testId,
        CookingRecipeCommand command, string assertionSummary)
    {
        var before = simulation.Snapshot();
        var result = simulation.Submit(command);
        CookingRecipeAcceptanceEvidenceWriter.Append(evidence.Path, new CookingRecipeAcceptanceEvidence(
            testId, "p2-container-as-item", command, simulation.LogicalTick, result.Outcome.ToString(), result.Reason.ToString(),
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
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AbilityKit.Game.Cooking.Tests", "container");
            _directory = System.IO.Path.Combine(root, testId, Guid.NewGuid().ToString("N"));
            Path = System.IO.Path.Combine(_directory, "container-acceptance.jsonl");
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
