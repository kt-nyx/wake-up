// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace WakeUp;

// Public solely for the narrow Prepatcher call from Assembly-CSharp. No generic
// runtime detour and no consumer or holder rewrite is installed.
public static class AssetRoutingRuntime
{
    private static readonly AssetRouteCache<Texture2D> Textures = new();
    private static readonly AssetRouteCache<AudioClip> Audio = new();
    private static readonly AssetRouteCache<string> Strings = new();
    private static readonly BundleRouteCache<Texture2D> TextureBundles = new();
    private static readonly BundleRouteCache<AudioClip> AudioBundles = new();
    private static readonly BundleRouteCache<Shader> ShaderBundles = new();
    private static AssetRoutingPatchGuard? guard;
    private static AssetRoutingPatchGuard? bundleGuard;
    private static bool enabled, bundlesEnabled, busy, bundleBusy;
    private static bool audioDeferred;
    internal static string Status { get; private set; } = "Asset routing is off.";
    internal static readonly string[] Bodies = {
        "060B55A6E9A13229795358A84E87DBE25642D5917481342BC5A7C3BD2DDC02EC",
        "43313E81121591A2B2D88883B33673658CC3071A363DA1EE58F4D89CAA5D0F05",
        "5C97E05DABC6B09BEA2BB397FF600F7DDAAB3CC418B39382004EEC6E4397AA87",
        "69A389BC972D545C9FA37DFF482447BADAA0F02172E4C3DC160E8799E74885E4",
        "11FC760F55E3F7C4CF228A4B8F50C4AD91A64A6F75B37F719E5B09D0462F1202"
    };
    // Match ContractMethods exactly, including the closed Texture2D holder.
    internal static readonly string[] DeferredBodies = {
        "43342AD4A02DF44C47C89A469C5A60E7F6816FA0B67C94A38779C58122D7337B",
        "7C798D9FF7A30D8B40157E152308E03176A024E91AF2448265BFE71B293738EE",
        "C00CAD52776BCD9D725D0C6A41A40167C453B44929C228E20A6B743B9063E140"
    };
    internal static MethodBase[] ContractMethods() => new MethodBase[] {
        AccessTools.Method(typeof(ContentFinder<>), "Get"),
        AccessTools.Method(typeof(ModContentHolder<Texture2D>), "Get"),
        AccessTools.Method(typeof(ModContentPack), "GetContentHolder").MakeGenericMethod(typeof(Texture2D)),
        AccessTools.PropertyGetter(typeof(LoadedModManager), "RunningModsListForReading"),
        AccessTools.PropertyGetter(typeof(UnityData), "IsInMainThread")
    };
    internal static void TryInitialize(IReadOnlyList<string> args)
    {
        if (StartupLaunchSelector.Parse(args).Selection != StartupSelection.Candidate
            || args.Count(a => a.StartsWith("--wake-up-asset-routing=", StringComparison.Ordinal)) != 1
            || !args.Contains("--wake-up-asset-routing=on")) return;
        CompatibilityStatus.Declare("asset-routing/top-level", "Top-level asset lookup");
        CompatibilityStatus.Declare("asset-routing/bundles", "Native asset bundle lookup");
        try
        {
            if (!RuntimeIdentity.ValidateBinaryIdentity(out string reason)) throw new InvalidOperationException(reason);
            if (!MutationStampsWork()) throw new InvalidOperationException("unsupported-dictionary-stamps");
            UnityData.DisposeStatic += Clear;
        }
        catch (Exception e)
        {
            CompatibilityStatus.Refuse("asset-routing/top-level", e.Message);
            CompatibilityStatus.Refuse("asset-routing/bundles", e.Message);
            Status = "Asset routing unavailable: " + e.Message;
            return;
        }
        try
        {
            MethodBase[] methods = ContractMethods();
            for (int i = 0; i < methods.Length; i++)
            {
                if (!SemanticMethodIdentity.TryHash(methods[i], out string body, out _)
                    || body != Bodies[i] && (i >= DeferredBodies.Length || body != DeferredBodies[i]))
                    throw new InvalidOperationException("changed-routing-body-" + methods[i].Name);
            }
            guard = new AssetRoutingPatchGuard();
            if (!guard.Allows()) throw new InvalidOperationException("foreign-routing-hook");
            AssetRoutingContract.Validate();
            audioDeferred = false; // C10 is deferred; stale flags cannot disable retained audio routes.
            enabled = true;
            CompatibilityStatus.Available("asset-routing/top-level");
        }
        catch (Exception e)
        {
            enabled = false; Textures.Clear();
            bool yaopt = LoaderSupplierPolicy.HasYaOptContentWrapper(ContractMethods()[0]);
            CompatibilityStatus.Refuse("asset-routing/top-level", yaopt ? "YaOpt's top-level wrapper retains texture completion, including when its lazy setting is off." : e.Message,
                yaopt ? "supplier-content-wrapper" : "required-contract", yaopt ? "YaOpt" : "Wake-Up");
        }
        try
        {
            // Bundle discovery does not read texture/audio/string holders or
            // call Get/Resources. Preserve all its actual native dependencies.
            var methods = ContractMethods();
            foreach (int i in new[] { 3, 4 })
                if (!SemanticMethodIdentity.TryHash(methods[i], out string hash, out _) || hash != Bodies[i])
                    throw new InvalidOperationException("changed-bundle-dependency-" + methods[i].Name);
            AssetRoutingContract.Validate(bundlesOnly: true);
            bundleGuard = new AssetRoutingPatchGuard(bundlesOnly: true);
            if (!bundleGuard.Allows()) throw new InvalidOperationException("foreign-bundle-hook");
            bundlesEnabled = true;
            CompatibilityStatus.Available("asset-routing/bundles");
        }
        catch (Exception e) { bundlesEnabled = false; CompatibilityStatus.Refuse("asset-routing/bundles", e.Message); }
        Status = "Asset routing: top-level " + (enabled ? "available" : "ordinary/provider lookup")
            + "; native bundles " + (bundlesEnabled ? "available" : "ordinary lookup") + ".";
        Log.Message("[Wake-Up] " + Status);
    }
    internal static bool MutationStampsWork()
    {
        var values = new Dictionary<string, object> { ["a"] = new object() };
        foreach (Action change in new Action[] { () => values["a"] = new object(), () => values.Remove("a"),
            () => values.Add("b", new object()), () => values.Clear() })
        {
            var enumerator = (System.Collections.IEnumerator)values.GetEnumerator(); change();
            try { enumerator.Reset(); return false; } catch (InvalidOperationException) { }
        }
        return true;
    }
    public static bool TryGet(Type type, string path, out object? result)
        => TryResolve(type, path, out result) && result != null;

