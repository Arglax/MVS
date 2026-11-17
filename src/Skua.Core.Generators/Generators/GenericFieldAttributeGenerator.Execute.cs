using Microsoft.CodeAnalysis;
using Skua.Core.Generators.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
namespace Skua.Core.Generators;
public partial class GenericFieldAttributeGenerator<TInfo>
{
public static class Execute
{
public static string GetGeneratedPropertyName(IFieldSymbol fieldSymbol)
{
string propertyName = fieldSymbol.Name;
if (propertyName.StartsWith("m_"))
{
propertyName = propertyName.Substring(2);
}
else if (propertyName.StartsWith("_"))
{
propertyName = propertyName.TrimStart('_');
}
return $"{char.ToUpper(propertyName[0], CultureInfo.InvariantCulture)}{propertyName.Substring(1)}";
}
public static bool HasNotifyPropertyChanged(ITypeSymbol typeSymbol)
{
INamedTypeSymbol? baseType = typeSymbol.BaseType;
while (baseType != null)
{
if (baseType.HasFullyQualifiedName("CommunityToolkit.Mvvm.ComponentModel.ObservableObject")
|| baseType.HasFullyQualifiedName("CommunityToolkit.Mvvm.ComponentModel.ObservableRecipient")
|| baseType.HasFullyQualifiedName("CommunityToolkit.Mvvm.ComponentModel.ObservableValidator"))
{
return true;
}
baseType = baseType.BaseType;
}
return false;
}
public static bool TryGatherBindingName(IFieldSymbol fieldSymbol, AttributeData attributeData, string attributeFullName, ImmutableArray<Diagnostic>.Builder diagnostics, DiagnosticDescriptor descriptor, out string bindingName)
{
if (attributeData.AttributeClass?.HasFullyQualifiedName(attributeFullName) == true)
{
foreach (string? name in attributeData.GetConstructorArguments<string>())
{
if (!string.IsNullOrWhiteSpace(name))
{
bindingName = name!.Replace("\"", "\\\"");
return true;
}
else
{
diagnostics.Add(
descriptor,
fieldSymbol,
fieldSymbol.ContainingType,
fieldSymbol.Name);
break;
}
}
}
bindingName = string.Empty;
return false;
}
public static bool TryGatherBindingNames(IFieldSymbol fieldSymbol, AttributeData attributeData, string attributeFullName, ImmutableArray<Diagnostic>.Builder diagnostics, DiagnosticDescriptor descriptor, out string[] bindingNames)
{
List<string> names = new();
if (attributeData.AttributeClass?.HasFullyQualifiedName(attributeFullName) == true)
{
foreach (string? name in attributeData.GetConstructorArguments<string>())
{
if (!string.IsNullOrWhiteSpace(name))
{
names.Add(name!.Replace("\"", "\\\""));
continue;
}
else
{
diagnostics.Add(
descriptor,
fieldSymbol,
fieldSymbol.ContainingType,
fieldSymbol.Name);
bindingNames = Array.Empty<string>();
return false;
}
}
bindingNames = names.ToArray();
return true;
}
bindingNames = Array.Empty<string>();
return false;
}
}
}
