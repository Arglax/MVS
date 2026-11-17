using Skua.Core.Models.Items;
using Skua.Core.Utils;
namespace Skua.Core.Interfaces;
public interface ICanBank : ICheckInventory<InventoryItem>, ILimitedInventory
{
bool ToBank(string name)
{
return TryGetItem(name, out InventoryItem? item) && ToBank(item!);
}
bool ToBank(int id)
{
return TryGetItem(id, out InventoryItem? item) && ToBank(item!);
}
bool ToBank(InventoryItem item);
void ToBank(params string[] names)
{
foreach (string t in names)
ToBank(t);
}
void ToBank(params int[] ids)
{
foreach (int t in ids)
ToBank(t);
}
bool EnsureToBank(string name);
bool EnsureToBank(int id);
void EnsureToBank(params string[] names)
{
foreach (string t in names)
EnsureToBank(t);
}
void EnsureToBank(params int[] ids)
{
{
foreach (int t in ids)
EnsureToBank(t);
}
}
void BankAllCoinItems()
{
Items.Where(i => i is { Coins: true, Equipped: false } && i.Name != "treasure potion").ForEach(i => ToBank(i));
}
void BankAllCoinItems(ItemCategory[] filterOut)
{
Items.Where(i => i is { Coins: true, Equipped: false } && i.Name != "treasure potion" && !filterOut.Contains(i.Category)).ForEach(i => ToBank(i));
}
void BankAllCoinItems(params string[] excludeNames)
{
Items.Where(i => i is { Coins: true, Equipped: false } && i.Name != "treasure potion" && !excludeNames.Contains(i.Name)).ForEach(i => ToBank(i));
}
void BankAllCoinItems(params int[] excludeIds)
{
Items.Where(i => i is { Coins: true, Equipped: false } && i.Name != "treasure potion" && !excludeIds.Contains(i.ID)).ForEach(i => ToBank(i));
}
void BankAllCoinItems(Predicate<InventoryItem> predicate)
{
Items.Where(i => i is { Coins: true, Equipped: false } && i.Name != "treasure potion" && predicate(i)).ForEach(i => ToBank(i));
}
}
