using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
namespace Skua.Core.Generators.Models;
internal sealed record Result<TValue>(TValue Value, ImmutableArray<Diagnostic> Errors);
