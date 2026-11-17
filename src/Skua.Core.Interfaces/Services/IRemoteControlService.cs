namespace Skua.Core.Interfaces;
public interface IRemoteControlService : IDisposable
{
bool IsRunning { get; }
int Port { get; }
void Start();
void Stop();
Func<int>? ActiveChildPidProvider { get; set; }
Action<int>? SelectChildPidAction { get; set; }
Action<int>? FocusChildPidAction { get; set; }
Func<int, bool>? CloseChildPidAction { get; set; }
Action<string, string?, string?>? LaunchAccountAction { get; set; }
Action<string>? LaunchGroupAction { get; set; }
Action? StartAllScriptsAction { get; set; }
Action? StopAllScriptsAction { get; set; }
Action<string>? LoadScriptAllAction { get; set; }
Action<bool>? HeadlessAllAction { get; set; }
Action? ReloginAllAction { get; set; }
Action<string>? BroadcastArmySchedulerAction { get; set; }
Action? StopArmySchedulerAction { get; set; }
Func<List<object>>? TabsInfoProvider { get; set; }
bool IsManagerMode { get; set; }
Func<object>? ArmyQueueProvider { get; set; }
Func<Task<object>>? StartArmyQueueAction { get; set; }
Action? StopArmyQueueAction { get; set; }
Action? PauseResumeArmyQueueAction { get; set; }
Action<int, string?>? SetArmyQueueStepScriptAction { get; set; }
Action<string>? AddArmyQueueStepAction { get; set; }
Action<int>? RemoveArmyQueueStepAction { get; set; }
}
