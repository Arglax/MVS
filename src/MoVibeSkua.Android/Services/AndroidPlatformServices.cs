using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AUri = Android.Net.Uri;
using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.OS;
using Android.Views;
using MoVibeSkua.Android.Controls;
using Skua.Core.Interfaces;
namespace MoVibeSkua.Android.Services;
public class AndroidClipboardService : IClipboardService
{
private readonly ConcurrentDictionary<string, object> _data = new();
private static ClipboardManager? Manager =>
global::Android.App.Application.Context.GetSystemService(Context.ClipboardService) as ClipboardManager;
public void SetText(string text)
{
RunOnMain(() =>
{
ClipboardManager? manager = Manager;
if (manager is not null)
manager.PrimaryClip = ClipData.NewPlainText("MVS", text);
});
}
public string GetText()
{
string result = string.Empty;
RunOnMain(() => result = Manager?.PrimaryClip?.GetItemAt(0)?.Text ?? string.Empty);
return result;
}
public void SetData(string format, object data) => _data[format] = data;
public object GetData(string format) => _data.TryGetValue(format, out object? value) ? value : string.Empty;
private static void RunOnMain(Action action)
{
if (Looper.MyLooper() == Looper.MainLooper)
{
action();
return;
}
using ManualResetEventSlim done = new();
new Handler(Looper.MainLooper!).Post(() =>
{
try { action(); } finally { done.Set(); }
});
done.Wait(TimeSpan.FromSeconds(2));
}
}
public class AndroidProcessService : IProcessService
{
public void OpenLink(string link)
{
try
{
Intent intent = new(Intent.ActionView, AUri.Parse(link));
intent.AddFlags(ActivityFlags.NewTask);
global::Android.App.Application.Context.StartActivity(intent);
}
catch (Exception ex)
{
System.Diagnostics.Trace.WriteLine($"Open link failed: {ex.Message}");
}
}
public void OpenVSC()
{
string? loaded = CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetService<IScriptManager>()?.LoadedScript;
if (!string.IsNullOrEmpty(loaded))
OpenVSC(loaded);
else
Toast("Load a script first (Scripts page).");
}
public void OpenVSC(string path)
{
Context context = global::Android.App.Application.Context;
if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
{
Toast("Load a script first, then tap Edit Script.");
return;
}
try
{
AUri uri = AndroidX.Core.Content.FileProvider.GetUriForFile(context, context.PackageName + ".files", new Java.IO.File(path))!;
Intent intent = new(Intent.ActionView);
intent.SetDataAndType(uri, "text/plain");
intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
List<ComponentName> editors = Handlers(context, intent);
HashSet<string> browsers = Handlers(context, new Intent(Intent.ActionView, AUri.Parse("https://example.com")), all: true)
.Select(c => c.PackageName).ToHashSet(StringComparer.Ordinal);
ComponentName[] excluded = editors.Where(c => browsers.Contains(c.PackageName)).ToArray();
System.Diagnostics.Trace.WriteLine($"[Editor] {editors.Count} apps can open text, {excluded.Length} of them browsers");
if (editors.Count > 0 && editors.Count == excluded.Length)
{
Toast("No text editor on this phone. Install one (a code or text editor from the Play Store) to edit scripts.");
return;
}
Intent chooser = Intent.CreateChooser(intent, "Edit " + System.IO.Path.GetFileName(path) + " with")!;
if (excluded.Length > 0)
chooser.PutExtra(Intent.ExtraExcludeComponents, excluded);
chooser.AddFlags(ActivityFlags.NewTask);
context.StartActivity(chooser);
}
catch (Exception ex)
{
System.Diagnostics.Trace.WriteLine($"Open editor failed: {ex.Message}");
Toast("No app can open the script. Install a text or code editor to edit scripts.");
}
}
private static List<ComponentName> Handlers(Context context, Intent intent, bool all = false)
{
List<ComponentName> found = new();
try
{
var matches = context.PackageManager?.QueryIntentActivities(intent, all ? global::Android.Content.PM.PackageInfoFlags.MatchAll : global::Android.Content.PM.PackageInfoFlags.MatchDefaultOnly);
if (matches is null)
return found;
foreach (global::Android.Content.PM.ResolveInfo match in matches)
{
if (match.ActivityInfo is { PackageName: { } package, Name: { } name })
found.Add(new ComponentName(package, name));
}
}
catch (Exception ex)
{
System.Diagnostics.Trace.WriteLine($"[Editor] app list: {ex.Message}");
}
return found;
}
private static void Toast(string message)
{
new Handler(Looper.MainLooper!).Post(() =>
global::Android.Widget.Toast.MakeText(global::Android.App.Application.Context, message, global::Android.Widget.ToastLength.Long)?.Show());
}
}
public class AndroidSoundService : ISoundService
{
public void Beep() => Beep(800, 200);
public void Beep(int frequency, int duration)
{
try
{
ToneGenerator tone = new(global::Android.Media.Stream.Notification, 80);
tone.StartTone(Tone.PropBeep, Math.Clamp(duration, 50, 2000));
Task.Delay(duration + 100).ContinueWith(_ => tone.Release());
}
catch
{
}
}
}
public class AndroidScreenshotService : IScreenshotService
{
public Task<byte[]> TakeScreenshotAsync()
{
TaskCompletionSource<byte[]> tcs = new();
global::Android.Webkit.WebView? view = GameView.WebView;
global::Android.App.Activity? activity = MainActivity.Current;
if (view is null || activity?.Window is null || view.Width == 0 || view.Height == 0)
{
tcs.SetResult(Array.Empty<byte>());
return tcs.Task;
}
view.Post(() =>
{
try
{
int[] location = new int[2];
view.GetLocationInWindow(location);
Bitmap bitmap = Bitmap.CreateBitmap(view.Width, view.Height, Bitmap.Config.Argb8888!)!;
Rect rect = new(location[0], location[1], location[0] + view.Width, location[1] + view.Height);
PixelCopy.Request(activity.Window, rect, bitmap, new CopyListener(bitmap, tcs), new Handler(Looper.MainLooper!));
}
catch (Exception)
{
tcs.TrySetResult(Array.Empty<byte>());
}
});
return tcs.Task;
}
private sealed class CopyListener : Java.Lang.Object, PixelCopy.IOnPixelCopyFinishedListener
{
private readonly Bitmap _bitmap;
private readonly TaskCompletionSource<byte[]> _tcs;
public CopyListener(Bitmap bitmap, TaskCompletionSource<byte[]> tcs)
{
_bitmap = bitmap;
_tcs = tcs;
}
public void OnPixelCopyFinished(int copyResult)
{
if (copyResult != (int)PixelCopyResult.Success)
{
_tcs.TrySetResult(Array.Empty<byte>());
return;
}
using MemoryStream ms = new();
_bitmap.Compress(Bitmap.CompressFormat.Png!, 100, ms);
_bitmap.Recycle();
_tcs.TrySetResult(ms.ToArray());
}
}
}
