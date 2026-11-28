using System;
using System.Linq;
using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
namespace MoVibeSkua.Android;
[Application]
public class MainApplication : AvaloniaAndroidApplication<App>
{
public MainApplication(IntPtr javaReference, JniHandleOwnership transfer)
: base(javaReference, transfer)
{
}
protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
{
builder = base.CustomizeAppBuilder(builder)
.With(new Avalonia.Media.FontManagerOptions { DefaultFamilyName = "avares://MoVibeSkua/Images/fonts#Roboto" })
.LogToTrace(Avalonia.Logging.LogEventLevel.Error);
if (Platform.ArmyMemory.GpuResourceBytes is long gpuBytes)
builder = builder.With(new SkiaOptions { MaxGpuResourceSizeBytes = gpuBytes });
return builder;
}
public override void OnCreate()
{
Skua.Flash.Ruffle.ControlChannel.IsUiThread = () => global::Android.OS.Looper.MyLooper() == global::Android.OS.Looper.MainLooper;
try
{
Platform.Army.SeparateWebViewData();
if (!System.Diagnostics.Trace.Listeners.OfType<Platform.LogcatTraceListener>().Any())
System.Diagnostics.Trace.Listeners.Add(new Platform.LogcatTraceListener());
Platform.LogcatLogMirror.Start();
Diagnostics.AppLog.Start();
Diagnostics.ProblemReports.Install();
if (Platform.Army.IsFirst)
Diagnostics.SelfTest.RunIfAsked();
}
catch (Exception ex)
{
System.Diagnostics.Trace.WriteLine($"[App] start-up helper failed: {ex}");
}
base.OnCreate();
}
}
