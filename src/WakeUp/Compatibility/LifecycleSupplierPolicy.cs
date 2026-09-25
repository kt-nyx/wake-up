// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace WakeUp;

internal sealed class SupplierConflictException : InvalidOperationException
{
    internal readonly string Provider, Code;
    internal SupplierConflictException(string reason, string provider, string code) : base(reason)
    { Provider = provider; Code = code; }
}

internal static class LifecycleSupplierPolicy
{
    internal const string RimThemesPackage = "arandomkiwi.rimthemes";
    internal const string RimThemesOwner = "rimworld.aRandomKiwi.RimTheme";
    private const string RimThemesHash = "f89b8d7858d50249f7016be40682bc78a227e51eb922d0083fc3617504d9ee01";
    private static bool themesResolved, themesPresent;
    private static Assembly? themes;
    private static FieldInfo? disableLoader, themeSettings;
    private static readonly Dictionary<MethodInfo, (MethodBase Target, PublishedPatchGuard Guard)> themeCallbacks = new();
    internal static bool Active(string id) => LoadedModManager.RunningModsListForReading.Any(m => m.PackageId.Equals(id, StringComparison.OrdinalIgnoreCase));
    private static void ResolveThemes()
    {
        if (themesResolved) return;
        themesResolved = true; themesPresent = Active(RimThemesPackage);
        if (!themesPresent) return;
        try
        {
            var assembly = LoaderSupplierPolicy.Find(RimThemesPackage, "aRandomKiwi.RimThemes.Settings");
            if (assembly == null || !LoaderSupplierPolicy.MatchesImage(assembly, RimThemesHash)) return;
            var settings = assembly.GetType("aRandomKiwi.RimThemes.Settings", true);
            disableLoader = AccessTools.Field(settings, "disableCustomLoader");
            themeSettings = AccessTools.Field(assembly.GetType("aRandomKiwi.RimThemes.Utils", true), "modSettings");
            if (disableLoader?.FieldType != typeof(bool) || !disableLoader.IsStatic
                || themeSettings?.FieldType != settings || !themeSettings.IsStatic) return;
            themes = assembly;
        }
        catch { themes = null; }
    }
    internal static bool RimThemesOwnsDisplay()
    {
        ResolveThemes();
        if (!themesPresent) return false;
        try { return themes == null || themeSettings!.GetValue(null) == null || !(bool)disableLoader!.GetValue(null); }
        catch { return true; }
    }
    internal static string DisplayReason()
        => Active(LoadingProgressCompatibility.PackageId)
            ? "RimThemes and Loading Progress both provide custom loading screens. Choose one in their settings; disable RimThemes' custom loader to keep Loading Progress. Wake-Up leaves its own drawing inactive."
            : "RimThemes provides the custom loading screen. To use Wake-Up's display, disable RimThemes' custom loader and restart.";
    internal static SupplierConflictException ThemeDisplayConflict() => new(DisplayReason(),
        Active(LoadingProgressCompatibility.PackageId) ? "RimThemes + Loading Progress" : "RimThemes", "supplier-display");
    internal static void Refuse(string id, string reason, Exception error)
    {
        var supplier = error as SupplierConflictException;
        CompatibilityStatus.Refuse(id, reason, supplier?.Code ?? "required-contract", supplier?.Provider ?? "Wake-Up");
    }
    internal static bool PreserveDlcPanel(bool explicitHide)
        => !explicitHide && Active("ferny.nomodlistonloading");

    internal static void AdviseDefLoadCache()
    {
        try
        {
            var assembly = LoaderSupplierPolicy.Find("fluxxfield.defloadcache", "FluxxField.DefLoadCache.DefLoadCacheMod");
            if (assembly == null || !LoaderSupplierPolicy.MatchesImage(assembly,
                "da9f3345767cb0f5d6d6626f0b877fd56b3bdec2bcd77c5b44a61ff34d8d75bb")) return;
            var mod = assembly.GetType("FluxxField.DefLoadCache.DefLoadCacheMod", true);
            var type = assembly.GetType("FluxxField.DefLoadCache.DefLoadCacheSettings", true);
            var backing = AccessTools.Field(mod, "<Settings>k__BackingField");
            if (backing?.IsStatic != true || backing.FieldType != type) return;
            var settings = backing.GetValue(null);
            if (settings == null) return; // Never construct a foreign Mod/settings object.
            bool Read(string name)
            {
                var field = AccessTools.Field(type, name);
                if (field == null || field.IsStatic || field.FieldType != typeof(bool)) throw new InvalidOperationException();
                return (bool)field.GetValue(settings);
            }
            if (Read("cacheEnabled") && Read("skipModFileLoading") && !Read("skipPatchApplication"))
                CompatibilityStatus.Registry?.Advise("source-skip-without-patch-replay", "DefLoadCache",
                    "DefLoadCache is set to skip reading mod files but still run XML patches. Its cached launch can then have no source XML to patch. In DefLoadCache settings, turn off 'Skip reading mod files on repeat launches' or enable its patch-skipping option, then restart. Wake-Up cannot repair this supplier setting combination and has not changed either setting.");
        }
        catch { } // An unreadable/unknown supplier does not justify a guessed advisory.
    }

