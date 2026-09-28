using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AbilityKit.Game.Cooking.Session;

public sealed record CookingMajorProgressSnapshot(
    IReadOnlyList<CookingStationReplacement> Decoration,
    IReadOnlyList<DefinitionId> Unlocks,
    bool CookFaster)
{
    public static CookingMajorProgressSnapshot From(CookingMajorProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return new CookingMajorProgressSnapshot(
            progress.Decoration
                .OrderBy(item => item.From.Value, StringComparer.Ordinal)
                .ThenBy(item => item.To.Value, StringComparer.Ordinal)
                .ToArray(),
            progress.Unlocks.OrderBy(item => item.Value, StringComparer.Ordinal).ToArray(),
            progress.CookFaster);
    }
}

public sealed record CookingSessionSnapshot(
    CookingLevelScope LevelScope,
    long Generation,
    long Sequence,
    CookingMajorProgressSnapshot Progress,
    CookingRecipeSnapshot Recipe)
{
    public string CanonicalText() => JsonSerializer.Serialize(new CanonicalSessionSnapshot(
        LevelScope.MatchScope.Session.Value,
        LevelScope.MatchScope.World.Value,
        LevelScope.MatchScope.Match.Value,
        LevelScope.RestaurantRuntime.Value,
        LevelScope.Level.Value,
        LevelScope.LevelEpoch,
        Generation,
        Sequence,
        Progress.Decoration.Select(item => $"{item.From.Value}>{item.To.Value}").ToArray(),
        Progress.Unlocks.Select(item => item.Value).ToArray(),
        Progress.CookFaster,
        Recipe.CanonicalText()));

    public string Sha256() =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalText())));

    private sealed record CanonicalSessionSnapshot(
        string Session,
        string World,
        string Match,
        long RestaurantRuntime,
        string Level,
        long LevelEpoch,
        long Generation,
        long Sequence,
        IReadOnlyList<string> Decoration,
        IReadOnlyList<string> Unlocks,
        bool CookFaster,
        string Recipe);
}

public enum CookingLevelTransitionReason
{
    None,
    InvalidState,
    SourceMismatch,
    TargetInvalid,
    Duplicate,
    ContentUnavailable,
    ChoiceRejected,
    HandoffRejected,
    CheckpointWriteFailed,
    RollbackFailed,
}

public sealed record CookingLevelTransitionRequest(
    string RequestId,
    CookingLevelScope SourceLevel,
    CookingLevelScope TargetLevel,
    IReadOnlyList<CookingStationReplacement> Decoration,
    IReadOnlyList<DefinitionId> Unlocks,
    bool EnableCookFaster);

public sealed record CookingLevelTransitionResult(
    bool Accepted,
    CookingLevelTransitionReason Reason,
    CookingSessionSnapshot Snapshot);
