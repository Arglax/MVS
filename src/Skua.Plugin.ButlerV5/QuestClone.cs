using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using Skua.Core.Interfaces;
namespace ButlerV5;
public static class QuestClone
{
public const int MaxSlots = 800;
public const string Suffix = "_butlerv5_quests";
public static string PathFor(string username) =>
Path.Combine(SyncFile.Directory, $"{username.ToLowerInvariant()}{Suffix}");
public static int[]? ReadOwnSlots(IScriptInterface bot)
{
try
{
Stopwatch sw = Stopwatch.StartNew();
int[] values = new int[MaxSlots];
for (int slot = 0; slot < MaxSlots; slot++)
{
try
{
values[slot] = bot.Flash.CallGameFunction<int>("world.getQuestValue", slot);
}
catch
{
values[slot] = 0;
}
}
sw.Stop();
DebugLog.Log("QuestClone", $"read {MaxSlots} own quest slots in {sw.ElapsedMilliseconds}ms " +
$"({values.Count(v => v > 0)} non-zero)");
return values;
}
catch (Exception ex)
{
DebugLog.Log("QuestClone", $"reading own slots failed: {ex.Message}");
return null;
}
}
private static readonly string[] QuestStringFields =
{ "strQuests", "strQuests2", "strQuests3", "strQuests4", "strQuests5", "strQuests6", "strQuests7", "strQuests8", "strQuests9" };
public static bool Publish(IScriptInterface bot, string username)
{
try
{
string content;
Stopwatch sw = Stopwatch.StartNew();
string? json = null;
try { json = bot.Flash.GetGameObject("world.myAvatar.objData"); } catch { }
Dictionary<string, string>? strings = ExtractQuestStrings(json);
if (strings != null)
{
sw.Stop();
DebugLog.Log("QuestClone", $"read quest strings via objData in {sw.ElapsedMilliseconds}ms (1 flash call, {strings.Count} strings)");
StringBuilder sb = new();
foreach ((string field, string value) in strings)
sb.Append(field).Append('=').Append(value).Append('\n');
foreach ((int slot, int value) in DecodeSlots(strings))
sb.Append(slot).Append('=').Append(value).Append('\n');
content = sb.ToString();
}
else
{
DebugLog.Log("QuestClone", "objData quest strings not found - falling back to per-slot read");
int[]? values = ReadOwnSlots(bot);
if (values == null)
return false;
StringBuilder sb = new();
for (int slot = 0; slot < values.Length; slot++)
{
if (values[slot] > 0)
sb.Append(slot).Append('=').Append(values[slot]).Append('\n');
}
content = sb.ToString();
}
string path = PathFor(username);
try
{
if (File.Exists(path) && File.ReadAllText(path) == content)
{
DebugLog.Log("QuestClone", "publish skipped (quest state unchanged)");
return false;
}
}
catch
{
}
Directory.CreateDirectory(SyncFile.Directory);
File.WriteAllText(path, content);
DebugLog.Log("QuestClone", $"published quest state ({content.Count(c => c == '\n')} slots) to {Path.GetFileName(path)}");
return true;
}
catch (Exception ex)
{
DebugLog.Log("QuestClone", $"publish failed: {ex.Message}");
return false;
}
}
public static DateTime GetFileTime(string masterUsername)
{
try
{
string path = PathFor(masterUsername);
return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
}
catch
{
return DateTime.MinValue;
}
}
public static void ApplyFromMaster(IScriptInterface bot, string masterUsername)
{
try
{
string path = PathFor(masterUsername);
if (!File.Exists(path))
{
DebugLog.Log("QuestClone", $"no quest file from {masterUsername} - is cloning enabled on their client?");
return;
}
List<(string Field, string Value)> strings = new();
List<(int Slot, int Value)> slots = new();
foreach (string line in File.ReadAllLines(path))
{
int eq = line.IndexOf('=');
if (eq <= 0)
continue;
string key = line[..eq];
string value = line[(eq + 1)..];
if (key.StartsWith("strQuests", StringComparison.OrdinalIgnoreCase))
strings.Add((key, value));
else if (int.TryParse(key, out int slot) && int.TryParse(value, out int v) &&
slot >= 0 && slot < MaxSlots && v > 0)
slots.Add((slot, v));
}
Stopwatch sw = Stopwatch.StartNew();
if (strings.Count > 0)
{
Dictionary<string, string>? own = null;
try { own = ExtractQuestStrings(bot.Flash.GetGameObject("world.myAvatar.objData")); }
catch { }
if (own == null)
DebugLog.Log("QuestClone", "could not read own quest strings - applying master's as-is");
int set = 0;
foreach ((string field, string masterValue) in strings)
{
string merged = own != null && own.TryGetValue(field, out string? mine)
? MergeMax(mine, masterValue)
: masterValue;
try
{
bot.Flash.SetGameObject($"world.myAvatar.objData.{field}", merged);
set++;
}
catch (Exception ex)
{
DebugLog.Log("QuestClone", $"SetGameObject({field}) failed: {ex.Message}");
}
}
sw.Stop();
DebugLog.Log("QuestClone",
$"(experimental clone) merged & set {set}/{strings.Count} quest strings from {masterUsername} in {sw.ElapsedMilliseconds}ms (merge-max)");
}
else
{
int applied = 0;
foreach ((int slot, int value) in slots)
{
bot.Quests.UpdateQuest(value, slot);
applied++;
}
sw.Stop();
DebugLog.Log("QuestClone",
$"(experimental clone) applied {applied} slots from {masterUsername} in {sw.ElapsedMilliseconds}ms (blind apply fallback)");
}
}
catch (Exception ex)
{
DebugLog.Log("QuestClone", $"apply failed: {ex.Message}");
}
}
private const char MaxQuestChar = 'z';
public static void UnlockAll(IScriptInterface bot)
{
try
{
if (bot.Player?.LoggedIn != true || bot.Map?.Loaded != true)
return;
Dictionary<string, string>? strings = null;
try { strings = ExtractQuestStrings(bot.Flash.GetGameObject("world.myAvatar.objData")); }
catch { }
if (strings == null || strings.Count == 0)
{
DebugLog.Log("QuestClone", "unlock-all: objData quest strings not readable yet - skipping");
return;
}
int maxed = 0;
foreach ((string field, string value) in strings)
{
if (value.Length == 0)
continue;
try
{
bot.Flash.SetGameObject($"world.myAvatar.objData.{field}", new string(MaxQuestChar, value.Length));
maxed++;
}
catch (Exception ex)
{
DebugLog.Log("QuestClone", $"unlock-all SetGameObject({field}) failed: {ex.Message}");
}
}
DebugLog.Log("QuestClone", $"unlocked all quests (client-side, {maxed} strings maxed to '{MaxQuestChar}')");
}
catch (Exception ex)
{
DebugLog.Log("QuestClone", $"unlock-all failed: {ex.Message}");
}
}
private static Dictionary<string, string>? ExtractQuestStrings(string? json)
{
if (string.IsNullOrEmpty(json))
return null;
try
{
using JsonDocument doc = JsonDocument.Parse(json);
Dictionary<string, string> result = new();
foreach (string field in QuestStringFields)
{
if (doc.RootElement.TryGetProperty(field, out JsonElement el) &&
el.ValueKind == JsonValueKind.String)
result[field] = el.GetString() ?? "";
}
return result.Count > 0 ? result : null;
}
catch
{
return null;
}
}
private static List<(int Slot, int Value)> DecodeSlots(Dictionary<string, string> strings)
{
List<(int, int)> result = new();
foreach ((string field, string value) in strings)
{
int block = field == "strQuests" ? 0 : int.TryParse(field["strQuests".Length..], out int n) ? n - 1 : -1;
if (block < 0)
continue;
for (int i = 0; i < value.Length; i++)
{
int v = DecodeChar(value[i]);
if (v > 0)
result.Add((block * 100 + i, v));
}
}
return result;
}
private static int DecodeChar(char c) => c switch
{
>= '0' and <= '9' => c - '0',
>= 'A' and <= 'Z' => c - 'A' + 10,
>= 'a' and <= 'z' => c - 'a' + 36,
_ => 0,
};
private static string MergeMax(string mine, string theirs)
{
int length = Math.Max(mine.Length, theirs.Length);
StringBuilder sb = new(length);
for (int i = 0; i < length; i++)
{
char a = i < mine.Length ? mine[i] : '0';
char b = i < theirs.Length ? theirs[i] : '0';
sb.Append(DecodeChar(a) >= DecodeChar(b) ? a : b);
}
return sb.ToString();
}
}
