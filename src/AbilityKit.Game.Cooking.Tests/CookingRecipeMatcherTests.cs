using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

/// <summary>
/// 纯配方匹配函数契约测试。匹配只依赖数据，不依赖仿真状态；
/// 容器内容到匹配函数的接线属任务 <c>09-22-cooking-kitchen-loop-simulation</c>。
/// </summary>
[Trait("Gate", "CookingRecipeLoop")]
public sealed class CookingRecipeMatcherTests
{
    private static readonly DefinitionId Tomato = new("tomato");
    private static readonly DefinitionId ChoppedTomato = new("chopped-tomato");
    private static readonly DefinitionId BeatenEgg = new("beaten-egg");
    private static readonly DefinitionId Water = new("water");
    private static readonly DefinitionId Soup = new("tomato-egg-soup");
    private static readonly RecipeId SoupRecipe = new("tomato-egg-soup");

    [Fact]
    public void M01_matching_is_independent_of_input_order()
    {
        using var evidence = CreateEvidence("M01");
        var forward = Match(evidence, new[] { ChoppedTomato, BeatenEgg }, "heat",
            new[] { SoupRecipeDefinition() }, "forward input order matches the soup recipe");

        var reversed = Match(evidence, new[] { BeatenEgg, ChoppedTomato }, "heat",
            new[] { SoupRecipeDefinition() }, "reversed input order yields the same match");

        Assert.Equal(CookingRecipeMatchOutcome.Matched, forward.Outcome);
        Assert.Equal(SoupRecipe, forward.Recipe);
        Assert.Equal(forward.Outcome, reversed.Outcome);
        Assert.Equal(forward.Recipe, reversed.Recipe);
        Assert.Equal(forward.Matches, reversed.Matches);
    }

    [Fact]
    public void M02_declared_default_inputs_fold_into_the_matched_set()
    {
        using var evidence = CreateEvidence("M02");
        var result = Match(evidence, new[] { ChoppedTomato, BeatenEgg }, "heat",
            new[] { SoupRecipeDefinition() }, "declared default input folds in and the recipe is hit");

        Assert.Equal(CookingRecipeMatchOutcome.Matched, result.Outcome);
        Assert.Equal(new[] { SoupRecipe }, result.Matches);

        var withoutEgg = Match(evidence, new[] { ChoppedTomato }, "heat",
            new[] { SoupRecipeDefinition() }, "a missing non-default input is not matched");

        Assert.Equal(CookingRecipeMatchOutcome.NotMatched, withoutEgg.Outcome);
        Assert.Null(withoutEgg.Recipe);
        Assert.Empty(withoutEgg.Matches);
    }

    [Fact]
    public void M03_present_default_input_still_matches_the_same_recipe()
    {
        using var evidence = CreateEvidence("M03");
        var result = Match(evidence, new[] { ChoppedTomato, BeatenEgg, Water }, "heat",
            new[] { SoupRecipeDefinition() }, "a physically present default input still matches the same recipe");

        Assert.Equal(CookingRecipeMatchOutcome.Matched, result.Outcome);
        Assert.Equal(SoupRecipe, result.Recipe);
    }

    [Fact]
    public void M04_extra_or_missing_inputs_never_match()
    {
        using var evidence = CreateEvidence("M04");
        var extra = Match(evidence, new[] { ChoppedTomato, BeatenEgg, Tomato }, "heat",
            new[] { SoupRecipeDefinition() }, "an input outside the declared universe is not matched");

        var missing = Match(evidence, new[] { ChoppedTomato }, "heat",
            new[] { SoupRecipeDefinition() }, "a missing declared input is not matched");

        Assert.Equal(CookingRecipeMatchOutcome.NotMatched, extra.Outcome);
        Assert.Equal(CookingRecipeMatchOutcome.NotMatched, missing.Outcome);
        Assert.Empty(extra.Matches);
        Assert.Empty(missing.Matches);
    }

    [Fact]
    public void M05_two_recipes_with_the_same_inputs_are_ambiguous_with_a_deterministic_candidate_list()
    {
        using var evidence = CreateEvidence("M05");
        var first = SoupRecipeDefinition();
        var second = new CookingRecipeDefinition(
            new RecipeId("another-soup"),
            new[] { ChoppedTomato, BeatenEgg },
            Soup,
            new ProcessId("another-soup-process"),
            "heat",
            6,
            new[] { Water });

        var forward = Match(evidence, new[] { ChoppedTomato, BeatenEgg }, "heat", new[] { first, second },
            "two recipes with the same inputs and capability are ambiguous");
        var reversed = Match(evidence, new[] { BeatenEgg, ChoppedTomato }, "heat", new[] { second, first },
            "reversed candidate order yields the same ambiguous candidate list");

        Assert.Equal(CookingRecipeMatchOutcome.Ambiguous, forward.Outcome);
        Assert.Null(forward.Recipe);
        Assert.Equal(new[] { new RecipeId("another-soup"), SoupRecipe }, forward.Matches);
        Assert.Equal(forward.Outcome, reversed.Outcome);
        Assert.Equal(forward.Matches, reversed.Matches);
    }

