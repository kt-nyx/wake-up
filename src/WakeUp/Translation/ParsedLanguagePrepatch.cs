// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Prepatcher;

namespace WakeUp;

// Seven coordinated connections, preserving native entry points and consumers.
// Only cloned bodies are edited; failure restores every original body/reference.
public static class ParsedLanguagePrepatch
{
    internal static readonly string[] Types = { "Verse.LoadedLanguage", "Verse.LoadedLanguage", "Verse.LoadedLanguage", "Verse.LanguageDatabase", "Verse.DefInjectionPackage", "Verse.DefInjectionPackage", "Verse.DefInjectionPackage" };
    internal static readonly string[] Names = { "LoadData", "LoadFromFile_Keyed", "LoadFromFile_Strings", "Clear", "TryAddInjection", "TryAddFullListInjection", "AddDataFromFile" };
    internal static readonly string[] Original = {
        "B6C54C95DC2904F3485505BFFBF43DCD65CE4F5E570210A94DC9BA5E357DDD72",
        "2C5EC16802288395BEA254A1385DA51D818EB3EC29648B67E87B46911EE35DF2",
        "3541996E50B408FBF7299B112217DECF508018EDAF1C56FE84FB26D7BB433D01",
        "8B3E99BF726940FDA9FA9CCA8B86B5AE2654F0E33C8EC5BD8034B3F0680EEEFF",
        "C48BBF0A5F843183E940688567BDE83E89ED5F7D24CAB5FAA7471704D7CD4C30",
        "3329C8DB4ABF3C5033AB592877F00F1AEBBFA538DB1D39A8C25838B3DAD21092",
        "C1CE86DA34BF4BE716953898241C8E907F7CBA59336FF728D1BCE4A8F9FA22DD" };
    internal static readonly string[] Rewritten = {
        "BA9900BC8CCEC4E0D06D610242FC62B43E3DA259729BA069FE0595900CD68430",
        "363CA167966F3CC2D58860CAE986B8D2EF4DC708316A6C637B6A2BDD646FA109",
        "310636D2AF6CB7AD6D5605E70537DDDF57C39F0D438B0A278374218CAF686451",
        "EE20F4D51E47988CEC8E9ADC6E18536D3638186F4CDCBCBF1B2D84894824D949",
        "798DD92328DFF0EEC86C46CCCA91F578FEC0BE601FC0461B9F43BC6256D403B2",
        "B6BC2E82110539B5AD88DA23957171A7BB9733DB4C48AFC9DB1A1DC7BD698203",
        "A76F89CA5A251E11C491075FA5EBEA3B0A3A50AEA27C78CB733BA7D70C95CD65" };
    internal static MethodDefinition[] Targets(ModuleDefinition module) => Names.Select((name, i) =>
        module.GetType(Types[i]).Methods.Single(m => m.Name == name)).ToArray();

    [FreePatch]
    public static bool RewriteAssembly(ModuleDefinition module)
    {
        MethodDefinition[]? methods = null;
        MethodBody[]? originals = null;
        int references = module.AssemblyReferences.Count;
        try
        {
            methods = Targets(module);
            if (methods.Where((m, i) => !m.HasBody || AssetRoutingPrepatch.Fingerprint(m) != Original[i]).Any()) return false;
            originals = methods.Select(m => m.Body).ToArray();
            foreach (var method in methods) method.Body = Clone(method);
            Inject(module, methods);
            return true;
        }
        catch
        {
            if (methods != null && originals != null)
                for (int i = 0; i < methods.Length; i++) methods[i].Body = originals[i];
            while (module.AssemblyReferences.Count > references) module.AssemblyReferences.RemoveAt(module.AssemblyReferences.Count - 1);
            return false;
        }
    }

