using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

/// <summary>
/// 任务 <c>09-22-cooking-level-settlement-confirmation</c>：一关一代只确认一次。
/// 确认只记住结算条，不打分，不加钱。
/// </summary>
[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingLevelSettlementConfirmationTests
{
    private static readonly CookingLevelScope Scope = new(
        new CookingScope(new SessionId("session"), new WorldId("world"), new MatchId("match")),
        new RestaurantRuntimeId(1),
        new LevelId("level-1"),
        1);

    [Fact]
    public void S01_confirmation_keeps_the_settlement_list_and_rejects_a_different_one()
    {
        var ledger = new CookingLevelSettlementLedger();
        var first = Settlement(1, "order-1");
        var second = Settlement(2, "order-2");

        var confirmed = ledger.Confirm(Scope, new[] { first, second });
        var duplicate = ledger.Confirm(Scope, new[] { first, second });
        var conflict = ledger.Confirm(Scope, new[] { first });

        Assert.Equal(CookingLevelSettlementConfirmationDisposition.Confirmed, confirmed.Disposition);
        Assert.Equal(new[] { first, second }, confirmed.Confirmation!.Settlements);
        Assert.Equal(CookingLevelSettlementConfirmationDisposition.Duplicate, duplicate.Disposition);
        Assert.Equal(CookingLevelSettlementConfirmationDisposition.Rejected, conflict.Disposition);
        Assert.Equal(CookingLevelSettlementConfirmationReason.LedgerConflict, conflict.Reason);
        Assert.True(ledger.TryRead(Scope, out var stored));
        Assert.Equal(new[] { first, second }, stored!.Settlements);
    }

    [Fact]
    public void S02_an_empty_level_can_be_confirmed_and_a_later_epoch_is_separate()
    {
        var ledger = new CookingLevelSettlementLedger();
        var empty = ledger.Confirm(Scope, Array.Empty<CookingOrderSettlement>());
        var laterScope = new CookingLevelScope(Scope.MatchScope, Scope.RestaurantRuntime, Scope.Level, 2);
        var later = ledger.Confirm(laterScope, new[] { Settlement(1, "order-9") });

        Assert.Equal(CookingLevelSettlementConfirmationDisposition.Confirmed, empty.Disposition);
        Assert.Empty(empty.Confirmation!.Settlements);
        Assert.Equal(CookingLevelSettlementConfirmationDisposition.Confirmed, later.Disposition);
        Assert.True(ledger.TryRead(Scope, out var original));
        Assert.Empty(original!.Settlements);
    }

    private static CookingOrderSettlement Settlement(long sequence, string order) => new(
        sequence,
        new OrderId(order),
        new OrderTemplateId("template"),
        new RecipeId("recipe"),
        new ItemId("product"),
        new PlayerId("chef"),
        new ItemId("bowl"),
        sequence);
}
