using Newtonsoft.Json;
namespace Skua.Core.Models;
public enum GameActions
{
LoadShop,
LoadEnhShop,
LoadHairShop,
EquipItem,
UnequipItem,
BuyItem,
SellItem,
GetMapItem,
TryQuestComplete,
AcceptQuest,
DoIA,
Rest,
Who,
Transfer
}
public readonly struct GameActionLock
{
[JsonProperty("cd")]
public long CD { get; }
[JsonProperty("ts")]
public long TS { get; }
}
