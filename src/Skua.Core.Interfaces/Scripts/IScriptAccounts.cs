namespace Skua.Core.Interfaces;
public record ManagedAccount(string Username, string Password, string DisplayName, List<string> Tags);
public interface IScriptAccounts
{
List<string> GetTags();
List<string> GetTags(string username);
bool HasTag(string tag);
bool HasTag(string username, string tag);
bool AddTag(string tag);
bool AddTag(string username, string tag);
bool RemoveTag(string tag);
bool RemoveTag(string username, string tag);
void AddTags(params string[] tags);
bool AddTags(string username, params string[] tags);
void RemoveTags(params string[] tags);
bool RemoveTags(string username, params string[] tags);
void SetTags(params string[] tags);
bool SetTags(string username, params string[] tags);
void ClearTags();
bool ClearTags(string username);
List<ManagedAccount> GetAllAccounts();
}
