using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

/// <summary>
/// 任务 <c>09-21-cooking-kitchen-loop-simulation</c> 的番茄蛋花汤闭环 fixture：
/// 从标准初始供应出发，走完取料、预处理、多输入煮制、端走继续、倒出、提交、洗碗回池全链路，
/// 并以确定快照哈希与可重复运行验收。工位绑定按加工区分：切→砧板、煮→灶台、烤→烤箱、打蛋→免工位。
/// </summary>
[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingKitchenLoopFixtureTests
{
    private static readonly SessionId Session = new("loop-session");
    private static readonly WorldId World = new("loop-world");
    private static readonly MatchId Match = new("loop-match");
    private static readonly PlayerId Player = new("chef-a");
    private static readonly StationSlotId Board = new("board-a");
    private static readonly StationSlotId Stove = new("stove-a");
    private static readonly StationSlotId Oven = new("oven-a");
    private static readonly StationSlotId Counter = new("counter-a");
    private static readonly DefinitionId Tomato = new("tomato");
    private static readonly DefinitionId ChoppedTomato = new("chopped-tomato");
    private static readonly DefinitionId Egg = new("egg");
    private static readonly DefinitionId BeatenEgg = new("beaten-egg");
    private static readonly DefinitionId Water = new("water");
    private static readonly DefinitionId Soup = new("tomato-egg-soup");
    private static readonly DefinitionId Dough = new("dough");
    private static readonly DefinitionId BreadSlice = new("bread-slice");
    private static readonly DefinitionId BowlDefinition = new("bowl");
    private static readonly DefinitionId PotDefinition = new("pot");
    private static readonly RecipeId ChopRecipe = new("chop-tomato");
    private static readonly RecipeId BeatRecipe = new("beat-egg");
    private static readonly RecipeId SoupRecipe = new("tomato-egg-soup");
    private static readonly RecipeId BakeRecipe = new("bake-bread");
    private static readonly OrderId SoupOrder = new("order-soup-1");
    private static readonly ItemId Pot = new("pot-1");
    private static readonly ItemId Bowl = new("pool-bowl-1");

    [Fact]
    public void L01_full_tomato_egg_soup_loop_from_the_standard_initial_supply()
    {
        using var evidence = CreateEvidence("L01");
        var fixture = CreateFixture();
        var simulation = fixture.Simulation;
        StageBowlOnCounter(simulation);

        // 取番茄 -> 放砧板 -> 切（单输入预处理，消耗番茄、在砧板生成番茄块）。
        PickupAndDrop(simulation, evidence, "L01", new ItemId("tomato-1"), Tomato, Board);
        StartAndFinish(simulation, evidence, "L01", new ItemId("tomato-1"), ChopRecipe, Board, 2, useFixedTick: false);
        var chopped = Assert.Single(simulation.Snapshot().Items, item => item.IsProduct);
        Assert.Equal(ChoppedTomato, chopped.Definition);
        Assert.Equal(ItemLocation.Station(Board), chopped.Location);

        // 番茄块入锅。
        MoveIntoContainer(simulation, evidence, "L01", chopped.Id, Pot);

        // 鸡蛋入碗 -> 手持碗免工位打蛋 -> 蛋液留碗。
        var egg = simulation.Snapshot().Items.Single(item => item.Id == new ItemId("egg-1"));
        AssertAccepted(Submit(simulation, evidence, "L01", Command(CookingRecipeOperation.Pickup, "pickup-egg",
            item: egg.Id, expectedVersion: egg.Version), "pick up the egg"));
        var heldEgg = simulation.Snapshot().Items.Single(item => item.Id == egg.Id);
        AssertAccepted(Submit(simulation, evidence, "L01", Command(CookingRecipeOperation.PutIn, "egg-into-bowl",
            item: egg.Id, container: Bowl, expectedVersion: heldEgg.Version), "put the egg into the bowl"));
        var bowlWithEgg = simulation.Snapshot().Items.Single(item => item.Id == Bowl);
        AssertAccepted(Submit(simulation, evidence, "L01", Command(CookingRecipeOperation.Pickup, "pickup-bowl",
            item: Bowl, expectedVersion: bowlWithEgg.Version), "pick up the bowl holding the egg"));
        var heldBowl = simulation.Snapshot().Items.Single(item => item.Id == Bowl);
        AssertAccepted(Submit(simulation, evidence, "L01", Command(CookingRecipeOperation.StartProcess, "beat-start",
            item: Bowl, expectedVersion: heldBowl.Version), "beating eggs needs no station"));
        simulation.AdvanceFixedTick(LevelScope(), 1);
        simulation.AdvanceFixedTick(LevelScope(), 2);
        var liquid = Assert.Single(simulation.Snapshot().Items, item => item.Definition == BeatenEgg);
        Assert.True(liquid.IsProduct);
        Assert.Equal(ItemLocation.Container(Bowl, "slot-0"), liquid.Location);

        // 蛋液倒进锅，碗倒空复用。
        var heldBowlWithLiquid = simulation.Snapshot().Items.Single(item => item.Id == Bowl);
        AssertAccepted(Submit(simulation, evidence, "L01", Command(CookingRecipeOperation.Pour, "pour-egg",
            item: Bowl, container: Pot, expectedVersion: heldBowlWithLiquid.Version),
            "pour the egg liquid into the pot and free the bowl"));
        Assert.Empty(simulation.ItemsInContainer(Bowl));
        var emptiedBowl = simulation.Snapshot().Items.Single(item => item.Id == Bowl);
        AssertAccepted(Submit(simulation, evidence, "L01", Command(CookingRecipeOperation.Drop, "bowl-back",
            item: Bowl, station: Counter, expectedVersion: emptiedBowl.Version),
            "put the emptied bowl back on the counter for reuse"));

        // 启动煮制（多输入集合匹配，水是默认供应），到点完成切“已完成”，输入保留。
        var potReady = simulation.Snapshot().Items.Single(item => item.Id == Pot);
        AssertAccepted(Submit(simulation, evidence, "L01", Command(CookingRecipeOperation.StartProcess, "soup-start",
            item: Pot, station: Stove, expectedVersion: potReady.Version), "start the multi-input cooking"));
        for (var frame = 3; frame <= 8; frame++)
            simulation.AdvanceFixedTick(LevelScope(), frame);
        Assert.True(simulation.Snapshot().Items.Single(item => item.Id == Pot).ContainerCompleted);
        Assert.Empty(simulation.Snapshot().Processes);

        // 端走锅：把锅从灶台端走，加工已完成、进度保留在“已完成”状态上。
        var completedPot = simulation.Snapshot().Items.Single(item => item.Id == Pot);
        AssertAccepted(Submit(simulation, evidence, "L01", Command(CookingRecipeOperation.Pickup, "carry-pot",
            item: Pot, expectedVersion: completedPot.Version), "carry the completed pot away from the stove"));

        // 倒出：碗中生成蛋花汤。
        var carriedPot = simulation.Snapshot().Items.Single(item => item.Id == Pot);
        AssertAccepted(Submit(simulation, evidence, "L01", Command(CookingRecipeOperation.Pour, "pour-soup",
            item: Pot, container: Bowl, expectedVersion: carriedPot.Version), "pour the soup into the bowl"));
        var soup = Assert.Single(simulation.Snapshot().Items, item => item.IsProduct);
        Assert.Equal(Soup, soup.Definition);
        Assert.Equal(ItemLocation.Container(Bowl, "slot-0"), soup.Location);
        Assert.Empty(simulation.ItemsInContainer(Pot));

        // 提交碗（含汤）到订单：订单完成一次，碗脏并交给 NPC。
        AssertAccepted(Submit(simulation, evidence, "L01", Command(CookingRecipeOperation.SubmitOrder, "submit-soup",
            item: soup.Id, order: SoupOrder, expectedVersion: soup.Version), "submit the bowl of soup to the order"));
        Assert.Equal(1, fixture.OrderPort.CompletedOrders.Count);
        Assert.Equal(SoupOrder, fixture.OrderPort.CompletedOrders[0]);
        Assert.Single(fixture.WashPort.Requests);
        Assert.DoesNotContain(simulation.Snapshot().Items, item => item.Id == Bowl);
        Assert.Equal(1, simulation.CleanContainerCount(BowlDefinition));

        // 注入清洗完成：碗回池。
        var washed = simulation.CompleteWash(Bowl);
        Assert.True(washed.Accepted);
        var returned = Assert.Single(simulation.Snapshot().Items, item => item.Id == Bowl);
        Assert.False(returned.IsDirty);
        Assert.Equal(ItemLocation.World("clean-pool"), returned.Location);
        Assert.Equal(2, simulation.CleanContainerCount(BowlDefinition));

        var first = simulation.Snapshot().Sha256();
        Assert.False(string.IsNullOrWhiteSpace(first));
        AssertEvidence(evidence.Path, "L01", 16);
    }

    [Fact]
    public void L02_the_same_loop_reaches_the_same_canonical_state_when_replayed()
    {
        var first = RunLoop();
        var second = RunLoop();

        Assert.Equal(first.Snapshot().CanonicalText(), second.Snapshot().CanonicalText());
        Assert.Equal(first.Snapshot().Sha256(), second.Snapshot().Sha256());
    }

    [Fact]
    public void L03_order_requirement_rejects_a_foreign_product_without_rollback()
    {
        using var evidence = CreateEvidence("L03");
        var fixture = CreateFixture();
        var simulation = fixture.Simulation;
        StageBowlOnCounter(simulation);

        simulation.AddWorldIngredient(new ItemId("dough-1"), Dough, "pantry");
        var dough = simulation.Snapshot().Items.Single(item => item.Id == new ItemId("dough-1"));
        AssertAccepted(Submit(simulation, evidence, "L03", Command(CookingRecipeOperation.Pickup, "pickup-dough",
            item: dough.Id, expectedVersion: dough.Version), "pick up the dough"));
        var heldDough = simulation.Snapshot().Items.Single(item => item.Id == dough.Id);
        AssertAccepted(Submit(simulation, evidence, "L03", Command(CookingRecipeOperation.Drop, "drop-dough",
            item: dough.Id, station: Oven, expectedVersion: heldDough.Version), "drop the dough onto the oven"));
        var platedDough = simulation.Snapshot().Items.Single(item => item.Id == dough.Id);
        AssertAccepted(Submit(simulation, evidence, "L03", Command(CookingRecipeOperation.StartProcess, "bake-start",
            item: dough.Id, station: Oven, expectedVersion: platedDough.Version), "the oven bakes with a single input"));
        AssertAccepted(Submit(simulation, evidence, "L03", Command(CookingRecipeOperation.AdvanceTicks, "bake-complete",
            process: simulation.Snapshot().Processes.Single().Id, ticks: 2), "baking completes on the oven"));
        var bread = Assert.Single(simulation.Snapshot().Items, item => item.IsProduct);
        Assert.Equal(BreadSlice, bread.Definition);
        Assert.Equal(ItemLocation.Station(Oven), bread.Location);

        var heldBread = simulation.Snapshot().Items.Single(item => item.Id == bread.Id);
        AssertAccepted(Submit(simulation, evidence, "L03", Command(CookingRecipeOperation.Pickup, "pickup-bread",
            item: bread.Id, expectedVersion: heldBread.Version), "pick up the baked bread"));
        var inHand = simulation.Snapshot().Items.Single(item => item.Id == bread.Id);
        AssertAccepted(Submit(simulation, evidence, "L03", Command(CookingRecipeOperation.PutIn, "bread-into-bowl",
            item: bread.Id, container: Bowl, expectedVersion: inHand.Version), "put the bread into the bowl"));
        var plated = simulation.Snapshot().Items.Single(item => item.Id == bread.Id);
        var before = simulation.Snapshot().CanonicalText();

        var rejected = Submit(simulation, evidence, "L03", Command(CookingRecipeOperation.SubmitOrder, "submit-bread",
            item: bread.Id, order: SoupOrder, expectedVersion: plated.Version),
            "the order requires the soup recipe, so the bread submission is rejected");

        AssertRejected(rejected, CookingRecipeRejectionReason.OrderRejected);
        Assert.Equal(before, simulation.Snapshot().CanonicalText());
        Assert.Empty(fixture.OrderPort.CompletedOrders);
        Assert.Equal(new[] { bread.Id }, simulation.ItemsInContainer(Bowl));
        AssertEvidence(evidence.Path, "L03", 7);
    }

    [Fact]
    public void L04_clean_bowl_pool_enforces_the_configured_cap()
    {
        var simulation = CreateFixture().Simulation;
        Assert.Equal(2, simulation.CleanContainerCount(BowlDefinition));
        Assert.Equal(2, simulation.Snapshot().Items.Count(item => item.Definition == BowlDefinition));

        var wash = simulation.CompleteWash(Bowl);
        Assert.False(wash.Accepted);
        Assert.Equal("BowlNotAwaitingWash", wash.ReasonCode);
        Assert.Equal(2, simulation.CleanContainerCount(BowlDefinition));
    }

    [Fact]
    public void L05_station_binding_follows_the_process_kind()
    {
        var simulation = CreateFixture().Simulation;
        var tomato = simulation.Snapshot().Items.Single(item => item.Id == new ItemId("tomato-1"));
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup", item: tomato.Id,
            expectedVersion: tomato.Version)));

        var wrongStation = simulation.Submit(Command(CookingRecipeOperation.StartProcess, "chop-at-stove",
            item: new ItemId("tomato-1"), station: Stove, expectedVersion: tomato.Version + 1));
        AssertRejected(wrongStation, CookingRecipeRejectionReason.ApplianceCapabilityMismatch);

        var rightStation = simulation.Submit(Command(CookingRecipeOperation.StartProcess, "chop-at-board",
            item: new ItemId("tomato-1"), station: Board, expectedVersion: tomato.Version + 1));
        AssertAccepted(rightStation);
    }

    private static CookingRecipeSimulation RunLoop()
    {
        var fixture = CreateFixture();
        var simulation = fixture.Simulation;
        StageBowlOnCounter(simulation);

        var tomato = simulation.Snapshot().Items.Single(item => item.Id == new ItemId("tomato-1"));
        simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup-tomato", item: tomato.Id,
            expectedVersion: tomato.Version));
        var heldTomato = simulation.Snapshot().Items.Single(item => item.Id == tomato.Id);
        simulation.Submit(Command(CookingRecipeOperation.Drop, "drop-tomato", item: tomato.Id, station: Board,
            expectedVersion: heldTomato.Version));
        var platedTomato = simulation.Snapshot().Items.Single(item => item.Id == tomato.Id);
        simulation.Submit(Command(CookingRecipeOperation.StartProcess, "chop-start", item: tomato.Id, station: Board,
            expectedVersion: platedTomato.Version));
        simulation.Submit(Command(CookingRecipeOperation.AdvanceTicks, "chop-complete",
            process: simulation.Snapshot().Processes.Single().Id, ticks: 2));
        var chopped = simulation.Snapshot().Items.Single(item => item.IsProduct);
        simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup-chopped", item: chopped.Id,
            expectedVersion: chopped.Version));
        var heldChopped = simulation.Snapshot().Items.Single(item => item.Id == chopped.Id);
        simulation.Submit(Command(CookingRecipeOperation.PutIn, "chopped-into-pot", item: chopped.Id, container: Pot,
            expectedVersion: heldChopped.Version));

        var egg = simulation.Snapshot().Items.Single(item => item.Id == new ItemId("egg-1"));
        simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup-egg", item: egg.Id, expectedVersion: egg.Version));
        var heldEgg = simulation.Snapshot().Items.Single(item => item.Id == egg.Id);
        simulation.Submit(Command(CookingRecipeOperation.PutIn, "egg-into-bowl", item: egg.Id, container: Bowl,
            expectedVersion: heldEgg.Version));
        var bowl = simulation.Snapshot().Items.Single(item => item.Id == Bowl);
        simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup-bowl", item: Bowl, expectedVersion: bowl.Version));
        var heldBowl = simulation.Snapshot().Items.Single(item => item.Id == Bowl);
        simulation.Submit(Command(CookingRecipeOperation.StartProcess, "beat-start", item: Bowl,
            expectedVersion: heldBowl.Version));
        simulation.AdvanceFixedTick(LevelScope(), 1);
        simulation.AdvanceFixedTick(LevelScope(), 2);
        var heldBowlWithLiquid = simulation.Snapshot().Items.Single(item => item.Id == Bowl);
        simulation.Submit(Command(CookingRecipeOperation.Pour, "pour-egg", item: Bowl, container: Pot,
            expectedVersion: heldBowlWithLiquid.Version));
        var emptiedBowl = simulation.Snapshot().Items.Single(item => item.Id == Bowl);
        simulation.Submit(Command(CookingRecipeOperation.Drop, "bowl-back", item: Bowl, station: Counter,
            expectedVersion: emptiedBowl.Version));

        var pot = simulation.Snapshot().Items.Single(item => item.Id == Pot);
        simulation.Submit(Command(CookingRecipeOperation.StartProcess, "soup-start", item: Pot, station: Stove,
            expectedVersion: pot.Version));
        for (var frame = 3; frame <= 8; frame++)
            simulation.AdvanceFixedTick(LevelScope(), frame);
        var completedPot = simulation.Snapshot().Items.Single(item => item.Id == Pot);
        simulation.Submit(Command(CookingRecipeOperation.Pickup, "carry-pot", item: Pot,
            expectedVersion: completedPot.Version));
        var carriedPot = simulation.Snapshot().Items.Single(item => item.Id == Pot);
        simulation.Submit(Command(CookingRecipeOperation.Pour, "pour-soup", item: Pot, container: Bowl,
            expectedVersion: carriedPot.Version));
        var soup = simulation.Snapshot().Items.Single(item => item.Definition == Soup);
        simulation.Submit(Command(CookingRecipeOperation.SubmitOrder, "submit-soup", item: soup.Id, order: SoupOrder,
            expectedVersion: soup.Version));
        simulation.CompleteWash(Bowl);
        return simulation;
    }

    private static void StageBowlOnCounter(CookingRecipeSimulation simulation)
    {
        var pooled = simulation.Snapshot().Items.Single(item => item.Id == Bowl);
        Assert.Equal(ItemLocation.World("clean-pool"), pooled.Location);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Pickup, "pickup-pool-bowl",
            item: Bowl, expectedVersion: pooled.Version)));
        var held = simulation.Snapshot().Items.Single(item => item.Id == Bowl);
        AssertAccepted(simulation.Submit(Command(CookingRecipeOperation.Drop, "drop-bowl",
            item: Bowl, station: Counter, expectedVersion: held.Version)));
    }

    private static void PickupAndDrop(CookingRecipeSimulation simulation, EvidenceScope evidence, string testId,
        ItemId item, DefinitionId definition, StationSlotId station)
    {
        var state = simulation.Snapshot().Items.Single(candidate => candidate.Id == item);
        AssertAccepted(Submit(simulation, evidence, testId, Command(CookingRecipeOperation.Pickup, $"pickup-{item.Value}",
            item: item, expectedVersion: state.Version), $"pick up the {definition.Value}"));
        var held = simulation.Snapshot().Items.Single(candidate => candidate.Id == item);
        AssertAccepted(Submit(simulation, evidence, testId, Command(CookingRecipeOperation.Drop, $"drop-{item.Value}",
            item: item, station: station, expectedVersion: held.Version), $"drop it onto {station.Value}"));
    }

    private static void StartAndFinish(CookingRecipeSimulation simulation, EvidenceScope evidence, string testId,
        ItemId item, RecipeId recipe, StationSlotId station, int ticks, bool useFixedTick)
    {
        var state = simulation.Snapshot().Items.Single(candidate => candidate.Id == item);
        AssertAccepted(Submit(simulation, evidence, testId, Command(CookingRecipeOperation.StartProcess,
            $"start-{item.Value}", recipe: recipe, item: item, station: station, expectedVersion: state.Version),
            $"start the {recipe.Value} process"));
        var process = simulation.Snapshot().Processes.Single();
        if (useFixedTick)
        {
            for (var frame = 1; frame <= ticks; frame++)
                simulation.AdvanceFixedTick(LevelScope(), frame);
        }
        else
        {
            AssertAccepted(Submit(simulation, evidence, testId, Command(CookingRecipeOperation.AdvanceTicks,
                $"complete-{item.Value}", process: process.Id, ticks: ticks), "finish the process"));
        }
    }

    private static void MoveIntoContainer(CookingRecipeSimulation simulation, EvidenceScope evidence, string testId,
        ItemId item, ItemId container)
    {
        var state = simulation.Snapshot().Items.Single(candidate => candidate.Id == item);
        AssertAccepted(Submit(simulation, evidence, testId, Command(CookingRecipeOperation.Pickup, $"pickup-{item.Value}",
            item: item, expectedVersion: state.Version), "pick up the semi-product"));
        var held = simulation.Snapshot().Items.Single(candidate => candidate.Id == item);
        AssertAccepted(Submit(simulation, evidence, testId, Command(CookingRecipeOperation.PutIn, $"into-{container.Value}",
            item: item, container: container, expectedVersion: held.Version), "put it into the pot"));
    }

    private sealed class Fixture
    {
        public CookingRecipeSimulation Simulation { get; }
        public RequirementOrderPort OrderPort { get; }
        public RecordingWashPort WashPort { get; }

        public Fixture(CookingRecipeSimulation simulation, RequirementOrderPort orderPort, RecordingWashPort washPort)
        {
            Simulation = simulation;
            OrderPort = orderPort;
            WashPort = washPort;
        }
    }

    private static Fixture CreateFixture()
    {
        var scope = new CookingScope(Session, World, Match);
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [Player] = new(Player, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new HashSet<string>(StringComparer.Ordinal) { Board.Value, Stove.Value, Oven.Value, Counter.Value }),
        };
        var items = new Dictionary<DefinitionId, CookingItemDefinition>
        {
            [Tomato] = new(Tomato, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [ChoppedTomato] = new(ChoppedTomato, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [Egg] = new(Egg, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [BeatenEgg] = new(BeatenEgg, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [Water] = new(Water, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [Soup] = new(Soup, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [Dough] = new(Dough, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [BreadSlice] = new(BreadSlice, new HashSet<string>(StringComparer.Ordinal) { "cook" }),
            [BowlDefinition] = new(BowlDefinition, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new CookingItemContainerCapability(1, new HashSet<DefinitionId> { Soup, BreadSlice, BeatenEgg, Egg })),
            [PotDefinition] = new(PotDefinition, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                new CookingItemContainerCapability(4, new HashSet<DefinitionId> { ChoppedTomato, BeatenEgg })),
        };
        var appliances = new Dictionary<StationSlotId, CookingApplianceDefinition>
        {
            [Board] = new(Board, new HashSet<string>(StringComparer.Ordinal) { "cut" }),
            [Stove] = new(Stove, new HashSet<string>(StringComparer.Ordinal) { "heat" }),
            [Oven] = new(Oven, new HashSet<string>(StringComparer.Ordinal) { "bake" }),
            [Counter] = new(Counter, new HashSet<string>(StringComparer.Ordinal)),
        };
        var recipes = new Dictionary<RecipeId, CookingRecipeDefinition>
        {
            [ChopRecipe] = new(ChopRecipe, new[] { Tomato }, ChoppedTomato, new ProcessId("chop-process"), "cut", 2),
            [BeatRecipe] = new(BeatRecipe, new[] { Egg }, BeatenEgg, new ProcessId("beat-process"), "beat", 2,
                Completion: CookingRecipeCompletionKind.ConsumeInputs, RequiresStation: false),
            [SoupRecipe] = new(SoupRecipe, new[] { ChoppedTomato, BeatenEgg }, Soup, new ProcessId("soup-process"),
                "heat", 6, new[] { Water }, CookingRecipeCompletionKind.RetainInputs),
            [BakeRecipe] = new(BakeRecipe, new[] { Dough }, BreadSlice, new ProcessId("bake-process"), "bake", 2),
        };
        var orderPort = new RequirementOrderPort(SoupRecipe);
        var washPort = new RecordingWashPort();
        var simulation = new CookingRecipeSimulation(
            new CookingRecipeFixture(scope, players, items, appliances, recipes,
                new HashSet<DefinitionId> { BowlDefinition },
                new Dictionary<DefinitionId, int> { [BowlDefinition] = 2 },
                "clean-pool"),
            orderPort, null, washPort);
        simulation.AddItem(Pot, PotDefinition, ItemLocation.Station(Stove));
        simulation.AddWorldIngredient(new ItemId("tomato-1"), Tomato, "pantry");
        simulation.AddWorldIngredient(new ItemId("egg-1"), Egg, "pantry");
        return new Fixture(simulation, orderPort, washPort);
    }

    private static CookingLevelScope LevelScope() => new(
        new CookingScope(Session, World, Match), new RestaurantRuntimeId(1), new LevelId("loop-level"), 1);

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
            testId, "p2-kitchen-loop-fixture", command, simulation.LogicalTick, result.Outcome.ToString(), result.Reason.ToString(),
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

    private sealed class RequirementOrderPort(RecipeId required) : ICookingOrderPort
    {
        public List<OrderId> CompletedOrders { get; } = new();

        public CookingOrderAcceptance Submit(CookingOrderSubmission submission)
        {
            if (submission.Recipe != required)
                return new CookingOrderAcceptance(false, "RequirementMismatch");
            CompletedOrders.Add(submission.Order);
            return new CookingOrderAcceptance(true, "fixture-accepted", true);
        }
    }

    private sealed class RecordingWashPort : ICookingBowlWashingPort
    {
        public List<ItemId> Requests { get; } = new();

        public void RequestWash(ItemId bowl, DefinitionId definition) => Requests.Add(bowl);
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
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AbilityKit.Game.Cooking.Tests", "loop");
            _directory = System.IO.Path.Combine(root, testId, Guid.NewGuid().ToString("N"));
            Path = System.IO.Path.Combine(_directory, "loop-acceptance.jsonl");
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
