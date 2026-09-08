// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimWorldLoadingOptimizer.RimWorld;

// Keep the original iterator and its elapsed-time budget. Coalesce only the
// pinned framework's additional repaint pauses during startup.
internal static class RepaintCoalescer
{
    internal const string Owner = "rimworldloadingoptimizer.repaint-coalescing";
    private static string path = "";
    private static bool active;
    private static long suppressed, refused;
    private static PublishedPatchGuard? stopGuard, loopGuard, transpilerGuard;
    internal static void TryInitialize(string[] args)
    {
        string[] selectors = args.Where(a => a.StartsWith("--rlo-loading-progress=", StringComparison.Ordinal)).ToArray();
        if (selectors.Length != 1)
            return;
        if (selectors[0] != "--rlo-loading-progress=coalesce")
            return;
        var choice = StartupLaunchSelector.Parse(args);
        if (!LoadedModManager.RunningModsListForReading.Any(m => m.PackageId == LoadingProgressCompatibility.PackageId))
            return;
        if (choice.Selection != StartupSelection.Candidate || !RuntimeIdentity.ValidateBinaryIdentity(out _))
            return;
        path = Path.Combine(choice.SaveDataRoot!, "RimWorldLoadingOptimizer", "repaint-coalescing.jsonl");
        var harmony = new Harmony(Owner);
        try
        {
            if (!LoadingProgressCompatibility.TryInitialize())
                throw new InvalidOperationException("unsupported-loading-framework");
            Assembly assembly = LoadingProgressCompatibility.FrameworkAssembly!;
            Type type = assembly.GetType("ilyvion.LoadingProgress.LongEventHandler_UpdateCurrentEnumeratorEvent_Patches", true)!;
            MethodInfo stop = AccessTools.Method(type, "ShouldStopEarly"), transpiler = AccessTools.Method(type, "Transpiler");
            Check(stop, "F730D86E32A29B82094522F639001E913AFC08F598CE52F896D4B4E99E7E0086");
            Check(transpiler, "D290D5CA89BF383968ACA32D5CA595C51B89783144482484660B3444791A295B");
            if (!PublishedPatchGuard.TryCreate(stop, Owner, out stopGuard, true)
                || !PublishedPatchGuard.TryCreate(transpiler, Owner, out transpilerGuard, true)
                || !PublishedPatchGuard.TryCreate(AccessTools.Method(typeof(LongEventHandler), "UpdateCurrentEnumeratorEvent"), Owner, out loopGuard, true,
                    p => p.owner == LoadingProgressCompatibility.PackageId && Equals(p.PatchMethod, transpiler)))
                throw new InvalidOperationException("patch-state-unavailable");
            harmony.Patch(stop, postfix: new HarmonyMethod(typeof(RepaintCoalescer), nameof(Coalesce)));
            harmony.Patch(AccessTools.Method(typeof(global::RimWorld.MainMenuDrawer), "MainMenuOnGUI"), postfix: new HarmonyMethod(typeof(RepaintCoalescer), nameof(Menu)));
            active = true;
            Write("{\"event\":\"installed\",\"nativeTimeBudgetUnchanged\":true}");
        }
        catch (Exception e) { harmony.UnpatchAll(Owner); JsonLineLog.WriteReceipt(path, "refused", e.Message); }
    }
    private static void Check(MethodInfo method, string expected)
    {
        using var hash = SHA256.Create();
        if (BitConverter.ToString(hash.ComputeHash(method.GetMethodBody()!.GetILAsByteArray())).Replace("-", "") != expected)
            throw new InvalidOperationException("body-" + method.Name);
    }
    private static void Coalesce(ref bool __result)
    {
        if (!active || !__result || !UnityData.IsInMainThread)
            return;
        if (!stopGuard!.AllowsOriginalContract() || !loopGuard!.AllowsOriginalContract() || !transpilerGuard!.AllowsOriginalContract())
        {
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
