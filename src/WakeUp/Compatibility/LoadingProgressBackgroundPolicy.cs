// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace WakeUp;

// LP's optional deferred renderer replaces native completion/error handling.
// With that option off and startup Finished, its dispatcher keeps native work.
internal static class LoadingProgressBackgroundPolicy
{
    private const string Prefix = "ilyvion.LoadingProgress.";
    private static bool resolved, present, admitted, mapResolved, mapAdmitted;
    private static Assembly? assembly;
    private static FieldInfo? instance, modSettings, deferred, stage;
    private static Type? settingsType;
    private static object? finishedStage;
    private static PublishedPatchGuard[] sharedBodies = Array.Empty<PublishedPatchGuard>(), mapBodies = Array.Empty<PublishedPatchGuard>();
    private static readonly Dictionary<MethodBase, MethodInfo> callbacks = new();

    private static void Resolve()
    {
        if (resolved) return;
        resolved = true; present = LifecycleSupplierPolicy.Active(LoadingProgressCompatibility.PackageId);
        if (!present) return;
        try
        {
            assembly = LoaderSupplierPolicy.Find(LoadingProgressCompatibility.PackageId, Prefix + "LoadingProgressMod");
            if (assembly == null || !LoaderSupplierPolicy.MatchesImage(assembly,
                "b12362274d99b5be65222b536ab568b03724bfabb4d2ffc1b583d94468359d61")) return;
            var modType = Type("LoadingProgressMod"); settingsType = Type("Settings");
            instance = AccessTools.Field(modType, "instance"); modSettings = AccessTools.Field(typeof(Mod), "modSettings");
            deferred = AccessTools.Field(settingsType, "_patchInGameDeferredRepaint");
            stage = AccessTools.Field(Type("LoadingProgressWindow"), "<CurrentStage>k__BackingField");
            var enumType = Type("LoadingStage");
            if (instance?.IsStatic != true || instance.FieldType != modType || modSettings == null || modSettings.IsStatic
                || deferred?.FieldType != typeof(bool) || deferred.IsStatic || stage?.IsStatic != true || stage.FieldType != enumType
                || !enumType.IsEnum || Convert.ToInt32(Enum.Parse(enumType, "Finished")) != 37) return;
            finishedStage = Enum.Parse(enumType, "Finished");
            var methods = new List<MethodBase>();
            Add(methods, "LongEventHandler_LongEventsUpdate_Patches", "Postfix");
            Add(methods, "LongEventHandler_ClearQueuedEvents_Patches", "Postfix");
            Add(methods, "LongEventHandler_UpdateCurrentEnumeratorEvent_Patches", "Transpiler", "ShouldStopEarly");
            Add(methods, "LongEventHandler_UpdateCurrentEnumeratorEvent_Patches+<>c", "<Transpiler>b__4_0");
            Add(methods, "LongEventHandler_ExecuteToExecuteWhenFinished_Patches", "Prefix");
            Add(methods, "LoadingProgressWindow", "get_CurrentStage");
            Add(methods, "LoadingProgressMod", "get_Settings", "Error", "DevMessage");
            Add(methods, "Settings", "get_PatchInGameDeferredRepaint", "get_ShowInGameLoadingProgress");
            Add(methods, "InGameLoadingSession", "SignalReset", "Update", "AdvanceSession", "DetermineKind", "End", "UpdateSceneLoadPhase",
                "DetermineStartPhase", "IsSceneLoadPhase", "ShouldEnterSpawningColonistsPhase", "SetProgress", "NextPhase", "PhasesFor",
                "get_CurrentKindPhases", "get_Kind", "set_Kind", "get_IsActive", "get_Phase", "set_Label", "LogUnrecognizedKeyIfNeeded");
            Add(methods, "InGameLoadingSession+ProgressSnapshot", "get_Phase");
            var constructor = Type("InGameLoadingSession+ProgressSnapshot").GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single();
            methods.Add(constructor);
            var supplierGuards = Guard(methods).ToList();
            if (!PublishedPatchGuard.TryCreate(AccessTools.Method(typeof(RepaintCoalescer), "Coalesce"),
                    BackgroundLoadingRuntime.Owner, out var ownRepaint, true) || !ownRepaint!.AllowsOriginalContract()) return;
            supplierGuards.Add(ownRepaint);
            sharedBodies = supplierGuards.ToArray();
            admitted = true;
        }
        catch { admitted = false; }
    }
    private static Type Type(string name) => assembly!.GetType(Prefix + name, true);
    private static void Add(List<MethodBase> methods, string type, params string[] names)
    {
        var all = Type(type).GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        foreach (string name in names)
        {
            var matches = all.Where(m => m.Name == name).ToArray();
            if (matches.Length == 0) throw new MissingMethodException(type, name);
            methods.AddRange(matches);
        }
    }
    private static PublishedPatchGuard[] Guard(IEnumerable<MethodBase> methods)
    {
        var guards = new List<PublishedPatchGuard>();
        foreach (var method in methods.Distinct())
        {
            if (!SupplierBodyIdentity.Matches(method) || !PublishedPatchGuard.TryCreate(method, BackgroundLoadingRuntime.Owner, out var guard, true,
                    p => RepaintCoalescer.AllowsBackgroundCooperation(method, p))
                || !guard!.AllowsOriginalContract()) throw new InvalidOperationException("Loading Progress helper changed: " + method.Name);
            guards.Add(guard);
        }
        return guards.ToArray();
    }
    private static bool ModeAllowed()
    {
        if (!admitted) return false;
        try
        {
            object? mod = instance!.GetValue(null);
            object? settings = mod == null ? null : modSettings!.GetValue(mod);
            return settings?.GetType() == settingsType && !(bool)deferred!.GetValue(settings);
        }
        catch { return false; }
    }
    private static bool Finished()
    { try { return admitted && Equals(stage!.GetValue(null), finishedStage); } catch { return false; } }
    internal static bool CanAcquire()
    { Resolve(); return !present || ModeAllowed() && Finished(); }
    internal static bool Unchanged(bool activeLease, bool map = false)
    {
        Resolve();
        return !present || ModeAllowed() && (!activeLease || Finished())
            && (map ? mapAdmitted && mapBodies.All(g => g.AllowsOriginalContract()) : sharedBodies.All(g => g.AllowsOriginalContract()));
    }
    internal static bool Allows(MethodBase target, Patch patch, bool map = false)
    {
        if (patch.owner != LoadingProgressCompatibility.PackageId) return false;
        Resolve();
        if (!ModeAllowed() || patch.PatchMethod.Module.Assembly != assembly) return false;
        string? name = null; HarmonyPatchType kind = HarmonyPatchType.Postfix;
        if (map && target.DeclaringType == typeof(WorldRenderer) && target.Name == "RegenerateLayersIfDirtyInLongEvent")
        { name = "WorldRenderer_RegenerateLayersIfDirtyInLongEvent_Patches"; kind = HarmonyPatchType.Prefix; }
        else if (!map && target.DeclaringType == typeof(LongEventHandler))
        {
            switch (target.Name)
            {
                case "LongEventsUpdate": name = "LongEventHandler_LongEventsUpdate_Patches"; break;
                case "ClearQueuedEvents": name = "LongEventHandler_ClearQueuedEvents_Patches"; break;
                case "UpdateCurrentEnumeratorEvent": name = "LongEventHandler_UpdateCurrentEnumeratorEvent_Patches"; kind = HarmonyPatchType.Transpiler; break;
                case "ExecuteToExecuteWhenFinished": name = "LongEventHandler_ExecuteToExecuteWhenFinished_Patches"; kind = HarmonyPatchType.Prefix; break;
            }
        }
        if (name == null) return false;
        if (map && !mapResolved)
        {
            mapResolved = true;
            try
            {
                var methods = new List<MethodBase>();
                Add(methods, name, "Prefix"); Add(methods, name + "+<>c", "<Prefix>b__0_0");
                Add(methods, "InGameLoadingSession", "CountDirtyVisibleLayers", "OnPlanetRegenerationQueued");
                Add(methods, "InGameLoadingSession+<>c", "<CountDirtyVisibleLayers>b__34_0");
                mapBodies = Guard(methods); mapAdmitted = true;
            }
            catch { mapAdmitted = false; }
        }
        if (!callbacks.TryGetValue(target, out var method))
            callbacks[target] = method = AccessTools.Method(Type(name), kind.ToString());
        return method == patch.PatchMethod && Unchanged(false, map) && Exact(target, method, kind);
    }
    internal static bool Exact(MethodBase target, MethodInfo method, HarmonyPatchType kind)
    {
        var info = Harmony.GetPatchInfo(target);
        var expected = kind == HarmonyPatchType.Prefix ? info?.Prefixes : kind == HarmonyPatchType.Postfix ? info?.Postfixes : info?.Transpilers;
        return expected?.Count(p => p.owner == LoadingProgressCompatibility.PackageId && p.PatchMethod == method
            && p.priority == Priority.Normal && p.before.Length == 0 && p.after.Length == 0) == 1
            && LoaderSupplierPolicy.Patches(target).Count(p => p.owner == LoadingProgressCompatibility.PackageId) == 1;
    }
    internal static SupplierConflictException Conflict()
    {
        Resolve();
        return admitted && !ModeAllowed()
            ? new SupplierConflictException("Loading Progress's renderer repaint mode prevents Wake-Up background loading. To use the supported mode, turn off 'Patch in-game renderer regeneration to keep the loading window responsive' in Loading Progress and restart. Its progress screen and compatible startup improvements can remain enabled.",
                "Loading Progress", "supplier-deferred-completion")
            : new SupplierConflictException("Loading Progress's loading callbacks or completion stage changed. Wake-Up stopped the affected background operation; native loading remains available. Its independent startup improvements can remain enabled.",
                "Loading Progress", "supplier-background-contract");
    }
}
