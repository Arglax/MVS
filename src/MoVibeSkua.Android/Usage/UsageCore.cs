using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
namespace MoVibeSkua.Android.Usage;
public sealed class SignalCounters
{
public const long DebounceMs = 250;
public const long BatchCap = 1000;
private readonly ConcurrentDictionary<string, long> _pending = new();
private readonly ConcurrentDictionary<string, long> _lastCounted = new();
private readonly Func<long> _now;
public SignalCounters(Func<long>? now = null)
{
_now = now ?? (() => Environment.TickCount64);
}
public bool Track(string id)
{
long time = _now();
if (_lastCounted.TryGetValue(id, out long previous) && time - previous < DebounceMs)
return false;
_lastCounted[id] = time;
_pending.AddOrUpdate(id, 1, (_, count) => Math.Min(count + 1, BatchCap));
return true;
}
public void AddRaw(string id, long delta)
{
if (delta > 0)
_pending.AddOrUpdate(id, delta, (_, count) => count + delta);
}
public void Restore(IReadOnlyDictionary<string, long> values)
{
foreach ((string id, long count) in values)
AddRaw(id, count);
}
public Dictionary<string, long> Snapshot() => new(_pending);
public void Subtract(IReadOnlyDictionary<string, long> uploaded)
{
foreach ((string id, long count) in uploaded)
{
while (_pending.TryGetValue(id, out long current))
{
long left = current - count;
if (left > 0 ? _pending.TryUpdate(id, left, current) : _pending.TryRemove(new KeyValuePair<string, long>(id, current)))
break;
}
}
}
public void Clear()
{
_pending.Clear();
_lastCounted.Clear();
}
}
public sealed class VisitClock
{
public const long MinVisitMs = 300;
public const long QuickExitMs = 4_000;
public const long IdleMs = 20_000;
public const long MaxVisitMs = 30L * 60 * 1000;
private readonly Func<long> _now;
private readonly object _gate = new();
private Screen? _screen;
private bool _inForeground;
private bool _running;
private long _startedAt;
private long _activeMs;
private int _actions;
public VisitClock(Func<long>? now = null)
{
_now = now ?? (() => Environment.TickCount64);
}
public Screen? Current
{
get
{
lock (_gate)
return _screen;
}
}
public void Enter(Screen next, Action<string, long> sink)
{
lock (_gate)
{
FinishLocked(sink);
_screen = next;
_activeMs = 0;
_actions = 0;
if (_inForeground)
{
_running = true;
_startedAt = _now();
}
}
}
public void Foreground()
{
lock (_gate)
{
_inForeground = true;
if (_screen is not null && !_running)
{
_running = true;
_startedAt = _now();
}
}
}
public void Background()
{
lock (_gate)
{
_inForeground = false;
if (_running)
{
_activeMs += _now() - _startedAt;
_running = false;
}
}
}
public void Action()
{
lock (_gate)
_actions++;
}
public void Finish(Action<string, long> sink)
{
lock (_gate)
FinishLocked(sink);
}
private void FinishLocked(Action<string, long> sink)
{
if (_screen is not { } current)
return;
if (_running)
{
_activeMs += _now() - _startedAt;
_running = false;
}
long ms = Math.Clamp(_activeMs, 0, MaxVisitMs);
bool acted = _actions > 0;
_screen = null;
_activeMs = 0;
_actions = 0;
if (ms < MinVisitMs && !acted)
return;
string prefix = "scr." + current + ".";
sink(prefix + "visits", 1);
sink(prefix + "ms", ms);
sink(prefix + (ms < 5_000 ? "d5" : ms < 15_000 ? "d15" : ms < 60_000 ? "d60" : ms < 180_000 ? "d180" : "dmax"), 1);
if (!acted && ms < QuickExitMs)
sink(prefix + "quick", 1);
if (!acted && ms >= IdleMs)
sink(prefix + "idle", 1);
}
}
public sealed class DayClock
{
private readonly string _kind;
private readonly Func<DateTime> _utcNow;
private readonly object _gate = new();
private DateTime? _since;
public DayClock(string kind, Func<DateTime>? utcNow = null)
{
_kind = kind;
_utcNow = utcNow ?? (() => DateTime.UtcNow);
}
public bool Running
{
get
{
lock (_gate)
return _since is not null;
}
}
public static string Key(DateTime utcDay, string kind) => $"day.d{utcDay:yyyyMMdd}.{kind}";
public void Start()
{
lock (_gate)
_since ??= _utcNow();
}
public void Stop(Action<string, long> sink)
{
lock (_gate)
{
FlushLocked(sink);
_since = null;
}
}
public void Flush(Action<string, long> sink)
{
lock (_gate)
FlushLocked(sink);
}
private void FlushLocked(Action<string, long> sink)
{
if (_since is not { } from)
return;
DateTime now = _utcNow();
_since = now;
if (now <= from)
return;
while (from.Date < now.Date)
{
DateTime midnight = from.Date.AddDays(1);
sink(Key(from.Date, _kind), (long)(midnight - from).TotalMilliseconds);
from = midnight;
}
sink(Key(from.Date, _kind), (long)(now - from).TotalMilliseconds);
}
}
public static class UsageIds
{
public static readonly string[] ScreenFields = { "visits", "ms", "quick", "idle", "d5", "d15", "d60", "d180", "dmax" };
public static readonly string[] PerfSums = { "n", "fpsSum", "ramSum", "ramPctSum", "tempSum", "hot" };
public static readonly string[] PerfDaySums = { "n", "fpsSum", "ramSum", "ramPctSum" };
public static readonly string[] FpsBuckets = { "b0", "b15", "b20", "b25", "b30", "b40", "b50", "b60" };
public static readonly string[] RamBuckets = { "p0", "p10", "p20", "p30", "p40", "p50" };
public static readonly string[] Health =
{
"sessions", "sessionMs", "crashes", "engineReloads", "loginFails", "mapLoads", "mapLoadMsSum",
"gameLoads", "gameLoadMsSum", "lowMemory", "anr"
};
public static readonly string[] ExitReasons =
{
"unknown", "exitSelf", "signaled", "lowMemory", "crash", "crashNative", "anr", "initFailure",
"permissionChange", "excessiveResourceUsage", "userRequested", "userStopped", "dependencyDied",
"other", "freezer", "packageStateChange", "packageUpdated"
};
public const int Scale = 100;
private static readonly HashSet<string> Known = Enum.GetNames<Signal>()
.Concat(Enum.GetNames<Screen>().SelectMany(screen => ScreenFields.Select(field => $"scr.{screen}.{field}")))
.ToHashSet(StringComparer.Ordinal);
private static readonly System.Text.RegularExpressions.Regex DayId = new(@"^day\.d\d{8}\.(fg|run|bot)$");
private static readonly System.Text.RegularExpressions.Regex PerfDayId = new(@"^pd\.d\d{8}\.(\w+)$");
private static readonly HashSet<string> KnownV2 = Features.Uses.Select(id => "f." + id)
.Concat(Features.Switches.SelectMany(id => new[] { $"f.{id}.on", $"f.{id}.off" }))
.Concat(PerfSums.Select(f => "p." + f))
.Concat(FpsBuckets.Select(b => "fh." + b))
.Concat(RamBuckets.Select(b => "rh." + b))
.Concat(Health.Select(h => "h." + h))
.Concat(ExitReasons.Select(r => "h.exitReasons." + r))
.ToHashSet(StringComparer.Ordinal);
public static bool IsKnown(string id) => Known.Contains(id) || DayId.IsMatch(id);
public static bool IsV2(string id)
{
if (KnownV2.Contains(id))
return true;
System.Text.RegularExpressions.Match day = PerfDayId.Match(id);
return day.Success && Array.IndexOf(PerfDaySums, day.Groups[1].Value) >= 0;
}
public static string PathV1(string id) =>
id.StartsWith("scr.", StringComparison.Ordinal) ? "screens." + id[4..]
: id.StartsWith("day.", StringComparison.Ordinal) ? "days." + id[4..]
: "features." + id;
public static string PathV2(string id) =>
id.StartsWith("f.", StringComparison.Ordinal) ? "features." + id[2..]
: id.StartsWith("pd.", StringComparison.Ordinal) ? "perfDays." + id[3..]
: id.StartsWith("p.", StringComparison.Ordinal) ? "perf." + id[2..]
: id.StartsWith("fh.", StringComparison.Ordinal) ? "fpsHist." + id[3..]
: id.StartsWith("rh.", StringComparison.Ordinal) ? "ramHist." + id[3..]
: "health." + id[2..];
public static bool IsScaled(string id) =>
id.EndsWith(".fpsSum", StringComparison.Ordinal) || id.EndsWith(".ramPctSum", StringComparison.Ordinal) || id.EndsWith(".tempSum", StringComparison.Ordinal);
public static string? PerfDay(string idOrPath)
{
int at = idOrPath.IndexOf(".d", StringComparison.Ordinal);
return at >= 0 && idOrPath.Length >= at + 10 && (idOrPath.StartsWith("pd.", StringComparison.Ordinal) || idOrPath.StartsWith("perfDays.", StringComparison.Ordinal))
? idOrPath.Substring(at + 1, 9)
: null;
}
}
public sealed class Extremes
{
private readonly object _gate = new();
private readonly Dictionary<string, long> _values = new(StringComparer.Ordinal);
public static bool IsMinimum(string path) => path.EndsWith(".fpsMin", StringComparison.Ordinal);
public void Offer(string path, long value)
{
lock (_gate)
{
if (!_values.TryGetValue(path, out long current) || (IsMinimum(path) ? value < current : value > current))
_values[path] = value;
}
}
public Dictionary<string, long> Snapshot()
{
lock (_gate)
return new Dictionary<string, long>(_values, StringComparer.Ordinal);
}
public void Restore(IReadOnlyDictionary<string, long> saved)
{
foreach ((string path, long value) in saved)
Offer(path, value);
}
public void Uploaded(IReadOnlyDictionary<string, long> sent)
{
lock (_gate)
{
foreach ((string path, long value) in sent)
{
if (_values.TryGetValue(path, out long current) && current == value)
_values.Remove(path);
}
}
}
public void Remove(IEnumerable<string> paths)
{
lock (_gate)
{
foreach (string path in paths)
_values.Remove(path);
}
}
}
