using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

/// <summary>
/// 任务 <c>09-21-cooking-kitchen-loop-contracts</c> 的 schema v2 契约测试。
/// 覆盖 v2 身份、v1 blocked、多输入/默认供应/完成形态/容器能力的数据模型与校验诊断。
/// </summary>
[Trait("Gate", "CookingConfigurationValidation")]
public sealed class CookingConfigurationSchemaV2Tests
{
    private static readonly DefinitionId Tomato = new("tomato");
    private static readonly DefinitionId Egg = new("egg");
    private static readonly DefinitionId ChoppedTomato = new("chopped-tomato");
    private static readonly DefinitionId BeatenEgg = new("beaten-egg");
    private static readonly DefinitionId TomatoEggSoup = new("tomato-egg-soup");
    private static readonly DefinitionId Pot = new("pot");
    private static readonly DefinitionId Water = new("water");

    [Fact]
    public void V01_schema_v2_is_current_and_valid_candidate_exposes_new_data_model()
    {
        using var evidence = CreateEvidence("V01");
        var registry = new CookingConfigurationRegistry();
        var result = registry.Submit(V2Candidate());
        AppendEvidence(evidence, "V01", result.Accepted, result.BeforeIdentity?.ToString(), result.AfterIdentity?.ToString(),
            result.Validation.Diagnostics,
            "schema v2 candidate commits and exposes container capability, multi inputs, default inputs and completion");

        Assert.True(result.Accepted);
        Assert.NotNull(result.AfterIdentity);
        Assert.Equal(CookingConfigurationIdentity.CurrentSchema, result.AfterIdentity!.Schema);
        Assert.Equal("cooking-definition-v2", CookingConfigurationIdentity.CurrentSchema);

        var snapshot = registry.Current!;
        var pot = snapshot.Items[Pot];
        Assert.NotNull(pot.Container);
        Assert.Equal(4, pot.Container!.Capacity);
        Assert.Contains(Tomato, pot.Container.AcceptedDefinitions);
        Assert.Contains(Egg, pot.Container.AcceptedDefinitions);

        var soup = snapshot.Recipes[new RecipeId("tomato-egg-soup")];
        Assert.Equal(new[] { ChoppedTomato, BeatenEgg }, soup.Inputs);
        Assert.Equal(new[] { Water }, soup.DefaultInputs!);
        Assert.Equal(CookingRecipeCompletionKind.RetainInputs, soup.Completion);
        Assert.Equal(CookingRecipeCompletionKind.ConsumeInputs,
            snapshot.Recipes[new RecipeId("chop-tomato")].Completion);
    }

    [Fact]
    public void V02_canonical_text_carries_container_inputs_default_inputs_and_completion()
    {
        var registry = new CookingConfigurationRegistry();
        Assert.True(registry.Submit(V2Candidate()).Accepted);
        var canonical = registry.Current!.CanonicalText();

        Assert.Contains("\"container\"", canonical, StringComparison.Ordinal);
        Assert.Contains("\"acceptedDefinitions\"", canonical, StringComparison.Ordinal);
        Assert.Contains("\"inputs\"", canonical, StringComparison.Ordinal);
        Assert.Contains("\"defaultInputs\"", canonical, StringComparison.Ordinal);
        Assert.Contains("\"completion\"", canonical, StringComparison.Ordinal);
        Assert.Contains("RetainInputs", canonical, StringComparison.Ordinal);
        Assert.Contains("cooking-definition-v2", canonical, StringComparison.Ordinal);
    }

