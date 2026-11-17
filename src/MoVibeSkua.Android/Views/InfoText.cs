using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;
namespace MoVibeSkua.Android.Views;
public class InfoHost : Grid
{
private Border? _layer;
private TextBlock? _title;
private TextBlock? _text;
public void ShowTip(string key)
{
if (!GuideContent.Tips.TryGetValue(key, out (string Title, string Text) tip))
return;
if (_layer is null)
{
_title = new TextBlock { Foreground = InfoText.Tip, FontSize = 16, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
_text = new TextBlock { Foreground = InfoText.Cream, FontSize = 14, LineHeight = 20, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
Button ok = new() { Content = "Got it", HorizontalAlignment = HorizontalAlignment.Stretch };
ok.Classes.Add("guide-action");
ok.Click += (_, _) => CloseTip();
Border card = new()
{
MaxWidth = 420,
Margin = new Thickness(24),
Padding = new Thickness(16, 14),
CornerRadius = new CornerRadius(12),
BorderBrush = InfoText.Tip,
BorderThickness = new Thickness(1),
VerticalAlignment = VerticalAlignment.Center,
Background = new LinearGradientBrush
{
StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
GradientStops = { new GradientStop(Color.Parse("#24190F"), 0), new GradientStop(Color.Parse("#150E08"), 1) }
},
Child = new StackPanel { Children = { _title, _text, ok } }
};
_layer = new Border { Background = Brush.Parse("#8C000000"), Child = card };
_layer.Tapped += (_, e) =>
{
if (ReferenceEquals(e.Source, _layer))
CloseTip();
};
SetRowSpan(_layer, 100);
SetColumnSpan(_layer, 100);
Children.Add(_layer);
}
_title!.Text = tip.Title;
_text!.Text = tip.Text;
_layer.IsVisible = true;
}
public void CloseTip()
{
if (_layer is not null)
_layer.IsVisible = false;
}
}
public class InfoText : TextBlock
{
internal static readonly IBrush Cream = Brush.Parse("#EAD9A8");
internal static readonly IBrush Gold = Brush.Parse("#F7C948");
internal static readonly IBrush Name = Brush.Parse("#FFD866");
internal static readonly IBrush Trim = Brush.Parse("#C9A447");
internal static readonly IBrush Green = Brush.Parse("#8BE28B");
internal static readonly IBrush Red = Brush.Parse("#FF7A6B");
internal static readonly IBrush Orange = Brush.Parse("#FFAE57");
internal static readonly IBrush Blue = Brush.Parse("#7FC8FF");
internal static readonly IBrush Tip = Brush.Parse("#7FD4FF");
private static readonly Regex Pattern = new(@"\*([^*]+)\*|\{([grob])\|([^}]+)\}|\[(\w+)\|([^\]]+)\]", RegexOptions.Compiled);
public static readonly StyledProperty<string?> MarkupProperty = AvaloniaProperty.Register<InfoText, string?>(nameof(Markup));
public static readonly StyledProperty<string?> LeadProperty = AvaloniaProperty.Register<InfoText, string?>(nameof(Lead));
private readonly List<(int Start, int End, string Key)> _words = new();
protected override Type StyleKeyOverride => typeof(TextBlock);
public InfoText()
{
TextWrapping = TextWrapping.Wrap;
Background = Brushes.Transparent;
Tapped += (_, e) =>
{
if (_words.Count == 0)
return;
Point p = e.GetPosition(this);
foreach (double dy in new[] { 0d, -7d, 7d })
{
TextHitTestResult hit = TextLayout.HitTestPoint(new Point(p.X, p.Y + dy));
if (!hit.IsInside)
continue;
foreach ((int start, int end, string key) in _words)
{
if (hit.TextPosition >= start && hit.TextPosition < end)
{
e.Handled = true;
this.FindAncestorOfType<InfoHost>()?.ShowTip(key);
return;
}
}
}
};
}
public string? Markup
{
get => GetValue(MarkupProperty);
set => SetValue(MarkupProperty, value);
}
public string? Lead
{
get => GetValue(LeadProperty);
set => SetValue(LeadProperty, value);
}
protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
{
base.OnPropertyChanged(change);
if (change.Property == MarkupProperty || change.Property == LeadProperty)
Fill();
}
private void Fill()
{
string markup = Markup ?? string.Empty;
InlineCollection inlines = new();
_words.Clear();
int length = 0;
void Add(Run run)
{
inlines.Add(run);
length += run.Text?.Length ?? 0;
}
if (!string.IsNullOrEmpty(Lead))
Add(new Run(Lead) { Foreground = Orange, FontWeight = FontWeight.Bold });
int at = 0;
foreach (Match m in Pattern.Matches(markup))
{
if (m.Index > at)
Add(new Run(markup[at..m.Index]));
if (m.Groups[1].Success)
{
Add(new Run(m.Groups[1].Value) { Foreground = Name, FontWeight = FontWeight.Bold });
}
else if (m.Groups[2].Success)
{
IBrush colour = m.Groups[2].Value switch { "g" => Green, "r" => Red, "o" => Orange, _ => Blue };
Add(new Run(m.Groups[3].Value) { Foreground = colour, FontWeight = FontWeight.Bold });
}
else
{
int start = length;
Add(new Run(m.Groups[5].Value) { Foreground = Tip, FontWeight = FontWeight.SemiBold, TextDecorations = Dotted() });
_words.Add((start, length, m.Groups[4].Value));
}
at = m.Index + m.Length;
}
if (at < markup.Length)
Add(new Run(markup[at..]));
Inlines = inlines;
}
private static TextDecorationCollection Dotted() => new()
{
new TextDecoration
{
Location = TextDecorationLocation.Underline,
Stroke = Tip,
StrokeThickness = 1.5,
StrokeThicknessUnit = TextDecorationUnit.Pixel,
StrokeDashArray = new AvaloniaList<double> { 1, 2 },
StrokeLineCap = PenLineCap.Round,
StrokeOffset = 2,
StrokeOffsetUnit = TextDecorationUnit.Pixel
}
};
}
