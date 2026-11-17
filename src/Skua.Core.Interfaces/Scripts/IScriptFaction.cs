using Skua.Core.Models.Factions;
namespace Skua.Core.Interfaces;
public interface IScriptFaction
{
List<Faction> FactionList { get; }
int GetRank(string name)
{
return FactionList.FirstOrDefault(f => f.Name == name)?.Rank ?? 0;
}
int GetRank(int id)
{
return FactionList.FirstOrDefault(f => f.ID == id)?.Rank ?? 0;
}
bool HasRank(string name, int rank = 1)
{
return GetRank(name) >= rank;
}
bool HasRank(int id, int rank = 1)
{
return GetRank(id) >= rank;
}
}
