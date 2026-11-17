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
public const string AskCrashed = "MVS has crashed. Send error report and crash log to the developer?";
public const string AskProblem = "MVS ran into a problem. Send error report and crash log to the developer?";
private static int _showing;
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
try
{
kinds = ProblemReports.WaitingKinds();
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
Platform.Modal.Show(close => Build(title, answer =>
{
close(string.Empty);
Interlocked.Exchange(ref _showing, 0);
Usage.UsageTracker.Feature(answer ? "crashPromptYes" : "crashPromptNo");
Trace.WriteLine($"[Report] player answered {(answer ? "Yes (full)" : "No (minimal)")}");
_ = Task.Run(() => ProblemReports.AnswerAsync(answer));
}));
});
});
}
private static Control Build(string title, Action<bool> answer)
{
bool answered = false;
void Answer(bool full)
{
if (answered)
return;
answered = true;
answer(full);
}
TextBlock question = new() { Text = title, FontSize = 16, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
Button yes = new() { Content = "Yes", MinWidth = 80, Classes = { "skua-button", "skua-primary" } };
yes.Click += (_, _) => Answer(true);
Button no = new() { Content = "No", MinWidth = 80, Classes = { "skua-button" } };
no.Click += (_, _) => Answer(false);
StackPanel buttons = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
buttons.Children.Add(yes);
buttons.Children.Add(no);
Grid layout = new() { RowDefinitions = new RowDefinitions("Auto,Auto"), RowSpacing = 16, Margin = new Thickness(16) };
Grid.SetRow(question, 0);
Grid.SetRow(buttons, 1);
layout.Children.Add(question);
layout.Children.Add(buttons);
Border card = new() { Child = layout };
card.Classes.Add("mobile-dialog");
return card;
}
}
