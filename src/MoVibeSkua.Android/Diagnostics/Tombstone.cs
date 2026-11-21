using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
namespace MoVibeSkua.Android.Diagnostics;
internal static class Tombstone
{
private const int MaxFrames = 32;
public static string Describe(byte[] data, int length)
{
try
{
uint crashTid = 0;
string signal = string.Empty, abort = string.Empty;
List<string> causes = new();
Dictionary<uint, (string Name, List<string> Frames, string Top)> threads = new();
Reader top = new(data, 0, length);
while (top.Next(out int field, out int wire))
{
switch (field)
{
case 6 when wire == 0:
crashTid = (uint)top.Varint();
break;
case 10 when wire == 2:
signal = ReadSignal(top.Sub());
break;
case 14 when wire == 2:
abort = top.Text();
break;
case 15 when wire == 2:
Reader cause = top.Sub();
while (cause.Next(out int cf, out int cw))
{
if (cf == 1 && cw == 2)
causes.Add(cause.Text());
else
cause.Skip(cw);
}
break;
case 16 when wire == 2:
Reader entry = top.Sub();
uint key = 0;
(string Name, List<string> Frames, string Top) thread = (string.Empty, new List<string>(), string.Empty);
while (entry.Next(out int ef, out int ew))
{
if (ef == 1 && ew == 0)
key = (uint)entry.Varint();
else if (ef == 2 && ew == 2)
thread = ReadThread(entry.Sub());
else
entry.Skip(ew);
}
threads[key] = thread;
break;
default:
top.Skip(wire);
break;
}
}
if (signal.Length == 0 && threads.Count == 0)
return string.Empty;
threads.TryGetValue(crashTid, out (string Name, List<string> Frames, string Top) crashed);
StringBuilder sb = new();
sb.Append(signal.Length > 0 ? signal : "native crash");
if (!string.IsNullOrEmpty(crashed.Top))
sb.Append(" in ").Append(crashed.Top);
sb.Append('\n');
if (!string.IsNullOrEmpty(crashed.Name))
sb.Append("thread: ").Append(crashed.Name).Append('\n');
if (abort.Length > 0)
sb.Append("abort: ").Append(abort.Trim()).Append('\n');
foreach (string cause in causes.Take(3))
sb.Append("cause: ").Append(cause.Trim()).Append('\n');
if (crashed.Frames is { Count: > 0 })
sb.Append(string.Join("\n", crashed.Frames));
else
sb.Append("(the crashing thread's frames were not in the record)");
return sb.ToString();
}
catch
{
return string.Empty;
}
}
public static string MainThread(string dump)
{
string[] lines = dump.Split('\n');
int start = Array.FindIndex(lines, l => l.StartsWith("\"main\"", StringComparison.Ordinal));
if (start < 0)
return string.Empty;
List<string> block = new();
for (int i = start; i < lines.Length && block.Count < 60; i++)
{
string line = lines[i].TrimEnd();
if (line.Length == 0)
break;
if (line.TrimStart().StartsWith("|", StringComparison.Ordinal) && !line.Contains("state=", StringComparison.Ordinal))
continue;
block.Add(Shorten(line.Trim()));
}
return string.Join("\n", block);
}
private static string ReadSignal(Reader r)
{
string name = string.Empty, code = string.Empty;
long number = 0;
bool hasAddress = false;
ulong address = 0;
while (r.Next(out int field, out int wire))
{
if (field == 1 && wire == 0) number = (long)r.Varint();
else if (field == 2 && wire == 2) name = r.Text();
else if (field == 4 && wire == 2) code = r.Text();
else if (field == 8 && wire == 0) hasAddress = r.Varint() != 0;
else if (field == 9 && wire == 0) address = r.Varint();
else r.Skip(wire);
}
string text = name.Length > 0 ? name : "signal " + number;
if (code.Length > 0)
text += " (" + code + ")";
if (hasAddress)
text += address < 4096 ? " null pointer" : " at a bad address";
return text;
}
private static (string Name, List<string> Frames, string Top) ReadThread(Reader r)
{
string name = string.Empty, top = string.Empty;
List<string> frames = new();
while (r.Next(out int field, out int wire))
{
if (field == 2 && wire == 2)
name = r.Text();
else if (field == 4 && wire == 2)
{
Reader f = r.Sub();
ulong relPc = 0, offset = 0;
string function = string.Empty, file = string.Empty, build = string.Empty;
while (f.Next(out int ff, out int fw))
{
if (ff == 1 && fw == 0) relPc = f.Varint();
else if (ff == 4 && fw == 2) function = f.Text();
else if (ff == 5 && fw == 0) offset = f.Varint();
else if (ff == 6 && fw == 2) file = f.Text();
else if (ff == 8 && fw == 2) build = f.Text();
else f.Skip(fw);
}
if (frames.Count >= MaxFrames)
continue;
string lib = Shorten(file);
string where = function.Length > 0 ? $"{lib} ({Clip(function, 120)}+{offset})" : lib;
if (frames.Count == 0)
top = function.Length > 0 ? $"{lib} ({Clip(function, 80)})" : lib;
frames.Add($"#{frames.Count:00} pc {relPc:x8} {where}" + (build.Length >= 8 ? $" [{build[..8]}]" : ""));
}
else
r.Skip(wire);
}
return (name, frames, top);
}
private static string Shorten(string path)
{
int app = path.IndexOf("/data/app/", StringComparison.Ordinal);
if (app < 0)
return path;
int end = path.IndexOfAny(new[] { ' ', ')' }, app);
string full = end < 0 ? path[app..] : path[app..end];
string[] parts = full.Split('/', StringSplitOptions.RemoveEmptyEntries);
string package = parts.Length > 3 ? parts[3] : string.Empty;
int dash = package.IndexOf('-');
if (dash > 0)
package = package[..dash];
string file = parts.Length > 0 ? parts[^1] : full;
return path.Replace(full, package.Length > 0 ? package + "/" + file : file, StringComparison.Ordinal);
}
private static string Clip(string text, int max) => text.Length <= max ? text : text[..max] + "...";
private struct Reader
{
private readonly byte[] _data;
private readonly int _end;
private int _pos;
public Reader(byte[] data, int start, int end)
{
_data = data;
_pos = start;
_end = Math.Min(end, data.Length);
}
public bool Next(out int field, out int wire)
{
field = 0;
wire = 0;
if (_pos >= _end)
return false;
ulong tag = Varint();
field = (int)(tag >> 3);
wire = (int)(tag & 7);
return field > 0 && wire is 0 or 1 or 2 or 5;
}
public ulong Varint()
{
ulong value = 0;
int shift = 0;
while (_pos < _end && shift < 64)
{
byte b = _data[_pos++];
value |= (ulong)(b & 0x7F) << shift;
if ((b & 0x80) == 0)
break;
shift += 7;
}
return value;
}
public Reader Sub()
{
int length = (int)Math.Min(Varint(), (ulong)(_end - _pos));
Reader sub = new(_data, _pos, _pos + length);
_pos += length;
return sub;
}
public string Text()
{
int length = (int)Math.Min(Varint(), (ulong)(_end - _pos));
string text = Encoding.UTF8.GetString(_data, _pos, length);
_pos += length;
return text;
}
public void Skip(int wire)
{
switch (wire)
{
case 0: Varint(); break;
case 1: _pos = Math.Min(_end, _pos + 8); break;
case 5: _pos = Math.Min(_end, _pos + 4); break;
case 2:
int length = (int)Math.Min(Varint(), (ulong)(_end - _pos));
_pos += length;
break;
default: _pos = _end; break;
}
}
}
}
