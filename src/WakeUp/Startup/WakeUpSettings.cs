// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using Verse;

namespace WakeUp;

public sealed class WakeUpSettings : ModSettings
{
    public bool Enabled = true;
    public bool DefinitionSearches = true;
    public bool TypeSearches = true;
    public bool TranslationApplication;
    public bool ParsedLanguage;
    public bool AssetRouting;
    public bool DeferredAudio;
    public bool StreamingXml;
    public bool ParsedXml;
    public bool OrderedInput;
    public bool ProcessedXml;
    public bool ResolvedInheritance;
    public bool XmlQueryExtensions;
    public int SharedCacheMiB = 1024;
    public bool BackgroundLoading;
    public bool GagarinReuse = true;
    public bool CharacterPresets = true;
    public bool LoadingProgress = true;
    public bool LoadingDisplay;
    public bool LoadingDisplayDiagnostics;
    public bool HideLoadingSummary;
    public bool LoadingTimings;
    public bool GiddyTextures = true;
    public bool PngCache;
    public bool PreparedTextures;
    public bool CompressTextureStorage;
    public bool PsdSupport;
    public bool FirstBuildImages;
    public bool StaticAtlases;
    public bool AtlasBatching;
    public int PreparedPreset;
    public string CacheMaintenanceNextLaunch = "normal";

    public override void ExposeData()
    {
        RetireReleaseSettings();
        Scribe_Values.Look(ref Enabled, "enabled", true);
        Scribe_Values.Look(ref DefinitionSearches, "definitionSearches", true);
        Scribe_Values.Look(ref TypeSearches, "typeSearches", true);
        Scribe_Values.Look(ref TranslationApplication, "translationApplication", false);
        Scribe_Values.Look(ref ParsedLanguage, "parsedLanguage", false);
        Scribe_Values.Look(ref AssetRouting, "assetRouting", false);
        Scribe_Values.Look(ref DeferredAudio, "deferredAudio", false);
        Scribe_Values.Look(ref StreamingXml, "streamingXml", false);
        Scribe_Values.Look(ref ParsedXml, "parsedXml", false);
        Scribe_Values.Look(ref OrderedInput, "orderedInput", false);
        Scribe_Values.Look(ref ProcessedXml, "processedXml", false);
        Scribe_Values.Look(ref ResolvedInheritance, "resolvedInheritance", false);
        Scribe_Values.Look(ref XmlQueryExtensions, "xmlQueryExtensions", false);
        Scribe_Values.Look(ref SharedCacheMiB, "sharedCacheMiB", 1024);
        if (SharedCacheMiB < 1 || SharedCacheMiB > 65536) SharedCacheMiB = 1024;
        Scribe_Values.Look(ref BackgroundLoading, "backgroundLoading", false);
        Scribe_Values.Look(ref GagarinReuse, "gagarinReuse", true);
        Scribe_Values.Look(ref CharacterPresets, "characterPresets", true);
        Scribe_Values.Look(ref LoadingProgress, "loadingProgress", true);
        Scribe_Values.Look(ref LoadingDisplay, "loadingDisplay", false);
        Scribe_Values.Look(ref LoadingDisplayDiagnostics, "loadingDisplayDiagnostics", false);
        Scribe_Values.Look(ref HideLoadingSummary, "hideLoadingSummary", false);
        Scribe_Values.Look(ref LoadingTimings, "loadingTimings", false);
        Scribe_Values.Look(ref GiddyTextures, "giddyTextures", true);
        Scribe_Values.Look(ref PngCache, "pngCache", false);
        Scribe_Values.Look(ref PreparedTextures, "preparedTextures", false);
        Scribe_Values.Look(ref CompressTextureStorage, "compressTextureStorage", false);
        Scribe_Values.Look(ref PsdSupport, "psdSupport", false);
        Scribe_Values.Look(ref FirstBuildImages, "firstBuildImages", false);
        Scribe_Values.Look(ref StaticAtlases, "staticAtlases", false);
        Scribe_Values.Look(ref AtlasBatching, "atlasBatching", false);
        Scribe_Values.Look(ref PreparedPreset, "preparedPreset", 0);
        // Older development builds exposed lossy presets. This release retains
        // native preservation only; stale settings must not select hidden modes.
        PreparedPreset = 0;
        Scribe_Values.Look(ref CacheMaintenanceNextLaunch, "cacheMaintenanceNextLaunch", "normal");
        RetireReleaseSettings();
    }

    // Keep the old keys readable for migration, but never persist or activate
    // retired release paths, including settings written by development builds.
    internal void RetireReleaseSettings()
    {
        ParsedLanguage = DeferredAudio = ParsedXml = OrderedInput = ResolvedInheritance = FirstBuildImages = StaticAtlases = AtlasBatching = false;
    }
}
