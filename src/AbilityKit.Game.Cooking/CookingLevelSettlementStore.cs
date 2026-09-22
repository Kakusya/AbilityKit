using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AbilityKit.Game.Cooking;

public enum CookingLevelSettlementStoreReason
{
    None,
    InvalidState,
    Missing,
    Duplicate,
    LedgerConflict,
    WriteFailed,
    RecordTruncated,
    RecordTooLarge,
    UnknownFormatVersion,
    IntegrityFailure,
}

public sealed record CookingLevelSettlementStoreResult(
    bool Accepted,
    CookingLevelSettlementConfirmationDisposition Disposition,
    CookingLevelSettlementStoreReason Reason,
    CookingLevelSettlementConfirmation? Confirmation);

/// <summary>
/// 一个代际一份已确认结算列表。目录根由调用方提供。
/// 同一列表再写是重复；换一份列表则拒绝，不覆盖第一次。
/// 不改长期进度，也不保存评分或收益。
/// </summary>
public sealed class CookingLevelSettlementStore
{
    public const int CurrentFormatVersion = 1;
    public const int MaximumRecordCharacters = 128 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly string _root;

    public CookingLevelSettlementStore(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("The settlement record directory is required.", nameof(root));
        _root = Path.GetFullPath(root);
    }

    public CookingLevelSettlementStoreResult Write(CookingLevelSettlementConfirmation confirmation)
    {
        ArgumentNullException.ThrowIfNull(confirmation);
        try
        {
            Directory.CreateDirectory(_root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new CookingLevelSettlementStoreResult(
                false,
                CookingLevelSettlementConfirmationDisposition.Rejected,
                CookingLevelSettlementStoreReason.WriteFailed,
                null);
        }

        var path = RecordPath(confirmation.Scope);
        var payload = Serialize(confirmation);
        if (File.Exists(path))
        {
            var existing = ReadRecord(path);
            if (!existing.Accepted || existing.Confirmation is null)
            {
                return new CookingLevelSettlementStoreResult(
                    false,
                    CookingLevelSettlementConfirmationDisposition.Rejected,
                    existing.Reason,
                    null);
            }

            if (!SameList(existing.Confirmation, confirmation))
            {
                return new CookingLevelSettlementStoreResult(
                    false,
                    CookingLevelSettlementConfirmationDisposition.Rejected,
                    CookingLevelSettlementStoreReason.LedgerConflict,
                    existing.Confirmation);
            }

            return new CookingLevelSettlementStoreResult(
                true,
                CookingLevelSettlementConfirmationDisposition.Duplicate,
                CookingLevelSettlementStoreReason.Duplicate,
                existing.Confirmation);
        }

        var temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, payload, Encoding.UTF8);
            File.Move(temporary, path, overwrite: false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TryDelete(temporary);
            return new CookingLevelSettlementStoreResult(
                false,
                CookingLevelSettlementConfirmationDisposition.Rejected,
                CookingLevelSettlementStoreReason.WriteFailed,
                null);
        }

        return new CookingLevelSettlementStoreResult(
            true,
            CookingLevelSettlementConfirmationDisposition.Confirmed,
            CookingLevelSettlementStoreReason.None,
            confirmation);
    }

    public CookingLevelSettlementStoreResult Read(CookingLevelScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var path = RecordPath(scope);
        if (!File.Exists(path))
        {
            return new CookingLevelSettlementStoreResult(
                false,
                CookingLevelSettlementConfirmationDisposition.Rejected,
                CookingLevelSettlementStoreReason.Missing,
                null);
        }

        var read = ReadRecord(path);
        return new CookingLevelSettlementStoreResult(
            read.Accepted,
            read.Accepted
                ? CookingLevelSettlementConfirmationDisposition.Confirmed
                : CookingLevelSettlementConfirmationDisposition.Rejected,
            read.Reason,
            read.Confirmation);
    }

    public static CookingLevelSettlementStoreResult Commit(
        CookingLevelSettlementLedger ledger,
        CookingLevelSettlementStore store,
        CookingLevelScope scope,
        IReadOnlyList<CookingOrderSettlement> settlements)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(store);
        var alreadyStored = ledger.TryRead(scope, out _);
        var confirmed = ledger.Confirm(scope, settlements);
        if (!confirmed.Accepted || confirmed.Confirmation is null)
            return FromLedger(confirmed);

        var written = store.Write(confirmed.Confirmation);
        if (written.Accepted || alreadyStored)
            return written;

