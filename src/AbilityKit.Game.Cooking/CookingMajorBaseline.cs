using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbilityKit.Game.Cooking;

public sealed record CookingMajorBaselineChoices(
    [property: JsonRequired] bool Locked,
    [property: JsonRequired] IReadOnlyList<CookingStationReplacement> Decoration,
    [property: JsonRequired] IReadOnlyList<DefinitionId> Unlocks,
    [property: JsonRequired] bool CookFaster)
{
    public static CookingMajorBaselineChoices Capture(CookingMajorProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return new(progress.Locked, Array.AsReadOnly(progress.Decoration.ToArray()),
            Array.AsReadOnly(progress.Unlocks.ToArray()), progress.CookFaster);
    }

    public CookingMajorProgress CreateProgress()
    {
        if (!Locked || Decoration is null || Unlocks is null)
            throw new ArgumentException("Confirmed choices are required.");
        var progress = new CookingMajorProgress();
        if (!progress.ChooseDecoration(Decoration).Accepted)
            throw new ArgumentException("Decoration is invalid.");
        foreach (var definition in Unlocks)
            if (!progress.Unlock(definition).Accepted)
                throw new ArgumentException("Unlock identity is invalid.");
        if (CookFaster) progress.EnableCookFaster();
        progress.Lock();
        return progress;
    }
}

/// <summary>A success-only restart baseline. Trusted content and host validation remain external.</summary>
public sealed record CookingMajorBaselinePayload(
    [property: JsonRequired] CookingLevelScope SourceScope,
    [property: JsonRequired] CookingLevelScope TargetScope,
    [property: JsonRequired] CookingLevelPreparation Preparation,
    [property: JsonRequired] CookingConfigurationIdentity ConfigIdentity,
    [property: JsonRequired] string? PreparationConfigurationIdentity,
    [property: JsonRequired] string? FrontConfigurationIdentity,
    [property: JsonRequired] string? MenuPolicyIdentity,
    [property: JsonRequired] CookingInstalledLayoutCheckpoint? InstalledLayout,
    [property: JsonRequired] CookingRecipeCheckpoint Kitchen,
    [property: JsonRequired] CookingMajorBaselineChoices Choices,
    [property: JsonRequired] long HostFrameSequence = 0)
{
    public string CanonicalText() => JsonSerializer.Serialize(new
    {
        SourceScope, TargetScope, ConfigIdentity, HostFrameSequence,
        preparation = new {
            Preparation.Level, Preparation.Map, Preparation.ConfigIdentity,
            Preparation.Layout.Id,
            stations = Preparation.Layout.ApplianceStations.Select(x => x.Value).Order(StringComparer.Ordinal).ToArray(),
            containers = Preparation.Layout.Containers.Select(x => x.Value).Order(StringComparer.Ordinal).ToArray()
        },
        PreparationConfigurationIdentity, FrontConfigurationIdentity, MenuPolicyIdentity,
        layout = InstalledLayout?.CanonicalText(), kitchen = Kitchen.CanonicalText(),
        choices = new {
            Choices.Locked, Choices.CookFaster,
            decoration = Choices.Decoration.Select(x => new { from = x.From.Value, to = x.To.Value }).ToArray(),
            unlocks = Choices.Unlocks.Select(x => x.Value).Order(StringComparer.Ordinal).ToArray()
        }
    });
}

public enum CookingMajorBaselineReason
{
    None, Missing, InvalidBaseline, RecordTruncated, RecordTooLarge,
    UnsupportedLegacyBaseline, UnknownFormatVersion, IntegrityFailure, ScopeMismatch, WriteFailed
}

public sealed record CookingMajorBaselineRead(bool Accepted, CookingMajorBaselineReason Reason,
    CookingMajorBaselinePayload? Payload = null);
public sealed record CookingMajorBaselineWrite(bool Accepted, CookingMajorBaselineReason Reason);

public sealed partial class CookingMajorCheckpointStore
{
    public const int CurrentBaselineFormatVersion = 3;
    public const int MaximumBaselineRecordCharacters = 1024 * 1024;
    private sealed record BaselineEnvelope(
        [property: JsonRequired] int FormatVersion,
        [property: JsonRequired] CookingMajorBaselinePayload Payload,
        [property: JsonRequired] string IntegritySha256);

