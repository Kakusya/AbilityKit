using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AbilityKit.ET.RelationAnalyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EtRelationAnalyzer : DiagnosticAnalyzer
{
    public const string ComponentDiagnosticId = "AKET001";
    public const string ChildDiagnosticId = "AKET002";

    private static readonly DiagnosticDescriptor ComponentRule = new(
        ComponentDiagnosticId,
        "ET component parent mismatch",
        "ET component '{0}' declares parent '{1}' but is accessed from '{2}'",
        "AbilityKit.ET.Relations",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ChildRule = new(
        ChildDiagnosticId,
        "ET child parent mismatch",
        "ET child '{0}' declares parent '{1}' but is created under '{2}'",
        "AbilityKit.ET.Relations",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(ComponentRule, ChildRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
            context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method ||
            !method.IsGenericMethod)
        {
            return;
        }

        var relation = RelationFor(method.Name);
        if (relation is null || method.OriginalDefinition.ContainingType.ToDisplayString() != "ET.Entity")
            return;

        if (context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type is not INamedTypeSymbol actualParent ||
            method.TypeArguments.Length == 0 || method.TypeArguments[0] is not INamedTypeSymbol entityType)
        {
            return;
        }

        var relationAttribute = entityType.GetAttributes().FirstOrDefault(attribute =>
            attribute.AttributeClass?.ToDisplayString() == relation.AttributeMetadataName);
        var declaredParent = relationAttribute?.ConstructorArguments.Length == 1
            ? relationAttribute.ConstructorArguments[0].Value as INamedTypeSymbol
            : null;
        if (declaredParent is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                relation.Rule,
                memberAccess.Name.GetLocation(),
                entityType.Name,
                $"missing {relation.AttributeMetadataName}",
                actualParent.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            return;
        }

        if (SymbolEqualityComparer.Default.Equals(actualParent, declaredParent))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            relation.Rule,
            memberAccess.Name.GetLocation(),
            entityType.Name,
            declaredParent.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            actualParent.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    private static Relation? RelationFor(string methodName) => methodName switch
    {
        "AddComponent" or "GetComponent" => new Relation("ET.ComponentOfAttribute", ComponentRule),
        "AddChild" or "AddChildWithId" => new Relation("ET.ChildOfAttribute", ChildRule),
        _ => null,
    };

    private sealed class Relation
    {
        public Relation(string attributeMetadataName, DiagnosticDescriptor rule)
        {
            AttributeMetadataName = attributeMetadataName;
            Rule = rule;
        }

        public string AttributeMetadataName { get; }
        public DiagnosticDescriptor Rule { get; }
    }
}
