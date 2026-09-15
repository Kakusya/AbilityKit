using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingConfigurationValidation")]
public sealed class CookingConfigurationValidationTests
{
    [Fact]
    public void C01_valid_candidate_commits_queryable_immutable_snapshot_with_stable_identity()
    {
        using var evidence = CreateEvidence("C01");
        var registry = new CookingConfigurationRegistry();
        var candidate = Candidate();

        var result = Submit(registry, candidate, evidence, "C01",
            "valid definition batch commits once and exposes a queryable immutable snapshot");
        Assert.True(((HashSet<string>)candidate.Items[0].AllowedPlayerCapabilities).Add("late-change"));

        Assert.True(result.Accepted);
        Assert.True(result.Validation.IsValid);
        Assert.Null(result.BeforeIdentity);
        Assert.NotNull(result.AfterIdentity);
        Assert.NotNull(registry.Current);
        Assert.Equal(result.AfterIdentity, registry.Current.Identity);
        Assert.Equal("raw-a", registry.Current.Items[new DefinitionId("raw-a")].Id.Value);
        Assert.DoesNotContain("late-change", registry.Current.Items[new DefinitionId("raw-a")].AllowedPlayerCapabilities);
        Assert.False(registry.Current.Items is Dictionary<DefinitionId, CookingItemDefinition>);
        Assert.Equal("heat", registry.Current.Recipes[new RecipeId("recipe-a")].RequiredApplianceCapability);
        AssertEvidence(evidence.Path, "C01", 1);
    }

    [Fact]
    public void C02_invalid_candidate_reports_all_stable_diagnostics_and_keeps_previous_identity()
    {
        using var evidence = CreateEvidence("C02");
        var registry = new CookingConfigurationRegistry();
        var initial = Submit(registry, Candidate(), evidence, "C02", "initial valid candidate establishes old identity");
        var before = Assert.IsType<CookingConfigurationIdentity>(initial.AfterIdentity);
        var invalid = Candidate(
            capabilities: new[] { "heat", "heat", "" },
            items: new[]
            {
                Item("raw-a"),
                Item("raw-a"),
                new CookingItemDefinition(new DefinitionId("product-a"), new HashSet<string>(StringComparer.Ordinal)),
            },
            appliances: new[]
            {
                Appliance("stove-a", "unknown"),
                Appliance("stove-a", "heat"),
            },
            recipes: new[]
            {
                Recipe("recipe-a", "missing-input", "product-a", "heat", 0),
                Recipe("recipe-a", "raw-a", "missing-product", "unknown", 3),
            },
            containers: new[]
            {
                new CookingContainerDefinition(new ContainerId("plate-a"), 0),
                new CookingContainerDefinition(new ContainerId("plate-a"), 1),
            });

        var rejected = Submit(registry, invalid, evidence, "C02",
            "invalid batch returns all diagnostics and leaves the committed configuration unchanged");

        Assert.False(rejected.Accepted);
        Assert.Equal(before, rejected.BeforeIdentity);
        Assert.Equal(before, rejected.AfterIdentity);
        Assert.Equal(before, registry.Current!.Identity);
        var repeated = registry.Validate(invalid);
        Assert.Equal(rejected.Validation.Diagnostics, repeated.Diagnostics);
        Assert.True(rejected.Validation.Diagnostics.Count >= 8);
        Assert.Contains(rejected.Validation.Diagnostics, diagnostic => diagnostic.Table == "Recipe" &&
            diagnostic.RecordId == "recipe-a" && diagnostic.Field == "InputDefinition" && diagnostic.Relation == "missing-input");
        Assert.Contains(rejected.Validation.Diagnostics, diagnostic => diagnostic.Table == "Appliance" &&
            diagnostic.RecordId == "stove-a" && diagnostic.Field == "Capabilities" && diagnostic.Relation == "unknown");
        AssertEvidence(evidence.Path, "C02", 2);
    }

    [Fact]
    public void C04_C05_recipe_and_appliance_extensions_are_validated_by_data_without_rule_branches()
    {
        using var evidence = CreateEvidence("C04-C05");
        var registry = new CookingConfigurationRegistry();
        var extended = Candidate(
            capabilities: new[] { "blend", "heat" },
            items: new[] { Item("product-a"), Item("raw-a"), Item("product-b"), Item("raw-b") },
            appliances: new[] { Appliance("blender-a", "blend"), Appliance("stove-a", "heat") },
            recipes: new[]
            {
                Recipe("recipe-b", "raw-b", "product-b", "blend", 2),
                Recipe("recipe-a", "raw-a", "product-a", "heat", 3),
            },
            containers: new[]
            {
                new CookingContainerDefinition(new ContainerId("plate-b"), 2),
                new CookingContainerDefinition(new ContainerId("plate-a"), 1),
            });

        var result = Submit(registry, extended, evidence, "C04-C05",
            "additional recipe and appliance definitions use the same candidate validator without content-specific rules");

        Assert.True(result.Accepted);
        Assert.Equal(2, registry.Current!.Recipes.Count);
        Assert.Equal(2, registry.Current.Appliances.Count);
        Assert.True(registry.Current.Recipes.ContainsKey(new RecipeId("recipe-b")));
        Assert.True(registry.Current.Appliances.ContainsKey(new StationSlotId("blender-a")));
        AssertEvidence(evidence.Path, "C04-C05", 1);
    }

