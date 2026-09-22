// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RimWorld;
using RuntimeAudioClipLoader;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace FixtureMenuObserver;

// Only existing native content is consumed. Reader diagnostics are snapshots;
// this probe never creates clips/definitions, reads samples or settles readers.
internal static class C10NaturalAudioProbe
{
    private sealed class Content
    {
        internal string Package = "", Key = "";
        internal ModContentHolder<AudioClip> Holder = null!;
        internal AudioClip Clip = null!;
        internal Dictionary<string, object> Menu = null!;
    }
    private static readonly List<Content> contents = new();
    private static readonly List<object> progress = new();
    private static readonly Dictionary<string, object> report = new();
    private static string directory = "", failure = "";
    private static MethodInfo snapshot = null!;
    private static Content? selected;
    private static SongDef? song;
    private static MusicManagerPlay? music;
    private static AudioSource? source;
    private static bool warm, contentPassed, playbackPassed, reloadPassed, playing, finished;
    private static int mainThread, startFrame, firstProgressFrame;
    private static long initialBytes, firstProgressBytes, lastRecordedBytes;
    private static float startedAt;
    private static SampleOneShot? wavSample;
    private static Content? wavContent;
    private static bool wavProgress, wavRemoved;
    private static bool musicReadinessPassed, wavReadinessPassed, unrelatedReadinessPassed, referenceReadPassed;
    private static int wavStartFrame;
    private static bool Readiness => C10AdvanceAudioProbe.Selected || Mode()?.StartsWith("readiness-", StringComparison.Ordinal) == true;
    private static bool Formats => Mode()?.StartsWith("formats-", StringComparison.Ordinal) == true || Mode()?.StartsWith("frames-", StringComparison.Ordinal) == true;
    private static bool NativeFormats => Formats || Readiness;
    private static bool Wav => NativeFormats || Mode()?.StartsWith("wav-", StringComparison.Ordinal) == true;
    internal static bool Selected => Mode() != null;
    private static string? Mode()
    {
        string[] values = Environment.GetCommandLineArgs().Where(a => a.StartsWith("--fixture-c10-prepared-audio=", StringComparison.Ordinal)).ToArray();
        if (values.Length != 1) return null;
        string mode = values[0].Substring("--fixture-c10-prepared-audio=".Length);
        return new[] { "natural-prepare", "natural-warm", "wav-prepare", "wav-warm", "formats-prepare", "formats-warm", "frames-prepare", "frames-warm",
            "readiness-prepare", "readiness-warm", "advance-prepare", "advance-warm" }.Contains(mode) ? mode : null;
    }

