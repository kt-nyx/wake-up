// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;

namespace WakeUp;

// Main-thread owner. A failed attempt stays pending, while successful ownership
// transfers to the native holder only after its publication callback completes.
internal sealed class DeferredAssetSet<TSource, TValue> where TValue : class
{
    private sealed class Entry
    {
        internal readonly TSource Source;
        internal bool Loading;
        internal Entry(TSource source) { Source = source; }
    }
    private readonly Dictionary<string, Entry> pending = new(StringComparer.Ordinal);
    private int generation;
    internal int Count => pending.Count;
    internal string[] Keys => pending.Keys.ToArray();
    internal bool Add(string key, TSource source, int limit)
    {
        if (pending.ContainsKey(key)) return true;
        if (pending.Count >= limit) return false;
        pending.Add(key, new Entry(source)); return true;
    }
    internal bool Contains(string key) => pending.ContainsKey(key);
    internal void Cancel() { generation++; pending.Clear(); }
    internal bool Complete(string key, Func<TSource, TValue?> load, Func<string, TValue, bool> publish, Action<TValue> discard)
    {
        if (!pending.TryGetValue(key, out Entry entry) || entry.Loading) return false;
        int started = generation;
        entry.Loading = true;
        TValue? result = null;
        try
        {
            result = load(entry.Source);
            if (result == null) return false;
            if (started != generation || !pending.TryGetValue(key, out Entry current) || !ReferenceEquals(current, entry))
                return false;
            if (!publish(key, result)) return false;
            result = null; // Native owner now owns both the object and its stream.
            pending.Remove(key);
            return true;
        }
        finally
        {
            entry.Loading = false;
            if (result != null) discard(result);
        }
    }
}
