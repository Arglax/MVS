using Newtonsoft.Json;
namespace Skua.Core.Models.Monsters;
public class Monster
{
[JsonProperty("strMonName")]
public string Name { get; set; }
[JsonProperty("MonID")]
public int ID { get; set; }
[JsonProperty("sRace")]
public string Race { get; set; }
[JsonProperty("strFrame")]
public string Cell { get; set; }
[JsonProperty("MonMapID")]
public int MapID { get; set; }
[JsonProperty("intHP")]
public int HP { get; set; }
[JsonProperty("intHPMax")]
public int MaxHP { get; set; }
[JsonProperty("intState")]
public int State { get; set; }
[JsonProperty("strMonFileName")]
public string FileName { get; set; }
public bool Alive => HP > 0 && State > 0;
public override string ToString()
{
return $"{Name} [{ID}] [{MapID}, {Cell}]";
}
}
