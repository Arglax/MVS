using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Skua.Core.Interfaces;
using Skua.Core.Messaging;
using Skua.Core.Models;
using Skua.Core.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
namespace Skua.App.Avalonia.ViewModels.AppLogs;
public partial class LogTabViewModel : ObservableRecipient
{
public LogTabViewModel(string title, ILogService logService, IDispatcherService dispatcherService, LogType logType)
{
Messenger.Register<LogTabViewModel, AddLogMessage>(this, AddLog);
Title = title;
_logService = logService;
_dispatcherService = dispatcherService;
_logType = logType;
Logs.AddRange(logService.GetLogs(logType).Select(Skua.Core.Scripts.ScriptOption.Mask));
}
private readonly ILogService _logService;
private readonly IDispatcherService _dispatcherService;
private readonly LogType _logType;
public string Title { get; }
public RangedObservableCollection<string> Logs { get; } = new();
[RelayCommand]
private void SaveLog()
{
Ioc.Default.GetRequiredService<IFileDialogService>().SaveText(LogsToString(Logs));
}
[RelayCommand]
private void ClearLog()
{
_logService.ClearLog(_logType);
Logs.Clear();
}
[RelayCommand]
private void CopyLog()
{
Ioc.Default.GetRequiredService<IClipboardService>().SetText(LogsToString(Logs));
}
private const int MaxVisibleLines = 2000;
private const int FlushIntervalMs = 200;
private readonly System.Collections.Concurrent.ConcurrentQueue<string> _pending = new();
private int _flushScheduled;
private void AddLog(LogTabViewModel recipient, AddLogMessage message)
{
if (message.LogType != recipient._logType)
return;
recipient._pending.Enqueue(message.Text);
if (System.Threading.Interlocked.Exchange(ref recipient._flushScheduled, 1) == 0)
_ = System.Threading.Tasks.Task.Delay(FlushIntervalMs).ContinueWith(_ => recipient._dispatcherService.Invoke(recipient.FlushPending));
}
private void FlushPending()
{
System.Threading.Interlocked.Exchange(ref _flushScheduled, 0);
List<string> batch = new();
while (_pending.TryDequeue(out string? line))
batch.Add(Skua.Core.Scripts.ScriptOption.Mask(line));
if (batch.Count == 0)
return;
if (batch.Count > MaxVisibleLines)
batch.RemoveRange(0, batch.Count - MaxVisibleLines);
foreach (string line in batch)
Logs.Add(line);
while (Logs.Count > MaxVisibleLines)
Logs.RemoveAt(0);
}
private static string LogsToString(IEnumerable<string> logs)
{
return string.Join(Environment.NewLine, logs);
}
}
