namespace Skua.Core.Interfaces;
public interface IPluginHelper
{
void AddMenuButton(string text, Action action);
void RemoveMenuButton(string text);
IReadOnlyList<ViewModels.IMainMenuItemViewModel> MenuButtons => Array.Empty<ViewModels.IMainMenuItemViewModel>();
}
