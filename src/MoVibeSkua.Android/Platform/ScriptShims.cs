namespace MoVibeSkua.Android.Platform;
public static class ScriptShims
{
public const string Source = """
        internal static class OperatingSystem
        {
            public static bool IsWindows() => true;
            public static bool IsWindowsVersionAtLeast(int major, int minor = 0, int build = 0, int revision = 0) => major <= 10;
            public static bool IsOSPlatform(string platform) =>
                platform.Equals("windows", global::System.StringComparison.OrdinalIgnoreCase) || global::System.OperatingSystem.IsOSPlatform(platform);
            public static bool IsOSPlatformVersionAtLeast(string platform, int major, int minor = 0, int build = 0, int revision = 0) =>
                platform.Equals("windows", global::System.StringComparison.OrdinalIgnoreCase)
                    ? IsWindowsVersionAtLeast(major, minor, build, revision)
                    : global::System.OperatingSystem.IsOSPlatformVersionAtLeast(platform, major, minor, build, revision);
            public static bool IsAndroid() => global::System.OperatingSystem.IsAndroid();
            public static bool IsAndroidVersionAtLeast(int major, int minor = 0, int build = 0, int revision = 0) =>
                global::System.OperatingSystem.IsAndroidVersionAtLeast(major, minor, build, revision);
            public static bool IsLinux() => false;
            public static bool IsMacOS() => false;
            public static bool IsMacCatalyst() => false;
            public static bool IsIOS() => false;
            public static bool IsTvOS() => false;
            public static bool IsWatchOS() => false;
            public static bool IsBrowser() => false;
            public static bool IsWasi() => false;
            public static bool IsFreeBSD() => false;
        }
        // Target of every Process.Start(...) call, which the compiler rewrites: web links open in the
        // phone's browser; Windows programs (Explorer on a folder, Notepad, installers) do not exist
        // here, so they are logged and skipped. Null stands for "nothing was started", as it does on Windows.
        internal static class ScriptShell
        {
            public static global::System.Diagnostics.Process Start(string fileName) => Start(fileName, (string)null);
            public static global::System.Diagnostics.Process Start(string fileName, string arguments)
            {
                string file = (fileName ?? string.Empty).Trim().Trim('"');
                string args = (arguments ?? string.Empty).Trim().Trim('"');
                if (IsLink(file))
                    return Open(file);
                if (IsLink(args))
                    return Open(args);
                global::System.Diagnostics.Trace.WriteLine("[Script] not started on a phone: " + global::System.IO.Path.GetFileName(file));
                return null;
            }
            public static global::System.Diagnostics.Process Start(string fileName, global::System.Collections.Generic.IEnumerable<string> arguments) =>
                Start(fileName, arguments is null ? null : string.Join(" ", arguments));
            public static global::System.Diagnostics.Process Start(global::System.Diagnostics.ProcessStartInfo info) =>
                info is null ? null : Start(info.FileName, info.Arguments);
            private static bool IsLink(string text) =>
                text.StartsWith("https://", global::System.StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("http://", global::System.StringComparison.OrdinalIgnoreCase);
            // Target of Path.Combine(CoreBots.ButlerLogDir, ...): Butler's map and order files, lower-cased
            // as CoreBots writes them, so a name typed in another case still finds them on a case-sensitive phone.
            public static string ButlerFile(params string[] parts)
            {
                string path = global::System.IO.Path.Combine(parts);
                string dir = global::System.IO.Path.GetDirectoryName(path);
                string name = global::System.IO.Path.GetFileName(path).ToLowerInvariant();
                return string.IsNullOrEmpty(dir) ? name : global::System.IO.Path.Combine(dir, name);
            }
            // Targets of File.WriteAllText/Lines/Bytes (rewritten by the compiler): the new content goes
            // to a temporary file that is then renamed over the old one, so another client reading at the
            // same moment sees the old file or the new one, never an empty one. Writers also take turns
            // through a lock file, as they do on Windows.
            public static void WriteAllText(string path, string contents) =>
                Replace(path, tmp => global::System.IO.File.WriteAllText(tmp, contents), () => global::System.IO.File.WriteAllText(path, contents));
            public static void WriteAllText(string path, string contents, global::System.Text.Encoding encoding) =>
                Replace(path, tmp => global::System.IO.File.WriteAllText(tmp, contents, encoding), () => global::System.IO.File.WriteAllText(path, contents, encoding));
            public static void WriteAllLines(string path, string[] contents) =>
                Replace(path, tmp => global::System.IO.File.WriteAllLines(tmp, contents), () => global::System.IO.File.WriteAllLines(path, contents));
            public static void WriteAllLines(string path, global::System.Collections.Generic.IEnumerable<string> contents)
            {
                string[] lines = global::System.Linq.Enumerable.ToArray(contents);
                WriteAllLines(path, lines);
            }
            public static void WriteAllLines(string path, string[] contents, global::System.Text.Encoding encoding) =>
                Replace(path, tmp => global::System.IO.File.WriteAllLines(tmp, contents, encoding), () => global::System.IO.File.WriteAllLines(path, contents, encoding));
            public static void WriteAllLines(string path, global::System.Collections.Generic.IEnumerable<string> contents, global::System.Text.Encoding encoding)
            {
                string[] lines = global::System.Linq.Enumerable.ToArray(contents);
                WriteAllLines(path, lines, encoding);
            }
            public static void WriteAllBytes(string path, byte[] bytes) =>
                Replace(path, tmp => global::System.IO.File.WriteAllBytes(tmp, bytes), () => global::System.IO.File.WriteAllBytes(path, bytes));
            private static void Replace(string path, global::System.Action<string> writeTemp, global::System.Action writeDirect)
            {
                string full;
                try { full = global::System.IO.Path.GetFullPath(path); }
                catch { writeDirect(); return; }
                string tmp = full + "." + global::System.Environment.ProcessId + "." + global::System.Environment.CurrentManagedThreadId + ".tmp";
                global::System.IO.FileStream gate = null;
                try
                {
                    // FileShare.None is an exclusive lock between processes on Android too; give up on
                    // the turn after about a second rather than hang the script.
                    for (int i = 0; i < 40 && gate is null; i++)
                    {
                        try { gate = new global::System.IO.FileStream(full + ".lock", global::System.IO.FileMode.OpenOrCreate, global::System.IO.FileAccess.ReadWrite, global::System.IO.FileShare.None); }
                        catch (global::System.IO.IOException) { global::System.Threading.Thread.Sleep(25); }
                        catch { break; }
                    }
                    writeTemp(tmp);
                    global::System.IO.File.Move(tmp, full, true);
                }
                catch (global::System.UnauthorizedAccessException)
                {
                    TryDelete(tmp);
                    writeDirect();
                }
                catch (global::System.IO.IOException)
                {
                    TryDelete(tmp);
                    writeDirect();
                }
                finally
                {
                    gate?.Dispose();
                }
            }
            private static void TryDelete(string file)
            {
                try { global::System.IO.File.Delete(file); } catch { }
            }
            // Target of Environment.MachineName (rewritten by the compiler): during a Run on all, the team's
            // room (VibeSkua/army-room.txt), the same in every client; otherwise the real name.
            public static string MachineName
            {
                get
                {
                    try
                    {
                        string file = global::System.IO.Path.Combine(global::Skua.Core.Models.ClientFileSources.SkuaDIR, "army-room.txt");
                        if (global::System.IO.File.Exists(file))
                        {
                            string room = global::System.IO.File.ReadAllText(file).Trim();
                            if (room.Length > 0)
                                return "MVS-ARMY-" + room;
                        }
                    }
                    catch { }
                    return global::System.Environment.MachineName;
                }
            }
            public static global::System.Diagnostics.Process Open(string target)
            {
                global::Skua.Core.Utils.Link.OpenBrowser((target ?? string.Empty).Trim().Trim('"'));
                return null;
            }
        }
        """;
}
