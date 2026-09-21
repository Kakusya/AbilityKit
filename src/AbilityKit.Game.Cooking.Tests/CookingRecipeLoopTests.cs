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
    private static readonly StationSlotId Counter = new("counter-a");
    private static readonly ItemId Plate = new("plate-a");
    private static readonly DefinitionId PlateDefinition = new("plate");
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
        simulation.AddItem(input, RawIngredient, ItemLocation.Station(Station));

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

        var picked = Submit(simulation, evidence, "R01", Command(CookingRecipeOperation.Pickup, "pickup", item: product.Id,
            expectedVersion: product.Version), "completed product is picked up from the appliance station");
        AssertAccepted(picked);
        Assert.Equal(product.Id, simulation.ItemInHand(Player));
        var putIn = Submit(simulation, evidence, "R01", Command(CookingRecipeOperation.PutIn, "put-in", item: product.Id,
            container: Plate, expectedVersion: product.Version + 1), "held product is put into the bowl container item");
        AssertAccepted(putIn);
        var platedProduct = Assert.Single(simulation.Snapshot().Items, item => item.Id == product.Id);
        Assert.Equal(ItemLocation.Container(Plate, "slot-0"), platedProduct.Location);
        Assert.Equal(new[] { product.Id }, simulation.ItemsInContainer(Plate));

        var submitted = Submit(simulation, evidence, "R01", Command(CookingRecipeOperation.SubmitOrder, "submit", item: product.Id,
            order: Order, expectedVersion: platedProduct.Version), "accepted fixture order consumes the plated product exactly once");
        AssertAccepted(submitted);
        Assert.Equal(new[] { Plate }, simulation.Snapshot().Items.Select(item => item.Id).ToArray());
        Assert.Empty(simulation.ItemsInContainer(Plate));
        Assert.Equal(new[] { Order }, simulation.Snapshot().AcceptedOrders);
        Assert.Single(orderPort.Submissions);
        AssertEvidence(evidence.Path, "R01", 7);
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
        wrongCapability.AddItem(input, RawIngredient, ItemLocation.Station(Station));
        AssertRejected(Submit(wrongCapability, evidence, "R02", Command(CookingRecipeOperation.StartProcess, "wrong-capability", recipe: Recipe,
            item: input, station: Station, expectedVersion: 1), "appliance capability mismatch is mutation-free"), CookingRecipeRejectionReason.ApplianceCapabilityMismatch);

        var unreachable = CreateSimulation(Recipe, RawIngredient, Product, "heat", new RecordingOrderPort(true), reachable: new HashSet<string>());
        unreachable.AddItem(input, RawIngredient, ItemLocation.Station(Station));
        AssertRejected(Submit(unreachable, evidence, "R02", Command(CookingRecipeOperation.StartProcess, "unreachable", recipe: Recipe,
            item: input, station: Station, expectedVersion: 1), "unreachable appliance is mutation-free"), CookingRecipeRejectionReason.TargetOutOfRange);

        var unavailable = CreateSimulation(Recipe, RawIngredient, Product, "heat", new RecordingOrderPort(true), applianceAvailable: false);
        unavailable.AddItem(input, RawIngredient, ItemLocation.Station(Station));
        AssertRejected(Submit(unavailable, evidence, "R02", Command(CookingRecipeOperation.StartProcess, "unavailable", recipe: Recipe,
            item: input, station: Station, expectedVersion: 1), "unavailable appliance is mutation-free"), CookingRecipeRejectionReason.ApplianceUnavailable);

        var incomplete = CreateSimulation(Recipe, RawIngredient, Product, "heat", new RecordingOrderPort(true));
        incomplete.AddItem(input, RawIngredient, ItemLocation.Station(Station));
        AssertAccepted(incomplete.Submit(Command(CookingRecipeOperation.StartProcess, "setup", recipe: Recipe, item: input, station: Station, expectedVersion: 1)));
        var process = incomplete.Snapshot().Processes.Single();
        AssertRejected(Submit(incomplete, evidence, "R02", Command(CookingRecipeOperation.PutIn, "incomplete-put-in", item: input,
            container: Plate, expectedVersion: 2), "an input locked by an active process cannot be moved into a container"), CookingRecipeRejectionReason.ItemStale);
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
        simulation.AddItem(secondInput, RawIngredient, ItemLocation.Station(Station));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.StartProcess, "second-start", recipe: Recipe, item: secondInput,
            station: Station, expectedVersion: 1)));
        var secondProcess = simulation.Snapshot().Processes.Single();
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.AdvanceTicks, "second-complete", process: secondProcess.Id, ticks: 3)));
        var secondProduct = simulation.Snapshot().Items.Single(item => item.IsProduct && item.Id != firstProduct);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "second-pickup", item: secondProduct.Id,
            expectedVersion: secondProduct.Version)));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.PutIn, "second-put-in", item: secondProduct.Id,
            container: Plate, expectedVersion: secondProduct.Version + 1)));

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
        simulation.AddItem(input, secondInput, ItemLocation.Station(Station));

        AssertAccepted(Submit(simulation, evidence, "R05", Command(CookingRecipeOperation.StartProcess, "second-start", recipe: secondRecipe,
            item: input, station: Station, expectedVersion: 1), "second recipe changes fixture data only"));
        var process = simulation.Snapshot().Processes.Single();
        AssertAccepted(Submit(simulation, evidence, "R05", Command(CookingRecipeOperation.AdvanceTicks, "second-complete", process: process.Id,
            ticks: 3), "same logical tick rule completes second fixture"));
        var product = simulation.Snapshot().Items.Single(item => item.IsProduct);
        Assert.Equal(secondProduct, product.Definition);
        AssertAccepted(Submit(simulation, evidence, "R05", Command(CookingRecipeOperation.Pickup, "second-pickup",
            item: product.Id, expectedVersion: product.Version), "same pickup rule lifts the second fixture product"));
        AssertAccepted(Submit(simulation, evidence, "R05", Command(CookingRecipeOperation.PutIn, "second-put-in",
            item: product.Id, container: Plate, expectedVersion: product.Version + 1),
            "same put-in rule accepts the second fixture product"));
        var plated = simulation.Snapshot().Items.Single(item => item.Id == product.Id);
        AssertAccepted(Submit(simulation, evidence, "R05", Command(CookingRecipeOperation.SubmitOrder, "second-submit", item: product.Id,
            order: Order, expectedVersion: plated.Version), "same order boundary accepts second fixture product"));
        Assert.Single(orderPort.Submissions);
        AssertEvidence(evidence.Path, "R05", 5);
    }

    [Fact]
    public void R06_host_local_and_remote_in_process_follow_the_same_recipe_authority_path()
    {
        var localPort = new RecordingOrderPort(true);
        var remotePort = new RecordingOrderPort(true);
        var local = CreateSimulation(Recipe, RawIngredient, Product, "heat", localPort);
        var remote = CreateSimulation(Recipe, RawIngredient, Product, "heat", remotePort);
        var input = new ItemId("shared-input");
        local.AddItem(input, RawIngredient, ItemLocation.Station(Station));
        remote.AddItem(input, RawIngredient, ItemLocation.Station(Station));

        ExecuteLoop(local, input);
        ExecuteLoop(remote, input);

        Assert.Equal(local.Snapshot().CanonicalText(), remote.Snapshot().CanonicalText());
        Assert.Equal(local.EventHistory, remote.EventHistory);
        Assert.Equal(localPort.Submissions, remotePort.Submissions);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void FixedTick_empty_frame_advances_tick_version_and_event_once()
    {
        var orderPort = new RecordingOrderPort(accepted: true);
        var simulation = CreateSimulation(Recipe, RawIngredient, Product, "heat", orderPort);

        var result = simulation.AdvanceFixedTick(LevelScope(), hostFrameSequence: 1);

        Assert.Equal(0, result.BeforeLogicalTick);
        Assert.Equal(1, result.AfterLogicalTick);
        Assert.Equal(0, result.BeforeStateVersion);
        Assert.Equal(1, result.AfterStateVersion);
        Assert.Empty(result.Processes);
        Assert.Equal(1, simulation.LogicalTick);
        Assert.Equal(1, simulation.Snapshot().Version);
        Assert.Empty(simulation.EventHistory);
        Assert.Equal(result.Event, Assert.Single(simulation.TickEventHistory));
        Assert.Equal(1, result.Event.Sequence);
        Assert.Empty(orderPort.Submissions);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void FixedTick_single_process_progresses_once_per_frame_and_completes_on_third_tick()
    {
        var orderPort = new RecordingOrderPort(accepted: true);
        var simulation = CreateSimulation(Recipe, RawIngredient, Product, "heat", orderPort);
        var input = new ItemId("ingredient-fixed");
        simulation.AddItem(input, RawIngredient, ItemLocation.Station(Station));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.StartProcess, "fixed-start", recipe: Recipe,
            item: input, station: Station, expectedVersion: 1)));

        var first = simulation.AdvanceFixedTick(LevelScope(), 1);
        var second = simulation.AdvanceFixedTick(LevelScope(), 2);
        var third = simulation.AdvanceFixedTick(LevelScope(), 3);

        Assert.Equal(1, Assert.Single(first.Processes).AfterElapsedTicks);
        Assert.False(first.Processes[0].Completed);
        Assert.Equal(2, Assert.Single(second.Processes).AfterElapsedTicks);
        Assert.False(second.Processes[0].Completed);
        var completion = Assert.Single(third.Processes);
        Assert.Equal(3, completion.AfterElapsedTicks);
        Assert.True(completion.Completed);
        Assert.Equal(new ItemId("product-1"), completion.Product);
        Assert.Equal(3, simulation.LogicalTick);
        Assert.Equal(4, simulation.Snapshot().Version);
        Assert.Empty(simulation.Snapshot().Processes);
        Assert.Equal(new ItemId("product-1"), Assert.Single(simulation.Snapshot().Items, item => item.IsProduct).Id);
        Assert.Equal(new long[] { 2, 3, 4 }, simulation.TickEventHistory.Select(@event => @event.Sequence));
        Assert.Empty(orderPort.Submissions);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void FixedTick_multiple_processes_complete_in_process_id_order_with_stable_products_and_one_version()
    {
        var firstAllocator = new RecordingProductIdAllocator();
        var secondAllocator = new RecordingProductIdAllocator();
        var first = CreateMultiProcessSimulation(processCount: 10, firstAllocator);
        var second = CreateMultiProcessSimulation(processCount: 10, secondAllocator);
        StartProcesses(first, processCount: 10);
        StartProcesses(second, processCount: 10);

        var firstResult = first.AdvanceFixedTick(LevelScope(), 1);
        var secondResult = second.AdvanceFixedTick(LevelScope(), 1);

        Assert.Equal(
            new[] { "process-1", "process-10", "process-2", "process-3", "process-4", "process-5", "process-6", "process-7", "process-8", "process-9" },
            firstResult.Processes.Select(result => result.Process.Value));
        Assert.Equal(
            new[] { "product-1", "product-2", "product-3", "product-4", "product-5", "product-6", "product-7", "product-8", "product-9", "product-10" },
            firstResult.Processes.Select(result => result.Product?.Value));
        Assert.All(firstResult.Processes, result => Assert.True(result.Completed));
        Assert.Equal(Enumerable.Range(1, 10).Select(value => (long)value), firstAllocator.Requests);
        Assert.Equal(1, first.LogicalTick);
        Assert.Equal(11, first.Snapshot().Version);
        Assert.Empty(first.Snapshot().Processes);
        Assert.Equal(first.Snapshot().CanonicalText(), second.Snapshot().CanonicalText());
        Assert.Equal(
            firstResult.Processes.Select(result => (result.Process, result.Product, result.Completed)),
            secondResult.Processes.Select(result => (result.Process, result.Product, result.Completed)));
        Assert.Equal(
            first.TickEventHistory.Select(@event => (@event.Sequence, @event.HostFrameSequence, @event.BeforeLogicalTick,
                @event.AfterLogicalTick, @event.BeforeStateVersion, @event.AfterStateVersion)),
            second.TickEventHistory.Select(@event => (@event.Sequence, @event.HostFrameSequence, @event.BeforeLogicalTick,
                @event.AfterLogicalTick, @event.BeforeStateVersion, @event.AfterStateVersion)));
        Assert.Equal(firstAllocator.Requests, secondAllocator.Requests);
        Assert.Equal(11, firstResult.Event.Sequence);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void FixedTick_allocator_collision_is_mutation_safe_and_does_not_advance_product_sequence()
    {
        var allocator = new FixedProductIdAllocator(new ItemId("ingredient-collision"));
        var orderPort = new RecordingOrderPort(accepted: true);
        var simulation = CreateSimulation(Recipe, RawIngredient, Product, "heat", orderPort, requiredTicks: 1,
            productIdAllocator: allocator);
        var input = new ItemId("ingredient-collision");
        simulation.AddItem(input, RawIngredient, ItemLocation.Station(Station));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.StartProcess, "collision-start", recipe: Recipe,
            item: input, station: Station, expectedVersion: 1)));
        var before = simulation.Snapshot().CanonicalText();
        var commandEventsBefore = simulation.EventHistory.ToArray();

        var error = Assert.Throws<InvalidOperationException>(() => simulation.AdvanceFixedTick(LevelScope(), 1));

        Assert.Contains("duplicate item identity", error.Message, StringComparison.Ordinal);
        Assert.Equal(before, simulation.Snapshot().CanonicalText());
        Assert.Equal(commandEventsBefore, simulation.EventHistory);
        Assert.Empty(simulation.TickEventHistory);
        Assert.Equal(new long[] { 1 }, allocator.Requests);
        Assert.Empty(orderPort.Submissions);

        Assert.Throws<InvalidOperationException>(() => simulation.AdvanceFixedTick(LevelScope(), 1));
        Assert.Equal(new long[] { 1, 1 }, allocator.Requests);
        Assert.Equal(before, simulation.Snapshot().CanonicalText());
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void FixedTick_staged_duplicate_product_ids_are_mutation_safe()
    {
        var allocator = new FixedProductIdAllocator(new ItemId("staged-product"));
        var simulation = CreateMultiProcessSimulation(processCount: 2, allocator);
        StartProcesses(simulation, processCount: 2);
        var before = simulation.Snapshot().CanonicalText();
        var commandEventsBefore = simulation.EventHistory.ToArray();

        var error = Assert.Throws<InvalidOperationException>(() => simulation.AdvanceFixedTick(LevelScope(), 1));

        Assert.Contains("duplicate item identity", error.Message, StringComparison.Ordinal);
        Assert.Equal(before, simulation.Snapshot().CanonicalText());
        Assert.Equal(commandEventsBefore, simulation.EventHistory);
        Assert.Empty(simulation.TickEventHistory);
        Assert.Equal(new long[] { 1, 2 }, allocator.Requests);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void FixedTick_blank_allocator_output_is_mutation_safe()
    {
        var allocator = new FixedProductIdAllocator(new ItemId("   "));
        var simulation = CreateSimulation(Recipe, RawIngredient, Product, "heat", new RecordingOrderPort(accepted: true),
            requiredTicks: 1, productIdAllocator: allocator);
        StartSingleProcess(simulation, "blank");
        var before = simulation.Snapshot().CanonicalText();
        var commandEventsBefore = simulation.EventHistory.ToArray();

        var error = Assert.Throws<InvalidOperationException>(() => simulation.AdvanceFixedTick(LevelScope(), 1));

        Assert.Contains("blank identity", error.Message, StringComparison.Ordinal);
        Assert.Equal(before, simulation.Snapshot().CanonicalText());
        Assert.Equal(commandEventsBefore, simulation.EventHistory);
        Assert.Empty(simulation.TickEventHistory);
        Assert.Equal(new long[] { 1 }, allocator.Requests);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void FixedTick_allocator_exception_is_mutation_safe()
    {
        var allocator = new ThrowingProductIdAllocator();
        var simulation = CreateSimulation(Recipe, RawIngredient, Product, "heat", new RecordingOrderPort(accepted: true),
            requiredTicks: 1, productIdAllocator: allocator);
        StartSingleProcess(simulation, "allocator-fault");
        var before = simulation.Snapshot().CanonicalText();
        var commandEventsBefore = simulation.EventHistory.ToArray();

        var error = Assert.Throws<InvalidOperationException>(() => simulation.AdvanceFixedTick(LevelScope(), 1));

        Assert.Equal("allocator-fault", error.Message);
        Assert.Equal(before, simulation.Snapshot().CanonicalText());
        Assert.Equal(commandEventsBefore, simulation.EventHistory);
        Assert.Empty(simulation.TickEventHistory);
        Assert.Equal(new long[] { 1 }, allocator.Requests);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void FixedTick_checked_overflow_is_mutation_safe()
    {
        var allocator = new RecordingProductIdAllocator();
        var simulation = CreateSimulation(Recipe, RawIngredient, Product, "heat", new RecordingOrderPort(accepted: true),
            requiredTicks: 1, productIdAllocator: allocator);
        StartSingleProcess(simulation, "overflow");
        simulation.SetFixedTickCountersForTesting(logicalTick: 0, stateVersion: 1, eventSequence: 1,
            productSequence: long.MaxValue);
        var before = simulation.Snapshot().CanonicalText();
        var commandEventsBefore = simulation.EventHistory.ToArray();
        var tickEventsBefore = simulation.TickEventHistory.ToArray();

        Assert.Throws<OverflowException>(() => simulation.AdvanceFixedTick(LevelScope(), 1));

        Assert.Equal(before, simulation.Snapshot().CanonicalText());
        Assert.Equal(commandEventsBefore, simulation.EventHistory);
        Assert.Equal(tickEventsBefore, simulation.TickEventHistory);
        Assert.Empty(allocator.Requests);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void FixedTick_reverse_index_orphan_is_rejected_without_mutation()
    {
        var simulation = CreateSimulation(Recipe, RawIngredient, Product, "heat", new RecordingOrderPort(accepted: true));
        simulation.AddFixedTickReverseIndexEntryForTesting(new ProcessId("orphan-process"), new StationSlotId("orphan-station"));
        var before = simulation.Snapshot().CanonicalText();

        var error = Assert.Throws<InvalidOperationException>(() => simulation.AdvanceFixedTick(LevelScope(), 1));

        Assert.Contains("reverse index count", error.Message, StringComparison.Ordinal);
        Assert.Equal(before, simulation.Snapshot().CanonicalText());
        Assert.Empty(simulation.TickEventHistory);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void FixedTick_reverse_index_mismatch_is_rejected_without_mutation()
    {
        var simulation = CreateSimulation(Recipe, RawIngredient, Product, "heat", new RecordingOrderPort(accepted: true));
        var process = StartSingleProcess(simulation, "index-mismatch");
        simulation.SetFixedTickReverseIndexEntryForTesting(process, new StationSlotId("wrong-station"));
        var before = simulation.Snapshot().CanonicalText();
        var commandEventsBefore = simulation.EventHistory.ToArray();

        var error = Assert.Throws<InvalidOperationException>(() => simulation.AdvanceFixedTick(LevelScope(), 1));

        Assert.Contains("does not match an authoritative station process", error.Message, StringComparison.Ordinal);
        Assert.Equal(before, simulation.Snapshot().CanonicalText());
        Assert.Equal(commandEventsBefore, simulation.EventHistory);
        Assert.Empty(simulation.TickEventHistory);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void FixedTick_completion_removes_indexes_releases_station_and_old_process_is_not_found()
    {
        var simulation = CreateSimulation(Recipe, RawIngredient, Product, "heat", new RecordingOrderPort(accepted: true),
            requiredTicks: 1);
        var oldProcess = StartSingleProcess(simulation, "first");

        var completed = simulation.AdvanceFixedTick(LevelScope(), 1);

        Assert.True(Assert.Single(completed.Processes).Completed);
        Assert.False(simulation.ContainsFixedTickReverseIndexForTesting(oldProcess));
        Assert.Empty(simulation.Snapshot().Processes);

        var secondInput = new ItemId("ingredient-reuse");
        simulation.AddItem(secondInput, RawIngredient, ItemLocation.Station(Station));
        var restarted = simulation.Submit(Command(CookingRecipeOperation.StartProcess, "reuse-station", recipe: Recipe,
            item: secondInput, station: Station, expectedVersion: 1));
        AssertAccepted(restarted);
        var newProcess = Assert.Single(simulation.Snapshot().Processes).Id;
        Assert.NotEqual(oldProcess, newProcess);

        var oldProcessResult = simulation.Submit(Command(CookingRecipeOperation.AdvanceTicks, "old-process",
            process: oldProcess, ticks: 1));
        AssertRejected(oldProcessResult, CookingRecipeRejectionReason.ProcessNotFound);
        Assert.Equal(newProcess, Assert.Single(simulation.Snapshot().Processes).Id);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void FixedTick_never_calls_external_order_port()
    {
        var simulation = CreateSimulation(Recipe, RawIngredient, Product, "heat", new ThrowingOrderPort(), requiredTicks: 1);
        var input = new ItemId("ingredient-no-port");
        simulation.AddItem(input, RawIngredient, ItemLocation.Station(Station));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.StartProcess, "no-port-start", recipe: Recipe,
            item: input, station: Station, expectedVersion: 1)));

        var result = simulation.AdvanceFixedTick(LevelScope(), 1);

        Assert.True(Assert.Single(result.Processes).Completed);
        Assert.Single(simulation.Snapshot().Items, item => item.IsProduct);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void FixedTick_wrong_scope_and_closed_lifecycle_reject_without_mutation()
    {
        var simulation = CreateSimulation(Recipe, RawIngredient, Product, "heat", new RecordingOrderPort(accepted: true));
        var before = simulation.Snapshot().CanonicalText();
        var wrongScope = new CookingLevelScope(
            new CookingScope(Session, World, new MatchId("other-match")),
            new RestaurantRuntimeId(1),
            new LevelId("fixture-level"),
            1);

        Assert.Throws<ArgumentException>(() => simulation.AdvanceFixedTick(wrongScope, 1));
        Assert.Equal(before, simulation.Snapshot().CanonicalText());
        Assert.Empty(simulation.TickEventHistory);

        simulation.CloseLifecycle();
        Assert.Throws<InvalidOperationException>(() => simulation.AdvanceFixedTick(LevelScope(), 1));
        Assert.Equal(before, simulation.Snapshot().CanonicalText());
        Assert.Empty(simulation.TickEventHistory);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public void Direct_AdvanceTicks_retains_legacy_multi_tick_command_behavior()
    {
        var simulation = CreateSimulation(Recipe, RawIngredient, Product, "heat", new RecordingOrderPort(accepted: true));
        var input = new ItemId("ingredient-direct-regression");
        simulation.AddItem(input, RawIngredient, ItemLocation.Station(Station));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.StartProcess, "direct-start", recipe: Recipe,
            item: input, station: Station, expectedVersion: 1)));
        var process = Assert.Single(simulation.Snapshot().Processes);

        var completed = simulation.Submit(Command(CookingRecipeOperation.AdvanceTicks, "direct-complete", process: process.Id, ticks: 3));

        AssertAccepted(completed);
        Assert.Equal(3, simulation.LogicalTick);
        Assert.Empty(simulation.Snapshot().Processes);
        Assert.Equal(new ItemId("product-1"), Assert.Single(simulation.Snapshot().Items, item => item.IsProduct).Id);
        Assert.Empty(simulation.TickEventHistory);
        Assert.Equal(2, simulation.EventHistory.Count);
    }

    private static CookingRecipeSimulation CompleteAndPlate(RecordingOrderPort orderPort, out ItemId product, int containerCapacity = 1)
    {
        var simulation = CreateSimulation(Recipe, RawIngredient, Product, "heat", orderPort, containerCapacity: containerCapacity);
        var input = new ItemId("ingredient-a");
        simulation.AddItem(input, RawIngredient, ItemLocation.Station(Station));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.StartProcess, "start", recipe: Recipe, item: input, station: Station, expectedVersion: 1)));
        var process = simulation.Snapshot().Processes.Single();
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.AdvanceTicks, "complete", process: process.Id, ticks: 3)));
        var completedProduct = simulation.Snapshot().Items.Single(item => item.IsProduct).Id;
        var state = simulation.Snapshot().Items.Single(item => item.Id == completedProduct);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup", item: completedProduct, expectedVersion: state.Version)));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.PutIn, "put-in", item: completedProduct,
            container: Plate, expectedVersion: state.Version + 1)));
        product = completedProduct;
        return simulation;
    }

    private static void ExecuteLoop(CookingRecipeSimulation simulation, ItemId input)
    {
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.StartProcess, "shared-start", recipe: Recipe, item: input, station: Station, expectedVersion: 1)));
        var process = simulation.Snapshot().Processes.Single();
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.AdvanceTicks, "shared-complete", process: process.Id, ticks: 3)));
        var product = simulation.Snapshot().Items.Single(item => item.IsProduct);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "shared-pickup", item: product.Id, expectedVersion: product.Version)));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.PutIn, "shared-put-in", item: product.Id,
            container: Plate, expectedVersion: product.Version + 1)));
        var plated = simulation.Snapshot().Items.Single(item => item.Id == product.Id);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.SubmitOrder, "shared-submit", item: product.Id, order: Order, expectedVersion: plated.Version)));
    }

    private static CookingRecipeSimulation CreateSimulation(RecipeId recipe, DefinitionId input, DefinitionId product,
        string applianceCapability, ICookingOrderPort orderPort, int requiredTicks = 3,
        IReadOnlySet<string>? applianceCapabilities = null, IReadOnlySet<string>? reachable = null, bool applianceAvailable = true,
        int containerCapacity = 1, ICookingProductIdAllocator? productIdAllocator = null)
    {
        var scope = new CookingScope(Session, World, Match);
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [Player] = new(Player, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                reachable ?? new HashSet<string>(StringComparer.Ordinal) { Station.Value, Counter.Value }),
            [OtherPlayer] = new(OtherPlayer, new HashSet<string>(StringComparer.Ordinal) { "cook" }, new HashSet<string>(StringComparer.Ordinal)),
        };
        var items = new Dictionary<DefinitionId, CookingItemDefinition>
        {
            [input] = new(input, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [product] = new(product, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [PlateDefinition] = new(PlateDefinition, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new CookingItemContainerCapability(containerCapacity, new HashSet<DefinitionId> { product })),
        };
        var appliances = new Dictionary<StationSlotId, CookingApplianceDefinition>
        {
            [Station] = new(Station, applianceCapabilities ?? new HashSet<string>(StringComparer.Ordinal) { applianceCapability }, applianceAvailable),
            [Counter] = new(Counter, new HashSet<string>(StringComparer.Ordinal)),
        };
        var recipes = new Dictionary<RecipeId, CookingRecipeDefinition>
        {
            [recipe] = new(recipe, new[] { input }, product, new ProcessId($"{recipe.Value}-process"), applianceCapability, requiredTicks),
        };
        var simulation = new CookingRecipeSimulation(
            new CookingRecipeFixture(scope, players, items, appliances, recipes), orderPort, productIdAllocator);
        simulation.AddItem(Plate, PlateDefinition, ItemLocation.Station(Counter));
        return simulation;
    }

    private static CookingLevelScope LevelScope() => new(
        new CookingScope(Session, World, Match),
        new RestaurantRuntimeId(1),
        new LevelId("fixture-level"),
        1);

    private static CookingRecipeSimulation CreateMultiProcessSimulation(int processCount, ICookingProductIdAllocator allocator)
    {
        var scope = new CookingScope(Session, World, Match);
        var players = new Dictionary<PlayerId, CookingPlayerConfig>();
        var appliances = new Dictionary<StationSlotId, CookingApplianceDefinition>();
        for (var index = 1; index <= processCount; index++)
        {
            var player = new PlayerId($"chef-{index:D2}");
            var station = new StationSlotId($"stove-{index:D2}");
            players.Add(player, new CookingPlayerConfig(
                player,
                new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new HashSet<string>(StringComparer.Ordinal) { station.Value }));
            appliances.Add(station, new CookingApplianceDefinition(
                station,
                new HashSet<string>(StringComparer.Ordinal) { "heat" }));
        }

        var items = new Dictionary<DefinitionId, CookingItemDefinition>
        {
            [RawIngredient] = new(RawIngredient, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [Product] = new(Product, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [PlateDefinition] = new(PlateDefinition, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new CookingItemContainerCapability(processCount, new HashSet<DefinitionId> { Product })),
        };
        var recipes = new Dictionary<RecipeId, CookingRecipeDefinition>
        {
            [Recipe] = new(Recipe, new[] { RawIngredient }, Product, new ProcessId("fixture-process"), "heat", 1),
        };
        var simulation = new CookingRecipeSimulation(
            new CookingRecipeFixture(scope, players, items, appliances, recipes),
            new RecordingOrderPort(accepted: true), allocator);
        simulation.AddItem(Plate, PlateDefinition, ItemLocation.World("plate-home"));
        return simulation;
    }

    private static ProcessId StartSingleProcess(CookingRecipeSimulation simulation, string suffix)
    {
        var input = new ItemId($"ingredient-{suffix}");
        simulation.AddItem(input, RawIngredient, ItemLocation.Station(Station));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.StartProcess, $"start-{suffix}", recipe: Recipe,
            item: input, station: Station, expectedVersion: 1)));
        return Assert.Single(simulation.Snapshot().Processes).Id;
    }

    private static void StartProcesses(CookingRecipeSimulation simulation, int processCount)
    {
        for (var index = 1; index <= processCount; index++)
        {
            var player = new PlayerId($"chef-{index:D2}");
            var station = new StationSlotId($"stove-{index:D2}");
            var input = new ItemId($"ingredient-{index:D2}");
            simulation.AddItem(input, RawIngredient, ItemLocation.Station(station));
            AssertAccepted(simulation.Submit(new CookingRecipeCommand(
                new CookingScope(Session, World, Match),
                10,
                player,
                new RecipeCommandId($"start-{index:D2}"),
                CookingRecipeOperation.StartProcess,
                Recipe,
                null,
                input,
                station,
                null,
                null,
                1,
                0)));
        }
    }

    private static CookingRecipeCommand Command(CookingRecipeOperation operation, string commandId, RecipeId? recipe = null,
        ProcessId? process = null, ItemId? item = null, StationSlotId? station = null, ItemId? container = null,
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

    private sealed class ThrowingOrderPort : ICookingOrderPort
    {
        public CookingOrderAcceptance Submit(CookingOrderSubmission submission) =>
            throw new InvalidOperationException("Fixed Tick must not call the external order port.");
    }

    private sealed class RecordingProductIdAllocator : ICookingProductIdAllocator
    {
        public List<long> Requests { get; } = new();

        public ItemId GetProductId(long productSequence)
        {
            Requests.Add(productSequence);
            return new ItemId($"product-{productSequence}");
        }
    }

    private sealed class FixedProductIdAllocator(ItemId productId) : ICookingProductIdAllocator
    {
        public List<long> Requests { get; } = new();

        public ItemId GetProductId(long productSequence)
        {
            Requests.Add(productSequence);
            return productId;
        }
    }

    private sealed class ThrowingProductIdAllocator : ICookingProductIdAllocator
    {
        public List<long> Requests { get; } = new();

        public ItemId GetProductId(long productSequence)
        {
            Requests.Add(productSequence);
            throw new InvalidOperationException("allocator-fault");
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
