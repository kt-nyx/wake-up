// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;
using RimWorld;
using Verse;

namespace WakeUp;

internal static class LeafSubclassRuntime
{
    private const string Owner = "wakeup.leaf-subclasses";
    private static readonly LeafSubclassIndex Index = new();
    private static readonly object IndexGate = new();
    private static PublishedPatchGuard[]? guards;
    private static FieldInfo? typesField, subclassesField;
    private static int ownerThread, constructionDepth;
    [ThreadStatic] private static int constructionEntries;
    private static bool installed;
    private static long predicates, avoidedScans, nativePredicates;
    private static long startupPredicates, startupAvoidedScans, startupNativePredicates;

    // Pinned method behavior, rather than a whole-game version restriction.
    // Values obtained from the approved unmodified GOG reference in offline tests.
    internal static readonly string[] ExpectedBodies = {
        "1E272A66AD300B21DA053CAE83A309D9FA5676A0CA63855585F846F6683BC3B6",
        "E8742731E67F6F853ADFFD522A06CB55B586FF9FADAEB624CC0D80E1C9365E5E",
        "0314085453379264DDEDC36916731F51F86DA14EF470A4E9BEB9A8DB16F2E4CA",
        "38CDC87D7F47FF4F1053226A1C8266DE484BF6399731402C9F8FFE6BA456D5F3",
        "04F2BB147E0B8CC5DE6B0E5153B6BDBC14FF9AFCACFD75C3B26CCE1C4CB27107",
        "A1082717DC34151FCA8339C736DF87E4B175911098CE4F8A05EDE89AB54EA5C3"
    };

    internal static MethodBase[] ContractMethods()
    {
        MethodInfo target = AccessTools.Method(typeof(GenTypes), nameof(GenTypes.AllLeafSubclasses), new[] { typeof(Type) });
        MethodInfo predicate = PatchProcessor.GetOriginalInstructions(target)
            .Where(i => i.opcode == OpCodes.Ldftn).Select(i => (MethodInfo)i.operand).Single();
        MethodInfo subclasses = AccessTools.Method(typeof(GenTypes), nameof(GenTypes.AllSubclasses), new[] { typeof(Type) });
        MethodInfo subclassPredicate = PatchProcessor.GetOriginalInstructions(subclasses)
            .Where(i => i.opcode == OpCodes.Ldftn).Select(i => (MethodInfo)i.operand).Single();
        return new MethodBase[] { target, predicate, subclasses,
            AccessTools.PropertyGetter(typeof(GenTypes), nameof(GenTypes.AllTypes)), subclassPredicate,
            AccessTools.Constructor(typeof(AlertsReadout)) };
    }

