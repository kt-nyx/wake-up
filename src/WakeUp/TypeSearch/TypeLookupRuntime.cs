// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;
using Verse;

namespace WakeUp;

internal static class TypeLookupRuntime
{
    private const string Owner = "wakeup.type-lookup";
    private static readonly MethodInfo Target = AccessTools.Method(typeof(AccessTools), nameof(AccessTools.TypeByName));
    private static readonly object Gate = new();
    private static bool attempted, installed, candidate;
    private static int finished, generation;
    private static string? evidencePath;
    private static LookupSegment[]? snapshot;
    private static int snapshotGeneration = -1;
    private static PublishedPatchGuard? patchGuard;
    private static PublishedPatchGuard[]? enumeratorGuards;
    private static long calls, misses, errors, ticks, fallbackCalls, lookups, builds, refusals, originals, dynamicReads;
    private static int indexedTypes;
    [ThreadStatic] private static bool building;

    internal static bool ActiveCandidate => installed && candidate && Volatile.Read(ref finished) == 0;
    internal static bool CandidateSelected => installed && candidate;
    internal static void LeafReceipt(string kind, string reason, string? fields = null) => Receipt(kind, reason, fields);

    internal static bool TryInitialize(IReadOnlyList<string> arguments)
    {
        if (!arguments.Any(a => a != null && a.StartsWith("--wake-up-strategy=type-lookup", StringComparison.Ordinal)))
            return false;
        if (attempted)
            return true;
        attempted = true;
        var harmony = new Harmony(Owner);
        try
        {
            StartupLaunchDecision mode = StartupLaunchSelector.Parse(arguments);
            if (arguments.Count(a => a != null && a.StartsWith("--wake-up-strategy=", StringComparison.Ordinal)) != 1
                || !arguments.Contains("--wake-up-strategy=type-lookup") || mode.Selection == StartupSelection.Off || PlayDataLoader.Loaded)
                return true;
            evidencePath = Path.Combine(mode.SaveDataRoot!, "WakeUp", "type-lookup.jsonl");
            candidate = mode.Selection == StartupSelection.Candidate;
            if (!RuntimeIdentity.ValidateBinaryIdentity(out string reason))
            {
                Receipt("refused", reason);
                return true;
            }
            if (candidate)
            {
                var guards = new List<PublishedPatchGuard>();
                foreach (string name in new[] { nameof(AccessTools.AllAssemblies), nameof(AccessTools.AllTypes), nameof(AccessTools.GetTypesFromAssembly) })
                {
                    MethodInfo method = AccessTools.Method(typeof(AccessTools), name);
                    if (Harmony.GetPatchInfo(method)?.Owners.Any() == true
                        || !PublishedPatchGuard.TryCreate(method, Owner, out PublishedPatchGuard? guard, allPatchKinds: true))
                    {
                        Receipt("refused", "foreign-type-enumerator-patch");
                        return true;
                    }
                    guards.Add(guard!);
                }
                enumeratorGuards = guards.ToArray();
                if (Harmony.GetPatchInfo(Target)?.Transpilers.Any(p => p.owner != Owner) == true
                    || !PublishedPatchGuard.TryCreate(Target, Owner, out patchGuard))
                {
                    Receipt("refused", "foreign-or-unavailable-type-lookup-patch-state");
                    return true;
                }
                AppDomain.CurrentDomain.AssemblyLoad += AssemblyLoaded;
            }
            harmony.Patch(Target, prefix: new HarmonyMethod(typeof(TypeLookupRuntime), nameof(Prefix)),
                finalizer: new HarmonyMethod(typeof(TypeLookupRuntime), nameof(Finalizer)),
                transpiler: new HarmonyMethod(typeof(TypeLookupRuntime), nameof(Transpiler)) { priority = Priority.Last });
            harmony.Patch(AccessTools.Method(typeof(ModContentPack).Assembly.GetType("Verse.Root_Entry", true), "Update"),
                postfix: new HarmonyMethod(typeof(TypeLookupRuntime), nameof(MenuUpdate)));
            installed = true;
            Receipt("installed", candidate ? "ordered-fallback-index" : "original-fallback-timing",
                "\"optimizationEnabled\":" + (candidate ? "true" : "false"));
        }
        catch (Exception exception)
        {
            try
            {
                harmony.UnpatchAll(Owner);
            }
            catch { }
            AppDomain.CurrentDomain.AssemblyLoad -= AssemblyLoaded;
            installed = false;
            Receipt("refused", "installation-" + exception.GetType().Name);
        }
        return true;
    }

