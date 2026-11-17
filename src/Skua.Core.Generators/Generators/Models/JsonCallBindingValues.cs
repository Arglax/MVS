namespace Skua.Core.Generators.Models;
public record struct JsonCallBindingValues(
string FunctionName,
string? Default,
bool Get,
bool HasSetter);
