using Avalonia.Controls;
using MoVibeSkua.Android.ViewModels;
namespace MoVibeSkua.Android.Views;
public partial class GuideView : UserControl
{
public GuideView()
{
InitializeComponent();
DataContextChanged += (_, _) =>
{
if (DataContext is GuideViewModel vm && Content is null)
Content = GuideContent.Game(vm);
};
}
}
