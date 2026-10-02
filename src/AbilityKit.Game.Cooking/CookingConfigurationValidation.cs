using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AbilityKit.Game.Cooking;

public static class CookingConfigurationDiagnosticCodes
{
    public const string DuplicateId = "DuplicateId";
    public const string RequiredFieldMissing = "RequiredFieldMissing";
    public const string InvalidValue = "InvalidValue";
    public const string MissingReference = "MissingReference";
    public const string UnknownCapability = "UnknownCapability";
    public const string CapabilityUnavailable = "CapabilityUnavailable";
}

public sealed record CookingConfigurationDiagnostic(
    string Code,
    string Table,
    string RecordId,
    string Field,
    string? Relation,
    string Message);

public sealed record CookingConfigurationValidationResult(IReadOnlyList<CookingConfigurationDiagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0;
}

/// <summary>
/// 订单模板：前厅按模板生成订单实例；要求由 recipe 身份与容器物品定义声明，
/// 不带耐心/时限/奖励字段（分别属前厅节奏与评分，均范围外）。
/// </summary>
public sealed record CookingOrderTemplateDefinition(
    OrderTemplateId Id,
    RecipeId RequiredRecipe,
    DefinitionId RequiredContainerDefinition,
    int BaseScore = 100,
    bool RequiresBinding = false);

public sealed record CookingScoreThresholds(int OneStar, int TwoStar, int ThreeStar)
{
    public static readonly CookingScoreThresholds Default = new(100, 200, 300);

    public int EvaluateStars(int totalScore)
    {
        if (totalScore >= ThreeStar) return 3;
        if (totalScore >= TwoStar) return 2;
        if (totalScore >= OneStar) return 1;
        return 0;
    }
}

/// <summary>
/// 标准初始供应项：位置语法为 <c>world:&lt;position&gt;</c>、<c>station:&lt;stationId&gt;</c> 或 <c>cleanPool</c>。
/// <c>cleanPool</c> 项同时声明该定义是可清洗容器并给出干净池上限。
/// </summary>
public sealed record CookingSupplyEntryDefinition(DefinitionId Definition, int Count, string Location);

public sealed record CookingConfigurationCandidate(
    IReadOnlyList<string> SupportedApplianceCapabilities,
    IReadOnlyList<CookingItemDefinition> Items,
    IReadOnlyList<CookingApplianceDefinition> Appliances,
    IReadOnlyList<CookingRecipeDefinition> Recipes,
    IReadOnlyList<CookingOrderTemplateDefinition>? OrderTemplates = null,
    IReadOnlyList<CookingSupplyEntryDefinition>? StandardInitialSupply = null,
    CookingSpatialConfiguration? Spatial = null)
{
    public CookingContentProvenance? ContentProvenance { get; init; }
    public CookingSupplyConfiguration? Supply { get; init; }
}

public sealed record CookingConfigurationIdentity(string Schema, string Sha256)
{
    public const string CurrentSchema = "cooking-definition-v3";

    public override string ToString() => $"{Schema}:{Sha256}";
}

public sealed class CookingConfigurationSnapshot
{
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    internal CookingConfigurationSnapshot(
        IReadOnlySet<string> supportedApplianceCapabilities,
        IReadOnlyDictionary<DefinitionId, CookingItemDefinition> items,
        IReadOnlyDictionary<StationSlotId, CookingApplianceDefinition> appliances,
        IReadOnlyDictionary<RecipeId, CookingRecipeDefinition> recipes,
        IReadOnlyDictionary<OrderTemplateId, CookingOrderTemplateDefinition>? orderTemplates = null,
        IReadOnlyList<CookingSupplyEntryDefinition>? standardInitialSupply = null,
        CookingSpatialConfiguration? spatial = null, CookingContentProvenance? contentProvenance = null, CookingSupplyConfiguration? supply = null)
    {
        ContentProvenance = contentProvenance?.Freeze();
        Spatial = spatial?.Freeze();
        Supply = CookingSupplyIntegration.ValidateAndFreeze(supply, items, Spatial);
        SupportedApplianceCapabilities = supportedApplianceCapabilities;
        Items = items;
        Appliances = appliances;
        Recipes = recipes;
        OrderTemplates = orderTemplates ?? new Dictionary<OrderTemplateId, CookingOrderTemplateDefinition>();
        StandardInitialSupply = standardInitialSupply ?? Array.Empty<CookingSupplyEntryDefinition>();
        Identity = new CookingConfigurationIdentity(CookingConfigurationIdentity.CurrentSchema, Sha256(CanonicalText()));
    }

