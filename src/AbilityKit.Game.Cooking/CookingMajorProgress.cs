using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AbilityKit.Game.Cooking;

public enum CookingMajorProgressReason
{
    None,
    InvalidState,
    UnknownChoice,
    StationConflict,
    Duplicate,
    Missing,
    WriteFailed,
    RecordTruncated,
    RecordTooLarge,
    UnknownFormatVersion,
    IntegrityFailure,
}

public sealed record CookingMajorProgressResult(
    bool Accepted,
    CookingMajorProgressReason Reason);

/// <summary>
/// 一个大关里还没锁定的选择：装修替换哪些工位、解锁了哪些定义、煮制是否加速。
/// 成功收口之后锁定。失败不清除当前进程里的选择，也不写检查点。
/// </summary>
public sealed class CookingMajorProgress
{
    private readonly List<CookingStationReplacement> _decoration = new();
    private readonly List<DefinitionId> _unlocks = new();
    private bool _cookFaster;
    private bool _locked;

    public bool Locked => _locked;
    public bool CookFaster => _cookFaster;
    public IReadOnlyList<CookingStationReplacement> Decoration => _decoration.ToArray();
    public IReadOnlyList<DefinitionId> Unlocks => _unlocks.ToArray();

    public CookingMajorProgressResult ChooseDecoration(IReadOnlyList<CookingStationReplacement> replacements)
    {
        var validation = ValidateDecoration(replacements);
        if (!validation.Accepted)
            return validation;
        _decoration.Clear();
        _decoration.AddRange(replacements);
        return Accept();
    }

    /// <summary>Checks a decoration choice without changing the persistent preferences.</summary>
    public CookingMajorProgressResult ValidateDecoration(IReadOnlyList<CookingStationReplacement> replacements)
    {
        ArgumentNullException.ThrowIfNull(replacements);
        if (_locked)
            return Reject(CookingMajorProgressReason.InvalidState);
        if (replacements.Any(item => item is null))
            return Reject(CookingMajorProgressReason.UnknownChoice);
        return Accept();
    }

    public CookingMajorProgressResult Unlock(DefinitionId definition)
    {
        var validation = ValidateUnlock(definition);
        if (!validation.Accepted || validation.Reason == CookingMajorProgressReason.Duplicate)
            return validation;
        _unlocks.Add(definition);
        return Accept();
    }

    /// <summary>Checks an unlock before the Level owner attempts physical placement.</summary>
    public CookingMajorProgressResult ValidateUnlock(DefinitionId definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Value))
            return Reject(CookingMajorProgressReason.UnknownChoice);
        if (_locked)
            return Reject(CookingMajorProgressReason.InvalidState);
        if (_unlocks.Contains(definition))
            return new CookingMajorProgressResult(true, CookingMajorProgressReason.Duplicate);
        return Accept();
    }

    public CookingMajorProgressResult EnableCookFaster()
    {
        if (_locked)
            return Reject(CookingMajorProgressReason.InvalidState);
        if (_cookFaster)
            return new CookingMajorProgressResult(true, CookingMajorProgressReason.Duplicate);
        _cookFaster = true;
        return Accept();
    }

    public void Lock() => _locked = true;

    public int CookTicks(RecipeId recipe, int requiredTicks)
    {
        if (requiredTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(requiredTicks));
        if (!_cookFaster || !string.Equals(recipe.Value, "tomato-egg-soup", StringComparison.Ordinal))
            return requiredTicks;
        return Math.Max(1, requiredTicks / 2);
    }

    private static CookingMajorProgressResult Accept() => new(true, CookingMajorProgressReason.None);

    private static CookingMajorProgressResult Reject(CookingMajorProgressReason reason) => new(false, reason);
}

public sealed record CookingStationReplacement(StationSlotId From, StationSlotId To);

/// <summary>
/// 小关成功之后的大关检查点：锁定的选择加上交接后的厨房现场。
/// 不含订单、结算条或命令水位。一个大关一份文件。
/// </summary>
public sealed class CookingMajorCheckpointStore
{
    public const int CurrentFormatVersion = 1;
    public const int MaximumRecordCharacters = 512 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly string _root;

    public CookingMajorCheckpointStore(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("The major checkpoint directory is required.", nameof(root));
        _root = Path.GetFullPath(root);
    }

    public CookingMajorProgressResult Write(
        CookingScope match,
        CookingMajorProgress progress,
        CookingRecipeCheckpoint kitchen)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(kitchen);
        if (!progress.Locked)
            return new CookingMajorProgressResult(false, CookingMajorProgressReason.InvalidState);
        try
        {
            Directory.CreateDirectory(_root);
            var path = Path.Combine(_root, "major.checkpoint.json");
            var next = path + ".next";
            File.WriteAllText(next, Serialize(match, progress, kitchen), Encoding.UTF8);
            var check = Deserialize(File.ReadAllText(next, Encoding.UTF8), match);
            if (!check.Accepted)
            {
                TryDelete(next);
                return new CookingMajorProgressResult(false, check.Reason);
            }

            File.Move(next, path, overwrite: true);
            return new CookingMajorProgressResult(true, CookingMajorProgressReason.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new CookingMajorProgressResult(false, CookingMajorProgressReason.WriteFailed);
        }
    }

