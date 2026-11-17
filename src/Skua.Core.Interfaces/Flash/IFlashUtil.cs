using Newtonsoft.Json;
using System.Xml.Linq;
namespace Skua.Core.Interfaces;
public delegate void FlashCallHandler(string function, params object[] args);
public interface IFlashUtil : IDisposable
{
event FlashCallHandler? FlashCall;
bool IsWorldLoaded => !IsNull("world");
void InitializeFlash();
string? Call(string function, params object[] args);
T? Call<T>(string function, params object[] args);
object? Call(string function, Type type, params object[] args);
object FromFlashXml(XElement el);
IFlashObject<T> CreateFlashObject<T>(string path);
bool IsNull(string path)
{
return Call<bool>("isNull", path);
}
string? GetGameObject(string path)
{
if (path.Contains('['))
{
string key = path.Split(new char[] { '"', '[', ']' }, StringSplitOptions.RemoveEmptyEntries).Last();
string finalPath = path.Split('[')[0];
return Call("getGameObjectKey", finalPath, key);
}
return Call("getGameObject", path);
}
T? GetGameObject<T>(string path, T? def = default)
{
try
{
return JsonConvert.DeserializeObject<T>(GetGameObject(path)!) ?? def;
}
catch
{
return def;
}
}
string? GetGameObjectStatic(string path)
{
return Call("getGameObjectS", path);
}
T? GetGameObjectStatic<T>(string path, T? def = default)
{
try
{
return JsonConvert.DeserializeObject<T>(GetGameObjectStatic(path)!);
}
catch
{
return def;
}
}
void SetGameObject(string path, object value)
{
if (path.Contains('['))
{
string key = path.Split(new char[] { '"', '[', ']' }, StringSplitOptions.RemoveEmptyEntries).Last();
string finalPath = path.Split('[')[0];
Call("setGameObjectKey", finalPath, key, value);
return;
}
Call("setGameObject", path, value);
}
string? CallGameFunction(string path, params object[] args)
{
return args.Length > 0 ? Call("callGameFunction", new object[] { path }.Concat(args).ToArray()) : Call("callGameFunction0", path);
}
T? CallGameFunction<T>(string path, params object[] args)
{
try
{
return JsonConvert.DeserializeObject<T>(CallGameFunction(path, args)!);
}
catch
{
return default;
}
}
string? GetArrayObject(string path, int index)
{
return Call("getArrayObject", path, index);
}
T? GetArrayObject<T>(string path, int index, T? def = default)
{
try
{
return JsonConvert.DeserializeObject<T>(GetArrayObject(path, index)!);
}
catch
{
return def;
}
}
void BeginReadCycle(string name) { }
List<T> SelectArrayObjects<T>(string path, string selector)
{
try
{
return JsonConvert.DeserializeObject<List<T>>(Call("selectArrayObjects", path, selector)!)!;
}
catch
{
return new List<T>();
}
}
}
