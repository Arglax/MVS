using Skua.Core.Generators.Helpers;
using System;
namespace Skua.Core.Generators.Models;
public sealed record CallBindingPropertyInfo(
string FieldName,
string PropertyName,
string PropertyType,
bool IsNullable,
bool NotifyProp,
CallBindingValues Values)
{
public sealed class Comparer : Comparer<CallBindingPropertyInfo, Comparer>
{
public override void AddToHashCode(ref HashCode hashCode, CallBindingPropertyInfo obj)
{
hashCode.Add(obj.FieldName);
hashCode.Add(obj.PropertyName);
hashCode.Add(obj.PropertyType);
hashCode.Add(obj.Values.Path);
}
public override bool AreEqual(CallBindingPropertyInfo x, CallBindingPropertyInfo y)
{
return
x.FieldName == y.FieldName &&
x.PropertyName == y.PropertyName &&
x.PropertyType == y.PropertyType &&
x.Values.Path == y.Values.Path;
}
}
}
