using Newtonsoft.Json;
using Skua.Core.Models.Items;
using System.Diagnostics.CodeAnalysis;
namespace Skua.Core.Models.Quests;
public class QuestData : IEqualityComparer<QuestData>
{
[JsonProperty("ID")]
public int ID { get; set; }
[JsonProperty("Slot")]
public int Slot { get; set; }
[JsonProperty("Value")]
public int Value { get; set; }
[JsonProperty("Name")]
public string Name { get; set; }
[JsonProperty("Once")]
public bool Once { get; set; }
[JsonProperty("Field")]
public string Field { get; set; }
[JsonProperty("Index")]
public int Index { get; set; }
[JsonProperty("Upgrade")]
public bool Upgrade { get; set; }
[JsonProperty("Level")]
public int Level { get; set; }
[JsonProperty("RequiredClassID")]
public int RequiredClassID { get; set; }
[JsonProperty("RequiredClassPoints")]
public int RequiredClassPoints { get; set; }
[JsonProperty("RequiredFactionId")]
public int RequiredFactionId { get; set; }
[JsonProperty("RequiredFactionRep")]
public int RequiredFactionRep { get; set; }
[JsonProperty("Gold")]
public int Gold { get; set; }
[JsonProperty("XP")]
public int XP { get; set; }
[JsonProperty("AcceptRequirements")]
public List<ItemBase> AcceptRequirements { get; set; } = new();
[JsonProperty("Requirements")]
public List<ItemBase> Requirements { get; set; } = new();
[JsonProperty("Rewards")]
public List<ItemBase> Rewards { get; set; } = new();
[JsonProperty("SimpleRewards")]
public List<SimpleReward> SimpleRewards { get; set; } = new();
public bool Equals(QuestData? x, QuestData? y)
{
return (x is null && y is null) || ((x is not null || y is null) && (x is null || y is not null) && x.ID == y.ID);
}
public override bool Equals(object? obj)
{
return obj is not null && obj is QuestData quest && quest.ID == ID;
}
public override int GetHashCode()
{
return HashCode.Combine(ID, Name);
}
public int GetHashCode([DisallowNull] QuestData obj)
{
return HashCode.Combine(obj.ID, obj.Name);
}
public override string ToString()
{
return $"[{ID}] {Name}{(Upgrade ? " (Member)" : string.Empty)}";
}
}
