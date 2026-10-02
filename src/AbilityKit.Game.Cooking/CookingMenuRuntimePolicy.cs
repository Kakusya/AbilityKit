using System.Collections.Frozen;

namespace AbilityKit.Game.Cooking;

public sealed partial class CookingRecipeSimulation
{
    private CookingLevelMenuConfiguration? _menuPolicy;
    private IReadOnlySet<RecipeId>? _menuPolicyRecipeIds;
    private IReadOnlySet<DefinitionId>? _menuPolicyMaterialDefinitions;

    /// <summary>Trusted kernel construction only. Saved state and gameplay commands cannot grant permissions.</summary>
    public void ConfigureMenuPolicy(CookingMenuCatalog catalog, CookingLevelMenuConfiguration policy)
    {
        ArgumentNullException.ThrowIfNull(catalog); ArgumentNullException.ThrowIfNull(policy);
        if (_lifecycleClosed || !IsAuthorityMutationOpen || _mutationInProgress ||
            _lifecycleGate is not null && !_lifecycleGate.IsLayoutInstallationOpen)
            throw new InvalidOperationException("The recipe simulation is not open for trusted menu configuration.");
        _mutationInProgress = true;
        try {
            var frozen = policy.Freeze();
            if (frozen.CatalogIdentity != catalog.Sha256) throw new ArgumentException("Menu policy catalog identity mismatch.", nameof(policy));
            var closure = catalog.Requirements(frozen.SelectedMenuIds).Recipes.ToFrozenSet();
            var materials = frozen.BaseAuthorizedMaterialDefinitions.Union(frozen.ConfirmedMaterialUnlocks)
                .Intersect(frozen.AllowedMaterialDefinitions).ToFrozenSet();
            if (_menuPolicy is not null) {
                if (_menuPolicy.Identity() == frozen.Identity()) return;
                throw new InvalidOperationException("The kernel menu policy is already configured.");
            }
            _menuPolicy = frozen; _menuPolicyRecipeIds = closure; _menuPolicyMaterialDefinitions = materials;
        }
        finally { _mutationInProgress = false; }
    }

    private bool MenuAllowsDefinition(DefinitionId definition) => _menuPolicy is null || _menuPolicyMaterialDefinitions!.Contains(definition);
    private bool MenuAllowsRecipe(CookingRecipeDefinition recipe) => _menuPolicy is null ||
        _menuPolicyRecipeIds!.Contains(recipe.Id) && recipe.Inputs.Append(recipe.ProductDefinition).All(MenuAllowsDefinition) &&
        (recipe.RequiredProcessingContainerDefinition is not { } carrier || MenuAllowsDefinition(carrier));
    private bool MenuAllowsObjects(params ItemId[] ids)
    {
        if (_menuPolicy is null) return true;
        var pending = new Stack<ItemId>(ids); var seen = new HashSet<ItemId>();
        while (pending.TryPop(out var id)) {
            if (!seen.Add(id)) continue;
            if (!_items.TryGetValue(id, out var item) || item.Removed || !MenuAllowsDefinition(item.Definition)) return false;
            foreach (var child in ItemsInContainer(id)) pending.Push(child);
        }
        return true;
    }

    private CookingRecipeCommandResult MutateBindingWithMenuPolicy(CookingRecipeCommand command)
    {
        // Scope/shape/reach are checked by the shared ingress before this wrapper.
        if (_menuPolicy?.BindingCommandsEnabled == false) {
            if (command.Item is not { } id || !_items.TryGetValue(id, out var item) || item.Removed || !item.IsProduct)
                return Reject(CookingRecipeRejectionReason.ProductNotFound);
            if (item.Version != command.ExpectedItemVersion) return Reject(CookingRecipeRejectionReason.ItemStale);
            return Reject(CookingRecipeRejectionReason.MenuNotAuthorized);
        }
        return MutateBinding(command);
    }
}
