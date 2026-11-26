using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using CommunityToolkit.Mvvm.Messaging;
using Skua.Core.Messaging;
namespace MoVibeSkua.Android.Diagnostics;
public static class ProblemReports
{
public const string Collection = "movibeskua_reports";
private static int DailyLimit => Usage.RemoteSettings.Current.PerDay;
private const int MaxError = 2000;
private const int MaxStack = 8000;
private static int MaxLog => Usage.RemoteSettings.Current.FullLog ? 48000 : 16000;
private static bool Allowed => (AskEnabled || OffSendsMinimal) && Usage.RemoteSettings.Current.ReportsEnabled;
private static bool OffSendsMinimal => !AskEnabled && Prefs?.GetString("reports.off", null) == "minimal";
private const int MaxContext = 240;
public const string StateAsk = "ask";
public const string StateFull = "full";
public const string StateMinimal = "minimal";
public const string StateLocal = "local";
private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
private static readonly object FileGate = new();
private static readonly object Recipient = new();
private static int _uploading;
private static bool _installed;
private static Context Context => global::Android.App.Application.Context;
private static string Folder => Path.Combine(Context.FilesDir!.AbsolutePath, "reports");
private static string SentPath => Path.Combine(Folder, "sent.json");
private static ISharedPreferences? Prefs => Context.GetSharedPreferences("movibeskua.shell", FileCreationMode.Private);
public static bool AskEnabled
{
get
{
ISharedPreferences? prefs = Prefs;
if (prefs is null)
return true;
if (prefs.Contains("reports.ask"))
return prefs.GetBoolean("reports.ask", true);
bool wasOff = prefs.GetString("reports.mode", null) == "Never"
|| (prefs.Contains("reports.send") && !prefs.GetBoolean("reports.send", true));
return !wasOff;
}
set
{
ISharedPreferencesEditor? editor = Prefs?.Edit()?.PutBoolean("reports.ask", value);
if (!value)
editor?.PutString("reports.off", "minimal");
editor?.Apply();
Usage.UsageTracker.Toggle("crashReports", value);
Trace.WriteLine($"[Report] full reports: {(value ? "on" : "off (bare minimum only)")}");
if (!value)
_ = Task.Run(() => AnswerAsync(full: false));
}
}
public static void Install()
{
if (_installed)
return;
_installed = true;
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
if (e.ExceptionObject is Exception ex)
Save("crash", "mvs", ex, null, null, fatal: true);
};
global::Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, e) =>
{
if (CrashGuard.Survive(e.Exception))
{
e.Handled = true;
return;
}
Save("crash", "mvs", e.Exception, null, null, fatal: true);
};
TaskScheduler.UnobservedTaskException += (_, e) =>
{
if (e.Exception.InnerExceptions.All(IsNoise))
return;
Save("problem", "mvs", e.Exception.GetBaseException(), null, null, fatal: false);
};
StrongReferenceMessenger.Default.Register<object, LoginMessage, int>(Recipient, (int)MessageChannels.GameEvents,
(_, m) => AppLog.RememberName(m.Username));
StrongReferenceMessenger.Default.Register<object, MapChangedMessage, int>(Recipient, (int)MessageChannels.GameEvents,
(_, m) => RunInfo.MapName = m.Map);
_ = Task.Run(async () =>
{
await Task.Delay(TimeSpan.FromSeconds(5));
RunState.Start(FeatureInUse, () => Usage.UsageTracker.CurrentScreen.ToString());
if (Platform.Army.IsFirst)
CollectExitRecords();
CrashPrompt.WhenReady();
if (Platform.Army.IsFirst)
AppUpdates.CheckWhenReady();
await Task.Delay(TimeSpan.FromSeconds(15));
await UploadDecidedAsync();
});
}
public static void HookGame(Skua.Core.Interfaces.IFlashUtil flash)
{
flash.FlashCall += (function, args) =>
{
if (function is not ("mvsEngineCrash" or "mvsEngineInit"))
return;
string text = args is { Length: > 0 } ? args[0]?.ToString() ?? string.Empty : string.Empty;
if (function == "mvsEngineInit")
_ = Task.Run(() => SaveEngineInit(text));
else
_ = Task.Run(() => SaveEngine(text));
};
}
public static void SaveEngine(string text)
{
try
{
text = Regex.Replace(text, @"(http://127\.0\.0\.1:\d+)/[0-9a-f]{24}/", "$1/<token>/");
string[] lines = text.Split('\n');
int? fps = null, wasm = null;
string error = text, stack = string.Empty;
if (lines.Length >= 2 && lines[0].Split('|') is { Length: 2 } numbers
&& int.TryParse(numbers[0], out int f) && int.TryParse(numbers[1], out int w))
{
fps = f;
wasm = w;
error = lines[1].Trim();
stack = string.Join("\n", lines.Skip(2)).Trim();
}
error = "Game engine crashed: " + error;
string normalised = Regex.Replace(error, @"0x[0-9a-fA-F]+|\d+", "#");
SaveRaw("engine", "mvs", error, stack, Hash("engine|" + normalised), when: DateTime.UtcNow, fatal: false,
fps: fps, wasmMb: wasm, feature: null);
}
catch (Exception ex)
{
Trace.WriteLine($"[Report] engine: {ex.Message}");
}
}
private static string? _lastInitSignature;
private static DateTime _lastInitAt;
public static void SaveEngineInit(string text)
{
try
{
text = Regex.Replace(text, @"(http://127\.0\.0\.1:\d+)/[0-9a-f]{24}/", "$1/<token>/");
string[] lines = text.Split('\n');
string kind = lines[0].Trim();
string message = lines.Length > 1 ? lines[1].Trim() : "";
string details = lines.Length > 2 ? string.Join("\n", lines.Skip(2)).Trim() : "";
string error = $"Game engine failed to start ({kind}): {message}";
string signature = Hash("engine-init|" + kind + "|" + Regex.Replace(message, @"0x[0-9a-fA-F]+|\d+", "#"));
lock (FileGate)
{
if (signature == _lastInitSignature && DateTime.UtcNow - _lastInitAt < TimeSpan.FromMinutes(3))
return;
_lastInitSignature = signature;
_lastInitAt = DateTime.UtcNow;
}
string memory = "";
try
{
if (Context.GetSystemService(Context.ActivityService) is ActivityManager am)
{
ActivityManager.MemoryInfo info = new();
am.GetMemoryInfo(info);
memory = $"availMem {info.AvailMem / 1048576} MB of {info.TotalMem / 1048576} MB, lowMemory {info.LowMemory}, " +
$"threshold {info.Threshold / 1048576} MB, app {Usage.PerfStats.AppMemoryMb(Usage.PerfStats.LastWasmMb)} MB, ";
}
}
catch
{
}
string state = $"\n[EngineInit] {DateTime.UtcNow:o} {memory}army slot {Platform.Army.Slot}";
SaveRaw("engine", "mvs", error, details, signature, when: DateTime.UtcNow, fatal: false, fps: 0, wasmMb: null,
feature: "engine start", log: AppLog.ReportLog() + state);
}
catch (Exception ex)
{
Trace.WriteLine($"[Report] engine init: {ex.Message}");
}
}
public static string? Save(string kind, string category, Exception? ex, string? message, string? script, bool fatal, string? feature = null)
{
try
{
string wireKind = kind is "crash" or "engine" ? kind : "problem";
string error = message ?? $"{ex?.GetType().FullName}: {ex?.Message}";
if (message is not null && ex is not null)
error += $" ({ex.GetType().FullName}: {ex.Message})";
string signature = Signature(wireKind, ex, message ?? error);
return SaveRaw(wireKind, category, error, ex?.ToString() ?? string.Empty, signature, DateTime.UtcNow, fatal,
fps: null, wasmMb: null, feature: feature, script: script);
}
catch
{
return null;
}
}
private static string? SaveRaw(string kind, string category, string error, string stack, string signature, DateTime when, bool fatal,
int? fps, int? wasmMb, string? feature, string? script = null, string? log = null, int? appMb = null, string? page = null,
JsonObject? deadRun = null)
{
try
{
bool local = category == "script";
if (!local && !Allowed)
{
Trace.WriteLine($"[Report] not kept (reports are off): {Cut(error, 160)}");
return null;
}
string id = $"{when:yyyyMMdd-HHmmss}-{signature[..8]}-{RandomNumberGenerator.GetHexString(6, lowercase: true)}";
bool ask = AskEnabled;
JsonObject report = new()
{
["id"] = id,
["state"] = local ? StateLocal : ask ? StateAsk : StateMinimal,
["kind"] = kind,
["ts"] = when.ToString("o"),
["signature"] = signature,
["app"] = Cut(AppLog.AppVersion(), 40),
["error"] = Cut(error, MaxError),
["stack"] = Cut(stack, MaxStack),
["context"] = ContextMap(page, feature, script, deadRun),
["scrubbed"] = false,
};
if (local || ask)
report["full"] = FullPart(fatal, fps, wasmMb, log, appMb, deadRun);
lock (FileGate)
{
Directory.CreateDirectory(Folder);
File.WriteAllText(Path.Combine(Folder, id + ".json"), report.ToJsonString());
}
Trace.WriteLine($"[Report] saved {kind}/{category} {id}: {Cut(error, 160)}");
if (!local && !fatal)
{
if (ask)
RunNotifier.ShowWhenOnScreen(CrashPrompt.ShowPending);
else
_ = Task.Run(UploadDecidedAsync);
}
return id;
}
catch
{
return null;
}
}
private static JsonObject ContextMap(string? page, string? feature, string? script, JsonObject? deadRun = null)
{
JsonObject context = new();
void Put(string key, Func<string?> value)
{
try
{
string? v = value();
if (!string.IsNullOrWhiteSpace(v))
context[key] = Cut(v.Trim(), MaxContext);
}
catch
{
}
}
string? Dead(string key) => deadRun?[key] is JsonValue v ? v.ToString() : null;
if (deadRun is not null)
{
Put("page", () => Dead("page") ?? page);
Put("feature", () => Dead("feature") ?? feature);
Put("script", () => Dead("script"));
Put("map", () => Dead("map"));
Put("client", () => $"army client {Dead("slot") ?? "?"}");
Put("runFor", () => Dead("runSec") is { } sec ? $"script ran {sec} s" : null);
Put("died", () => DeadSummary(deadRun));
Put("process", () => Dead("process"));
return context;
}
Put("page", () => page ?? Usage.UsageTracker.CurrentScreen.ToString());
Put("feature", () => feature ?? FeatureInUse());
Put("script", () => script ?? (RunInfo.StartedUtc is not null ? RunInfo.ScriptName : null));
Put("map", () => RunInfo.MapName);
Put("client", () => $"army client {Platform.Army.Slot}");
Put("runFor", () => RunInfo.StartedUtc is not null && RunInfo.RunTime is { } t ? $"script ran {(long)t.TotalSeconds} s" : null);
Put("thread", () =>
{
Thread thread = Thread.CurrentThread;
bool ui = Avalonia.Threading.Dispatcher.UIThread.CheckAccess();
return $"{(ui ? "UI thread" : thread.IsThreadPoolThread ? "thread pool" : "own thread")} #{thread.ManagedThreadId} {thread.Name}";
});
Put("died", () => $"now: threads {Directory.GetDirectories("/proc/self/task").Length}, managed {GC.GetTotalMemory(false) / 1048576} MB, "
+ $"heat {Services.ThermalMonitor.LastStatus}, gc {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}");
return context;
}
private static string FeatureInUse()
{
List<string> parts = new();
var services = CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default;
if (services.GetService<Skua.Core.Interfaces.IScriptManager>() is { ScriptRunning: true })
parts.Add("script");
if (services.GetService<Skua.Core.Interfaces.IScriptAuto>() is { IsRunning: true } auto)
parts.Add(auto.IsHunting ? "auto hunt" : "auto attack");
if (Platform.BotControl.BotOnly)
parts.Add("bot only");
if (Platform.Headless.Active)
parts.Add("headless");
if (Platform.BotControl.InBackground)
parts.Add("in background");
return parts.Count == 0 ? "game" : string.Join(", ", parts);
}
private static string DeadSummary(JsonObject state)
{
string V(string key) => state[key] is JsonValue v ? v.ToString() : "?";
return $"last note {V("ts")}: up {(state["uptimeMs"] is JsonValue up ? up.GetValue<long>() / 1000 : -1)} s, app {V("appMb")} MB, wasm {V("wasmMb")} MB, "
+ $"managed {V("managedMb")} MB, free {V("freeMb")} MB, threads {V("threads")}, heat {V("thermal")} ({V("battC")} C), fps {V("fps")}";
}
private static JsonObject FullPart(bool fatal, int? fps, int? wasmMb, string? log, int? appMb, JsonObject? deadRun = null)
{
JsonObject full = new();
void Put(string key, Func<JsonNode?> value)
{
try
{
if (value() is { } node)
full[key] = node;
}
catch
{
}
}
Put("device", () => Usage.DeviceInfo.Describe());
Put("mem", () =>
{
int wasm = wasmMb ?? Usage.PerfStats.LastWasmMb;
JsonObject mem = new() { ["appMb"] = appMb ?? Usage.PerfStats.AppMemoryMb(wasm), ["freeMb"] = Usage.PerfStats.DeviceFreeMb() };
if (wasm > 0)
mem["wasmMb"] = wasm;
if (deadRun is not null)
{
mem.Remove("freeMb");
mem.Remove("wasmMb");
foreach (string key in new[] { "freeMb", "wasmMb", "managedMb", "threads" })
{
if (deadRun[key] is JsonValue v && v.TryGetValue(out int n))
mem[key] = n;
}
}
else
{
mem["managedMb"] = (int)(GC.GetTotalMemory(false) / 1048576);
}
return mem;
});
Put("fps", () => deadRun is not null ? (deadRun["fps"] is JsonValue f0 && f0.TryGetValue(out int f1) ? JsonValue.Create(f1) : null)
: (fps ?? Usage.PerfStats.LastFps) is > 0 and var f ? JsonValue.Create(f) : null);
Put("temp", () => deadRun is not null ? (deadRun["battC"] is JsonValue t0 && t0.TryGetValue(out double t1) ? JsonValue.Create(t1) : null)
: Services.ThermalMonitor.LastBattery > 0 ? JsonValue.Create(Math.Round(Services.ThermalMonitor.LastBattery, 1)) : null);
Put("uptimeMs", () => JsonValue.Create(global::Android.OS.SystemClock.ElapsedRealtime() - global::Android.OS.Process.StartElapsedRealtime));
Put("settings", Settings);
Put("log", () => log ?? (fatal ? AppLog.RecentRaw(200) : AppLog.ReportLog()));
return full;
}
private static JsonObject Settings()
{
JsonObject settings = new();
ISharedPreferences? shell = Prefs;
if (shell?.All is { } all)
{
foreach (KeyValuePair<string, object> entry in all)
{
if (!(entry.Key.StartsWith("gfx.", StringComparison.Ordinal) || entry.Key is "botMode" or "sidebar" or "streamerMode" or "devMode" or "reports.ask"))
continue;
string key = entry.Key.Replace('.', '_');
switch (entry.Value)
{
case Java.Lang.Boolean flag: settings[key] = flag.BooleanValue(); break;
case Java.Lang.Integer number: settings[key] = number.IntValue(); break;
case Java.Lang.Float real: settings[key] = Math.Round(real.DoubleValue(), 3); break;
case string text when text.Length <= 20: settings[key] = text; break;
case Java.Lang.String text when text.Length() <= 20: settings[key] = text.ToString(); break;
}
}
}
foreach ((string key, bool on) in Usage.UsageTracker.OptionStates())
settings[key] = on;
Services.PerformanceGuard guard = Services.PerformanceGuard.Instance;
settings["perfGuard"] = guard.EnablePerformanceGuard;
settings["targetFps"] = guard.TargetFps;
settings["autoThrottle"] = guard.AutoThrottleOnOverheat;
settings["botOnly"] = Platform.BotControl.BotOnly;
settings["headless"] = Platform.Headless.Active;
return settings;
}
private static void CollectExitRecords()
{
if (!OperatingSystem.IsAndroidVersionAtLeast(30))
return;
try
{
ActivityManager? am = Context.GetSystemService(Context.ActivityService) as ActivityManager;
var records = am?.GetHistoricalProcessExitReasons(Context.PackageName, 0, 16);
if (records is null || records.Count == 0)
return;
long seen = Prefs?.GetLong("exit.seen", 0) ?? 0;
long newest = seen;
foreach (ApplicationExitInfo info in records)
{
if (info.Timestamp <= seen)
continue;
newest = Math.Max(newest, info.Timestamp);
if (seen == 0)
continue;
int reason = (int)info.Reason;
Usage.UsageTracker.Health("exitReasons." + ExitReasonName(reason));
if (reason == (int)ApplicationExitInfoReason.CrashNative)
Usage.UsageTracker.Health("crashes");
if (reason == (int)ApplicationExitInfoReason.Anr)
Usage.UsageTracker.Health("anr");
string? what = reason == (int)ApplicationExitInfoReason.CrashNative ? $"native crash (signal {info.Status})"
: reason == (int)ApplicationExitInfoReason.Anr ? "app not responding: Android closed it"
: reason == (int)ApplicationExitInfoReason.LowMemory ? "closed by Android for memory"
: null;
if (what is null)
continue;
DateTime died = DateTimeOffset.FromUnixTimeMilliseconds(info.Timestamp).UtcDateTime;
string clues = TraceClues(info);
int slot = RunState.SlotOf(info.ProcessName);
JsonObject deadRun = RunState.Read(slot, info.Pid) ?? new JsonObject { ["slot"] = slot };
deadRun["process"] = $"{info.ProcessName} pid {info.Pid}";
string error = $"MVS closed unexpectedly: {what}" + (string.IsNullOrEmpty(info.Description) ? "" : $" ({info.Description})")
+ ExitState(info);
if (SelfTest.NativeTest)
error = "MVS self-test: " + error;
string signature = Hash(NativeKey(what, clues));
SaveRaw("crash", "mvs", error, clues, signature, died, fatal: true, fps: null, wasmMb: null,
feature: "previous run", log: AppLog.PreviousRunTail(died, slot), appMb: (int)(info.Pss / 1024), page: "previous run",
deadRun: deadRun);
}
if (newest != seen)
Prefs?.Edit()?.PutLong("exit.seen", newest)?.Apply();
}
catch (Exception ex)
{
Trace.WriteLine($"[Report] exit records: {ex.Message}");
}
}
private static string ExitState(ApplicationExitInfo info)
{
try
{
int importance = (int)info.Importance;
string where = importance <= 100 ? "on screen" : importance <= 230 ? "visible" : "in background";
long rssMb = info.Rss / 1024, pssMb = info.Pss / 1024;
return $" [{where}, importance {importance}, pss {pssMb} MB, rss {rssMb} MB]";
}
catch
{
return "";
}
}
private static readonly string[] ExitReasonNames =
{
"unknown", "exitSelf", "signaled", "lowMemory", "crash", "crashNative", "anr", "initFailure",
"permissionChange", "excessiveResourceUsage", "userRequested", "userStopped", "dependencyDied",
"other", "freezer", "packageStateChange", "packageUpdated"
};
private static string ExitReasonName(int reason) => reason >= 0 && reason < ExitReasonNames.Length ? ExitReasonNames[reason] : "other";
private static string TraceClues(ApplicationExitInfo info)
{
if (!OperatingSystem.IsAndroidVersionAtLeast(30))
return string.Empty;
try
{
using Stream? stream = info.TraceInputStream;
if (stream is null)
return string.Empty;
byte[] buffer = new byte[2 * 1024 * 1024];
int read = 0, n;
while (read < buffer.Length && (n = stream.Read(buffer, read, buffer.Length - read)) > 0)
read += n;
if ((int)info.Reason == (int)ApplicationExitInfoReason.CrashNative)
{
string crash = Tombstone.Describe(buffer, read);
if (crash.Length > 0)
return Cut(crash, MaxStack);
}
else if ((int)info.Reason == (int)ApplicationExitInfoReason.Anr)
{
string main = Tombstone.MainThread(Encoding.UTF8.GetString(buffer, 0, read));
if (main.Length > 0)
return Cut(main, MaxStack);
}
List<string> found = new();
StringBuilder run = new();
for (int i = 0; i <= read; i++)
{
char c = i < read ? (char)buffer[i] : '\0';
if (c >= ' ' && c < 127)
{
run.Append(c);
continue;
}
string text = run.ToString().Trim();
run.Clear();
if (text.Length >= 12 && (text.Contains("Assertion", StringComparison.OrdinalIgnoreCase) || text.Contains("Abort", StringComparison.OrdinalIgnoreCase)
|| text.Contains("signal", StringComparison.OrdinalIgnoreCase) || text.Contains("Exception", StringComparison.Ordinal)
|| text.Contains(".so", StringComparison.Ordinal) && found.Count < 30))
found.Add(text);
}
return Cut(string.Join("\n", found.Distinct().Take(40)), MaxStack);
}
catch (Exception ex)
{
Trace.WriteLine($"[Report] trace clues: {ex.Message}");
return string.Empty;
}
}
private static bool IsNoise(Exception ex) => ex is OperationCanceledException or TimeoutException or HttpRequestException
or IOException or WebException or System.Net.Sockets.SocketException or System.Net.WebSockets.WebSocketException
|| ex is Skua.Flash.Ruffle.FlashCallException && ex.Message.Contains("player not ready", StringComparison.OrdinalIgnoreCase);
private static string Signature(string kind, Exception? ex, string summary)
{
string where = ex?.StackTrace?.Split('\n').FirstOrDefault(l => l.Contains(" at ", StringComparison.Ordinal))?.Trim()
?? Regex.Replace(summary, @"0x[0-9a-fA-F]+|\d+", "#");
return Hash(kind + "|" + ex?.GetType().FullName + "|" + where);
}
internal static string NativeKey(string what, string clues)
{
static string Plain(string text)
{
text = Regex.Replace(text, @"<anonymous:[0-9a-fA-F]+>", "<anonymous>");
text = Regex.Replace(text, @" ?\(BuildId: [0-9a-fA-F]+\)", "");
text = Regex.Replace(text, @"/data/app/\S*?/lib/", "/data/app/.../lib/");
text = Regex.Replace(text, @"\b[0-9a-fA-F]{5,}\b", "#");
return Regex.Replace(text, @"0x[0-9a-fA-F]+|\d+", "#").Trim();
}
string[] lines = clues.Split('\n');
if (!lines[0].StartsWith("SIG", StringComparison.Ordinal))
return "crash|" + Regex.Replace(what, @" \(signal \d+\)", "") + "|" + Plain(lines[0]);
Match head = Regex.Match(lines[0], @"^(SIG\w+) \((\w+)\).*? in (\S+)");
string where = head.Success ? $"{head.Groups[1].Value} {head.Groups[2].Value} {head.Groups[3].Value}" : lines[0];
string overflow = lines.Any(l => l.StartsWith("cause: stack pointer", StringComparison.Ordinal)) ? "stack overflow" : string.Empty;
string abort = lines.FirstOrDefault(l => l.StartsWith("abort: ", StringComparison.Ordinal)) is { } a ? Plain(a) : string.Empty;
return "native|" + Plain(where) + "|" + overflow + "|" + abort;
}
private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();
private static string Cut(string text, int max) => text.Length > max ? text[..max] : text;
private static IEnumerable<(string File, JsonObject Report)> Files()
{
string[] files;
lock (FileGate)
files = Directory.Exists(Folder) ? Directory.GetFiles(Folder, "2*.json").OrderBy(f => f, StringComparer.Ordinal).ToArray() : Array.Empty<string>();
foreach (string file in files)
{
JsonObject? report = null;
try
{
report = JsonNode.Parse(File.ReadAllText(file)) as JsonObject;
}
catch
{
}
if (report?["state"] is null)
{
TryDelete(file);
continue;
}
yield return (file, report);
}
}
public static IReadOnlyList<string> WaitingKinds() => Waiting().Select(w => w.Kind).ToList();
public static IReadOnlyList<(string Kind, string Error, string Script)> Waiting()
{
List<(string Kind, string Error, string Script)> kinds = new();
if (!Allowed)
{
Trace.WriteLine("[Report] not asked: problem reports are off");
DropWaiting();
return kinds;
}
if (!AskEnabled)
return kinds;
Dictionary<string, string> sent = ReadSent();
string today = DateTime.UtcNow.ToString("yyyyMMdd");
int room = DailyLimit - sent.Values.Count(d => d == today);
if (room <= 0)
Trace.WriteLine($"[Report] not asked: today's limit of {DailyLimit} reports is used up");
HashSet<string> seen = new(StringComparer.Ordinal);
foreach ((string file, JsonObject report) in Files())
{
if (Str(report, "state") != StateAsk)
continue;
string signature = Str(report, "signature");
bool duplicate = (sent.TryGetValue(signature, out string? day) && day == today) || !seen.Add(signature);
if (duplicate || kinds.Count >= room)
{
TryDelete(file);
continue;
}
string script = report["context"] is JsonObject context ? Str(context, "script") : string.Empty;
kinds.Add((Str(report, "kind"), Str(report, "error"), script));
}
return kinds;
}
public static async Task AnswerAsync(bool full = true)
{
foreach ((string file, JsonObject report) in Files().ToList())
{
if (Str(report, "state") != StateAsk)
continue;
report["state"] = full ? StateFull : StateMinimal;
if (!full)
report.Remove("full");
lock (FileGate)
File.WriteAllText(file, report.ToJsonString());
}
await UploadDecidedAsync();
}
private static void DropWaiting()
{
foreach ((string file, JsonObject report) in Files().ToList())
{
if (Str(report, "state") == StateAsk)
TryDelete(file);
}
}
public static async Task UploadDecidedAsync()
{
if (Interlocked.Exchange(ref _uploading, 1) == 1)
return;
try
{
List<(string File, JsonObject Report)> decided = Files()
.Where(r => Str(r.Report, "state") is StateFull or StateMinimal)
.ToList();
if (decided.Count == 0)
return;
if (!Allowed)
{
foreach ((string file, _) in decided)
TryDelete(file);
return;
}
if (!AskEnabled)
{
foreach ((string file, JsonObject report) in decided)
{
if (Str(report, "state") != StateFull)
continue;
report["state"] = StateMinimal;
report.Remove("full");
lock (FileGate)
File.WriteAllText(file, report.ToJsonString());
}
}
if (Usage.UsageSync.Backoff.Active)
return;
Dictionary<string, string> sent = ReadSent();
string today = DateTime.UtcNow.ToString("yyyyMMdd");
List<(string File, JsonObject Report)> toSend = new();
HashSet<string> seen = new(StringComparer.Ordinal);
int room = DailyLimit - sent.Values.Count(d => d == today);
foreach ((string file, JsonObject report) in decided)
{
string signature = Str(report, "signature");
bool duplicate = (sent.TryGetValue(signature, out string? day) && day == today) || !seen.Add(signature);
if (duplicate || toSend.Count >= room)
TryDelete(file);
else
toSend.Add((file, report));
}
if (toSend.Count == 0)
return;
var auth = await Usage.UsageSync.SignedInAsync();
if (auth is null)
return;
foreach ((string file, JsonObject report) in toSend)
{
bool? result = await SendAsync(auth.Value, report);
if (result is null)
break;
if (result == true)
{
sent[Str(report, "signature")] = today;
WriteSent(sent);
}
TryDelete(file);
}
}
catch (Exception ex)
{
Trace.WriteLine($"[Report] upload failed: {ex.Message}");
}
finally
{
Interlocked.Exchange(ref _uploading, 0);
}
}
public static JsonObject Document(JsonObject report, string? install)
{
bool full = Str(report, "state") == StateFull;
JsonObject S(string v) => new() { ["stringValue"] = v };
JsonObject fields = new()
{
["consent"] = S(full ? "full" : "minimal"),
["kind"] = S(Str(report, "kind")),
["app"] = S(Cut(Str(report, "app"), 40)),
["ts"] = new JsonObject { ["timestampValue"] = DateTime.Parse(Str(report, "ts"), null, System.Globalization.DateTimeStyles.RoundtripKind).ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'") },
["signature"] = S(Str(report, "signature")),
["error"] = S(Cut(AppLog.Redact(Str(report, "error")), MaxError)),
["stack"] = S(Cut(AppLog.Redact(Str(report, "stack")), MaxStack)),
};
JsonObject context = new();
if (report["context"] is JsonObject saved)
{
foreach ((string key, JsonNode? value) in saved.Take(12))
{
if (value?.GetValueKind() == JsonValueKind.String)
context[key] = Cut(AppLog.Redact(value.GetValue<string>()), MaxContext);
}
}
fields["context"] = Usage.UsageSync.Value(context);
if (full && report["full"] is JsonObject extra)
{
if (!string.IsNullOrEmpty(install))
fields["install"] = S(install);
foreach (string key in new[] { "device", "mem", "fps", "temp", "uptimeMs", "settings" })
{
if (extra[key] is { } value)
fields[key] = Usage.UsageSync.Value(value.DeepClone());
}
if (extra["log"]?.GetValueKind() == JsonValueKind.String)
{
string log = AppLog.Redact(extra["log"]!.GetValue<string>());
fields["log"] = S(log.Length > MaxLog ? "(older lines cut)\n" + log[^(MaxLog - 20)..] : log);
}
}
return fields;
}
private static async Task<bool?> SendAsync((string ProjectId, string Uid, string IdToken) auth, JsonObject report)
{
bool full = Str(report, "state") == StateFull;
JsonObject fields = Document(report, full ? auth.Uid : null);
string id = Str(report, "id");
using HttpRequestMessage request = new(HttpMethod.Post,
$"https://firestore.googleapis.com/v1/projects/{auth.ProjectId}/databases/(default)/documents/{Collection}?documentId={id}")
{
Content = new StringContent(new JsonObject { ["fields"] = fields }.ToJsonString(), Encoding.UTF8, "application/json")
};
request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.IdToken);
HttpResponseMessage response;
try
{
response = await Http.SendAsync(request);
}
catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
{
Trace.WriteLine($"[Report] not sent now: {ex.Message}");
return null;
}
using HttpResponseMessage _ = response;
if (response.StatusCode == HttpStatusCode.Unauthorized)
{
Usage.UsageSync.TokenRefused();
return null;
}
if (response.StatusCode == HttpStatusCode.Conflict)
return true;
if (!response.IsSuccessStatusCode)
{
string error = await response.Content.ReadAsStringAsync();
Trace.WriteLine($"[Report] refused: {(int)response.StatusCode} {Cut(error, 300)}");
if (Usage.UsageSync.Classify(response.StatusCode, error) != Usage.UsageSync.Outcome.Throttled)
return false;
Usage.UsageSync.Backoff.Hit();
return null;
}
Trace.WriteLine($"[Report] sent {id} ({(full ? "full" : "minimal")})");
return true;
}
private static string Str(JsonObject report, string key) =>
report[key]?.GetValueKind() == JsonValueKind.String ? report[key]!.GetValue<string>() : string.Empty;
private static void TryDelete(string file)
{
try
{
lock (FileGate)
File.Delete(file);
}
catch
{
}
}
private static Dictionary<string, string> ReadSent()
{
try
{
return File.Exists(SentPath)
? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(SentPath)) ?? new()
: new();
}
catch
{
return new();
}
}
private static void WriteSent(Dictionary<string, string> sent)
{
string cutoff = DateTime.UtcNow.AddDays(-7).ToString("yyyyMMdd");
Dictionary<string, string> kept = sent.Where(p => string.CompareOrdinal(p.Value, cutoff) >= 0).ToDictionary(p => p.Key, p => p.Value);
lock (FileGate)
File.WriteAllText(SentPath, JsonSerializer.Serialize(kept));
}
}
