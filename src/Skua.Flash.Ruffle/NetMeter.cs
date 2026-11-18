using System.Diagnostics;
using System.Globalization;
namespace Skua.Flash.Ruffle;
public static class NetMeter
{
private static readonly object Gate = new();
private static readonly Dictionary<string, (long Bytes, int Count)> Files = new(StringComparer.Ordinal);
private static long _fileBytes, _socketBytes, _totalBytes;
private static int _fileCount;
private static Timer? _timer;
public static double TotalMb => Interlocked.Read(ref _totalBytes) / 1048576.0;
public static double LastMinuteMb { get; private set; }
public static void Download(string path, long bytes)
{
if (bytes <= 0)
return;
lock (Gate)
{
_fileBytes += bytes;
_fileCount++;
Files.TryGetValue(path, out (long Bytes, int Count) seen);
Files[path] = (seen.Bytes + bytes, seen.Count + 1);
Start();
}
Interlocked.Add(ref _totalBytes, bytes);
}
public static void Socket(long bytes)
{
if (bytes <= 0)
return;
lock (Gate)
{
_socketBytes += bytes;
Start();
}
Interlocked.Add(ref _totalBytes, bytes);
}
private static void Start() => _timer ??= new Timer(_ => Report(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
private static void Report()
{
try
{
long files, socket;
int count;
List<KeyValuePair<string, (long Bytes, int Count)>> top;
lock (Gate)
{
files = _fileBytes;
socket = _socketBytes;
count = _fileCount;
top = Files.OrderByDescending(f => f.Value.Bytes).Take(3).ToList();
_fileBytes = 0;
_socketBytes = 0;
_fileCount = 0;
Files.Clear();
}
LastMinuteMb = (files + socket) / 1048576.0;
if (files + socket < 20 * 1024)
return;
string biggest = top.Count == 0 ? string.Empty : "; biggest: " + string.Join(", ", top.Select(f =>
$"{Path.GetFileName(f.Key)} {Mb(f.Value.Bytes)}{(f.Value.Count > 1 ? $" (x{f.Value.Count})" : string.Empty)}"));
KeyValuePair<string, (long Bytes, int Count)> repeat = top.FirstOrDefault(f => f.Value.Count >= 5);
string warn = repeat.Key is null ? string.Empty : $"; WARNING {Path.GetFileName(repeat.Key)} downloaded {repeat.Value.Count} times in a minute";
Trace.WriteLine($"[Net] last minute: {Mb(files)} in {count} files, {Mb(socket)} game connection{biggest}{warn}; since start {Mb(Interlocked.Read(ref _totalBytes))}");
}
catch
{
}
}
private static string Mb(long bytes) => bytes >= 1048576
? (bytes / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB"
: (bytes / 1024.0).ToString("0", CultureInfo.InvariantCulture) + " KB";
}
