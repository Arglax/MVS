using System.ComponentModel;
using System.Net;
namespace Skua.Core.Interfaces;
public interface ICaptureProxy : INotifyPropertyChanged
{
IPEndPoint? Destination { get; set; }
List<IInterceptor> Interceptors { get; }
bool Running { get; }
void Start();
void Stop();
}
