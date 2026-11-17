namespace Skua.Core.Generators.Models;
public record struct CallBindingValues(
string Path,
string? Default,
bool UseValue,
bool Get,
bool Set,
bool HasSetter);
