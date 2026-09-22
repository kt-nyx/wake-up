// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;

namespace WakeUp;

// One native GUI-art consumer, independent of Def loading and mod package IDs.
// Directory names, alpha and user consent cannot establish other texture roles.
internal static class PreparedColorRole
{
    internal const string Identity = "native-bgplanet-gui-v1";
    internal static readonly MethodBase[] Targets = {
        typeof(UI_BackgroundMain).TypeInitializer!,
        AccessTools.Method(typeof(UI_BackgroundMain), nameof(UI_BackgroundMain.BackgroundOnGUI))
    };
    internal static readonly string[] Bodies = { "D4C5894F267CAEA87628058F4961AEB5E8D0D3D407B6D83B917DCA938754B6C4",
        "BF86C34020E278BC6E4B6D391B74E159BBFED3442F34E512CF6967860405EC42" };
    private static List<PublishedPatchGuard>? guards;
    internal static bool MatchesPath(string logical) => string.Equals(
        Path.ChangeExtension(logical.Replace('\\', '/'), null), "Textures/UI/HeroArt/BGPlanet", StringComparison.Ordinal);
    internal static bool Allows(string logical)
    {
        if (!MatchesPath(logical)) return false;
        try
        {
            if (guards == null)
            {
                if (!RuntimeIdentity.ValidateBinaryIdentity(out _)) return false;
                var checkedGuards = new List<PublishedPatchGuard>();
                for (int i = 0; i < Targets.Length; i++)
                {
                    if (!SemanticMethodIdentity.TryHash(Targets[i], out string body, out _) || body != Bodies[i]
                        || !PublishedPatchGuard.TryCreate(Targets[i], PngRuntime.Owner, out var guard, true)) return false;
                    checkedGuards.Add(guard!);
                }
                guards = checkedGuards;
            }
            return guards.All(g => g.AllowsOriginalContract());
        }
        catch { return false; }
    }
}
