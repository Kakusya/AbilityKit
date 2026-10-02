using System.Text;
using System.Text.Json;

namespace AbilityKit.Game.Cooking;

/// <summary>
/// 配方匹配的三种结果：唯一命中、未命中、命中多个（配置错误的结构化表现）。
/// </summary>
public enum CookingRecipeMatchOutcome
{
    Matched,
    NotMatched,
    Ambiguous,
}

/// <summary>
/// 纯函数匹配结果。<see cref="Matches"/> 是按 DefinitionId 稳定排序的全部命中配方，
/// 在 <see cref="CookingRecipeMatchOutcome.Ambiguous"/> 时用于结构化诊断。
/// </summary>
public sealed record CookingRecipeMatchResult(
    CookingRecipeMatchOutcome Outcome,
    RecipeId? Recipe,
    IReadOnlyList<RecipeId> Matches);

/// <summary>Pure counted-multiset recipe matching. Input order is irrelevant; repeated definitions retain their counts.
/// Default inputs provide the configured missing count without creating runtime objects.</summary>
public static class CookingRecipeMatcher
{
    /// <param name="presentInputs">容器内物品的定义集合；重复项折叠为集合。</param>
    /// <param name="applianceCapabilities">
    /// 工位提供的能力集合；候选配方要求的能力必须在集合内。传 <c>null</c> 表示无工位约束
    /// （免工位加工），此时候选集已由调用方限定为免工位配方。
    /// </param>
    /// <param name="candidates">
    /// 候选配方，每个候选自带 <see cref="CookingRecipeDefinition.DefaultInputs"/>。
    /// 约定来自已通过配置校验的批次：输入集合为空的配方由配置校验拒绝，
    /// 这里额外跳过，保证函数对未校验数据也是全函数。
    /// </param>
    public static CookingRecipeMatchResult Match(
        IReadOnlyCollection<DefinitionId> presentInputs,
        IReadOnlySet<string>? applianceCapabilities,
        IReadOnlyCollection<CookingRecipeDefinition> candidates,
        DefinitionId? processingContainerDefinition = null)
    {
        ArgumentNullException.ThrowIfNull(presentInputs);
        ArgumentNullException.ThrowIfNull(candidates);

        var present = presentInputs.ToArray();
        var matched = new List<CookingRecipeDefinition>();
        foreach (var candidate in candidates)
        {
            if (candidate is null || candidate.Inputs.Count == 0)
                continue;
            if (applianceCapabilities is not null &&
                !applianceCapabilities.Contains(candidate.RequiredApplianceCapability))
                continue;
            if (candidate.RequiredProcessingContainerDefinition is { } requiredCarrier && requiredCarrier != processingContainerDefinition)
                continue;
            if (!MultisetEqualsPresentPlusDefaults(present, candidate))
                continue;
            matched.Add(candidate);
        }

        if (matched.Count == 0)
            return new CookingRecipeMatchResult(CookingRecipeMatchOutcome.NotMatched, null, Array.Empty<RecipeId>());

        var ordered = matched
            .OrderBy(recipe => recipe.Id.Value, StringComparer.Ordinal)
            .Select(recipe => recipe.Id)
            .ToArray();
        return matched.Count == 1
            ? new CookingRecipeMatchResult(CookingRecipeMatchOutcome.Matched, ordered[0], ordered)
            : new CookingRecipeMatchResult(CookingRecipeMatchOutcome.Ambiguous, null, ordered);
    }

    private static bool MultisetEqualsPresentPlusDefaults(IReadOnlyCollection<DefinitionId> present, CookingRecipeDefinition candidate)
    {
        static Dictionary<DefinitionId, int> Counts(IEnumerable<DefinitionId> values) =>
            values.GroupBy(v => v).ToDictionary(g => g.Key, g => g.Count());
        var effective = Counts(present);
        var expected = Counts(candidate.Inputs);
        foreach (var (definition, count) in Counts(candidate.DefaultInputs ?? Array.Empty<DefinitionId>()))
        {
            expected[definition] = expected.GetValueOrDefault(definition) + count;
            effective[definition] = Math.Max(effective.GetValueOrDefault(definition), count);
        }
        return effective.Count == expected.Count && expected.All(p => effective.GetValueOrDefault(p.Key) == p.Value);
    }

}

public sealed record CookingRecipeMatchAcceptanceEvidence(
    string TestId,
    IReadOnlyList<DefinitionId> PresentInputs,
    string ApplianceCapability,
    IReadOnlyList<RecipeId> CandidateRecipes,
    string Outcome,
    RecipeId? MatchedRecipe,
    IReadOnlyList<RecipeId> Matches,
    string AssertionSummary,
    string Runner,
    string TimestampUtc);

public static class CookingRecipeMatchAcceptanceEvidenceWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static void Append(string path, CookingRecipeMatchAcceptanceEvidence evidence)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new ArgumentException("Evidence path has no directory.", nameof(path)));
        File.AppendAllText(path, JsonSerializer.Serialize(evidence, Options) + Environment.NewLine, Encoding.UTF8);
    }

    public static IReadOnlyList<CookingRecipeMatchAcceptanceEvidence> ReadAll(string path) =>
        File.ReadLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<CookingRecipeMatchAcceptanceEvidence>(line, Options)
                ?? throw new InvalidDataException("Invalid cooking recipe match evidence line."))
            .ToArray();
}
