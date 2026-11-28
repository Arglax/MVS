using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Skua.Core.Models;
namespace MoVibeSkua.Android.Platform;
public static class ArmyTeam
{
private static string ServerFile => Path.Combine(Army.StatusDirectory, "team-server.txt");
public static string RoomFile => Path.Combine(ClientFileSources.SkuaDIR, "army-room.txt");
public static string? Server
{
get
{
try { return File.Exists(ServerFile) ? File.ReadAllText(ServerFile).Trim() is { Length: > 0 } s ? s : null : null; }
catch { return null; }
}
}
public static void Set(string server, int room, IEnumerable<string> usernames)
{
try
{
Directory.CreateDirectory(Army.StatusDirectory);
File.WriteAllText(ServerFile, server);
Directory.CreateDirectory(ClientFileSources.SkuaDIR);
File.WriteAllText(RoomFile, room.ToString(System.Globalization.CultureInfo.InvariantCulture));
Directory.CreateDirectory(ClientFileSources.SkuaOptionsDIR);
foreach (string name in usernames)
SetCoreBotsRoom(name, room);
Trace.WriteLine($"[RunAll] team server={server} room={room} accounts={string.Join(",", usernames)}");
}
catch (Exception ex)
{
Trace.WriteLine($"[RunAll] team setup failed: {ex.Message}");
}
}
private static void SetCoreBotsRoom(string username, int room)
{
string dir = ClientFileSources.SkuaOptionsDIR;
string wanted = $"CBO_Storage({username}).txt";
List<string> files = Directory.EnumerateFiles(dir, "CBO_Storage(*).txt")
.Where(f => Path.GetFileName(f).Equals(wanted, StringComparison.OrdinalIgnoreCase)).ToList();
if (files.Count == 0)
files.Add(Path.Combine(dir, $"CBO_Storage({username.ToLowerInvariant()}).txt"));
foreach (string file in files)
{
List<string> lines = File.Exists(file) ? File.ReadAllLines(file).ToList() : new List<string>();
Put(lines, "PrivateRooms", "True");
Put(lines, "PrivateRoomNr", room.ToString(System.Globalization.CultureInfo.InvariantCulture));
File.WriteAllLines(file, lines);
}
}
private static void Put(List<string> lines, string name, string value)
{
int index = lines.FindIndex(l => l.StartsWith(name + ": ", StringComparison.Ordinal));
string line = $"{name}: {value}";
if (index >= 0)
lines[index] = line;
else
lines.Add(line);
}
}
