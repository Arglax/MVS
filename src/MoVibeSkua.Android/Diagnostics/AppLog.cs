using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
namespace MoVibeSkua.Android.Diagnostics;
public sealed class AppLog : TraceListener
{
private const int KeepDays = 5;
private const long MaxFileBytes = 4 * 1024 * 1024;
private const int RecentLines = 3000;
private static readonly object Gate = new();
private static readonly Queue<string> Recent = new();
private static StreamWriter? _writer;
private static string _day = string.Empty;
private static int _part;
public static string Folder => Path.Combine(global::Android.App.Application.Context.FilesDir!.AbsolutePath, "logs");
public static void Start()
{
if (Trace.Listeners.OfType<AppLog>().Any())
return;
try
{
Directory.CreateDirectory(Folder);
foreach (string old in Directory.GetFiles(Folder, "mvs-*.log"))
{
if (File.GetLastWriteTimeUtc(old) < DateTime.UtcNow.AddDays(-KeepDays))
File.Delete(old);
}
}
catch
{
}
Trace.Listeners.Add(new AppLog());
Trace.WriteLine($"[App] MVS {AppVersion()} on {(global::Android.OS.Build.Manufacturer)} {(global::Android.OS.Build.Model)}, Android {(global::Android.OS.Build.VERSION.Release)} (client {Platform.Army.Slot})");
}
public static void Add(string line) => Append(line);
public override void Write(string? message)
{
if (!string.IsNullOrEmpty(message))
Append(message);
}
public override void WriteLine(string? message) => Write(message);
private static void Append(string message)
{
string line = $"{DateTime.Now:HH:mm:ss.fff} {message}";
Watch(message);
lock (Gate)
{
Recent.Enqueue(line);
while (Recent.Count > RecentLines)
Recent.Dequeue();
try
{
Writer()?.WriteLine(line);
}
catch
{
_writer = null;
}
}
}
private static StreamWriter? Writer()
{
string day = DateTime.Now.ToString("yyyyMMdd");
if (_writer is not null && day == _day && _writer.BaseStream.Length < MaxFileBytes)
return _writer;
_writer?.Dispose();
_part = day == _day ? _part + 1 : 0;
_day = day;
string client = Platform.Army.IsFirst ? string.Empty : $"-c{Platform.Army.Slot}";
string part = _part > 0 ? $"-{_part}" : string.Empty;
_writer = new StreamWriter(new FileStream(Path.Combine(Folder, $"mvs-{day}{client}{part}.log"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8)
{
AutoFlush = true
};
return _writer;
}
private static readonly Dictionary<string, string> Latest = new();
private static readonly (string Prefix, string Key)[] Measured =
{
("[Ruffle] memory:", "memory"),
("[Ruffle] governor:", "frame rate"),
("[Thermal]", "heat"),
("[Flash] ", "game calls"),
("[RuffleHost] proxy", "last download failure"),
("[RuffleHost] server list fetch failed", "last download failure"),
("[Ruffle] page error:", "last page error"),
("[WebView] ERROR: panicked", "last engine panic"),
("[Script] [Relogin]", "last re-login line"),
("[Page]", "last page"),
};
private static void Watch(string message)
{
foreach ((string prefix, string key) in Measured)
{
if (!message.StartsWith(prefix, StringComparison.Ordinal))
continue;
if (key == "game calls" && !message.Contains("calls/s", StringComparison.Ordinal))
continue;
lock (Latest)
Latest[key] = $"{DateTime.Now:HH:mm:ss} {(message.Length > 220 ? message[..220] : message)}";
break;
}
}
public static string MeasurementsText()
{
lock (Latest)
return string.Join("\n", Latest.OrderBy(p => p.Key).Select(p => $"{p.Key}: {p.Value}"));
}
private static readonly Regex Noise = Rx(@"\[Flash\] \w+ Args\[\d*\]\s*$|isLoggedIn failed: FlashCallException|%xt%chatm|%xt%whisper|\bchatm\b");
public static string ReportLog(int minutes = 5, int maxChars = 22000)
{
string[] copy;
lock (Gate)
copy = Recent.ToArray();
string since = DateTime.Now.AddMinutes(-minutes).ToString("HH:mm:ss.fff");
bool sameDay = DateTime.Now.AddMinutes(-minutes).Date == DateTime.Now.Date;
List<string> lines = new();
string? last = null;
int repeats = 0;
for (int i = 0; i < copy.Length; i++)
{
string line = copy[i];
bool recent = copy.Length - i <= 80 || !sameDay || string.CompareOrdinal(line, since) >= 0;
if (!recent)
continue;
string body = line.Length > 13 ? line[13..] : line;
if (Noise.IsMatch(body))
continue;
if (body == last)
{
repeats++;
continue;
}
if (repeats > 0)
lines.Add($"             (same line {repeats} more time{(repeats == 1 ? "" : "s")})");
repeats = 0;
last = body;
lines.Add(line);
}
if (repeats > 0)
lines.Add($"             (same line {repeats} more time{(repeats == 1 ? "" : "s")})");
string text = Redact(string.Join("\n", lines));
return text.Length > maxChars ? "(older lines cut)\n" + text[^maxChars..] : text;
}
public static string PreviousRunTail(DateTime diedAtUtc, int maxChars = 20000)
{
try
{
string? file = Directory.GetFiles(Folder, "mvs-*.log")
.Where(f => File.GetLastWriteTimeUtc(f) <= diedAtUtc.AddMinutes(2))
.OrderByDescending(File.GetLastWriteTimeUtc)
.FirstOrDefault();
if (file is null)
return string.Empty;
using FileStream stream = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
long start = Math.Max(0, stream.Length - maxChars * 2);
stream.Seek(start, SeekOrigin.Begin);
using StreamReader reader = new(stream, Encoding.UTF8);
string text = reader.ReadToEnd();
string[] lines = text.Split('\n').Skip(start > 0 ? 1 : 0).Where(l => !Noise.IsMatch(l)).ToArray();
string tail = Redact(string.Join("\n", lines));
return tail.Length > maxChars ? tail[^maxChars..] : tail;
}
catch
{
return string.Empty;
}
}
public static string RecentText(int lines = 150) => Redact(RecentRaw(lines));
public static string RecentRaw(int lines = 150)
{
string[] copy;
lock (Gate)
copy = Recent.Skip(Math.Max(0, Recent.Count - lines)).ToArray();
return string.Join("\n", copy);
}
private static Regex Rx(string pattern)
{
try
{
return new Regex(pattern, RegexOptions.NonBacktracking);
}
catch (Exception)
{
return new Regex(pattern);
}
}
private static readonly Regex Secrets = Rx(
@"(?i)(token|password|pass|pwd|key|auth|session|ccid)(["":=\s]+)[^\s,""&;}]+");
private static readonly Regex NameFields = Rx(
@"(?i)\b(str\w*username|str\w*password|strToken|sToken|username|user|unm|login|charname|character)(\s*[""']?\s*[:=]\s*[""']?)[^\s,""'&;}]+");
private static readonly Regex Emails = Rx(@"[\w.+-]+@[\w-]+\.[\w.-]+");
private static readonly Regex UserPaths = Rx(
@"(?i)(/data/user(_de)?/\d+|/data/data|/storage/emulated/\d+|/sdcard|[A-Z]:\\Users\\[^\\\s""']+|/home/[^/\s""']+|/Users/[^/\s""']+)");
private static readonly Regex Webhooks = Rx(@"/api/webhooks/\d+/[\w-]+");
public const string Hidden = "<redacted>";
public static string Redact(string text)
{
if (string.IsNullOrEmpty(text))
return text;
foreach (string secret in KnownSecrets())
{
if (secret.Length >= 3)
text = text.Replace(secret, Hidden, StringComparison.OrdinalIgnoreCase);
}
text = Webhooks.Replace(text, "/api/webhooks/" + Hidden);
text = Emails.Replace(text, Hidden);
text = UserPaths.Replace(text, Hidden);
text = NameFields.Replace(text, m => m.Groups[1].Value + m.Groups[2].Value + Hidden);
return Secrets.Replace(text, m => m.Groups[1].Value + m.Groups[2].Value + Hidden);
}
private static string? _lastName;
public static void RememberName(string? name)
{
if (!string.IsNullOrWhiteSpace(name))
_lastName = name.Trim();
}
private static string? _loginName, _loginPassword;
public static void RememberLogin(string? name, string? password)
{
_loginName = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
_loginPassword = string.IsNullOrEmpty(password) ? null : password;
}
private static IEnumerable<string> KnownSecrets()
{
if (_lastName is not null)
yield return _lastName;
if (_loginName is not null)
yield return _loginName;
if (_loginPassword is not null)
yield return _loginPassword;
List<Platform.SavedAccount> saved;
try
{
saved = Platform.SavedAccounts.Load();
}
catch
{
yield break;
}
foreach (Platform.SavedAccount account in saved)
{
yield return account.Username;
yield return account.Password;
}
}
public static string AppVersion()
{
try
{
global::Android.Content.Context context = global::Android.App.Application.Context;
var info = context.PackageManager!.GetPackageInfo(context.PackageName!, 0)!;
long code = OperatingSystem.IsAndroidVersionAtLeast(28) ? info.LongVersionCode : info.VersionCode;
return $"{info.VersionName} ({code})";
}
catch
{
return "unknown";
}
}
}
