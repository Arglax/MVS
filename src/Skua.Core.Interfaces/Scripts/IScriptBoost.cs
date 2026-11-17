using Skua.Core.Models.Items;
using System.ComponentModel;
namespace Skua.Core.Interfaces;
public interface IScriptBoost : INotifyPropertyChanged
{
bool Enabled { get; }
int ClassBoostID { get; set; }
int ExperienceBoostID { get; set; }
int GoldBoostID { get; set; }
int ReputationBoostID { get; set; }
bool UseClassBoost { get; set; }
bool UseExperienceBoost { get; set; }
bool UseGoldBoost { get; set; }
bool UseReputationBoost { get; set; }
bool UsingBoosts => UseClassBoost || UseExperienceBoost || UseGoldBoost || UseReputationBoost;
int GetBoostID(BoostType boostType, bool searchBank = true);
bool IsBoostActive(BoostType boost);
void SetAllBoostsIDs()
{
SetClassBoostID();
SetGoldBoostID();
SetExperienceBoostID();
SetReputationBoostID();
}
void SetAllBoostsIDs(bool searchBank)
{
SetClassBoostID(searchBank);
SetGoldBoostID(searchBank);
SetExperienceBoostID(searchBank);
SetReputationBoostID(searchBank);
}
void SetClassBoostID(bool searchBank = true)
{
ClassBoostID = GetBoostID(BoostType.Class, searchBank);
}
void SetExperienceBoostID(bool searchBank = true)
{
ExperienceBoostID = GetBoostID(BoostType.Experience, searchBank);
}
void SetGoldBoostID(bool searchBank = true)
{
GoldBoostID = GetBoostID(BoostType.Gold, searchBank);
}
void SetReputationBoostID(bool searchBank = true)
{
ReputationBoostID = GetBoostID(BoostType.Reputation, searchBank);
}
void Start();
void Stop();
ValueTask StopAsync();
void UseBoost(int id);
}
