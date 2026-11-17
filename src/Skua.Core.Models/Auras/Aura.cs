using Newtonsoft.Json;
namespace Skua.Core.Models.Auras;
public class Aura
{
[JsonProperty("val")]
public float Value { get; set; } = 1;
[JsonProperty("icon")]
public string Icon { get; set; } = string.Empty;
[JsonProperty("nam")]
public string Name { get; set; } = string.Empty;
[JsonProperty("t")]
public string Type { get; set; } = string.Empty;
[JsonProperty("cLeaf")]
public string cLeaf { get; set; } = string.Empty;
[JsonProperty("dur")]
public int Duration { get; set; } = 0;
[JsonProperty("isNew")]
public bool IsNew { get; set; } = true;
[JsonProperty("ts")]
public long UnixTimeStamp { get; set; } = 0;
[JsonProperty("passive")]
public bool Passive { get; set; } = false;
[JsonProperty("potionType")]
public string? PotionType { get; set; } = string.Empty;
[JsonIgnore]
public DateTimeOffset TimeStamp => UnixTimeStamp > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(UnixTimeStamp) : DateTimeOffset.MinValue;
[JsonIgnore]
public DateTimeOffset ExpiresAt => TimeStamp.AddSeconds(Duration);
[JsonIgnore]
public int RemainingTime => Math.Max(0, (int)(ExpiresAt - DateTimeOffset.Now).TotalSeconds);
[JsonProperty("cat")]
public string Category { get; set; } = string.Empty;
[JsonProperty("fx")]
public string Fx { get; set; } = string.Empty;
[JsonProperty("msgOn")]
public string MsgOn { get; set; } = string.Empty;
[JsonProperty("animOn")]
public string AnimationOn { get; set; } = string.Empty;
[JsonProperty("animOff")]
public string AnimationOff { get; set; } = string.Empty;
public override string ToString()
{
return $"{Name} (x{Value})";
}
public string ToJson(Formatting formatting = Formatting.None)
{
return JsonConvert.SerializeObject(this, formatting);
}
}
