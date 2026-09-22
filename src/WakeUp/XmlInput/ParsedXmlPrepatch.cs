// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Prepatcher;

namespace WakeUp;

public static class ParsedXmlPrepatch
{
    // Original method contracts are checked before any of the coordinated edits.
    internal static readonly string[] Names = { "XmlAssetsInModFolder", "LoadAllActiveMods", "CreateModClasses" };
    internal static readonly string[] Bodies = {
        "EF99D607AD092B372DA63344EB77D50B027EBD5F39E3738AE074C4B9C5817657",
        "C774CE9946976070DA2CE5815D3EB6BF6545B36FAC33BD9822A1F452B1AD09CF",
        "6688BACC4FF9E1D72FA8A68B41B31B6DD94DA98C78D6543505F8A793B4A2572B" };
    internal static MethodDefinition[] Targets(ModuleDefinition module) => new[] {
        module.GetType("Verse.DirectXmlLoader").Methods.Single(m => m.Name == Names[0]),
        module.GetType("Verse.LoadedModManager").Methods.Single(m => m.Name == Names[1]),
        module.GetType("Verse.LoadedModManager").Methods.Single(m => m.Name == Names[2]) };

    [FreePatch]
    public static bool RewriteAssembly(ModuleDefinition module)
    {
        try
        {
            var methods = Targets(module);
            if (methods.Where((m, i) => AssetRoutingPrepatch.Fingerprint(m) != Bodies[i]).Any() || !ProcessedXmlPrepatch.Matches(module)) return false;
            Inject(module, methods);
            return true;
        }
        catch { return false; }
    }

    internal static MethodReference Bridge(ModuleDefinition module, string typeName, string name, TypeReference result, params TypeReference[] args)
    {
        var scope = AssemblyNameReference.Parse(typeof(ParsedXmlPrepatch).Assembly.FullName);
        var previous = module.AssemblyReferences.FirstOrDefault(a => a.FullName == scope.FullName);
        if (previous != null) scope = previous; else module.AssemblyReferences.Add(scope);
        var method = new MethodReference(name, result, new TypeReference("WakeUp", typeName, module, scope)) { HasThis = false };
        foreach (var arg in args) method.Parameters.Add(new ParameterDefinition(arg));
        return method;
    }

    internal static void Inject(ModuleDefinition module, MethodDefinition[] methods)
    {
        var files = methods[0];
        var selectedCall = files.Body.Instructions.Single(i => i.Operand is GenericInstanceMethod g && g.Name == "ToList"
            && g.GenericArguments.Count == 1 && g.GenericArguments[0].FullName == "System.IO.FileInfo");
        var store = selectedCall.Next;
        VariableDefinition selected = store.OpCode == OpCodes.Stloc_0 ? files.Body.Variables[0]
            : store.OpCode == OpCodes.Stloc_1 ? files.Body.Variables[1]
            : store.OpCode == OpCodes.Stloc_2 ? files.Body.Variables[2]
            : store.OpCode == OpCodes.Stloc_3 ? files.Body.Variables[3] : (VariableDefinition)store.Operand;
        var original = store.Next;
        OrderedInputPrepatch.Inject(module, files, selectedCall);
        var result = new VariableDefinition(files.ReturnType);
        var load = Bridge(module, nameof(ParsedXmlRuntime), "TryLoadSelected", module.TypeSystem.Boolean,
            files.Parameters[0].ParameterType, files.Parameters[1].ParameterType, selected.VariableType, new ByReferenceType(files.ReturnType));
        var injected = new[] { Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldarg_1), Instruction.Create(OpCodes.Ldloc, selected),
            Instruction.Create(OpCodes.Ldloca, result), Instruction.Create(OpCodes.Call, load), Instruction.Create(OpCodes.Brfalse, original),
            Instruction.Create(OpCodes.Ldloc, result), Instruction.Create(OpCodes.Ret) };
        files.Body.Variables.Add(result); files.Body.InitLocals = true; files.Body.MaxStackSize = Math.Max(4, files.Body.MaxStackSize);
        foreach (var instruction in injected) files.Body.GetILProcessor().InsertBefore(original, instruction);

        var all = methods[1];
        var sources = all.Body.Instructions.Single(i => i.Operand is MethodReference m && m.DeclaringType.FullName == "Verse.LoadedModManager" && m.Name == "LoadModXML");
        var sourceMethod = (MethodReference)sources.Operand;
        sources.Operand = Bridge(module, nameof(ParsedXmlRuntime), "LoadSources", sourceMethod.ReturnType, sourceMethod.Parameters[0].ParameterType);
        var combine = all.Body.Instructions.Single(i => i.Operand is MethodReference m && m.DeclaringType.FullName == "Verse.LoadedModManager" && m.Name == "CombineIntoUnifiedXML");
        var combineMethod = (MethodReference)combine.Operand;
        // The final argument becomes ref: complete private document/map pairs
        // publish through two non-failing reference assignments, never map edits.
        var mapLoad = combine.Previous;
        VariableDefinition map = mapLoad.OpCode == OpCodes.Ldloc_0 ? all.Body.Variables[0]
            : mapLoad.OpCode == OpCodes.Ldloc_1 ? all.Body.Variables[1]
            : mapLoad.OpCode == OpCodes.Ldloc_2 ? all.Body.Variables[2]
            : mapLoad.OpCode == OpCodes.Ldloc_3 ? all.Body.Variables[3] : (VariableDefinition)mapLoad.Operand;
        mapLoad.OpCode = OpCodes.Ldloca; mapLoad.Operand = map;
        combine.Operand = Bridge(module, nameof(ParsedXmlRuntime), "Combine", combineMethod.ReturnType,
            combineMethod.Parameters[0].ParameterType, new ByReferenceType(combineMethod.Parameters[1].ParameterType));
        ProcessedXmlPrepatch.Inject(module, all);
        var construct = methods[2].Body.Instructions.Single(i => i.Operand is MethodReference m
            && m.DeclaringType.FullName == "System.Activator" && m.Name == "CreateInstance");
        var ctor = (MethodReference)construct.Operand;
        construct.Operand = Bridge(module, nameof(EarlyLoadingObservation), "CreateMod", ctor.ReturnType, ctor.Parameters.Select(p => p.ParameterType).ToArray());
    }
}
