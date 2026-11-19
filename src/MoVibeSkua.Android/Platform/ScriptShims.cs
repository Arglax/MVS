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
            public static global::System.Diagnostics.Process Open(string target)
            {
                global::Skua.Core.Utils.Link.OpenBrowser((target ?? string.Empty).Trim().Trim('"'));
                return null;
            }
        }
        """;
}
