using Newtonsoft.Json;
using Skua.Core.Models.Converters;
namespace Skua.Core.Models.Items;
public class ItemBase
{
[JsonProperty("ItemID")]
public virtual int ID { get; set; }
[JsonProperty("sName")]
[JsonConverter(typeof(TrimConverter))]
public virtual string Name { get; set; }
[JsonProperty("sDesc")]
public virtual string Description { get; set; }
[JsonProperty("iQty")]
public virtual int Quantity { get; set; }
[JsonProperty("iStk")]
public virtual int MaxStack { get; set; }
[JsonProperty("bUpg")]
[JsonConverter(typeof(StringBoolConverter))]
public virtual bool Upgrade { get; set; }
[JsonProperty("bWear")]
[JsonConverter(typeof(StringBoolConverter))]
public virtual bool Wearing { get; set; }
[JsonProperty("bCoins")]
[JsonConverter(typeof(StringBoolConverter))]
public virtual bool Coins { get; set; }
[JsonProperty("sType")]
public virtual string CategoryString { get; set; }
[JsonProperty("EnhPatternID")]
public virtual int EnhancementPatternID { get; set; }
[JsonProperty("ProcID")]
[JsonConverter(typeof(IntConverter))]
public virtual int ProcID { get; set; }
[JsonProperty("bHouse")]
[JsonConverter(typeof(StringBoolConverter))]
public virtual bool House { get; set; }
private ItemCategory? _category = null;
public virtual ItemCategory Category => _category is not null
? (ItemCategory)_category
: (ItemCategory)(_category = Enum.TryParse(CategoryString, true, out ItemCategory result) ? result : ItemCategory.Unknown);
[JsonProperty("bTemp")]
[JsonConverter(typeof(StringBoolConverter))]
public virtual bool Temp { get; set; }
[JsonProperty("sES")]
public virtual string ItemGroup { get; set; }
[JsonProperty("sLink")]
public virtual string FileName { get; set; }
[JsonProperty("sFile")]
public virtual string FileLink { get; set; }
[JsonProperty("sMeta")]
public virtual string Meta { get; set; }
public override string ToString()
{
string tag = string.Empty;
tag += Coins ? "AC " : string.Empty;
tag += Upgrade ? "Member" : string.Empty;
string itemGroup = ItemGroup switch
{
"co" => "(Armor)",
"ba" => "(Cape)",
"he" => "(Helm)",
"pe" => "(Pet)",
"Weapon" => "(Weapon)",
_ => "(Item)"
};
return $"[{ID}]\t{itemGroup} {Name} x{Quantity} {tag}";
}
public override bool Equals(object? obj)
{
return obj is ItemBase item && ID == item.ID;
}
public override int GetHashCode()
{
return HashCode.Combine(ID);
}
}