    public IReadOnlySet<string> SupportedApplianceCapabilities { get; }
    public IReadOnlyDictionary<DefinitionId, CookingItemDefinition> Items { get; }
    public IReadOnlyDictionary<StationSlotId, CookingApplianceDefinition> Appliances { get; }
    public IReadOnlyDictionary<RecipeId, CookingRecipeDefinition> Recipes { get; }
    public IReadOnlyDictionary<OrderTemplateId, CookingOrderTemplateDefinition> OrderTemplates { get; }
    public IReadOnlyList<CookingSupplyEntryDefinition> StandardInitialSupply { get; }
    public CookingSupplyConfiguration? Supply { get; }
    public CookingSpatialConfiguration? Spatial { get; }
    public CookingConfigurationIdentity Identity { get; }
    public CookingContentProvenance? ContentProvenance { get; }

    public string CanonicalText() => JsonSerializer.Serialize(new CanonicalConfiguration(
        CookingConfigurationIdentity.CurrentSchema,
        SupportedApplianceCapabilities.OrderBy(capability => capability, StringComparer.Ordinal).ToArray(),
        Items.Values.OrderBy(item => item.Id.Value, StringComparer.Ordinal)
            .Select(item => new CanonicalItem(item.Id.Value,
                item.AllowedPlayerCapabilities.OrderBy(capability => capability, StringComparer.Ordinal).ToArray(),
                item.Container is null
                    ? null
                    : new CanonicalItemContainer(item.Container.Capacity,
                        item.Container.AcceptedDefinitions.Select(definition => definition.Value)
                            .OrderBy(definition => definition, StringComparer.Ordinal).ToArray(), item.Container.DisposableOnSubmission)))
            .ToArray(),
        Appliances.Values.OrderBy(appliance => appliance.Station.Value, StringComparer.Ordinal)
            .Select(appliance => new CanonicalAppliance(appliance.Station.Value,
                appliance.Capabilities.OrderBy(capability => capability, StringComparer.Ordinal).ToArray(), appliance.IsAvailable)).ToArray(),
        Recipes.Values.OrderBy(recipe => recipe.Id.Value, StringComparer.Ordinal)
            .Select(recipe => new CanonicalRecipe(recipe.Id.Value,
                recipe.Inputs.Select(input => input.Value).OrderBy(input => input, StringComparer.Ordinal).ToArray(),
                NormalizeDefinitionList(recipe.DefaultInputs),
                recipe.ProductDefinition.Value,
                recipe.Process.Value, recipe.RequiredApplianceCapability,
                recipe.Completion.ToString(), recipe.RequiredTicks, recipe.RequiresStation, recipe.Execution.ToString(), recipe.YieldPortions, recipe.RequiredProcessingContainerDefinition?.Value)).ToArray(),
        OrderTemplates.Values.OrderBy(template => template.Id.Value, StringComparer.Ordinal)
            .Select(template => new CanonicalOrderTemplate(template.Id.Value, template.RequiredRecipe.Value,
                template.RequiredContainerDefinition.Value, template.BaseScore, template.RequiresBinding)).ToArray(),
        StandardInitialSupply.OrderBy(entry => entry.Definition.Value, StringComparer.Ordinal)
            .ThenBy(entry => entry.Location, StringComparer.Ordinal)
            .Select(entry => new CanonicalSupplyEntry(entry.Definition.Value, entry.Count, entry.Location)).ToArray(),
        Spatial is null ? null : Spatial with {
            InitialPoses = Spatial.InitialPoses.OrderBy(p => p.Player.Value, StringComparer.Ordinal).ToArray(),
            Anchors = Spatial.Anchors.OrderBy(a => a.Kind).ThenBy(a => a.Id, StringComparer.Ordinal).ToArray(),
            Obstacles = Spatial.Obstacles.OrderBy(o => o.MinX).ThenBy(o => o.MinY).ThenBy(o => o.MaxX).ThenBy(o => o.MaxY).ToArray() }, ContentProvenance, Supply),
        CanonicalJsonOptions);

