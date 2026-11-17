using Skua.Core.Interfaces;
namespace ButlerV5;
public static class GameServer
{
public static string CurrentName(IScriptInterface bot)
{
try
{
string? name = bot.Flash.GetGameObject("objServerInfo.sName");
if (!string.IsNullOrEmpty(name) && name != "null")
{
name = name.Trim().Trim('"').Trim();
if (name.Length > 0)
return name;
}
}
catch
{
}
try
{
string? lastName = bot.Servers?.LastName;
if (!string.IsNullOrEmpty(lastName))
return lastName;
}
catch
{
}
return bot.Player?.ServerIP ?? "";
}
public static void LogCandidates(IScriptInterface bot)
{
string flash;
try { flash = bot.Flash.GetGameObject("objServerInfo.sName") ?? "(null)"; } catch (Exception ex) { flash = $"(err {ex.Message})"; }
string lastName;
try { lastName = bot.Servers?.LastName ?? "(null)"; } catch (Exception ex) { lastName = $"(err {ex.Message})"; }
string ip;
try { ip = bot.Player?.ServerIP ?? "(null)"; } catch (Exception ex) { ip = $"(err {ex.Message})"; }
DebugLog.Log("GameServer", $"candidates: objServerInfo.sName='{flash}' Servers.LastName='{lastName}' ServerIP='{ip}' -> using '{CurrentName(bot)}'");
}
}
