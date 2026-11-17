using Skua.Core.Models;
namespace Skua.Core.Interfaces;
public interface IScriptWait
{
int WAIT_SLEEP { get; set; }
bool OverrideTimeout { get; set; }
int PlayerActionTimeout { get; set; }
int DropActionTimeout { get; set; }
int GameActionTimeout { get; set; }
int ItemActionTimeout { get; set; }
int MapActionTimeout { get; set; }
int MonsterActionTimeout { get; set; }
int QuestActionTimeout { get; set; }
bool For(Func<object> function, object value, int timeout = 10);
ValueTask<bool> ForAsync(Func<object> function, object value, int timeout = 10, CancellationToken token = default);
bool ForActionCooldown(GameActions action, int timeout = 40);
bool ForActionCooldown(string action, int timeout = 40);
bool ForBankLoad(int timeout = 20);
bool ForBankToInventory(string name, int timeout = 14);
bool ForBankToInventory(int id, int timeout = 14);
bool ForBankToHouseInventory(string name, int timeout = 14);
bool ForBankToHouseInventory(int id, int timeout = 14);
bool ForCellChange(string name);
bool ForCombatExit(int timeout = 10);
bool ForDrop(string name, int timeout = 10);
bool ForDrop(int id, int timeout = 10);
bool ForFullyRested(int timeout = -1);
bool ForInventoryToBank(string name, int timeout = 14);
bool ForInventoryToBank(int id, int timeout = 14);
bool ForHouseInventoryToBank(string name, int timeout = 14);
bool ForHouseInventoryToBank(int id, int timeout = 14);
bool ForItemBuy(int timeout = 10);
bool ForItemEquip(string name, int timeout = 10);
bool ForItemEquip(int id, int timeout = 10);
bool ForItemSell(int timeout = 10);
bool ForMapLoad(string name, int timeout = 20);
bool ForMonsterDeath(int timeout = -1);
bool ForMonsterSpawn(string name, int timeout = 10);
bool ForMonsterSpawn(int id, int timeout = 10);
bool ForPickup(string name, int timeout = 10);
bool ForPickup(int id, int timeout = 10);
bool ForPlayerPosition(int x, int y, int timeout = 10);
bool ForQuestAccept(int id, int timeout = 14);
bool ForQuestComplete(int id, int timeout = 10);
bool ForQuestLoad(int id, int timeout = 20);
bool ForQuestLoad(int startId, int endId, int timeout = 20);
bool ForSkillCooldown(int index, int timeout = 50);
bool ForTrue(Func<bool> predicate, Action? loopFunction, int timeout, int sleepOverride = -1);
ValueTask<bool> ForTrueAsync(Func<bool> predicate, Action? loopFunction, int timeout, int sleepOverride = -1, CancellationToken token = default);
bool ForTrue(Func<bool> predicate, int timeout, int sleepOverride = -1);
ValueTask<bool> ForTrueAsync(Func<bool> predicate, int timeout, int sleepOverride = -1, CancellationToken token = default);
bool ForTrue(Func<bool> predicate, Action? loopFunction, int sleepOverride, CancellationToken token);
bool IsActionAvailable(GameActions action);
bool IsActionAvailable(string action);
}
