using Newtonsoft.Json;
using Skua.Core.Models.Converters;
namespace Skua.Core.Models.Servers;
public class Server
{
[JsonProperty("sName")]
public string Name { get; set; }
[JsonProperty("sIP")]
public string IP { get; set; }
[JsonProperty("iChat")]
public int ChatLevel { get; set; }
[JsonProperty("iPort")]
public int Port { get; set; }
[JsonProperty("bOnline")]
[JsonConverter(typeof(StringBoolConverter))]
public bool Online { get; set; }
[JsonProperty("sLang")]
public string Lang { get; set; }
[JsonProperty("iCount")]
public int PlayerCount { get; set; }
[JsonProperty("bUpg")]
[JsonConverter(typeof(StringBoolConverter))]
public bool Upgrade { get; set; }
[JsonProperty("iMax")]
public int MaxPlayers { get; set; }
[JsonProperty("iLevel")]
public int Level { get; set; }
[JsonIgnore]
public long Ping { get; set; } = -1;
public override string ToString()
{
return $"{Name} - {PlayerCount}";
}
public static bool operator ==(Server s0, Server s1)
{
return s0?.Name == s1?.Name;
}
public static bool operator !=(Server s0, Server s1)
{
return s0?.Name != s1?.Name;
}
public override bool Equals(object? obj)
{
return obj is Server server && server.Name == Name;
}
public override int GetHashCode()
{
return HashCode.Combine(Name, IP);
}
}
