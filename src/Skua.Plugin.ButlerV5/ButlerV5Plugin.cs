using System.Collections;
using System.IO;
using System.Reflection;
using Avalonia.Threading;
using Skua.Core.Interfaces;
using Skua.Core.Options;
using Skua.Shared.Avalonia.Services;
namespace ButlerV5;
public class ButlerV5Plugin : ISkuaPlugin
{
private const string MenuButtonText = "Butler";
public string Name => "Butler";
public string OptionsStorage => "l0newolf12_ButlerV5_Avalonia";
public string Author => "l0newolf12";
public string Description =>
"Lets your accounts follow and fight alongside each other, so you can follow someone or have your other accounts follow you.";
public List<IOption>? Options { get; } = new()
{
new Option<bool>("passiveAttack", "Only attack when master attacks",
"While following: only fight while the master is fighting.\n" +
"Off = always attack, like Butler v3 did. Default: Off.", false),
new Option<bool>("antiLagSummoned", "AntiLag while summoned",
"Turn on Skua's AntiLag (Lag Killer, animation cuts, hidden monsters) while\n" +
"this account is summoned by a master. Great for background alts when you're\n" +
"running several at once. Note: it hides monsters and animations in this\n" +
"client until the follow ends. Default: On.",
true),
new Option<bool>("debugLogs", "Enable debug logs",
"Record diagnostics into the in-plugin log viewer (Settings window). Turn off\n" +
"to stop all logging entirely. Default: Off.", false),
new Option<bool>("autoSaveLog", "Auto-save logs to file",
"Saves Butler's log to a file on this device.\n" +
"One file per account. Off = logs stay in the viewer until you click Save.\n" +
"This is a global option - applies to all accounts. Default: Off.", false),
new Option<int>("gotoDelay", "Goto delay (milliseconds)",
"Milliseconds between goto attempts while chasing the master. Default: 500,\n" +
"the same cadence Butler v3 used. Lower it (down to ~250, the loop tick) to\n" +
"chase harder on map changes; raise it to send fewer goto commands, which\n" +
"matters most with 'Always goto the master' on, since that keeps them going\n" +
"for the whole follow. Below ~250 does nothing - the loop is the floor.",
500),
new Option<bool>("alwaysGoto", "Always goto the master",
"Keep sending goto even while already in the master's map and cell. Fixes\n" +
"butlers that end up in the wrong cell after dying and never walk back to\n" +
"the master on their own. Costs extra goto commands - one every 'Goto delay'\n" +
"milliseconds for as long as the follow lasts - so raise Goto delay if that\n" +
"feels like too much. Ignored in leech mode and when Copy master's walking is on. Default: Off.",
false),
new Option<bool>("copyWalk", "Copy master's walking",
"When following in the same room and cell, mirror the master's actual walking\n" +
"position instead of standing still after teleporting beside them. Existing\n" +
"goto, cell-jump, direct-join and rescue behavior still handles map/room\n" +
"changes and recovery. Ignored in leech mode and takes over from Always goto. Default: Off.",
false),
new Option<ButlerClassType>("classType", "Class type",
"Which CoreBots class this butler equips when it starts following:\n" +
"Farm / Solo / Dodge / Boss as selected in CoreBots options, or None to\n" +
"keep current gear. Saved per account - after login, each account shows\n" +
"and keeps its own choice. Default: None.",
ButlerClassType.None),
new Option<ParkSpot>("parkLocation", "Park location",
"Where a butler goes when it's released or its master goes offline. Default: House.\n" +
"House = its own house (safest, quietest). Whitemap = the classic empty map.\n" +
"Stay = don't move at all - careful, Stay can leave a butler standing\n" +
"in a boss room eating hits after you release it.",
ParkSpot.House),
new Option<bool>("obeySummons", "Obey summons",
"If off, this account ignores every Summon / 'Follow me' order from your\n" +
"other accounts. Turn this OFF on the account you play by hand, so a\n" +
"misclick in another client can never yank it across the map mid-fight.\n" +
"Default: On.",
true),
new Option<bool>("leechMode", "Leech mode (wait at Enter)",
"While following, stay in the master's room but wait at the Enter cell\n" +
"instead of fighting beside them. Good for dragging a low-level alt through\n" +
"content without it dying every pull. The butler does NOT attack at all\n" +
"in this mode - don't expect damage from it. Default: Off.",
false),
new Option<int>("rescueThreshold", "Failed gotos before rescue",
"How many goto attempts without reaching the master before the butler joins\n" +
"the master's exact map-room from the sync file (how it enters locked maps).\n" +
"Default: 3. Lower reacts faster but can false-trigger on normal map-change\n" +
"lag; don't set it too high or it will wait forever before rescuing itself.",
3),
new Option<int>("rosterRefresh", "Butler window refresh (seconds)",
"How often the Butler window re-reads everyone's status files. Default: 2.\n" +
"Lower feels snappier but reads the disk more; don't set it too high\n" +
"or the list and incoming Summon orders will feel laggy.",
2),
new Option<bool>("questBypass", "Quest bypasses (UpdateQuest)",
"When the butler lands on a map with quest-locked cells or mobs, fake the\n" +
"gate quest CLIENT-SIDE so the cell opens and mobs are targetable - only\n" +
"on maps in the built-in table, never as a blanket at startup. The server\n" +
"is never told anything. Default: On.",
true),
new Option<string>("questBypassCustom", "Custom bypass quest IDs",
"Extra quest IDs to fake when the follow starts, separated by commas\n" +
"(example: 1234, 5678). Use this when a map's cells stay locked for\n" +
"butlers and it isn't in the built-in table yet. Empty = none.",
""),
new Option<bool>("questClone", "EXPERIMENTAL: clone master quest state",
"While following, copy the master's quest progress CLIENT-SIDE so every\n" +
"quest-locked cell/mob the master can see, the butler can see too - no\n" +
"outdated bypass list. Both sides must have this enabled. Fast: one game\n" +
"read per sync, timing shown in the debug log. Default: On.",
true),
new Option<bool>("syncQuests", "Follow master's quests",
"While following, mirror the master's active and registered quest IDs.\n" +
"The butler registers the same quests locally, including selected rewards,\n" +
"so it can accept them, collect its own required drops and complete them using\n" +
"its own inventory. Both master and followers must have this enabled. Default: Off.",
false),
new Option<bool>("questUnlockAll", "EXPERIMENTAL: unlock all quests",
"Nuclear bypass: sets EVERY quest slot to max CLIENT-SIDE, so every quest-\n" +
"locked cell/mob/map opens without cloning or a bypass list - a blunter\n" +
"alternative to quest cloning. Overrides 'clone master quest state' and\n" +
"'quest bypasses' while on. Client-side only, restored on relogin, but it\n" +
"scrambles this account's real quest log until then - avoid on an account\n" +
"you play by hand. Default: On.",
true),
new Option<bool>("fakeLevel", "Fake level 100 (client-side)",
"Shows this client as level 100 so level-gated maps (e.g. icestormunder)\n" +
"let the butler in. Purely visual/client-side, but your character will\n" +
"display as level 100 to yourself until relog. Default: Off.",
false),
new Option<bool>("instantWarnings", "Instant warning detection",
"Reads the raw game packet stream to catch 'Locked zone' / 'room is full' /\n" +
"'ignoring goto' warnings the moment they arrive - near-instant rescue\n" +
"or a direct join. Failed-goto counting still works if no warning arrives.\n" +
"Turn off if following misbehaves. Default: On.",
true),
new Option<bool>("antiLagFollowing", "AntiLag while following",
"Same as 'AntiLag while summoned', but only while MANUALLY following someone\n" +
"(you clicked Follow in this client's window), not when summoned. Default: Off.",
false),
new Option<bool>("enabled", "Enable Butler",
"Master switch for the ENTIRE plugin. This is a GLOBAL setting - it affects\n" +
"every account at once. Turn it OFF to completely stop Butler: no\n" +
"broadcasting, following, summoning, or quest/skill actions anywhere until you\n" +
"turn it back on. Turning it off stops any active butler in place (no parking).\n" +
"Default: On.",
true),
};
private IServiceProvider? _services;
private IScriptInterface? _bot;
private IPluginHelper? _helper;
private IPluginContainer? _container;
private IScriptManager? _scriptManager;
private IDispatcherService? _dispatcher;
private IDialogService? _dialogs;
private CancellationTokenSource? _menuRegistrationCts;
private IList? _hostPluginMenu;
private object? _hostMenuItem;
private Broadcaster? _broadcaster;
private Follower? _follower;
private PluginWindow? _window;
private RosterWindow? _roster;
private PluginWindow? _settingsWindow;
private CancellationTokenSource? _orderWatcherCts;
private Task? _orderWatcher;
private DateTime _orderGoneSince = DateTime.MinValue;
private bool _busyOrderLogged;
private readonly HashSet<string> _processedSendAllCommands = new(StringComparer.OrdinalIgnoreCase);
private readonly Queue<string> _processedSendAllCommandOrder = new();
private bool _wasEnabledOrder = true;
private string? _declinedMaster;
private EventHandler? _processExitHandler;
private SyncWatcher? _syncWatcher;
private readonly AutoResetEvent _orderSignal = new(false);
private const int OrderFallbackMs = 3000;
public event Action? SyncFilesChanged;
public string? OwnUsername => _bot?.Player?.LoggedIn == true ? _bot.Player.Username : null;
public string MyServerName => _broadcaster?.CurrentServer ?? "";
public bool PluginEnabled => GetOption("enabled", true);
public IReadOnlyList<string> CurrentFollowers => _broadcaster?.Followers ?? new List<string>();
public IReadOnlyList<string> RecentPlayerTargets => PlayerTargetHistory.Load();
public string? FollowingWho => _follower?.Master;
public FollowSource FollowingHow => _follower?.Source ?? FollowSource.None;
public bool IsScriptRunning => _scriptManager?.ScriptRunning == true;
public string RunningScriptName
{
get
{
if (!IsScriptRunning)
return "";
string name = Path.GetFileName(_scriptManager?.LoadedScript ?? "");
return string.IsNullOrEmpty(name) ? "script" : name;
}
}
public void Load(IServiceProvider provider, IPluginHelper helper)
{
_services = provider;
_helper = helper;
_bot = provider.GetService(typeof(IScriptInterface)) as IScriptInterface ?? IScriptInterface.Instance;
IPluginManager? manager = provider.GetService(typeof(IPluginManager)) as IPluginManager;
_container = manager?.GetContainer(this);
_scriptManager = provider.GetService(typeof(IScriptManager)) as IScriptManager;
_dispatcher = provider.GetService(typeof(IDispatcherService)) as IDispatcherService;
_dialogs = provider.GetService(typeof(IDialogService)) as IDialogService;
DebugLog.EnabledProvider = () => GetOption("debugLogs", false);
DebugLog.AutoSaveProvider = () => GetOption("autoSaveLog", false);
DebugLog.Log("Plugin", $"Load: pid={Environment.ProcessId} container={( _container != null ? "ok" : "NULL")} bot={( _bot != null ? "ok" : "NULL")}");
_broadcaster = new Broadcaster(_bot!,
() => PluginEnabled,
() => GetOption("questClone", true) && !GetOption("questUnlockAll", true),
() => GetOption("syncQuests", false),
() => (_follower?.IsRunning == true && _follower.IsParked == false) ? _follower.Master : null,
() => IsScriptRunning ? RunningScriptName : null,
() => _declinedMaster,
() => _follower?.IsParking == true,
Log);
_follower = new Follower(_bot!, new FollowerSettings
{
PassiveAttack = () => GetOption("passiveAttack", false),
GotoDelayMs = () => GetOptionInt("gotoDelay", 500),
RescueThreshold = () => GetOptionInt("rescueThreshold", 3),
LeechMode = () => GetOption("leechMode", false),
Park = () => GetOptionEnum("parkLocation", ParkSpot.House),
InstantWarnings = () => GetOption("instantWarnings", false),
QuestBypasses = () => GetOption("questBypass", true),
CustomBypassIds = () => GetOptionString("questBypassCustom", ""),
LevelFake = () => GetOption("fakeLevel", false),
QuestClone = () => GetOption("questClone", true),
QuestSync = () => GetOption("syncQuests", false),
AlwaysGoto = () => GetOption("alwaysGoto", false),
CopyWalk = () => GetOption("copyWalk", false),
UnlockAllQuests = () => GetOption("questUnlockAll", true),
ScriptRunning = () => IsScriptRunning,
AntiLagSummoned = () => GetOption("antiLagSummoned", true),
AntiLagFollowing = () => GetOption("antiLagFollowing", false),
ScriptSuspendNotice = ShowSuspendNotice,
}, Log);
try
{
_syncWatcher = new SyncWatcher(SyncFile.Directory, $"*{SyncFile.Suffix}");
_syncWatcher.Changed += OnSyncFilesChanged;
}
catch (Exception ex)
{
DebugLog.Log("Plugin", $"SyncWatcher failed ({ex.Message}) - falling back to polling only");
}
_broadcaster.Start();
StartOrderWatcher();
_processExitHandler = (_, _) => _broadcaster?.WriteOffline();
AppDomain.CurrentDomain.ProcessExit += _processExitHandler;
RegisterMenuButton(provider, helper);
Log("Butler plugin loaded.");
}
public void Unload()
{
try
{
_menuRegistrationCts?.Cancel();
_menuRegistrationCts?.Dispose();
_menuRegistrationCts = null;
if (_helper != null)
{
RunOnUi(() =>
{
try
{
if (_hostPluginMenu != null && _hostMenuItem != null && _hostPluginMenu.Contains(_hostMenuItem))
_hostPluginMenu.Remove(_hostMenuItem);
}
catch { }
_helper?.RemoveMenuButton(MenuButtonText);
_hostPluginMenu = null;
_hostMenuItem = null;
});
}
if (_syncWatcher != null)
{
_syncWatcher.Changed -= OnSyncFilesChanged;
_syncWatcher.Dispose();
_syncWatcher = null;
}
_orderWatcherCts?.Cancel();
_orderSignal.Set();
try { _orderWatcher?.Wait(2000); } catch { }
_orderWatcherCts?.Dispose();
_orderWatcherCts = null;
_follower?.Stop();
_broadcaster?.WriteOffline();
_broadcaster?.Stop();
if (_processExitHandler != null)
AppDomain.CurrentDomain.ProcessExit -= _processExitHandler;
RunOnUi(() =>
{
_window?.Close();
_window = null;
_settingsWindow?.Close();
_settingsWindow = null;
});
}
catch
{
}
}
private void ShowSuspendNotice(string master)
{
try
{
RunOnUi(() =>
{
bool? keepSuspended = _dialogs?.ShowMessageBox(
$"A script started while this account was following {master}.\n\n" +
"Following is now SUSPENDED and will resume automatically when the script stops.\n\n" +
"Yes - keep it suspended (resume after the script)\n" +
"No - stop following entirely",
"Butler",
yesAndNo: true);
if (keepSuspended == false)
StopFollow();
});
}
catch
{
}
}
public void StartFollow(string master, FollowSource source)
{
DebugLog.Log("UI", $"StartFollow({master}, {source})");
if (!PluginEnabled)
{
Log($"Can't follow {master} - Butler is disabled ('Enable Butler' is off). Turn it on first.",
"Can't follow the master - Butler is disabled ('Enable Butler' is off). Turn it on first.");
return;
}
if (IsScriptRunning)
{
Log($"Can't start following {master}: a script is running ({RunningScriptName}). Stop it first.",
"Can't start following the master: a script is running on this account. Stop it first.");
return;
}
if (CurrentFollowers.Any(f => f.Equals(master, StringComparison.OrdinalIgnoreCase)))
{
Log($"Releasing {master} (can't follow an account that follows me).",
"Releasing a butler (can't follow an account that follows me).");
ToggleFollower(master);
}
_follower?.Start(master, source);
}
public void StopFollow()
{
DebugLog.Log("UI", "StopFollow clicked");
if (_follower?.Source == FollowSource.Ordered && _follower.Master is { } orderedMaster)
{
_declinedMaster = orderedMaster;
DebugLog.Log("UI", $"declining {orderedMaster}'s summon (stop on an ordered follow)");
}
_follower?.Stop();
}
public void SendAllToPlayer(string playerName)
{
string target = playerName.Trim();
if (target.Length == 0)
{
Log("Enter a player name first.", "Enter a player name first.");
return;
}
if (!PluginEnabled)
{
Log("Can't send all - Butler is disabled ('Enable Butler' is off). Turn it on first.");
return;
}
string me = OwnUsername ?? "";
if (me.Length == 0)
{
Log("Can't send all - this account is not logged in.");
return;
}
List<SyncData> roster = Roster.GetOnline(me);
if (target.Equals(me, StringComparison.OrdinalIgnoreCase) ||
roster.Any(account => account.Username.Equals(target, StringComparison.OrdinalIgnoreCase)))
{
Log($"'{target}' is already one of the active Butler client accounts. Enter an external player name instead.");
return;
}
string myServer = MyServerName;
bool alreadyManual(SyncData account) =>
account.Following.Equals(me, StringComparison.OrdinalIgnoreCase) &&
!CurrentFollowers.Any(f => f.Equals(account.Username, StringComparison.OrdinalIgnoreCase));
bool isMaster(SyncData account) => account.Followers.Count > 0;
bool differentServer(SyncData account) =>
!string.IsNullOrEmpty(account.Server) && !string.IsNullOrEmpty(myServer) &&
!account.Server.Equals(myServer, StringComparison.OrdinalIgnoreCase);
bool followingAnother(SyncData account) =>
!string.IsNullOrEmpty(account.Following) &&
!account.Following.Equals(me, StringComparison.OrdinalIgnoreCase);
bool unavailable(SyncData account) =>
account.ScriptOn || alreadyManual(account) || isMaster(account) ||
differentServer(account) || account.Parking || followingAnother(account);
List<string> skipped = roster.Where(unavailable).Select(account => account.Username).ToList();
List<string> recipients = roster.Where(account => !unavailable(account))
.Select(account => account.Username)
.ToList();
bool sentLocally = false;
if (!IsScriptRunning)
{
try
{
IScriptPlayer? player = _bot?.Player;
if (player?.LoggedIn == true)
{
player.Goto(target);
sentLocally = true;
}
}
catch (Exception ex)
{
DebugLog.Log("UI", $"Local send-all goto failed for {target}: {ex.Message}");
}
}
if (recipients.Count == 0)
{
if (sentLocally)
{
PlayerTargetHistory.Add(target);
Log($"Sent this account to {target}; no other available Butler accounts were online.");
return;
}
Log(skipped.Count == 0
? "No available Butler accounts to send, and this account is busy running a script."
: $"No available butlers to send to {target}. Skipped: {string.Join(", ", skipped)}");
return;
}
Broadcaster? broadcaster = _broadcaster;
if (broadcaster is null)
{
Log(sentLocally
? $"This account was sent to {target}, but Butler is still initializing and couldn't broadcast to the other accounts."
: "Can't send all - Butler is still initializing. Try again shortly.");
return;
}
bool published = broadcaster.PublishSendAllGoto(target, recipients);
PlayerTargetHistory.Add(target);
if (published)
{
Log($"Sent {recipients.Count} other butler(s){(sentLocally ? " and this account" : "")} to {target}." +
(skipped.Count > 0 ? $" Skipped {skipped.Count} unavailable account(s)." : ""),
"Sent accounts to an external player.");
}
else
{
Log($"{(sentLocally ? "This account was sent" : "This account was not sent")} to {target}; queued Send All for {recipients.Count} other butler(s). It will publish when this account finishes loading its map.",
"Send All queued; waiting for the sync file to become writable.");
}
DebugLog.Log("UI", $"Send all to {target}: local=[{sentLocally}], recipients=[{string.Join(", ", recipients)}], skipped=[{string.Join(", ", skipped)}]");
try { SyncFilesChanged?.Invoke(); } catch { }
}
public void OrderFollowers(IEnumerable<string> usernames)
{
_broadcaster?.SetFollowers(usernames);
Log("Ordered to follow me: " + string.Join(", ", CurrentFollowers),
$"Ordered to follow me: {CurrentFollowers.Count} account(s).");
}
public void ToggleFollower(string username)
{
List<string> current = CurrentFollowers.ToList();
int index = current.FindIndex(f => f.Equals(username, StringComparison.OrdinalIgnoreCase));
if (index >= 0)
{
current.RemoveAt(index);
}
else
{
if (FollowingWho is { } iAmFollowing)
{
Log($"Can't summon {username} while following {iAmFollowing} - stop that first.",
"Can't summon a butler while following a master - stop that first.");
return;
}
if (!PluginEnabled)
{
Log($"Can't summon {username} - Butler is disabled ('Enable Butler' is off). Turn it on first.",
"Can't summon a butler - Butler is disabled ('Enable Butler' is off). Turn it on first.");
return;
}
current.Add(username);
}
DebugLog.Log("UI", $"ToggleFollower({username}) -> {(index >= 0 ? "release" : "summon")}, list now [{string.Join(",", current)}]");
_broadcaster?.SetFollowers(current);
Log(index >= 0 ? $"Released {username}." : $"Summoned {username} to follow me.",
index >= 0 ? "Released a butler." : "Summoned a butler to follow me.");
}
public void SummonAll()
{
DebugLog.Log("UI", "Summon all clicked");
if (FollowingWho is { } iAmFollowing)
{
Log($"Can't summon while following {iAmFollowing} - stop that first.",
"Can't summon while following a master - stop that first.");
return;
}
if (!PluginEnabled)
{
Log("Can't summon - Butler is disabled ('Enable Butler' is off). Turn it on first.");
return;
}
string me = OwnUsername ?? "";
List<SyncData> roster = Roster.GetOnline(OwnUsername);
string myServer = MyServerName;
bool AlreadyManual(SyncData d) =>
d.Following.Equals(me, StringComparison.OrdinalIgnoreCase) &&
!CurrentFollowers.Any(f => f.Equals(d.Username, StringComparison.OrdinalIgnoreCase));
bool IsMaster(SyncData d) => d.Followers.Count > 0;
bool DifferentServer(SyncData d) =>
!string.IsNullOrEmpty(d.Server) && !string.IsNullOrEmpty(myServer) &&
!d.Server.Equals(myServer, StringComparison.OrdinalIgnoreCase);
bool FollowingAnother(SyncData d) =>
!string.IsNullOrEmpty(d.Following) && !d.Following.Equals(me, StringComparison.OrdinalIgnoreCase);
bool Unavailable(SyncData d) =>
d.ScriptOn || AlreadyManual(d) || IsMaster(d) || DifferentServer(d) || d.Parking || FollowingAnother(d);
List<string> skipped = roster.Where(Unavailable).Select(d => d.Username).ToList();
List<string> online = roster.Where(d => !Unavailable(d)).Select(d => d.Username).ToList();
if (skipped.Count > 0)
Log("Skipped (script / already following me / following another / is a master / different server / parking): " + string.Join(", ", skipped),
$"Skipped {skipped.Count} unavailable account(s) (script / following / is a master / different server / parking).");
if (online.Count == 0)
{
Log("No available butlers to summon.");
return;
}
if (FollowingWho is { } master &&
online.Any(u => u.Equals(master, StringComparison.OrdinalIgnoreCase)))
{
Log($"Stopped following {master} (summoning everyone).",
"Stopped following the master (summoning everyone).");
_follower?.Stop();
}
List<string> finalList = CurrentFollowers.Concat(online)
.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
_broadcaster?.SetFollowers(finalList);
Log("Summoned all: " + string.Join(", ", online.Count > 0 ? online : new[] { "(nobody new available)" }),
$"Summoned all: {online.Count} account(s).");
}
public void ReleaseFollowers()
{
DebugLog.Log("UI", "Release all clicked");
_broadcaster?.SetFollowers(Array.Empty<string>());
Log("Released all followers.");
}
public IOptionContainer? OptionContainer => _container?.OptionContainer;
public IServiceProvider? Services => _services;
public void SaveSetting(string name, object value)
{
try
{
IOptionContainer? oc = _container?.OptionContainer;
if (oc == null)
return;
oc.Set(name, value);
oc.Save();
DebugLog.Log("UI", $"setting changed: {name} = {value}");
}
catch (Exception ex)
{
Log($"Could not save setting {name}: {ex.Message}",
"Could not save a setting. Turn on Butler's logs in its settings to see what happened.");
}
}
public void OpenSettings()
{
DebugLog.Log("UI", "Settings clicked");
try
{
RunOnUi(() =>
{
if (_settingsWindow == null)
{
SettingsWindow view = new(this);
_settingsWindow = PluginWindow.Show(view, "Butler - Settings", 900, 630, 720, 440);
_settingsWindow.Closed += (_, _) =>
{
view.OnClosed();
_settingsWindow = null;
};
}
else
{
_settingsWindow.Activate();
}
});
}
catch (Exception ex)
{
Log($"Could not open settings: {ex.Message}",
"Could not open settings. Turn on Butler's logs in its settings to see what happened.");
}
}
public void OpenWindow()
{
try
{
RunOnUi(() =>
{
if (_window == null)
{
RosterWindow view = _roster = new(this);
_window = PluginWindow.Show(view, "Butler", 560, 510, 450, 320);
_window.Closed += (_, _) =>
{
view.OnClosed();
_window = null;
_roster = null;
};
}
else
{
_roster?.RefreshRoster();
_window.Activate();
}
});
}
catch (Exception ex)
{
Log($"Could not open window: {ex.Message}",
"Could not open window. Turn on Butler's logs in its settings to see what happened.");
}
}
public void ShowMessage(string message) =>
_dialogs?.ShowMessageBox(message, "Butler");
public bool ConfirmDisablePlugin() =>
_dialogs?.ShowMessageBox(
"Turning this off stops Butler from working on ALL accounts - no following, " +
"summoning, or broadcasting until you turn it back on.\n\nDisable Butler?",
"Butler",
yesAndNo: true) == true;
private void RunOnUi(Action action)
{
if (_dispatcher != null)
_dispatcher.Invoke(action);
else
Dispatcher.UIThread.Post(action);
}
private void RegisterMenuButton(IServiceProvider provider, IPluginHelper helper)
{
_menuRegistrationCts = new CancellationTokenSource();
CancellationToken token = _menuRegistrationCts.Token;
RunOnUi(() =>
{
if (token.IsCancellationRequested)
return;
helper.AddMenuButton(MenuButtonText, OpenWindow);
TryAttachMenuItemDirect(provider, helper);
});
_ = Task.Run(async () =>
{
try
{
await Task.Delay(1500, token);
if (!token.IsCancellationRequested)
RunOnUi(() => TryAttachMenuItemDirect(provider, helper));
}
catch (OperationCanceledException)
{
}
}, token);
}
private void TryAttachMenuItemDirect(IServiceProvider provider, IPluginHelper helper)
{
try
{
const string mainMenuTypeName = "Skua.App.Avalonia.ViewModels.MainMenu.MainMenuViewModel";
Type? mainMenuType = AppDomain.CurrentDomain.GetAssemblies()
.Select(a =>
{
try { return a.GetType(mainMenuTypeName, throwOnError: false); }
catch { return null; }
})
.FirstOrDefault(t => t != null)
?? Type.GetType($"{mainMenuTypeName}, Skua", throwOnError: false);
object? mainMenu = mainMenuType == null ? null : provider.GetService(mainMenuType);
object? plugins = mainMenuType?.GetProperty("Plugins", BindingFlags.Public | BindingFlags.Instance)
?.GetValue(mainMenu);
FieldInfo? buttonsField = helper.GetType().GetField("_buttons", BindingFlags.NonPublic | BindingFlags.Instance);
IDictionary? buttons = buttonsField?.GetValue(helper) as IDictionary;
object? menuItem = buttons?[MenuButtonText];
if (plugins is not IList menu || menuItem == null)
{
DebugLog.Log("UI", "Avalonia menu fallback is waiting for the host menu");
return;
}
bool present = menu.Cast<object>().Any(item => ReferenceEquals(item, menuItem));
if (!present)
{
menu.Add(menuItem);
DebugLog.Log("UI", "attached ButlerV5 Avalonia directly to the host Plugins menu", LogLevel.Success);
}
_hostPluginMenu = menu;
_hostMenuItem = menuItem;
}
catch (Exception ex)
{
DebugLog.Log("UI", $"Avalonia menu fallback failed: {ex.Message}", LogLevel.Error);
}
}
private void OnSyncFilesChanged()
{
_orderSignal.Set();
try { SyncFilesChanged?.Invoke(); }
catch { }
}
private void WaitForOrderTick(CancellationToken token)
{
try { WaitHandle.WaitAny(new[] { token.WaitHandle, _orderSignal }, OrderFallbackMs); }
catch { token.WaitHandle.WaitOne(OrderFallbackMs); }
}
private void StartOrderWatcher()
{
_orderWatcherCts = new CancellationTokenSource();
CancellationToken token = _orderWatcherCts.Token;
_orderWatcher = Task.Run(() =>
{
while (!token.IsCancellationRequested)
{
try
{
string? me = OwnUsername;
if (me != null)
SyncClassTypeOption(me);
else
_classTypePushed = false;
CheckOptionsReload();
bool enabled = PluginEnabled;
if (enabled != _wasEnabledOrder)
{
_wasEnabledOrder = enabled;
if (!enabled)
{
DebugLog.Log("OrderWatcher", "ButlerV5 disabled - ceasing all activity (stop in place)");
Log("Butler disabled - stopped. Turn 'Enable Butler' back on to resume.");
_follower?.Stop();
}
else
{
DebugLog.Log("OrderWatcher", "ButlerV5 enabled - resuming");
Log("Butler enabled - resuming.");
}
}
if (!enabled)
{
WaitForOrderTick(token);
continue;
}
List<SyncData> allOnline = me != null ? Roster.GetOnline() : new();
List<SyncData> online = me != null
? allOnline.Where(account => !account.Username.Equals(me, StringComparison.OrdinalIgnoreCase)).ToList()
: new();
ProcessSendAllGotoCommands(me, allOnline);
ProcessServingHandshakes(me, online);
if (me != null && _follower != null && _follower.Source != FollowSource.Manual)
{
if (!GetOption("obeySummons", true))
{
if (_follower.Source == FollowSource.Ordered)
{
DebugLog.Log("OrderWatcher", "obeySummons turned off - dropping ordered follow");
_follower.Stop(park: true);
}
WaitForOrderTick(token);
continue;
}
SyncData? master = Roster.FindMasterOrdering(me, online);
if (master != null &&
CurrentFollowers.Any(f => f.Equals(master.Username, StringComparison.OrdinalIgnoreCase)))
{
DebugLog.Log("OrderWatcher", $"ignoring order from {master.Username}: they are in my own followers list");
master = null;
}
if (master != null &&
master.Username.Equals(_declinedMaster, StringComparison.OrdinalIgnoreCase))
{
master = null;
}
if (master != null)
{
_orderGoneSince = DateTime.MinValue;
if (IsScriptRunning)
{
if (!_busyOrderLogged)
{
_busyOrderLogged = true;
DebugLog.Log("OrderWatcher", $"order from {master.Username} on hold - script running here");
}
}
else if (!master.Username.Equals(_follower.Master, StringComparison.OrdinalIgnoreCase) || !_follower.IsRunning)
{
_busyOrderLogged = false;
DebugLog.Log("OrderWatcher", $"{master.Username} ordered {me} to follow - obeying");
_follower.Start(master.Username, FollowSource.Ordered);
}
}
else if (_follower.Source == FollowSource.Ordered && _follower.Master is { } curMaster)
{
SyncData? cur = online.FirstOrDefault(d => d.Username.Equals(curMaster, StringComparison.OrdinalIgnoreCase));
bool cleanlyReleased = cur != null &&
!cur.Followers.Any(f => f.Equals(me, StringComparison.OrdinalIgnoreCase));
if (cleanlyReleased)
{
_orderGoneSince = DateTime.MinValue;
DebugLog.Log("OrderWatcher", $"{curMaster} released me - stopping and parking");
_follower.Stop(park: true);
}
else if (_orderGoneSince == DateTime.MinValue)
_orderGoneSince = DateTime.UtcNow;
else if ((DateTime.UtcNow - _orderGoneSince).TotalSeconds >= 2.5)
{
_orderGoneSince = DateTime.MinValue;
DebugLog.Log("OrderWatcher", "order gone 2.5s (master offline/unreadable) - stopping and parking");
_follower.Stop(park: true);
}
}
else
{
_orderGoneSince = DateTime.MinValue;
}
}
}
catch
{
}
WaitForOrderTick(token);
}
}, token);
}
private void ProcessSendAllGotoCommands(string? me, List<SyncData> online)
{
if (string.IsNullOrWhiteSpace(me) || online.Count == 0 || !GetOption("obeySummons", true))
return;
long nowTicks = DateTime.UtcNow.Ticks;
long maxAgeTicks = TimeSpan.FromSeconds(60).Ticks;
string myServer = MyServerName;
foreach (SyncData command in online)
{
if (string.IsNullOrWhiteSpace(command.SendAllCommand) ||
string.IsNullOrWhiteSpace(command.SendAllTarget) ||
command.SendAllIssuedUtcTicks <= 0 ||
!command.SendAllRecipients.Any(name => name.Equals(me, StringComparison.OrdinalIgnoreCase)))
continue;
long ageTicks = nowTicks - command.SendAllIssuedUtcTicks;
if (ageTicks < 0 || ageTicks > maxAgeTicks)
continue;
string commandKey = $"{command.Username}:{command.SendAllCommand}";
if (!_processedSendAllCommands.Add(commandKey))
continue;
_processedSendAllCommandOrder.Enqueue(commandKey);
while (_processedSendAllCommandOrder.Count > 64)
_processedSendAllCommands.Remove(_processedSendAllCommandOrder.Dequeue());
if (IsScriptRunning)
{
DebugLog.Log("OrderWatcher", $"ignored send-all goto from {command.Username}: a script is running");
continue;
}
if (!string.IsNullOrEmpty(command.Server) && !string.IsNullOrEmpty(myServer) &&
!command.Server.Equals(myServer, StringComparison.OrdinalIgnoreCase))
{
DebugLog.Log("OrderWatcher", $"ignored send-all goto from {command.Username}: server mismatch");
continue;
}
try
{
_bot?.Player?.Goto(command.SendAllTarget);
DebugLog.Log("OrderWatcher", $"goto {command.SendAllTarget} requested by {command.Username}");
Log($"Sent to external player {command.SendAllTarget} on request from {command.Username}.",
"Sent to an external player.");
}
catch (Exception ex)
{
DebugLog.Log("OrderWatcher", $"send-all goto failed for {command.SendAllTarget}: {ex.Message}");
}
}
}
private void SetOptionQuiet(string name, string value)
{
try
{
IOptionContainer? oc = _container?.OptionContainer;
IOption? option = oc?.Options.FirstOrDefault(o => o.Name == name);
if (oc != null && option != null)
oc.OptionValues[option] = value;
}
catch (Exception ex)
{
DebugLog.Log("Plugin", $"quiet option set failed: {ex.Message}");
}
}
private void ProcessServingHandshakes(string? me, List<SyncData> online)
{
try
{
if (me == null)
return;
if (_declinedMaster != null)
{
SyncData? masterFile = online.FirstOrDefault(d => d.Username.Equals(_declinedMaster, StringComparison.OrdinalIgnoreCase));
if (masterFile == null ||
!masterFile.Followers.Any(f => f.Equals(me, StringComparison.OrdinalIgnoreCase)))
{
DebugLog.Log("OrderWatcher", $"decline of {_declinedMaster} acknowledged - cleared");
_declinedMaster = null;
}
}
if (CurrentFollowers.Count > 0)
{
foreach (SyncData d in online)
{
if (d.Declined.Equals(me, StringComparison.OrdinalIgnoreCase) &&
CurrentFollowers.Any(f => f.Equals(d.Username, StringComparison.OrdinalIgnoreCase)))
{
Log($"{d.Username} declined the summon - released.",
"A butler declined the summon - released.");
ToggleFollower(d.Username);
}
}
}
}
catch (Exception ex)
{
DebugLog.Log("OrderWatcher", $"handshake processing failed: {ex.Message}");
}
}
private bool GetOption(string name, bool fallback)
{
try
{
return _container?.OptionContainer?.Get<bool>(name) ?? fallback;
}
catch
{
return fallback;
}
}
private int GetOptionInt(string name, int fallback)
{
try
{
int value = _container?.OptionContainer?.Get<int>(name) ?? fallback;
return value > 0 ? value : fallback;
}
catch
{
return fallback;
}
}
private string GetOptionString(string name, string fallback)
{
try
{
return _container?.OptionContainer?.Get<string>(name) ?? fallback;
}
catch
{
return fallback;
}
}
private T GetOptionEnum<T>(string name, T fallback) where T : struct, Enum
{
try
{
return _container?.OptionContainer?.Get<T>(name) ?? fallback;
}
catch
{
return fallback;
}
}
public int RosterRefreshSeconds => GetOptionInt("rosterRefresh", 2);
private bool _classTypePushed;
private ButlerClassType _lastUiClassType;
private DateTime _lastOptionsFileTime = DateTime.MinValue;
private void CheckOptionsReload()
{
try
{
string? path = _container?.OptionsFile;
if (path == null || !File.Exists(path))
return;
DateTime fileTime = File.GetLastWriteTimeUtc(path);
if (_lastOptionsFileTime == DateTime.MinValue)
{
_lastOptionsFileTime = fileTime;
return;
}
if (fileTime <= _lastOptionsFileTime)
return;
_lastOptionsFileTime = fileTime;
_container!.OptionContainer.Load();
if (_classTypePushed)
SetOptionQuiet("classType", _lastUiClassType.ToString());
DebugLog.Log("Plugin", "options file changed on disk - reloaded (live settings sync)");
Log("Settings reloaded (changed from another client).");
}
catch (Exception ex)
{
DebugLog.Log("Plugin", $"options reload failed: {ex.Message}");
}
}
private void SyncClassTypeOption(string me)
{
try
{
if (!_classTypePushed)
{
_lastUiClassType = AccountConfig.GetClassType(me);
SetOptionQuiet("classType", _lastUiClassType.ToString());
_classTypePushed = true;
DebugLog.Log("Plugin", $"class type for {me} loaded: {_lastUiClassType}");
return;
}
ButlerClassType ui = GetOptionEnum("classType", _lastUiClassType);
if (ui != _lastUiClassType)
{
_lastUiClassType = ui;
AccountConfig.SetClassType(me, ui);
DebugLog.Log("Plugin", $"class type for {me} -> {ui} (changed in options window)");
}
}
catch
{
}
}
private void Log(string message)
=> Log(message, message);
private void Log(string message, string scriptMessage)
{
DebugLog.Log("ButlerV5", message);
try
{
_bot?.Log($"[Butler] {scriptMessage}");
}
catch
{
}
}
}
