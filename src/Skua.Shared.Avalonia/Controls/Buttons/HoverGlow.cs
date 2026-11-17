using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
namespace Skua.Shared.Avalonia.Controls.Buttons;
public static class HoverGlow
{
private const double Radius = 56;
public static readonly AttachedProperty<bool> EnabledProperty =
AvaloniaProperty.RegisterAttached<Button, bool>("Enabled", typeof(HoverGlow));
public static readonly AttachedProperty<IBrush?> BaseProperty =
AvaloniaProperty.RegisterAttached<Button, IBrush?>("Base", typeof(HoverGlow));
public static readonly AttachedProperty<IBrush?> LightProperty =
AvaloniaProperty.RegisterAttached<Button, IBrush?>("Light", typeof(HoverGlow));
public static readonly AttachedProperty<double> AmountProperty =
AvaloniaProperty.RegisterAttached<Button, double>("Amount", typeof(HoverGlow), 0.22);
public static readonly AttachedProperty<IBrush?> EdgeBaseProperty =
AvaloniaProperty.RegisterAttached<Button, IBrush?>("EdgeBase", typeof(HoverGlow));
public static readonly AttachedProperty<IBrush?> EdgeLightProperty =
AvaloniaProperty.RegisterAttached<Button, IBrush?>("EdgeLight", typeof(HoverGlow));
public static readonly AttachedProperty<double> EdgeAmountProperty =
AvaloniaProperty.RegisterAttached<Button, double>("EdgeAmount", typeof(HoverGlow), 1.0);
public static readonly AttachedProperty<IBrush?> FillProperty =
AvaloniaProperty.RegisterAttached<Button, IBrush?>("Fill", typeof(HoverGlow));
public static readonly AttachedProperty<IBrush?> EdgeProperty =
AvaloniaProperty.RegisterAttached<Button, IBrush?>("Edge", typeof(HoverGlow));
static HoverGlow()
{
InputElement.PointerEnteredEvent.AddClassHandler<Button>(OnPointerEntered, handledEventsToo: true);
InputElement.PointerMovedEvent.AddClassHandler<Button>(OnPointerMoved, handledEventsToo: true);
foreach (AvaloniaProperty property in new AvaloniaProperty[]
{ BaseProperty, LightProperty, AmountProperty, EdgeBaseProperty, EdgeLightProperty, EdgeAmountProperty })
property.Changed.AddClassHandler<Button>((button, _) => UpdateColors(button));
}
public static bool GetEnabled(Button button) => button.GetValue(EnabledProperty);
public static void SetEnabled(Button button, bool value) => button.SetValue(EnabledProperty, value);
public static IBrush? GetBase(Button button) => button.GetValue(BaseProperty);
public static void SetBase(Button button, IBrush? value) => button.SetValue(BaseProperty, value);
public static IBrush? GetLight(Button button) => button.GetValue(LightProperty);
public static void SetLight(Button button, IBrush? value) => button.SetValue(LightProperty, value);
public static double GetAmount(Button button) => button.GetValue(AmountProperty);
public static void SetAmount(Button button, double value) => button.SetValue(AmountProperty, value);
public static IBrush? GetEdgeBase(Button button) => button.GetValue(EdgeBaseProperty);
public static void SetEdgeBase(Button button, IBrush? value) => button.SetValue(EdgeBaseProperty, value);
public static IBrush? GetEdgeLight(Button button) => button.GetValue(EdgeLightProperty);
public static void SetEdgeLight(Button button, IBrush? value) => button.SetValue(EdgeLightProperty, value);
public static double GetEdgeAmount(Button button) => button.GetValue(EdgeAmountProperty);
public static void SetEdgeAmount(Button button, double value) => button.SetValue(EdgeAmountProperty, value);
public static IBrush? GetFill(Button button) => button.GetValue(FillProperty);
public static IBrush? GetEdge(Button button) => button.GetValue(EdgeProperty);
private static void OnPointerEntered(Button button, PointerEventArgs e)
{
if (!GetEnabled(button))
return;
Ensure(button, FillProperty);
Ensure(button, EdgeProperty);
UpdateColors(button);
Move(button, e);
}
private static void UpdateColors(Button button)
{
if (button.GetValue(FillProperty) is not RadialGradientBrush fill
|| button.GetValue(EdgeProperty) is not RadialGradientBrush border)
return;
Color fillBase = ColorOf(button.GetValue(BaseProperty), Colors.Gray);
Color fillLight = ColorOf(button.GetValue(LightProperty), Colors.White);
Color edgeBase = ColorOf(button.GetValue(EdgeBaseProperty), Colors.Gray);
Color edgeLight = ColorOf(button.GetValue(EdgeLightProperty), Colors.White);
SetStops(fill, Mix(fillBase, fillLight, GetAmount(button)), fillBase);
SetStops(border, Mix(edgeBase, edgeLight, GetEdgeAmount(button)), edgeBase);
}
private static void OnPointerMoved(Button button, PointerEventArgs e)
{
if (!GetEnabled(button) || !button.IsPointerOver)
return;
UpdateColors(button);
Move(button, e);
}
private static void Move(Button button, PointerEventArgs e)
{
if (button.GetValue(FillProperty) is not RadialGradientBrush fill
|| button.GetValue(EdgeProperty) is not RadialGradientBrush border)
return;
Point p = e.GetPosition(button);
var centre = new RelativePoint(p.X, p.Y, RelativeUnit.Absolute);
fill.Center = fill.GradientOrigin = centre;
border.Center = border.GradientOrigin = centre;
}
private static void Ensure(Button button, AttachedProperty<IBrush?> property)
{
if (button.GetValue(property) is RadialGradientBrush)
return;
var brush = new RadialGradientBrush
{
RadiusX = new RelativeScalar(Radius, RelativeUnit.Absolute),
RadiusY = new RelativeScalar(Radius, RelativeUnit.Absolute),
GradientStops = { new GradientStop(Colors.Transparent, 0), new GradientStop(Colors.Transparent, 1) }
};
button.SetValue(property, brush);
}
private static void SetStops(RadialGradientBrush brush, Color centre, Color rim)
{
if (brush.GradientStops[0].Color != centre)
brush.GradientStops[0].Color = centre;
if (brush.GradientStops[1].Color != rim)
brush.GradientStops[1].Color = rim;
}
private static Color ColorOf(IBrush? brush, Color fallback)
=> brush is ISolidColorBrush solid ? solid.Color : fallback;
private static Color Mix(Color from, Color to, double amount)
{
amount = Math.Clamp(amount, 0, 1);
static byte Lerp(byte a, byte b, double t) => (byte)Math.Round(a + (b - a) * t);
return Color.FromArgb(Lerp(from.A, to.A, amount), Lerp(from.R, to.R, amount), Lerp(from.G, to.G, amount),
Lerp(from.B, to.B, amount));
}
}
