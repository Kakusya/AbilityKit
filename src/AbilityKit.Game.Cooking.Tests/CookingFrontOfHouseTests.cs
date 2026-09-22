using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

/// <summary>
/// 任务 <c>09-22-cooking-front-of-house</c>：问完才开单，空闲才洗碗，
/// 营业和收尾走完且座位空了才允许成功。
/// </summary>
[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingFrontOfHouseTests
{
    private static readonly SessionId Session = new("front-session");
    private static readonly WorldId World = new("front-world");
    private static readonly MatchId Match = new("front-match");
    private static readonly PlayerId Player = new("chef-a");
    private static readonly OrderTemplateId Template = new("tomato-egg-soup-order");
    private static readonly ItemId Bowl = new("pool-bowl-1");
    private static readonly DefinitionId BowlDefinition = new("bowl");

    [Fact]
    public void F01_an_order_opens_only_after_the_inquiry_finishes()
    {
        var house = House();
        var kitchen = Kitchen();

        Assert.Equal(1, house.Step(kitchen, Template).SeatedCount);
        Assert.Empty(kitchen.Orders);

        house.Step(kitchen, Template);
        Assert.Empty(kitchen.Orders);

        house.Step(kitchen, Template);
        var order = Assert.Single(kitchen.Orders);
        Assert.Equal("table-1-order", order.Id.Value);
        Assert.Equal(CookingOrderStatus.Open.ToString(), order.Status);
    }

    [Fact]
    public void F02_washing_waits_until_inquiry_is_finished()
    {
        var house = House();
        var kitchen = Kitchen();
        house.Step(kitchen, Template);
        Dirty(kitchen);

        house.Step(kitchen, Template);
        Assert.Contains(Bowl, kitchen.DirtyBowlsAwaitingWash());

        house.Step(kitchen, Template);
        Assert.Contains(Bowl, kitchen.DirtyBowlsAwaitingWash());

        house.Step(kitchen, Template);
        house.Step(kitchen, Template);
        Assert.DoesNotContain(Bowl, kitchen.DirtyBowlsAwaitingWash());
        Assert.Equal(2, kitchen.CleanContainerCount(BowlDefinition));
    }

    [Fact]
    public void F03_closing_admits_nobody_and_success_requires_empty_seats()
    {
        var house = new CookingFrontOfHouse(Schedule(serviceTicks: 1, waitLimitTicks: 4, diningTicks: 1));
        var kitchen = Kitchen();

        house.Step(kitchen, Template);
        Assert.True(house.IsClosing);
        Assert.Equal(1, house.Step(kitchen, Template).SeatedCount);
        Assert.False(house.CanSucceed);

        house.Step(kitchen, Template);
        var order = new OrderId("table-1-order");
        Assert.Equal(CookingOrderStatus.Open.ToString(), Status(kitchen, order));
        kitchen.MarkOrderUnsatisfied(order);
        Assert.Empty(kitchen.SettlementHistory);

        var left = house.Step(kitchen, Template);
        Assert.Equal(0, left.SeatedCount);
        Assert.True(left.CanSucceed);
        Assert.Empty(kitchen.SettlementHistory);
    }

    [Fact]
    public void F04_a_timeout_does_not_write_a_settlement()
    {
        var house = House(waitLimitTicks: 1);
        var kitchen = Kitchen();
        Assert.Equal(1, house.Step(kitchen, Template).SeatedCount);
        var left = house.Step(kitchen, Template);
        Assert.Equal(1, left.UnsatisfiedCount);
        Assert.Empty(kitchen.SettlementHistory);
        Assert.Empty(kitchen.Orders);
        Assert.Contains(new OrderId("table-1-order"), house.UnsatisfiedOrders);
    }

    [Fact]
    public void Q01_a_failed_retry_drops_the_front_of_house_without_touching_the_kitchen()
    {
        var house = new CookingFrontOfHouse(Schedule(serviceTicks: 2, waitLimitTicks: 1));
        var kitchen = Kitchen();
        var clean = kitchen.CleanContainerCount(BowlDefinition);
        house.Step(kitchen, Template);
        house.Step(kitchen, Template);
        var left = house.Step(kitchen, Template);
        Assert.Equal(1, left.UnsatisfiedCount);
        Dirty(kitchen);

        house.DropFailedScene();

        Assert.Equal(0, house.SeatedCount);
        Assert.False(house.IsClosing);
        Assert.Empty(house.UnsatisfiedOrders);
        Assert.Empty(kitchen.Orders);
        Assert.Equal(clean - 1, kitchen.CleanContainerCount(BowlDefinition));
        Assert.Contains(Bowl, kitchen.DirtyBowlsAwaitingWash());
    }

    [Fact]
    public void N01_the_next_level_clears_seats_and_keeps_a_dirty_bowl()
    {
        var house = new CookingFrontOfHouse(Schedule(serviceTicks: 2, waitLimitTicks: 1));
        var kitchen = Kitchen();
        house.Step(kitchen, Template);
        house.Step(kitchen, Template);
        Assert.True(house.IsClosing);
        var left = house.Step(kitchen, Template);
        Assert.Equal(1, left.UnsatisfiedCount);
        Dirty(kitchen);
        house.Step(kitchen, Template);
        Assert.Contains(Bowl, kitchen.DirtyBowlsAwaitingWash());

        house.ResetForNextLevel(kitchen, Template);

        Assert.Equal(0, house.SeatedCount);
        Assert.False(house.IsClosing);
        Assert.Empty(house.UnsatisfiedOrders);
        Assert.DoesNotContain(Bowl, kitchen.DirtyBowlsAwaitingWash());
        Assert.Equal(1, house.Step(kitchen, Template).SeatedCount);
        Assert.False(house.IsClosing);
    }

    [Fact]
    public void G01_a_served_guest_keeps_the_seat_until_dining_ends()
    {
        var house = new CookingFrontOfHouse(Schedule(serviceTicks: 1, diningTicks: 2));
        var kitchen = Kitchen();
        house.Step(kitchen, Template);
        house.Step(kitchen, Template);
        house.Step(kitchen, Template);
        var order = new OrderId("table-1-order");
        kitchen.MarkOrderCompletedForTest(order);

        Assert.Equal(1, house.Step(kitchen, Template).SeatedCount);
        Assert.False(house.CanSucceed);
        Assert.Equal(1, house.Step(kitchen, Template).SeatedCount);

        var left = house.Step(kitchen, Template);
        Assert.Equal(0, left.SeatedCount);
        Assert.True(left.CanSucceed);
        Assert.Empty(kitchen.SettlementHistory);
    }

    [Fact]
    public void F05_success_finish_completes_the_current_inquiry_and_wash()
    {
        var house = House();
        var kitchen = Kitchen();
        house.Step(kitchen, Template);
        house.Step(kitchen, Template);

        house.FinishInProgress(kitchen, Template);

        Assert.Equal(CookingOrderStatus.Open.ToString(), Status(kitchen, new OrderId("table-1-order")));
        Assert.True(house.CompanionIdle);

        Dirty(kitchen);
        house.Step(kitchen, Template);
        Assert.Contains(Bowl, kitchen.DirtyBowlsAwaitingWash());
        house.FinishInProgress(kitchen, Template);
        Assert.DoesNotContain(Bowl, kitchen.DirtyBowlsAwaitingWash());
        Assert.Equal(2, kitchen.CleanContainerCount(BowlDefinition));
    }

    private static CookingFrontOfHouse House(int waitLimitTicks = 9) =>
        new(Schedule(waitLimitTicks: waitLimitTicks));

    private static CookingFrontOfHouseSchedule Schedule(int serviceTicks = 8, int waitLimitTicks = 9, int diningTicks = 1) =>
        new(2, serviceTicks, 2, 2, 2, diningTicks, waitLimitTicks);

    private static CookingRecipeSimulation Kitchen()
    {
        var content = CookingContentCatalog.Load(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, CookingContentCatalog.ContentFileName)));
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [Player] = new(Player, new HashSet<string> { "carry" }, new HashSet<string> { "counter-a", "stove-a" }),
        };
        var fixture = CookingContentCatalog.BuildFixture(content, new CookingScope(Session, World, Match), players);
        var simulation = new CookingRecipeSimulation(fixture);
        CookingContentCatalog.ApplyStandardInitialSupply(simulation, content);
        return simulation;
    }

    private static void Dirty(CookingRecipeSimulation kitchen) => kitchen.MarkBowlDirtyForTest(Bowl);

    private static string Status(CookingRecipeSimulation kitchen, OrderId order) =>
        kitchen.Orders.Single(candidate => candidate.Id == order).Status;
}
