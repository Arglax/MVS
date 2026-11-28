using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Android.App;
using Android.Content;
namespace MoVibeSkua.Android.Platform;
public static class Army
{
public const int MaxClients = 12;
public static int ComfortableClients => DeviceProfile.Current.ComfortableClients;
private const string ProcessSuffix = ":army";
private static bool _webViewDataSeparated;
private static string? _lastPublished;
public static int Slot { get; } = ReadSlot();
public static bool IsFirst => Slot == 1;
public static string Label(int slot) => $"Client {slot}";
private static readonly (Type Activity, Type Service)[] Slots =
{
(typeof(Army2Activity), typeof(Army2Service)),
(typeof(Army3Activity), typeof(Army3Service)),
(typeof(Army4Activity), typeof(Army4Service)),
(typeof(Army5Activity), typeof(Army5Service)),
(typeof(Army6Activity), typeof(Army6Service)),
(typeof(Army7Activity), typeof(Army7Service)),
(typeof(Army8Activity), typeof(Army8Service)),
(typeof(Army9Activity), typeof(Army9Service)),
(typeof(Army10Activity), typeof(Army10Service)),
(typeof(Army11Activity), typeof(Army11Service)),
(typeof(Army12Activity), typeof(Army12Service)),
};
public static Type ActivityType(int slot) => slot == 1 ? typeof(MainActivity) : Slots[slot - 2].Activity;
public static Type ServiceType(int slot) => Slots[slot - 2].Service;
private static int ReadSlot()
{
string name;
try
{
name = File.ReadAllText("/proc/self/cmdline").TrimEnd('\0');
}
catch
{
return 1;
}
int at = name.LastIndexOf(ProcessSuffix, StringComparison.Ordinal);
return at >= 0 && int.TryParse(name.AsSpan(at + ProcessSuffix.Length), out int slot) ? slot : 1;
}
public static void SeparateWebViewData()
{
if (IsFirst || _webViewDataSeparated || !OperatingSystem.IsAndroidVersionAtLeast(28))
return;
_webViewDataSeparated = true;
global::Android.Webkit.WebView.SetDataDirectorySuffix($"army{Slot}");
}
public static Dictionary<int, int> RunningClients(Context context)
{
Dictionary<int, int> running = new();
if (context.GetSystemService(Context.ActivityService) is not ActivityManager am || am.RunningAppProcesses is not { } processes)
return running;
string package = context.PackageName!;
foreach (ActivityManager.RunningAppProcessInfo process in processes)
{
string? name = process.ProcessName;
if (name == package)
running[1] = process.Pid;
else if (name is not null && name.StartsWith(package + ProcessSuffix, StringComparison.Ordinal)
&& int.TryParse(name.AsSpan(package.Length + ProcessSuffix.Length), out int slot))
running[slot] = process.Pid;
}
return running;
}
public static int? NextFreeSlot(Context context)
{
Dictionary<int, int> running = RunningClients(context);
for (int slot = 2; slot <= MaxClients; slot++)
{
if (!running.ContainsKey(slot))
return slot;
}
return null;
}
public static void Open(Context context, int slot)
{
Intent intent = new(context, ActivityType(slot));
intent.AddFlags(ActivityFlags.NewTask);
context.StartActivity(intent);
}
public static void Close(Context context, int slot)
{
if (slot == 1)
return;
string activity = Java.Lang.Class.FromType(ActivityType(slot)).Name;
try
{
if (context.GetSystemService(Context.ActivityService) is ActivityManager am && am.AppTasks is { } tasks)
{
foreach (ActivityManager.AppTask task in tasks)
{
if (task.TaskInfo?.BaseIntent?.Component?.ClassName == activity)
task.FinishAndRemoveTask();
}
}
}
catch (Exception ex)
{
Trace.WriteLine($"[Army] closing client {slot}'s task failed: {ex.Message}");
}
KeepAliveService.Release(context, slot);
}
public sealed record ClientStatus(int Slot, bool LoggedIn, string Name, string Map, string Script, bool ScriptRunning,
string RunAllId = "", string RunAll = "", string RunAllError = "");
public sealed record Command(string Id, string Action, string Script, string Account, int[] Targets, long IssuedAt);
public const string ActionStart = "start";
public const string ActionStop = "stop";
public const string ActionLogin = "login";
private static string CommandPath => Path.Combine(StatusDirectory, "command.json");
public static Command SendCommand(string action, string script, string account, IEnumerable<int> targets)
{
Command command = new(Guid.NewGuid().ToString("N"), action, script, account, new List<int>(targets).ToArray(),
DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
string json = JsonSerializer.Serialize(command);
lock (PublishGate)
{
WithIoRetry(() =>
{
Directory.CreateDirectory(StatusDirectory);
string temp = CommandPath + $".{Slot}.tmp";
File.WriteAllText(temp, json);
File.Move(temp, CommandPath, true);
return true;
}, "write order");
}
Trace.WriteLine($"[RunAll] order id={command.Id[..8]} action={action} targets={string.Join(",", command.Targets)}");
return command;
}
public static Command? ReadCommand()
{
if (!File.Exists(CommandPath))
return null;
try
{
string text = WithIoRetry(() => File.ReadAllText(CommandPath), "read order");
return JsonSerializer.Deserialize<Command>(text);
}
catch
{
return null;
}
}
private static T WithIoRetry<T>(Func<T> step, string what)
{
for (int attempt = 1; ; attempt++)
{
try
{
return step();
}
catch (Exception ex) when (attempt < 4 && ex is IOException or UnauthorizedAccessException)
{
Trace.WriteLine($"[RunAll] client {Slot} step=file try={attempt}/4 result=retry reason=FileBusy detail=\"{what}: {ex.GetType().Name}: {ex.Message}\"");
System.Threading.Thread.Sleep(50 * attempt);
}
}
}
public static void ClearStatus(int slot)
{
try
{
File.Delete(StatusPath(slot));
}
catch
{
}
}
internal static string StatusDirectory => Path.Combine(global::Android.App.Application.Context.FilesDir!.AbsolutePath, "army");
private static string StatusPath(int slot) => Path.Combine(StatusDirectory, $"client{slot}.json");
private static readonly object PublishGate = new();
public static void Publish(ClientStatus status)
{
lock (PublishGate)
{
string json = JsonSerializer.Serialize(status);
if (json == _lastPublished)
return;
try
{
Directory.CreateDirectory(StatusDirectory);
string path = StatusPath(status.Slot);
string temp = path + ".tmp";
File.WriteAllText(temp, json);
File.Move(temp, path, true);
_lastPublished = json;
}
catch (Exception ex)
{
Trace.WriteLine($"[Army] status write failed: {ex.Message}");
}
}
}
public static ClientStatus? ReadStatus(int slot)
{
try
{
string path = StatusPath(slot);
return File.Exists(path) ? JsonSerializer.Deserialize<ClientStatus>(File.ReadAllText(path)) : null;
}
catch
{
return null;
}
}
}
