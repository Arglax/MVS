namespace Skua.Core.ViewModels;
public class InputDialogViewModel : Skua.Shared.Avalonia.ViewModels.Dialogs.InputDialogViewModel
{
public InputDialogViewModel(string title)
: base(title)
{
}
public InputDialogViewModel(string title, string dialogHint)
: base(title, dialogHint)
{
}
public InputDialogViewModel(string title, string dialogHint, string textBoxHint)
: base(title, dialogHint, textBoxHint)
{
}
public InputDialogViewModel(string title, bool numericInputOnly)
: base(title, numericInputOnly)
{
}
public InputDialogViewModel(string title, string dialogHint, bool numericInputOnly)
: base(title, dialogHint, numericInputOnly)
{
}
public InputDialogViewModel(string title, string dialogHint, string textBoxHint, bool numericInputOnly)
: base(title, dialogHint, textBoxHint, numericInputOnly)
{
}
}
