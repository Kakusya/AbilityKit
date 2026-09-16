using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Frozen;

namespace AbilityKit.Game.Cooking;

public readonly record struct CookingProgressOwnerId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct SettlementId(string Value)
{
    public override string ToString() => Value;
}

public sealed record CookingProgressOwnerScope(CookingProgressOwnerId Owner, PlayerId Player);

public sealed record CookingProgressReward(
    IReadOnlyList<string> Unlocks,
    IReadOnlyDictionary<string, int> Upgrades,
    long CurrencyDelta,
    long BusinessProgressDelta);

public sealed record CookingConfirmedSettlement(
    CookingProgressOwnerScope OwnerScope,
    CookingScope MatchScope,
    SettlementId Id,
    bool IsConfirmed,
    long TargetProgressVersion,
    CookingConfigurationIdentity ConfigIdentity,
    CookingProgressReward Reward);

public sealed record CookingAppliedSettlement(SettlementId Id, string Fingerprint, long AppliedRevision);

public sealed record CookingLongTermProgress(
    CookingProgressOwnerScope OwnerScope,
    string ConfigIdentity,
    long ProgressVersion,
    long Revision,
    IReadOnlyList<string> Unlocks,
    IReadOnlyDictionary<string, int> Upgrades,
    long Currency,
    long BusinessProgress,
    IReadOnlyDictionary<SettlementId, CookingAppliedSettlement> AppliedSettlements);

public enum CookingPersistenceReason
{
    None,
    SettlementNotConfirmed,
    OwnerScopeInvalid,
    SettlementIdentityInvalid,
    MatchScopeInvalid,
    ProgressVersionMismatch,
    ConfigIdentityMismatch,
    SettlementIdentityConflict,
    StorePrepareFailed,
    StoreCommitFailed,
    StoreReadFailed,
    ResponseLostAfterCommit,
    UnknownFormatVersion,
    IntegrityFailure,
    RecordTruncated,
    OwnerMismatch,
    RevisionMismatch,
    RecordTooLarge,
    BlockedByOwnerDecision,
}

public enum CookingSettlementDisposition
{
    Applied,
    Duplicate,
    Rejected,
    Failed,
}

public sealed record CookingSettlementResult(
    CookingSettlementDisposition Disposition,
    CookingPersistenceReason Reason,
    CookingLongTermProgress? Progress,
    bool IsDuplicate);

public sealed record CookingProgressEnvelope(
    int FormatVersion,
    CookingLongTermProgress Progress,
    string IntegritySha256);

public sealed record CookingPersistenceReadResult(
    bool Accepted,
    CookingPersistenceReason Reason,
    CookingProgressEnvelope? Envelope);

public enum CookingPersistenceFault
{
    None,
    PrepareFailure,
    CommitFailure,
    ResponseLostAfterCommit,
    ReadFailure,
    TruncatedRead,
    TamperedRead,
}

public sealed record CookingPersistenceCommitResult(
    CookingPersistenceReason Reason,
    bool ResponseLostAfterCommit = false);

public interface ICookingProgressStore
{
    CookingPersistenceReason Prepare(CookingProgressEnvelope envelope);
    CookingPersistenceCommitResult Commit();
    CookingPersistenceReadResult Read();
}

public sealed class InMemoryCookingProgressStore : ICookingProgressStore
{
    private string? _prepared;
    private string? _committed;

    public CookingPersistenceFault NextFault { get; set; }
    public int CommitCount { get; private set; }

    public CookingPersistenceReason Prepare(CookingProgressEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (ConsumeFault(CookingPersistenceFault.PrepareFailure))
            return CookingPersistenceReason.StorePrepareFailed;
        _prepared = CookingProgressCodec.Serialize(envelope);
        return CookingPersistenceReason.None;
    }

