using System;
namespace MoVibeSkua.Android.Diagnostics;
public static class CrashReasons
{
private const string WhyReport =
"What is sent: the error and where it happened (page, script, map), your phone's model, country and perf stats, " +
"MVS's settings, its recent log and a random install number. No personal details are sent. " +
"It shows Arglax where it broke so it can be fixed in an update. " +
"Turning reports off sends bare minimum info, just enough to know something happened (the error and where, " +
"nothing about your phone). To turn back on, the switch can be found on the Support page.";
public static string Explain(string kind, string error, string script)
{
error ??= string.Empty;
string ran = string.IsNullOrWhiteSpace(script) ? string.Empty : $" “{script}” was running, but it did not cause this.";
string what;
if (kind == "engine")
{
what = "What happened: the game engine (Ruffle, the Flash player inside MVS) broke and the game was reloaded. " +
"It is not your account and not a script's mistake." + ran + " " +
"It happens most while a map or a crowd of players loads, and more often when the phone is low on memory or hot. " +
"If it keeps happening: use private room, Bot only mode, or fewer army clients.";
}
else if (Has(error, "closed by Android for memory"))
{
what = "What happened: the phone ran out of memory and Android closed MVS. It is not a script's mistake. " +
"Closing other apps, fewer army clients, Bot only mode or lower graphics leave more room.";
}
else if (Has(error, "not responding"))
{
what = "What happened: MVS stopped answering touches and Android closed it. That is a fault in the app, not in a script.";
}
else if (Has(error, "native crash"))
{
what = "What happened: a part underneath the app (its runtime or Android's web view) crashed. " +
"That is not a script's mistake; updating Android System WebView from the Play Store sometimes helps.";
}
else if (Has(error, "kept running after a screen-reader error"))
{
what = "What happened: a screen reader or another accessibility app read a switch MVS could not describe. " +
"MVS kept running and nothing was lost.";
}
else if (Has(error, "kept running after an error"))
{
what = "What happened: something on screen failed inside the app, and MVS kept running instead of closing. " +
"It is a fault in the app, not in a script." + ran + " If something looks stuck, reopen the page or restart MVS.";
}
else if (Has(error, "Script could not start") || Has(error, "Script stopped"))
{
what = "What happened: the script ran into something MVS could not do. The script itself is fine; the fault is on MVS's side.";
}
else if (kind == "crash")
{
what = "What happened: a fault in the app itself closed MVS. It is not a script's mistake." + ran;
}
else
{
what = "What happened: something failed inside the app in the background. MVS kept running; it is not a script's mistake.";
}
string detail = Short(error);
return detail.Length > 0 ? $"{what}\n\nError: {detail}\n\n{WhyReport}" : $"{what}\n\n{WhyReport}";
}
private static bool Has(string text, string part) => text.Contains(part, StringComparison.OrdinalIgnoreCase);
private static string Short(string error)
{
string line = error.Split('\n')[0].Trim();
return line.Length > 160 ? line[..160] + "…" : line;
}
}
