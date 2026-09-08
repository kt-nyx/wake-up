// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using Verse;
using UnityEngine;

namespace WakeUp;

// RimWorld constructs Mod subclasses before LoadModXML. No early assembly rewrite
// or diagnostic pipeline is needed by the retained startup optimizations.
public sealed class WakeUpMod : Mod
{
    private WakeUpSettings? settings;
    private string launchStatus = "Startup optimizations are unavailable for this launch.";
    private bool linuxSupport;

    public WakeUpMod(ModContentPack content) : base(content)
    {
        try
        {
            if (PlayDataLoader.Loaded)
                return;
            string[] arguments = Environment.GetCommandLineArgs();
            if (!UserStartupSelection.HasExplicitSelection(arguments))
            {
                settings = GetSettings<WakeUpSettings>();
                arguments = UserStartupSelection.Resolve(arguments, settings, GenFilePaths.SaveDataFolderPath);
                string reason = "disabled";
                bool recognized = settings.Enabled && RuntimeIdentity.ValidateBinaryIdentity(out reason);
                linuxSupport = recognized && GameBuildContract.Current.IsLinux;
                launchStatus = !settings.Enabled ? "Startup optimizations were disabled for this launch."
                    : recognized ? "Game revisions are not restricted. Selected features use their own compatibility checks; future updates may disable individual improvements."
                    : RuntimeIdentity.DescribeFailure(reason);
                Log.Message("[Wake-Up] " + launchStatus);
                if (settings.Enabled && !recognized)
                    Log.Message("[Wake-Up] Compatibility check: " + reason);
            }
            else
                launchStatus = "This launch uses explicit development controls instead of these settings.";
            StartupFeatureRunner.Run("Character Editor", () => CharacterPresetRuntime.TryInitialize(arguments));
            StartupFeatureRunner.Run("Giddy-Up", () => GiddyTextureRuntime.TryInitialize(arguments));
            StartupFeatureRunner.Run("Loading Progress", () => RepaintCoalescer.TryInitialize(arguments));
            StartupFeatureRunner.Run("PNG cache", () => PngRuntime.TryInitialize(arguments));
            StartupFeatureRunner.Run("Gagarin", () => GagarinCacheRuntime.TryInitialize(arguments));
            StartupSearchRuntime.Initialize(arguments);
        }
        catch (Exception exception)
        {
            Log.Warning("[Wake-Up] Startup setup stopped: " + exception.GetType().Name + ". Ordinary fallback remains available.");
        }
    }

    public override string SettingsCategory() => "Wake-Up";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        settings ??= GetSettings<WakeUpSettings>();
        var list = new Listing_Standard();
        list.Begin(inRect);
        list.Label("Changes take effect after restarting RimWorld.");
        list.Label(launchStatus);
        if (linuxSupport)
            list.Label("Linux OpenGL support: PNG caching and the Giddy-Up texture improvement use feature compatibility checks. No extra launch flag is required.");
        list.Gap();
        list.CheckboxLabeled("Enable startup optimizations", ref settings.Enabled);
        list.CheckboxLabeled("Faster definition and template searches", ref settings.DefinitionSearches);
        list.CheckboxLabeled("Faster code type searches", ref settings.TypeSearches);
        list.Gap();
        list.Label("Optional mod improvements: missing or unsupported versions keep ordinary loading.");
        list.CheckboxLabeled("Reuse Gagarin's parsed XML", ref settings.GagarinReuse);
        list.CheckboxLabeled("Reuse Character Editor preset lookups", ref settings.CharacterPresets);
        list.CheckboxLabeled("Reduce Loading Progress repaint pauses", ref settings.LoadingProgress);
        list.CheckboxLabeled("Reduce Giddy-Up texture readback", ref settings.GiddyTextures);
        list.Gap();
        list.CheckboxLabeled("Cache processed PNG textures (up to 512 MiB)", ref settings.PngCache);
        list.Label("PNG caching can slow the first launch while building its cache. Later launches may be faster. DDS textures are unaffected. Supports compatible Windows Direct3D 11 and Linux OpenGL loaders, including Steam's default Linux launch settings.");
        list.End();
    }
}
