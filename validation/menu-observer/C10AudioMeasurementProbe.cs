// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using RimWorld;
using RuntimeAudioClipLoader;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace FixtureMenuObserver;

// Sparse observations of existing assets and normal game consumers. No reference
// decoding, sample hashes, injected failures or cache/sentinel controls run here.
internal static class C10AudioMeasurementProbe
{
    internal static string? Mode => Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--fixture-c10-audio-measurement=", StringComparison.Ordinal))?.Split('=')[1];
    internal static bool Selected => Mode != null;
    internal static bool BootstrapExpected => Mode != "absent";
    private static readonly Dictionary<string, object> report = new();
    private static string directory = "";
    private static long launch, frequency, requested;
    private static MethodInfo runtime = null!, clipState = null!, mp3State = null!;
    private static SongDef song = null!;
    private static SoundDef sound = null!;
    private static AudioClip mp3 = null!;
    private static AudioSource source = null!;
    private static SampleOneShot sample = null!;
    private static int requestFrame;
    [DllImport("kernel32.dll")] private static extern bool QueryPerformanceCounter(out long value);
    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string name);
    private static long Counter() { if (!QueryPerformanceCounter(out long value)) throw new InvalidOperationException("QPC unavailable."); return value; }
    private static double Seconds(long counter) => (counter - launch) / (double)frequency;

    internal static void Menu(string output)
    {
        directory = output;
        string[] clock = File.ReadAllLines(Path.Combine(output, "launch-clock.txt"));
        frequency = long.Parse(clock[1], System.Globalization.CultureInfo.InvariantCulture);
        launch = long.Parse(clock[2], System.Globalization.CultureInfo.InvariantCulture);
        report["schema"] = "c10-audio-measurement.v1"; report["mode"] = Mode!;
        report["completed"] = false; report["failure"] = "";
        report["diagnosticDecodingEnabled"] = false;
        bool registryPresent = AppDomain.CurrentDomain.GetData("WakeUp.PreparedAudio.Bootstrap.v1") != null;
        bool assemblyPresent = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "WakeUp.AudioBootstrap");
        bool nativePresent = GetModuleHandle("mono-profiler-wakeupentry.dll") != IntPtr.Zero;
        string[] arguments = Environment.GetCommandLineArgs();
        bool nativeRequested = arguments.Contains("wakeupentry") && arguments.Contains("-monoProfiler");
        report["bootstrapExpected"] = BootstrapExpected;
        report["bootstrapRegistryPresent"] = registryPresent;
        report["bootstrapAssemblyPresent"] = assemblyPresent;
        report["nativeModulePresent"] = nativePresent;
        report["nativeStartupRequested"] = nativeRequested;
        bool presencePassed = registryPresent == BootstrapExpected && assemblyPresent == BootstrapExpected
            && nativePresent == BootstrapExpected && nativeRequested == BootstrapExpected;
        report["bootstrapPresencePassed"] = presencePassed;
        Require(presencePassed, "Actual bootstrap/native presence differs from selected measurement arm.");
        Assembly core = AppDomain.CurrentDomain.GetAssemblies().Single(a => !a.ReflectionOnly && a.GetName().Name == "WakeUp");
        Type type = core.GetType("WakeUp.PreparedAudioRuntime", true)!;
        runtime = type.GetMethod("Snapshot")!; clipState = type.GetMethod("SnapshotForClip")!; mp3State = type.GetMethod("Mp3SnapshotForClip")!;
        song = DefDatabase<SongDef>.GetNamed("A_Place_of_Our_Own");
        sound = DefDatabase<SoundDef>.GetNamed("VWE_Shot_LightSMG");
        var holder = LoadedModManager.RunningModsListForReading.Single(m => m.PackageId == "tro.vwe.overhaul").GetContentHolder<AudioClip>();
        mp3 = holder.Get("Things/LightSMG");
        Require(song.clip != null && mp3 != null, "Authored measurement assets are unavailable.");
        report["song"] = song.defName; report["sound"] = sound.defName;
        report["menuObservationSeconds"] = Seconds(Counter());
        report["menu"] = Snapshot();
        var state = (Dictionary<string, object>)runtime.Invoke(null, null)!;
        Require((bool)state["enabled"] == (Mode == "cold" || Mode == "warm"), "Audio measurement selector did not control runtime admission.");
        Write();
    }

    internal static void BeginGameplay()
    {
        report["colonyReadySeconds"] = Seconds(Counter());
        Require(UnityData.IsInMainThread && Current.ProgramState == ProgramState.Playing && Find.CurrentMap != null,
            "First use requires the ordinary loaded colony.");
        Require(Prefs.VolumeMaster == 0f && AudioListener.volume == 0f, "Measurement fixture is not muted.");
        report["beforeFirstUse"] = Snapshot();
        var music = Find.MusicManagerPlay;
        source = (AudioSource)typeof(MusicManagerPlay).GetField("audioSource", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(music);
        requested = Counter(); requestFrame = Time.frameCount;
        music.ForcePlaySong(song, false);
        sound.PlayOneShot(SoundInfo.InMap(new TargetInfo(Find.CurrentMap!.Center, Find.CurrentMap)));
        report["firstUseCallsReturnedSeconds"] = Seconds(Counter());
        report["firstUseSynchronousSeconds"] = (Counter() - requested) / (double)frequency;
        sample = Find.SoundRoot.oneShotManager.PlayingOneShots.FirstOrDefault(s => s.subDef.parentDef == sound && ReferenceEquals(s.source.clip, mp3))!;
        Require(sample != null && sample.source.isPlaying && ReferenceEquals(source.clip, song.clip), "Normal audio consumers did not start selected assets.");
    }

    internal static bool AdvanceGameplay()
    {
        Require(ReferenceEquals(source.clip, song.clip) && source.isPlaying, "Selected music changed or failed.");
        if (Time.frameCount > requestFrame && source.time > 0f && sample.source.time > 0f)
        {
            report["firstPlaybackProgressSeconds"] = Seconds(Counter());
            report["firstUseToProgressSeconds"] = (Counter() - requested) / (double)frequency;
            report["afterFirstUse"] = Snapshot();
            report["naturalPlaybackPassed"] = true;
            // Same normal music stop in every arm; the authored one-shot ends
            // through its ordinary manager while the gameplay smoke continues.
            Find.MusicManagerPlay.Stop();
            Write();
            return false;
        }
        if ((Counter() - requested) / (double)frequency > 30) throw new TimeoutException("Selected audio made no playback progress.");
        return true;
    }

    internal static void Finish()
    {
        if (!Selected) return;
        report["gameplayAndReloadCompleteSeconds"] = Seconds(Counter());
        report["final"] = Snapshot();
        Require(report.TryGetValue("naturalPlaybackPassed", out object passed) && Equals(passed, true), "Natural playback did not complete.");
        report["completed"] = true;
        Write();
    }
    internal static void Fail(Exception error)
    {
        if (!Selected || directory.Length == 0) return;
        report["failure"] = error.ToString(); report["completed"] = false; Write();
    }
    private static Dictionary<string, object> Snapshot() => new Dictionary<string, object> {
        ["runtime"] = Sparse(runtime.Invoke(null, null)), ["song"] = Sparse(clipState.Invoke(null, new object[] { song.clip })),
        ["mp3"] = Sparse(mp3State.Invoke(null, new object[] { mp3 })) };
    private static object Sparse(object? value)
    {
        if (value is not Dictionary<string, object> dictionary) return value ?? "untracked";
        var result = new Dictionary<string, object>();
        foreach (string key in new[] { "enabled", "pending", "recordings", "cacheHits", "cacheMisses", "cachedBytes", "decoderConstructions",
            "sourceBytes", "sourceNativeHashCalls", "sourceManagedHashCalls", "replayMismatches", "cleanupFailures", "nativeOnly", "reason", "mp3CompletionFailures", "warm", "cacheHit", "streaming",
            "recordingPublished", "returnedBytes", "readCalls", "firstReconstructionCause", "firstReconstructionThread", "terminallyFaulted",
            "frames", "reader", "index", "mp3Index", "nativeFrames", "cachedFrames", "replayFrames", "historyBytes", "faulted", "refusal",
            "nativeScans", "skippedScans", "published", "publications", "loadSucceeded", "disposed" })
            if (dictionary.TryGetValue(key, out object item)) result[key] = Sparse(item);
        return result;
    }
    private static void Write() => File.WriteAllText(Path.Combine(directory, "c10-audio-measurement.json"), C10NaturalAudioProbe.Json(report), new System.Text.UTF8Encoding(false));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
