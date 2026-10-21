// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using UnityEngine;
using Verse;

namespace WakeUp.V17;

// The distinct class name also gives 1.7 its own settings filename. Do not read
// or migrate the 1.6 options that selected the retired pipelines and stores.
public sealed class WakeUp17Mod : Mod
{
    private string state = "Startup optimizations are disabled: no reliable loading-time benefit is qualified for RimWorld 1.7.";

    public WakeUp17Mod(ModContentPack content) : base(content)
    {
        try
        {
            var settings = GetSettings<WakeUp17Settings>();
            RuntimeContract.RequireRuntime(typeof(Mod).Assembly);
            if (settings.TranslationPredicate && !string.Equals(Prefs.LangFolderName, "English", StringComparison.Ordinal))
            {
                var context = new RuntimeContext(typeof(Mod).Assembly, m => Log.Message(m));
                var feature = new TranslationPredicates(context);
                context.Install(TranslationPredicates.Feature, true, feature.Install, feature.Clear);
                if (!feature.Installed) { state = context.Report; return; }
                try
                {
                    new SessionLifetime(context, () => PlayDataLoader.Loaded, () => LongEventHandler.AnyEventNowOrWaiting)
                        .Install(context.NamedMethod("Verse.Root_Entry", "Update"));
                }
                catch
                {
                    try { context.CompleteStartup(); }
                    finally { context.Unpatch("lifetime"); }
                    throw;
                }
                state = "Experimental translation predicate selected. Loading-time benefit remains unqualified.";
                return;
            }
            Log.Message("[Wake-Up 1.7] " + state + " Native loading is used; no Wake-Up patches are installed.");
        }
        catch (Exception error)
        {
            state = "Ordinary loading: " + error.GetBaseException().Message;
            Log.Warning("[Wake-Up 1.7] " + state);
        }
    }

    public override string SettingsCategory() => "Wake-Up";
    public override void DoSettingsWindowContents(Rect inRect)
    {
        var list = new Listing_Standard { maxOneColumn = true };
        list.Begin(inRect);
        list.Label("Wake-Up 1.7 development preview — experimental feature off by default.");
        var settings = GetSettings<WakeUp17Settings>();
        list.CheckboxLabeled("Experimental translation predicate (next startup; benefit unqualified)", ref settings.TranslationPredicate);
        list.Label("Repeated cloud comparisons did not establish a reliable loading-time improvement. RimWorld uses its native loading behavior.");
        list.Gap();
        list.Label(state);
        list.Label("Existing preview preferences are preserved but do not activate retired search features. The experimental predicate is a separate option.");
        list.Label("No loading-time improvement is claimed. Wake-Up is not needed for native RimWorld 1.7 loading.");
        list.End();
    }
}

// Retained only for compatibility with existing preview settings. These values
// are deliberately ignored by the inactive 1.7 mod; do not migrate 1.6 settings.
public sealed class WakeUp17Settings : ModSettings
{
    public bool Enabled = true;
    public bool Definitions = true;
    public bool TypeNames = true;
    public bool LeafClasses = true;
    public bool LoadingMembers = true;
    public bool TranslationMembers;
    public bool TranslationPredicate;
    public override void ExposeData()
    {
        Scribe_Values.Look(ref Enabled, "enabled17", true);
        Scribe_Values.Look(ref Definitions, "definitionSearches17", true);
        Scribe_Values.Look(ref TypeNames, "typeNameSearches17", true);
        Scribe_Values.Look(ref LeafClasses, "leafClasses17", true);
        Scribe_Values.Look(ref LoadingMembers, "loadingMembers17", true);
        Scribe_Values.Look(ref TranslationMembers, "translationMembers17", false);
        Scribe_Values.Look(ref TranslationPredicate, "translationPredicate17", false);
    }
}
