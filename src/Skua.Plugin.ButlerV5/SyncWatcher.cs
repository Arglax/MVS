using System.IO;
namespace ButlerV5;
public sealed class SyncWatcher : IDisposable
{
private readonly FileSystemWatcher _fsw;
private readonly System.Timers.Timer _debounce;
private int _pending;
public event Action? Changed;
public SyncWatcher(string directory, string filter, double debounceMs = 150)
{
Directory.CreateDirectory(directory);
_debounce = new System.Timers.Timer(debounceMs) { AutoReset = false };
_debounce.Elapsed += (_, _) =>
{
Interlocked.Exchange(ref _pending, 0);
try { Changed?.Invoke(); } catch { }
};
_fsw = new FileSystemWatcher(directory, filter)
{
NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
IncludeSubdirectories = false,
InternalBufferSize = 64 * 1024,
};
_fsw.Changed += OnEvent;
_fsw.Created += OnEvent;
_fsw.Deleted += OnEvent;
_fsw.Renamed += OnEvent;
_fsw.Error += OnError;
_fsw.EnableRaisingEvents = true;
DebugLog.Log("SyncWatcher", $"watching {directory} filter {filter}");
}
private void OnEvent(object sender, FileSystemEventArgs e) => Schedule();
private void OnError(object sender, ErrorEventArgs e) => Schedule();
private void Schedule()
{
if (Interlocked.Exchange(ref _pending, 1) == 0)
_debounce.Start();
}
public void Dispose()
{
try { _fsw.EnableRaisingEvents = false; } catch { }
try { _fsw.Dispose(); } catch { }
try { _debounce.Dispose(); } catch { }
}
}
