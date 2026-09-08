// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Verse;

namespace RimWorldLoadingOptimizer.RimWorld;

// Optional, exact-version framework identity. Unknown frameworks retain original loading.
internal static class LoadingProgressCompatibility
{
    internal const string PackageId = "ilyvion.loadingprogress";
    private const string Prefix = "ilyvion.LoadingProgress.";
    private static bool attempted, admitted;
    internal static Assembly? FrameworkAssembly;
    internal static bool TryInitialize()
    {
        if (attempted)
            return admitted;
        attempted = true;
        try
        {
            var mod = LoadedModManager.RunningModsListForReading.SingleOrDefault(m => m.PackageId == PackageId);
            if (mod == null)
                return false;
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetType(Prefix + "ReloadContentIntReplacement") != null);
            if (assembly.ManifestModule.ModuleVersionId != new Guid("d5765fe2-129d-4df2-a914-5b48feef8562"))
                return false;
            using (var file = File.OpenRead(Path.Combine(mod.RootDir, "1.6", "Assemblies", "ilyvion.LoadingProgress.dll")))
                if (Hash(file) != "4F19F836FD41C8E702295CB90D7D5E66C53F38CDA8A13A3ED79F1D063E1AFCEF")
                    return false;
            FrameworkAssembly = assembly;
            admitted = true;
        }
        catch { admitted = false; }
        return admitted;
    }
    private static string Hash(Stream stream)
    {
        using var hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
    }
}
