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

public sealed record CookingConfigurationCandidate(
    IReadOnlyList<string> SupportedApplianceCapabilities,
    IReadOnlyList<CookingItemDefinition> Items,
    IReadOnlyList<CookingApplianceDefinition> Appliances,
    IReadOnlyList<CookingRecipeDefinition> Recipes,
    IReadOnlyList<CookingContainerDefinition> Containers);

public sealed record CookingConfigurationIdentity(string Schema, string Sha256)
{
    public const string CurrentSchema = "cooking-definition-v1";

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
        IReadOnlyDictionary<ContainerId, CookingContainerDefinition> containers)
    {
        SupportedApplianceCapabilities = supportedApplianceCapabilities;
        Items = items;
        Appliances = appliances;
        Recipes = recipes;
        Containers = containers;
        Identity = new CookingConfigurationIdentity(CookingConfigurationIdentity.CurrentSchema, Sha256(CanonicalText()));
    }

    public IReadOnlySet<string> SupportedApplianceCapabilities { get; }
    public IReadOnlyDictionary<DefinitionId, CookingItemDefinition> Items { get; }
    public IReadOnlyDictionary<StationSlotId, CookingApplianceDefinition> Appliances { get; }
    public IReadOnlyDictionary<RecipeId, CookingRecipeDefinition> Recipes { get; }
    public IReadOnlyDictionary<ContainerId, CookingContainerDefinition> Containers { get; }
    public CookingConfigurationIdentity Identity { get; }

    public string CanonicalText() => JsonSerializer.Serialize(new CanonicalConfiguration(
        CookingConfigurationIdentity.CurrentSchema,
        SupportedApplianceCapabilities.OrderBy(capability => capability, StringComparer.Ordinal).ToArray(),
        Items.Values.OrderBy(item => item.Id.Value, StringComparer.Ordinal)
            .Select(item => new CanonicalItem(item.Id.Value,
                item.AllowedPlayerCapabilities.OrderBy(capability => capability, StringComparer.Ordinal).ToArray())).ToArray(),
        Appliances.Values.OrderBy(appliance => appliance.Station.Value, StringComparer.Ordinal)
            .Select(appliance => new CanonicalAppliance(appliance.Station.Value,
                appliance.Capabilities.OrderBy(capability => capability, StringComparer.Ordinal).ToArray(), appliance.IsAvailable)).ToArray(),
        Recipes.Values.OrderBy(recipe => recipe.Id.Value, StringComparer.Ordinal)
            .Select(recipe => new CanonicalRecipe(recipe.Id.Value, recipe.InputDefinition.Value, recipe.ProductDefinition.Value,
                recipe.Process.Value, recipe.RequiredApplianceCapability, recipe.RequiredTicks)).ToArray(),
        Containers.Values.OrderBy(container => container.Id.Value, StringComparer.Ordinal)
            .Select(container => new CanonicalContainer(container.Id.Value, container.Capacity)).ToArray()), CanonicalJsonOptions);

    private static string Sha256(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private sealed record CanonicalConfiguration(
        string Schema,
        IReadOnlyList<string> SupportedApplianceCapabilities,
        IReadOnlyList<CanonicalItem> Items,
        IReadOnlyList<CanonicalAppliance> Appliances,
        IReadOnlyList<CanonicalRecipe> Recipes,
        IReadOnlyList<CanonicalContainer> Containers);

    private sealed record CanonicalItem(string Id, IReadOnlyList<string> AllowedPlayerCapabilities);
    private sealed record CanonicalAppliance(string Station, IReadOnlyList<string> Capabilities, bool IsAvailable);
    private sealed record CanonicalRecipe(string Id, string InputDefinition, string ProductDefinition, string Process,
        string RequiredApplianceCapability, int RequiredTicks);
    private sealed record CanonicalContainer(string Id, int Capacity);
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
        var supportedCapabilities = ValidateCapabilities(candidate.SupportedApplianceCapabilities, diagnostics);
        var items = ValidateItems(candidate.Items, diagnostics);
        var appliances = ValidateAppliances(candidate.Appliances, supportedCapabilities, diagnostics);
        ValidateRecipes(candidate.Recipes, items, appliances, supportedCapabilities, diagnostics);
        ValidateContainers(candidate.Containers, diagnostics);
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
        ArgumentNullException.ThrowIfNull(candidate.Containers);
        if (candidate.Items.Any(item => item is null) || candidate.Appliances.Any(appliance => appliance is null) ||
            candidate.Recipes.Any(recipe => recipe is null) || candidate.Containers.Any(container => container is null))
            throw new ArgumentException("Configuration candidates cannot contain null definitions.", nameof(candidate));
    }

    private static CookingConfigurationSnapshot CreateSnapshot(CookingConfigurationCandidate candidate)
    {
        var capabilities = candidate.SupportedApplianceCapabilities
            .ToFrozenSet(StringComparer.Ordinal);
        var items = candidate.Items.ToFrozenDictionary(item => item.Id,
            item => new CookingItemDefinition(item.Id, item.AllowedPlayerCapabilities.ToFrozenSet(StringComparer.Ordinal)));
        var appliances = candidate.Appliances.ToFrozenDictionary(appliance => appliance.Station,
            appliance => new CookingApplianceDefinition(appliance.Station,
                appliance.Capabilities.ToFrozenSet(StringComparer.Ordinal), appliance.IsAvailable));
        var recipes = candidate.Recipes.ToFrozenDictionary(recipe => recipe.Id,
            recipe => new CookingRecipeDefinition(recipe.Id, recipe.InputDefinition, recipe.ProductDefinition, recipe.Process,
                recipe.RequiredApplianceCapability, recipe.RequiredTicks));
        var containers = candidate.Containers.ToFrozenDictionary(container => container.Id,
            container => new CookingContainerDefinition(container.Id, container.Capacity));
        return new CookingConfigurationSnapshot(capabilities, items, appliances, recipes, containers);
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
            if (appliance.Capabilities.Count == 0 || appliance.Capabilities.Any(string.IsNullOrWhiteSpace))
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

    private static void ValidateRecipes(
        IReadOnlyList<CookingRecipeDefinition> recipes,
        IReadOnlyDictionary<DefinitionId, CookingItemDefinition> items,
        IReadOnlyDictionary<StationSlotId, CookingApplianceDefinition> appliances,
        IReadOnlySet<string> supportedCapabilities,
        ICollection<CookingConfigurationDiagnostic> diagnostics)
    {
        var unique = new HashSet<RecipeId>();
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
            if (recipe.RequiredTicks <= 0)
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.InvalidValue, "Recipe", recordId, "RequiredTicks", null,
                    "Recipe required ticks must be positive."));
            if (string.IsNullOrWhiteSpace(recipe.RequiredApplianceCapability))
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.RequiredFieldMissing, "Recipe", recordId,
                    "RequiredApplianceCapability", null, "Recipe appliance capability must be nonblank."));
            else if (!supportedCapabilities.Contains(recipe.RequiredApplianceCapability))
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.UnknownCapability, "Recipe", recordId,
                    "RequiredApplianceCapability", recipe.RequiredApplianceCapability,
                    "Recipe requires a capability not supported by this configuration schema."));
            else if (!appliances.Values.Any(appliance => appliance.Capabilities.Contains(recipe.RequiredApplianceCapability)))
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.CapabilityUnavailable, "Recipe", recordId,
                    "RequiredApplianceCapability", recipe.RequiredApplianceCapability,
                    "No appliance declares the capability required by this recipe."));

            ValidateRecipeDefinitionReference(recipe.InputDefinition, "InputDefinition", recipe.Id, items, diagnostics);
            ValidateRecipeDefinitionReference(recipe.ProductDefinition, "ProductDefinition", recipe.Id, items, diagnostics);
        }
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

    private static void ValidateContainers(IReadOnlyList<CookingContainerDefinition> containers,
        ICollection<CookingConfigurationDiagnostic> diagnostics)
    {
        var unique = new HashSet<ContainerId>();
        foreach (var container in containers)
        {
            var recordId = container.Id.Value;
            if (string.IsNullOrWhiteSpace(recordId))
            {
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.RequiredFieldMissing, "Container", "<blank>", "Id", null,
                    "Container ID must be nonblank."));
                continue;
            }
            if (!unique.Add(container.Id))
            {
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.DuplicateId, "Container", recordId, "Id", null,
                    "Container ID is declared more than once."));
                continue;
            }
            if (container.Capacity <= 0)
                diagnostics.Add(Diagnostic(CookingConfigurationDiagnosticCodes.InvalidValue, "Container", recordId, "Capacity", null,
                    "Container capacity must be positive."));
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
