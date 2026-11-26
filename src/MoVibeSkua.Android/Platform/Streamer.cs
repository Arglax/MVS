using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
namespace MoVibeSkua.Android.Platform;
public static class Streamer
{
public const string BaseAlias = "MVS VibeLord";
public const string HeaderAlias = "MVS User";
public const string Guild = "MVS";
public const string EditBlockedNote = "Turn off Streamer Mode to edit accounts";
private static bool _isOn;
private static long _stamp = -1;
public static event Action<bool>? Changed;
private static string FlagPath => Path.Combine(global::Android.App.Application.Context.FilesDir!.AbsolutePath, "streamer.flag");
public static bool IsOn => _isOn;
public static string Alias(int slot) => $"VibeLord {slot}";
public static string OwnAlias => BaseAlias;
public static void Init(bool fallback)
{
try
{
if (!File.Exists(FlagPath))
Write(fallback);
}
catch { }
Refresh();
}
public static void Set(bool on)
{
if (on == _isOn && File.Exists(FlagPath))
return;
Write(on);
Refresh();
}
public static void Refresh()
{
try
{
FileInfo file = new(FlagPath);
long stamp = file.Exists ? file.LastWriteTimeUtc.Ticks ^ file.Length : 0;
if (stamp == _stamp)
return;
_stamp = stamp;
bool on = file.Exists && File.ReadAllText(FlagPath).Trim() == "1";
if (on == _isOn)
return;
_isOn = on;
Changed?.Invoke(on);
}
catch { }
}
private static void Write(bool on)
{
try
{
string temp = FlagPath + "." + Army.Slot + ".tmp";
File.WriteAllText(temp, on ? "1" : "0");
File.Move(temp, FlagPath, true);
}
catch { }
}
private static readonly Regex Emails = new(@"[\w.+-]+@[\w-]+\.[\w.-]+", RegexOptions.Compiled);
public static string Mask(string? text)
{
if (!_isOn || string.IsNullOrEmpty(text))
return text ?? string.Empty;
foreach ((string name, string alias) in KnownNames())
text = Regex.Replace(text, @"(?<![\w])" + Regex.Escape(name) + @"(?![\w])", alias, RegexOptions.IgnoreCase);
return Emails.Replace(text, Alias(Army.Slot));
}
private static string? _ownName, _ownGuild;
public static void RememberOwn(string? name, string? guild)
{
if (!string.IsNullOrWhiteSpace(name) && name != "loginInfo.strUsername")
{
if (_ownName != name.Trim())
_namesAt = 0;
_ownName = name.Trim();
}
if (!string.IsNullOrWhiteSpace(guild))
_ownGuild = guild.Trim();
}
private static List<(string Name, string Alias)> _names = new();
private static long _namesAt;
private static List<(string Name, string Alias)> KnownNames()
{
long now = Environment.TickCount64;
if (now - _namesAt < 2000)
return _names;
_namesAt = now;
return _names = BuildNames();
}
private static List<(string Name, string Alias)> BuildNames()
{
Dictionary<string, string> names = new(StringComparer.OrdinalIgnoreCase);
void Add(string? name, string alias)
{
if (!string.IsNullOrWhiteSpace(name) && name.Trim().Length >= 2 && !names.ContainsKey(name.Trim()))
names[name.Trim()] = alias;
}
Add(_ownName, Alias(Army.Slot));
Add(_ownGuild, Guild);
for (int slot = 1; slot <= Army.MaxClients; slot++)
{
if (slot == Army.Slot)
continue;
try
{
Add(Army.ReadStatus(slot)?.Name, Alias(slot));
}
catch { }
}
try
{
List<SavedAccount> saved = SavedAccounts.Load();
for (int i = 0; i < saved.Count; i++)
Add(saved[i].Username, AccountAlias(i));
}
catch { }
return names.OrderByDescending(n => n.Key.Length).Select(n => (n.Key, n.Value)).ToList();
}
public static string AccountAlias(int index) => $"VibeLord {index + 1}";
}
