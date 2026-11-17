using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Skua.App.Avalonia.ViewModels;
namespace Skua.App.Avalonia.UserControls;
public partial class AutoUserControl : UserControl
{
public AutoUserControl()
{
InitializeComponent();
if (ClassPickerButton.Flyout is PopupFlyoutBase flyout)
flyout.Opening += ClassFlyout_Opening;
ClassList.SelectionChanged += ClassList_SelectionChanged;
ClassSearchBox.AddHandler(PointerPressedEvent, (_, _) => ClassSearchBox.Focusable = true, RoutingStrategies.Tunnel);
AttachedToVisualTree += (_, _) =>
{
if (DataContext is not AutoViewModel vm)
return;
vm.SyncEquippedClassCommand.Execute(null);
if (vm.MapMonsterRows.Count == 0)
vm.RefreshMonstersCommand.Execute(null);
};
}
private void ClassFlyout_Opening(object? sender, System.EventArgs e)
{
if (DataContext is not AutoViewModel vm)
return;
ClassSearchBox.Focusable = false;
vm.ClassSearch = string.Empty;
ClassList.SelectedItem = null;
vm.ReloadClassesCommand.Execute(null);
}
private void ClassList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
{
if (ClassList.SelectedItem is not ClassEntry entry || DataContext is not AutoViewModel vm)
return;
vm.SelectedClass = entry.Name;
ClassPickerButton.Flyout?.Hide();
}
}
