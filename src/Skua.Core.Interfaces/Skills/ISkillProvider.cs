namespace Skua.Core.Interfaces;
public interface ISkillProvider
{
int SkillCount { get; }
bool ResetOnTarget { get; set; }
bool? ShouldUseSkill(int skillIndex, bool canUse);
(int, int) GetNextSkill();
bool OnTargetReset();
void OnPlayerDeath();
void Stop();
void Load(string file);
void Save(string file);
}