    internal static void Inject(ModuleDefinition module, MethodDefinition[] methods)
    {
        ReplaceLoop(module, methods[0], "LoadFromFile_DefInject", "InjectionGroup", true);
        ReplaceLoop(module, methods[0], "LoadFromFile_Keyed", "KeyedGroup", false);
        WrapLoad(module, methods[0]);
        SourceCall(module, methods[1], "Verse.DirectXmlLoaderSimple", "ValuesFromXmlFile", "KeyedRows");
        SourceCall(module, methods[2], "Verse.GenText", "LinesFromString", "StringLines");
        Before(methods[3], methods[3].Body.Instructions[0], Instruction.Create(OpCodes.Call,
            Bridge(module, "ParsedLanguageRuntime", "ClearSession", module.TypeSystem.Void)));
        CapturePrefix(module, methods[4], false);
        CapturePrefix(module, methods[5], true);
        ReplayInsideCatch(module, methods[6], methods[4], methods[5]);
        foreach (var method in methods) ExpandBranches(method);
    }

    private static MethodReference Bridge(ModuleDefinition module, string type, string name, TypeReference result, params TypeReference[] args)
        => ParsedXmlPrepatch.Bridge(module, type, name, result, args);
    private static void Before(MethodDefinition method, Instruction original, params Instruction[] code)
    { foreach (var instruction in code) method.Body.GetILProcessor().InsertBefore(original, instruction); }
    private static Instruction Copy(Instruction source)
    { var copy = Instruction.Create(OpCodes.Nop); copy.OpCode = source.OpCode; copy.Operand = source.Operand; return copy; }

    private static void ReplaceLoop(ModuleDefinition module, MethodDefinition method, string parser, string bridge, bool injection)
    {
        var code = method.Body.Instructions;
        int at = code.ToList().FindIndex(i => i.Operand is MethodReference m && m.Name == parser);
        int begin = at - (injection ? 11 : 10), end = at + 8;
        if (begin < 0 || code[begin].OpCode != OpCodes.Ldc_I4_0 || code[end].OpCode != OpCodes.Blt_S && code[end].OpCode != OpCodes.Blt)
            throw new InvalidOperationException("language-loop-control");
        var native = (MethodReference)code[at].Operand;
        var list = new GenericInstanceType(module.ImportReference(typeof(List<>))); list.GenericArguments.Add(native.Parameters[0].ParameterType);
        var texts = module.ImportReference(typeof(List<string>));
        var args = new List<TypeReference> { method.DeclaringType, list, texts };
        var replacement = new List<Instruction> { Instruction.Create(OpCodes.Ldarg_0), Copy(code[at - (injection ? 7 : 6)]), Copy(code[at - 3]) };
        if (injection) { replacement.Add(Copy(code[at - 4])); args.Add(native.Parameters[1].ParameterType); }
        replacement.Add(Instruction.Create(OpCodes.Call, Bridge(module, "ParsedLanguageRuntime", bridge, module.TypeSystem.Void, args.ToArray())));
        var removed = new HashSet<Instruction>(code.Skip(begin).Take(end - begin + 1));
        var first = code[begin];
        foreach (var instruction in code.Where(i => !removed.Contains(i)))
        {
            if (instruction.Operand is Instruction target && removed.Contains(target))
            { if (target != first) throw new InvalidOperationException("language-loop-entry"); instruction.Operand = replacement[0]; }
            if (instruction.Operand is Instruction[] targets && targets.Any(removed.Contains)) throw new InvalidOperationException("language-loop-switch");
        }
        if (method.Body.ExceptionHandlers.Any(h => removed.Contains(h.TryStart) || removed.Contains(h.TryEnd) || removed.Contains(h.HandlerStart) || removed.Contains(h.HandlerEnd)))
            throw new InvalidOperationException("language-loop-handler");
        Before(method, first, replacement.ToArray());
        foreach (var instruction in removed) code.Remove(instruction);
    }

