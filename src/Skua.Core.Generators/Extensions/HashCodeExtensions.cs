using System;
using System.Collections.Generic;
using System.Collections.Immutable;
namespace Skua.Core.Generators.Extensions;
internal static class HashCodeExtensions
{
public static void AddRange<T>(this ref HashCode hashCode, ImmutableArray<T> items)
{
foreach (T item in items)
{
hashCode.Add(item);
}
}
public static void AddRange<T>(this ref HashCode hashCode, ImmutableArray<T> items, IEqualityComparer<T> comparer)
{
foreach (T item in items)
{
hashCode.Add(item, comparer);
}
}
}
