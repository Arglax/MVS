using System;
using System.IO;
using System.Linq;
using Android.App;
using Android.Content;
using Android.OS;
namespace MoVibeSkua.Android.Platform;
public sealed record PhoneFacts(string Model, string Android, int Sdk, string Soc, int Cores, int RamMb, bool? Is64, bool IsArm,
long MaxKhz, long FreeMb, int WebViewMajor)
{
public static PhoneFacts Read()
{
Context context = Application.Context;
string model = Try(() => $"{Build.Manufacturer} {Build.Model}".Trim(), string.Empty);
string android = Try(() => Build.VERSION.Release ?? string.Empty, string.Empty);
int sdk = Try(() => (int)Build.VERSION.SdkInt, 0);
string soc = Try(() =>
{
if (OperatingSystem.IsAndroidVersionAtLeast(31) && Build.SocModel is { Length: > 0 } name
&& !string.Equals(name, Build.Unknown, StringComparison.OrdinalIgnoreCase))
return name;
return Build.Hardware ?? string.Empty;
}, string.Empty);
int cores = Try(() => Java.Lang.Runtime.GetRuntime()?.AvailableProcessors() ?? 0, 0);
int ramMb = Try(() =>
{
ActivityManager.MemoryInfo info = new();
(context.GetSystemService(Context.ActivityService) as ActivityManager)?.GetMemoryInfo(info);
return (int)(info.TotalMem / (1024 * 1024));
}, 0);
bool? is64 = null;
bool isArm = false;
try
{
if (Build.SupportedAbis is { Count: > 0 } abis)
{
is64 = Build.Supported64BitAbis is { Count: > 0 };
isArm = abis.Any(a => a.StartsWith("arm", StringComparison.OrdinalIgnoreCase));
}
}
catch
{
}
long maxKhz = 0;
for (int i = 0; i < Math.Max(cores, 1) && i < 64; i++)
{
try
{
string text = File.ReadAllText($"/sys/devices/system/cpu/cpu{i}/cpufreq/cpuinfo_max_freq").Trim();
if (long.TryParse(text, out long khz))
maxKhz = Math.Max(maxKhz, khz);
}
catch
{
}
}
long freeMb = Try(() =>
{
string? path = context.FilesDir?.Path;
return path is null ? -1 : new StatFs(path).AvailableBytes / (1024 * 1024);
}, -1L);
int webView = Try(() =>
{
string version = global::Android.Webkit.WebView.CurrentWebViewPackage?.VersionName ?? string.Empty;
string major = new(version.TakeWhile(char.IsDigit).ToArray());
return int.TryParse(major, out int n) ? n : 0;
}, 0);
return new PhoneFacts(model, android, sdk, soc, cores, ramMb, is64, isArm, maxKhz, freeMb, webView);
}
private static T Try<T>(Func<T> read, T unknown)
{
try
{
return read();
}
catch
{
return unknown;
}
}
}
