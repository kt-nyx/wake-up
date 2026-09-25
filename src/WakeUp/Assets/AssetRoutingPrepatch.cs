// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Prepatcher;

namespace WakeUp;

// Rewrite the generic definition before Mono compiles it. Detouring one closed
// reference-type instance can also redirect other instances sharing Mono code.
// The bridge carries typeof(T) explicitly and leaves the original Get intact.
public static class AssetRoutingPrepatch
{
    internal const string NativeBody = "811DA2F28281EE33B67ADB230AC440AB0043D1DA59AF6B0861EA49942205F950";
    internal const string NativeBundleBody = "5A33A648283A7509939429E6F8AADE25A06BD44326BBB55981DE99134853E8DA";

    [FreePatch]
    public static bool RewriteAssembly(ModuleDefinition module)
    {
        try
        {
            MethodDefinition? method = FindGet(module);
            var bundle = module.GetType("Verse.ContentFinder`1")?.Methods.SingleOrDefault(m => m.Name == "TryFindAssetInModBundles");
            // Each bridge has its own native contract. A supplier may have
            // already wrapped Get while still calling the native bundle path.
            bool changed = TryInject(module, method, NativeBody, Inject);
            return TryInject(module, bundle, NativeBundleBody, InjectBundle) || changed;
        }
        catch (Exception) { return false; }
    }

    internal static bool TryInject(ModuleDefinition module, MethodDefinition? method, string expected,
        Action<ModuleDefinition, MethodDefinition> inject)
    {
        if (method?.HasBody != true || Fingerprint(method) != expected) return false;
        var body = method.Body;
        var instructions = body.Instructions.ToArray();
        var variables = body.Variables.ToArray();
        var references = module.AssemblyReferences.ToArray();
        bool locals = body.InitLocals;
        int stack = body.MaxStackSize;
        try { inject(module, method); return true; }
        catch
        {
            // These injectors only prepend instructions/add a local/reference.
            // Restore this attempt, including metadata, before reporting false.
            body.Instructions.Clear(); foreach (var item in instructions) body.Instructions.Add(item);
            body.Variables.Clear(); foreach (var item in variables) body.Variables.Add(item);
            body.InitLocals = locals; body.MaxStackSize = stack;
            foreach (var item in module.AssemblyReferences.Where(r => !references.Contains(r)).ToArray())
                module.AssemblyReferences.Remove(item);
            return false;
        }
    }

    internal static MethodDefinition? FindGet(ModuleDefinition module)
    {
        var type = module.GetType("Verse.ContentFinder`1");
        if (type == null || type.GenericParameters.Count != 1) return null;
        var candidates = type.Methods.Where(m => m.Name == "Get" && m.IsStatic && m.HasBody
            && m.Parameters.Count == 2 && m.Parameters[0].ParameterType.FullName == "System.String"
            && m.Parameters[1].ParameterType.FullName == "System.Boolean"
            && m.ReturnType is GenericParameter p && p.Position == 0 && p.Type == GenericParameterType.Type).ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }

