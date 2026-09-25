// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using HarmonyLib;
using Verse;

namespace WakeUp;

// Redirecting a query changes the expression AND the context seen by its
// observers. Multi-result workers do not use this single-result exception.
internal static class SingleWorkerQueryPolicy
{
    private const string PreviewHash = "4cb5a8434d1e44f3e69d483860f377b4204ed00483036f98fc2b86e34b7283ec";
    private static readonly MethodInfo Query = typeof(XmlNode).GetMethod("SelectSingleNode", new[] { typeof(string) })!;
    private static readonly MethodInfo Stage = AccessTools.Method(typeof(LoadedModManager), "ApplyPatches");
    private static PublishedPatchGuard? native;
    private static Assembly? preview;
    private static FieldInfo? active;
    private static MethodInfo[] hooks = Array.Empty<MethodInfo>();
    private static readonly List<PublishedPatchGuard> guards = new();
    private static PublishedPatchGuard.PublicationSet? publications;
    private static bool initialized;

    private static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        PublishedPatchGuard.TryCreate(Query, "wakeup.def-lookup", out native, allPatchKinds: true);
        var supplier = LoaderSupplierPolicy.Find("taranchuk.fastergameloading", "FasterGameLoading.FasterGameLoadingMod");
        if (supplier == null || !LoaderSupplierPolicy.MatchesImage(supplier, PreviewHash)) return;
        Type? query = supplier.GetType("FasterGameLoading.XmlNode_SelectSingleNode_Patch");
        Type? stage = supplier.GetType("FasterGameLoading.LoadedModManager_ApplyPatches_Patch");
        if (query == null || stage == null) return;
        TryBindPreview(query, stage);
    }
    // Called only after loaded-image admission; separately exercises the live
    // callback/scope contract with synthetic supplier methods in offline tests.
    internal static bool TryBindPreview(Type query, Type stage)
    {
        preview = null; active = null; publications = null; guards.Clear();
        var field = query.GetField("isInPatchOperationValue", BindingFlags.Static | BindingFlags.NonPublic);
        if (field?.FieldType != typeof(bool) || !field.IsDefined(typeof(ThreadStaticAttribute), false)) return false;
        hooks = new[] { AccessTools.Method(query, "Prefix"), AccessTools.Method(query, "Postfix"),
            AccessTools.Method(stage, "Prefix"), AccessTools.Method(stage, "Postfix"), AccessTools.Method(stage, "Finalizer"),
            AccessTools.PropertyGetter(query, "isInPatchOperation"), AccessTools.PropertySetter(query, "isInPatchOperation") };
        if (hooks.Any(m => m == null || !SupplierBodyIdentity.Matches(m))) return false;
        foreach (var method in hooks.Cast<MethodBase>().Concat(new[] { Query, Stage }))
        {
            if (!PublishedPatchGuard.TryCreate(method, "wakeup.def-lookup", out var guard, allPatchKinds: true,
                allowedForeignPatch: p => method == Stage || method == Query && hooks.Take(2).Contains(p.PatchMethod)
                    && p.owner == "FasterGameLoadingMod")) return false;
            guards.Add(guard!);
        }
        active = field; preview = query.Assembly;
        return true;
    }
    internal static bool CanInstall()
    {
        try { Initialize(); return native?.AllowsOriginalContract() == true || PreviewContract(); }
        catch { return false; }
    }
    internal static bool CanUse()
    {
        try
        {
            Initialize();
            return native?.AllowsOriginalContract() == true || PreviewContract() && active?.GetValue(null) is true;
        }
        catch { return false; }
    }
    private static bool PreviewContract()
    {
        if (preview == null) return false;
        if (publications?.Unchanged == true) return true;
        publications = null;
        var query = Harmony.GetPatchInfo(Query);
        var stage = Harmony.GetPatchInfo(Stage);
        if (query == null || stage == null || !Once(query.Prefixes, hooks[0]) || !Once(query.Postfixes, hooks[1])
            || !Once(stage.Prefixes, hooks[2]) || !Once(stage.Postfixes, hooks[3]) || !Once(stage.Finalizers, hooks[4])) return false;
        if (LoaderSupplierPolicy.Patches(Query).Count(p => p.owner == "FasterGameLoadingMod") != 2
            || LoaderSupplierPolicy.Patches(Stage).Count(p => p.owner == "FasterGameLoadingMod") != 3) return false;
        // Callback method hooks can change the meaning without changing the
        // outer patch list; those publications are part of the same snapshot.
        return PublishedPatchGuard.TryCapturePublications(guards, out publications);
    }
    private static bool Once(IEnumerable<Patch> patches, MethodInfo method)
        => patches.Count(p => p.PatchMethod == method && p.owner == "FasterGameLoadingMod") == 1;
}
