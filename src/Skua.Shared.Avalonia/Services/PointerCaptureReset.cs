using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Collections.Generic;
namespace Skua.Shared.Avalonia.Services;
public static class PointerCaptureReset
{
private static readonly List<IPointer> s_releasing = [];
private static bool s_tracking;
public static void Track()
{
if (s_tracking)
return;
s_tracking = true;
InputElement.PointerReleasedEvent.AddClassHandler<TopLevel>(
(_, e) => s_releasing.Add(e.Pointer), RoutingStrategies.Tunnel, handledEventsToo: true);
InputElement.PointerReleasedEvent.AddClassHandler<TopLevel>(
(_, e) => s_releasing.Remove(e.Pointer), RoutingStrategies.Bubble, handledEventsToo: true);
}
public static void Release()
{
foreach (IPointer pointer in s_releasing.ToArray())
{
if (pointer.Captured is not null)
pointer.Capture(null);
}
}
}