    internal static void Menu(string output)
    {
        directory = output;
        try
        {
            report["mode"] = Mode() ?? throw new InvalidOperationException("Natural audio mode is absent.");
            warm = ((string)report["mode"]).EndsWith("-warm", StringComparison.Ordinal);
            Require(Environment.GetCommandLineArgs().Contains("--fixture-gameplay-smoke"), "Natural audio requires gameplay smoke.");
            mainThread = Thread.CurrentThread.ManagedThreadId;
            Assembly core = AppDomain.CurrentDomain.GetAssemblies().Single(a => !a.ReflectionOnly && a.GetName().Name == "WakeUp");
            snapshot = core.GetType("WakeUp.PreparedAudioRuntime", true)!.GetMethod("SnapshotForClip", BindingFlags.Public | BindingFlags.Static)!;
            report["runtimeAtMenu"] = core.GetType("WakeUp.PreparedAudioRuntime", true)!.GetMethod("Snapshot", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null)!;
            if (NativeFormats) C10NativeMp3Probe.Menu(core, warm);
            if (Readiness) Require(Flag((Dictionary<string, object>)report["runtimeAtMenu"], "readinessActive"),
                "Selected native audio readiness was not admitted: " + ((Dictionary<string, object>)report["runtimeAtMenu"])["readinessStatus"]);
            if (C10AdvanceAudioProbe.Selected) C10AdvanceAudioProbe.Menu(core, warm);
            foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
            {
                ModContentHolder<AudioClip> holder = mod.GetContentHolder<AudioClip>();
                foreach (var entry in holder.contentList.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    if (entry.Value == null) continue;
                    var observed = Snapshot(entry.Value);
                    if (observed != null) contents.Add(new Content { Package = mod.PackageId, Key = entry.Key,
                        Holder = holder, Clip = entry.Value, Menu = observed });
                }
            }
            report["menuInventory"] = contents.Select(c => (object)new Dictionary<string, object>
            { ["package"] = c.Package, ["key"] = c.Key, ["samples"] = c.Clip.samples,
                ["channels"] = c.Clip.channels, ["frequency"] = c.Clip.frequency,
                ["loadState"] = Manager.GetAudioClipLoadState(c.Clip).ToString(), ["snapshot"] = c.Menu }).ToArray();
            Require(contents.Count > 0, "No automatically loaded native clips are tracked.");
            song = DefDatabase<SongDef>.AllDefs.OrderBy(s => s.defName, StringComparer.Ordinal).FirstOrDefault(s =>
                s.clip != null && contents.Any(c => ReferenceEquals(c.Clip, s.clip) && Flag(c.Menu, "streaming")
                    && (!warm || Number(Reader(c.Menu), "decoderConstructions") == 0)));
            Require(song != null, "No existing resolved streaming SongDef retains an eligible natural clip.");
            selected = contents.First(c => ReferenceEquals(c.Clip, song!.clip));
            report["selectedSong"] = song!.defName;
            report["selectedClipPath"] = song.clipPath;
            report["selectedPackage"] = selected.Package;
            report["selectedHolderKey"] = selected.Key;
            report["mainThread"] = mainThread;
            Write();
        }
        catch (Exception error) { Abort(error); }
    }

