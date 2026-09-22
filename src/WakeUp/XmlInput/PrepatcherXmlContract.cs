// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace WakeUp;

// The inspected callbacks only bracket native XML conversion with a private
// boolean. Conversion and both callbacks still run once, including cleanup on
// exceptions. Neither callback receives or retains document/node references.
internal static class PrepatcherXmlContract
{
    internal const string Identity = "4936A27ED4F8C960A574B4CB5B1A9D8B425E78D48814C3ABAD083E9E3D340B9D|FA8050178772B5B4C97BA54A9C58C908686571D2131AE769311BA1720D5D0E32";
    private static readonly Dictionary<MethodInfo, PublishedPatchGuard> Guards = new();
    internal static bool AllowsCallbacks() => Guards.Values.All(g => g.AllowsOriginalContract());

    // During LoadModXML the exact native labels are "Loading " + mod,
    // "Load defs via DirectXmlLoader" and "Parse loaded defs". None matches
    // this callback's six stage-changing labels, so it has no effects here.
    internal static bool AllowsInputPatch(MethodBase target, Patch patch)
    {
        MethodInfo method = patch.PatchMethod;
        bool profiler = target.DeclaringType == typeof(DeepProfiler) && target.Name == "Start" && method.Name == "InitAllMetadataPrefix";
        bool error = target.DeclaringType == typeof(Log) && target.Name == "Error" && method.Name == "LogErrorPrefix";
        bool warning = target.DeclaringType == typeof(Log) && target.Name == "Warning" && method.Name == "LogWarningPrefix";
        if ((!profiler && !error && !warning) || patch.owner != "prepatcher"
            || method.DeclaringType?.FullName != "Prepatcher.HarmonyPatches" || !method.IsStatic
            || method.ReturnType != (profiler ? typeof(void) : typeof(bool))
            || method.GetParameters().Length != 1 || method.GetParameters()[0].ParameterType != typeof(string)) return false;
        string expected = profiler ? "F3BF134E039AE4B883C0E1BC982A4F5B11E927C830D12125416CC11EBECDADD4"
            : error ? "3992040EE86732E765EC1D3BCA9A8FCFF384EB88BFDEE8791BA55A39B97DE136"
            : "17F8373EFA304B60C569E1BD6FABB2ECAB1E17F16AD5B5CC993FEA5BBAFBC8D8";
        if (!Guards.TryGetValue(method, out var guard))
        {
            if (!SemanticMethodIdentity.TryHash(method, out string hash, out _)
                || hash != expected
                || !PublishedPatchGuard.TryCreate(method, "wakeup.xml-callback-contract", out guard, allPatchKinds: true)) return false;
            Guards.Add(method, guard!);
        }
        return guard!.AllowsOriginalContract();
    }

    internal static bool AllowsPatch(MethodBase target, Patch patch)
    {
        MethodInfo method = patch.PatchMethod;
        if (target.DeclaringType != typeof(LoadedModManager) || target.Name != "ParseAndProcessXML"
            || patch.owner != "prepatcher" || method.DeclaringType?.FullName != "Prepatcher.HarmonyPatches"
            || !method.IsStatic || method.ReturnType != typeof(void) || method.GetParameters().Length != 0) return false;
        string? expected = method.Name == "PrefixParseXML" ? Identity.Split('|')[0]
            : method.Name == "FinalizeParseXML" ? Identity.Split('|')[1] : null;
        if (expected == null) return false;
        if (!Guards.TryGetValue(method, out var guard))
        {
            if (!SemanticMethodIdentity.TryHash(method, out string hash, out _) || hash != expected
                || !PublishedPatchGuard.TryCreate(method, "wakeup.xml-callback-contract", out guard, allPatchKinds: true)) return false;
            Guards.Add(method, guard!);
        }
        return guard!.AllowsOriginalContract();
    }
}
