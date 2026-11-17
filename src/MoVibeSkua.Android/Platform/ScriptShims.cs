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
        """;
}