    public CookingMajorCheckpointRead Read(CookingScope match)
    {
        ArgumentNullException.ThrowIfNull(match);
        var path = Path.Combine(_root, "major.checkpoint.json");
        if (!File.Exists(path))
            return CookingMajorCheckpointRead.Reject(CookingMajorProgressReason.Missing);
        try
        {
            return Deserialize(File.ReadAllText(path, Encoding.UTF8), match);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return CookingMajorCheckpointRead.Reject(CookingMajorProgressReason.WriteFailed);
        }
    }

    internal static string Serialize(CookingScope match, CookingMajorProgress progress, CookingRecipeCheckpoint kitchen)
    {
        var document = new MajorDocument(
            CurrentFormatVersion,
            match.Session.Value,
            match.World.Value,
            match.Match.Value,
            progress.Decoration.Select(item => new StationLine(item.From.Value, item.To.Value)).ToArray(),
            progress.Unlocks.Select(item => item.Value).ToArray(),
            progress.CookFaster,
            kitchen.CanonicalText(),
            "");
        var integrity = Integrity(document with { IntegritySha256 = "" });
        return JsonSerializer.Serialize(document with { IntegritySha256 = integrity }, JsonOptions);
    }

    internal static CookingMajorCheckpointRead Deserialize(string serialized, CookingScope expectedMatch)
    {
        if (string.IsNullOrWhiteSpace(serialized))
            return CookingMajorCheckpointRead.Reject(CookingMajorProgressReason.RecordTruncated);
        if (serialized.Length > MaximumRecordCharacters)
            return CookingMajorCheckpointRead.Reject(CookingMajorProgressReason.RecordTooLarge);
        try
        {
            var document = JsonSerializer.Deserialize<MajorDocument>(serialized, JsonOptions);
            if (document is null || document.Stations is null || document.Unlocks is null ||
                string.IsNullOrWhiteSpace(document.KitchenCanonical))
                return CookingMajorCheckpointRead.Reject(CookingMajorProgressReason.RecordTruncated);
            if (document.FormatVersion != CurrentFormatVersion)
                return CookingMajorCheckpointRead.Reject(CookingMajorProgressReason.UnknownFormatVersion);
            if (!StringComparer.Ordinal.Equals(document.IntegritySha256, Integrity(document with { IntegritySha256 = "" })))
                return CookingMajorCheckpointRead.Reject(CookingMajorProgressReason.IntegrityFailure);
            if (!string.Equals(document.Session, expectedMatch.Session.Value, StringComparison.Ordinal) ||
                !string.Equals(document.World, expectedMatch.World.Value, StringComparison.Ordinal) ||
                !string.Equals(document.Match, expectedMatch.Match.Value, StringComparison.Ordinal))
                return CookingMajorCheckpointRead.Reject(CookingMajorProgressReason.InvalidState);

            var progress = new CookingMajorProgress();
            if (document.Stations.Count > 0)
            {
                var chosen = progress.ChooseDecoration(document.Stations
                    .Select(line => new CookingStationReplacement(new StationSlotId(line.From), new StationSlotId(line.To)))
                    .ToArray());
                if (!chosen.Accepted)
                    return CookingMajorCheckpointRead.Reject(chosen.Reason);
            }

            foreach (var unlock in document.Unlocks)
            {
                var added = progress.Unlock(new DefinitionId(unlock));
                if (!added.Accepted && added.Reason != CookingMajorProgressReason.Duplicate)
                    return CookingMajorCheckpointRead.Reject(added.Reason);
            }

            if (document.CookFaster)
                progress.EnableCookFaster();
            progress.Lock();
            return new CookingMajorCheckpointRead(true, CookingMajorProgressReason.None, progress, document.KitchenCanonical);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            return CookingMajorCheckpointRead.Reject(CookingMajorProgressReason.RecordTruncated);
        }
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

    private static string Integrity(MajorDocument document)
    {
        var canonical = JsonSerializer.Serialize(new CanonicalMajor(
            document.FormatVersion,
            document.Session,
            document.World,
            document.Match,
            document.Stations.Select(line => line.From + ">" + line.To).ToArray(),
            document.Unlocks.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            document.CookFaster,
            document.KitchenCanonical), JsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private sealed record MajorDocument(
        int FormatVersion,
        string Session,
        string World,
        string Match,
        IReadOnlyList<StationLine> Stations,
        IReadOnlyList<string> Unlocks,
        bool CookFaster,
        string KitchenCanonical,
        string IntegritySha256);

    private sealed record StationLine(string From, string To);

    private sealed record CanonicalMajor(
        int FormatVersion,
        string Session,
        string World,
        string Match,
        IReadOnlyList<string> Stations,
        IReadOnlyList<string> Unlocks,
        bool CookFaster,
        string KitchenCanonical);
}

public sealed record CookingMajorCheckpointRead(
    bool Accepted,
    CookingMajorProgressReason Reason,
    CookingMajorProgress? Progress,
    string? KitchenCanonical)
{
    public static CookingMajorCheckpointRead Reject(CookingMajorProgressReason reason) =>
        new(false, reason, null, null);
}
