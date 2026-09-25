// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml;
using HarmonyLib;
using Verse;

namespace WakeUp;

// XML Extensions counts the entire query result in PreCheck before any Patch
// method can mutate it. Adapt just that callsite, preserving its native count,
// stored enumerable, error checks and later ordered mutations.
internal static class ExtendedXmlQueryRuntime
{
    internal const string Owner = "wakeup.extended-xml-query";
    internal static readonly SupplierMethodContract[] Contracts = {
        new("PreCheck", "XmlExtensions.PatchOperationExtendedPathed", "PreCheck",
            "M|XmlExtensions:XmlExtensions.PatchOperationExtendedPathed|PreCheck|mscorlib:System.Boolean|I||System.Xml:System.Xml.XmlDocument", "6C62B11E8E0A78FF4FABDDC9B08B38DF1569E1EB5DB8826B23DC7B055052D63C"),
        new("SelectNodes", "XmlExtensions.Helpers", "SelectNodes",
            "M|XmlExtensions:XmlExtensions.Helpers|SelectNodes|System.Xml:System.Xml.XmlNodeList|S||mscorlib:System.String,System.Xml:System.Xml.XmlDocument,Assembly-CSharp:Verse.PatchOperation", "5D67D76C6A8D50C291AC266CCE91095D6790D886D8DD7AF79287C4D6F9329BEE")
    };
    private static Func<string, XmlDocument, PatchOperation, XmlNodeList>? original;
    private static Func<XmlNode, string, XmlNodeList?>? select;
    private static MethodInfo? precheck, helper;
    private static PublishedPatchGuard[] guards = Array.Empty<PublishedPatchGuard>();
    internal static bool SupplierPresent => AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "XmlExtensions");
    internal static bool Installed { get; private set; }
    internal static string Reason { get; private set; } = "not-attempted";
    internal static long Hits { get; private set; }
    internal static long Fallbacks { get; private set; }

    internal static bool Install(Func<XmlNode, string, XmlNodeList?> selector)
    {
        Assembly[] matches = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name == "XmlExtensions").ToArray();
        if (matches.Length != 1)
        {
            Reason = matches.Length == 0 ? "supplier-absent" : "ambiguous-supplier";
            return false;
        }
        return Install(matches[0], selector);
    }

    internal static bool Install(Assembly assembly, Func<XmlNode, string, XmlNodeList?> selector)
    {
        if (Installed) return true;
        var harmony = new Harmony(Owner);
        try
        {
            Dictionary<string, MethodBase> methods = SupplierMethodContract.ResolveAll(assembly, Contracts);
            precheck = (MethodInfo)methods["PreCheck"];
            helper = (MethodInfo)methods["SelectNodes"];
            var admitted = new List<PublishedPatchGuard>();
            foreach (MethodBase method in methods.Values)
            {
                if (!PublishedPatchGuard.TryCreate(method, Owner, out PublishedPatchGuard? guard, allPatchKinds: true)
                    || !guard!.AllowsOriginalContract())
                { Reason = "foreign-supplier-query-hook"; return false; }
                admitted.Add(guard);
            }
            guards = admitted.ToArray();
            original = (Func<string, XmlDocument, PatchOperation, XmlNodeList>)Delegate.CreateDelegate(
                typeof(Func<string, XmlDocument, PatchOperation, XmlNodeList>), helper);
            select = selector;
            harmony.Patch(precheck, transpiler: new HarmonyMethod(typeof(ExtendedXmlQueryRuntime), nameof(Transpiler))
                { priority = Priority.Last });
            Installed = true;
            Reason = "eager-precheck-query";
            return true;
        }
        catch (Exception exception)
        {
            if (precheck != null) harmony.Unpatch(precheck, HarmonyPatchType.Transpiler, Owner);
            select = null;
            guards = Array.Empty<PublishedPatchGuard>();
            Reason = "supplier-contract-" + exception.GetType().Name;
            return false;
        }
    }

    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = instructions.Select(i => new CodeInstruction(i)).ToList();
        // A later foreign transpiler causes recompilation: keep the supplier
        // helper call rather than attempting to interpret its new caller.
        if (precheck == null || helper == null || Harmony.GetPatchInfo(precheck)?.Transpilers.Any(p => p.owner != Owner) == true)
        { CompatibilityStatus.Guard("extended-query", false); return code; }
        CodeInstruction[] calls = code.Where(i => i.opcode == OpCodes.Call && Equals(i.operand, helper)).ToArray();
        if (calls.Length != 1) throw new InvalidOperationException("Unique eager XML query call unavailable.");
        calls[0].operand = AccessTools.Method(typeof(ExtendedXmlQueryRuntime), nameof(SelectNodes));
        return code;
    }

    private static XmlNodeList SelectNodes(string xpath, XmlDocument xml, PatchOperation operation)
    {
        // Root admission supplies the active loading document and all native
        // query publication guards. The helper is pure only at its pinned body
        // and while no foreign hook adds effects to this caller/helper chain.
        if (Installed && CompatibilityStatus.Guard("extended-query", guards.All(g => g.AllowsOriginalContract())) && select != null)
        {
            XmlNodeList? indexed = select(xml, xpath);
            if (indexed != null) { Hits++; return indexed; }
        }
        Fallbacks++;
        return original!(xpath, xml, operation);
    }

    internal static void Uninstall()
    {
        if (precheck != null) new Harmony(Owner).Unpatch(precheck, HarmonyPatchType.Transpiler, Owner);
        Installed = false;
        select = null;
        original = null;
        helper = null;
        precheck = null;
        guards = Array.Empty<PublishedPatchGuard>();
        Hits = Fallbacks = 0;
        Reason = "not-attempted";
    }
}
