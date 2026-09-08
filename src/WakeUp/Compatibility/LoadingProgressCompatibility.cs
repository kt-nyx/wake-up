// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Linq;
using System.Reflection;
using Verse;

namespace WakeUp;

// Discover the optional framework. Repaint and PNG validate their own method sets independently.
internal static class LoadingProgressCompatibility
{
    internal const string PackageId = "ilyvion.loadingprogress";
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
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "ilyvion.LoadingProgress");
            FrameworkAssembly = assembly;
            admitted = true;
        }
        catch { admitted = false; }
        return admitted;
    }
}
