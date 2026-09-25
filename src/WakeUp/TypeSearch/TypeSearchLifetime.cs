// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HarmonyLib;
using Verse;
namespace WakeUp;

// Selection and lifetime belong to the search group, not to TypeByName's hook.
internal static class TypeSearchLifetime
{
    internal const string Owner = "wakeup.type-search-lifetime";
    private static bool selected;
    private static int finished;
    internal static bool Selected => selected;
    internal static bool Active => selected && Volatile.Read(ref finished) == 0;
    internal static void Initialize(IReadOnlyList<string> arguments)
    {
        StartupFeatureRunner.Run("Type searches", () => TypeLookupRuntime.TryInitialize(arguments));
        if (selected || PlayDataLoader.Loaded || StartupLaunchSelector.Parse(arguments).Selection != StartupSelection.Candidate
            || arguments.Count(a => a.StartsWith("--wake-up-strategy=", StringComparison.Ordinal)) != 1
            || !arguments.Contains("--wake-up-strategy=type-lookup")) return;
        try
        {
            if (!RuntimeIdentity.ValidateBinaryIdentity(out string reason)) throw new InvalidOperationException(reason);
            new Harmony(Owner).Patch(AccessTools.Method(typeof(Root_Entry), "Update"),
                postfix: new HarmonyMethod(typeof(TypeSearchLifetime), nameof(MenuUpdate)));
            selected = true;
        }
        catch (Exception error)
        {
            try { new Harmony(Owner).UnpatchAll(Owner); } catch { }
            foreach (string id in new[] { "leaf", "reflection", "attributes" }) CompatibilityStatus.Refuse(id, "Search lifetime unavailable: " + error.Message);
            return;
        }
        StartupFeatureRunner.Run("Leaf searches", LeafSubclassRuntime.Initialize);
        StartupFeatureRunner.Run("Reflection searches", LoadingReflectionRuntime.Initialize);
    }
    private static void MenuUpdate()
    {
        if (!Active || !PlayDataLoader.Loaded || LongEventHandler.AnyEventNowOrWaiting || Interlocked.Exchange(ref finished, 1) != 0) return;
        LeafSubclassRuntime.CompleteStartup();
        LoadingReflectionRuntime.Complete();
    }
}