    private static IReadOnlyList<string> NormalizeDefinitionList(IReadOnlyList<DefinitionId>? definitions) =>
        definitions is null
            ? Array.Empty<string>()
            : definitions.Select(definition => definition.Value).OrderBy(definition => definition, StringComparer.Ordinal).ToArray();

    private static string Sha256(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private sealed record CanonicalConfiguration(
        string Schema,
        IReadOnlyList<string> SupportedApplianceCapabilities,
        IReadOnlyList<CanonicalItem> Items,
        IReadOnlyList<CanonicalAppliance> Appliances,
        IReadOnlyList<CanonicalRecipe> Recipes,
        IReadOnlyList<CanonicalOrderTemplate> OrderTemplates,
        IReadOnlyList<CanonicalSupplyEntry> StandardInitialSupply, CookingSpatialConfiguration? Spatial, CookingContentProvenance? ContentProvenance, CookingSupplyConfiguration? Supply);

    private sealed record CanonicalItem(string Id, IReadOnlyList<string> AllowedPlayerCapabilities,
        CanonicalItemContainer? Container);
    private sealed record CanonicalItemContainer(int Capacity, IReadOnlyList<string> AcceptedDefinitions, bool DisposableOnSubmission);
    private sealed record CanonicalAppliance(string Station, IReadOnlyList<string> Capabilities, bool IsAvailable);
    private sealed record CanonicalRecipe(string Id, IReadOnlyList<string> Inputs, IReadOnlyList<string> DefaultInputs,
        string ProductDefinition, string Process, string RequiredApplianceCapability, string Completion, int RequiredTicks,
        bool RequiresStation, string Execution, int YieldPortions, string? RequiredProcessingContainerDefinition);
    private sealed record CanonicalOrderTemplate(string Id, string RequiredRecipe, string RequiredContainerDefinition,
        int BaseScore, bool RequiresBinding);
    private sealed record CanonicalSupplyEntry(string Definition, int Count, string Location);
}

public sealed record CookingConfigurationSubmissionResult(
    bool Accepted,
    CookingConfigurationValidationResult Validation,
    CookingConfigurationIdentity? BeforeIdentity,
    CookingConfigurationIdentity? AfterIdentity);

public sealed class CookingConfigurationRegistry
{
    private CookingConfigurationSnapshot? _current;

    public CookingConfigurationSnapshot? Current => _current;

    public CookingConfigurationSubmissionResult Submit(CookingConfigurationCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var validation = Validate(candidate);
        var before = _current?.Identity;
        if (!validation.IsValid)
            return new CookingConfigurationSubmissionResult(false, validation, before, before);

        _current = CreateSnapshot(candidate);
        return new CookingConfigurationSubmissionResult(true, validation, before, _current.Identity);
    }

