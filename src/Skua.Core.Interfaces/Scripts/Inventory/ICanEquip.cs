using Skua.Core.Models.Items;
namespace Skua.Core.Interfaces;
public interface ICanEquip : ICheckEquipped
{
void EquipItem(int id);
void EquipItem(string name)
{
if (TryGetItem(name, out InventoryItem? item))
EquipItem(item!.ID);
}
void EquipUsableItem(int id)
{
if (TryGetItem(id, out InventoryItem? item))
EquipUsableItem(item);
}
void EquipUsableItem(string name)
{
if (TryGetItem(name, out InventoryItem? item))
EquipUsableItem(item);
}
void EquipUsableItem(InventoryItem? item);
void EquipItems(params string[] names)
{
foreach (string t in names)
{
if (TryGetItem(t, out InventoryItem? item))
EquipItem(item!.ID);
}
}
void EquipItems(params int[] ids)
{
foreach (int t in ids)
{
EquipItem(t);
}
}
}
