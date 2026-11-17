using Skua.Core.Models.Shops;
namespace Skua.Core.Interfaces;
public interface IScriptShop
{
bool IsLoaded { get; }
int ID { get; }
List<ShopItem> Items { get; }
List<ShopInfo> LoadedCache { get; set; }
string Name { get; }
bool ProtectFavorites { get; set; }
List<string> GetUnmetPurchaseRequirements(ShopItem item, int quantity = -1);
void BuyItem(string name, int quantity = -1);
void BuyItem(int id, int shopItemId = -1, int quantity = -1);
void LoadAndBuyItem(int shopId, string name, int quantity = -1)
{
Load(shopId);
BuyItem(name, quantity);
}
void LoadAndBuyItem(int shopId, int itemId, int shopItemId = -1, int quantity = -1)
{
Load(shopId);
BuyItem(itemId, shopItemId, quantity);
}
void EnsureLoadAndBuyItem(int shopId, string itemName, int quantity = -1);
void EnsureLoadAndBuyItem(int shopId, int itemId, int shopItemId = -1, int quantity = -1);
void Load(int id);
void LoadArmourCustomizer();
void LoadHairShop(int id);
void SellItem(string name, int quantity = -1);
void SellItem(int id, int quantity = -1);
}
