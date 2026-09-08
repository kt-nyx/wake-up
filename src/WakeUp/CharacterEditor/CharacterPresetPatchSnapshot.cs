// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace WakeUp;

// The pinned Harmony binary publishes a fresh serialized byte array whenever
// its patch record changes. Capture once per scope; compare references under
// Harmony's own state lock, without deserializing once per preset.
internal sealed class CharacterPresetPatchSnapshot
{
    private readonly Dictionary<MethodBase, byte[]> state;
    private readonly MethodBase[] methods;
    private readonly byte[]?[] stamps;
    private CharacterPresetPatchSnapshot(Dictionary<MethodBase, byte[]> state, MethodBase[] methods, byte[]?[] stamps)
    {
        this.state = state;
        this.methods = methods;
        this.stamps = stamps;
    }

    internal static bool TryCapture(MethodBase[] methods, string owner, out CharacterPresetPatchSnapshot? snapshot)
    {
        snapshot = null;
        try
        {
            Type? shared = typeof(Harmony).Assembly.GetType("HarmonyLib.HarmonySharedState");
            FieldInfo? field = shared?.GetField("state", BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null || !field.IsInitOnly || field.GetValue(null) is not Dictionary<MethodBase, byte[]> state)
                return false;
            byte[]?[] stamps;
            lock (state)
                stamps = methods.Select(m => state.TryGetValue(m, out byte[] stamp) ? stamp : null).ToArray();
            foreach (MethodBase method in methods)
                if (Harmony.GetPatchInfo(method)?.Owners.Any(id => id != owner) == true)
                    return false;
            var captured = new CharacterPresetPatchSnapshot(state, methods, stamps);
            if (!captured.Unchanged())
                return false;
            snapshot = captured;
            return true;
        }
        catch { return false; }
    }

    internal bool Unchanged()
    {
        lock (state)
            for (int i = 0; i < methods.Length; i++)
                if (!ReferenceEquals(stamps[i], state.TryGetValue(methods[i], out byte[] stamp) ? stamp : null))
                    return false;
        return true;
    }
}
