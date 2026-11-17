using Skua.Core.Models.Items;
using Skua.Core.Models.Monsters;
using Skua.Core.Models.Players;
using Skua.Core.Models.Skills;
using System.Drawing;
namespace Skua.Core.Interfaces;
public interface IScriptPlayer
{
string Guild { get; }
int ID { get; }
int XP { get; }
int RequiredXP { get; }
int AccessLevel { get; set; }
string Cell { get; }
string Pad { get; }
string ServerIP { get; }
int State { get; }
bool InCombat => State == 2;
bool Alive => State > 0;
bool LoggedIn { get; }
bool Playing { get; }
string Username { get; }
string Password { get; }
bool Kicked { get; }
bool IsMember { get; }
int Health { get; }
int MaxHealth { get; }
int Mana { get; }
int MaxMana { get; }
int Level { get; }
int Gold { get; }
int Coins { get; }
InventoryItem? CurrentClass { get; }
int CurrentClassRank { get; }
Monster? Target { get; }
bool HasTarget { get; }
bool Loaded { get; }
SkillInfo[]? Skills { get; }
PlayerStats? Stats { get; }
bool AFK { get; }
int X { get; }
int Y { get; }
Point Position => new(X, Y);
int Scale { get; set; }
int WalkSpeed { get; set; }
void Goto(string name);
void SetSpawnPoint()
{
SetSpawnPoint(Cell, Pad);
}
void SetSpawnPoint(string cell, string pad);
void WalkTo(int x, int y, int speed = 8);
void Rest(bool full = false);
}
