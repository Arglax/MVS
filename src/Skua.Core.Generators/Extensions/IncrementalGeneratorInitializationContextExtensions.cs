using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
namespace Skua.Core.Generators.Extensions;
internal static class IncrementalGeneratorInitializationContextExtensions
{
public static void ReportDiagnostics(this IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<ImmutableArray<Diagnostic>> diagnostics)
{
context.RegisterSourceOutput(diagnostics, static (context, diagnostics) =>
{
foreach (Diagnostic diagnostic in diagnostics)
{
context.ReportDiagnostic(diagnostic);
}
});
}
public static IncrementalValuesProvider<IFieldSymbol> GetFieldsWithAttributes(this IncrementalGeneratorInitializationContext context)
{
return context.SyntaxProvider
.CreateSyntaxProvider(
static (node, _) => node is FieldDeclarationSyntax field && field.AttributeLists.Count > 0,
static (context, _) => ((FieldDeclarationSyntax)context.Node).Declaration.Variables.Select(v => (IFieldSymbol)context.SemanticModel.GetDeclaredSymbol(v)!))
.SelectMany(static (item, _) => item);
}
}