    public CookingPersistenceCommitResult Commit()
    {
        if (ConsumeFault(CookingPersistenceFault.CommitFailure))
        {
            _prepared = null;
            return new CookingPersistenceCommitResult(CookingPersistenceReason.StoreCommitFailed);
        }
        if (_prepared is null)
            return new CookingPersistenceCommitResult(CookingPersistenceReason.StoreCommitFailed);

        _committed = _prepared;
        _prepared = null;
        CommitCount++;
        return new CookingPersistenceCommitResult(CookingPersistenceReason.None,
            ConsumeFault(CookingPersistenceFault.ResponseLostAfterCommit));
    }

    public CookingPersistenceReadResult Read()
    {
        if (ConsumeFault(CookingPersistenceFault.ReadFailure))
            return new CookingPersistenceReadResult(false, CookingPersistenceReason.StoreReadFailed, null);
        if (_committed is null)
            return new CookingPersistenceReadResult(false, CookingPersistenceReason.StoreReadFailed, null);
        if (ConsumeFault(CookingPersistenceFault.TruncatedRead))
            return CookingProgressCodec.Deserialize(_committed[..Math.Max(0, _committed.Length / 2)]);
        if (ConsumeFault(CookingPersistenceFault.TamperedRead))
            return CookingProgressCodec.Deserialize(_committed.Replace("currency", "currencY", StringComparison.Ordinal));
        return CookingProgressCodec.Deserialize(_committed);
    }

    private bool ConsumeFault(CookingPersistenceFault fault)
    {
        if (NextFault != fault)
            return false;
        NextFault = CookingPersistenceFault.None;
        return true;
    }
}

public sealed class SettlementIdJsonConverter : JsonConverter<SettlementId>
{
    public override SettlementId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString() ?? throw new JsonException("Settlement ID must be a string."));

    public override void Write(Utf8JsonWriter writer, SettlementId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);

    public override SettlementId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString() ?? throw new JsonException("Settlement ID property must be a string."));

    public override void WriteAsPropertyName(Utf8JsonWriter writer, SettlementId value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value.Value);
}

public static class CookingProgressCodec
{
    public const int CurrentFormatVersion = 1;
    public const int MaximumRecordCharacters = 128 * 1024;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new SettlementIdJsonConverter() },
    };

    public static CookingProgressEnvelope CreateEnvelope(CookingLongTermProgress progress) =>
        new(CurrentFormatVersion, progress, ComputeIntegrity(progress));

    public static string Serialize(CookingProgressEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return JsonSerializer.Serialize(envelope, Options);
    }

    public static CookingPersistenceReadResult Deserialize(string serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized))
            return new CookingPersistenceReadResult(false, CookingPersistenceReason.RecordTruncated, null);
        if (serialized.Length > MaximumRecordCharacters)
            return new CookingPersistenceReadResult(false, CookingPersistenceReason.RecordTooLarge, null);
        try
        {
            var envelope = JsonSerializer.Deserialize<CookingProgressEnvelope>(serialized, Options);
            if (envelope is null || envelope.Progress is null || string.IsNullOrWhiteSpace(envelope.IntegritySha256) ||
                !IsValidProgressShape(envelope.Progress))
                return new CookingPersistenceReadResult(false, CookingPersistenceReason.RecordTruncated, null);
            if (envelope.FormatVersion != CurrentFormatVersion)
                return new CookingPersistenceReadResult(false, CookingPersistenceReason.UnknownFormatVersion, null);
            if (!StringComparer.Ordinal.Equals(envelope.IntegritySha256, ComputeIntegrity(envelope.Progress)))
                return new CookingPersistenceReadResult(false, CookingPersistenceReason.IntegrityFailure, null);
            return new CookingPersistenceReadResult(true, CookingPersistenceReason.None, envelope);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentNullException)
        {
            return new CookingPersistenceReadResult(false, CookingPersistenceReason.RecordTruncated, null);
        }
    }

    private static bool IsValidProgressShape(CookingLongTermProgress progress) =>
        progress.OwnerScope is not null && !string.IsNullOrWhiteSpace(progress.OwnerScope.Owner.Value) &&
        !string.IsNullOrWhiteSpace(progress.OwnerScope.Player.Value) && !string.IsNullOrWhiteSpace(progress.ConfigIdentity) &&
        progress.ProgressVersion > 0 && progress.Revision >= 0 && progress.Unlocks is not null && progress.Upgrades is not null &&
        progress.AppliedSettlements is not null && progress.Unlocks.All(unlock => !string.IsNullOrWhiteSpace(unlock)) &&
        progress.Upgrades.All(pair => !string.IsNullOrWhiteSpace(pair.Key)) &&
        progress.AppliedSettlements.All(pair => !string.IsNullOrWhiteSpace(pair.Key.Value) && pair.Value is not null &&
            pair.Key == pair.Value.Id && !string.IsNullOrWhiteSpace(pair.Value.Fingerprint) &&
            pair.Value.AppliedRevision > 0 && pair.Value.AppliedRevision <= progress.Revision);

    private static string ComputeIntegrity(CookingLongTermProgress progress)
    {
        var canonical = new CanonicalProgress(
            progress.OwnerScope.Owner.Value,
            progress.OwnerScope.Player.Value,
            progress.ConfigIdentity,
            progress.ProgressVersion,
            progress.Revision,
            progress.Unlocks.OrderBy(unlock => unlock, StringComparer.Ordinal).ToArray(),
            progress.Upgrades.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new CanonicalUpgrade(pair.Key, pair.Value)).ToArray(),
            progress.Currency,
            progress.BusinessProgress,
            progress.AppliedSettlements.OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
                .Select(pair => new CanonicalSettlement(pair.Key.Value, pair.Value.Fingerprint, pair.Value.AppliedRevision)).ToArray());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical, Options))));
    }

    private sealed record CanonicalProgress(string Owner, string Player, string ConfigIdentity, long ProgressVersion, long Revision,
        IReadOnlyList<string> Unlocks, IReadOnlyList<CanonicalUpgrade> Upgrades, long Currency, long BusinessProgress,
        IReadOnlyList<CanonicalSettlement> AppliedSettlements);
    private sealed record CanonicalUpgrade(string Id, int Level);
    private sealed record CanonicalSettlement(string Id, string Fingerprint, long AppliedRevision);
}

