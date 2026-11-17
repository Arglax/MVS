using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using MoVibeSkua.Android.ViewModels;
namespace MoVibeSkua.Android.Views;
public sealed class CheckPhoneView : UserControl
{
private static readonly IBrush[] Colour = { InfoText.Red, InfoText.Orange, InfoText.Green };
private static readonly string[] Word = { "Below minimum", "Meets minimum", "Recommended" };
private static readonly IBrush Muted = Brush.Parse("#C8B89A");
private static readonly IBrush Head = Brush.Parse("#C9A447");
private readonly StackPanel _stack = new() { Margin = new Thickness(10, 8, 10, 12) };
public CheckPhoneView()
{
InfoHost host = new();
host.Children.Add(new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Content = _stack });
Border page = new() { Child = host };
page.Classes.Add("aqw-page");
Content = new ThemeVariantScope { RequestedThemeVariant = ThemeVariant.Dark, Child = page };
_stack.Children.Add(Band("Check my phone", first: true));
_stack.Children.Add(Text("Reading this phone...", 14, InfoText.Cream));
DataContextChanged += async (_, _) =>
{
if (DataContext is not CheckPhoneViewModel vm)
return;
await vm.Load();
Dispatcher.UIThread.Post(() => Fill(vm));
};
}
private void Fill(CheckPhoneViewModel vm)
{
_stack.Children.Clear();
_stack.Children.Add(Band("Check my phone", first: true));
_stack.Children.Add(Summary(vm));
_stack.Children.Add(Band("This phone", first: false));
if (vm.Model.Length > 0)
_stack.Children.Add(Text(vm.Model, 13, Muted, new Thickness(0, 0, 0, 6)));
foreach (CheckRow row in vm.Rows)
_stack.Children.Add(Card(row));
_stack.Children.Add(Text("Nothing on this page is sent anywhere.", 12.5, Muted, new Thickness(0, 4, 0, 0)));
}
private static Control Summary(CheckPhoneViewModel vm)
{
int v = vm.Overall;
IBrush colour = v < 0 ? InfoText.Orange : Colour[v];
StackPanel box = new() { Spacing = 4 };
void Line(string markup) => box.Children.Add(new InfoText { Markup = markup, FontSize = 14, LineHeight = 20, Foreground = InfoText.Cream });
void Step(int n, string markup)
{
Grid step = new() { ColumnDefinitions = new ColumnDefinitions("22,*") };
step.Children.Add(new TextBlock { Text = n + ".", Foreground = InfoText.Gold, FontWeight = FontWeight.Bold, FontSize = 14, LineHeight = 20, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 6, 0) });
InfoText text = new() { Markup = markup, FontSize = 14, LineHeight = 20, Foreground = InfoText.Cream };
Grid.SetColumn(text, 1);
step.Children.Add(text);
box.Children.Add(step);
}
string title = v switch { < 0 => "Could not check this phone", 0 => "Below the minimum", 1 => "Meets the minimum", _ => "Recommended or better" };
box.Children.Add(new TextBlock { Text = title, Foreground = colour, FontSize = 16, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
switch (v)
{
case < 0:
Line("MVS could not read this phone's details.");
break;
case 0:
Line("MVS may run slowly or close by itself on this phone. To get the most out of it:");
Step(1, "Tap the *MVS crest* once, then *Graphics*.");
Step(2, "Pick *Battery*, and keep *Auto smooth* on.");
break;
case 1:
Line("MVS is playable on this phone. Busy maps may slow down: pick *Balanced* in *Graphics* and keep *Auto smooth* on.");
break;
default:
Line("MVS should run smoothly on this phone, even on the *Quality* preset.");
break;
}
if (vm.Tightest is { } tight)
Line($"Tightest spot: *{tight.Short}*, at {(tight.Verdict == 2 ? "{g|" : tight.Verdict == 1 ? "{o|" : "{r|")}{CheckPhoneViewModel.Percent(tight.Pct)}}} of [pct|recommended].");
return new Border
{
Margin = new Thickness(0, 0, 0, 4),
Padding = new Thickness(10, 8),
BorderBrush = colour,
BorderThickness = new Thickness(3, 0, 0, 0),
CornerRadius = new CornerRadius(0, 8, 8, 0),
Background = Brush.Parse("#1AC9A447"),
Child = box
};
}
private static Control Card(CheckRow row)
{
Grid top = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 6) };
InfoText name = new() { Markup = row.Name.StartsWith('[') ? row.Name : $"*{row.Name}*", FontSize = 14, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center };
top.Children.Add(name);
TextBlock verdict = new()
{
Text = row.Verdict < 0 ? "Not checked" : Word[row.Verdict],
Foreground = row.Verdict < 0 ? Muted : Colour[row.Verdict],
FontSize = 13,
FontWeight = FontWeight.Bold,
VerticalAlignment = VerticalAlignment.Center
};
Grid.SetColumn(verdict, 1);
top.Children.Add(verdict);
Grid cells = new() { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 8 };
cells.Children.Add(Cell(0, "THIS PHONE", row.Me, row.MeSmall, Brush.Parse("#FFF4C0")));
cells.Children.Add(Cell(1, "MINIMUM", row.Min, "", InfoText.Cream));
cells.Children.Add(Cell(2, "RECOMMENDED", row.Rec, row.RecSmall, InfoText.Cream));
StackPanel body = new() { Children = { top, cells } };
if (row.Verdict >= 0 && row.Pct > 0 && row.Pct < 10)
body.Children.Add(Text($"{CheckPhoneViewModel.Percent(row.Pct)} of recommended", 12.5, Colour[row.Verdict], new Thickness(0, 6, 0, 0)));
Border card = new() { Child = body };
card.Classes.Add("page-card");
return card;
}
private static Control Cell(int column, string head, string value, string small, IBrush brush)
{
StackPanel cell = new();
cell.Children.Add(new TextBlock { Text = head, Foreground = Head, FontSize = 10.5, LetterSpacing = 0.4 });
cell.Children.Add(new TextBlock { Text = value, Foreground = brush, FontSize = 13, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
if (small.Length > 0)
cell.Children.Add(new TextBlock { Text = small, Foreground = Muted, FontSize = 11.5, TextWrapping = TextWrapping.Wrap });
Grid.SetColumn(cell, column);
return cell;
}
private static Border Band(string title, bool first)
{
Border band = new() { Child = new TextBlock { Text = title.ToUpperInvariant() } };
band.Classes.Add("page-band");
if (first)
band.Margin = new Thickness(0, 2, 0, 8);
return band;
}
private static TextBlock Text(string text, double size, IBrush brush, Thickness margin = default) => new()
{
Text = text,
FontSize = size,
Foreground = brush,
TextWrapping = TextWrapping.Wrap,
Margin = margin
};
}
