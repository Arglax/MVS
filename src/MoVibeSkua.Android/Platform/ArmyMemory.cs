using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Skua.Core.Interfaces;
using MoVibeSkua.Android.Views;
using Trace = System.Diagnostics.Trace;
namespace MoVibeSkua.Android.Platform;
public static class ArmyMemory
{
private static readonly TimeSpan TrimDelay = TimeSpan.FromSeconds(20);
private static int _generation;
private static int _trimmedGeneration = -1;
private static double RamGB => DeviceProfile.Current.RamGB;
public static int MaxKeptPages
{
get
{
double ram = RamGB;
if (Army.IsFirst)
return ram > 0 && ram < 6 ? 6 : 12;
return ram switch { <= 0 => 5, < 8 => 3, < 12 => 5, _ => 8 };
}
}
public static bool PrepareBotPages => Army.IsFirst || RamGB >= 12;
public static void OnBackground()
{
int generation = Interlocked.Increment(ref _generation);
if (Army.IsFirst)
_ = LowMemLaterAsync(generation);
else
{
_ = TrimLaterAsync(generation);
if (ArmyShell.DetachEnabled)
_ = DetachLaterAsync(generation);
}
}
public static void OnForeground()
{
Interlocked.Increment(ref _generation);
if (Interlocked.Exchange(ref _lowMemSent, 0) == 1)
PostLowMem(false);
if (ArmyShell.Detached)
{
if (Dispatcher.UIThread.CheckAccess())
ArmyShell.Attach();
else
Dispatcher.UIThread.Post(() => ArmyShell.Attach());
}
if (Volatile.Read(ref _petsHidden) == 1)
_ = Task.Run(ShowOtherPets);
if (Skua.Flash.Ruffle.RuffleHost.SkipGearDownloads)
{
Skua.Flash.Ruffle.RuffleHost.SkipGearDownloads = false;
Trace.WriteLine($"[ArmyMemory] client {Army.Slot} gear downloads on (skipped {Skua.Flash.Ruffle.RuffleHost.GearSkipped} so far)");
}
}
private static bool SkipGearWhenHidden => !Army.IsFirst && RamGB < 16;
private static async Task DetachLaterAsync(int generation)
{
try
{
await Task.Delay(ArmyShell.DetachDelay).ConfigureAwait(false);
if (generation != Volatile.Read(ref _generation))
return;
long before = GC.GetTotalMemory(false);
bool detached = await Dispatcher.UIThread.InvokeAsync(() =>
generation == Volatile.Read(ref _generation) && ArmyShell.Detach(), DispatcherPriority.Background);
if (!detached)
return;
await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
if (generation != Volatile.Read(ref _generation))
{
Trace.WriteLine($"[ArmyMemory] client {Army.Slot} shell detached (back on screen before collection)");
return;
}
await Dispatcher.UIThread.InvokeAsync(SkiaSharp.SKGraphics.PurgeAllCaches, DispatcherPriority.Background);
GC.Collect(2, GCCollectionMode.Forced, blocking: true);
GC.WaitForPendingFinalizers();
GC.Collect(2, GCCollectionMode.Forced, blocking: true);
Java.Lang.JavaSystem.Gc();
long after = GC.GetTotalMemory(false);
var pss = Measure();
Trace.WriteLine($"[ArmyMemory] client {Army.Slot} shell detached: managed {before / 1048576} → {after / 1048576} MB (PSS {pss.Pss} MB, Graphics {pss.Graphics} MB)");
}
catch (Exception ex)
{
Trace.WriteLine($"[ArmyMemory] detach failed: {ex.Message}");
}
}
private static int _petsHidden;
private static bool HidePetsWhenHidden => RamGB < 16;
private static void HideOtherPets(int generation)
{
try
{
IFlashUtil? flash = Ioc.Default.GetService<IFlashUtil>();
if (flash is null || !flash.IsWorldLoaded || generation != Volatile.Read(ref _generation))
return;
if (flash.GetGameObject<bool>("world.hideOtherPets"))
return;
if (Interlocked.Exchange(ref _petsHidden, 1) == 1)
return;
flash.SetGameObject("world.hideOtherPets", true);
flash.CallGameFunction("world.hideAllPets", false);
Trace.WriteLine($"[ArmyMemory] client {Army.Slot} other players' pets hidden");
if (generation != Volatile.Read(ref _generation))
ShowOtherPets();
}
catch (Exception ex)
{
Trace.WriteLine($"[ArmyMemory] hide pets failed: {ex.Message}");
}
}
private static void ShowOtherPets()
{
try
{
if (Interlocked.Exchange(ref _petsHidden, 0) == 0)
return;
IFlashUtil? flash = Ioc.Default.GetService<IFlashUtil>();
if (flash is null || !flash.IsWorldLoaded)
return;
flash.SetGameObject("world.hideOtherPets", false);
flash.CallGameFunction("world.showAllPets", false);
Trace.WriteLine($"[ArmyMemory] client {Army.Slot} other players' pets shown");
}
catch (Exception ex)
{
Trace.WriteLine($"[ArmyMemory] show pets failed: {ex.Message}");
}
}
private static int _lowMemSent;
public static long? GpuResourceBytes => Army.IsFirst || RamGB >= 12 ? null : 8L * 1024 * 1024;
private static void PostLowMem(bool on) =>
CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetService<Skua.Flash.Ruffle.RuffleFlashUtil>()
?.PostCommand(on ? "{\"t\":\"lowmem\",\"v\":true}" : "{\"t\":\"lowmem\",\"v\":false}");
private static (long Graphics, long Pss) Measure()
{
try
{
var info = new global::Android.OS.Debug.MemoryInfo();
global::Android.OS.Debug.GetMemoryInfo(info);
static long Mb(string? kb) => long.TryParse(kb, out long v) ? v / 1024 : -1;
return (Mb(info.GetMemoryStat("summary.graphics")), Mb(info.GetMemoryStat("summary.total-pss")));
}
catch (Exception)
{
return (-1, -1);
}
}
private static async Task LowMemLaterAsync(int generation)
{
try
{
await Task.Delay(TrimDelay).ConfigureAwait(false);
if (generation != Volatile.Read(ref _generation))
return;
await LowMemAsync(generation).ConfigureAwait(false);
}
catch (Exception ex)
{
Trace.WriteLine($"[ArmyMemory] lowmem failed: {ex.Message}");
}
}
private static async Task LowMemAsync(int generation)
{
if (Interlocked.Exchange(ref _lowMemSent, 1) != 0)
return;
var before = Measure();
PostLowMem(true);
await Task.Delay(TimeSpan.FromSeconds(6)).ConfigureAwait(false);
if (generation != Volatile.Read(ref _generation))
return;
var after = Measure();
Trace.WriteLine($"[ArmyMemory] client {Army.Slot} lowmem: Graphics {before.Graphics}→{after.Graphics} MB, PSS {before.Pss}→{after.Pss} MB");
}
private static async Task TrimLaterAsync(int generation)
{
try
{
await Task.Delay(TrimDelay).ConfigureAwait(false);
if (generation != Volatile.Read(ref _generation) || generation == _trimmedGeneration)
return;
_trimmedGeneration = generation;
long before = GC.GetTotalMemory(false);
await Dispatcher.UIThread.InvokeAsync(() =>
{
if (generation != Volatile.Read(ref _generation))
return;
MobileShellView.Instance?.PurgeTransientViews();
SkiaSharp.SKGraphics.PurgeAllCaches();
}, DispatcherPriority.Background);
if (generation != Volatile.Read(ref _generation))
return;
Task lowMem = LowMemAsync(generation);
if (HidePetsWhenHidden)
HideOtherPets(generation);
if (SkipGearWhenHidden && generation == Volatile.Read(ref _generation))
{
Skua.Flash.Ruffle.RuffleHost.SkipGearDownloads = true;
Trace.WriteLine($"[ArmyMemory] client {Army.Slot} gear downloads off while hidden");
}
GC.Collect(2, GCCollectionMode.Forced, blocking: true);
GC.WaitForPendingFinalizers();
Java.Lang.JavaSystem.Gc();
long after = GC.GetTotalMemory(false);
Trace.WriteLine($"[ArmyMemory] client {Army.Slot} trimmed in background: managed {before / 1048576} → {after / 1048576} MB");
await lowMem.ConfigureAwait(false);
}
catch (Exception ex)
{
Trace.WriteLine($"[ArmyMemory] trim failed: {ex.Message}");
}
}
}
