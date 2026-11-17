namespace Skua.Core.Interfaces;
public interface IScriptStatus
{
bool ShouldExit { get; }
bool ScriptRunning { get; }
string LoadedScript { get; }
string CompiledScript { get; }
IScriptOptionContainer? Config { get; set; }
IScriptOptionContainer? RunningConfig { get; }
Task RestartScriptAsync();
Task<Exception?> StartScript();
void SetLoadedScript(string path);
ValueTask StopScript(bool runScriptStoppingEvent = true);
}