    public CookingConfigurationValidationResult Validate(CookingConfigurationCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ValidateCandidateShape(candidate);
        var diagnostics = new List<CookingConfigurationDiagnostic>();
        if (candidate.ContentProvenance is { } provenance && !provenance.IsValid())
            diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.InvalidValue, "ContentProvenance", "catalog", "Sources/SelectedMenus", null,
                "Provenance requires nonblank schema, lowercase SHA-256 hashes, unique source paths and selected menu IDs."));
        var supportedCapabilities = ValidateCapabilities(candidate.SupportedApplianceCapabilities, diagnostics);
        var items = ValidateItems(candidate.Items, diagnostics);
        var appliances = ValidateAppliances(candidate.Appliances, supportedCapabilities, diagnostics);
        var recipes = ValidateRecipes(candidate.Recipes, items, appliances, supportedCapabilities, diagnostics);
        ValidateOrderTemplates(candidate.OrderTemplates, recipes, items, diagnostics);
        ValidateStandardInitialSupply(candidate.StandardInitialSupply, items, appliances, diagnostics);
        try { CookingSupplyIntegration.ValidateAndFreeze(candidate.Supply, items, candidate.Spatial); }
        catch (ArgumentException e) { diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.InvalidValue, "Supply", "configuration", "Suppliers", null, e.Message)); }
        if (candidate.Spatial is { } spatial)
        {
            try
            {
                var players = spatial.InitialPoses.ToDictionary(p => p.Player,
                    p => new CookingPlayerConfig(p.Player, new HashSet<string>(), new HashSet<string>()));
                spatial.Validate(players, appliances);
                var supply = candidate.StandardInitialSupply ?? Array.Empty<CookingSupplyEntryDefinition>();
                if (supply.Where(e => e.Location != CookingContentCatalog.CleanPoolLocation).GroupBy(e => e.Location).Any(g => g.Sum(e => (long)e.Count) > 1))
                    throw new ArgumentException("Ordinary spatial supply anchors hold one object.");
                foreach (var entry in supply)
                {
                    var kind = entry.Location.StartsWith("station:", StringComparison.Ordinal) ? LocationKind.StationSlot : LocationKind.WorldPosition;
                    var id = entry.Location.StartsWith("station:", StringComparison.Ordinal) ? entry.Location[8..] :
                        entry.Location.StartsWith("world:", StringComparison.Ordinal) ? entry.Location[6..] :
                        entry.Location == CookingContentCatalog.CleanPoolLocation ? "clean-pool" : entry.Location;
                    if (!spatial.Anchors.Any(a => a.Kind == kind && a.Id == id))
                        throw new ArgumentException("Initial supply location lacks spatial anchor.");
                }
            }
            catch (ArgumentException e)
            {
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.InvalidValue, "Spatial", "map", "Anchors/Poses", null, e.Message));
            }
        }
        return new CookingConfigurationValidationResult(diagnostics
            .OrderBy(diagnostic => diagnostic.Table, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.RecordId, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Field, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Relation, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ToArray());
    }

    private static void ValidateCandidateShape(CookingConfigurationCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate.SupportedApplianceCapabilities);
        ArgumentNullException.ThrowIfNull(candidate.Items);
        ArgumentNullException.ThrowIfNull(candidate.Appliances);
        ArgumentNullException.ThrowIfNull(candidate.Recipes);
        if (candidate.Items.Any(item => item is null) || candidate.Appliances.Any(appliance => appliance is null) ||
            candidate.Recipes.Any(recipe => recipe is null) ||
            (candidate.OrderTemplates is { } orderTemplates && orderTemplates.Any(template => template is null)) ||
            (candidate.StandardInitialSupply is { } supply && supply.Any(entry => entry is null)))
            throw new ArgumentException("Configuration candidates cannot contain null definitions.", nameof(candidate));
    }

    private static CookingConfigurationSnapshot CreateSnapshot(CookingConfigurationCandidate candidate)
    {
        var capabilities = candidate.SupportedApplianceCapabilities
            .ToFrozenSet(StringComparer.Ordinal);
        var items = candidate.Items.ToFrozenDictionary(item => item.Id,
            item => new CookingItemDefinition(item.Id,
                item.AllowedPlayerCapabilities.ToFrozenSet(StringComparer.Ordinal),
                item.Container is null
                    ? null
                    : new CookingItemContainerCapability(item.Container.Capacity,
                        item.Container.AcceptedDefinitions.ToFrozenSet(), item.Container.DisposableOnSubmission)));
        var appliances = candidate.Appliances.ToFrozenDictionary(appliance => appliance.Station,
            appliance => new CookingApplianceDefinition(appliance.Station,
                appliance.Capabilities.ToFrozenSet(StringComparer.Ordinal), appliance.IsAvailable));
        var recipes = candidate.Recipes.ToFrozenDictionary(recipe => recipe.Id,
            recipe => new CookingRecipeDefinition(recipe.Id, recipe.Inputs.ToArray(), recipe.ProductDefinition, recipe.Process,
                recipe.RequiredApplianceCapability, recipe.RequiredTicks,
                recipe.DefaultInputs?.ToArray(), recipe.Completion, recipe.RequiresStation, recipe.Execution, recipe.YieldPortions, recipe.RequiredProcessingContainerDefinition));
        var orderTemplates = (candidate.OrderTemplates ?? Array.Empty<CookingOrderTemplateDefinition>())
            .ToFrozenDictionary(template => template.Id);
        var standardInitialSupply = (candidate.StandardInitialSupply ?? Array.Empty<CookingSupplyEntryDefinition>()).ToArray();
        return new CookingConfigurationSnapshot(capabilities, items, appliances, recipes, orderTemplates, standardInitialSupply, candidate.Spatial, candidate.ContentProvenance, candidate.Supply);
    }

    private static HashSet<string> ValidateCapabilities(IReadOnlyList<string> capabilities,
        ICollection<CookingConfigurationDiagnostic> diagnostics)
    {
        var unique = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < capabilities.Count; index++)
        {
            var capability = capabilities[index];
            if (string.IsNullOrWhiteSpace(capability))
            {
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.RequiredFieldMissing, "ApplianceCapability", $"index-{index}",
                    "Value", null, "Supported appliance capability must be nonblank."));
                continue;
            }
            if (!unique.Add(capability))
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.DuplicateId, "ApplianceCapability", capability,
                    "Value", null, "Supported appliance capability is declared more than once."));
        }
        return unique;
    }

    private static Dictionary<DefinitionId, CookingItemDefinition> ValidateItems(IReadOnlyList<CookingItemDefinition> items,
        ICollection<CookingConfigurationDiagnostic> diagnostics)
    {
        var unique = new Dictionary<DefinitionId, CookingItemDefinition>();
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Id.Value))
            {
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.RequiredFieldMissing, "ItemDefinition", "<blank>",
                    "Id", null, "Item definition ID must be nonblank."));
                continue;
            }
            if (!unique.TryAdd(item.Id, item))
            {
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.DuplicateId, "ItemDefinition", item.Id.Value,
                    "Id", null, "Item definition ID is declared more than once."));
                continue;
            }
            if (item.AllowedPlayerCapabilities.Count == 0 || item.AllowedPlayerCapabilities.Any(string.IsNullOrWhiteSpace))
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.RequiredFieldMissing, "ItemDefinition", item.Id.Value,
                    "AllowedPlayerCapabilities", null, "Item definition must declare only nonblank player capabilities."));
        }

        // Container capability references must be checked against the whole item table, not the
        // partially built dictionary, so diagnostics never depend on declaration order.
        foreach (var item in unique.Values)
        {
            if (item.Container is null)
                continue;
            if (item.Container.Capacity <= 0)
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.InvalidValue, "ItemDefinition", item.Id.Value,
                    "Container.Capacity", null, "Item container capacity must be positive."));
            foreach (var accepted in item.Container.AcceptedDefinitions.Where(accepted => !string.IsNullOrWhiteSpace(accepted.Value)))
            {
                if (!unique.ContainsKey(accepted))
                    diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.MissingReference, "ItemDefinition", item.Id.Value,
                        "Container.AcceptedDefinitions", accepted.Value,
                        "Item container accepts an item definition that is absent from this candidate batch."));
            }
        }
        return unique;
    }

    private static Dictionary<StationSlotId, CookingApplianceDefinition> ValidateAppliances(
        IReadOnlyList<CookingApplianceDefinition> appliances,
        IReadOnlySet<string> supportedCapabilities,
        ICollection<CookingConfigurationDiagnostic> diagnostics)
    {
        var unique = new Dictionary<StationSlotId, CookingApplianceDefinition>();
        foreach (var appliance in appliances)
        {
            if (string.IsNullOrWhiteSpace(appliance.Station.Value))
            {
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.RequiredFieldMissing, "Appliance", "<blank>",
                    "Station", null, "Appliance station ID must be nonblank."));
                continue;
            }
            if (!unique.TryAdd(appliance.Station, appliance))
            {
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.DuplicateId, "Appliance", appliance.Station.Value,
                    "Station", null, "Appliance station ID is declared more than once."));
                continue;
            }
            if (appliance.Capabilities.Any(string.IsNullOrWhiteSpace))
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.RequiredFieldMissing, "Appliance", appliance.Station.Value,
                    "Capabilities", null, "Appliance must declare only nonblank capabilities."));
            foreach (var capability in appliance.Capabilities.Where(capability => !string.IsNullOrWhiteSpace(capability)))
            {
                if (!supportedCapabilities.Contains(capability))
                    diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.UnknownCapability, "Appliance", appliance.Station.Value,
                        "Capabilities", capability, "Appliance declares a capability not supported by this configuration schema."));
            }
        }
        return unique;
    }

    private static Dictionary<RecipeId, CookingRecipeDefinition> ValidateRecipes(
        IReadOnlyList<CookingRecipeDefinition> recipes,
        IReadOnlyDictionary<DefinitionId, CookingItemDefinition> items,
        IReadOnlyDictionary<StationSlotId, CookingApplianceDefinition> appliances,
        IReadOnlySet<string> supportedCapabilities,
        ICollection<CookingConfigurationDiagnostic> diagnostics)
    {
        var unique = new HashSet<RecipeId>();
        var validated = new Dictionary<RecipeId, CookingRecipeDefinition>();
        foreach (var recipe in recipes)
        {
            var recordId = recipe.Id.Value;
            if (string.IsNullOrWhiteSpace(recordId))
            {
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.RequiredFieldMissing, "Recipe", "<blank>", "Id", null,
                    "Recipe ID must be nonblank."));
                continue;
            }
            if (!unique.Add(recipe.Id))
            {
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.DuplicateId, "Recipe", recordId, "Id", null,
                    "Recipe ID is declared more than once."));
                continue;
            }
            if (string.IsNullOrWhiteSpace(recipe.Process.Value))
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.RequiredFieldMissing, "Recipe", recordId, "Process", null,
                    "Recipe process ID must be nonblank."));
            if (!Enum.IsDefined(recipe.Execution) || recipe.YieldPortions <= 0 ||
                (recipe.YieldPortions > 1 && recipe.Completion != CookingRecipeCompletionKind.RetainInputs))
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.InvalidValue, "Recipe", recordId, "Execution/YieldPortions", null,
                    "Invalid execution kind or unsupported yield/completion combination."));
            if (recipe.RequiredTicks <= 0)
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.InvalidValue, "Recipe", recordId, "RequiredTicks", null,
                    "Recipe required ticks must be positive."));
            if (!Enum.IsDefined(recipe.Completion))
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.InvalidValue, "Recipe", recordId, "Completion",
                    ((int)recipe.Completion).ToString(), "Recipe completion kind is not a defined value."));
            if (string.IsNullOrWhiteSpace(recipe.RequiredApplianceCapability))
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.RequiredFieldMissing, "Recipe", recordId,
                    "RequiredApplianceCapability", null, "Recipe appliance capability must be nonblank."));
            else if (!supportedCapabilities.Contains(recipe.RequiredApplianceCapability))
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.UnknownCapability, "Recipe", recordId,
                    "RequiredApplianceCapability", recipe.RequiredApplianceCapability,
                    "Recipe requires a capability not supported by this configuration schema."));
            else if (recipe.RequiresStation &&
                     !appliances.Values.Any(appliance => appliance.Capabilities.Contains(recipe.RequiredApplianceCapability)))
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.CapabilityUnavailable, "Recipe", recordId,
                    "RequiredApplianceCapability", recipe.RequiredApplianceCapability,
                    "No appliance declares the capability required by this recipe."));

            if (recipe.Inputs.Count == 0)
            {
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.RequiredFieldMissing, "Recipe", recordId, "Inputs", null,
                    "Recipe must declare at least one input definition."));
            }
            else
            {
                foreach (var input in recipe.Inputs)
                {
                    if (string.IsNullOrWhiteSpace(input.Value) || !items.ContainsKey(input))
                    {
                        diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.MissingReference, "Recipe", recordId, "Inputs",
                            input.Value, "Recipe references an item definition that is absent from this candidate batch."));
                        continue;
                    }

                }
            }

            var declaredInputs = recipe.Inputs.Where(input => !string.IsNullOrWhiteSpace(input.Value)).ToHashSet();
            foreach (var defaultInput in recipe.DefaultInputs ?? Array.Empty<DefinitionId>())
            {
                if (string.IsNullOrWhiteSpace(defaultInput.Value) || !items.ContainsKey(defaultInput))
                {
                    diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.MissingReference, "Recipe", recordId, "DefaultInputs",
                        defaultInput.Value, "Recipe declares a default input that is absent from this candidate batch."));
                    continue;
                }
                if (declaredInputs.Contains(defaultInput))
                    diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.InvalidValue, "Recipe", recordId, "DefaultInputs",
                        defaultInput.Value, "Recipe default input duplicates a declared item input."));
            }

            if (recipe.RequiredProcessingContainerDefinition is { } carrier)
            {
                if (!items.TryGetValue(carrier, out var carrierItem) || carrierItem.Container is not { } capability)
                    diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.MissingReference, "Recipe", recordId,
                        "RequiredProcessingContainerDefinition", carrier.Value, "The processing carrier must be a declared container."));
                else if (capability.Capacity < recipe.Inputs.Count || !recipe.Inputs.All(capability.AcceptedDefinitions.Contains))
                    diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.InvalidValue, "Recipe", recordId,
                        "RequiredProcessingContainerDefinition", carrier.Value, "The processing carrier must fit and accept every explicit input."));
            }
            ValidateRecipeDefinitionReference(recipe.ProductDefinition, "ProductDefinition", recipe.Id, items, diagnostics);
            validated[recipe.Id] = recipe;
        }

        return validated;
    }

    private static void ValidateRecipeDefinitionReference(
        DefinitionId definition,
        string field,
        RecipeId recipe,
        IReadOnlyDictionary<DefinitionId, CookingItemDefinition> items,
        ICollection<CookingConfigurationDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(definition.Value) || !items.ContainsKey(definition))
            diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.MissingReference, "Recipe", recipe.Value, field,
                definition.Value, "Recipe references an item definition that is absent from this candidate batch."));
    }

    private static void ValidateOrderTemplates(
        IReadOnlyList<CookingOrderTemplateDefinition>? templates,
        IReadOnlyDictionary<RecipeId, CookingRecipeDefinition> recipes,
        IReadOnlyDictionary<DefinitionId, CookingItemDefinition> items,
        ICollection<CookingConfigurationDiagnostic> diagnostics)
    {
        if (templates is null)
            return;
        var processingCarriers = recipes.Values
            .Where(recipe => recipe.RequiredProcessingContainerDefinition is not null)
            .Select(recipe => recipe.RequiredProcessingContainerDefinition!.Value).ToHashSet();
        var unique = new HashSet<OrderTemplateId>();
        foreach (var template in templates)
        {
            if (string.IsNullOrWhiteSpace(template.Id.Value))
            {
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.RequiredFieldMissing, "OrderTemplate", "<blank>",
                    "Id", null, "Order template ID must be nonblank."));
                continue;
            }
            if (!unique.Add(template.Id))
            {
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.DuplicateId, "OrderTemplate", template.Id.Value,
                    "Id", null, "Order template ID is declared more than once."));
                continue;
            }
            if (!recipes.ContainsKey(template.RequiredRecipe))
            {
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.MissingReference, "OrderTemplate", template.Id.Value,
                    "RequiredRecipe", template.RequiredRecipe.Value,
                    "Order template requires a recipe that is absent from this candidate batch."));
                continue;
            }
            if (!items.TryGetValue(template.RequiredContainerDefinition, out var container) ||
                container.Container is null)
            {
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.MissingReference, "OrderTemplate", template.Id.Value,
                    "RequiredContainerDefinition", template.RequiredContainerDefinition.Value,
                    "Order template requires a container item definition that is absent from this candidate batch."));
                continue;
            }
            if (processingCarriers.Contains(template.RequiredContainerDefinition))
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.InvalidValue, "OrderTemplate", template.Id.Value,
                    "RequiredContainerDefinition", template.RequiredContainerDefinition.Value,
                    "A declared processing carrier cannot be used as a serving vessel."));
            var product = recipes[template.RequiredRecipe].ProductDefinition;
            if (!container.Container.AcceptedDefinitions.Contains(product))
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.MissingReference, "OrderTemplate", template.Id.Value,
                    "RequiredContainerDefinition", product.Value,
                    "Order template container does not accept the product definition of its required recipe."));
        }
    }

    private static void ValidateStandardInitialSupply(
        IReadOnlyList<CookingSupplyEntryDefinition>? supply,
        IReadOnlyDictionary<DefinitionId, CookingItemDefinition> items,
        IReadOnlyDictionary<StationSlotId, CookingApplianceDefinition> appliances,
        ICollection<CookingConfigurationDiagnostic> diagnostics)
    {
        if (supply is null)
            return;
        foreach (var entry in supply)
        {
            var recordId = string.IsNullOrWhiteSpace(entry.Definition.Value) ? "<blank>" : entry.Definition.Value;
            if (string.IsNullOrWhiteSpace(entry.Definition.Value) || !items.ContainsKey(entry.Definition))
            {
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.MissingReference, "StandardInitialSupply", recordId,
                    "Definition", entry.Definition.Value,
                    "Standard initial supply references an item definition that is absent from this candidate batch."));
                continue;
            }
            if (entry.Count <= 0)
            {
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.InvalidValue, "StandardInitialSupply", recordId,
                    "Count", entry.Count.ToString(), "Standard initial supply count must be positive."));
                continue;
            }
            if (string.Equals(entry.Location, "cleanPool", StringComparison.Ordinal))
            {
                if (items[entry.Definition].Container is { DisposableOnSubmission: true })
                    diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.InvalidValue, "StandardInitialSupply", recordId,
                        "Location", entry.Location, "Disposable serving vessels require physical supply and cannot be dispensed from the clean pool."));
                if (items[entry.Definition].Container is null)
                    diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.InvalidValue, "StandardInitialSupply", recordId,
                        "Location", entry.Location,
                        "A clean-pool supply entry must reference an item definition with a container capability."));
                continue;
            }
            if (entry.Location.StartsWith("station:", StringComparison.Ordinal))
            {
                var station = new StationSlotId(entry.Location["station:".Length..]);
                if (!appliances.ContainsKey(station))
                    diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.MissingReference, "StandardInitialSupply", recordId,
                        "Location", entry.Location,
                        "Standard initial supply references a station that is absent from this candidate batch."));
                continue;
            }
            if (!entry.Location.StartsWith("world:", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(entry.Location["world:".Length..]))
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.InvalidValue, "StandardInitialSupply", recordId,
                    "Location", entry.Location,
                    "Standard initial supply location must be 'cleanPool', 'station:<stationId>' or 'world:<position>'."));
        }
    }

    private static CookingConfigurationDiagnostic Diagnostic(string code, string table, string recordId, string field,
        string? relation, string message) => new(code, table, recordId, field, relation, message);
}

