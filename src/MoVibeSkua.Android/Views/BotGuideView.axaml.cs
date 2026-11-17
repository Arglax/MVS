using Avalonia.Controls;
using MoVibeSkua.Android.ViewModels;
namespace MoVibeSkua.Android.Views;
public partial class BotGuideView : UserControl
{
public BotGuideView()
{
InitializeComponent();
DataContextChanged += (_, _) =>
{
if (DataContext is BotGuideViewModel vm && Content is null)
Content = GuideContent.Bot(vm);
};
}
}
