using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace Skua.Core;
internal sealed class ExplorerCallRewriter : CSharpSyntaxRewriter
{
public static SyntaxTree Rewrite(SyntaxTree tree, CSharpParseOptions options)
{
SyntaxNode root = tree.GetRoot();
ExplorerCallRewriter rewriter = new();
SyntaxNode newRoot = rewriter.Visit(root);
return rewriter._changed ? CSharpSyntaxTree.Create((CSharpSyntaxNode)newRoot, options, tree.FilePath) : tree;
}
private bool _changed;
public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
{
node = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
if (node.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Start" } access
|| node.ArgumentList.Arguments.Count == 0
|| !IsProcessType(access.Expression))
return node;
_changed = true;
return node.WithExpression(SyntaxFactory.ParseExpression("global::ScriptShell.Start").WithTriviaFrom(node.Expression));
}
private static bool IsProcessType(ExpressionSyntax expression)
{
string text = expression.ToString().Replace(" ", string.Empty);
return text is "Process" or "Diagnostics.Process" or "System.Diagnostics.Process" or "global::System.Diagnostics.Process";
}
}
