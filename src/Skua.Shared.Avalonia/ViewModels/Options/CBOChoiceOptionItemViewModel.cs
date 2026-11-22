using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace Skua.Shared.Avalonia.ViewModels.Options;
public partial class CBOChoiceOptionItemViewModel : DisplayOptionItemViewModel<int>
{
public CBOChoiceOptionItemViewModel(string optionTitle, string description, string tag, IEnumerable<string> options)
: base(optionTitle, description, tag)
{
Options = new ObservableCollection<string>(options);
Value = 0;
}
public CBOChoiceOptionItemViewModel(string optionTitle, string description, string tag, IEnumerable<string> options, int value)
: base(optionTitle, description, tag)
{
Options = new ObservableCollection<string>(options);
Value = value;
}
public ObservableCollection<string> Options { get; }
}
