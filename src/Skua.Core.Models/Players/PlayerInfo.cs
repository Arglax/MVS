using Newtonsoft.Json;
namespace Skua.Core.Models.Players;
public class PlayerInfo
{
[JsonProperty("uoName")]
public string Name { get; set; }
[JsonProperty("intHP")]
public int HP { get; set; }
[JsonProperty("intHPMax")]
public int MaxHP { get; set; }
[JsonProperty("intMP")]
public int MP { get; set; }
[JsonProperty("sta")]
public PlayerStats? Stats { get; set; } = null;
[JsonProperty("afk")]
public bool AFK { get; set; }
[JsonProperty("entID")]
public int EntID { get; set; }
[JsonProperty("intLevel")]
public int Level { get; set; }
[JsonProperty("strFrame")]
public string Cell { get; set; }
[JsonProperty("strPad")]
public string Pad { get; set; }
[JsonProperty("tx")]
public float X { get; set; }
[JsonProperty("ty")]
public float Y { get; set; }
[JsonProperty("intState")]
public int State { get; set; }
public override string ToString()
{
return $"{EntID}: {Name}";
}
}
