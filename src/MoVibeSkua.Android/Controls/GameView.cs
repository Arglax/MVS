using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AView = Android.Views.View;
using AWebView = Android.Webkit.WebView;
using Android.Content;
using Android.Graphics;
using Android.Views.InputMethods;
using Android.Webkit;
using Avalonia.Android;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Platform;
namespace MoVibeSkua.Android.Controls;
public class GameView : NativeControlHost
{
private static AWebView? _webView;
private static string? _pendingUrl;
private static string? _lastUrl;
private static double _visibleFraction = 1;
private static double _visibleLeft;
public static AWebView? WebView => _webView;
protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);
public static void Navigate(string url)
{
_lastUrl = url;
if (_webView is null)
{
_pendingUrl = url;
return;
}
_webView.Post(() => _webView.LoadUrl(url));
}
public static void SetCovered(bool covered)
{
AWebView? view = _webView;
view?.Post(() => view.Alpha = covered ? 0f : 1f);
}
public static void SetVisibleFraction(double fraction) => SetVisibleRange(_visibleLeft, fraction);
public static void SetVisibleRange(double left, double right)
{
right = Math.Clamp(right, 0, 1);
left = Math.Clamp(left, 0, right);
if (right == _visibleFraction && left == _visibleLeft)
return;
_visibleFraction = right;
_visibleLeft = left;
AWebView? view = _webView;
view?.Post(() => ApplyClip(view));
}
private static int VisibleWidth(AView view) =>
_visibleFraction >= 1 ? view.Width : (int)Math.Round(view.Width * _visibleFraction);
private static int VisibleLeft(AView view) =>
_visibleLeft <= 0 ? 0 : (int)Math.Round(view.Width * _visibleLeft);
private static void ApplyClip(AView view)
{
view.ClipBounds = _visibleFraction >= 1 && _visibleLeft <= 0
? null
: new Rect(VisibleLeft(view), 0, VisibleWidth(view), view.Height);
}
public static bool Contains(float screenX, float screenY)
{
AWebView? view = _webView;
if (view is null || !view.IsShown || view.Alpha == 0f)
return false;
int[] location = new int[2];
view.GetLocationOnScreen(location);
return screenX >= location[0] + VisibleLeft(view) && screenX < location[0] + VisibleWidth(view)
&& screenY >= location[1] && screenY < location[1] + view.Height;
}
public static void ShowKeyboard()
{
Usage.UsageTracker.Track(Usage.Signal.GAME_KEYBOARD);
AWebView? view = _webView;
view?.Post(() =>
{
if (!view.HasFocus)
view.RequestFocus();
InputMethodManager? imm = view.Context?.GetSystemService(Context.InputMethodService) as InputMethodManager;
imm?.ShowSoftInput(view, 0);
});
}
public static void Resume()
{
AWebView? view = _webView;
view?.Post(() =>
{
view.OnResume();
view.ResumeTimers();
});
}
protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
{
Context context = (parent as AndroidViewControlHandle)?.View.Context ?? global::Android.App.Application.Context;
if (_webView is { Handle: 0 })
{
_webView = null;
_pendingUrl ??= _lastUrl;
}
if (_webView is null)
{
_webView = CreateWebView(context);
if (_pendingUrl is not null)
{
_webView.LoadUrl(_pendingUrl);
_pendingUrl = null;
}
}
else if (_webView.Parent is global::Android.Views.ViewGroup oldParent)
{
oldParent.RemoveView(_webView);
}
return new AndroidViewControlHandle(_webView);
}
protected override void DestroyNativeControlCore(IPlatformHandle control)
{
if (_webView is { Handle: not 0, Parent: global::Android.Views.ViewGroup parent })
parent.RemoveView(_webView);
}
private static AWebView CreateWebView(Context context)
{
AWebView.SetWebContentsDebuggingEnabled(Platform.DevMode.IsOn);
AWebView view = new GameWebView(context);
view.SetBackgroundColor(Color.Black);
view.Focusable = true;
view.FocusableInTouchMode = true;
WebSettings s = view.Settings;
s.JavaScriptEnabled = true;
s.DomStorageEnabled = true;
s.DatabaseEnabled = true;
s.MediaPlaybackRequiresUserGesture = false;
s.MixedContentMode = MixedContentHandling.AlwaysAllow;
s.LoadWithOverviewMode = true;
s.UseWideViewPort = false;
s.SetSupportZoom(false);
s.BuiltInZoomControls = false;
s.DisplayZoomControls = false;
s.CacheMode = CacheModes.Default;
s.OffscreenPreRaster = false;
view.SetRendererPriorityPolicy(RendererPriority.Important, false);
view.SetWebViewClient(new GameWebViewClient());
view.SetWebChromeClient(new GameChromeClient());
return view;
}
private sealed class GameWebView : AWebView
{
public GameWebView(Context context) : base(context)
{
}
public GameWebView(IntPtr handle, global::Android.Runtime.JniHandleOwnership transfer) : base(handle, transfer)
{
}
protected override void OnWindowVisibilityChanged(global::Android.Views.ViewStates visibility)
=> base.OnWindowVisibilityChanged(global::Android.Views.ViewStates.Visible);
protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
{
base.OnSizeChanged(w, h, oldw, oldh);
ApplyClip(this);
}
public override bool DispatchTouchEvent(global::Android.Views.MotionEvent? e)
{
if (e?.ActionMasked == global::Android.Views.MotionEventActions.Down
&& (Alpha == 0f || e.GetX() >= VisibleWidth(this) || e.GetX() < VisibleLeft(this)))
return false;
return base.DispatchTouchEvent(e);
}
}
private sealed class GameWebViewClient : WebViewClient
{
public override bool ShouldOverrideUrlLoading(AWebView? view, IWebResourceRequest? request)
{
global::Android.Net.Uri? uri = request?.Url;
if (uri is null || IsGamePage(uri))
return false;
if (Platform.WebSheet.IsCharPage(uri))
{
Platform.WebSheet.Show(uri.ToString()!);
return true;
}
if (uri.Scheme != "http" && uri.Scheme != "https")
return true;
try
{
Intent intent = new(Intent.ActionView, uri);
intent.AddFlags(ActivityFlags.NewTask);
view?.Context?.StartActivity(intent);
}
catch (Exception ex)
{
Trace.WriteLine($"[GameView] open link failed: {ex.Message}");
}
return true;
}
private static bool IsGamePage(global::Android.Net.Uri uri) =>
uri.Host == "127.0.0.1" && Uri.TryCreate(_lastUrl, UriKind.Absolute, out Uri? page) && uri.Port == page.Port;
public override bool OnRenderProcessGone(AWebView? view, RenderProcessGoneDetail? detail)
{
Trace.WriteLine("[GameView] renderer process gone; reloading");
Usage.UsageTracker.Track(Usage.Signal.SYS_RENDERER_GONE);
Usage.UsageTracker.Health("engineReloads");
if (detail?.DidCrash() == true)
System.Threading.Tasks.Task.Run(() => Diagnostics.ProblemReports.SaveEngine("WebView renderer process crashed; game page reloaded"));
if (view is not null)
{
string? url = view.Url;
if (view.Parent is global::Android.Views.ViewGroup parent)
parent.RemoveView(view);
view.Destroy();
_webView = null;
_pendingUrl = url;
}
GameViewRecovery.RequestRebuild();
return true;
}
}
private sealed class GameChromeClient : WebChromeClient
{
public override bool OnConsoleMessage(ConsoleMessage? consoleMessage)
{
if (consoleMessage is not null && consoleMessage.InvokeMessageLevel() != ConsoleMessage.MessageLevel.Log)
Trace.WriteLine($"[WebView] {consoleMessage.InvokeMessageLevel()}: {consoleMessage.Message()}");
return true;
}
}
}
public static class GameViewRecovery
{
public static event Action? RebuildRequested;
public static void RequestRebuild() => RebuildRequested?.Invoke();
}
