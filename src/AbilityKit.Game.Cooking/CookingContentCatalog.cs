using System.Text.Json;

namespace AbilityKit.Game.Cooking;

/// <summary>内容文档的 JSON 传输形状；与领域类型分离，避免把序列化属性写进领域记录。</summary>
public sealed record CookingContentItem(
    string Id,
    IReadOnlyList<string> AllowedPlayerCapabilities,
    CookingContentContainer? Container = null);

public sealed record CookingContentContainer(int Capacity, IReadOnlyList<string> AcceptedDefinitions, bool DisposableOnSubmission = false);

public sealed record CookingContentAppliance(string Station, IReadOnlyList<string> Capabilities, bool IsAvailable = true);

public sealed record CookingContentRecipe(
    string Id,
    IReadOnlyList<string> Inputs,
    string ProductDefinition,
    string Process,
    string RequiredApplianceCapability,
    int RequiredTicks,
    IReadOnlyList<string>? DefaultInputs = null,
    string Completion = nameof(CookingRecipeCompletionKind.ConsumeInputs),
    bool RequiresStation = true,
    string Execution = nameof(CookingRecipeExecutionKind.Automatic),
    int YieldPortions = 1,
    string? RequiredProcessingContainerDefinition = null);

public sealed record CookingContentOrderTemplate(string Id, string RequiredRecipe, string RequiredContainerDefinition, int? BaseScore = null, bool RequiresBinding = false);

public sealed record CookingContentSupplyEntry(string Definition, int Count, string Location);

public sealed record CookingContentDocument(
    string Schema,
    IReadOnlyList<string> SupportedApplianceCapabilities,
    IReadOnlyList<CookingContentItem> Items,
    IReadOnlyList<CookingContentAppliance> Appliances,
    IReadOnlyList<CookingContentRecipe> Recipes,
    IReadOnlyList<CookingContentOrderTemplate> OrderTemplates,
    IReadOnlyList<CookingContentSupplyEntry> StandardInitialSupply,
    CookingSpatialConfiguration? Spatial = null)
{
    public CookingContentProvenance? ContentProvenance { get; init; }
    public CookingSupplyConfiguration? Supply { get; init; }
}

/// <summary>
/// 正式内容：经 <c>cooking-definition-v3</c> 校验的物品、工位、配方、订单模板与标准初始供应。
/// 内容文档是内部受信数据；加载失败（schema 不符或校验诊断）属于开发期错误，直接抛出。
/// </summary>
public sealed record CookingContent(
    CookingConfigurationCandidate Candidate,
    IReadOnlyDictionary<OrderTemplateId, CookingOrderTemplateDefinition> OrderTemplates,
    IReadOnlyList<CookingSupplyEntryDefinition> StandardInitialSupply,
    CookingConfigurationIdentity Identity)
{
    /// <summary>加载时经 v2 校验得到的快照；Level 生命周期与 preparation 身份从同一份已验证内容取得。</summary>
    public CookingConfigurationSnapshot Snapshot { get; init; } = null!;

    public IReadOnlyDictionary<DefinitionId, CookingItemDefinition> Items { get; init; } =
        Candidate.Items.ToDictionary(item => item.Id);
    public IReadOnlyDictionary<StationSlotId, CookingApplianceDefinition> Appliances { get; init; } =
        Candidate.Appliances.ToDictionary(appliance => appliance.Station);
    public IReadOnlyDictionary<RecipeId, CookingRecipeDefinition> Recipes { get; init; } =
        Candidate.Recipes.ToDictionary(recipe => recipe.Id);
}

public static class CookingContentCatalog
{
    public const string ContentFileName = "cooking-content-v3.json";
    public const string CleanPoolLocation = "cleanPool";
    private const string StationLocationPrefix = "station:";
    private const string WorldLocationPrefix = "world:";

    private static readonly JsonSerializerOptions DocumentJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>从内容文档 JSON 加载正式内容；schema 不符或校验失败时抛出结构化错误。</summary>
    public static CookingContent Load(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var document = JsonSerializer.Deserialize<CookingContentDocument>(json, DocumentJsonOptions) ??
            throw new ArgumentException("The cooking content document is empty.", nameof(json));
        return Load(document);
    }

    public static CookingContent Load(CookingContentDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!StringComparer.Ordinal.Equals(document.Schema, CookingConfigurationIdentity.CurrentSchema))
            throw new ArgumentException(
                $"The cooking content schema '{document.Schema}' is not '{CookingConfigurationIdentity.CurrentSchema}'.",
                nameof(document));

