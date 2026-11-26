using Skua.Core.Models.Monsters;
namespace Skua.Core.Interfaces;
public interface IScriptCombat
{
bool EnableCounterHandler { get; set; }
bool StopAttacking { get; set; }
void ApproachTarget();
bool Attack(int id);
bool Attack(Monster monster)
{
return Attack(monster.MapID);
}
bool Attack(string name);
bool SelectMonster(int id);
bool SelectMonster(Monster monster)
{
return SelectMonster(monster.MapID);
}
bool SelectMonster(string name);
bool AttackPlayer(string name);
void CancelAutoAttack();
void CancelTarget();
void Exit();
void UntargetSelf();
}
