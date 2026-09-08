// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using Verse;

namespace WakeUp;

// Admission authenticates both the loaded identities and the pinned physical files.
internal static class RuntimeIdentity
{
    private const string HarmonySha = "7B9E756306FA3D7620E02A857C8927A6AB04973F9BD8A77D3866700A6DEAC55C";
    internal static bool ValidateBinaryIdentity(out string reason)
        => ValidateBinaryIdentity(typeof(ModContentPack).Module, ResolveHarmonyFileWithoutModBindings, out reason);

    internal static string DescribeFailure(string reason)
    {
        string detail = reason == "game-build-unreviewed" ? "This RimWorld build has not been reviewed."
            : reason == "game-module-file-unavailable" ? "Wake-Up could not locate the original RimWorld game file."
            : reason.StartsWith("game-", StringComparison.Ordinal) ? "The RimWorld game file does not match its reviewed build."
            : reason == "harmony-file-unavailable" ? "Wake-Up could not locate the Harmony library supplied by Prepatcher."
            : reason.StartsWith("harmony-", StringComparison.Ordinal) ? "This Harmony library does not match the reviewed build."
            : "Wake-Up could not verify the game and Harmony files.";
        return detail + " Ordinary loading is preserved.";
    }

    internal static bool ValidateBinaryIdentity(Module module, Func<Assembly, string?> harmonyResolver, out string reason)
    {
        reason = "runtime-identity-mismatch";
        try
        {
            AssemblyName game = module.Assembly.GetName();
            if (!string.Equals(game.Name, "Assembly-CSharp", StringComparison.Ordinal))
            {
                reason = "game-assembly-name-mismatch";
                return false;
            }
            GameBuildContract? build = GameBuildContract.Select(game, module.ModuleVersionId);
            if (build is null)
            {
                reason = "game-build-unreviewed";
                return false;
            }
            string? gameAssemblyPath = ResolveGameAssemblyPath(module, build);
            if (gameAssemblyPath is null)
            {
                reason = "game-module-file-unavailable";
                return false;
            }
            if (!string.Equals(HashFile(gameAssemblyPath), build.Sha256, StringComparison.Ordinal))
            {
                reason = "game-module-sha-mismatch";
                return false;
            }
            Assembly harmony = typeof(Harmony).Assembly;
            if (!string.Equals(harmony.GetName().Version?.ToString(), "2.4.2.0", StringComparison.Ordinal))
            {
                reason = "harmony-assembly-version-mismatch";
                return false;
            }
            if (harmony.ManifestModule.ModuleVersionId != new Guid("024a0e6e-c8c2-437e-ad04-7b6279389c23"))
            {
                reason = "harmony-module-mvid-mismatch";
                return false;
            }
            string? harmonyPath = harmonyResolver(harmony);
            if (harmonyPath is null)
            {
                reason = "harmony-file-unavailable";
                return false;
            }
            if (!string.Equals(HashFile(harmonyPath), HarmonySha, StringComparison.Ordinal))
            {
                reason = "harmony-file-sha-mismatch";
                return false;
            }
            reason = "binary-identity-exact";
            return true;
        }
        catch { return false; }
    }

    private static string? ResolveGameAssemblyPath(Module module, GameBuildContract build)
    {
        string? modulePath = null, assemblyPath = null;
        try { modulePath = module.FullyQualifiedName; } catch { }
        try { assemblyPath = module.Assembly.Location; } catch { }
        return ResolveGameAssemblyPath(AppDomain.CurrentDomain.BaseDirectory, build, modulePath, assemblyPath);
    }

    // Prepatcher may load bytes without a physical assembly Location. Select the
    // platform's original game file; the caller still authenticates its full hash.
    internal static string? ResolveGameAssemblyPath(string gameRoot, GameBuildContract build,
        string? modulePath = null, string? assemblyPath = null)
    {
        string?[] candidates = { modulePath, assemblyPath,
            Path.Combine(gameRoot, build.DataDirectoryName, "Managed", "Assembly-CSharp.dll") };
        foreach (string? candidate in candidates)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                    return Path.GetFullPath(candidate);
            }
            catch { }
        }
        return null;
    }
    private static string? ResolveHarmonyFileWithoutModBindings(Assembly harmony)
    {
        if (!string.IsNullOrWhiteSpace(harmony.Location) && File.Exists(harmony.Location))
            return Path.GetFullPath(harmony.Location);
        string? codeBasePath = ResolveAssemblyCodeBasePath(harmony);
        if (codeBasePath is not null)
            return codeBasePath;
        string? localGamePath = ResolveLocalPrepatcherHarmonyPath(AppDomain.CurrentDomain.BaseDirectory);
        if (localGamePath is not null)
            return localGamePath;
        string? sameLibraryWorkshopPath = ResolveSameLibraryPrepatcherHarmonyPath();
        if (sameLibraryWorkshopPath is not null)
            return sameLibraryWorkshopPath;
        return null;
    }

    internal static string? ResolveLocalPrepatcherHarmonyPath(string gameRoot)
    {
        try
        {
            string path = Path.GetFullPath(Path.Combine(gameRoot, "Mods", "2934420800", "Assemblies", "0Harmony.dll"));
            return File.Exists(path) ? path : null;
        }
        catch { return null; }
    }

    private static string? ResolveSameLibraryPrepatcherHarmonyPath()
        => ResolveSameLibraryPrepatcherHarmonyPath(AppDomain.CurrentDomain.BaseDirectory);

    internal static string? ResolveSameLibraryPrepatcherHarmonyPath(string gameRootPath)
    {
        try
        {
            var gameRoot = new DirectoryInfo(Path.GetFullPath(gameRootPath));
            DirectoryInfo? common = gameRoot.Parent;
            DirectoryInfo? steamApps = common?.Parent;
            if (common is null || steamApps is null
                || !string.Equals(common.Name, "common", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(steamApps.Name, "steamapps", StringComparison.OrdinalIgnoreCase))
                return null;
            string path = Path.GetFullPath(Path.Combine(
                steamApps.FullName, "workshop", "content", "294100", "2934420800", "Assemblies", "0Harmony.dll"));
            return File.Exists(path) ? path : null;
        }
        catch { return null; }
    }
    private static string? ResolveAssemblyCodeBasePath(Assembly assembly)
    {
        try
        {
            string? codeBase = assembly.CodeBase;
            if (string.IsNullOrWhiteSpace(codeBase)
                || !Uri.TryCreate(codeBase, UriKind.Absolute, out Uri? uri)
                || !uri.IsFile)
                return null;
            string path = Path.GetFullPath(uri.LocalPath);
            return File.Exists(path) ? path : null;
        }
        catch { return null; }
    }
    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using SHA256 hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
    }
}
