using Skua.Core.Generators.Helpers;
using System;
using System.Collections.Generic;
namespace Skua.Core.Generators.Models;
public sealed record ObjectBindingPropertyInfo(
string FieldName,
string PropertyName,
string PropertyType,
bool IsNullable,
bool NotifyProp,
ObjectBindingValues Values)
{
public sealed class Comparer : Comparer<ObjectBindingPropertyInfo, Comparer>
{
public override void AddToHashCode(ref HashCode hashCode, ObjectBindingPropertyInfo obj)
{
hashCode.Add(obj.FieldName);
hashCode.Add(obj.PropertyName);
hashCode.Add(obj.PropertyType);
hashCode.Add(obj.Values.Paths[0]);
}
public override bool AreEqual(ObjectBindingPropertyInfo x, ObjectBindingPropertyInfo y)
{
return
x.FieldName == y.FieldName &&
x.PropertyName == y.PropertyName &&
x.PropertyType == y.PropertyType &&
x.Values.Paths[0] == y.Values.Paths[0];
}
}
}
