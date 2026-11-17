using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace MoVibeSkua.Android.Usage;
internal static class RemoteSettings
{
public const string Url = "https://raw.githubusercontent.com/Arglax/MVS/main/telemetry.json";
private static readonly TimeSpan FetchEvery = TimeSpan.FromHours(24);
private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };
private static int _fetching;
public sealed record Values(bool UsageEnabled, int MinMinutes, double Sample, bool ReportsEnabled, int PerDay, bool FullLog);
public static readonly Values Defaults = new(true, 60, 1.0, true, 3, true);
private static Values? _current;
public static Values Current => _current ??= Read(CachePath) ?? Defaults;
private static string CachePath => Path.Combine(UsageSync.Folder, "remote.json");
private static string MetaPath => Path.Combine(UsageSync.Folder, "remote.meta");
public static void RefreshIfDue()
{
if (Interlocked.Exchange(ref _fetching, 1) == 1)
return;
_ = Task.Run(async () =>
{
try
{
(long fetchedAt, string etag) = ReadMeta();
if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - fetchedAt < FetchEvery.TotalMilliseconds)
return;
using HttpRequestMessage request = new(HttpMethod.Get, Url);
if (etag.Length > 0 && File.Exists(CachePath))
request.Headers.TryAddWithoutValidation("If-None-Match", etag);
using HttpResponseMessage response = await Http.SendAsync(request);
string newTag = response.Headers.ETag?.Tag ?? etag;
if (response.StatusCode == HttpStatusCode.OK)
{
string text = await response.Content.ReadAsStringAsync();
if (Parse(text) is { } parsed)
{
Directory.CreateDirectory(UsageSync.Folder);
string temp = CachePath + ".tmp";
File.WriteAllText(temp, text);
File.Move(temp, CachePath, true);
_current = parsed;
}
}
WriteMeta(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), newTag);
System.Diagnostics.Trace.WriteLine($"[Usage] remote settings: {(int)response.StatusCode}, {Current}");
}
catch (Exception ex)
{
System.Diagnostics.Trace.WriteLine($"[Usage] remote settings not read: {ex.Message}");
}
finally
{
Interlocked.Exchange(ref _fetching, 0);
}
});
}
public static bool InSample(string? installId)
{
double sample = Current.Sample;
if (sample >= 1 || string.IsNullOrEmpty(installId))
return true;
if (sample <= 0)
return false;
byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(installId));
double at = BitConverter.ToUInt32(hash, 0) / (double)uint.MaxValue;
return at < sample;
}
private static Values? Read(string path)
{
try
{
return File.Exists(path) ? Parse(File.ReadAllText(path)) : null;
}
catch
{
return null;
}
}
internal static Values? Parse(string text)
{
try
{
using JsonDocument json = JsonDocument.Parse(text);
JsonElement root = json.RootElement;
if (!root.TryGetProperty("v", out JsonElement v) || v.ValueKind != JsonValueKind.Number || v.GetInt32() != 1)
return null;
JsonElement usage = root.TryGetProperty("usage", out JsonElement u) && u.ValueKind == JsonValueKind.Object ? u : default;
JsonElement reports = root.TryGetProperty("reports", out JsonElement r) && r.ValueKind == JsonValueKind.Object ? r : default;
return new Values(
Bool(usage, "enabled", Defaults.UsageEnabled),
Math.Clamp(Int(usage, "minMinutes", Defaults.MinMinutes), 10, 1440),
Math.Clamp(Num(usage, "sample", Defaults.Sample), 0, 1),
Bool(reports, "enabled", Defaults.ReportsEnabled),
Math.Clamp(Int(reports, "perDay", Defaults.PerDay), 0, 10),
Bool(reports, "fullLog", Defaults.FullLog));
}
catch
{
return null;
}
}
private static bool Bool(JsonElement e, string name, bool fallback) =>
e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out JsonElement x) && x.ValueKind is JsonValueKind.True or JsonValueKind.False
? x.GetBoolean() : fallback;
private static int Int(JsonElement e, string name, int fallback) =>
e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out JsonElement x) && x.ValueKind == JsonValueKind.Number
? (int)Math.Round(x.GetDouble()) : fallback;
private static double Num(JsonElement e, string name, double fallback) =>
e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out JsonElement x) && x.ValueKind == JsonValueKind.Number
? x.GetDouble() : fallback;
private static (long FetchedAt, string Etag) ReadMeta()
{
try
{
if (!File.Exists(MetaPath))
return (0, string.Empty);
string[] parts = File.ReadAllText(MetaPath).Split('\n');
return (long.TryParse(parts[0], out long at) ? at : 0, parts.Length > 1 ? parts[1].Trim() : string.Empty);
}
catch
{
return (0, string.Empty);
}
}
private static void WriteMeta(long fetchedAt, string etag)
{
try
{
Directory.CreateDirectory(UsageSync.Folder);
File.WriteAllText(MetaPath, fetchedAt + "\n" + etag);
}
catch
{
}
}
}
