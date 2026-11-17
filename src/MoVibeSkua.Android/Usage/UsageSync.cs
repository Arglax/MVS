using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Android.Content;
namespace MoVibeSkua.Android.Usage;
internal static class UsageSync
{
public const int Schema = 1;
public const string AppId = "movibeskua";
public const string Collection = "movibeskua_installs";
private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
private static Config? _config;
private static bool _configRead;
private sealed record Config(string ApiKey, string ProjectId);
private sealed class Auth
{
public string Uid { get; set; } = string.Empty;
public string RefreshToken { get; set; } = string.Empty;
public string IdToken { get; set; } = string.Empty;
public long ExpiresAt { get; set; }
public bool Created { get; set; }
}
private static Context Context => global::Android.App.Application.Context;
public static string Folder => Path.Combine(Context.FilesDir!.AbsolutePath, "usage");
private static string AuthPath => Path.Combine(Folder, "auth.json");
private static string LockPath => Path.Combine(Folder, "auth.lock");
public static bool Configured => ReadConfig() is not null;
public static string? InstallId => ReadAuth()?.Uid is { Length: > 0 } uid ? uid : null;
private static Config? ReadConfig()
{
if (_configRead)
return _config;
_configRead = true;
try
{
using Stream stream = Context.Assets!.Open("firebase.json");
using JsonDocument json = JsonDocument.Parse(stream);
string? key = json.RootElement.GetProperty("apiKey").GetString();
string? project = json.RootElement.GetProperty("projectId").GetString();
if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(project))
_config = new Config(key, project);
}
catch
{
_config = null;
}
return _config;
}
public readonly record struct Result(bool V1, bool V2, bool Throttled = false);
public enum Outcome { Ok, Denied, Throttled, Failed }
private static bool _singleDenied;
public static async Task<Result> UploadAsync(IReadOnlyDictionary<string, long> counts, IReadOnlyDictionary<string, long> v2Counts,
IReadOnlyDictionary<string, long> extremes)
{
if (ReadConfig() is not { } config || counts.Count + v2Counts.Count + extremes.Count == 0)
return default;
if (Backoff.Active)
return new Result(false, false, true);
Directory.CreateDirectory(Folder);
using FileStream? held = await LockAsync();
if (held is null)
return default;
Auth? auth = await SignInAsync(config);
if (auth is null)
return default;
string document = $"projects/{config.ProjectId}/databases/(default)/documents/{Collection}/{auth.Uid}";
List<string> days = ReadPerfDays();
HashSet<string> known = days.ToHashSet(StringComparer.Ordinal);
foreach (string id in v2Counts.Keys.Concat(extremes.Keys))
{
if (UsageIds.PerfDay(id) is { } day && known.Add(day))
days.Add(day);
}
days.Sort(StringComparer.Ordinal);
List<string> dropped = days.Count > MaxPerfDays ? days.GetRange(0, days.Count - MaxPerfDays) : new List<string>();
if (auth.Created && !_singleDenied)
{
string body = Merge(CommitBody(document, counts, false), CommitBodyV2(document, v2Counts, extremes, dropped));
Outcome single = await CommitAsync(config, auth, body, "single");
if (single == Outcome.Ok)
{
Trace.WriteLine($"[Usage] uploaded {counts.Count} counts, {v2Counts.Count} v2 counts, {extremes.Count} lows/highs (1 write)");
days.RemoveRange(0, dropped.Count);
WritePerfDays(days);
return new Result(true, true);
}
if (single == Outcome.Throttled)
return new Result(false, false, true);
if (single != Outcome.Denied)
return default;
_singleDenied = true;
Trace.WriteLine("[Usage] single write refused by the rules: sending the two parts apart");
}
bool v1 = false;
if (counts.Count > 0 || !auth.Created)
{
Outcome first = await CommitAsync(config, auth, CommitBody(document, counts, !auth.Created), "counts");
if (first == Outcome.Throttled)
return new Result(false, false, true);
v1 = first == Outcome.Ok;
if (v1)
Trace.WriteLine($"[Usage] uploaded {counts.Count} counts");
if (v1 && !auth.Created)
{
auth.Created = true;
WriteAuth(auth);
}
}
bool v2 = false;
if (auth.Created && auth.ExpiresAt > 0)
{
Outcome second = await CommitAsync(config, auth, CommitBodyV2(document, v2Counts, extremes, dropped), "v2");
if (second == Outcome.Throttled)
return new Result(v1, false, true);
v2 = second == Outcome.Ok;
if (v2)
{
Trace.WriteLine($"[Usage] uploaded {v2Counts.Count} v2 counts, {extremes.Count} lows/highs");
days.RemoveRange(0, dropped.Count);
WritePerfDays(days);
}
}
return new Result(v1, v2);
}
private static string Merge(string v1Body, string v2Body)
{
JsonObject a = (JsonObject)JsonNode.Parse(v1Body)!["writes"]![0]!;
JsonObject b = (JsonObject)JsonNode.Parse(v2Body)!["writes"]![0]!;
JsonObject fields = (JsonObject)a["update"]!["fields"]!.DeepClone();
foreach ((string key, JsonNode? value) in (JsonObject)b["update"]!["fields"]!)
fields[key] = value?.DeepClone();
JsonArray mask = (JsonArray)a["updateMask"]!["fieldPaths"]!.DeepClone();
foreach (JsonNode? path in (JsonArray)b["updateMask"]!["fieldPaths"]!)
mask.Add(path?.DeepClone());
JsonArray transforms = (JsonArray)a["updateTransforms"]!.DeepClone();
if (b["updateTransforms"] is JsonArray more)
{
foreach (JsonNode? t in more)
transforms.Add(t?.DeepClone());
}
JsonObject write = new()
{
["update"] = new JsonObject { ["name"] = a["update"]!["name"]!.DeepClone(), ["fields"] = fields },
["updateMask"] = new JsonObject { ["fieldPaths"] = mask },
["updateTransforms"] = transforms
};
return new JsonObject { ["writes"] = new JsonArray { write } }.ToJsonString();
}
public static Outcome Classify(HttpStatusCode status, string body)
{
int code = (int)status;
if (code is >= 200 and < 300)
return Outcome.Ok;
if (code == 429 || code >= 500 || body.Contains("RESOURCE_EXHAUSTED", StringComparison.Ordinal))
return Outcome.Throttled;
if (code == 403 || body.Contains("PERMISSION_DENIED", StringComparison.Ordinal))
return Outcome.Denied;
return Outcome.Failed;
}
public static class Backoff
{
private static string PathOf => Path.Combine(Folder, "backoff.txt");
public static bool Active => Read().Until > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
public static void Hit()
{
int level = Math.Min(Read().Level + 1, 3);
DateTimeOffset now = DateTimeOffset.UtcNow;
DateTimeOffset until = level switch
{
1 => now.AddHours(1),
2 => now.AddHours(3),
_ => new DateTimeOffset(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero)
};
Write(until.ToUnixTimeMilliseconds(), level);
Trace.WriteLine($"[Usage] server asked to slow down: next try after {until:u} (step {level})");
}
public static void Clear()
{
if (Read().Level > 0)
Write(0, 0);
}
private static (long Until, int Level) Read()
{
try
{
if (!File.Exists(PathOf))
return (0, 0);
string[] parts = File.ReadAllText(PathOf).Split(' ');
return (long.Parse(parts[0]), int.Parse(parts[1]));
}
catch
{
return (0, 0);
}
}
private static void Write(long until, int level)
{
try
{
Directory.CreateDirectory(Folder);
File.WriteAllText(PathOf, until + " " + level);
}
catch
{
}
}
}
public const int MaxPerfDays = 400;
private static string PerfDaysPath => Path.Combine(Folder, "perfdays.json");
private static List<string> ReadPerfDays()
{
try
{
return File.Exists(PerfDaysPath) ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(PerfDaysPath)) ?? new() : new();
}
catch
{
return new();
}
}
private static void WritePerfDays(List<string> days)
{
try
{
string temp = PerfDaysPath + ".tmp";
File.WriteAllText(temp, JsonSerializer.Serialize(days));
File.Move(temp, PerfDaysPath, true);
}
catch
{
}
}
private static async Task<Outcome> CommitAsync(Config config, Auth auth, string body, string what)
{
using HttpRequestMessage request = new(HttpMethod.Post, $"https://firestore.googleapis.com/v1/projects/{config.ProjectId}/databases/(default)/documents:commit")
{
Content = new StringContent(body, Encoding.UTF8, "application/json")
};
request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.IdToken);
using HttpResponseMessage response = await Http.SendAsync(request);
if (response.StatusCode == HttpStatusCode.Unauthorized)
{
auth.ExpiresAt = 0;
WriteAuth(auth);
return Outcome.Failed;
}
string error = response.IsSuccessStatusCode ? string.Empty : await response.Content.ReadAsStringAsync();
Outcome outcome = Classify(response.StatusCode, error);
if (outcome == Outcome.Ok)
{
Backoff.Clear();
return outcome;
}
Trace.WriteLine($"[Usage] {what} upload refused: {(int)response.StatusCode} {(error.Length > 300 ? error[..300] : error)}");
if (outcome == Outcome.Throttled)
Backoff.Hit();
return outcome;
}
internal static string CommitBodyV2(string document, IReadOnlyDictionary<string, long> counts, IReadOnlyDictionary<string, long> extremes,
IReadOnlyList<string> droppedDays)
{
JsonObject fields = new() { ["device"] = Value(DeviceInfo.Describe()) };
JsonArray mask = new() { "device" };
HashSet<string> dropped = droppedDays.ToHashSet(StringComparer.Ordinal);
foreach (string day in droppedDays)
mask.Add("perfDays." + day);
JsonArray transforms = new();
foreach ((string id, long count) in counts.OrderBy(c => c.Key, StringComparer.Ordinal))
{
if (count <= 0 || (UsageIds.PerfDay(id) is { } day && dropped.Contains(day)))
continue;
JsonObject amount = UsageIds.IsScaled(id)
? new JsonObject { ["doubleValue"] = Math.Round(count / (double)UsageIds.Scale, 2) }
: new JsonObject { ["integerValue"] = count.ToString() };
transforms.Add(new JsonObject { ["fieldPath"] = UsageIds.PathV2(id), ["increment"] = amount });
}
foreach ((string path, long value) in extremes.OrderBy(c => c.Key, StringComparer.Ordinal))
{
if (UsageIds.PerfDay(path) is { } day && dropped.Contains(day))
continue;
transforms.Add(new JsonObject
{
["fieldPath"] = path,
[Extremes.IsMinimum(path) ? "minimum" : "maximum"] = new JsonObject { ["integerValue"] = value.ToString() }
});
}
JsonObject write = new()
{
["update"] = new JsonObject { ["name"] = document, ["fields"] = fields },
["updateMask"] = new JsonObject { ["fieldPaths"] = mask },
["currentDocument"] = new JsonObject { ["exists"] = true }
};
if (transforms.Count > 0)
write["updateTransforms"] = transforms;
return new JsonObject { ["writes"] = new JsonArray { write } }.ToJsonString();
}
public static JsonObject Value(JsonNode? node)
{
switch (node)
{
case JsonObject map:
JsonObject mapped = new();
foreach ((string key, JsonNode? child) in map)
mapped[key] = Value(child);
return new JsonObject { ["mapValue"] = new JsonObject { ["fields"] = mapped } };
case JsonValue value when value.TryGetValue(out bool flag):
return new JsonObject { ["booleanValue"] = flag };
case JsonValue value when value.TryGetValue(out string? text):
return new JsonObject { ["stringValue"] = text };
case JsonValue value when value.TryGetValue(out int small):
return new JsonObject { ["integerValue"] = small.ToString() };
case JsonValue value when value.TryGetValue(out long whole):
return new JsonObject { ["integerValue"] = whole.ToString() };
case JsonValue value when value.TryGetValue(out double number):
return new JsonObject { ["doubleValue"] = number };
default:
return new JsonObject { ["nullValue"] = null };
}
}
public static async Task<(string ProjectId, string Uid, string IdToken)?> SignedInAsync()
{
if (ReadConfig() is not { } config)
return null;
Directory.CreateDirectory(Folder);
using FileStream? held = await LockAsync();
if (held is null)
return null;
Auth? auth = await SignInAsync(config);
return auth is null ? null : (config.ProjectId, auth.Uid, auth.IdToken);
}
public static void TokenRefused()
{
if (ReadAuth() is { } auth)
{
auth.ExpiresAt = 0;
WriteAuth(auth);
}
}
private static string CommitBody(string document, IReadOnlyDictionary<string, long> counts, bool firstWrite)
{
(string version, long code) = AppVersion();
JsonObject fields = new()
{
["schema"] = new JsonObject { ["integerValue"] = Schema.ToString() },
["app"] = new JsonObject { ["stringValue"] = AppId },
["appVersion"] = new JsonObject { ["stringValue"] = version.Length > 24 ? version[..24] : version },
["appVersionCode"] = new JsonObject { ["integerValue"] = code.ToString() },
["country"] = new JsonObject { ["stringValue"] = DeviceInfo.Country() },
};
JsonArray transforms = new() { new JsonObject { ["fieldPath"] = "lastSeen", ["setToServerValue"] = "REQUEST_TIME" } };
if (firstWrite)
transforms.Add(new JsonObject { ["fieldPath"] = "firstSeen", ["setToServerValue"] = "REQUEST_TIME" });
foreach ((string id, long count) in counts.OrderBy(c => c.Key, StringComparer.Ordinal))
{
if (count <= 0)
continue;
string path = UsageIds.PathV1(id);
transforms.Add(new JsonObject
{
["fieldPath"] = path,
["increment"] = new JsonObject { ["integerValue"] = count.ToString() }
});
}
JsonObject body = new()
{
["writes"] = new JsonArray
{
new JsonObject
{
["update"] = new JsonObject { ["name"] = document, ["fields"] = fields },
["updateMask"] = new JsonObject { ["fieldPaths"] = new JsonArray("schema", "app", "appVersion", "appVersionCode", "country") },
["updateTransforms"] = transforms
}
}
};
return body.ToJsonString();
}
private static async Task<Auth?> SignInAsync(Config config)
{
Auth? auth = ReadAuth();
long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
if (auth is not null && auth.ExpiresAt - 60_000 > now)
return auth;
if (auth is not null)
{
using HttpResponseMessage refreshed = await Http.PostAsync(
$"https://securetoken.googleapis.com/v1/token?key={config.ApiKey}",
new FormUrlEncodedContent(new Dictionary<string, string>
{
["grant_type"] = "refresh_token",
["refresh_token"] = auth.RefreshToken
}));
if (refreshed.IsSuccessStatusCode)
{
using JsonDocument json = JsonDocument.Parse(await refreshed.Content.ReadAsStringAsync());
JsonElement root = json.RootElement;
auth.IdToken = root.GetProperty("id_token").GetString()!;
auth.RefreshToken = root.GetProperty("refresh_token").GetString()!;
auth.ExpiresAt = now + long.Parse(root.GetProperty("expires_in").GetString()!) * 1000;
WriteAuth(auth);
return auth;
}
if (refreshed.StatusCode != HttpStatusCode.BadRequest)
return null;
}
using HttpResponseMessage signedUp = await Http.PostAsync(
$"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={config.ApiKey}",
new StringContent("{\"returnSecureToken\":true}", Encoding.UTF8, "application/json"));
if (!signedUp.IsSuccessStatusCode)
{
string error = await signedUp.Content.ReadAsStringAsync();
Trace.WriteLine($"[Usage] sign-up refused: {(int)signedUp.StatusCode} {(error.Length > 300 ? error[..300] : error)}");
return null;
}
using (JsonDocument json = JsonDocument.Parse(await signedUp.Content.ReadAsStringAsync()))
{
JsonElement root = json.RootElement;
auth = new Auth
{
Uid = root.GetProperty("localId").GetString()!,
IdToken = root.GetProperty("idToken").GetString()!,
RefreshToken = root.GetProperty("refreshToken").GetString()!,
ExpiresAt = now + long.Parse(root.GetProperty("expiresIn").GetString()!) * 1000
};
}
WriteAuth(auth);
return auth;
}
private static Auth? ReadAuth()
{
try
{
return File.Exists(AuthPath) ? JsonSerializer.Deserialize<Auth>(File.ReadAllText(AuthPath)) : null;
}
catch
{
return null;
}
}
private static void WriteAuth(Auth auth)
{
string temp = AuthPath + ".tmp";
File.WriteAllText(temp, JsonSerializer.Serialize(auth));
File.Move(temp, AuthPath, true);
}
private static async Task<FileStream?> LockAsync()
{
for (int attempt = 0; attempt < 50; attempt++)
{
try
{
return new FileStream(LockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
}
catch (IOException)
{
await Task.Delay(200);
}
}
return null;
}
private static (string Version, long Code) AppVersion()
{
try
{
var info = Context.PackageManager!.GetPackageInfo(Context.PackageName!, 0)!;
long code = OperatingSystem.IsAndroidVersionAtLeast(28) ? info.LongVersionCode : info.VersionCode;
return (info.VersionName ?? "unknown", code);
}
catch
{
return ("unknown", 0);
}
}
}
