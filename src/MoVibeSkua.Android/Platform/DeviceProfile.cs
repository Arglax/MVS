using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Android.App;
using Android.Content;
using Android.OS;
using Trace = System.Diagnostics.Trace;
namespace MoVibeSkua.Android.Platform;
public sealed class DeviceProfile
{
public enum Tiers { Low, Mid, High }
public int Cores { get; private init; }
public int BigCores { get; private init; }
public double MaxGHz { get; private init; }
public double RamGB { get; private init; }
public bool LowRamDevice { get; private init; }
public int Sdk { get; private init; }
public float MaxRefreshHz { get; private init; }
public bool HasThermalHeadroom { get; private init; }
public bool IsPhone { get; private init; }
public Tiers Tier { get; private init; }
private static DeviceProfile? _current;
public static DeviceProfile Current => _current ??= Read();
private static DeviceProfile Read()
{
Context context = global::Android.App.Application.Context;
int cores = System.Environment.ProcessorCount;
double[] clocks = Enumerable.Range(0, Math.Max(cores, 1)).Select(MaxClockGHz).Where(c => c > 0).ToArray();
double maxGHz = clocks.Length > 0 ? clocks.Max() : 0;
int big = clocks.Count(c => c >= 2.4);
double ramGB = 0;
bool lowRam = false;
if (context.GetSystemService(Context.ActivityService) is ActivityManager am)
{
ActivityManager.MemoryInfo memory = new();
am.GetMemoryInfo(memory);
ramGB = memory.TotalMem / (1024d * 1024 * 1024);
lowRam = am.IsLowRamDevice;
}
float maxHz = 60;
try
{
if (context.GetSystemService(Context.DisplayService) is global::Android.Hardware.Display.DisplayManager displays
&& displays.GetDisplay(0) is { } display)
maxHz = display.GetSupportedModes()?.Max(m => m.RefreshRate) ?? display.RefreshRate;
}
catch
{
}
int smallestDp = context.Resources?.Configuration?.SmallestScreenWidthDp ?? 0;
bool phone = smallestDp is > 0 and < 600;
int sdk = (int)Build.VERSION.SdkInt;
double clock = maxGHz > 0 ? maxGHz : 2.6;
Tiers tier = lowRam || ramGB < 4.5 || cores <= 4 || clock < 2.2 ? Tiers.Low
: clock >= 2.8 && ramGB >= 7.5 && cores >= 8 ? Tiers.High
: Tiers.Mid;
DeviceProfile profile = new()
{
Cores = cores,
BigCores = big,
MaxGHz = maxGHz,
RamGB = ramGB,
LowRamDevice = lowRam,
Sdk = sdk,
MaxRefreshHz = maxHz,
HasThermalHeadroom = sdk >= 30,
IsPhone = phone,
Tier = tier
};
Trace.WriteLine($"[Device] {Build.Manufacturer} {Build.Model} ({Build.Hardware}): {cores} cores, {big} big, up to {maxGHz:0.00} GHz, " +
$"{ramGB:0.0} GB{(lowRam ? " low-RAM" : "")}, Android API {sdk}, {maxHz:0} Hz, {(phone ? "phone" : "tablet")} ({smallestDp} dp) → {tier} tier");
return profile;
}
private static double MaxClockGHz(int core)
{
try
{
string text = File.ReadAllText($"/sys/devices/system/cpu/cpu{core}/cpufreq/cpuinfo_max_freq").Trim();
return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long khz) ? khz / 1_000_000d : 0;
}
catch
{
return 0;
}
}
public string DefaultResolution => Tier switch { Tiers.High => "0", Tiers.Mid => "2", _ => "1.5" };
public string DefaultAntiAlias => Tier switch { Tiers.High => "", Tiers.Mid => "MEDIUM", _ => "LOW" };
public bool DefaultSmoothMotion => Tier == Tiers.High;
public double StartupPixelRatio => Tier switch { Tiers.High => 3, Tiers.Mid => 2, _ => 1.5 };
public int ComfortableClients => Math.Clamp((int)Math.Floor((RamGB - 3) / 0.9), 1, Army.MaxClients);
}
