using Skua.Core.Models.Skills;
using System.ComponentModel;
namespace Skua.Core.Interfaces;
public interface IAdvancedSkillContainer : INotifyPropertyChanged
{
List<AdvancedSkill> LoadedSkills { get; set; }
void Add(AdvancedSkill skill);
void TryOverride(AdvancedSkill skill);
void LoadSkills();
void ResetSkillsSets();
void SyncSkills();
void Remove(AdvancedSkill skill);
void Save();
Dictionary<string, List<string>> GetAvailableClassModes();
AdvancedSkill? GetClassModeSkills(string className, string mode);
}
