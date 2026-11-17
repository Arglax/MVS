namespace Skua.Core.Generators.Models;
public record struct ObjectBindingValues(
string[] Paths,
bool Get,
bool Set,
string? Select,
string? RequireNotNull,
string? Default,
bool IsStatic,
bool HasSetter);
