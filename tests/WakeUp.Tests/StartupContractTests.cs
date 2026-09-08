// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;
using WakeUp;

namespace WakeUp.Tests;

[TestFixture]
public sealed class StartupContractTests
{
    [Test]
    public void SelectionRequiresExplicitModeAndAnIsolatedUnambiguousProfile()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "wake-up-selector"));
        string[] selected = { "--wake-up-mode=candidate", "-savedatafolder=" + root };
        Assert.That(StartupLaunchSelector.Parse(selected).Selection, Is.EqualTo(StartupSelection.Candidate));
        Assert.That(StartupLaunchSelector.Parse(new[] { "--wake-up-mode=baseline", "-savedatafolder=" + root }).Selection, Is.EqualTo(StartupSelection.Baseline));
        foreach (string[] refused in new[] {
            Array.Empty<string>(), new[] { "--wake-up-mode=candidate" },
            new[] { "--wake-up-mode=candidate", "-savedatafolder=relative" },
            new[] { "--wake-up-mode=unknown", "-savedatafolder=" + root },
            new[] { selected[0], selected[1], "--wake-up-mode=baseline" },
            new[] { selected[0], selected[1], selected[1] },
            new[] { selected[0], selected[1], "--wake-up-reference" },
            new[] { selected[0], selected[1], "--wake-up-bypass" },
            new[] { selected[0], "-savedatafolder=\\\\server\\share" } })
            Assert.That(StartupLaunchSelector.Parse(refused).Selection, Is.EqualTo(StartupSelection.Off));
    }

    [Test]
    public void MemoryLoadedHarmonyCanResolveThePhysicalFixtureSupplier()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "wake-up-harmony-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.That(RuntimeIdentity.ResolveLocalPrepatcherHarmonyPath(root), Is.Null);
            string expected = Path.Combine(root, "Mods", "2934420800", "Assemblies", "0Harmony.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(expected)!);
            File.WriteAllBytes(expected, new byte[] { 1 });
            Assert.That(RuntimeIdentity.ResolveLocalPrepatcherHarmonyPath(root), Is.EqualTo(Path.GetFullPath(expected)));
            // Resolving a path does not bypass the separate pinned-byte check.
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Test]
    public void MemoryLoadedLinuxGameUsesLinuxDataFolderAndStillPrefersPublishedLocation()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "wake-up-linux-path-" + Guid.NewGuid().ToString("N"));
        try
        {
            string windows = Path.Combine(root, "RimWorldWin64_Data", "Managed", "Assembly-CSharp.dll");
            string linux = Path.Combine(root, "RimWorldLinux_Data", "Managed", "Assembly-CSharp.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(windows)!);
            File.WriteAllBytes(windows, new byte[] { 1 });
            Assert.That(RuntimeIdentity.ResolveGameAssemblyPath(root, GameBuildContract.LinuxRev600), Is.Null,
                "A Windows file cannot substitute for a missing native Linux game file.");
            Directory.CreateDirectory(Path.GetDirectoryName(linux)!);
            File.WriteAllBytes(linux, new byte[] { 2 });
            Assert.That(RuntimeIdentity.ResolveGameAssemblyPath(root, GameBuildContract.LinuxRev600, "<Unknown>", ""), Is.EqualTo(linux));
            Assert.That(RuntimeIdentity.ResolveGameAssemblyPath(root, GameBuildContract.GogRev573), Is.EqualTo(windows));
            string published = Path.Combine(root, "published.dll");
            File.WriteAllBytes(published, new byte[] { 3 });
            Assert.That(RuntimeIdentity.ResolveGameAssemblyPath(root, GameBuildContract.LinuxRev600, published), Is.EqualTo(published),
                "A published file must be authenticated, not silently replaced by a convenient fallback.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Test]
    public void MemoryLoadedHarmonyResolvesBesideTheSelectedSteamLibrary()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "wake-up-steam-library-" + Guid.NewGuid().ToString("N"));
        try
        {
            string game = Path.Combine(root, "steamapps", "common", "RimWorld");
            string harmony = Path.Combine(root, "steamapps", "workshop", "content", "294100", "2934420800", "Assemblies", "0Harmony.dll");
            Assert.That(RuntimeIdentity.ResolveSameLibraryPrepatcherHarmonyPath(game), Is.Null);
            Directory.CreateDirectory(Path.GetDirectoryName(harmony)!);
            File.WriteAllBytes(harmony, new byte[] { 1 });
            Assert.That(RuntimeIdentity.ResolveSameLibraryPrepatcherHarmonyPath(game + Path.DirectorySeparatorChar), Is.EqualTo(harmony));
            Assert.That(RuntimeIdentity.ResolveSameLibraryPrepatcherHarmonyPath(Path.Combine(root, "other-library", "steamapps", "common", "RimWorld")), Is.Null);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Test]
    public void UnreviewedGameReturnsSpecificReasonBeforeLookingForHarmony()
    {
        var name = new AssemblyName("Assembly-CSharp") { Version = new Version(1, 6, 9999, 1) };
        var module = AppDomain.CurrentDomain.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run).DefineDynamicModule("unreviewed");
        bool resolvedHarmony = false;
        Assert.That(RuntimeIdentity.ValidateBinaryIdentity(module, _ => { resolvedHarmony = true; return null; }, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("game-build-unreviewed"));
        Assert.That(resolvedHarmony, Is.False);
        Assert.That(RuntimeIdentity.DescribeFailure(reason), Does.Contain("RimWorld build has not been reviewed"));
        Assert.That(RuntimeIdentity.DescribeFailure("harmony-file-sha-mismatch"), Does.Contain("Harmony library does not match"));
    }
}
