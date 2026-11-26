using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using MoVibeSkua.Android.Controls;
using MoVibeSkua.Android.ViewModels;
using MoVibeSkua.Android.Views;
using Trace = System.Diagnostics.Trace;
namespace MoVibeSkua.Android.Platform;
public static class ArmyShell
{
private static Control? _placeholder;
public const bool DetachEnabled = false;
public static bool Detached { get; private set; }
public static TimeSpan DetachDelay
{
get
{
double ram = DeviceProfile.Current.RamGB;
return ram switch { <= 0 => TimeSpan.FromSeconds(20), < 12 => TimeSpan.FromSeconds(20), < 16 => TimeSpan.FromSeconds(60), _ => TimeSpan.FromSeconds(120) };
}
}
public static bool Detach()
{
if (Army.IsFirst || Detached || !Dispatcher.UIThread.CheckAccess())
return false;
MobileShellView? shell = MobileShellView.Instance;
if (shell is null || shell.HasDialogs || TopLevel.GetTopLevel(shell) is not { } top)
return false;
GameView.ParkOnDestroy = true;
if (MainActivity.Current is { } activity)
GameView.Park(activity);
shell.Release();
_placeholder ??= new Panel { Background = Brushes.Black };
top.Content = _placeholder;
SetMainView(_placeholder);
Detached = true;
DispatcherTimer.RunOnce(() =>
{
if (Detached && !GameView.IsParked && MainActivity.Current is { } activity)
GameView.Park(activity);
}, TimeSpan.FromSeconds(1), DispatcherPriority.Background);
return true;
}
public static MobileShellView? Attach()
{
if (!Detached)
return MobileShellView.Instance;
if (!Dispatcher.UIThread.CheckAccess())
return null;
Detached = false;
GameView.ParkOnDestroy = false;
try
{
MobileShellView shell = new() { DataContext = Ioc.Default.GetRequiredService<ShellViewModel>() };
if (_placeholder is not null && TopLevel.GetTopLevel(_placeholder) is { } top)
top.Content = shell;
SetMainView(shell);
Trace.WriteLine($"[ArmyMemory] client {Army.Slot} shell attached: managed {GC.GetTotalMemory(false) / 1048576} MB");
return shell;
}
catch (Exception ex)
{
Trace.WriteLine($"[ArmyMemory] shell attach failed: {ex}");
return MobileShellView.Instance;
}
}
public static MobileShellView? Shell()
{
if (!Detached)
return MobileShellView.Instance;
return Dispatcher.UIThread.CheckAccess() ? Attach() : Dispatcher.UIThread.Invoke(Attach);
}
private static void SetMainView(Control view)
{
try
{
if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime single && !ReferenceEquals(single.MainView, view))
single.MainView = view;
}
catch (Exception ex)
{
Trace.WriteLine($"[ArmyMemory] main view: {ex.Message}");
}
}
}