    [Fact]
    public void M06_required_appliance_capability_filters_candidates()
    {
        using var evidence = CreateEvidence("M06");
        var boil = SoupRecipeDefinition();
        var blend = new CookingRecipeDefinition(
            SoupRecipe,
            new[] { ChoppedTomato, BeatenEgg },
            Soup,
            new ProcessId("tomato-egg-soup-process"),
            "blend",
            6,
            new[] { Water });

        var atStove = Match(evidence, new[] { ChoppedTomato, BeatenEgg }, "heat", new[] { boil, blend },
            "the heat capability selects the boiling recipe");
        var atBlender = Match(evidence, new[] { ChoppedTomato, BeatenEgg }, "blend", new[] { boil, blend },
            "the blend capability selects the blending recipe");
        var atUnknownStation = Match(evidence, new[] { ChoppedTomato, BeatenEgg }, "fry", new[] { boil, blend },
            "an unsupported capability matches no recipe");

        Assert.Equal(CookingRecipeMatchOutcome.Matched, atStove.Outcome);
        Assert.Equal(SoupRecipe, atStove.Recipe);
        Assert.Equal(CookingRecipeMatchOutcome.Matched, atBlender.Outcome);
        Assert.Equal(SoupRecipe, atBlender.Recipe);
        Assert.Equal(CookingRecipeMatchOutcome.NotMatched, atUnknownStation.Outcome);
        Assert.Empty(atUnknownStation.Matches);
    }

    [Fact]
    public void M07_empty_present_inputs_and_empty_candidates_are_not_matched()
    {
        using var evidence = CreateEvidence("M07");
        var emptyPresent = Match(evidence, Array.Empty<DefinitionId>(), "heat", new[] { SoupRecipeDefinition() },
            "an empty container matches no recipe");
        var emptyCandidates = Match(evidence, new[] { ChoppedTomato, BeatenEgg }, "heat",
            Array.Empty<CookingRecipeDefinition>(), "no candidate recipe means no match");

        Assert.Equal(CookingRecipeMatchOutcome.NotMatched, emptyPresent.Outcome);
        Assert.Equal(CookingRecipeMatchOutcome.NotMatched, emptyCandidates.Outcome);
        Assert.Empty(emptyPresent.Matches);
        Assert.Empty(emptyCandidates.Matches);
    }

    [Fact]
    public void M08_repeated_present_inputs_are_counted_before_matching()
    {
        using var evidence = CreateEvidence("M08");
        var duplicated = Match(evidence, new[] { ChoppedTomato, BeatenEgg, ChoppedTomato }, "heat",
            new[] { SoupRecipeDefinition() }, "repeated present inputs retain their counts before matching");

        Assert.Equal(CookingRecipeMatchOutcome.NotMatched, duplicated.Outcome);
        Assert.Null(duplicated.Recipe);
        var counted = SoupRecipeDefinition() with { Inputs = new[] { ChoppedTomato, ChoppedTomato, BeatenEgg } };
        Assert.Equal(CookingRecipeMatchOutcome.Matched, Match(evidence, new[] { ChoppedTomato, BeatenEgg, ChoppedTomato }, "heat",
            new[] { counted }, "a recipe requiring two tomatoes retains exactly two inputs").Outcome);
    }

    private static CookingRecipeDefinition SoupRecipeDefinition() => new(
        SoupRecipe,
        new[] { ChoppedTomato, BeatenEgg },
        Soup,
        new ProcessId("tomato-egg-soup-process"),
        "heat",
        6,
        new[] { Water });

    private static EvidenceScope CreateEvidence(string testId) => new(testId);

    private static CookingRecipeMatchResult Match(EvidenceScope evidence,
        IReadOnlyList<DefinitionId> presentInputs, string applianceCapability,
        IReadOnlyList<CookingRecipeDefinition> candidates, string assertionSummary) =>
        MatchCapabilities(evidence, presentInputs, new HashSet<string>(StringComparer.Ordinal) { applianceCapability },
            candidates, assertionSummary);

    private static CookingRecipeMatchResult MatchCapabilities(EvidenceScope evidence,
        IReadOnlyList<DefinitionId> presentInputs, IReadOnlySet<string>? applianceCapabilities,
        IReadOnlyList<CookingRecipeDefinition> candidates, string assertionSummary)
    {
        var result = CookingRecipeMatcher.Match(presentInputs, applianceCapabilities, candidates);
        CookingRecipeMatchAcceptanceEvidenceWriter.Append(evidence.Path, new CookingRecipeMatchAcceptanceEvidence(
            evidence.TestId,
            presentInputs.OrderBy(definition => definition.Value, StringComparer.Ordinal).ToArray(),
            applianceCapabilities is null ? "<none>" : string.Join(',', applianceCapabilities.OrderBy(value => value, StringComparer.Ordinal)),
            candidates.Select(candidate => candidate.Id).OrderBy(id => id.Value, StringComparer.Ordinal).ToArray(),
            result.Outcome.ToString(),
            result.Recipe,
            result.Matches,
            assertionSummary,
            "dotnet test AbilityKit.Game.Cooking.Tests",
            DateTimeOffset.UtcNow.ToString("O")));
        return result;
    }

    private sealed class EvidenceScope : IDisposable
    {
        private readonly string _directory;
        private readonly bool _keepArtifacts;

        public EvidenceScope(string testId)
        {
            TestId = testId;
            var requestedRoot = Environment.GetEnvironmentVariable("COOKING_RECIPE_MATCH_EVIDENCE_DIRECTORY");
            _keepArtifacts = !string.IsNullOrWhiteSpace(requestedRoot);
            var root = _keepArtifacts ? System.IO.Path.GetFullPath(requestedRoot!) :
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AbilityKit.Game.Cooking.Tests", "recipe-match");
            _directory = System.IO.Path.Combine(root, testId, Guid.NewGuid().ToString("N"));
            Path = System.IO.Path.Combine(_directory, "recipe-match.jsonl");
        }

        public string Path { get; }

        public string TestId { get; }

        public void Dispose()
        {
            if (!_keepArtifacts && System.IO.Directory.Exists(_directory))
                System.IO.Directory.Delete(_directory, recursive: true);
        }
    }
}
