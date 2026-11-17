using Skua.Core.Models.Items;
namespace Skua.Core.Interfaces;
public interface IScriptBank : ICheckInventory<InventoryItem>, ILimitedInventory
{
bool Loaded { get; set; }
void Open();
void Load(bool waitForLoad = true);
bool Swap(string invItem, string bankItem);
bool Swap(int invItem, int bankItem);
bool Swap(InventoryItem invItem, InventoryItem bankItem);
bool EnsureSwap(string invItem, string bankItem, bool loadBank = true);
bool EnsureSwap(int invItem, int bankItem, bool loadBank = true);
bool ToInventory(string name)
{
if (TryGetItem(name, out InventoryItem? item))
ToInventory(item!);
return false;
}
bool ToInventory(int id)
{
if (TryGetItem(id, out InventoryItem? item))
ToInventory(item!);
return false;
}
bool ToInventory(InventoryItem item);
void ToInventory(params string[] names)
{
foreach (string t in names)
ToInventory(t);
}
void ToInventory(params int[] ids)
{
foreach (int t in ids)
ToInventory(t);
}
bool EnsureToInventory(string name, bool loadBank = true);
bool EnsureToInventory(int id, bool loadBank = true);
void EnsureToInventory(bool loadBank, params string[] names)
{
if (loadBank)
Load();
foreach (string t in names)
EnsureToInventory(t, false);
}
void EnsureToInventory(bool loadBank, params int[] ids)
{
if (loadBank)
Load();
foreach (int t in ids)
EnsureToInventory(t, false);
}
}
