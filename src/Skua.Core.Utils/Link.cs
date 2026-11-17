using System.Diagnostics;
namespace Skua.Core.Utils;
public class Link
{
public static Action<string>? Opener { get; set; }
public static void OpenBrowser(string link)
{
if (Opener is not null)
{
Opener(link);
return;
}
ProcessStartInfo ps = new("explorer", link)
{
UseShellExecute = true,
Verb = "open"
};
Process.Start(ps);
}
}
