using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MoVibeSkua.Android.Views;
namespace MoVibeSkua.Android.Diagnostics;
public static class CrashPrompt
{
public const string AskCrashed = "MVS has crashed.";
public const string AskProblem = "MVS ran into a problem.";
private static int _showing;
public static bool IsShowing => Volatile.Read(ref _showing) == 1;
public static void WhenReady()
{
_ = Task.Run(async () =>
{
for (int i = 0; i < 120 && MobileShellView.Instance is null; i++)
await Task.Delay(1000);
if (MobileShellView.Instance is not null)
RunNotifier.ShowWhenOnScreen(ShowPending);
});
}
public static void ShowPending()
{
if (Interlocked.CompareExchange(ref _showing, 1, 0) != 0)
return;
_ = Task.Run(() =>
{
IReadOnlyList<string> kinds;
string why = string.Empty;
try
{
IReadOnlyList<(string Kind, string Error, string Script)> waiting = ProblemReports.Waiting();
kinds = waiting.Select(w => w.Kind).ToList();
if (waiting.Count > 0)
{
(string Kind, string Error, string Script) worst = waiting
.OrderBy(w => w.Kind == "crash" ? 0 : w.Kind == "engine" ? 1 : 2).First();
why = CrashReasons.Explain(worst.Kind, worst.Error, worst.Script);
}
}
catch
{
kinds = Array.Empty<string>();
}
if (kinds.Count == 0)
{
Interlocked.Exchange(ref _showing, 0);
return;
}
string title = kinds.Any(k => k is "crash" or "engine") ? AskCrashed : AskProblem;
Dispatcher.UIThread.Post(() =>
{
if (MobileShellView.Instance is null)
{
Interlocked.Exchange(ref _showing, 0);
return;
}
Usage.UsageTracker.Feature("crashPromptShown");
Trace.WriteLine($"[Report] asking about {kinds.Count} report(s)");
Platform.Modal.Show(close => Build(title, why, answer =>
{
close(string.Empty);
Interlocked.Exchange(ref _showing, 0);
Usage.UsageTracker.Feature(answer ? "crashPromptYes" : "crashPromptNo");
Trace.WriteLine($"[Report] player answered {(answer ? "OK (full report)" : "Turn reports off (bare minimum)")}");
if (answer)
_ = Task.Run(() => ProblemReports.AnswerAsync());
else
ProblemReports.AskEnabled = false;
}));
});
});
}
public static void ShowSample(string kind, string error, string script)
{
string title = kind is "crash" or "engine" ? AskCrashed : AskProblem;
string why = CrashReasons.Explain(kind, error, script);
Dispatcher.UIThread.Post(() =>
{
if (MobileShellView.Instance is not null)
Platform.Modal.Show(close => Build(title, why, _ => close(string.Empty)));
});
}
private static Control Build(string title, string why, Action<bool> answer)
{
bool answered = false;
void Answer(bool full)
{
if (answered)
return;
answered = true;
answer(full);
}
TextBlock question = new()
{
Text = title,
FontSize = 16,
FontWeight = FontWeight.SemiBold,
TextWrapping = TextWrapping.Wrap,
TextAlignment = TextAlignment.Center,
HorizontalAlignment = HorizontalAlignment.Center,
};
Button yes = new() { Content = "OK, send report", MinWidth = 80, Classes = { "skua-button", "skua-primary" } };
yes.Click += (_, _) => Answer(true);
Button no = new() { Content = "Turn reports off", MinWidth = 80, Classes = { "skua-button" } };
no.Click += (_, _) => Answer(false);
StackPanel buttons = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
buttons.Children.Add(yes);
buttons.Children.Add(no);
TextBlock reason = new()
{
Text = why,
FontSize = 13,
Opacity = 0.85,
TextWrapping = TextWrapping.Wrap,
IsVisible = why.Length > 0,
};
ScrollViewer reasonScroll = new() { Content = reason, MaxHeight = 250, IsVisible = why.Length > 0 };
reasonScroll.Loaded += (_, _) => Dispatcher.UIThread.Post(reasonScroll.ScrollToHome, DispatcherPriority.Background);
Grid layout = new() { RowDefinitions = new RowDefinitions("Auto,Auto,Auto"), RowSpacing = 12, Margin = new Thickness(16), MaxWidth = 600 };
Grid.SetRow(question, 0);
Grid.SetRow(reasonScroll, 1);
Grid.SetRow(buttons, 2);
layout.Children.Add(question);
layout.Children.Add(reasonScroll);
layout.Children.Add(buttons);
Border card = new() { Child = layout };
card.Classes.Add("mobile-dialog");
return card;
}
}
