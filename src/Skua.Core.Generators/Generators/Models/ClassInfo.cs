using Skua.Core.Generators.Helpers;
using System;
using System.Collections.Generic;
namespace Skua.Core.Generators.Models;
internal sealed record ClassInfo(
string Name,
string Namespace,
string? InheritanceFormattedNames)
{
public sealed class Comparer : Comparer<ClassInfo, Comparer>
{
public override void AddToHashCode(ref HashCode hashCode, ClassInfo obj)
{
hashCode.Add(obj.Name);
hashCode.Add(obj.Namespace);
hashCode.Add(obj.InheritanceFormattedNames);
}
public override bool AreEqual(ClassInfo x, ClassInfo y)
{
return
x.Name == y.Name &&
x.Namespace == y.Namespace &&
x.InheritanceFormattedNames == y.InheritanceFormattedNames;
}
}
}
