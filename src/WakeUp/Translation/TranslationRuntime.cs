// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;
using Verse;
using Injection = Verse.DefInjectionPackage.DefInjection;

namespace WakeUp;

// Only reduce the input of the native duplicate loop. Path resolution,
// assignment, diagnostics and value-type writeback remain native instructions.
internal static class TranslationRuntime
{
    internal const string Owner = "wakeup.translations";
    internal const int MaximumEntries = 65536;
    private static bool installed;
    private static int ownerThread;
    private static int contention;
    private static string? evidencePath;
    private static PublishedPatchGuard[] guards = Array.Empty<PublishedPatchGuard>();
    [ThreadStatic] private static Scope? current;
    internal static long Lookups, SeedVisits, CompletedScopes;
    internal static bool HasScope => current != null;

    internal static readonly string[] ExpectedBodies = {
        "A1D95A44772EB0DCEFDF3EDBDB933230848399471F6C4E5EF63B4FA9E61F61E1",
        // Frozen IL names [System.Xml]XmlNode; .NET 8 resolves that reference
        // through System.Private.Xml, which is not the game's Mono identity.
        "2CAA55EAE0643AD3B3DE70CA29A9AEDE0E22BCFDE9AF801BFE18744C8880D1A2",
        "1D6574BF2CA2ACAF6DF864B0A10C483B5508CC89AB6ED89BA5515423E3D55954"
    };
    internal static MethodBase[] ContractMethods() => new MethodBase[] {
        AccessTools.Method(typeof(DefInjectionPackage), nameof(DefInjectionPackage.InjectIntoDefs)),
        AccessTools.Method(typeof(DefInjectionPackage), "SetDefFieldAtPath"),
        AccessTools.Method(typeof(DefInjectionPackage), "GetFieldNamed")
    };

    internal static bool TryInitialize(IReadOnlyList<string> arguments)
    {
        string[] selectors = arguments.Where(a => a != null && a.StartsWith("--wake-up-translations=", StringComparison.Ordinal)).ToArray();
        if (selectors.Length != 1 || selectors[0] != "--wake-up-translations=on") return false;
        StartupLaunchDecision mode = StartupLaunchSelector.Parse(arguments);
        if (mode.Selection != StartupSelection.Candidate) return false;
        evidencePath = Path.Combine(mode.SaveDataRoot!, "WakeUp", "translations.jsonl");
        if (!RuntimeIdentity.ValidateBinaryIdentity(out string reason))
        {
            JsonLineLog.WriteReceipt(evidencePath, "refused", reason);
            return false;
        }
        return Install();
    }

    internal static bool Install()
    {
        if (installed) return true;
        var harmony = new Harmony(Owner);
        try
        {
            MethodBase[] methods = ContractMethods();
            var checks = new List<PublishedPatchGuard>();
            for (int i = 0; i < methods.Length; i++)
            {
                if (!SemanticMethodIdentity.TryHash(methods[i], out string body, out _) || body != ExpectedBodies[i])
                    throw new InvalidOperationException("changed-native-translation-contract");
                if (!PublishedPatchGuard.TryCreate(methods[i], Owner, out PublishedPatchGuard? guard, allPatchKinds: true,
                        allowedForeignPatch: LoadingReflectionRuntime.IsKnownPatch)
                    || !guard!.AllowsOriginalContract())
                    throw new InvalidOperationException("foreign-native-translation-patch");
                checks.Add(guard);
            }
            guards = checks.ToArray();
            harmony.Patch(methods[0], prefix: new HarmonyMethod(typeof(TranslationRuntime), nameof(Begin)),
                transpiler: new HarmonyMethod(typeof(TranslationRuntime), nameof(RecordTranspiler)) { priority = Priority.Last },
                finalizer: new HarmonyMethod(typeof(TranslationRuntime), nameof(End)));
            harmony.Patch(methods[1], prefix: new HarmonyMethod(typeof(TranslationRuntime), nameof(BeginSetter)),
                transpiler: new HarmonyMethod(typeof(TranslationRuntime), nameof(DuplicateTranspiler)) { priority = Priority.Last },
                finalizer: new HarmonyMethod(typeof(TranslationRuntime), nameof(EndSetter)));
            installed = true;
            JsonLineLog.WriteReceipt(evidencePath, "installed", "per-package-application-successful-targets");
            return true;
        }
        catch (Exception exception)
        {
            installed = false;
            try { harmony.UnpatchAll(Owner); } catch { }
            JsonLineLog.WriteReceipt(evidencePath, "refused", exception.Message);
            return false;
        }
    }

