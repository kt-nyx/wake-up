// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace WakeUp;

public static class XmlRegionPrepatch
{
    internal const string NativeBody = "D5C285157A105C0CA36774A400380EBEBDA0C09B799E3DD42952BE2D118DC16B";

    // Rejected experiment: explicit test invocation only, never registered.
    public static bool RewriteAssembly(ModuleDefinition module)
    {
        try
        {
            MethodDefinition? apply = FindApplyPatches(module);
            if (apply == null || AssetRoutingPrepatch.Fingerprint(apply) != NativeBody) return false;
            Inject(module, apply);
            return true;
        }
        catch { return false; }
    }

    internal static MethodDefinition? FindApplyPatches(ModuleDefinition module)
        => module.GetType("Verse.LoadedModManager")?.Methods.SingleOrDefault(m => m.Name == "ApplyPatches"
            && m.IsStatic && m.HasBody && m.ReturnType.FullName == "System.Void" && m.Parameters.Count == 2
            && m.Parameters[0].ParameterType.FullName == "System.Xml.XmlDocument"
            && m.Parameters[1].ParameterType.FullName == "System.Collections.Generic.Dictionary`2<System.Xml.XmlNode,Verse.LoadableXmlAsset>");

    // The instruction object stays in place: original branches, foreach order,
    // locals and exception boundaries are unchanged. No other native method is edited.
    internal static void Inject(ModuleDefinition module, MethodDefinition apply)
    {
        Instruction call = apply.Body.Instructions.Single(i => i.OpCode == OpCodes.Callvirt
            && i.Operand is MethodReference m && m.DeclaringType.FullName == "Verse.PatchOperation"
            && m.Name == "Apply" && m.HasThis && m.Parameters.Count == 1
            && m.Parameters[0].ParameterType.FullName == "System.Xml.XmlDocument" && m.ReturnType.FullName == "System.Boolean");
        var original = (MethodReference)call.Operand;
        var scope = AssemblyNameReference.Parse(typeof(XmlRegionPrepatch).Assembly.FullName);
        scope = module.AssemblyReferences.FirstOrDefault(a => a.FullName == scope.FullName) ?? scope;
        var runtime = new TypeReference("WakeUp", "XmlRegionRuntime", module, scope);
        var bridge = new MethodReference("ApplyOne", module.TypeSystem.Boolean, runtime) { HasThis = false };
        bridge.Parameters.Add(new ParameterDefinition(module.TypeSystem.Object));
        bridge.Parameters.Add(new ParameterDefinition(original.Parameters[0].ParameterType));
        if (!module.AssemblyReferences.Contains(scope)) module.AssemblyReferences.Add(scope);
        call.OpCode = OpCodes.Call;
        call.Operand = bridge;
    }
}
