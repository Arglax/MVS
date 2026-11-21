using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Build = Android.OS.Build;
using BuildVersionCodes = Android.OS.BuildVersionCodes;
using AndroidX.Core.App;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Skua.Core.Interfaces;
using Skua.Core.Messaging;
using Skua.Core.Models;
namespace MoVibeSkua.Android.Diagnostics;
public static class RunInfo
{
public static string? ScriptName { get; internal set; }
public static DateTime? StartedUtc { get; internal set; }
public static string? MapName { get; internal set; }
public static TimeSpan? RunTime => StartedUtc is { } s ? DateTime.UtcNow - s : null;
}
public static class RunNotifier
{
public const string ActionRunAgain = "com.arglax.movibeskua.action.RUN_AGAIN";
public const string ActionCopy = "com.arglax.movibeskua.action.COPY_DETAILS";
private const string ChannelId = "script_results";
private const int FinishedId = 2101, ProblemId = 2102, ConnectionId = 2103;
private static readonly object Recipient = new();
private static bool _errored;
private static string _lastDetails = string.Empty;
private static string? _lastReportId;
private static Action? _pendingOnScreen;
private static Context Context => global::Android.App.Application.Context;
public static void Start()
{
CreateChannel();
StrongReferenceMessenger.Default.Register<ScriptStartedMessage, int>(Recipient, (int)MessageChannels.ScriptStatus, (_, _) => OnStarted());
StrongReferenceMessenger.Default.Register<ScriptStoppedMessage, int>(Recipient, (int)MessageChannels.ScriptStatus, (_, _) => OnStopped());
StrongReferenceMessenger.Default.Register<ReloginPausedMessage, int>(Recipient, (int)MessageChannels.GameEvents, (_, m) => OnReloginPaused(m));
StrongReferenceMessenger.Default.Register<ReloginSkippedMessage, int>(Recipient, (int)MessageChannels.GameEvents, (_, m) => OnReloginSkipped(m));
}
private static bool Helper => Bot.AutoEnhance.IsRunning;
private static void OnStarted()
{
if (Helper)
return;
_errored = false;
try
{
string? path = Ioc.Default.GetService<IScriptManager>()?.LoadedScript;
RunInfo.ScriptName = string.IsNullOrEmpty(path) ? null : Path.GetFileNameWithoutExtension(path);
}
catch
{
}
RunInfo.StartedUtc = DateTime.UtcNow;
Cancel(FinishedId);
Cancel(ProblemId);
Cancel(ConnectionId);
}
private static void OnStopped()
{
if (Helper || _errored)
return;
bool external = (Ioc.Default.GetService<IScriptManager>() as Skua.Core.Scripts.ScriptManager)?.LastStopWasExternal ?? true;
string clock = Clock();
Trace.WriteLine($"[Run] {RunInfo.ScriptName} ended after {clock}: {(external ? "stopped from outside (player, logout or re-login)" : "finished")}");
if (external)
return;
(string line, string details) = Platform.BotControl.Farmed();
Post(FinishedId, $"✅ {RunInfo.ScriptName ?? "Script"} finished ({clock})", details.Length > 0 ? details : line,
("Run again", ActionRunAgain));
}
public static void OnProblem(string script, bool starting, bool scriptsFault, string reason, string shortText, string details, string? reportId)
{
_errored = true;
_lastDetails = details;
_lastReportId = reportId;
string who = scriptsFault ? "problem in the script" : "problem in MVS";
string title = starting ? $"⚠️ {script} could not start: {who}" : $"⚠️ {script} stopped after {Clock()}: {who}";
string sent = scriptsFault
? "For the script team (Skua / VibeSkua / Tato)."
: reportId is not null ? "Open MVS to choose whether the report goes to Arglax." : "Problem reports are off: nothing is sent.";
string text = $"{shortText}\n{reason}\n{sent}";
Post(ProblemId, title, text, ("Copy details", ActionCopy), (scriptsFault ? "Run again" : "Restart script", ActionRunAgain));
}
private static void OnReloginSkipped(ReloginSkippedMessage m)
{
if (!m.ScriptWasRunning)
return;
Post(ConnectionId, $"📶 {RunInfo.ScriptName ?? "Script"} stopped: connection lost",
"Auto re-login is off, so MVS did not log back in. Turn on \"Log back in after a disconnect\" on the Accounts page to keep scripts running through disconnects.");
}
private static void OnReloginPaused(ReloginPausedMessage m)
{
if (!m.ScriptWasRunning)
return;
Post(ConnectionId, $"📶 {RunInfo.ScriptName ?? "Script"} paused: can't reach AQW",
$"Re-login failed {m.Rounds} round{(m.Rounds == 1 ? "" : "s")}. Trying again in {m.Minutes} min; the script restarts when it's back in.");
}
public static void ShowWhenOnScreen(Action show)
{
if (!Platform.BotControl.InBackground && MainActivity.Current is not null)
Dispatcher.UIThread.Post(show);
else
_pendingOnScreen += show;
}
public static void OnScreen()
{
Action? pending = _pendingOnScreen;
_pendingOnScreen = null;
if (pending is not null)
Dispatcher.UIThread.Post(pending);
}
public static void Handle(string action)
{
Trace.WriteLine($"[Run] notification: {action[(action.LastIndexOf('.') + 1)..]}");
switch (action)
{
case ActionCopy:
Usage.UsageTracker.Feature("notifCopyDetails");
try
{
(Context.GetSystemService(Context.ClipboardService) as ClipboardManager)?.PrimaryClip = (ClipData.NewPlainText("MVS details", _lastDetails));
}
catch (Exception ex)
{
Trace.WriteLine($"[Run] copy: {ex.Message}");
}
break;
case ActionRunAgain:
Usage.UsageTracker.Feature("notifRunAgain");
Cancel(FinishedId);
Cancel(ProblemId);
_ = Task.Run(async () =>
{
try
{
IScriptManager? scripts = Ioc.Default.GetService<IScriptManager>();
if (scripts is null || scripts.ScriptRunning || string.IsNullOrWhiteSpace(scripts.LoadedScript))
return;
Exception? ex = await scripts.StartScript();
if (ex is not null && Skua.App.Avalonia.ViewModels.ScriptLoaderViewModel.StartErrorHandler?.Invoke(ex) != true)
Trace.WriteLine($"[Run] start: {ex.Message}");
}
catch (Exception ex)
{
Trace.WriteLine($"[Run] run again: {ex.Message}");
}
});
break;
}
}
private static string Clock()
{
TimeSpan t = RunInfo.RunTime ?? TimeSpan.Zero;
return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
}
private static void CreateChannel()
{
if (Build.VERSION.SdkInt < BuildVersionCodes.O)
return;
NotificationChannel channel = new(ChannelId, "Script results", NotificationImportance.Default)
{
Description = "When a script finishes, stops on a problem, or can't reach AQW."
};
(Context.GetSystemService(Context.NotificationService) as NotificationManager)?.CreateNotificationChannel(channel);
}
private static void Post(int id, string title, string text, params (string Label, string Action)[] actions)
{
try
{
Intent open = new(Context, typeof(MainActivity));
open.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ReorderToFront | ActivityFlags.NewTask);
PendingIntent? tap = PendingIntent.GetActivity(Context, id, open, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
NotificationCompat.Builder builder = new NotificationCompat.Builder(Context, ChannelId)
.SetContentTitle(title)
.SetContentText(text.Split('\n')[0])
.SetStyle(new NotificationCompat.BigTextStyle().BigText(text))
.SetSmallIcon(Resource.Drawable.ic_notification)
.SetAutoCancel(true)
.SetContentIntent(tap)
.SetPriority(NotificationCompat.PriorityDefault);
int code = id * 10;
foreach ((string label, string action) in actions)
{
Intent intent = new(Context, typeof(Platform.KeepAliveService));
intent.SetAction(action);
builder.AddAction(0, label, PendingIntent.GetService(Context, ++code, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent));
}
NotificationManagerCompat.From(Context).Notify(id, builder.Build()!);
Trace.WriteLine($"[Run] notified: {title}");
}
catch (Exception ex)
{
Trace.WriteLine($"[Run] notify: {ex.Message}");
}
}
private static void Cancel(int id)
{
try
{
NotificationManagerCompat.From(Context).Cancel(id);
}
catch
{
}
}
}
