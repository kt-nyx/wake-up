// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WakeUp;

// A short-lived list of actual active load folders, not a cache of mask absence.
// Each admission probes only the relevant sibling basename in those folders.
// Adding a file cannot hide behind an old directory timestamp or provider map.
internal sealed class PreparedTextureEligibility
{
    internal const string Contract = "prepared-role-v2";
    private readonly string[] folders;
    internal readonly string ActiveIdentity;
    internal PreparedTextureEligibility(IEnumerable<string> activeFolders, string activeIdentity)
    {
        folders = activeFolders.Select(Path.GetFullPath).Distinct(StringComparer.Ordinal).ToArray();
        if (folders.Length > 4096) throw new InvalidDataException("eligibility-folder-limit");
        ActiveIdentity = activeIdentity;
    }
    internal static PreparedTextureEligibility Capture() => new(
        Verse.LoadedModManager.RunningModsListForReading.SelectMany(m => m.foldersToLoadDescendingOrder),
        PreparedTextureRuntime.ActiveIdentity());

    internal bool NoCurrentMask(string logical)
    {
        try
        {
            string relative = logical.Replace('\\', '/');
            if (!relative.StartsWith("Textures/", StringComparison.Ordinal) || relative.Split('/').Any(p => p == ".." || p == ".")
                || relative.IndexOf(':') >= 0 || relative.IndexOfAny(new[] { '*', '?' }) >= 0) return false;
            string stem = Path.GetFileNameWithoutExtension(relative);
            if (stem.EndsWith("_m", StringComparison.OrdinalIgnoreCase)) return false;
            string directory = Path.GetDirectoryName(relative)!;
            foreach (string folder in folders)
            {
                try
                {
                    // A top-level basename probe also sees uppercase extensions,
                    // newly created directories and masks in earlier providers.
                    // No GetAllFilesForMod / recursive mod scan on this path.
                    int count = 0;
                    foreach (string file in Directory.EnumerateFiles(Path.Combine(folder, directory), stem + "_m.*", SearchOption.TopDirectoryOnly))
                    {
                        if (++count > 128) return false;
                        if (Verse.ModContentLoader<UnityEngine.Texture2D>.IsAcceptableExtension(Path.GetExtension(file))) return false;
                    }
                }
                catch (DirectoryNotFoundException) { } // absent sibling directory has no mask
            }
            return true;
        }
        catch (Exception) { return false; } // uncertain/access-denied is not absence
    }
    internal bool Allows(string logical, Func<string, bool> knownColorRole)
        => knownColorRole(logical) && NoCurrentMask(logical);

    // Both boundaries share this admission. Pure store operations are also
    // exercisable offline without invoking Unity texture construction.
    internal PngCache.Entry? Read(PreparedTextureStore store, string identity, string logical, Func<string, bool> knownColorRole)
        => Allows(logical, knownColorRole) ? store.Read(identity) : null;
    internal bool Publish(PreparedTextureStore store, string identity, string logical, PngCache.Entry entry, Func<string, bool> knownColorRole)
        => Allows(logical, knownColorRole) && store.Publish(identity, entry);
}
