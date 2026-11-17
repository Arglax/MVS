using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Skua.Core.Interfaces;
namespace MoVibeSkua.Android.ViewModels;
public sealed partial class GuideViewModel : ObservableObject
{
private readonly Action _switchRails;
private readonly Action _openSupport;
private readonly Action _openBotGuide;
public GuideViewModel(Action switchRails, Action openSupport, Action openBotGuide)
{
_switchRails = switchRails;
_openSupport = openSupport;
_openBotGuide = openBotGuide;
}
[RelayCommand]
private void SwitchRails() => _switchRails();
[RelayCommand]
private void OpenSupport() => _openSupport();
[RelayCommand]
private void OpenBotGuide() => _openBotGuide();
}
public sealed partial class BotGuideViewModel : ObservableObject
{
private readonly Action _showBotTools;
public BotGuideViewModel(Action showBotTools)
{
_showBotTools = showBotTools;
}
[RelayCommand]
private void ShowBotTools() => _showBotTools();
[RelayCommand]
private void OpenAuqwDiscord() => SupportViewModel.Open(SupportViewModel.AuqwDiscordUrl);
}
public sealed partial class SupportViewModel : ObservableObject
{
public const string BuyMeACoffeeUrl = "https://buymeacoffee.com/arglaxaqwv";
public const string KoFiUrl = "https://ko-fi.com/arglaxgaming";
public const string DiscordUrl = "https://discord.gg/Dwb7XMkpSh";
public const string AuqwDiscordUrl = "https://discord.gg/cZgJTvjGU2";
public const string SkuaDonationsUrl = "https://auqw.github.io/Skua-and-Grim-Donations/";
private readonly Action? _openGraphics;
private readonly Action? _openCheck;
public SupportViewModel(Action? openGraphics = null, Action? openCheck = null)
{
_openGraphics = openGraphics;
_openCheck = openCheck;
}
[RelayCommand]
private void OpenCheck() => _openCheck?.Invoke();
[RelayCommand]
private void OpenGraphics()
{
_openGraphics?.Invoke();
}
public bool SendUsage
{
get => Usage.UsageTracker.OptedIn;
set
{
Usage.UsageTracker.OptedIn = value;
OnPropertyChanged();
}
}
[CommunityToolkit.Mvvm.Input.RelayCommand]
private void EnterHeadless()
{
Usage.UsageTracker.Feature("supportHeadless");
global::MoVibeSkua.Android.Platform.Headless.Enter();
}
public const string InstaPayName = "";
public const string InstaPayNumber = "";
public bool HasInstaPay => InstaPayName.Length > 0 && InstaPayNumber.Length > 0;
public string InstaPayNameText => InstaPayName;
public string InstaPayNumberText => InstaPayNumber;
[RelayCommand]
private void OpenBuyMeACoffee()
{
Usage.UsageTracker.Feature("supportCoffee");
Open(BuyMeACoffeeUrl);
}
[RelayCommand]
private void OpenKoFi()
{
Usage.UsageTracker.Feature("supportKofi");
Open(KoFiUrl);
}
[RelayCommand]
private void OpenDiscord()
{
Usage.UsageTracker.Feature("supportDiscord");
Open(DiscordUrl);
}
[RelayCommand]
private void OpenAuqwDiscord()
{
Usage.UsageTracker.Feature("supportAuqwDiscord");
Open(AuqwDiscordUrl);
}
[RelayCommand]
private void OpenSkuaDonations()
{
Usage.UsageTracker.Feature("supportSkuaDonations");
Open(SkuaDonationsUrl);
}
internal static void Open(string url) => Ioc.Default.GetService<IProcessService>()?.OpenLink(url);
public bool SendReports
{
get => Diagnostics.ProblemReports.AskEnabled;
set
{
Diagnostics.ProblemReports.AskEnabled = value;
OnPropertyChanged();
}
}
[ObservableProperty]
private string _reportButtonText = "Report a problem";
[RelayCommand]
private void ReportProblem()
{
Usage.UsageTracker.Feature("supportReportProblem");
Diagnostics.ProblemReports.Save("problem", "mvs", null, "Reported by the player from the Support page", null, fatal: false, feature: "userReport");
ReportButtonText = "Thanks! Choose Yes or No to send it.";
}
public string VersionText => $"MVS {Diagnostics.AppLog.AppVersion()}" + (Platform.DevMode.IsOn ? " · developer mode" : "");
private int _versionTaps;
private System.DateTime _lastVersionTap;
[RelayCommand]
private void VersionTap()
{
System.DateTime now = System.DateTime.UtcNow;
_versionTaps = now - _lastVersionTap < System.TimeSpan.FromSeconds(2) ? _versionTaps + 1 : 1;
_lastVersionTap = now;
if (_versionTaps < 7)
return;
_versionTaps = 0;
bool on = Platform.DevMode.Toggle();
Usage.UsageTracker.Toggle("devMode", on);
OnPropertyChanged(nameof(VersionText));
Ioc.Default.GetService<IDialogService>()?.ShowMessageBox(
on ? "Developer mode is on. Restart MVS to see the developer tools." : "Developer mode is off. Restart MVS to hide the developer tools.",
"Developer mode");
}
}
