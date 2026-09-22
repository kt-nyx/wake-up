// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld.IO;
using Verse;

namespace WakeUp;

// Interpret source with the actual pinned parser, before state-dependent native
// insertions. Replay stays inside AddDataFromFile's original try/catch and calls
// its actual insertion methods. No merged records or predicted Def names persist.
public static class ParsedInjectionInstructions
{
    public sealed class Operation
    {
        public byte Kind; // scalar, full list, then the legacy syntax flag
        public string Path = "";
        public string? Text;
        public List<string>? Values;
        public List<Pair<int, string>>? Comments;
    }
    private sealed class Capture
    {
        internal DefInjectionPackage Package = null!;
        internal List<Operation> Operations = new();
    }
    internal sealed class Replay : IDisposable
    {
        internal readonly VirtualFile File;
        internal readonly string Text;
        internal readonly Type Type;
        internal readonly List<Operation> Operations;
        internal int Calls;
        internal bool Consumed;
        private readonly Replay? previous;
        internal Replay(VirtualFile file, string text, Type type, List<Operation> operations)
        { File = file; Text = text; Type = type; Operations = operations; previous = replay; replay = this; }
        public void Dispose() { replay = previous; }
    }
    [ThreadStatic] private static Capture? capture;
    [ThreadStatic] private static Replay? replay;
    private static readonly MethodInfo Scalar = AccessTools.Method(typeof(DefInjectionPackage), "TryAddInjection");
    private static readonly MethodInfo FullList = AccessTools.Method(typeof(DefInjectionPackage), "TryAddFullListInjection");
    private static readonly FieldInfo OldSyntax = AccessTools.Field(typeof(DefInjectionPackage), "usedOldRepSyntax");

