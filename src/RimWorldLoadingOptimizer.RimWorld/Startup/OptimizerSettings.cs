// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWorldLoadingOptimizer.RimWorld;

public sealed class OptimizerSettings : ModSettings
{
    public bool Enabled = true;
    public bool DefinitionSearches = true;
    public bool TypeSearches = true;
    public bool GagarinReuse = true;
    public bool CharacterPresets = true;
    public bool LoadingProgress = true;
    public bool GiddyTextures = true;
    public bool PngCache;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref Enabled, "enabled", true);
        Scribe_Values.Look(ref DefinitionSearches, "definitionSearches", true);
        Scribe_Values.Look(ref TypeSearches, "typeSearches", true);
        Scribe_Values.Look(ref GagarinReuse, "gagarinReuse", true);
        Scribe_Values.Look(ref CharacterPresets, "characterPresets", true);
        Scribe_Values.Look(ref LoadingProgress, "loadingProgress", true);
        Scribe_Values.Look(ref GiddyTextures, "giddyTextures", true);
        Scribe_Values.Look(ref PngCache, "pngCache", false);
    }
}

// Settings select the existing independently guarded implementations. Explicit
// developer arguments retain their exact semantics, including malformed/off
// selections: they must never silently fall through to normal activation.
internal static class UserStartupSelection
{
    internal static bool HasExplicitSelection(IReadOnlyList<string> arguments)
        => arguments.Any(a => a != null && a.StartsWith("--rlo-", StringComparison.Ordinal));

    internal static string[] Resolve(IReadOnlyList<string> arguments, OptimizerSettings settings, string saveDataRoot)
    {
        if (HasExplicitSelection(arguments)) return arguments.ToArray();
        if (!settings.Enabled) return Array.Empty<string>();
        var selected = new List<string> { "--rlo-op7=candidate", "-savedatafolder=" + saveDataRoot };
        // Optional features are independent of whether either search is selected.
        // StartupSearchRuntime applies the two opt-outs only to its own children.
        selected.Add("--rlo-strategy=startup-searches");
        if (!settings.DefinitionSearches) selected.Add("--rlo-user-def-search=off");
        if (!settings.TypeSearches) selected.Add("--rlo-user-type-search=off");
        if (settings.GagarinReuse) selected.Add("--rlo-gagarin-cache=on");
        if (settings.CharacterPresets) selected.Add("--rlo-character-presets=on");
        if (settings.LoadingProgress) selected.Add("--rlo-loading-progress=coalesce");
        if (settings.GiddyTextures) selected.Add("--rlo-giddy-textures=on");
        if (settings.PngCache) selected.Add("--rlo-png=cache");
        return selected.ToArray();
    }
}
