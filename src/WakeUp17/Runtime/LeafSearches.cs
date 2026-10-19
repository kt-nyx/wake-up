// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;

namespace WakeUp.V17;

internal sealed class LeafSearches
{
    internal const string Feature = "leaf-classes";
    private static LeafSearches? instance;
    private readonly RuntimeContext context;
    private readonly object gate = new();
    private readonly LeafSubclassIndex index = new();
    private PublishedPatchGuard[] guards = Array.Empty<PublishedPatchGuard>();
    private Func<List<Type>?> types = null!;
    private Func<Dictionary<Type, List<Type>>?> subclassesCache = null!;
    private bool enabled;
    private long hits, native;
    private int ownerThread, constructionDepth;
    [ThreadStatic] private static int alertEntries;
    internal LeafSearches(RuntimeContext context) { this.context = context; instance = this; }
    internal void Install()
    {
        Type gen = context.Game.GetType("Verse.GenTypes", true)!;
        var getTypes = AccessTools.PropertyGetter(gen, "AllTypes");
        var field = AccessTools.Field(gen, "allTypesCached");
        var cache = AccessTools.Field(gen, "cachedSubclasses");
        if (field?.FieldType != typeof(List<Type>)) throw new InvalidOperationException("Native type list changed.");
        if (cache?.FieldType != typeof(Dictionary<Type, List<Type>>)) throw new InvalidOperationException("Native subclass cache changed.");
        Install(context.Method("Verse.GenTypes", "AllLeafSubclasses", typeof(Type)),
            context.Method("Verse.GenTypes", "AllSubclasses", typeof(Type)), getTypes,
            AccessTools.Constructor(context.Game.GetType("RimWorld.AlertsReadout", true)), () => (List<Type>?)field.GetValue(null),
            () => (Dictionary<Type, List<Type>>?)cache.GetValue(null));
    }
    internal void Install(MethodInfo target, MethodInfo subclasses, MethodInfo getTypes, ConstructorInfo alerts,
        Func<List<Type>?> readTypes, Func<Dictionary<Type, List<Type>>?> readSubclasses)
    {
        types = readTypes; subclassesCache = readSubclasses;
        MethodInfo predicate = PatchProcessor.GetOriginalInstructions(target).Where(i => i.opcode == OpCodes.Ldftn)
            .Select(i => (MethodInfo)i.operand).Single();
        MethodInfo childPredicate = PatchProcessor.GetOriginalInstructions(subclasses).Where(i => i.opcode == OpCodes.Ldftn)
            .Select(i => (MethodInfo)i.operand).Single();
        var methods = new MethodBase[] { target, subclasses, getTypes, predicate, childPredicate, alerts };
        foreach (MethodBase method in methods) context.Require(method);
        guards = methods.Select(context.Guard).ToArray();
        context.Patch(Feature, target, transpiler: Hook(nameof(Rewrite)));
        context.Patch(Feature, alerts, prefix: Hook(nameof(BeginAlerts)), finalizer: Hook(nameof(EndAlerts)));
        enabled = true;
        context.Status(Feature, "Available during startup and native alert construction.");
    }
    private static MethodInfo Hook(string name) => RuntimeContext.Hook(typeof(LeafSearches), name);
    private static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var code = instructions.Select(i => new CodeInstruction(i)).ToList();
        if (!InstructionComparison.SameInstructions(code, PatchProcessor.GetOriginalInstructions(__originalMethod)))
        { instance!.enabled = false; instance.context.Status(Feature, "Ordinary search: method changed during patch rebuilding."); return code; }
        CodeInstruction where = code.Single(i => i.operand is MethodInfo m && m.DeclaringType == typeof(Enumerable)
            && m.Name == nameof(Enumerable.Where) && m.IsGenericMethod && m.GetGenericArguments().SequenceEqual(new[] { typeof(Type) }));
        where.operand = Hook(nameof(Filter));
        return code;
    }
    private static IEnumerable<Type> Filter(IEnumerable<Type> source, Func<Type, bool> predicate)
        => source.Where(type => instance!.Evaluate(type, predicate));
    private bool Allowed()
    {
        if (!enabled || !guards.Take(alertEntries != 0 ? 6 : 5).All(g => g.AllowsOriginalContract())) return false;
        int thread = Thread.CurrentThread.ManagedThreadId;
        lock (gate)
            if (context.StartupActive && constructionDepth == 0 && alertEntries == 0)
                Interlocked.CompareExchange(ref ownerThread, thread, 0);
        return Volatile.Read(ref ownerThread) == thread && ScopeActive();
    }
    private bool ScopeActive() => constructionDepth == 1 && alertEntries == 1
        || constructionDepth == 0 && alertEntries == 0 && context.StartupActive;
    private bool Evaluate(Type type, Func<Type, bool> predicate)
    {
        bool avoided = false;
        try
        {
            if (Allowed())
            {
                var source = types(); var cache = subclassesCache();
                bool proven = false;
                if (cache != null && !cache.ContainsKey(type))
                    lock (gate) proven = ScopeActive() && index.TryProveLeaf(type, source);
                if (proven && Allowed())
                    lock (gate)
                        if (ScopeActive() && ReferenceEquals(types(), source) && ReferenceEquals(subclassesCache(), cache)
                            && index.IsCurrent(source) && !cache!.ContainsKey(type))
                        { cache.Add(type, new List<Type>()); hits++; avoided = true; }
            }
        }
        catch { Release(); }
        if (!avoided) native++;
        // Always run the native predicate and preserve its cache/list semantics.
        return predicate(type);
    }
    private static void BeginAlerts(out int __state)
    {
        var self = instance!;
        alertEntries++; __state = 1;
        if (!self.enabled || alertEntries != 1 || !self.guards.All(g => g.AllowsOriginalContract())) return;
        int thread = Thread.CurrentThread.ManagedThreadId;
        lock (self.gate)
        {
            if (Interlocked.CompareExchange(ref self.ownerThread, thread, 0) != 0 && Volatile.Read(ref self.ownerThread) != thread) return;
            self.constructionDepth++; __state = 2;
        }
    }
    private static void EndAlerts(int __state)
    {
        if (__state == 0) return;
        alertEntries--;
        var self = instance!;
        if (__state == 2 && --self.constructionDepth == 0)
        {
            self.Release(); Volatile.Write(ref self.ownerThread, 0);
            self.context.Status(Feature, "Alert scope completed: " + self.hits + " total proven leaves, "
                + self.native + " native checks; index released.");
        }
    }
    private void Release() { lock (gate) index.Clear(); }
    internal void Clear()
    {
        Release();
        if (constructionDepth == 0) Volatile.Write(ref ownerThread, 0);
        context.Status(Feature, "Startup completed: " + hits + " proven leaves, " + native + " native checks; startup index released. Alert scopes remain bounded.");
    }
}