    [Fact]
    public void C02_unknown_capability_and_missing_item_reject_before_any_configuration_is_available()
    {
        var registry = new CookingConfigurationRegistry();
        var result = registry.Submit(Candidate(
            capabilities: new[] { "heat" },
            recipes: new[] { Recipe("recipe-a", "missing", "product-a", "blend", 3) }));

        Assert.False(result.Accepted);
        Assert.Null(registry.Current);
        Assert.Contains(result.Validation.Diagnostics, diagnostic => diagnostic.Code == CookingConfigurationDiagnosticCodes.UnknownCapability &&
            diagnostic.Table == "Recipe" && diagnostic.Relation == "blend");
        Assert.Contains(result.Validation.Diagnostics, diagnostic => diagnostic.Code == CookingConfigurationDiagnosticCodes.MissingReference &&
            diagnostic.Field == "InputDefinition" && diagnostic.Relation == "missing");
    }

    [Fact]
    public void diagnostics_are_independent_of_unordered_capability_insertion_order()
    {
        var first = Candidate(appliances: new[] { Appliance("stove-a", "unknown-b", "unknown-a") });
        var second = Candidate(appliances: new[] { Appliance("stove-a", "unknown-a", "unknown-b") });
        var registry = new CookingConfigurationRegistry();

        var firstDiagnostics = registry.Validate(first).Diagnostics;
        var secondDiagnostics = registry.Validate(second).Diagnostics;

        Assert.Equal(firstDiagnostics, secondDiagnostics);
        Assert.Equal(new[] { "unknown-a", "unknown-b" }, firstDiagnostics
            .Where(diagnostic => diagnostic.Code == CookingConfigurationDiagnosticCodes.UnknownCapability)
            .Select(diagnostic => diagnostic.Relation));
    }

    [Fact]
    public void C07_unapproved_schema_migration_is_blocked_without_conversion()
    {
        using var evidence = CreateEvidence("C07");
        var current = Assert.IsType<CookingConfigurationIdentity>(new CookingConfigurationRegistry().Submit(Candidate()).AfterIdentity);
        var legacy = current with { Schema = "cooking-definition-v0" };

        var result = CookingConfigurationCompatibility.Evaluate(legacy, current);
        CookingConfigurationAcceptanceEvidenceWriter.Append(evidence.Path, new CookingConfigurationAcceptanceEvidence(
            "C07",
            false,
            legacy.ToString(),
            current.ToString(),
            Array.Empty<CookingConfigurationDiagnostic>(),
            "schema mismatch remains blocked until an owner-approved migration policy exists; no conversion is performed",
            "dotnet test AbilityKit.Game.Cooking.Tests",
            DateTimeOffset.UtcNow.ToString("O")));

        Assert.Equal(CookingConfigurationCompatibilityStatus.Blocked, result.Status);
        Assert.Equal(CookingConfigurationCompatibility.MigrationPolicyNotApproved, result.ReasonCode);
        Assert.Equal(legacy, result.PresentedIdentity);
        Assert.Equal(current, result.ExpectedIdentity);

        var differentHash = current with { Sha256 = new string('A', current.Sha256.Length) };
        var mismatch = CookingConfigurationCompatibility.Evaluate(differentHash, current);
        Assert.Equal(CookingConfigurationCompatibilityStatus.Blocked, mismatch.Status);
        Assert.Equal(CookingConfigurationCompatibility.IdentityMismatch, mismatch.ReasonCode);
        AssertEvidence(evidence.Path, "C07", 1);
    }

