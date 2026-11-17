namespace Skua.Core.Interfaces;
public interface IPluginManager
{
List<IPluginContainer> Containers { get; }
void Initialize();
Exception? Load(string path);
void Unload(ISkuaPlugin plugin);
void Unload(string pluginName);
IPluginContainer? GetContainer(ISkuaPlugin plugin);
IPluginContainer? GetContainer(string pluginName);
IPluginContainer GetContainer<T>() where T : ISkuaPlugin;
}
