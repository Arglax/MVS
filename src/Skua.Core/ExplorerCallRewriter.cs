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
|| !access.Expression.ToString().EndsWith("Process", StringComparison.Ordinal)
|| node.ArgumentList.Arguments.Count != 2
|| node.ArgumentList.Arguments[0].Expression is not LiteralExpressionSyntax literal
|| !literal.IsKind(SyntaxKind.StringLiteralExpression))
return node;
string file = literal.Token.ValueText;
if (!file.Equals("explorer", StringComparison.OrdinalIgnoreCase)
&& !file.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase))
return node;
_changed = true;
InvocationExpressionSyntax replacement = SyntaxFactory.InvocationExpression(
SyntaxFactory.ParseExpression("global::ScriptShell.Open"),
SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(node.ArgumentList.Arguments[1])))
.WithTriviaFrom(node);
return replacement;
}
}
