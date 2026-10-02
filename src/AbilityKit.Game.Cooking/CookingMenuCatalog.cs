using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AbilityKit.Game.Cooking;

public enum CookingMenuMaterialKind { Supply, Preparation, Stage, Finished }
public enum CookingMenuExecutionKind { Automatic, Manual }
public enum CookingMenuCategory { Meal, Dessert, Drink }

public sealed record CookingMenuSource(string Path, string Sha256);
public sealed record CookingMenuMaterial(DefinitionId Id, string SourceId, string Name, CookingMenuMaterialKind Kind);
public sealed record CookingMenuInput(DefinitionId Definition, int Portions);
public sealed record CookingMenuStation(string SourceId, StationSlotId Id, string Name, IReadOnlyList<string> Capabilities);
public sealed record CookingMenuContainer(DefinitionId Id, string Name, int Capacity, IReadOnlyList<DefinitionId> AcceptedDefinitions,
    bool Disposable = false);
public sealed record CookingMenuSourceNode(string Menu, string SourceNode, int ExcelRow, string SourceLocator,
    IReadOnlyList<string> SourceCells, string Classification, IReadOnlyList<RecipeId> Recipes, string DeliveryPolicy);
public sealed record CookingMenuStep(RecipeId Id, string SourceId, string SourceLocator,
    IReadOnlyList<CookingMenuInput> Inputs, DefinitionId Output, ProcessId Process, string Capability,
    DefinitionId Carrier, CookingMenuExecutionKind ExecutionKind, int RequiredTicks, int YieldPortions,
    bool MustLast, string Operation, DefinitionId? OutputStorageContainer = null);
public sealed record CookingMenuEntry(string SourceId, string Name, CookingMenuCategory Category,
    DefinitionId Product, RecipeId FinalRecipe, DefinitionId ServingContainer, OrderTemplateId OrderTemplate,
    bool RequiresBinding, int BaseScore, string SourceLocator, bool RequiresStagedFinal,
    IReadOnlyList<DefinitionId> FinalAdditions);
public sealed record CookingMenuDocument(string Schema, string ValueStatus, IReadOnlyList<CookingMenuSource> Sources,
    IReadOnlyList<string> Decisions, IReadOnlyList<CookingMenuMaterial> Materials,
    IReadOnlyList<CookingMenuStation> Stations, IReadOnlyList<CookingMenuContainer> Containers,
    IReadOnlyList<CookingMenuStep> Steps, IReadOnlyList<CookingMenuEntry> Menus,
    IReadOnlyList<CookingMenuSourceNode> SourceNodes);
