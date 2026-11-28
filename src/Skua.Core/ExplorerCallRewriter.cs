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
public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
{
node = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node)!;
if (node.Name.Identifier.ValueText != "MachineName")
return node;
string type = node.Expression.ToString().Replace(" ", string.Empty);
if (type is not ("Environment" or "System.Environment" or "global::System.Environment"))
return node;
_changed = true;
return SyntaxFactory.ParseExpression("global::ScriptShell.MachineName").WithTriviaFrom(node);
}
public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
{
node = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
if (IsButlerLogPath(node))
{
_changed = true;
return node.WithExpression(SyntaxFactory.ParseExpression("global::ScriptShell.ButlerFile").WithTriviaFrom(node.Expression));
}
if (IsWholeFileWrite(node, out string method))
{
_changed = true;
return node.WithExpression(SyntaxFactory.ParseExpression("global::ScriptShell." + method).WithTriviaFrom(node.Expression));
}
if (node.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Start" } access
|| node.ArgumentList.Arguments.Count == 0
|| !IsProcessType(access.Expression))
return node;
_changed = true;
return node.WithExpression(SyntaxFactory.ParseExpression("global::ScriptShell.Start").WithTriviaFrom(node.Expression));
}
private static bool IsButlerLogPath(InvocationExpressionSyntax node)
{
if (node.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Combine" } access
|| node.ArgumentList.Arguments.Count < 2)
return false;
string type = access.Expression.ToString().Replace(" ", string.Empty);
if (type is not ("Path" or "IO.Path" or "System.IO.Path" or "global::System.IO.Path"))
return false;
string first = node.ArgumentList.Arguments[0].Expression.ToString().Replace(" ", string.Empty);
return first is "ButlerLogDir" or "CoreBots.ButlerLogDir";
}
private static bool IsWholeFileWrite(InvocationExpressionSyntax node, out string method)
{
method = string.Empty;
if (node.Expression is not MemberAccessExpressionSyntax access
|| access.Name.Identifier.ValueText is not ("WriteAllText" or "WriteAllLines" or "WriteAllBytes")
|| node.ArgumentList.Arguments.Count is < 2 or > 3)
return false;
string type = access.Expression.ToString().Replace(" ", string.Empty);
if (type is not ("File" or "IO.File" or "System.IO.File" or "global::System.IO.File"))
return false;
method = access.Name.Identifier.ValueText;
return true;
}
private static bool IsProcessType(ExpressionSyntax expression)
{
string text = expression.ToString().Replace(" ", string.Empty);
return text is "Process" or "Diagnostics.Process" or "System.Diagnostics.Process" or "global::System.Diagnostics.Process";
}
}