    private static void SourceCall(ModuleDefinition module, MethodDefinition method, string parserType, string parser, string bridge)
    {
        var call = method.Body.Instructions.Single(i => i.Operand is MethodReference m && m.DeclaringType.FullName == parserType && m.Name == parser);
        var native = (MethodReference)call.Operand;
        // The original text argument remains on the stack. No extra path reads.
        var first = Instruction.Create(OpCodes.Ldarg_0);
        foreach (var instruction in method.Body.Instructions)
            if (ReferenceEquals(instruction.Operand, call)) instruction.Operand = first;
        Before(method, call, first, Instruction.Create(OpCodes.Ldarg_1));
        call.Operand = Bridge(module, "ParsedLanguageRuntime", bridge, native.ReturnType, module.TypeSystem.String,
            method.DeclaringType, method.Parameters[0].ParameterType);
    }

    private static void WrapLoad(ModuleDefinition module, MethodDefinition method)
    {
        var first = method.Body.Instructions[0];
        var token = new VariableDefinition(module.TypeSystem.Object);
        var complete = new VariableDefinition(module.TypeSystem.Boolean);
        method.Body.Variables.Add(token); method.Body.Variables.Add(complete); method.Body.InitLocals = true;
        var loaded = method.DeclaringType.Fields.Single(f => f.Name == "dataIsLoaded");
        Before(method, first, Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, loaded),
            Instruction.Create(OpCodes.Call, Bridge(module, "ParsedLanguageRuntime", "BeginLoad", module.TypeSystem.Object, method.DeclaringType, module.TypeSystem.Boolean)),
            Instruction.Create(OpCodes.Stloc, token));
        var end = Instruction.Create(OpCodes.Ret);
        foreach (var ret in method.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray())
        {
            ret.OpCode = OpCodes.Ldc_I4_1;
            var store = Instruction.Create(OpCodes.Stloc, complete);
            method.Body.GetILProcessor().InsertAfter(ret, store);
            method.Body.GetILProcessor().InsertAfter(store, Instruction.Create(OpCodes.Leave, end));
        }
        var cleanup = Instruction.Create(OpCodes.Ldloc, token);
        foreach (var handler in method.Body.ExceptionHandlers)
        { if (handler.TryEnd == null) handler.TryEnd = cleanup; if (handler.HandlerEnd == null) handler.HandlerEnd = cleanup; }
        method.Body.Instructions.Add(cleanup); method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldloc, complete));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Call, Bridge(module, "ParsedLanguageRuntime", "EndLoad", module.TypeSystem.Void, module.TypeSystem.Object, module.TypeSystem.Boolean)));
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Endfinally)); method.Body.Instructions.Add(end);
        method.Body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Finally) { TryStart = first, TryEnd = cleanup, HandlerStart = cleanup, HandlerEnd = end });
    }

    private static void CapturePrefix(ModuleDefinition module, MethodDefinition method, bool list)
    {
        var first = method.Body.Instructions[0];
        var args = new List<TypeReference> { method.DeclaringType, module.TypeSystem.String, method.Parameters[2].ParameterType };
        var code = new List<Instruction> { Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldarg_2), Instruction.Create(OpCodes.Ldarg_3) };
        if (list) { args.Add(method.Parameters[3].ParameterType); code.Add(Instruction.Create(OpCodes.Ldarg, method.Parameters[3])); }
        code.Add(Instruction.Create(OpCodes.Call, Bridge(module, "ParsedInjectionInstructions", list ? "CaptureList" : "CaptureScalar", module.TypeSystem.Boolean, args.ToArray())));
        code.Add(Instruction.Create(OpCodes.Brtrue, first)); code.Add(Instruction.Create(OpCodes.Ret));
        Before(method, first, code.ToArray());
    }

    private static void ReplayInsideCatch(ModuleDefinition module, MethodDefinition method, MethodDefinition scalar, MethodDefinition list)
    {
        var handler = method.Body.ExceptionHandlers.Single(h => h.HandlerType == ExceptionHandlerType.Catch);
        var first = handler.TryStart;
        var leave = method.Body.Instructions.TakeWhile(i => i != handler.HandlerStart).Last(i => i.OpCode == OpCodes.Leave || i.OpCode == OpCodes.Leave_S);
        var old = method.DeclaringType.Fields.Single(f => f.Name == "usedOldRepSyntax");
        var oldStore = method.Body.Instructions.Single(i => i.OpCode == OpCodes.Stfld && ((FieldReference)i.Operand).Name == old.Name);
        oldStore.OpCode = OpCodes.Call; oldStore.Operand = Bridge(module, "ParsedInjectionInstructions", "CaptureOldSyntax", module.TypeSystem.Void, method.DeclaringType, module.TypeSystem.Boolean);
        var opType = module.ImportReference(typeof(ParsedInjectionInstructions.Operation));
        var itemsType = module.ImportReference(typeof(List<ParsedInjectionInstructions.Operation>));
        var items = new VariableDefinition(itemsType); var index = new VariableDefinition(module.TypeSystem.Int32); var op = new VariableDefinition(opType);
        method.Body.Variables.Add(items); method.Body.Variables.Add(index); method.Body.Variables.Add(op); method.Body.InitLocals = true;
        var body = Instruction.Create(OpCodes.Nop); var check = Instruction.Create(OpCodes.Nop); var listLabel = Instruction.Create(OpCodes.Nop);
        var flag = Instruction.Create(OpCodes.Nop); var next = Instruction.Create(OpCodes.Nop);
        var code = new List<Instruction>();
        void Emit(Instruction i) => code.Add(i);
        void Field(string name) { Emit(Instruction.Create(OpCodes.Ldloc, op)); Emit(Instruction.Create(OpCodes.Ldfld, module.ImportReference(typeof(ParsedInjectionInstructions.Operation).GetField(name)!))); }
        Emit(Instruction.Create(OpCodes.Ldarg_0)); Emit(Instruction.Create(OpCodes.Ldarg_1)); Emit(Instruction.Create(OpCodes.Ldarg_3));
        Emit(Instruction.Create(OpCodes.Call, Bridge(module, "ParsedInjectionInstructions", "Take", itemsType, method.DeclaringType, method.Parameters[0].ParameterType, module.TypeSystem.String)));
        Emit(Instruction.Create(OpCodes.Stloc, items)); Emit(Instruction.Create(OpCodes.Ldloc, items)); Emit(Instruction.Create(OpCodes.Brfalse, first));
        Emit(Instruction.Create(OpCodes.Ldc_I4_0)); Emit(Instruction.Create(OpCodes.Stloc, index)); Emit(Instruction.Create(OpCodes.Br, check));
        Emit(body); Emit(Instruction.Create(OpCodes.Ldloc, items)); Emit(Instruction.Create(OpCodes.Ldloc, index));
        Emit(Instruction.Create(OpCodes.Callvirt, module.ImportReference(typeof(List<ParsedInjectionInstructions.Operation>).GetProperty("Item")!.GetGetMethod()))); Emit(Instruction.Create(OpCodes.Stloc, op));
        Field("Kind"); Emit(Instruction.Create(OpCodes.Ldc_I4_2)); Emit(Instruction.Create(OpCodes.Beq, flag));
        Field("Kind"); Emit(Instruction.Create(OpCodes.Ldc_I4_1)); Emit(Instruction.Create(OpCodes.Beq, listLabel));
        Emit(Instruction.Create(OpCodes.Ldarg_0)); Emit(Instruction.Create(OpCodes.Ldarg_1)); Field("Path"); Field("Text"); Emit(Instruction.Create(OpCodes.Call, scalar)); Emit(Instruction.Create(OpCodes.Br, next));
        Emit(listLabel); Emit(Instruction.Create(OpCodes.Ldarg_0)); Emit(Instruction.Create(OpCodes.Ldarg_1)); Field("Path");
        Emit(Instruction.Create(OpCodes.Ldloc, op)); Emit(Instruction.Create(OpCodes.Call, module.ImportReference(typeof(ParsedInjectionInstructions).GetMethod("FreshValues"))));
        Emit(Instruction.Create(OpCodes.Ldloc, op)); Emit(Instruction.Create(OpCodes.Call, module.ImportReference(typeof(ParsedInjectionInstructions).GetMethod("FreshComments"))));
        Emit(Instruction.Create(OpCodes.Call, list)); Emit(Instruction.Create(OpCodes.Br, next));
        Emit(flag); Emit(Instruction.Create(OpCodes.Ldarg_0)); Emit(Instruction.Create(OpCodes.Ldc_I4_1)); Emit(Instruction.Create(OpCodes.Stfld, old));
        Emit(next); Emit(Instruction.Create(OpCodes.Ldloc, index)); Emit(Instruction.Create(OpCodes.Ldc_I4_1)); Emit(Instruction.Create(OpCodes.Add)); Emit(Instruction.Create(OpCodes.Stloc, index));
        Emit(check); Emit(Instruction.Create(OpCodes.Ldloc, index)); Emit(Instruction.Create(OpCodes.Ldloc, items));
        Emit(Instruction.Create(OpCodes.Callvirt, module.ImportReference(typeof(List<ParsedInjectionInstructions.Operation>).GetProperty("Count")!.GetGetMethod())));
        Emit(Instruction.Create(OpCodes.Blt, body)); Emit(Instruction.Create(OpCodes.Leave, (Instruction)leave.Operand));
        Before(method, first, code.ToArray()); handler.TryStart = code[0];
    }

    private static MethodBody Clone(MethodDefinition method)
    {
        var old = method.Body; var result = new MethodBody(method) { InitLocals = old.InitLocals, MaxStackSize = old.MaxStackSize };
        foreach (var local in old.Variables) result.Variables.Add(new VariableDefinition(local.VariableType));
        var map = old.Instructions.ToDictionary(i => i, Copy);
        foreach (var source in old.Instructions)
        {
            var copy = map[source];
            if (source.Operand is Instruction target) copy.Operand = map[target];
            else if (source.Operand is Instruction[] targets) copy.Operand = targets.Select(t => map[t]).ToArray();
            else if (source.Operand is VariableDefinition variable) copy.Operand = result.Variables[variable.Index];
            result.Instructions.Add(copy);
        }
        Instruction? Map(Instruction? i) => i == null ? null : map[i];
        foreach (var h in old.ExceptionHandlers) result.ExceptionHandlers.Add(new ExceptionHandler(h.HandlerType) {
            TryStart = Map(h.TryStart), TryEnd = Map(h.TryEnd), HandlerStart = Map(h.HandlerStart), HandlerEnd = Map(h.HandlerEnd), FilterStart = Map(h.FilterStart), CatchType = h.CatchType });
        return result;
    }

    private static void ExpandBranches(MethodDefinition method)
    {
        var shortCodes = new[] { OpCodes.Br_S, OpCodes.Brfalse_S, OpCodes.Brtrue_S, OpCodes.Beq_S, OpCodes.Bge_S, OpCodes.Bgt_S, OpCodes.Ble_S, OpCodes.Blt_S, OpCodes.Bne_Un_S, OpCodes.Bge_Un_S, OpCodes.Bgt_Un_S, OpCodes.Ble_Un_S, OpCodes.Blt_Un_S, OpCodes.Leave_S };
        var longCodes = new[] { OpCodes.Br, OpCodes.Brfalse, OpCodes.Brtrue, OpCodes.Beq, OpCodes.Bge, OpCodes.Bgt, OpCodes.Ble, OpCodes.Blt, OpCodes.Bne_Un, OpCodes.Bge_Un, OpCodes.Bgt_Un, OpCodes.Ble_Un, OpCodes.Blt_Un, OpCodes.Leave };
        foreach (var i in method.Body.Instructions) { int at = Array.IndexOf(shortCodes, i.OpCode); if (at >= 0) i.OpCode = longCodes[at]; }
    }
}
