// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace WakeUp;

// Reorders only pure field/string tests. The live dictionary enumerator, first
// match, diagnostics and writes remain native. No state survives an iteration.
internal static class TranslationPredicateRewrite
{
    internal static IEnumerable<CodeInstruction> Apply(IEnumerable<CodeInstruction> input,
        ILGenerator generator, FieldInfo injected, FieldInfo normalized)
    {
        var code = input.Select(c => new CodeInstruction(c)).ToList();
        var matches = new List<int>();
        var equality = typeof(string).GetMethod("op_Equality", new[] { typeof(string), typeof(string) })!;
        bool Address(CodeInstruction c) => c.opcode == OpCodes.Ldloca || c.opcode == OpCodes.Ldloca_S;
        bool Branch(CodeInstruction c, OpCode shortOp, OpCode longOp) => c.opcode == shortOp || c.opcode == longOp;
        bool SameTarget(object a, object b) => Equals(a, b) ||
            a is Label left && b is Label right && code.Any(c => c.labels.Contains(left) && c.labels.Contains(right));
        for (int at = 7; at + 8 < code.Count; at++)
        {
            if (!code[at].LoadsField(injected)) continue;
            int s = at - 7;
            var p = code.GetRange(s, 16);
            if (!Address(p[0]) || p[1].operand is not MethodInfo key || key.Name != "get_Key" || key.ReturnType != typeof(string) ||
                p[3].operand is not MethodInfo equal1 || equal1 != equality || !Branch(p[4], OpCodes.Brtrue_S, OpCodes.Brtrue) ||
                !Address(p[5]) || !Equals(p[0].operand, p[5].operand) || p[6].operand is not MethodInfo value ||
                value.Name != "get_Value" || value.ReturnType != injected.DeclaringType || value.DeclaringType != key.DeclaringType ||
                !Branch(p[8], OpCodes.Brfalse_S, OpCodes.Brfalse) || !SameTarget(p[8].operand, p[4].operand) ||
                !Address(p[9]) || !Equals(p[0].operand, p[9].operand) || !Equals(p[10].operand, value) ||
                !p[11].LoadsField(normalized) || p[13].opcode != OpCodes.Ldind_Ref || !Equals(p[14].operand, equality) ||
                !Branch(p[15], OpCodes.Brfalse_S, OpCodes.Brfalse) || !SameTarget(p[15].operand, p[4].operand) ||
                p.Skip(1).Any(c => c.labels.Count != 0 || c.blocks.Count != 0) ||
                injected.FieldType != typeof(bool) || normalized.FieldType != typeof(string) || injected.DeclaringType != normalized.DeclaringType)
                throw new InvalidOperationException("Unreviewed duplicate-target predicate shape.");
            matches.Add(s);
        }
        if (matches.Count != 1) throw new InvalidOperationException("Expected exactly one duplicate-target predicate.");
        int start = matches[0];
        var old = code.GetRange(start, 16);
        CodeInstruction Copy(int i) { var c = new CodeInstruction(old[i]); c.labels.Clear(); c.blocks.Clear(); return c; }
        Label nonNull = generator.DefineLabel();
        // Keep the native null-self short circuit. A null other entry still
        // throws at its field read, into the unchanged native exception handler.
        var replacement = new List<CodeInstruction>
        {
            Copy(5), Copy(6), new(OpCodes.Dup), new(OpCodes.Brtrue, nonNull), new(OpCodes.Pop),
            Copy(0), Copy(1), Copy(2), Copy(3), Copy(4), new(OpCodes.Ldnull), Copy(7)
        };
        replacement[^1].labels.Add(nonNull);
        replacement.Add(Copy(8));
        replacement.AddRange(Enumerable.Range(9, 7).Select(Copy));
        replacement.AddRange(Enumerable.Range(0, 5).Select(Copy));
        replacement[0].labels.AddRange(old[0].labels);
        replacement[0].blocks.AddRange(old[0].blocks);
        // The longer block can exceed old short-branch distances.
        foreach (var c in replacement)
        {
            if (c.opcode == OpCodes.Brtrue_S) c.opcode = OpCodes.Brtrue;
            if (c.opcode == OpCodes.Brfalse_S) c.opcode = OpCodes.Brfalse;
        }
        code.RemoveRange(start, 16); code.InsertRange(start, replacement);
        for (int i = 0; i < code.Count; i++)
            code[i].opcode = code[i].opcode.Value switch
            {
                43 => OpCodes.Br, 44 => OpCodes.Brfalse, 45 => OpCodes.Brtrue,
                46 => OpCodes.Beq, 47 => OpCodes.Bge, 48 => OpCodes.Bgt,
                49 => OpCodes.Ble, 50 => OpCodes.Blt, 51 => OpCodes.Bne_Un,
                52 => OpCodes.Bge_Un, 53 => OpCodes.Bgt_Un, 54 => OpCodes.Ble_Un,
                55 => OpCodes.Blt_Un, 222 => OpCodes.Leave,
                _ => code[i].opcode
            };
        return code;
    }
}
