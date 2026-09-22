using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

/// <summary>
/// 任务 <c>09-22-cooking-level-settlement-store</c>：已确认的结算列表按代际落盘。
/// 关掉读取器再开一个，读回的仍是同一份。不加钱，不解锁。
/// </summary>
[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingLevelSettlementStoreTests
{
    private static readonly CookingLevelScope Scope = new(
        new CookingScope(new SessionId("session"), new WorldId("world"), new MatchId("match")),
        new RestaurantRuntimeId(1),
        new LevelId("level-1"),
        1);

    [Fact]
    public void D01_a_confirmed_list_is_reread_by_a_new_store_and_a_different_list_does_not_replace_it()
    {
        using var directory = new TempDirectory();
        var first = Settlement(1, "order-1");
        var second = Settlement(2, "order-2");
        var ledger = new CookingLevelSettlementLedger();
        var store = new CookingLevelSettlementStore(directory.Path);

        var written = CookingLevelSettlementStore.Commit(ledger, store, Scope, new[] { first, second });
        var again = new CookingLevelSettlementStore(directory.Path);
        var read = again.Read(Scope);
        var duplicate = again.Write(written.Confirmation!);
        var conflict = again.Write(new CookingLevelSettlementConfirmation(Scope, new[] { first }));
        var afterConflict = again.Read(Scope);

        Assert.Equal(CookingLevelSettlementConfirmationDisposition.Confirmed, written.Disposition);
        Assert.Equal(new[] { first, second }, read.Confirmation!.Settlements);
        Assert.Equal(CookingLevelSettlementStoreReason.Duplicate, duplicate.Reason);
        Assert.Equal(CookingLevelSettlementStoreReason.LedgerConflict, conflict.Reason);
        Assert.Equal(new[] { first, second }, afterConflict.Confirmation!.Settlements);
        Assert.True(ledger.TryRead(Scope, out var remembered));
        Assert.Equal(new[] { first, second }, remembered!.Settlements);
    }

    [Fact]
    public void D02_an_empty_confirmation_is_distinct_from_a_missing_record_and_a_later_epoch_is_separate()
    {
        using var directory = new TempDirectory();
        var store = new CookingLevelSettlementStore(directory.Path);
        var missing = store.Read(Scope);
        var empty = store.Write(new CookingLevelSettlementConfirmation(Scope, Array.Empty<CookingOrderSettlement>()));
        var laterScope = new CookingLevelScope(Scope.MatchScope, Scope.RestaurantRuntime, Scope.Level, 2);
        var later = store.Write(new CookingLevelSettlementConfirmation(laterScope, new[] { Settlement(1, "order-9") }));
        var reread = new CookingLevelSettlementStore(directory.Path);

        Assert.Equal(CookingLevelSettlementStoreReason.Missing, missing.Reason);
        Assert.Equal(CookingLevelSettlementConfirmationDisposition.Confirmed, empty.Disposition);
        Assert.Empty(reread.Read(Scope).Confirmation!.Settlements);
        Assert.Equal(CookingLevelSettlementConfirmationDisposition.Confirmed, later.Disposition);
        Assert.Equal(new[] { Settlement(1, "order-9") }, reread.Read(laterScope).Confirmation!.Settlements);
    }

    [Fact]
    public void D03_a_truncated_or_tampered_record_is_rejected_and_a_failed_write_is_not_remembered()
    {
        using var directory = new TempDirectory();
        var ledger = new CookingLevelSettlementLedger();
        var store = new CookingLevelSettlementStore(directory.Path);
        var confirmation = new CookingLevelSettlementConfirmation(Scope, new[] { Settlement(1, "order-1") });
        Assert.Equal(CookingLevelSettlementConfirmationDisposition.Confirmed, store.Write(confirmation).Disposition);

        var path = Assert.Single(Directory.GetFiles(directory.Path, "*.settlement.json"));
        var original = File.ReadAllText(path);
        File.WriteAllText(path, original[..Math.Max(1, original.Length / 2)]);
        var truncated = new CookingLevelSettlementStore(directory.Path).Read(Scope);
        File.WriteAllText(path, original.Replace("order-1", "order-X", StringComparison.Ordinal));
        var tampered = new CookingLevelSettlementStore(directory.Path).Read(Scope);
        File.WriteAllText(path, original);

        var blockedRoot = Path.Combine(directory.Path, "not-a-directory");
        File.WriteAllText(blockedRoot, "occupied");
        var blocked = new CookingLevelSettlementStore(blockedRoot);
        var other = new CookingLevelScope(Scope.MatchScope, Scope.RestaurantRuntime, new LevelId("level-2"), 1);
        var failed = CookingLevelSettlementStore.Commit(
            ledger, blocked, other, new[] { Settlement(1, "order-2") });

        Assert.Equal(CookingLevelSettlementStoreReason.RecordTruncated, truncated.Reason);
        Assert.Null(truncated.Confirmation);
        Assert.Equal(CookingLevelSettlementStoreReason.IntegrityFailure, tampered.Reason);
        Assert.Equal(CookingLevelSettlementStoreReason.WriteFailed, failed.Reason);
        Assert.False(ledger.TryRead(other, out _));
        Assert.Equal(new[] { Settlement(1, "order-1") }, new CookingLevelSettlementStore(directory.Path).Read(Scope).Confirmation!.Settlements);
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

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cooking-settlement-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
