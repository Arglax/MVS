using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MoVibeSkua.Android.Platform;
using Skua.Core.Interfaces;
using Skua.Core.Messaging;
using Skua.Core.Models;
using Skua.Core.Models.Servers;
namespace MoVibeSkua.Android.ViewModels;
public sealed partial class LoginViewModel : ObservableObject
{
private const string PrefsName = "movibeskua.login";
private const string ReloginTag = "[Relogin]";
private readonly IScriptInterface _bot;
private readonly ISharedPreferences? _prefs;
private CancellationTokenSource? _loginCts;
private readonly string _preferredServer;
public LoginViewModel(IScriptInterface bot)
{
_bot = bot;
_prefs = global::Android.App.Application.Context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
_username = _prefs?.GetString("user", string.Empty) ?? string.Empty;
_password = string.Empty;
_server = _prefs?.GetString("server", "Twilly") ?? "Twilly";
_preferredServer = _server;
_remember = _prefs?.GetBoolean("remember", true) ?? true;
LoadAccounts();
_ = LoadServers();
}
public ObservableCollection<string> Servers { get; } = new();
public ObservableCollection<SavedAccount> Accounts { get; } = new();
public bool HasAccounts => Accounts.Count > 0;
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(ShowForm), nameof(FormTitle))]
private bool _isEditing;
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(FormTitle), nameof(EditingAccount))]
private string? _editingName;
public SavedAccount? EditingAccount => EditingName is null ? null
: Accounts.FirstOrDefault(a => a.Username.Equals(EditingName, StringComparison.OrdinalIgnoreCase));
public bool ShowForm => IsEditing || !HasAccounts;
public string FormTitle => EditingName is null ? "Add account" : $"Edit {EditingName}";
[RelayCommand]
private void AddAccount()
{
Usage.UsageTracker.Feature("accountAdd");
EditingName = null;
Username = string.Empty;
Password = string.Empty;
Status = string.Empty;
IsEditing = true;
}
[RelayCommand]
private void CloseForm()
{
IsEditing = false;
EditingName = null;
Password = string.Empty;
Status = string.Empty;
}
[RelayCommand]
private async Task SaveAndLogin()
{
if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
{
Status = "Enter a username and password.";
return;
}
StoreAccount(Username.Trim(), Password, Server);
Usage.UsageTracker.Track(Usage.Signal.ACCOUNT_SAVED);
IsEditing = false;
EditingName = null;
await Login();
}
private void LoadAccounts()
{
List<SavedAccount> accounts = SavedAccounts.Load();
string? oldPass = _prefs?.GetString("pass", null);
if (!string.IsNullOrEmpty(oldPass) && !string.IsNullOrWhiteSpace(_username)
&& !accounts.Exists(a => a.Username.Equals(_username, StringComparison.OrdinalIgnoreCase)))
{
accounts.Add(new SavedAccount { Username = _username, Password = oldPass, Server = _server });
SavedAccounts.Save(accounts);
}
if (oldPass is not null)
_prefs?.Edit()?.Remove("pass")?.Apply();
Accounts.Clear();
foreach (SavedAccount a in accounts)
Accounts.Add(a);
OnPropertyChanged(nameof(HasAccounts));
}
[RelayCommand]
private void SaveAccount()
{
if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
{
Status = "Enter a username and password to save the account.";
return;
}
StoreAccount(Username.Trim(), Password, Server);
Usage.UsageTracker.Track(Usage.Signal.ACCOUNT_SAVED);
IsEditing = false;
EditingName = null;
Status = $"Saved {Username.Trim()} on this phone.";
}
private void StoreAccount(string user, string pass, string server)
{
SavedAccount? existing = Accounts.FirstOrDefault(a => a.Username.Equals(user, StringComparison.OrdinalIgnoreCase));
SavedAccount account = new() { Username = user, Password = pass, Server = string.IsNullOrWhiteSpace(server) ? "Twilly" : server };
if (existing is not null)
Accounts[Accounts.IndexOf(existing)] = account;
else
Accounts.Add(account);
SavedAccounts.Save(Accounts);
OnPropertyChanged(nameof(HasAccounts));
OnPropertyChanged(nameof(ShowForm));
}
[RelayCommand]
private async Task QuickLogin(SavedAccount? account)
{
if (account is null || IsBusy)
return;
Username = account.Username;
Password = account.Password;
Server = account.Server;
Usage.UsageTracker.Track(Usage.Signal.LOGIN_QUICK);
await Login();
}
[RelayCommand]
private void EditAccount(SavedAccount? account)
{
Usage.UsageTracker.Feature("accountEdit");
if (account is null)
return;
Username = account.Username;
Password = account.Password;
Server = account.Server;
EditingName = account.Username;
IsEditing = true;
Status = string.Empty;
}
[RelayCommand]
private void RemoveAccount(SavedAccount? account)
{
Usage.UsageTracker.Feature("accountRemove");
if (account is null)
return;
Accounts.Remove(account);
SavedAccounts.Save(Accounts);
OnPropertyChanged(nameof(HasAccounts));
OnPropertyChanged(nameof(ShowForm));
IsEditing = false;
EditingName = null;
Status = $"Removed {account.Username} from this phone.";
}
[ObservableProperty]
private string _username;
[ObservableProperty]
private string _password;
[ObservableProperty]
private string _server;
[ObservableProperty]
private bool _remember;
[ObservableProperty]
private string _status = string.Empty;
[ObservableProperty]
private bool _isBusy;
[ObservableProperty]
private bool _autoRelogin = AutoReloginSaved;
partial void OnAutoReloginChanged(bool value)
{
global::Android.App.Application.Context.GetSharedPreferences(PrefsName, FileCreationMode.Private)
?.Edit()?.PutBoolean("autoRelogin", value)?.Apply();
Usage.UsageTracker.Feature(value ? "autoReloginOn" : "autoReloginOff");
ApplyAutoRelogin(value);
}
public static bool AutoReloginSaved =>
global::Android.App.Application.Context.GetSharedPreferences(PrefsName, FileCreationMode.Private)?.GetBoolean("autoRelogin", false) ?? false;
public static void ApplyAutoRelogin(bool on)
{
Skua.Core.Scripts.ScriptOption.ReloginAllowed = on;
Skua.Core.Scripts.ScriptOption.KeepLoggedIn = on;
CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetRequiredService<IScriptOption>().AutoRelogin = on;
System.Diagnostics.Trace.WriteLine($"[Login] auto re-login is {(on ? "on" : "off")} (Accounts page switch)");
}
private async Task LoadServers()
{
try
{
List<Server> servers = await _bot.Servers.GetServers(true);
foreach (Server s in servers.Where(s => s.Online).OrderBy(s => s.Name))
Servers.Add(s.Name);
string? wanted = !string.IsNullOrWhiteSpace(Server) && Servers.Contains(Server) ? Server : _preferredServer;
Server = Servers.Contains(wanted) ? wanted
: Servers.Contains("Twilly") ? "Twilly"
: Servers.FirstOrDefault() ?? wanted;
}
catch (Exception ex)
{
System.Diagnostics.Trace.WriteLine($"[Login] servers: {ex}");
Status = "Couldn't load the server list. Check your connection.";
}
}
[RelayCommand]
private async Task Login()
{
Usage.UsageTracker.Feature("loginManual");
if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
{
Status = "Enter a username and password.";
return;
}
SaveCredentials();
using CancellationTokenSource cts = new();
_loginCts = cts;
IsBusy = true;
Status = $"Logging in to {Server}...";
WeakReferenceMessenger.Default.Register<LoginViewModel, AddLogMessage>(this, static (r, m) => r.OnLog(m));
try
{
string user = Username.Trim();
string pass = Password;
Diagnostics.AppLog.RememberLogin(user, pass);
string server = string.IsNullOrWhiteSpace(Server) ? "Twilly" : Server;
bool ok = await Task.Run(() =>
{
_bot.Servers.SetLoginInfo(user, pass);
_bot.Servers.IsIntentionalLogout = false;
return _bot.Servers.EnsureRelogin(server, cts.Token);
});
if (!ok && _bot.Servers.LastLoginError is not null)
{
Usage.UsageTracker.Track(Usage.Signal.LOGIN_REFUSED);
Usage.UsageTracker.Health("loginFails");
}
System.Diagnostics.Trace.WriteLine($"[Login] from the Accounts page: {(ok ? "logged in" : cts.IsCancellationRequested ? "cancelled" : _bot.Servers.LastLoginError is { } why ? "refused by AQW: " + why : "did not complete")} (server {server})");
Status = ok ? "Logged in."
: cts.IsCancellationRequested ? "Login cancelled."
: _bot.Servers.LastLoginError is { } refused ? $"AQW refused the login: {refused} Tap the eye to check what was typed."
: "Login did not complete. Check your details or try another server.";
}
catch (OperationCanceledException)
{
Status = "Login cancelled.";
}
catch (Exception ex)
{
System.Diagnostics.Trace.WriteLine($"[Login] failed: {ex}");
Status = "Login failed. Try again, or pick another server.";
}
finally
{
WeakReferenceMessenger.Default.Unregister<AddLogMessage>(this);
_loginCts = null;
IsBusy = false;
}
}
[RelayCommand]
private void CancelLogin()
{
Usage.UsageTracker.Feature("loginCancel");
_loginCts?.Cancel();
Status = "Cancelling...";
}
private void OnLog(AddLogMessage message)
{
int tag = message.Text.IndexOf(ReloginTag, StringComparison.Ordinal);
if (message.LogType != LogType.Script || tag < 0)
return;
string text = message.Text[(tag + ReloginTag.Length)..].Trim();
System.Diagnostics.Trace.WriteLine($"[Login] {text}");
Dispatcher.UIThread.Post(() =>
{
if (IsBusy)
Status = text;
});
}
[RelayCommand]
private void Logout()
{
Usage.UsageTracker.Feature("logout");
Task.Run(() =>
{
_bot.Servers.IsIntentionalLogout = true;
_bot.Servers.Logout();
});
Status = "Logged out.";
}
private void SaveCredentials()
{
if (_prefs is null)
return;
ISharedPreferencesEditor? editor = _prefs.Edit();
if (editor is null)
return;
editor.PutBoolean("remember", Remember);
editor.PutString("server", Server);
editor.PutString("user", Username);
editor.Apply();
if (Remember)
StoreAccount(Username.Trim(), Password, Server);
}
}
