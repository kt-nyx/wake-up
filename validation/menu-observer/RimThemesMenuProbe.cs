// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace FixtureMenuObserver;

// Functional-only observer for this supplier's complete menu replacement.
// The ordinary native menu/timing observer retains its original execution check.
internal static class RimThemesMenuProbe
{
    private static int completedFrame = -1;
    internal static bool Completed => completedFrame == Time.frameCount;
    internal static void Install(Harmony harmony)
    {
        var mod = LoadedModManager.RunningModsListForReading.Single(m => m.PackageId.Equals("arandomkiwi.rimthemes", StringComparison.OrdinalIgnoreCase));
        var assembly = mod.assemblies.loadedAssemblies.Single(a => a.GetType("aRandomKiwi.RimThemes.MainMenuOnGUI_Patch") != null);
        bool known = ModContentPack.GetAllFilesForMod(mod, "Assemblies", e => e.Equals(".dll", StringComparison.OrdinalIgnoreCase)).Values.Any(f => {
            using var stream = File.OpenRead(f.FullName);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() == "f89b8d7858d50249f7016be40682bc78a227e51eb922d0083fc3617504d9ee01";
        });
        MethodInfo method = AccessTools.Method(assembly.GetType("aRandomKiwi.RimThemes.MainMenuOnGUI_Patch"), "Prefix");
        if (!known || method == null || method.ReturnType != typeof(bool) || !method.IsStatic
            || method.GetParameters().Length != 6 || Harmony.GetPatchInfo(method) != null)
            throw new InvalidOperationException("Unqualified RimThemes menu replacement observer");
        harmony.Patch(method, postfix: new HarmonyMethod(typeof(RimThemesMenuProbe), nameof(CompletedPrefix)) { priority = Priority.Last });
    }
    private static void CompletedPrefix(bool __runOriginal, bool __result)
    {
        if (__runOriginal && !__result && Event.current?.type == EventType.Repaint)
            completedFrame = Time.frameCount;
    }
}