    [Fact]
    public void V03_nullable_collections_normalize_to_empty_set_in_canonical_identity()
    {
        var explicitEmpty = new CookingConfigurationRegistry();
        Assert.True(explicitEmpty.Submit(new CookingConfigurationCandidate(
            new[] { "heat" },
            new[] { Item(Tomato), Item(Egg), Item(TomatoEggSoup) },
            new[] { Appliance("stove-a", "heat") },
            new[]
            {
                Recipe("chop-tomato", new[] { Tomato }, TomatoEggSoup, "heat", 3),
                Recipe("chop-tomato-explicit-empty", new[] { Egg }, TomatoEggSoup, "heat", 3,
                    defaultInputs: Array.Empty<DefinitionId>()),
            })).Accepted);

        var omitted = new CookingConfigurationRegistry();
        Assert.True(omitted.Submit(new CookingConfigurationCandidate(
            new[] { "heat" },
            new[] { Item(Tomato), Item(Egg), Item(TomatoEggSoup) },
            new[] { Appliance("stove-a", "heat") },
            new[]
            {
                Recipe("chop-tomato", new[] { Tomato }, TomatoEggSoup, "heat", 3),
                Recipe("chop-tomato-explicit-empty", new[] { Egg }, TomatoEggSoup, "heat", 3),
            })).Accepted);

        Assert.Equal(explicitEmpty.Current!.CanonicalText(), omitted.Current!.CanonicalText());
        Assert.Equal(explicitEmpty.Current!.Identity, omitted.Current!.Identity);
        Assert.Contains("\"defaultInputs\":[]", explicitEmpty.Current!.CanonicalText(), StringComparison.Ordinal);
    }

    [Fact]
    public void V04_snapshot_rebuild_passes_container_capability_through()
    {
        var registry = new CookingConfigurationRegistry();
        Assert.True(registry.Submit(V2Candidate()).Accepted);
        var pot = registry.Current!.Items[Pot];

        Assert.NotNull(pot.Container);
        Assert.Equal(4, pot.Container!.Capacity);
        Assert.True(pot.Container.AcceptedDefinitions.Contains(ChoppedTomato));
        Assert.False(pot.Container.AcceptedDefinitions.Contains(Water));
    }

    [Fact]
    public void V05_snapshot_is_immutable_against_later_candidate_mutation()
    {
        var registry = new CookingConfigurationRegistry();
        var candidate = V2Candidate();
        Assert.True(registry.Submit(candidate).Accepted);

        var candidatePot = candidate.Items.Single(item => item.Id == Pot);
        var mutableCapabilities = Assert.IsType<HashSet<DefinitionId>>(candidatePot.Container!.AcceptedDefinitions);
        Assert.True(mutableCapabilities.Add(new DefinitionId("late-change")));

        var snapshotPot = registry.Current!.Items[Pot];
        Assert.False(snapshotPot.Container!.AcceptedDefinitions.Contains(new DefinitionId("late-change")));
    }

    [Fact]
    public void V06_v1_identity_is_blocked_without_migration_conversion()
    {
        using var evidence = CreateEvidence("V06");
        var v2 = Assert.IsType<CookingConfigurationIdentity>(
            new CookingConfigurationRegistry().Submit(V2Candidate()).AfterIdentity);
        var v1 = v2 with { Schema = "cooking-definition-v1" };

        var schemaBlocked = CookingConfigurationCompatibility.Evaluate(v1, v2);
        Assert.Equal(CookingConfigurationCompatibilityStatus.Blocked, schemaBlocked.Status);
        Assert.Equal(CookingConfigurationCompatibility.MigrationPolicyNotApproved, schemaBlocked.ReasonCode);
        Assert.Equal(v1, schemaBlocked.PresentedIdentity);
        Assert.Equal(v2, schemaBlocked.ExpectedIdentity);

        var hashBlocked = CookingConfigurationCompatibility.Evaluate(v2 with { Sha256 = new string('0', 64) }, v2);
        Assert.Equal(CookingConfigurationCompatibilityStatus.Blocked, hashBlocked.Status);
        Assert.Equal(CookingConfigurationCompatibility.IdentityMismatch, hashBlocked.ReasonCode);

        var compatible = CookingConfigurationCompatibility.Evaluate(v2, v2);
        Assert.Equal(CookingConfigurationCompatibilityStatus.Compatible, compatible.Status);
        Assert.Equal(CookingConfigurationCompatibility.Compatible, compatible.ReasonCode);

        AppendEvidence(evidence, "V06", false, v1.ToString(), v2.ToString(), Array.Empty<CookingConfigurationDiagnostic>(),
            "v1 schema and mismatched sha256 are blocked with structured reason codes; no migration conversion exists");
        AppendEvidence(evidence, "V06", true, v2.ToString(), v2.ToString(), Array.Empty<CookingConfigurationDiagnostic>(),
            "identical v2 identity is accepted as compatible");
    }

