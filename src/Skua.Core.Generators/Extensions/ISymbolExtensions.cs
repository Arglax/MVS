using Microsoft.CodeAnalysis;
using Skua.Core.Generators.Models;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
namespace Skua.Core.Generators.Extensions;
internal static class ISymbolExtensions
{
public static ClassInfo GetClassInfo(this ISymbol symbol)
{
INamedTypeSymbol classSymbol = symbol.ContainingType;
string? baseClass = classSymbol.BaseType?.ToDisplayString();
if (baseClass is not null and "object")
baseClass = null;
List<string> interfaces = classSymbol.Interfaces.Select(i => i.ToDisplayString()).ToList();
string inheritances = $"{baseClass ?? ""}{(baseClass is not null && interfaces.Count > 0 ? ", " : "")}{(interfaces.Count > 0 ? string.Join(", ", interfaces) : "")}";
return new ClassInfo(
classSymbol.Name,
classSymbol.ContainingNamespace.ToDisplayString(),
inheritances);
}
public static bool HasFullyQualifiedName(this ISymbol symbol, string name)
{
return symbol.ToDisplayString() == name;
}
public static bool HasAttributeWithFullyQualifiedName(this ISymbol symbol, string name)
{
ImmutableArray<AttributeData> attributes = symbol.GetAttributes();
foreach (AttributeData attribute in attributes)
{
if (attribute.AttributeClass?.HasFullyQualifiedName(name) == true)
{
return true;
}
}
return false;
}
public static string GetFullyQualifiedNameWithNullabilityAnnotations(this ISymbol symbol)
{
return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier));
}
}