        ledger.Forget(scope);
        return written;
    }

    private CookingLevelSettlementStoreResult ReadRecord(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Reject(CookingLevelSettlementStoreReason.WriteFailed);
        }

        return Deserialize(text);
    }

    private string RecordPath(CookingLevelScope scope)
    {
        var name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(IdentityText(scope))));
        return Path.Combine(_root, name + ".settlement.json");
    }

    internal static string Serialize(CookingLevelSettlementConfirmation confirmation)
    {
        var document = new SettlementDocument(
            CurrentFormatVersion,
            IdentityText(confirmation.Scope),
            confirmation.Settlements.Select(settlement => new SettlementLine(
                settlement.Sequence,
                settlement.Order.Value,
                settlement.Template.Value,
                settlement.Recipe.Value,
                settlement.Product.Value,
                settlement.Player.Value,
                settlement.Container.Value,
                settlement.LogicalTick)).ToArray(),
            "");
        var integrity = Integrity(document with { IntegritySha256 = "" });
        return JsonSerializer.Serialize(document with { IntegritySha256 = integrity }, JsonOptions);
    }

    internal static CookingLevelSettlementStoreResult Deserialize(string serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized))
            return Reject(CookingLevelSettlementStoreReason.RecordTruncated);
        if (serialized.Length > MaximumRecordCharacters)
            return Reject(CookingLevelSettlementStoreReason.RecordTooLarge);
        try
        {
            var document = JsonSerializer.Deserialize<SettlementDocument>(serialized, JsonOptions);
            if (document is null || document.Settlements is null || string.IsNullOrWhiteSpace(document.Identity))
                return Reject(CookingLevelSettlementStoreReason.RecordTruncated);
            if (document.FormatVersion != CurrentFormatVersion)
                return Reject(CookingLevelSettlementStoreReason.UnknownFormatVersion);
            if (!StringComparer.Ordinal.Equals(document.IntegritySha256, Integrity(document with { IntegritySha256 = "" })))
                return Reject(CookingLevelSettlementStoreReason.IntegrityFailure);
            if (!TryParseIdentity(document.Identity, out var scope))
                return Reject(CookingLevelSettlementStoreReason.RecordTruncated);

            var settlements = new CookingOrderSettlement[document.Settlements.Count];
            for (var index = 0; index < settlements.Length; index++)
            {
                var line = document.Settlements[index];
                if (line is null || line.Sequence <= 0 || line.LogicalTick < 0 ||
                    string.IsNullOrWhiteSpace(line.Order) || string.IsNullOrWhiteSpace(line.Template) ||
                    string.IsNullOrWhiteSpace(line.Recipe) || string.IsNullOrWhiteSpace(line.Product) ||
                    string.IsNullOrWhiteSpace(line.Player) || string.IsNullOrWhiteSpace(line.Container))
                    return Reject(CookingLevelSettlementStoreReason.RecordTruncated);
                settlements[index] = new CookingOrderSettlement(
                    line.Sequence,
                    new OrderId(line.Order),
                    new OrderTemplateId(line.Template),
                    new RecipeId(line.Recipe),
                    new ItemId(line.Product),
                    new PlayerId(line.Player),
                    new ItemId(line.Container),
                    line.LogicalTick);
            }

            return new CookingLevelSettlementStoreResult(
                true,
                CookingLevelSettlementConfirmationDisposition.Confirmed,
                CookingLevelSettlementStoreReason.None,
                new CookingLevelSettlementConfirmation(scope, settlements));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or
            NotSupportedException or ArgumentException)
        {
            return Reject(CookingLevelSettlementStoreReason.RecordTruncated);
        }
    }

    private static bool SameList(
        CookingLevelSettlementConfirmation left,
        CookingLevelSettlementConfirmation right) =>
        left.Scope.Equals(right.Scope) && left.Settlements.SequenceEqual(right.Settlements);

    private static CookingLevelSettlementStoreResult FromLedger(CookingLevelSettlementConfirmationResult result) =>
        new(
            result.Accepted,
            result.Disposition,
            result.Reason switch
            {
                CookingLevelSettlementConfirmationReason.LedgerConflict => CookingLevelSettlementStoreReason.LedgerConflict,
                CookingLevelSettlementConfirmationReason.InvalidState => CookingLevelSettlementStoreReason.InvalidState,
                _ => CookingLevelSettlementStoreReason.None,
            },
            result.Confirmation);

    private static CookingLevelSettlementStoreResult Reject(CookingLevelSettlementStoreReason reason) =>
        new(false, CookingLevelSettlementConfirmationDisposition.Rejected, reason, null);

    private static string IdentityText(CookingLevelScope scope) =>
        string.Join('\u001f', new[]
        {
            scope.MatchScope.Session.Value,
            scope.MatchScope.World.Value,
            scope.MatchScope.Match.Value,
            scope.RestaurantRuntime.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            scope.Level.Value,
            scope.LevelEpoch.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });

    private static bool TryParseIdentity(string text, out CookingLevelScope scope)
    {
        scope = null!;
        var parts = text.Split('\u001f');
        if (parts.Length != 6)
            return false;
        if (!long.TryParse(parts[3], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var runtime) ||
            !long.TryParse(parts[5], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var epoch))
            return false;
        try
        {
            scope = new CookingLevelScope(
                new CookingScope(new SessionId(parts[0]), new WorldId(parts[1]), new MatchId(parts[2])),
                new RestaurantRuntimeId(runtime),
                new LevelId(parts[4]),
                epoch);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string Integrity(SettlementDocument document)
    {
        var canonical = JsonSerializer.Serialize(new CanonicalDocument(
            document.FormatVersion,
            document.Identity,
            document.Settlements.Select(line => new CanonicalLine(
                line.Sequence, line.Order, line.Template, line.Recipe, line.Product, line.Player, line.Container, line.LogicalTick)).ToArray()),
            JsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed record SettlementDocument(
        int FormatVersion,
        string Identity,
        IReadOnlyList<SettlementLine> Settlements,
        string IntegritySha256);

    private sealed record SettlementLine(
        long Sequence,
        string Order,
        string Template,
        string Recipe,
        string Product,
        string Player,
        string Container,
        long LogicalTick);

    private sealed record CanonicalDocument(int FormatVersion, string Identity, IReadOnlyList<CanonicalLine> Settlements);

    private sealed record CanonicalLine(
        long Sequence,
        string Order,
        string Template,
        string Recipe,
        string Product,
        string Player,
        string Container,
        long LogicalTick);
}