    internal static void BeginGameplay()
    {
        if (finished || failure.Length != 0) return;
        try
        {
            Require(UnityData.IsInMainThread && Current.ProgramState == ProgramState.Playing && Current.Game != null, "Natural playback requires actual gameplay.");
            Require(Prefs.VolumeMaster == 0f && AudioListener.volume == 0f, "The fixture must already be muted by its native master listener.");
            if (C10AdvanceAudioProbe.Selected) C10AdvanceAudioProbe.Begin();
            Require(selected != null && song != null && ReferenceEquals(song.clip, selected.Clip)
                && ReferenceEquals(selected.Holder.Get(selected.Key), selected.Clip), "Naturally loaded song/holder identity changed before play.");
            var before = Snapshot(selected!.Clip) ?? throw new InvalidOperationException("Natural streaming reader lost its owner.");
            ValidateAutomatic(selected.Clip, before);
            var shortClips = contents.Where(c => !Flag(c.Menu, "streaming")).ToArray();
            Require(shortClips.Length > 0, "No automatically loaded short nonstreaming clip is tracked.");
            var shortProofs = new List<object>();
            foreach (Content c in shortClips)
            {
                var current = Snapshot(c.Clip) ?? throw new InvalidOperationException("Natural short reader lost its owner.");
                // Identical files may reuse a recording even within the cold
                // startup. Require an actual publisher, not every duplicate.
                if (!warm && Number(current, "recordingPublished") == 0) continue;
                ValidateAutomatic(c.Clip, current);
                var state = Reader(current);
                Require(Flag(current, "loadInBackground") && Number(state, "readCalls") > 0 && Number(state, "returnedBytes") > 0,
                    "Short audio did not pass through native background decoding.");
                var rows = ((object[])state["trace"]).Cast<Dictionary<string, object>>();
                Require(rows.Any(row => Equals(row["kind"], "read") && Flag(row, "completed") && Number(row, "thread") != mainThread),
                    "Short audio has no completed native worker read.");
                shortProofs.Add(new Dictionary<string, object> { ["package"] = c.Package, ["key"] = c.Key, ["snapshot"] = current });
            }
            Require(shortProofs.Count > 0, "No short clip supplies the required automatic cold recording or warm cache-hit proof.");
            report["shortAudio"] = shortProofs;
            contentPassed = true;
            if (Wav) StartWavSound();
            if (NativeFormats) C10NativeMp3Probe.Begin();
            music = Find.MusicManagerPlay;
            source = (AudioSource?)typeof(MusicManagerPlay).GetField("audioSource", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(music);
            Require(source != null, "The native gameplay music manager has not initialized its source.");
            // Recheck immediately before Play; native initialization might have
            // selected this song itself. Such a clip cannot prove first use here.
            before = Snapshot(selected.Clip)!;
            if (warm) Require(Number(Reader(before), "decoderConstructions") == 0, "Selected warm song was already reconstructed before playback.");
            report["beforePlayback"] = before;
            initialBytes = Number(Reader(before), "returnedBytes");
            lastRecordedBytes = initialBytes;
            startFrame = Time.frameCount;
            startedAt = Time.realtimeSinceStartup;
            playing = true;
            var unrelated = Readiness ? PendingSongsExcept(selected.Clip) : null;
            music.ForcePlaySong(song!, false);
            Require(ReferenceEquals(source!.clip, selected.Clip) && AudioListener.volume == 0f, "Native music source did not retain the selected master-muted clip.");
            if (Readiness)
            {
                var afterAssignment = Snapshot(selected.Clip)!;
                ValidateReadiness(afterAssignment, "music");
                musicReadinessPassed = true;
                report["afterMusicAssignment"] = afterAssignment;
                ObservePendingSongs(unrelated!, "music");
                VerifySongReferenceReads();
            }
            report["startFrame"] = startFrame;
            report["masterVolume"] = Prefs.VolumeMaster;
            report["listenerVolume"] = AudioListener.volume;
            report["sourceVolume"] = source.volume;
            Write();
        }
        catch (Exception error) { Abort(error); }
    }

    internal static bool AdvanceGameplay()
    {
        if (finished || failure.Length != 0) return false;
        try
        {
            Require(playing && source != null && music != null && selected != null, "Natural playback was not started.");
            Require(Prefs.VolumeMaster == 0f && AudioListener.volume == 0f, "Native playback is no longer muted.");
            Require(ReferenceEquals(source!.clip, selected!.Clip) && ReferenceEquals(music!.CurrentSong, song), "Native music changed the selected song during observation.");
            var current = Snapshot(selected.Clip)!;
            var state = Reader(current);
            if (Wav) ObserveWavSound();
            if (NativeFormats) C10NativeMp3Probe.Advance();
            if (C10AdvanceAudioProbe.Selected) C10AdvanceAudioProbe.Advance();
            long reads = Number(state, "readCalls");
            long bytes = Number(state, "returnedBytes");
            if (bytes > initialBytes && firstProgressFrame == 0) { firstProgressFrame = Time.frameCount; firstProgressBytes = bytes; }
            if (bytes > lastRecordedBytes)
            {
                if (progress.Count < 16) progress.Add(new Dictionary<string, object>
                { ["frame"] = Time.frameCount, ["sourceTime"] = source.time, ["readCalls"] = reads, ["returnedBytes"] = state["returnedBytes"],
                    ["decoderConstructions"] = state["decoderConstructions"] });
                lastRecordedBytes = bytes;
            }
            bool later = firstProgressFrame != 0 && Time.frameCount >= firstProgressFrame + 3 && bytes > firstProgressBytes && source.time >= 1f;
            if (later && (!Wav || wavProgress && wavRemoved) && (!NativeFormats || C10NativeMp3Probe.PlaybackPassed)
                && (!C10AdvanceAudioProbe.Selected || C10AdvanceAudioProbe.PlaybackPassed))
            {
                Require(source.isPlaying && music!.IsPlaying && Number(state, "replayMismatches") == 0, "Native streaming stopped or replay differed.");
                Require(!Flag(state, "terminallyFaulted") && ((object[])state["trace"]).Cast<Dictionary<string, object>>()
                    .Where(row => Equals(row["kind"], "read")).All(row => Flag(row, "completed")), "Native playback contains a failed reader request.");
                if (Readiness) ValidateReadiness(current, "music");
                else if (warm) Require(Number(state, "decoderConstructions") == 1 && Number(state, "firstReconstructionThread") > 0
                    && new[] { "read", "position-get", "position-set" }.Contains((string)state["firstReconstructionCause"]),
                    "Warm first reconstruction did not come from a native playback read/position request.");
                report["afterPlayback"] = current;
                report["lastPlaybackFrame"] = Time.frameCount;
                report["sourceTimeAfterPlayback"] = source.time;
                playbackPassed = true;
                Stop();
                if (Formats) C10NativeMp3Probe.Controls();
                if (Wav && !Readiness) C10Mp3ProviderProbe.Capture(directory,
                    @"Z:\Development\Large Projects\RimWorld Loading Optimizer\.rlo-test-instance\game\Mods\3457785624\Sounds\Things\LightSMG.mp3");
                finished = true;
                Write();
                return false;
            }
            if (Time.realtimeSinceStartup - startedAt > 30f) throw new TimeoutException("Muted native playback did not establish later-frame reader progress within the 30-second functional bound.");
            return true;
        }
        catch (Exception error) { Abort(error); return false; }
    }

    internal static void AfterGameplayReload()
    {
        if (!Selected || directory.Length == 0) return;
        try
        {
            Require(playbackPassed && selected != null && song != null, "Natural playback was not established before reload.");
            foreach (Content item in contents)
                Require(item.Clip != null && ReferenceEquals(item.Holder.Get(item.Key), item.Clip)
                    && Manager.GetAudioClipLoadState(item.Clip) == AudioDataLoadState.Loaded, "Natural clip ownership/load state changed across gameplay reload.");
            Require(ReferenceEquals(song!.clip, selected!.Clip), "The existing SongDef lost its original clip across reload.");
            report["afterGameplayReload"] = Snapshot(selected.Clip)!;
            if (NativeFormats) C10NativeMp3Probe.Reload();
            if (C10AdvanceAudioProbe.Selected) C10AdvanceAudioProbe.AfterGameplayReload();
            reloadPassed = true;
        }
        catch (Exception error) { failure += (failure.Length == 0 ? "" : "\n") + error; }
        Write();
    }

    private static void ValidateAutomatic(AudioClip clip, Dictionary<string, object> snapshot)
    {
        Require(Manager.GetAudioClipLoadState(clip) == AudioDataLoadState.Loaded, "Native clip is not in Loaded state.");
        var state = Reader(snapshot);
        Require(Number(state, "replayMismatches") == 0, "Automatic native replay differed.");
        if (warm) Require(Flag(snapshot, "cacheHit") && Flag(state, "warm") && Number(state, "cachedBytes") > 0
            && Number(state, "decoderConstructions") == 0, "Automatic warm content loading did not avoid native decoder construction.");
        else Require(!Flag(snapshot, "cacheHit") && Number(snapshot, "recordingPublished") > 0
            && Number(state, "decoderConstructions") == 1, "Automatic cold content loading did not publish native recording.");
    }
    private static void StartWavSound()
    {
        wavContent = contents.FirstOrDefault(c => Equals(c.Menu["format"], "wav") && Flag(c.Menu, "streaming") == NativeFormats
            && (!NativeFormats || c.Package == "tro.vwe.overhaul" && c.Key == "Things/AntiMaterialRifle"));
        Require(wavContent != null, "WAV mode requires an automatically loaded WAV clip with the selected streaming state.");
        ValidateAutomatic(wavContent!.Clip, Snapshot(wavContent.Clip)!);
        FieldInfo grains = typeof(SubSoundDef).GetField("resolvedGrains", BindingFlags.Instance | BindingFlags.NonPublic)!;
        SoundDef? definition = DefDatabase<SoundDef>.AllDefs.OrderBy(d => d.defName, StringComparer.Ordinal).FirstOrDefault(d => !d.sustain
            && d.subSounds.Any(s => ((IEnumerable<ResolvedGrain>)grains.GetValue(s)).OfType<ResolvedGrain_Clip>()
                .Any(g => ReferenceEquals(g.clip, wavContent.Clip))));
        Require(definition != null, "No existing resolved SoundDef uses the natural WAV clip.");
        var beforePlayback = Snapshot(wavContent.Clip)!;
        var unrelated = Readiness ? PendingSongsExcept(wavContent.Clip) : null;
        definition!.PlayOneShot(SoundInfo.InMap(new TargetInfo(Find.CurrentMap.Center, Find.CurrentMap)));
        wavSample = Find.SoundRoot.oneShotManager.PlayingOneShots.FirstOrDefault(s => s.subDef.parentDef == definition
            && ReferenceEquals(s.source.clip, wavContent.Clip));
        Require(wavSample != null && wavSample.source.isPlaying, "Native SoundDef playback did not retain the original WAV clip.");
        wavStartFrame = Time.frameCount;
        report["wavSound"] = new Dictionary<string, object> { ["definition"] = definition!.defName,
            ["package"] = wavContent.Package, ["holderKey"] = wavContent.Key,
            ["streaming"] = Flag(wavContent.Menu, "streaming"),
            ["beforePlayback"] = beforePlayback, ["startFrame"] = wavStartFrame };
        if (Readiness)
        {
            var afterAssignment = Snapshot(wavContent.Clip)!;
            ValidateReadiness(afterAssignment, "oneshot");
            wavReadinessPassed = true;
            ((Dictionary<string, object>)report["wavSound"])["afterAssignment"] = afterAssignment;
            ObservePendingSongs(unrelated!, "oneshot");
        }
    }
    private static void ObserveWavSound()
    {
        Require(wavSample != null && wavContent != null, "WAV native sample was not established.");
        bool active = Find.SoundRoot.oneShotManager.PlayingOneShots.Contains(wavSample!);
        var result = (Dictionary<string, object>)report["wavSound"];
        if (active && ReferenceEquals(wavSample!.source.clip, wavContent!.Clip) && wavSample.source.isPlaying
            && Time.frameCount > wavStartFrame && wavSample.source.time >= wavContent.Clip.length * 0.9f)
        {
            wavProgress = true;
            result["progressFrame"] = Time.frameCount;
            result["sourceTime"] = wavSample.source.time;
        }
        if (!active && wavProgress)
        {
            wavRemoved = true;
            result["leftNativeManagerFrame"] = Time.frameCount;
            result["completionReasonObserved"] = false;
            var after = Snapshot(wavContent!.Clip)!;
            if (NativeFormats)
            {
                var state = Reader(after);
                Require(Number(state, "replayMismatches") == 0 && !Flag(state, "terminallyFaulted"), "Streaming WAV native replay failed.");
                Require(Number(state, "returnedBytes") > Number(Reader((Dictionary<string, object>)result["beforePlayback"]), "returnedBytes"), "Streaming WAV has no native playback byte progress.");
                if (Readiness) ValidateReadiness(after, "oneshot");
                else if (warm) Require(Number(state, "decoderConstructions") == 1
                    && new[] { "read", "position-get", "position-set" }.Contains((string)state["firstReconstructionCause"]), "Streaming WAV did not construct its first lower native reader on playback demand.");
            }
            result["afterPlayback"] = after;
        }
    }

    private static void ValidateReadiness(Dictionary<string, object> value, string cause)
    {
        var state = Reader(value);
        Require(Flag(state, "nativeReady") && !Flag(state, "terminallyFaulted") && Number(state, "replayMismatches") == 0,
            "Selected native reader is not ready without a replay failure.");
        if (warm)
            Require(Number(value, "readinessAttempts") == 1 && Equals(value["readinessCause"], cause)
                && Number(state, "decoderConstructions") == 1 && Number(state, "firstReconstructionThread") == mainThread
                && Equals(state["firstReconstructionCause"], "readiness-" + cause),
                "Warm selected clip was not prepared once on the main thread before native playback.");
        else Require(Number(value, "readinessAttempts") == 0 && Number(state, "decoderConstructions") == 1,
            "Cold readiness unexpectedly reconstructed an already native reader.");
    }

    // Snapshot immediately around each selected playback, not from menu through
    // colony setup: native setup may legitimately have played other songs.
    private static Dictionary<Content, Dictionary<string, object>> PendingSongsExcept(AudioClip target)
    {
        var result = new Dictionary<Content, Dictionary<string, object>>();
        if (!warm) return result;
        var songs = DefDatabase<SongDef>.AllDefs.Select(s => s.clip).ToArray();
        foreach (Content content in contents.Where(c => !ReferenceEquals(c.Clip, target)
            && Flag(c.Menu, "streaming") && songs.Any(clip => ReferenceEquals(clip, c.Clip))))
        {
            var value = Snapshot(content.Clip)!;
            var state = Reader(value);
            if (Number(state, "decoderConstructions") == 0 && !Flag(state, "nativeReady") && !Flag(state, "disposed"))
                result.Add(content, value);
        }
        Require(result.Count > 0, "No unrelated pending streaming song remains before selected playback.");
        return result;
    }

    private static void ObservePendingSongs(Dictionary<Content, Dictionary<string, object>> before, string cause)
    {
        if (!warm) return;
        var proofs = new List<object>();
        foreach (var entry in before)
        {
            var after = Snapshot(entry.Key.Clip)!;
            Require(Number(Reader(after), "decoderConstructions") == 0 && !Flag(Reader(after), "nativeReady")
                && Number(after, "readinessAttempts") == 0,
                "Selected playback also reconstructed an unrelated pending streaming song.");
            proofs.Add(new Dictionary<string, object> { ["package"] = entry.Key.Package, ["key"] = entry.Key.Key,
                ["before"] = entry.Value, ["after"] = after });
        }
        report["unrelatedPendingAfter-" + cause] = proofs;
        unrelatedReadinessPassed = proofs.Count > 0;
    }

    private static void VerifySongReferenceReads()
    {
        SongDef retainedSong = song!;
        AudioClip retainedClip = selected!.Clip;
        FieldInfo field = typeof(SongDef).GetField("clip", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
        bool publicRead = ReferenceEquals(retainedSong.clip, retainedClip);
        bool reflectedRead = ReferenceEquals(field.GetValue(retainedSong), retainedClip);
        var worker = Task.Run(() => new Dictionary<string, object>
        {
            // Reflection retains a real CLR field read even when the existing
            // consumer prepatch rewrites compiled SongDef.clip field accesses.
            ["thread"] = Thread.CurrentThread.ManagedThreadId,
            ["reflectedReferenceSame"] = ReferenceEquals(field.GetValue(retainedSong), retainedClip)
        }).GetAwaiter().GetResult();
        Require(publicRead && reflectedRead && Flag(worker, "reflectedReferenceSame")
            && Number(worker, "thread") != mainThread, "Selected public/reflected/worker clip references changed.");
        referenceReadPassed = true;
        report["clipReferenceReads"] = new Dictionary<string, object> { ["publicReferenceSame"] = publicRead,
            ["reflectedReferenceSame"] = reflectedRead, ["worker"] = worker, ["workerUsedUnityApis"] = false };
    }
    private static void Stop()
    {
        if (!playing || music == null) return;
        music.Stop();
        playing = false;
        Require(!music.IsPlaying && source != null && !source.isPlaying && selected != null
            && ReferenceEquals(source.clip, selected.Clip), "Native Stop changed the retained clip or left playback active.");
    }
    internal static void Abort(Exception error)
    {
        if (!Selected || directory.Length == 0) return;
        failure += (failure.Length == 0 ? "" : "\n") + error;
        try { Stop(); } catch (Exception cleanup) { failure += "\nStop: " + cleanup; }
        finished = true;
        Write();
    }
    private static Dictionary<string, object>? Snapshot(AudioClip clip) => (Dictionary<string, object>?)snapshot.Invoke(null, new object[] { clip });
    private static Dictionary<string, object> Reader(Dictionary<string, object> value) => (Dictionary<string, object>)value["reader"];
    private static bool Flag(Dictionary<string, object> value, string key) => (bool)value[key];
    private static long Number(Dictionary<string, object> value, string key) => Convert.ToInt64(value[key], CultureInfo.InvariantCulture);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Write()
    {
        report["schema"] = "c10-natural-audio-functional.v1";
        report["failure"] = failure;
        report["naturalContentLoadingPassed"] = contentPassed;
        report["sustainedNativePlaybackPassed"] = playbackPassed;
        report["gameplaySaveReloadPassed"] = reloadPassed;
        report["wavContentPlaybackPassed"] = Wav && wavProgress && wavRemoved && reloadPassed && failure.Length == 0;
        report["nativeFormatsPassed"] = NativeFormats && C10NativeMp3Probe.PlaybackPassed && wavProgress && wavRemoved && reloadPassed && failure.Length == 0;
        if (NativeFormats) report["mp3"] = C10NativeMp3Probe.Report;
        bool readinessPassed = Readiness && musicReadinessPassed && wavReadinessPassed && referenceReadPassed
            && (!warm || unrelatedReadinessPassed) && playbackPassed && reloadPassed && failure.Length == 0;
        report["audioReadinessPassed"] = readinessPassed;
        if (C10AdvanceAudioProbe.Selected) report["advance"] = C10AdvanceAudioProbe.Report;
        report["audioAdvancePassed"] = C10AdvanceAudioProbe.Selected && C10AdvanceAudioProbe.Passed && failure.Length == 0;
        report["functionalPassed"] = failure.Length == 0 && contentPassed && playbackPassed && reloadPassed && (!Readiness || readinessPassed)
            && (!C10AdvanceAudioProbe.Selected || C10AdvanceAudioProbe.Passed);
        report["playbackProgress"] = progress;
        report["observerCreatedClipsOrDefinitions"] = false;
        report["observerReadSeekOrSettlement"] = false;
        report["nativeCallbackOrigin"] = Readiness
            ? "Playback reads inferred from unchanged native ownership and later-frame progress; selected decoder reconstruction is separately attributed to readiness."
            : "Inferred from unchanged native music ownership and reader progress; no direct Unity callback instrumentation.";
        report["audibleQualityClaim"] = false;
        report["performanceClaim"] = false;
        File.WriteAllText(Path.Combine(directory, "c10-prepared-audio-probe.json"), Json(report) + "\n", new UTF8Encoding(false));
    }
    internal static string Json(object? value)
    {
        if (value == null) return "null";
        if (value is string text)
        {
            var result = new StringBuilder("\"");
            foreach (char c in text)
                if (c == '\\' || c == '"') result.Append('\\').Append(c);
                else if (c < 32) result.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else result.Append(c);
            return result.Append('"').ToString();
        }
        if (value is bool flag) return flag ? "true" : "false";
        if (value is IDictionary<string, object> map) return "{" + string.Join(",", map.Select(p => Json(p.Key) + ":" + Json(p.Value))) + "}";
        if (value is IEnumerable items) return "[" + string.Join(",", items.Cast<object>().Select(Json)) + "]";
        return Convert.ToString(value, CultureInfo.InvariantCulture)!;
    }
}
