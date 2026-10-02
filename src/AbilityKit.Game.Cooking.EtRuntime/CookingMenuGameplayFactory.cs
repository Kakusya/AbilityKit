using AbilityKit.Game.Cooking;

namespace AbilityKit.Game.Cooking.EtRuntime;

/// <summary>Trusted per-scope menu graph and policy on the existing preparation factory.</summary>
public interface ICookingMenuGameplayFactory : ICookingPreparationGameplayFactory
{
    CookingMenuCatalog CreateMenuCatalog(CookingLevelScope scope, CookingConfigurationSnapshot configuration);
    CookingLevelMenuConfiguration CreateMenuConfiguration(CookingLevelScope scope, CookingConfigurationSnapshot configuration);
}

/// <summary>Optional per-scope Front policy; legacy factories retain their fixed property.</summary>
public interface ICookingScopedFrontOfHouseGameplayFactory : ICookingFrontOfHouseGameplayFactory
{
    CookingFrontOfHouseConfiguration CreateFrontOfHouseConfiguration(CookingLevelScope scope, CookingConfigurationSnapshot configuration);
}