public sealed class CookingProgressPersistence
{
    private readonly CookingProgressOwnerScope _ownerScope;
    private readonly CookingConfigurationIdentity _configIdentity;
    private readonly ICookingProgressStore _store;

    public CookingProgressPersistence(
        CookingProgressOwnerScope ownerScope,
        CookingConfigurationIdentity configIdentity,
        CookingLongTermProgress initialProgress,
        ICookingProgressStore store)
    {
        ArgumentNullException.ThrowIfNull(ownerScope);
        ArgumentNullException.ThrowIfNull(configIdentity);
        ArgumentNullException.ThrowIfNull(initialProgress);
        ArgumentNullException.ThrowIfNull(store);
        if (!Equals(ownerScope, initialProgress.OwnerScope))
            throw new ArgumentException("Initial progress owner scope must match the persistence owner.", nameof(initialProgress));
        if (!StringComparer.Ordinal.Equals(configIdentity.ToString(), initialProgress.ConfigIdentity))
            throw new ArgumentException("Initial progress config identity must match persistence configuration.", nameof(initialProgress));

        _ownerScope = ownerScope;
        _configIdentity = configIdentity;
        _store = store;
        _current = CopyProgress(initialProgress);
    }

    private CookingLongTermProgress _current;

    public CookingLongTermProgress Current => CopyProgress(_current);

