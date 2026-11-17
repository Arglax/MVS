namespace Skua.Core.Utils;
public class TimeLimiter
{
private Dictionary<string, int> _last = new();
private const int MaxCacheSize = 1000;
private const int CleanupThreshold = 5 * 60 * 1000;
public bool LimitedRun(string name, int delay, Action action)
{
if (_last.Count > MaxCacheSize)
{
CleanupOldEntries();
}
bool run = !_last.TryGetValue(name, out int time) || Environment.TickCount - time >= delay;
if (run)
{
action();
_last[name] = Environment.TickCount;
}
return run;
}
private void CleanupOldEntries()
{
int currentTick = Environment.TickCount;
string[] keysToRemove = _last.Where(kvp => currentTick - kvp.Value > CleanupThreshold)
.Select(kvp => kvp.Key)
.ToArray();
foreach (string key in keysToRemove)
{
_last.Remove(key);
}
}
}
