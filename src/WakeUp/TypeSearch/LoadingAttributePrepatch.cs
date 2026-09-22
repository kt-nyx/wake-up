// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Prepatcher;

namespace WakeUp;

// Keep the generic helper and its callers intact so patches on its entry point
// retain their ordinary behavior. Only its non-generic metadata lookup changes.
public static class LoadingAttributePrepatch
{
    internal const string OriginalNativeBody = "194841CE2DC0384D1E6FB83D53A0699E58EA946AA5E6C6005128C0676DDB8F38";

    [FreePatch]
    public static bool RewriteAssembly(ModuleDefinition module)
    {
        try
        {
            var helper = FindHelper(module);
            if (helper == null || AssetRoutingPrepatch.Fingerprint(helper) != OriginalNativeBody) return false;
            Inject(module, helper);
            return true;
        }
        catch { return false; }
    }

    // The shared body fingerprint does not encode method generic constraints.
    // Check those separately, including the actual core-library type identity.
    internal static MethodDefinition? FindHelper(ModuleDefinition module)
    {
        var type = module.GetType("Verse.GenAttribute");
        if (type == null || type.HasGenericParameters) return null;
        var candidates = type.Methods.Where(m => m.Name == "HasAttribute").ToArray();
        if (candidates.Length != 1) return null;
        var method = candidates[0];
        if (!method.IsPublic || !method.IsStatic || method.HasThis || method.ExplicitThis || !method.HasBody
            || method.CallingConvention != MethodCallingConvention.Generic
            || !CoreType(method.ReturnType, "System.Boolean") || method.Parameters.Count != 1
            || !CoreType(method.Parameters[0].ParameterType, "System.Reflection.MemberInfo")
            || method.GenericParameters.Count != 1) return null;
        var generic = method.GenericParameters[0];
        if (generic.Position != 0 || generic.Type != GenericParameterType.Method || generic.Owner != method
            || generic.Attributes != GenericParameterAttributes.NonVariant || generic.HasCustomAttributes
            || generic.Constraints.Count != 1 || generic.Constraints[0].HasCustomAttributes
            || !CoreType(generic.Constraints[0].ConstraintType, "System.Attribute")) return null;
        return method;
    }

    internal static void Inject(ModuleDefinition module, MethodDefinition helper)
    {
        var call = helper.Body.Instructions.Single(i => i.OpCode == OpCodes.Call
            && i.Operand is MethodReference method && NativeLookup(method));
        var native = (MethodReference)call.Operand;
        var bridge = ParsedXmlPrepatch.Bridge(module, "LoadingAttributeRuntime", "IsDefined", native.ReturnType,
            native.Parameters.Select(p => p.ParameterType).ToArray());
        call.Operand = bridge;
    }

    private static bool NativeLookup(MethodReference method) => method.Name == "IsDefined"
        && CoreType(method.DeclaringType, "System.Attribute") && !method.HasThis && !method.ExplicitThis
        && !method.HasGenericParameters && !(method is GenericInstanceMethod)
        && method.CallingConvention == MethodCallingConvention.Default
        && CoreType(method.ReturnType, "System.Boolean") && method.Parameters.Count == 3
        && CoreType(method.Parameters[0].ParameterType, "System.Reflection.MemberInfo")
        && CoreType(method.Parameters[1].ParameterType, "System.Type")
        && CoreType(method.Parameters[2].ParameterType, "System.Boolean");

    private static bool CoreType(TypeReference type, string name) => type.FullName == name
        && type.Scope is AssemblyNameReference scope
        && scope.FullName == "mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";
}
