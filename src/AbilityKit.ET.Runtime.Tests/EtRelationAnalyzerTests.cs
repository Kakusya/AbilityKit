using System.Collections.Immutable;
using AbilityKit.ET.RelationAnalyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

public sealed class EtRelationAnalyzerTests
{
    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public async Task Correct_relations_have_no_diagnostics_under_the_real_Cooking_assembly_name()
    {
        var diagnostics = await AnalyzeAsync("""
            using ET;

            [ComponentOf(typeof(Scene))]
            public sealed class AppComponent : Entity, IAwake { }

            [ComponentOf(typeof(AppComponent))]
            public sealed class RegistryComponent : Entity, IAwake { }

            [ChildOf(typeof(RegistryComponent))]
            public sealed class MatchEntity : Entity, IAwake { }

            public static class Bootstrap
            {
                public static void Install(Scene scene)
                {
                    var app = scene.AddComponent<AppComponent>();
                    var registry = app.AddComponent<RegistryComponent>();
                    registry.AddChild<MatchEntity>();
                    _ = scene.GetComponent<AppComponent>();
                }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public async Task Wrong_component_parent_reports_AKET001()
    {
        var diagnostic = Assert.Single(await AnalyzeAsync("""
            using ET;

            [ComponentOf(typeof(Scene))]
            public sealed class AppComponent : Entity, IAwake { }

            public static class Bootstrap
            {
                public static void Install(Entity wrongParent)
                {
                    wrongParent.AddComponent<AppComponent>();
                }
            }
            """));

        Assert.Equal(EtRelationAnalyzer.ComponentDiagnosticId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public async Task Wrong_child_parent_reports_AKET002()
    {
        var diagnostic = Assert.Single(await AnalyzeAsync("""
            using ET;

            [ComponentOf(typeof(Scene))]
            public sealed class RegistryComponent : Entity, IAwake { }

            [ChildOf(typeof(RegistryComponent))]
            public sealed class MatchEntity : Entity, IAwake { }

            public static class Bootstrap
            {
                public static void Install(Scene wrongParent)
                {
                    wrongParent.AddChild<MatchEntity>();
                }
            }
            """));

        Assert.Equal(EtRelationAnalyzer.ChildDiagnosticId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    [Trait("Gate", "CookingLevelRuntime")]
    public async Task Missing_relation_attribute_reports_the_matching_diagnostic()
    {
        var diagnostics = await AnalyzeAsync("""
            using ET;

            public sealed class MissingComponent : Entity, IAwake { }
            public sealed class MissingChild : Entity, IAwake { }

            public static class Bootstrap
            {
                public static void Install(Scene scene)
                {
                    scene.AddComponent<MissingComponent>();
                    scene.AddChild<MissingChild>();
                }
            }
            """);

        Assert.Equal(
            new[] { EtRelationAnalyzer.ComponentDiagnosticId, EtRelationAnalyzer.ChildDiagnosticId },
            diagnostics.Select(diagnostic => diagnostic.Id).OrderBy(id => id, StringComparer.Ordinal));
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));
        var references = TrustedPlatformReferences()
            .Append(MetadataReference.CreateFromFile(typeof(global::ET.Entity).Assembly.Location));
        var compilation = CSharpCompilation.Create(
            "AbilityKit.Game.Cooking.EtRuntime",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compilationErrors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(compilationErrors);

        return await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new EtRelationAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
    }

    private static IEnumerable<MetadataReference> TrustedPlatformReferences()
    {
        var paths = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            ?? throw new InvalidOperationException("Trusted platform assemblies are unavailable.");
        return paths.Select(path => MetadataReference.CreateFromFile(path));
    }
}
