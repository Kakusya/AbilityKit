using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AbilityKit.Game.Cooking;

/// <summary>
/// 宿主级恢复信封内容：一代 Level 运行态的完整恢复描述。
/// 与 <see cref="CookingLevelLifecycleSnapshot"/>（每帧同步投影）分工不同：同步快照服务远端对齐，
/// 本记录服务“销毁宿主后按同一代际重建并继续”，因此携带 preparation、lifecycle 水位、
/// 宿主帧序号与命令水位，以及整册仿真恢复载荷。内容（configuration/fixture）不随信封走，
/// 由 config identity 校验同一性。
/// </summary>
public sealed record CookingLevelCheckpoint(
    CookingLevelScope Scope,
    CookingConfigurationIdentity ConfigIdentity,
    CookingLevelPreparation Preparation,
    CookingLevelState State,
    CookingLevelOutcome? Outcome,
    long LifecycleVersion,
    long HostFrameSequence,
    long LastCommittedSimulationBatch,
    CookingRecipeCheckpoint Recipe,
    CookingFrontOfHouseCheckpoint? FrontOfHouse = null)
{
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public string CanonicalText() => JsonSerializer.Serialize(new CanonicalLevelCheckpoint(
        Scope.MatchScope.Session.Value,
        Scope.MatchScope.World.Value,
        Scope.MatchScope.Match.Value,
        Scope.RestaurantRuntime.Value,
        Scope.Level.Value,
        Scope.LevelEpoch,
        ConfigIdentity.Schema,
        ConfigIdentity.Sha256,
        Preparation.Level.Value,
        Preparation.Map.Value,
        Preparation.Layout.Id.Value,
        Preparation.Layout.ApplianceStations.Select(station => station.Value).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        Preparation.Layout.Containers.Select(container => container.Value).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        State.ToString(),
        Outcome?.ToString(),
        LifecycleVersion,
        HostFrameSequence,
        LastCommittedSimulationBatch,
        Recipe.CanonicalText(),
        FrontOfHouse?.CanonicalText()),
        CanonicalJsonOptions);

    public string Sha256() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalText())));

    private sealed record CanonicalLevelCheckpoint(string SessionId, string WorldId, string MatchId,
        long RestaurantRuntimeId, string LevelId, long LevelEpoch, string ConfigSchema, string ConfigSha256,
        string PreparationLevelId, string PreparationMapId, string PreparationLayoutId,
        IReadOnlyList<string> PreparationStations, IReadOnlyList<string> PreparationContainers,
        string State, string? Outcome, long LifecycleVersion, long HostFrameSequence,
        long LastCommittedSimulationBatch, string RecipeCanonical, string? FrontOfHouseCanonical);
}

public enum CookingCheckpointReadReason
{
    None,
    RecordTruncated,
    RecordTooLarge,
    UnknownFormatVersion,
    IntegrityFailure,
}

public sealed record CookingCheckpointReadResult(
    bool Accepted,
    CookingCheckpointReadReason Reason,
    CookingLevelCheckpoint? Checkpoint = null);

/// <summary>
/// 宿主级恢复信封：格式版本 + 载荷 + 完整性校验，形态对照 P5 <see cref="CookingProgressCodec"/>。
/// 序列化使 checkpoint 可脱离宿主自包含存在（证明不引用活对象）；durable store 不在本契约范围。
/// </summary>
public static class CookingLevelCheckpointCodec
{
    public const int CurrentFormatVersion = 4;
    public const int MaximumRecordCharacters = 1024 * 1024;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static CookingCheckpointEnvelope CreateEnvelope(CookingLevelCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        return new CookingCheckpointEnvelope(CurrentFormatVersion, checkpoint, checkpoint.Sha256());
    }

    public static string Serialize(CookingCheckpointEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return JsonSerializer.Serialize(envelope, Options);
    }

    public static CookingCheckpointReadResult Deserialize(string serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized))
            return new CookingCheckpointReadResult(false, CookingCheckpointReadReason.RecordTruncated, null);
        if (serialized.Length > MaximumRecordCharacters)
            return new CookingCheckpointReadResult(false, CookingCheckpointReadReason.RecordTooLarge, null);
        try
        {
            var envelope = JsonSerializer.Deserialize<CookingCheckpointEnvelope>(serialized, Options);
            if (envelope?.Checkpoint is null || string.IsNullOrWhiteSpace(envelope.IntegritySha256))
                return new CookingCheckpointReadResult(false, CookingCheckpointReadReason.RecordTruncated, null);
            if (envelope.FormatVersion != CurrentFormatVersion)
                return new CookingCheckpointReadResult(false, CookingCheckpointReadReason.UnknownFormatVersion, null);
            if (!StringComparer.Ordinal.Equals(envelope.IntegritySha256, envelope.Checkpoint.Sha256()))
                return new CookingCheckpointReadResult(false, CookingCheckpointReadReason.IntegrityFailure, null);
            return new CookingCheckpointReadResult(true, CookingCheckpointReadReason.None, envelope.Checkpoint);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or
            NotSupportedException or ArgumentException)
        {
            return new CookingCheckpointReadResult(false, CookingCheckpointReadReason.RecordTruncated, null);
        }
    }
}

public sealed record CookingCheckpointEnvelope(
    int FormatVersion,
    CookingLevelCheckpoint Checkpoint,
    string IntegritySha256);
