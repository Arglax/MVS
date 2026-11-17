namespace Skua.Core.Interfaces;
public interface IOptionContainer
{
Dictionary<string, List<IOption>> MultipleOptions { get; }
List<IOption> Options { get; }
string OptionsFile { get; set; }
Dictionary<IOption, string> OptionValues { get; }
IReadOnlyList<string> ResetNotices => Array.Empty<string>();
T? Get<T>(IOption? option) where T : IConvertible;
T? Get<T>(string name) where T : IConvertible;
T? Get<T>(string category, string name) where T : IConvertible;
string GetDirect(IOption? option);
void Load();
void Save();
void Set(string name, object value);
void Set(string category, string name, object value);
void Set<T>(IOption? option, T value);
void SetDefaults();
void Configure();
}
