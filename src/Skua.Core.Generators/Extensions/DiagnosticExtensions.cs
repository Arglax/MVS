using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Linq;
namespace Skua.Core.Generators.Extensions;
internal static class DiagnosticExtensions
{
public static void Add(
this ImmutableArray<Diagnostic>.Builder diagnostics,
DiagnosticDescriptor descriptor,
ISymbol symbol,
params object[] args)
{
diagnostics.Add(descriptor.CreateDiagnostic(symbol, args));
}
public static Diagnostic CreateDiagnostic(this DiagnosticDescriptor descriptor, ISymbol symbol, params object[] args)
{
return Diagnostic.Create(descriptor, symbol.Locations.FirstOrDefault(), args);
}
}
