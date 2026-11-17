using Newtonsoft.Json;
using Skua.Core.Models.Converters;
namespace Skua.Core.Models.Items;
public class InventoryItem : ItemBase
{
[JsonProperty("CharItemID")]
public int CharItemID { get; set; }
[JsonProperty("bEquip")]
[JsonConverter(typeof(StringBoolConverter))]
public bool Equipped { get; set; }
[JsonProperty("iLvl")]
public int Level { get; set; }
[JsonProperty("EnhLvl")]
public virtual int EnhancementLevel { get; set; }
public override bool Equals(object? obj)
{
return obj is InventoryItem item && item.ID == ID && item.CharItemID == CharItemID;
}
public override int GetHashCode()
{
return HashCode.Combine(ID, CharItemID);
}
}
