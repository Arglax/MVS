using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Skua.Core.Interfaces;
using Skua.Core.Messaging;
using Skua.Core.Models;
using Skua.Core.Utils;
using System.Diagnostics;
using System.Reflection;
namespace Skua.Core.Plugins;
public delegate void PluginLoadDelegate(IPluginContainer container);
public class PluginManager : IPluginManager
{
private Dictionary<ISkuaPlugin, IPluginContainer> _plugins = new();
public List<IPluginContainer> Containers => _plugins.Values.ToList();
public void Initialize()
{
Directory.CreateDirectory(Path.Combine(ClientFileSources.SkuaPluginsDIR, "options"));
string localPluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");
if (Directory.Exists(localPluginsDir))
{
foreach (string file in Directory.GetFiles(localPluginsDir, "*.dll"))
{
string dest = Path.Combine(ClientFileSources.SkuaPluginsDIR, Path.GetFileName(file));
try
{
if (!File.Exists(dest) || File.GetLastWriteTimeUtc(file) > File.GetLastWriteTimeUtc(dest))
File.Copy(file, dest, true);
}
catch { }
}
}
Directory.GetFiles(ClientFileSources.SkuaPluginsDIR)
.Where(f => f.EndsWith(".dll"))
.ForEach(f =>
{
Exception? e = Load(f);
if (e is not null)
Trace.WriteLine($"Error while loading plugin '{f}': {e.Message}");
});
}
public Exception? Load(string path)
{
ISkuaPlugin? plugin = null;
try
{
Assembly asm = Assembly.LoadFrom(path);
TypeInfo? type = asm.DefinedTypes.FirstOrDefault(t => typeof(ISkuaPlugin).IsAssignableFrom(t));
if (type is null)
return new Exception("Plugin class not found.");
if (Activator.CreateInstance(type) is not ISkuaPlugin pluginInstance)
return new Exception("Failed to create plugin instance.");
plugin = pluginInstance;
PluginContainer? container = new(plugin);
_plugins.Add(plugin, container);
try
{
plugin.Load(Ioc.Default, Ioc.Default.GetRequiredService<IPluginHelper>());
container.OptionContainer.SetDefaults();
container.OptionContainer.Load();
StrongReferenceMessenger.Default.Send<PluginLoadedMessage, int>(new(container), (int)MessageChannels.Plugins);
return null;
}
catch (Exception loadException)
{
_plugins.Remove(plugin);
try
{
plugin.Unload();
}
catch
{
}
throw new Exception($"Failed to initialize plugin: {loadException.Message}", loadException);
}
}
catch (Exception e)
{
if (plugin != null)
{
_plugins.Remove(plugin);
}
return e;
}
}
public void Unload(ISkuaPlugin plugin)
{
StrongReferenceMessenger.Default.Send<PluginUnloadedMessage, int>(new(_plugins[plugin]), (int)MessageChannels.Plugins);
plugin.Unload();
_plugins.Remove(plugin);
}
public void Unload(string pluginName)
{
ISkuaPlugin? plugin = Containers.Find(x => x.ToString() == pluginName)?.Plugin;
if (plugin is null)
return;
StrongReferenceMessenger.Default.Send<PluginUnloadedMessage, int>(new(_plugins[plugin]), (int)MessageChannels.Plugins);
plugin.Unload();
_plugins.Remove(plugin);
}
public IPluginContainer? GetContainer(ISkuaPlugin plugin)
{
return _plugins.TryGetValue(plugin, out IPluginContainer? container) ? container : null;
}
public IPluginContainer? GetContainer(string pluginName)
{
return Containers.Exists(x => x.ToString() == pluginName) ? Containers.Find(x => x.ToString() == pluginName) : null;
}
public IPluginContainer GetContainer<T>() where T : ISkuaPlugin
{
return _plugins.FirstOrDefault(kvp => kvp.Key is T).Value;
}
}
