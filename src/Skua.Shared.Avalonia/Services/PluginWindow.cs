using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using System;
namespace Skua.Shared.Avalonia.Services;
public interface IPluginPageHost
{
void Show(string key, string title, Control view, Action closed);
void Close(string key);
}
public sealed class PluginWindow
{
public static IPluginPageHost? PageHost { get; set; }
private readonly Window? _window;
private readonly Control? _page;
private readonly string _key;
private readonly string _title;
private bool _closed;
public event EventHandler? Closed;
public Control View { get; }
private PluginWindow(Control view, string title, double width, double height, double minWidth, double minHeight, bool ownTitleBar)
{
View = view;
_title = title;
_key = "plugin:" + title + "#" + view.GetHashCode();
if (PageHost is not null)
{
_page = PageContent(view, minHeight);
return;
}
_window = new Window
{
Title = title,
Width = width,
Height = height,
MinWidth = minWidth,
MinHeight = minHeight,
Content = view,
};
if (ownTitleBar)
{
_window.SystemDecorations = SystemDecorations.BorderOnly;
_window.ExtendClientAreaToDecorationsHint = true;
_window.ExtendClientAreaChromeHints = global::Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome;
_window.ExtendClientAreaTitleBarHeightHint = 32;
}
_window.Classes.Add("skua");
_window[!Window.BackgroundProperty] = new DynamicResourceExtension("SkuaPageBrush");
_window.Closed += (_, _) => OnClosed();
WindowPlacementHelper.CenterOnActiveScreen(_window);
}
public static PluginWindow Show(Control view, string title, double width, double height, double minWidth = 0, double minHeight = 0, bool ownTitleBar = false)
{
PluginWindow window = new(view, title, width, height, minWidth, minHeight, ownTitleBar);
window.Activate();
return window;
}
public void Activate()
{
if (_closed)
return;
if (_window is null)
{
PageHost?.Show(_key, _title, _page!, OnClosed);
return;
}
if (!_window.IsVisible)
_window.Show();
if (_window.WindowState == WindowState.Minimized)
_window.WindowState = WindowState.Normal;
_window.Activate();
}
public void Close()
{
if (_window is not null)
_window.Close();
else
PageHost?.Close(_key);
}
private static Control PageContent(Control view, double minHeight)
{
ScrollViewer scroller = new()
{
Content = view,
HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
};
scroller.SizeChanged += (_, e) => view.Height = Math.Max(minHeight, e.NewSize.Height);
return scroller;
}
private void OnClosed()
{
if (_closed)
return;
_closed = true;
Closed?.Invoke(this, EventArgs.Empty);
}
}
