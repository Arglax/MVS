using Skua.Core.Interfaces;
namespace ButlerV5;
public static class QuestBypass
{
public static readonly (string Map, int QuestId)[] MapSpecific =
{
("ultradage", 793),
("ultradarkon", 8733),
("ultradrago", 8395),
("ultraspeaker", 9125),
("ultradrakath", 3879),
("doomvault", 2954),
("doomvault", 3008),
("victormatsuri", 10294),
("astralshrine", 9802),
("astralshrine", 9803),
("novashrine", 9802),
("bocklincastle", 102520),
("bocklingrove", 10239),
("necroproject", 9901),
("wolfwing", 598),
("doomvaultb", 3004),
("doomvaultb", 3008),
("towerofdoom10", 3484),
("finalbattle", 3799),
("mummies", 4616),
("gluttony", 5915),
("borgars", 7522),
("downbelow", 8107),
("manacradle", 9126),
("liatarahill", 9814),
("vath", 354),
("chaoscave", 567),
("chaoscave", 597),
("ledgermayne", 847),
("chaoslord", 3879),
("chaoslord", 3880),
("queenbattle", 8361),
("trygve", 8298),
("thunderfang", 1170),
("shadowattack", 3799),
("hakuwar", 9607),
("championdrakath", 2814),
("alteonbattle", 3824),
("necrodungeon", 2059),
("battleunderc", 935),
("wanders", 976),
("wanders", 3773),
("titanattack", 8777),
("techfortress", 7646),
("stonewooddeep", 7650),
("blindingsnow", 899),
("mummies", 4614),
("pyramid", 4614),
("maloth", 6000),
("moonyardb", 1176),
("sandsea", 811),
("zorbaspalace", 7484),
("voidrefuge", 9531),
("darkoviagrave", 498),
("backroom", 8060),
("pyrewatch", 4077),
("astravia", 8000),
("firestorm", 1542),
("void", 904),
("rangda", 7622),
("starfest", 8094),
("badmoon", 9844),
("dawnfortress", 8297),
("dawnsanctum", 8297),
};
private const string AchievementPacket =
"{\"t\":\"xt\",\"b\":{\"r\":-1,\"o\":{\"cmd\":\"setAchievement\",\"field\":\"ia0\",\"index\":18,\"value\":1}}}";
private const string LevelPacket =
"{\"t\":\"xt\",\"b\":{\"r\":-1,\"o\":{\"cmd\":\"levelUp\",\"intExpToLevel\":\"0\",\"intLevel\":100}}}";
public static void ApplyCustom(IScriptInterface bot, List<int> customIds)
{
if (customIds.Count == 0)
{
DebugLog.Log("QuestBypass", "no custom quest IDs configured");
return;
}
DebugLog.Log("QuestBypass", $"custom option has {customIds.Count} id(s): {string.Join(",", customIds)}");
foreach (int id in customIds)
FakeQuest(bot, id, "custom option");
}
public static void ApplyForMap(IScriptInterface bot, string? mapName)
{
try
{
if (string.IsNullOrEmpty(mapName))
return;
int[] matches = MapSpecific
.Where(e => e.Map.Equals(mapName, StringComparison.OrdinalIgnoreCase))
.Select(e => e.QuestId)
.ToArray();
if (matches.Length == 0)
{
DebugLog.Log("QuestBypass", $"map '{mapName}': no bypass entries");
}
else
{
DebugLog.Log("QuestBypass", $"map '{mapName}': {matches.Length} bypass entr{(matches.Length == 1 ? "y" : "ies")}: {string.Join(",", matches)}");
foreach (int questId in matches)
FakeQuest(bot, questId, $"map {mapName}");
}
if (mapName.Equals("doomvaultb", StringComparison.OrdinalIgnoreCase))
{
DebugLog.Log("QuestBypass", "doomvaultb: sending achievement fake (ia0/18)");
bot.Send.ClientPacket(AchievementPacket, "json");
}
}
catch (Exception ex)
{
DebugLog.Log("QuestBypass", $"map apply failed: {ex.Message}");
}
}
private static void FakeQuest(IScriptInterface bot, int questId, string reason)
{
try
{
bot.Quests.Load(questId);
bot.Quests.UpdateQuest(questId);
DebugLog.Log("QuestBypass", $"UpdateQuest({questId}) sent ({reason})");
}
catch (Exception ex)
{
DebugLog.Log("QuestBypass", $"UpdateQuest({questId}) FAILED ({reason}): {ex.Message}");
}
}
public static void ApplyLevelFake(IScriptInterface bot)
{
try
{
if (bot.Player?.Level is < 100)
{
DebugLog.Log("QuestBypass", "applying client-side level 100 fake");
bot.Send.ClientPacket(LevelPacket, "json");
}
}
catch (Exception ex)
{
DebugLog.Log("QuestBypass", $"level fake failed: {ex.Message}");
}
}
public static List<int> ParseCustomIds(string? csv)
{
List<int> result = new();
if (string.IsNullOrWhiteSpace(csv))
return result;
foreach (string part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
{
if (int.TryParse(part, out int id) && id > 0)
result.Add(id);
}
return result;
}
}
