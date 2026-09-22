namespace AbilityKit.Game.Cooking;

/// <summary>
/// 一关一代只确认一次。身份是 Match、LevelId 与 LevelEpoch。
/// 载荷是交接前的结算条，不含评分、收益或长期进度。
/// </summary>
public enum CookingLevelSettlementConfirmationDisposition
{
    Confirmed,
    Duplicate,
    Rejected,
}

public enum CookingLevelSettlementConfirmationReason
{
    None,
    InvalidState,
    LedgerConflict,
}

public sealed record CookingLevelSettlementConfirmation(
    CookingLevelScope Scope,
    IReadOnlyList<CookingOrderSettlement> Settlements);

public sealed record CookingLevelSettlementConfirmationResult(
    bool Accepted,
    CookingLevelSettlementConfirmationDisposition Disposition,
    CookingLevelSettlementConfirmationReason Reason,
    CookingLevelSettlementConfirmation? Confirmation);

/// <summary>
/// 内存中的关卡确认账。同一代际同一份结算列表再确认一次是重复；
/// 同一代际换一份列表则拒绝，不覆盖第一次。不写文件，也不改长期进度。
/// </summary>
public sealed class CookingLevelSettlementLedger
{
    private readonly Dictionary<CookingLevelScope, CookingOrderSettlement[]> _confirmed = new();

    public CookingLevelSettlementConfirmationResult Confirm(
        CookingLevelScope scope,
        IReadOnlyList<CookingOrderSettlement> settlements)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(settlements);
        var snapshot = settlements.ToArray();
        if (_confirmed.TryGetValue(scope, out var existing))
        {
            if (!existing.SequenceEqual(snapshot))
            {
                return new CookingLevelSettlementConfirmationResult(
                    false,
                    CookingLevelSettlementConfirmationDisposition.Rejected,
                    CookingLevelSettlementConfirmationReason.LedgerConflict,
                    new CookingLevelSettlementConfirmation(scope, existing));
            }

            return new CookingLevelSettlementConfirmationResult(
                true,
                CookingLevelSettlementConfirmationDisposition.Duplicate,
                CookingLevelSettlementConfirmationReason.None,
                new CookingLevelSettlementConfirmation(scope, existing));
        }

        _confirmed.Add(scope, snapshot);
        return new CookingLevelSettlementConfirmationResult(
            true,
            CookingLevelSettlementConfirmationDisposition.Confirmed,
            CookingLevelSettlementConfirmationReason.None,
            new CookingLevelSettlementConfirmation(scope, snapshot));
    }

    public bool TryRead(CookingLevelScope scope, out CookingLevelSettlementConfirmation? confirmation)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (!_confirmed.TryGetValue(scope, out var existing))
        {
            confirmation = null;
            return false;
        }

        confirmation = new CookingLevelSettlementConfirmation(scope, existing);
        return true;
    }
}
