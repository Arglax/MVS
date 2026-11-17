using Newtonsoft.Json;
using Skua.Core.Models.Items;
namespace Skua.Core.Models.Shops;
public class ShopItem : ItemBase
{
[JsonProperty("ShopItemID")]
public int ShopItemID { get; set; }
[JsonProperty("iCost")]
public int Cost { get; set; }
[JsonProperty("iLvl")]
public int Level { get; set; }
[JsonProperty("sFaction")]
public string Faction { get; set; } = string.Empty;
[JsonProperty("iReqRep")]
public int RequiredReputation { get; set; }
[JsonProperty("turnin")]
public List<ItemBase> Requirements { get; set; } = new();
[JsonProperty("TokID")]
public int TokenID { get; set; }
[JsonProperty("TokQty")]
public double TokenQuantity { get; set; }
[JsonProperty("TokName")]
public string TokenName { get; set; } = string.Empty;
public override bool Equals(object? obj)
{
return obj is ShopItem item && item.ID == ID && item.ShopItemID == ShopItemID;
}
public override int GetHashCode()
{
return HashCode.Combine(ID, ShopItemID);
}
}
