namespace Skua.Core.Interfaces;
public interface IScriptInventoryHelper
{
bool Check(string name, int quantity = 1, bool moveToInventory = true);
bool Check(int id, int quantity = 1, bool moveToInventory = true);
bool HasAll(IEnumerable<string> itemNames, int quantity = 1, bool moveToInventory = true);
bool HasAny(IEnumerable<string> itemNames, int quantity = 1, bool moveToInventory = true);
bool HasAll(IEnumerable<int> itemIds, int quantity = 1, bool moveToInventory = true);
bool HasAny(IEnumerable<int> itemIds, int quantity = 1, bool moveToInventory = true);
}
