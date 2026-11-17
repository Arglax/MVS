namespace Skua.Core.Utils;
public delegate void Consumer<T>(T arg);
public static class EnumerableExtensions
{
public static void ForEach<T>(this IEnumerable<T> enumerable, Consumer<T> func)
{
foreach (T item in enumerable)
func(item);
}
public static bool Contains<T>(this IEnumerable<T> enumerable, Predicate<T> pred)
{
if (enumerable is null || !enumerable.Any())
return false;
foreach (T item in enumerable)
if (item is not null && pred(item))
return true;
return false;
}
public static IList<T> Swap<T>(this IList<T> list, int indexA, int indexB)
{
(list[indexB], list[indexA]) = (list[indexA], list[indexB]);
return list;
}
public static IEnumerable<Tuple<T, T>> PairUp<T>(this IEnumerable<T> list)
{
using IEnumerator<T> iterator = list.GetEnumerator();
while (iterator.MoveNext())
{
T? first = iterator.Current;
T? second = iterator.MoveNext() ? iterator.Current : default;
yield return Tuple.Create(first, second);
}
}
}