    private static void AssemblyLoaded(object sender, AssemblyLoadEventArgs args) => Interlocked.Increment(ref generation);
    private static void Prefix(out long __state) => __state = installed && Volatile.Read(ref finished) == 0 ? Stopwatch.GetTimestamp() : 0;
    private static void Finalizer(long __state, Type? __result, Exception? __exception)
    {
        if (__state == 0)
            return;
        Interlocked.Add(ref ticks, Stopwatch.GetTimestamp() - __state);
        Interlocked.Increment(ref calls);
        if (__exception != null)
            Interlocked.Increment(ref errors);
        else if (__result == null)
            Interlocked.Increment(ref misses);
    }

    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = instructions.Select(i => new CodeInstruction(i)).ToList();
        if (Harmony.GetPatchInfo(Target)?.Transpilers.Any(p => p.owner != Owner) == true
            || !InstructionComparison.SameInstructions(code, PatchProcessor.GetOriginalInstructions(Target)))
        { CompatibilityStatus.Guard("type-name", false); return code; }
        int[] matches = Enumerable.Range(0, Math.Max(0, code.Count - 1)).Where(i =>
            code[i].opcode == OpCodes.Call && Equals(code[i].operand, AccessTools.Method(typeof(AccessTools), nameof(AccessTools.AllTypes)))
            && code[i + 1].opcode == OpCodes.Call && code[i + 1].operand is MethodInfo method
            && method.DeclaringType == typeof(Enumerable) && method.Name == nameof(Enumerable.ToArray)
            && method.IsGenericMethod && method.GetGenericArguments().SequenceEqual(new[] { typeof(Type) })).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException("Original TypeByName fallback unavailable.");
        int index = matches[0];
        code[index].opcode = OpCodes.Ldarg_0;
        code[index].operand = null;
        code[index + 1].operand = AccessTools.Method(typeof(TypeLookupRuntime), nameof(FallbackCandidates));
        // Type.GetType, per-assembly GetType, full-name-first/simple-name-second
        // predicates, missing-type logging, and exceptions remain original.
        return code;
    }

    private static Type[] FallbackCandidates(string name)
    {
        Interlocked.Increment(ref fallbackCalls);
        if (!installed || !candidate || Volatile.Read(ref finished) != 0 || building)
            return OriginalTypes();
        // Harmony may call TypeByName while holding its patch-processor lock.
        // Never inspect patch records while holding our snapshot Gate.
        if (!CompatiblePatches())
        {
            CompatibilityStatus.Guard("type-name", false);
            ForgetSnapshot();
            return OriginalTypes();
        }
        Type[]? answer = null;
        try
        {
            lock (Gate)
            {
                int before = Volatile.Read(ref generation);
                if (snapshotGeneration != before)
                {
                    building = true;
                    try
                    {
                        snapshot = BuildSnapshot();
                    }
                    catch { snapshot = null; Interlocked.Increment(ref refusals); }
                    finally { building = false; snapshotGeneration = before; }
                }
                if (snapshot != null && before == Volatile.Read(ref generation))
                {
                    var dynamicTypes = new Dictionary<Assembly, Type[]>();
                    foreach (LookupSegment segment in snapshot)
                        if (segment.Index == null)
                        {
                            dynamicTypes.Add(segment.Assembly, AccessTools.GetTypesFromAssembly(segment.Assembly));
                            Interlocked.Increment(ref dynamicReads);
                        }
                    Type? match = Find(snapshot, dynamicTypes, name);
                    if (before == Volatile.Read(ref generation))
                    {
                        answer = match == null ? Array.Empty<Type>() : new[] { match };
                    }
                }
            }
        }
        catch { Interlocked.Increment(ref refusals); }
        bool stillCompatible = CompatiblePatches();
        if (!stillCompatible) { CompatibilityStatus.Guard("type-name", false); ForgetSnapshot(); }
        if (answer != null && stillCompatible)
        {
            Interlocked.Increment(ref lookups);
            return answer;
        }
        return OriginalTypes();
    }

    private static bool CompatiblePatches()
    {
        if (patchGuard?.AllowsOriginalContract() != true || enumeratorGuards == null)
            return false;
        foreach (PublishedPatchGuard guard in enumeratorGuards)
            if (!guard.AllowsOriginalContract())
                return false;
        return true;
    }

    private static void ForgetSnapshot()
    {
        // A build could have overlapped the foreign patch. Discard that result
        // so removing the patch cannot reactivate an index of altered outputs.
        lock (Gate)
        {
            snapshot = null;
            snapshotGeneration = -1;
        }
    }

    private static Type[] OriginalTypes()
    {
        Interlocked.Increment(ref originals);
        return AccessTools.AllTypes().ToArray();
    }

    private static LookupSegment[]? BuildSnapshot()
    {
        Interlocked.Increment(ref builds);
        Assembly[] assemblies = AccessTools.AllAssemblies().ToArray();
        var result = new List<LookupSegment>(assemblies.Length);
        int typeCount = 0, nameCharacters = 0;
        foreach (Assembly assembly in assemblies)
        {
            TypeLookupIndex? index = null;
            if (!assembly.IsDynamic)
            {
                // Do not cache a partial ReflectionTypeLoadException result.
                // OriginalTypes then retains Harmony's original error handling.
                Type[] types = assembly.GetTypes();
                index = TypeLookupIndex.Create(types, ref typeCount, ref nameCharacters);
                if (index == null)
                {
                    Interlocked.Increment(ref refusals);
                    return null;
                }
            }
            result.Add(new LookupSegment(assembly, index));
        }
        indexedTypes = typeCount;
        return result.ToArray();
    }

    private static Type? Find(LookupSegment[] segments, Dictionary<Assembly, Type[]> dynamicTypes, string name)
    {
        // Preserve global full-name precedence, then global simple-name order.
        for (int pass = 0; pass < 2; pass++)
            foreach (LookupSegment segment in segments)
            {
                Type? found = segment.Index != null ? segment.Index.Find(name, pass == 0)
                    : dynamicTypes[segment.Assembly].FirstOrDefault(type => (pass == 0 ? type.FullName : type.Name) == name);
                if (found != null)
                    return found;
            }
        return null;
    }

    private static void MenuUpdate()
    {
        if (!installed || Volatile.Read(ref finished) != 0 || !PlayDataLoader.Loaded || LongEventHandler.AnyEventNowOrWaiting)
            return;
        if (Interlocked.Exchange(ref finished, 1) != 0)
            return;
        AppDomain.CurrentDomain.AssemblyLoad -= AssemblyLoaded;
        lock (Gate)
            snapshot = null;
        Receipt("startup-complete", "constructor-to-menu-ready", "\"calls\":" + calls + ",\"misses\":" + misses + ",\"errors\":" + errors
            + ",\"milliseconds\":" + (ticks * 1000d / Stopwatch.Frequency).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
            + ",\"fallbackCalls\":" + fallbackCalls + ",\"indexLookups\":" + lookups + ",\"indexBuilds\":" + builds
            + ",\"indexRefusals\":" + refusals + ",\"originalFallbacks\":" + originals + ",\"dynamicAssemblyReads\":" + dynamicReads
            + ",\"indexedTypes\":" + indexedTypes + ",\"assemblyGenerations\":" + generation
            + ",\"maximumIndexedTypes\":" + TypeLookupIndex.MaximumTypes
            + ",\"maximumNameCharacters\":" + TypeLookupIndex.MaximumNameCharacters);
    }

    private static void Receipt(string kind, string reason, string? fields = null)
    {
        if (kind == "refused") CompatibilityStatus.Refuse("type-name", reason, "required-contract",
            LoaderSupplierPolicy.ProviderFor(Target, AccessTools.Method(typeof(AccessTools), nameof(AccessTools.AllTypes))));
        else CompatibilityStatus.Receipt("type-name", kind, reason);
        JsonLineLog.WriteReceipt(evidencePath, kind, reason, fields);
    }

    private sealed class LookupSegment
    {
        internal readonly Assembly Assembly;
        internal readonly TypeLookupIndex? Index;
        internal LookupSegment(Assembly assembly, TypeLookupIndex? index)
        {
            Assembly = assembly;
            Index = index;
        }
    }
}
