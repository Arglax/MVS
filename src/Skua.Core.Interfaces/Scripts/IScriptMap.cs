using Skua.Core.Models.Items;
using Skua.Core.Models.Players;
using Skua.Core.Utils;
namespace Skua.Core.Interfaces;
public interface IScriptMap
{
string LastMap { get; set; }
bool Loading { get; }
string FilePath { get; set; }
string FileName { get; }
string Name { get; }
string FullName { get; }
bool Loaded { get; }
int RoomID { get; }
int RoomNumber { get; }
int PlayerCount { get; }
List<string> Cells { get; }
List<string>? PlayerNames { get; }
List<PlayerInfo>? Players { get; }
List<PlayerInfo>? CellPlayers { get; }
void Reload();
void Join(string map, string cell = "Enter", string pad = "Spawn", bool ignoreCheck = false, bool autoCorrect = true);
void JoinIgnore(string map)
{
Join(map, "Enter", "Spawn", true);
}
void JoinPacket(string map, string cell = "Enter", string pad = "Spawn");
void Jump(string cell, string pad, bool autoCorrect = true, bool clientOnly = false);
void GetMapItem(int id);
void GetMapItem(int id, int quantity)
{
for (int i = 0; i < quantity; i++)
GetMapItem(id);
}
bool PlayerExists(string name)
{
return PlayerNames != null && PlayerNames.Contains(x => x == name);
}
bool TryGetPlayer(string username, out PlayerInfo? player)
{
return (player = GetPlayer(username)) is not null;
}
PlayerInfo? GetPlayer(string username);
List<MapItem>? FindMapItems(bool forceRefresh = false);
void ClearMapItemsCache(string? specificMap = null);
}
