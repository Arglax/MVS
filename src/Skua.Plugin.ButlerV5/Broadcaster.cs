using Skua.Core.Interfaces;
namespace ButlerV5;
public class Broadcaster
{
private readonly IScriptInterface _bot;
private readonly Func<bool> _enabled;
private readonly Func<bool> _questClone;
private readonly Func<bool> _questSync;
private readonly Action<string> _log;
private DateTime _lastQuestPublish = DateTime.MinValue;
private DateTime _lastQuestSyncPublish = DateTime.MinValue;
private DateTime _lastAttackingSeen = DateTime.MinValue;
private DateTime _lastTouch = DateTime.MinValue;
private volatile bool _publishing;
private readonly object _writeLock = new();
private long _nextPublication;
private long _publishedVersion;
private CancellationTokenSource? _cts;
private Task? _watcher;
private string _lastUsername = "";
private string _lastWritten = "";
private string _lastKnownRoom = "1";
private volatile string _lastServer = "";
private bool _lastOffGoto;
private string _lastClassName = "";
private DateTime _lastClassCheck = DateTime.MinValue;
public string CurrentServer => _lastServer;
private bool _wasLoggedIn;
private bool _wasEnabled = true;
private List<string> _followers = new();
private string _sendAllTarget = "";
private string _sendAllCommand = "";
private List<string> _sendAllRecipients = new();
private long _sendAllIssuedUtcTicks;
private bool _sendAllPending;
private readonly Func<string?> _followingProvider;
private readonly Func<string?> _scriptNameProvider;
private readonly Func<string?> _declinedProvider;
private readonly Func<bool> _parkingProvider;
public Broadcaster(IScriptInterface bot, Func<bool> enabled, Func<bool> questClone, Func<bool> questSync,
Func<string?> followingProvider, Func<string?> scriptNameProvider,
Func<string?> declinedProvider, Func<bool> parkingProvider, Action<string> log)
{
_bot = bot;
_enabled = enabled;
_questClone = questClone;
_questSync = questSync;
_followingProvider = followingProvider;
_scriptNameProvider = scriptNameProvider;
_declinedProvider = declinedProvider;
_parkingProvider = parkingProvider;
_log = log;
}
public IReadOnlyList<string> Followers
{
get { lock (_writeLock) return _followers.ToList(); }
}
public void SetFollowers(IEnumerable<string> followers)
{
lock (_writeLock)
_followers = followers.Select(f => f.Trim()).Where(f => f.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
WriteNow();
}
public bool PublishSendAllGoto(string target, IEnumerable<string> recipients)
{
string normalizedTarget = target.Trim();
List<string> normalizedRecipients = recipients
.Select(name => name.Trim())
.Where(name => name.Length > 0)
.Distinct(StringComparer.OrdinalIgnoreCase)
.ToList();
if (normalizedTarget.Length == 0 || normalizedRecipients.Count == 0)
return false;
lock (_writeLock)
{
_sendAllTarget = normalizedTarget;
_sendAllCommand = Guid.NewGuid().ToString("N");
_sendAllRecipients = normalizedRecipients;
_sendAllIssuedUtcTicks = 0;
_sendAllPending = true;
}
return WriteNow();
}
public void Start()
{
DebugLog.Log("Broadcaster", "Start: polling watcher + Logout event");
_cts = new CancellationTokenSource();
_bot.Events.Logout += OnLogout;
CancellationToken token = _cts.Token;
_watcher = Task.Run(() =>
{
while (!token.IsCancellationRequested)
{
try
{
bool enabled = _enabled();
if (enabled != _wasEnabled)
{
_wasEnabled = enabled;
DebugLog.Log("Broadcaster", $"ButlerV5 enabled -> {enabled}");
if (!enabled)
WriteOffline();
}
if (!enabled)
{
token.WaitHandle.WaitOne(250);
continue;
}
bool loggedIn = _bot.Player?.LoggedIn == true;
if (loggedIn != _wasLoggedIn)
{
_wasLoggedIn = loggedIn;
DebugLog.Log("Broadcaster", $"login state flipped -> {(loggedIn ? "logged in as " + _bot.Player?.Username : "logged out")}");
if (!loggedIn)
WriteOffline();
}
if (loggedIn)
{
WriteNow();
string publishUser = _bot.Player!.Username;
if (DateTime.UtcNow - _lastTouch >= SyncFile.TouchEvery)
{
_lastTouch = DateTime.UtcNow;
SyncFile.Touch(publishUser);
}
if (_questSync() &&
(DateTime.UtcNow - _lastQuestSyncPublish).TotalSeconds >= 2)
{
_lastQuestSyncPublish = DateTime.UtcNow;
QuestSync.Publish(_bot, publishUser);
}
if (_questClone() &&
!_publishing &&
_bot.Map?.Loaded == true &&
(DateTime.UtcNow - _lastQuestPublish).TotalSeconds >= 120)
{
_lastQuestPublish = DateTime.UtcNow;
_publishing = true;
Task.Run(() =>
{
try { QuestClone.Publish(_bot, publishUser); }
finally { _publishing = false; }
});
}
}
}
catch
{
}
token.WaitHandle.WaitOne(AppContext.GetData("mvs.lowPower") is true ? 1000 : 250);
}
}, token);
}
public void Stop()
{
_bot.Events.Logout -= OnLogout;
_cts?.Cancel();
try { _watcher?.Wait(2000); } catch { }
_cts?.Dispose();
_cts = null;
}
private void OnLogout()
{
DebugLog.Log("Broadcaster", "Logout event");
WriteOffline();
}
public bool WriteNow()
{
long version = Interlocked.Increment(ref _nextPublication);
try
{
if (!_enabled())
return false;
IScriptPlayer? player = _bot.Player;
if (player?.LoggedIn != true)
return false;
string username = player.Username;
if (string.IsNullOrEmpty(username))
return false;
IScriptMap? map = _bot.Map;
string mapName = map?.Name?.ToLowerInvariant() ?? "";
if (map?.Loaded != true || string.IsNullOrEmpty(mapName) || mapName == "-1")
return false;
bool rawAttacking = player.InCombat || player.HasTarget;
string server = GameServer.CurrentName(_bot);
bool? gotoEnabled = _bot.Flash.GetGameObject<bool?>("uoPref.bGoto");
bool checkClass;
lock (_writeLock)
{
checkClass = !username.Equals(_lastUsername, StringComparison.OrdinalIgnoreCase) ||
(DateTime.UtcNow - _lastClassCheck).TotalSeconds >= 2;
}
string? className = null;
if (checkClass)
{
try { className = player.CurrentClass?.Name; }
catch { }
}
string fullName = SafeGet(() => map?.FullName) ?? "";
string cell = player.Cell ?? "Enter";
string pad = player.Pad ?? "Spawn";
string following = SafeGet(_followingProvider) ?? "";
string? scriptName = SafeGet(_scriptNameProvider);
string declined = SafeGet(_declinedProvider) ?? "";
bool parking = SafeGetBool(_parkingProvider);
lock (_writeLock)
{
if (version < _publishedVersion)
return false;
if (rawAttacking)
_lastAttackingSeen = DateTime.UtcNow;
bool attacking = rawAttacking || (DateTime.UtcNow - _lastAttackingSeen).TotalMilliseconds < 4000;
_lastServer = server;
if (gotoEnabled.HasValue)
_lastOffGoto = !gotoEnabled.Value;
if (_sendAllPending)
_sendAllIssuedUtcTicks = DateTime.UtcNow.Ticks;
if (!username.Equals(_lastUsername, StringComparison.OrdinalIgnoreCase))
{
_lastClassName = "";
_lastClassCheck = DateTime.MinValue;
}
_lastUsername = username;
if (checkClass)
{
_lastClassCheck = DateTime.UtcNow;
_lastClassName = className ?? _lastClassName;
}
SyncData data = new()
{
Username = username,
Pid = Environment.ProcessId,
Server = server,
ClassName = _lastClassName,
Map = mapName,
Room = ResolveRoom(fullName),
Cell = cell,
Pad = pad,
LoggedIn = true,
Attacking = attacking,
OffGoto = _lastOffGoto,
Followers = _followers.ToList(),
Following = following,
ScriptOn = scriptName != null,
ScriptName = scriptName ?? "",
Declined = declined,
Parking = parking,
SendAllTarget = _sendAllTarget,
SendAllCommand = _sendAllCommand,
SendAllRecipients = _sendAllRecipients.ToList(),
SendAllIssuedUtcTicks = _sendAllIssuedUtcTicks,
};
bool written = WriteIfChanged(data);
if (written)
_publishedVersion = version;
if (written && _sendAllPending)
{
_sendAllPending = false;
DebugLog.Log("Broadcaster", $"published send-all goto to {_sendAllRecipients.Count} accounts: {_sendAllTarget}");
}
return written;
}
}
catch (Exception ex)
{
DebugLog.Log("Broadcaster", $"sync write exception: {ex.Message}");
return false;
}
}
public void WriteOffline()
{
try
{
lock (_writeLock)
{
_publishedVersion = Interlocked.Increment(ref _nextPublication);
if (string.IsNullOrEmpty(_lastUsername))
{
DebugLog.Log("Broadcaster", "offline write skipped: never logged in this session");
return;
}
SyncData data = new()
{
Username = _lastUsername,
Pid = Environment.ProcessId,
LoggedIn = false,
Followers = _followers.ToList(),
};
WriteIfChanged(data);
_lastClassName = "";
_lastClassCheck = DateTime.MinValue;
}
}
catch
{
}
}
private static string? SafeGet(Func<string?> provider)
{
try { return provider(); }
catch { return null; }
}
private static bool SafeGetBool(Func<bool> provider)
{
try { return provider(); }
catch { return false; }
}
private bool WriteIfChanged(SyncData data)
{
string serialized = data.Serialize();
if (serialized == _lastWritten)
return true;
if (SyncFile.Write(data))
{
_lastWritten = serialized;
DebugLog.Log("Broadcaster", $"wrote: {DebugLog.Describe(data)}");
return true;
}
DebugLog.Log("Broadcaster", $"WRITE FAILED: {DebugLog.Describe(data)}");
return false;
}
private string ResolveRoom(string fullName)
{
try
{
int dash = fullName.LastIndexOf('-');
if (dash > 0 && dash < fullName.Length - 1)
{
string room = fullName[(dash + 1)..];
if (room.All(char.IsDigit))
{
_lastKnownRoom = room;
return room;
}
}
}
catch
{
}
return _lastKnownRoom;
}
}
