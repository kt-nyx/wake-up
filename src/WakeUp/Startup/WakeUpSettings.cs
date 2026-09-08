// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using Verse;

namespace WakeUp;

public sealed class WakeUpSettings : ModSettings
{
    public bool Enabled = true;
    public bool DefinitionSearches = true;
    public bool TypeSearches = true;
    public bool GagarinReuse = true;
    public bool CharacterPresets = true;
    public bool LoadingProgress = true;
    public bool GiddyTextures = true;
    public bool PngCache;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref Enabled, "enabled", true);
        Scribe_Values.Look(ref DefinitionSearches, "definitionSearches", true);
        Scribe_Values.Look(ref TypeSearches, "typeSearches", true);
        Scribe_Values.Look(ref GagarinReuse, "gagarinReuse", true);
        Scribe_Values.Look(ref CharacterPresets, "characterPresets", true);
        Scribe_Values.Look(ref LoadingProgress, "loadingProgress", true);
        Scribe_Values.Look(ref GiddyTextures, "giddyTextures", true);
        Scribe_Values.Look(ref PngCache, "pngCache", false);
    }
}