        var candidate = new CookingConfigurationCandidate(
            document.SupportedApplianceCapabilities,
            document.Items.Select(item => new CookingItemDefinition(
                new DefinitionId(item.Id),
                item.AllowedPlayerCapabilities.ToHashSet(StringComparer.Ordinal),
                item.Container is null
                    ? null
                    : new CookingItemContainerCapability(item.Container.Capacity,
                        item.Container.AcceptedDefinitions.Select(definition => new DefinitionId(definition))
                            .ToHashSet(), item.Container.DisposableOnSubmission))).ToArray(),
            document.Appliances.Select(appliance => new CookingApplianceDefinition(
                new StationSlotId(appliance.Station),
                appliance.Capabilities.ToHashSet(StringComparer.Ordinal),
                appliance.IsAvailable)).ToArray(),
            document.Recipes.Select(recipe => new CookingRecipeDefinition(
                new RecipeId(recipe.Id),
                recipe.Inputs.Select(input => new DefinitionId(input)).ToArray(),
                new DefinitionId(recipe.ProductDefinition),
                new ProcessId(recipe.Process),
                recipe.RequiredApplianceCapability,
                recipe.RequiredTicks,
                recipe.DefaultInputs?.Select(input => new DefinitionId(input)).ToArray(),
                Enum.Parse<CookingRecipeCompletionKind>(recipe.Completion),
                recipe.RequiresStation, Enum.Parse<CookingRecipeExecutionKind>(recipe.Execution), recipe.YieldPortions, recipe.RequiredProcessingContainerDefinition is null ? null : new DefinitionId(recipe.RequiredProcessingContainerDefinition))).ToArray(),
            document.OrderTemplates.Select(template => new CookingOrderTemplateDefinition(
                new OrderTemplateId(template.Id),
                new RecipeId(template.RequiredRecipe),
                new DefinitionId(template.RequiredContainerDefinition),
                template.BaseScore ?? 100, template.RequiresBinding)).ToArray(),
            document.StandardInitialSupply.Select(entry => new CookingSupplyEntryDefinition(
                new DefinitionId(entry.Definition), entry.Count, entry.Location)).ToArray(), document.Spatial) { ContentProvenance = document.ContentProvenance, Supply = document.Supply };

        var registry = new CookingConfigurationRegistry();
        var submission = registry.Submit(candidate);
        if (!submission.Accepted || registry.Current is not { } snapshot)
            throw new ArgumentException(
                "The cooking content document failed cooking-definition-v3 validation: " +
                string.Join(" | ", submission.Validation.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Table}/{diagnostic.RecordId}/{diagnostic.Field}/{diagnostic.Code}/{diagnostic.Relation}")),
                nameof(document));

        return new CookingContent(candidate, snapshot.OrderTemplates, snapshot.StandardInitialSupply, snapshot.Identity)
        {
            Snapshot = snapshot,
            Items = snapshot.Items,
            Appliances = snapshot.Appliances,
            Recipes = snapshot.Recipes,
        };
    }

    /// <summary>
    /// 从内容构造 fixture：订单模板、可洗碗定义与干净池上限由内容的 cleanPool 供应项派生，
    /// 不引入第二套池规则；玩家与布局属宿主/测试关注点，由调用方提供。
    /// </summary>
    public static CookingRecipeFixture BuildFixture(CookingContent content, CookingScope scope,
        IReadOnlyDictionary<PlayerId, CookingPlayerConfig> players, string? cleanPoolLocation = null, CookingSpatialConfiguration? spatial = null) =>
        new(scope,
            players,
            content.Items,
            content.Appliances,
            content.Recipes,
            WashableDefinitions(content),
            CleanContainerSupply(content),
            cleanPoolLocation ?? CleanPoolLocation,
            content.OrderTemplates, spatial: spatial ?? content.Snapshot.Spatial, supply: content.Snapshot.Supply);

    /// <summary>
    /// 按内容的标准初始供应实例化物品：cleanPool 项由仿真构造器自动建池，此处跳过；
    /// 其余按 <c>&lt;definition&gt;-&lt;n&gt;</c> 确定生成实例 ID，保证可重放。
    /// </summary>
    public static void ApplyStandardInitialSupply(CookingRecipeSimulation simulation, CookingContent content)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(content);
        foreach (var entry in content.StandardInitialSupply)
        {
            if (StringComparer.Ordinal.Equals(entry.Location, CleanPoolLocation))
                continue;
            for (var index = 1; index <= entry.Count; index++)
            {
                var id = new ItemId($"{entry.Definition.Value}-{index}");
                if (entry.Location.StartsWith(StationLocationPrefix, StringComparison.Ordinal))
                    simulation.AddItem(id, entry.Definition,
                        ItemLocation.Station(new StationSlotId(entry.Location[StationLocationPrefix.Length..])));
                else if (entry.Location.StartsWith(WorldLocationPrefix, StringComparison.Ordinal))
                    simulation.AddWorldIngredient(id, entry.Definition, entry.Location[WorldLocationPrefix.Length..]);
                else
                    throw new ArgumentException(
                        $"Standard initial supply location '{entry.Location}' is not supported by the catalog.",
                        nameof(content));
            }
        }
    }

    private static HashSet<DefinitionId> WashableDefinitions(CookingContent content) =>
        content.StandardInitialSupply
            .Where(entry => StringComparer.Ordinal.Equals(entry.Location, CleanPoolLocation))
            .Select(entry => entry.Definition)
            .ToHashSet();

    private static Dictionary<DefinitionId, int> CleanContainerSupply(CookingContent content) =>
        content.StandardInitialSupply
            .Where(entry => StringComparer.Ordinal.Equals(entry.Location, CleanPoolLocation))
            .ToDictionary(entry => entry.Definition, entry => entry.Count);
}
