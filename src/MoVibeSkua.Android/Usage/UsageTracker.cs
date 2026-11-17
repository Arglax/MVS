using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;
using Android.Content.Res;
using CommunityToolkit.Mvvm.Messaging;
using MoVibeSkua.Android.Platform;
using Skua.Core.Messaging;
namespace MoVibeSkua.Android.Usage;
public static class UsageTracker
{
private const int SaveEvery = 5;
private static readonly TimeSpan FirstUpload = TimeSpan.FromSeconds(30);
private static bool _waitingAtStart;
private static readonly TimeSpan BackgroundUploadGap = TimeSpan.FromMinutes(5);
private static readonly SignalCounters Counters = new();
private static readonly Extremes Peaks = new();
private static readonly VisitClock Clock = new();
private static readonly DayClock Foreground = new("fg");
private static readonly DayClock Alive = new("run");
private static readonly DayClock Botting = new("bot");
private static bool _scriptRunning;
private static bool _autoRunning;
private static readonly object FileGate = new();
private static readonly SemaphoreSlim UploadGate = new(1, 1);
private static readonly object Recipient = new();
private static int _sinceSave;
private static bool _enabled = true;
private static bool _loaded;
private static bool _started;
private static Screen _screen = Screen.GAME;
public static Screen CurrentScreen => _screen;
private static long _lastUpload;
private static string PendingPath => Path.Combine(UsageSync.Folder, $"pending-{Army.Slot}.json");
private static string PeaksPath => Path.Combine(UsageSync.Folder, $"peaks-{Army.Slot}.json");
public static bool OptedIn
{
get
{
try
{
return !(global::Android.App.Application.Context.GetSharedPreferences("movibeskua.usage", FileCreationMode.Private)?.GetBoolean("optOut", false) ?? false);
}
catch
{
return true;
}
}
set
{
try
{
global::Android.App.Application.Context.GetSharedPreferences("movibeskua.usage", FileCreationMode.Private)?.Edit()?.PutBoolean("optOut", !value)?.Apply();
if (!value)
{
_enabled = false;
Counters.Clear();
}
}
catch
{
}
}
}
public static void Start(Context context)
{
try
{
if (_started)
return;
_started = true;
if (!UsageSync.Configured || !OptedIn)
{
_enabled = false;
Counters.Clear();
return;
}
Load();
HookCrashes();
HookMessages();
HookAuto();
HookGame();
Alive.Start();
if (Army.IsFirst)
{
Track(Signal.SYS_SESSION_START);
Health("sessions");
}
else
{
Track(Signal.ARMY_CLIENT_START);
Track(ArmySize(Army.RunningClients(context).Count));
}
Track(context.Resources?.Configuration?.Orientation == Orientation.Portrait ? Signal.SYS_LAYOUT_PORTRAIT : Signal.SYS_LAYOUT_LANDSCAPE);
EnterScreen(_screen);
_ = Task.Run(UploadLoop);
}
catch
{
}
}
public static void Track(Signal signal)
{
try
{
if (!_enabled)
return;
if (Counters.Track(signal.ToString()))
Clock.Action();
if (Interlocked.Increment(ref _sinceSave) >= SaveEvery)
Save();
}
catch
{
}
}
public static void Feature(string id)
{
try
{
if (!_enabled || !Features.IsUse(id))
return;
if (Counters.Track("f." + id))
Clock.Action();
if (Interlocked.Increment(ref _sinceSave) >= SaveEvery)
Save();
}
catch
{
}
}
public static void Toggle(string id, bool on)
{
try
{
if (!_enabled || !Features.IsSwitch(id))
return;
if (Counters.Track($"f.{id}.{(on ? "on" : "off")}"))
Clock.Action();
if (Interlocked.Increment(ref _sinceSave) >= SaveEvery)
Save();
}
catch
{
}
}
public static void Health(string name, long delta = 1)
{
try
{
if (_enabled && delta > 0 && UsageIds.IsV2("h." + name))
Counters.AddRaw("h." + name, delta);
}
catch
{
}
}
public static void OnLowMemory() => Health("lowMemory");
public static void PerfSample(DateTime utcNow, double fps, int fpsLow, int fpsHigh, int appMb, double ramShare, double battery, bool hot)
{
try
{
if (!_enabled)
return;
string day = $"d{utcNow:yyyyMMdd}";
long fpsHundredths = (long)Math.Round(fps * UsageIds.Scale);
long shareHundredths = (long)Math.Round(ramShare * UsageIds.Scale);
Counters.AddRaw("p.n", 1);
Counters.AddRaw("p.fpsSum", fpsHundredths);
Counters.AddRaw("p.ramSum", appMb);
Counters.AddRaw("p.ramPctSum", shareHundredths);
if (hot)
Counters.AddRaw("p.hot", 1);
Counters.AddRaw($"pd.{day}.n", 1);
Counters.AddRaw($"pd.{day}.fpsSum", fpsHundredths);
Counters.AddRaw($"pd.{day}.ramSum", appMb);
Counters.AddRaw($"pd.{day}.ramPctSum", shareHundredths);
Counters.AddRaw("fh." + PerfStats.FpsBucket(fps), 1);
Counters.AddRaw("rh." + PerfStats.RamBucket(ramShare), 1);
if (fpsLow > 0)
{
Peaks.Offer("perf.fpsMin", fpsLow);
Peaks.Offer($"perfDays.{day}.fpsMin", fpsLow);
}
if (fpsHigh > 0)
{
Peaks.Offer("perf.fpsMax", fpsHigh);
Peaks.Offer($"perfDays.{day}.fpsMax", fpsHigh);
}
if (appMb > 0)
{
Peaks.Offer("perf.ramPeak", appMb);
Peaks.Offer($"perfDays.{day}.ramPeak", appMb);
}
if (battery > 0)
{
Counters.AddRaw("p.tempSum", (long)Math.Round(battery * UsageIds.Scale));
Peaks.Offer("perf.tempPeak", (long)Math.Round(battery));
}
Save();
}
catch
{
}
}
public static void EnterScreen(Screen screen)
{
try
{
_screen = screen;
if (_enabled)
Clock.Enter(screen, Counters.AddRaw);
}
catch
{
}
}
public static void OnForeground()
{
try
{
Foreground.Start();
Clock.Foreground();
if (_enabled && _started && Clock.Current is null)
Clock.Enter(_screen, Counters.AddRaw);
}
catch
{
}
}
public static void OnBackground()
{
try
{
Clock.Background();
if (!_enabled || !_started)
{
Foreground.Stop((_, _) => { });
return;
}
Foreground.Stop(ForegroundSink);
FlushTime();
Clock.Finish(Counters.AddRaw);
Save();
if (Environment.TickCount64 - _lastUpload > BackgroundUploadGap.TotalMilliseconds)
_ = Task.Run(UploadAsync);
}
catch
{
}
}
private static Signal ArmySize(int clients) => clients switch
{
<= 2 => Signal.ARMY_SIZE_2,
3 => Signal.ARMY_SIZE_3,
4 => Signal.ARMY_SIZE_4,
<= 7 => Signal.ARMY_SIZE_5_7,
_ => Signal.ARMY_SIZE_8_PLUS
};
private static async Task UploadLoop()
{
RemoteSettings.RefreshIfDue();
await Task.Delay(FirstUpload);
if (_waitingAtStart)
await UploadAsync();
long last = Environment.TickCount64;
while (true)
{
double minutes = RemoteSettings.Current.MinMinutes * (1 + Random.Shared.NextDouble() * 0.2);
await Task.Delay(TimeSpan.FromMinutes(minutes));
RemoteSettings.RefreshIfDue();
bool onScreen = !Platform.BotControl.InBackground;
if (onScreen || Environment.TickCount64 - last >= 4 * minutes * 60_000)
{
await UploadAsync();
last = Environment.TickCount64;
}
}
}
private static async Task UploadAsync()
{
if (!RemoteSettings.Current.UsageEnabled || !RemoteSettings.InSample(UsageSync.InstallId))
return;
if (!await UploadGate.WaitAsync(0))
return;
try
{
FlushTime();
Dictionary<string, long> snapshot = Counters.Snapshot();
string oldestDay = "pd.d" + DateTime.UtcNow.AddDays(-UsageSync.MaxPerfDays).ToString("yyyyMMdd");
Dictionary<string, long> stale = snapshot
.Where(c => !UsageIds.IsKnown(c.Key) && (!UsageIds.IsV2(c.Key) || (c.Key.StartsWith("pd.", StringComparison.Ordinal) && string.CompareOrdinal(c.Key, oldestDay) < 0)))
.ToDictionary(c => c.Key, c => c.Value);
if (stale.Count > 0)
{
Counters.Subtract(stale);
foreach (string id in stale.Keys)
snapshot.Remove(id);
}
Dictionary<string, long> first = snapshot.Where(c => UsageIds.IsKnown(c.Key)).ToDictionary(c => c.Key, c => c.Value);
Dictionary<string, long> second = snapshot.Where(c => !UsageIds.IsKnown(c.Key)).ToDictionary(c => c.Key, c => c.Value);
Dictionary<string, long> peaks = Peaks.Snapshot();
if (snapshot.Count == 0 && peaks.Count == 0)
return;
UsageSync.Result result = await UsageSync.UploadAsync(first, second, peaks);
if (result.V1)
Counters.Subtract(first);
if (result.V2)
{
Counters.Subtract(second);
Peaks.Uploaded(peaks);
}
if (result.V1 || result.V2)
Save();
_lastUpload = Environment.TickCount64;
}
catch (Exception ex)
{
System.Diagnostics.Trace.WriteLine($"[Usage] upload failed: {ex.Message}");
}
finally
{
UploadGate.Release();
}
}
private static void Load()
{
lock (FileGate)
{
if (_loaded)
return;
try
{
if (File.Exists(PendingPath) && JsonSerializer.Deserialize<Dictionary<string, long>>(File.ReadAllText(PendingPath)) is { } saved)
{
Counters.Restore(saved);
_waitingAtStart = saved.Count > 0;
}
}
catch
{
}
try
{
if (File.Exists(PeaksPath) && JsonSerializer.Deserialize<Dictionary<string, long>>(File.ReadAllText(PeaksPath)) is { } peaks)
Peaks.Restore(peaks);
}
catch
{
}
_loaded = true;
}
}
private static void Save()
{
lock (FileGate)
{
if (!_loaded)
return;
Interlocked.Exchange(ref _sinceSave, 0);
try
{
Directory.CreateDirectory(UsageSync.Folder);
string temp = PendingPath + ".tmp";
File.WriteAllText(temp, JsonSerializer.Serialize(Counters.Snapshot()));
File.Move(temp, PendingPath, true);
temp = PeaksPath + ".tmp";
File.WriteAllText(temp, JsonSerializer.Serialize(Peaks.Snapshot()));
File.Move(temp, PeaksPath, true);
}
catch
{
}
}
}
private static void HookCrashes()
{
AppDomain.CurrentDomain.UnhandledException += (_, _) => OnCrash();
global::Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, _) => OnCrash();
}
private static void OnCrash()
{
try
{
Counters.Track(nameof(Signal.SYS_CRASH));
Counters.AddRaw("h.crashes", 1);
FlushTime();
Clock.Finish(Counters.AddRaw);
Save();
}
catch
{
}
}
private static void FlushTime()
{
if (!_enabled)
return;
Foreground.Flush(ForegroundSink);
Alive.Flush(Counters.AddRaw);
Botting.Flush(Counters.AddRaw);
}
private static void ForegroundSink(string id, long ms)
{
Counters.AddRaw(id, ms);
Counters.AddRaw("h.sessionMs", ms);
}
private static void UpdateBotting()
{
try
{
if (_scriptRunning || _autoRunning)
Botting.Start();
else if (_enabled)
Botting.Stop(Counters.AddRaw);
}
catch
{
}
}
private static void HookAuto()
{
if (CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetService<Skua.Core.Interfaces.IScriptAuto>() is not { } auto
|| auto is not System.ComponentModel.INotifyPropertyChanged notify)
return;
notify.PropertyChanged += (_, e) =>
{
if (e.PropertyName != nameof(Skua.Core.Interfaces.IScriptAuto.IsRunning) || auto.IsRunning == _autoRunning)
return;
_autoRunning = auto.IsRunning;
if (_autoRunning)
Track(auto.IsHunting ? Signal.AUTO_HUNT_START : Signal.AUTO_ATTACK_START);
UpdateBotting();
};
}
private static void HookGame()
{
if (CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetService<Skua.Core.Interfaces.IFlashUtil>() is not { } flash)
return;
flash.FlashCall += (function, args) =>
{
try
{
string text = args is { Length: > 0 } ? args[0]?.ToString() ?? string.Empty : string.Empty;
switch (function)
{
case "mvsPerf":
PerfStats.OnPageSample(text);
break;
case "mvsGameLoad":
if (long.TryParse(text, out long loadMs) && loadMs is > 0 and < 30 * 60_000)
{
Health("gameLoads");
Health("gameLoadMsSum", loadMs);
}
break;
case "mvsMapLoad":
if (long.TryParse(text, out long mapMs) && mapMs is > 0 and < 10 * 60_000)
{
Health("mapLoads");
Health("mapLoadMsSum", mapMs);
}
break;
case "mvsOption":
OptionSeen(text);
break;
case "mvsEngineCrash":
Health("engineReloads");
break;
}
}
catch
{
}
};
}
private static void OptionSeen(string raw)
{
System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(raw, @"mvs([A-Z]\w{1,30}):(on|off)");
if (!m.Success)
return;
string id = "opt" + m.Groups[1].Value;
if (!Features.IsSwitch(id))
return;
bool on = m.Groups[2].Value == "on";
ISharedPreferences? prefs = global::Android.App.Application.Context.GetSharedPreferences("movibeskua.usage", FileCreationMode.Private);
if (prefs is null)
return;
bool known = prefs.Contains(id);
if (known && prefs.GetBoolean(id, on) == on)
return;
prefs.Edit()?.PutBoolean(id, on)?.Apply();
if (known)
Toggle(id, on);
}
public static IDictionary<string, bool> OptionStates()
{
Dictionary<string, bool> states = new(StringComparer.Ordinal);
try
{
ISharedPreferences? prefs = global::Android.App.Application.Context.GetSharedPreferences("movibeskua.usage", FileCreationMode.Private);
if (prefs?.All is { } all)
{
foreach (KeyValuePair<string, object> entry in all)
{
if (entry.Value is Java.Lang.Boolean flag && Features.IsSwitch(entry.Key))
states[entry.Key] = flag.BooleanValue();
}
}
}
catch
{
}
return states;
}
private static void HookMessages()
{
IMessenger messenger = StrongReferenceMessenger.Default;
int scripts = (int)MessageChannels.ScriptStatus;
int game = (int)MessageChannels.GameEvents;
messenger.Register<object, ScriptStartedMessage, int>(Recipient, scripts, (_, _) =>
{
Track(Signal.SCRIPT_STARTED);
_scriptRunning = true;
UpdateBotting();
});
messenger.Register<object, ScriptStoppedMessage, int>(Recipient, scripts, (_, _) =>
{
Track(Signal.SCRIPT_STOPPED);
_scriptRunning = false;
UpdateBotting();
});
messenger.Register<object, ScriptErrorMessage, int>(Recipient, scripts, (_, _) => Track(Signal.SCRIPT_ERROR));
messenger.Register<object, LoginMessage, int>(Recipient, game, (_, _) => Track(Signal.LOGIN_DONE));
messenger.Register<object, ReloginPausedMessage, int>(Recipient, game, (_, _) => Health("loginFails"));
messenger.Register<object, ReloginTriggeredMessage, int>(Recipient, game, (_, m) => Track(m.WasKicked ? Signal.RELOGIN_KICKED : Signal.RELOGIN));
}
}
public sealed class CountedCommand : System.Windows.Input.ICommand
{
private readonly Signal _signal;
private readonly string? _feature;
private readonly System.Windows.Input.ICommand _inner;
public CountedCommand(Signal signal, System.Windows.Input.ICommand inner, string? feature = null)
{
_signal = signal;
_feature = feature;
_inner = inner;
}
public event EventHandler? CanExecuteChanged
{
add => _inner.CanExecuteChanged += value;
remove => _inner.CanExecuteChanged -= value;
}
public bool CanExecute(object? parameter) => _inner.CanExecute(parameter);
public void Execute(object? parameter)
{
UsageTracker.Track(_signal);
if (_feature is not null)
UsageTracker.Feature(_feature);
_inner.Execute(parameter);
}
}
