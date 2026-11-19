using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Threading;
using MoVibeSkua.Android.Views;
namespace MoVibeSkua.Android.Diagnostics;
public static class SelfTest
{
public static void RunIfAsked()
{
_ = Task.Run(async () =>
{
try
{
string mode = ReadProperty("debug.mvs.selftest");
if (mode.Length == 0)
return;
if (mode == "script")
PlantForeignPlugin();
for (int i = 0; i < 60 && MobileShellView.Instance is null; i++)
await Task.Delay(1000);
await Task.Delay(6000);
Trace.WriteLine($"[SelfTest] running '{mode}'");
switch (mode)
{
case "ui":
Dispatcher.UIThread.Post(() => throw new InvalidOperationException("MVS self-test: error on the UI thread"));
break;
case "android":
new global::Android.OS.Handler(global::Android.OS.Looper.MainLooper!).Post(
() => throw new InvalidOperationException("MVS self-test: error in an Android callback"));
break;
case "engine":
ProblemReports.SaveEngine("30|400\nRuntimeError: MVS self-test: engine fault\n    at self-test");
break;
case "popup-engine":
CrashPrompt.ShowSample("engine", "Game engine crashed: Uncaught RuntimeError: memory access out of bounds", "DiamondTokenofDage");
break;
case "popup-guard":
CrashPrompt.ShowSample("problem", "MVS kept running after an error (System.ArgumentException: -80 is not a valid value for 'Width'.)", string.Empty);
break;
case "popup-memory":
CrashPrompt.ShowSample("crash", "MVS closed unexpectedly: closed by Android for memory", string.Empty);
break;
case "script":
await RunUnofficialScriptAsync();
break;
case "script-official":
{
await Task.Delay(60000);
string official = System.IO.Path.Combine(Skua.Core.Models.ClientFileSources.SkuaScriptsDIR, "Templates", "Examples", "ItemGathering", "EmptyTemplate[Basics].cs");
Skua.Core.Interfaces.IScriptManager scripts =
CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetRequiredService<Skua.Core.Interfaces.IScriptManager>();
scripts.SetLoadedScript(official);
Exception? startError = await scripts.StartScript();
Trace.WriteLine($"[SelfTest] official script start: {(startError is null ? "started" : startError.GetType().Name + ": " + startError.Message)}");
break;
}
case "flood":
for (int i = 0; i < 8; i++)
{
int n = i;
Dispatcher.UIThread.Post(() => throw new InvalidOperationException($"MVS self-test: flood {n}"));
}
break;
}
await Task.Delay(3000);
Trace.WriteLine("[SelfTest] still running after the test");
}
catch (Exception ex)
{
Trace.WriteLine($"[SelfTest] {ex.Message}");
}
});
}
private const string ForeignPlugin = "selftest-foreign.dll";
private static void PlantForeignPlugin()
{
try
{
string folder = Skua.Core.Models.ClientFileSources.SkuaPluginsDIR;
System.IO.Directory.CreateDirectory(folder);
System.IO.File.WriteAllText(System.IO.Path.Combine(folder, ForeignPlugin), "MVS self-test: not a plugin");
Trace.WriteLine("[SelfTest] foreign plugin planted");
}
catch (Exception ex)
{
Trace.WriteLine($"[SelfTest] plugin plant failed: {ex.Message}");
}
}
private static async Task RunUnofficialScriptAsync()
{
string folder = System.IO.Path.Combine(Skua.Core.Models.ClientFileSources.SkuaDIR, "selftest");
string file = System.IO.Path.Combine(folder, "MvsSelfTest.cs");
try
{
System.IO.Directory.CreateDirectory(folder);
System.IO.File.WriteAllText(file, UnofficialScript);
Skua.Core.Interfaces.IScriptManager manager =
CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetRequiredService<Skua.Core.Interfaces.IScriptManager>();
manager.SetLoadedScript(file);
Exception? error = await manager.StartScript();
Trace.WriteLine($"[SelfTest] script start: {(error is null ? "started" : error.GetType().Name + ": " + error.Message)}");
}
finally
{
await Task.Delay(8000);
try { System.IO.Directory.Delete(folder, true); } catch { }
try { System.IO.File.Delete(System.IO.Path.Combine(Skua.Core.Models.ClientFileSources.SkuaPluginsDIR, ForeignPlugin)); } catch { }
Trace.WriteLine("[SelfTest] test files removed");
}
}
private const string UnofficialScript = """
        using Skua.Core.Interfaces;
        using Skua.Core.ViewModels;
        public class MvsSelfTest
        {
            // Words the question should pick up: HttpClient, File.WriteAllText
            public void ScriptMain(IScriptInterface bot)
            {
                bot.Log("MVS self-test: input dialog is " + typeof(InputDialogViewModel).BaseType?.FullName);
                System.Diagnostics.Process started = System.Diagnostics.Process.Start("notepad.exe", "selftest.txt");
                bot.Log("MVS self-test: notepad " + (started is null ? "skipped" : "started"));
                System.Diagnostics.Process info = System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo("msiexec", "/i nothing.msi") { UseShellExecute = true });
                bot.Log("MVS self-test: installer " + (info is null ? "skipped" : "started"));
            }
        }
        """;
public static string EngineMemory() => ReadProperty("debug.mvs.enginemem");
private static string ReadProperty(string name)
{
try
{
using Java.Lang.Process? process = Java.Lang.Runtime.GetRuntime()?.Exec(new[] { "getprop", name });
if (process?.InputStream is null)
return string.Empty;
using System.IO.StreamReader reader = new(process.InputStream);
return reader.ReadToEnd().Trim();
}
catch
{
return string.Empty;
}
}
}
