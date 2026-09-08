// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.IO;
using NUnit.Framework;

namespace RimWorldLoadingOptimizer.RimWorld.Tests;

[TestFixture]
public sealed class UserStartupSelectionTests
{
    private static string Profile => Path.GetFullPath(Path.Combine(Path.GetTempPath(), "rlo-user-profile"));

    [Test]
    public void OrdinaryLaunchEnablesSupportedFeaturesWithoutEnablingPersistentTextureWrites()
    {
        var args = UserStartupSelection.Resolve(Array.Empty<string>(), new OptimizerSettings(), Profile);
        Assert.That(StartupLaunchSelector.Parse(args).Selection, Is.EqualTo(StartupSelection.Candidate));
        Assert.That(StartupLaunchSelector.Parse(args).SaveDataRoot, Is.EqualTo(Profile));
        Assert.That(args, Does.Contain("--rlo-character-presets=on"));
        Assert.That(args, Does.Contain("--rlo-giddy-textures=on"));
        Assert.That(args, Does.Contain("--rlo-loading-progress=coalesce"));
        Assert.That(args, Does.Not.Contain("--rlo-png=cache"));
    }

    [TestCase("--rlo-bypass")]
    [TestCase("--rlo-reference")]
    [TestCase("--rlo-op7=baseline")]
    [TestCase("--rlo-op7=invalid")]
    [TestCase("--rlo-strategy=invalid")]
    public void ExplicitControlsNeverFallThroughToNormalActivation(string control)
    {
        var args = new[] { control, "-savedatafolder=" + Profile };
        Assert.That(UserStartupSelection.Resolve(args, new OptimizerSettings(), Profile), Is.EqualTo(args));
    }

    [Test]
    public void MasterOffAndIndividualSelectionsRemainIndependent()
    {
        var settings = new OptimizerSettings { Enabled = false };
        Assert.That(UserStartupSelection.Resolve(Array.Empty<string>(), settings, Profile), Is.Empty);
        settings.Enabled = true;
        settings.DefinitionSearches = settings.TypeSearches = settings.GagarinReuse = settings.CharacterPresets = settings.LoadingProgress = settings.GiddyTextures = false;
        settings.PngCache = true;
        var args = UserStartupSelection.Resolve(Array.Empty<string>(), settings, Profile);
        Assert.That(args, Does.Contain("--rlo-user-def-search=off"));
        Assert.That(args, Does.Contain("--rlo-user-type-search=off"));
        Assert.That(args, Does.Contain("--rlo-png=cache"));
        Assert.That(args, Does.Not.Contain("--rlo-character-presets=on"));
        Assert.That(args, Does.Not.Contain("--rlo-gagarin-cache=on"));
    }
}
