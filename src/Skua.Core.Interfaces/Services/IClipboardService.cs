namespace Skua.Core.Interfaces;
public interface IClipboardService
{
void SetText(string text);
void SetData(string format, object data);
object GetData(string format);
string GetText();
}
