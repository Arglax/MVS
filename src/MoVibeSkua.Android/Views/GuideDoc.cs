using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
namespace MoVibeSkua.Android.Views;
public sealed class GuideDoc : InfoHost
{
private static readonly IBrush Text = InfoText.Cream;
private static readonly IBrush Gold = InfoText.Gold;
private static readonly IBrush Trim = InfoText.Trim;
private static readonly IBrush NoteText = Brush.Parse("#D9C690");
private static readonly IBrush NoteBack = Brush.Parse("#1AC9A447");
private readonly ScrollViewer _scroll;
private readonly StackPanel _stack;
private readonly UniformGrid _jump;
public GuideDoc(string title)
{
Background = Rail();
_stack = new StackPanel { Margin = new Thickness(10, 8, 10, 12) };
_scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _stack };
Children.Add(_scroll);
_stack.Children.Add(PageTitle(title));
_stack.Children.Add(SectionTitle("QuickJump", first: true));
_jump = new UniformGrid { Columns = 3, Margin = new Thickness(-3, 0, -3, 4) };
_stack.Children.Add(_jump);
SizeChanged += (_, e) => _jump.Columns = Math.Clamp((int)((e.NewSize.Width - 14) / 116), 2, 6);
}
public GuideDoc Section(string jump, string title)
{
Border band = SectionTitle(title, first: false);
_stack.Children.Add(band);
Button b = new()
{
Content = new TextBlock { Text = jump, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, FontSize = 13, FontWeight = FontWeight.SemiBold },
Margin = new Thickness(3),
HorizontalAlignment = HorizontalAlignment.Stretch,
VerticalAlignment = VerticalAlignment.Stretch
};
b.Classes.Add("guide-jump");
b.Click += (_, _) =>
{
if (band.TranslatePoint(default, _stack) is { } at)
_scroll.Offset = new Vector(0, Math.Max(0, at.Y + _stack.Margin.Top - 4));
};
_jump.Children.Add(b);
return this;
}
public GuideDoc P(string markup)
{
_stack.Children.Add(Line(markup, 14, Text, new Thickness(0, 0, 0, 6)));
return this;
}
public GuideDoc Steps(params string[] items)
{
for (int i = 0; i < items.Length; i++)
_stack.Children.Add(Item($"{i + 1}.", Gold, FontWeight.Bold, items[i]));
return this;
}
public GuideDoc Bullets(params string[] items)
{
foreach (string item in items)
_stack.Children.Add(Item("•", Trim, FontWeight.Normal, item));
return this;
}
public GuideDoc Note(string markup)
{
_stack.Children.Add(new Border
{
Margin = new Thickness(0, 2, 0, 6),
Padding = new Thickness(8, 5),
BorderBrush = Trim,
BorderThickness = new Thickness(3, 0, 0, 0),
CornerRadius = new CornerRadius(0, 6, 6, 0),
Background = NoteBack,
Child = Line(markup, 13, NoteText, default, "Note: ")
});
return this;
}
public GuideDoc Buttons(bool primary, params (string Label, ICommand Command)[] buttons)
{
UniformGrid row = new() { Rows = 1, Margin = new Thickness(-3, 4, -3, 4) };
foreach ((string label, ICommand command) in buttons)
{
Button b = new() { Content = label, Command = command, Margin = new Thickness(3, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
b.Classes.Add(primary ? "guide-action" : "guide-jump");
row.Children.Add(b);
}
_stack.Children.Add(row);
return this;
}
private static Control Item(string marker, IBrush markerBrush, FontWeight weight, string markup)
{
Grid row = new() { ColumnDefinitions = new ColumnDefinitions("22,*"), Margin = new Thickness(0, 0, 0, 4) };
row.Children.Add(new TextBlock { Text = marker, Foreground = markerBrush, FontWeight = weight, FontSize = 14, LineHeight = 20, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 6, 0) });
TextBlock line = Line(markup, 14, Text, default);
Grid.SetColumn(line, 1);
row.Children.Add(line);
return row;
}
private static TextBlock Line(string markup, double size, IBrush brush, Thickness margin, string? lead = null) => new InfoText
{
FontSize = size,
LineHeight = Math.Round(size * 1.42),
Foreground = brush,
Margin = margin,
Lead = lead,
Markup = markup
};
private static Border SectionTitle(string title, bool first)
{
Border band = new() { Margin = new Thickness(0, first ? 2 : 18, 0, 8), Child = new TextBlock { Text = title.ToUpperInvariant() } };
band.Classes.Add("page-band");
return band;
}
private static Control PageTitle(string title)
{
static Border Rule(bool mirrored) => new()
{
Width = 46,
Height = 2,
Margin = new Thickness(12, 0),
VerticalAlignment = VerticalAlignment.Center,
Background = new LinearGradientBrush
{
StartPoint = new RelativePoint(mirrored ? 1 : 0, 0, RelativeUnit.Relative),
EndPoint = new RelativePoint(mirrored ? 0 : 1, 0, RelativeUnit.Relative),
GradientStops = { new GradientStop(Color.Parse("#00C9A447"), 0), new GradientStop(Color.Parse("#C9A447"), 1) }
}
};
TextBlock text = new()
{
Text = title.ToUpperInvariant(),
Foreground = Gold,
FontFamily = new FontFamily("serif"),
FontSize = 22,
FontWeight = FontWeight.ExtraBold,
LetterSpacing = 5,
VerticalAlignment = VerticalAlignment.Center
};
Border right = Rule(mirrored: true);
Grid.SetColumn(text, 1);
Grid.SetColumn(right, 2);
return new Grid
{
ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto"),
HorizontalAlignment = HorizontalAlignment.Center,
MinHeight = 42,
Margin = new Thickness(0, 2, 0, 10),
Children = { Rule(mirrored: false), text, right }
};
}
private static IBrush Rail() => new LinearGradientBrush
{
StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
GradientStops = { new GradientStop(Color.Parse("#1E150D"), 0), new GradientStop(Color.Parse("#0E0905"), 1) }
};
}
