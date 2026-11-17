using Skua.Core.Models.Skills;
using System.ComponentModel;
namespace Skua.Core.Interfaces;
public interface IScriptAuto : INotifyPropertyChanged
{
bool IsRunning { get; }
bool IsHunting { get; }
string? TargetInfo { get; }
void StartAutoAttack(string? className = null, ClassUseMode classUseMode = ClassUseMode.Base, int[]? manualMapIDs = null);
void StartAutoHunt(string? className = null, ClassUseMode classUseMode = ClassUseMode.Base, int[]? manualMapIDs = null);
void Stop();
ValueTask StopAsync();
}
