// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Reflection;

namespace WakeUp;

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
    internal static readonly GameBuildContract LinuxRev600 = new(
        "linux-rev600", "1.6.9676.18020", "b4d967f0-d45a-413f-bb02-23eefee4f2ae",
        "082DB1DD4F7F1D0B72960D7E1BEEAD8FBFE6957200E8627F65BDA0DBBE1DD8F8");

    // The reviewed Steam Windows loading/texture methods match GOG; Linux's
    // loading methods match too, with its separate PNG LoadItem fingerprint.
    // See windows-steam-support.md and linux-support.md for exact comparisons.
    internal bool HasReviewedLoadingMethods => ReferenceEquals(this, GogRev573)
        || ReferenceEquals(this, SteamRev590) || ReferenceEquals(this, LinuxRev600);
    internal bool IsLinux => ReferenceEquals(this, LinuxRev600);
    internal string DataDirectoryName => IsLinux ? "RimWorldLinux_Data" : "RimWorldWin64_Data";

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
        foreach (GameBuildContract build in new[] { SteamRev590, GogRev573, LinuxRev600 })
            if (name.Version?.ToString() == build.Version && mvid == build.Mvid)
                return build;
        return null;
    }

}
