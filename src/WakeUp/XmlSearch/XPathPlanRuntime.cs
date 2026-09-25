// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml;
using System.Xml.XPath;
using HarmonyLib;

namespace WakeUp;

internal static class XPathPlanRuntime
{
    private const string Owner = "wakeup.def-lookup";
    private static readonly MethodInfo Nodes = typeof(XmlNode).GetMethod(nameof(XmlNode.SelectNodes), new[] { typeof(string) })!;
    private static readonly MethodInfo SelectString = typeof(XPathNavigator).GetMethod(nameof(XPathNavigator.Select), new[] { typeof(string) })!;
    private static readonly MethodInfo SelectExpression = typeof(XPathNavigator).GetMethod(nameof(XPathNavigator.Select), new[] { typeof(XPathExpression) })!;
    private static readonly MethodInfo Compile = typeof(XPathExpression).GetMethod(nameof(XPathExpression.Compile), new[] { typeof(string) })!;
    private static readonly List<PublishedPatchGuard> Guards = new();
    [ThreadStatic] private static ScopedXPathPlans? active;
    internal static bool Installed { get; private set; }
    internal static void Reset() { Installed = false; Guards.Clear(); }

    internal static bool Install(Harmony harmony)
    {
        try
        {
            if (!NativeStringWrapper() || Harmony.GetPatchInfo(Nodes)?.Transpilers.Any(p => p.owner != Owner) == true) return false;
            foreach (MethodInfo method in XPathQueryContract.Methods)
            {
                if (!PublishedPatchGuard.TryCreate(method, Owner, out var guard, allPatchKinds: true) || !guard!.AllowsOriginalContract())
                { Guards.Clear(); return false; }
                Guards.Add(guard);
            }
            harmony.Patch(Nodes, transpiler: new HarmonyMethod(typeof(XPathPlanRuntime), nameof(Transpiler)) { priority = Priority.Last });
            Installed = true;
            return true;
        }
        catch
        {
            harmony.Unpatch(Nodes, HarmonyPatchType.Transpiler, Owner);
            Guards.Clear(); Installed = false; return false;
        }
    }

    // The skipped native string wrapper must remain exactly a compile followed
    // by native expression selection. Do not infer that from a method name.
    internal static bool NativeStringWrapper()
    {
        var code = PatchProcessor.GetOriginalInstructions(SelectString).Where(i => i.opcode != OpCodes.Nop).ToArray();
        return code.Length == 5 && code[0].opcode == OpCodes.Ldarg_0 && code[1].opcode == OpCodes.Ldarg_1
            && code[2].opcode == OpCodes.Call && Equals(code[2].operand, Compile)
            && code[3].opcode == OpCodes.Callvirt && Equals(code[3].operand, SelectExpression)
            && code[4].opcode == OpCodes.Ret;
    }

    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = instructions.Select(i => new CodeInstruction(i)).ToList();
        if (Harmony.GetPatchInfo(Nodes)?.Transpilers.Any(p => p.owner != Owner) == true)
        { CompatibilityStatus.Guard("query-plans", false); return code; }
        var selected = code.Where(i => i.opcode == OpCodes.Callvirt && Equals(i.operand, SelectString)).ToArray();
        if (selected.Length != 1) throw new InvalidOperationException("Unique native XPath string selection unavailable.");
        selected[0].opcode = OpCodes.Call;
        selected[0].operand = AccessTools.Method(typeof(XPathPlanRuntime), nameof(Select));
        return code;
    }

    private static XPathNodeIterator Select(XPathNavigator navigator, string xpath)
        => active != null && CompatibilityStatus.Guard("query-plans", Guards.All(g => g.AllowsOriginalContract())) ? active.Select(navigator, xpath) : navigator.Select(xpath);

    internal static ScopedXPathPlans? Begin(XmlDocument document)
        => Installed ? active = new ScopedXPathPlans(document) : null;
    internal static void End(ScopedXPathPlans? scope)
    { if (ReferenceEquals(active, scope)) active = null; scope?.Dispose(); }
}
