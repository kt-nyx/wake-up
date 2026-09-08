// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;

namespace WakeUp;

// The pinned Harmony implementation
// serializes into a NEW byte[] for every publication and never changes those
// bytes in place. Their reference is a cheap version stamp; GetPatchInfo itself
// deserializes the entire record and must not run on every lookup.
internal sealed class PublishedPatchGuard
{
    private readonly Dictionary<MethodBase, byte[]> state;
    private readonly MethodBase target;
    private readonly string owner;
    private readonly bool allPatchKinds;
    private readonly Func<Patch, bool>? allowedForeignPatch;
    private PublishedDecision? decision;
    private int patchInfoReads;
    internal int PatchInfoReads => Volatile.Read(ref patchInfoReads);

    private sealed class PublishedDecision
    {
        internal readonly byte[]? Stamp;
        internal readonly bool Allowed;
        internal PublishedDecision(byte[]? stamp, bool allowed)
        {
            Stamp = stamp;
            Allowed = allowed;
        }
    }

    private PublishedPatchGuard(Dictionary<MethodBase, byte[]> state, MethodBase target, string owner, bool allPatchKinds, Func<Patch, bool>? allowedForeignPatch)
    {
        this.state = state;
        this.target = target;
        this.owner = owner;
        this.allPatchKinds = allPatchKinds;
        this.allowedForeignPatch = allowedForeignPatch;
    }

    // Production calls this only after the existing pinned Harmony binary gate.
    internal static bool TryCreate(MethodBase target, string owner, out PublishedPatchGuard? guard, bool allPatchKinds = false, Func<Patch, bool>? allowedForeignPatch = null)
    {
        guard = null;
        try
        {
            Type? shared = typeof(Harmony).Assembly.GetType("HarmonyLib.HarmonySharedState");
            FieldInfo? field = shared?.GetField("state", BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null || !field.IsInitOnly || field.FieldType != typeof(Dictionary<MethodBase, byte[]>))
                return false;
            if (!(field.GetValue(null) is Dictionary<MethodBase, byte[]> state))
                return false;
            guard = new PublishedPatchGuard(state, target, owner, allPatchKinds, allowedForeignPatch);
            return true;
        }
        catch { return false; }
    }

    internal bool AllowsOriginalContract()
    {
        try
        {
            byte[]? stamp = ReadStamp();
            PublishedDecision? cached = Volatile.Read(ref decision);
            if (cached != null && ReferenceEquals(stamp, cached.Stamp))
                return cached.Allowed;
            Interlocked.Increment(ref patchInfoReads);
            // Do not hold state here: public GetPatchInfo first takes Harmony's
            // patch-processor lock, then state, and protects its deserializer.
            Patches? patches = Harmony.GetPatchInfo(target);
            bool compatible = allPatchKinds ? patches == null || !patches.Prefixes.Concat(patches.Postfixes).Concat(patches.Transpilers).Concat(patches.Finalizers)
                    .Any(p => p.owner != owner && allowedForeignPatch?.Invoke(p) != true)
                : patches?.Transpilers.Any(p => p.owner != owner) != true;
            if (!ReferenceEquals(stamp, ReadStamp()))
                return false;
            // A single immutable publication also supports lookup callers
            // on multiple threads. Never hold our own lock across GetPatchInfo:
            // Harmony may itself invoke a patched lookup while holding its lock.
            Volatile.Write(ref decision, new PublishedDecision(stamp, compatible));
            return compatible;
        }
        catch { return false; }
    }

    private byte[]? ReadStamp()
    {
        // Use the same lock as Harmony's publication; Dictionary does not allow
        // a concurrent unlocked reader while another thread replaces an entry.
        lock (state)
            return state.TryGetValue(target, out byte[] value) ? value : null;
    }
}
