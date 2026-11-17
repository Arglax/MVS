using Skua.Core.Models.Auras;
namespace Skua.Core.Interfaces;
public interface IUltraBossHelper
{
#region Counter Attack Methods
void EnableCounterAttack();
void DisableCounterAttack();
bool IsCounterAttackEnabled { get; }
bool IsCounterAttackActive { get; }
void SetAttacksStopped(bool shouldStop);
#endregion
#region Aura Analysis Methods
(bool hasPositive, bool hasNegative, bool hasReversed) AnalyzeChargeMechanics(
IScriptSelfAuras auras,
string positiveChargeName,
string negativeChargeName,
string? reversedSuffix = null);
bool CheckAuraThreshold(IScriptSelfAuras auras, string auraName, int threshold, string comparison = ">=");
List<Aura> GetAurasMatchingPattern(List<Aura> auras, string namePattern);
bool ShouldUseSkill(IScriptSelfAuras selfAuras, Dictionary<string, Func<float, bool>> conditions);
bool ShouldUseSkill(IScriptTargetAuras targetAuras, Dictionary<string, Func<float, bool>> conditions);
#endregion
}
