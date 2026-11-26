using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Views.InputMethods;
namespace MoVibeSkua.Android.Services;
public readonly record struct RamBoostResult(long AvailBeforeMb, long AvailAfterMb, long TotalMb, int AppsTrimmed, bool LowMemory, IReadOnlyList<RamBoostApp> StillRunning, bool UsedShizuku);
public sealed record RamBoostApp(string Package, string Label, long RamMb, string State);
public static class RamBoost
{
private const string PrefsName = "movibeskua.shell";
private const string AutoKey = "ram.autoBoost";
private const int MaxListed = 15;
private static readonly TimeSpan AutoInterval = TimeSpan.FromMinutes(5);
private static long _lastAutoTicks;
private static int _running;
public static bool AutoBoost
{
get => Prefs()?.GetBoolean(AutoKey, false) ?? false;
set => Prefs()?.Edit()?.PutBoolean(AutoKey, value)?.Apply();
}
private static ISharedPreferences? Prefs() =>
global::Android.App.Application.Context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
public static (long AvailMb, long TotalMb, bool Low) Snapshot()
{
ActivityManager? am = Manager();
if (am is null)
return (0, 0, false);
var info = new ActivityManager.MemoryInfo();
am.GetMemoryInfo(info);
return (info.AvailMem / (1024 * 1024), info.TotalMem / (1024 * 1024), info.LowMemory);
}
public static Task<RamBoostResult?> BoostAsync() => Task.Run(Boost);
public static void AutoBoostOnForeground()
{
if (!AutoBoost)
return;
long now = DateTime.UtcNow.Ticks;
long last = Interlocked.Read(ref _lastAutoTicks);
if (last != 0 && now - last < AutoInterval.Ticks)
return;
Interlocked.Exchange(ref _lastAutoTicks, now);
_ = Task.Run(() =>
{
try { Boost(); }
catch (Exception e) { global::Android.Util.Log.Warn("MVS", "auto boost: " + e.Message); }
});
}
public static void TrimSelf()
{
GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
GC.WaitForPendingFinalizers();
GC.Collect();
Java.Lang.Runtime.GetRuntime()?.Gc();
}
private static RamBoostResult? Boost()
{
if (Interlocked.Exchange(ref _running, 1) == 1)
return null;
try
{
(long before, long total, _) = Snapshot();
bool shizuku = ShizukuShell.Granted;
int trimmed = 0;
if (shizuku)
{
foreach (RamBoostApp app in ListBackground(int.MaxValue))
{
if (app.State is "Playing / noticeable" or "Backup")
continue;
if (ShizukuShell.ForceStop(app.Package))
trimmed++;
}
}
else
trimmed = TrimOthers();
TrimSelf();
Thread.Sleep(800);
(long after, _, bool low) = Snapshot();
IReadOnlyList<RamBoostApp> left = shizuku ? ListBackground(MaxListed) : Array.Empty<RamBoostApp>();
return new RamBoostResult(before, after, total, trimmed, low, left, shizuku);
}
finally
{
Interlocked.Exchange(ref _running, 0);
}
}
private static readonly Dictionary<string, string> BackgroundGroups = new(StringComparer.OrdinalIgnoreCase)
{
["Perceptible"] = "Playing / noticeable",
["Perceptible Low"] = "Playing / noticeable",
["Perceptible Medium"] = "Playing / noticeable",
["Backup"] = "Backup",
["Heavy Weight"] = "Background",
["A Services"] = "Background service",
["B Services"] = "Background service",
["Previous"] = "Recently used",
["Cached"] = "Cached"
};
private static readonly Regex MemLine = new(@"^(\s*)([\d,]+)\s*(?:K|kB):\s+(.+?)\s*$", RegexOptions.Compiled);
private static readonly Regex ProcLine = new(@"^(\S+)\s+\(pid\s+\d+", RegexOptions.Compiled);
public static IReadOnlyList<RamBoostApp> ListBackground(int max = MaxListed)
{
string? text = ShizukuShell.Exec("dumpsys meminfo");
if (string.IsNullOrEmpty(text))
return Array.Empty<RamBoostApp>();
Context context = global::Android.App.Application.Context;
PackageManager? pm = context.PackageManager;
HashSet<string> skip = ProtectedPackages(context, pm);
var sizes = new Dictionary<string, (long Kb, string State)>(StringComparer.Ordinal);
bool inSection = false;
int groupIndent = -1;
string? group = null;
foreach (string raw in text.Split('\n'))
{
string line = raw.TrimEnd('\r');
if (line.StartsWith("Total", StringComparison.Ordinal))
{
inSection = line.Contains("OOM adjustment", StringComparison.OrdinalIgnoreCase);
groupIndent = -1;
group = null;
continue;
}
if (!inSection)
continue;
Match m = MemLine.Match(line);
if (!m.Success)
continue;
int indent = m.Groups[1].Value.Length;
string rest = m.Groups[3].Value;
Match proc = ProcLine.Match(rest);
if (!proc.Success)
{
if (groupIndent < 0 || indent <= groupIndent)
{
groupIndent = indent;
group = BackgroundGroups.TryGetValue(rest, out string? state) ? state : null;
}
continue;
}
if (group is null)
continue;
string package = proc.Groups[1].Value.Split(':')[0];
if (skip.Contains(package) || package.StartsWith("com.arglax.", StringComparison.Ordinal) || IsSystem(pm, package))
continue;
if (!long.TryParse(m.Groups[2].Value.Replace(",", ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out long kb))
continue;
sizes[package] = sizes.TryGetValue(package, out var old) ? (old.Kb + kb, old.State) : (kb, group);
}
var list = new List<RamBoostApp>();
foreach (var (package, value) in sizes)
list.Add(new RamBoostApp(package, Label(pm, package), value.Kb / 1024, value.State));
list.Sort((a, b) => b.RamMb.CompareTo(a.RamMb));
if (list.Count > max)
list.RemoveRange(max, list.Count - max);
return list;
}
private static bool IsSystem(PackageManager? pm, string package)
{
try
{
ApplicationInfo? info = pm?.GetApplicationInfo(package, 0);
if (info is null)
return true;
return (info.Flags & (ApplicationInfoFlags.System | ApplicationInfoFlags.UpdatedSystemApp | ApplicationInfoFlags.Persistent)) != 0;
}
catch (PackageManager.NameNotFoundException)
{
return true;
}
}
private static string Label(PackageManager? pm, string package)
{
try { return pm?.GetApplicationInfo(package, 0)?.LoadLabel(pm)?.ToString() ?? package; }
catch (PackageManager.NameNotFoundException) { return package; }
}
public static void OpenAppInfo(string package)
{
var intent = new Intent(global::Android.Provider.Settings.ActionApplicationDetailsSettings, global::Android.Net.Uri.FromParts("package", package, null));
intent.AddFlags(ActivityFlags.NewTask);
global::Android.App.Application.Context.StartActivity(intent);
}
public static void OpenAppList()
{
var intent = new Intent(global::Android.Provider.Settings.ActionManageApplicationsSettings);
intent.AddFlags(ActivityFlags.NewTask);
global::Android.App.Application.Context.StartActivity(intent);
}
private static HashSet<string> ProtectedPackages(Context context, PackageManager? pm)
{
var skip = new HashSet<string>(StringComparer.Ordinal) { context.PackageName ?? "" };
if (pm is not null)
{
ResolveInfo? home = pm.ResolveActivity(new Intent(Intent.ActionMain).AddCategory(Intent.CategoryHome), PackageInfoFlags.MatchDefaultOnly);
if (home?.ActivityInfo?.PackageName is { } launcher)
skip.Add(launcher);
}
string? ime = global::Android.Provider.Settings.Secure.GetString(context.ContentResolver, global::Android.Provider.Settings.Secure.DefaultInputMethod);
if (!string.IsNullOrEmpty(ime))
skip.Add(ime.Split('/')[0]);
if (context.GetSystemService(Context.InputMethodService) is InputMethodManager imm && imm.EnabledInputMethodList is { } methods)
{
foreach (InputMethodInfo method in methods)
if (method.PackageName is { } name)
skip.Add(name);
}
return skip;
}
private static int TrimOthers()
{
Context context = global::Android.App.Application.Context;
ActivityManager? am = Manager();
PackageManager? pm = context.PackageManager;
if (am is null || pm is null)
return 0;
HashSet<string> skip = ProtectedPackages(context, pm);
var launcher = new Intent(Intent.ActionMain).AddCategory(Intent.CategoryLauncher);
IList<ResolveInfo>? apps = pm.QueryIntentActivities(launcher, PackageInfoFlags.MatchAll);
if (apps is null)
return 0;
int count = 0;
var done = new HashSet<string>(StringComparer.Ordinal);
foreach (ResolveInfo app in apps)
{
ApplicationInfo? info = app.ActivityInfo?.ApplicationInfo;
string? name = info?.PackageName;
if (info is null || name is null || !done.Add(name) || skip.Contains(name))
continue;
if (name.StartsWith("com.arglax.", StringComparison.Ordinal))
continue;
if ((info.Flags & (ApplicationInfoFlags.System | ApplicationInfoFlags.UpdatedSystemApp | ApplicationInfoFlags.Persistent)) != 0)
continue;
if ((info.Flags & ApplicationInfoFlags.Stopped) != 0)
continue;
try
{
am.KillBackgroundProcesses(name);
count++;
}
catch (Java.Lang.SecurityException)
{
}
}
return count;
}
private static ActivityManager? Manager() =>
global::Android.App.Application.Context.GetSystemService(Context.ActivityService) as ActivityManager;
}
