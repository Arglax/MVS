using CommunityToolkit.Mvvm.Messaging;
using Newtonsoft.Json.Linq;
using Skua.Core.Interfaces;
using Skua.Core.Interfaces.Services;
using Skua.Core.Messaging;
using System.Diagnostics;
namespace Skua.Core.Services;
public class ScriptWatchdogService : IScriptWatchdogService, IDisposable
{
private readonly Lazy<IScriptManager> _lazyManager;
private readonly Lazy<IScriptOption> _lazyOptions;
private readonly Lazy<IScriptPlayer> _lazyPlayer;
private readonly Lazy<IScriptCombat> _lazyCombat;
private readonly Lazy<IScriptMap> _lazyMap;
private readonly Lazy<IScriptServers> _lazyServers;
private readonly Lazy<IFlashUtil> _lazyFlash;
private readonly ILogService _logger;
private readonly IDiscordWebhookService _discord;
private readonly IDiagnosticsService _diagnostics;
private IScriptManager Manager => _lazyManager.Value;
private IScriptOption Options => _lazyOptions.Value;
private IScriptPlayer Player => _lazyPlayer.Value;
private IScriptCombat Combat => _lazyCombat.Value;
private IScriptMap Map => _lazyMap.Value;
private IScriptServers Servers => _lazyServers.Value;
private IFlashUtil Flash => _lazyFlash.Value;
private readonly object _stateLock = new();
private CancellationTokenSource? _timerCts;
private Task? _monitorTask;
private long _lastProgressTimestamp;
private volatile string? _myActorTag;
private volatile RescueEcho? _rescueEcho;
private int _currentTier;
private bool _isRunning;
private bool _disposed;
public bool IsRunning
{
get { lock (_stateLock) return _isRunning; }
private set { lock (_stateLock) _isRunning = value; }
}
public int SecondsSinceLastProgress
{
get
{
long last = Volatile.Read(ref _lastProgressTimestamp);
if (last == 0)
return 0;
return (int)((Stopwatch.GetTimestamp() - last) / (double)Stopwatch.Frequency);
}
}
public int CurrentTier => Volatile.Read(ref _currentTier);
public ScriptWatchdogService(
Lazy<IScriptManager> manager,
Lazy<IScriptOption> options,
Lazy<IScriptPlayer> player,
Lazy<IScriptCombat> combat,
Lazy<IScriptMap> map,
Lazy<IScriptServers> servers,
Lazy<IFlashUtil> flash,
ILogService logger,
IDiscordWebhookService discord,
IDiagnosticsService diagnostics)
{
_lazyManager = manager;
_lazyOptions = options;
_lazyPlayer = player;
_lazyCombat = combat;
_lazyMap = map;
_lazyServers = servers;
_lazyFlash = flash;
_logger = logger;
_discord = discord;
_diagnostics = diagnostics;
RegisterMessages();
}
private void RegisterMessages()
{
StrongReferenceMessenger.Default.Register<ScriptWatchdogService, ScriptStartedMessage, int>(this, (int)MessageChannels.ScriptStatus, (r, _) => r.OnScriptStarted());
StrongReferenceMessenger.Default.Register<ScriptWatchdogService, ScriptStoppedMessage, int>(this, (int)MessageChannels.ScriptStatus, (r, _) => r.OnScriptStopped());
StrongReferenceMessenger.Default.Register<ScriptWatchdogService, MonsterKilledMessage, int>(this, (int)MessageChannels.GameEvents, (r, _) => r.Heartbeat());
StrongReferenceMessenger.Default.Register<ScriptWatchdogService, QuestTurninMessage, int>(this, (int)MessageChannels.GameEvents, (r, _) => r.Heartbeat());
StrongReferenceMessenger.Default.Register<ScriptWatchdogService, ItemDroppedMessage, int>(this, (int)MessageChannels.GameEvents, (r, _) => r.Heartbeat());
StrongReferenceMessenger.Default.Register<ScriptWatchdogService, TryBuyItemMessage, int>(this, (int)MessageChannels.GameEvents, (r, _) => r.Heartbeat());
StrongReferenceMessenger.Default.Register<ScriptWatchdogService, ItemBoughtMessage, int>(this, (int)MessageChannels.GameEvents, (r, _) => r.Heartbeat());
StrongReferenceMessenger.Default.Register<ScriptWatchdogService, ItemSoldMessage, int>(this, (int)MessageChannels.GameEvents, (r, _) => r.Heartbeat());
StrongReferenceMessenger.Default.Register<ScriptWatchdogService, ItemAddedToBankMessage, int>(this, (int)MessageChannels.GameEvents, (r, _) => r.Heartbeat());
StrongReferenceMessenger.Default.Register<ScriptWatchdogService, BankLoadedMessage, int>(this, (int)MessageChannels.GameEvents, (r, _) => r.Heartbeat());
StrongReferenceMessenger.Default.Register<ScriptWatchdogService, CellChangedMessage, int>(this, (int)MessageChannels.GameEvents, (r, m) => r.OnCellChanged(m.Map, m.Cell));
StrongReferenceMessenger.Default.Register<ScriptWatchdogService, MapChangedMessage, int>(this, (int)MessageChannels.GameEvents, (r, _) => r.Heartbeat());
StrongReferenceMessenger.Default.Register<ScriptWatchdogService, ExtensionPacketMessage, int>(this, (int)MessageChannels.GameEvents, (r, m) => r.OnExtensionPacket(m));
}
public void Heartbeat()
{
Volatile.Write(ref _lastProgressTimestamp, Stopwatch.GetTimestamp());
Volatile.Write(ref _currentTier, 0);
}
public void Reset()
{
Volatile.Write(ref _lastProgressTimestamp, Stopwatch.GetTimestamp());
Volatile.Write(ref _currentTier, 0);
}
private void OnCellChanged(string? map, string? cell)
{
RescueEcho? echo = _rescueEcho;
if (echo is not null && Stopwatch.GetTimestamp() < echo.Until
&& string.Equals(map, echo.Map, StringComparison.OrdinalIgnoreCase)
&& echo.Cells.Contains(cell, StringComparer.OrdinalIgnoreCase))
return;
Heartbeat();
}
private void ExpectRescueEcho(int seconds, params string[] cells)
{
_rescueEcho = new RescueEcho(Map.Name, cells, Stopwatch.GetTimestamp() + seconds * Stopwatch.Frequency);
}
private sealed record RescueEcho(string Map, string[] Cells, long Until);
private bool TryEnterTier(int tier, long lastProgress)
{
Volatile.Write(ref _currentTier, tier);
if (Volatile.Read(ref _lastProgressTimestamp) == lastProgress)
return true;
Interlocked.CompareExchange(ref _currentTier, 0, tier);
return false;
}
private void OnExtensionPacket(ExtensionPacketMessage message)
{
string? me = _myActorTag;
if (me is null)
return;
try
{
if (message.Packet is not JObject packet
|| packet["params"] is not JObject parameters
|| (string?)parameters["type"] != "json"
|| parameters["dataObj"] is not JObject data)
return;
bool hit = (string?)data["cmd"] switch
{
"ct" => data["sarsa"] is JArray results && results.Any(r => r is JObject result && IsOwnHitOnMonster(result, me)),
"sars" => IsOwnHitOnMonster(data, me),
_ => false
};
if (hit)
Heartbeat();
}
catch (Exception ex)
{
Trace.WriteLine($"[Watchdog] Could not read combat packet: {ex.Message}");
}
}
private static bool IsOwnHitOnMonster(JObject result, string me)
{
if ((string?)result["cInf"] != me || (int?)result["iRes"] != 1 || result["a"] is not JArray targets)
return false;
foreach (JToken target in targets)
{
if (target is JObject t && ((string?)t["tInf"])?.StartsWith("m:", StringComparison.Ordinal) == true && (double?)t["hp"] > 0)
return true;
}
return false;
}
private void RefreshActorTag()
{
int id = Player.ID;
_myActorTag = id > 0 ? $"p:{id}" : null;
}
private void OnScriptStarted()
{
if (!Options.EnableScriptWatchdog)
return;
Start();
}
private void OnScriptStopped()
{
Stop();
}
public void Start()
{
lock (_stateLock)
{
if (_isRunning || _disposed)
return;
_isRunning = true;
_currentTier = 0;
_rescueEcho = null;
_lastProgressTimestamp = Stopwatch.GetTimestamp();
_timerCts?.Cancel();
_timerCts?.Dispose();
_timerCts = new CancellationTokenSource();
_monitorTask = Task.Run(() => MonitorLoopAsync(_timerCts.Token));
}
}
public void Stop()
{
lock (_stateLock)
{
if (!_isRunning)
return;
_isRunning = false;
_currentTier = 0;
_myActorTag = null;
_timerCts?.Cancel();
_timerCts?.Dispose();
_timerCts = null;
}
}
private async Task MonitorLoopAsync(CancellationToken token)
{
using PeriodicTimer timer = new(TimeSpan.FromSeconds(5));
while (!token.IsCancellationRequested)
{
try
{
if (!await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
break;
CheckWatchdogState();
}
catch (OperationCanceledException)
{
break;
}
catch (Exception ex)
{
Trace.WriteLine($"[Watchdog] Error in monitor loop: {ex}");
}
}
}
private void CheckWatchdogState()
{
if (!Options.EnableScriptWatchdog || !Manager.ScriptRunning)
return;
if (Manager.ScriptPaused || Servers.IsReloginInProgress || !Player.LoggedIn || Manager.ShouldExit)
{
Volatile.Write(ref _lastProgressTimestamp, Stopwatch.GetTimestamp());
return;
}
RefreshActorTag();
long lastProgress = Volatile.Read(ref _lastProgressTimestamp);
if (lastProgress == 0)
{
Volatile.Write(ref _lastProgressTimestamp, Stopwatch.GetTimestamp());
return;
}
double elapsedSeconds = (Stopwatch.GetTimestamp() - lastProgress) / (double)Stopwatch.Frequency;
int baseTimeout = Math.Max(30, Options.ScriptWatchdogTimeout);
int tier2Threshold = baseTimeout + 60;
int tier3Threshold = baseTimeout + 120;
if (elapsedSeconds >= baseTimeout && _currentTier < 1)
{
ExecuteTier1Rescue(elapsedSeconds, baseTimeout, lastProgress);
}
else if (elapsedSeconds >= tier2Threshold && _currentTier < 2)
{
ExecuteTier2Rescue(elapsedSeconds, lastProgress);
}
else if (elapsedSeconds >= tier3Threshold && _currentTier < 3)
{
ExecuteTier3Recovery(elapsedSeconds, lastProgress);
}
}
private void ExecuteTier1Rescue(double elapsedSeconds, int baseTimeout, long lastProgress)
{
if (!TryEnterTier(1, lastProgress))
return;
_logger.ScriptLog($"[Watchdog] Stalled for {elapsedSeconds:F0}s (threshold: {baseTimeout}s). Executing Tier 1 rescue (Revive / Target Reset)...");
_diagnostics.RecordEvent("Watchdog", "Tier1_SoftRescue", elapsedSeconds, "seconds");
try
{
string cell = Player.Cell;
if (!Player.Alive)
{
_logger.ScriptLog("[Watchdog] Character is dead; invoking game resurrect and clearing modals.");
string spawnCell = Flash.GetGameObject<string>("world.spawnPoint.strFrame") ?? "Enter";
ExpectRescueEcho(15, cell, spawnCell);
try { Flash.CallGameFunction("world.resPlayer"); } catch { }
try { Flash.Call("killLoginModals"); } catch { }
try { Map.Jump(cell, "Spawn"); } catch { }
}
else
{
ExpectRescueEcho(15, cell);
try { Combat.CancelTarget(); } catch { }
try { Flash.Call("world.cancelTarget"); } catch { }
try { Map.Jump(cell, Player.Pad); } catch { }
}
}
catch (Exception ex)
{
_logger.ScriptLog($"[Watchdog] Error executing Tier 1 rescue: {ex.Message}");
}
}
private void ExecuteTier2Rescue(double elapsedSeconds, long lastProgress)
{
if (!TryEnterTier(2, lastProgress))
return;
_logger.ScriptLog($"[Watchdog] Still stalled after {elapsedSeconds:F0}s. Executing Tier 2 rescue (Reloading Map {Map.Name})...");
_diagnostics.RecordEvent("Watchdog", "Tier2_RoomRefresh", elapsedSeconds, "seconds");
try
{
ExpectRescueEcho(30, Player.Cell);
try { Combat.CancelTarget(); } catch { }
try { Map.Reload(); }
catch
{
_rescueEcho = null;
}
}
catch (Exception ex)
{
_logger.ScriptLog($"[Watchdog] Error executing Tier 2 rescue: {ex.Message}");
}
}
private void ExecuteTier3Recovery(double elapsedSeconds, long lastProgress)
{
if (!TryEnterTier(3, lastProgress))
return;
_logger.ScriptLog($"[Watchdog] Critical stall detected ({elapsedSeconds:F0}s). Executing Tier 3 recovery (Script Restart)...");
_diagnostics.RecordEvent("Watchdog", "Tier3_CriticalRestart", elapsedSeconds, "seconds");
string currentScript = Path.GetFileName(Manager.LoadedScript);
string currentMap = Map.Name;
string currentCell = Player.Cell;
bool autoRestart = Options.WatchdogAutoRestart;
if (Options.WatchdogDiscordAlerts)
{
_ = Task.Run(async () =>
{
try
{
await _discord.SendEmbedAsync(
autoRestart ? "⚠️ Script Stalled - Watchdog Auto-Restart" : "⚠️ Script Stalled",
$"Script `{currentScript}` stalled for {elapsedSeconds:F0}s in `{currentMap}` (Cell: `{currentCell}`). "
+ (autoRestart ? "Automatically restarting script." : "Auto-restart is off, so the script was left running."),
0xFFA500);
}
catch { }
});
}
if (autoRestart)
{
Volatile.Write(ref _lastProgressTimestamp, Stopwatch.GetTimestamp());
_ = Task.Run(async () =>
{
try
{
await Manager.RestartScriptAsync();
}
catch (Exception ex)
{
_logger.ScriptLog($"[Watchdog] Failed to restart script: {ex.Message}");
}
});
}
}
public void Dispose()
{
if (_disposed)
return;
_disposed = true;
Stop();
StrongReferenceMessenger.Default.UnregisterAll(this);
}
}
