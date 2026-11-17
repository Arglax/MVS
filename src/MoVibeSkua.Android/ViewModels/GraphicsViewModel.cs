using System;
using System.Collections.Generic;
using System.Globalization;
using Android.Content;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Skua.Core.Interfaces;
namespace MoVibeSkua.Android.ViewModels;
public partial class GraphicsViewModel : ObservableObject
{
public record Choice(string Label, string Value);
public static readonly IReadOnlyList<Choice> Resolutions = new[]
{
new Choice("Battery (1x)", "1"),
new Choice("Balanced (1.5x)", "1.5"),
new Choice("Sharp (2x)", "2"),
new Choice("Native screen", "0"),
new Choice("Ultra (supersampled)", "-1.2")
};
public static readonly IReadOnlyList<Choice> AntiAliasing = new[]
{
new Choice("Game setting", ""),
new Choice("Off (fastest)", "LOW"),
new Choice("2x", "MEDIUM"),
new Choice("4x", "HIGH"),
new Choice("8x (best)", "8X8")
};
public static readonly IReadOnlyList<Choice> ColourStyles = new[]
{
new Choice("Natural", "natural"),
new Choice("Vivid", "vivid"),
new Choice("Cinematic", "cinematic"),
new Choice("Bright", "bright")
};
public static readonly IReadOnlyList<Choice> TargetFpsOptions = new[]
{
new Choice("Game decides (AQW's Max FPS option)", "0"),
new Choice("30 FPS (power saver)", "30"),
new Choice("48 FPS", "48"),
new Choice("60 FPS", "60")
};
private const string PrefsName = "movibeskua.shell";
private readonly ISharedPreferences _prefs;
private bool _loading;
public GraphicsViewModel()
{
_prefs = global::Android.App.Application.Context.GetSharedPreferences(PrefsName, FileCreationMode.Private)!;
_loading = true;
Platform.DeviceProfile device = Platform.DeviceProfile.Current;
Resolution = Find(Resolutions, _prefs.GetString("gfx.dpr", device.DefaultResolution));
AntiAlias = Find(AntiAliasing, _prefs.GetString("gfx.quality", device.DefaultAntiAlias));
ColourStyle = Find(ColourStyles, _prefs.GetString("gfx.filter", "natural"));
ShowFps = _prefs.GetBoolean("gfx.fps", false);
AutoSmooth = _prefs.GetBoolean("gfx.auto", true);
HeatWarnings = _prefs.GetBoolean("gfx.heat", true);
SmoothMotion = _prefs.GetBoolean("gfx.smooth", device.DefaultSmoothMotion);
Services.PerformanceGuard guard = Services.PerformanceGuard.Instance;
EnablePerformanceGuard = guard.EnablePerformanceGuard;
TargetFps = Find(TargetFpsOptions, guard.TargetFps.ToString(CultureInfo.InvariantCulture));
AutoThrottleOnOverheat = guard.AutoThrottleOnOverheat;
ShowHardwareAlerts = guard.ShowHardwareAlerts;
_loading = false;
RefreshTelemetry();
_telemetryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
_telemetryTimer.Tick += (_, _) => RefreshTelemetry();
_telemetryTimer.Start();
_loading = false;
}
public IReadOnlyList<Choice> ResolutionChoices => Resolutions;
public IReadOnlyList<Choice> AntiAliasChoices => AntiAliasing;
public IReadOnlyList<Choice> ColourChoices => ColourStyles;
public IReadOnlyList<Choice> TargetFpsChoices => TargetFpsOptions;
[ObservableProperty]
private bool _enablePerformanceGuard;
[ObservableProperty]
private Choice _targetFps = TargetFpsOptions[0];
[ObservableProperty]
private bool _autoThrottleOnOverheat;
[ObservableProperty]
private bool _showHardwareAlerts;
[ObservableProperty]
private string _liveThermalStatus = "Normal";
[ObservableProperty]
private string _liveMemoryStatus = "Checking...";
[ObservableProperty]
private string _livePacingStatus = "30 FPS locked";
private DispatcherTimer? _telemetryTimer;
[ObservableProperty]
private Choice _resolution;
[ObservableProperty]
private Choice _antiAlias;
[ObservableProperty]
private Choice _colourStyle;
[ObservableProperty]
private bool _showFps;
[ObservableProperty]
private bool _autoSmooth;
[ObservableProperty]
private bool _heatWarnings;
[ObservableProperty]
private bool _smoothMotion;
public IScriptOption Options => Ioc.Default.GetRequiredService<IScriptOption>();
partial void OnResolutionChanged(Choice value) => Changed("gfxResolution");
partial void OnAntiAliasChanged(Choice value) => Changed("gfxAntiAlias");
partial void OnColourStyleChanged(Choice value) => Changed("gfxColourStyle");
partial void OnShowFpsChanged(bool value) => Changed("gfxShowFps", value);
partial void OnAutoSmoothChanged(bool value) => Changed("gfxAutoSmooth", value);
partial void OnHeatWarningsChanged(bool value) => Changed("gfxHeatWarnings", value);
partial void OnSmoothMotionChanged(bool value) => Changed("gfxSmoothMotion", value);
partial void OnEnablePerformanceGuardChanged(bool value)
{
if (_loading) return;
Usage.UsageTracker.Toggle("gfxPerfGuard", value);
Services.PerformanceGuard.Instance.EnablePerformanceGuard = value;
RefreshTelemetry();
}
partial void OnTargetFpsChanged(Choice value)
{
if (_loading) return;
Usage.UsageTracker.Feature("gfxTargetFps");
if (int.TryParse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fps))
Services.PerformanceGuard.Instance.TargetFps = fps;
RefreshTelemetry();
}
partial void OnAutoThrottleOnOverheatChanged(bool value)
{
if (_loading) return;
Usage.UsageTracker.Toggle("gfxAutoThrottle", value);
Services.PerformanceGuard.Instance.AutoThrottleOnOverheat = value;
RefreshTelemetry();
}
partial void OnShowHardwareAlertsChanged(bool value)
{
if (_loading) return;
Usage.UsageTracker.Toggle("gfxHardwareAlerts", value);
Services.PerformanceGuard.Instance.ShowHardwareAlerts = value;
}
public void RefreshTelemetry()
{
Services.PerformanceGuard guard = Services.PerformanceGuard.Instance;
int effective = Services.PerformanceGuard.EffectiveTargetFps;
if (!guard.EnablePerformanceGuard)
LivePacingStatus = "Disabled (Unlimited)";
else if (guard.IsThermalThrottled)
LivePacingStatus = $"Throttled to {effective} FPS (Thermal Guard)";
else if (guard.IsDynamicThrottled)
LivePacingStatus = $"Dynamic Stepped to {effective} FPS (Strain Guard)";
else
LivePacingStatus = effective > 0 ? $"Locked: {effective} FPS" : "Unlimited";
int status = guard.CurrentThermalStatus;
double battery = guard.CurrentBatteryTemp;
string statusStr = status switch
{
0 => "None (Cool)",
1 => "Light",
2 => "Moderate",
3 => "Severe",
4 => "Critical (Throttled)",
5 => "Emergency",
6 => "Shutdown",
_ => "Normal"
};
LiveThermalStatus = battery > 0 ? $"{statusStr} ({battery:0.0}°C)" : statusStr;
Services.MemoryMetrics mem = guard.GetMemoryMetrics();
LiveMemoryStatus = $"{mem.UsedMb:0} MB used / {mem.AvailableMb:0} MB free (Total: {mem.TotalMb:0} MB)";
}
[RelayCommand]
private void ApplyPreset(string preset)
{
Usage.UsageTracker.Feature(preset switch
{
"battery" => "gfxPresetBattery",
"balanced" => "gfxPresetBalanced",
"ultra" => "gfxPresetUltra",
_ => "gfxPresetQuality"
});
_loading = true;
(string dpr, string quality, string filter) = preset switch
{
"battery" => ("1", "LOW", "natural"),
"balanced" => ("1.5", "MEDIUM", "natural"),
"ultra" => ("-1.2", "8X8", "vivid"),
_ => ("0", "HIGH", "natural")
};
Resolution = Find(Resolutions, dpr);
AntiAlias = Find(AntiAliasing, quality);
ColourStyle = Find(ColourStyles, filter);
_loading = false;
Changed();
}
private void Changed(string feature, bool? on = null)
{
if (_loading)
return;
if (on is { } value)
Usage.UsageTracker.Toggle(feature, value);
else
Usage.UsageTracker.Feature(feature);
Changed();
}
private void Changed()
{
if (_loading)
return;
_prefs.Edit()!
.PutString("gfx.dpr", Resolution.Value)!
.PutString("gfx.quality", AntiAlias.Value)!
.PutString("gfx.filter", ColourStyle.Value)!
.PutBoolean("gfx.fps", ShowFps)!
.PutBoolean("gfx.auto", AutoSmooth)!
.PutBoolean("gfx.heat", HeatWarnings)!
.PutBoolean("gfx.smooth", SmoothMotion)!
.Apply();
Apply();
}
public void Apply()
{
string json = string.Create(CultureInfo.InvariantCulture,
$"{{\"t\":\"graphics\",\"dpr\":{double.Parse(Resolution.Value, CultureInfo.InvariantCulture)},\"quality\":\"{AntiAlias.Value}\",\"filter\":\"{ColourStyle.Value}\",\"fps\":{B(ShowFps)},\"auto\":{B(AutoSmooth)},\"heat\":{B(HeatWarnings)},\"smooth\":{(SmoothMotion ? 60 : 0)},\"dev\":{B(Platform.DevMode.IsOn)}}}");
Ioc.Default.GetService<Skua.Flash.Ruffle.RuffleFlashUtil>()?.PostCommand(json);
}
private static string B(bool value) => value ? "true" : "false";
public static double StartupPixelRatio()
{
ISharedPreferences? prefs = global::Android.App.Application.Context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
Platform.DeviceProfile device = Platform.DeviceProfile.Current;
string value = prefs?.GetString("gfx.dpr", device.DefaultResolution) ?? device.DefaultResolution;
return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double ratio) && ratio > 0 ? ratio : device.StartupPixelRatio;
}
private static Choice Find(IReadOnlyList<Choice> choices, string? value)
{
foreach (Choice choice in choices)
{
if (choice.Value == value)
return choice;
}
return choices[0];
}
}
