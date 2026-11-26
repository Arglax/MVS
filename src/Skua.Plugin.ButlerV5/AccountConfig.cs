using System.IO;
using Skua.Core.Models;
using Skua.Core.Models.Skills;
namespace ButlerV5;
public enum ButlerClassType { None, Farm, Solo, Dodge, Boss }
public enum ParkSpot { House, Whitemap, Stay }
public static class AccountConfig
{
private static readonly string Dir = Path.Combine(
Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
"Skua",
"butlerv5_options");
private static string PathFor(string username) =>
Path.Combine(Dir, $"{username.ToLowerInvariant()}.txt");
public static ButlerClassType GetClassType(string username)
{
try
{
string path = PathFor(username);
if (!File.Exists(path))
return ButlerClassType.None;
foreach (string line in File.ReadAllLines(path))
{
if (line.StartsWith("classtype=", StringComparison.OrdinalIgnoreCase) &&
Enum.TryParse(line["classtype=".Length..].Trim(), ignoreCase: true, out ButlerClassType type))
return type;
}
}
catch
{
}
return ButlerClassType.None;
}
public static void SetClassType(string username, ButlerClassType type)
{
try
{
Directory.CreateDirectory(Dir);
File.WriteAllText(PathFor(username), $"classtype={type}\n");
}
catch
{
}
}
public const string CboCurrentClass = "[Current]";
public static string? GetCboClassName(string username, ButlerClassType type) => type switch
{
ButlerClassType.Farm => GetCboValue(username, "FarmClassSelect:"),
ButlerClassType.Solo => GetCboValue(username, "SoloClassSelect:"),
ButlerClassType.Dodge => GetCboValue(username, "DodgeClassSelect:"),
ButlerClassType.Boss => GetCboValue(username, "BossClassSelect:"),
_ => null,
};
public static string? GetCboClassMode(string username, ButlerClassType type) => type switch
{
ButlerClassType.Farm => GetCboValue(username, "FarmModeSelect:"),
ButlerClassType.Solo => GetCboValue(username, "SoloModeSelect:"),
ButlerClassType.Dodge => GetCboValue(username, "DodgeModeSelect:"),
ButlerClassType.Boss => GetCboValue(username, "BossModeSelect:"),
_ => null,
};
public static ClassUseMode ResolveSkillMode(string className, ClassUseMode wanted, ButlerClassType type,
IEnumerable<AdvancedSkill>? skillSets)
{
if (skillSets == null)
return wanted;
List<ClassUseMode> modes = skillSets
.Where(s => string.Equals(s.ClassName, className, StringComparison.CurrentCultureIgnoreCase))
.Select(s => s.ClassUseMode)
.ToList();
if (modes.Count == 0 || modes.Contains(wanted))
return wanted;
ClassUseMode? typeMode = type switch
{
ButlerClassType.Farm => ClassUseMode.Farm,
ButlerClassType.Solo => ClassUseMode.Solo,
ButlerClassType.Dodge => ClassUseMode.Dodge,
_ => null,
};
if (typeMode is ClassUseMode m && modes.Contains(m))
return m;
return modes.Contains(ClassUseMode.Base) ? ClassUseMode.Base : modes[0];
}
private static string? FindCboFile(string username)
{
string expected = $"CBO_Storage({username}).txt";
foreach (string optionsDir in new[]
{
ClientFileSources.SkuaOptionsDIR,
Path.Combine(ClientFileSources.LegacySkuaDIR, "options"),
})
{
if (!Directory.Exists(optionsDir))
continue;
string? file = Directory.EnumerateFiles(optionsDir, "CBO_Storage(*).txt")
.FirstOrDefault(f => Path.GetFileName(f).Equals(expected, StringComparison.OrdinalIgnoreCase));
if (file != null)
return file;
}
return null;
}
private static string? GetCboValue(string username, string key)
{
try
{
string? file = FindCboFile(username);
if (file == null)
return null;
foreach (string line in File.ReadAllLines(file))
{
if (line.StartsWith(key, StringComparison.OrdinalIgnoreCase))
{
string value = line[key.Length..].Trim();
return value.Length > 0 ? value : null;
}
}
}
catch
{
}
return null;
}
}
