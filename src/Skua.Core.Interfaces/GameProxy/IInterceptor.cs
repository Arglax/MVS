using Skua.Core.Models;
namespace Skua.Core.Interfaces;
public interface IInterceptor
{
int Priority { get; }
void Intercept(MessageInfo message, bool outbound);
}
