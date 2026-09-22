// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
// Nonshipping R1 experiment. Invoke explicitly in offline tests only.
using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace WakeUp;

public static class XmlReusePrepatch
{
    internal const string LoadBody = "C774CE9946976070DA2CE5815D3EB6BF6545B36FAC33BD9822A1F452B1AD09CF";
    internal const string ApplyBody = "D5C285157A105C0CA36774A400380EBEBDA0C09B799E3DD42952BE2D118DC16B";

    public static bool RewriteAssembly(ModuleDefinition module)
    {
        try
        {
            var type = module.GetType("Verse.LoadedModManager");
            var load = type?.Methods.SingleOrDefault(m => m.Name == "LoadAllActiveMods");
            var apply = type?.Methods.SingleOrDefault(m => m.Name == "ApplyPatches");
            if (load == null || apply == null || AssetRoutingPrepatch.Fingerprint(load) != LoadBody
                || AssetRoutingPrepatch.Fingerprint(apply) != ApplyBody) return false;
            Inject(module, load, apply);
            return true;
        }
        catch { return false; }
    }

    // Both complete methods are checked before either is edited. Preserve the
    // original iteration, exception handling, completion and inheritance calls.
    internal static void Inject(ModuleDefinition module, MethodDefinition load, MethodDefinition apply)
    {
        var call = load.Body.Instructions.Single(i => i.Operand is MethodReference m
            && m.DeclaringType.FullName == "Verse.LoadedModManager" && m.Name == "ApplyPatches");
        var docLoad = call.Previous.Previous;
        var mapLoad = call.Previous;
        var doc = Local(load, docLoad);
        var map = Local(load, mapLoad);
        var inputs = load.Body.Variables.Single(v => v.VariableType.FullName == "System.Collections.Generic.List`1<Verse.LoadableXmlAsset>");
        var opCall = apply.Body.Instructions.Single(i => i.OpCode == OpCodes.Callvirt
            && i.Operand is MethodReference m && m.DeclaringType.FullName == "Verse.PatchOperation" && m.Name == "Apply");
        var scope = AssemblyNameReference.Parse(typeof(XmlReusePrepatch).Assembly.FullName);
        scope = module.AssemblyReferences.FirstOrDefault(a => a.FullName == scope.FullName) ?? scope;
        var runtime = new TypeReference("WakeUp", "XmlReuseRuntime", module, scope);
        var mapObject = new VariableDefinition(module.TypeSystem.Object);
        var bridge = new MethodReference("Apply", module.TypeSystem.Void, runtime) { HasThis = false };
        bridge.Parameters.Add(new ParameterDefinition(new ByReferenceType(doc.VariableType)));
        bridge.Parameters.Add(new ParameterDefinition(new ByReferenceType(module.TypeSystem.Object)));
        bridge.Parameters.Add(new ParameterDefinition(module.TypeSystem.Object));
        bridge.Parameters.Add(new ParameterDefinition(module.TypeSystem.Boolean));
        var original = (MethodReference)opCall.Operand;
        var one = new MethodReference("ApplyOne", module.TypeSystem.Boolean, runtime) { HasThis = false };
        one.Parameters.Add(new ParameterDefinition(module.TypeSystem.Object));
        one.Parameters.Add(new ParameterDefinition(original.Parameters[0].ParameterType));
        if (!module.AssemblyReferences.Contains(scope)) module.AssemblyReferences.Add(scope);
        docLoad.OpCode = OpCodes.Ldloca; docLoad.Operand = doc;
        var il = load.Body.GetILProcessor();
        load.Body.Variables.Add(mapObject);
        il.InsertBefore(call, Instruction.Create(OpCodes.Stloc, mapObject));
        il.InsertBefore(call, Instruction.Create(OpCodes.Ldloca, mapObject));
        il.InsertBefore(call, Instruction.Create(OpCodes.Ldloc, inputs));
        il.InsertBefore(call, Instruction.Create(OpCodes.Ldarg_0));
        call.Operand = bridge;
        var resultLoad = Instruction.Create(OpCodes.Ldloc, mapObject);
        var resultCast = Instruction.Create(OpCodes.Castclass, map.VariableType);
        il.InsertAfter(call, resultLoad);
        il.InsertAfter(resultLoad, resultCast);
        il.InsertAfter(resultCast, Instruction.Create(OpCodes.Stloc, map));
        load.Body.MaxStackSize = Math.Max(load.Body.MaxStackSize, 4);
        opCall.OpCode = OpCodes.Call; opCall.Operand = one;
    }

    private static VariableDefinition Local(MethodDefinition method, Instruction instruction)
        => instruction.OpCode.Code switch
        {
            Code.Ldloc_0 => method.Body.Variables[0], Code.Ldloc_1 => method.Body.Variables[1],
            Code.Ldloc_2 => method.Body.Variables[2], Code.Ldloc_3 => method.Body.Variables[3],
            Code.Ldloc or Code.Ldloc_S => (VariableDefinition)instruction.Operand,
            _ => throw new InvalidOperationException("xml-callsite-local")
        };
}
