// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
        var methods = SupplierMethodContract.ResolveAll(assembly, SupplierContracts.PngBridge);
        MethodInfo factory = (MethodInfo)methods["Factory"];
        Iterator = (MethodInfo)methods["Iterator"];
        var iterator = (IteratorStateMachineAttribute)factory.GetCustomAttributes(typeof(IteratorStateMachineAttribute), false).Single();
        if (iterator.StateMachineType != Iterator.DeclaringType)
            throw new InvalidOperationException("changed-loading-progress-iterator");
        MethodInfo tracker = (MethodInfo)methods["Tracker"], progress = (MethodInfo)methods["Progress"], skipOriginal = (MethodInfo)methods["Skip"];
        ReviewedMethods = new[] { factory, Iterator, tracker, progress, skipOriginal };
        foreach (MethodInfo method in ReviewedMethods)
        {
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
