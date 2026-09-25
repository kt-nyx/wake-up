// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace WakeUp;

// Keep the original iterator and its elapsed-time budget. Coalesce only the
// pinned framework's additional repaint pauses during startup.
internal static class RepaintCoalescer
{
    internal const string Owner = "wakeup.repaint-coalescing";
    private static string path = "";
    private static bool active;
    internal static bool AllowsBackgroundCooperation(MethodBase target, Patch patch)
        => target.DeclaringType?.Assembly == LoadingProgressCompatibility.FrameworkAssembly
            && target.DeclaringType?.FullName == "ilyvion.LoadingProgress.LongEventHandler_UpdateCurrentEnumeratorEvent_Patches"
            && target.Name == "ShouldStopEarly" && patch.owner == Owner
            && patch.PatchMethod == AccessTools.Method(typeof(RepaintCoalescer), nameof(Coalesce))
            && ImageSupplierPolicy.ExactPublication(target, patch.PatchMethod, Owner, false)
            && LoaderSupplierPolicy.Patches(target).Count(p => p.owner == Owner) == 1;
    private static long suppressed, refused;
    private static PublishedPatchGuard? stopGuard, loopGuard, transpilerGuard;
    internal static void TryInitialize(string[] args)
    {
        string[] selectors = args.Where(a => a.StartsWith("--wake-up-loading-progress=", StringComparison.Ordinal)).ToArray();
        if (selectors.Length != 1)
            return;
        if (selectors[0] != "--wake-up-loading-progress=coalesce")
            return;
        var choice = StartupLaunchSelector.Parse(args);
        if (!LoadedModManager.RunningModsListForReading.Any(m => m.PackageId == LoadingProgressCompatibility.PackageId))
        { CompatibilityStatus.Absent("repaint"); return; }
        if (choice.Selection != StartupSelection.Candidate)
            return;
        path = Path.Combine(choice.SaveDataRoot!, "WakeUp", "repaint-coalescing.jsonl");
        if (!RuntimeIdentity.ValidateBinaryIdentity(out string reason))
        {
            CompatibilityStatus.Refuse("repaint", reason);
            JsonLineLog.WriteReceipt(path, "refused", reason);
            return;
        }
        var harmony = new Harmony(Owner);
        try
        {
            if (!LoadingProgressCompatibility.TryInitialize())
                throw new InvalidOperationException("unsupported-loading-framework");
            Assembly assembly = LoadingProgressCompatibility.FrameworkAssembly!;
            var methods = SupplierMethodContract.ResolveAll(assembly, SupplierContracts.Repaint);
            MethodInfo stop = (MethodInfo)methods["ShouldStopEarly"], transpiler = (MethodInfo)methods["Transpiler"];
            if (!PublishedPatchGuard.TryCreate(stop, Owner, out stopGuard, true)
                || !PublishedPatchGuard.TryCreate(transpiler, Owner, out transpilerGuard, true)
                || !PublishedPatchGuard.TryCreate(AccessTools.Method(typeof(LongEventHandler), "UpdateCurrentEnumeratorEvent"), Owner, out loopGuard, true,
                    p => p.owner == LoadingProgressCompatibility.PackageId && Equals(p.PatchMethod, transpiler)))
                throw new InvalidOperationException("patch-state-unavailable");
            harmony.Patch(stop, postfix: new HarmonyMethod(typeof(RepaintCoalescer), nameof(Coalesce)));
            harmony.Patch(AccessTools.Method(typeof(global::RimWorld.MainMenuDrawer), "MainMenuOnGUI"), postfix: new HarmonyMethod(typeof(RepaintCoalescer), nameof(Menu)));
            active = true;
            CompatibilityStatus.Available("repaint");
            Write("{\"event\":\"installed\",\"nativeTimeBudgetUnchanged\":true}");
        }
        catch (Exception e) { CompatibilityStatus.Refuse("repaint", e.Message); harmony.UnpatchAll(Owner); JsonLineLog.WriteReceipt(path, "refused", e.Message); }
    }
    private static void Coalesce(ref bool __result)
    {
        if (!active || !__result || !UnityData.IsInMainThread)
            return;
        if (!stopGuard!.AllowsOriginalContract() || !loopGuard!.AllowsOriginalContract() || !transpilerGuard!.AllowsOriginalContract())
        {
            CompatibilityStatus.Guard("repaint", false);
            refused++;
            return;
        }
        // The original has already consumed its repaint request. Its caller's
        // unmodified elapsed-time condition still decides when to end this frame.
        __result = false;
        suppressed++;
    }
    private static void Menu()
    {
        if (!active || Current.ProgramState != ProgramState.Entry || !PlayDataLoader.Loaded)
            return;
        active = false;
        Write("{\"event\":\"complete\",\"suppressed\":" + suppressed + ",\"refused\":" + refused + "}");
    }
    private static void Write(string json) => JsonLineLog.Append(path, json);
}
