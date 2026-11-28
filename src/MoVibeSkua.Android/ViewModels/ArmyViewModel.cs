using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using MoVibeSkua.Android.Platform;
using MoVibeSkua.Android.Usage;
using Skua.Core.Interfaces;
using Skua.Core.Models;
namespace MoVibeSkua.Android.ViewModels;
public sealed partial class ArmyViewModel : ObservableRecipient
{
private readonly Func<bool> _isStreamerMode;
private readonly IDialogService _dialogs;
private readonly DispatcherTimer _timer;
private (int Slot, bool Running)? _awaiting;
private bool _lowMemory;
private readonly Action<Action<string>> _pickScript;
public ArmyViewModel(Func<bool> isStreamerMode, IDialogService dialogs, Action<Action<string>> pickScript)
{
_pickScript = pickScript;
_isStreamerMode = isStreamerMode;
_dialogs = dialogs;
_timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
_timer.Tick += (_, _) => Refresh();
}
private static Context Context => global::Android.App.Application.Context;
public ObservableCollection<ArmyClientItem> Clients { get; } = new();
public string ThisClient => $"You are on {Army.Label(Army.Slot)}.";
[ObservableProperty]
private string _memoryText = string.Empty;
[ObservableProperty]
private string _warning = string.Empty;
[ObservableProperty]
private string _status = string.Empty;
[ObservableProperty]
private bool _canAdd = true;
protected override void OnActivated()
{
Refresh();
_timer.Start();
}
protected override void OnDeactivated() => _timer.Stop();
[RelayCommand]
private void OpenButler()
{
UsageTracker.Feature("armyButler");
IPluginManager? manager = Ioc.Default.GetService<IPluginManager>();
IPluginContainer? container = manager?.Containers.FirstOrDefault(c =>
c.Plugin.Name.Equals("Butler", StringComparison.OrdinalIgnoreCase) ||
c.Plugin.GetType().Name.Contains("Butler", StringComparison.OrdinalIgnoreCase));
if (container is null)
{
Status = "Butler plugin is not loaded. Check Menu -> Manage Plugins.";
return;
}
MethodInfo? openWindow = container.Plugin.GetType().GetMethod(
"OpenWindow",
BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
Type.EmptyTypes);
openWindow?.Invoke(container.Plugin, null);
}
[RelayCommand]
private void Add()
{
int? slot = Army.NextFreeSlot(Context);
if (slot is null)
{
UsageTracker.Track(Signal.ARMY_FULL);
Status = $"All {Army.MaxClients} clients are running.";
return;
}
Status = $"Starting {Army.Label(slot.Value)}... log in to its account there.";
_awaiting = (slot.Value, true);
UsageTracker.Track(Signal.ARMY_ADD);
Army.Open(Context, slot.Value);
}
[RelayCommand]
private void SwitchTo(ArmyClientItem? client)
{
if (client is null || client.IsThis)
return;
UsageTracker.Track(Signal.ARMY_SWITCH);
Army.Open(Context, client.Slot);
}
[RelayCommand]
private void Close(ArmyClientItem? client)
{
if (client is null || client.Slot == 1)
return;
if (_dialogs.ShowMessageBox($"Close {client.Title}? It logs out of the game and stops its script.", "Army", true) != true)
return;
Status = $"Closing {client.Title}...";
_awaiting = (client.Slot, false);
UsageTracker.Track(Signal.ARMY_CLOSE);
Army.Close(Context, client.Slot);
}
private const string ShellPrefs = "movibeskua.shell";
private const string TeamPrefPrefix = "runall.team.";
private const long ReserveMb = 600;
private readonly HashSet<string> _runIds = new();
private CancellationTokenSource? _runCts;
private string _runScriptName = string.Empty;
public ObservableCollection<RunAllTeamItem> Team { get; } = new();
[ObservableProperty]
private bool _isPickerOpen;
[ObservableProperty]
private bool _isRunning;
[ObservableProperty]
private string _runScript = string.Empty;
[ObservableProperty]
private string _runScriptHint = string.Empty;
[ObservableProperty]
private string _teamTitle = "2. CLIENTS";
[ObservableProperty]
private string _roomText = string.Empty;
[ObservableProperty]
private string _runTitle = "Start one army script on every account at once.";
[ObservableProperty]
private string _runDetail = string.Empty;
public string RunScriptName => string.IsNullOrEmpty(RunScript) ? "No script chosen" : Path.GetFileName(RunScript);
public string OptionsHint => "Each client uses its own script options.";
partial void OnRunScriptChanged(string value)
{
OnPropertyChanged(nameof(RunScriptName));
if (IsPickerOpen)
BuildTeam();
}
[RelayCommand]
private void OpenRunAll()
{
if (IsRunning)
return;
UsageTracker.Feature("armyRunAll");
string? loaded = Ioc.Default.GetService<IScriptManager>()?.LoadedScript;
if (!string.IsNullOrWhiteSpace(loaded) && (string.IsNullOrEmpty(RunScript) || loaded != _loadedAtLastOpen))
RunScript = loaded;
_loadedAtLastOpen = loaded;
RunScriptHint = !string.IsNullOrWhiteSpace(loaded) && RunScript == loaded
? "Default: the script loaded on this client"
: "Choose the army script every account runs";
LoadRecent();
BuildTeam();
IsPickerOpen = true;
}
private string? _loadedAtLastOpen;
[RelayCommand]
private void CancelRunAll() => IsPickerOpen = false;
[RelayCommand]
private void ChooseScript() => _pickScript(path =>
{
RunScript = path;
RunScriptHint = "Chosen for this run";
});
private const string RecentKey = "runall.recent";
public ObservableCollection<RecentScriptItem> RecentScripts { get; } = new();
public bool HasRecentScripts => RecentScripts.Count > 0;
private void LoadRecent()
{
RecentScripts.Clear();
string? stored = Prefs?.GetString(RecentKey, null);
foreach (string path in (stored ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries))
{
if (File.Exists(path))
RecentScripts.Add(new RecentScriptItem(path));
}
OnPropertyChanged(nameof(HasRecentScripts));
}
private static void RememberRecent(string script)
{
try
{
string? stored = Prefs?.GetString(RecentKey, null);
List<string> list = (stored ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries)
.Where(p => !p.Equals(script, StringComparison.OrdinalIgnoreCase)).Prepend(script).Take(5).ToList();
Prefs?.Edit()?.PutString(RecentKey, string.Join("\n", list))?.Apply();
}
catch
{
}
}
[RelayCommand]
private void UseRecent(RecentScriptItem? item)
{
if (item is null)
return;
RunScript = item.Path;
RunScriptHint = "Recent army script";
}
private void BuildTeam()
{
Team.Clear();
bool masked = _isStreamerMode();
Dictionary<int, Army.ClientStatus> statuses = LiveStatuses();
List<SavedAccount> saved = SavedAccounts.Load();
if (saved.Count > 0)
{
TeamTitle = "2. ARMY TEAM (saved accounts)";
HashSet<string>? remembered = RememberedTeam(RunScript);
foreach (SavedAccount account in saved)
{
int? slot = statuses.Values.FirstOrDefault(st => st.LoggedIn && st.Name.Equals(account.Username, StringComparison.OrdinalIgnoreCase))?.Slot;
bool pick = remembered is null ? slot is not null : remembered.Contains(Hash(account.Username));
string sub = slot is { } s ? $"In game on {Army.Label(s)} · reused" : "Opens a new client and logs in";
Team.Add(new RunAllTeamItem(account.Title, sub, pick, true, account.Username, slot, this));
}
}
else
{
TeamTitle = "2. CLIENTS";
foreach (ArmyClientItem client in Clients)
{
Army.ClientStatus? status = statuses.GetValueOrDefault(client.Slot);
bool loggedIn = status?.LoggedIn == true;
string name = !loggedIn ? Army.Label(client.Slot) : masked ? Platform.Streamer.Alias(client.Slot) : status!.Name;
string sub = loggedIn ? $"{Army.Label(client.Slot)} · in game" : "Not logged in";
Team.Add(new RunAllTeamItem(name, sub, loggedIn, loggedIn, null, client.Slot, this));
}
}
UpdateRoom();
}
internal void UpdateRoom()
{
int extra = Team.Count(t => t.IsChecked && t.Slot is null);
int room = RoomForClients();
RoomText = extra == 0
? string.Empty
: $"Room for {room} more client{(room == 1 ? "" : "s")} ({AvailMb() / 1024d:0.0} GB free, ~{PerClientMb() / 1024d:0.0} GB each)."
+ (extra > room ? $" {extra} picked: Start asks before opening fewer." : string.Empty);
}
private static Dictionary<int, Army.ClientStatus> LiveStatuses()
{
Dictionary<int, Army.ClientStatus> statuses = new();
foreach (int slot in Army.RunningClients(Context).Keys.Append(Army.Slot).Distinct())
{
if (Army.ReadStatus(slot) is { } status)
statuses[slot] = status;
}
return statuses;
}
private static long AvailMb()
{
if (Context.GetSystemService(Context.ActivityService) is not ActivityManager am)
return 0;
ActivityManager.MemoryInfo memory = new();
am.GetMemoryInfo(memory);
return memory.AvailMem / (1024 * 1024);
}
private static long PerClientMb() => DeviceProfile.Current.Tier == DeviceProfile.Tiers.Low ? 800 : 700;
private static int RoomForClients()
{
Dictionary<int, int> running = Army.RunningClients(Context);
running[Army.Slot] = 0;
int free = Army.MaxClients - running.Count;
long fit = Math.Max(0, (AvailMb() - ReserveMb) / PerClientMb());
return (int)Math.Max(0, Math.Min(free, fit));
}
private static string Hash(string username) =>
Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(username.Trim().ToLowerInvariant())))[..16];
private static ISharedPreferences? Prefs => Context.GetSharedPreferences(ShellPrefs, FileCreationMode.Private);
private static string TeamKey(string script) => TeamPrefPrefix + Path.GetFileName(script).ToLowerInvariant();
private static HashSet<string>? RememberedTeam(string script)
{
if (string.IsNullOrEmpty(script))
return null;
string? stored = Prefs?.GetString(TeamKey(script), null);
return stored?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
}
private static void RememberTeam(string script, IEnumerable<string> usernames)
{
try
{
Prefs?.Edit()?.PutString(TeamKey(script), string.Join(",", usernames.Select(Hash)))?.Apply();
}
catch
{
}
}
private readonly Dictionary<int, string> _localFailures = new();
private readonly List<(RunAllTeamItem Member, string Reason)> _failedMembers = new();
[ObservableProperty]
private bool _canRetry;
[RelayCommand]
private async Task StartRunAll()
{
if (IsRunning)
return;
if (string.IsNullOrWhiteSpace(RunScript) || !File.Exists(RunScript))
{
_dialogs.ShowMessageBox("Choose a script first.", "Run on all clients");
return;
}
List<RunAllTeamItem> picked = Team.Where(t => t.IsChecked && t.IsEnabled).ToList();
if (picked.Count == 0)
{
_dialogs.ShowMessageBox("Pick at least one account.", "Run on all clients");
return;
}
List<Skua.Core.Scripts.ScriptTrust.Finding> unofficial = Skua.Core.Scripts.ScriptTrust.Check(new[] { RunScript },
Ioc.Default.GetService<IGetScriptsService>() is { } repo ? Skua.Core.Models.GitHub.ScriptInfo.Snapshot(repo.Scripts) : null);
if (unofficial.Count > 0)
{
if (_dialogs.ShowMessageBox(Skua.Core.Scripts.ScriptTrust.Describe(unofficial), "Unofficial script", true) != true)
return;
Skua.Core.Scripts.ScriptTrust.Approve(unofficial);
ArmyRunAll.LogStep(0, "trust", string.Empty, 1, 1, "ok", "ApprovedForAll", Path.GetFileName(RunScript));
}
List<SavedAccount> saved = SavedAccounts.Load();
string teamServer = picked.Select(t => saved.Find(a => a.Username.Equals(t.Username, StringComparison.OrdinalIgnoreCase))?.Server)
.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "Twilly";
int teamRoom = Random.Shared.Next(100000, 999999);
ArmyTeam.Set(teamServer, teamRoom, picked.Select(t => t.Username).Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u!));
List<RunAllTeamItem> toOpen = picked.Where(t => t.Slot is null).ToList();
List<(RunAllTeamItem, string)> noRoom = new();
int room = RoomForClients();
if (toOpen.Count > room)
{
string ask = room == 0
? $"There is no room for another client (phone memory is short). Run on the {picked.Count - toOpen.Count} account(s) already in game?"
: $"Room for {room} more client{(room == 1 ? "" : "s")}, {toOpen.Count} picked. Continue with {room}?";
if (_dialogs.ShowMessageBox(ask, "Run on all clients", true) != true)
return;
noRoom.AddRange(toOpen.Skip(room).Select(m => (m, "Not enough memory to open another client")));
foreach ((RunAllTeamItem m, string _) in noRoom)
ArmyRunAll.LogStep(0, "open", m.Name, 1, 1, "fail", "NoMemory", $"avail={AvailMb()}MB per={PerClientMb()}MB room={room}");
toOpen = toOpen.Take(room).ToList();
}
if (picked.Any(t => t.Username is not null))
RememberTeam(RunScript, picked.Where(t => t.Username is not null).Select(t => t.Username!));
IsPickerOpen = false;
RememberRecent(RunScript);
_runScriptName = Path.GetFileNameWithoutExtension(RunScript);
_localFailures.Clear();
_failedMembers.Clear();
_failedMembers.AddRange(noRoom);
UsageTracker.Feature("armyRunAllStart");
await RunTeam(toOpen, picked.Where(t => t.Slot is not null).Select(t => t.Slot!.Value).ToList());
}
[RelayCommand]
private async Task RetryFailed()
{
if (IsRunning || string.IsNullOrEmpty(RunScript))
return;
List<RunAllTeamItem> members = _failedMembers.Select(f => f.Member).ToList();
List<int> slots = Clients.Where(c => c.IsRunAllFailed && Army.ReadStatus(c.Slot)?.LoggedIn == true).Select(c => c.Slot).ToList();
if (members.Count == 0 && slots.Count == 0)
return;
Trace.WriteLine($"[RunAll] retry failed: {members.Count} account(s) to log in, clients {string.Join(",", slots)} to restart");
UsageTracker.Feature("armyRunAllRetry");
foreach (int slot in slots)
_localFailures.Remove(slot);
_failedMembers.Clear();
await RunTeam(members, slots);
}
private async Task RunTeam(List<RunAllTeamItem> toOpen, List<int> slots)
{
IsRunning = true;
CanRetry = false;
using CancellationTokenSource cts = new();
_runCts = cts;
Stopwatch total = Stopwatch.StartNew();
try
{
slots = new List<int>(slots);
for (int i = 0; i < toOpen.Count; i++)
{
(int? slot, string? reason) = await OpenAndLogin(toOpen[i], i + 1, toOpen.Count, cts.Token);
if (slot is { } s && reason is null)
slots.Add(s);
else
_failedMembers.Add((toOpen[i], reason ?? "Login did not complete"));
}
if (toOpen.Count > 0)
Army.Open(Context, Army.Slot);
slots = slots.Distinct().OrderBy(s => s).ToList();
if (slots.Count > 0)
await StartOn(slots, cts.Token);
else
RunTitle = $"{_runScriptName}: no client to run it on";
}
catch (OperationCanceledException)
{
RunTitle = "Run on all clients cancelled";
Trace.WriteLine("[RunAll] cancelled by the user");
}
finally
{
_runCts = null;
IsRunning = false;
Refresh();
Trace.WriteLine($"[RunAll] done ms={(int)total.ElapsedMilliseconds} failedAccounts={_failedMembers.Count} failedClients={Clients.Count(c => c.IsRunAllFailed)}");
}
}
private async Task StartOn(List<int> slots, CancellationToken token)
{
RunTitle = $"Starting {_runScriptName} on {slots.Count} client{(slots.Count == 1 ? "" : "s")}...";
int first = slots.Contains(Army.Slot) ? Army.Slot : slots[0];
List<int> rest = slots.Where(s => s != first).ToList();
RunDetail = rest.Count > 0
? $"Compiling on {Army.Label(first)} first; the others load its result. Butler following turned off."
: "Butler following turned off.";
await StartWave(new List<int> { first }, TimeSpan.FromSeconds(240), token);
if (rest.Count == 0)
return;
RunDetail = $"Starting the other {rest.Count} {ArmyRunAll.StaggerSeconds} s apart.";
await StartWave(rest, TimeSpan.FromSeconds(ArmyRunAll.StaggerSeconds * rest.Count + 150), token);
}
private async Task StartWave(List<int> slots, TimeSpan timeout, CancellationToken token)
{
List<int> pending = slots;
for (int attempt = 1; attempt <= 2 && pending.Count > 0; attempt++)
{
Stopwatch sw = Stopwatch.StartNew();
Army.Command command = Army.SendCommand(Army.ActionStart, RunScript, string.Empty, pending);
_runIds.Add(command.Id);
List<int> waiting = pending;
await WaitFor(() =>
{
Dictionary<int, int> alive = Army.RunningClients(Context);
alive[Army.Slot] = 0;
waiting = waiting.Where(s => !Confirmed(s, command.Id)).ToList();
return waiting.All(s => !alive.ContainsKey(s));
}, timeout, token);
Dictionary<int, int> running = Army.RunningClients(Context);
running[Army.Slot] = 0;
List<int> retry = new();
foreach (int slot in waiting)
{
if (!running.ContainsKey(slot))
{
_localFailures[slot] = "Client closed by Android (low memory)";
ArmyRunAll.LogStep(slot, "start-confirm", string.Empty, attempt, 2, "fail", "ProcessDied", "process gone while starting");
}
else if (attempt < 2)
{
retry.Add(slot);
ArmyRunAll.LogStep(slot, "start-confirm", string.Empty, attempt, 2, "retry", "NoConfirm", $"state={Army.ReadStatus(slot)?.RunAll}", sw.Elapsed);
}
else
{
_localFailures[slot] = "Script did not start in time (2 tries)";
ArmyRunAll.LogStep(slot, "start-confirm", string.Empty, attempt, 2, "fail", "NoConfirm", $"state={Army.ReadStatus(slot)?.RunAll}", sw.Elapsed);
}
}
foreach (int slot in pending.Except(waiting))
{
Army.ClientStatus? st = Army.ReadStatus(slot);
bool failed = st?.RunAll == "failed";
bool busy = failed && st!.RunAllError.Contains("files busy", StringComparison.OrdinalIgnoreCase);
if (busy && attempt < 2)
retry.Add(slot);
ArmyRunAll.LogStep(slot, "start-confirm", string.Empty, attempt, 2, !failed ? "ok" : busy && attempt < 2 ? "retry" : "fail",
failed ? (busy ? "FilesBusy" : "StartFailed") : string.Empty, st?.RunAllError ?? string.Empty, sw.Elapsed);
}
pending = retry;
if (pending.Count > 0)
await Task.Delay(TimeSpan.FromSeconds(5), token);
}
}
private async Task<(int? Slot, string? Reason)> OpenAndLogin(RunAllTeamItem member, int index, int count, CancellationToken token)
{
RunTitle = $"Opening clients {index}/{count}...";
int slot = 0;
bool opened = false;
for (int attempt = 1; attempt <= 2 && !opened; attempt++)
{
if (RoomForClients() == 0 || Army.NextFreeSlot(Context) is not { } free)
{
ArmyRunAll.LogStep(0, "open", member.Name, attempt, 2, "fail", "NoMemory", $"avail={AvailMb()}MB per={PerClientMb()}MB");
return (null, "Not enough memory to open another client");
}
slot = free;
RunDetail = $"Starting {Army.Label(slot)} for {member.Name}" + (attempt > 1 ? " (try 2 of 2)" : string.Empty);
Stopwatch sw = Stopwatch.StartNew();
ArmyRunAll.LogStep(slot, "open", member.Name, attempt, 2, "begin");
Army.ClearStatus(slot);
UsageTracker.Track(Signal.ARMY_ADD);
Army.Open(Context, slot);
opened = await WaitFor(() => Army.ReadStatus(slot) is not null, TimeSpan.FromSeconds(75), token);
if (opened)
{
ArmyRunAll.LogStep(slot, "open", member.Name, attempt, 2, "ok", took: sw.Elapsed);
break;
}
bool alive = Army.RunningClients(Context).ContainsKey(slot);
ArmyRunAll.LogStep(slot, "open", member.Name, attempt, 2, attempt < 2 ? "retry" : "fail", alive ? "OpenTimeout" : "ProcessDied",
alive ? "no status from the client within 75 s" : "process gone while opening", sw.Elapsed);
Army.Close(Context, slot);
await Task.Delay(TimeSpan.FromSeconds(3), token);
}
if (!opened)
return (null, "Client did not open (2 tries)");
await Task.Delay(TimeSpan.FromSeconds(3), token);
string reason = "Login did not complete";
for (int attempt = 1; attempt <= 2; attempt++)
{
RunDetail = $"Logging in {member.Name} on {Army.Label(slot)} (try {attempt} of 2)";
Stopwatch sw = Stopwatch.StartNew();
ArmyRunAll.LogStep(slot, "login", member.Name, attempt, 2, "begin");
Army.Command command = Army.SendCommand(Army.ActionLogin, string.Empty, member.Username!, new[] { slot });
_runIds.Add(command.Id);
Army.ClientStatus? status = null;
bool died = false;
bool answered = await WaitFor(() =>
{
if (!Army.RunningClients(Context).ContainsKey(slot))
{
died = true;
return true;
}
status = Army.ReadStatus(slot);
return status?.RunAllId == command.Id && status.RunAll is "in game" or "failed";
}, TimeSpan.FromSeconds(300), token);
if (died)
{
_localFailures[slot] = "Client closed by Android (low memory)";
ArmyRunAll.LogStep(slot, "login", member.Name, attempt, 2, "fail", "ProcessDied", "process gone while logging in", sw.Elapsed);
return (slot, _localFailures[slot]);
}
if (answered && status?.RunAll == "in game")
{
ArmyRunAll.LogStep(slot, "login", member.Name, attempt, 2, "ok", took: sw.Elapsed);
_localFailures.Remove(slot);
await Task.Delay(TimeSpan.FromSeconds(5), token);
return (slot, null);
}
bool final = attempt == 2;
if (!answered)
{
reason = "Login timed out (2 tries)";
ArmyRunAll.LogStep(slot, "login", member.Name, attempt, 2, final ? "fail" : "retry", "Timeout", "no answer from the client within 300 s", sw.Elapsed);
}
else
{
reason = status!.RunAllError.Length > 0 ? status.RunAllError : "Login did not complete";
if (reason == "Login timed out")
reason = "Login timed out (2 tries)";
if (reason.StartsWith("Account not saved", StringComparison.Ordinal))
final = true;
ArmyRunAll.LogStep(slot, "login", member.Name, attempt, 2, final ? "fail" : "retry", "Refused", reason, sw.Elapsed);
}
if (final)
break;
await Task.Delay(TimeSpan.FromSeconds(8), token);
}
_localFailures[slot] = reason;
return (slot, reason);
}
private static bool Confirmed(int slot, string id) =>
Army.ReadStatus(slot) is { } st && st.RunAllId == id && st.RunAll is "running" or "failed" or "stopped";
private static async Task<bool> WaitFor(Func<bool> done, TimeSpan timeout, CancellationToken token)
{
DateTime until = DateTime.UtcNow + timeout;
while (DateTime.UtcNow < until)
{
token.ThrowIfCancellationRequested();
if (await Task.Run(done, token))
return true;
await Task.Delay(1000, token);
}
return false;
}
[RelayCommand]
private void CancelRun() => _runCts?.Cancel();
[RelayCommand]
private void StopRunAll()
{
List<int> slots = Clients.Select(c => c.Slot).ToList();
if (_dialogs.ShowMessageBox($"Stop the script on all {slots.Count} clients?", "Run on all clients", true) != true)
return;
_runCts?.Cancel();
UsageTracker.Feature("armyRunAllStop");
Army.Command command = Army.SendCommand(Army.ActionStop, string.Empty, string.Empty, slots);
_runIds.Add(command.Id);
_failedMembers.Clear();
_localFailures.Clear();
RunTitle = "Stopped the script on all clients";
RunDetail = string.Empty;
CanRetry = false;
}
private void RefreshRunAllSummary()
{
CanRetry = !IsRunning && _runScriptName.Length > 0 && (_failedMembers.Count > 0 || Clients.Any(c => c.IsRunAllFailed));
if (IsRunning || _runScriptName.Length == 0 || RunTitle.StartsWith("Stopped", StringComparison.Ordinal))
return;
List<ArmyClientItem> failedClients = Clients.Where(c => c.IsRunAllFailed).ToList();
List<string> lines = _failedMembers.Select(f => $"{f.Member.Name}: {f.Reason}")
.Concat(failedClients.Where(c => !_failedMembers.Any(f => c.RunAllText == f.Reason)).Select(c => $"{c.Title}: {c.RunAllText}"))
.Distinct().ToList();
int failed = lines.Count;
int running = Clients.Count(c => c.RunAllText == "Run on all: running");
RunTitle = failed > 0
? $"{_runScriptName}: {failed} failed, {running} running"
: running > 0 ? $"{_runScriptName} on {running} client{(running == 1 ? "" : "s")}" : RunTitle;
RunDetail = lines.Count > 0 ? string.Join("\n", lines) : $"Started {ArmyRunAll.StaggerSeconds} s apart. Butler following turned off.";
}
private void Refresh()
{
Dictionary<int, int> running = Army.RunningClients(Context);
running[Army.Slot] = global::Android.OS.Process.MyPid();
bool masked = _isStreamerMode();
if (_awaiting is { } awaiting && running.ContainsKey(awaiting.Slot) == awaiting.Running)
{
Status = string.Empty;
_awaiting = null;
}
List<int> slots = running.Keys.OrderBy(s => s).ToList();
if (!Clients.Select(c => c.Slot).SequenceEqual(slots))
{
Clients.Clear();
foreach (int slot in slots)
Clients.Add(new ArmyClientItem(slot));
}
foreach (ArmyClientItem client in Clients)
client.Update(Army.ReadStatus(client.Slot), masked, _runIds, _localFailures.GetValueOrDefault(client.Slot));
RefreshRunAllSummary();
CanAdd = slots.Count < Army.MaxClients;
Warning = slots.Count >= Army.ComfortableClients
? $"{slots.Count} clients: each one is a full copy of AQW. Past {Army.ComfortableClients} the phone may run short of memory or get hot, and Android may close clients to cope."
: string.Empty;
if (Context.GetSystemService(Context.ActivityService) is ActivityManager am)
{
ActivityManager.MemoryInfo memory = new();
am.GetMemoryInfo(memory);
const double gb = 1024d * 1024 * 1024;
MemoryText = $"Phone memory free: {memory.AvailMem / gb:0.0} of {memory.TotalMem / gb:0.0} GB";
if (memory.LowMemory && !_lowMemory)
UsageTracker.Track(Signal.ARMY_LOW_MEMORY);
_lowMemory = memory.LowMemory;
if (memory.LowMemory)
Warning = "The phone is low on memory: another client may not fit, and Android may close some.";
}
}
}
public sealed partial class ArmyClientItem : ObservableObject
{
public ArmyClientItem(int slot)
{
Slot = slot;
}
public int Slot { get; }
public bool IsThis => Slot == Army.Slot;
public string Title => Army.Label(Slot);
public string Badge => IsThis ? "This one" : string.Empty;
public bool CanSwitch => !IsThis;
public bool CanClose => Slot != 1;
[ObservableProperty]
private string _detail = "Starting...";
[ObservableProperty]
private string _scriptText = string.Empty;
[ObservableProperty]
private bool _isScriptRunning;
[ObservableProperty]
private string _runAllText = string.Empty;
[ObservableProperty]
private bool _isRunAllFailed;
public void Update(Army.ClientStatus? status, bool masked, ISet<string> runIds, string? localFailure = null)
{
if (localFailure is not null)
{
RunAllText = localFailure;
IsRunAllFailed = true;
}
if (status is null)
{
Detail = "Starting...";
ScriptText = string.Empty;
IsScriptRunning = false;
if (localFailure is null)
{
RunAllText = string.Empty;
IsRunAllFailed = false;
}
return;
}
if (localFailure is not null)
return;
bool ours = status.RunAllId.Length > 0 && runIds.Contains(status.RunAllId);
IsRunAllFailed = ours && status.RunAll == "failed";
RunAllText = !ours ? string.Empty
: status.RunAll == "failed" ? (status.RunAllError.Length > 0 ? status.RunAllError : "Failed")
: status.RunAllError.Length > 0 ? status.RunAllError
: !status.LoggedIn && status.RunAll != "logging in" ? "Not logged in"
: $"Run on all: {status.RunAll}";
Detail = !status.LoggedIn
? "Not logged in"
: $"{(masked ? Platform.Streamer.Alias(Slot) : status.Name)} · {status.Map}";
string script = string.IsNullOrEmpty(status.Script) ? string.Empty : Path.GetFileNameWithoutExtension(status.Script);
ScriptText = status.ScriptRunning ? $"Running {script}" : script.Length > 0 ? $"Loaded {script}" : "No script loaded";
IsScriptRunning = status.ScriptRunning;
}
}
public sealed partial class RunAllTeamItem : ObservableObject
{
private readonly ArmyViewModel _owner;
public RunAllTeamItem(string name, string detail, bool isChecked, bool isEnabled, string? username, int? slot, ArmyViewModel owner)
{
Name = name;
Detail = detail;
_isChecked = isChecked && isEnabled;
IsEnabled = isEnabled;
Username = username;
Slot = slot;
_owner = owner;
}
public string Name { get; }
public string Detail { get; }
public bool IsEnabled { get; }
public string? Username { get; }
public int? Slot { get; }
[ObservableProperty]
private bool _isChecked;
partial void OnIsCheckedChanged(bool value) => _owner.UpdateRoom();
}
public sealed class RecentScriptItem
{
public RecentScriptItem(string path) => Path = path;
public string Path { get; }
public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);
}
