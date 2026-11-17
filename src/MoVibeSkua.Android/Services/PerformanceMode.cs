using System;
using System.Linq;
using Android.App;
using Android.OS;
using Android.Views;
namespace MoVibeSkua.Android.Services;
public static class PerformanceMode
{
public const double CoolDownAt = 47.0;
public const double FullSpeedBelow = 45.0;
private static bool _sustained;
public static void Apply(Activity activity)
{
try
{
PreferHighestRefreshRate(activity);
}
catch (Exception ex)
{
System.Diagnostics.Trace.WriteLine($"[Perf] refresh rate: {ex.Message}");
}
try
{
if (OperatingSystem.IsAndroidVersionAtLeast(33)
&& activity.GetSystemService(global::Android.Content.Context.GameService) is GameManager games)
{
games.SetGameState(new GameState(false, (int)GameStateMode.GameplayInterruptible));
System.Diagnostics.Trace.WriteLine($"[Perf] game mode {games.GameMode}");
}
}
catch (Exception ex)
{
System.Diagnostics.Trace.WriteLine($"[Perf] game state: {ex.Message}");
}
}
public static void SetLowRefresh(Activity activity, bool low)
{
try
{
if (low)
PreferRefreshRate(activity, lowest: true);
else
PreferHighestRefreshRate(activity);
}
catch (Exception ex)
{
System.Diagnostics.Trace.WriteLine($"[Perf] refresh rate: {ex.Message}");
}
}
private static void PreferHighestRefreshRate(Activity activity) => PreferRefreshRate(activity, lowest: false);
private static void PreferRefreshRate(Activity activity, bool lowest)
{
Display? display = OperatingSystem.IsAndroidVersionAtLeast(30) ? activity.Display : activity.WindowManager?.DefaultDisplay;
Window? window = activity.Window;
if (display is null || window is null)
return;
Display.Mode? current = display.GetMode();
Display.Mode? best = display.GetSupportedModes()?
.Where(m => current is null || (m.PhysicalWidth == current.PhysicalWidth && m.PhysicalHeight == current.PhysicalHeight))
.Where(m => !lowest || m.RefreshRate >= 59f)
.OrderBy(m => lowest ? m.RefreshRate : -m.RefreshRate)
.FirstOrDefault();
if (best is null)
return;
WindowManagerLayoutParams attrs = window.Attributes!;
attrs.PreferredDisplayModeId = best.ModeId;
attrs.PreferredRefreshRate = best.RefreshRate;
window.Attributes = attrs;
System.Diagnostics.Trace.WriteLine($"[Perf] display mode {best.ModeId}: {best.PhysicalWidth}x{best.PhysicalHeight} @ {best.RefreshRate:0} Hz");
}
public static void OnTemperature(double batteryCelsius, int thermalStatus)
{
bool hot = batteryCelsius >= CoolDownAt || thermalStatus >= 3;
bool cool = batteryCelsius < FullSpeedBelow && thermalStatus < 3;
bool want = _sustained ? !cool : hot;
if (want == _sustained)
return;
Activity? activity = MainActivity.Current;
if (activity is null)
return;
activity.RunOnUiThread(() =>
{
try
{
if (activity.GetSystemService(global::Android.Content.Context.PowerService) is PowerManager power
&& power.IsSustainedPerformanceModeSupported)
{
activity.Window?.SetSustainedPerformanceMode(want);
}
_sustained = want;
System.Diagnostics.Trace.WriteLine($"[Perf] {(want ? "cool-down" : "full speed")} mode at {batteryCelsius:0.0} C (status {thermalStatus})");
}
catch (Exception ex)
{
System.Diagnostics.Trace.WriteLine($"[Perf] sustained mode: {ex.Message}");
}
});
}
}
