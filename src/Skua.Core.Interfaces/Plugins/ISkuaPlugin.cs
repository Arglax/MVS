namespace Skua.Core.Interfaces;
public interface ISkuaPlugin
{
string Name { get; }
string Author { get; }
string Description { get; }
string OptionsStorage => $"{Author}_{Name}".Replace(' ', '_').Trim();
void Load(IServiceProvider provider, IPluginHelper helper);
void Unload();
List<IOption>? Options { get; }
}
