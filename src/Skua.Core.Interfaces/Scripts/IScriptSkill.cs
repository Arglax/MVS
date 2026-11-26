using Skua.Core.Models.Skills;
namespace Skua.Core.Interfaces;
public interface IScriptSkill
{
ISkillProvider BaseProvider { get; }
ISkillProvider? OverrideProvider { get; set; }
int SkillTimeout { get; set; }
int SkillInterval { get; set; }
bool TimerRunning { get; }
SkillUseMode SkillUseMode { get; set; }
bool CanUseSkill(int index);
bool UseSkill(int index);
void Resume();
void Pause();
void LoadAdvanced(string skills, int skillTimeout = -1, SkillUseMode skillMode = SkillUseMode.UseIfAvailable);
void LoadAdvanced(string className, bool autoEquip, ClassUseMode useMode = ClassUseMode.Base);
void LoadAdvanced(string className, string mode, bool autoEquip = true);
void StartAdvanced(string className, string mode, bool autoEquip = true)
{
LoadAdvanced(className, mode, autoEquip);
Start();
}
void SetProvider(ISkillProvider provider);
void StartAdvanced(string skills, int skillTimeout = -1, SkillUseMode skillMode = SkillUseMode.UseIfAvailable)
{
LoadAdvanced(skills, skillTimeout, skillMode);
Start();
}
void StartAdvanced(string className, bool autoEquip, ClassUseMode useMode = ClassUseMode.Base)
{
LoadAdvanced(className, autoEquip, useMode);
Start();
}
void Start();
void Stop();
void StartSkills() => Start();
void StopSkills() => Stop();
void UseBaseProvider();
}
