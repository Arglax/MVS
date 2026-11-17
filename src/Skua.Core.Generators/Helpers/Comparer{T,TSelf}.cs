using System;
using System.Collections.Generic;
namespace Skua.Core.Generators.Helpers;
public abstract class Comparer<T, TSelf> : IEqualityComparer<T>
where TSelf : Comparer<T, TSelf>, new()
{
public static TSelf Default { get; } = new();
public bool Equals(T? x, T? y)
{
return (x is null && y is null) || (x is not null && y is not null && (ReferenceEquals(x, y) || AreEqual(x, y)));
}
public int GetHashCode(T obj)
{
HashCode hashCode = default;
AddToHashCode(ref hashCode, obj);
return hashCode.ToHashCode();
}
public abstract void AddToHashCode(ref HashCode hashCode, T obj);
public abstract bool AreEqual(T x, T y);
}
