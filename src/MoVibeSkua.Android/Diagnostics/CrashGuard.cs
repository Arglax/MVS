using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
namespace MoVibeSkua.Android.Diagnostics;
public static class CrashGuard
{
private const int MaxInWindow = 5;
private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);
private static readonly object Gate = new();
private static readonly Queue<long> Recent = new();
private static readonly ConditionalWeakTable<Exception, object> Decided = new();
private static readonly HashSet<string> QuietSeen = new(StringComparer.Ordinal);
public static void InstallUi()
{
try
{
Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) =>
{
if (Survive(e.Exception))
e.Handled = true;
};
}
catch (Exception ex)
{
Trace.WriteLine($"[Guard] not installed on the UI loop: {ex.Message}");
}
}
public static bool Survive(Exception? error)
{
if (error is null)
return false;
try
{
lock (Gate)
{
if (Decided.TryGetValue(error, out object? earlier))
return (bool)earlier;
bool survive = Decide(error);
Decided.AddOrUpdate(error, survive);
return survive;
}
}
catch
{
return false;
}
}
private static bool Decide(Exception error)
{
Exception root = error.GetBaseException();
if (IsHopeless(error) || IsHopeless(root))
return false;
string text = error.ToString();
if (text.Contains("Avalonia.Android.Automation", StringComparison.Ordinal)
|| text.Contains("AvaloniaAccessHelper", StringComparison.Ordinal))
{
if (QuietSeen.Add("accessibility"))
{
Trace.WriteLine($"[Guard] screen-reader error ignored from now on: {root.GetType().Name}: {root.Message}");
ProblemReports.Save("problem", "mvs", error, "MVS kept running after a screen-reader error", null, fatal: false, feature: "guard");
}
return true;
}
long now = Stopwatch.GetTimestamp();
long oldest = now - (long)(Window.TotalSeconds * Stopwatch.Frequency);
while (Recent.Count > 0 && Recent.Peek() < oldest)
Recent.Dequeue();
if (Recent.Count >= MaxInWindow)
{
Trace.WriteLine($"[Guard] {MaxInWindow} errors in {Window.TotalSeconds:0} s: letting the app close ({root.GetType().Name})");
return false;
}
Recent.Enqueue(now);
Trace.WriteLine($"[Guard] kept running after {root.GetType().FullName}: {root.Message}");
ProblemReports.Save("problem", "mvs", error, "MVS kept running after an error", null, fatal: false, feature: "guard");
return true;
}
private static bool IsHopeless(Exception ex) => ex is OutOfMemoryException or StackOverflowException
or AccessViolationException or InvalidProgramException or BadImageFormatException
or TypeInitializationException or ThreadAbortException or Java.Lang.OutOfMemoryError
or Java.Lang.StackOverflowError;
}
