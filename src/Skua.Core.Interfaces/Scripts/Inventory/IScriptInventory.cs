using Skua.Core.Models.Items;
namespace Skua.Core.Interfaces;
public interface IScriptInventory : ICanEquip
{
bool HasCategories { get; }
int MiscSlots { get; }
int MiscUsedSlots { get; }
int MiscFreeSlots { get; }
string GetPool(ItemBase item);
bool HasSpaceFor(ItemBase item, int quantity = 1);
bool CanBank(InventoryItem item);
bool IsFavorited(int itemId);
bool IsFavorited(InventoryItem item) => IsFavorited(item.ID);
IEnumerable<InventoryItem> FavoritedItems => Items.Where(item => IsFavorited(item.ID)).ToArray();
IEnumerable<InventoryItem> UnfavoritedItems => Items.Where(item => !IsFavorited(item.ID)).ToArray();
int BankAllItems(bool skipFavorited = true, bool skipEquipped = true)
{
int banked = 0;
InventoryItem[] items = Items.ToArray();
foreach (InventoryItem item in items)
{
if (!CanBank(item) || (skipEquipped && item.Equipped) || (skipFavorited && IsFavorited(item.ID)))
continue;
if (ToBank(item))
banked++;
}
return banked;
}
}
