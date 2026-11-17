using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
namespace Skua.Core.Generators.Extensions;
internal static class IEqualityComparerExtensions
{
public static IEqualityComparer<ImmutableArray<T>> ForImmutableArray<T>(this IEqualityComparer<T> comparer)
{
return new Comparer<T>(comparer);
}
private sealed class Comparer<T> : IEqualityComparer<ImmutableArray<T>>
{
private readonly IEqualityComparer<T> comparer;
public Comparer(IEqualityComparer<T> comparer)
{
this.comparer = comparer;
}
public bool Equals(ImmutableArray<T> x, ImmutableArray<T> y)
{
return
x == y ||
x.SequenceEqual(y, comparer);
}
public int GetHashCode(ImmutableArray<T> obj)
{
HashCode hashCode = default;
hashCode.AddRange(obj, comparer);
return hashCode.ToHashCode();
}
}
}
