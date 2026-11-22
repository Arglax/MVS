using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.DependencyInjection;
using Skua.Core.Interfaces;
using System;
using System.Linq;
namespace Skua.App.Avalonia.Views;
public partial class GameOptionsView : UserControl
{
private const string CompactModeSettingKey = "GameOptionsCompactMode";
private bool _compactMode;
public GameOptionsView()
{
InitializeComponent();
Loaded += GameOptionsView_Loaded;
LayoutUpdated += (_, _) => UpdateColumns();
try
{
_compactMode = Ioc.Default.GetService<ISettingsService>()?.Get(CompactModeSettingKey, false) ?? false;
}
catch { }
if (this.FindControl<ToggleButton>("CompactToggle") is { } compactToggle)
{
compactToggle.IsChecked = _compactMode;
compactToggle.IsCheckedChanged += (_, _) =>
{
_compactMode = compactToggle.IsChecked == true;
try
{
Ioc.Default.GetService<ISettingsService>()?.Set(CompactModeSettingKey, _compactMode);
}
catch { }
UpdateColumns();
};
}
if (OperatingSystem.IsAndroid()
&& this.FindControl<ToggleButton>("SearchToggle") is { } toggle
&& this.FindControl<TextBox>("SearchBox") is { } box)
{
toggle.IsVisible = true;
box.IsVisible = false;
toggle.IsCheckedChanged += (_, _) =>
{
box.IsVisible = toggle.IsChecked == true;
if (box.IsVisible)
box.Focus();
else
box.Text = string.Empty;
};
}
}
private void UpdateColumns()
{
int cols = _compactMode ? 2 : (OperatingSystem.IsAndroid() || (Bounds.Width > 0 && Bounds.Width < 560) ? 1 : 2);
if (this.FindControl<ItemsControl>("GroupsList") is { } groups)
groups.Classes.Set("two-col", cols == 2);
foreach (UniformGrid grid in this.GetVisualDescendants().OfType<UniformGrid>())
{
if (grid.Columns != cols)
grid.Columns = cols;
}
}
private void GameOptionsView_Loaded(object? sender, RoutedEventArgs e)
{
if (this.FindControl<TextBox>("ColumnsBox") is { } columnsBox)
{
columnsBox.TextInput -= ColumnsBox_TextInput;
columnsBox.TextChanged -= ColumnsBox_TextChanged;
columnsBox.TextInput += ColumnsBox_TextInput;
columnsBox.TextChanged += ColumnsBox_TextChanged;
}
}
private static void ColumnsBox_TextInput(object? sender, TextInputEventArgs e)
{
char firstChar = string.IsNullOrEmpty(e.Text) ? '\0' : e.Text[0];
if (!char.IsDigit(firstChar))
e.Handled = true;
}
private static void ColumnsBox_TextChanged(object? sender, TextChangedEventArgs e)
{
if (sender is not TextBox textBox)
return;
string original = textBox.Text ?? string.Empty;
char[] digits = original.Where(char.IsDigit).ToArray();
string sanitized = new(digits);
if (sanitized != original)
{
textBox.Text = sanitized;
textBox.CaretIndex = sanitized.Length;
}
}
}
