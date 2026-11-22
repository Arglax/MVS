using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Skua.Core.Interfaces;
using Skua.Core.Messaging;
using Skua.Core.Models;
namespace MoVibeSkua.Android.Diagnostics;
public static class ScriptProblems
{
public static void Start()
{
StrongReferenceMessenger.Default.Register<ScriptErrorMessage, int>(Recipient, (int)MessageChannels.ScriptStatus,
(_, m) => Handle(m.Exception, starting: false));
Skua.App.Avalonia.ViewModels.ScriptLoaderViewModel.StartErrorHandler = ex =>
{
Handle(ex, starting: true);
return true;
};
}
private static readonly object Recipient = new();
private static string ScriptName()
{
try
{
string? path = Ioc.Default.GetService<IScriptManager>()?.LoadedScript;
return string.IsNullOrEmpty(path) ? "The script" : Path.GetFileNameWithoutExtension(path);
}
catch
{
return "The script";
}
}
private static void Handle(Exception error, bool starting)
{
Exception unwrapped = ScriptVerdicts.Unwrap(error);
if (unwrapped is Skua.Core.Models.ScriptTrustException)
{
Trace.WriteLine($"[Script] {ScriptName()} not started: unofficial, not allowed by the player");
return;
}
if (starting && string.Equals(unwrapped.Message, "Script already running.", StringComparison.Ordinal))
{
Trace.WriteLine($"[Script] {ScriptName()} not started: another script is already running");
RunNotifier.ShowWhenOnScreen(() => Ioc.Default.GetService<IDialogService>()?.ShowMessageBox(
"Another script is already running. Stop it first before starting a new one.", "Script already running"));
return;
}
ScriptVerdicts.Verdict verdict;
try
{
verdict = ScriptVerdicts.Classify(error);
}
catch
{
verdict = new(ScriptVerdicts.Who.Mvs, "Something went wrong inside MVS while running the script.", ScriptVerdicts.FirstLine(error.Message));
}
string script = ScriptName();
Trace.WriteLine($"[Script] {script} {(starting ? "did not start" : "stopped")}: {verdict.Who} ({verdict.Reason}) {verdict.Short}");
if (verdict.Who == ScriptVerdicts.Who.Setup)
{
RunNotifier.ShowWhenOnScreen(() => Ioc.Default.GetService<IDialogService>()?.ShowMessageBox(
$"“{script}” could not start.\n\n{verdict.Short}", "Script could not start"));
return;
}
string? reportId = ProblemReports.Save("problem", verdict.Who == ScriptVerdicts.Who.Mvs ? "mvs" : "script", ScriptVerdicts.Unwrap(error),
$"Script {(starting ? "could not start" : "stopped")}: {verdict.Short}", script, fatal: false, feature: "script");
string details = Details(error, script, verdict);
RunNotifier.OnProblem(script, starting, verdict.Who == ScriptVerdicts.Who.Script, verdict.Reason, verdict.Short, details, reportId);
RunNotifier.ShowWhenOnScreen(() => Show(script, starting, verdict, details, reportId));
}
private static string Details(Exception error, string script, ScriptVerdicts.Verdict verdict)
{
StringBuilder sb = new();
sb.AppendLine($"Script: {script}");
sb.AppendLine($"App: MVS {AppLog.AppVersion()} on Android {(global::Android.OS.Build.VERSION.Release)}");
sb.AppendLine($"Problem: {verdict.Short}");
sb.AppendLine();
sb.AppendLine(ScriptVerdicts.Unwrap(error).ToString());
return AppLog.Redact(sb.ToString());
}
private static void Show(string script, bool starting, ScriptVerdicts.Verdict verdict, string details, string? reportId)
{
IDialogService? dialogs = Ioc.Default.GetService<IDialogService>();
if (dialogs is null)
return;
string what = starting ? "could not start" : "stopped";
string body;
string[] buttons;
if (verdict.Who == ScriptVerdicts.Who.Script)
{
body = $"“{script}” {what} because of a problem in the script itself, not in MVS.\n\n" +
$"{verdict.Reason}\n{verdict.Short}\n\n" +
"Please report it to the script team (Skua, VibeSkua and Tato) on their Discord so they can fix it. " +
"Scripts are theirs, not MVS's. “Copy details” copies what they need.";
buttons = new[] { "Copy details", "Report to script team", "OK" };
}
else
{
body = $"“{script}” {what} because of a problem in MVS, not in the script.\n\n" +
$"{verdict.Reason}\n{verdict.Short}\n\n" +
"Trying again often works. If MVS asks to send a report, Yes shows Arglax exactly where it broke so it can be " +
"fixed in an update; the MVS Discord can help too. “Copy details” copies what happened.";
buttons = new[] { "Copy details", "MVS Discord", "OK" };
}
DialogResult result = dialogs.ShowMessageBox(body, starting ? "Script could not start" : "Script stopped", buttons);
switch (result.Value)
{
case 0:
Ioc.Default.GetService<IClipboardService>()?.SetText(details);
break;
case 1:
ViewModels.SupportViewModel.Open(verdict.Who == ScriptVerdicts.Who.Script ? ViewModels.SupportViewModel.AuqwDiscordUrl : ViewModels.SupportViewModel.DiscordUrl);
break;
}
}
}
