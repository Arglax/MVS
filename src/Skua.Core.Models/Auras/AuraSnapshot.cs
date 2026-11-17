using Newtonsoft.Json;
namespace Skua.Core.Models.Auras;
public class AuraSnapshot
{
[JsonProperty("nam")]
public string Name { get; set; } = string.Empty;
[JsonProperty("n")]
public int Stacks { get; set; }
[JsonProperty("dur")]
public double Duration { get; set; }
[JsonProperty("remaining")]
public double RemainingTime { get; set; }
[JsonProperty("persist")]
public bool Persistent { get; set; }
[JsonProperty("icon")]
public string Icon { get; set; } = string.Empty;
[JsonProperty("desc")]
public string Description { get; set; } = string.Empty;
}
