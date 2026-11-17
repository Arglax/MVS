using Skua.Core.Models.Items;
using Skua.Core.Utils;
namespace Skua.Core.Interfaces;
public interface ICheckInventory<T> : IItemContainer<T> where T : ItemBase
{
bool Contains(string name, int quantity = 1)
{
return quantity == 0 || Items.Contains(i => i.Name == name && (i.Quantity >= quantity || i.Category == ItemCategory.Class));
}
bool Contains(int id, int quantity = 1)
{
return quantity == 0 || Items.Contains(i => i.ID == id && (i.Quantity >= quantity || i.Category == ItemCategory.Class));
}
bool Contains(bool needAll, int quantity, params string[] names)
{
foreach (string t in names)
{
bool hasItem = Contains(t, quantity);
switch (hasItem)
{
case true when !needAll:
return true;
case true when needAll:
continue;
case false when needAll:
return false;
}
}
return needAll;
}
bool Contains(bool needAll, int quantity, params int[] ids)
{
foreach (int t in ids)
{
bool hasItem = Contains(t, quantity);
switch (hasItem)
{
case true when !needAll:
return true;
case true when needAll:
continue;
case false when needAll:
return false;
}
}
return needAll;
}
bool TryGetItem(string name, out T? item)
{
return (item = GetItem(name)) is not null;
}
bool TryGetItem(int id, out T? item)
{
return (item = GetItem(id)) is not null;
}
T? GetItem(string name)
{
return Items?.Find(x => x.Name == name);
}
T? GetItem(int id)
{
return Items?.Find(x => x.ID == id);
}
int GetQuantity(string name)
{
return TryGetItem(name, out T? item) ? item!.Quantity : 0;
}
int GetQuantity(int id)
{
return TryGetItem(id, out T? item) ? item!.Quantity : 0;
}
bool IsMaxStack(string name)
{
return TryGetItem(name, out T? item) && item!.Quantity >= item!.MaxStack;
}
bool IsMaxStack(int id)
{
return TryGetItem(id, out T? item) && item!.Quantity >= item!.MaxStack;
}
}
