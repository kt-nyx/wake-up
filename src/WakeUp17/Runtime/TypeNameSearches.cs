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

internal sealed class TypeNameSearches
{
    internal const string Feature = "type-names";
    private static TypeNameSearches? instance;
    private readonly RuntimeContext context;
    private readonly object gate = new();
    private PublishedPatchGuard[] guards = Array.Empty<PublishedPatchGuard>();
    private MethodInfo target = null!, allTypes = null!;
    private List<Segment>? snapshot;
    private readonly Dictionary<Assembly, Segment> staticSegments = new();
    private int generation, snapshotGeneration = -1;
    private long builds, maps, hits, native;
    private bool enabled;
    [ThreadStatic] private static bool building;
    internal TypeNameSearches(RuntimeContext context) { this.context = context; instance = this; }

    internal void Install()
    {
        if (System.Reflection.Metadata.MetadataUpdater.IsSupported)
            throw new InvalidOperationException("Ordinary type search: runtime metadata updates are enabled.");
        target = AccessTools.Method(typeof(AccessTools), nameof(AccessTools.TypeByName));
        allTypes = AccessTools.Method(typeof(AccessTools), nameof(AccessTools.AllTypes));
        guards = new[] { target, allTypes,
            AccessTools.Method(typeof(AccessTools), nameof(AccessTools.AllAssemblies)),
            AccessTools.Method(typeof(AccessTools), nameof(AccessTools.GetTypesFromAssembly)) }.Select(context.Guard).ToArray();
        AppDomain.CurrentDomain.AssemblyLoad += AssemblyLoaded;
        context.Patch(Feature, target, transpiler: RuntimeContext.Hook(typeof(TypeNameSearches), nameof(Rewrite)));
        enabled = true;
        context.Status(Feature, "Available for native type-name fallback during startup.");
    }

    private static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        TypeNameSearches self = instance!;
        var code = instructions.Select(i => new CodeInstruction(i)).ToList();
        if (!InstructionComparison.SameInstructions(code, PatchProcessor.GetOriginalInstructions(__originalMethod)))
        { self.enabled = false; self.context.Status(Feature, "Ordinary search: method changed during patch rebuilding."); return code; }
        int[] matches = Enumerable.Range(0, Math.Max(0, code.Count - 1)).Where(i => code[i].Calls(self.allTypes)
            && code[i + 1].operand is MethodInfo m && m.DeclaringType == typeof(Enumerable)
            && m.Name == nameof(Enumerable.ToArray) && m.IsGenericMethod
            && m.GetGenericArguments().SequenceEqual(new[] { typeof(Type) })).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("Native type fallback shape changed.");
        int at = matches[0];
        code[at].opcode = OpCodes.Ldarg_0; code[at].operand = null;
        code[at + 1].opcode = OpCodes.Call; code[at + 1].operand = RuntimeContext.Hook(typeof(TypeNameSearches), nameof(Candidates));
        return code;
    }

    private void AssemblyLoaded(object? sender, AssemblyLoadEventArgs args) => Interlocked.Increment(ref generation);
    private bool Allowed() => enabled && context.StartupActive && guards.All(g => g.AllowsOriginalContract());
    private static Type[] Candidates(string name) => instance!.Find(name);
    private Type[] Find(string name)
    {
        if (building || !Allowed()) return Original();
        Type[]? answer = null;
        try
        {
            lock (gate)
            {
                int before = Volatile.Read(ref generation);
                if (snapshotGeneration != before)
                {
                    building = true;
                    try { snapshot = Build(); }
                    finally { building = false; snapshotGeneration = before; }
                }
                if (snapshot != null)
                {
                    var dynamic = new Dictionary<Assembly, Type[]>();
                    foreach (Segment segment in snapshot)
                        if (segment.Index == null) dynamic.Add(segment.Assembly, AccessTools.GetTypesFromAssembly(segment.Assembly));
                    Type? match = null;
                    for (int pass = 0; pass < 2 && match == null; pass++)
                    foreach (Segment segment in snapshot)
                    {
                        match = segment.Index?.Find(name, pass == 0)
                            ?? (segment.Index == null ? dynamic[segment.Assembly].FirstOrDefault(t => (pass == 0 ? t.FullName : t.Name) == name) : null);
                        if (match != null) break;
                    }
                    if (before == Volatile.Read(ref generation)) answer = match == null ? Array.Empty<Type>() : new[] { match };
                }
            }
        }
        catch { lock (gate) { snapshot = null; snapshotGeneration = -1; } }
        // Patch inspection stays outside our lock; Harmony can itself call
        // TypeByName while holding the patch processor lock.
        if (Allowed() && answer != null) { Interlocked.Increment(ref hits); return answer; }
        lock (gate) { snapshot = null; snapshotGeneration = -1; }
        return Original();
    }
    private List<Segment>? Build()
    {
        Interlocked.Increment(ref builds);
        int count = 0, characters = 0;
        var result = new List<Segment>();
        foreach (Assembly assembly in AccessTools.AllAssemblies())
        {
            // Assemblies keep their native order. A late assembly requires a
            // refreshed sequence, not rebuilding every unchanged type map.
            // Dynamic/collectible assemblies remain native and refresh per call.
            bool reusable = !assembly.IsDynamic && !assembly.IsCollectible;
            if (reusable && staticSegments.TryGetValue(assembly, out Segment? cached))
            {
                count += cached.Types; characters += cached.Characters;
                if (count > TypeLookupIndex.MaximumTypes || characters > TypeLookupIndex.MaximumNameCharacters) return null;
                result.Add(cached);
                continue;
            }
            int oldCount = count, oldCharacters = characters;
            var index = reusable ? TypeLookupIndex.Create(assembly.GetTypes(), ref count, ref characters) : null;
            if (index == null && reusable) return null;
            if (reusable) Interlocked.Increment(ref maps);
            var segment = new Segment(assembly, index, count - oldCount, characters - oldCharacters);
            if (reusable) staticSegments.Add(assembly, segment);
            result.Add(segment);
        }
        return result;
    }
    private Type[] Original() { Interlocked.Increment(ref native); return AccessTools.AllTypes().ToArray(); }
    internal void Clear()
    {
        enabled = false;
        AppDomain.CurrentDomain.AssemblyLoad -= AssemblyLoaded;
        lock (gate) { snapshot = null; snapshotGeneration = -1; staticSegments.Clear(); }
        context.Status(Feature, "Startup completed: " + hits + " indexed fallbacks, " + native + " native fallbacks, " + maps + " static type maps, " + builds + " assembly sequence refreshes; indexes released.");
    }
    private sealed record Segment(Assembly Assembly, TypeLookupIndex? Index, int Types, int Characters);
}
