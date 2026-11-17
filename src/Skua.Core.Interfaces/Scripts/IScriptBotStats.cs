using System.ComponentModel;
namespace Skua.Core.Interfaces;
public interface IScriptBotStats : INotifyPropertyChanged
{
int Deaths { get; set; }
int Drops { get; set; }
long Gold { get; set; }
long Experience { get; set; }
int Kills { get; set; }
int QuestsAccepted { get; set; }
int QuestsCompleted { get; set; }
int Relogins { get; set; }
int InventorySpace { get; }
int InventoryFilledSpace { get; }
int InventoryFreeSpace { get; }
int BankSpace { get; }
int BankFilledSpace { get; }
int BankFreeSpace { get; }
TimeSpan Time { get; }
TimeSpan SessionTime { get; }
double ExperienceProgress { get; }
string ExperienceProgressText { get; }
bool IsMaxLevel { get; }
bool IsLevelProgressVisible { get; }
double ClassRank10Progress { get; }
string ClassRank10ProgressText { get; }
int ExperiencePerHour { get; }
long GoldPerHour { get; }
double KillsPerMinute { get; }
double DeathsPerMinute { get; }
int ItemsPerHour { get; }
int CurrentClassPoints { get; }
int CurrentClassRank { get; }
bool IsMaxClassRank { get; }
bool IsClassRankProgressVisible { get; }
int ClassPointsPerHour { get; }
TimeSpan? TimeToLevel { get; }
TimeSpan? TimeToClassRank10 { get; }
void RecordItemPickup(int quantity);
void UpdateGameplaySnapshot(int experience, int requiredExperience, int level, int gold, int classId, int classPoints, int classPointsToRank, int classRank, bool connected);
void Reset();
void GetSpace();
}
