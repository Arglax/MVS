using Newtonsoft.Json;
namespace Skua.Core.Models.Factions;
public class Faction
{
[JsonProperty("FactionID")]
public int ID { get; set; }
[JsonProperty("sName")]
public string Name { get; set; }
[JsonProperty("iRank")]
public int Rank { get; set; }
[JsonProperty("iRep")]
public int TotalRep { get; set; }
[JsonProperty("iSpillRep")]
public int Rep { get; set; }
[JsonProperty("iRepToRank")]
public int RequiredRep { get; set; }
public int RemainingRep => RequiredRep - Rep;
}
