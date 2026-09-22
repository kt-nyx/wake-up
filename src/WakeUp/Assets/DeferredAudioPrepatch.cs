// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Prepatcher;

namespace WakeUp;

// Core changes are coordinated by AssetRoutingPrepatch, after its own prefix.
// Public holders remain complete on escape. Only native path lookups bypass that
// exposure gate; no partially populated dictionary is returned to a mod caller.
public static class DeferredAudioPrepatch
{
    internal const string ConsumerMarker = "WakeUp.Generated.DeferredAudioConsumerContract";
    internal static readonly string[] NativeBodies = {
        "E567BF7654FE58D7962D871A83AFC8454C5D1547FF22CC54D2ABE58077E74055",
        "3107B9CDC6F9752775431444690AE1A452A32730BF1BBED37418CC4F54ACF973",
        "DE345BBDCFC85EEA329EF6F8B906572E35878BCCB1FDF54079F74AC2C0DECDD0",
        "D9A69B41B7F51EB2B2C79302AA0E7893D8B8C3E77605D3E106FDCFAD866EA279",
        "3E463EE55EE306B13CACBB0332EE8F885534C7C4F751691C61FE8985BAAD24F0",
        "4BCF8F5794D828E1BC93963CC00C43DF9EE647541F11CB4A48088993605F37FB",
        "4228F666D3273C136507A9164B6116BDF44EA7D9EB72CF3A1854E6D2F4632014",
        "B9580362F5CBFCFFC93A8131169E9F25DA54D162CE81A2A9D14E02C5CD46DE00",
        "B4E5DAC793A586A55CC489F48D3CE00C4580677D44C526A4D2F57A85A97CC459",
        "00F5D470EC21D5258937A2E9347F68B949E0BF7F9224CB726035A4D5364A703D",
        "E923087B37F3B4C722F17520F8DDA54C9B9691C4AEE341B4E7888D6DA3C8FFE7",
        "D728470B07D6C0103CCC8A94B5A62626FE60337BE73D2A5B4EC891E724FBCF00",
        "712806BABB1F955DF5CB0978C1FC4E523FF2066029C97E487F7E2137D603EC70"
    };

    internal static IEnumerable<TypeDefinition> Types(ModuleDefinition module)
    {
        foreach (var type in module.Types)
            foreach (var item in Descend(type)) yield return item;
    }
    private static IEnumerable<TypeDefinition> Descend(TypeDefinition type)
    {
        yield return type;
        foreach (var nested in type.NestedTypes)
            foreach (var item in Descend(nested)) yield return item;
    }
    internal static MethodDefinition[] Targets(ModuleDefinition module)
    {
        var holder = module.GetType("Verse.ModContentHolder`1");
        var pack = module.GetType("Verse.ModContentPack");
        var song = module.GetType("Verse.SongDef");
        var finder = module.GetType("Verse.ContentFinder`1");
        return new[] {
            holder.Methods.Single(m => m.Name == "ReloadAll"),
            holder.Methods.Single(m => m.Name == "Get"),
            holder.Methods.Single(m => m.Name == "GetAllUnderPath"),
            holder.Methods.Single(m => m.Name == "ClearDestroy"),
            pack.Methods.Single(m => m.Name == "GetContentHolder"),
            pack.Methods.Single(m => m.Name == "AnyNonTranslationContentLoaded"),
            song.Methods.Single(m => m.HasBody && m.Body.Instructions.Any(i => i.OpCode == OpCodes.Stfld && IsClip(i.Operand))),
            finder.NestedTypes.Single(t => t.Name.StartsWith("<GetAllInFolder>", StringComparison.Ordinal)).Methods.Single(m => m.Name == "MoveNext"),
            module.GetType("Verse.ModContentLoader`1").Methods.Single(m => m.Name == "LoadItem"),
            module.GetType("Verse.ModContentLoader`1").Methods.Single(m => m.Name == "ShouldStreamAudioClipFromFile"),
            pack.Methods.Single(m => m.Name == "GetAllFilesForMod"),
            module.GetType("RuntimeAudioClipLoader.Manager").Methods.Single(m => m.Name == "Load" && m.Parameters.Count == 6),
            module.GetType("Verse.ModContentLoader`1").Methods.Single(m => m.Name == "IsAcceptableExtension")
        };
    }

