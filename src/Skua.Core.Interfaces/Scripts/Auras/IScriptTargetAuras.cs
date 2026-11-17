namespace Skua.Core.Interfaces;
public interface IScriptTargetAuras : IScriptAuras
{
string GetMonsterAura(string monsterName);
string GetMonsterAura(int monID);
}
