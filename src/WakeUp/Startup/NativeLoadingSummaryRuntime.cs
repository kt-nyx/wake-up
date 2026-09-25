// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace WakeUp;

// Gate only the native summary condition at its loading-screen callsite. The
// native acknowledgement, tips, tooltips, event work and other summary callers stay native.
internal static class NativeLoadingSummaryRuntime
{
    internal const string Owner = "wakeup.native-loading-summary";
    internal static MethodInfo Target => LoadingDisplayRuntime.Targets[0];
    private static PublishedPatchGuard? guard;
    private static bool enabled;
    private static long hidden;
    internal static string Status { get; private set; } = "Native loading summary hiding is off.";
    internal static bool Selected(IReadOnlyList<string> args) => StartupLaunchSelector.Parse(args).Selection == StartupSelection.Candidate
        && args.Count(a => a.StartsWith("--wake-up-hide-loading-summary=", StringComparison.Ordinal)) == 1
        && args.Contains("--wake-up-hide-loading-summary=on");

    internal static void TryInitialize(string[] args)
    {
        if (!Selected(args)) return;
        var harmony = new Harmony(Owner);
        try
        {
            if (LifecycleSupplierPolicy.RimThemesOwnsDisplay())
                throw LifecycleSupplierPolicy.ThemeDisplayConflict();
            if (LifecycleSupplierPolicy.PreserveDlcPanel(args.Contains("--wake-up-summary-provider=wakeup")))
            {
                Status = "No Modlist on Loading keeps the DLC panel. Wake-Up preserves it; select 'Also hide the DLC panel' to hide the entire summary and restart.";
                CompatibilityStatus.Refuse("summary", Status, "supplier-dlc-panel", "No Modlist on Loading");
                return;
            }
            if (LoadedModManager.RunningModsListForReading.Any(m => m.PackageId == LoadingProgressCompatibility.PackageId))
                throw new SupplierConflictException("Loading Progress owns the loading screen; its display stays in use", "Loading Progress", "supplier-display");
            if (!RuntimeIdentity.ValidateBinaryIdentity(out string reason)) throw new InvalidOperationException(reason);
            if (!SemanticMethodIdentity.TryHash(Target, out string body, out _) || body != LoadingDisplayRuntime.ExpectedBodies[0])
                throw new InvalidOperationException("native loading drawing changed");
            Install(harmony);
            CompatibilityStatus.Available("summary");
            Status = "Native loading summary hiding selected; waiting for a screen that would show it.";
        }
        catch (Exception e)
        {
            enabled = false;
            harmony.UnpatchAll(Owner);
            Status = "Native loading summary hiding refused: " + e.Message + ". Ordinary drawing remains."; LifecycleSupplierPolicy.Refuse("summary", Status, e);
        }
        Log.Message("[Wake-Up] " + Status);
    }

    internal static void Install(Harmony harmony)
    {
        if (!PublishedPatchGuard.TryCreate(Target, Owner, out guard, allPatchKinds: true,
            allowedForeignPatch: p => ExactHook(Target, p, LoadingDisplayRuntime.Owner,
                AccessTools.Method(typeof(LoadingDisplayRuntime), "Draw"), false)) || !guard!.AllowsOriginalContract())
            throw new InvalidOperationException("another mod changes the native loading screen");
        harmony.Patch(Target, transpiler: new HarmonyMethod(typeof(NativeLoadingSummaryRuntime), nameof(Rewrite)));
        enabled = true;
    }

    internal static bool AllowsDisplayCooperation(MethodBase target, Patch patch) => target == Target
        && ExactHook(target, patch, Owner, AccessTools.Method(typeof(NativeLoadingSummaryRuntime), nameof(Rewrite)), true);

    internal static bool ExactHook(MethodBase target, Patch patch, string owner, MethodInfo method, bool transpiler)
    {
        if (patch.owner != owner || patch.PatchMethod != method || method.Module != typeof(NativeLoadingSummaryRuntime).Module) return false;
        var info = Harmony.GetPatchInfo(target);
        if (info == null) return false;
        var expected = transpiler ? info.Transpilers : info.Postfixes;
        return expected.Count(p => p.owner == owner && p.PatchMethod == method) == 1
            && info.Prefixes.Concat(info.Finalizers).Concat(info.InnerPrefixes).Concat(info.InnerPostfixes)
                .Concat(transpiler ? info.Postfixes : info.Transpilers).All(p => p.PatchMethod != method);
    }

    internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        var size = AccessTools.Method(typeof(ModSummaryWindow), nameof(ModSummaryWindow.GetEffectiveSize));
        int at = code.FindIndex(i => i.Calls(size));
        // Original: stloc.2; ldloc.2; brtrue size; call zero; br end; call size.
        // Transform the stored flag itself, so BOTH layout and drawing use the choice.
        if (at < 5 || code.Count(i => i.Calls(size)) != 1 || code[at - 5].opcode != OpCodes.Stloc_2
            || code[at - 4].opcode != OpCodes.Ldloc_2 || code[at - 3].opcode != OpCodes.Brtrue_S
            || !code[at - 2].Calls(AccessTools.PropertyGetter(typeof(UnityEngine.Vector2), "zero"))
            || code[at - 1].opcode != OpCodes.Br_S)
            throw new InvalidOperationException("native summary condition changed");
        var gate = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NativeLoadingSummaryRuntime), nameof(ShowSummary)));
        gate.labels.AddRange(code[at - 5].labels); code[at - 5].labels.Clear();
        gate.blocks.AddRange(code[at - 5].blocks); code[at - 5].blocks.Clear();
        code.Insert(at - 5, gate);
        return code;
    }

    internal static bool ShowSummary(bool nativeChoice)
    {
        if (!nativeChoice || !enabled) return nativeChoice;
        if (LifecycleSupplierPolicy.RimThemesOwnsDisplay())
        {
            enabled = false; Status = LifecycleSupplierPolicy.DisplayReason();
            LifecycleSupplierPolicy.Refuse("summary", Status, LifecycleSupplierPolicy.ThemeDisplayConflict());
            return nativeChoice;
        }
        if (guard?.AllowsOriginalContract() != true)
        {
            enabled = false;
            Status = "Native loading summary hiding refused a changed drawing hook; ordinary drawing remains."; CompatibilityStatus.Refuse("summary", Status);
            return nativeChoice;
        }
        hidden++;
        Status = "Native loading summary hidden on " + hidden + " drawing calls. Mod information remains available in Mods.";
        return false;
    }

    internal static void Reset() { enabled = false; guard = null; hidden = 0; }
}
