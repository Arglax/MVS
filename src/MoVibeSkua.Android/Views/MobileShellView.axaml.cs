using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MoVibeSkua.Android.Controls;
using MoVibeSkua.Android.ViewModels;
namespace MoVibeSkua.Android.Views;
public partial class MobileShellView : UserControl
{
private const double GameAspect = 958d / 550d;
private static readonly HashSet<Popup> _openPopups = new();
public static MobileShellView? Instance { get; private set; }
static MobileShellView()
{
Popup.IsOpenProperty.Changed.AddClassHandler<Popup>((popup, e) =>
{
if (e.GetNewValue<bool>())
_openPopups.Add(popup);
else
_openPopups.Remove(popup);
Instance?.UpdateCovered();
Dispatcher.UIThread.Post(() => Instance?.UpdateCovered(), DispatcherPriority.Background);
DispatcherTimer.RunOnce(() => Instance?.UpdateCovered(), TimeSpan.FromMilliseconds(150));
});
}
private ShellViewModel? _vm;
private readonly Action _rebuildGame;
private TopLevel? _topLevel;
private Thickness _safeArea;
private double _keyboardHeight;
private bool? _landscape;
public MobileShellView()
{
InitializeComponent();
Instance = this;
Root.SizeChanged += (_, _) =>
{
UpdateLayoutMode();
UpdateRailScale();
};
Body.SizeChanged += (_, _) => UpdateBodyLayout();
Body.LayoutUpdated += (_, _) => UpdateGameClip();
GameFrame.SizeChanged += (_, _) => FitGame();
Split.PropertyChanged += (_, e) =>
{
if (e.Property == SplitView.IsPaneOpenProperty || e.Property == SplitView.OpenPaneLengthProperty)
Dispatcher.UIThread.Post(UpdateGameClip, DispatcherPriority.Background);
};
_rebuildGame = () => Dispatcher.UIThread.Post(RebuildGameView);
GameViewRecovery.RebuildRequested += _rebuildGame;
PageGrip.PointerPressed += OnGripPressed;
PageGrip.PointerMoved += OnGripMoved;
PageGrip.PointerReleased += (_, e) => EndGripDrag(e.Pointer);
PageGrip.PointerCaptureLost += (_, e) => EndGripDrag(null);
}
protected override void OnDataContextChanged(EventArgs e)
{
base.OnDataContextChanged(e);
if (_vm is not null)
_vm.PropertyChanged -= OnVmPropertyChanged;
_vm = DataContext as ShellViewModel;
if (_vm is not null)
_vm.PropertyChanged += OnVmPropertyChanged;
ApplyRailSource();
ShowCurrentPageView();
UpdateBodyLayout();
StartPreparingPages();
}
protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
{
base.OnAttachedToVisualTree(e);
_topLevel = TopLevel.GetTopLevel(this);
if (_topLevel is null)
return;
_topLevel.BackRequested += OnBackRequested;
if (_topLevel.InsetsManager is { } insets)
{
insets.DisplayEdgeToEdgePreference = true;
insets.IsSystemBarVisible = false;
insets.SafeAreaChanged += OnSafeAreaChanged;
_safeArea = insets.SafeAreaPadding;
}
if (_topLevel.InputPane is { } pane)
pane.StateChanged += OnInputPaneChanged;
ApplyInsets();
}
protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
{
if (_topLevel is not null)
{
_topLevel.BackRequested -= OnBackRequested;
if (_topLevel.InsetsManager is { } insets)
insets.SafeAreaChanged -= OnSafeAreaChanged;
if (_topLevel.InputPane is { } pane)
pane.StateChanged -= OnInputPaneChanged;
}
_topLevel = null;
base.OnDetachedFromVisualTree(e);
}
private void OnSafeAreaChanged(object? sender, SafeAreaChangedArgs e)
{
_safeArea = e.SafeAreaPadding;
ApplyInsets();
UpdateRailScale();
}
private void OnInputPaneChanged(object? sender, InputPaneStateEventArgs e)
{
_keyboardHeight = e.NewState == InputPaneState.Open ? e.EndRect.Height : 0;
ApplyInsets();
WatchKeyboard();
}
private DispatcherTimer? _keyboardWatch;
private int _keyboardGone;
private void WatchKeyboard()
{
_keyboardGone = 0;
if (_keyboardHeight <= 0)
{
_keyboardWatch?.Stop();
return;
}
if (_keyboardWatch is null)
{
_keyboardWatch = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
_keyboardWatch.Tick += (_, _) =>
{
if (_keyboardHeight <= 0)
{
_keyboardWatch.Stop();
return;
}
_keyboardGone = KeyboardShowing() ? 0 : _keyboardGone + 1;
if (_keyboardGone < 2)
return;
_keyboardWatch.Stop();
_keyboardHeight = 0;
Trace.WriteLine("[Shell] keyboard height dropped: the keyboard is gone but no close event came");
ApplyInsets();
};
}
_keyboardWatch.Start();
}
private static bool KeyboardShowing()
{
try
{
global::Android.Views.View? view = MainActivity.Current?.Window?.DecorView;
AndroidX.Core.View.WindowInsetsCompat? insets = view is null ? null : AndroidX.Core.View.ViewCompat.GetRootWindowInsets(view);
return insets?.IsVisible(AndroidX.Core.View.WindowInsetsCompat.Type.Ime()) ?? true;
}
catch
{
return true;
}
}
private const double RailWidth = 68;
private const double BottomBarHeight = 60;
private double _railScale = 1;
private void UpdateRailScale()
{
double scale = 1;
if (_landscape != false && Root.Bounds.Height > 0)
{
double spare = Root.Bounds.Width - Root.Bounds.Height * GameAspect - _safeArea.Left - _safeArea.Right;
scale = Math.Clamp(Math.Floor(spare / (2 * RailWidth) * 50) / 50, 1, 1.3);
}
if (Math.Abs(scale - _railScale) < 0.01)
return;
_railScale = scale;
ApplyInsets();
}
private void ApplyInsets()
{
Thickness s = _safeArea;
if (_landscape != false)
{
Root.Margin = new Thickness(0, s.Top, 0, s.Bottom);
ApplyKeyboardLandscape();
LeftRailFrame.Width = RailWidth * _railScale + s.Left;
LeftRailFrame.Padding = new Thickness(2 + s.Left, 4, 2, 4);
RightRailFrame.Width = RailWidth * _railScale + s.Right;
LeftRailScale.LayoutTransform = RightRailScale.LayoutTransform =
_railScale > 1 ? new Avalonia.Media.ScaleTransform(_railScale, _railScale) : null;
RightRailFrame.Height = double.NaN;
RightRailFrame.Padding = new Thickness(2, 4, 2 + s.Right, 4);
TopBar.Padding = new Thickness(2, 2, 6, 2);
TopBar.MinHeight = 48;
SyncRailColumns();
}
else
{
GameFrame.Margin = default;
PageFrame.Margin = default;
double bottom = _keyboardHeight > 0 ? 0 : s.Bottom;
Root.Margin = new Thickness(s.Left, 0, s.Right, _keyboardHeight);
TopBar.Padding = new Thickness(2, 2 + s.Top, 6, 2);
TopBar.MinHeight = 48 + s.Top;
RightRailFrame.Width = double.NaN;
RightRailScale.LayoutTransform = null;
RightRailFrame.Height = BottomBarHeight + bottom;
RightRailFrame.Padding = new Thickness(4, 2, 4, 2 + bottom);
}
}
private void SyncRailColumns()
{
if (_landscape != true || Root.ColumnDefinitions.Count != 3)
return;
double left = LeftRailFrame.IsVisible ? LeftRailFrame.Width : 0;
double right = RightRailFrame.IsVisible ? RightRailFrame.Width : 0;
if (Root.ColumnDefinitions[0].Width.Value != left || !Root.ColumnDefinitions[0].Width.IsAbsolute)
Root.ColumnDefinitions[0].Width = new GridLength(left, GridUnitType.Pixel);
if (Root.ColumnDefinitions[2].Width.Value != right || !Root.ColumnDefinitions[2].Width.IsAbsolute)
Root.ColumnDefinitions[2].Width = new GridLength(right, GridUnitType.Pixel);
}
private void OnBackRequested(object? sender, RoutedEventArgs e)
{
e.Handled = true;
if (Platform.WebSheet.Open)
{
Platform.WebSheet.Close();
return;
}
if (DialogLayer.Children.Count > 0)
{
Control top = (Control)DialogLayer.Children[^1];
if (_dismiss.TryGetValue(top, out Action? dismiss))
dismiss();
return;
}
if (_vm?.HandleBack() == true)
return;
MainActivity.Current?.MoveTaskToBack(true);
}
private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
{
if (e.PropertyName == nameof(ShellViewModel.CurrentPage))
ShowCurrentPageView();
if (e.PropertyName is nameof(ShellViewModel.HasPage) or nameof(ShellViewModel.IsGameMaximized) or nameof(ShellViewModel.CurrentPage)
or nameof(ShellViewModel.IsPageWide))
UpdateBodyLayout();
if (e.PropertyName == nameof(ShellViewModel.RailsShown))
ApplySidebar();
}
private void ApplySidebar()
{
bool shown = _vm?.RailsShown ?? true;
bool landscape = _landscape == true;
LeftRailFrame.IsVisible = landscape && shown;
RightRailScroll.IsVisible = !landscape || shown;
GuideSlot.IsVisible = landscape && shown;
UpdateRailScale();
ApplyInsets();
UpdateBodyLayout();
}
private static readonly int MaxKeptPages = Platform.ArmyMemory.MaxKeptPages;
private readonly Dictionary<object, Control> _pageViews = new(ReferenceEqualityComparer.Instance);
private readonly List<object> _pageOrder = new();
private object? _shownContent;
private void ShowCurrentPageView()
{
ShellPage? page = _vm?.CurrentPage;
object? content = page?.Content;
if (ReferenceEquals(content, _shownContent))
return;
_shownContent = content;
Stopwatch sw = Stopwatch.StartNew();
Control? view = null;
bool built = false;
if (content is not null && !_pageViews.TryGetValue(content, out view))
{
built = true;
view = BuildPageView(content);
}
if (content is not null)
{
_pageOrder.Remove(content);
_pageOrder.Add(content);
DropOldPageViews();
}
foreach (Control child in PageHost.Children)
child.IsVisible = child == view;
if (_topLevel?.FocusManager is { } focus && focus.GetFocusedElement() is Control { IsEffectivelyVisible: false })
focus.ClearFocus();
if (_landscape != false)
ApplyKeyboardLandscape();
if (page is null)
return;
string key = page.Key;
Dispatcher.UIThread.Post(() => Trace.WriteLine($"[Page] {key} shown in {sw.ElapsedMilliseconds} ms ({(built ? "built" : "kept view")})"),
DispatcherPriority.Background);
}
private Control BuildPageView(object content)
{
Control view = content as Control ?? this.FindDataTemplate(content)?.Build(content) ?? new TextBlock { Text = content.GetType().Name };
if (view != content)
view.DataContext = content;
view.IsVisible = false;
_pageViews[content] = view;
PageHost.Children.Add(view);
return view;
}
private bool _prepareStarted;
private DispatcherTimer? _prepareTimer;
public bool HasDialogs => DialogLayer.Children.Count > 0;
public void Release()
{
GameViewRecovery.RebuildRequested -= _rebuildGame;
if (_vm is not null)
_vm.PropertyChanged -= OnVmPropertyChanged;
_vm = null;
_prepareTimer?.Stop();
_keyboardWatch?.Stop();
_uiDebugTimer?.Stop();
if (_topLevel is not null)
_topLevel.LayoutUpdated -= CountLayout;
_pageViews.Clear();
_pageOrder.Clear();
_shownContent = null;
_dismiss.Clear();
PageHost.Children.Clear();
_openPopups.Clear();
if (ReferenceEquals(Instance, this))
Instance = null;
}
private void StartPreparingPages()
{
if (_prepareStarted || _vm is null || !Platform.ArmyMemory.PrepareBotPages)
return;
_prepareStarted = true;
Queue<object>? queue = null;
DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(700) };
_prepareTimer = timer;
int waited = 0;
timer.Tick += (_, _) =>
{
if (++waited < 5)
return;
try
{
queue ??= new Queue<object>(_vm?.BotPagesToPrepare() ?? new List<object>());
while (queue.Count > 0 && _pageViews.ContainsKey(queue.Peek()))
queue.Dequeue();
if (queue.Count == 0)
{
timer.Stop();
Trace.WriteLine("[Page] bot pages prepared");
return;
}
object content = queue.Dequeue();
Stopwatch sw = Stopwatch.StartNew();
Control view = BuildPageView(content);
_pageOrder.Insert(0, content);
double w = PageHost.Bounds.Width > 100 ? PageHost.Bounds.Width : 900;
double h = PageHost.Bounds.Height > 100 ? PageHost.Bounds.Height : 1000;
view.IsVisible = true;
try
{
view.Measure(new Size(w, h));
view.Arrange(new Rect(0, 0, w, h));
using RenderTargetBitmap image = new(new PixelSize((int)w, (int)h));
image.Render(view);
}
finally
{
view.IsVisible = ReferenceEquals(content, _shownContent);
}
Trace.WriteLine($"[Page] prepared {content.GetType().Name} in {sw.ElapsedMilliseconds} ms");
}
catch (Exception ex)
{
Trace.WriteLine($"[Page] prepare failed: {ex.Message}");
}
};
timer.Start();
}
private void DropOldPageViews()
{
HashSet<object> open = new(_vm?.Pages.Select(p => p.Content) ?? Enumerable.Empty<object>(), ReferenceEqualityComparer.Instance);
for (int i = 0; i < _pageOrder.Count && _pageOrder.Count > MaxKeptPages; )
{
object old = _pageOrder[i];
if (open.Contains(old))
{
i++;
continue;
}
_pageOrder.RemoveAt(i);
if (_pageViews.Remove(old, out Control? oldView))
PageHost.Children.Remove(oldView);
}
}
public void PurgeTransientViews()
{
if (!Dispatcher.UIThread.CheckAccess())
{
Dispatcher.UIThread.Invoke(PurgeTransientViews, DispatcherPriority.Background);
return;
}
try
{
int purged = 0;
for (int i = _pageOrder.Count - 1; i >= 0; i--)
{
object item = _pageOrder[i];
if (ReferenceEquals(item, _shownContent))
continue;
_pageOrder.RemoveAt(i);
if (_pageViews.Remove(item, out Control? view))
{
PageHost.Children.Remove(view);
purged++;
}
}
Trace.WriteLine($"[PerfGuard] Purged {purged} cached page views under memory pressure");
}
catch (Exception ex)
{
Trace.WriteLine($"[PerfGuard] Error purging page views: {ex.Message}");
}
}
private void UpdateLayoutMode()
{
bool landscape = Root.Bounds.Width >= Root.Bounds.Height;
if (_landscape == landscape)
return;
_landscape = landscape;
Root.RowDefinitions.Clear();
Root.ColumnDefinitions.Clear();
TopBar.IsVisible = !landscape;
LeftRailFrame.IsVisible = landscape;
MaximizeButton.IsVisible = landscape;
GuideSlot.IsVisible = landscape;
ApplyRailSource();
RightRailFrame.Classes.Set("bottom", !landscape);
if (landscape)
{
Root.ColumnDefinitions.Add(new ColumnDefinition(RailWidth, GridUnitType.Pixel));
Root.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
Root.ColumnDefinitions.Add(new ColumnDefinition(RailWidth, GridUnitType.Pixel));
Place(LeftRailFrame, 0, 0);
Place(Split, 0, 1);
Place(RightRailFrame, 0, 2);
RightRailItems.ItemsPanel = new FuncTemplate<Panel?>(() => new UniformGrid { Columns = 1 });
RightRailScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
RightRailScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
Split.OpenPaneLength = 230;
}
else
{
Root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
Root.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
Root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
Place(TopBar, 0, 0);
Place(Split, 1, 0);
Place(RightRailFrame, 2, 0);
RightRailItems.ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Orientation = Orientation.Horizontal });
RightRailScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
RightRailScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
Split.OpenPaneLength = 250;
}
Place(DialogLayer, 0, 0);
Grid.SetRowSpan(DialogLayer, Math.Max(1, Root.RowDefinitions.Count));
Grid.SetColumnSpan(DialogLayer, Math.Max(1, Root.ColumnDefinitions.Count));
ApplySidebar();
}
private void ApplyRailSource()
{
if (_vm is not null)
RightRailItems.ItemsSource = _landscape == false ? _vm.AllRail : _vm.RightRail;
}
private static void Place(Control control, int row, int column)
{
Grid.SetRow(control, row);
Grid.SetColumn(control, column);
}
private void UpdateBodyLayout()
{
bool showPage = _vm is { HasPage: true, IsGameMaximized: false };
double width = Body.Bounds.Width;
double height = Body.Bounds.Height;
RowDefinition gameRow = Body.RowDefinitions[0];
RowDefinition pageRow = Body.RowDefinitions[1];
PageFrame.IsVisible = showPage;
PageHeader.IsVisible = _landscape == true;
PageGrip.IsVisible = _landscape == true;
PageFrame.Width = double.NaN;
PageFrame.HorizontalAlignment = HorizontalAlignment.Stretch;
gameRow.Height = new GridLength(1, GridUnitType.Star);
pageRow.Height = new GridLength(0);
if (!showPage || width <= 0 || height <= 0)
return;
if (width >= height)
{
double share = _vm!.CurrentPage is { } page ? _vm.GetPageWidth(page.Key) ?? 0.5 : 0.5;
PageFrame.Width = _vm.IsPageWide ? MaxPageWidth(width) : ClampPageWidth(width * share, width);
PageFrame.HorizontalAlignment = HorizontalAlignment.Right;
Place(PageFrame, 0, 0);
}
else
{
gameRow.Height = new GridLength(Math.Min(height * 0.42, width / GameAspect));
pageRow.Height = new GridLength(1, GridUnitType.Star);
Place(PageFrame, 1, 0);
}
}
private static double MaxPageWidth(double width) => Math.Max(0, width - Math.Max(80, width * 0.1));
private static double ClampPageWidth(double pageWidth, double width)
{
double max = MaxPageWidth(width);
double min = Math.Min(Math.Min(280, Math.Max(0, width * 0.5)), max);
return double.IsFinite(pageWidth) ? Math.Clamp(pageWidth, min, max) : min;
}
private double _gripStartX;
private double _gripStartWidth;
private bool _gripDragging;
private void OnGripPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
{
_gripDragging = true;
_gripStartX = e.GetPosition(Body).X;
_gripStartWidth = PageFrame.Bounds.Width;
e.Pointer.Capture(PageGrip);
e.Handled = true;
}
private void OnGripMoved(object? sender, Avalonia.Input.PointerEventArgs e)
{
if (!_gripDragging || Body.Bounds.Width <= 0)
return;
PageFrame.Width = ClampPageWidth(_gripStartWidth + _gripStartX - e.GetPosition(Body).X, Body.Bounds.Width);
e.Handled = true;
}
private void EndGripDrag(Avalonia.Input.IPointer? pointer)
{
if (!_gripDragging)
return;
_gripDragging = false;
pointer?.Capture(null);
if (_vm?.CurrentPage is { } page && Body.Bounds.Width > 0 && double.IsFinite(PageFrame.Width))
_vm.SetPageWidth(page.Key, PageFrame.Width / Body.Bounds.Width);
}
private void FitGame()
{
if (GameFrame.Child is not Control game)
return;
double width = GameFrame.Bounds.Width;
double height = GameFrame.Bounds.Height;
if (width <= 0 || height <= 0)
return;
if (width / height > GameAspect)
width = height * GameAspect;
else
height = width / GameAspect;
game.Width = Math.Floor(width);
game.Height = Math.Floor(height);
}
private int _layouts;
private DispatcherTimer? _uiDebugTimer;
public void SetUiDebug(bool on)
{
TopLevel? top = TopLevel.GetTopLevel(this);
if (top is null)
return;
top.RendererDiagnostics.DebugOverlays = on
? Avalonia.Rendering.RendererDebugOverlays.DirtyRects | Avalonia.Rendering.RendererDebugOverlays.Fps
: Avalonia.Rendering.RendererDebugOverlays.None;
top.LayoutUpdated -= CountLayout;
_uiDebugTimer?.Stop();
if (!on)
return;
top.LayoutUpdated += CountLayout;
_layouts = 0;
_uiDebugTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
_uiDebugTimer.Tick += (_, _) =>
{
Trace.WriteLine($"[UiDebug] {_layouts / 5.0:0.0} layout passes/s");
_layouts = 0;
};
_uiDebugTimer.Start();
}
private void CountLayout(object? sender, EventArgs e) => _layouts++;
public void SetUiDebugHidden(string name, bool hidden)
{
if (this.FindControl<Control>(name) is { } control)
{
control.IsVisible = !hidden;
Trace.WriteLine($"[UiDebug] {name} {(hidden ? "hidden" : "shown")}");
}
else if (name == "pages")
{
PageHost.Children.Clear();
_pageViews.Clear();
_pageOrder.Clear();
_shownContent = null;
Trace.WriteLine("[UiDebug] kept pages removed");
}
}
private void UpdateGameClip()
{
double fraction = 1;
double left = 0;
if (PageFrame.IsVisible && GameFrame.Child is Control game && game.Bounds.Width > 0
&& PageFrame.TranslatePoint(default, game) is { } pageOrigin
&& new Rect(pageOrigin, PageFrame.Bounds.Size).Intersects(new Rect(game.Bounds.Size)))
fraction = pageOrigin.X / game.Bounds.Width;
if (Split.IsPaneOpen && GameFrame.Child is Control g && g.Bounds.Width > 0
&& Split.TranslatePoint(new Point(Split.OpenPaneLength, 0), g) is { } paneEdge)
left = paneEdge.X / g.Bounds.Width;
_visibleFraction = fraction;
_visibleLeft = Math.Clamp(left, 0, 1);
GameView.SetVisibleRange(_visibleLeft, fraction);
}
private double _visibleFraction = 1;
private double _visibleLeft;
private void ApplyKeyboardLandscape()
{
double kb = _keyboardHeight;
object? focused = _topLevel?.FocusManager?.GetFocusedElement();
if (focused is Control { IsEffectivelyVisible: false })
kb = 0;
bool pageTyping = kb > 0 && focused is TextBox;
PageFrame.Margin = pageTyping ? new Thickness(0, 0, 0, kb) : default;
double shift = kb > 0 && !pageTyping && _vm?.IsLoggedIn == true ? kb : 0;
GameFrame.Margin = shift > 0 ? new Thickness(0, -shift, 0, shift) : default;
}
private void RebuildGameView()
{
GameFrame.Child = new GameView { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
FitGame();
}
public void UpdateCovered()
{
GameView.SetCovered(DialogLayer.Children.Count > 0 || _openPopups.Any(PopupCoversGame));
}
private bool PopupCoversGame(Popup popup)
{
if (popup.Child is not Control child || GameFrame.Child is not Control game || game.Bounds.Width <= 0)
return true;
if (child.Bounds.Width <= 0 || child.Bounds.Height <= 0)
return true;
if (child.TranslatePoint(default, game) is not { } origin)
return true;
Rect shown = new(game.Bounds.Width * _visibleLeft, 0, game.Bounds.Width * (_visibleFraction - _visibleLeft), game.Bounds.Height);
return shown.Intersects(new Rect(origin, child.Bounds.Size));
}
public void PushDialog(Control dialog)
{
DialogLayer.Children.Add(dialog);
DialogLayer.IsVisible = true;
UpdateCovered();
}
private readonly Dictionary<Control, Action> _dismiss = new();
public void SetDismiss(Control dialog, Action close) => _dismiss[dialog] = close;
public void RemoveDialog(Control dialog)
{
_dismiss.Remove(dialog);
DialogLayer.Children.Remove(dialog);
DialogLayer.IsVisible = DialogLayer.Children.Count > 0;
UpdateCovered();
}
}
