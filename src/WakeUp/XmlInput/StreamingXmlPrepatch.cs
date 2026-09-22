// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Prepatcher;

namespace WakeUp;

public static class StreamingXmlPrepatch
{
    internal const string NativeBody = "AD1D6439374DFDBF33BDED4C447B4C55E055FC3AFAB1837B1885FB3282B29A62";

    [FreePatch]
    public static bool RewriteAssembly(ModuleDefinition module)
    {
        try
        {
            var method = FindConstructor(module);
            if (method == null || AssetRoutingPrepatch.Fingerprint(method) != NativeBody) return false;
            Inject(module, method);
            return true;
        }
        catch (Exception) { return false; }
    }

    internal static MethodDefinition? FindConstructor(ModuleDefinition module)
        => module.GetType("Verse.LoadableXmlAsset")?.Methods.SingleOrDefault(m => m.IsConstructor && !m.IsStatic
            && m.Parameters.Count == 2 && m.Parameters[0].ParameterType.FullName == "System.IO.FileInfo"
            && m.Parameters[1].ParameterType.FullName == "Verse.ModContentPack");

    internal static void Inject(ModuleDefinition module, MethodDefinition method)
    {
        var type = method.DeclaringType;
        var document = type.Fields.Single(f => f.Name == "xmlDoc");
        var settings = type.Fields.Single(f => f.Name == "Settings");
        // Attribution and the object base constructor execute before this point.
        // The original outer try/catch and every original instruction stay intact.
        var original = method.Body.ExceptionHandlers.Where(h => h.HandlerType == ExceptionHandlerType.Catch)
            .Select(h => h.TryStart).OrderBy(i => method.Body.Instructions.IndexOf(i)).First();
        var scope = AssemblyNameReference.Parse(typeof(StreamingXmlPrepatch).Assembly.FullName);
        var existing = module.AssemblyReferences.FirstOrDefault(a => a.FullName == scope.FullName);
        if (existing != null) scope = existing;
        else module.AssemblyReferences.Add(scope);
        var runtime = new TypeReference("WakeUp", "StreamingXmlRuntime", module, scope);
        var bridge = new MethodReference("TryRead", module.TypeSystem.Boolean, runtime) { HasThis = false };
        bridge.Parameters.Add(new ParameterDefinition(method.Parameters[0].ParameterType));
        bridge.Parameters.Add(new ParameterDefinition(settings.FieldType));
        bridge.Parameters.Add(new ParameterDefinition(new ByReferenceType(document.FieldType)));
        var local = new VariableDefinition(document.FieldType);
        var fallback = Instruction.Create(OpCodes.Nop);
        var prefix = new[] {
            Instruction.Create(OpCodes.Ldarg_1), Instruction.Create(OpCodes.Ldsfld, settings),
            Instruction.Create(OpCodes.Ldloca, local), Instruction.Create(OpCodes.Call, bridge),
            Instruction.Create(OpCodes.Brfalse, fallback), Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Ldloc, local), Instruction.Create(OpCodes.Stfld, document),
            Instruction.Create(OpCodes.Ret), fallback
        };
        method.Body.Variables.Add(local);
        method.Body.InitLocals = true;
        method.Body.MaxStackSize = Math.Max(method.Body.MaxStackSize, 3);
        var il = method.Body.GetILProcessor();
        foreach (var instruction in prefix) il.InsertBefore(original, instruction);
    }
}
