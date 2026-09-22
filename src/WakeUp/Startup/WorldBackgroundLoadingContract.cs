// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace WakeUp;

internal static class WorldBackgroundLoadingContract
{
    internal static readonly MethodInfo[] Targets = FindTargets();
    // Independent of save admission: only the new native Entry page boundary,
    // its admission gate, worker and deferred page/redraw callback are added.
    // Queue execution, callback drainage and preferences retain the existing
    // shared contract. No world content generation algorithm is replaced.
    internal static readonly string[] Expected = new[] {
        "4A7954BFDE1866FB07266C660B8770874A6AAB76430A33A0A26AF64D62CDB3F9", // RimWorld.Page:CanDoNext
        "1EC17AAE2EF2C79DBB0C5E7F6ABD1B9ED678E5AAE1A71F7CE3C67A59BBB71607", // Page_CreateWorldParams:CanDoNext
        "495B7264E1D1EE69EFAB2FD47F439123EF457865DA6773A8B602F5D0BDC44435", // CanDoNext world worker
        "F6C1AE693760285BFB99CED162B18643690875973D6B11B87B2CD7EB05A064FE", // CanDoNext page/redraw callback
        "529F534469FB8CBB53D22CBC095EF3FA72F4427CAC3EC54A354C7F7DABEC154F", // Verse.Root_Entry:Update
    };

    private static MethodInfo[] FindTargets()
    {
        var methods = new List<MethodInfo> {
            BackgroundLoadingRuntime.WorldNext,
            AccessTools.Method(typeof(Page), "CanDoNext"),
            AccessTools.Method(typeof(Root_Entry), "Update") };
        AddClosures(methods, typeof(Page_CreateWorldParams));
        return methods.Distinct().OrderBy(BackgroundLoadingContract.Key, StringComparer.Ordinal).ToArray();
    }
    private static void AddClosures(List<MethodInfo> methods, Type type)
    {
        methods.AddRange(type.GetMethods(AccessTools.allDeclared)
            .Where(method => method.Name.StartsWith("<CanDoNext>", StringComparison.Ordinal)));
        foreach (Type nested in type.GetNestedTypes(AccessTools.all)) AddClosures(methods, nested);
    }
    internal static void Validate()
    {
        if (Targets.Length != Expected.Length) throw new InvalidOperationException("Native world-generation functions changed.");
        for (int i = 0; i < Targets.Length; i++)
            if (!SemanticMethodIdentity.TryHash(Targets[i], out string hash, out _) || hash != Expected[i])
                throw new InvalidOperationException("Native world-generation function changed: " + BackgroundLoadingContract.Key(Targets[i]));
    }
}
