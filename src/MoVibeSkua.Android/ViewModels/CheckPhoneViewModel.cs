using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MoVibeSkua.Android.Platform;
namespace MoVibeSkua.Android.ViewModels;
public sealed record CheckRow(string Name, string Me, string MeSmall, string Min, string Rec, string RecSmall, int Verdict, double Pct, string Short);
public sealed class CheckPhoneViewModel
{
private const int MinSdk = 26, RecSdk = 31;
private const int MinRamGb = 4, RecRamGb = 8;
private const int MinFreeMb = 500, RecFreeMb = 1024;
private const int WebViewOldMonths = 36, WebViewFreshMonths = 12;
private static readonly int[] RamSizes = { 1, 2, 3, 4, 6, 8, 12, 16, 18, 24, 32 };
public const string Unread = "Could not be read";
public string Model { get; private set; } = string.Empty;
public IReadOnlyList<CheckRow> Rows { get; private set; } = Array.Empty<CheckRow>();
public int Overall { get; private set; } = -1;
public CheckRow? Tightest { get; private set; }
public Task Load() => Task.Run(() =>
{
PhoneFacts f = PhoneFacts.Read();
Model = f.Model;
Rows = Build(f);
List<CheckRow> read = Rows.Where(r => r.Verdict >= 0).ToList();
Overall = read.Count == 0 ? -1 : read.Min(r => r.Verdict);
Tightest = read.Where(r => r.Pct > 0).OrderBy(r => r.Pct).FirstOrDefault();
});
private static List<CheckRow> Build(PhoneFacts f)
{
List<CheckRow> rows = new();
rows.Add(new CheckRow("Android", f.Android.Length > 0 ? "Android " + f.Android : Unread, "", "8.0", "12 or newer", "",
f.Sdk <= 0 ? -1 : f.Sdk < MinSdk ? 0 : f.Sdk < RecSdk ? 1 : 2, 0, "Android"));
int gb = f.RamMb <= 0 ? 0 : RamSizes.FirstOrDefault(s => s * 1024 >= f.RamMb, (int)Math.Ceiling(f.RamMb / 1024.0));
rows.Add(new CheckRow("[ram|RAM]", gb > 0 ? gb + " GB" : Unread, "", MinRamGb + " GB", RecRamGb + " GB or more",
"About 1 GB more for each extra Army client",
gb <= 0 ? -1 : gb < MinRamGb ? 0 : gb < RecRamGb ? 1 : 2, gb > 0 ? (double)gb / RecRamGb : 0, "RAM"));
double ghz = f.MaxKhz / 1e6;
string bits = f.Is64 == true ? (f.IsArm ? "64-bit ARM" : "64-bit") : f.Is64 == false ? "32-bit" : "";
string cpu = string.Join(", ", new[] { bits, f.Cores > 0 ? f.Cores + " cores" : "", ghz > 0 ? ghz.ToString("0.00") + " GHz" : "" }.Where(s => s.Length > 0));
rows.Add(new CheckRow("[chip|Processor]", cpu.Length > 0 ? cpu : Unread, f.Soc.Length > 0 ? "Chip: " + f.Soc : "",
"64-bit ARM", "64-bit ARM", "", f.Is64 is null ? -1 : f.Is64 == true ? 2 : 0, 0, "processor"));
rows.Add(new CheckRow("Free storage", f.FreeMb >= 0 ? Size(f.FreeMb) : Unread, "", "500 MB", "1 GB", "",
f.FreeMb < 0 ? -1 : f.FreeMb < MinFreeMb ? 0 : f.FreeMb < RecFreeMb ? 1 : 2, f.FreeMb >= 0 ? (double)f.FreeMb / RecFreeMb : 0, "free storage"));
int months = WebViewMonths(f.WebViewMajor);
string age = months < 1 ? "this month" : months < 24 ? $"about {months} month{(months == 1 ? "" : "s")} old" : $"about {months / 12} years old";
rows.Add(new CheckRow("[webview|System WebView]", f.WebViewMajor > 0 ? "Version " + f.WebViewMajor : Unread, f.WebViewMajor > 0 ? age : "",
"From the last 3 years", "Up to date (last 12 months)", "",
f.WebViewMajor <= 0 ? -1 : months > WebViewOldMonths ? 0 : months > WebViewFreshMonths ? 1 : 2, 0, "System WebView"));
return rows;
}
private static string Size(long mb) => mb >= 1024 ? (mb / 1024.0).ToString(mb >= 10240 ? "0" : "0.0") + " GB" : mb + " MB";
private static int WebViewMonths(int major)
{
DateTime now = DateTime.Now;
return Math.Max(0, (now.Year - 2024) * 12 + (now.Month - 10) - (major - 130));
}
public static string Percent(double pct)
{
int n = (int)Math.Round(pct * 100);
return n > 999 ? "over 999%" : n + "%";
}
}