    private static void Clear()
    {
        Textures.Clear(); Audio.Clear(); Strings.Clear();
        TextureBundles.Clear(); AudioBundles.Clear(); ShaderBundles.Clear();
    }

    private static bool Admitted(Type type, bool selected)
        => selected && (type == typeof(Texture2D) || type == typeof(AudioClip)
            || type == typeof(string) || type == typeof(Shader)) && UnityData.IsInMainThread;

    // True means resolution ran, including a native miss. The prepatch sends a
    // missing result to the original reportFailure/requester diagnostics.
    public static bool TryResolve(Type type, string path, out object? result)
    {
        // C09 memoization assumes populated public holders. This experimental
        // private population uses the native provider search and request bridge.
        if (type == typeof(Texture2D) && DemandTextureRuntime.Enabled) { result = null; return false; }
        result = null;
        if (!Admitted(type, enabled) || busy || path == null) return false;
        busy = true;
        try
        {
            if (guard?.Allows() != true)
            {
                Clear();
                Status = "Top-level asset routing refused a changed hook chain; ordinary loading is in use."; CompatibilityStatus.Refuse("asset-routing/top-level", Status);
                return false;
            }
            var mods = LoadedModManager.RunningModsListForReading;
            bool incompatibleComparer = false;
            bool canRoute = type == typeof(Texture2D) ? AssetRouteCache<Texture2D>.CanRoute(mods, out incompatibleComparer)
                : type == typeof(AudioClip) ? !audioDeferred && AssetRouteCache<AudioClip>.CanRoute(mods, out incompatibleComparer)
                : type != typeof(string) || AssetRouteCache<string>.CanRoute(mods, out incompatibleComparer);
            if (!canRoute)
            {
                Clear(); Status = "Asset routing could not validate the last lookup; ordinary loading is in use.";
                if (incompatibleComparer) CompatibilityStatus.Registry?.Set("asset-routing/top-level", OperationState.PartiallyAvailable,
                    "A provider uses unsupported asset-name comparison; affected lookups stay native.", "provider-comparer");
                return false;
            }
            if (type == typeof(Texture2D))
            {
                if (mods.Count != 0) PreparedQualityRuntime.ObserveRoute(path);
                result = Resolve(Textures, path, () => Resources.Load<Texture2D>(GenFilePaths.ContentPath<Texture2D>() + path), () => ContentFinder<Texture2D>.TryFindAssetInModBundles(path));
            }
            else if (type == typeof(AudioClip)) result = Resolve(Audio, path, () => Resources.Load<AudioClip>(GenFilePaths.ContentPath<AudioClip>() + path), () => ContentFinder<AudioClip>.TryFindAssetInModBundles(path));
            else if (type == typeof(string)) result = Resolve(Strings, path, null, null);
            else result = ContentFinder<Shader>.TryFindAssetInModBundles(path);
            Status = result == null ? "Asset routing is ready; the last lookup used ordinary loading diagnostics."
                : "Asset routing is active for filesystem, Resources and bundles; native readiness is unchanged.";
            return true;
        }
        finally { busy = false; }
    }

    private static T? Resolve<T>(AssetRouteCache<T> cache, string path, Func<T>? resource, Func<T>? bundle) where T : class
    {
        var mods = LoadedModManager.RunningModsListForReading;
        if (!cache.HasExternalRoute(mods, path) && cache.TryGet(mods, path, out T? held)) return held;
        // Even a remembered bundle route must ask Resources first. ActiveAPI
        // can be changed and its callbacks must execute exactly once per call.
        T? result = resource?.Invoke();
        if (result == null) result = bundle?.Invoke();
        if (result != null && guard?.Allows() == true) cache.RememberExternal(mods, path);
        else cache.Forget(path);
        return result;
    }

    public static bool TryBundles(Type type, string path, out object? result)
    {
        result = null;
        if (!Admitted(type, bundlesEnabled) || type == typeof(string) || bundleBusy || path == null) return false;
        // Recheck after a Resources callback, which may have changed hooks.
        if (bundleGuard?.Allows() != true)
        { Clear(); CompatibilityStatus.Refuse("asset-routing/bundles", "The native bundle hook chain changed."); return false; }
        bundleBusy = true;
        try
        {
            var mods = LoadedModManager.RunningModsListForReading;
            if (type == typeof(Texture2D)) result = TextureBundles.Resolve(mods, path, ModAssetBundlesHandler.TextureExtensions);
            else if (type == typeof(AudioClip)) result = AudioBundles.Resolve(mods, path, ModAssetBundlesHandler.AudioClipExtensions);
            else result = ShaderBundles.Resolve(mods, path, ModAssetBundlesHandler.ShaderExtensions);
            return true;
        }
        finally { bundleBusy = false; }
    }
}
