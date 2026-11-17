using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.DependencyInjection;
using MoVibeSkua.Android.Services;
using Skua.Flash.Ruffle;
using Trace = System.Diagnostics.Trace;
namespace MoVibeSkua.Android.Platform;
public static class FollowerSaver
{
public const int SaverFps = 10;
private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(3);
private static int _started;
private static volatile bool _active;
public static bool Active => _active;
public static void Start()
{
if (System.Threading.Interlocked.Exchange(ref _started, 1) == 1)
return;
_ = Task.Run(async () =>
{
while (true)
{
try
{
bool want = BotControl.InBackground && IsFollowing();
if (want != _active)
Apply(want);
}
catch (Exception ex)
{
Trace.WriteLine($"[FollowerSaver] check failed: {ex.Message}");
}
await Task.Delay(CheckEvery).ConfigureAwait(false);
}
});
}
private static void Apply(bool on)
{
_active = on;
AppContext.SetData("mvs.lowPower", on);
RuffleFlashUtil? flash = Ioc.Default.GetService<RuffleFlashUtil>();
if (on)
{
Usage.UsageTracker.Feature("followerSaver");
flash?.PostCommand(string.Create(CultureInfo.InvariantCulture, $"{{\"t\":\"targetFps\",\"fps\":{SaverFps}}}"));
Trace.WriteLine($"[FollowerSaver] following out of sight: game held at {SaverFps} fps");
return;
}
int effective = PerformanceGuard.EffectiveTargetFps;
flash?.PostCommand(string.Create(CultureInfo.InvariantCulture, $"{{\"t\":\"targetFps\",\"fps\":{Math.Max(0, effective)}}}"));
PerformanceGuard.Instance.ApplyFramePacing();
Trace.WriteLine("[FollowerSaver] on screen or not following: normal frame rate back");
}
private static bool IsFollowing()
{
string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Skua", "character_locations");
if (!Directory.Exists(dir))
return false;
string pid = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
foreach (string path in Directory.EnumerateFiles(dir, "*_butlerv5"))
{
Dictionary<string, string> fields;
try
{
fields = Parse(File.ReadAllText(path));
}
catch (IOException)
{
continue;
}
if (fields.TryGetValue("pid", out string? p) && p == pid)
return fields.TryGetValue("loggedin", out string? l) && l == "1"
&& fields.TryGetValue("following", out string? f) && !string.IsNullOrWhiteSpace(f);
}
return false;
}
private static Dictionary<string, string> Parse(string text)
{
Dictionary<string, string> fields = new(StringComparer.OrdinalIgnoreCase);
foreach (string line in text.Split('\n'))
{
int eq = line.IndexOf('=');
if (eq > 0)
fields[line[..eq].Trim()] = line[(eq + 1)..].Trim();
}
return fields;
}
}
