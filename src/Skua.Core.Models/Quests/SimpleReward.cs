using Newtonsoft.Json;
using Skua.Core.Models.Items;
namespace Skua.Core.Models.Quests;
public class SimpleReward : ItemBase
{
[JsonProperty("iRate")]
public double Rate { get; set; }
[JsonProperty("iType")]
public int Type { get; set; }
}
