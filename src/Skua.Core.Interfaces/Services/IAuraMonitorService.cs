using Skua.Core.Models.Auras;
namespace Skua.Core.Interfaces.Services;
public interface IAuraMonitorService : IDisposable
{
bool IsMonitoring { get; }
int SubscriberCount { get; }
event Action<string, DateTimeOffset, float, float, SubjectType>? AuraActivated;
event Action<string, SubjectType>? AuraDeactivated;
event Action<string, float, float, SubjectType>? AuraStackChanged;
void EnsureMonitoring(int pollIntervalMs = 100);
void StartMonitoring(int pollIntervalMs = 100);
void StopMonitoring();
}
