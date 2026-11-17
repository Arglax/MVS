namespace Skua.Core.Interfaces;
public interface IScriptHandlers
{
IEnumerable<IHandler> CurrentHandlers { get; }
IHandler RegisterHandler(int ticks, Func<IScriptInterface, bool> function, string name = null!);
IHandler RegisterHandler(int ticks, Action<IScriptInterface> function, string name = null!)
{
return RegisterHandler(ticks, b =>
{
function(b);
return true;
}, name);
}
IHandler RegisterOnce(int ticks, Action<IScriptInterface> function, string name = null!)
{
return RegisterHandler(ticks, b =>
{
function(b);
return false;
}, name);
}
bool Remove(string name);
bool Remove(IHandler handler);
bool Remove(List<IHandler> handlers);
void Clear();
}
