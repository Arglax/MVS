namespace Skua.Core.Generators;
public record struct MethodCallBindingValues(
string Path,
string? Default,
bool RunMethodPre,
bool RunMethodPost,
bool GameFunction,
bool ParseFromJson);
