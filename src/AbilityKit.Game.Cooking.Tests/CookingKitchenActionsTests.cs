using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingKitchenActionsTests
{
    private static readonly SessionId Session = new("actions-session");
    private static readonly WorldId World = new("actions-world");
    private static readonly MatchId Match = new("actions-match");
    private static readonly PlayerId Player = new("chef-a");
    private static readonly PlayerId OtherPlayer = new("chef-b");
    private static readonly StationSlotId Stove = new("stove-a");
    private static readonly StationSlotId Counter = new("counter-a");
    private static readonly StationSlotId FarStation = new("far-stove");
    private static readonly DefinitionId Raw = new("raw");
    private static readonly DefinitionId Product = new("product");
    private static readonly DefinitionId BowlDefinition = new("bowl");
    private static readonly DefinitionId PotDefinition = new("pot");
    private static readonly RecipeId Recipe = new("cook");
    private static readonly ItemId Bowl = new("bowl-1");
    private static readonly ItemId Pot = new("pot-1");
    private static readonly ItemId Ingredient = new("ingredient-1");
    private static readonly ItemId SecondIngredient = new("ingredient-2");

    [Fact]
    public void A01_drop_moves_a_held_item_onto_a_reachable_appliance_station()
    {
        using var evidence = CreateEvidence("A01");
        var simulation = CreateSimulation();
        var ingredient = simulation.Snapshot().Items.Single(item => item.Id == Ingredient);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup", item: Ingredient,
            expectedVersion: ingredient.Version)));

        var dropped = Submit(simulation, evidence, "A01", Command(CookingRecipeOperation.Drop, "drop", item: Ingredient,
            station: Stove, expectedVersion: ingredient.Version + 1), "a held item is dropped onto a reachable appliance");

        AssertAccepted(dropped);
        Assert.Null(simulation.ItemInHand(Player));
        var placed = Assert.Single(simulation.Snapshot().Items, item => item.Id == Ingredient);
        Assert.Equal(ItemLocation.Station(Stove), placed.Location);
        AssertEvidence(evidence.Path, "A01", 1);
    }

    [Fact]
    public void A02_drop_rejects_unreachable_unavailable_and_non_held_items_without_mutation()
    {
        using var evidence = CreateEvidence("A02");
        var simulation = CreateSimulation();
        var ingredient = simulation.Snapshot().Items.Single(item => item.Id == Ingredient);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup", item: Ingredient,
            expectedVersion: ingredient.Version)));

        var unreachable = Submit(simulation, evidence, "A02", Command(CookingRecipeOperation.Drop, "unreachable",
            item: Ingredient, station: FarStation, expectedVersion: ingredient.Version + 1),
            "an appliance outside the player's reach cannot receive a drop");
        AssertRejected(unreachable, CookingRecipeRejectionReason.TargetOutOfRange);

        var notHeld = Submit(simulation, evidence, "A02", Command(CookingRecipeOperation.Drop, "not-held", item: Bowl,
            station: Counter, expectedVersion: 1), "an item the player does not hold cannot be dropped");
        AssertRejected(notHeld, CookingRecipeRejectionReason.CurrentLocationMismatch);

        var unavailable = CreateSimulation(stoveAvailable: false);
        var held = unavailable.Snapshot().Items.Single(item => item.Id == Ingredient);
        AssertAccepted(unavailable.Submit(Command(CookingRecipeOperation.Pickup, "pickup", item: Ingredient,
            expectedVersion: held.Version)));
        var unavailableDrop = Submit(unavailable, evidence, "A02", Command(CookingRecipeOperation.Drop, "unavailable",
            item: Ingredient, station: Stove, expectedVersion: held.Version + 1),
            "an unavailable appliance cannot receive a drop");
        AssertRejected(unavailableDrop, CookingRecipeRejectionReason.ApplianceUnavailable);
        AssertEvidence(evidence.Path, "A02", 3);
    }

    [Fact]
    public void A03_put_in_moves_a_held_item_into_a_reachable_container_item()
    {
        using var evidence = CreateEvidence("A03");
        var simulation = CreateSimulation();
        var ingredient = simulation.Snapshot().Items.Single(item => item.Id == Ingredient);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup", item: Ingredient,
            expectedVersion: ingredient.Version)));

        var putIn = Submit(simulation, evidence, "A03", Command(CookingRecipeOperation.PutIn, "put-in", item: Ingredient,
            container: Bowl, expectedVersion: ingredient.Version + 1), "a held item is put into the reachable bowl container");

        AssertAccepted(putIn);
        Assert.Null(simulation.ItemInHand(Player));
        var contained = Assert.Single(simulation.Snapshot().Items, item => item.Id == Ingredient);
        Assert.Equal(ItemLocation.Container(Bowl, "slot-0"), contained.Location);
        Assert.Equal(new[] { Ingredient }, simulation.ItemsInContainer(Bowl));
        AssertEvidence(evidence.Path, "A03", 1);
    }

    [Fact]
    public void A04_put_in_rejects_foreign_full_and_locked_destinations_without_mutation()
    {
        using var evidence = CreateEvidence("A04");
        var simulation = CreateSimulation(potAccepts: new HashSet<DefinitionId>());
        var ingredient = simulation.Snapshot().Items.Single(item => item.Id == Ingredient);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup", item: Ingredient,
            expectedVersion: ingredient.Version)));

        var foreign = Submit(simulation, evidence, "A04", Command(CookingRecipeOperation.PutIn, "foreign", item: Ingredient,
            container: Pot, expectedVersion: ingredient.Version + 1),
            "a container that does not declare the item definition rejects it at the put-in stage");
        AssertRejected(foreign, CookingRecipeRejectionReason.ContainerRejectsItem);

        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.PutIn, "put-in", item: Ingredient,
            container: Bowl, expectedVersion: ingredient.Version + 1)));
        Assert.Equal(new[] { Ingredient }, simulation.ItemsInContainer(Bowl));

        var second = simulation.Snapshot().Items.Single(item => item.Id == SecondIngredient);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup-second", item: SecondIngredient,
            expectedVersion: second.Version)));
        var overflow = Submit(simulation, evidence, "A04", Command(CookingRecipeOperation.PutIn, "overflow", item: SecondIngredient,
            container: Bowl, expectedVersion: second.Version + 1), "a full container rejects another put-in");
        AssertRejected(overflow, CookingRecipeRejectionReason.ContainerFull);
        Assert.Equal(new[] { Ingredient }, simulation.ItemsInContainer(Bowl));
        Assert.Equal(SecondIngredient, simulation.ItemInHand(Player));

        var locked = CreateSimulation();
        locked.AddItem(new ItemId("process-input"), Raw, ItemLocation.Station(Stove));
        AssertAccepted(locked.Submit(Command(CookingRecipeOperation.StartProcess, "start", recipe: Recipe,
            item: new ItemId("process-input"), station: Stove, expectedVersion: 1)));
        var lockedDrop = Submit(locked, evidence, "A04", Command(CookingRecipeOperation.PutIn, "locked",
            item: new ItemId("process-input"), container: Bowl, expectedVersion: 2),
            "an input locked by an active process cannot be put into a container");
        AssertRejected(lockedDrop, CookingRecipeRejectionReason.ItemStale);
        Assert.Empty(locked.ItemsInContainer(Bowl));
        AssertEvidence(evidence.Path, "A04", 3);
    }

    [Fact]
    public void A05_take_out_moves_a_contained_item_into_an_empty_hand()
    {
        using var evidence = CreateEvidence("A05");
        var simulation = CreateSimulation();
        var ingredient = simulation.Snapshot().Items.Single(item => item.Id == Ingredient);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup", item: Ingredient,
            expectedVersion: ingredient.Version)));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.PutIn, "put-in", item: Ingredient,
            container: Bowl, expectedVersion: ingredient.Version + 1)));
        var contained = simulation.Snapshot().Items.Single(item => item.Id == Ingredient);

        var taken = Submit(simulation, evidence, "A05", Command(CookingRecipeOperation.TakeOut, "take-out", item: Ingredient,
            container: Bowl, expectedVersion: contained.Version), "a contained item is taken out into an empty hand");

        AssertAccepted(taken);
        Assert.Equal(Ingredient, simulation.ItemInHand(Player));
        var held = Assert.Single(simulation.Snapshot().Items, item => item.Id == Ingredient);
        Assert.Equal(ItemLocation.Hand(Player), held.Location);
        Assert.Empty(simulation.ItemsInContainer(Bowl));
        AssertEvidence(evidence.Path, "A05", 1);
    }

    [Fact]
    public void A06_take_out_rejects_foreign_contents_and_occupied_hands_without_mutation()
    {
        using var evidence = CreateEvidence("A06");
        var simulation = CreateSimulation();
        var ingredient = simulation.Snapshot().Items.Single(item => item.Id == Ingredient);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup", item: Ingredient,
            expectedVersion: ingredient.Version)));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.PutIn, "put-in", item: Ingredient,
            container: Bowl, expectedVersion: ingredient.Version + 1)));
        var contained = simulation.Snapshot().Items.Single(item => item.Id == Ingredient);

        var foreign = Submit(simulation, evidence, "A06", Command(CookingRecipeOperation.TakeOut, "foreign",
            item: Ingredient, container: Pot, expectedVersion: contained.Version),
            "an item cannot be taken out of a container that does not hold it");
        AssertRejected(foreign, CookingRecipeRejectionReason.CurrentLocationMismatch);

        var second = simulation.Snapshot().Items.Single(item => item.Id == Ingredient);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup-pot", item: Pot,
            expectedVersion: 1)));
        var occupied = Submit(simulation, evidence, "A06", Command(CookingRecipeOperation.TakeOut, "occupied",
            item: Ingredient, container: Bowl, expectedVersion: second.Version),
            "a take-out cannot fill an already occupied hand");
        AssertRejected(occupied, CookingRecipeRejectionReason.IngredientAlreadyCollected);
        Assert.Equal(new[] { Ingredient }, simulation.ItemsInContainer(Bowl));
        Assert.Equal(Pot, simulation.ItemInHand(Player));
        AssertEvidence(evidence.Path, "A06", 2);
    }

    [Fact]
    public void A07_pour_transfers_all_contents_between_reachable_containers()
    {
        using var evidence = CreateEvidence("A07");
        var simulation = CreateSimulation();
        var ingredient = simulation.Snapshot().Items.Single(item => item.Id == Ingredient);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup", item: Ingredient,
            expectedVersion: ingredient.Version)));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.PutIn, "put-in", item: Ingredient,
            container: Bowl, expectedVersion: ingredient.Version + 1)));
        var bowlState = simulation.Snapshot().Items.Single(item => item.Id == Bowl);

        var poured = Submit(simulation, evidence, "A07", Command(CookingRecipeOperation.Pour, "pour", item: Bowl,
            container: Pot, expectedVersion: bowlState.Version), "pouring empties the source bowl into the target pot");

        AssertAccepted(poured);
        Assert.Empty(simulation.ItemsInContainer(Bowl));
        Assert.Equal(new[] { Ingredient }, simulation.ItemsInContainer(Pot));
        var moved = Assert.Single(simulation.Snapshot().Items, item => item.Id == Ingredient);
        Assert.Equal(ItemLocation.Container(Pot, "slot-0"), moved.Location);
        AssertEvidence(evidence.Path, "A07", 1);
    }

    [Fact]
    public void A08_pour_rejects_empty_full_self_and_locked_sources_without_mutation()
    {
        using var evidence = CreateEvidence("A08");
        var simulation = CreateSimulation();
        var bowlState = simulation.Snapshot().Items.Single(item => item.Id == Bowl);

        var empty = Submit(simulation, evidence, "A08", Command(CookingRecipeOperation.Pour, "empty", item: Bowl,
            container: Pot, expectedVersion: bowlState.Version), "an empty source container has nothing to pour");
        AssertRejected(empty, CookingRecipeRejectionReason.ProductNotFound);

        var self = Submit(simulation, evidence, "A08", Command(CookingRecipeOperation.Pour, "self", item: Bowl,
            container: Bowl, expectedVersion: bowlState.Version), "a container cannot be poured into itself");
        AssertRejected(self, CookingRecipeRejectionReason.CurrentLocationMismatch);

        var ingredient = simulation.Snapshot().Items.Single(item => item.Id == Ingredient);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup", item: Ingredient,
            expectedVersion: ingredient.Version)));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.PutIn, "put-in", item: Ingredient,
            container: Bowl, expectedVersion: ingredient.Version + 1)));
        var second = simulation.Snapshot().Items.Single(item => item.Id == SecondIngredient);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup-second", item: SecondIngredient,
            expectedVersion: second.Version)));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.PutIn, "put-in-second", item: SecondIngredient,
            container: Pot, expectedVersion: second.Version + 1)));
        var filled = simulation.Snapshot().Items.Single(item => item.Id == Bowl);

        var overflow = Submit(simulation, evidence, "A08", Command(CookingRecipeOperation.Pour, "overflow", item: Bowl,
            container: Pot, expectedVersion: filled.Version),
            "the capacity-one bowl cannot pour its content into the already full capacity-one pot");
        AssertRejected(overflow, CookingRecipeRejectionReason.ContainerFull);
        Assert.Equal(new[] { Ingredient }, simulation.ItemsInContainer(Bowl));
        Assert.Equal(new[] { SecondIngredient }, simulation.ItemsInContainer(Pot));
        AssertEvidence(evidence.Path, "A08", 3);
    }

    [Fact]
    public void A09_pour_rejects_sources_and_targets_the_player_cannot_reach()
    {
        using var evidence = CreateEvidence("A09");
        var simulation = CreateSimulation();
        var farBowl = new ItemId("bowl-far");
        simulation.AddItem(farBowl, BowlDefinition, ItemLocation.Station(FarStation));
        var ingredient = simulation.Snapshot().Items.Single(item => item.Id == Ingredient);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup", item: Ingredient,
            expectedVersion: ingredient.Version)));

        var unreachablePutIn = Submit(simulation, evidence, "A09", Command(CookingRecipeOperation.PutIn, "put-in-far",
            item: Ingredient, container: farBowl, expectedVersion: ingredient.Version + 1),
            "a container on a station outside the player's reach cannot receive a put-in");
        AssertRejected(unreachablePutIn, CookingRecipeRejectionReason.TargetOutOfRange);

        var unreachable = Submit(simulation, evidence, "A09", Command(CookingRecipeOperation.Pour, "unreachable",
            item: farBowl, container: Pot, expectedVersion: 1),
            "a container on a station outside the player's reach cannot be poured from");
        AssertRejected(unreachable, CookingRecipeRejectionReason.TargetOutOfRange);
        Assert.Empty(simulation.ItemsInContainer(Pot));
        AssertEvidence(evidence.Path, "A09", 2);
    }

    [Fact]
    public void A10_active_process_input_is_locked_against_every_movement_action()
    {
        using var evidence = CreateEvidence("A10");
        var simulation = CreateSimulation();
        simulation.AddItem(new ItemId("process-input"), Raw, ItemLocation.Station(Stove));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.StartProcess, "start", recipe: Recipe,
            item: new ItemId("process-input"), station: Stove, expectedVersion: 1)));
        var process = simulation.Snapshot().Processes.Single();
        var before = simulation.Snapshot().CanonicalText();

        var pickup = Submit(simulation, evidence, "A10", Command(CookingRecipeOperation.Pickup, "pickup",
            item: new ItemId("process-input"), expectedVersion: 1), "a locked processing input cannot be picked up");
        AssertRejected(pickup, CookingRecipeRejectionReason.ItemStale);

        var drop = Submit(simulation, evidence, "A10", Command(CookingRecipeOperation.Drop, "drop",
            item: new ItemId("process-input"), station: Counter, expectedVersion: 1),
            "a locked processing input cannot be dropped");
        AssertRejected(drop, CookingRecipeRejectionReason.ItemStale);

        var putIn = Submit(simulation, evidence, "A10", Command(CookingRecipeOperation.PutIn, "put-in",
            item: new ItemId("process-input"), container: Bowl, expectedVersion: 1),
            "a locked processing input cannot be put into a container");
        AssertRejected(putIn, CookingRecipeRejectionReason.ItemStale);

        var takeOut = Submit(simulation, evidence, "A10", Command(CookingRecipeOperation.TakeOut, "take-out",
            item: new ItemId("process-input"), container: Bowl, expectedVersion: 1),
            "a locked processing input cannot be taken out of a container");
        AssertRejected(takeOut, CookingRecipeRejectionReason.CurrentLocationMismatch);

        Assert.Equal(before, simulation.Snapshot().CanonicalText());
        Assert.Equal(process.Id, simulation.Snapshot().Processes.Single().Id);
        AssertEvidence(evidence.Path, "A10", 4);
    }

    [Fact]
    public void A11_container_cannot_contain_itself_through_a_chain()
    {
        using var evidence = CreateEvidence("A11");
        var simulation = CreateSimulation();

        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup-bowl", item: Bowl,
            expectedVersion: 1)));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.PutIn, "put-in-bowl", item: Bowl,
            container: Pot, expectedVersion: 2)));
        Assert.Equal(ItemLocation.Container(Pot, "slot-0"),
            simulation.Snapshot().Items.Single(item => item.Id == Bowl).Location);

        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup-pot", item: Pot,
            expectedVersion: 1)));
        var cycle = Submit(simulation, evidence, "A11", Command(CookingRecipeOperation.PutIn, "cycle", item: Pot,
            container: Bowl, expectedVersion: 2), "putting an ancestor container into its own descendant forms a cycle");
        AssertRejected(cycle, CookingRecipeRejectionReason.ContainerRejectsItem);
        Assert.Equal(ItemLocation.Hand(Player), simulation.Snapshot().Items.Single(item => item.Id == Pot).Location);
        Assert.Equal(ItemLocation.Container(Pot, "slot-0"),
            simulation.Snapshot().Items.Single(item => item.Id == Bowl).Location);
        AssertEvidence(evidence.Path, "A11", 1);
    }

    private static CookingRecipeSimulation CreateSimulation(bool stoveAvailable = true,
        IReadOnlySet<DefinitionId>? potAccepts = null)
    {
        var scope = new CookingScope(Session, World, Match);
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [Player] = new(Player, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new HashSet<string>(StringComparer.Ordinal) { Stove.Value, Counter.Value }),
            [OtherPlayer] = new(OtherPlayer, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new HashSet<string>(StringComparer.Ordinal)),
        };
        var items = new Dictionary<DefinitionId, CookingItemDefinition>
        {
            [Raw] = new(Raw, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [Product] = new(Product, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [BowlDefinition] = new(BowlDefinition, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new CookingItemContainerCapability(1, new HashSet<DefinitionId> { Raw, Product, PotDefinition })),
            [PotDefinition] = new(PotDefinition, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new CookingItemContainerCapability(1, potAccepts ?? new HashSet<DefinitionId> { Raw, Product, BowlDefinition })),
        };
        var appliances = new Dictionary<StationSlotId, CookingApplianceDefinition>
        {
            [Stove] = new(Stove, new HashSet<string>(StringComparer.Ordinal) { "heat" }, stoveAvailable),
            [Counter] = new(Counter, new HashSet<string>(StringComparer.Ordinal)),
            [FarStation] = new(FarStation, new HashSet<string>(StringComparer.Ordinal) { "heat" }),
        };
        var recipes = new Dictionary<RecipeId, CookingRecipeDefinition>
        {
            [Recipe] = new(Recipe, new[] { Raw }, Product, new ProcessId("cook-process"), "heat", 3),
        };
        var simulation = new CookingRecipeSimulation(
            new CookingRecipeFixture(scope, players, items, appliances, recipes));
        simulation.AddItem(Bowl, BowlDefinition, ItemLocation.Station(Counter));
        simulation.AddItem(Pot, PotDefinition, ItemLocation.Station(Counter));
        simulation.AddWorldIngredient(Ingredient, Raw, "spawn");
        simulation.AddWorldIngredient(SecondIngredient, Raw, "spawn-2");
        return simulation;
    }

    private static CookingRecipeCommand Command(CookingRecipeOperation operation, string commandId, RecipeId? recipe = null,
        ProcessId? process = null, ItemId? item = null, StationSlotId? station = null, ItemId? container = null,
        OrderId? order = null, int expectedVersion = 0, int ticks = 0, PlayerId? player = null) =>
        new(new CookingScope(Session, World, Match), 10, player ?? Player, new RecipeCommandId(commandId), operation,
            recipe, process, item, station, container, order, expectedVersion, ticks);

    private static CookingRecipeCommandResult Submit(CookingRecipeSimulation simulation, EvidenceScope evidence, string testId,
        CookingRecipeCommand command, string assertionSummary)
    {
        var before = simulation.Snapshot();
        var result = simulation.Submit(command);
        CookingRecipeAcceptanceEvidenceWriter.Append(evidence.Path, new CookingRecipeAcceptanceEvidence(
            testId, "p2-kitchen-actions", command, simulation.LogicalTick, result.Outcome.ToString(), result.Reason.ToString(),
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
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AbilityKit.Game.Cooking.Tests", "actions");
            _directory = System.IO.Path.Combine(root, testId, Guid.NewGuid().ToString("N"));
            Path = System.IO.Path.Combine(_directory, "actions-acceptance.jsonl");
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