    public CookingSettlementResult Apply(CookingConfirmedSettlement settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        var validation = ValidateSettlement(settlement);
        if (validation != CookingPersistenceReason.None)
            return Reject(validation);

        var fingerprint = SettlementFingerprint(settlement);
        if (_current.AppliedSettlements.TryGetValue(settlement.Id, out var applied))
        {
            if (!StringComparer.Ordinal.Equals(applied.Fingerprint, fingerprint))
                return Reject(CookingPersistenceReason.SettlementIdentityConflict);
            return new CookingSettlementResult(CookingSettlementDisposition.Duplicate, CookingPersistenceReason.None, SnapshotCurrent(), true);
        }

        var next = ApplyMutation(_current, settlement, fingerprint);
        var prepare = _store.Prepare(CookingProgressCodec.CreateEnvelope(next));
        if (prepare != CookingPersistenceReason.None)
            return Reject(prepare, CookingSettlementDisposition.Failed);
        var commit = _store.Commit();
        if (commit.Reason != CookingPersistenceReason.None)
            return Reject(commit.Reason, CookingSettlementDisposition.Failed);

        _current = CopyProgress(next);
        if (commit.ResponseLostAfterCommit)
            return new CookingSettlementResult(CookingSettlementDisposition.Applied, CookingPersistenceReason.ResponseLostAfterCommit,
                SnapshotCurrent(), false);
        return new CookingSettlementResult(CookingSettlementDisposition.Applied, CookingPersistenceReason.None, SnapshotCurrent(), false);
    }

    public CookingPersistenceReadResult RestartReadBack()
    {
        var read = _store.Read();
        if (!read.Accepted || read.Envelope is null)
            return read;
        var progress = read.Envelope.Progress;
        if (!Equals(progress.OwnerScope, _ownerScope))
            return new CookingPersistenceReadResult(false, CookingPersistenceReason.OwnerMismatch, null);
        if (!StringComparer.Ordinal.Equals(progress.ConfigIdentity, _configIdentity.ToString()))
            return new CookingPersistenceReadResult(false, CookingPersistenceReason.ConfigIdentityMismatch, null);
        if (progress.ProgressVersion != _current.ProgressVersion || progress.Revision < _current.Revision ||
            !IsRevisionConsistent(progress))
            return new CookingPersistenceReadResult(false, CookingPersistenceReason.RevisionMismatch, null);

        _current = CopyProgress(progress);
        return new CookingPersistenceReadResult(true, CookingPersistenceReason.None, CookingProgressCodec.CreateEnvelope(_current));
    }

    public CookingSettlementResult RequireOwnerDecision(CookingOwnerDecision decision) =>
        Reject(CookingPersistenceReason.BlockedByOwnerDecision);

    private CookingPersistenceReason ValidateSettlement(CookingConfirmedSettlement settlement)
    {
        if (!settlement.IsConfirmed)
            return CookingPersistenceReason.SettlementNotConfirmed;
        if (string.IsNullOrWhiteSpace(settlement.OwnerScope.Owner.Value) || string.IsNullOrWhiteSpace(settlement.OwnerScope.Player.Value) ||
            !Equals(settlement.OwnerScope, _ownerScope))
            return CookingPersistenceReason.OwnerScopeInvalid;
        if (string.IsNullOrWhiteSpace(settlement.Id.Value))
            return CookingPersistenceReason.SettlementIdentityInvalid;
        if (string.IsNullOrWhiteSpace(settlement.MatchScope.Session.Value) || string.IsNullOrWhiteSpace(settlement.MatchScope.World.Value) ||
            string.IsNullOrWhiteSpace(settlement.MatchScope.Match.Value))
            return CookingPersistenceReason.MatchScopeInvalid;
        if (settlement.TargetProgressVersion != _current.ProgressVersion)
            return CookingPersistenceReason.ProgressVersionMismatch;
        if (!Equals(settlement.ConfigIdentity, _configIdentity))
            return CookingPersistenceReason.ConfigIdentityMismatch;
        return CookingPersistenceReason.None;
    }

