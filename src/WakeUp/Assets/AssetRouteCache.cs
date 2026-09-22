// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace WakeUp;

// A route owns no asset. It remembers a supplier index, then asks that supplier's
// current holder for the object. T separates content types; paths stay exact.
internal sealed class AssetRouteCache<T> where T : class
{
    internal const int MaximumRoutes = 16384, MaximumProviders = 4096;
    private readonly Dictionary<string, int> routes = new(StringComparer.Ordinal);
    // The snapshot includes enumerators, which themselves retain collections.
    // Keep the WHOLE graph weak: removed holders must not keep assets alive.
    private System.WeakReference<Generation>? generation;
    private sealed class Generation
    {
        internal readonly List<ModContentPack> Mods;
        internal readonly IEnumerator Order;
        internal readonly HolderStamp?[] Stamps;
        internal Generation(List<ModContentPack> mods)
        { Mods = mods; Order = (IEnumerator)mods.GetEnumerator(); Stamps = new HolderStamp?[mods.Count]; }
    }
    private Generation? Snapshot => generation != null && generation.TryGetTarget(out var value) ? value : null;
    internal long Hits { get; private set; }
    internal long Searches { get; private set; }
    internal long HolderLookups { get; private set; }
    internal long Invalidations { get; private set; }
    internal int Count => routes.Count;
    internal string[] ExternalPaths => routes.Where(r => r.Value < 0).Select(r => r.Key).ToArray();
    internal void Forget(string path) => routes.Remove(path);
    internal static bool CanRoute(List<ModContentPack> current)
    {
        if (current == null || current.Count > MaximumProviders) return false;
        try
        {
            foreach (var mod in current)
            {
                var values = mod.GetContentHolder<T>().contentList;
                if (values == null || !ReferenceEquals(values.Comparer, EqualityComparer<string>.Default)
                    && !ReferenceEquals(values.Comparer, StringComparer.Ordinal)) return false;
            }
            return true;
        }
        catch (Exception) { return false; }
    }
    // Negative provider index denotes a successful external route, never a miss.
    internal bool HasExternalRoute(List<ModContentPack> current, string path)
    {
        var state = Snapshot;
        if (state == null || !ReferenceEquals(state.Mods, current) || !Unchanged(state.Order)
            || !routes.TryGetValue(path, out int provider) || provider != -1) return false;
        for (int i = current.Count - 1; i >= 0; i--)
            if (state.Stamps[i]?.Current(current[i].GetContentHolder<T>()) != true) return false;
        Hits++;
        return true;
    }
    internal void RememberExternal(List<ModContentPack> current, string path)
    {
        // Stamps precede external callbacks. Never refresh them after a callback
        // and accidentally certify a different provider generation.
        var state = Snapshot;
        if (state == null || !ReferenceEquals(state.Mods, current) || !Unchanged(state.Order)) return;
        for (int i = current.Count - 1; i >= 0; i--)
            if (state.Stamps[i]?.Current(current[i].GetContentHolder<T>()) != true) return;
        if (routes.Count < MaximumRoutes) routes[path] = -1;
    }

    private sealed class HolderStamp
    {
        internal readonly ModContentHolder<T> Holder;
        internal readonly Dictionary<string, T> Content;
        private readonly IEnumerator version;
        internal HolderStamp(ModContentHolder<T> holder)
        {
            Holder = holder; Content = holder.contentList;
            if (!ReferenceEquals(Content.Comparer, EqualityComparer<string>.Default)
                && !ReferenceEquals(Content.Comparer, StringComparer.Ordinal))
                throw new InvalidOperationException("unknown-content-comparer");
            version = (IEnumerator)Content.GetEnumerator();
        }
        internal bool Current(ModContentHolder<T> holder)
            => ReferenceEquals(Holder, holder) && ReferenceEquals(Content, holder.contentList) && Unchanged(version);
    }

    private static bool Unchanged(IEnumerator version)
    {
        try { version.Reset(); return true; }
        catch (InvalidOperationException) { return false; }
    }

    internal bool TryGet(List<ModContentPack> current, string path, out T? result)
    {
        result = null;
        if (path == null || current == null || current.Count > MaximumProviders) { Clear(); return false; }
        var state = Snapshot;
        if (state == null || !ReferenceEquals(state.Mods, current) || !Unchanged(state.Order))
        {
            Clear(); state = new Generation(current); generation = new System.WeakReference<Generation>(state);
        }
        if (routes.TryGetValue(path, out int supplier) && supplier >= 0)
        {
            // Earlier suppliers cannot override this winner. Later holders must
            // remain unchanged even when empty: mods may write directly to the
            // public dictionary, without calling ReloadAll or ClearDestroy.
            bool valid = true;
            for (int i = current.Count - 1; i >= supplier; i--)
            {
                var holder = current[i].GetContentHolder<T>();
                if (state.Stamps[i]?.Current(holder) != true) { valid = false; break; }
            }
            if (valid)
            {
                HolderLookups++;
                result = current[supplier].GetContentHolder<T>().Get(path);
                if (result != null) { Hits++; return true; }
            }
            // Invalidate all routes, including paths unrelated to this request:
            // a changed higher provider may now override any of them.
            routes.Clear(); Array.Clear(state.Stamps, 0, state.Stamps.Length); Invalidations++;
        }
        Searches++;
        for (int i = current.Count - 1; i >= 0; i--)
        {
            var holder = current[i].GetContentHolder<T>();
            // Replacing a shared stamp must also invalidate older routes whose
            // proofs depended on it, even on a previously unseen path.
            if (state.Stamps[i] != null && !state.Stamps[i]!.Current(holder))
            { routes.Clear(); Array.Clear(state.Stamps, 0, state.Stamps.Length); Invalidations++; }
            state.Stamps[i] ??= new HolderStamp(holder);
            HolderLookups++;
            result = holder.Get(path);
            if (result == null) continue;
            if (routes.Count < MaximumRoutes) routes[path] = i;
            return true;
        }
        // Absence is never stored. The bridge falls through to native resolution,
        // including Resources, bundles, diagnostics and later retry.
        return false;
    }

    internal void Clear()
    {
        routes.Clear(); generation = null;
    }
}
