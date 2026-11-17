using System.ComponentModel;
using System.Threading;
namespace Skua.Core.Interfaces;
public interface IScriptManager : IScriptStatus, INotifyPropertyChanged
{
CancellationTokenSource? ScriptCts { get; }
bool ScriptPaused { get; set; }
Task<bool> SetPausedAsync(bool paused);
ManualResetEventSlim ScriptPauseEvent { get; }
IScriptOptionContainer? Config { get; set; }
string? OverrideStorage { get; set; }
bool SilentConfig { get; set; }
Task<Exception?> StartScript();
object? Compile(string source);
void LoadScriptConfig(object? script);
void SetLoadedScript(string path);
}
