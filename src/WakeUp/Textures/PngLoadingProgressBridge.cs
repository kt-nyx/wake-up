// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using HarmonyLib;

namespace WakeUp;

// Loading Progress replaces the outer content loader with a yielding iterator.
// Keep that iterator and its progress/profiler stages; replace only its texture
// holder call with the same guarded helper used by the native loader.
internal sealed class PngLoadingProgressBridge
{
    internal readonly MethodInfo Iterator;
    internal readonly MethodInfo[] ReviewedMethods;
    private readonly HashSet<MethodInfo> reloadPrefixes;
    private readonly List<PublishedPatchGuard> guards = new();

    internal PngLoadingProgressBridge(Assembly assembly)
    {
        const string prefix = "ilyvion.LoadingProgress.";
        MethodInfo factory = AccessTools.Method(assembly.GetType(prefix + "ReloadContentIntReplacement", true), "ReloadContentInt");
        var iterator = (IteratorStateMachineAttribute)factory.GetCustomAttributes(typeof(IteratorStateMachineAttribute), false).Single();
        Iterator = AccessTools.Method(iterator.StateMachineType, "MoveNext");
        MethodInfo tracker = AccessTools.Method(assembly.GetType(prefix + "ModContentPack_LoadingDataTracker_Patches", true), "ReloadContentIntPrefix");
        Type patch = assembly.GetType(prefix + "ModContentPack_ReloadContentInt_Patch", true)!;
        MethodInfo progress = AccessTools.Method(patch, "ProgressPrefix"), skipOriginal = AccessTools.Method(patch, "Prefix");
        ReviewedMethods = new[] { factory, Iterator, tracker, progress, skipOriginal };
        string[] expected = {
            "5A0B620EFD31369BCA279D190A1BB45E41D1627F44713A0EBB7904533F5CCEE8",
            "2AD8B5CA9C885C0EDF807EE60DB65E8C018D3D41D604A09975F2BF00ADE341BC",
            "62F6ADD0ECC1FBDA039EDCD4D3AE01099743F2413B25072D9D05AC7065793C5D",
            "1E8037693D63DEF16F182F964745BAA6C167134F1C126B7ABFBD22F178F87C8E",
            "60323971A473C6041B288EC3BDE1B3E3563F97E41EDAABC16454BED972854379"
        };
        // Physical hash/MVID are authenticated by LoadingProgressCompatibility
        // before construction. These checks also detect rewritten loaded bodies.
        using var sha = SHA256.Create();
        for (int i = 0; i < ReviewedMethods.Length; i++)
        {
            MethodInfo method = ReviewedMethods[i];
            if (BitConverter.ToString(sha.ComputeHash(method.GetMethodBody()!.GetILAsByteArray())).Replace("-", "") != expected[i])
                throw new InvalidOperationException("loading-progress-body-" + method.Name);
            if (!PublishedPatchGuard.TryCreate(method, PngRuntime.Owner, out var guard, allPatchKinds: true)
                || !guard!.AllowsOriginalContract())
                throw new InvalidOperationException("loading-progress-hook-" + method.Name);
            guards.Add(guard);
        }
        reloadPrefixes = new HashSet<MethodInfo> { tracker, progress, skipOriginal };
    }

    internal bool AllowsReloadPatch(Patch patch) => patch.owner == LoadingProgressCompatibility.PackageId
        && reloadPrefixes.Contains(patch.PatchMethod);
    internal bool Compatible() => guards.All(g => g.AllowsOriginalContract());
}
