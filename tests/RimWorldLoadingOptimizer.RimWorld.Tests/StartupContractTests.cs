// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.IO;
using NUnit.Framework;
using RimWorldLoadingOptimizer.RimWorld;

namespace RimWorldLoadingOptimizer.RimWorld.Tests;

[TestFixture]
public sealed class StartupContractTests
{
    [Test]
    public void SelectionRequiresExplicitModeAndAnIsolatedUnambiguousProfile()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "rlo-selector"));
        string[] selected = { "--rlo-op7=candidate", "-savedatafolder=" + root };
        Assert.That(StartupLaunchSelector.Parse(selected).Selection, Is.EqualTo(StartupSelection.Candidate));
        Assert.That(StartupLaunchSelector.Parse(new[] { "--rlo-op7=baseline", "-savedatafolder=" + root }).Selection, Is.EqualTo(StartupSelection.Baseline));
        foreach (string[] refused in new[] {
            Array.Empty<string>(), new[] { "--rlo-op7=candidate" },
            new[] { "--rlo-op7=candidate", "-savedatafolder=relative" },
            new[] { "--rlo-op7=unknown", "-savedatafolder=" + root },
            new[] { selected[0], selected[1], "--rlo-op7=baseline" },
            new[] { selected[0], selected[1], selected[1] },
            new[] { selected[0], selected[1], "--rlo-reference" },
            new[] { selected[0], selected[1], "--rlo-bypass" },
            new[] { selected[0], "-savedatafolder=\\\\server\\share" } })
            Assert.That(StartupLaunchSelector.Parse(refused).Selection, Is.EqualTo(StartupSelection.Off));
    }

    [Test]
    public void MemoryLoadedHarmonyCanResolveThePhysicalFixtureSupplier()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "rlo-harmony-" + Guid.NewGuid().ToString("N"));
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
}