    internal static bool AllowsThemeObserver(MethodBase target, Patch patch)
    {
        if (patch.owner != RimThemesOwner) return false;
        ResolveThemes(); if (themes == null || patch.PatchMethod.Module.Assembly != themes) return false;
        string? name = ThemeObserver(target);
        if (name == null || patch.PatchMethod.DeclaringType?.FullName != "aRandomKiwi.RimThemes." + name
            || patch.PatchMethod.Name != "Prefix") return false;
        var method = patch.PatchMethod;
        if (!themeCallbacks.TryGetValue(method, out var admitted))
        {
            if (!SupplierBodyIdentity.Matches(method) || !PublishedPatchGuard.TryCreate(method, BackgroundLoadingRuntime.Owner, out var body, true)
                || !body!.AllowsOriginalContract()) return false;
            admitted = (target, body);
            themeCallbacks.Add(method, admitted);
        }
        return admitted.Target == target && admitted.Guard.AllowsOriginalContract() && ImageSupplierPolicy.ExactPublication(target, method, RimThemesOwner, true)
            && LoaderSupplierPolicy.Patches(target).Count(p => p.owner == RimThemesOwner) == 1;
    }
    private static string? ThemeObserver(MethodBase target)
    {
        if (target.DeclaringType == typeof(WorldRenderer) && target.Name == "RegenerateDirtyLayersNow_Async" && target.GetParameters().Length == 0)
            return "WorldRenderer_RegenerateDirtyLayersNow_Async_Patch";
        if (target.DeclaringType != typeof(LongEventHandler) || target.Name != "QueueLongEvent") return null;
        var args = target.GetParameters().Select(p => p.ParameterType);
        if (args.SequenceEqual(new[] { typeof(IEnumerable), typeof(string), typeof(Action<Exception>), typeof(bool), typeof(bool) })) return "LongEventHandler_QueueLongEvent_Patch";
        if (args.SequenceEqual(new[] { typeof(Action), typeof(string), typeof(string), typeof(bool), typeof(Action<Exception>), typeof(bool), typeof(bool) })) return "LongEventHandler_QueueLongEvent2_Patch";
        if (args.SequenceEqual(new[] { typeof(Action), typeof(string), typeof(bool), typeof(Action<Exception>), typeof(bool), typeof(bool), typeof(Action) })) return "LongEventHandler_QueueLongEvent3_Patch";
        return null;
    }
    internal static bool ThemeCallbacksUnchanged(bool map = false) => themeCallbacks.Values
        .Where(g => (g.Target.DeclaringType == typeof(WorldRenderer)) == map).All(g => g.Guard.AllowsOriginalContract());
    internal static SupplierConflictException ThemeBackgroundConflict() => new(
        "A previously admitted RimThemes loading callback changed. Wake-Up leaves the affected loading operation with the current provider; independent startup improvements can remain enabled.",
        "RimThemes", "supplier-background-contract");
    internal static Exception BackgroundConflict(MethodBase target)
    {
        if (ThemeObserver(target) is string observer && LoaderSupplierPolicy.Patches(target).Any(p => p.owner == RimThemesOwner
                && p.PatchMethod.Module.Assembly == themes && p.PatchMethod.DeclaringType?.FullName == "aRandomKiwi.RimThemes." + observer
                && !AllowsThemeObserver(target, p)))
            return ThemeBackgroundConflict();
        if (LoaderSupplierPolicy.Patches(target).Any(p => p.owner == "LoadInBackgroundMod"))
            return new SupplierConflictException("Load in Background controls the run-in-background preference. Wake-Up leaves shared background loading with that provider; its independent startup improvements can still run.", "Load in Background", "supplier-background-preference");
        var loadingProgress = LoaderSupplierPolicy.Find(LoadingProgressCompatibility.PackageId, "ilyvion.LoadingProgress.LongEventHandler_UpdateCurrentEnumeratorEvent_Patches");
        if (loadingProgress != null && LoaderSupplierPolicy.Patches(target).Any(p => p.PatchMethod.Module.Assembly == loadingProgress))
            return LoadingProgressBackgroundPolicy.Conflict();
        return new InvalidOperationException("Another mod changes the save-loading lifecycle.");
    }
}
