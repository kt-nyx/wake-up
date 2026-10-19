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
    private WakeUp17Settings? settings;
    private RuntimeContext? context;
    private string state = "Startup improvements have not been selected.";
    private Vector2 scroll;
    private float height = 700f;

    public WakeUp17Mod(ModContentPack content) : base(content)
    {
        try
        {
            settings = GetSettings<WakeUp17Settings>();
            if (!settings.Enabled) { state = "Startup improvements are off for this launch."; return; }
            if (PlayDataLoader.Loaded) { state = "Ordinary loading: startup has already finished."; return; }
            RuntimeContract.RequireRuntime(typeof(Mod).Assembly);
            context = new RuntimeContext(typeof(Mod).Assembly, message => Log.Message(message));
            new SessionLifetime(context, () => PlayDataLoader.Loaded, () => LongEventHandler.AnyEventNowOrWaiting)
                .Install(context.NamedMethod("Verse.Root_Entry", "Update"));
            state = "Private 1.7 testing candidate. Selected improvements use their own compatibility checks.";
            context.Log(state + " Reviewed game: " + RuntimeContract.GameLabel + ". No Prepatcher, texture cache or early loader is used.");
            var definitions = new DefinitionSearches(context);
            context.Install(DefinitionSearches.Feature, settings.Definitions, definitions.Install, definitions.Clear);
            var types = new TypeNameSearches(context);
            context.Install(TypeNameSearches.Feature, settings.TypeNames, types.Install, types.Clear);
            var leaves = new LeafSearches(context);
            context.Install(LeafSearches.Feature, settings.LeafClasses, leaves.Install, leaves.Clear);
            var members = new MemberSearches(context, settings.LoadingMembers, settings.TranslationMembers);
            context.Install(MemberSearches.Feature, settings.LoadingMembers || settings.TranslationMembers, members.Install, members.Clear);
        }
        catch (Exception error)
        {
            context?.CompleteStartup();
            state = "Ordinary loading: " + error.GetBaseException().Message;
            Log.Warning("[Wake-Up 1.7] " + state);
        }
    }

    public override string SettingsCategory() => "Wake-Up";
    public override void DoSettingsWindowContents(Rect inRect)
    {
        settings ??= GetSettings<WakeUp17Settings>();
        scroll = GUI.BeginScrollView(inRect, scroll, new Rect(0f, 0f, inRect.width - 20f, height));
        var list = new Listing_Standard { maxOneColumn = true };
        list.Begin(new Rect(0f, 0f, inRect.width - 20f, height));
        list.Label("Wake-Up 1.7 preview — restart RimWorld after changing these options.");
        list.Label("This build is ready for game testing. Gameplay compatibility and faster loading have not yet been established.");
        list.Gap();
        list.CheckboxLabeled("Enable startup improvements", ref settings.Enabled);
        list.CheckboxLabeled("Reuse definition and template searches", ref settings.Definitions);
        list.CheckboxLabeled("Reuse code type-name searches", ref settings.TypeNames);
        list.CheckboxLabeled("Reduce repeated leaf-class checks", ref settings.LeafClasses);
        list.Label("Leaf classes have no more specialized classes beneath them. This option reduces repeated checks during startup and alert creation.");
        list.CheckboxLabeled("Reuse loading member lookups", ref settings.LoadingMembers);
        list.Label("Remembers where fields, properties and methods are found. Their actual reads, writes and callbacks still run normally.");
        list.CheckboxLabeled("Reuse translation member lookups (optional)", ref settings.TranslationMembers);
        list.Label("Default off. Reuses member lookup information within each translation package. Duplicate translation checks stay native.");
        list.Gap();
        list.Label(state);
        list.Label(context?.Report ?? "No search features were installed.");
        list.Label("RimWorld handles textures, background asset preparation and texture caching. This preview does not change those systems or require Prepatcher.");
        list.End(); height = list.CurHeight + 20f;
        GUI.EndScrollView();
    }
}

public sealed class WakeUp17Settings : ModSettings
{
    public bool Enabled = true;
    public bool Definitions = true;
    public bool TypeNames = true;
    public bool LeafClasses = true;
    public bool LoadingMembers = true;
    public bool TranslationMembers;
    public override void ExposeData()
    {
        Scribe_Values.Look(ref Enabled, "enabled17", true);
        Scribe_Values.Look(ref Definitions, "definitionSearches17", true);
        Scribe_Values.Look(ref TypeNames, "typeNameSearches17", true);
        Scribe_Values.Look(ref LeafClasses, "leafClasses17", true);
        Scribe_Values.Look(ref LoadingMembers, "loadingMembers17", true);
        Scribe_Values.Look(ref TranslationMembers, "translationMembers17", false);
    }
}
