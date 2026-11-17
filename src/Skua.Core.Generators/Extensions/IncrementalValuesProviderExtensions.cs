using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
namespace Skua.Core.Generators.Extensions;
internal static class IncrementalValuesProviderExtensions
{
public static IncrementalValuesProvider<(TLeft Left, ImmutableArray<TRight> Right)> GroupBy<TLeft, TRight>(
this IncrementalValuesProvider<(TLeft Left, TRight Right)> source,
IEqualityComparer<TLeft> comparer)
{
return source.Collect().SelectMany((item, _) =>
{
Dictionary<TLeft, ImmutableArray<TRight>.Builder> map = new(comparer);
foreach ((TLeft hierarchy, TRight info) in item)
{
if (!map.TryGetValue(hierarchy, out ImmutableArray<TRight>.Builder builder))
{
builder = ImmutableArray.CreateBuilder<TRight>();
map.Add(hierarchy, builder);
}
builder.Add(info);
}
ImmutableArray<(TLeft Hierarchy, ImmutableArray<TRight> Properties)>.Builder result =
ImmutableArray.CreateBuilder<(TLeft, ImmutableArray<TRight>)>();
foreach (KeyValuePair<TLeft, ImmutableArray<TRight>.Builder> entry in map)
{
result.Add((entry.Key, entry.Value.ToImmutable()));
}
return result;
});
}
public static IncrementalValuesProvider<(TLeft Left, TRight Right)> WithComparers<TLeft, TRight>(
this IncrementalValuesProvider<(TLeft Left, TRight Right)> source,
IEqualityComparer<TLeft> comparerLeft,
IEqualityComparer<TRight> comparerRight)
{
return source.WithComparer(new Comparer<TLeft, TRight>(comparerLeft, comparerRight));
}
private sealed class Comparer<TLeft, TRight> : IEqualityComparer<(TLeft Left, TRight Right)>
{
private readonly IEqualityComparer<TLeft> comparerLeft;
private readonly IEqualityComparer<TRight> comparerRight;
public Comparer(IEqualityComparer<TLeft> comparerLeft, IEqualityComparer<TRight> comparerRight)
{
this.comparerLeft = comparerLeft;
this.comparerRight = comparerRight;
}
public bool Equals((TLeft Left, TRight Right) x, (TLeft Left, TRight Right) y)
{
return
comparerLeft.Equals(x.Left, y.Left) &&
comparerRight.Equals(x.Right, y.Right);
}
public int GetHashCode((TLeft Left, TRight Right) obj)
{
return HashCode.Combine(
comparerLeft.GetHashCode(obj.Left),
comparerRight.GetHashCode(obj.Right));
}
}
}
