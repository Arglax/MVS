using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Skua.Core.Interfaces;
using Skua.Core.Models.Servers;
using Skua.Core.Scripts;
using Skua.Shared.Avalonia.ViewModels;
using Skua.Shared.Avalonia.ViewModels.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
namespace Skua.App.Avalonia.ViewModels.Options;
public class GameOptionsViewModel : BotControlViewModelBase
{
private static readonly string[] DefaultAQWServers =
{
"Alteon", "Artix", "Espada", "Galanoth", "Gravelyn", "Safiria",
"Sepulchure", "Sir Ver", "Swordhaven (EU)", "Twig", "Twilly", "Yokai (SEA)", "Yorumi"
};
private static readonly string[] VisualsOptions = { "LagKiller", "HidePlayers", "OptimizePlayers", "DisableFX", "DisableCollisions", "ShowFPS", "SetFPS" };
private static readonly string[] CombatOptions = { "AggroMonsters", "AggroAllMonsters", "AttackWithoutTarget", "InfiniteRange", "Magnetise", "HuntDelay", "HuntBuffer", "RestPackets", "SafeTimings" };
private static readonly string[] DropsOptions = { "AcceptACDrops", "AcceptAllDrops", "RejectAllDrops" };
private static readonly string[] ReloginOptions = { "AutoRelogin", "AutoReloginAny", "ReloginFallback", "RetryRelogin", "SafeRelogin", "ReloginTries", "LoginTimeout" };
private readonly IScriptServers _servers;
private readonly IScriptOption _options;
private readonly CBOChoiceOptionItemViewModel _reloginServerItem;
private readonly List<string> _reloginServerNames;
private bool _refreshingServers;
public GameOptionsViewModel(List<DisplayOptionItemViewModelBase> gameOptions, IScriptServers servers, IScriptOption options)
: base("Game Options", 800, 720)
{
_servers = servers;
_options = options;
string initialServer = NormalizeServerName(options.ReloginServer);
_selectedServer = initialServer;
if (!string.Equals(options.ReloginServer, initialServer, StringComparison.Ordinal))
options.ReloginServer = initialServer;
GameOptions = gameOptions;
ResetOptionsCommand = new RelayCommand(_options.Reset);
ResetDefaultOptionsCommand = new RelayCommand(_options.ResetToDefault);
SaveOptionsCommand = new RelayCommand(_options.Save);
_reloginServerNames = ServersList;
int initialIndex = _reloginServerNames.FindIndex(n => string.Equals(n, SelectedServer, StringComparison.OrdinalIgnoreCase));
_reloginServerItem = new CBOChoiceOptionItemViewModel("Relogin Server", "Server to log back in to", "ReloginServer",
_reloginServerNames, Math.Max(0, initialIndex));
_reloginServerItem.PropertyChanged += ReloginServerItem_PropertyChanged;
_servers.PropertyChanged += (_, e) =>
{
if (e.PropertyName is nameof(IScriptServers.CachedServers) or nameof(IScriptServers.ServerList))
Dispatcher.UIThread.Post(RefreshServersList);
};
if (_options is INotifyPropertyChanged optChanges)
{
optChanges.PropertyChanged += (_, e) =>
{
if (e.PropertyName == nameof(IScriptOption.ReloginServer) && !string.IsNullOrWhiteSpace(_options.ReloginServer))
{
string server = _options.ReloginServer!;
Dispatcher.UIThread.Post(() => SyncSelectedServer(server, markExplicit: false));
}
};
}
BuildGroups();
Sections = new List<OptionGroupViewModel> { OptionGroupViewModel.CreateAll(OptionGroups) }.Concat(OptionGroups).ToList();
_selectedSection = Sections[0];
if ((_servers.CachedServers is null || _servers.CachedServers.Count == 0)
&& (_servers.ServerList is null || _servers.ServerList.Count == 0))
{
_ = LoadLiveServersAsync();
}
}
protected override void OnActivated()
{
Messenger.Register<GameOptionsViewModel, PropertyChangedMessage<List<Server>>>(this, ServersChanged);
Messenger.Register<GameOptionsViewModel, PropertyChangedMessage<string>>(this, OptionServerChanged);
RefreshServersList();
if ((_servers.CachedServers is null || _servers.CachedServers.Count == 0)
&& (_servers.ServerList is null || _servers.ServerList.Count == 0))
{
_ = LoadLiveServersAsync();
}
}
private async Task LoadLiveServersAsync()
{
try
{
await _servers.GetServers(true);
Dispatcher.UIThread.Post(RefreshServersList);
}
catch { }
}
private void ReloginNow()
{
if (_servers.IsReloginInProgress)
return;
string target = NormalizeServerName(SelectedServer ?? _options.ReloginServer);
_ = Task.Run(async () =>
{
try
{
_servers.IsIntentionalLogout = false;
_servers.DisconnectForRelogin();
await _servers.EnsureRelogin(target, System.Threading.CancellationToken.None);
}
catch { }
});
}
public List<DisplayOptionItemViewModelBase> GameOptions { get; }
public List<OptionGroupViewModel> OptionGroups { get; private set; } = new();
public List<OptionGroupViewModel> Sections { get; }
private string _searchText = string.Empty;
private OptionGroupViewModel _selectedSection;
public string SearchText
{
get => _searchText;
set
{
if (SetProperty(ref _searchText, value))
OnPropertyChanged(nameof(FilteredGroups));
}
}
public OptionGroupViewModel SelectedSection
{
get => _selectedSection;
set
{
if (value is not null && SetProperty(ref _selectedSection, value))
{
OnPropertyChanged(nameof(FilteredGroups));
OnPropertyChanged(nameof(ShowSectionHeaders));
}
}
}
public bool ShowSectionHeaders => _selectedSection.IsAll;
public IEnumerable<OptionGroupViewModel> FilteredGroups =>
OptionGroupViewModel.Filter(_selectedSection.IsAll ? OptionGroups : new[] { _selectedSection }, _searchText);
private void BuildGroups()
{
string? BindingOf(DisplayOptionItemViewModelBase item) => (item as IBindingOptionItem)?.Binding;
List<DisplayOptionItemViewModelBase> InSection(string[] names) =>
names.Select(n => GameOptions.FirstOrDefault(i => BindingOf(i) == n)).OfType<DisplayOptionItemViewModelBase>().ToList();
List<DisplayOptionItemViewModelBase> visuals = InSection(VisualsOptions)
.Concat(GameOptions.Where(i => BindingOf(i) is null))
.ToList();
List<DisplayOptionItemViewModelBase> combat = InSection(CombatOptions);
List<DisplayOptionItemViewModelBase> drops = InSection(DropsOptions);
List<DisplayOptionItemViewModelBase> relogin = InSection(ReloginOptions);
relogin.Insert(0, _reloginServerItem);
relogin.Insert(1, new CommandOptionItemViewModel<IRelayCommand>(
"Relogin Now",
"Log in (or reconnect) to the selected Relogin Server now",
"4",
new RelayCommand(ReloginNow))
{
ActionText = "Relogin"
});
HashSet<DisplayOptionItemViewModelBase> used = new(visuals.Concat(combat).Concat(drops).Concat(relogin));
List<DisplayOptionItemViewModelBase> gameplay = GameOptions.Where(i => !used.Contains(i))
.OrderBy(i => i.Content, StringComparer.OrdinalIgnoreCase)
.ToList();
OptionGroups = new List<OptionGroupViewModel>
{
new() { Title = "Performance & Visuals", ShortTitle = "Visuals", Icon = "Speedometer", Items = visuals },
new() { Title = "Combat & Targeting", ShortTitle = "Combat", Icon = "SwordCross", Items = combat },
new() { Title = "Drops & Looting", ShortTitle = "Drops", Icon = "PackageVariantClosed", Items = drops },
new() { Title = "Auto-Relogin & Network", ShortTitle = "Relogin", Icon = "ServerNetwork", Items = relogin },
new() { Title = "Gameplay & Character", ShortTitle = "Gameplay", Icon = "AccountCogOutline", Items = gameplay }
};
}
public List<string> ServersList
{
get
{
HashSet<string> serverNames = new(StringComparer.OrdinalIgnoreCase);
if (_servers.CachedServers != null && _servers.CachedServers.Count > 0)
{
foreach (Server server in _servers.CachedServers)
{
if (!string.IsNullOrWhiteSpace(server.Name))
serverNames.Add(server.Name);
}
}
if (serverNames.Count == 0 && _servers.ServerList != null && _servers.ServerList.Count > 0)
{
foreach (Server server in _servers.ServerList)
{
if (!string.IsNullOrWhiteSpace(server.Name))
serverNames.Add(server.Name);
}
}
if (serverNames.Count == 0)
{
foreach (string name in DefaultAQWServers)
serverNames.Add(name);
}
string current = NormalizeServerName(_selectedServer ?? _options.ReloginServer);
if (!string.IsNullOrWhiteSpace(current)
&& !serverNames.Any(s => string.Equals(s, current, StringComparison.OrdinalIgnoreCase)
|| s.StartsWith(current + " (", StringComparison.OrdinalIgnoreCase)))
{
serverNames.Add(current);
}
return serverNames.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
}
}
private static string NormalizeServerName(string? name)
{
if (string.IsNullOrWhiteSpace(name))
return "Twilly";
string trimmed = name.Trim();
if (trimmed.Equals("Yokai", StringComparison.OrdinalIgnoreCase))
return "Yokai (SEA)";
if (trimmed.Equals("Swordhaven", StringComparison.OrdinalIgnoreCase))
return "Swordhaven (EU)";
if (trimmed.Equals("Galalanoth", StringComparison.OrdinalIgnoreCase))
return "Galanoth";
if (trimmed.Equals("Zorbot", StringComparison.OrdinalIgnoreCase))
return "Twilly";
return trimmed;
}
private string? _selectedServer;
public string? SelectedServer
{
get => _selectedServer ?? NormalizeServerName(_options.ReloginServer);
set => SyncSelectedServer(value, markExplicit: true);
}
private void SyncSelectedServer(string? value, bool markExplicit)
{
if (string.IsNullOrWhiteSpace(value))
return;
string normalized = NormalizeServerName(value);
SetProperty(ref _selectedServer, normalized, nameof(SelectedServer));
if (markExplicit)
ScriptOption.ReloginServerExplicitlyChosen = true;
if (!string.Equals(_options.ReloginServer, normalized, StringComparison.OrdinalIgnoreCase))
_options.ReloginServer = normalized;
if (markExplicit)
_options.Save();
int index = _reloginServerNames.FindIndex(n => string.Equals(n, normalized, StringComparison.OrdinalIgnoreCase));
if (index < 0 && !_refreshingServers)
{
RefreshServersList();
index = _reloginServerNames.FindIndex(n => string.Equals(n, normalized, StringComparison.OrdinalIgnoreCase));
}
if (index >= 0 && !Equals(_reloginServerItem.Value, index))
_reloginServerItem.Value = index;
}
private void RefreshServersList()
{
if (_refreshingServers)
return;
_refreshingServers = true;
try
{
string wanted = NormalizeServerName(SelectedServer);
List<string> fresh = ServersList;
bool same = fresh.Count == _reloginServerNames.Count
&& fresh.SequenceEqual(_reloginServerNames, StringComparer.Ordinal);
if (!same)
{
_reloginServerNames.Clear();
_reloginServerNames.AddRange(fresh);
for (int i = _reloginServerItem.Options.Count - 1; i >= 0; i--)
{
if (!fresh.Contains(_reloginServerItem.Options[i], StringComparer.Ordinal))
_reloginServerItem.Options.RemoveAt(i);
}
for (int i = 0; i < fresh.Count; i++)
{
if (i >= _reloginServerItem.Options.Count || !string.Equals(_reloginServerItem.Options[i], fresh[i], StringComparison.Ordinal))
{
int existing = _reloginServerItem.Options.IndexOf(fresh[i]);
if (existing >= 0)
_reloginServerItem.Options.Move(existing, i);
else
_reloginServerItem.Options.Insert(i, fresh[i]);
}
}
}
int idx = _reloginServerNames.FindIndex(n => string.Equals(n, wanted, StringComparison.OrdinalIgnoreCase));
if (idx < 0)
idx = _reloginServerNames.FindIndex(n => n.StartsWith(wanted + " (", StringComparison.OrdinalIgnoreCase));
if (idx >= 0)
{
_selectedServer = _reloginServerNames[idx];
_reloginServerItem.Value = -1;
_reloginServerItem.Value = idx;
}
OnPropertyChanged(nameof(ServersList));
}
finally
{
_refreshingServers = false;
}
}
public IRelayCommand ResetOptionsCommand { get; }
public IRelayCommand ResetDefaultOptionsCommand { get; }
public IRelayCommand SaveOptionsCommand { get; }
private void ReloginServerItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
{
if (_refreshingServers)
return;
if (e.PropertyName == nameof(DisplayOptionItemViewModelBase.Value)
&& _reloginServerItem.Value is int index && index >= 0 && index < _reloginServerNames.Count)
SyncSelectedServer(_reloginServerNames[index], markExplicit: true);
}
private void ServersChanged(GameOptionsViewModel recipient, PropertyChangedMessage<List<Server>> message)
{
if (message.PropertyName == nameof(IScriptServers.CachedServers))
recipient.RefreshServersList();
}
private void OptionServerChanged(GameOptionsViewModel recipient, PropertyChangedMessage<string> message)
{
if (message.PropertyName == nameof(IScriptOption.ReloginServer) && !string.IsNullOrWhiteSpace(message.NewValue))
recipient.SyncSelectedServer(message.NewValue, markExplicit: false);
}
}
