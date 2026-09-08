// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.IO;
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
}
