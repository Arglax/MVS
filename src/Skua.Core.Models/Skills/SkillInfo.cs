using Newtonsoft.Json;
namespace Skua.Core.Models.Skills;
public class SkillInfo
{
[JsonProperty("id")]
public int ID { get; set; }
[JsonProperty("actID")]
public int ActID { get; set; }
[JsonProperty("damage")]
public double Damage { get; set; }
[JsonProperty("icon")]
public string Icon { get; set; }
[JsonProperty("auto")]
public bool Auto { get; set; }
[JsonProperty("nam")]
public string Name { get; set; }
[JsonProperty("desc")]
public string Description { get; set; }
[JsonProperty("range")]
public int Range { get; set; }
[JsonProperty("mp")]
public int MP { get; set; }
[JsonProperty("isOK")]
public bool IsOk { get; set; }
[JsonProperty("anim")]
public string _Anim { get; set; }
public string[] Animations => _Anim.Split(',');
[JsonProperty("typ")]
public string Type { get; set; }
[JsonProperty("tgtMin")]
public int MinTargets { get; set; }
[JsonProperty("tgtMax")]
public int MaxTargets { get; set; }
[JsonProperty("cd")]
public int Cooldown { get; set; }
[JsonProperty("lock")]
public bool Locked { get; set; }
[JsonProperty("ts")]
public long LastUse { get; set; }
public override string ToString()
{
return Name;
}
}
