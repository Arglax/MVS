using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Android.Content;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Skua.App.Avalonia.ViewModels;
using Skua.App.Avalonia.ViewModels.FastTravel;
using Skua.Core.Interfaces;
using Skua.Core.Interfaces.ViewModels;
using Skua.Core.Messaging;
namespace MoVibeSkua.Android.ViewModels;
public sealed partial class MobileJumpViewModel : ObservableRecipient
{
private const int PrivateRoomFrom = 1000;
private readonly IScriptInterface _bot;
private readonly IRelayCommand<object> _fastTravelCommand;
private readonly object _recipient = new();
public MobileJumpViewModel(JumpViewModel jump, FastTravelViewModel fastTravel, IScriptInterface bot)
{
Jump = jump;
FastTravel = fastTravel;
_bot = bot;
_fastTravelCommand = new RelayCommand<object>(fastTravel.MapService.Travel);
}
public JumpViewModel Jump { get; }
public FastTravelViewModel FastTravel { get; }
protected override void OnActivated()
{
Jump.IsActive = true;
StrongReferenceMessenger.Default.Register<object, MapChangedMessage, int>(_recipient, (int)MessageChannels.GameEvents,
(_, _) => RefreshHere());
RefreshHere();
}
protected override void OnDeactivated()
{
Jump.IsActive = false;
StrongReferenceMessenger.Default.UnregisterAll(_recipient);
IsEditing = false;
IsSortOpen = false;
CancelRename();
}
public ObservableCollection<IFastTravelItemViewModel> Spots => FastTravel.FastTravelItems;
[ObservableProperty]
private string _newMap = string.Empty;
[ObservableProperty]
private string _newRoom = string.Empty;
[ObservableProperty]
private string _status = string.Empty;
[ObservableProperty]
private string _hereText = "Log in to save where you stand.";
[ObservableProperty]
private bool _isEditing;
[ObservableProperty]
private bool _isSortOpen;
[ObservableProperty]
private IFastTravelItemViewModel? _renaming;
[ObservableProperty]
private string _renameText = string.Empty;
public bool IsRenaming => Renaming is not null;
partial void OnRenamingChanged(IFastTravelItemViewModel? value) => OnPropertyChanged(nameof(IsRenaming));
private sealed record Here(string Map, string Cell, string Pad);
private Here? ReadHere()
{
if (!_bot.Player.Playing)
return null;
string name = (_bot.Map.Name ?? string.Empty).Trim().ToLowerInvariant();
string cell = (_bot.Player.Cell ?? string.Empty).Trim();
string pad = (_bot.Player.Pad ?? string.Empty).Trim();
if (name.Length == 0 || cell.Length == 0)
return null;
int room = _bot.Map.RoomNumber;
string map = room >= PrivateRoomFrom ? $"{name}-{room}" : name;
return new Here(map, cell, pad.Length == 0 ? "Spawn" : pad);
}
private void RefreshHere()
{
_ = Task.Run(() =>
{
string text;
try
{
Here? here = ReadHere();
text = here is null ? "Log in to save where you stand." : $"Here: {here.Map} · cell {here.Cell} · pad {here.Pad}";
}
catch
{
text = "Log in to save where you stand.";
}
Avalonia.Threading.Dispatcher.UIThread.Post(() => HereText = text);
});
}
[RelayCommand]
private void SaveHere()
{
Usage.UsageTracker.Feature("jumpSaveHere");
_ = Task.Run(() =>
{
Here? here;
try
{
here = ReadHere();
}
catch
{
here = null;
}
Avalonia.Threading.Dispatcher.UIThread.Post(() =>
{
if (here is null)
{
Status = "Log in and stand in a map first.";
HereText = "Log in to save where you stand.";
return;
}
HereText = $"Here: {here.Map} · cell {here.Cell} · pad {here.Pad}";
if (Spots.Any(s => Same(s.MapName, here.Map) && Same(s.Cell, here.Cell) && Same(s.Pad, here.Pad)))
{
Status = $"{here.Map} {here.Cell} is already saved.";
return;
}
string name = Clean($"{here.Map.Split('-')[0]} {here.Cell}");
FastTravelItemViewModel spot = new(name, here.Map, here.Cell, here.Pad, _fastTravelCommand);
Spots.Insert(0, spot);
MarkAdded(spot);
Status = $"Saved {name}. Tap Edit to rename or move it.";
});
});
}
[RelayCommand]
private void AddSpot()
{
Usage.UsageTracker.Feature("jumpAddSpot");
string map = Clean(NewMap).ToLowerInvariant().Replace(" ", string.Empty);
string room = NewRoom.Trim();
if (map.Length == 0)
{
Status = "Type a map name first.";
return;
}
if (room.Length > 0)
{
if (!room.All(char.IsDigit))
{
Status = "The room has to be a number.";
return;
}
map = $"{map.Split('-')[0]}-{room}";
}
if (Spots.Any(s => Same(s.MapName, map) && Same(s.Cell, "Enter")))
{
Status = $"{map} is already saved.";
return;
}
FastTravelItemViewModel spot = new(map, map, "Enter", "Spawn", _fastTravelCommand);
Spots.Insert(0, spot);
MarkAdded(spot);
NewMap = string.Empty;
NewRoom = string.Empty;
Status = $"Saved {map}.";
}
[RelayCommand]
private void TravelTo(IFastTravelItemViewModel? spot)
{
Usage.UsageTracker.Feature("jumpTravel");
if (spot is null)
return;
Status = $"Joining {spot.MapName} ({spot.Cell}, {spot.Pad})...";
string map = spot.MapName;
string cell = spot.Cell;
string pad = spot.Pad;
_ = Task.Run(() =>
{
try
{
if (!_bot.Player.Playing)
{
Avalonia.Threading.Dispatcher.UIThread.Post(() => Status = "Log in first.");
return;
}
if (!map.Contains('-'))
{
FastTravel.MapService.Travel(spot);
return;
}
_bot.Map.Join(map, cell, pad);
}
catch (Exception ex)
{
System.Diagnostics.Trace.WriteLine($"[Jump] join {map} failed: {ex.Message}");
Avalonia.Threading.Dispatcher.UIThread.Post(() => Status = $"Couldn't join {map}. Try again.");
}
});
}
[RelayCommand]
private void ToggleEdit()
{
IsEditing = !IsEditing;
IsSortOpen = false;
if (IsEditing)
Usage.UsageTracker.Feature("jumpEdit");
else
CancelRename();
}
[RelayCommand]
private void ToggleSort() => IsSortOpen = !IsSortOpen;
[RelayCommand]
private void SortBy(string? how)
{
IsSortOpen = false;
Usage.UsageTracker.Feature("jumpSort");
List<IFastTravelItemViewModel> current = Spots.ToList();
Dictionary<IFastTravelItemViewModel, int> position = current.Select((s, i) => (s, i)).ToDictionary(p => p.s, p => p.i);
IEnumerable<IFastTravelItemViewModel> sorted = how switch
{
"map" => current.OrderBy(s => s.MapName, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.DescriptionName, StringComparer.OrdinalIgnoreCase),
"newest" => current.OrderByDescending(AddedAt).ThenBy(s => position[s]),
"oldest" => current.OrderBy(AddedAt).ThenBy(s => position[s]),
_ => current.OrderBy(s => s.DescriptionName, StringComparer.OrdinalIgnoreCase),
};
List<IFastTravelItemViewModel> order = sorted.ToList();
for (int i = 0; i < order.Count; i++)
{
int from = Spots.IndexOf(order[i]);
if (from != i)
Spots.Move(from, i);
}
Status = how switch
{
"map" => "Sorted by map.",
"newest" => "Newest first.",
"oldest" => "Oldest first.",
_ => "Sorted by name.",
};
}
[RelayCommand]
private void MoveUp(IFastTravelItemViewModel? spot) => MoveBy(spot, -1);
[RelayCommand]
private void MoveDown(IFastTravelItemViewModel? spot) => MoveBy(spot, 1);
private void MoveBy(IFastTravelItemViewModel? spot, int step)
{
if (spot is null)
return;
int from = Spots.IndexOf(spot);
int to = from + step;
if (from < 0 || to < 0 || to >= Spots.Count)
return;
Usage.UsageTracker.Feature("jumpMoveSpot");
Spots.Move(from, to);
}
[RelayCommand]
private void StartRename(IFastTravelItemViewModel? spot)
{
if (spot is null)
return;
Renaming = spot;
RenameText = spot.DescriptionName;
}
[RelayCommand]
private void SaveRename()
{
if (Renaming is not FastTravelItemViewModel spot)
{
CancelRename();
return;
}
string name = Clean(RenameText);
if (name.Length == 0)
{
Status = "Type a name first.";
return;
}
Usage.UsageTracker.Feature("jumpRenameSpot");
long added = AddedAt(spot);
Forget(spot);
spot.DescriptionName = name;
Remember(spot, added);
FastTravel.SaveFastTravelsCommand.Execute(null);
Status = $"Renamed to {name}.";
CancelRename();
}
[RelayCommand]
private void CancelRename()
{
Renaming = null;
RenameText = string.Empty;
}
[RelayCommand]
private void RemoveSpot(IFastTravelItemViewModel? spot)
{
Usage.UsageTracker.Feature("jumpRemoveSpot");
if (spot is null)
return;
if (ReferenceEquals(Renaming, spot))
CancelRename();
if (Spots.Remove(spot))
{
Forget(spot);
Status = $"Removed {spot.DescriptionName}.";
}
}
private static bool Same(string? a, string? b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
private static string Clean(string? text) => (text ?? string.Empty).Replace(",", " ").Trim();
private static ISharedPreferences? Prefs =>
global::Android.App.Application.Context.GetSharedPreferences("movibeskua.jump", FileCreationMode.Private);
private static string Key(IFastTravelItemViewModel spot) => "added:" + spot;
private static long AddedAt(IFastTravelItemViewModel spot)
{
try
{
return Prefs?.GetLong(Key(spot), 0) ?? 0;
}
catch
{
return 0;
}
}
private static void MarkAdded(IFastTravelItemViewModel spot) => Remember(spot, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
private static void Remember(IFastTravelItemViewModel spot, long when)
{
if (when <= 0)
return;
try
{
Prefs?.Edit()?.PutLong(Key(spot), when)?.Apply();
}
catch
{
}
}
private static void Forget(IFastTravelItemViewModel spot)
{
try
{
Prefs?.Edit()?.Remove(Key(spot))?.Apply();
}
catch
{
}
}
}
