namespace Skua.Shared.Avalonia;
public static class Platform
{
public static bool IsDesktop => !OperatingSystem.IsAndroid();
}
