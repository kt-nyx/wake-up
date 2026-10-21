// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using System.Threading;
using HarmonyLib;
using Verse;

namespace WakeUp.V17;

internal sealed class TranslationPredicates
{
    internal const string Feature = "translation-predicate";
    private static TranslationPredicates? instance;
    private readonly RuntimeContext context;
    private PublishedPatchGuard? guard;
    private long calls, rewrites;
    private long observedRows, skippedKeys;
    // Private test bridges may request a diagnostic walk. Ordinary launches
    // never perform it; these counters do not affect the native predicate.
    internal static bool ObserveComparisons = false;
    private bool activeRewrite;
    internal bool Installed { get; private set; }
    private static long completedCalls, completedRewrites;
    private static long completedRows, completedSkipped;
    private static bool completed;
    internal TranslationPredicates(RuntimeContext context) { this.context = context; instance = this; }
    internal void Install()
    {
        var target = context.NamedMethod("Verse.DefInjectionPackage", "SetDefFieldAtPath");
        context.Require(target); guard = context.Guard(target);
        context.Patch(Feature, target, transpiler: RuntimeContext.Hook(typeof(TranslationPredicates), nameof(Rewrite)));
        if (!activeRewrite) throw new InvalidOperationException("Translation worker kept its native predicate.");
        Installed = true;
        context.Status(Feature, "Experimental live duplicate-target predicate rewrite installed; no cache or timing benefit assumed.");
    }
    private static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> code, ILGenerator generator)
    {
        var self = instance;
        if (self?.guard?.AllowsOriginalContract() != true) { if (self != null) self.activeRewrite = false; return code; }
        var result = TranslationPredicateRewrite.Apply(code, generator,
            typeof(DefInjectionPackage.DefInjection).GetField("injected")!,
            typeof(DefInjectionPackage.DefInjection).GetField("normalizedPath")!).ToList();
        int hits = 0;
        for (int i = 0; i < result.Count; i++)
        {
            var c = result[i];
            if (c.operand is not MethodInfo m || m.DeclaringType != typeof(Dictionary<string, DefInjectionPackage.DefInjection>) || m.Name != "GetEnumerator") continue;
            if (c.labels.Count != 0 || c.blocks.Count != 0) throw new InvalidOperationException("Unreviewed translation loop entry.");
            c.opcode = OpCodes.Call; c.operand = RuntimeContext.Hook(typeof(TranslationPredicates), nameof(Enumerate));
            result.InsertRange(i, new[] { new CodeInstruction(OpCodes.Ldloc_0), new CodeInstruction(OpCodes.Ldarg_S, (byte)8), new CodeInstruction(OpCodes.Ldind_Ref) });
            i += 3; hits++;
        }
        if (hits != 1) throw new InvalidOperationException("Expected one live translation enumerator.");
        Interlocked.Increment(ref self.rewrites); self.activeRewrite = true; return result;
    }
    private static Dictionary<string, DefInjectionPackage.DefInjection>.Enumerator Enumerate(
        Dictionary<string, DefInjectionPackage.DefInjection> source, string ownKey, string? path)
    {
        var self = instance;
        if (self != null)
        {
            Interlocked.Increment(ref self.calls);
            if (ObserveComparisons)
            {
                long rows = 0, skipped = 0;
                // No mutation or callbacks occur in these plain field/string
                // tests. Stop at the same native match or null-other failure.
                foreach (var row in source)
                {
                    rows++;
                    var value = row.Value;
                    if (value == null) { if (row.Key != ownKey) break; continue; }
                    if (!value.injected || value.normalizedPath != path) skipped++;
                    if (row.Key != ownKey && value.injected && value.normalizedPath == path) break;
                }
                Interlocked.Add(ref self.observedRows, rows); Interlocked.Add(ref self.skippedKeys, skipped);
            }
        }
        return source.GetEnumerator();
    }
    internal void Clear()
    {
        context.Unpatch(Feature);
        activeRewrite = false; Installed = false;
        completedCalls = calls; completedRewrites = rewrites; completed = true;
        completedRows = observedRows; completedSkipped = skippedKeys;
        context.Status(Feature, $"Startup complete: {calls:N0} live duplicate-target scans used; {rewrites} guarded rewrite applications. Native traversal, errors and writes retained.");
        instance = null;
    }
    internal static object Snapshot() => new { active = instance?.activeRewrite == true, completed,
        scanCalls = instance?.calls ?? completedCalls, rewriteApplications = instance?.rewrites ?? completedRewrites,
        diagnosticRows = instance?.observedRows ?? completedRows, diagnosticSkippedKeyComparisons = instance?.skippedKeys ?? completedSkipped };
}