    private static bool Compatible() => installed && guards.All(g => g.AllowsOriginalContract());

    internal static void Begin(DefInjectionPackage __instance, bool errorOnDefNotFound, out bool __state)
    {
        __state = false;
        // Reentrant code may change the outer package/definitions. Suspend the
        // whole outer scope thereafter; never publish nested successes into it.
        if (current != null) { current.Invalid = true; return; }
        if (!Compatible()) return;
        if (Interlocked.CompareExchange(ref ownerThread, Thread.CurrentThread.ManagedThreadId, 0) != 0)
        {
            Interlocked.Increment(ref contention);
            return;
        }
        try
        {
            current = new Scope(__instance, errorOnDefNotFound);
            __state = true;
        }
        catch { Volatile.Write(ref ownerThread, 0); }
    }

    internal static void End(bool __state)
    {
        if (!__state) return;
        Scope? scope = current;
        current = null;
        Volatile.Write(ref ownerThread, 0);
        CompletedScopes++;
        JsonLineLog.WriteReceipt(evidencePath, "package-complete", scope?.ErrorsOnMissing == true ? "after-implied" : "before-implied",
            "\"lookups\":" + (scope?.Lookups ?? 0) + ",\"seedVisits\":" + (scope?.SeedVisits ?? 0)
            + ",\"nativeFallback\":" + (scope?.Invalid == true ? "true" : "false") + ",\"retainedEntries\":0");
    }

    private static void BeginSetter(out bool __state)
    {
        __state = current != null;
        if (current == null && Volatile.Read(ref ownerThread) != 0) Interlocked.Increment(ref contention);
        if (current != null && ++current.SetterDepth != 1) current.Invalid = true;
    }

    private static void EndSetter(bool __state)
    {
        if (__state && current != null) current.SetterDepth--;
    }