    [Fact]
    public void V07_v2_identity_differs_from_v1_identity_for_the_same_fixture_content()
    {
        var v1Candidate = new CookingConfigurationCandidate(
            new[] { "heat" },
            new[] { Item(Tomato), Item(ChoppedTomato) },
            new[] { Appliance("stove-a", "heat") },
            new[] { Recipe("chop-tomato", new[] { Tomato }, ChoppedTomato, "heat", 3) });

        var v2Registry = new CookingConfigurationRegistry();
        Assert.True(v2Registry.Submit(v1Candidate).Accepted);
        var v2Identity = v2Registry.Current!.Identity;

        Assert.Equal("cooking-definition-v2", v2Identity.Schema);
        Assert.DoesNotContain("cooking-definition-v1", v2Identity.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void V08_multi_input_validation_reports_one_diagnostic_per_defect_with_deterministic_order()
    {
        using var evidence = CreateEvidence("V08");
        var registry = new CookingConfigurationRegistry();
        var candidate = new CookingConfigurationCandidate(
            new[] { "heat" },
            new[]
            {
                Item(Tomato),
                Item(Egg),
                Item(TomatoEggSoup),
                new CookingItemDefinition(Pot, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                    new CookingItemContainerCapability(0, new HashSet<DefinitionId> { new DefinitionId("absent-pot-content") })),
            },
            new[] { Appliance("stove-a", "heat") },
            new[]
            {
                Recipe("empty-inputs", Array.Empty<DefinitionId>(), TomatoEggSoup, "heat", 3),
                Recipe("missing-input-ref", new[] { new DefinitionId("absent-input") }, TomatoEggSoup, "heat", 3),
                Recipe("duplicate-inputs", new[] { Tomato, Tomato }, TomatoEggSoup, "heat", 3),
                Recipe("overlapping-defaults", new[] { Tomato, Egg }, TomatoEggSoup, "heat", 3,
                    defaultInputs: new[] { Tomato }),
            });

        var result = registry.Validate(candidate);
        AppendEvidence(evidence, "V08", result.IsValid, null, null, result.Diagnostics,
            "multi-input, default-input and container-capability diagnostics are collected once and deterministically ordered");

        Assert.False(result.IsValid);
        var diagnostics = result.Diagnostics;
        Assert.Contains(diagnostics, d => d.Code == CookingConfigurationDiagnosticCodes.RequiredFieldMissing &&
            d.Table == "Recipe" && d.RecordId == "empty-inputs" && d.Field == "Inputs");
        Assert.Contains(diagnostics, d => d.Code == CookingConfigurationDiagnosticCodes.MissingReference &&
            d.Table == "Recipe" && d.RecordId == "missing-input-ref" && d.Field == "Inputs" &&
            d.Relation == "absent-input");
        Assert.Contains(diagnostics, d => d.Code == CookingConfigurationDiagnosticCodes.DuplicateId &&
            d.Table == "Recipe" && d.RecordId == "duplicate-inputs" && d.Field == "Inputs" && d.Relation == "tomato");
        Assert.Contains(diagnostics, d => d.Code == CookingConfigurationDiagnosticCodes.InvalidValue &&
            d.Table == "Recipe" && d.RecordId == "overlapping-defaults" && d.Field == "DefaultInputs" &&
            d.Relation == "tomato");
        Assert.Contains(diagnostics, d => d.Code == CookingConfigurationDiagnosticCodes.InvalidValue &&
            d.Table == "ItemDefinition" && d.RecordId == "pot" && d.Field == "Container.Capacity");
        Assert.Contains(diagnostics, d => d.Code == CookingConfigurationDiagnosticCodes.MissingReference &&
            d.Table == "ItemDefinition" && d.RecordId == "pot" && d.Field == "Container.AcceptedDefinitions" &&
            d.Relation == "absent-pot-content");

        var repeated = registry.Validate(candidate);
        Assert.Equal(diagnostics, repeated.Diagnostics);
    }

    [Fact]
    public void V09_adding_recipe_inputs_stays_data_only_without_rule_branches()
    {
        var single = new CookingConfigurationRegistry();
        Assert.True(single.Submit(new CookingConfigurationCandidate(
            new[] { "heat" },
            new[] { Item(Tomato), Item(Egg), Item(TomatoEggSoup) },
            new[] { Appliance("stove-a", "heat") },
            new[] { Recipe("chop-tomato", new[] { Tomato }, TomatoEggSoup, "heat", 3) })).Accepted);

        var multi = new CookingConfigurationRegistry();
        Assert.True(multi.Submit(new CookingConfigurationCandidate(
            new[] { "heat" },
            new[] { Item(Tomato), Item(Egg), Item(TomatoEggSoup) },
            new[] { Appliance("stove-a", "heat") },
            new[] { Recipe("chop-tomato", new[] { Tomato, Egg }, TomatoEggSoup, "heat", 3) })).Accepted);

        Assert.NotEqual(single.Current!.Identity, multi.Current!.Identity);
        Assert.Equal(new[] { Tomato, Egg }, multi.Current!.Recipes[new RecipeId("chop-tomato")].Inputs);
    }

    [Fact]
    public void V10_load_order_does_not_change_v2_identity()
    {
        var first = new CookingConfigurationRegistry();
        var second = new CookingConfigurationRegistry();
        Assert.True(first.Submit(V2Candidate()).Accepted);
        var reordered = second.Submit(V2Candidate(
            items: new[]
            {
                PotItem(), Item(Water), Item(Egg), Item(ChoppedTomato), Item(Tomato), Item(BeatenEgg), Item(TomatoEggSoup),
            },
            recipes: new[]
            {
                Recipe("tomato-egg-soup", new[] { ChoppedTomato, BeatenEgg }, TomatoEggSoup, "heat", 6, new[] { Water },
                    CookingRecipeCompletionKind.RetainInputs),
                Recipe("chop-tomato", new[] { Tomato }, ChoppedTomato, "cut", 2),
                Recipe("beat-egg", new[] { Egg }, BeatenEgg, "blend", 2),
            }));
        Assert.True(reordered.Accepted, string.Join(" | ", reordered.Validation.Diagnostics
            .Select(diagnostic => $"{diagnostic.Table}/{diagnostic.RecordId}/{diagnostic.Field}/{diagnostic.Code}/{diagnostic.Relation}")));

        Assert.Equal(first.Current!.CanonicalText(), second.Current!.CanonicalText());
        Assert.Equal(first.Current!.Identity, second.Current!.Identity);
    }

    [Fact]
    public void V11_container_capability_reference_diagnostics_are_declaration_order_independent()
    {
        using var evidence = CreateEvidence("V11");
        var potFirst = new CookingConfigurationRegistry();
        var potLast = new CookingConfigurationRegistry();
        var missing = new DefinitionId("absent-content");

        var potFirstResult = potFirst.Validate(new CookingConfigurationCandidate(
            new[] { "heat" },
            new[]
            {
                new CookingItemDefinition(Pot, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                    new CookingItemContainerCapability(2, new HashSet<DefinitionId> { missing })),
                Item(Tomato),
            },
            new[] { Appliance("stove-a", "heat") },
            Array.Empty<CookingRecipeDefinition>()));
        var potLastResult = potLast.Validate(new CookingConfigurationCandidate(
            new[] { "heat" },
            new[]
            {
                Item(Tomato),
                new CookingItemDefinition(Pot, new HashSet<string>(StringComparer.Ordinal) { "cook" },
                    new CookingItemContainerCapability(2, new HashSet<DefinitionId> { missing })),
            },
            new[] { Appliance("stove-a", "heat") },
            Array.Empty<CookingRecipeDefinition>()));

        Assert.Equal(potFirstResult.Diagnostics, potLastResult.Diagnostics);
        Assert.Contains(potFirstResult.Diagnostics, diagnostic => diagnostic.Code == CookingConfigurationDiagnosticCodes.MissingReference &&
            diagnostic.Table == "ItemDefinition" && diagnostic.RecordId == "pot" &&
            diagnostic.Field == "Container.AcceptedDefinitions" && diagnostic.Relation == "absent-content");

        AppendEvidence(evidence, "V11", false, null, null, potFirstResult.Diagnostics,
            "container capability reference diagnostics do not depend on item declaration order");
    }

    private static CookingConfigurationCandidate V2Candidate(
        IReadOnlyList<CookingItemDefinition>? items = null,
        IReadOnlyList<CookingRecipeDefinition>? recipes = null) => new(
        new[] { "heat", "cut", "blend" },
        items ?? new[]
        {
            Item(Tomato),
            Item(Egg),
            Item(ChoppedTomato),
            Item(BeatenEgg),
            Item(TomatoEggSoup),
            PotItem(),
            Item(Water),
        },
        new[] { Appliance("stove-a", "heat"), Appliance("board-a", "cut"), Appliance("bowl-station", "blend") },
        recipes ?? new[]
        {
            Recipe("chop-tomato", new[] { Tomato }, ChoppedTomato, "cut", 2),
            Recipe("beat-egg", new[] { Egg }, BeatenEgg, "blend", 2),
            Recipe("tomato-egg-soup", new[] { ChoppedTomato, BeatenEgg }, TomatoEggSoup, "heat", 6, new[] { Water },
                CookingRecipeCompletionKind.RetainInputs),
        });

    private static CookingItemDefinition Item(DefinitionId id) =>
        new(id, new HashSet<string>(StringComparer.Ordinal) { "cook" });

    private static CookingItemDefinition PotItem() =>
        new(Pot, new HashSet<string>(StringComparer.Ordinal) { "cook" },
            new CookingItemContainerCapability(4, new HashSet<DefinitionId> { Tomato, Egg, ChoppedTomato, BeatenEgg }));

    private static CookingApplianceDefinition Appliance(string station, params string[] capabilities) =>
        new(new StationSlotId(station), new HashSet<string>(capabilities, StringComparer.Ordinal));

    private static CookingRecipeDefinition Recipe(string id, IReadOnlyList<DefinitionId> inputs, DefinitionId product,
        string capability, int ticks, IReadOnlyList<DefinitionId>? defaultInputs = null,
        CookingRecipeCompletionKind completion = CookingRecipeCompletionKind.ConsumeInputs) =>
        new(new RecipeId(id), inputs, product, new ProcessId($"{id}-process"), capability, ticks, defaultInputs, completion);

    private static EvidenceScope CreateEvidence(string testId) => new(testId);

    private static void AppendEvidence(EvidenceScope evidence, string testId, bool accepted,
        string? before, string? after,
        IReadOnlyList<CookingConfigurationDiagnostic> diagnostics, string assertionSummary) =>
        CookingConfigurationAcceptanceEvidenceWriter.Append(evidence.Path, new CookingConfigurationAcceptanceEvidence(
            testId, accepted, before, after, diagnostics, assertionSummary,
            "dotnet test AbilityKit.Game.Cooking.Tests", DateTimeOffset.UtcNow.ToString("O")));

    private sealed class EvidenceScope : IDisposable
    {
        private readonly string _directory;
        private readonly bool _keepArtifacts;

        public EvidenceScope(string testId)
        {
            var requestedRoot = Environment.GetEnvironmentVariable("COOKING_CONFIGURATION_EVIDENCE_DIRECTORY");
            _keepArtifacts = !string.IsNullOrWhiteSpace(requestedRoot);
            var root = _keepArtifacts ? System.IO.Path.GetFullPath(requestedRoot!) :
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AbilityKit.Game.Cooking.Tests", "configuration-v2");
            _directory = System.IO.Path.Combine(root, testId, Guid.NewGuid().ToString("N"));
            Path = System.IO.Path.Combine(_directory, "configuration-validation.jsonl");
        }

        public string Path { get; }

        public void Dispose()
        {
            if (!_keepArtifacts && System.IO.Directory.Exists(_directory))
                System.IO.Directory.Delete(_directory, recursive: true);
        }
    }
}