public sealed record CookingMenuDiagnostic(string Code, string Record, string Relation);
public sealed record CookingMenuRequirements(IReadOnlyList<DefinitionId> Supplies, IReadOnlyList<string> Capabilities,
    IReadOnlyList<DefinitionId> Containers, IReadOnlyList<RecipeId> Recipes)
{
    public IReadOnlyList<string> ProductionCapabilities { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DeliveryCapabilities { get; init; } = Array.Empty<string>();
    public IReadOnlyList<DefinitionId> RefillContainers { get; init; } = Array.Empty<DefinitionId>();
}
public sealed record CookingMenuLevelAvailability(IReadOnlySet<DefinitionId> Supplies,
    IReadOnlySet<string> Capabilities, IReadOnlySet<DefinitionId> Containers);

/// <summary>
/// Validated content graph, not a second kitchen simulation. Only selected dependency closures
/// are projected to the existing formal content loader. All bundled numerical values are fixtures.
/// </summary>
public sealed class CookingMenuCatalog
{
    public const string CurrentSchema = "cooking-menu-catalog-v1";
    public const string ContentFileName = "menu-catalog-v1.json";
    public const string BindingCapability = "menu-capability-bind-order";
    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();
    private readonly CookingMenuDocument document;
    private readonly Dictionary<DefinitionId, CookingMenuMaterial> materials;
    private readonly Dictionary<DefinitionId, CookingMenuStep> producers;
    private readonly Dictionary<string, CookingMenuEntry> menus;

    private CookingMenuCatalog(CookingMenuDocument document)
    {
        this.document = Normalize(document);
        materials = this.document.Materials.ToDictionary(x => x.Id);
        producers = this.document.Steps.ToDictionary(x => x.Output);
        menus = this.document.Menus.ToDictionary(x => x.SourceId, StringComparer.Ordinal);
        Canonical = JsonSerializer.Serialize(this.document, JsonOptions);
        Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical))).ToLowerInvariant();
    }

    // Never expose mutable arrays backing the validated catalog.
    public CookingMenuDocument Document => JsonSerializer.Deserialize<CookingMenuDocument>(Canonical, JsonOptions)!;
    public string Canonical { get; }
    public string Sha256 { get; }

    public static CookingMenuDocument Deserialize(string json) =>
        JsonSerializer.Deserialize<CookingMenuDocument>(json, JsonOptions) ??
        throw new ArgumentException("The menu document is empty.", nameof(json));

    public static CookingMenuCatalog Load(string json) => Load(Deserialize(json));

    public static CookingMenuCatalog Load(CookingMenuDocument candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        // Defensive copy before validation: caller mutations cannot invalidate a loaded graph.
        var copy = Deserialize(JsonSerializer.Serialize(candidate, JsonOptions));
        var diagnostics = Validate(copy);
        if (diagnostics.Count > 0)
            throw new ArgumentException("Menu validation failed: " + string.Join(" | ", diagnostics.Select(x =>
                $"{x.Code}/{x.Record}/{x.Relation}")), nameof(candidate));
        return new CookingMenuCatalog(copy);
    }

    public static IReadOnlyList<CookingMenuDiagnostic> Validate(CookingMenuDocument candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var errors = new List<CookingMenuDiagnostic>();
        void Error(string code, string? record, string? relation) => errors.Add(new(code, record ?? "null", relation ?? "null"));
        if (candidate.Schema != CurrentSchema)
            Error("UnknownSchema", "Catalog", candidate.Schema ?? "null");
        if (candidate.ValueStatus != "fixture-defaults-not-balanced")
            Error("InvalidValueStatus", "Catalog", candidate.ValueStatus ?? "null");
        if (candidate.Materials is null || candidate.Steps is null || candidate.Menus is null ||
            candidate.Stations is null || candidate.Containers is null || candidate.Sources is null ||
            candidate.Decisions is null || candidate.SourceNodes is null ||
            candidate.SourceNodes.Any(x => x is null || x.SourceCells is null || x.Recipes is null) || candidate.Materials.Any(x => x is null) ||
            candidate.Steps.Any(x => x is null || x.Inputs is null || x.Inputs.Any(i => i is null)) ||
            candidate.Menus.Any(x => x is null || x.FinalAdditions is null) || candidate.Sources.Any(x => x is null) ||
            candidate.Stations.Any(x => x is null || x.Capabilities is null) ||
            candidate.Containers.Any(x => x is null || x.AcceptedDefinitions is null))
            return new[] { new CookingMenuDiagnostic("MissingCollection", "Catalog", "document") };

        Dictionary<TKey, TValue> Unique<TKey, TValue>(IEnumerable<TValue> rows, Func<TValue, TKey> key, string table)
            where TKey : notnull
        {
            var result = new Dictionary<TKey, TValue>();
            foreach (var row in rows)
            {
                var id = key(row);
                if (id is null) Error("InvalidId", table, "null");
                else if (!result.TryAdd(id, row)) Error("DuplicateId", table, id.ToString());
            }
            return result;
        }
        var items = Unique(candidate.Materials, x => x.Id, "Materials");
        var carriers = Unique(candidate.Containers, x => x.Id, "Containers");
        var recipes = Unique(candidate.Steps, x => x.Id, "Steps");
        var outputs = Unique(candidate.Steps, x => x.Output, "Producers");
        Unique(candidate.Menus, x => x.SourceId, "Menus");
        Unique(candidate.Menus, x => x.Product, "MenuProducts");
        Unique(candidate.Menus, x => x.OrderTemplate, "Orders");
        Unique(candidate.Stations, x => x.Id, "Stations");
        Unique(candidate.Stations, x => x.SourceId, "StationSources");
        Unique(candidate.Sources, x => x.Path, "Sources");
        Unique(candidate.SourceNodes, x => x.SourceLocator, "SourceNodes");
        foreach (var node in candidate.SourceNodes)
        {
            if (node.Classification is not ("delivery-operation" or "shared-preparation" or "expanded-final" or "final-recipe"))
                Error("InvalidProvenance", node.SourceLocator, "classification");
            if (node.ExcelRow < 5 || node.SourceCells.Count != 10 || node.SourceCells[0] != node.Menu ||
                node.SourceCells[2] != node.SourceNode || !candidate.Menus.Any(x => x.SourceId == node.Menu) ||
                node.SourceLocator != $"加工节点/{node.Menu}/{node.SourceNode}")
                Error("InvalidProvenance", node.SourceLocator, "source coordinates/cells");
            if (node.Classification == "delivery-operation")
            {
                if (node.SourceNode != "SERVE" || node.Recipes.Count != 0 || node.DeliveryPolicy !=
                    (node.Menu.StartsWith('D') ? "bind-then-submit" : "submit-without-binding"))
                    Error("InvalidProvenance", node.SourceLocator, "delivery");
            }
            else if (node.Recipes.Count == 0 || node.Recipes.Any(x => !recipes.TryGetValue(x, out var recipe) ||
                recipe.SourceId != (node.SourceNode == "FINAL" ? node.Menu : node.SourceNode)))
                Error("InvalidProvenance", node.SourceLocator, "recipe projection");
        }
        foreach (var menu in candidate.Menus)
        {
            if (!candidate.SourceNodes.Any(x => x.Menu == menu.SourceId && x.SourceNode == "FINAL") ||
                !candidate.SourceNodes.Any(x => x.Menu == menu.SourceId && x.SourceNode == "SERVE"))
                Error("MissingProvenance", menu.SourceId, "FINAL/SERVE");
            var seen = new HashSet<DefinitionId>();
            void Trace(DefinitionId id)
            {
                if (!seen.Add(id) || !outputs.TryGetValue(id, out var producer)) return;
                if (items.TryGetValue(id, out var material) && material.Kind == CookingMenuMaterialKind.Preparation &&
                    !candidate.SourceNodes.Any(x => x.Menu == menu.SourceId && x.SourceNode == material.SourceId &&
                        x.Recipes.Contains(producer.Id)))
                    Error("MissingProvenance", menu.SourceId, material.SourceId);
                foreach (var input in producer.Inputs) Trace(input.Definition);
            }
            Trace(menu.Product);
        }
        var supported = candidate.Stations.SelectMany(x => x.Capabilities).ToHashSet(StringComparer.Ordinal);
        foreach (var source in candidate.Sources)
            if (string.IsNullOrWhiteSpace(source.Path) || source.Sha256 is null ||
                source.Sha256.Length != 64 || !source.Sha256.All(Uri.IsHexDigit))
                Error("InvalidSource", source.Path ?? "null", "sha256/path");
        if (candidate.Sources.Count == 0) Error("InvalidSource", "Catalog", "sources");
        foreach (var item in items.Values)
        {
            if (string.IsNullOrWhiteSpace(item.Id.Value) || string.IsNullOrWhiteSpace(item.SourceId) ||
                string.IsNullOrWhiteSpace(item.Name) || !Enum.IsDefined(item.Kind))
                Error("InvalidMaterial", item.Id.Value ?? "null", "identity/kind");
            if (carriers.ContainsKey(item.Id)) Error("DuplicateId", "Materials/Containers", item.Id.Value);
            if (item.Kind == CookingMenuMaterialKind.Supply && outputs.ContainsKey(item.Id))
                Error("SupplyHasProducer", item.Id.Value, outputs[item.Id].Id.Value);
            if (item.Kind != CookingMenuMaterialKind.Supply && !outputs.ContainsKey(item.Id))
                Error("MissingProducer", item.Id.Value, "recipe");
        }
        foreach (var station in candidate.Stations)
            if (string.IsNullOrWhiteSpace(station.Id.Value) || string.IsNullOrWhiteSpace(station.SourceId) ||
                station.Capabilities.Count == 0 || station.Capabilities.Any(string.IsNullOrWhiteSpace) ||
                station.Capabilities.Distinct(StringComparer.Ordinal).Count() != station.Capabilities.Count)
                Error("InvalidStation", station.Id.Value ?? "null", "capabilities/identity");
        foreach (var carrier in carriers.Values)
        {
            if (string.IsNullOrWhiteSpace(carrier.Id.Value) || carrier.Capacity < 1)
                Error("InvalidContainer", carrier.Id.Value ?? "null", "capacity/id");
            foreach (var accepted in carrier.AcceptedDefinitions)
                if (!items.ContainsKey(accepted)) Error("MissingReference", carrier.Id.Value, accepted.Value);
        }
        foreach (var step in recipes.Values)
        {
            var record = step.Id.Value;
            if (string.IsNullOrWhiteSpace(record) || string.IsNullOrWhiteSpace(step.Process.Value) ||
                string.IsNullOrWhiteSpace(step.SourceLocator) || string.IsNullOrWhiteSpace(step.SourceId))
                Error("InvalidStep", record ?? "null", "identity/source/process");
            if (!Enum.IsDefined(step.ExecutionKind)) Error("InvalidMode", record, "executionKind");
            if (step.RequiredTicks < 1 || step.YieldPortions < 1 || step.Inputs.Count == 0 || step.Inputs.Any(x => x.Portions < 1))
                Error("InvalidPortions", record, "inputs/ticks/yield");
            if (step.Inputs.Select(x => x.Definition).Distinct().Count() != step.Inputs.Count)
                Error("DuplicateId", record, "inputDefinition: use Portions");
            if (!items.ContainsKey(step.Output)) Error("MissingReference", record, step.Output.Value);
            foreach (var input in step.Inputs)
                if (!items.ContainsKey(input.Definition)) Error("MissingReference", record, input.Definition.Value);
            if (string.IsNullOrWhiteSpace(step.Capability) || !supported.Contains(step.Capability))
                Error("MissingCapability", record, step.Capability ?? "null");
            if (!carriers.TryGetValue(step.Carrier, out var carrier))
                Error("MissingContainer", record, step.Carrier.Value);
            else
            {
                if (step.Inputs.Sum(x => (long)x.Portions) > carrier.Capacity)
                    Error("ContainerCapacity", record, carrier.Id.Value);
                foreach (var definition in step.Inputs.Select(x => x.Definition).Append(step.Output))
                    if (!carrier.AcceptedDefinitions.Contains(definition))
                        Error("ContainerRejectsMaterial", record, definition.Value);
            }
            if (step.OutputStorageContainer is { } storageId)
            {
                if (!carriers.TryGetValue(storageId, out var storage))
                    Error("MissingContainer", record, storageId.Value);
                else if (!storage.AcceptedDefinitions.Contains(step.Output) || storage.Capacity < step.YieldPortions)
                    Error("InvalidOutputStorage", record, storageId.Value);
            }
            if (step.MustLast)
            {
                if (outputs.Values.Any(x => x.Inputs.Any(i => i.Definition == step.Output)))
                    Error("MustLastViolation", record, "output consumed by a later step");
                if (!step.Inputs.Any(x => items.TryGetValue(x.Definition, out var input) && input.Kind == CookingMenuMaterialKind.Stage))
                    Error("MustLastViolation", record, "missing explicit prior stage");
            }
        }
        foreach (var menu in candidate.Menus)
        {
            if (string.IsNullOrWhiteSpace(menu.SourceId) || string.IsNullOrWhiteSpace(menu.OrderTemplate.Value) ||
                !Enum.IsDefined(menu.Category) || menu.BaseScore < 0 ||
                !items.TryGetValue(menu.Product, out var product) || product.Kind != CookingMenuMaterialKind.Finished ||
                product.SourceId != menu.SourceId || !recipes.TryGetValue(menu.FinalRecipe, out var final) || final.Output != menu.Product)
                Error("InvalidFinal", menu.SourceId ?? "null", "product/recipe/order/category");
            if (!carriers.TryGetValue(menu.ServingContainer, out var carrier) || !carrier.AcceptedDefinitions.Contains(menu.Product))
                Error("MissingContainer", menu.SourceId, menu.ServingContainer.Value);
            if (menu.RequiresBinding != (menu.Category == CookingMenuCategory.Drink))
                Error("InvalidBinding", menu.SourceId, "drinks require binding; meals/desserts do not");
            if (menu.RequiresBinding && !supported.Contains(BindingCapability))
                Error("MissingCapability", menu.SourceId, BindingCapability);
            if (recipes.TryGetValue(menu.FinalRecipe, out var finalStep))
            {
                if (menu.RequiresStagedFinal != finalStep.MustLast ||
                    menu.FinalAdditions.Any(x => !finalStep.Inputs.Any(i => i.Definition == x)))
                    Error("MustLastViolation", menu.SourceId, "required final additions/stage");
                var pending = new Stack<DefinitionId>(finalStep.Inputs.Select(x => x.Definition)
                    .Where(x => items.TryGetValue(x, out var item) && item.Kind == CookingMenuMaterialKind.Stage));
                var seen = new HashSet<DefinitionId>();
                while (pending.TryPop(out var stage))
                {
                    if (!seen.Add(stage) || !outputs.TryGetValue(stage, out var earlier)) continue;
                    if (earlier.Inputs.Any(x => menu.FinalAdditions.Contains(x.Definition)))
                        Error("MustLastViolation", menu.SourceId, "final addition used in an earlier stage");
                    foreach (var input in earlier.Inputs.Where(x => items.TryGetValue(x.Definition, out var item) && item.Kind == CookingMenuMaterialKind.Stage))
                        pending.Push(input.Definition);
                }
            }
        }
        var states = new Dictionary<DefinitionId, int>();
        void Visit(DefinitionId id)
        {
            if (states.TryGetValue(id, out var state))
            {
                if (state == 1) Error("Cycle", id.Value, "recipe input graph");
                return;
            }
            states[id] = 1;
            if (outputs.TryGetValue(id, out var step)) foreach (var input in step.Inputs) Visit(input.Definition);
            states[id] = 2;
        }
        foreach (var item in items.Keys) Visit(item);
        return errors.OrderBy(x => x.Code, StringComparer.Ordinal).ThenBy(x => x.Record, StringComparer.Ordinal)
            .ThenBy(x => x.Relation, StringComparer.Ordinal).ToArray();
    }

    /// <summary>Dependency closure only; independent preparation branches are never ordered by row number.</summary>
    public CookingMenuRequirements Requirements(IEnumerable<string> sourceIds)
    {
        var supplies = new HashSet<DefinitionId>();
        var recipes = new HashSet<RecipeId>();
        var caps = new HashSet<string>(StringComparer.Ordinal);
        var delivery = new HashSet<string>(StringComparer.Ordinal);
        var carriers = new HashSet<DefinitionId>();
        void Visit(DefinitionId id)
        {
            if (materials[id].Kind == CookingMenuMaterialKind.Supply) { supplies.Add(id); return; }
            var step = producers[id];
            if (!recipes.Add(step.Id)) return;
            caps.Add(step.Capability);
            carriers.Add(step.Carrier);
            if (step.OutputStorageContainer is { } storage) carriers.Add(storage);
            foreach (var input in step.Inputs) Visit(input.Definition);
        }
        foreach (var sourceId in sourceIds.Distinct(StringComparer.Ordinal))
        {
            if (!menus.TryGetValue(sourceId, out var menu))
                throw new ArgumentException($"UnknownMenu/{sourceId}", nameof(sourceIds));
            Visit(menu.Product);
            carriers.Add(menu.ServingContainer);
            if (menu.RequiresBinding) delivery.Add(BindingCapability);
        }
        return new(Sort(supplies, x => x.Value), caps.Concat(delivery).Distinct().Order(StringComparer.Ordinal).ToArray(),
            Sort(carriers, x => x.Value), Sort(recipes, x => x.Value))
        {
            ProductionCapabilities = caps.Order(StringComparer.Ordinal).ToArray(),
            DeliveryCapabilities = delivery.Order(StringComparer.Ordinal).ToArray(),
            RefillContainers = Sort(document.Containers.Where(x => x.Disposable && carriers.Contains(x.Id)).Select(x => x.Id), x => x.Value),
        };
    }

    public IReadOnlyList<CookingMenuDiagnostic> ValidateLevel(IEnumerable<string> sourceIds, CookingMenuLevelAvailability available)
    {
        var required = Requirements(sourceIds);
        return required.Supplies.Where(x => !available.Supplies.Contains(x)).Select(x => new CookingMenuDiagnostic("MissingSupply", "Level", x.Value))
            .Concat(required.Capabilities.Where(x => !available.Capabilities.Contains(x)).Select(x => new CookingMenuDiagnostic("MissingCapability", "Level", x)))
            .Concat(required.Containers.Where(x => !available.Containers.Contains(x)).Select(x => new CookingMenuDiagnostic("MissingContainer", "Level", x.Value)))
            .OrderBy(x => x.Code, StringComparer.Ordinal).ThenBy(x => x.Relation, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Projects into the actual content document. The core-owner adapter supplies new schema fields;
    /// no serializer/reflection trick may silently drop Manual or YieldPortions. Initial supply is a
    /// compatibility fixture (8 material portions, one carrier, two serving containers), not spatial
    /// stock/refill proof or procurement balance. Spatial fixtures use unique anchors/S07 packages.
    /// </summary>
    public CookingContentDocument ToContentDocument(CookingContentDocument baseline, IEnumerable<string> sourceIds,
        Func<CookingMenuStep, CookingContentRecipe>? recipeFactory = null)
    {
        var selected = sourceIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var required = Requirements(selected);
        var steps = document.Steps.Where(x => required.Recipes.Contains(x.Id)).ToArray();
        var selectedMenus = selected.Select(x => menus[x]).ToArray();
        var definitions = steps.SelectMany(x => x.Inputs.Select(i => i.Definition).Append(x.Output)).ToHashSet();
        var carrierDefs = document.Containers.Where(x => required.Containers.Contains(x.Id)).ToArray();
        var projectedRecipes = steps.Select(x => (recipeFactory ?? MapRuntimeRecipe)(CloneStep(x))).ToArray();
        // Adapters are allowed to add runtime fields, never to alter recipe identity or its multiset.
        foreach (var pair in steps.Zip(projectedRecipes))
            if (pair.Second.Id != pair.First.Id.Value || pair.Second.ProductDefinition != pair.First.Output.Value ||
                pair.Second.Process != pair.First.Process.Value || pair.Second.RequiredApplianceCapability != pair.First.Capability ||
                pair.Second.RequiredTicks != pair.First.RequiredTicks || pair.Second.DefaultInputs?.Count > 0 ||
                pair.Second.Execution != pair.First.ExecutionKind.ToString() || pair.Second.YieldPortions != pair.First.YieldPortions ||
                pair.Second.RequiredProcessingContainerDefinition != pair.First.Carrier.Value ||
                !pair.Second.RequiresStation || pair.Second.Completion != Completion(pair.First) ||
                !pair.Second.Inputs.Order(StringComparer.Ordinal).SequenceEqual(ExpandInputs(pair.First).Order(StringComparer.Ordinal)))
                throw new ArgumentException($"InvalidRuntimeAdapter/{pair.First.Id.Value}", nameof(recipeFactory));
        var additions = document.Materials.Where(x => definitions.Contains(x.Id))
            .Select(x => new CookingContentItem(x.Id.Value, new[] { "cook" }))
            .Concat(carrierDefs.Select(x => new CookingContentItem(x.Id.Value, new[] { "cook" },
                new CookingContentContainer(x.Capacity, x.AcceptedDefinitions.Where(definitions.Contains).Select(d => d.Value).ToArray(),
                    DisposableOnSubmission: x.Disposable))));
        var servingIds = selectedMenus.Select(x => x.ServingContainer).ToHashSet();
        var supply = required.Supplies.Select(x => new CookingContentSupplyEntry(x.Value, 8, "world:menu-supply-" + x.Value))
            .Concat(carrierDefs.Select(x => new CookingContentSupplyEntry(x.Id.Value, servingIds.Contains(x.Id) ? 2 : 1,
                servingIds.Contains(x.Id) && !x.Disposable ? CookingContentCatalog.CleanPoolLocation : "world:menu-carrier-" + x.Id.Value)));
        var appliances = document.Stations.Where(x => x.Capabilities.Any(required.Capabilities.Contains))
            .Select(x => new CookingContentAppliance(x.Id.Value, x.Capabilities));
        return baseline with
        {
            // A physical multi-mode station retains its declared modes even when the selected
            // menu only needs one; every declared mode must remain in the supported vocabulary.
            SupportedApplianceCapabilities = baseline.SupportedApplianceCapabilities.Concat(required.Capabilities)
                .Concat(appliances.SelectMany(x => x.Capabilities)).Distinct(StringComparer.Ordinal).ToArray(),
            Items = baseline.Items.Concat(additions).ToArray(),
            Appliances = baseline.Appliances.Concat(appliances).ToArray(),
            Recipes = baseline.Recipes.Concat(projectedRecipes).ToArray(),
            OrderTemplates = baseline.OrderTemplates.Concat(selectedMenus.Select(x => new CookingContentOrderTemplate(
                x.OrderTemplate.Value, x.FinalRecipe.Value, x.ServingContainer.Value, x.BaseScore,
                RequiresBinding: x.RequiresBinding))).ToArray(),
            StandardInitialSupply = baseline.StandardInitialSupply.Concat(supply).ToArray(),
            ContentProvenance = new(CurrentSchema, Sha256,
                document.Sources.Select(x => new CookingContentSourceIdentity(x.Path, x.Sha256)).ToArray(), selected),
        };
    }

    /// <summary>Production loader entry: runtime identity/validation comes from the existing registry.</summary>
    public CookingContent LoadContent(CookingContentDocument baseline, IEnumerable<string> sourceIds,
        Func<CookingMenuStep, CookingContentRecipe>? recipeFactory = null) =>
        CookingContentCatalog.Load(ToContentDocument(baseline, sourceIds, recipeFactory));

    public static IReadOnlyList<string> ExpandInputs(CookingMenuStep step) => step.Inputs
        .SelectMany(x => Enumerable.Repeat(x.Definition.Value, x.Portions)).ToArray();

    public static string Completion(CookingMenuStep step) => step.YieldPortions > 1
        ? nameof(CookingRecipeCompletionKind.RetainInputs) : nameof(CookingRecipeCompletionKind.ConsumeInputs);

    private static CookingContentRecipe MapRuntimeRecipe(CookingMenuStep step) =>
        new(step.Id.Value, ExpandInputs(step), step.Output.Value, step.Process.Value,
            step.Capability, step.RequiredTicks, Completion: Completion(step),
            Execution: step.ExecutionKind.ToString(), YieldPortions: step.YieldPortions,
            RequiredProcessingContainerDefinition: step.Carrier.Value);

    private static CookingMenuStep CloneStep(CookingMenuStep step) => step with { Inputs = step.Inputs.ToArray() };
    private static T[] Sort<T>(IEnumerable<T> values, Func<T, string> key) => values.OrderBy(key, StringComparer.Ordinal).ToArray();
    private static CookingMenuDocument Normalize(CookingMenuDocument candidate) => candidate with
    {
        Sources = Sort(candidate.Sources.Select(x => x with { Sha256 = x.Sha256.ToLowerInvariant() }), x => x.Path),
        Decisions = candidate.Decisions.Order(StringComparer.Ordinal).ToArray(),
        Materials = Sort(candidate.Materials, x => x.Id.Value),
        Stations = Sort(candidate.Stations.Select(x => x with { Capabilities = x.Capabilities.Order(StringComparer.Ordinal).ToArray() }), x => x.Id.Value),
        Containers = Sort(candidate.Containers.Select(x => x with { AcceptedDefinitions = Sort(x.AcceptedDefinitions.Distinct(), d => d.Value) }), x => x.Id.Value),
        Steps = Sort(candidate.Steps.Select(x => x with { Inputs = Sort(x.Inputs, i => i.Definition.Value) }), x => x.Id.Value),
        Menus = Sort(candidate.Menus.Select(x => x with { FinalAdditions = Sort(x.FinalAdditions, i => i.Value) }), x => x.SourceId),
        SourceNodes = Sort(candidate.SourceNodes.Select(x => x with { Recipes = Sort(x.Recipes, r => r.Value) }), x => x.SourceLocator),
    };

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new IdConverter<DefinitionId>(x => new(x), x => x.Value));
        options.Converters.Add(new IdConverter<RecipeId>(x => new(x), x => x.Value));
        options.Converters.Add(new IdConverter<ProcessId>(x => new(x), x => x.Value));
        options.Converters.Add(new IdConverter<StationSlotId>(x => new(x), x => x.Value));
        options.Converters.Add(new IdConverter<OrderTemplateId>(x => new(x), x => x.Value));
        return options;
    }

    private sealed class IdConverter<T>(Func<string, T> create, Func<T, string> value) : JsonConverter<T>
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            create(reader.GetString() ?? throw new JsonException("Missing ID."));
        public override void Write(Utf8JsonWriter writer, T item, JsonSerializerOptions options) => writer.WriteStringValue(value(item));
    }
}
