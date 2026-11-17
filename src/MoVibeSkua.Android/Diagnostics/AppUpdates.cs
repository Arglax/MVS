using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MoVibeSkua.Android.Views;
namespace MoVibeSkua.Android.Diagnostics;
public static class AppUpdates
{
public const string Url = "https://raw.githubusercontent.com/Arglax/MVS/main/app-version.txt";
private const string DefaultReleaseUrl = "https://github.com/Arglax/MVS/releases/latest";
private static readonly TimeSpan CheckEvery = TimeSpan.FromHours(6);
private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
private static string StatePath => Path.Combine(global::Android.App.Application.Context.FilesDir!.AbsolutePath, "updates.json");
private sealed class State
{
public long CheckedAt { get; set; }
public string? SeenNotice { get; set; }
}
public static void CheckWhenReady()
{
_ = Task.Run(async () =>
{
try
{
State state = LoadState();
long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
if (now - state.CheckedAt < CheckEvery.TotalMilliseconds)
return;
string text = await Http.GetStringAsync(Url);
state.CheckedAt = now;
SaveState(state);
Dictionary<string, string> info = Parse(text);
string current = CurrentVersion();
string? latest = Get(info, "version");
Trace.WriteLine($"[Updates] latest {latest ?? "?"}, this {current}");
for (int i = 0; i < 120 && (MobileShellView.Instance is null || CrashPrompt.IsShowing); i++)
await Task.Delay(1000);
if (MobileShellView.Instance is null)
return;
if (latest is not null && IsNewer(latest, current))
{
string link = Get(info, "apk_url") ?? Get(info, "release_url") ?? DefaultReleaseUrl;
await ShowAsync(close => BuildUpdate(latest, current, link, close));
}
string? noticeId = Get(info, "notice_id");
string? noticeText = Get(info, "notice_text");
if (noticeId is not null && noticeText is not null && noticeId != state.SeenNotice)
{
state.SeenNotice = noticeId;
SaveState(state);
await ShowAsync(close => BuildNotice(Get(info, "notice_title") ?? "Notice from Arglax",
noticeText.Replace("\\n", "\n"), close));
}
}
catch (Exception ex)
{
Trace.WriteLine($"[Updates] not checked: {ex.Message}");
}
});
}
internal static Dictionary<string, string> Parse(string text)
{
Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
foreach (string raw in text.Split('\n'))
{
string line = raw.Trim();
if (line.Length == 0 || line.StartsWith('#'))
continue;
int colon = line.IndexOf(':');
if (colon <= 0)
{
if (line.Contains('.') && !values.ContainsKey("version"))
values["version"] = line;
continue;
}
values[line[..colon].Trim()] = line[(colon + 1)..].Trim();
}
return values;
}
private static string? Get(Dictionary<string, string> info, string key) =>
info.TryGetValue(key, out string? value) && value.Length > 0 ? value : null;
private static string[] Parts(string version) => version.Trim().TrimStart('v', 'V').Split('.');
internal static bool IsNewer(string latest, string current)
{
string[] a = Parts(latest);
string[] b = Parts(current);
for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
{
int x = i < a.Length && int.TryParse(a[i], out int ax) ? ax : 0;
int y = i < b.Length && int.TryParse(b[i], out int by) ? by : 0;
if (x != y)
return x > y;
}
return false;
}
private static bool IsBugfix(string latest, string current)
{
string[] a = Parts(latest);
string[] b = Parts(current);
return a.Length > 1 && b.Length > 1 && a[0] == b[0] && a[1] == b[1];
}
private static string CurrentVersion()
{
global::Android.Content.Context context = global::Android.App.Application.Context;
return context.PackageManager?.GetPackageInfo(context.PackageName!, 0)?.VersionName ?? "0";
}
private static Task ShowAsync(Func<Action, Control> build)
{
TaskCompletionSource done = new(TaskCreationOptions.RunContinuationsAsynchronously);
RunNotifier.ShowWhenOnScreen(() =>
{
if (MobileShellView.Instance is null)
{
done.TrySetResult();
return;
}
Platform.Modal.Show(close => build(() =>
{
close(string.Empty);
done.TrySetResult();
}));
});
return done.Task;
}
private static Control BuildUpdate(string latest, string current, string link, Action close)
{
string shown = latest.Trim().TrimStart('v', 'V');
string kind = IsBugfix(latest, current) ? "A bugfix update is available." : "A new feature update is available.";
Button download = new() { Content = "Download", MinWidth = 80, Classes = { "skua-button", "skua-primary" } };
download.Click += (_, _) =>
{
close();
Usage.UsageTracker.Feature("updateDownload");
new Services.AndroidProcessService().OpenLink(link);
};
Button later = new() { Content = "Later", MinWidth = 80, Classes = { "skua-button" } };
later.Click += (_, _) => close();
Trace.WriteLine($"[Updates] offering v{shown}");
return Card($"Update available: v{shown}", new[]
{
Text(kind, false),
Text($"Current: v{current}\nLatest: v{shown}", true),
Text("Install the new APK over this one; your accounts and settings stay.", true),
}, download, later);
}
private static Control BuildNotice(string title, string text, Action close)
{
Button ok = new() { Content = "OK", MinWidth = 80, Classes = { "skua-button", "skua-primary" } };
ok.Click += (_, _) => close();
return Card(title, new[] { Text(text, false) }, ok);
}
private static TextBlock Text(string text, bool muted)
{
TextBlock block = new() { Text = text, FontSize = 13, TextWrapping = TextWrapping.Wrap };
if (muted && Application.Current?.TryFindResource("SkuaMutedTextBrush", out object? brush) == true && brush is IBrush mutedBrush)
block.Foreground = mutedBrush;
return block;
}
private static Control Card(string title, IEnumerable<Control> lines, params Button[] buttons)
{
StackPanel body = new() { Spacing = 8, Margin = new Thickness(16) };
body.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
foreach (Control line in lines)
body.Children.Add(line);
StackPanel row = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
foreach (Button button in buttons)
row.Children.Add(button);
body.Children.Add(row);
Border card = new() { Child = body };
card.Classes.Add("mobile-dialog");
return card;
}
private static State LoadState()
{
try
{
return File.Exists(StatePath) ? JsonSerializer.Deserialize<State>(File.ReadAllText(StatePath)) ?? new() : new();
}
catch
{
return new();
}
}
private static void SaveState(State state)
{
try
{
File.WriteAllText(StatePath, JsonSerializer.Serialize(state));
}
catch (Exception ex)
{
Trace.WriteLine($"[Updates] state not saved: {ex.Message}");
}
}
}
