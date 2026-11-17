using System;
using Android.Content;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using Activity = Android.App.Activity;
using AUri = Android.Net.Uri;
using AWebView = Android.Webkit.WebView;
using Trace = System.Diagnostics.Trace;
namespace MoVibeSkua.Android.Platform;
public static class WebSheet
{
private static FrameLayout? _sheet;
private static AWebView? _page;
public static bool Open => _sheet is not null;
public static bool IsCharPage(AUri? uri) =>
uri is not null && uri.Scheme == "https" && string.Equals(uri.Host, "account.aq.com", StringComparison.OrdinalIgnoreCase)
&& (uri.Path ?? string.Empty).StartsWith("/charpage", StringComparison.OrdinalIgnoreCase);
public static void Show(string url)
{
Activity? activity = MainActivity.Current;
if (activity is null)
return;
activity.RunOnUiThread(() =>
{
try
{
if (_page is not null)
{
_page.LoadUrl(url);
return;
}
Usage.UsageTracker.Feature("charPage");
FrameLayout root = new(activity) { Clickable = true };
root.SetBackgroundColor(global::Android.Graphics.Color.Rgb(18, 12, 8));
AWebView page = new(activity);
page.Settings.JavaScriptEnabled = true;
page.Settings.DomStorageEnabled = true;
page.Settings.MediaPlaybackRequiresUserGesture = false;
page.Settings.LoadWithOverviewMode = true;
page.Settings.UseWideViewPort = true;
page.SetWebViewClient(new SheetClient());
root.AddView(page, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
TextView close = new(activity) { Text = "✕", Gravity = GravityFlags.Center };
close.SetTextSize(global::Android.Util.ComplexUnitType.Sp, 22);
close.SetTextColor(global::Android.Graphics.Color.Rgb(232, 193, 90));
close.SetBackgroundColor(global::Android.Graphics.Color.Argb(204, 26, 18, 6));
close.Click += (_, _) => Close();
int size = (int)(52 * activity.Resources!.DisplayMetrics!.Density);
root.AddView(close, new FrameLayout.LayoutParams(size, size, GravityFlags.Top | GravityFlags.End));
activity.AddContentView(root, new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
_sheet = root;
_page = page;
page.LoadUrl(url);
}
catch (Exception ex)
{
Trace.WriteLine($"[WebSheet] open failed: {ex.Message}");
Close();
}
});
}
public static void Close()
{
Activity? activity = MainActivity.Current;
void Remove()
{
try
{
if (_sheet?.Parent is ViewGroup parent)
parent.RemoveView(_sheet);
_page?.Destroy();
}
catch (Exception ex)
{
Trace.WriteLine($"[WebSheet] close failed: {ex.Message}");
}
_sheet = null;
_page = null;
}
if (activity is null)
Remove();
else
activity.RunOnUiThread(Remove);
}
private const string PlayScript = "(function(){var n=0;var t=setInterval(function(){var p=document.querySelector('ruffle-embed,ruffle-object,ruffle-player');if(p){if(n===0)p.scrollIntoView({block:'center'});try{p.play&&p.play()}catch(e){}}if(++n>16)clearInterval(t)},400)})()";
private sealed class SheetClient : WebViewClient
{
public override void OnPageFinished(AWebView? view, string? url)
{
base.OnPageFinished(view, url);
view?.EvaluateJavascript(PlayScript, null);
}
public override bool ShouldOverrideUrlLoading(AWebView? view, IWebResourceRequest? request)
{
AUri? uri = request?.Url;
if (uri is null || (uri.Scheme == "https" && string.Equals(uri.Host, "account.aq.com", StringComparison.OrdinalIgnoreCase)))
return false;
try
{
Intent intent = new(Intent.ActionView, uri);
intent.AddFlags(ActivityFlags.NewTask);
view?.Context?.StartActivity(intent);
}
catch (Exception ex)
{
Trace.WriteLine($"[WebSheet] open link failed: {ex.Message}");
}
return true;
}
}
}