    internal static void Initialize()
    {
        var harmony = new Harmony(Owner);
        try
        {
            MethodBase[] methods = ContractMethods();
            for (int i = 0; i < methods.Length; i++)
                if (!SemanticMethodIdentity.TryHash(methods[i], out string body, out _) || body != ExpectedBodies[i])
                    throw new InvalidOperationException("changed-native-leaf-contract");
            typesField = AccessTools.Field(typeof(GenTypes), "allTypesCached");
            subclassesField = AccessTools.Field(typeof(GenTypes), "cachedSubclasses");
            if (typesField?.IsStatic != true || typesField.FieldType != typeof(List<Type>)
                || subclassesField?.IsStatic != true || subclassesField.FieldType != typeof(Dictionary<Type, List<Type>>))
                throw new InvalidOperationException("changed-native-type-cache-fields");
            var checks = new List<PublishedPatchGuard>();
            foreach (MethodBase method in methods)
            {
                if (!PublishedPatchGuard.TryCreate(method, Owner, out PublishedPatchGuard? guard, allPatchKinds: true)
                    || (method != methods[5] && !guard!.AllowsOriginalContract()))
                    throw new InvalidOperationException("foreign-native-leaf-patch");
                checks.Add(guard!);
            }
            guards = checks.ToArray();
            harmony.Patch(methods[0], transpiler: new HarmonyMethod(typeof(LeafSubclassRuntime), nameof(Transpiler)) { priority = Priority.Last });
            harmony.Patch(methods[5], prefix: new HarmonyMethod(typeof(LeafSubclassRuntime), nameof(BeginConstruction)),
                finalizer: new HarmonyMethod(typeof(LeafSubclassRuntime), nameof(EndConstruction)));
            installed = true;
            CompatibilityStatus.Available("leaf");
            TypeLookupRuntime.LeafReceipt("leaf-installed", "startup-and-alerts-readout-construction");
        }
        catch (Exception exception)
        {
            installed = false;
            CompatibilityStatus.Refuse("leaf", exception.Message, "required-contract",
                LoaderSupplierPolicy.ProviderFor(AccessTools.Method(typeof(GenTypes), nameof(GenTypes.AllLeafSubclasses), new[] { typeof(Type) })));
            try { harmony.UnpatchAll(Owner); } catch { }
            TypeLookupRuntime.LeafReceipt("leaf-refused", exception.Message);
        }
    }

    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = instructions.Select(i => new CodeInstruction(i)).ToList();
        MethodInfo target = AccessTools.Method(typeof(GenTypes), nameof(GenTypes.AllLeafSubclasses), new[] { typeof(Type) });
        if (Harmony.GetPatchInfo(target)?.Transpilers.Any(p => p.owner != Owner) == true
            || !InstructionComparison.SameInstructions(code, PatchProcessor.GetOriginalInstructions(target)))
        { CompatibilityStatus.Guard("leaf", false); return code; }
        CodeInstruction where = code.Single(i => i.opcode == OpCodes.Call && i.operand is MethodInfo method
            && method.DeclaringType == typeof(Enumerable) && method.Name == nameof(Enumerable.Where)
            && method.IsGenericMethod && method.GetGenericArguments().SequenceEqual(new[] { typeof(Type) }));
        where.operand = AccessTools.Method(typeof(LeafSubclassRuntime), nameof(Filter));
        return code;
    }

    private static IEnumerable<Type> Filter(IEnumerable<Type> source, Func<Type, bool> predicate)
        // Keep native eager AllSubclasses capture and LINQ's List iterator. Do
        // not return an array/set or take a snapshot of the caller's result.
        => (TypeSearchLifetime.Active || Compatible())
            ? source.Where(type => Evaluate(type, predicate)) : source.Where(predicate);

    private static bool Admitted(bool construction)
    {
        if (!installed || !TypeSearchLifetime.Selected || guards?.Length != 6) return false;
        // Startup discovery depends only on the five native leaf/type methods.
        // An unrelated AlertsReadout hook cannot change their contract. The
        // constructor scope additionally requires its own sixth guard.
        for (int i = 0; i < (construction ? 6 : 5); i++)
            if (!CompatibilityStatus.Guard("leaf", guards[i].AllowsOriginalContract())) return false;
        return true;
    }

    private static bool Compatible()
    {
        if (!Admitted(constructionEntries != 0)) return false;
        int thread = Thread.CurrentThread.ManagedThreadId;
        // A startup iterator can be created by any loader thread. The first
        // predicate evaluation owns the shared proof until menu completion; other
        // threads keep the native predicate and native caches.
        lock (IndexGate)
            if (TypeSearchLifetime.Active && constructionDepth == 0 && constructionEntries == 0)
                Interlocked.CompareExchange(ref ownerThread, thread, 0);
        return Volatile.Read(ref ownerThread) == thread && ScopeActive();
    }

    private static bool ScopeActive() => (constructionDepth == 1 && constructionEntries == 1)
        || (constructionDepth == 0 && constructionEntries == 0 && TypeSearchLifetime.Active);

    // The ordinary menu-first path constructs AlertsReadout while creating the
    // play UI. After startup only this constructor owns a proof. Nested
    // constructors temporarily suspend reuse.
    internal static void BeginConstruction(out int __state)
    {
        // Even a refused constructor must block the independent startup scope
        // while its body executes. State 1 records entry; 2 also owns the proof.
        constructionEntries++;
        __state = 1;
        if (!Admitted(construction: true) || constructionEntries != 1) return;
        // Play-data loading can install us on a different thread from play-UI
        // construction. Ownership belongs to this constructor, not installation.
        int thread = Thread.CurrentThread.ManagedThreadId;
        lock (IndexGate)
        {
            if (Interlocked.CompareExchange(ref ownerThread, thread, 0) != 0 && Volatile.Read(ref ownerThread) != thread)
                return;
            constructionDepth++;
            __state = 2;
        }
    }

    internal static void EndConstruction(int __state)
    {
        if (__state == 0) return;
        constructionEntries--;
        if (__state == 2 && --constructionDepth == 0)
        {
            try { Complete(); }
            finally { Volatile.Write(ref ownerThread, 0); }
        }
    }

    private static bool Evaluate(Type type, Func<Type, bool> predicate)
    {
        if (!Compatible())
            return predicate(type);
        bool startup = constructionDepth == 0;
        if (startup) startupPredicates++; else predicates++;
        bool avoided = false;
        try
        {
            var cache = subclassesField!.GetValue(null) as Dictionary<Type, List<Type>>;
            // Reading fields does not trigger assembly enumeration. A missing
            // AllTypes cache must be initialized by native code, including its
            // original partial-load/error handling and exception timing.
            var types = typesField!.GetValue(null) as List<Type>;
            bool proven = false;
            if (cache != null && !cache.ContainsKey(type))
                lock (IndexGate) proven = ScopeActive() && Index.TryProveLeaf(type, types);
            // Harmony's patch-state lock must never be acquired under IndexGate.
            if (proven && Compatible())
            {
                lock (IndexGate)
                    if (ScopeActive() && ReferenceEquals(typesField.GetValue(null), types)
                        && ReferenceEquals(subclassesField.GetValue(null), cache) && Index.IsCurrent(types)
                        && !cache!.ContainsKey(type))
                    {
                        cache.Add(type, new List<Type>());
                        if (startup) startupAvoidedScans++; else avoidedScans++;
                        avoided = true;
                    }
            }
        }
        catch { lock (IndexGate) Index.Clear(); }
        if (!avoided)
        {
            if (startup) startupNativePredicates++; else nativePredicates++;
        }
        // Existing mutable child lists always win. Nonleaf misses keep native
        // PLINQ population/order; even successful proofs run the native predicate.
        return predicate(type);
    }

    internal static void Complete()
    {
        lock (IndexGate) Index.Clear();
        if (installed)
            TypeLookupRuntime.LeafReceipt("leaf-loading-complete", "alerts-readout-constructor",
                "\"predicates\":" + predicates + ",\"avoidedFullScans\":" + avoidedScans
                + ",\"nativePredicates\":" + nativePredicates + ",\"ancestryBuilds\":" + Index.Builds
                + ",\"ancestryRefusals\":" + Index.Refusals + ",\"retainedTypes\":0");
    }

    // Called after TypeLookupRuntime closes its startup admission. Captured
    // deferred iterators then use the original predicate on every enumeration.
    internal static void CompleteStartup()
    {
        lock (IndexGate)
            if (constructionDepth == 0)
            {
                Index.Clear();
                Volatile.Write(ref ownerThread, 0);
            }
        if (installed)
            TypeLookupRuntime.LeafReceipt("leaf-startup-complete", "constructor-to-menu-ready",
                "\"predicates\":" + startupPredicates + ",\"avoidedFullScans\":" + startupAvoidedScans
                + ",\"nativePredicates\":" + startupNativePredicates);
    }
}
