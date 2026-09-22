// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using RimWorld;
using RimWorld.Planet;
using RuntimeAudioClipLoader;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace FixtureMenuObserver;

// Observes an existing biome's native ambience. No sample requests, reader
// preparation, artificial SoundDefs, or observer-owned sustainers are involved.
internal static class C10AdvanceAudioProbe
{
    private const string Package = "sarg.alphabiomes", BiomeName = "AB_OcularForest", SoundName = "AB_AmbientAlien";
    private static readonly FieldInfo BiomeSustainers = typeof(AmbientSoundManager).GetField("biomeAmbientSustainers", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly FieldInfo Subs = typeof(Sustainer).GetField("subSustainers", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo Samples = typeof(SubSustainer).GetField("samples", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static MethodInfo snapshot = null!, runtimeSnapshot = null!;
    private static AudioClip clip = null!;
    private static ModContentHolder<AudioClip> holder = null!;
    private static BiomeDef biome = null!;
    private static SoundDef sound = null!;
    private static Sustainer originalSustainer = null!;
    private static SampleSustainer originalSample = null!;
    private static AudioSource originalSource = null!;
    private static Sustainer preReloadSustainer = null!;
    private static AudioSource preReloadSource = null!;
    private static Map originalMap = null!;
    private static bool warm, playbackPassed, navigationPassed, mapRestored, reloadPassed, reloadObservationStarted;
    private static int mainThread, startFrame, firstProgressFrame;
    private static long initialBytes, firstProgressBytes;
    private static float startTime, startedAt;
    private static float reloadStartedAt;
    private static float navigationStartedAt;
    private static int navigationStartFrame;
    internal static readonly Dictionary<string, object> Report = new();
    internal static bool Selected => Environment.GetCommandLineArgs().Any(a => a == "--fixture-c10-prepared-audio=advance-prepare"
        || a == "--fixture-c10-prepared-audio=advance-warm");
    internal static bool PlaybackPassed => playbackPassed;
    internal static bool Passed => playbackPassed && navigationPassed && reloadPassed;

    internal static void Menu(Assembly core, bool isWarm)
    {
        warm = isWarm;
        mainThread = Thread.CurrentThread.ManagedThreadId;
        Type runtime = core.GetType("WakeUp.PreparedAudioRuntime", true)!;
        snapshot = runtime.GetMethod("SnapshotForClip")!;
        runtimeSnapshot = runtime.GetMethod("Snapshot")!;
        var advance = AdvanceSnapshot();
        Require((bool)advance["active"], "Native advance audio preparation was not admitted.");
        ModContentPack mod = LoadedModManager.RunningModsListForReading.Single(m => m.PackageId.Equals(Package, StringComparison.OrdinalIgnoreCase));
        holder = mod.GetContentHolder<AudioClip>();
        Require(holder.contentList.TryGetValue(SoundName, out clip!), "The original Alpha Biomes audio clip was not loaded.");
        biome = DefDatabase<BiomeDef>.GetNamed(BiomeName);
        sound = DefDatabase<SoundDef>.GetNamed(SoundName);
        Require(sound.sustain && biome.soundsAmbient.Contains(sound), "The authored biome does not retain the sustained SoundDef.");
        var grains = typeof(SubSoundDef).GetField("resolvedGrains", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Require(sound.subSounds.Any(sub => ((IEnumerable<ResolvedGrain>)grains.GetValue(sub)).OfType<ResolvedGrain_Clip>()
            .Any(grain => ReferenceEquals(grain.clip, clip))), "The authored resolved grain does not retain the original clip.");
        var menu = Snapshot();
        Require((bool)menu["streaming"] && Equals(menu["format"], "ogg")
            && Manager.GetAudioClipLoadState(clip) == AudioDataLoadState.Loaded, "Natural ambience is not a loaded streaming OGG.");
        var reader = Reader(menu);
        Require(Number(reader, "decoderConstructions") == (warm ? 0 : 1) && Number(reader, "replayMismatches") == 0,
            "Natural ambience lacks the required untouched warm or original cold reader.");
        if (warm) Require((bool)menu["cacheHit"] && !(bool)reader["nativeReady"], "Warm ambience was not deferred automatically.");
        else Require(Number(menu, "recordingPublished") > 0, "Cold ambience did not publish its native loading transcript.");
        Report["package"] = Package; Report["biome"] = BiomeName; Report["sound"] = SoundName;
        Report["menu"] = menu; Report["advanceAtMenu"] = advance;
        Report["samples"] = clip.samples; Report["channels"] = clip.channels; Report["frequency"] = clip.frequency;
        Report["observerCreatedDefinitionOrClip"] = false;
        Report["observerSpawnedMaintainedEndedSustainer"] = false;
        Report["observerReadSeekOrPreparation"] = false;
        Report["performanceClaim"] = false;
    }

    // Called after the ordinary WorldGenerator result exists. Select an actual
    // valid generated tile; do not mutate its biome or retry world generation.
    internal static PlanetTile ChooseStartingTile()
    {
        var layer = Find.WorldGrid.Surface;
        for (int i = 0; i < layer.TilesCount; i++)
        {
            var tile = layer[i];
            if (!ReferenceEquals(tile.PrimaryBiome, biome) || !TileFinder.IsValidTileForNewSettlement(tile.tile)) continue;
            Report["generatedStartingTile"] = tile.tile.ToString();
            Report["tileBiomeWasMutated"] = false;
            return tile.tile;
        }
        throw new InvalidOperationException("The generated fixture world contains no valid authored Ocular Forest starting tile.");
    }

    internal static void Begin()
    {
        Require(ReferenceEquals(Find.CurrentMap.Biome, biome), "Native map generation did not retain the selected authored biome.");
        originalMap = Find.CurrentMap;
        originalSustainer = CurrentSustainer();
        originalSample = CurrentSample(originalSustainer);
        originalSource = originalSample.source;
        Require(!originalSustainer.Ended && originalSource != null && originalSource.isPlaying
            && originalSource.loop && ReferenceEquals(originalSource.clip, clip), "Native biome ambience did not start its original looping clip.");
        var value = Snapshot();
        ValidatePreparation(value);
        initialBytes = Number(Reader(value), "returnedBytes");
        startFrame = Time.frameCount; startTime = originalSource!.time; startedAt = Time.realtimeSinceStartup;
        Report["beforeObservation"] = value;
        Report["advanceAfterMapEntry"] = AdvanceSnapshot();
        Report["startFrame"] = startFrame; Report["sourceTimeAtStart"] = startTime;
        Report["nativeSustainerRegistered"] = Find.SoundRoot.sustainerManager.AllSustainers.Contains(originalSustainer);
    }

    internal static bool Advance()
    {
        if (playbackPassed) return true;
        Require(ReferenceEquals(Find.CurrentMap, originalMap) && !originalSustainer.Ended
            && CurrentBiomeSustainers().Contains(originalSustainer)
            && Find.SoundRoot.sustainerManager.AllSustainers.Contains(originalSustainer), "Native biome sustainer changed before playback proof.");
        Require(originalSource != null && originalSource.isPlaying && originalSource.loop && ReferenceEquals(originalSource.clip, clip),
            "Native looping sample lost its source or clip.");
        var value = Snapshot();
        ValidatePreparation(value);
        long bytes = Number(Reader(value), "returnedBytes");
        if (bytes > initialBytes && firstProgressFrame == 0) { firstProgressFrame = Time.frameCount; firstProgressBytes = bytes; }
        if (firstProgressFrame != 0 && Time.frameCount >= firstProgressFrame + 3 && bytes > firstProgressBytes
            && originalSource!.time >= startTime + 1f)
        {
            playbackPassed = true;
            Report["afterPlayback"] = value; Report["lastPlaybackFrame"] = Time.frameCount;
            Report["sourceTimeAfterPlayback"] = originalSource.time;
            Report["loopingNativePlaybackPassed"] = true;
            return true;
        }
        if (Time.realtimeSinceStartup - startedAt > 30f)
            throw new TimeoutException("Native biome ambience did not show later-frame streaming progress within 30 seconds.");
        return false;
    }

    internal static void AfterGameplayReload()
    {
        Require(TryAfterGameplayReload(), "Native ambience reload cleanup is still pending.");
    }

    // Native map navigation is the actual End path. A full game reload resets
    // SoundRoot and can abandon the old managed sustainer without calling End.
    internal static void BeginMapNavigation()
    {
        Require(playbackPassed && ReferenceEquals(Find.CurrentMap, originalMap), "Native ambience playback must precede map navigation.");
        navigationStartFrame = Time.frameCount;
        navigationStartedAt = Time.realtimeSinceStartup;
        Report["navigation"] = new Dictionary<string, object>
        {
            ["leftMapFrame"] = navigationStartFrame, ["beforeNavigation"] = Snapshot(),
            ["mapIdentity"] = originalMap.GetUniqueLoadID(), ["usedNativeCurrentMapSetter"] = true
        };
        Current.Game.CurrentMap = null;
        Require(Find.CurrentMap == null, "Native navigation did not leave the current map.");
    }

    internal static bool AdvanceMapNavigation()
    {
        if (navigationPassed) return true;
        var result = (Dictionary<string, object>)Report["navigation"];
        if (!mapRestored)
        {
            if (Time.frameCount <= navigationStartFrame) return false;
            Require(Find.CurrentMap == null && Current.Game.Maps.Contains(originalMap), "Native navigation changed the original map's ownership.");
            Current.Game.CurrentMap = originalMap;
            mapRestored = true;
            result["returnedMapFrame"] = Time.frameCount;
        }
        Require(ReferenceEquals(Find.CurrentMap, originalMap), "Native navigation did not retain the original map identity.");
        bool ended = originalSustainer.Ended;
        bool removed = !CurrentBiomeSustainers().Contains(originalSustainer)
            && !Find.SoundRoot.sustainerManager.AllSustainers.Contains(originalSustainer);
        int remaining = SamplesFor(originalSustainer).Count();
        bool destroyed = originalSource == null;
        result["oldEnded"] = ended; result["oldRemoved"] = removed;
        result["oldSamplesRemaining"] = remaining; result["oldSourceDestroyed"] = destroyed;
        Sustainer? replacement = CurrentBiomeSustainers().FirstOrDefault(s => ReferenceEquals(s.def, sound)
            && !ReferenceEquals(s, originalSustainer) && !s.Ended);
        SampleSustainer? sample = replacement == null ? null : SamplesFor(replacement)
            .FirstOrDefault(item => item.source != null && ReferenceEquals(item.source.clip, clip));
        if (!ended || !removed || remaining != 0 || !destroyed || sample == null || sample.source == null || !sample.source.isPlaying)
        {
            if (Time.realtimeSinceStartup - navigationStartedAt > 30f)
                throw new TimeoutException("Native map navigation did not end/clean the original ambience and recreate playback within 30 seconds.");
            return false;
        }
        var value = Snapshot();
        ValidatePreparation(value);
        Require(ReferenceEquals(holder.contentList[SoundName], clip) && Manager.GetAudioClipLoadState(clip) == AudioDataLoadState.Loaded,
            "Native navigation changed the original loaded clip.");
        preReloadSustainer = replacement!;
        preReloadSource = sample.source;
        result["completedFrame"] = Time.frameCount; result["afterNavigation"] = value;
        result["nativeEndCleanupAndReplacementPassed"] = true;
        navigationPassed = true;
        return true;
    }

    // Reload replaces SoundRoot and destroys its Unity sources; abandoned
    // managed samples need not receive End. Poll actual destruction and native
    // replacement playback without driving sound lifecycle or reader methods.
    internal static bool TryAfterGameplayReload()
    {
        if (reloadPassed) return true;
        if (!reloadObservationStarted) { reloadObservationStarted = true; reloadStartedAt = Time.realtimeSinceStartup; }
        Require(playbackPassed && navigationPassed && !ReferenceEquals(Find.CurrentMap, originalMap)
            && ReferenceEquals(Find.CurrentMap.Biome, biome), "Advance ambience requires actual native map reload.");
        bool oldEnded = preReloadSustainer.Ended;
        bool removed = !CurrentBiomeSustainers().Contains(preReloadSustainer)
            && !Find.SoundRoot.sustainerManager.AllSustainers.Contains(preReloadSustainer);
        bool oldSourceDestroyed = preReloadSource == null;
        int oldSamplesRemaining = ((List<SubSustainer>)Subs.GetValue(preReloadSustainer))
            .Sum(sub => ((List<SampleSustainer>)Samples.GetValue(sub)).Count);
        Report["oldSustainerEnded"] = oldEnded; Report["oldSustainerRemoved"] = removed;
        Report["oldSampleSourceDestroyed"] = oldSourceDestroyed;
        Report["oldSamplesRemaining"] = oldSamplesRemaining;
        Report["reloadRequiredEndCall"] = false;
        Report["reloadObservationFrame"] = Time.frameCount;
        Sustainer? replacement = CurrentBiomeSustainers().FirstOrDefault(s => ReferenceEquals(s.def, sound)
            && !ReferenceEquals(s, preReloadSustainer) && !s.Ended);
        SampleSustainer? replacementSample = replacement == null ? null : SamplesFor(replacement)
            .FirstOrDefault(sample => sample.source != null && ReferenceEquals(sample.source.clip, clip));
        bool ready = removed && oldSourceDestroyed
            && replacementSample != null && replacementSample.source != null && replacementSample.source.isPlaying;
        if (!ready)
        {
            if (Time.realtimeSinceStartup - reloadStartedAt > 30f)
                throw new TimeoutException("Native reload did not finish old ambience cleanup and replacement playback within 30 seconds.");
            return false;
        }
        Require(ReferenceEquals(holder.contentList[SoundName], clip) && Manager.GetAudioClipLoadState(clip) == AudioDataLoadState.Loaded,
            "Native reload did not recreate ambience with the same original loaded clip.");
        var value = Snapshot();
        ValidatePreparation(value);
        Report["afterGameplayReload"] = value; Report["advanceAfterReload"] = AdvanceSnapshot();
        Report["nativeReloadRecreatedSustainer"] = true;
        reloadPassed = true;
        return true;
    }

    private static void ValidatePreparation(Dictionary<string, object> value)
    {
        var reader = Reader(value);
        Require((bool)reader["nativeReady"] && !(bool)reader["terminallyFaulted"] && Number(reader, "replayMismatches") == 0
            && Number(reader, "decoderConstructions") == 1, "Native ambience reader is not ready without a replay failure.");
        Require(((object[])reader["trace"]).Cast<Dictionary<string, object>>().All(row => (bool)row["completed"]),
            "Native ambience contains a failed captured reader or position request.");
        Require(Number(value, "selectedPlaybackCalls") > 0 && Equals(value["lastSelectedPlaybackCause"], "sustainer"),
            "The original sustained-sample clip assignment was not observed.");
        if (warm)
            Require(Number(value, "readinessAttempts") == 1 && Equals(value["readinessCause"], "advance-biome")
                && Number(value, "advanceGeneration") > 0 && Number(value, "advancePreparedOrdinal") > 0
                && Number(value, "advancePreparedOrdinal") < Number(value, "firstSelectedPlaybackOrdinal")
                && Number(value, "advancePreparedThread") != mainThread && Number(value, "advancePreparedThread") > 0
                && Number(reader, "firstReconstructionThread") == Number(value, "advancePreparedThread")
                && Equals(reader["firstReconstructionCause"], "readiness-advance-biome"),
                "Warm ambience was not prepared by the loading worker before the native selected-sample call.");
        else Require(Number(value, "readinessAttempts") == 0 && Number(value, "advancePreparedOrdinal") == 0,
            "Cold advance preparation unexpectedly reconstructed an already-native reader.");
    }

    private static List<Sustainer> CurrentBiomeSustainers() => (List<Sustainer>)BiomeSustainers.GetValue(null);
    private static Sustainer CurrentSustainer() => CurrentBiomeSustainers().Single(s => ReferenceEquals(s.def, sound) && !s.Ended);
    private static IEnumerable<SampleSustainer> SamplesFor(Sustainer sustainer) => ((List<SubSustainer>)Subs.GetValue(sustainer))
        .SelectMany(sub => (List<SampleSustainer>)Samples.GetValue(sub));
    private static SampleSustainer CurrentSample(Sustainer sustainer) => SamplesFor(sustainer)
        .First(sample => sample.source != null && ReferenceEquals(sample.source.clip, clip));
    private static Dictionary<string, object> Snapshot() => (Dictionary<string, object>)snapshot.Invoke(null, new object[] { clip });
    private static Dictionary<string, object> AdvanceSnapshot() => (Dictionary<string, object>)
        ((Dictionary<string, object>)runtimeSnapshot.Invoke(null, null))["advance"];
    private static Dictionary<string, object> Reader(Dictionary<string, object> value) => (Dictionary<string, object>)value["reader"];
    private static long Number(Dictionary<string, object> value, string key) => Convert.ToInt64(value[key], CultureInfo.InvariantCulture);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
