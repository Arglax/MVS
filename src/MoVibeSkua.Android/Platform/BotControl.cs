using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Android.Content;
using Android.OS;
using CommunityToolkit.Mvvm.DependencyInjection;
using Skua.Core.Interfaces;
using Skua.Flash.Ruffle;
using Trace = System.Diagnostics.Trace;
namespace MoVibeSkua.Android.Platform;
public static class BotControl
{
public const string ActionToggle = "com.arglax.movibeskua.action.TOGGLE_SCRIPT";
public const string ActionPause = "com.arglax.movibeskua.action.PAUSE_SCRIPT";
public const string ActionBotOnly = "com.arglax.movibeskua.action.BOT_ONLY";
public const string ActionClose = "com.arglax.movibeskua.action.CLOSE";
private static bool _started;
public static bool BotOnly { get; private set; }
public static event Action? Changed;
public static void Start()
{
if (_started)
return;
_started = true;
try
{
IScriptManager scripts = Ioc.Default.GetRequiredService<IScriptManager>();
scripts.PropertyChanged += (_, e) =>
{
if (e.PropertyName == nameof(IScriptManager.ScriptRunning) && scripts.ScriptRunning)
{
TakeSnapshot();
AskForNotifications();
}
if (e.PropertyName is nameof(IScriptManager.ScriptRunning) or nameof(IScriptManager.ScriptPaused) or nameof(IScriptManager.LoadedScript))
Changed?.Invoke();
};
IFlashUtil flash = Ioc.Default.GetRequiredService<IFlashUtil>();
flash.FlashCall += (function, args) =>
{
if (function == "mvsUiDebug" && args is { Length: > 0 })
{
string arg = args[0]?.ToString() ?? "";
Avalonia.Threading.Dispatcher.UIThread.Post(() =>
{
if (arg.StartsWith("hide:") || arg.StartsWith("show:"))
Views.MobileShellView.Instance?.SetUiDebugHidden(arg[5..], arg.StartsWith("hide:"));
else
Views.MobileShellView.Instance?.SetUiDebug(arg == "on");
});
}
else if (function == "mvsBotOnly" && args is { Length: > 0 })
Apply(args[0]?.ToString() is "on" or "true" or "True", fromPage: true);
else if (function == "loaded")
{
if (BotOnly)
Send(true);
if (_inBackground)
Ioc.Default.GetService<RuffleFlashUtil>()?.PostCommand("{\"t\":\"background\",\"v\":true}");
}
};
}
catch (Exception ex)
{
Trace.WriteLine($"[BotControl] {ex.Message}");
}
}
public static void SetBotOnly(bool on) => Apply(on, fromPage: false);
private static bool _inBackground;
public static bool InBackground => _inBackground;
public static void SetInBackground(bool background)
{
if (background == _inBackground)
return;
_inBackground = background;
Ioc.Default.GetService<RuffleFlashUtil>()?.PostCommand(background ? "{\"t\":\"background\",\"v\":true}" : "{\"t\":\"background\",\"v\":false}");
}
private static (long Gold, long Exp, int Drops, int Kills, int Quests, int Deaths) _base;
private static DateTime _runStarted = DateTime.UtcNow;
private static void TakeSnapshot()
{
try
{
IScriptBotStats stats = Ioc.Default.GetRequiredService<IScriptInterface>().Stats;
_base = (stats.Gold, stats.Experience, stats.Drops, stats.Kills, stats.QuestsCompleted, stats.Deaths);
_runStarted = DateTime.UtcNow;
}
catch (Exception ex)
{
Trace.WriteLine($"[BotControl] stats: {ex.Message}");
}
}
private static string Short(long n) => n switch
{
>= 10_000_000 => $"{n / 1_000_000.0:0.#}M",
>= 100_000 => $"{n / 1000.0:0}K",
>= 10_000 => $"{n / 1000.0:0.#}K",
_ => n.ToString("N0")
};
private static string Clock(TimeSpan t) => t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
public static (string Line, string Details) Farmed()
{
try
{
IScriptBotStats stats = Ioc.Default.GetRequiredService<IScriptInterface>().Stats;
long gold = stats.Gold - _base.Gold, exp = stats.Experience - _base.Exp;
int drops = stats.Drops - _base.Drops, kills = stats.Kills - _base.Kills;
int quests = stats.QuestsCompleted - _base.Quests, deaths = stats.Deaths - _base.Deaths;
string line = $"Gold +{Short(gold)} · XP +{Short(exp)} · Drops {drops}";
string details = $"Gold +{gold:N0} · XP +{exp:N0}\nDrops {drops} · Kills {kills} · Quests {quests} · Deaths {deaths}";
return (line, details);
}
catch
{
return ("", "");
}
}
private static bool _askedNotifications;
private static void AskForNotifications()
{
if (_askedNotifications || !OperatingSystem.IsAndroidVersionAtLeast(33))
return;
_askedNotifications = true;
global::Android.App.Activity? activity = MainActivity.Current;
activity?.RunOnUiThread(() =>
{
try
{
const string permission = "android.permission.POST_NOTIFICATIONS";
if (AndroidX.Core.Content.ContextCompat.CheckSelfPermission(activity, permission) != global::Android.Content.PM.Permission.Granted)
AndroidX.Core.App.ActivityCompat.RequestPermissions(activity, new[] { permission }, 4022);
}
catch (Exception ex)
{
Trace.WriteLine($"[BotControl] notifications: {ex.Message}");
}
});
}
private static void Apply(bool on, bool fromPage)
{
bool changed = on != BotOnly;
BotOnly = on;
if (!fromPage)
Send(on);
if (!changed)
return;
Usage.UsageTracker.Toggle("botOnly", on);
Trace.WriteLine($"[BotControl] bot only {(on ? "on" : "off")}");
if (on)
AskForNotifications();
global::Android.App.Activity? activity = MainActivity.Current;
activity?.RunOnUiThread(() =>
{
Services.PerformanceMode.SetLowRefresh(activity, on);
if (on)
AskToRunUnrestricted(activity);
});
Changed?.Invoke();
}
private static void Send(bool on)
=> Ioc.Default.GetService<RuffleFlashUtil>()?.PostCommand(on ? "{\"t\":\"botOnly\",\"v\":true}" : "{\"t\":\"botOnly\",\"v\":false}");
private static bool _askedUnrestricted;
private static void AskToRunUnrestricted(global::Android.App.Activity activity)
{
if (_askedUnrestricted)
return;
_askedUnrestricted = true;
try
{
if (activity.GetSystemService(Context.PowerService) is PowerManager power
&& !power.IsIgnoringBatteryOptimizations(activity.PackageName))
{
Intent ask = new(global::Android.Provider.Settings.ActionRequestIgnoreBatteryOptimizations);
ask.SetData(global::Android.Net.Uri.Parse("package:" + activity.PackageName));
activity.StartActivity(ask);
}
}
catch (Exception ex)
{
Trace.WriteLine($"[BotControl] battery optimisation: {ex.Message}");
}
}
public static (string Title, string Text, string Details, bool Running, bool Paused, bool Loaded) Describe()
{
try
{
IScriptManager scripts = Ioc.Default.GetRequiredService<IScriptManager>();
string? path = scripts.LoadedScript;
bool loaded = !string.IsNullOrWhiteSpace(path);
string name = loaded ? Path.GetFileNameWithoutExtension(path)! : "No script loaded";
bool running = scripts.ScriptRunning, paused = running && scripts.ScriptPaused;
string state = running ? (paused ? "Paused" : "Running") : loaded ? "Ready" : "Idle";
string where = "";
try
{
IScriptInterface bot = Ioc.Default.GetRequiredService<IScriptInterface>();
if (running && bot.Player.LoggedIn)
where = bot.Map.Name;
}
catch
{
}
string title = $"{name} · {state}" + (running ? " " + Clock(DateTime.UtcNow - _runStarted) : "") + (BotOnly ? " · bot only" : "");
if (!running && !scripts.ScriptRunning && _base == default && !loaded)
return (title, "Keeping the game awake in the background.", "", running, paused, loaded);
(string line, string details) = Farmed();
string text = running ? line : "Last run: " + line;
string more = details + (where.Length > 0 ? $"\n{where}" : "");
return (title, text, more, running, paused, loaded);
}
catch
{
return ("MVS is running", "Keeping the game awake in the background.", "", false, false, false);
}
}
public static void Handle(string action)
{
IScriptManager? scripts = Ioc.Default.GetService<IScriptManager>();
if (scripts is null)
return;
Trace.WriteLine($"[BotControl] notification: {action[(action.LastIndexOf('.') + 1)..]}");
switch (action)
{
case ActionToggle:
Usage.UsageTracker.Feature("notifScriptToggle");
_ = Task.Run(async () =>
{
try
{
if (scripts.ScriptRunning)
{
await scripts.StopScript();
}
else if (!string.IsNullOrWhiteSpace(scripts.LoadedScript))
{
Exception? ex = await scripts.StartScript();
if (ex is not null && Skua.App.Avalonia.ViewModels.ScriptLoaderViewModel.StartErrorHandler?.Invoke(ex) != true)
Trace.WriteLine($"[BotControl] start: {ex.Message}");
}
}
catch (Exception ex)
{
Trace.WriteLine($"[BotControl] start/stop: {ex.Message}");
}
Changed?.Invoke();
});
break;
case ActionPause:
Usage.UsageTracker.Feature("notifPause");
if (scripts.ScriptRunning)
scripts.ScriptPaused = !scripts.ScriptPaused;
Changed?.Invoke();
break;
case ActionBotOnly:
Usage.UsageTracker.Feature("notifBotOnly");
SetBotOnly(!BotOnly);
break;
case ActionClose:
Usage.UsageTracker.Feature("notifClose");
_ = Task.Run(async () =>
{
try
{
if (scripts.ScriptRunning)
await scripts.StopScript().AsTask().WaitAsync(TimeSpan.FromSeconds(8));
}
catch (Exception ex)
{
Trace.WriteLine($"[BotControl] close: {ex.Message}");
}
Quit();
});
break;
}
}
private static void Quit()
{
Context context = global::Android.App.Application.Context;
try
{
foreach (int slot in Army.RunningClients(context).Keys)
{
if (slot > 1)
KeepAliveService.Release(context, slot);
}
}
catch
{
}
new Handler(Looper.MainLooper!).PostDelayed(() =>
{
try { KeepAliveService.Stop(context); } catch { }
try { MainActivity.Current?.FinishAndRemoveTask(); } catch { }
new Handler(Looper.MainLooper!).PostDelayed(() => global::Android.OS.Process.KillProcess(global::Android.OS.Process.MyPid()), 400);
}, 1800);
}
}
