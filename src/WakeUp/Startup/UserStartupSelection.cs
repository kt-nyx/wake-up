// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Linq;

namespace WakeUp;

// Settings select the existing independently guarded implementations. Explicit
// developer arguments retain their exact semantics, including malformed/off
// selections: they must never silently fall through to normal activation.
internal static class UserStartupSelection
{
    internal static bool HasExplicitSelection(IReadOnlyList<string> arguments)
        => arguments.Any(a => a != null && a.StartsWith("--wake-up-", StringComparison.Ordinal));

    internal static string[] Resolve(IReadOnlyList<string> arguments, WakeUpSettings settings, string saveDataRoot)
    {
        if (HasExplicitSelection(arguments))
            return arguments.ToArray();
        if (!settings.Enabled)
            return Array.Empty<string>();
        var selected = new List<string> { "--wake-up-mode=candidate", "-savedatafolder=" + saveDataRoot };
        // Optional features are independent of whether either search is selected.
        // StartupSearchRuntime applies the two opt-outs only to its own children.
        selected.Add("--wake-up-strategy=startup-searches");
        if (!settings.DefinitionSearches)
            selected.Add("--wake-up-user-def-search=off");
        if (!settings.TypeSearches)
            selected.Add("--wake-up-user-type-search=off");
        if (settings.GagarinReuse)
            selected.Add("--wake-up-gagarin-cache=on");
        if (settings.CharacterPresets)
            selected.Add("--wake-up-character-presets=on");
        if (settings.LoadingProgress)
            selected.Add("--wake-up-loading-progress=coalesce");
        if (settings.GiddyTextures)
            selected.Add("--wake-up-giddy-textures=on");
        if (settings.PngCache)
            selected.Add("--wake-up-png=cache");
        return selected.ToArray();
    }
}
