using System.Diagnostics;
namespace MoVibeSkua.Android.Platform;
public sealed class LogcatTraceListener : TraceListener
{
private const string Tag = "MoVibeSkua";
public override void Write(string? message)
{
if (!string.IsNullOrEmpty(message))
global::Android.Util.Log.Info(Tag, message);
}
public override void WriteLine(string? message) => Write(message);
}
