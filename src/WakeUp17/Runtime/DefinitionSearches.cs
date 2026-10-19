// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml;
using HarmonyLib;

namespace WakeUp.V17;

internal sealed class DefinitionSearches
{
    internal const string Feature = "definitions";
    private static DefinitionSearches? instance;
    private static readonly MethodInfo Nodes = typeof(XmlNode).GetMethod(nameof(XmlNode.SelectNodes), new[] { typeof(string) })!;
    private static readonly MethodInfo Single = typeof(XmlNode).GetMethod(nameof(XmlNode.SelectSingleNode), new[] { typeof(string) })!;
    private readonly RuntimeContext context;
    private readonly Dictionary<MethodBase, int> slots = new();
    private readonly List<PublishedPatchGuard> workers = new();
    private readonly List<PublishedPatchGuard> queryGuards = new();
    private PublishedPatchGuard? stageGuard;
    private readonly ConcurrentDictionary<int, byte> yielded = new();
    [ThreadStatic] private static ScopedDefLookup? active;
    internal DefinitionSearches(RuntimeContext context) { this.context = context; instance = this; }

    internal void Install()
    {
        MethodInfo stage = context.NamedMethod("Verse.LoadedModManager", "ApplyPatches");
        Install(stage, new[] { "Add", "AddModExtension", "Insert", "Remove", "Replace", "SetName",
            "AttributeAdd", "AttributeRemove", "AttributeSet", "Test", "Conditional" }
            .Select(s => AccessTools.DeclaredMethod(context.Game.GetType("Verse.PatchOperation" + s, true), "ApplyWorker")!)
            .Concat(OptionalXmlQueries.Find()).ToArray());
    }

    // The production stage's source-map value is a game type. Resolve by name
    // and arity rather than loading that type into the synthetic test host.
    internal void Install(MethodInfo stage, IReadOnlyList<MethodInfo> targets)
    {
        // This hook only brackets an execution scope; it does not replace the
        // stage's work. Prefix/postfix callbacks remain native and in order.
        // Begin checks __runOriginal, so a supplier cache hit creates no index.
        // The lookup admits only the exact scoped XmlDocument and observes its
        // before/after mutation events. A replaced argument runs native. Worker
        // and XPath-operation guards still refuse every foreign interception.
        // Keep refusing rewritten stage bodies, including late publications.
        context.Require(stage); stageGuard = context.Guard(stage, allPatchKinds: false);
        foreach (MethodInfo method in QueryMethods().Concat(XPathQueryContract.Methods).Distinct()) queryGuards.Add(context.Guard(method));
        foreach (MethodInfo worker in targets)
        {
            bool patchAttempted = false;
            try
            {
                context.Require(worker);
                // The native query remains inside the original worker body.
                // Surrounding callbacks are not bypassed; rewritten bodies are.
                // Supplier helpers return their query result to arbitrary callers:
                // keep their admission strict. Native workers expose only a bool.
                var guard = context.Guard(worker, allPatchKinds: worker.Module.Assembly != context.Game);
                var native = PatchProcessor.GetOriginalInstructions(worker);
                if (native.Count(i => i.Calls(Nodes) || i.Calls(Single)) != 1)
                    throw new InvalidOperationException("Expected one native query.");
                slots.Add(worker, workers.Count); workers.Add(guard);
                patchAttempted = true;
                context.Patch(Feature, worker, transpiler: Hook(nameof(Rewrite)));
            }
            catch (Exception error)
            {
                if (patchAttempted)
                {
                    yielded.TryAdd(slots[worker], 0);
                    try { context.UnpatchTarget(Feature, worker); } catch { }
                }
                context.Status(Feature + "/" + worker.DeclaringType!.Name, "Ordinary query: " + error.Message);
            }
        }
        int admitted = slots.Count - yielded.Count;
        if (admitted == 0) throw new InvalidOperationException("No compatible native XML query workers.");
        context.Patch(Feature, stage, prefix: Hook(nameof(Begin)), finalizer: Hook(nameof(End)));
        context.Status(Feature, "Available for " + admitted + " native XML workers; execution counters follow when the stage finishes.");
    }

