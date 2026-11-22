using Skua.Core.Models.Servers;
using System.ComponentModel;
namespace Skua.Core.Interfaces;
public interface IScriptServers : INotifyPropertyChanged
{
void SetLoginInfo(string username, string password);
string LastIP { get; set; }
string? CachedUsername { get; }
bool HasCredentials => false;
string LastName { get; set; }
bool IsIntentionalLogout { get; set; }
bool IsReloginInProgress => false;
bool IsLogoutInProgress => false;
List<Server> ServerList { get; }
List<Server> CachedServers { get; }
ValueTask<List<Server>> GetServers(bool forceUpdate = false);
bool Connect(Server server)
{
return ConnectIP(server.IP);
}
bool Connect(string serverName)
{
Server? s = ServerList.Find(x => x.Name.Contains(serverName));
return s is not null && Connect(s.IP);
}
bool ConnectIP(string serverIp);
bool ConnectIP(string serverIp, int port);
bool IsConnected { get; }
string? LastLoginError { get; }
bool LastLoginWasFallback { get => false; set { } }
void Login(string username, string password);
void Login();
void Logout();
void DisconnectForRelogin()
{
Logout();
IsIntentionalLogout = false;
}
bool Reconnect(string serverName, int loginDelay = 2000);
bool Reconnect(Server server, int loginDelay = 2000);
bool Relogin(Server server = null!);
bool Relogin(string serverName);
bool ReloginIP(string ip);
bool EnsureRelogin(string serverName);
Task<bool> EnsureRelogin(CancellationToken token);
Task<bool> EnsureRelogin(string serverName, CancellationToken token);
}
