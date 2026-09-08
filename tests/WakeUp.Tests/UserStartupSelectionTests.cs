// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.IO;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class UserStartupSelectionTests
{
    private static string Profile => Path.GetFullPath(Path.Combine(Path.GetTempPath(), "wake-up-user-profile"));

    [Test]
    public void OrdinaryLaunchEnablesSupportedFeaturesWithoutEnablingPersistentTextureWrites()
    {
        var args = UserStartupSelection.Resolve(Array.Empty<string>(), new WakeUpSettings(), Profile);
        Assert.That(StartupLaunchSelector.Parse(args).Selection, Is.EqualTo(StartupSelection.Candidate));
        Assert.That(StartupLaunchSelector.Parse(args).SaveDataRoot, Is.EqualTo(Profile));
        Assert.That(args, Does.Contain("--wake-up-character-presets=on"));
        Assert.That(args, Does.Contain("--wake-up-giddy-textures=on"));
        Assert.That(args, Does.Contain("--wake-up-loading-progress=coalesce"));
        Assert.That(args, Does.Not.Contain("--wake-up-png=cache"));
    }

    [TestCase("--wake-up-bypass")]
    [TestCase("--wake-up-reference")]
    [TestCase("--wake-up-mode=baseline")]
    [TestCase("--wake-up-mode=invalid")]
    [TestCase("--wake-up-strategy=invalid")]
    public void ExplicitControlsNeverFallThroughToNormalActivation(string control)
    {
        var args = new[] { control, "-savedatafolder=" + Profile };
        Assert.That(UserStartupSelection.Resolve(args, new WakeUpSettings(), Profile), Is.EqualTo(args));
    }

    [Test]
    public void MasterOffAndIndividualSelectionsRemainIndependent()
    {
        var settings = new WakeUpSettings { Enabled = false };
        Assert.That(UserStartupSelection.Resolve(Array.Empty<string>(), settings, Profile), Is.Empty);
        settings.Enabled = true;
        settings.DefinitionSearches = settings.TypeSearches = settings.GagarinReuse = settings.CharacterPresets = settings.LoadingProgress = settings.GiddyTextures = false;
        settings.PngCache = true;
        var args = UserStartupSelection.Resolve(Array.Empty<string>(), settings, Profile);
        Assert.That(args, Does.Contain("--wake-up-user-def-search=off"));
        Assert.That(args, Does.Contain("--wake-up-user-type-search=off"));
        Assert.That(args, Does.Contain("--wake-up-png=cache"));
        Assert.That(args, Does.Not.Contain("--wake-up-character-presets=on"));
        Assert.That(args, Does.Not.Contain("--wake-up-gagarin-cache=on"));
    }
}
