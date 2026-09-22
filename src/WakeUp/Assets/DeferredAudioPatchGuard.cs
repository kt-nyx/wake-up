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

internal sealed class DeferredAudioPatchGuard
{
    private readonly Dictionary<MethodBase, byte[]> state;
    private IEnumerator? stamp;
    private bool allowed;
    internal DeferredAudioPatchGuard()
    {
        var field = typeof(Harmony).Assembly.GetType("HarmonyLib.HarmonySharedState")?.GetField("state", BindingFlags.NonPublic | BindingFlags.Static);
        if (field?.IsInitOnly != true || field.FieldType != typeof(Dictionary<MethodBase, byte[]>))
            throw new InvalidOperationException("unknown Harmony publication state");
        state = (Dictionary<MethodBase, byte[]>)field.GetValue(null);
    }
    internal bool Allows()
    {
        MethodBase[] methods;
        IEnumerator current;
        lock (state)
        {
            if (stamp != null) try { stamp.Reset(); return allowed; } catch (InvalidOperationException) { }
            current = (IEnumerator)state.GetEnumerator();
            methods = state.Keys.Where(Relevant).ToArray();
        }
        bool result = methods.All(method =>
        {
            Patches info = Harmony.GetPatchInfo(method);
            return info == null || !info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers)
                .Concat(info.Finalizers).Concat(info.InnerPrefixes).Concat(info.InnerPostfixes).Any();
        });
        lock (state)
        {
            try { current.Reset(); } catch (InvalidOperationException) { return false; }
            stamp = current; allowed = result; return result;
        }
    }
    internal static bool Relevant(MethodBase method)
    {
        Type? type = method.DeclaringType;
        while (type?.DeclaringType != null) type = type.DeclaringType;
        if (type?.IsGenericType == true) type = type.GetGenericTypeDefinition();
        return type == typeof(ContentFinder<>) || type == typeof(ModContentHolder<>) || type == typeof(ModContentLoader<>)
            || type == typeof(SongDef) || type?.FullName == "RimWorld.MusicManagerPlay" || type?.FullName == "RimWorld.Screen_Credits"
            || type?.FullName == "RuntimeAudioClipLoader.Manager" || type?.FullName == "RuntimeAudioClipLoader.CustomAudioFileReader"
            || type == typeof(ModContentPack) && (method.Name == "GetContentHolder" || method.Name == "ClearDestroy" || method.Name == "GetAllFilesForMod")
            || type == typeof(UnityData) && method.Name == "get_IsInMainThread";
    }
}
