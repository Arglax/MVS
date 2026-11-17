using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
namespace MoVibeSkua.Android.Usage;
public static class PerfStats
{
private static int _busy;
public static int LastFps { get; private set; }
public static int LastAppMb { get; private set; }
public static int LastWasmMb { get; private set; }
public static void OnPageSample(string? raw)
{
if (string.IsNullOrWhiteSpace(raw) || Interlocked.Exchange(ref _busy, 1) == 1)
return;
_ = Task.Run(() =>
{
try
{
Take(raw);
}
catch
{
}
finally
{
Interlocked.Exchange(ref _busy, 0);
}
});
}
private static void Take(string raw)
{
string[] parts = raw.Trim().Split('|');
if (parts.Length < 5
|| !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double average)
|| !int.TryParse(parts[1], out int lowest)
|| !int.TryParse(parts[2], out int highest)
|| !int.TryParse(parts[3], out int seconds)
|| !int.TryParse(parts[4], out int wasmMb))
return;
if (seconds < 30 || average < 0 || average > 240 || lowest < 0 || highest > 240 || lowest > highest && lowest != 0)
return;
int appMb = AppMemoryMb(wasmMb);
int deviceMb = DeviceInfo.RamMb;
double share = deviceMb > 0 ? Math.Min(100, appMb * 100.0 / deviceMb) : 0;
double battery = Services.ThermalMonitor.LastBattery;
bool hot = Services.ThermalMonitor.LastStatus >= 2 || Services.ThermalMonitor.LastHeadroom >= 1.0;
LastFps = (int)Math.Round(average);
LastAppMb = appMb;
LastWasmMb = wasmMb;
UsageTracker.PerfSample(DateTime.UtcNow, average, lowest, highest, appMb, share, battery, hot);
}
public static int AppMemoryMb(int wasmMb)
{
int own = 0;
try
{
global::Android.OS.Debug.MemoryInfo info = new();
global::Android.OS.Debug.GetMemoryInfo(info);
own = info.TotalPss / 1024;
}
catch
{
}
return own + Math.Max(0, wasmMb);
}
public static int DeviceFreeMb()
{
try
{
ActivityManager.MemoryInfo info = new();
(Application.Context.GetSystemService(Context.ActivityService) as ActivityManager)?.GetMemoryInfo(info);
return (int)(info.AvailMem / 1048576);
}
catch
{
return 0;
}
}
public static string FpsBucket(double fps) =>
fps < 15 ? "b0" : fps < 20 ? "b15" : fps < 25 ? "b20" : fps < 30 ? "b25" : fps < 40 ? "b30" : fps < 50 ? "b40" : fps < 60 ? "b50" : "b60";
public static string RamBucket(double share) =>
share < 10 ? "p0" : share < 20 ? "p10" : share < 30 ? "p20" : share < 40 ? "p30" : share < 50 ? "p40" : "p50";
}
