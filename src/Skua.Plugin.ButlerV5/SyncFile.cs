using System.Diagnostics;
using System.IO;
using System.Text;
namespace ButlerV5;
public class SyncData
{
public string Username = "";
public int Pid;
public string Server = "";
public string ClassName = "";
public string Map = "";
public string Room = "1";
public string Cell = "Enter";
public string Pad = "Spawn";
public bool LoggedIn;
public bool Attacking;
public bool OffGoto;
public List<string> Followers = new();
public string Following = "";
public bool ScriptOn;
public string ScriptName = "";
public string Declined = "";
public bool Parking;
public string SendAllTarget = "";
public string SendAllCommand = "";
public List<string> SendAllRecipients = new();
public long SendAllIssuedUtcTicks;
public DateTime LastModified { get; set; } = DateTime.MinValue;
public string MapWithRoom => string.IsNullOrEmpty(Room) ? Map : $"{Map}-{Room}";
public string Serialize()
{
StringBuilder sb = new();
sb.Append("username=").Append(Username).Append('\n');
sb.Append("pid=").Append(Pid).Append('\n');
sb.Append("server=").Append(Server).Append('\n');
sb.Append("class=").Append(ClassName).Append('\n');
sb.Append("map=").Append(Map).Append('\n');
sb.Append("room=").Append(Room).Append('\n');
sb.Append("cell=").Append(Cell).Append('\n');
sb.Append("pad=").Append(Pad).Append('\n');
sb.Append("loggedin=").Append(LoggedIn ? '1' : '0').Append('\n');
sb.Append("attacking=").Append(Attacking ? '1' : '0').Append('\n');
sb.Append("offgoto=").Append(OffGoto ? '1' : '0').Append('\n');
sb.Append("followers=").Append(string.Join(",", Followers)).Append('\n');
sb.Append("following=").Append(Following).Append('\n');
sb.Append("script=").Append(ScriptOn ? '1' : '0').Append('\n');
sb.Append("scriptname=").Append(ScriptName).Append('\n');
sb.Append("declined=").Append(Declined).Append('\n');
sb.Append("parking=").Append(Parking ? '1' : '0').Append('\n');
sb.Append("sendalltarget=").Append(SendAllTarget).Append('\n');
sb.Append("sendallcommand=").Append(SendAllCommand).Append('\n');
sb.Append("sendallrecipients=").Append(string.Join(",", SendAllRecipients)).Append('\n');
sb.Append("sendallissued=").Append(SendAllIssuedUtcTicks).Append('\n');
return sb.ToString();
}
public static SyncData? Parse(string? content)
{
if (string.IsNullOrWhiteSpace(content))
return null;
SyncData data = new();
bool sawUsername = false;
foreach (string rawLine in content.Split('\n'))
{
string line = rawLine.Trim();
int eq = line.IndexOf('=');
if (eq <= 0)
continue;
string key = line[..eq].Trim().ToLowerInvariant();
string value = line[(eq + 1)..].Trim();
switch (key)
{
case "username": data.Username = value; sawUsername = !string.IsNullOrEmpty(value); break;
case "pid": _ = int.TryParse(value, out data.Pid); break;
case "server": data.Server = value; break;
case "class": data.ClassName = value; break;
case "map": data.Map = value.ToLowerInvariant(); break;
case "room": data.Room = value; break;
case "cell": data.Cell = value; break;
case "pad": data.Pad = value; break;
case "loggedin": data.LoggedIn = value == "1"; break;
case "attacking": data.Attacking = value == "1"; break;
case "offgoto": data.OffGoto = value == "1"; break;
case "followers":
data.Followers = value
.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
.ToList();
break;
case "following": data.Following = value; break;
case "script": data.ScriptOn = value == "1"; break;
case "scriptname": data.ScriptName = value; break;
case "declined": data.Declined = value; break;
case "parking": data.Parking = value == "1"; break;
case "sendalltarget": data.SendAllTarget = value; break;
case "sendallcommand": data.SendAllCommand = value; break;
case "sendallrecipients":
data.SendAllRecipients = value
.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
.ToList();
break;
case "sendallissued": _ = long.TryParse(value, out data.SendAllIssuedUtcTicks); break;
}
}
return sawUsername ? data : null;
}
}
public static class SyncFile
{
public const string Suffix = "_butlerv5";
public static string Directory { get; } = Path.Combine(
Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
"Skua",
"character_locations");
public static string PathFor(string username) =>
Path.Combine(Directory, $"{username.ToLowerInvariant()}{Suffix}");
public static bool Write(SyncData data)
{
try
{
System.IO.Directory.CreateDirectory(Directory);
string path = PathFor(data.Username);
string temp = path + ".tmp";
string content = data.Serialize();
for (int attempt = 0; attempt < 3; attempt++)
{
try
{
File.WriteAllText(temp, content);
File.Move(temp, path, overwrite: true);
return true;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
Thread.Sleep(30);
}
}
try { File.Delete(temp); } catch { }
using FileStream fs = new(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
using StreamWriter sw = new(fs);
sw.Write(content);
return true;
}
catch
{
}
return false;
}
public static SyncData? Read(string path)
{
for (int attempt = 0; attempt < 3; attempt++)
{
try
{
if (!File.Exists(path))
return null;
DateTime lastWrite = File.GetLastWriteTimeUtc(path);
using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
using StreamReader sr = new(fs);
SyncData? data = SyncData.Parse(sr.ReadToEnd());
if (data != null)
data.LastModified = lastWrite;
return data;
}
catch (IOException)
{
Thread.Sleep(50);
}
catch
{
return null;
}
}
return null;
}
public static SyncData? ReadUser(string username) => Read(PathFor(username));
public static List<SyncData> ReadAll()
{
List<SyncData> result = new();
try
{
if (!System.IO.Directory.Exists(Directory))
return result;
foreach (string path in System.IO.Directory.EnumerateFiles(Directory, $"*{Suffix}"))
{
SyncData? data = Read(path);
if (data != null)
result.Add(data);
}
}
catch
{
}
return result;
}
public static bool IsOnline(SyncData data) =>
data.LoggedIn && data.Pid > 0 && IsProcessAlive(data.Pid, data.LastModified > DateTime.MinValue ? data.LastModified : null);
public static bool IsProcessAlive(int pid, DateTime? fileWriteTimeUtc = null)
{
if (pid <= 0)
return false;
if (pid == Environment.ProcessId)
return true;
if (!OperatingSystem.IsWindows())
return IsUnixProcessAlive(pid, fileWriteTimeUtc);
try
{
using Process process = Process.GetProcessById(pid);
if (process.HasExited)
return false;
string name = process.ProcessName.ToLowerInvariant();
if (!name.Contains("skua") && !name.Contains("vibeskua") && !name.Contains("movi") && name != "dotnet")
return false;
if (fileWriteTimeUtc.HasValue && fileWriteTimeUtc.Value > DateTime.MinValue)
{
try
{
if (process.StartTime.ToUniversalTime() > fileWriteTimeUtc.Value.AddMinutes(1))
return false;
}
catch
{
}
}
return true;
}
catch (ArgumentException)
{
return false;
}
catch
{
return false;
}
}
public static readonly TimeSpan FreshFor = TimeSpan.FromSeconds(45);
public static readonly TimeSpan TouchEvery = TimeSpan.FromSeconds(10);
private static readonly Lazy<bool> ProcHidden = new(() =>
{
try { return !System.IO.Directory.Exists("/proc/1"); }
catch { return true; }
});
public static void Touch(string username)
{
if (OperatingSystem.IsWindows() || !ProcHidden.Value)
return;
try
{
string path = PathFor(username);
if (File.Exists(path))
File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
}
catch
{
}
}
private static bool IsUnixProcessAlive(int pid, DateTime? fileWriteTimeUtc = null)
{
try
{
string procDir = $"/proc/{pid}";
if (!System.IO.Directory.Exists(procDir))
return ProcHidden.Value && fileWriteTimeUtc is { } written && DateTime.UtcNow - written < FreshFor;
try
{
string statPath = $"{procDir}/stat";
if (File.Exists(statPath))
{
string stat = File.ReadAllText(statPath);
int closeParen = stat.LastIndexOf(')');
if (closeParen > 0 && closeParen + 2 < stat.Length)
{
char state = stat[closeParen + 2];
if (state is 'Z' or 'X')
return false;
}
}
}
catch
{
}
try
{
string cmdlinePath = $"{procDir}/cmdline";
if (File.Exists(cmdlinePath))
{
string cmdline = File.ReadAllText(cmdlinePath).Replace('\0', ' ').Trim();
if (!string.IsNullOrEmpty(cmdline))
{
return cmdline.Contains("skua", StringComparison.OrdinalIgnoreCase)
|| cmdline.Contains("movi", StringComparison.OrdinalIgnoreCase)
|| cmdline.Contains("dotnet", StringComparison.OrdinalIgnoreCase);
}
}
}
catch
{
}
return true;
}
catch
{
return false;
}
}
}
