using Skua.Core.Models;
using System.Collections.Immutable;
using System.ComponentModel;
namespace Skua.Core.Interfaces;
public interface IScriptOption : INotifyPropertyChanged
{
ImmutableDictionary<string, Func<object>> OptionDictionary { get; }
int ReloginTryDelay { get; set; }
int LoginTimeout { get; set; }
bool AcceptACDrops { get; set; }
bool AcceptAllDrops { get; set; }
bool RejectAllDrops { get; set; }
bool AggroAllMonsters { get; set; }
bool AggroMonsters { get; set; }
bool AttackWithoutTarget { get; set; }
bool AutoRelogin { get; set; }
bool AutoReloginAny { get; set; }
bool RetryRelogin { get; set; }
string CustomGuild { get; set; }
string CustomName { get; set; }
bool DisableCollisions { get; set; }
bool DisableDeathAds { get; set; }
bool DisableFX { get; set; }
int MaximumTries { get; set; }
int ActionDelay { get; set; }
int GuildColor { get; set; }
bool HidePlayers { get; set; }
int HuntBuffer { get; set; }
int HuntDelay { get; set; }
bool InfiniteRange { get; set; }
bool LagKiller { get; set; }
bool HeadlessMode { get; set; }
bool StreamerMode { get; set; }
bool OptimizePlayers { get; set; }
bool UseFunctionBasedSkills { get; set; }
int LoadTimeout { get; set; }
string? ReloginServer { get; set; }
bool Magnetise { get; set; }
int NameColor { get; set; }
bool PrivateRooms { get; set; }
bool RestPackets { get; set; }
bool SafeRelogin { get; set; }
bool SafeTimings { get; set; }
int SetFPS { get; set; }
bool ShowFPS { get; set; }
bool SkipCutscenes { get; set; }
int WalkSpeed { get; set; }
int PrivateNumber { get; set; }
int JoinMapTries { get; set; }
int QuestAcceptAndCompleteTries { get; set; }
int ReloginTries { get; set; }
HuntPriorities HuntPriority { get; set; }
void Reset();
void ResetToDefault();
void Save();
}
