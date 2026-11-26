using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Skua.Core.Interfaces;
namespace ButlerV5;
public static class GameServer
{
private static volatile string _lastGood = "";
private static readonly ConcurrentDictionary<string, byte> _loggedMismatches = new(StringComparer.Ordinal);
public static string CurrentName(IScriptInterface bot)
{
try
{
string? name = bot.Flash.GetGameObject("objServerInfo.sName");
if (!string.IsNullOrEmpty(name) && name != "null")
{
name = name.Trim().Trim('"').Trim();
if (name.Length > 0 && !LooksLikeAddress(name))
{
_lastGood = name;
return name;
}
}
}
catch
{
}
if (_lastGood.Length > 0)
return _lastGood;
try
{
string? lastName = bot.Servers?.LastName;
if (!string.IsNullOrEmpty(lastName) && !LooksLikeAddress(lastName))
return lastName.Trim();
}
catch
{
}
return "";
}
public static string Key(string? server)
{
if (string.IsNullOrWhiteSpace(server))
return "";
string s = server.Trim().Trim('"').Trim();
if (LooksLikeAddress(s))
return "";
int paren = s.IndexOf('(');
if (paren > 0)
s = s[..paren];
return s.Trim().ToLowerInvariant();
}
public static bool Same(string? theirs, string? mine, string? who = null)
{
string a = Key(theirs), b = Key(mine);
if (a.Length == 0 || b.Length == 0 || a == b)
return true;
if (_loggedMismatches.TryAdd($"{who}|{theirs}|{mine}", 0))
{
string line = $"server mismatch for {who}: theirs='{theirs}' mine='{mine}' (keys '{a}' vs '{b}')";
Trace.WriteLine("[ButlerV5] " + line);
DebugLog.Log("GameServer", line);
}
return false;
}
private static bool LooksLikeAddress(string s) =>
IPAddress.TryParse(s, out _) ||
s.EndsWith(".com", StringComparison.OrdinalIgnoreCase) ||
s.EndsWith(".net", StringComparison.OrdinalIgnoreCase) ||
s.Contains(':');
public static void LogCandidates(IScriptInterface bot)
{
string flash;
try { flash = bot.Flash.GetGameObject("objServerInfo.sName") ?? "(null)"; } catch (Exception ex) { flash = $"(err {ex.Message})"; }
string lastName;
try { lastName = bot.Servers?.LastName ?? "(null)"; } catch (Exception ex) { lastName = $"(err {ex.Message})"; }
string ip;
try { ip = bot.Player?.ServerIP ?? "(null)"; } catch (Exception ex) { ip = $"(err {ex.Message})"; }
string line = $"candidates: objServerInfo.sName='{flash}' Servers.LastName='{lastName}' ServerIP='{ip}' -> using '{CurrentName(bot)}'";
Trace.WriteLine("[ButlerV5] " + line);
DebugLog.Log("GameServer", line);
}
}
