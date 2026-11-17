using Skua.Core.Models.Items;
namespace Skua.Core.Interfaces;
public interface IItemContainer<T> where T : ItemBase
{
List<T> Items { get; }
}
