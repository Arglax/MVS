namespace Skua.Core.Models;
public record DialogResult(string Text, int Value)
{
public static DialogResult Cancelled { get; } = new("Cancel", -1);
}
