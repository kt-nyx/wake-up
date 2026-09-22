// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using NUnit.Framework;
using Verse;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
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
        Assert.That(args, Does.Not.Contain("--wake-up-translations=on"));
        Assert.That(args, Does.Not.Contain("--wake-up-deferred-audio=on"));
        Assert.That(args, Does.Not.Contain("--wake-up-streaming-xml=on"));
        Assert.That(args, Does.Not.Contain("--wake-up-xml-reuse=on"));
        Assert.That(args, Does.Not.Contain("--wake-up-loading-display=on"));
        Assert.That(args, Does.Not.Contain("--wake-up-loading-display-diagnostics=on"));
    }

    [TestCase("--wake-up-bypass")]
    [TestCase("--wake-up-reference")]
    [TestCase("--wake-up-mode=baseline")]
    [TestCase("--wake-up-mode=invalid")]
    [TestCase("--wake-up-strategy=invalid")]
    [TestCase("--wake-up-translations=off")]
    [TestCase("--wake-up-translations=invalid")]
    [TestCase("--wake-up-xml-reuse=off")]
    [TestCase("--wake-up-streaming-xml=off")]
    [TestCase("--wake-up-streaming-xml=invalid")]
    [TestCase("--wake-up-loading-display=off")]
    [TestCase("--wake-up-loading-display=invalid")]
    public void ExplicitControlsNeverFallThroughToNormalActivation(string control)
    {
        var args = new[] { control, "-savedatafolder=" + Profile };
        Assert.That(UserStartupSelection.Resolve(args, new WakeUpSettings { TranslationApplication = true, LoadingDisplay = true, LoadingDisplayDiagnostics = true }, Profile), Is.EqualTo(args));
    }

    [Test]
    public void MasterOffAndIndividualSelectionsRemainIndependent()
    {
        var settings = new WakeUpSettings { Enabled = false, TranslationApplication = true };
        Assert.That(UserStartupSelection.Resolve(Array.Empty<string>(), settings, Profile), Is.Empty);
        settings.Enabled = true;
        settings.DefinitionSearches = settings.TypeSearches = settings.GagarinReuse = settings.CharacterPresets = settings.LoadingProgress = settings.GiddyTextures = false;
        settings.PngCache = true;
        var args = UserStartupSelection.Resolve(Array.Empty<string>(), settings, Profile);
        Assert.That(args, Does.Contain("--wake-up-user-def-search=off"));
        Assert.That(args, Does.Contain("--wake-up-user-type-search=off"));
        Assert.That(args, Does.Contain("--wake-up-png=cache"));
        Assert.That(args, Does.Contain("--wake-up-translations=on"));
        Assert.That(args, Does.Not.Contain("--wake-up-character-presets=on"));
        Assert.That(args, Does.Not.Contain("--wake-up-gagarin-cache=on"));
    }

    [Test]
    public void StreamingXmlIsOptInAndIndependentOfSearches()
    {
        var settings = new WakeUpSettings { StreamingXml = true, DefinitionSearches = false, TypeSearches = false };
        var args = UserStartupSelection.Resolve(Array.Empty<string>(), settings, Profile);
        Assert.That(args, Does.Contain("--wake-up-streaming-xml=on"));
        Assert.That(args, Does.Contain("--wake-up-user-def-search=off"));
        Assert.That(args, Does.Contain("--wake-up-user-type-search=off"));
        settings.Enabled = false;
        Assert.That(UserStartupSelection.Resolve(Array.Empty<string>(), settings, Profile), Is.Empty);
        var controls = new[] { "--wake-up-streaming-xml=off" };
        Assert.That(UserStartupSelection.Resolve(controls, settings, Profile), Is.EqualTo(controls));
    }

    [Test]
    public void TranslationSettingSelectsTheFollowingLaunchWithoutChangingAnExistingSelection()
    {
        var settings = new WakeUpSettings();
        var existing = UserStartupSelection.Resolve(Array.Empty<string>(), settings, Profile);
        settings.TranslationApplication = true;
        var next = UserStartupSelection.Resolve(Array.Empty<string>(), settings, Profile);
        Assert.That(existing, Does.Not.Contain("--wake-up-translations=on"));
        Assert.That(next, Does.Contain("--wake-up-translations=on"));
        settings.TranslationApplication = false;
        Assert.That(UserStartupSelection.Resolve(Array.Empty<string>(), settings, Profile), Is.EqualTo(existing));

        var repeated = new[] { "--wake-up-mode=candidate", "-savedatafolder=" + Profile,
            "--wake-up-translations=on", "--wake-up-translations=off" };
        settings.TranslationApplication = true;
        Assert.That(UserStartupSelection.Resolve(repeated, settings, Profile), Is.EqualTo(repeated));
        Assert.That(TranslationRuntime.TryInitialize(repeated), Is.False);
        foreach (string control in new[] { "--wake-up-bypass", "--wake-up-mode=baseline", "--wake-up-mode=invalid" })
        {
            var explicitArgs = new[] { control, "-savedatafolder=" + Profile, "--wake-up-translations=on" };
            Assert.That(UserStartupSelection.Resolve(explicitArgs, settings, Profile), Is.EqualTo(explicitArgs));
            Assert.That(TranslationRuntime.TryInitialize(explicitArgs), Is.False);
        }
    }

    [Test]
    public void RetiredSettingsCannotSelectWorkAndRetainedTextureOptionsRemainIndependent()
    {
        var settings = new WakeUpSettings();
        Assert.That(UserStartupSelection.Resolve(Array.Empty<string>(), settings, Profile), Does.Not.Contain("--wake-up-parsed-language=on"));
        settings.ParsedLanguage = settings.ParsedXml = settings.OrderedInput = settings.ResolvedInheritance = settings.DeferredAudio = settings.FirstBuildImages = settings.StaticAtlases = settings.AtlasBatching = true;
        settings.ProcessedXml = settings.TranslationApplication = settings.PsdSupport = settings.PreparedTextures = settings.PngCache = true;
        var selected = UserStartupSelection.Resolve(Array.Empty<string>(), settings, Profile);
        foreach (string key in new[] { "parsed-language", "parsed-xml", "ordered-input", "resolved-inheritance", "deferred-audio", "first-build-images", "static-atlases", "atlas-batching" })
            Assert.That(selected, Does.Not.Contain("--wake-up-" + key + "=on"));
        foreach (string retained in new[] { "processed-xml=on", "translations=on", "psd=merged", "prepared=0", "png=cache" })
            Assert.That(selected, Does.Contain("--wake-up-" + retained));
        settings.PreparedTextures = settings.PngCache = false;
        Assert.That(UserStartupSelection.Resolve(Array.Empty<string>(), settings, Profile), Does.Contain("--wake-up-png=control"), "PSD does not depend on retired first-build acceleration");
        settings.Enabled = false;
        Assert.That(UserStartupSelection.Resolve(Array.Empty<string>(), settings, Profile), Is.Empty);
        var controls = new[] { "--wake-up-parsed-language=off" };
        Assert.That(UserStartupSelection.Resolve(controls, settings, Profile), Is.EqualTo(controls));
    }

    [Test]
    public void LegacyAtlasFlagsCannotInstallRetiredHooksOrDisableDisplaySelection()
    {
        string root = Path.Combine(Path.GetTempPath(), "wakeup-retired-atlas-" + Guid.NewGuid().ToString("N"));
        try
        {
            var arguments = new[] { "--wake-up-mode=candidate", "-savedatafolder=" + root,
                "--wake-up-static-atlases=on", "--wake-up-atlas-batching=on", "--wake-up-loading-display=on" };
            var selected = UserStartupSelection.Resolve(arguments,
                new WakeUpSettings { StaticAtlases = true, AtlasBatching = true }, root);
            AtlasRuntime.Initialize(selected, root);
            Assert.That(AtlasRuntime.ReuseInstalled || AtlasRuntime.CanBatch, Is.False);
            Assert.That(Harmony.GetAllPatchedMethods().Any(method =>
                Harmony.GetPatchInfo(method)?.Owners.Contains(AtlasRuntime.Owner) == true), Is.False);
            Assert.That(LoadingDisplayRuntime.Selected(selected), Is.True,
                "retired atlas controls must not suppress the independent display");
            Assert.That(Directory.Exists(Path.Combine(root, "WakeUp", "StaticAtlases")), Is.False,
                "legacy activation must not construct a retired cache");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void NativeSettingsPersistenceRoundTripsTranslationAndKeepsOldDefaults(bool selected)
    {
        ScribeSaver previousSaver = Scribe.saver;
        ScribeLoader previousLoader = Scribe.loader;
        LoadSaveMode previousMode = Scribe.mode;
        string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "translation-setting-" + selected + ".xml");
        try
        {
            Scribe.saver = new ScribeSaver();
            Scribe.loader = new ScribeLoader();
            Scribe.mode = LoadSaveMode.Inactive;
            Scribe.saver.InitSaving(path, "Settings");
            new WakeUpSettings { TranslationApplication = selected, LoadingDisplay = selected, LoadingDisplayDiagnostics = selected, AssetRouting = selected, BackgroundLoading = selected, DeferredAudio = selected,
                StreamingXml = selected, HideLoadingSummary = selected, LoadingTimings = selected }.ExposeData();
            Scribe.saver.FinalizeSaving();
            Assert.That(File.ReadAllText(path).Contains("<translationApplication>"), Is.EqualTo(selected),
                "Default false omits the key, matching an older settings file.");
            // Simulate a persisted development profile, not merely new defaults.
            string retired = string.Concat(new[] { "parsedLanguage", "parsedXml", "orderedInput", "resolvedInheritance", "deferredAudio", "firstBuildImages", "staticAtlases", "atlasBatching" }
                .Select(key => "<" + key + ">true</" + key + ">"));
            File.WriteAllText(path, File.ReadAllText(path).Replace("</Settings>", retired + "</Settings>"));
            Scribe.loader.InitLoading(path);
            var restored = new WakeUpSettings { TranslationApplication = !selected, LoadingDisplay = !selected, LoadingDisplayDiagnostics = !selected, AssetRouting = !selected, BackgroundLoading = !selected, DeferredAudio = !selected,
                StreamingXml = !selected, HideLoadingSummary = !selected, LoadingTimings = !selected };
            restored.ExposeData();
            Assert.That(restored.TranslationApplication, Is.EqualTo(selected));
            Assert.That(restored.LoadingDisplay, Is.EqualTo(selected));
            Assert.That(restored.LoadingDisplayDiagnostics, Is.EqualTo(selected));
            Assert.That(restored.AssetRouting, Is.EqualTo(selected));
            Assert.That(restored.BackgroundLoading, Is.EqualTo(selected));
            Assert.That(restored.DeferredAudio, Is.False);
            Assert.That(restored.ParsedLanguage || restored.ParsedXml || restored.OrderedInput || restored.ResolvedInheritance || restored.FirstBuildImages || restored.StaticAtlases || restored.AtlasBatching, Is.False);
            Assert.That(restored.StreamingXml, Is.EqualTo(selected));
            Assert.That(restored.HideLoadingSummary, Is.EqualTo(selected));
            Assert.That(restored.LoadingTimings, Is.EqualTo(selected));
            Assert.That(restored.Enabled && restored.DefinitionSearches && restored.TypeSearches && restored.GagarinReuse
                && restored.CharacterPresets && restored.LoadingProgress && restored.GiddyTextures, Is.True);
            Assert.That(restored.PngCache, Is.False);
            Assert.That(restored.CacheMaintenanceNextLaunch, Is.EqualTo("normal"));
            Assert.That(UserStartupSelection.Resolve(Array.Empty<string>(), restored, Profile).Contains("--wake-up-translations=on"), Is.EqualTo(selected));
            Assert.That(LoadingDisplayRuntime.Selected(UserStartupSelection.Resolve(Array.Empty<string>(), restored, Profile)), Is.EqualTo(selected));
        }
        finally
        {
            Scribe.ForceStop();
            Scribe.saver = previousSaver;
            Scribe.loader = previousLoader;
            Scribe.mode = previousMode;
        }
    }
}
