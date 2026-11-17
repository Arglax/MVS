using Microsoft.CodeAnalysis;
namespace Skua.Core.Generators.Diagnostics;
internal static class DiagnosticDescriptors
{
public static DiagnosticDescriptor CallBindingPropertyNameCollision = new(
id: "SkuaGen_001",
title: "Name collision for generated property",
messageFormat: "The field {0}.{1} cannot be used to generate a call binding property, as its name would collide with the field name (instance fields should use the \"lowerCamel\", \"_lowerCamel\" or \"m_lowerCamel\" pattern)",
category: typeof(CallBindingGenerator).FullName,
defaultSeverity: DiagnosticSeverity.Error,
isEnabledByDefault: true,
description: "The name of fields annotated with [CallBinding] should use \"lowerCamel\", \"_lowerCamel\" or \"m_lowerCamel\" pattern to avoid collisions with the generated properties.");
public static DiagnosticDescriptor CallBindingPathNullorEmpty = new(
id: "SkuaGen_002",
title: "The Path for the binding is null, empty or whitespace.",
messageFormat: "The field {0}.{1} cannot be used to generate a call binding property, as its Path value is not defined.",
category: typeof(CallBindingGenerator).FullName,
defaultSeverity: DiagnosticSeverity.Error,
isEnabledByDefault: true,
description: "The Path property of a Call Binding needs to have a valid value to be generated.");
public static DiagnosticDescriptor ObjectBindingPropertyNameCollision = new(
id: "SkuaGen_003",
title: "Name collision for generated property",
messageFormat: "The field {0}.{1} cannot be used to generate a object binding property, as its name would collide with the field name (instance fields should use the \"lowerCamel\", \"_lowerCamel\" or \"m_lowerCamel\" pattern)",
category: typeof(ObjectBindingGenerator).FullName,
defaultSeverity: DiagnosticSeverity.Error,
isEnabledByDefault: true,
description: "The name of fields annotated with [ObjectBinding] should use \"lowerCamel\", \"_lowerCamel\" or \"m_lowerCamel\" pattern to avoid collisions with the generated properties.");
public static DiagnosticDescriptor ObjectBindingPathNullorEmpty = new(
id: "SkuaGen_004",
title: "The Path for the binding is null, empty or whitespace.",
messageFormat: "The field {0}.{1} cannot be used to generate a object binding property, as its Paths value is not defined.",
category: typeof(ObjectBindingGenerator).FullName,
defaultSeverity: DiagnosticSeverity.Error,
isEnabledByDefault: true,
description: "The Paths property of a Object Binding needs to have a valid value to be generated.");
public static DiagnosticDescriptor ModuleBindingPropertyNameCollision = new(
id: "SkuaGen_005",
title: "Name collision for generated property",
messageFormat: "The field {0}.{1} cannot be used to generate a module binding property, as its name would collide with the field name (instance fields should use the \"lowerCamel\", \"_lowerCamel\" or \"m_lowerCamel\" pattern)",
category: typeof(ModuleBindingGenerator).FullName,
defaultSeverity: DiagnosticSeverity.Error,
isEnabledByDefault: true,
description: "The name of fields annotated with [ModuleBinding] should use \"lowerCamel\", \"_lowerCamel\" or \"m_lowerCamel\" pattern to avoid collisions with the generated properties.");
public static DiagnosticDescriptor ModuleBindingPathNullorEmpty = new(
id: "SkuaGen_006",
title: "The Name for the binding is null, empty or whitespace.",
messageFormat: "The field {0}.{1} cannot be used to generate a module binding property, as its Name value is not defined.",
category: typeof(ModuleBindingGenerator).FullName,
defaultSeverity: DiagnosticSeverity.Error,
isEnabledByDefault: true,
description: "The Name property of a Module Binding needs to have a valid value to be generated.");
public static DiagnosticDescriptor MethodCallBindingMethodNameCollision = new(
id: "SkuaGen_007",
title: "Name collision for generated method",
messageFormat: "The method {0}.{1} cannot be used to generate a method call binding method, as its name would collide with the generated method name (method names for generation should use the \"lowerCamel\", \"_lowerCamel\" or \"m_lowerCamel\" pattern)",
category: typeof(MethodCallBindingGenerator).FullName,
defaultSeverity: DiagnosticSeverity.Error,
isEnabledByDefault: true,
description: "The name of methods annotated with [MethodCallBinding] should use \"lowerCamel\", \"_lowerCamel\" or \"m_lowerCamel\" pattern to avoid collisions with the generated methods.");
}
