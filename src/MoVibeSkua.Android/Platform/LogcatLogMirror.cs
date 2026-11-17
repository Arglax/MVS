using CommunityToolkit.Mvvm.Messaging;
using Skua.Core.Messaging;
using Skua.Core.Models;
namespace MoVibeSkua.Android.Platform;
public sealed class LogcatLogMirror : IRecipient<AddLogMessage>
{
private const string Tag = "MoVibeSkua";
private static readonly LogcatLogMirror Instance = new();
public static void Start()
{
if (!WeakReferenceMessenger.Default.IsRegistered<AddLogMessage>(Instance))
WeakReferenceMessenger.Default.Register(Instance);
}
public void Receive(AddLogMessage message)
{
if (message.LogType != LogType.Debug && !string.IsNullOrEmpty(message.Text))
{
global::Android.Util.Log.Info(Tag, $"[{message.LogType}] {message.Text}");
Diagnostics.AppLog.Add($"[{message.LogType}] {message.Text}");
}
}
}
