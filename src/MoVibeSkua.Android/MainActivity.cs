using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia;
using Avalonia.Android;
namespace MoVibeSkua.Android;
[Activity(
Label = "MVS",
Theme = "@style/MyTheme.NoActionBar",
Icon = "@mipmap/ic_launcher",
RoundIcon = "@mipmap/ic_launcher_round",
MainLauncher = true,
LaunchMode = LaunchMode.SingleTask,
ScreenOrientation = ScreenOrientation.SensorLandscape,
WindowSoftInputMode = SoftInput.AdjustResize,
ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.Density)]
public class MainActivity : AvaloniaMainActivity<App>
{
public static MainActivity? Current { get; private set; }
protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
{
return base.CustomizeAppBuilder(builder)
.With(new Avalonia.Media.FontManagerOptions { DefaultFamilyName = "avares://MoVibeSkua/Images/fonts#Roboto" })
.LogToTrace(Avalonia.Logging.LogEventLevel.Error);
}
protected override void OnCreate(Bundle? savedInstanceState)
{
Current = this;
Skua.Flash.Ruffle.ControlChannel.IsUiThread = () => global::Android.OS.Looper.MyLooper() == global::Android.OS.Looper.MainLooper;
Platform.Army.SeparateWebViewData();
if (!System.Diagnostics.Trace.Listeners.OfType<Platform.LogcatTraceListener>().Any())
System.Diagnostics.Trace.Listeners.Add(new Platform.LogcatTraceListener());
Platform.LogcatLogMirror.Start();
Diagnostics.AppLog.Start();
Diagnostics.ProblemReports.Install();
Platform.AndroidBootstrap.Prepare(this);
base.OnCreate(savedInstanceState);
Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
Services.PerformanceMode.Apply(this);
Services.PerformanceGuard.Instance.Initialize(this);
}
public override void OnTrimMemory(global::Android.Content.TrimMemory level)
{
base.OnTrimMemory(level);
if (level is global::Android.Content.TrimMemory.RunningCritical or global::Android.Content.TrimMemory.Complete)
Usage.UsageTracker.OnLowMemory();
Services.PerformanceGuard.OnTrimMemory(level);
}
private bool _touchOnGame;
private Handler? _handler;
public override bool DispatchTouchEvent(MotionEvent? ev)
{
if (ev?.ActionMasked == MotionEventActions.Down)
_touchOnGame = Controls.GameView.Contains(ev.RawX, ev.RawY);
if (!_touchOnGame && ev?.ActionMasked == MotionEventActions.Up)
return DeferTouchUp(ev);
if (!_touchOnGame || Controls.GameView.WebView?.Parent is not View host || !host.Focusable)
return base.DispatchTouchEvent(ev);
bool focusableInTouchMode = host.FocusableInTouchMode;
host.Focusable = false;
try
{
return base.DispatchTouchEvent(ev);
}
finally
{
host.Focusable = true;
host.FocusableInTouchMode = focusableInTouchMode;
}
}
private bool DeferTouchUp(MotionEvent ev)
{
MotionEvent up = MotionEvent.Obtain(ev);
(_handler ??= new Handler(Looper.MainLooper!)).PostAtFrontOfQueue(() =>
{
try
{
if (!IsDestroyed)
base.DispatchTouchEvent(up);
}
finally
{
up.Recycle();
}
});
return true;
}
public override bool DispatchKeyEvent(KeyEvent? e)
{
if (e is { IsSystem: false } && Controls.GameView.WebView is { HasFocus: true } game && game.DispatchKeyEvent(e))
return true;
return base.DispatchKeyEvent(e);
}
protected override void OnResume()
{
base.OnResume();
Controls.GameView.Resume();
Usage.UsageTracker.OnForeground();
Diagnostics.RunNotifier.OnScreen();
}
protected override void OnStop()
{
Platform.BotControl.SetInBackground(true);
base.OnStop();
}
protected override void OnStart()
{
base.OnStart();
Platform.BotControl.SetInBackground(false);
Platform.Headless.Reassert();
}
protected override void OnPause()
{
Usage.UsageTracker.OnBackground();
base.OnPause();
}
protected override void OnDestroy()
{
if (Current == this)
Current = null;
if (IsFinishing && !Platform.Army.IsFirst)
Platform.KeepAliveService.Release(this, Platform.Army.Slot);
base.OnDestroy();
}
}