    private static MethodInfo Hook(string name) => RuntimeContext.Hook(typeof(DefinitionSearches), name);
    private static IEnumerable<MethodInfo> QueryMethods()
    {
        yield return Nodes; yield return Single;
        yield return typeof(XmlNode).GetMethod(nameof(XmlNode.CreateNavigator), Type.EmptyTypes)!;
        yield return typeof(XmlDocument).GetMethod(nameof(XmlDocument.CreateNavigator), Type.EmptyTypes)!;
        yield return AccessTools.Method(typeof(XmlDocument), nameof(XmlDocument.CreateNavigator), new[] { typeof(XmlNode) });
        Type list = typeof(XmlNode).Assembly.GetType("System.Xml.XPathNodeList", true)!;
        yield return AccessTools.Method(list, "Item", new[] { typeof(int) });
        yield return AccessTools.Method(list, "ReadUntil");
        yield return typeof(System.Xml.XPath.XPathNavigator).GetMethod("Select", new[] { typeof(string) })!;
        yield return typeof(System.Xml.XPath.XPathNavigator).GetMethod("SelectSingleNode", new[] { typeof(string) })!;
    }
    private bool Allowed(int slot) => context.StartupActive && !yielded.ContainsKey(slot)
        && stageGuard?.AllowsOriginalContract() == true && workers[slot].AllowsOriginalContract()
        && queryGuards.All(g => g.AllowsOriginalContract());

    private static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var code = instructions.Select(i => new CodeInstruction(i)).ToList();
        DefinitionSearches self = instance!;
        int slot = self.slots[__originalMethod];
        // During Harmony rebuild a foreign transpiler can already have edited
        // this input while the published patch record still looks unchanged.
        if (!InstructionComparison.SameInstructions(code, PatchProcessor.GetOriginalInstructions(__originalMethod)))
        {
            self.yielded.TryAdd(slot, 0);
            self.context.Status(Feature + "/" + __originalMethod.DeclaringType!.Name, "Ordinary query: worker changed during patch rebuilding.");
            return code;
        }
        int at = code.FindIndex(i => i.Calls(Nodes) || i.Calls(Single));
        if (at < 0 || code.Count(i => i.Calls(Nodes) || i.Calls(Single)) != 1) return code;
        var argument = new CodeInstruction(OpCodes.Ldc_I4, slot);
        argument.labels.AddRange(code[at].labels); code[at].labels.Clear();
        argument.blocks.AddRange(code[at].blocks); code[at].blocks.Clear();
        bool single = code[at].Calls(Single);
        code[at].opcode = OpCodes.Call; code[at].operand = Hook(single ? nameof(SelectSingle) : nameof(SelectNodes));
        code.Insert(at, argument);
        return code;
    }
    private static XmlNodeList SelectNodes(XmlNode node, string query, int slot)
        => active != null && instance!.Allowed(slot) ? active.SelectNodes(node, query) : node.SelectNodes(query)!;
    private static XmlNode? SelectSingle(XmlNode node, string query, int slot)
        => active != null && instance!.Allowed(slot) ? active.SelectSingleNode(node, query) : node.SelectSingleNode(query);
    private static void Begin(XmlDocument __0, bool __runOriginal, out Scope? __state)
    {
        __state = null;
        if (!__runOriginal || !instance!.context.StartupActive) return;
        __state = new Scope(active, active == null ? new ScopedDefLookup(__0) : null);
        // Reentrant XML application runs native and cannot contaminate its parent.
        active = __state.Lookup;
    }
    private static void End(Scope? __state)
    {
        if (__state == null) return;
        active = __state.Parent;
        if (__state.Lookup == null) return;
        instance!.context.Status(Feature, "Stage completed: " + __state.Lookup.Hits + " indexed queries, "
            + __state.Lookup.Fallbacks + " index fallbacks; index released. Unsupported work runs native outside these counters.");
        __state.Lookup.Dispose();
    }
    internal void Clear() { active?.Dispose(); active = null; }
    private sealed record Scope(ScopedDefLookup? Parent, ScopedDefLookup? Lookup);
}
