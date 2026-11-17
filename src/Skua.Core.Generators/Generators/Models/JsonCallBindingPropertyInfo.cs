using Skua.Core.Generators.Helpers;
using System;
namespace Skua.Core.Generators.Models;
public sealed record JsonCallBindingPropertyInfo(
string FieldName,
string PropertyName,
string PropertyType,
bool IsNullable,
bool NotifyProp,
JsonCallBindingValues Values)
{
public sealed class Comparer : Comparer<JsonCallBindingPropertyInfo, Comparer>
{
public override void AddToHashCode(ref HashCode hashCode, JsonCallBindingPropertyInfo obj)
{
hashCode.Add(obj.FieldName);
hashCode.Add(obj.PropertyName);
hashCode.Add(obj.PropertyType);
hashCode.Add(obj.Values.FunctionName);
}
public override bool AreEqual(JsonCallBindingPropertyInfo x, JsonCallBindingPropertyInfo y)
{
return
x.FieldName == y.FieldName &&
x.PropertyName == y.PropertyName &&
x.PropertyType == y.PropertyType &&
x.Values.FunctionName == y.Values.FunctionName;
}
}
}
