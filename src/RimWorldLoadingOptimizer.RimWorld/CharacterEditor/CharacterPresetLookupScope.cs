// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Threading;
using Verse;

namespace RimWorldLoadingOptimizer.RimWorld;

// One synchronous original CreateDefaultObjects/CreateDefaultTurrets invocation.
// Only GetTurretDef's read-only dictionary callsite can see this dictionary;
// the public Character Editor property still creates a fresh dictionary.
internal sealed class CharacterPresetLookupScope : IDisposable
{
    internal const int MaximumEntries = 4096;
    private readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
    private readonly Func<Dictionary<string, ThingDef>> original;
    private readonly Func<bool> unchanged;
    private Dictionary<string, ThingDef>? cached;
    private bool disabled;
    internal int Builds { get; private set; }
    internal int Hits { get; private set; }
    internal int Fallbacks { get; private set; }
    internal int RetainedEntries => cached?.Count ?? 0;

    internal CharacterPresetLookupScope(Func<Dictionary<string, ThingDef>> original, Func<bool> unchanged)
    { this.original = original; this.unchanged = unchanged; }

    internal Dictionary<string, ThingDef> Get()
    {
        // Cross-thread callers neither see nor mutate the owner's cached object.
        if (Thread.CurrentThread.ManagedThreadId != ownerThread) return original();
        bool valid = false;
        if (!disabled)
            try { valid = unchanged(); } catch { }
        if (!valid)
        {
            disabled = true;
            cached = null;
            Fallbacks++;
            return original();
        }
        if (cached != null) { Hits++; return cached; }
        Builds++;
        // Exceptions propagate exactly once through the original callback.
        Dictionary<string, ThingDef> result = original();
        if (result.Count <= MaximumEntries) cached = result;
        else { disabled = true; Fallbacks++; }
        return result;
    }

    public void Dispose()
    {
        // Drop our reference, never Clear() an object the current call used.
        cached = null;
        disabled = true;
    }
}
