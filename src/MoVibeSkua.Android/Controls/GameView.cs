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
public static AWebView? WebView => Live();
private static AWebView? Live()
{
AWebView? view = _webView;
try
{
return view is null || view.Handle == IntPtr.Zero ? null : view;
}
catch (ObjectDisposedException)
{
return null;
}
}
private static void OnView(Action<AWebView> work)
{
try
{
AWebView? view = Live();
view?.Post(() =>
{
try
{
if (view.Handle != IntPtr.Zero)
work(view);
}
catch (ObjectDisposedException)
{
}
});
}
catch (ObjectDisposedException)
{
}
}
protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);
public static string? LoadReason { get; set; }
public static void Navigate(string url)
{
LoadReason ??= "game page requested";
_lastUrl = url;
if (Live() is null)
{
_pendingUrl = url;
return;
}
OnView(view => view.LoadUrl(url));
}
public static void SetCovered(bool covered)
{
OnView(view => view.Alpha = covered ? 0f : 1f);
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
OnView(ApplyClip);
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
try
{
AWebView? view = Live();
if (view is null || !view.IsShown || view.Alpha == 0f)
return false;
int[] location = new int[2];
view.GetLocationOnScreen(location);
return screenX >= location[0] + VisibleLeft(view) && screenX < location[0] + VisibleWidth(view)
&& screenY >= location[1] && screenY < location[1] + view.Height;
}
catch (ObjectDisposedException)
{
return false;
}
}
public static void ShowKeyboard()
{
Usage.UsageTracker.Track(Usage.Signal.GAME_KEYBOARD);
OnView(view =>
{
if (!view.HasFocus)
view.RequestFocus();
InputMethodManager? imm = view.Context?.GetSystemService(Context.InputMethodService) as InputMethodManager;
imm?.ShowSoftInput(view, 0);
});
}
public static void Resume()
{
OnView(view =>
{
view.OnResume();
view.ResumeTimers();
});
}
private static FrameLayoutHolder? _holder;
private sealed class FrameLayoutHolder : global::Android.Widget.FrameLayout
{
public FrameLayoutHolder(Context context) : base(context) { }
}
private static global::Android.Widget.FrameLayout Holder(Context context)
{
if (_holder is { Handle: not 0 })
return _holder;
_holder = new FrameLayoutHolder(global::Android.App.Application.Context);
return _holder;
}
protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
{
Context context = (parent as AndroidViewControlHandle)?.View.Context ?? global::Android.App.Application.Context;
global::Android.Widget.FrameLayout holder = Holder(context);
if (_webView is { Handle: 0 })
{
_webView = null;
_pendingUrl ??= _lastUrl;
}
if (_webView is null)
{
_webView = CreateWebView(context);
LoadReason ??= _lastUrl is null ? null : "new WebView (previous one gone)";
Trace.WriteLine("[GameView] game WebView created" + (_lastUrl is null ? " (first start)" : " (previous one gone; the game loads again)"));
if (_pendingUrl is not null)
{
_webView.LoadUrl(_pendingUrl);
_pendingUrl = null;
}
}
if (!ReferenceEquals(_webView.Parent, holder))
{
if (_webView.Parent is global::Android.Views.ViewGroup oldParent)
oldParent.RemoveView(_webView);
holder.AddView(_webView, new global::Android.Widget.FrameLayout.LayoutParams(-1, -1));
}
bool wasParked = _park is not null && ReferenceEquals(holder.Parent, _park);
if (holder.Parent is global::Android.Views.ViewGroup holderParent)
holderParent.RemoveView(holder);
holder.ClipBounds = null;
global::Android.Widget.FrameLayout wrapper = new(context);
wrapper.AddView(holder, new global::Android.Widget.FrameLayout.LayoutParams(-1, -1));
if (wasParked)
{
ApplyClip(_webView);
Trace.WriteLine("[GameView] game view unparked");
}
ParkOnDestroy = false;
return new AndroidViewControlHandle(wrapper);
}
protected override void DestroyNativeControlCore(IPlatformHandle control)
{
try
{
if (ParkOnDestroy && MainActivity.Current is { } activity)
Park(activity);
else if (_holder is { Handle: not 0 } holder && holder.Parent is global::Android.Views.ViewGroup parent && !ReferenceEquals(parent, _park))
parent.RemoveView(holder);
}
catch (Exception ex)
{
Trace.WriteLine($"[GameView] host release: {ex.Message}");
}
}
private static global::Android.Widget.FrameLayout? _park;
public static bool ParkOnDestroy { get; set; }
public static void Park(global::Android.App.Activity activity)
{
try
{
if (_holder is not { Handle: not 0 } holder || Live() is null)
return;
if (_park is not null && ReferenceEquals(holder.Parent, _park))
return;
if (_park is null || !ReferenceEquals(_park.Context, activity) || _park.Parent is null)
{
if (_park?.Parent is global::Android.Views.ViewGroup oldPark)
oldPark.RemoveView(_park);
_park = new global::Android.Widget.FrameLayout(activity) { ImportantForAccessibility = global::Android.Views.ImportantForAccessibility.NoHideDescendants };
activity.AddContentView(_park, new global::Android.Views.ViewGroup.LayoutParams(-1, -1));
}
int width = holder.Width > 0 ? holder.Width : -1;
int height = holder.Height > 0 ? holder.Height : -1;
int[] at = new int[2];
holder.GetLocationInWindow(at);
if (holder.Parent is global::Android.Views.ViewGroup parent)
parent.RemoveView(holder);
holder.ClipBounds = new Rect(0, 0, 0, 0);
_park.AddView(holder, new global::Android.Widget.FrameLayout.LayoutParams(width, height) { LeftMargin = at[0], TopMargin = at[1] });
ParkOnDestroy = false;
Trace.WriteLine($"[GameView] game view parked ({width}x{height})");
}
catch (Exception ex)
{
Trace.WriteLine($"[GameView] park failed: {ex.Message}");
}
}
public static bool IsParked => _park is not null && _holder is { Handle: not 0 } holder && ReferenceEquals(holder.Parent, _park);
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
public override void OnPageStarted(AWebView? view, string? url, global::Android.Graphics.Bitmap? favicon)
{
base.OnPageStarted(view, url, favicon);
Trace.WriteLine($"[GameView] game page load: {url} ({LoadReason ?? "first start or the page itself"})");
LoadReason = null;
}
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
LoadReason = "renderer process gone";
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
