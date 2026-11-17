using CommunityToolkit.Mvvm.Messaging;
using Skua.Core.Flash;
using Skua.Core.Interfaces;
using Skua.Core.Messaging;
using Skua.Core.Utils;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Xml.Linq;
namespace Skua.Flash.Ruffle;
public class RuffleFlashUtil : IFlashUtil
{
private readonly IMessenger _messenger;
private readonly Lazy<IScriptManager> _lazyManager;
private readonly ConcurrentDictionary<string, long> _lastErrorTrace = new();
private const long ReadCacheMs = 50;
private static readonly HashSet<string> ReadOnlyCalls = new(StringComparer.Ordinal)
{
"isNull", "isTrue", "isLoggedIn", "isKicked", "getGameObject", "getGameObjectS", "getGameObjectKey",
"getArrayObject", "getTargetMonster", "canUseSkill", "availableMonsters", "lnkGetValue", "inventoryBagUsedSlots",
"inventoryMiscSlots", "inventoryMiscUsedSlots", "inventoryHasSpaceFor", "isFavoriteItem"
};
private static readonly HashSet<string> CacheNeutralCalls = new(StringComparer.Ordinal)
{
"killLag", "magnetise", "infiniteRange", "skipCutscenes", "catchPackets"
};
private static readonly HashSet<string> ReadOnlyGameFunctions = new(StringComparer.Ordinal)
{
"world.myAvatar.pMC.artLoaded"
};
private const long PrefetchMs = 200;
private const int MaxPrefetch = 40;
private readonly ConcurrentDictionary<string, (string? Raw, long At, long Ttl)> _readCache = new();
private sealed class ReadCycle
{
public required string Name;
public readonly HashSet<string> Seen = new(StringComparer.Ordinal);
public readonly List<(string Function, string ArgsJson)> Reads = new();
}
private const long StickyMs = 5000;
private static readonly HashSet<string> StickyReads = new(StringComparer.Ordinal)
{
"getGameObjectS|[\"loginInfo.strUsername\"]",
"getGameObjectS|[\"serverIP\"]"
};
private readonly ConcurrentDictionary<string, (string? Raw, long At)> _sticky = new();
private const long PresenceMs = 300;
private static readonly HashSet<string> PresenceReads = new(StringComparer.Ordinal)
{
"isNull|[\"world\"]", "isNull|[\"world.myAvatar\"]", "isLoggedIn|[]", "isKicked|[]"
};
private readonly ConcurrentDictionary<string, (string? Raw, long At)> _presence = new();
private readonly ConcurrentDictionary<string, int> _statByFunction = new(StringComparer.Ordinal);
private readonly AsyncLocal<ReadCycle?> _cycle = new();
private readonly ConcurrentDictionary<string, List<(string Function, string ArgsJson)>> _cycleReads = new();
private long _statBatches;
private long _statCalls, _statHits, _statMs, _statStart = Environment.TickCount64;
public RuffleFlashUtil(IMessenger messenger, Lazy<IScriptManager> manager, RuffleHost host)
{
_messenger = messenger;
_lazyManager = manager;
Host = host;
Host.Control.ExternalCall += OnExternalCall;
}
public RuffleHost Host { get; }
public event FlashCallHandler? FlashCall;
public event Action<string>? PageRequested;
public void InitializeFlash()
{
Host.Start();
if (!Host.Control.Post("{\"t\":\"reload\"}"))
PageRequested?.Invoke(Host.PageUrl);
}
private void OnExternalCall(string name, string argsJson)
{
try
{
FlashCall?.Invoke(name, FlashValues.ExternalArgs(argsJson));
}
catch (Exception ex)
{
System.Diagnostics.Trace.WriteLine($"Flash CallHandler exception: {ex.Message}");
}
}
public string? Call(string function, params object[] args)
{
return Call<string>(function, args);
}
public T? Call<T>(string function, params object[] args)
{
try
{
object? o = Call(function, typeof(T), args);
return o is T value ? value : GetDefaultValue<T>();
}
catch
{
return GetDefaultValue<T>();
}
}
private static T GetDefaultValue<T>()
{
object? defaultValue = DefaultProvider.GetDefault<T>(typeof(T));
return defaultValue is T typed ? typed : default!;
}
public object? Call(string function, Type type, params object[] args)
{
if (_lazyManager.Value.ShouldExit && Thread.CurrentThread.Name == "Script Thread")
_lazyManager.Value.ScriptCts?.Token.ThrowIfCancellationRequested();
try
{
string argsJson = FlashValues.SerializeArgs(args);
string? raw;
long now = Environment.TickCount64;
if (IsReadOnly(function, args))
{
string key = function + "|" + argsJson;
if (PresenceReads.Contains(key))
{
if (_presence.TryGetValue(key, out var seen) && now - seen.At <= PresenceMs)
{
Interlocked.Increment(ref _statHits);
return FlashValues.ConvertResult(FlashValues.ResultToString(seen.Raw), type);
}
string? fresh = TimedCall(function, argsJson);
_presence[key] = (fresh, Environment.TickCount64);
ReportStats();
return FlashValues.ConvertResult(FlashValues.ResultToString(fresh), type);
}
if (StickyReads.Contains(key))
{
if (_sticky.TryGetValue(key, out var kept) && now - kept.At <= StickyMs)
{
Interlocked.Increment(ref _statHits);
return FlashValues.ConvertResult(FlashValues.ResultToString(kept.Raw), type);
}
string? fresh = TimedCall(function, argsJson);
if (!string.IsNullOrEmpty(fresh) && fresh != "null" && fresh != "\"\"")
_sticky[key] = (fresh, Environment.TickCount64);
return FlashValues.ConvertResult(FlashValues.ResultToString(fresh), type);
}
if (_cycle.Value is { } cycle && cycle.Reads.Count < MaxPrefetch && cycle.Seen.Add(key))
cycle.Reads.Add((function, argsJson));
if (_readCache.TryGetValue(key, out var hit) && now - hit.At <= hit.Ttl)
{
Interlocked.Increment(ref _statHits);
raw = hit.Raw;
}
else
{
raw = TimedCall(function, argsJson);
_readCache[key] = (raw, Environment.TickCount64, ReadCacheMs);
if (_readCache.Count > 512)
_readCache.Clear();
}
}
else
{
if (!CacheNeutralCalls.Contains(function))
_readCache.Clear();
raw = TimedCall(function, argsJson);
}
ReportStats();
return FlashValues.ConvertResult(FlashValues.ResultToString(raw), type);
}
catch (Exception e)
{
if (function != "isKicked")
{
_messenger.Send<FlashErrorMessage>(new(e, function, HideLogin(function, args)));
TraceError(function, e);
}
return default;
}
}
private static object[] HideLogin(string function, object[] args)
{
int keep = function == "login" ? 0
: function == "callGameFunction" && args.Length > 0 && args[0] is string path && path == "login" ? 1
: -1;
if (keep < 0 || args.Length <= keep)
return args;
object[] shown = new object[args.Length];
for (int i = 0; i < shown.Length; i++)
shown[i] = i < keep ? args[i] : "<hidden>";
return shown;
}
private static bool IsReadOnly(string function, object[] args) =>
ReadOnlyCalls.Contains(function)
|| (function == "callGameFunction" && args.Length == 1 && args[0] is string path && ReadOnlyGameFunctions.Contains(path));
public void BeginReadCycle(string name)
{
ReadCycle? last = _cycle.Value;
if (last is not null && last.Reads.Count > 0)
_cycleReads[last.Name] = last.Reads;
_cycle.Value = new ReadCycle { Name = name };
if (!_cycleReads.TryGetValue(name, out var reads) || reads.Count < 2)
return;
long now = Environment.TickCount64;
List<(string Function, string ArgsJson)> wanted = new(reads.Count);
foreach (var read in reads)
{
if (!(_readCache.TryGetValue(read.Function + "|" + read.ArgsJson, out var hit) && now - hit.At <= hit.Ttl))
wanted.Add(read);
}
if (wanted.Count < 2)
return;
try
{
long start = Environment.TickCount64;
var results = Host.Control.CallBatch(wanted);
Interlocked.Increment(ref _statBatches);
Interlocked.Increment(ref _statCalls);
Interlocked.Add(ref _statMs, Environment.TickCount64 - start);
if (results is null)
return;
long at = Environment.TickCount64;
for (int i = 0; i < wanted.Count; i++)
{
if (results[i].Ok)
_readCache[wanted[i].Function + "|" + wanted[i].ArgsJson] = (results[i].Raw, at, PrefetchMs);
}
}
catch (Exception e)
{
TraceError("batch", e);
}
}
private string? TimedCall(string function, string argsJson)
{
long start = Environment.TickCount64;
try
{
return Host.Control.Call(function, argsJson);
}
finally
{
Interlocked.Increment(ref _statCalls);
Interlocked.Add(ref _statMs, Environment.TickCount64 - start);
string key = function is "getGameObject" or "getGameObjectS" or "isNull" && argsJson.Length < 120
? function + " " + argsJson.Trim('[', ']', '"')
: function;
_statByFunction.AddOrUpdate(key, 1, (_, n) => n + 1);
}
}
private void ReportStats()
{
long now = Environment.TickCount64, start = Interlocked.Read(ref _statStart);
if (now - start < 30_000 || Interlocked.CompareExchange(ref _statStart, now, start) != start)
return;
long calls = Interlocked.Exchange(ref _statCalls, 0), hits = Interlocked.Exchange(ref _statHits, 0), ms = Interlocked.Exchange(ref _statMs, 0), batches = Interlocked.Exchange(ref _statBatches, 0);
double secs = (now - start) / 1000.0;
if (calls + hits > 0)
{
var top = _statByFunction.ToArray().OrderByDescending(kv => kv.Value).Take(8)
.Select(kv => $"{kv.Key} {kv.Value / secs:0.#}/s");
_statByFunction.Clear();
Trace.WriteLine($"[Flash] {calls / secs:0} calls/s to the game, {(calls > 0 ? (double)ms / calls : 0):0.0} ms each, {hits / secs:0}/s from the cache ({batches / secs:0.0}/s read ahead); most: {string.Join(", ", top)}");
}
}
private void TraceError(string function, Exception e)
{
long now = Environment.TickCount64;
if (_lastErrorTrace.TryGetValue(function, out long last) && now - last < 30_000)
return;
_lastErrorTrace[function] = now;
Trace.WriteLine($"[Flash] {function} failed: {e.GetType().Name}: {e.Message}");
}
public object FromFlashXml(XElement el) => FlashValues.FromFlashXml(el);
public IFlashObject<T> CreateFlashObject<T>(string path)
{
return new FlashObject<T>(Call<int>("lnkCreate", path), this);
}
public bool PostCommand(string json) => Host.Control.Post(json);
public void Dispose()
{
Host.Control.ExternalCall -= OnExternalCall;
}
}