    [Fact]
    public void C08_load_order_does_not_change_identity_and_covered_definition_change_is_detected()
    {
        var first = new CookingConfigurationRegistry();
        var second = new CookingConfigurationRegistry();
        var changed = new CookingConfigurationRegistry();

        var firstResult = first.Submit(Candidate());
        var reorderedResult = second.Submit(Candidate(
            capabilities: new[] { "blend", "heat" },
            items: new[] { Item("product-b"), Item("raw-b"), Item("product-a"), Item("raw-a") },
            appliances: new[] { Appliance("blender-a", "blend"), Appliance("stove-a", "heat") },
            recipes: new[]
            {
                Recipe("recipe-b", "raw-b", "product-b", "blend", 2),
                Recipe("recipe-a", "raw-a", "product-a", "heat", 3),
            },
            containers: new[]
            {
                new CookingContainerDefinition(new ContainerId("plate-b"), 2),
                new CookingContainerDefinition(new ContainerId("plate-a"), 1),
            }));
        var changedResult = changed.Submit(Candidate(
            items: new[]
            {
                Item("raw-a"),
                new CookingItemDefinition(new DefinitionId("product-a"), new HashSet<string>(StringComparer.Ordinal) { "cook", "server" }),
                Item("raw-b"),
                Item("product-b"),
            }));

        Assert.True(firstResult.Accepted);
        Assert.True(reorderedResult.Accepted);
        Assert.True(changedResult.Accepted);
        Assert.Equal(firstResult.AfterIdentity, reorderedResult.AfterIdentity);
        Assert.NotEqual(firstResult.AfterIdentity, changedResult.AfterIdentity);
        Assert.Equal(first.Current!.CanonicalText(), second.Current!.CanonicalText());
    }

    private static CookingConfigurationCandidate Candidate(
        IReadOnlyList<string>? capabilities = null,
        IReadOnlyList<CookingItemDefinition>? items = null,
        IReadOnlyList<CookingApplianceDefinition>? appliances = null,
        IReadOnlyList<CookingRecipeDefinition>? recipes = null,
        IReadOnlyList<CookingContainerDefinition>? containers = null) => new(
        capabilities ?? new[] { "heat", "blend" },
        items ?? new[] { Item("raw-a"), Item("product-a"), Item("raw-b"), Item("product-b") },
        appliances ?? new[] { Appliance("stove-a", "heat"), Appliance("blender-a", "blend") },
        recipes ?? new[]
        {
            Recipe("recipe-a", "raw-a", "product-a", "heat", 3),
            Recipe("recipe-b", "raw-b", "product-b", "blend", 2),
        },
        containers ?? new[]
        {
            new CookingContainerDefinition(new ContainerId("plate-a"), 1),
            new CookingContainerDefinition(new ContainerId("plate-b"), 2),
        });

    private static CookingItemDefinition Item(string id) =>
        new(new DefinitionId(id), new HashSet<string>(StringComparer.Ordinal) { "cook" });

    private static CookingApplianceDefinition Appliance(string station, params string[] capabilities) =>
        new(new StationSlotId(station), new HashSet<string>(capabilities, StringComparer.Ordinal));

    private static CookingRecipeDefinition Recipe(string id, string input, string product, string capability, int ticks) =>
        new(new RecipeId(id), new DefinitionId(input), new DefinitionId(product), new ProcessId($"{id}-process"), capability, ticks);

    private static CookingConfigurationSubmissionResult Submit(CookingConfigurationRegistry registry,
        CookingConfigurationCandidate candidate, EvidenceScope evidence, string testId, string assertionSummary)
    {
        var result = registry.Submit(candidate);
        CookingConfigurationAcceptanceEvidenceWriter.Append(evidence.Path, new CookingConfigurationAcceptanceEvidence(
            testId,
            result.Accepted,
            result.BeforeIdentity?.ToString(),
            result.AfterIdentity?.ToString(),
            result.Validation.Diagnostics,
            assertionSummary,
            "dotnet test AbilityKit.Game.Cooking.Tests",
            DateTimeOffset.UtcNow.ToString("O")));
        return result;
    }

    private static void AssertEvidence(string path, string testId, int count)
    {
        var records = CookingConfigurationAcceptanceEvidenceWriter.ReadAll(path);
        Assert.Equal(count, records.Count);
        Assert.All(records, record =>
        {
            Assert.Equal(testId, record.TestId);
            Assert.False(string.IsNullOrWhiteSpace(record.Runner));
            Assert.NotNull(record.Diagnostics);
        });
    }

    private sealed class EvidenceScope : IDisposable
    {
        private readonly string _directory;
        private readonly bool _keepArtifacts;

        public EvidenceScope(string testId)
        {
            var requestedRoot = Environment.GetEnvironmentVariable("COOKING_CONFIGURATION_EVIDENCE_DIRECTORY");
            _keepArtifacts = !string.IsNullOrWhiteSpace(requestedRoot);
            var root = _keepArtifacts ? System.IO.Path.GetFullPath(requestedRoot!) :
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AbilityKit.Game.Cooking.Tests", "configuration");
            _directory = System.IO.Path.Combine(root, testId, Guid.NewGuid().ToString("N"));
            Path = System.IO.Path.Combine(_directory, "configuration-validation.jsonl");
        }

        public string Path { get; }

        public void Dispose()
        {
            if (!_keepArtifacts && Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }
    }

    private static EvidenceScope CreateEvidence(string testId) => new(testId);
}
