using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MoVibeSkua.Android.Services;
namespace MoVibeSkua.Android.Views;
public static class RamBoostDialogs
{
private static readonly FontFamily NameFont = new("avares://MoVibeSkua/Images/fonts#Roboto, Noto Sans CJK JP, Noto Sans CJK SC, Noto Sans CJK KR, Noto Sans, sans-serif");
public static void ShowResult(RamBoostResult result, string summary)
{
Platform.Modal.Show(close => Build(close, () =>
{
StackPanel head = new() { Spacing = 8 };
head.Children.Add(Text(summary, false));
StackPanel list = new() { Spacing = 6 };
List<Button> buttons = new();
if (result.UsedShizuku)
{
if (result.StillRunning.Count == 0)
head.Children.Add(Text("No other background apps are running.", true));
else
{
head.Children.Add(Text("Still running in the background, biggest first. Force stop closes an app completely; anything unsaved in it is lost.", true));
List<RamBoostApp> left = new(result.StillRunning);
foreach (RamBoostApp app in left)
list.Children.Add(AppRow(app, list));
Button all = new() { Content = "Force stop all listed", Classes = { "skua-button" } };
all.Click += async (_, _) =>
{
all.IsEnabled = false;
Usage.UsageTracker.Feature("gfxRamBoostStopAll");
await Task.Run(() => { foreach (RamBoostApp app in left) ShizukuShell.ForceStop(app.Package); });
list.Children.Clear();
list.Children.Add(Text($"Stopped {left.Count} apps.", true));
};
buttons.Add(all);
}
}
else
{
Border hint = new() { Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(Color.Parse("#33E0A060")) };
hint.Child = Text("Android does not tell apps which other apps are running or how much RAM they use. Install Shizuku for real RAM per app and Force stop from MVS.", false);
head.Children.Add(hint);
Button apps = new() { Content = "Open Android's app list", Classes = { "skua-button" } };
apps.Click += (_, _) => Safe(RamBoost.OpenAppList);
buttons.Add(apps);
if (ShizukuShell.Available)
{
Button use = new() { Content = "Use Shizuku", Classes = { "skua-button" } };
use.Click += async (_, _) =>
{
use.IsEnabled = false;
Usage.UsageTracker.Feature("gfxRamBoostShizuku");
bool granted = await ShizukuShell.RequestAsync();
IReadOnlyList<RamBoostApp> running = granted ? await Task.Run(() => RamBoost.ListBackground()) : Array.Empty<RamBoostApp>();
Dispatcher.UIThread.Post(() =>
{
if (!granted)
{
use.IsEnabled = true;
list.Children.Clear();
list.Children.Add(Text("Shizuku did not allow MVS. Open Shizuku and allow MVS, then try again.", true));
return;
}
close(string.Empty);
ShowResult(result with { StillRunning = running, UsedShizuku = true }, summary);
});
};
buttons.Add(use);
}
}
return (head, list, buttons);
}, "RAM boosted"));
}
public static void ShowAutoBoostWarning(Action<bool> onAnswer)
{
bool answered = false;
void Answer(bool keep)
{
if (answered) return;
answered = true;
onAnswer(keep);
}
Platform.Modal.Show(close => Build(_ => { close(string.Empty); Answer(false); }, () =>
{
StackPanel head = new() { Spacing = 8 };
head.Children.Add(Text("Each time you come back to MVS (at most every 5 minutes), other apps running in the background are closed: music, chats, other games.", false));
head.Children.Add(Text("Anything unsaved in those apps, like game progress or a half-written message, may be lost. Android still keeps apps on screen, system apps and your keyboard running.", true));
Button keep = new() { Content = "Turn on", MinWidth = 80, Classes = { "skua-button", "skua-primary" } };
keep.Click += (_, _) => { close(string.Empty); Answer(true); };
return (head, new StackPanel(), new List<Button> { keep });
}, "Auto boost closes other apps", "Cancel"));
}
private static Control AppRow(RamBoostApp app, Panel list)
{
Grid row = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
StackPanel text = new() { VerticalAlignment = VerticalAlignment.Center };
text.Children.Add(new TextBlock { Text = app.Label, FontFamily = NameFont, FontSize = 14, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
text.Children.Add(Text(string.Create(CultureInfo.InvariantCulture, $"{app.RamMb} MB · {app.State}"), true));
row.Children.Add(text);
Button stop = new() { Content = "Force stop", Classes = { "skua-button" }, VerticalAlignment = VerticalAlignment.Center };
string package = app.Package;
stop.Click += async (_, _) =>
{
stop.IsEnabled = false;
Usage.UsageTracker.Feature("gfxRamBoostForceStop");
bool ok = await Task.Run(() => ShizukuShell.ForceStop(package));
if (ok)
list.Children.Remove(row);
else
stop.IsEnabled = true;
};
Grid.SetColumn(stop, 1);
row.Children.Add(stop);
return row;
}
private static Control Build(Action<string> close, Func<(Control Head, Control List, List<Button> Buttons)> content, string title, string doneLabel = "Done")
{
(Control head, Control list, List<Button> buttons) = content();
void Close() => close(string.Empty);
Grid body = new() { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"), RowSpacing = 8, Margin = new Thickness(16, 8, 8, 14) };
Grid header = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
header.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
Button x = new() { Content = "×", FontSize = 22, Padding = new Thickness(14, 0, 14, 4), Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
x.Click += (_, _) => Close();
Grid.SetColumn(x, 1);
header.Children.Add(x);
body.Children.Add(header);
Border headBox = new() { Child = head, Margin = new Thickness(0, 0, 8, 0) };
Grid.SetRow(headBox, 1);
body.Children.Add(headBox);
ScrollViewer scroll = new() { Content = list, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 0, 8, 0) };
Grid.SetRow(scroll, 2);
body.Children.Add(scroll);
WrapPanel row = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 8, 0) };
foreach (Button button in buttons)
{
button.Margin = new Thickness(8, 4, 0, 0);
row.Children.Add(button);
}
Button done = new() { Content = doneLabel, MinWidth = 80, Margin = new Thickness(8, 4, 0, 0), Classes = { "skua-button" } };
if (doneLabel != "Cancel")
done.Classes.Add("skua-primary");
done.Click += (_, _) => Close();
row.Children.Add(done);
Grid.SetRow(row, 3);
body.Children.Add(row);
Border card = new() { Child = body };
card.Classes.Add("mobile-dialog");
card.PointerPressed += (_, e) => e.Handled = true;
Panel wrapper = new() { Background = Brushes.Transparent };
wrapper.SizeChanged += (_, e) => card.MaxHeight = Math.Max(160, e.NewSize.Height - 24);
wrapper.PointerPressed += (_, e) =>
{
if (ReferenceEquals(e.Source, wrapper))
Close();
};
wrapper.Children.Add(card);
MobileShellView.Instance?.SetDismiss(wrapper, Close);
return wrapper;
}
private static void Safe(Action action)
{
try { action(); }
catch (Exception e) { global::Android.Util.Log.Warn("MVS", "ram boost: " + e.Message); }
}
private static TextBlock Text(string text, bool muted)
{
TextBlock block = new() { Text = text, FontSize = 13, TextWrapping = TextWrapping.Wrap };
if (muted && Application.Current?.TryFindResource("SkuaMutedTextBrush", out object? brush) == true && brush is IBrush mutedBrush)
block.Foreground = mutedBrush;
return block;
}
}
