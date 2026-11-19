using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Skua.Core.Interfaces;
using Skua.Core.Models;
using Skua.Core.Models.GitHub;
namespace Skua.Core.Scripts;
public static class ScriptTrust
{
public enum Standing { Official, Unofficial, Unverifiable }
public sealed record Finding(string File, Standing Standing, string Hash, IReadOnlyList<string> Reaches);
private static readonly object _lock = new();
private static ConcurrentDictionary<string, string>? _ledger;
private static HashSet<string>? _approved;
private static int _flushQueued;
private static string LedgerFile => Path.Combine(ClientFileSources.SkuaDIR, "official-scripts.json");
private static string ApprovedFile => Path.Combine(ClientFileSources.SkuaDIR, "approved-scripts.json");
private static readonly (string Label, Regex Pattern, bool Grave)[] _signs =
{
("your saved logins or password", new Regex(@"\.Password\b|strPassword|strToken|\bsToken\b|loginInfo|GetAllAccounts|ManagedAccounts|SavedAccounts|IScriptAccounts|\.Accounts\b|KeyStore|SharedPreferences", RegexOptions.Compiled | RegexOptions.IgnoreCase), true),
("hidden parts of the app (reflection or loading other code)", new Regex(@"System\.Reflection|\bGetMethod\(|\bGetField\(|\bGetProperty\(|\bInvokeMember\(|\bAssembly\.|\bActivator\.|AssemblyLoadContext|DllImport|^\s*//cs_ref", RegexOptions.Compiled | RegexOptions.Multiline), true),
("the internet (it can send data out)", new Regex(@"HttpClient|WebClient|WebRequest|TcpClient|UdpClient|\bSocket\b|webhook|System\.Net\b|PostAsync|UploadString", RegexOptions.Compiled | RegexOptions.IgnoreCase), false),
("files on this device", new Regex(@"\bFile\.(Write|Append|Delete|Copy|Move|Create|Open|Read)|\bDirectory\.|FileStream|StreamWriter|plugins|Cached-Scripts", RegexOptions.Compiled), false),
};
public static void RecordOfficial(string repoPath, byte[] contents)
{
try
{
Ledger()[Key(repoPath)] = Hash(contents);
if (Interlocked.Exchange(ref _flushQueued, 1) == 0)
{
_ = Task.Run(async () =>
{
await Task.Delay(3000);
Interlocked.Exchange(ref _flushQueued, 0);
Save(LedgerFile, Ledger());
});
}
}
catch (Exception ex)
{
Trace.WriteLine($"[Trust] could not record {repoPath}: {ex.Message}");
}
}
public static List<Finding> Check(IEnumerable<string> files, IReadOnlyCollection<ScriptInfo>? repository)
{
List<Finding> findings = new();
Dictionary<string, string> published = new(StringComparer.OrdinalIgnoreCase);
foreach (ScriptInfo info in repository ?? Array.Empty<ScriptInfo>())
{
if (!string.IsNullOrEmpty(info.Sha256))
published[Key(info.FilePath)] = info.Sha256!;
}
string root = Path.GetFullPath(ClientFileSources.SkuaScriptsDIR) + Path.DirectorySeparatorChar;
foreach (string file in files.Distinct(StringComparer.OrdinalIgnoreCase))
{
try
{
if (!file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || !File.Exists(file))
continue;
byte[] contents = File.ReadAllBytes(file);
string hash = Hash(contents);
if (Approved().Contains(hash))
continue;
string full = Path.GetFullPath(file);
Standing standing = Standing.Unofficial;
if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
{
string key = Key(full[root.Length..]);
bool inRepository = published.TryGetValue(key, out string? publishedHash);
bool recorded = Ledger().TryGetValue(key, out string? recordedHash);
if ((inRepository && Same(publishedHash, hash)) || (recorded && Same(recordedHash, hash)))
continue;
if (published.Count == 0 && !recorded)
standing = Standing.Unverifiable;
}
string text = Encoding.UTF8.GetString(contents);
List<string> reaches = new();
bool grave = false;
foreach ((string label, Regex pattern, bool isGrave) in _signs)
{
if (!pattern.IsMatch(text))
continue;
reaches.Add(label);
grave |= isGrave;
}
if (standing == Standing.Unverifiable && !grave)
continue;
findings.Add(new Finding(full, standing, hash, reaches));
}
catch (Exception ex)
{
Trace.WriteLine($"[Trust] could not check {Path.GetFileName(file)}: {ex.Message}");
}
}
return findings;
}
public static string Describe(IReadOnlyList<Finding> findings)
{
StringBuilder text = new();
text.AppendLine(findings.Count == 1
? "This script is not an official one from the script repository:"
: "These files are not official ones from the script repository:");
text.AppendLine();
foreach (Finding finding in findings.Take(6))
text.AppendLine("  • " + Path.GetFileName(finding.File));
if (findings.Count > 6)
text.AppendLine($"  • and {findings.Count - 6} more");
List<string> reaches = findings.SelectMany(f => f.Reaches).Distinct().ToList();
text.AppendLine();
if (reaches.Count > 0)
{
text.AppendLine("It uses:");
foreach (string reach in reaches)
text.AppendLine("  • " + reach);
text.AppendLine();
}
text.AppendLine("A script can do anything the app can, including reading your saved AQW logins and sending them to someone else. "
+ "Official scripts are checked against the repository; this one was changed or came from somewhere else.");
text.AppendLine();
text.Append("Only run it if you wrote it or trust the person who gave it to you. Run it?");
return text.ToString();
}
public static void Approve(IEnumerable<Finding> findings)
{
lock (_lock)
{
HashSet<string> approved = Approved();
foreach (Finding finding in findings)
approved.Add(finding.Hash);
Save(ApprovedFile, approved);
}
}
private static ConcurrentDictionary<string, string> Ledger()
{
if (_ledger is not null)
return _ledger;
lock (_lock)
{
if (_ledger is not null)
return _ledger;
Dictionary<string, string>? saved = Load<Dictionary<string, string>>(LedgerFile);
ConcurrentDictionary<string, string> ledger = new(saved ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
if (saved is null)
SeedFromDisk(ledger);
return _ledger = ledger;
}
}
private static void SeedFromDisk(ConcurrentDictionary<string, string> ledger)
{
try
{
string root = Path.GetFullPath(ClientFileSources.SkuaScriptsDIR) + Path.DirectorySeparatorChar;
if (!Directory.Exists(root))
return;
foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
{
try { ledger[Key(file[root.Length..])] = Hash(File.ReadAllBytes(file)); } catch { }
}
Trace.WriteLine($"[Trust] first run: {ledger.Count} scripts already on the device taken as they are");
Save(LedgerFile, ledger);
}
catch (Exception ex)
{
Trace.WriteLine($"[Trust] first-run record failed: {ex.Message}");
}
}
private static HashSet<string> Approved()
{
if (_approved is not null)
return _approved;
lock (_lock)
{
return _approved ??= new HashSet<string>(Load<List<string>>(ApprovedFile) ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
}
}
private static T? Load<T>(string file) where T : class
{
try
{
return File.Exists(file) ? JsonConvert.DeserializeObject<T>(File.ReadAllText(file)) : null;
}
catch
{
return null;
}
}
private static void Save(string file, object value)
{
try
{
string temporary = file + ".tmp";
File.WriteAllText(temporary, JsonConvert.SerializeObject(value));
File.Move(temporary, file, true);
}
catch (Exception ex)
{
Trace.WriteLine($"[Trust] could not save {Path.GetFileName(file)}: {ex.Message}");
}
}
private static string Key(string repoPath) => repoPath.Replace('\\', '/').TrimStart('/');
private static string Hash(byte[] contents) => Convert.ToHexString(SHA256.HashData(contents)).ToLowerInvariant();
private static bool Same(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
