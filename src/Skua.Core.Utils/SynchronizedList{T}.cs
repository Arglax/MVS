namespace Skua.Core.Utils;
public class SynchronizedList<T>
{
private readonly List<T> list = new();
private readonly object sync = new();
public IEnumerable<T> Items
{
get
{
lock (sync)
return list.ToArray();
}
}
public int Count
{
get
{
lock (sync)
return list.Count;
}
}
public void Add(T value)
{
lock (sync)
list.Add(value);
}
public void AddRange(IEnumerable<T> values)
{
lock (sync)
list.AddRange(values);
}
public bool Remove(T item)
{
lock (sync)
return list.Remove(item);
}
public int Remove(Predicate<T> predicate)
{
lock (sync)
return list.RemoveAll(predicate);
}
public T? Find(Predicate<T> predicate)
{
lock (sync)
return list.Find(predicate);
}
public bool Any()
{
lock (sync)
return list.Any();
}
public bool Any(Func<T, bool> predicate)
{
lock (sync)
return list.Any(predicate);
}
public void Clear()
{
lock (sync)
list.Clear();
}
public void ForEach(Action<T> action)
{
lock (sync)
list.ForEach(action);
}
}
