using Avalonia;
using Avalonia.Controls;
using Skua.App.Avalonia.ViewModels;
using System;
using System.ComponentModel;
namespace Skua.App.Avalonia.Views;
public partial class ScriptStatsView : UserControl
{
private const double AdvancedHeight = 760;
private ScriptStatsViewModel? _vm;
private Window? _window;
public ScriptStatsView()
{
InitializeComponent();
DataContextChanged += (_, _) => Watch(DataContext as ScriptStatsViewModel);
AttachedToVisualTree += (_, _) =>
{
Watch(DataContext as ScriptStatsViewModel);
WatchWindow(TopLevel.GetTopLevel(this) as Window);
ApplyWindowSize();
};
DetachedFromVisualTree += (_, _) =>
{
Watch(null);
WatchWindow(null);
};
}
private void WatchWindow(Window? window)
{
if (_window is not null)
_window.Activated -= OnWindowActivated;
_window = window;
if (_window is not null)
_window.Activated += OnWindowActivated;
}
private void OnWindowActivated(object? sender, EventArgs e)
{
if (_vm is { AdvancedMode: true })
_vm.RefreshHistory();
}
private void Watch(ScriptStatsViewModel? vm)
{
if (_vm is not null)
_vm.PropertyChanged -= OnViewModelPropertyChanged;
_vm = vm;
if (_vm is not null)
_vm.PropertyChanged += OnViewModelPropertyChanged;
}
private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
{
if (e.PropertyName == nameof(ScriptStatsViewModel.AdvancedMode))
ApplyWindowSize();
}
private void ApplyWindowSize()
{
if (_vm is null || TopLevel.GetTopLevel(this) is not Window window)
return;
if (_vm.AdvancedMode)
{
window.SizeToContent = SizeToContent.Manual;
window.Height = Math.Min(AdvancedHeight, window.MaxHeight);
}
else
{
window.SizeToContent = SizeToContent.Height;
}
}
}