public enum CookingConfigurationCompatibilityStatus
{
    Compatible,
    Blocked,
}

public sealed record CookingConfigurationCompatibilityResult(
    CookingConfigurationCompatibilityStatus Status,
    string ReasonCode,
    CookingConfigurationIdentity PresentedIdentity,
    CookingConfigurationIdentity ExpectedIdentity);

public static class CookingConfigurationCompatibility
{
    public const string MigrationPolicyNotApproved = "MigrationPolicyNotApproved";
    public const string IdentityMismatch = "IdentityMismatch";
    public const string Compatible = "Compatible";

    public static CookingConfigurationCompatibilityResult Evaluate(
        CookingConfigurationIdentity presentedIdentity,
        CookingConfigurationIdentity expectedIdentity)
    {
        ArgumentNullException.ThrowIfNull(presentedIdentity);
        ArgumentNullException.ThrowIfNull(expectedIdentity);
        if (!StringComparer.Ordinal.Equals(presentedIdentity.Schema, expectedIdentity.Schema))
            return new CookingConfigurationCompatibilityResult(CookingConfigurationCompatibilityStatus.Blocked,
                MigrationPolicyNotApproved, presentedIdentity, expectedIdentity);
        if (!StringComparer.Ordinal.Equals(presentedIdentity.Sha256, expectedIdentity.Sha256))
            return new CookingConfigurationCompatibilityResult(CookingConfigurationCompatibilityStatus.Blocked,
                IdentityMismatch, presentedIdentity, expectedIdentity);
        return new CookingConfigurationCompatibilityResult(CookingConfigurationCompatibilityStatus.Compatible, Compatible,
            presentedIdentity, expectedIdentity);
    }
}

public sealed record CookingConfigurationAcceptanceEvidence(
    string TestId,
    bool Accepted,
    string? BeforeIdentity,
    string? AfterIdentity,
    IReadOnlyList<CookingConfigurationDiagnostic> Diagnostics,
    string AssertionSummary,
    string Runner,
    string TimestampUtc);

public static class CookingConfigurationAcceptanceEvidenceWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static void Append(string path, CookingConfigurationAcceptanceEvidence evidence)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new ArgumentException("Evidence path has no directory.", nameof(path)));
        File.AppendAllText(path, JsonSerializer.Serialize(evidence, Options) + Environment.NewLine, Encoding.UTF8);
    }

    public static IReadOnlyList<CookingConfigurationAcceptanceEvidence> ReadAll(string path) =>
        File.ReadLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<CookingConfigurationAcceptanceEvidence>(line, Options)
                ?? throw new InvalidDataException("Invalid cooking configuration evidence line."))
            .ToArray();
}
