using Skua.Core.Models.Auras;
namespace Skua.Core.Interfaces;
public interface IScriptAuras
{
List<Aura> Auras { get; }
List<AuraSnapshot> Snapshots { get; }
AuraSnapshot? GetAuraSnapshot(string auraName);
int GetAuraStacks(string auraName);
bool HasActiveAura(string auraName);
Aura? GetAura(string auraName);
float GetAuraValue(string auraName);
bool HasAnyActiveAura(params string[] auraNames);
bool TryGetAura(string auraName, out Aura? aura)
{
if (HasActiveAura(auraName))
{
aura = GetAura(auraName);
return true;
}
aura = null;
return false;
}
}