    internal static IEnumerable<CodeInstruction> DuplicateTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.Select(i => new CodeInstruction(i)).ToList();
        MethodBase target = ContractMethods()[1];
        if (!Unchanged(code, target)) return code;
        MethodInfo enumerator = AccessTools.Method(typeof(Dictionary<string, Injection>), "GetEnumerator");
        int at = code.FindIndex(i => i.Calls(enumerator));
        if (at < 1 || code.Count(i => i.Calls(enumerator)) != 1
            || !code[at - 1].LoadsField(AccessTools.Field(typeof(DefInjectionPackage), nameof(DefInjectionPackage.injections))))
            throw new InvalidOperationException("changed-duplicate-enumerator");
        // Derive the argument by its verified signature, not compiler locals.
        ParameterInfo normalized = target.GetParameters().Single(p => p.Name == "normalizedPath" && p.ParameterType == typeof(string).MakeByRefType());
        var load = new CodeInstruction(OpCodes.Ldarg, normalized.Position + 1);
        load.labels.AddRange(code[at].labels);
        load.blocks.AddRange(code[at].blocks);
        code[at] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TranslationRuntime), nameof(Candidates)));
        code.InsertRange(at, new[] { load, new CodeInstruction(OpCodes.Ldind_Ref) });
        return code;
    }

    internal static IEnumerable<CodeInstruction> RecordTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.Select(i => new CodeInstruction(i)).ToList();
        if (!Unchanged(code, ContractMethods()[0])) return code;
        FieldInfo suggested = AccessTools.Field(typeof(Injection), nameof(Injection.suggestedPath));
        CodeInstruction store = code.Single(i => i.StoresField(suggested));
        // The stack already contains the record and value for this field store.
        store.opcode = OpCodes.Call;
        store.operand = AccessTools.Method(typeof(TranslationRuntime), nameof(Record));
        return code;
    }

    private static bool Unchanged(List<CodeInstruction> code, MethodBase target)
        => Harmony.GetPatchInfo(target)?.Transpilers.Any(p => p.owner != Owner && !LoadingReflectionRuntime.IsKnownPatch(p)) != true
            && InstructionComparison.SameInstructions(LoadingReflectionRuntime.RestoreNativeCalls(code), PatchProcessor.GetOriginalInstructions(target));

    private static Dictionary<string, Injection>.Enumerator Candidates(Dictionary<string, Injection> source, string normalized)
    {
        Scope? scope = current;
        try
        {
            if (scope != null && !scope.Invalid && scope.SetterDepth == 1
                && ReferenceEquals(scope.Source, source) && ReferenceEquals(scope.Package.injections, source) && Compatible()
                && scope.Prepare(source))
            {
                scope.Matches.Clear();
                if (scope.Successes!.TryGetValue(normalized, out KeyValuePair<string, Injection> winner))
                    scope.Matches.Add(winner.Key, winner.Value);
                scope.Lookups++;
                Lookups++;
                return scope.Matches.GetEnumerator();
            }
        }
        catch { if (scope != null) scope.Invalid = true; }
        return source.GetEnumerator();
    }

    private static void Record(Injection record, string suggested)
    {
        record.suggestedPath = suggested;
        Scope? scope = current;
        if (scope == null || scope.Invalid || scope.Successes == null || !record.injected) return;
        try
        {
            // The dictionary key is authoritative for native self-exclusion;
            // it need not equal record.path for programmatically created data.
            if (!scope.Keys!.TryGetValue(record, out string key)) { scope.Invalid = true; return; }
            scope.Add(key, record);
        }
        catch { scope.Invalid = true; }
    }

    private sealed class Scope
    {
        internal readonly DefInjectionPackage Package;
        internal readonly Dictionary<string, Injection> Source;
        internal readonly bool ErrorsOnMissing;
        internal readonly Dictionary<string, Injection> Matches = new();
        internal Dictionary<string, KeyValuePair<string, Injection>>? Successes;
        internal Dictionary<Injection, string>? Keys;
        private IEnumerator<KeyValuePair<string, Injection>>? version;
        private readonly int initialContention = Volatile.Read(ref contention);
        internal bool Invalid;
        internal int SetterDepth;
        internal long Lookups, SeedVisits;

        internal Scope(DefInjectionPackage package, bool errorsOnMissing)
        {
            Package = package;
            Source = package.injections;
            ErrorsOnMissing = errorsOnMissing;
        }

        internal bool Prepare(Dictionary<string, Injection> source)
        {
            if (initialContention != Volatile.Read(ref contention)) { Invalid = true; return false; }
            if (Successes != null) { version!.Reset(); return !Invalid; }
            if (source.Count > MaximumEntries) { Invalid = true; return false; }
            Successes = new Dictionary<string, KeyValuePair<string, Injection>>(StringComparer.Ordinal);
            Keys = new Dictionary<Injection, string>();
            version = source.GetEnumerator();
            foreach (var entry in source)
            {
                SeedVisits++;
                TranslationRuntime.SeedVisits++;
                if (entry.Value == null || Keys.ContainsKey(entry.Value)) { Invalid = true; return false; }
                Keys.Add(entry.Value, entry.Key);
                if (entry.Value.injected) Add(entry.Key, entry.Value);
            }
            return !Invalid;
        }

        internal void Add(string key, Injection record)
        {
            if (record.normalizedPath == null) return;
            if (Successes!.ContainsKey(record.normalizedPath))
            {
                // Unusual pre-populated duplicate successes keep the exact
                // original dictionary order and self-exclusion through fallback.
                Invalid = true;
                return;
            }
            Successes.Add(record.normalizedPath, new KeyValuePair<string, Injection>(key, record));
        }
    }
}
