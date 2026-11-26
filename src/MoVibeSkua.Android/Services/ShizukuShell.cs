using System;
using System.Threading.Tasks;
using Android.Runtime;
namespace MoVibeSkua.Android.Services;
public static class ShizukuShell
{
private const int RequestCode = 8831;
private static IntPtr _class;
private static IntPtr Class()
{
if (_class == IntPtr.Zero)
{
IntPtr local = JNIEnv.FindClass("com/arglax/movibeskua/ShizukuShell");
_class = JNIEnv.NewGlobalRef(local);
JNIEnv.DeleteLocalRef(local);
}
return _class;
}
private static bool CallBool(string name)
{
try
{
IntPtr method = JNIEnv.GetStaticMethodID(Class(), name, "()Z");
return JNIEnv.CallStaticBooleanMethod(Class(), method);
}
catch (Exception e)
{
global::Android.Util.Log.Warn("MVS", "shizuku " + name + ": " + e.Message);
return false;
}
}
public static bool Available => CallBool("available");
public static bool Granted => CallBool("granted");
public static bool Denied => CallBool("denied");
public static async Task<bool> RequestAsync()
{
if (Granted)
return true;
if (!Available)
return false;
try
{
IntPtr method = JNIEnv.GetStaticMethodID(Class(), "request", "(I)V");
JNIEnv.CallStaticVoidMethod(Class(), method, new JValue(RequestCode));
}
catch (Exception e)
{
global::Android.Util.Log.Warn("MVS", "shizuku request: " + e.Message);
return false;
}
for (int i = 0; i < 240; i++)
{
await Task.Delay(500).ConfigureAwait(false);
if (Granted)
return true;
}
return false;
}
public static string? Exec(string command)
{
try
{
IntPtr method = JNIEnv.GetStaticMethodID(Class(), "exec", "(Ljava/lang/String;)Ljava/lang/String;");
IntPtr arg = JNIEnv.NewString(command);
try
{
IntPtr result = JNIEnv.CallStaticObjectMethod(Class(), method, new JValue(arg));
return JNIEnv.GetString(result, JniHandleOwnership.TransferLocalRef);
}
finally
{
JNIEnv.DeleteLocalRef(arg);
}
}
catch (Exception e)
{
global::Android.Util.Log.Warn("MVS", "shizuku exec: " + e.Message);
return null;
}
}
public static bool ForceStop(string package)
{
foreach (char c in package)
{
if (!(char.IsAsciiLetterOrDigit(c) || c is '.' or '_'))
return false;
}
return Exec("am force-stop " + package) is not null;
}
}
