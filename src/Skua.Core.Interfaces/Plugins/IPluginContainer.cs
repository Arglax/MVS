using Skua.Core.Models;
namespace Skua.Core.Interfaces;
public interface IPluginContainer
{
IOptionContainer OptionContainer { get; }
ISkuaPlugin Plugin { get; }
string OptionsFile => Path.Combine(ClientFileSources.SkuaPluginsDIR, "options", $"{Plugin.OptionsStorage}.cfg");
}
