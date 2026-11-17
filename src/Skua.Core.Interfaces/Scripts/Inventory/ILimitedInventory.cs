namespace Skua.Core.Interfaces;
public interface ILimitedInventory
{
int Slots { get; }
int UsedSlots { get; }
int FreeSlots => Slots - UsedSlots;
}
