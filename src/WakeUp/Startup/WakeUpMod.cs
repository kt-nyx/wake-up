// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using Verse;
using UnityEngine;

namespace WakeUp;

// RimWorld constructs Mod subclasses before LoadModXML. S11's inert Prepatcher
// entry hook is installed earlier; this constructor selects runtime features.
public sealed class WakeUpMod : Mod
{
    private WakeUpSettings? settings;
    private string launchStatus = "Startup optimizations are unavailable for this launch.";
    private bool linuxSupport;
    private Vector2 settingsScroll;
    private float settingsHeight = 900f;

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
                StartupFeatureRunner.Run("standalone preparation request", () => PreparationRequest.Apply(GenFilePaths.SaveDataFolderPath, settings));
                StartupFeatureRunner.Run("cache launch selection", () =>
                    CacheLaunchPolicy.Initialize(settings.CacheMaintenanceNextLaunch, () =>
                    {
                        settings.CacheMaintenanceNextLaunch = "normal";
                        settings.Write();
                    }));
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
            {
                CacheLaunchPolicy.Initialize("normal", () => { });
                launchStatus = "This launch uses explicit development controls instead of these settings.";
            }
            StartupFeatureRunner.Run("shared cache budget", () => SharedCacheBudget.Initialize(GenFilePaths.SaveDataFolderPath,
                settings?.SharedCacheMiB ?? SharedCacheBudget.DefaultMiB, CacheLaunchPolicy.Current));
            if (CacheLaunchPolicy.Current.Action == CacheAction.Clear)
            {
                // Preserve explicit maintenance of old owned stores without
                // allowing old settings or command lines to select their work.
                StartupFeatureRunner.Run("retired parsed XML maintenance", () => ParsedXmlRuntime.Initialize(Array.Empty<string>(), GenFilePaths.SaveDataFolderPath));
                StartupFeatureRunner.Run("retired inheritance maintenance", () => ResolvedInheritanceRuntime.Initialize(Array.Empty<string>(), GenFilePaths.SaveDataFolderPath));
                StartupFeatureRunner.Run("retired language maintenance", () => ParsedLanguageRuntime.Initialize(Array.Empty<string>(), GenFilePaths.SaveDataFolderPath));
            }
            PreparedTextureRuntime.Initialize(GenFilePaths.SaveDataFolderPath, content.RootDir, arguments);
            StartupFeatureRunner.Run("prepared cache maintenance", () =>
                PreparedTextureStore.Maintain(GenFilePaths.SaveDataFolderPath, CacheLaunchPolicy.Current));
            StartupFeatureRunner.Run("cache maintenance", () =>
                PngCache.Maintain(GenFilePaths.SaveDataFolderPath, CacheLaunchPolicy.Current));
            StartupFeatureRunner.Run("Character Editor", () => CharacterPresetRuntime.TryInitialize(arguments));
            StartupFeatureRunner.Run("Giddy-Up", () => GiddyTextureRuntime.TryInitialize(arguments));
            StartupFeatureRunner.Run("Loading Progress", () => RepaintCoalescer.TryInitialize(arguments));
            StartupFeatureRunner.Run("PNG cache", () => PngRuntime.TryInitialize(arguments));
            StartupFeatureRunner.Run("retired atlas maintenance", () => AtlasRuntime.Initialize(arguments, GenFilePaths.SaveDataFolderPath));
            StartupFeatureRunner.Run("Gagarin", () => GagarinCacheRuntime.TryInitialize(arguments));
            StartupFeatureRunner.Run("Translations", () => TranslationRuntime.TryInitialize(arguments));
            StartupFeatureRunner.Run("Asset routing", () => AssetRoutingRuntime.TryInitialize(arguments));
            StartupFeatureRunner.Run("Streaming XML", () => StreamingXmlRuntime.TryInitialize(arguments));
            StartupFeatureRunner.Run("Processed XML", () => ProcessedXmlRuntime.Initialize(arguments, GenFilePaths.SaveDataFolderPath));
            StartupSearchRuntime.Initialize(arguments);
            StartupFeatureRunner.Run("Native loading summary", () => NativeLoadingSummaryRuntime.TryInitialize(arguments));
            StartupFeatureRunner.Run("Per-mod XML timings", () => LoadingTimingRuntime.TryInitialize(arguments));
            StartupFeatureRunner.Run("Requested XML export completion", () => LoadingDiagnosticsRuntime.ObserveStartupCompletion());
            StartupFeatureRunner.Run("Loading display", () => LoadingDisplayRuntime.TryInitialize(arguments));
            StartupFeatureRunner.Run("Background save loading", () => BackgroundLoadingRuntime.TryInitialize(arguments));
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
        var contentRect = new Rect(0f, 0f, inRect.width - 20f, settingsHeight);
        settingsScroll = GUI.BeginScrollView(inRect, settingsScroll, contentRect);
        // Keep all rows in the scroll view; column wrapping truncates CurHeight.
        var list = new Listing_Standard { maxOneColumn = true };
        list.Begin(contentRect);
        list.Label("Changes take effect after restarting RimWorld.");
        list.Label(launchStatus);
        if (linuxSupport)
            list.Label("Linux OpenGL support: PNG caching and the Giddy-Up texture improvement use feature compatibility checks. No extra launch flag is required.");
        list.Gap();
        list.CheckboxLabeled("Enable startup optimizations", ref settings.Enabled);
        list.CheckboxLabeled("Faster definition and template searches", ref settings.DefinitionSearches);
        list.CheckboxLabeled("Faster code type searches", ref settings.TypeSearches);
        list.CheckboxLabeled("Stream XML input (experimental)", ref settings.StreamingXml);
        list.Label("Default off. Parses supported XML files without first allocating their complete text. Keeps the game's reading workers and file order. No persistent XML cache; not yet tested in game.");
        list.Label(StreamingXmlRuntime.Status);
        list.CheckboxLabeled("Reuse completed XML patches on later launches", ref settings.ProcessedXml);
        list.Label(ProcessedXmlRuntime.Status);
        list.CheckboxLabeled("Extend live XML query reuse", ref settings.XmlQueryExtensions);
        list.Label("Processed XML and extended query reuse default off. Unsupported work runs normally. Performance qualification is pending.");
        list.CheckboxLabeled("Faster translation application", ref settings.TranslationApplication);
        list.CheckboxLabeled("Remember successful asset routes", ref settings.AssetRouting);
        list.Label(AssetRoutingRuntime.Status);
        list.Gap();
        list.CheckboxLabeled("Show Wake-Up loading display", ref settings.LoadingDisplay);
        list.CheckboxLabeled("Record loading display diagnostics", ref settings.LoadingDisplayDiagnostics);
        list.Label("Shows actual startup and later loading work, completed units where known, unknown totals otherwise, and logged errors. A single constructor can still hold a frame; Wake-Up never invents a smooth percentage. Loading Progress retains screen ownership when active.");
        list.Label(LoadingDisplayRuntime.Status);
        list.CheckboxLabeled("Hide native loading mod summary", ref settings.HideLoadingSummary);
        list.Label("Default off. Hides the native summary of expansions and mods on supported loading screens. Loading text, tips and error handling remain; mod information stays in Mods. Independent of Wake-Up's panel. Not a measured startup improvement.");
        list.Label(NativeLoadingSummaryRuntime.Status);
        list.CheckboxLabeled("Record loading session reports", ref settings.LoadingTimings);
        list.Label("Default off. Records early mod constructors, native XML/language/content work, callbacks and later loading. Reports include mod identifiers when known, errors, omissions, and time with and without nested work. Keep up to eight sessions; limits and unobserved regions are explicit. Durations are diagnostic and cannot be added as whole startup time.");
        list.Label(LoadingTimingRuntime.Status);
        if (list.ButtonText("Inspect and save loading sessions...")) Find.WindowStack.Add(new LoadingTimingWindow());
        if (list.ButtonText("Loading features, compatibility and maintenance guidance...")) Find.WindowStack.Add(new LoadingGuidanceWindow());
        list.Label(LoadingDiagnosticsRuntime.Status);
        if (list.ButtonText("Export processed XML and sources on next launch")) LoadingDiagnosticsRuntime.RequestNextLaunch(GenFilePaths.SaveDataFolderPath);
        if (list.ButtonText("Cancel next-launch XML export")) LoadingDiagnosticsRuntime.CancelRequest(GenFilePaths.SaveDataFolderPath);
        list.CheckboxLabeled("Allow loading to finish while unfocused", ref settings.BackgroundLoading);
        list.Label("Temporarily allows supported save, world, new-colony and later native map loading to finish, then restores your current background preference. Keeps native pause-on-load and never unpauses. Default off. Native startup and direct synchronous map/pocket generation remain native. Unknown mod-created loading queues are not covered; this is not a recommendation to remove Load in Background.");
        list.Label(BackgroundLoadingRuntime.Status);
        list.Label(BackgroundLoadingRuntime.WorldStatus);
        list.Label(BackgroundLoadingRuntime.ColonyStatus);
        list.Label(BackgroundLoadingRuntime.MapStatus);
        list.Gap();
        list.Label("Optional mod improvements: updated mods are checked automatically. Missing or incompatible functions keep ordinary loading.");
        list.CheckboxLabeled("Reuse Gagarin's parsed XML", ref settings.GagarinReuse);
        list.CheckboxLabeled("Reuse Character Editor preset lookups", ref settings.CharacterPresets);
        list.CheckboxLabeled("Reduce Loading Progress repaint pauses", ref settings.LoadingProgress);
        list.CheckboxLabeled("Reduce Giddy-Up texture readback", ref settings.GiddyTextures);
        list.Gap();
        list.CheckboxLabeled("Cache processed textures", ref settings.PngCache);
        list.Label("Stores native PNG/JPEG output, and optional PSD output, for later reuse. Cold PNG/JPEG decoding and authored DDS loading stay native. Building entries adds work; historical PNG warm gains do not qualify this combined candidate.");
        list.Gap();
        list.CheckboxLabeled("Load composited PSD images (bundled decoder; restart required)", ref settings.PsdSupport);
        list.Label("Optional new PSD decoding, separate from native preservation. Supports the saved RGB composite within the documented format limits; no separate installation. Unsupported files retain ordinary loading.");
        list.CheckboxLabeled("Use prepared textures", ref settings.PreparedTextures);
        list.CheckboxLabeled("Compress stored texture bytes (lossless; rebuilt entries only)", ref settings.CompressTextureStorage);
        list.Label("Reuses native output and complete explicit quality selections on the next ordinary launch; default off. Prepare textures offers native batches, in-game quality and separate DDS exports. Quality changes can affect pixels even at full size; reset restores ordinary output. All owned caches and exports share the overall limit.");
        list.Label(PreparedQualityRuntime.Status + $" (quality used {PreparedQualityRuntime.Applied}, groups {PreparedQualityRuntime.Groups}, refused {PreparedQualityRuntime.Refused}).");
        list.Label(PreparedTextureRuntime.Status + $" (used {PreparedTextureRuntime.Hits}, missing {PreparedTextureRuntime.Misses}, refused {PreparedTextureRuntime.Refused}).");
        if (list.ButtonText("Prepare textures..."))
            Find.WindowStack.Add(new TexturePreparationWindow(settings));
        list.Gap();
        list.Label("Overall cache storage limit: " + settings.SharedCacheMiB + " MiB. Default 1024 MiB, shared by processed XML, atlas and texture caches/exports. Existing files count even when their feature is disabled.");
        if (list.ButtonText("Use 1 GiB overall limit")) settings.SharedCacheMiB = 1024;
        if (list.ButtonText("Use 2 GiB overall limit")) settings.SharedCacheMiB = 2048;
        if (list.ButtonText("Use 4 GiB overall limit")) settings.SharedCacheMiB = 4096;
        list.Label("Owned cache maintenance: choose an action for the next ordinary launch. Clear also removes saved texture quality selections and DDS exports, restoring ordinary native quality and sampling. Disabled caches stay disabled; original artwork stays intact.");
        Rect actions = list.GetRect(30f);
        float actionWidth = (actions.width - 12f) / 3f;
        if (Widgets.ButtonText(new Rect(actions.x, actions.y, actionWidth, actions.height), "Bypass once"))
            QueueCacheAction("bypass");
        if (Widgets.ButtonText(new Rect(actions.x + actionWidth + 6f, actions.y, actionWidth, actions.height), "Rebuild next launch"))
            QueueCacheAction("rebuild");
        if (Widgets.ButtonText(new Rect(actions.x + 2f * (actionWidth + 6f), actions.y, actionWidth, actions.height), "Clear next launch"))
            QueueCacheAction("clear");
        string pending = settings.CacheMaintenanceNextLaunch;
        list.Label(pending == "bypass" ? "Queued: skip cache reads and writes for one launch, keeping existing files. Normal settings resume afterward."
            : pending == "rebuild" ? "Queued: rebuild enabled caches next launch. Existing entries remain until valid replacements are ready. Normal settings resume afterward."
            : pending == "clear" ? "Queued: remove Wake-Up's owned caches, saved texture quality selections and DDS exports next launch, even when caching is disabled. Ordinary native quality and sampling return; original artwork stays intact. Keep caches unused that launch; normal feature settings resume afterward."
            : "No maintenance queued. These actions apply to owned caches, including older retired entries.");
        if (pending != "normal" && list.ButtonText("Cancel queued maintenance"))
            QueueCacheAction("normal");
        list.End();
        settingsHeight = list.CurHeight + 16f;
        GUI.EndScrollView();
    }

    private void QueueCacheAction(string action)
    {
        settings!.CacheMaintenanceNextLaunch = action;
        settings.Write();
    }
}
