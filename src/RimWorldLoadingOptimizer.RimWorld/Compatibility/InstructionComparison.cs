// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace RimWorldLoadingOptimizer.RimWorld;

internal static class InstructionComparison
{
    internal static bool SameInstructions(IReadOnlyList<CodeInstruction> left, IReadOnlyList<CodeInstruction> right)
    {
        if (left.Count != right.Count)
            return false;
        Dictionary<Label, int>? leftLabels = LabelTargets(left), rightLabels = LabelTargets(right);
        if (leftLabels == null || rightLabels == null)
            return false;
        for (int index = 0; index < left.Count; index++)
        {
            CodeInstruction a = left[index], b = right[index];
            if (a.opcode != b.opcode || a.blocks.Count != b.blocks.Count
                || !SameOperand(a.operand, b.operand, leftLabels, rightLabels))
                return false;
            for (int block = 0; block < a.blocks.Count; block++)
                if (a.blocks[block].blockType != b.blocks[block].blockType || a.blocks[block].catchType != b.blocks[block].catchType)
                    return false;
        }
        return true;
    }

    private static Dictionary<Label, int>? LabelTargets(IReadOnlyList<CodeInstruction> code)
    {
        var targets = new Dictionary<Label, int>();
        for (int index = 0; index < code.Count; index++)
            foreach (Label label in code[index].labels)
            {
                if (targets.ContainsKey(label))
                    return null;
                targets.Add(label, index);
            }
        return targets;
    }

    private static bool SameOperand(object? left, object? right, Dictionary<Label, int> leftLabels, Dictionary<Label, int> rightLabels)
    {
        if (left == null || right == null)
            return left == null && right == null;
        if (left is Label a && right is Label b)
            return leftLabels.TryGetValue(a, out int x) && rightLabels.TryGetValue(b, out int y) && x == y;
        if (left is Label[] aa && right is Label[] bb)
            return aa.Length == bb.Length && aa.Where((label, index) => !SameOperand(label, bb[index], leftLabels, rightLabels)).Any() == false;
        if (left is LocalBuilder al && right is LocalBuilder bl)
            return al.LocalIndex == bl.LocalIndex && al.LocalType == bl.LocalType && al.IsPinned == bl.IsPinned;
        if (left is MemberInfo am && right is MemberInfo bm)
            return am.Equals(bm);
        if (left.GetType() != right.GetType())
            return false;
        if (left is float af && right is float bf)
            return BitConverter.GetBytes(af).SequenceEqual(BitConverter.GetBytes(bf));
        if (left is double ad && right is double bd)
            return BitConverter.GetBytes(ad).SequenceEqual(BitConverter.GetBytes(bd));
        if (left is string || left is byte || left is sbyte || left is short || left is ushort
            || left is int || left is uint || left is long || left is ulong)
            return left.Equals(right);
        // Refuse unfamiliar signature/operand forms instead of guessing equivalence.
        return false;
    }

}