    // Called only after the exact complete original body is admitted. All
    // imports/instructions are prepared before changing the original body.
    internal static void Inject(ModuleDefinition module, MethodDefinition method)
    {
        var scope = AssemblyNameReference.Parse(typeof(AssetRoutingPrepatch).Assembly.FullName);
        var existingScope = module.AssemblyReferences.FirstOrDefault(a => a.FullName == scope.FullName);
        if (existingScope != null) scope = existingScope;
        else module.AssemblyReferences.Add(scope);
        var runtime = new TypeReference("WakeUp", "AssetRoutingRuntime", module, scope);
        var bridge = new MethodReference("TryResolve", module.TypeSystem.Boolean, runtime) { HasThis = false };
        bridge.Parameters.Add(new ParameterDefinition(module.ImportReference(typeof(Type))));
        bridge.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
        bridge.Parameters.Add(new ParameterDefinition(new ByReferenceType(module.TypeSystem.Object)));
        var typeFromHandle = module.ImportReference(typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!);
        var local = new VariableDefinition(module.TypeSystem.Object);
        var generic = method.DeclaringType.GenericParameters[0];
        var original = method.Body.Instructions[0];
        // An admitted missing lookup resumes only native diagnostics, without
        // replaying Resources callbacks or bundle loads that already happened.
        var missing = method.Body.Instructions.Single(i => i.OpCode == OpCodes.Ldarg_1);
        var returned = Instruction.Create(OpCodes.Ldloc, local);
        var prefix = new[]
        {
            Instruction.Create(OpCodes.Ldtoken, generic),
            Instruction.Create(OpCodes.Call, typeFromHandle),
            Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Ldloca, local),
            Instruction.Create(OpCodes.Call, bridge),
            Instruction.Create(OpCodes.Brfalse, original),
            Instruction.Create(OpCodes.Ldloc, local),
            Instruction.Create(OpCodes.Brtrue, returned),
            Instruction.Create(OpCodes.Br, missing),
            returned,
            Instruction.Create(OpCodes.Unbox_Any, generic),
            Instruction.Create(OpCodes.Ret),
        };
        method.Body.Variables.Add(local);
        method.Body.InitLocals = true;
        method.Body.MaxStackSize = Math.Max(3, method.Body.MaxStackSize);
        var il = method.Body.GetILProcessor();
        foreach (var instruction in prefix) il.InsertBefore(original, instruction);
    }

    internal static void InjectBundle(ModuleDefinition module, MethodDefinition method)
    {
        var scope = AssemblyNameReference.Parse(typeof(AssetRoutingPrepatch).Assembly.FullName);
        var existing = module.AssemblyReferences.FirstOrDefault(a => a.FullName == scope.FullName);
        if (existing != null) scope = existing; else module.AssemblyReferences.Add(scope);
        var runtime = new TypeReference("WakeUp", "AssetRoutingRuntime", module, scope);
        var bridge = new MethodReference("TryBundles", module.TypeSystem.Boolean, runtime) { HasThis = false };
        bridge.Parameters.Add(new ParameterDefinition(module.ImportReference(typeof(Type))));
        bridge.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
        bridge.Parameters.Add(new ParameterDefinition(new ByReferenceType(module.TypeSystem.Object)));
        var local = new VariableDefinition(module.TypeSystem.Object);
        var original = method.Body.Instructions[0];
        var generic = method.DeclaringType.GenericParameters[0];
        var instructions = new[] {
            Instruction.Create(OpCodes.Ldtoken, generic),
            Instruction.Create(OpCodes.Call, module.ImportReference(typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!)),
            Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldloca, local),
            Instruction.Create(OpCodes.Call, bridge), Instruction.Create(OpCodes.Brfalse, original),
            Instruction.Create(OpCodes.Ldloc, local), Instruction.Create(OpCodes.Unbox_Any, generic), Instruction.Create(OpCodes.Ret)
        };
        method.Body.Variables.Add(local); method.Body.InitLocals = true;
        method.Body.MaxStackSize = Math.Max(3, method.Body.MaxStackSize);
        foreach (var instruction in instructions) method.Body.GetILProcessor().InsertBefore(original, instruction);
    }

    // Cecil is used only at preprocessing and in offline evidence. Runtime hit
    // admission uses the existing reflection-based SemanticMethodIdentity.
    // Resolved names/scopes replace unstable metadata tokens; branch positions,
    // signatures, flags, local layouts and exception regions all remain exact.
    internal static string Fingerprint(MethodDefinition method)
    {
        var text = new StringBuilder();
        var body = method.Body;
        text.Append(method.FullName).Append('|').Append((int)method.Attributes).Append('|')
            .Append((int)method.ImplAttributes).Append('|').Append((int)method.CallingConvention).Append('|')
            .Append(body.InitLocals).Append('|').Append(body.MaxStackSize).Append(';');
        foreach (var generic in method.DeclaringType.GenericParameters)
        {
            text.Append("generic:").Append(generic.Position).Append(':').Append((int)generic.Attributes);
            foreach (var constraint in generic.Constraints) text.Append(':').Append(TypeIdentity(constraint.ConstraintType));
            text.Append(';');
        }
        foreach (var parameter in method.Parameters)
            text.Append("parameter:").Append(parameter.Index).Append(':').Append((int)parameter.Attributes)
                .Append(':').Append(TypeIdentity(parameter.ParameterType)).Append(';');
        foreach (var local in body.Variables)
            text.Append("local:").Append(local.Index).Append(':').Append(TypeIdentity(local.VariableType)).Append(';');
        foreach (var instruction in body.Instructions)
        {
            text.Append(instruction.OpCode.Code).Append(':');
            object operand = instruction.Operand;
            if (operand is Instruction target) text.Append(body.Instructions.IndexOf(target));
            else if (operand is Instruction[] targets)
                foreach (var branch in targets) text.Append(body.Instructions.IndexOf(branch)).Append(',');
            else if (operand is TypeReference type) text.Append(TypeIdentity(type));
            else if (operand is MemberReference member)
                text.Append(member.FullName).Append('@').Append(TypeIdentity(member.DeclaringType));
            else if (operand is VariableDefinition variable) text.Append(variable.Index);
            else if (operand is ParameterDefinition parameter) text.Append(parameter.Index);
            else if (operand is string value) text.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(value)));
            else if (operand is float single) text.Append(BitConverter.ToString(BitConverter.GetBytes(single)));
            else if (operand is double number) text.Append(BitConverter.ToString(BitConverter.GetBytes(number)));
            else if (operand != null) text.Append(Convert.ToString(operand, CultureInfo.InvariantCulture));
            text.Append(';');
        }
        foreach (var handler in body.ExceptionHandlers)
            text.Append("eh:").Append(handler.HandlerType).Append(':').Append(Position(handler.TryStart)).Append(':')
                .Append(Position(handler.TryEnd)).Append(':').Append(Position(handler.HandlerStart)).Append(':')
                .Append(Position(handler.HandlerEnd)).Append(':').Append(Position(handler.FilterStart)).Append(':')
                .Append(handler.CatchType == null ? "-" : TypeIdentity(handler.CatchType)).Append(';');
        using var algorithm = SHA256.Create();
        return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "");

        int Position(Instruction? instruction) => instruction == null ? -1 : body.Instructions.IndexOf(instruction);
    }

    private static string TypeIdentity(TypeReference type)
        => type.FullName + "@" + (type.Scope is AssemblyNameReference assembly ? assembly.Name : type.Scope?.Name);
}
