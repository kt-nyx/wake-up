// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace WakeUp;

// Mono may share reference-type generic implementations. Watch all published
// instantiations of the relevant methods, including patches added after startup.
internal sealed class AssetRoutingPatchGuard
{
    private readonly Dictionary<MethodBase, byte[]> state;
    private IEnumerator? version;
    private bool allowed;
    internal AssetRoutingPatchGuard()
    {
        var field = typeof(Harmony).Assembly.GetType("HarmonyLib.HarmonySharedState")?
            .GetField("state", BindingFlags.NonPublic | BindingFlags.Static);
        if (field?.IsInitOnly != true || field.FieldType != typeof(Dictionary<MethodBase, byte[]>))
            throw new InvalidOperationException("unknown-harmony-state");
        state = (Dictionary<MethodBase, byte[]>)field.GetValue(null);
    }
    internal bool Allows()
    {
        lock (state)
        {
            if (version != null)
                try { version.Reset(); return allowed; } catch (InvalidOperationException) { }
        }
        // Never take Harmony's processor lock while holding its state lock.
        MethodBase[] methods;
        IEnumerator snapshot;
        lock (state) { snapshot = (IEnumerator)state.GetEnumerator(); methods = state.Keys.Where(Relevant).ToArray(); }
        bool decision = methods.All(m =>
        {
            var info = Harmony.GetPatchInfo(m);
            bool qualityRead = m == AccessTools.Method(typeof(ModContentHolder<UnityEngine.Texture2D>), "Get");
            return info == null || info.Prefixes.All(p => qualityRead && PreparedQualityRuntime.IsRoutingReadPrefix(p))
                && !info.Postfixes.Concat(info.Transpilers).Concat(info.Finalizers)
                    .Concat(info.InnerPrefixes).Concat(info.InnerPostfixes).Any();
        });
        lock (state)
        {
            try { snapshot.Reset(); } catch (InvalidOperationException) { return false; }
            version = snapshot; allowed = decision; return allowed;
        }
    }
    internal static bool Relevant(MethodBase method)
    {
        var type = method.DeclaringType;
        if (type?.IsGenericType == true) type = type.GetGenericTypeDefinition();
        return (type == typeof(ContentFinder<>) || type == typeof(ModContentHolder<>)) && (method.Name == "Get" || method.Name == "TryFindAssetInModBundles")
            || type == typeof(ModContentPack) && (method.Name == "GetContentHolder" || method.Name == "get_FolderName"
                || method.Name == "get_PackageIdPlayerFacing" || method.Name == "get_IsOfficialMod")
            || type == typeof(GenFilePaths) && method.Name == "ContentPath"
            || type == typeof(UnityEngine.AssetBundle) && (method.Name == "LoadAsset" || method.Name == "LoadAsset_Internal")
            || type == typeof(LoadedModManager) && method.Name == "get_RunningModsListForReading"
            || type == typeof(UnityData) && method.Name == "get_IsInMainThread";
    }
}