    private static CookingLongTermProgress ApplyMutation(CookingLongTermProgress current, CookingConfirmedSettlement settlement,
        string fingerprint)
    {
        var upgrades = current.Upgrades.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var upgrade in settlement.Reward.Upgrades)
            upgrades[upgrade.Key] = upgrades.GetValueOrDefault(upgrade.Key) + upgrade.Value;
        var unlocks = current.Unlocks.Concat(settlement.Reward.Unlocks)
            .Distinct(StringComparer.Ordinal).OrderBy(unlock => unlock, StringComparer.Ordinal).ToArray();
        var revision = current.Revision + 1;
        var ledger = current.AppliedSettlements.ToDictionary(pair => pair.Key, pair => pair.Value);
        ledger.Add(settlement.Id, new CookingAppliedSettlement(settlement.Id, fingerprint, revision));
        return new CookingLongTermProgress(current.OwnerScope, current.ConfigIdentity, current.ProgressVersion, revision, unlocks,
            upgrades, current.Currency + settlement.Reward.CurrencyDelta,
            current.BusinessProgress + settlement.Reward.BusinessProgressDelta, ledger);
    }

    private CookingSettlementResult Reject(CookingPersistenceReason reason,
        CookingSettlementDisposition disposition = CookingSettlementDisposition.Rejected) =>
        new(disposition, reason, SnapshotCurrent(), false);

    private CookingLongTermProgress SnapshotCurrent() => CopyProgress(_current);

    private static bool IsRevisionConsistent(CookingLongTermProgress progress) =>
        progress.Revision == 0
            ? progress.AppliedSettlements.Count == 0
            : progress.AppliedSettlements.Values.All(entry => entry.AppliedRevision > 0 && entry.AppliedRevision <= progress.Revision);

    private static CookingLongTermProgress CopyProgress(CookingLongTermProgress progress) =>
        new(progress.OwnerScope, progress.ConfigIdentity, progress.ProgressVersion, progress.Revision,
            progress.Unlocks.OrderBy(unlock => unlock, StringComparer.Ordinal).ToArray(),
            progress.Upgrades.ToFrozenDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            progress.Currency, progress.BusinessProgress,
            progress.AppliedSettlements.ToFrozenDictionary(pair => pair.Key, pair => pair.Value));

    private static string SettlementFingerprint(CookingConfirmedSettlement settlement)
    {
        var canonical = new CanonicalSettlement(
            settlement.OwnerScope.Owner.Value,
            settlement.OwnerScope.Player.Value,
            settlement.MatchScope.Session.Value,
            settlement.MatchScope.World.Value,
            settlement.MatchScope.Match.Value,
            settlement.Id.Value,
            settlement.IsConfirmed,
            settlement.TargetProgressVersion,
            settlement.ConfigIdentity.ToString(),
            settlement.Reward.Unlocks.OrderBy(unlock => unlock, StringComparer.Ordinal).ToArray(),
            settlement.Reward.Upgrades.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new CanonicalUpgrade(pair.Key, pair.Value)).ToArray(),
            settlement.Reward.CurrencyDelta,
            settlement.Reward.BusinessProgressDelta);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical))));
    }

    private sealed record CanonicalSettlement(string Owner, string Player, string Session, string World, string Match,
        string Settlement, bool Confirmed, long ProgressVersion, string ConfigIdentity, IReadOnlyList<string> Unlocks,
        IReadOnlyList<CanonicalUpgrade> Upgrades, long CurrencyDelta, long BusinessProgressDelta);
    private sealed record CanonicalUpgrade(string Id, int Value);
}

public sealed record CookingPersistenceAcceptanceEvidence(
    string TestId,
    string Operation,
    string OwnerScope,
    string? SettlementId,
    string BeforeProgressHash,
    string AfterProgressHash,
    long BeforeRevision,
    long AfterRevision,
    string Disposition,
    string Reason,
    string AssertionSummary,
    string Runner,
    string TimestampUtc);

public static class CookingPersistenceAcceptanceEvidenceWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static void Append(string path, CookingPersistenceAcceptanceEvidence evidence)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new ArgumentException("Evidence path has no directory.", nameof(path)));
        File.AppendAllText(path, JsonSerializer.Serialize(evidence, Options) + Environment.NewLine, Encoding.UTF8);
    }

    public static IReadOnlyList<CookingPersistenceAcceptanceEvidence> ReadAll(string path) =>
        File.ReadLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<CookingPersistenceAcceptanceEvidence>(line, Options)
                ?? throw new InvalidDataException("Invalid cooking persistence evidence line."))
            .ToArray();
}
