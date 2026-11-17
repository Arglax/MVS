using Skua.Core.Models.Monsters;
namespace Skua.Core.Interfaces;
public interface IScriptMonster
{
List<Monster> CurrentMonsters { get; }
List<Monster> MapMonsters { get; }
List<Monster> CurrentAvailableMonsters { get; }
bool Exists(string name)
{
return CurrentAvailableMonsters.Any(m => name == "*" || (m.Name.Trim() == name.Trim()));
}
bool Exists(int id)
{
return CurrentAvailableMonsters.Any(m => m.ID == id || m.MapID == id);
}
Dictionary<string, List<Monster>> GetCellMonsters();
List<string> GetLivingMonsterCells(string name)
{
try
{
return MapMonsters.Where(m => m.Alive && (name == "*" || m.Name.Trim() == name.Trim())).Select(m => m.Cell).Distinct().ToList();
}
catch
{
return new List<string>();
}
}
List<string> GetLivingMonsterCells(int id)
{
try
{
return MapMonsters.Where(m => m.Alive && m.ID == id).Select(m => m.Cell).Distinct().ToList();
}
catch
{
return new List<string>();
}
}
List<string> GetLivingMonsterDataLeafCells(string name)
{
if (name == "*")
return MapMonsters.Where(m => m.Alive).Select(m => m.Cell).Distinct().ToList();
if (TryGetMonster(name, out Monster? monster) && monster != null)
{
try
{
return MapMonsters.Where(m => m.Alive && m.ID == monster.ID).Select(m => m.Cell).Distinct().ToList();
}
catch
{
return new List<string>();
}
}
return new List<string>();
}
List<string> GetLivingMonsterDataLeafCells(int id)
{
try
{
return MapMonsters.Where(m => m.Alive && m.ID == id).Select(m => m.Cell).Distinct().ToList();
}
catch
{
return new List<string>();
}
}
List<string> GetMonsterCells(string name)
{
try
{
return MapMonsters.Where(m => m.Name.Trim() == name.Trim()).Select(m => m.Cell).Distinct().ToList();
}
catch
{
return new List<string>();
}
}
List<string> GetMonsterCells(int id)
{
try
{
return MapMonsters.Where(m => m.ID == id).Select(m => m.Cell).Distinct().ToList();
}
catch
{
return new List<string>();
}
}
List<Monster> GetMonstersByCell(string cell)
{
return MapMonsters.FindAll(x => x.Cell == cell);
}
bool TryGetMonster(string name, out Monster? monster)
{
return (monster = MapMonsters.Find(m => name == "*" || m.Name.Trim() == name.Trim())) is not null;
}
bool TryGetMonster(int id, out Monster? monster)
{
return (monster = MapMonsters.Find(m => m.ID == id)) is not null;
}
}
