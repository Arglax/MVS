namespace Skua.Core.Interfaces.Services;
public interface IScriptWatchdogService : IDisposable
{
bool IsRunning { get; }
int SecondsSinceLastProgress { get; }
int CurrentTier { get; }
void Heartbeat();
void Start();
void Stop();
void Reset();
}
