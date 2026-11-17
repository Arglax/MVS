using System.Text.RegularExpressions;
namespace Skua.Core.Utils;
public static class StringUtils
{
public static readonly Regex RemoveLetter = new(@"[^0-9]", RegexOptions.Compiled);
public static readonly Regex Alphabetical = new(@"^[a-zA-Z]+$", RegexOptions.Compiled);
public static string RemoveLetters(this string text)
{
return RemoveLetter.Replace(text, "");
}
public static bool IsAlphabetical(this string text)
{
return Alphabetical.IsMatch(text);
}
public static bool IsNumber(this string text)
{
return !RemoveLetter.IsMatch(text);
}
public static string JoinWithPipeCharacter(this IEnumerable<string> items)
{
return string.Join("|", items);
}
public static string Join(this string[] items, char separator)
{
return string.Join(separator, items);
}
public static string Join(this IEnumerable<string> items, char separator)
{
return string.Join(separator, items);
}
public static string Join(this string[] items, string separator)
{
return string.Join(separator, items);
}
public static string Join(this IEnumerable<string> items, string separator)
{
return string.Join(separator, items);
}
}
