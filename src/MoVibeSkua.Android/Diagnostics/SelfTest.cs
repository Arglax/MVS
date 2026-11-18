using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Threading;
using MoVibeSkua.Android.Views;
namespace MoVibeSkua.Android.Diagnostics;
public static class SelfTest
{
public static void RunIfAsked()
{
_ = Task.Run(async () =>
{
try
{
string mode = ReadProperty("debug.mvs.selftest");
if (mode.Length == 0)
return;
for (int i = 0; i < 60 && MobileShellView.Instance is null; i++)
await Task.Delay(1000);
await Task.Delay(6000);
Trace.WriteLine($"[SelfTest] running '{mode}'");
switch (mode)
{
case "ui":
Dispatcher.UIThread.Post(() => throw new InvalidOperationException("MVS self-test: error on the UI thread"));
break;
case "android":
new global::Android.OS.Handler(global::Android.OS.Looper.MainLooper!).Post(
() => throw new InvalidOperationException("MVS self-test: error in an Android callback"));
break;
case "engine":
ProblemReports.SaveEngine("30|400\nRuntimeError: MVS self-test: engine fault\n    at self-test");
break;
case "popup-engine":
CrashPrompt.ShowSample("engine", "Game engine crashed: Uncaught RuntimeError: memory access out of bounds", "DiamondTokenofDage");
break;
case "popup-guard":
CrashPrompt.ShowSample("problem", "MVS kept running after an error (System.ArgumentException: -80 is not a valid value for 'Width'.)", string.Empty);
break;
case "popup-memory":
CrashPrompt.ShowSample("crash", "MVS closed unexpectedly: closed by Android for memory", string.Empty);
break;
case "flood":
for (int i = 0; i < 8; i++)
{
int n = i;
Dispatcher.UIThread.Post(() => throw new InvalidOperationException($"MVS self-test: flood {n}"));
}
break;
}
await Task.Delay(3000);
Trace.WriteLine("[SelfTest] still running after the test");
}
catch (Exception ex)
{
Trace.WriteLine($"[SelfTest] {ex.Message}");
}
});
}
private static string ReadProperty(string name)
{
try
{
using Java.Lang.Process? process = Java.Lang.Runtime.GetRuntime()?.Exec(new[] { "getprop", name });
if (process?.InputStream is null)
return string.Empty;
using System.IO.StreamReader reader = new(process.InputStream);
return reader.ReadToEnd().Trim();
}
catch
{
return string.Empty;
}
}
}
