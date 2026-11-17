using Skua.Core.Models;
using Skua.Core.Models.Monsters;
namespace Skua.Core.Interfaces;
public interface IScriptHunt
{
void Monster(string name);
void Monster(string name, CancellationToken? token);
void Monster(int id);
void Monster(int id, CancellationToken? token);
void Monster(Monster monster);
void Monster(Monster monster, CancellationToken? token);
void ForItem(string name, string item, int quantity, bool tempItem = false);
void ForItem(IEnumerable<string> names, string item, int quantity, bool tempItem = false);
void ForItems(string name, IEnumerable<string> items, IEnumerable<int> quantities, bool tempItems = false);
void ForItems(string name, IEnumerable<string> items, IEnumerable<int> quantities, IEnumerable<bool> tempItems);
void ForItems(IEnumerable<string> names, IEnumerable<string> items, IEnumerable<int> quantities, bool tempItems = false);
void ForItems(IEnumerable<string> names, IEnumerable<string> items, IEnumerable<int> quantities, IEnumerable<bool> tempItems);
void WithPriority(string name, HuntPriorities priority);
void WithPriority(string name, HuntPriorities priority, CancellationToken? token);
void WithPriority(Monster monster, HuntPriorities priority);
void WithPriority(Monster monster, HuntPriorities priority, CancellationToken? token);
void WithPriority(int id, HuntPriorities priority);
void WithPriority(int id, HuntPriorities priority, CancellationToken? token);
}
