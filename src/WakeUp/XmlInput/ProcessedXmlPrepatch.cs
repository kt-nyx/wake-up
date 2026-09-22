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
        var methods = Targets(module); var parse = methods[0]; var resolve = methods[1];
        var call = parse.Body.Instructions.Single(i => i.Operand is MethodReference m && m.DeclaringType.FullName == "Verse.XmlInheritance" && m.Name == "Resolve");
        var il = parse.Body.GetILProcessor();
        il.InsertBefore(call, Instruction.Create(OpCodes.Ldarg_0)); il.InsertBefore(call, Instruction.Create(OpCodes.Ldarg_1)); il.InsertBefore(call, Instruction.Create(OpCodes.Ldarg_2));
        call.Operand = ParsedXmlPrepatch.Bridge(module, nameof(ResolvedInheritanceRuntime), "Resolve", module.TypeSystem.Void,
            parse.Parameters[0].ParameterType, parse.Parameters[1].ParameterType, module.TypeSystem.Boolean);
        var clone = resolve.Body.Instructions.Single(i => i.Operand is MethodReference m && m.Name == "CloneNode");
        var merge = resolve.Body.Instructions.Single(i => i.Operand is MethodReference m && m.Name == "RecursiveNodeCopyOverwriteElements");
        var xml = module.GetType("Verse.XmlInheritance").NestedTypes.Single(t => t.Name == "XmlInheritanceNode");
        var source = xml.Fields.Single(f => f.Name == "xmlNode"); var parent = xml.Fields.Single(f => f.Name == "parent");
        var resolved = xml.Fields.Single(f => f.Name == "resolvedXmlNode");
        var result = Local(resolve, clone.Next);
        // The duplicate diagnostic call is the final native call before clone.
        var duplicate = resolve.Body.Instructions.Last(i => i.Offset < clone.Offset && i.Operand is MethodReference m && m.Name == "CheckForDuplicateNodes");
        var start = duplicate.Next; var commit = merge.Next;
        var restore = ParsedXmlPrepatch.Bridge(module, nameof(ResolvedInheritanceRuntime), "TryRestore", module.TypeSystem.Boolean,
            source.FieldType, source.FieldType, new ByReferenceType(source.FieldType));
        var inserted = new[] { Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, source),
            Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, parent), Instruction.Create(OpCodes.Ldfld, resolved),
            Instruction.Create(OpCodes.Ldloca, result), Instruction.Create(OpCodes.Call, restore), Instruction.Create(OpCodes.Brtrue, commit) };
        foreach (var instruction in inserted) resolve.Body.GetILProcessor().InsertBefore(start, instruction);
        var capture = ParsedXmlPrepatch.Bridge(module, nameof(ResolvedInheritanceRuntime), "Capture", module.TypeSystem.Void,
            source.FieldType, source.FieldType, source.FieldType);
        var captures = new[] { Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, source),
            Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, parent), Instruction.Create(OpCodes.Ldfld, resolved),
            Instruction.Create(OpCodes.Ldloc, result), Instruction.Create(OpCodes.Call, capture) };
        foreach (var instruction in captures) resolve.Body.GetILProcessor().InsertBefore(commit, instruction);
        all.Body.MaxStackSize = Math.Max(4, all.Body.MaxStackSize);
        parse.Body.MaxStackSize = Math.Max(4, parse.Body.MaxStackSize); resolve.Body.MaxStackSize = Math.Max(4, resolve.Body.MaxStackSize);
    }
    private static VariableDefinition Local(MethodDefinition method, Instruction instruction)
        => instruction.OpCode == OpCodes.Ldloc_0 || instruction.OpCode == OpCodes.Stloc_0 ? method.Body.Variables[0]
        : instruction.OpCode == OpCodes.Ldloc_1 || instruction.OpCode == OpCodes.Stloc_1 ? method.Body.Variables[1]
        : instruction.OpCode == OpCodes.Ldloc_2 || instruction.OpCode == OpCodes.Stloc_2 ? method.Body.Variables[2]
        : instruction.OpCode == OpCodes.Ldloc_3 || instruction.OpCode == OpCodes.Stloc_3 ? method.Body.Variables[3]
        : (VariableDefinition)instruction.Operand;
}
