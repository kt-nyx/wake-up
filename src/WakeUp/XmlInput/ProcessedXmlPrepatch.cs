// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace WakeUp;

// Called by the existing coordinated C01 owner after all native contracts pass.
internal static class ProcessedXmlPrepatch
{
    internal static readonly string[] Bodies = { "C1F776F08E061A79B8D9F061267337B9ADA9167D87714FCC8407C7D9D23BAC61", "086AE49657599E04781D0928A716B26D20C9F4F118B11E943DDC7C2518AD0C40" };
    internal static MethodDefinition[] Targets(ModuleDefinition module) => new[] {
        module.GetType("Verse.LoadedModManager").Methods.Single(m => m.Name == "ParseAndProcessXML"),
        module.GetType("Verse.XmlInheritance").Methods.Single(m => m.Name == "ResolveXmlNodeFor") };
    internal static bool Matches(ModuleDefinition module) => Targets(module).Select(AssetRoutingPrepatch.Fingerprint).SequenceEqual(Bodies);
    internal static void Inject(ModuleDefinition module, MethodDefinition all)
    {
        var patch = all.Body.Instructions.Single(i => i.Operand is MethodReference m && m.DeclaringType.FullName == "Verse.LoadedModManager" && m.Name == "ApplyPatches");
        var nativePatch = (MethodReference)patch.Operand;
        foreach (var load in new[] { patch.Previous.Previous, patch.Previous })
        { var local = Local(all, load); load.OpCode = OpCodes.Ldloca; load.Operand = local; }
        all.Body.GetILProcessor().InsertBefore(patch, Instruction.Create(OpCodes.Ldarg_0));
        patch.Operand = ParsedXmlPrepatch.Bridge(module, nameof(ProcessedXmlRuntime), "Apply", module.TypeSystem.Void,
            new ByReferenceType(nativePatch.Parameters[0].ParameterType), new ByReferenceType(nativePatch.Parameters[1].ParameterType), module.TypeSystem.Boolean);
        // Resolved-inheritance reuse is retired. Preserve the native Resolve call
        // and ResolveXmlNodeFor body for suppliers that install post-inheritance
        // work. A disabled runtime bridge still hides those callsites from them.
        all.Body.MaxStackSize = Math.Max(4, all.Body.MaxStackSize);
    }
    private static VariableDefinition Local(MethodDefinition method, Instruction instruction)
        => instruction.OpCode == OpCodes.Ldloc_0 || instruction.OpCode == OpCodes.Stloc_0 ? method.Body.Variables[0]
        : instruction.OpCode == OpCodes.Ldloc_1 || instruction.OpCode == OpCodes.Stloc_1 ? method.Body.Variables[1]
        : instruction.OpCode == OpCodes.Ldloc_2 || instruction.OpCode == OpCodes.Stloc_2 ? method.Body.Variables[2]
        : instruction.OpCode == OpCodes.Ldloc_3 || instruction.OpCode == OpCodes.Stloc_3 ? method.Body.Variables[3]
        : (VariableDefinition)instruction.Operand;
}