    internal static void Install(Harmony harmony)
    {
        harmony.Patch(Scalar, prefix: new HarmonyMethod(typeof(ParsedInjectionInstructions), nameof(CaptureScalar)));
        harmony.Patch(FullList, prefix: new HarmonyMethod(typeof(ParsedInjectionInstructions), nameof(CaptureList)));
        harmony.Patch(AccessTools.Method(typeof(DefInjectionPackage), "AddDataFromFile"),
            transpiler: new HarmonyMethod(typeof(ParsedInjectionInstructions), nameof(Transpiler)) { priority = Priority.Last });
    }
    internal static List<Operation>? Parse(VirtualFile file, string text, Type type)
    {
        if (capture != null || replay != null) return null;
        var frame = new Capture { Package = new DefInjectionPackage(type) };
        try
        {
            capture = frame;
            frame.Package.AddDataFromFile(file, out bool error, text);
            return error || frame.Package.loadErrors.Count != 0 || frame.Operations.Count > 262144 ? null : frame.Operations;
        }
        finally { capture = null; }
    }
    public static bool CaptureScalar(DefInjectionPackage __instance, string key, string translation)
    {
        if (capture != null && ReferenceEquals(capture.Package, __instance))
        {
            if (capture.Operations.Count >= 262144) throw new InvalidDataException("language-instruction-count");
            capture.Operations.Add(new Operation { Kind = 0, Path = key, Text = translation }); return false;
        }
        if (replay != null && replay.Consumed && __instance.defType == replay.Type) replay.Calls++;
        return true;
    }
    public static bool CaptureList(DefInjectionPackage __instance, string key, List<string> translation, List<Pair<int, string>> comments)
    {
        if (capture != null && ReferenceEquals(capture.Package, __instance))
        {
            if (capture.Operations.Count >= 262144) throw new InvalidDataException("language-instruction-count");
            capture.Operations.Add(new Operation { Kind = 1, Path = key,
                Values = translation == null ? null : new List<string>(translation),
                Comments = comments == null ? null : new List<Pair<int, string>>(comments) }); return false;
        }
        if (replay != null && replay.Consumed && __instance.defType == replay.Type) replay.Calls++;
        return true;
    }
    public static void CaptureOldSyntax(DefInjectionPackage package, bool value)
    {
        package.usedOldRepSyntax = value;
        if (capture != null && ReferenceEquals(capture.Package, package))
        {
            if (!value || capture.Operations.Count >= 262144) throw new InvalidDataException("language-old-syntax-contract");
            capture.Operations.Add(new Operation { Kind = 2 });
        }
    }
    public static List<Operation>? Take(DefInjectionPackage package, VirtualFile file, string text)
    {
        if (capture != null || replay == null || replay.Consumed || !ReferenceEquals(replay.File, file)
            || !ReferenceEquals(replay.Text, text) || package.defType != replay.Type) return null;
        replay.Consumed = true; return replay.Operations;
    }
    public static List<string>? FreshValues(Operation op) => op.Values == null ? null : new List<string>(op.Values);
    public static List<Pair<int, string>>? FreshComments(Operation op) => op.Comments == null ? null : new List<Pair<int, string>>(op.Comments);

    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> source, ILGenerator generator)
    {
        var code = source.Select(c => new CodeInstruction(c)).ToList();
        // The enclosing runtime validates the pinned native semantic body and
        // rejects foreign parser/insertion hooks before these instructions run.
        int first = code.FindIndex(c => c.blocks.Any(b => b.blockType == ExceptionBlockType.BeginExceptionBlock));
        int catcher = code.FindIndex(c => c.blocks.Any(b => b.blockType == ExceptionBlockType.BeginCatchBlock));
        int exit = code.FindLastIndex(catcher - 1, c => c.opcode == OpCodes.Leave || c.opcode == OpCodes.Leave_S);
        if (first < 0 || catcher < 0 || exit < first || code.Count(c => c.StoresField(OldSyntax)) != 1)
            throw new InvalidOperationException("language-instruction-parser-shape");
        object leave = code[exit].operand;
        foreach (var instruction in code)
            if (instruction.StoresField(OldSyntax)) { instruction.opcode = OpCodes.Call; instruction.operand = AccessTools.Method(typeof(ParsedInjectionInstructions), nameof(CaptureOldSyntax)); }
        var items = generator.DeclareLocal(typeof(List<Operation>));
        var index = generator.DeclareLocal(typeof(int));
        var op = generator.DeclareLocal(typeof(Operation));
        var original = generator.DefineLabel(); var body = generator.DefineLabel(); var check = generator.DefineLabel();
        var list = generator.DefineLabel(); var flag = generator.DefineLabel(); var next = generator.DefineLabel();
        var add = new List<CodeInstruction>();
        void Emit(OpCode opcode, object? operand = null) => add.Add(new CodeInstruction(opcode, operand));
        void Label(Label label) { var nop = new CodeInstruction(OpCodes.Nop); nop.labels.Add(label); add.Add(nop); }
        void Field(string name) { Emit(OpCodes.Ldloc, op); Emit(OpCodes.Ldfld, AccessTools.Field(typeof(Operation), name)); }
        Emit(OpCodes.Ldarg_0); Emit(OpCodes.Ldarg_1); Emit(OpCodes.Ldarg_3);
        Emit(OpCodes.Call, AccessTools.Method(typeof(ParsedInjectionInstructions), nameof(Take)));
        Emit(OpCodes.Stloc, items); Emit(OpCodes.Ldloc, items); Emit(OpCodes.Brfalse, original);
        Emit(OpCodes.Ldc_I4_0); Emit(OpCodes.Stloc, index); Emit(OpCodes.Br, check);
        Label(body); Emit(OpCodes.Ldloc, items); Emit(OpCodes.Ldloc, index);
        Emit(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(List<Operation>), "Item")); Emit(OpCodes.Stloc, op);
        Field(nameof(Operation.Kind)); Emit(OpCodes.Ldc_I4_2); Emit(OpCodes.Beq, flag);
        Field(nameof(Operation.Kind)); Emit(OpCodes.Ldc_I4_1); Emit(OpCodes.Beq, list);
        Emit(OpCodes.Ldarg_0); Emit(OpCodes.Ldarg_1); Field(nameof(Operation.Path)); Field(nameof(Operation.Text));
        Emit(OpCodes.Call, Scalar); Emit(OpCodes.Br, next);
        Label(list); Emit(OpCodes.Ldarg_0); Emit(OpCodes.Ldarg_1); Field(nameof(Operation.Path));
        Emit(OpCodes.Ldloc, op); Emit(OpCodes.Call, AccessTools.Method(typeof(ParsedInjectionInstructions), nameof(FreshValues)));
        Emit(OpCodes.Ldloc, op); Emit(OpCodes.Call, AccessTools.Method(typeof(ParsedInjectionInstructions), nameof(FreshComments)));
        Emit(OpCodes.Call, FullList); Emit(OpCodes.Br, next);
        Label(flag); Emit(OpCodes.Ldarg_0); Emit(OpCodes.Ldc_I4_1); Emit(OpCodes.Stfld, OldSyntax);
        Label(next); Emit(OpCodes.Ldloc, index); Emit(OpCodes.Ldc_I4_1); Emit(OpCodes.Add); Emit(OpCodes.Stloc, index);
        Label(check); Emit(OpCodes.Ldloc, index); Emit(OpCodes.Ldloc, items);
        Emit(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(List<Operation>), "Count")); Emit(OpCodes.Blt, body);
        Emit(OpCodes.Leave, leave);
        add[0].blocks.AddRange(code[first].blocks); code[first].blocks.Clear();
        code[first].labels.Add(original); code.InsertRange(first, add); return code;
    }
}
