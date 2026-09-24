using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests.Harness;

/// <summary>
/// 代表性番茄蛋花汤全闭环可复用场景。
/// 流程：
/// 1. 取碗放台备用
/// 2. 取番茄放砧板切块并入锅
/// 3. 鸡蛋入碗手持打蛋，蛋液入锅
/// 4. 放下碗
/// 5. 灶台煮制番茄蛋花汤
/// 6. 端锅离灶倒汤入碗
/// 7. 提交订单
/// </summary>
public sealed class TomatoEggSoupScenario : ICookingScenario
{
    public string ScenarioName => "TomatoEggSoupLoop";

    private readonly PlayerId _chefA;
    private readonly PlayerId _chefB;

    private static readonly StationSlotId Board = new("board-a");
    private static readonly StationSlotId Stove = new("stove-a");
    private static readonly StationSlotId Counter = new("counter-a");

    private static readonly DefinitionId ChoppedTomato = new("chopped-tomato");
    private static readonly DefinitionId Soup = new("tomato-egg-soup");
    private static readonly RecipeId ChopRecipe = new("chop-tomato");
    private static readonly RecipeId BeatRecipe = new("beat-egg");
    private static readonly RecipeId SoupRecipe = new("tomato-egg-soup");
    private static readonly OrderId SoupOrder = new("order-soup-1");

    private static readonly ItemId Pot = new("pot-1");
    private static readonly ItemId Bowl = new("pool-bowl-1");
    private static readonly ItemId TomatoItem = new("tomato-1");
    private static readonly ItemId EggItem = new("egg-1");

    public TomatoEggSoupScenario(PlayerId chefA, PlayerId? chefB = null)
    {
        _chefA = chefA;
        _chefB = chefB ?? chefA;
    }

    public async Task ExecuteAsync(ICookingTestTopology topology, CancellationToken ct = default)
    {
        var chefA = topology.GetActor(_chefA);
        var chefB = topology.GetActor(_chefB);

        // 0. 从 clean-pool 取碗并摆放到 Counter (料理台) 备用
        var pickPoolBowl = await chefA.PickupAsync(Bowl, ct);
        Assert.Equal(CookingRecipeOutcome.Accepted, pickPoolBowl.Outcome);
        var dropBowl = await chefA.DropAsync(Bowl, Counter, ct);
        Assert.Equal(CookingRecipeOutcome.Accepted, dropBowl.Outcome);

        // 1. Chef-A 取番茄并放在砧板上
        var pickupRes = await chefA.PickupAsync(TomatoItem, ct);
        Assert.Equal(CookingRecipeOutcome.Accepted, pickupRes.Outcome);
        var dropRes = await chefA.DropAsync(TomatoItem, Board, ct);
        Assert.Equal(CookingRecipeOutcome.Accepted, dropRes.Outcome);

        // 2. Chef-A 在砧板切番茄 (2 ticks)
        var startChop = await chefA.StartProcessAsync(ChopRecipe, TomatoItem, Board, ct);
        Assert.Equal(CookingRecipeOutcome.Accepted, startChop.Outcome);
        await topology.AdvanceTicksAsync(2, ct);
        topology.AssertStateHashConsensus();

        // 获取切好的番茄产品
        var chopped = Assert.Single(chefA.GetCurrentSnapshot().Items, item => item.IsProduct && item.Definition == ChoppedTomato);
        Assert.Equal(ItemLocation.Station(Board), chopped.Location);

        // 3. Chef-A 拾取番茄块并放入锅中
        var pickChopped = await chefA.PickupAsync(chopped.Id, ct);
        Assert.Equal(CookingRecipeOutcome.Accepted, pickChopped.Outcome);
        var putTomato = await chefA.PutInContainerAsync(chopped.Id, Pot, ct);
        Assert.Equal(CookingRecipeOutcome.Accepted, putTomato.Outcome);

        // 4. Chef-B 取鸡蛋 -> 放入碗 -> 拾起碗 -> 手持碗打蛋 (2 ticks)
        var pickEgg = await chefB.PickupAsync(EggItem, ct);
        Assert.Equal(CookingRecipeOutcome.Accepted, pickEgg.Outcome);
        var putEgg = await chefB.PutInContainerAsync(EggItem, Bowl, ct);
        Assert.Equal(CookingRecipeOutcome.Accepted, putEgg.Outcome);

        var pickBowl = await chefB.PickupAsync(Bowl, ct);
        Assert.Equal(CookingRecipeOutcome.Accepted, pickBowl.Outcome);
        var startBeat = await chefB.StartProcessAsync(BeatRecipe, Bowl, station: null, ct);
        Assert.Equal(CookingRecipeOutcome.Accepted, startBeat.Outcome);
        await topology.AdvanceTicksAsync(2, ct);
        topology.AssertStateHashConsensus();

        // 5. 蛋液倒入锅中
        var pourEgg = await chefB.PlateAsync(Bowl, Pot, ct);
        Assert.Equal(CookingRecipeOutcome.Accepted, pourEgg.Outcome);

        // 5.1 Chef-B 将空出来的碗放回料理台 Counter
        var dropBowlBack = await chefB.DropAsync(Bowl, Counter, ct);
        Assert.Equal(CookingRecipeOutcome.Accepted, dropBowlBack.Outcome);

        // 6. 锅在灶台上启动番茄蛋花汤煮制 (正式配置需要 6 ticks)
        var startSoup = await chefA.StartProcessAsync(SoupRecipe, Pot, Stove, ct);
        Assert.Equal(CookingRecipeOutcome.Accepted, startSoup.Outcome);
        await topology.AdvanceTicksAsync(6, ct);
        topology.AssertStateHashConsensus();

        // 7. Chef-A 端锅离灶台，煮好的汤倒入碗中
        var pickPot = await chefA.PickupAsync(Pot, ct);
        Assert.Equal(CookingRecipeOutcome.Accepted, pickPot.Outcome);
        var pourSoup = await chefA.PlateAsync(Pot, Bowl, ct);
        Assert.Equal(CookingRecipeOutcome.Accepted, pourSoup.Outcome);

        // 8. 提交订单 (提交产品汤的 ItemId)
        var soupItem = Assert.Single(chefA.GetCurrentSnapshot().Items, item => item.Definition == Soup);
        var submit = await chefA.SubmitOrderAsync(soupItem.Id, SoupOrder, ct);
        Assert.Equal(CookingRecipeOutcome.Accepted, submit.Outcome);

        // 最终一致性断言
        topology.AssertStateHashConsensus();
    }
}
