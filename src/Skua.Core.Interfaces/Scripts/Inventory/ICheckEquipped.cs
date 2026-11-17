using Skua.Core.Models.Items;
namespace Skua.Core.Interfaces;
public interface ICheckEquipped : ICanBank
{
bool IsEquipped(string name)
{
return TryGetItem(name, out InventoryItem? item) && item!.Equipped;
}
bool IsEquipped(int id)
{
return TryGetItem(id, out InventoryItem? item) && item!.Equipped;
}
}
