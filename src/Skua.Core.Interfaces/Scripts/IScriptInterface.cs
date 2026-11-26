using Skua.Core.Interfaces.Services;
using Skua.Core.Models;
namespace Skua.Core.Interfaces;
public interface IScriptInterfaceManager
{
void Initialize();
Task StopTimerAsync();
}
public interface IScriptInterface
{
static IScriptInterface Instance { get; protected set; }
IFlashUtil Flash { get; }
IScriptStatus Manager { get; }
IScriptAuto Auto { get; }
IScriptBoost Boosts { get; }
IScriptBotStats Stats { get; }
IScriptSelfAuras Self { get; }
IScriptTargetAuras Target { get; }
IAuraMonitorService AuraMonitor { get; }
IScriptWatchdogService Watchdog { get; }
IUltraBossHelper UltraBossHelper { get; }
IScriptCombat Combat { get; }
IScriptKill Kill { get; }
IScriptHunt Hunt { get; }
IScriptDrop Drops { get; }
IScriptEvent Events { get; }
IScriptFaction Reputation { get; }
IScriptHouseInv House { get; }
IScriptInventory Inventory { get; }
IScriptTempInv TempInv { get; }
IScriptBank Bank { get; }
IScriptInventoryHelper InvHelper { get; }
IScriptLite Lite { get; }
IScriptOption Options { get; }
IScriptMap Map { get; }
IScriptMonster Monsters { get; }
IScriptPlayer Player { get; }
IScriptQuest Quests { get; }
IScriptSend Send { get; }
IScriptShop Shops { get; }
IScriptSkill Skills { get; }
IScriptWait Wait { get; }
IScriptServers Servers { get; }
IScriptHandlers Handlers { get; }
ICaptureProxy GameProxy { get; }
IScriptAccounts Accounts { get; }
IScriptOptionContainer? Config { get; }
bool ShouldExit { get; }
Version Version { get; }
Random Random { get; set; }
void SellJunk();
void Sleep(int ms);
Task SleepAsync(int ms);
[Obsolete("Use StopAsync instead for proper async/await pattern.")]
void Stop(bool runScriptStoppingEvent = true);
Task StopAsync(bool runScriptStoppingEvent = true);
void Log(string message);
Task Schedule(int delay, Action<IScriptInterface> action);
Task Schedule(int delay, Func<IScriptInterface, Task> function);
bool? ShowMessageBox(string message, string caption, bool yesAndNo = false);
DialogResult ShowMessageBox(string message, string caption, params string[] buttons);
}
