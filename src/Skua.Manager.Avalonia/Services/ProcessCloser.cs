using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
namespace Skua.Manager.Avalonia.Services;
internal static class ProcessCloser
{
public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
private const uint WM_CLOSE = 0x0010;
private const uint GW_OWNER = 4;
private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
[DllImport("user32.dll")]
private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
[DllImport("user32.dll")]
private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
[DllImport("user32.dll")]
private static extern bool IsWindowVisible(IntPtr hWnd);
[DllImport("user32.dll")]
private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
[DllImport("user32.dll")]
private static extern int GetWindowTextLength(IntPtr hWnd);
[DllImport("user32.dll")]
private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
public static bool RequestClose(Process process)
{
uint pid = (uint)process.Id;
bool posted = false;
EnumWindows((hWnd, _) =>
{
GetWindowThreadProcessId(hWnd, out uint windowPid);
if (windowPid == pid
&& IsWindowVisible(hWnd)
&& GetWindow(hWnd, GW_OWNER) == IntPtr.Zero
&& GetWindowTextLength(hWnd) > 0)
{
posted |= PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
}
return true;
}, IntPtr.Zero);
return posted;
}
public static async Task CloseAsync(Process process, TimeSpan timeout)
{
if (process.HasExited)
return;
if (RequestClose(process))
{
using CancellationTokenSource cts = new(timeout);
try
{
await process.WaitForExitAsync(cts.Token);
return;
}
catch (OperationCanceledException)
{
Trace.WriteLine($"Process {process.Id} did not close within {timeout.TotalSeconds:0.#}s, killing it.");
}
}
if (!process.HasExited)
{
process.Kill();
await process.WaitForExitAsync();
}
}
}