    public CookingMajorBaselineWrite WriteBaseline(CookingMajorBaselinePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        string serialized;
        try
        {
            if (!ValidBaseline(payload)) return new(false, CookingMajorBaselineReason.InvalidBaseline);
            serialized = JsonSerializer.Serialize(new BaselineEnvelope(CurrentBaselineFormatVersion,
                payload, BaselineIntegrity(payload)), JsonOptions);
            var frozen = DecodeBaseline(serialized, payload.TargetScope.MatchScope);
            if (!frozen.Accepted) return new(false, frozen.Reason);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException or NotSupportedException)
        { return new(false, CookingMajorBaselineReason.InvalidBaseline); }

        var path = Path.Combine(_root, "major.checkpoint.json");
        var next = path + ".next";
        try
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(next, serialized, Encoding.UTF8);
            var checkedRead = DecodeBaseline(File.ReadAllText(next, Encoding.UTF8), payload.TargetScope.MatchScope);
            if (!checkedRead.Accepted) { TryDelete(next); return new(false, checkedRead.Reason); }
            File.Move(next, path, overwrite: true);
            return new(true, CookingMajorBaselineReason.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { TryDelete(next); return new(false, CookingMajorBaselineReason.WriteFailed); }
    }

    public CookingMajorBaselineRead ReadBaseline(CookingScope expectedMatch)
    {
        ArgumentNullException.ThrowIfNull(expectedMatch);
        var path = Path.Combine(_root, "major.checkpoint.json");
        if (!File.Exists(path)) return new(false, CookingMajorBaselineReason.Missing);
        try { return DecodeBaseline(File.ReadAllText(path, Encoding.UTF8), expectedMatch); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { return new(false, CookingMajorBaselineReason.WriteFailed); }
    }

    private static CookingMajorBaselineRead DecodeBaseline(string serialized, CookingScope expectedMatch)
    {
        if (string.IsNullOrWhiteSpace(serialized)) return new(false, CookingMajorBaselineReason.RecordTruncated);
        if (serialized.Length > MaximumBaselineRecordCharacters) return new(false, CookingMajorBaselineReason.RecordTooLarge);
        try
        {
            using var document = JsonDocument.Parse(serialized);
            if (!document.RootElement.TryGetProperty("formatVersion", out var version) || !version.TryGetInt32(out var format))
                return new(false, CookingMajorBaselineReason.RecordTruncated);
            if (format == CurrentFormatVersion || format == 2) return new(false, CookingMajorBaselineReason.UnsupportedLegacyBaseline);
            if (format != CurrentBaselineFormatVersion) return new(false, CookingMajorBaselineReason.UnknownFormatVersion);
            var envelope = JsonSerializer.Deserialize<BaselineEnvelope>(serialized, JsonOptions);
            if (envelope?.Payload is not { } payload || string.IsNullOrWhiteSpace(envelope.IntegritySha256))
                return new(false, CookingMajorBaselineReason.RecordTruncated);
            if (!ValidBaseline(payload)) return new(false, CookingMajorBaselineReason.InvalidBaseline);
            if (!StringComparer.Ordinal.Equals(envelope.IntegritySha256, BaselineIntegrity(payload)))
                return new(false, CookingMajorBaselineReason.IntegrityFailure);
            if (payload.TargetScope.MatchScope != expectedMatch) return new(false, CookingMajorBaselineReason.ScopeMismatch);
            return new(true, CookingMajorBaselineReason.None, payload);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException or NotSupportedException)
        { return new(false, CookingMajorBaselineReason.RecordTruncated); }
    }

    private static string BaselineIntegrity(CookingMajorBaselinePayload payload) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload.CanonicalText())));

    private static bool ValidBaseline(CookingMajorBaselinePayload p)
    {
        if (p.HostFrameSequence < 0 || p.SourceScope is null || p.TargetScope is null || p.Preparation is null || p.ConfigIdentity is null ||
            p.Kitchen is null || p.Choices is null || !p.Choices.Locked || p.Choices.Decoration is null ||
            p.Choices.Decoration.Any(x => x is null || string.IsNullOrWhiteSpace(x.From.Value) || string.IsNullOrWhiteSpace(x.To.Value)) ||
            p.Choices.Unlocks is null || p.Choices.Unlocks.Any(x => string.IsNullOrWhiteSpace(x.Value)) ||
            p.Choices.Unlocks.Distinct().Count() != p.Choices.Unlocks.Count ||
            p.SourceScope.MatchScope != p.TargetScope.MatchScope || p.Kitchen.Scope != p.TargetScope.MatchScope ||
            p.SourceScope.RestaurantRuntime != p.TargetScope.RestaurantRuntime || p.TargetScope.LevelEpoch <= p.SourceScope.LevelEpoch ||
            p.TargetScope.Level == p.SourceScope.Level || p.Preparation.Level != p.TargetScope.Level ||
            p.Preparation.ConfigIdentity != p.ConfigIdentity || string.IsNullOrWhiteSpace(p.ConfigIdentity.Schema) ||
            string.IsNullOrWhiteSpace(p.ConfigIdentity.Sha256) || string.IsNullOrWhiteSpace(p.Preparation.Map.Value) ||
            p.Preparation.Layout is null || string.IsNullOrWhiteSpace(p.Preparation.Layout.Id.Value) ||
            p.Preparation.Layout.ApplianceStations is null || p.Preparation.Layout.Containers is null ||
            (p.PreparationConfigurationIdentity is null) != (p.InstalledLayout is null) ||
            new[] { p.PreparationConfigurationIdentity, p.FrontConfigurationIdentity, p.MenuPolicyIdentity }.Any(x => x is not null && string.IsNullOrWhiteSpace(x)))
            return false;
        var k = p.Kitchen;
        if (k.SchemaVersion != 5 || k.StateVersion != 0 || k.LogicalTick != 0 || k.EventSequence != 0 || k.LevelScope is not null ||
            k.NextSettlementSequence != 0 || k.NextProcessId < 0 || k.NextProductId < 0 ||
            k.Items is null || k.Items.Any(x => x is null || x.Location is null || x.BoundOrder is not null) ||
            k.Processes is null || k.Processes.Any(x => x is null || x.ActiveWorker is not null || x.LockedInputs is null) ||
            k.Containers is null || k.Containers.Any(x => x is null || x.ItemIds is null) ||
            k.Orders is null || k.Orders.Count != 0 || k.Settlements is null || k.Settlements.Count != 0 ||
            k.Deduplication is null || k.Deduplication.Count != 0 || k.Events is null || k.Events.Count != 0 ||
            k.TickEvents is null || k.TickEvents.Count != 0 || k.ConsumedProducts is null ||
            k.CleanContainerCounts is null || k.CleanContainerCounts.Any(x => x is null) || k.Poses is null || k.Poses.Any(x => x is null))
            return false;
        _ = p.InstalledLayout?.CanonicalText();
        _ = k.CanonicalText();
        return true;
    }
}
