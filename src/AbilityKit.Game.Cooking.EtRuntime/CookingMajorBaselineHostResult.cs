using AbilityKit.Game.Cooking;

namespace AbilityKit.Game.Cooking.EtRuntime;

/// <summary>Confirmed major choices reconstructed by the application, independently of saved payloads.</summary>
public interface ICookingConfirmedMajorChoicesGameplayFactory : ICookingLevelGameplayFactory
{
    CookingMajorBaselineChoices CreateConfirmedMajorChoices(CookingLevelScope scope, CookingConfigurationSnapshot configuration);
}

public enum CookingMajorBaselineHostLoadReason
{
    None, ReadRejected, ConfigurationMismatch, ChoicesRejected, PreparationRejected, GameplayRestoreRejected, InitializationFailed
}

public sealed record CookingMajorBaselineHostLoadResult(bool Accepted, CookingMajorBaselineHostLoadReason Reason,
    CookingMajorBaselineReason BaselineReason = CookingMajorBaselineReason.None,
    CookingLevelEtHost? Host = null, CookingMajorProgress? Progress = null, string? Detail = null);
