using Android.Content;
namespace MoVibeSkua.Android.Platform;
public static class DevMode
{
private const string Key = "devMode";
private static ISharedPreferences? Prefs =>
global::Android.App.Application.Context.GetSharedPreferences("movibeskua.shell", FileCreationMode.Private);
public static bool IsOn => Prefs?.GetBoolean(Key, false) ?? false;
public static bool Toggle()
{
bool on = !IsOn;
Prefs?.Edit()?.PutBoolean(Key, on)?.Apply();
System.Diagnostics.Trace.WriteLine($"[App] developer mode {(on ? "on" : "off")}");
return on;
}
}
