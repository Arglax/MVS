using Skua.Core.Interfaces;
using Skua.Core.Models.Players;
using Skua.Core.Models.Skills;
namespace ButlerV5;
public class FollowerSettings
{
public required Func<bool> PassiveAttack { get; init; }
public required Func<int> GotoDelayMs { get; init; }
public required Func<int> RescueThreshold { get; init; }
public required Func<bool> LeechMode { get; init; }
public required Func<ParkSpot> Park { get; init; }
public required Func<bool> InstantWarnings { get; init; }
public required Func<bool> QuestBypasses { get; init; }
public required Func<string> CustomBypassIds { get; init; }
public required Func<bool> LevelFake { get; init; }
public required Func<bool> QuestClone { get; init; }
public required Func<bool> QuestSync { get; init; }
public required Func<bool> AlwaysGoto { get; init; }
public required Func<bool> CopyWalk { get; init; }
public required Func<bool> UnlockAllQuests { get; init; }
public required Func<bool> ScriptRunning { get; init; }
public required Func<bool> AntiLagSummoned { get; init; }
public required Func<bool> AntiLagFollowing { get; init; }
public Action<string>? ScriptSuspendNotice { get; init; }
public Func<IEnumerable<AdvancedSkill>?>? SkillSets { get; init; }
}
public enum FollowSource
{
None,
Manual,
Ordered,
}
public class Follower
{
private const int LoopDelayMs = 250;
private static int TickMs => AppContext.GetData("mvs.lowPower") is true ? 750 : LoopDelayMs;
private const int JumpMinIntervalMs = 500;
private const string ParkMap = "whitemap-100000";
private readonly IScriptInterface _bot;
private readonly FollowerSettings _settings;
private readonly Action<string, string> _log;
private CancellationTokenSource? _cts;
private Task? _loop;
private DateTime _lastGoto = DateTime.MinValue;
private DateTime _lastSnapshot = DateTime.MinValue;
private DateTime _lastJumpTime = DateTime.MinValue;
private DateTime _lastWalkCommandTime = DateTime.MinValue;
private int _lastWalkTargetX;
private int _lastWalkTargetY;
private bool _hasWalkTarget;
private DateTime _lastJoinTime = DateTime.MinValue;
private string? _lastJoinTarget;
private int _gotoAttempts;
private int _directJoinAttempts;
private string? _directJoinTarget;
private bool? _lastReportedOffGoto;
private bool _packetStreamLogged;
private string? _skillClass;
private ClassUseMode _skillMode = ClassUseMode.Base;
private ButlerClassType _classType = ButlerClassType.None;
private string? _modeNoteShownFor;
private bool _skillsActive;
private bool _skillsPaused;
private string? _lastBypassMap;
private string? _lastPreApplyMap;
private DateTime _lastCloneFileTime = DateTime.MinValue;
private DateTime _lastQuestSyncCheck = DateTime.MinValue;
private DateTime _lastQuestSyncFileTime = DateTime.MinValue;
private readonly HashSet<int> _syncedQuestIds = new();
private readonly Dictionary<int, int> _syncedQuestRewards = new();
private string? _lastRescueMap;
private int _rescueRounds;
private string? _blockedMap;
private int _roomFullAttempts;
private string? _fullCountRoom;
private string? _blockedRoom;
private int _badReads;
private bool _suspended;
private bool _wasLoggedIn;
private bool? _lastWithMaster;
private volatile bool _parked;
private volatile bool _lockedZone;
private volatile bool _roomFull;
private volatile bool _gotoOff;
private volatile bool _isParking;
private bool _antiLagApplied;
private AntiLagSnapshot? _antiLagPrior;
private bool _butlerOptsApplied;
private (bool InfiniteRange, bool SkipCutscenes)? _butlerOptsPrior;
private bool _autoStopLogged;
private string? _ownHouseUsername;
private string? _ownHouseRoom;
public bool IsParking => _isParking;
public bool IsParked => _parked;
public string? Master { get; private set; }
public FollowSource Source { get; private set; } = FollowSource.None;
public bool IsRunning => _loop is { IsCompleted: false };
public Follower(IScriptInterface bot, FollowerSettings settings, Action<string, string> log)
{
_bot = bot;
_settings = settings;
_log = log;
}
public void Start(string master, FollowSource source)
{
Stop();
Master = master;
Source = source;
_lastReportedOffGoto = null;
_lastQuestSyncCheck = DateTime.MinValue;
_lastQuestSyncFileTime = DateTime.MinValue;
_lastWalkCommandTime = DateTime.MinValue;
_lastWalkTargetX = 0;
_lastWalkTargetY = 0;
_hasWalkTarget = false;
_directJoinTarget = null;
_directJoinAttempts = 0;
_suspended = false;
_parked = false;
_lastWithMaster = null;
_lastPreApplyMap = null;
_lastRescueMap = null;
_rescueRounds = 0;
_blockedMap = null;
_roomFullAttempts = 0;
_fullCountRoom = null;
_blockedRoom = null;
_lockedZone = _roomFull = _gotoOff = false;
DebugLog.Log("Follower", $"Start following {master} ({source})");
_cts = new CancellationTokenSource();
CancellationToken token = _cts.Token;
_bot.Events.ExtensionPacketReceived += PacketListener;
try { _bot.Flash.FlashCall += OnFlashCall; } catch { }
_packetStreamLogged = false;
DebugLog.Log("Follower",
$"options in THIS client: passiveAttack={_settings.PassiveAttack()} leech={_settings.LeechMode()} " +
$"park={_settings.Park()} gotoDelay={_settings.GotoDelayMs()}ms alwaysGoto={_settings.AlwaysGoto()} copyWalk={_settings.CopyWalk()} " +
$"questSync={_settings.QuestSync()} rescue={_settings.RescueThreshold()} " +
$"instantWarnings={_settings.InstantWarnings()} questBypass={_settings.QuestBypasses()} questClone={_settings.QuestClone()}");
_log($"Following {master} ({source}).", $"Following the master ({source}).");
_loop = Task.Run(() => Run(master, token), token);
}
public void Stop(bool park = false)
{
if (_cts == null)
{
ClearSyncedQuestRegistrations();
Source = FollowSource.None;
return;
}
_bot.Events.ExtensionPacketReceived -= PacketListener;
try { _bot.Flash.FlashCall -= OnFlashCall; } catch { }
_cts.Cancel();
try { _loop?.Wait(3000); } catch { }
_cts.Dispose();
_cts = null;
_loop = null;
ClearSyncedQuestRegistrations();
StopSkills();
StopAttacking(deaggroJump: true);
RevertAntiLag();
RevertButlerOptions();
if (park && (!_parked ||
(_settings.Park() == ParkSpot.House && !IsInOwnHouse(GetMyMapRoom()))))
Park("Released by master.", "Released by master.");
else if (park)
DebugLog.Log("Follower", "released while already parked - staying put");
_log(Master != null ? $"Stopped following {Master}." : "Follow stopped.",
Master != null ? "Stopped following the master." : "Follow stopped.");
Master = null;
Source = FollowSource.None;
}
private void Run(string master, CancellationToken token)
{
try
{
Arm(master, freshLogin: false);
_wasLoggedIn = _bot.Player?.LoggedIn == true;
while (!token.IsCancellationRequested)
{
if (_bot.Player?.LoggedIn != true || !_bot.Player.Alive)
{
if (_bot.Player?.LoggedIn != true)
{
_wasLoggedIn = false;
_syncedQuestIds.Clear();
_syncedQuestRewards.Clear();
_lastQuestSyncFileTime = DateTime.MinValue;
_lastQuestSyncCheck = DateTime.MinValue;
}
Wait(token, 1000);
continue;
}
if (!_wasLoggedIn)
{
_wasLoggedIn = true;
DebugLog.Log("Follower", "re-login detected - waiting for character to load");
WaitForCharacterReady(token);
Arm(master, freshLogin: true);
}
if (_settings.ScriptRunning())
{
if (!_suspended)
{
_suspended = true;
ClearSyncedQuestRegistrations();
StopSkills();
StopAttacking(deaggroJump: true);
_log($"A script is running - following {master} is suspended until it stops.",
"A script is running - following the master is suspended until it stops.");
DebugLog.Log("Follower", "suspended (script running in this client)");
try { _settings.ScriptSuspendNotice?.Invoke(master); } catch { }
}
Wait(token, 1000);
continue;
}
if (_suspended)
{
_suspended = false;
_gotoAttempts = 0;
_log($"Script stopped - resuming follow of {master}.",
"Script stopped - resuming follow of the master.");
DebugLog.Log("Follower", "resuming after script");
}
StopUserAuto();
if (!_settings.QuestSync())
ClearSyncedQuestRegistrations();
if (_parked)
ForceParkingAggroOff();
SyncData? m = SyncFile.ReadUser(master);
if ((DateTime.UtcNow - _lastSnapshot).TotalSeconds >= 10)
{
_lastSnapshot = DateTime.UtcNow;
if (!_settings.UnlockAllQuests() && _settings.QuestClone())
{
DateTime fileTime = QuestClone.GetFileTime(master);
if (fileTime > _lastCloneFileTime)
{
_lastCloneFileTime = fileTime;
DebugLog.Log("QuestClone", $"{master}'s quest file changed - re-cloning");
QuestClone.ApplyFromMaster(_bot, master);
}
}
DebugLog.Log("Follower",
$"tick: me map={_bot.Map?.Name} cell={_bot.Player?.Cell} pad={_bot.Player?.Pad} " +
$"server={GameServer.CurrentName(_bot)} parked={_parked} " +
$"flags locked={_lockedZone} full={_roomFull} gotoOff={_gotoOff} | master {DebugLog.Describe(m)}" +
(m != null ? $" pidAlive={SyncFile.IsProcessAlive(m.Pid)}" : ""));
}
if (m == null || !SyncFile.IsOnline(m))
{
_badReads++;
if (_badReads >= 3)
{
ClearSyncedQuestRegistrations();
ParkOnce(m == null
? $"No ButlerV5 file for {master} at {SyncFile.PathFor(master)} - is their plugin installed?"
: $"{master} looks offline (loggedin={(m.LoggedIn ? 1 : 0)}, pid={m.Pid}, " +
$"pidAlive={SyncFile.IsProcessAlive(m.Pid)}). Parked, waiting for them to return.",
m == null
? "No ButlerV5 file for the master - is their plugin installed? Parked."
: "The master looks offline. Parked, waiting for them to return.");
}
Wait(token, 1000);
continue;
}
_badReads = 0;
if (_lastReportedOffGoto == true && !m.OffGoto)
{
_gotoOff = false;
_gotoAttempts = 0;
_directJoinAttempts = 0;
_directJoinTarget = null;
_blockedMap = null;
_blockedRoom = null;
DebugLog.Log("Follower", $"{master} enabled goto again");
}
_lastReportedOffGoto = m.OffGoto;
if (!SameServer(m))
{
ClearSyncedQuestRegistrations();
ParkOnce($"{master} is on a different server (theirs='{m.Server}' mine='{GameServer.CurrentName(_bot)}'). Parked.",
"The master is on a different server. Parked.");
Wait(token, 2000);
continue;
}
if (_settings.QuestSync())
SyncRegisteredQuests(master);
if (_blockedMap != null)
{
if (_blockedMap.Equals(m.Map, StringComparison.OrdinalIgnoreCase))
{
Wait(token, 2000);
continue;
}
DebugLog.Log("Follower", $"master left blocked map {_blockedMap} - trying again");
_blockedMap = null;
}
if (_blockedRoom != null)
{
if (_blockedRoom.Equals(m.MapWithRoom, StringComparison.OrdinalIgnoreCase))
{
Wait(token, 2000);
continue;
}
DebugLog.Log("Follower", $"master left full room {_blockedRoom} - trying again");
_blockedRoom = null;
}
if (_parked)
{
_parked = false;
_log($"{master} is back - resuming follow.", "The master is back - resuming follow.");
}
if (_lockedZone || _roomFull)
{
bool full = _roomFull;
_lockedZone = _roomFull = false;
if (full)
{
if (string.Equals(m.MapWithRoom, _fullCountRoom, StringComparison.OrdinalIgnoreCase))
_roomFullAttempts++;
else
{
_fullCountRoom = m.MapWithRoom;
_roomFullAttempts = 1;
}
if (_roomFullAttempts >= 3)
{
_blockedRoom = m.MapWithRoom;
_roomFullAttempts = 0;
_fullCountRoom = null;
ParkOnce($"{m.MapWithRoom} is full after 3 attempts - parked until {master} changes rooms.",
"The master's room is full after 3 attempts - parked until the master changes rooms.");
Wait(token, 2000);
continue;
}
}
_log(full
? $"Room full - joining {m.MapWithRoom} directly (attempt {_roomFullAttempts})."
: $"Locked zone - joining {m.MapWithRoom} from {master}'s file.",
full
? $"Room full - joining the master's room directly (attempt {_roomFullAttempts})."
: "Locked zone - joining the master's room from their file.");
JoinMasterRoom(m, token);
Wait(token, TickMs);
continue;
}
string myRoom = GetMyMapRoom();
if (string.IsNullOrEmpty(myRoom))
{
Wait(token, TickMs);
continue;
}
string myMapName = _bot.Map?.Name ?? "";
if ((_settings.UnlockAllQuests() || _settings.QuestBypasses()) &&
!string.Equals(myMapName, _lastBypassMap, StringComparison.OrdinalIgnoreCase))
{
_lastBypassMap = myMapName;
if (_settings.UnlockAllQuests())
QuestClone.UnlockAll(_bot);
else
QuestBypass.ApplyForMap(_bot, myMapName);
}
bool sameRoom = string.Equals(myRoom, m.MapWithRoom, StringComparison.OrdinalIgnoreCase);
if (sameRoom != _lastWithMaster)
{
_lastWithMaster = sameRoom;
DebugLog.Log("Follower", $"room check: mine={myRoom} master={m.MapWithRoom} -> sameRoom={sameRoom}");
}
if (sameRoom)
{
_directJoinAttempts = 0;
_directJoinTarget = null;
_rescueRounds = 0;
_lastRescueMap = null;
_roomFullAttempts = 0;
_fullCountRoom = null;
}
if (!sameRoom)
{
if (_skillsActive && !_skillsPaused)
{
DebugLog.Log("Follower", "pausing skill engine to travel");
PauseSkills();
}
StopAttacking(deaggroJump: true);
if (!_settings.UnlockAllQuests() && _settings.QuestBypasses() &&
!string.Equals(m.Map, _lastPreApplyMap, StringComparison.OrdinalIgnoreCase))
{
_lastPreApplyMap = m.Map;
_lastBypassMap = m.Map;
DebugLog.Log("Follower", $"pre-applying quest fakes for target map {m.Map}");
QuestBypass.ApplyForMap(_bot, m.Map);
}
if (m.OffGoto || _gotoOff)
{
_gotoAttempts = 0;
if (!string.Equals(_directJoinTarget, m.MapWithRoom, StringComparison.OrdinalIgnoreCase))
{
_directJoinTarget = m.MapWithRoom;
_directJoinAttempts = 0;
DebugLog.Log("Follower", $"{master} has goto off - directly joining {m.MapWithRoom}");
}
if (_directJoinAttempts >= 3)
{
bool onMasterMap = string.Equals(_bot.Map?.Name ?? "", m.Map, StringComparison.OrdinalIgnoreCase);
if (onMasterMap)
{
_blockedRoom = m.MapWithRoom;
ParkOnce(m.Map.Equals("house", StringComparison.OrdinalIgnoreCase)
? $"Can't reach {master} in {m.MapWithRoom} through their own house - they may be visiting someone else's house. Parked until they change rooms."
: $"Can't enter {m.MapWithRoom} after 3 direct joins - parked until {master} changes rooms.",
m.Map.Equals("house", StringComparison.OrdinalIgnoreCase)
? "Can't reach the master through their own house - they may be visiting someone else's house. Parked until they change rooms."
: "Can't enter the master's room after 3 direct joins - parked until the master changes rooms.");
}
else
{
_blockedMap = m.Map;
ParkOnce($"Can't enter {m.Map} after 3 direct joins - parked until {master} changes maps.",
"Can't enter the master's map after 3 direct joins - parked until the master changes maps.");
}
Wait(token, 2000);
continue;
}
if (JoinMasterRoom(m, token))
_directJoinAttempts++;
}
else
{
int rescueThreshold = Math.Clamp(_settings.RescueThreshold(), 1, 10);
if (_gotoAttempts >= rescueThreshold)
{
_gotoAttempts = 0;
if (string.Equals(m.Map, _lastRescueMap, StringComparison.OrdinalIgnoreCase))
_rescueRounds++;
else
{
_lastRescueMap = m.Map;
_rescueRounds = 1;
}
if (_rescueRounds >= 3)
{
bool onMasterMap = string.Equals(_bot.Map?.Name ?? "", m.Map, StringComparison.OrdinalIgnoreCase);
_rescueRounds = 0;
if (onMasterMap)
{
_blockedRoom = m.MapWithRoom;
ParkOnce($"{m.MapWithRoom} is full after 3 rescue attempts - parked until {master} changes rooms.",
"The master's room is full after 3 rescue attempts - parked until the master changes rooms.");
}
else
{
_blockedMap = m.Map;
ParkOnce($"Can't enter {m.Map} after 3 rescue attempts - a quest gate may be missing. Parked until {master} changes maps.",
"Can't enter the master's map after 3 rescue attempts - a quest gate may be missing. Parked until the master changes maps.");
}
Wait(token, 2000);
continue;
}
DebugLog.Log("Follower", $"goto not landing after {rescueThreshold} attempts - file-join rescue (round {_rescueRounds})");
JoinMasterRoom(m, token);
}
else if (TryGoto(master))
{
_gotoAttempts++;
}
}
}
else if (_settings.LeechMode())
{
_gotoAttempts = 0;
if (_skillsActive)
StopSkills();
StopAttacking(deaggroJump: true);
if (!string.Equals(_bot.Player!.Cell, "Enter", StringComparison.OrdinalIgnoreCase) &&
(DateTime.UtcNow - _lastJumpTime).TotalMilliseconds >= JumpMinIntervalMs)
{
DebugLog.Log("Follower", $"leech: jump {_bot.Player.Cell} -> Enter/Spawn");
_bot.Map!.Jump("Enter", "Spawn", autoCorrect: false);
_lastJumpTime = DateTime.UtcNow;
}
}
else
{
_gotoAttempts = 0;
string targetCell = m.Cell;
string targetPad = m.Pad;
PlayerInfo? masterInfo = null;
if (_bot.Map!.TryGetPlayer(master, out PlayerInfo? info) &&
!string.IsNullOrEmpty(info?.Cell))
{
masterInfo = info;
targetCell = info.Cell;
targetPad = string.IsNullOrEmpty(info.Pad) ? m.Pad : info.Pad;
}
bool canCopyWalk = _settings.CopyWalk() && masterInfo is not null &&
_bot.Player!.Cell.Equals(targetCell, StringComparison.OrdinalIgnoreCase);
if (_settings.AlwaysGoto() && !canCopyWalk && !m.OffGoto && !_gotoOff)
TryGoto(master);
if (!string.Equals(_bot.Player!.Cell, targetCell, StringComparison.OrdinalIgnoreCase) &&
(DateTime.UtcNow - _lastJumpTime).TotalMilliseconds >= JumpMinIntervalMs)
{
DebugLog.Log("Follower", $"jump {_bot.Player.Cell}/{_bot.Player.Pad} -> {targetCell}/{targetPad}");
_bot.Map.Jump(targetCell, targetPad, autoCorrect: false);
_lastJumpTime = DateTime.UtcNow;
}
if (canCopyWalk)
CopyMasterWalk(masterInfo!);
else if (!_settings.CopyWalk())
{
_hasWalkTarget = false;
}
HandleCombat(m);
}
Wait(token, TickMs);
}
}
catch (Exception ex)
{
_log($"Follow loop error: {ex.Message}",
"Follow loop error. Turn on Butler's logs in its settings to see what happened.");
}
finally
{
ClearSyncedQuestRegistrations();
}
}
private void Arm(string master, bool freshLogin)
{
GameServer.LogCandidates(_bot);
StopUserAuto();
StopSkills();
SetupClassAndSkills(forceEquipCurrent: freshLogin);
if (_settings.LevelFake())
QuestBypass.ApplyLevelFake(_bot);
_lastBypassMap = null;
_lastPreApplyMap = null;
if (_settings.UnlockAllQuests())
{
QuestClone.UnlockAll(_bot);
}
else
{
if (_settings.QuestBypasses())
QuestBypass.ApplyCustom(_bot, QuestBypass.ParseCustomIds(_settings.CustomBypassIds()));
if (_settings.QuestClone())
{
QuestClone.ApplyFromMaster(_bot, master);
_lastCloneFileTime = QuestClone.GetFileTime(master);
}
}
ApplyAntiLag();
ApplyButlerOptions();
}
private readonly record struct AntiLagSnapshot(
bool LagKiller, bool FreezeMonsterPosition, bool DisableMonsterAnimation,
bool DisableDamageStrobe, bool DisableSelfAnimation, bool DisableWeaponAnimation,
bool DisableSkillAnimation, bool DisableAuraAnimations, bool MonstersHidden);
private void ApplyAntiLag()
{
bool want = (Source == FollowSource.Ordered && _settings.AntiLagSummoned())
|| (Source == FollowSource.Manual && _settings.AntiLagFollowing());
if (!want)
return;
if (!_antiLagApplied)
{
_antiLagPrior = CaptureAntiLagState();
_antiLagApplied = true;
DebugLog.Log("Follower", $"AntiLag ON (source={Source})");
}
SetAntiLag(true);
}
private void ApplyButlerOptions()
{
try
{
if (!_butlerOptsApplied)
{
_butlerOptsPrior = (_bot.Options.InfiniteRange, _bot.Options.SkipCutscenes);
_butlerOptsApplied = true;
DebugLog.Log("Follower",
$"butler options ON (was infiniteRange={_butlerOptsPrior.Value.InfiniteRange} " +
$"skipCutscenes={_butlerOptsPrior.Value.SkipCutscenes})");
}
SetInfiniteRange(true);
SetSkipCutscenes(true);
}
catch (Exception ex)
{
DebugLog.Log("Follower", $"butler options apply failed: {ex.Message}");
}
}
private void RevertButlerOptions()
{
if (!_butlerOptsApplied)
return;
_butlerOptsApplied = false;
try
{
if (_butlerOptsPrior is { } prior)
{
SetInfiniteRange(prior.InfiniteRange);
SetSkipCutscenes(prior.SkipCutscenes);
DebugLog.Log("Follower", "butler options OFF (restored pre-follow settings)");
}
}
catch (Exception ex)
{
DebugLog.Log("Follower", $"butler options restore failed: {ex.Message}");
}
_butlerOptsPrior = null;
}
private void SetInfiniteRange(bool on)
{
if (_bot.Options.InfiniteRange != on)
_bot.Options.InfiniteRange = on;
}
private void SetSkipCutscenes(bool on)
{
if (_bot.Options.SkipCutscenes != on)
_bot.Options.SkipCutscenes = on;
}
private void RevertAntiLag()
{
if (!_antiLagApplied)
return;
_antiLagApplied = false;
RestoreAntiLag(_antiLagPrior);
_antiLagPrior = null;
DebugLog.Log("Follower", "AntiLag OFF (restored pre-follow settings)");
}
private AntiLagSnapshot? CaptureAntiLagState()
{
try
{
return new AntiLagSnapshot(
_bot.Options.LagKiller,
_bot.Lite.FreezeMonsterPosition,
_bot.Lite.DisableMonsterAnimation,
_bot.Lite.DisableDamageStrobe,
_bot.Lite.DisableSelfAnimation,
_bot.Lite.DisableWeaponAnimation,
_bot.Lite.DisableSkillAnimation,
_bot.Lite.DisableAuraAnimations,
_bot.Flash.GetGameObject<bool>("ui.monsterIcon.redX.visible"));
}
catch (Exception ex)
{
DebugLog.Log("Follower", $"AntiLag capture failed: {ex.Message}");
return null;
}
}
private void RestoreAntiLag(AntiLagSnapshot? snapshot)
{
if (snapshot is not { } s)
{
SetAntiLag(false);
return;
}
try
{
_bot.Options.LagKiller = s.LagKiller;
_bot.Lite.FreezeMonsterPosition = s.FreezeMonsterPosition;
_bot.Lite.DisableMonsterAnimation = s.DisableMonsterAnimation;
_bot.Lite.DisableDamageStrobe = s.DisableDamageStrobe;
_bot.Lite.DisableSelfAnimation = s.DisableSelfAnimation;
_bot.Lite.DisableWeaponAnimation = s.DisableWeaponAnimation;
_bot.Lite.DisableSkillAnimation = s.DisableSkillAnimation;
_bot.Lite.DisableAuraAnimations = s.DisableAuraAnimations;
bool hidden = _bot.Flash.GetGameObject<bool>("ui.monsterIcon.redX.visible");
if (hidden != s.MonstersHidden)
_bot.Flash.CallGameFunction("world.toggleMonsters");
}
catch (Exception ex)
{
DebugLog.Log("Follower", $"AntiLag restore failed: {ex.Message}");
}
}
private void SetAntiLag(bool on)
{
try
{
_bot.Options.LagKiller = on;
_bot.Lite.FreezeMonsterPosition = on;
_bot.Lite.DisableMonsterAnimation = on;
_bot.Lite.DisableDamageStrobe = on;
_bot.Lite.DisableSelfAnimation = on;
_bot.Lite.DisableWeaponAnimation = on;
_bot.Lite.DisableSkillAnimation = on;
_bot.Lite.DisableAuraAnimations = on;
bool hidden = _bot.Flash.GetGameObject<bool>("ui.monsterIcon.redX.visible");
if (hidden != on)
_bot.Flash.CallGameFunction("world.toggleMonsters");
}
catch (Exception ex)
{
DebugLog.Log("Follower", $"AntiLag set {on} failed: {ex.Message}");
}
}
private void SetupClassAndSkills(bool forceEquipCurrent)
{
try
{
string? me = _bot.Player?.Username;
if (string.IsNullOrEmpty(me))
return;
_skillClass = null;
_skillMode = ClassUseMode.Base;
_modeNoteShownFor = null;
ButlerClassType type = AccountConfig.GetClassType(me);
_classType = type;
string? classToEquip = null;
if (type != ButlerClassType.None)
{
if (Enum.TryParse(AccountConfig.GetCboClassMode(me, type), ignoreCase: true, out ClassUseMode mode))
_skillMode = mode;
string? cboClass = AccountConfig.GetCboClassName(me, type);
if (string.IsNullOrEmpty(cboClass))
_log($"No CoreBots {type} class configured for {me} - keeping current class.",
$"No CoreBots {type} class configured for this account - keeping current class.");
else if (cboClass.Equals(AccountConfig.CboCurrentClass, StringComparison.OrdinalIgnoreCase))
classToEquip = forceEquipCurrent ? _bot.Player?.CurrentClass?.Name : null;
else
classToEquip = cboClass;
}
else if (forceEquipCurrent)
{
classToEquip = _bot.Player?.CurrentClass?.Name;
}
if (!string.IsNullOrEmpty(classToEquip))
{
_log($"Equipping class: {classToEquip}", $"Equipping class: {classToEquip}");
_bot.Inventory.EquipItem(classToEquip);
bool equipped = false;
for (int i = 0; i < 6; i++)
{
Thread.Sleep(250);
string? worn = _bot.Player?.CurrentClass?.Name;
if (!string.IsNullOrEmpty(worn) &&
worn.Trim().Equals(classToEquip.Trim(), StringComparison.OrdinalIgnoreCase))
{
equipped = true;
break;
}
}
if (equipped)
{
_skillClass = classToEquip;
}
else
{
string? worn = _bot.Player?.CurrentClass?.Name;
_log($"Couldn't equip '{classToEquip}' - not in {me}'s inventory? Keeping current class ({worn ?? "unknown"}).",
$"Couldn't equip '{classToEquip}' - not in this account's inventory? Keeping current class ({worn ?? "unknown"}).");
_skillClass = worn;
}
}
}
catch (Exception ex)
{
DebugLog.Log("Follower", $"class equip failed: {ex.Message}");
}
}
private void WaitForCharacterReady(CancellationToken token)
{
for (int i = 0; i < 40 && !token.IsCancellationRequested; i++)
{
try
{
if (_bot.Player?.LoggedIn == true && _bot.Player.Alive &&
_bot.Map?.Loaded == true &&
!string.IsNullOrEmpty(_bot.Player.CurrentClass?.Name))
break;
}
catch
{
}
Wait(token, 250);
}
DebugLog.Log("Follower", $"character ready: class={_bot.Player?.CurrentClass?.Name} map={_bot.Map?.Name} loaded={_bot.Map?.Loaded}");
}
private ClassUseMode ResolveSkillMode(string skillClass)
{
ClassUseMode mode = _skillMode;
try
{
List<AdvancedSkill>? sets = _settings.SkillSets?.Invoke()?.ToList();
mode = AccountConfig.ResolveSkillMode(skillClass, _skillMode, _classType, sets);
}
catch (Exception ex)
{
DebugLog.Log("Follower", $"skill mode lookup failed: {ex.Message}");
}
if (mode != _skillMode && _modeNoteShownFor != skillClass)
{
_modeNoteShownFor = skillClass;
_log($"{skillClass} has no {_skillMode} skill set - using {mode}.",
$"{skillClass} has no {_skillMode} skill set - using {mode}.");
}
return mode;
}
private void StartSkills()
{
RefreshSkillState();
if (_skillsActive)
{
if (_skillsPaused)
ResumeSkills();
return;
}
try
{
string skillClass = _skillClass ?? _bot.Player?.CurrentClass?.Name ?? "generic";
ClassUseMode mode = ResolveSkillMode(skillClass);
DebugLog.Log("Follower", $"starting skill engine: {skillClass} ({mode})");
_bot.Skills.StartAdvanced(skillClass, false, mode);
RefreshSkillState();
DebugLog.Log("Follower", $"skill engine start state: TimerRunning={_skillsActive} IsPaused={_skillsPaused}");
if (!_skillsActive)
DebugLog.Log("Follower", "skill engine did not report a running timer after StartAdvanced");
}
catch (Exception ex)
{
RefreshSkillState();
DebugLog.Log("Follower", $"skill engine start failed: {ex.Message}");
}
}
private void StopUserAuto()
{
try
{
if (_bot.Auto.IsRunning)
{
if (!_autoStopLogged)
{
_autoStopLogged = true;
_log("Stopped Skua's Auto Attack/Hunt - it conflicts with following; the butler runs combat itself.",
"Stopped Skua's Auto Attack/Hunt - it conflicts with following; the butler runs combat itself.");
}
_bot.Auto.Stop();
}
else
{
_autoStopLogged = false;
}
}
catch
{
}
}
private void StopSkills()
{
try
{
if (_bot.Skills is Skua.Core.Scripts.ScriptSkill ss && ss.IsPaused)
{
DebugLog.Log("Follower", "resuming paused skill engine before stop to release Skua's pause wait");
ss.Resume();
}
_bot.Skills.Stop();
}
catch (Exception ex)
{
DebugLog.Log("Follower", $"skill engine stop failed: {ex.Message}");
}
RefreshSkillState();
DebugLog.Log("Follower", $"skill engine stop state: TimerRunning={_skillsActive} IsPaused={_skillsPaused}");
}
private void PauseSkills()
{
RefreshSkillState();
if (!_skillsActive || _skillsPaused)
return;
if (_bot.Skills is Skua.Core.Scripts.ScriptSkill ss)
{
try
{
ss.Pause();
RefreshSkillState();
DebugLog.Log("Follower", _skillsPaused
? "skill engine paused (rotation kept)"
: "skill engine did not enter the paused state");
}
catch (Exception ex)
{
RefreshSkillState();
DebugLog.Log("Follower", $"skill pause failed: {ex.Message}");
}
}
else
{
StopSkills();
}
}
private void ResumeSkills()
{
RefreshSkillState();
if (!_skillsActive)
{
StartSkills();
return;
}
if (!_skillsPaused)
return;
if (_bot.Skills is Skua.Core.Scripts.ScriptSkill ss)
{
try
{
ss.Resume();
RefreshSkillState();
DebugLog.Log("Follower", !_skillsPaused
? "skill engine resumed"
: "skill engine still reports paused after Resume");
}
catch (Exception ex)
{
RefreshSkillState();
DebugLog.Log("Follower", $"skill resume failed: {ex.Message}");
}
}
}
private void RefreshSkillState()
{
bool running = _bot.Skills.TimerRunning;
bool paused = running &&
_bot.Skills is Skua.Core.Scripts.ScriptSkill ss &&
ss.IsPaused;
_skillsActive = running;
_skillsPaused = paused;
}
private void OnFlashCall(string function, object[] args)
{
try
{
if (!_settings.InstantWarnings())
return;
if (!string.Equals(function, "pext", StringComparison.OrdinalIgnoreCase))
return;
if (!_packetStreamLogged)
{
_packetStreamLogged = true;
DebugLog.Log("Follower", "instant warnings: packet stream active");
}
if (args is not { Length: > 0 })
return;
string text = string.Join(" ", args.Select(a => a?.ToString() ?? ""));
if (text.Length == 0)
return;
if (text.Contains("a Locked zone.", StringComparison.OrdinalIgnoreCase) ||
text.Contains("is not available.", StringComparison.OrdinalIgnoreCase))
{
_lockedZone = true;
DebugLog.Log("Follower", "instant warning: locked zone / not available");
}
if (text.Contains("is full", StringComparison.OrdinalIgnoreCase))
{
_roomFull = true;
DebugLog.Log("Follower", "instant warning: room full");
}
if (text.Contains("ignoring goto", StringComparison.OrdinalIgnoreCase))
{
_gotoOff = true;
DebugLog.Log("Follower", "instant warning: ignoring goto");
}
}
catch
{
}
}
private void HandleCombat(SyncData master)
{
try
{
RefreshSkillState();
bool shouldAttack = !_settings.PassiveAttack() || master.Attacking;
if (shouldAttack)
{
if (!_skillsActive)
StartSkills();
else if (_skillsPaused)
ResumeSkills();
_bot.Combat.Attack("*");
}
else
{
if (_skillsActive && !_skillsPaused)
PauseSkills();
StopAttacking(deaggroJump: true);
}
}
catch
{
}
}
private void StopAttacking(bool deaggroJump = false)
{
try
{
bool wasFighting = _bot.Player?.HasTarget == true || _bot.Player?.InCombat == true;
if (!wasFighting)
return;
_bot.Combat.CancelAutoAttack();
_bot.Combat.CancelTarget();
if (deaggroJump)
{
DebugLog.Log("Follower", "same-cell rejoin to drop monster aggro");
_bot.Map!.Jump(_bot.Player?.Cell ?? "Enter", _bot.Player?.Pad ?? "Spawn", autoCorrect: false);
}
}
catch
{
}
}
private void SyncRegisteredQuests(string master)
{
DateTime now = DateTime.UtcNow;
if ((now - _lastQuestSyncCheck).TotalSeconds < 1)
return;
_lastQuestSyncCheck = now;
DateTime fileTime = QuestSync.GetFileTime(master);
if (fileTime == DateTime.MinValue || fileTime <= _lastQuestSyncFileTime)
return;
QuestSync.QuestSyncEntry[]? requestedEntries = QuestSync.Read(master);
if (requestedEntries is null)
return;
Dictionary<int, int> requested = requestedEntries.ToDictionary(entry => entry.QuestId, entry => entry.RewardId);
int[] removed = _syncedQuestIds
.Where(id => !requested.TryGetValue(id, out int rewardId) ||
(_syncedQuestRewards.TryGetValue(id, out int previousReward) && previousReward != rewardId))
.ToArray();
if (removed.Length > 0)
{
_bot.Quests.UnregisterQuests(removed);
_syncedQuestIds.ExceptWith(removed);
foreach (int questId in removed)
_syncedQuestRewards.Remove(questId);
}
HashSet<int> alreadyRegistered = new(_bot.Quests.Registered);
(int questId, int rewardId)[] added = requested
.Where(pair => !alreadyRegistered.Contains(pair.Key))
.Select(pair => (pair.Key, pair.Value))
.ToArray();
if (added.Length > 0)
{
_bot.Quests.RegisterQuests(added);
foreach ((int questId, int rewardId) in added)
{
_syncedQuestIds.Add(questId);
_syncedQuestRewards[questId] = rewardId;
}
}
_lastQuestSyncFileTime = fileTime;
DebugLog.Log("QuestSync",
$"{master}: added/updated {added.Length}, removed {removed.Length}, tracking {requested.Count} active/registered quest(s)");
}
private void ClearSyncedQuestRegistrations()
{
if (_syncedQuestIds.Count == 0)
{
_syncedQuestRewards.Clear();
_lastQuestSyncFileTime = DateTime.MinValue;
_lastQuestSyncCheck = DateTime.MinValue;
return;
}
try
{
int[] questIds = _syncedQuestIds.ToArray();
_bot.Quests.UnregisterQuests(questIds);
DebugLog.Log("QuestSync", $"removed {_syncedQuestIds.Count} mirrored quest registration(s)");
}
catch (Exception ex)
{
DebugLog.Log("QuestSync", $"could not remove mirrored quests: {ex.Message}");
}
finally
{
_syncedQuestIds.Clear();
_syncedQuestRewards.Clear();
_lastQuestSyncFileTime = DateTime.MinValue;
_lastQuestSyncCheck = DateTime.MinValue;
}
}
private void CopyMasterWalk(PlayerInfo master)
{
IScriptPlayer? player = _bot.Player;
if (player is null)
return;
int targetX = (int)Math.Round(master.X);
int targetY = (int)Math.Round(master.Y);
double deltaX = targetX - player.X;
double deltaY = targetY - player.Y;
if (deltaX * deltaX + deltaY * deltaY <= 100)
{
_hasWalkTarget = false;
return;
}
DateTime now = DateTime.UtcNow;
bool targetChanged = !_hasWalkTarget ||
Math.Abs(targetX - _lastWalkTargetX) >= 4 ||
Math.Abs(targetY - _lastWalkTargetY) >= 4;
double elapsedMs = (now - _lastWalkCommandTime).TotalMilliseconds;
if (!targetChanged && elapsedMs < 1500)
return;
int throttleMs = Math.Clamp(_settings.GotoDelayMs(), LoopDelayMs, 2000);
if (elapsedMs < throttleMs)
return;
try
{
int speed = player.WalkSpeed > 0 ? Math.Clamp(player.WalkSpeed, 1, 20) : 8;
_bot.Flash.Call("walkTo", targetX, targetY, speed);
_lastWalkTargetX = targetX;
_lastWalkTargetY = targetY;
_lastWalkCommandTime = now;
_hasWalkTarget = true;
}
catch (Exception ex)
{
DebugLog.Log("Follower", $"copy walk failed: {ex.Message}");
}
}
private bool TryGoto(string master)
{
int delayMs = Math.Clamp(_settings.GotoDelayMs(), 0, 60000);
if ((DateTime.UtcNow - _lastGoto).TotalMilliseconds < delayMs)
return false;
_lastGoto = DateTime.UtcNow;
try
{
DebugLog.Log("Follower", $"goto {master} (my map={_bot.Map?.Name})");
_bot.Player.Goto(master);
return true;
}
catch (Exception ex)
{
DebugLog.Log("Follower", $"goto failed: {ex.Message}");
return false;
}
}
private bool JoinMasterRoom(SyncData m, CancellationToken token)
{
if (string.Equals(m.MapWithRoom, _lastJoinTarget, StringComparison.OrdinalIgnoreCase) &&
(DateTime.UtcNow - _lastJoinTime).TotalMilliseconds < 5000)
return false;
_lastJoinTarget = m.MapWithRoom;
_lastJoinTime = DateTime.UtcNow;
try
{
DebugLog.Log("Follower", $"join {m.MapWithRoom} cell={m.Cell} pad={m.Pad} (from map={_bot.Map?.Name})");
StopAttacking();
if (m.Map.Equals("house", StringComparison.OrdinalIgnoreCase))
{
string roomBefore = GetMyMapRoom();
_bot.Send.Packet($"%xt%zm%house%1%{m.Username}%");
for (int i = 0; i < 24 && !token.IsCancellationRequested; i++)
{
Wait(token, 250);
string currentRoom = GetMyMapRoom();
if (currentRoom.Equals(m.MapWithRoom, StringComparison.OrdinalIgnoreCase))
{
DebugLog.Log("Follower", $"house join reached {currentRoom}");
return true;
}
if (currentRoom.StartsWith("house-", StringComparison.OrdinalIgnoreCase) &&
!currentRoom.Equals(roomBefore, StringComparison.OrdinalIgnoreCase))
{
_blockedRoom = m.MapWithRoom;
ParkOnce($"House join reached {currentRoom}, but {m.Username} is in {m.MapWithRoom}. Parked until they change rooms.",
"House join reached a different house from the master's room. Parked until they change rooms.");
return true;
}
}
}
else
{
if (string.Equals(_bot.Map?.Name, m.Map, StringComparison.OrdinalIgnoreCase))
{
_bot.Map!.JoinPacket(m.MapWithRoom, m.Cell, m.Pad);
}
else
{
_bot.Map!.Join(m.MapWithRoom, m.Cell, m.Pad, ignoreCheck: true);
}
_bot.Wait.ForMapLoad(m.Map);
}
DebugLog.Log("Follower", $"join done, now on map={_bot.Map?.Name} loaded={_bot.Map?.Loaded}");
}
catch (Exception ex)
{
_log($"Join {m.MapWithRoom} failed: {ex.Message}",
"Joining the master's room failed. Turn on Butler's logs in its settings to see what happened.");
DebugLog.Log("Follower", $"join {m.MapWithRoom} FAILED: {ex}");
}
return true;
}
private void ParkOnce(string reason, string scriptReason)
{
if (_parked)
return;
_parked = true;
Park(reason, scriptReason);
}
private void Park(string reason, string scriptReason)
{
_isParking = true;
try
{
_log(reason, scriptReason);
DebugLog.Log("Follower", $"PARK: {reason}");
ForceParkingAggroOff();
StopSkills();
StopAttacking(deaggroJump: true);
ParkSpot spot = _settings.Park();
if (spot == ParkSpot.Stay)
{
DebugLog.Log("Follower", "park mode Stay - not moving");
return;
}
string already = GetMyMapRoom();
if (spot == ParkSpot.House && IsInOwnHouse(already))
{
DebugLog.Log("Follower", "already home - park is a no-op");
return;
}
if (spot == ParkSpot.Whitemap && already.StartsWith("whitemap-", StringComparison.OrdinalIgnoreCase))
{
DebugLog.Log("Follower", "already at whitemap - park is a no-op");
return;
}
for (int i = 0; i < 12 && _bot.Player?.InCombat == true; i++)
ParkingWait(250);
if (_bot.Player?.InCombat == true)
DebugLog.Log("Follower", "still flagged in-combat after 3s - parking anyway");
try
{
if (spot == ParkSpot.House &&
_bot.Player?.LoggedIn == true &&
_bot.House?.Items?.Any(h => h.Equipped) == true)
{
DebugLog.Log("Follower", "parking in own house");
ParkingWait(3000);
for (int attempt = 1; attempt <= 3; attempt++)
{
_bot.Send.Packet($"%xt%zm%house%1%{_bot.Player.Username}%");
for (int i = 0; i < 24; i++)
{
ParkingWait(250);
string current = GetMyMapRoom();
if (IsInOwnHouse(current))
{
DebugLog.Log("Follower", $"parked in own house ({current}, attempt {attempt})");
return;
}
}
if (attempt < 3)
{
DebugLog.Log("Follower", $"house join attempt {attempt} didn't land (post-combat cooldown?) - retrying in 5s");
ParkingWait(5000);
}
}
if (_bot.Player?.InCombat == true)
{
DebugLog.Log("Follower", "still in combat after 3 house attempts - jumping to Enter/Spawn to shed aggro");
try { _bot.Map!.Jump("Enter", "Spawn", autoCorrect: false); } catch { }
for (int i = 0; i < 12 && _bot.Player?.InCombat == true; i++)
ParkingWait(250);
ParkingWait(4000);
_bot.Send.Packet($"%xt%zm%house%1%{_bot.Player!.Username}%");
for (int i = 0; i < 24; i++)
{
ParkingWait(250);
string current = GetMyMapRoom();
if (IsInOwnHouse(current))
{
DebugLog.Log("Follower", $"parked in own house ({current}, after Enter/Spawn deaggro)");
return;
}
}
}
if (IsInOwnHouse(GetMyMapRoom()))
{
DebugLog.Log("Follower", "parked in own house after final wait");
return;
}
DebugLog.Log("Follower", "own house join didn't land - falling back to whitemap");
}
}
catch (Exception ex)
{
DebugLog.Log("Follower", $"house park failed: {ex.Message}");
}
ForceParkingAggroOff();
_bot.Map!.Join(ParkMap);
}
catch (Exception ex)
{
DebugLog.Log("Follower", $"park failed: {ex.Message}");
}
finally
{
ForceParkingAggroOff();
_isParking = false;
}
}
private void ForceParkingAggroOff()
{
try
{
bool roomAggro = _bot.Options.AggroMonsters;
bool mapAggro = _bot.Options.AggroAllMonsters;
_bot.Options.AggroMonsters = false;
_bot.Options.AggroAllMonsters = false;
if (roomAggro || mapAggro)
DebugLog.Log("Follower", $"parking forced aggro OFF (room={roomAggro} map={mapAggro})");
}
catch (Exception ex)
{
DebugLog.Log("Follower", $"parking aggro-off enforcement failed: {ex.Message}");
}
}
private void ParkingWait(int milliseconds)
{
int remaining = Math.Max(0, milliseconds);
while (remaining > 0)
{
ForceParkingAggroOff();
int slice = Math.Min(100, remaining);
Thread.Sleep(slice);
remaining -= slice;
}
ForceParkingAggroOff();
}
private string GetMyMapRoom()
{
try
{
if (_bot.Map?.Loaded != true)
return "";
string name = _bot.Map.Name?.ToLowerInvariant() ?? "";
if (string.IsNullOrEmpty(name) || name == "-1")
return "";
string fullName = _bot.Map.FullName ?? "";
int dash = fullName.LastIndexOf('-');
if (dash > 0 && dash < fullName.Length - 1)
{
string room = fullName[(dash + 1)..];
if (room.All(char.IsDigit))
return $"{name}-{room}";
}
return "";
}
catch
{
return "";
}
}
private bool IsInOwnHouse(string room)
{
try
{
string? username = _bot.Player?.Username;
if (string.IsNullOrWhiteSpace(username))
return false;
if (!string.Equals(username, _ownHouseUsername, StringComparison.OrdinalIgnoreCase))
{
_ownHouseUsername = username;
_ownHouseRoom = null;
}
if (_ownHouseRoom == null)
{
int charId = _bot.Flash.GetGameObject<int>("world.myAvatar.objData.CharID");
if (charId <= 0)
return false;
_ownHouseRoom = $"house-{charId}";
DebugLog.Log("Follower", $"own house room resolved from CharID: {_ownHouseRoom}");
}
return string.Equals(room, _ownHouseRoom, StringComparison.OrdinalIgnoreCase);
}
catch (Exception ex)
{
DebugLog.Log("Follower", $"own house CharID lookup failed: {ex.Message}");
return false;
}
}
private bool SameServer(SyncData m)
{
string myServer = GameServer.CurrentName(_bot);
return GameServer.Same(m.Server, myServer, m.Username);
}
private void Wait(CancellationToken token, int ms)
{
if (!_parked)
{
token.WaitHandle.WaitOne(ms);
return;
}
int remaining = Math.Max(0, ms);
while (remaining > 0 && !token.IsCancellationRequested)
{
ForceParkingAggroOff();
int slice = Math.Min(100, remaining);
if (token.WaitHandle.WaitOne(slice))
break;
remaining -= slice;
}
ForceParkingAggroOff();
}
private void PacketListener(dynamic packet)
{
try
{
if (packet == null)
return;
dynamic? paramsObj = packet["params"];
if (paramsObj is null)
return;
if (paramsObj!.type != "str")
return;
dynamic? dataObj = paramsObj!.dataObj;
if (dataObj == null)
return;
string? cmd = dataObj[0];
if (string.IsNullOrEmpty(cmd))
return;
if (cmd == "server")
{
string? text = dataObj[2]?.ToString();
if (!string.IsNullOrWhiteSpace(text) &&
text.Contains("is ignoring goto requests", StringComparison.OrdinalIgnoreCase))
_gotoOff = true;
}
else if (cmd == "warning")
{
string chat = Convert.ToString(packet) ?? "";
if (chat.Contains("a Locked zone.", StringComparison.OrdinalIgnoreCase) ||
chat.Contains("is not available.", StringComparison.OrdinalIgnoreCase))
{
_lockedZone = true;
DebugLog.Log("Follower", "warning packet: locked zone / not available");
}
if (chat.Contains("is full", StringComparison.OrdinalIgnoreCase))
{
_roomFull = true;
DebugLog.Log("Follower", "warning packet: room full");
}
if (chat.Contains("ignoring goto", StringComparison.OrdinalIgnoreCase))
{
_gotoOff = true;
DebugLog.Log("Follower", "warning packet: ignoring goto");
}
}
}
catch
{
}
}
}
