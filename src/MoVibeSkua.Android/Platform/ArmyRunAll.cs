using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Skua.App.Avalonia.ViewModels;
using Skua.Core.Interfaces;
using Skua.Core.Models;
namespace MoVibeSkua.Android.Platform;
public static class ArmyRunAll
{
public const int StaggerSeconds = 2;
private static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(90);
private static string _handled = string.Empty;
private static int _busy;
private static string _id = string.Empty;
private static string _state = string.Empty;
private static string _error = string.Empty;
public static void Poll(bool loggedIn)
{
Army.Command? command = Army.ReadCommand();
if (command is null || command.Id == _handled)
return;
if (!command.Targets.Contains(Army.Slot)
|| DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(command.IssuedAt) > MaxAge)
{
_handled = command.Id;
return;
}
if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
return;
_handled = command.Id;
_ = Task.Run(async () =>
{
try
{
await Execute(command, loggedIn);
}
catch (Exception ex)
{
Set(command.Id, "failed", "Something went wrong on this client");
LogStep(Army.Slot, command.Action, string.Empty, 1, 1, "fail", "Unexpected", $"{ex.GetType().Name}: {ex.Message}");
}
finally
{
Interlocked.Exchange(ref _busy, 0);
}
});
}
public static (string Id, string State, string Error) Report(bool scriptRunning)
{
string state = _state;
if (state == "running" && !scriptRunning)
state = "stopped";
return (_id, state, _error);
}
private static void Set(string id, string state, string error = "")
{
_id = id;
_state = state;
_error = error;
}
public static void LogStep(int slot, string step, string who, int attempt, int tries, string result,
string reason = "", string detail = "", TimeSpan? took = null)
{
string line = $"[RunAll] client {slot} step={step}";
if (who.Length > 0)
line += $" account={who}";
line += $" try={attempt}/{tries} result={result}";
if (took is { } t)
line += $" ms={(int)t.TotalMilliseconds}";
if (reason.Length > 0)
line += $" reason={reason}";
if (detail.Length > 0)
line += $" detail=\"{detail.Replace('"', '\'').Replace('\n', ' ').Replace('\r', ' ')}\"";
Trace.WriteLine(line);
}
public static string LogName(string username, int slot)
{
if (!Streamer.IsOn)
return username;
int index = SavedAccounts.Load().FindIndex(a => a.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
return index >= 0 ? Streamer.AccountAlias(index) : Streamer.Alias(slot);
}
public static (string Code, string Text) LoginReason(string? refusal)
{
string r = (refusal ?? string.Empty).ToLowerInvariant();
if (r.Contains("not saved"))
return ("NotSaved", "Account not saved on this phone");
if (r.Contains("timeout") || r.Contains("timed out"))
return ("AuthTimeout", "Login timed out");
if (r.Contains("full"))
return ("ServerFull", "Login failed: server full");
if (r.Contains("password") || r.Contains("invalid") || r.Contains("incorrect") || r.Contains("ban") || r.Contains("disabled") || r.Contains("locked"))
return ("Refused", "Login failed: wrong password or account banned");
if (r.Contains("maintenance") || r.Contains("offline"))
return ("ServerDown", "Login failed: server offline");
if (r.Contains("already"))
return ("Busy", "Login failed: a login was already running");
return ("NotCompleted", "Login did not complete");
}
private static readonly Regex CompileLine = new(@"(?:(?<file>[\w\-. ]+\.cs))?\s*\(?(?<line>\d+),\d+\)?\s*:\s*error\s+(?<code>CS\d+)\s*:\s*(?<msg>[^\r\n]+)",
RegexOptions.Compiled | RegexOptions.IgnoreCase);
public static (string Code, string Text, string Detail, bool Transient) StartReason(Exception ex, string script)
{
string message = Flatten(ex);
if (IsFilesBusy(ex, message))
{
Match include = IncludeFile.Match(message);
string file = include.Success ? Path.GetFileName(include.Groups[1].Value) : Path.GetFileName(script);
return ("FilesBusy", $"Script did not compile: {file} (files busy)", $"{ex.GetType().Name}: {FirstLine(message)}", true);
}
if (ex is ScriptCompileException)
{
if (message.Contains("Download All", StringComparison.OrdinalIgnoreCase))
return ("MissingIncludes", "Script needs files: open Script Repo, Download All", FirstLine(message), false);
Match m = CompileLine.Match(message);
if (m.Success)
{
string file = m.Groups["file"].Success && m.Groups["file"].Value.Trim().Length > 0
? m.Groups["file"].Value.Trim() : Path.GetFileName(script);
return ("CompileError", $"Script did not compile: {file} line {m.Groups["line"].Value}",
$"{file}:{m.Groups["line"].Value} {m.Groups["code"].Value} {m.Groups["msg"].Value}", false);
}
return ("CompileError", "Script did not compile", FirstLine(message), false);
}
return ex.GetType().Name switch
{
"ScriptVersionException" => ("Version", "Script needs a newer MVS", FirstLine(message), false),
"ScriptTrustException" => ("NotTrusted", "Script was not allowed to run", FirstLine(message), false),
_ => ("StartError", "Script did not start", $"{ex.GetType().Name}: {FirstLine(message)}", false),
};
}
private static readonly Regex IncludeFile = new(@"included file '([^']+)'", RegexOptions.Compiled);
private static string Flatten(Exception ex)
{
if (ex is AggregateException agg)
return string.Join(" | ", agg.Flatten().InnerExceptions.Select(e => e.Message).Prepend(agg.Message));
return ex.InnerException is null ? ex.Message ?? string.Empty : $"{ex.Message} | {ex.InnerException.Message}";
}
private static bool IsFilesBusy(Exception ex, string message)
{
Exception[] all = ex is AggregateException agg ? agg.Flatten().InnerExceptions.Append(ex).ToArray() : new[] { ex, ex.InnerException! };
return all.Any(e => e is IOException or UnauthorizedAccessException or BadImageFormatException)
|| message.Contains("Bad IL format", StringComparison.OrdinalIgnoreCase)
|| message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase)
|| message.Contains("files busy", StringComparison.OrdinalIgnoreCase)
|| message.Contains("Timeout waiting for script compilation", StringComparison.OrdinalIgnoreCase);
}
private static string FirstLine(string text)
{
string line = (text ?? string.Empty).Split('\n')[0].Trim();
return line.Length > 200 ? line[..197] + "..." : line;
}
private static async Task Execute(Army.Command command, bool loggedIn)
{
switch (command.Action)
{
case Army.ActionLogin:
await Login(command, loggedIn);
break;
case Army.ActionStop:
await Stop(command);
break;
case Army.ActionStart:
await Start(command);
break;
}
}
private static async Task Login(Army.Command command, bool loggedIn)
{
string who = LogName(command.Account, Army.Slot);
IScriptInterface bot0 = Ioc.Default.GetRequiredService<IScriptInterface>();
string? teamServer = ArmyTeam.Server;
string? onServer = Skua.Core.Scripts.ScriptServers.ConnectedServerName;
if (loggedIn && teamServer is not null && onServer is not null && !onServer.Equals(teamServer, StringComparison.OrdinalIgnoreCase)
&& await InGameAsync(bot0))
{
LogStep(Army.Slot, "login", who, 1, 1, "retry", "WrongServer", $"on={onServer} team={teamServer}");
Set(command.Id, "moving to " + teamServer);
bot0.Servers.Logout();
await Task.Delay(4000);
loggedIn = false;
}
if (loggedIn && await InGameAsync(bot0))
{
Set(command.Id, "in game");
LogStep(Army.Slot, "login", who, 1, 1, "ok", "AlreadyInGame");
return;
}
Set(command.Id, "logging in");
Stopwatch sw = Stopwatch.StartNew();
MoVibeSkua.Android.ViewModels.LoginViewModel login = Ioc.Default.GetRequiredService<MoVibeSkua.Android.ViewModels.LoginViewModel>();
Skua.Core.Scripts.ScriptServers.TeamServerName = ArmyTeam.Server;
Skua.Core.Scripts.ScriptServers.StayOnPreferredServer = true;
(bool ok, string refusal) = await Dispatcher.UIThread.InvokeAsync(() => login.LoginSavedAsync(command.Account, ArmyTeam.Server));
Skua.Core.Scripts.ScriptServers.StayOnPreferredServer = false;
if (ok)
{
Set(command.Id, "in game");
LogStep(Army.Slot, "login", who, 1, 1, "ok", took: sw.Elapsed);
return;
}
(string code, string text) = LoginReason(refusal);
Set(command.Id, "failed", text);
LogStep(Army.Slot, "login", who, 1, 1, "fail", code, refusal, sw.Elapsed);
}
private static async Task<bool> InGameAsync(IScriptInterface bot)
{
for (int read = 0; read < 2; read++)
{
if (read > 0)
await Task.Delay(1000);
try
{
if (!bot.Player.LoggedIn || !bot.Flash.IsWorldLoaded || string.IsNullOrEmpty(bot.Player.Username))
return false;
}
catch
{
return false;
}
}
return true;
}
private static async Task Stop(Army.Command command)
{
IScriptManager scripts = Ioc.Default.GetRequiredService<IScriptManager>();
if (scripts.ScriptRunning)
await scripts.StopScript();
Set(command.Id, "stopped");
LogStep(Army.Slot, "stop", string.Empty, 1, 1, "ok");
}
private static async Task Start(Army.Command command)
{
int index = Array.IndexOf(command.Targets, Army.Slot);
Set(command.Id, "waiting");
await Task.Delay(TimeSpan.FromSeconds(StaggerSeconds * Math.Max(0, index)));
Set(command.Id, "starting");
string scriptName = Path.GetFileName(command.Script);
IScriptInterface bot = Ioc.Default.GetRequiredService<IScriptInterface>();
if (!await InGameAsync(bot))
{
Set(command.Id, "failed", "Not logged in");
LogStep(Army.Slot, "start", string.Empty, 1, 1, "fail", "NotLoggedIn", scriptName);
return;
}
if (!File.Exists(command.Script))
{
Set(command.Id, "failed", "Script file not found on this phone");
LogStep(Army.Slot, "start", string.Empty, 1, 1, "fail", "NoFile", command.Script);
return;
}
IScriptManager scripts = Ioc.Default.GetRequiredService<IScriptManager>();
if (scripts.ScriptRunning)
{
await scripts.StopScript();
for (int i = 0; i < 20 && scripts.ScriptRunning; i++)
await Task.Delay(500);
if (scripts.ScriptRunning)
{
Set(command.Id, "failed", "The running script did not stop");
LogStep(Army.Slot, "stop-own", string.Empty, 1, 1, "fail", "StopTimeout", Path.GetFileName(scripts.LoadedScript ?? string.Empty));
return;
}
}
StopButlerFollow();
const int tries = 2;
for (int attempt = 1; attempt <= tries; attempt++)
{
Stopwatch sw = Stopwatch.StartNew();
LogStep(Army.Slot, "start", string.Empty, attempt, tries, "begin", detail: scriptName);
Exception? ex;
try
{
scripts.SetLoadedScript(command.Script);
if (ScriptLoaderViewModel.BeforeStart is { } before)
{
try { await before(); } catch { }
}
ex = await scripts.StartScript();
}
catch (Exception thrown)
{
ex = thrown;
}
if (ex is null)
{
Set(command.Id, "running");
LogStep(Army.Slot, "start", string.Empty, attempt, tries, "ok", detail: scriptName, took: sw.Elapsed);
return;
}
(string code, string text, string detail, bool transient) = StartReason(ex, command.Script);
bool retry = transient && attempt < tries;
LogStep(Army.Slot, "start", string.Empty, attempt, tries, retry ? "retry" : "fail", code, detail, sw.Elapsed);
if (!retry)
{
Set(command.Id, "failed", text);
return;
}
Set(command.Id, "starting", "Script files were busy, retrying");
await Task.Delay(TimeSpan.FromSeconds(5));
}
}
private static void StopButlerFollow()
{
try
{
IPluginContainer? container = Ioc.Default.GetService<IPluginManager>()?.Containers.FirstOrDefault(c =>
c.Plugin.Name.Equals("Butler", StringComparison.OrdinalIgnoreCase) ||
c.Plugin.GetType().Name.Contains("Butler", StringComparison.OrdinalIgnoreCase));
if (container is null)
return;
Type type = container.Plugin.GetType();
if (type.GetProperty("FollowingWho")?.GetValue(container.Plugin) is string)
{
type.GetMethod("StopFollow", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes)?.Invoke(container.Plugin, null);
LogStep(Army.Slot, "butler-off", string.Empty, 1, 1, "ok");
}
}
catch (Exception ex)
{
LogStep(Army.Slot, "butler-off", string.Empty, 1, 1, "fail", "Butler", $"{ex.GetType().Name}: {ex.Message}");
}
}
}
