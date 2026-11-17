using Skua.Core.Models.Items;
using Skua.Core.Utils;
using System.ComponentModel;
namespace Skua.Core.Interfaces;
public interface IScriptDrop : INotifyPropertyChanged
{
IEnumerable<ItemBase> CurrentDropInfos { get; }
IEnumerable<string> CurrentDrops { get; }
bool Enabled { get; }
int Interval { get; set; }
bool RejectElse { get; set; }
IEnumerable<string> ToPickup { get; }
IEnumerable<int> ToPickupIDs { get; }
void Add(params string[] names);
void Add(params int[] ids);
void Remove(params string[] names);
void Remove(params int[] ids);
void Start();
void Stop();
ValueTask StopAsync();
void Clear();
bool Exists(string name)
{
return (name == "*" && CurrentDrops.Any()) || CurrentDrops.Contains(name, StringComparer.OrdinalIgnoreCase);
}
bool Exists(int id)
{
return CurrentDropInfos.Contains(x => x.ID == id);
}
void Pickup(string name);
void Pickup(int id);
void Pickup(params string[] names);
void Pickup(params int[] ids);
void PickupACItems()
{
Pickup(CurrentDropInfos.Where(d => d.Coins).Select(d => d.Name).ToArray());
}
void PickupAll(bool skipWait = false);
void PickupFast(params string[] names)
{
foreach (string t in names)
Pickup(t);
}
void PickupFast(params int[] ids)
{
foreach (int t in ids)
Pickup(t);
}
void RejectAll(bool skipWait = false);
void RejectExcept(params string[] names);
void RejectExcept(params int[] ids);
}
