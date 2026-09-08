// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Reflection;

namespace RimWorldLoadingOptimizer.RimWorld;

// Distribution is not authority. Only one of these reviewed assembly identities
// selects a contract; runtime admission also authenticates the physical bytes.
internal sealed class GameBuildContract
{
    internal static readonly GameBuildContract SteamRev590 = new(
        "steam-rev590", "1.6.9676.17735", "61e41735-6189-4da4-9d21-0260257b5097",
        "5CF1B5BE399D5B1C9C56CA72C9D35B4ECF307FEACF5859D04AC5A1AA5926356A");
    internal static readonly GameBuildContract GogRev573 = new(
        "gog-rev573", "1.6.9676.17238", "13bee51f-e6fa-4214-a4a5-e1e7b84a41ee",
        "4A170804FBFEFABDB620D8914E584E58F822A58C6E304DCB76A67003588DAB28");

    private GameBuildContract(string target, string version, string mvid, string sha256)
    {
        Target = target;
        Version = version;
        Mvid = new Guid(mvid);
        Sha256 = sha256;
    }

    internal string Target
    {
        get;
    }
    internal string Version
    {
        get;
    }
    internal Guid Mvid
    {
        get;
    }
    internal string Sha256
    {
        get;
    }
    internal static GameBuildContract Current => For(typeof(Verse.Root).Assembly.ManifestModule);

    internal static GameBuildContract For(Module module) => Select(module.Assembly.GetName(), module.ModuleVersionId)
        ?? throw new InvalidOperationException("Unreviewed game build.");

    internal static GameBuildContract? Select(AssemblyName name, Guid mvid)
    {
        if (!string.Equals(name.Name, "Assembly-CSharp", StringComparison.Ordinal))
            return null;
        foreach (GameBuildContract build in new[] { SteamRev590, GogRev573 })
            if (name.Version?.ToString() == build.Version && mvid == build.Mvid)
                return build;
        return null;
    }

}
