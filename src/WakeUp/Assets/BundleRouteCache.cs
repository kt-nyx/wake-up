// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace WakeUp;

// Metadata only. Native bundles own their content; every hit performs the same
// typed LoadAsset on the current winning bundle. No object or miss is cached.
internal sealed class BundleRouteCache<T> where T : UnityEngine.Object
{
    private readonly Dictionary<string, Route> routes = new(StringComparer.Ordinal);
    // Weak as a complete graph: list enumerators must not root removed bundles.
    private System.WeakReference<Generation>? generation;
    private sealed class Generation
    {
        internal readonly List<ModContentPack> Mods;
        internal readonly IEnumerator Order;
        internal readonly Provider[] Providers;
        internal readonly string[] Extensions;
        internal Generation(List<ModContentPack> mods, string[] suffixes)
        {
            Mods = mods; Order = (IEnumerator)mods.GetEnumerator();
            Providers = mods.Select(m => new Provider(m)).ToArray();
            Extensions = (string[])suffixes.Clone();
        }
    }
    private Generation? Snapshot => generation != null && generation.TryGetTarget(out var value) ? value : null;
    internal long Hits { get; private set; }
    internal long Searches { get; private set; }
    internal long Loads { get; private set; }
    internal long Invalidations { get; private set; }
    internal int Count => routes.Count;
    internal string[] Paths => routes.Keys.ToArray();
    private sealed class Route
    {
        internal readonly int Mod, Bundle;
        internal readonly string Name;
        internal Route(int mod, int bundle, string name) { Mod = mod; Bundle = bundle; Name = name; }
    }
    private sealed class Provider
    {
        internal readonly ModAssetBundlesHandler Handler;
        internal readonly List<AssetBundle> Bundles;
        internal readonly IEnumerator Version;
        internal readonly string Folder, Package;
        internal readonly bool Official;
        internal Provider(ModContentPack mod)
        {
            Handler = mod.assetBundles; Bundles = Handler.loadedAssetBundles;
            Version = (IEnumerator)Bundles.GetEnumerator();
            Folder = mod.FolderName; Package = mod.PackageIdPlayerFacing; Official = mod.IsOfficialMod;
        }
        internal bool Current(ModContentPack mod)
            => ReferenceEquals(Handler, mod.assetBundles) && ReferenceEquals(Bundles, Handler.loadedAssetBundles)
                && Unchanged(Version) && Folder == mod.FolderName && Package == mod.PackageIdPlayerFacing
                && Official == mod.IsOfficialMod && Bundles.All(b => b != null);
    }
    private static bool Unchanged(IEnumerator stamp)
    { try { stamp.Reset(); return true; } catch (InvalidOperationException) { return false; } }
    internal T? Resolve(List<ModContentPack> current, string path, string[] suffixes)
    {
        var state = Snapshot;
        bool same = false;
        try
        {
            same = state != null && ReferenceEquals(state.Mods, current) && Unchanged(state.Order)
                && state.Extensions.SequenceEqual(suffixes) && state.Providers.Length == current.Count;
            if (same)
                for (int i = 0; i < state!.Providers.Length; i++)
                    if (!state.Providers[i].Current(current[i])) { same = false; break; }
        }
        catch (Exception) { same = false; } // metadata only, before native loads
        if (!same)
        {
            Clear(); Invalidations++;
        }
        if (same && routes.TryGetValue(path, out var route))
        {
            Loads++;
            T? value = current[route.Mod].assetBundles.loadedAssetBundles[route.Bundle].LoadAsset(route.Name, typeof(T)) as T;
            if (!ReferenceEquals(value, null)) { Hits++; return value; }
            // The higher immutable providers still cannot win. Do not replay
            // this load; forget it so the next request retries native discovery.
            routes.Clear(); Invalidations++; return null;
        }
        Searches++;
        string root = Path.Combine("Assets", "Data");
        for (int i = current.Count - 1; i >= 0; i--)
        {
            ModContentPack mod = current[i];
            string folder = Path.Combine(root, mod.FolderName);
            string package = Path.Combine(root, mod.PackageIdPlayerFacing);
            bool alternate = !mod.IsOfficialMod;
            int j = 0;
            // Keep native enumeration and its mutation exceptions.
            foreach (AssetBundle bundle in mod.assetBundles.loadedAssetBundles)
            {
                string first = Path.Combine(Path.Combine(folder, GenFilePaths.ContentPath<T>()), path);
                string second = Path.Combine(Path.Combine(package, GenFilePaths.ContentPath<T>()), path);
                foreach (string suffix in suffixes)
                {
                    Loads++;
                    if (bundle.LoadAsset(first + suffix, typeof(T)) is T value)
                    { Remember(current, suffixes, path, i, j, first + suffix); return value; }
                    if (alternate)
                    {
                        Loads++;
                        if (bundle.LoadAsset(second + suffix, typeof(T)) is T other)
                        { Remember(current, suffixes, path, i, j, second + suffix); return other; }
                    }
                }
                j++;
            }
        }
        routes.Remove(path);
        return null;
    }
    private void Remember(List<ModContentPack> current, string[] suffixes, string path, int mod, int bundle, string name)
    {
        try
        {
            // Native success comes first. Malformed lower providers must not
            // prevent a winner that the native search would already return.
            var state = Snapshot;
            if (state == null && current.Count <= AssetRouteCache<string>.MaximumProviders)
            {
                state = new Generation(current, suffixes);
                generation = new System.WeakReference<Generation>(state);
            }
            if (state != null && routes.Count < AssetRouteCache<string>.MaximumRoutes)
                routes[path] = new Route(mod, bundle, name);
        }
        catch (Exception) { Clear(); } // never retry a successful native load
    }
    internal void Clear()
    { routes.Clear(); generation = null; }
}
