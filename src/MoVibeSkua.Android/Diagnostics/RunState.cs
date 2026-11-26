using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
namespace MoVibeSkua.Android.Diagnostics;
public static class RunState
{
private static readonly TimeSpan Every = TimeSpan.FromSeconds(10);
private static int _started;
private static string Folder => Path.Combine(global::Android.App.Application.Context.FilesDir!.AbsolutePath, "reports");
public static string PathFor(int slot) => System.IO.Path.Combine(Folder, $"runstate-c{slot}.json");
public static void Start(Func<string> feature, Func<string> page)
{
if (Interlocked.Exchange(ref _started, 1) == 1)
return;
_ = Task.Run(async () =>
{
using PeriodicTimer timer = new(Every);
do
{
Write(feature, page);
}
while (await timer.WaitForNextTickAsync());
});
}
private static void Write(Func<string> feature, Func<string> page)
{
try
{
JsonObject state = new()
{
["pid"] = global::Android.OS.Process.MyPid(),
["slot"] = Platform.Army.Slot,
["ts"] = DateTime.UtcNow.ToString("o"),
["uptimeMs"] = global::Android.OS.SystemClock.ElapsedRealtime() - global::Android.OS.Process.StartElapsedRealtime,
};
void Put(string key, Func<JsonNode?> value)
{
try
{
if (value() is { } node)
state[key] = node;
}
catch
{
}
}
Put("feature", () => feature());
Put("page", () => page());
Put("script", () => RunInfo.StartedUtc is not null ? RunInfo.ScriptName : null);
Put("runSec", () => RunInfo.RunTime is { } t ? (long)t.TotalSeconds : null);
Put("map", () => RunInfo.MapName);
Put("wasmMb", () => Usage.PerfStats.LastWasmMb > 0 ? Usage.PerfStats.LastWasmMb : null);
Put("appMb", () => Usage.PerfStats.LastAppMb > 0 ? Usage.PerfStats.LastAppMb : null);
Put("freeMb", () => Usage.PerfStats.DeviceFreeMb());
Put("managedMb", () => GC.GetTotalMemory(false) / (1024 * 1024));
Put("threads", () => Directory.GetDirectories("/proc/self/task").Length);
Put("fps", () => Usage.PerfStats.LastFps > 0 ? Usage.PerfStats.LastFps : null);
Put("thermal", () => Services.ThermalMonitor.LastStatus >= 0 ? Services.ThermalMonitor.LastStatus : null);
Put("battC", () => Services.ThermalMonitor.LastBattery > 0 ? Math.Round(Services.ThermalMonitor.LastBattery, 1) : null);
Directory.CreateDirectory(Folder);
string path = PathFor(Platform.Army.Slot);
File.WriteAllText(path + ".tmp", state.ToJsonString());
File.Move(path + ".tmp", path, overwrite: true);
}
catch (Exception ex)
{
Trace.WriteLine($"[Report] run state: {ex.Message}");
}
}
public static JsonObject? Read(int slot, int pid)
{
try
{
string path = PathFor(slot);
if (!File.Exists(path))
return null;
if (JsonNode.Parse(File.ReadAllText(path)) is not JsonObject state)
return null;
return pid <= 0 || state["pid"]?.GetValue<int>() == pid ? state : null;
}
catch
{
return null;
}
}
public static int SlotOf(string? processName)
{
int colon = processName?.LastIndexOf(":army", StringComparison.Ordinal) ?? -1;
return colon >= 0 && int.TryParse(processName![(colon + 5)..], out int slot) && slot > 1 ? slot : 1;
}
}
