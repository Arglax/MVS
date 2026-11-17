using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Android.Content;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Newtonsoft.Json.Linq;
using Skua.Core.Interfaces;
using Skua.Core.Models;
namespace MoVibeSkua.Android.Bot;
public static class AutoEnhance
{
public static readonly string[] UseCases = { "Farming", "Bossing", "Ultra" };
public const string Name = "Use Best Enhancement";
private const string Prefs = "movibeskua.autoenhance";
private static ISharedPreferences? P => global::Android.App.Application.Context.GetSharedPreferences(Prefs, FileCreationMode.Private);
public static bool Enabled
{
get => P?.GetBoolean("enabled", false) ?? false;
set => P?.Edit()?.PutBoolean("enabled", value)?.Apply();
}
public static string UseCase
{
get
{
string v = P?.GetString("useCase", "Bossing") ?? "Bossing";
return UseCases.Contains(v) ? v : "Bossing";
}
set => P?.Edit()?.PutString("useCase", value)?.Apply();
}
public static readonly string[] Slots = { "class", "weapon", "helm", "cape" };
public static bool SlotOn(string slot) => P?.GetBoolean("slot." + slot, true) ?? true;
public static void SetSlot(string slot, bool on) => P?.Edit()?.PutBoolean("slot." + slot, on)?.Apply();
public static string LastResult
{
get => P?.GetString("last", "") ?? "";
private set => P?.Edit()?.PutString("last", value)?.Apply();
}
public sealed record GearRow(string Slot, string Item, string Now, string Best, bool Ok, bool Locked, string Note = "");
public static (List<GearRow> Rows, string Class, string UseCase, string Source, DateTime When) LastRead
{
get
{
try
{
JObject saved = JObject.Parse(P?.GetString("read", "{}") ?? "{}");
List<GearRow> rows = (saved["rows"] as JArray)?.Select(r => new GearRow((string?)r["Slot"] ?? "", (string?)r["Item"] ?? "",
(string?)r["Now"] ?? "", (string?)r["Best"] ?? "", (bool?)r["Ok"] ?? false, (bool?)r["Locked"] ?? false, (string?)r["Note"] ?? "")).ToList() ?? new();
return (rows, (string?)saved["class"] ?? "", (string?)saved["useCase"] ?? "", (string?)saved["source"] ?? "",
WhenOf(saved["when"]));
}
catch
{
return (new(), "", "", "", DateTime.MinValue);
}
}
}
private static DateTime WhenOf(JToken? token)
{
if (token is null)
return DateTime.MinValue;
if (token.Type == JTokenType.Date)
return token.Value<DateTime>().ToUniversalTime();
return DateTime.TryParse((string?)token, System.Globalization.CultureInfo.InvariantCulture,
System.Globalization.DateTimeStyles.RoundtripKind, out DateTime w) ? w.ToUniversalTime() : DateTime.MinValue;
}
public static event Action? Read;
public static string ScriptPath => Path.Combine(ClientFileSources.SkuaScriptsDIR, "MVS", "MvsAutoEnhance.cs");
private static bool _running;
public static bool IsRunning => _running;
public static void Install()
{
Skua.App.Avalonia.ViewModels.ScriptLoaderViewModel.BeforeStart = async () =>
{
if (Enabled)
await RunAsync(beforeScript: true);
};
}
public static async Task<string> RunAsync(bool beforeScript, bool preview = false)
{
if (_running)
return Name + " is already running.";
IScriptInterface bot = Ioc.Default.GetRequiredService<IScriptInterface>();
IScriptManager manager = Ioc.Default.GetRequiredService<IScriptManager>();
if (!bot.Player.LoggedIn)
return "Log in first.";
string? userScript = manager.LoadedScript;
if (string.Equals(userScript, ScriptPath, StringComparison.OrdinalIgnoreCase) || manager.ScriptRunning && !beforeScript)
return manager.ScriptRunning ? "Stop the running script first." : "";
_running = true;
try
{
string? problem = Generate(preview);
if (problem is not null)
{
LastResult = problem;
Notify(problem, failed: true);
return problem;
}
string resultFile = Path.Combine(ClientFileSources.SkuaOptionsDIR, "mvs-autoenhance-result.json");
try { File.Delete(resultFile); } catch { }
manager.SetLoadedScript(ScriptPath);
Exception? ex = await manager.StartScript();
if (ex is null)
{
DateTime until = DateTime.UtcNow.AddMinutes(5);
await Task.Delay(500);
while (manager.ScriptRunning && DateTime.UtcNow < until)
await Task.Delay(400);
if (manager.ScriptRunning)
await manager.StopScript();
}
if (!string.IsNullOrEmpty(userScript))
manager.SetLoadedScript(userScript);
string summary;
bool failed = false, show = false;
if (ex is not null)
{
summary = Name + " couldn't start: " + ex.Message;
failed = show = true;
}
else if (File.Exists(resultFile))
{
JObject r = JObject.Parse(File.ReadAllText(resultFile));
string status = (string?)r["Status"] ?? (string?)r["status"] ?? "ok";
summary = string.Join("\n", r["Lines"]?.Select(t => (string?)t) ?? Array.Empty<string?>());
failed = status is "failed" or "partial" or "error";
show = !preview && (failed || status == "enhanced" || !beforeScript);
if (r["Rows"] is JArray rows && rows.Count > 0)
{
P?.Edit()?.PutString("read", new JObject
{
["rows"] = rows, ["class"] = r["Class"], ["useCase"] = r["UseCase"], ["source"] = r["Source"],
["when"] = DateTime.UtcNow.ToString("o")
}.ToString(Newtonsoft.Json.Formatting.None))?.Apply();
}
}
else
{
summary = Name + " finished without a result.";
show = !beforeScript && !preview;
}
LastResult = $"{DateTime.Now:HH:mm} {summary}";
Trace.WriteLine("[AutoEnhance] " + (preview ? "read: " : "") + summary.Replace('\n', ' '));
Dispatcher.UIThread.Post(() => Read?.Invoke());
if (show)
Notify(summary, failed);
return summary;
}
catch (Exception e)
{
Trace.WriteLine("[AutoEnhance] " + e);
if (!string.IsNullOrEmpty(userScript))
try { manager.SetLoadedScript(userScript); } catch { }
return Name + " hit a problem: " + e.Message;
}
finally
{
_running = false;
}
}
private static void Notify(string text, bool failed)
{
Dispatcher.UIThread.Post(() =>
{
try
{
Ioc.Default.GetService<IDialogService>()?.ShowMessageBox(
text + (failed ? "\n\nThe script carries on with what you have." : ""),
failed ? Name + ": couldn't finish" : Name);
}
catch { }
});
}
public static string? Generate(bool preview = false)
{
string core = Path.Combine(ClientFileSources.SkuaScriptsDIR, "CoreAdvanced.cs");
if (!File.Exists(core))
return Name + " needs the community scripts: open Repo and download them first.";
string src = File.ReadAllText(core);
string? forge = ExtractFunction(src, "bool ForgeEnhancementLibrary()");
string? awe = ExtractFunction(src, "void AweEnhancementLibrary()");
if (forge is null || awe is null)
return Name + " couldn't read AUQW's enhancement list (CoreAdvanced.cs changed). Update the scripts in Repo, or tell the MVS Discord.";
StringBuilder lib = new();
lib.AppendLine("    private " + forge.Trim());
lib.AppendLine();
lib.AppendLine("    private " + awe.Trim());
lib.AppendLine();
foreach (Match m in Regex.Matches(src, @"public bool (u[A-Z]\w*)\(\)"))
lib.AppendLine($"    private bool {m.Groups[1].Value}() => Has(\"{m.Groups[1].Value}\");");
string[] aweClasses = Regex.Matches(awe, @"case\s+(""(?:[^""\\]|\\.)*"")\s*:").Select(m => m.Groups[1].Value).Distinct().ToArray();
lib.AppendLine("    private static readonly HashSet<string> AweClasses = new() { " + string.Join(", ", aweClasses) + " };");
foreach (string f in new[] { "EnhancementType CurrentClassEnh", "CapeSpecial CurrentCapeSpecial", "HelmSpecial CurrentHelmSpecial", "WeaponSpecial CurrentWeaponSpecial" })
{
string name = f.Split(' ')[1];
lib.AppendLine(name == "CurrentWeaponSpecial" ? $"    private {f}() => MvsWeaponSpecial();" : $"    private {f}() => Adv.{name}();");
}
string template = ReadResource("MvsAutoEnhance.template.txt");
string script = template.Replace("    //@@AUQW_LIBRARIES@@", lib.ToString());
string dir = Path.GetDirectoryName(ScriptPath)!;
Directory.CreateDirectory(dir);
File.WriteAllText(ScriptPath, script);
File.WriteAllText(Path.Combine(dir, "mvs-enhance-ultra.json"), ReadResource("mvs-enhance-ultra.json"));
Directory.CreateDirectory(ClientFileSources.SkuaOptionsDIR);
File.WriteAllText(Path.Combine(ClientFileSources.SkuaOptionsDIR, "mvs-autoenhance.json"),
new JObject { ["useCase"] = UseCase, ["preview"] = preview, ["slots"] = new JArray(Slots.Where(SlotOn).ToArray()) }.ToString());
return null;
}
private static string ReadResource(string name)
{
using Stream s = typeof(AutoEnhance).Assembly.GetManifestResourceStream(name)
?? throw new FileNotFoundException(name);
using StreamReader r = new(s);
return r.ReadToEnd();
}
public static string? ExtractFunction(string src, string signature)
{
int at = src.IndexOf(signature, StringComparison.Ordinal);
if (at < 0)
return null;
int open = src.IndexOf('{', at);
if (open < 0)
return null;
int depth = 0;
for (int i = open; i < src.Length; i++)
{
char c = src[i];
if (c == '/' && i + 1 < src.Length && src[i + 1] == '/')
{
int nl = src.IndexOf('\n', i);
i = nl < 0 ? src.Length : nl;
continue;
}
if (c == '/' && i + 1 < src.Length && src[i + 1] == '*')
{
int end = src.IndexOf("*/", i + 2, StringComparison.Ordinal);
i = end < 0 ? src.Length : end + 1;
continue;
}
if (c == '"')
{
for (i++; i < src.Length && src[i] != '"'; i++)
if (src[i] == '\\') i++;
continue;
}
if (c == '\'')
{
for (i++; i < src.Length && src[i] != '\''; i++)
if (src[i] == '\\') i++;
continue;
}
if (c == '{') depth++;
else if (c == '}' && --depth == 0)
return src.Substring(at, i - at + 1);
}
return null;
}
}