    internal static bool RewriteNative(ModuleDefinition module)
    {
        // Validate every core body before touching any of them. The existing
        // texture prefix is owned and validated by the caller.
        var targets = Targets(module);
        if (targets.Where((m, i) => AssetRoutingPrepatch.Fingerprint(m) != NativeBodies[i]).Any()) return false;
        if (module.GetType("Verse.SongDef").Methods.Any(m => m.Name == "WakeUpDeferredAudioContract")) return false;
        if (Types(module).SelectMany(t => t.Methods).Where(m => m.HasBody)
            .SelectMany(m => m.Body.Instructions).Any(i => IsClip(i.Operand) && i.OpCode == OpCodes.Ldflda)) return false;
        InjectNative(module, targets);
        return true;
    }

    internal static void InjectNative(ModuleDefinition module, MethodDefinition[] targets)
    {
        var runtime = Runtime(module);
        var getType = module.ImportReference(typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!);
        var sysType = module.ImportReference(typeof(Type));
        var generic = targets[0].DeclaringType.GenericParameters[0];
        var reload = Bridge(module, runtime, "TryReload", module.TypeSystem.Boolean, sysType, module.TypeSystem.Object, module.TypeSystem.Boolean);
        Prefix(targets[0], Instruction.Create(OpCodes.Ldtoken, generic), Instruction.Create(OpCodes.Call, getType),
            Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldarg_1), Instruction.Create(OpCodes.Call, reload),
            Instruction.Create(OpCodes.Brfalse, targets[0].Body.Instructions[0]), Instruction.Create(OpCodes.Ret));
        var ensure = Bridge(module, runtime, "EnsurePath", module.TypeSystem.Void, sysType, module.TypeSystem.Object, module.TypeSystem.String, module.TypeSystem.Boolean);
        foreach (int index in new[] { 1, 2 })
            Prefix(targets[index], Instruction.Create(OpCodes.Ldtoken, generic), Instruction.Create(OpCodes.Call, getType),
                Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldarg_1), Instruction.Create(index == 1 ? OpCodes.Ldc_I4_0 : OpCodes.Ldc_I4_1), Instruction.Create(OpCodes.Call, ensure));
        Prefix(targets[3], Instruction.Create(OpCodes.Ldtoken, generic), Instruction.Create(OpCodes.Call, getType), Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Call, Bridge(module, runtime, "ClearHolder", module.TypeSystem.Void, sysType, module.TypeSystem.Object)));

        var expose = Bridge(module, runtime, "ExposeHolder", module.TypeSystem.Void, sysType, module.TypeSystem.Object);
        var publicGetter = targets[4];
        var saved = new VariableDefinition(module.TypeSystem.Object);
        publicGetter.Body.Variables.Add(saved);
        publicGetter.Body.InitLocals = true;
        foreach (var ret in publicGetter.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray())
        {
            // Change the existing return itself so branches to it cannot skip
            // the exposure gate; leave its returned holder on the stack.
            ret.OpCode = OpCodes.Dup;
            ret.Operand = null;
            After(publicGetter, ret, Instruction.Create(OpCodes.Stloc, saved), Instruction.Create(OpCodes.Ldtoken, publicGetter.GenericParameters[0]),
                Instruction.Create(OpCodes.Call, getType), Instruction.Create(OpCodes.Ldloc, saved), Instruction.Create(OpCodes.Call, expose), Instruction.Create(OpCodes.Ret));
        }
        publicGetter.Body.MaxStackSize = Math.Max(4, publicGetter.Body.MaxStackSize);

        var audioField = module.GetType("Verse.ModContentPack").Fields.Single(f => f.Name == "audioClips");
        Prefix(targets[5], Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, audioField),
            Instruction.Create(OpCodes.Call, Bridge(module, runtime, "HasPending", module.TypeSystem.Boolean, module.TypeSystem.Object)),
            Instruction.Create(OpCodes.Brfalse, targets[5].Body.Instructions[0]), Instruction.Create(OpCodes.Ldc_I4_1), Instruction.Create(OpCodes.Ret));
        var song = module.GetType("Verse.SongDef");
        Prefix(targets[6], Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Call, Bridge(module, runtime, "ResolveSong", module.TypeSystem.Boolean, song)),
            Instruction.Create(OpCodes.Brfalse, targets[6].Body.Instructions[0]), Instruction.Create(OpCodes.Ret));
        RewriteLookup(module, AssetRoutingPrepatch.FindGet(module)!, runtime, getType);
        RewriteLookup(module, targets[7], runtime, getType);
        RewriteClipReads(module, runtime);
        foreach (var method in targets.Take(8).Concat(new[] { AssetRoutingPrepatch.FindGet(module)! })) WidenBranches(method);
        AddVersionMethod(song, "WakeUpDeferredAudioContract", module);
    }

    private static void RewriteLookup(ModuleDefinition module, MethodDefinition method, TypeReference runtime, MethodReference getType)
    {
        foreach (var call in method.Body.Instructions.Where(i => i.Operand is GenericInstanceMethod m && m.Name == "GetContentHolder" && m.DeclaringType.FullName == "Verse.ModContentPack").ToArray())
        {
            var original = (GenericInstanceMethod)call.Operand;
            var bridge = module.ImportReference(typeof(DeferredAudioRuntime).GetMethod("GetHolder"));
            var closed = new GenericInstanceMethod(bridge);
            closed.GenericArguments.Add(original.GenericArguments[0]);
            call.OpCode = OpCodes.Call;
            call.Operand = closed;
        }
    }

    // Dormant C10 research; no automatic Prepatcher registration.
    public static bool RewriteConsumers(ModuleDefinition module)
    {
        if (module.Assembly.Name.Name == "Assembly-CSharp" || module.Assembly.Name.Name == "WakeUp"
            || !module.AssemblyReferences.Any(a => a.Name == "Assembly-CSharp") || module.GetType(ConsumerMarker) != null) return false;
        var instructions = Types(module).SelectMany(t => t.Methods).Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).ToArray();
        // Address-taking cannot be expressed as an ordinary first-use getter.
        // Do not mark this assembly admitted; runtime will keep audio eager.
        if (instructions.Any(i => IsClip(i.Operand) && i.OpCode == OpCodes.Ldflda)) return false;
        if (Types(module).SelectMany(t => t.Methods).Where(m => m.HasBody).Any(HasReflectedClipAccess)) return false;
        RewriteClipReads(module, Runtime(module));
        var marker = new TypeDefinition("WakeUp.Generated", "DeferredAudioConsumerContract", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed, module.TypeSystem.Object);
        AddVersionMethod(marker, "Version", module);
        module.Types.Add(marker);
        return true;
    }

    private static bool IsClip(object operand) => operand is FieldReference field && field.Name == "clip" && field.DeclaringType.FullName == "Verse.SongDef";
    private static bool HasReflectedClipAccess(MethodDefinition method)
    {
        var instructions = method.Body.Instructions;
        bool namesClip = instructions.Any(i => i.Operand is TypeReference t && t.FullName == "Verse.SongDef"
            || i.Operand is string text && (text == "clip" || text == "Verse.SongDef"));
        return namesClip && instructions.Any(i => i.Operand is MethodReference m
            && ((m.DeclaringType.FullName == "System.Reflection.FieldInfo" && (m.Name.StartsWith("GetValue", StringComparison.Ordinal) || m.Name.StartsWith("SetValue", StringComparison.Ordinal)))
                || (m.Name == "Field" && m.DeclaringType.FullName == "HarmonyLib.AccessTools")));
    }
    private static void RewriteClipReads(ModuleDefinition module, TypeReference runtime)
    {
        foreach (var instruction in Types(module).SelectMany(t => t.Methods).Where(m => m.HasBody).SelectMany(m => m.Body.Instructions))
        {
            if (instruction.OpCode != OpCodes.Ldfld || !IsClip(instruction.Operand)) continue;
            var field = (FieldReference)instruction.Operand;
            instruction.OpCode = OpCodes.Call;
            instruction.Operand = Bridge(module, runtime, "GetSongClip", module.ImportReference(field.FieldType), module.ImportReference(field.DeclaringType));
        }
    }
    private static TypeReference Runtime(ModuleDefinition module)
    {
        var scope = AssemblyNameReference.Parse(typeof(DeferredAudioPrepatch).Assembly.FullName);
        var existing = module.AssemblyReferences.FirstOrDefault(a => a.FullName == scope.FullName);
        if (existing == null) module.AssemblyReferences.Add(scope); else scope = existing;
        return new TypeReference("WakeUp", "DeferredAudioRuntime", module, scope);
    }
    private static MethodReference Bridge(ModuleDefinition module, TypeReference runtime, string name, TypeReference result, params TypeReference[] parameters)
    {
        if (name == "ResolveSong" || name == "GetSongClip")
            return module.ImportReference(typeof(DeferredAudioRuntime).GetMethod(name));
        var method = new MethodReference(name, result, runtime) { HasThis = false };
        foreach (var parameter in parameters) method.Parameters.Add(new ParameterDefinition(parameter));
        return method;
    }
    private static void AddVersionMethod(TypeDefinition type, string name, ModuleDefinition module)
    {
        var marker = new MethodDefinition(name, MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Int32);
        marker.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4_3));
        marker.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        type.Methods.Add(marker);
    }
    private static void Prefix(MethodDefinition method, params Instruction[] instructions)
    {
        var first = method.Body.Instructions[0];
        var il = method.Body.GetILProcessor();
        foreach (var instruction in instructions) il.InsertBefore(first, instruction);
        method.Body.MaxStackSize = Math.Max(5, method.Body.MaxStackSize);
    }
    private static void After(MethodDefinition method, Instruction after, params Instruction[] instructions)
    {
        var il = method.Body.GetILProcessor();
        foreach (var instruction in instructions) { il.InsertAfter(after, instruction); after = instruction; }
    }
    private static void WidenBranches(MethodDefinition method)
    {
        foreach (var instruction in method.Body.Instructions)
        {
            switch (instruction.OpCode.Code)
            {
                case Code.Br_S: instruction.OpCode = OpCodes.Br; break;
                case Code.Brfalse_S: instruction.OpCode = OpCodes.Brfalse; break;
                case Code.Brtrue_S: instruction.OpCode = OpCodes.Brtrue; break;
                case Code.Beq_S: instruction.OpCode = OpCodes.Beq; break;
                case Code.Bge_S: instruction.OpCode = OpCodes.Bge; break;
                case Code.Bgt_S: instruction.OpCode = OpCodes.Bgt; break;
                case Code.Ble_S: instruction.OpCode = OpCodes.Ble; break;
                case Code.Blt_S: instruction.OpCode = OpCodes.Blt; break;
                case Code.Bne_Un_S: instruction.OpCode = OpCodes.Bne_Un; break;
                case Code.Bge_Un_S: instruction.OpCode = OpCodes.Bge_Un; break;
                case Code.Bgt_Un_S: instruction.OpCode = OpCodes.Bgt_Un; break;
                case Code.Ble_Un_S: instruction.OpCode = OpCodes.Ble_Un; break;
                case Code.Blt_Un_S: instruction.OpCode = OpCodes.Blt_Un; break;
                case Code.Leave_S: instruction.OpCode = OpCodes.Leave; break;
            }
        }
    }
}
