using Skua.Core.Models.Quests;
using System.ComponentModel;
namespace Skua.Core.Interfaces;
public interface IScriptQuest : INotifyPropertyChanged
{
int RegisterCompleteInterval { get; set; }
List<Quest> Active { get; }
List<Quest> Completed { get; }
List<Quest> Tree { get; }
Dictionary<int, QuestData> CachedDictionary { get; set; }
List<QuestData> Cached { get; set; }
IEnumerable<int> Registered { get; }
IReadOnlyDictionary<int, int> RegisteredRewards { get; }
void RegisterQuests(params int[] ids);
void RegisterQuests(params (int questId, int rewardId)[] quests);
void UnregisterQuests(params int[] ids);
void UnregisterAllQuests();
bool Accept(int id);
void Accept(params int[] ids);
bool EnsureAccept(int id);
void EnsureAccept(params int[] ids);
bool CanComplete(int id);
bool CanCompleteFullCheck(int id);
bool Complete(int id, int itemId = -1, bool special = false);
void Complete(params int[] ids);
bool EnsureComplete(int id, int itemId = -1, bool special = false);
void EnsureComplete(params int[] ids);
bool IsAvailable(int id);
bool IsDailyComplete(int id);
bool IsDailyComplete(Quest quest);
bool IsInProgress(int id);
bool IsUnlocked(int id);
bool IsUnlocked(Quest quest);
bool HasBeenCompleted(int id);
bool HasBeenCompleted(Quest quest);
void Load(params int[] ids);
Quest? EnsureLoad(int id);
bool TryGetQuest(int id, out Quest? quest);
bool UpdateQuest(int id);
void UpdateQuest(int value, int slot);
void LoadCachedQuests();
List<QuestData> GetCachedQuests(int start, int count);
List<QuestData> GetCachedQuests(params int[] ids);
}
