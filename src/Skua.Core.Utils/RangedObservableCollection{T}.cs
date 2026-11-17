using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
namespace Skua.Core.Utils;
public class RangedObservableCollection<T> : ObservableCollection<T>
{
private const string CountName = "Count";
private const string IndexerName = "Item[]";
private readonly object _lock = new();
public RangedObservableCollection()
: base() { }
public RangedObservableCollection(IEnumerable<T> collection)
: base(collection) { }
public void AddRange(IEnumerable<T> collection)
{
if (collection?.Any() != true)
return;
CheckReentrancy();
lock (_lock)
{
foreach (T i in collection)
Items.Add(i);
}
OnCountPropertyChanged();
OnIndexerPropertyChanged();
OnCollectionReset();
}
public void ReplaceRange(IEnumerable<T> collection)
{
if (collection == null)
return;
CheckReentrancy();
lock (_lock)
{
Items.Clear();
foreach (T i in collection)
Items.Add(i);
}
OnCountPropertyChanged();
OnIndexerPropertyChanged();
OnCollectionReset();
}
public void RemoveRange(IEnumerable<T> collection)
{
if (collection?.Any() != true)
return;
CheckReentrancy();
List<T> changedItems = new(collection);
lock (_lock)
{
for (int i = 0; i < changedItems.Count; i++)
{
if (!Items.Remove(changedItems[i]))
{
changedItems.RemoveAt(i);
i--;
}
}
}
if (changedItems.Count > 0)
{
OnCountPropertyChanged();
OnIndexerPropertyChanged();
OnCollectionReset();
}
}
public void Replace(T item)
{
if (item is null)
return;
CheckReentrancy();
lock (_lock)
{
Items.Clear();
Items.Add(item);
}
OnCountPropertyChanged();
OnIndexerPropertyChanged();
OnCollectionReset();
}
private void OnCountPropertyChanged()
{
OnPropertyChanged(EventArgsCache.CountPropertyChanged);
}
private void OnIndexerPropertyChanged()
{
OnPropertyChanged(EventArgsCache.IndexerPropertyChanged);
}
private void OnCollectionReset()
{
OnCollectionChanged(EventArgsCache.ResetCollectionChanged);
}
private static class EventArgsCache
{
internal static readonly PropertyChangedEventArgs CountPropertyChanged = new(CountName);
internal static readonly PropertyChangedEventArgs IndexerPropertyChanged = new(IndexerName);
internal static readonly NotifyCollectionChangedEventArgs ResetCollectionChanged = new(NotifyCollectionChangedAction.Reset);
}
}
