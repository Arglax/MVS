using System;
using System.Linq;
using System.Text.Json.Nodes;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Telephony;
namespace MoVibeSkua.Android.Usage;
public static class DeviceInfo
{
private static JsonObject? _cached;
private static int _ramMb = -1;
public static int RamMb
{
get
{
if (_ramMb >= 0)
return _ramMb;
try
{
ActivityManager.MemoryInfo info = new();
(Application.Context.GetSystemService(Context.ActivityService) as ActivityManager)?.GetMemoryInfo(info);
_ramMb = (int)(info.TotalMem / 1048576);
}
catch
{
_ramMb = 0;
}
return _ramMb;
}
}
public static JsonObject Describe()
{
if (_cached is not null)
return (JsonObject)_cached.DeepClone();
JsonObject device = new();
void Put(string name, Func<JsonNode?> value)
{
try
{
if (value() is { } node)
device[name] = node;
}
catch
{
}
}
Context context = Application.Context;
Put("model", () => Cut($"{Build.Manufacturer} {Build.Model}".Trim(), 80));
Put("android", () => Cut(Build.VERSION.Release ?? string.Empty, 20));
Put("sdk", () => (int)Build.VERSION.SdkInt);
Put("soc", () => Cut(Soc(), 60));
Put("cores", () => Java.Lang.Runtime.GetRuntime()?.AvailableProcessors() ?? System.Environment.ProcessorCount);
Put("ramMb", () => RamMb);
Put("refreshHz", () => RefreshHz(context));
Put("webview", () => Cut(WebViewVersion(), 40));
Put("country", () => Country());
Put("app", () => Cut(Diagnostics.AppLog.AppVersion(), 40));
_cached = device;
return (JsonObject)device.DeepClone();
}
private static string Cut(string text, int max) => text.Length > max ? text[..max] : text;
private static string Soc()
{
if (OperatingSystem.IsAndroidVersionAtLeast(31))
{
string? model = Build.SocModel;
if (!string.IsNullOrWhiteSpace(model) && !string.Equals(model, Build.Unknown, StringComparison.OrdinalIgnoreCase))
return model;
}
return Build.Hardware ?? string.Empty;
}
private static int RefreshHz(Context context)
{
global::Android.Views.Display? display = null;
if (context.GetSystemService(Context.DisplayService) is global::Android.Hardware.Display.DisplayManager displays)
display = displays.GetDisplay(global::Android.Views.Display.DefaultDisplay);
if (display is null)
return 0;
float best = display.RefreshRate;
global::Android.Views.Display.Mode[]? modes = display.GetSupportedModes();
if (modes is not null)
{
foreach (global::Android.Views.Display.Mode mode in modes)
best = Math.Max(best, mode.RefreshRate);
}
return (int)Math.Round(best);
}
private static string WebViewVersion()
{
global::Android.Content.PM.PackageInfo? package = global::Android.Webkit.WebView.CurrentWebViewPackage;
return package?.VersionName ?? string.Empty;
}
public static string Country()
{
try
{
Context context = Application.Context;
TelephonyManager? telephony = context.GetSystemService(Context.TelephonyService) as TelephonyManager;
string?[] candidates =
{
telephony?.NetworkCountryIso,
telephony?.SimCountryIso,
context.Resources?.Configuration?.Locales.Get(0)?.Country,
Java.Util.Locale.Default.Country
};
return candidates.Select(c => c?.Trim().ToUpperInvariant()).FirstOrDefault(c => c is { Length: 2 }) ?? "Unknown";
}
catch
{
return "Unknown";
}
}
}
