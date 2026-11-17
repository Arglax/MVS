using Microsoft.CodeAnalysis;
namespace Skua.Core.Generators.Extensions;
internal static class ITypeSymbolExtensions
{
public static bool HasOrInheritsAttributeWithFullyQualifiedName(this ITypeSymbol typeSymbol, string name)
{
for (ITypeSymbol? currentType = typeSymbol; currentType is not null; currentType = currentType.BaseType)
{
if (currentType.HasAttributeWithFullyQualifiedName(name))
{
return true;
}
}
return false;
}
}
