// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NAudio.Wave;
using RimWorld;
using RuntimeAudioClipLoader;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace FixtureMenuObserver;

// Natural content/playback proof is separate from the explicitly owned native
// reader controls, which run only after playback and compare reads and seeks.
internal static class C10NativeMp3Probe
{
    private static MethodInfo snapshot = null!;
    private static AudioClip clip = null!;
    private static ModContentHolder<AudioClip> holder = null!;
    private static string key = "";
    private static SampleOneShot sample = null!;
    private static int startFrame;
    private static bool progress;
    private static Dictionary<string, object> natural = null!;
    private static readonly List<(AudioClip Clip, ModContentHolder<AudioClip> Holder, string Key, Dictionary<string, object> Receipt)> additional = new();
    internal static readonly Dictionary<string, object> Report = new();
    internal static bool PlaybackPassed { get; private set; }

    internal static void Menu(Assembly core, bool warm)
    {
        snapshot = core.GetType("WakeUp.PreparedAudioRuntime", true)!.GetMethod("Mp3SnapshotForClip")!;
        var inventory = new List<object>();
        foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
        foreach (var item in mod.GetContentHolder<AudioClip>().contentList)
        {
            if (item.Value == null) continue;
            var state = (Dictionary<string, object>?)snapshot.Invoke(null, new object[] { item.Value });
            if (state == null) continue;
            inventory.Add(new Dictionary<string, object> { ["package"] = mod.PackageId, ["key"] = item.Key,
                ["samples"] = item.Value.samples, ["channels"] = item.Value.channels,
                ["frequency"] = item.Value.frequency, ["snapshot"] = state });
            if (mod.PackageId == "tro.vwe.overhaul" && item.Key == "Things/LightSMG")
            { clip = item.Value; holder = mod.GetContentHolder<AudioClip>(); key = item.Key; natural = state; }
            else if (C10Mp3CorrectionProbe.Selected && state.TryGetValue("frames", out object otherFrames))
            {
                var frames = (Dictionary<string, object>)otherFrames;
                Require(warm ? Count(frames, "cachedFrames") > 0 : (bool)frames["published"], "Additional natural MP3 did not record/reuse frames.");
                var receipt = new Dictionary<string, object> { ["package"] = mod.PackageId, ["key"] = item.Key,
                    ["frames"] = frames, ["fullPcm"] = EqualSamples(item.Value, (string)state["sourcePath"]) };
                additional.Add((item.Value, mod.GetContentHolder<AudioClip>(), item.Key, receipt));
            }
        }
        Require(clip != null, "Ordinary MP3 content did not provide the authored LightSMG clip.");
        var index = (Dictionary<string, object>)natural["index"];
        Require((bool)index["loadSucceeded"] && Count(index, "scanEntries") == 1,
            "Ordinary MP3 native constructor did not complete the qualified index path.");
        Require(warm ? Count(index, "skippedScans") == 1 && Count(index, "nativeScans") == 0
            : Count(index, "nativeScans") == 1 && (bool)index["published"],
            "Ordinary MP3 loading did not establish the expected cold publication or warm skipped scan.");
        Report["menuInventory"] = inventory;
        Report["warm"] = warm;
        Report["naturalLoad"] = natural;
        if (Environment.GetCommandLineArgs().Any(a => a.StartsWith("--fixture-c10-prepared-audio=frames-", StringComparison.Ordinal)))
            Require(natural.ContainsKey("frames"), "Selected MP3 frame prototype was not admitted.");
        if (natural.TryGetValue("frames", out object frameValue))
        {
            var frames = (Dictionary<string, object>)frameValue;
            Require(Count(frames, "firstNativeConversions") == 1 && (bool)frames["disposed"], "MP3 retained decoder did not convert first and dispose normally.");
            Require(warm ? Count(frames, "cachedFrames") > 0 && Count(frames, "nativeFrames") == 1 && Count(frames, "replayFrames") == 0
                : (bool)frames["published"], "MP3 frame recording/reuse was not delivered: " + C10NaturalAudioProbe.Json(frames));
            using var reference = new NAudio.Wave.Mp3FileReader((string)natural["sourcePath"]);
            var samples = new NAudio.Wave.SampleProviders.SampleChannel(reference, false);
            var expected = new float[clip!.samples * clip.channels];
            int read = 0, next;
            while (read < expected.Length && (next = samples.Read(expected, read, Math.Min(997, expected.Length - read))) > 0) read += next;
            var actual = new float[expected.Length];
            Require(read == expected.Length && clip.GetData(actual, 0) && actual.SequenceEqual(expected), "Full native MP3 samples differ from the ordinary loaded clip.");
            Require(reference.WaveFormat.SampleRate == clip.frequency && reference.WaveFormat.Channels == clip.channels, "Native MP3 metadata differs.");
            var bytes = new byte[actual.Length * sizeof(float)]; Buffer.BlockCopy(actual, 0, bytes, 0, bytes.Length);
            using var hash = SHA256.Create();
            Report["fullPcm"] = new Dictionary<string, object> { ["samples"] = actual.Length,
                ["sha256"] = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", ""), ["nativeEqual"] = true };
        }
        Report["observerCreatedClipsOrDefinitions"] = false;
        Report["additionalNatural"] = additional.Select(item => item.Receipt).ToArray();
    }

    internal static void Begin()
    {
        Require(Manager.GetAudioClipLoadState(clip) == AudioDataLoadState.Loaded && ReferenceEquals(holder.Get(key), clip), "MP3 native ownership/load state changed.");
        SoundDef definition = DefDatabase<SoundDef>.GetNamed("VWE_Shot_LightSMG");
        FieldInfo grains = typeof(SubSoundDef).GetField("resolvedGrains", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Require(definition.subSounds.Any(s => ((IEnumerable<ResolvedGrain>)grains.GetValue(s)).OfType<ResolvedGrain_Clip>()
            .Any(g => ReferenceEquals(g.clip, clip))), "Authored MP3 SoundDef does not retain the ordinary loaded clip.");
        definition.PlayOneShot(SoundInfo.InMap(new TargetInfo(Find.CurrentMap.Center, Find.CurrentMap)));
        sample = Find.SoundRoot.oneShotManager.PlayingOneShots.FirstOrDefault(s => s.subDef.parentDef == definition && ReferenceEquals(s.source.clip, clip))!;
        Require(sample != null && sample.source.isPlaying, "Native MP3 sound did not start.");
        startFrame = Time.frameCount;
        Report["definition"] = definition.defName;
        Report["startFrame"] = startFrame;
        foreach (var item in additional)
        {
            SoundDef other = DefDatabase<SoundDef>.AllDefsListForReading.First(def => def.subSounds.Any(s =>
                ((IEnumerable<ResolvedGrain>?)grains.GetValue(s) ?? Enumerable.Empty<ResolvedGrain>()).OfType<ResolvedGrain_Clip>().Any(g => ReferenceEquals(g.clip, item.Clip))));
            other.PlayOneShot(SoundInfo.InMap(new TargetInfo(Find.CurrentMap.Center, Find.CurrentMap)));
            Require(Find.SoundRoot.oneShotManager.PlayingOneShots.Any(s => s.subDef.parentDef == other && ReferenceEquals(s.source.clip, item.Clip) && s.source.isPlaying),
                "Additional natural MP3 SoundDef did not start through the ordinary manager.");
            item.Receipt["definition"] = other.defName;
            item.Receipt["ordinaryPlaybackStarted"] = true;
        }
    }

    internal static void Advance()
    {
        if (PlaybackPassed) return;
        bool active = Find.SoundRoot.oneShotManager.PlayingOneShots.Contains(sample);
        if (active && ReferenceEquals(sample.source.clip, clip) && sample.source.isPlaying
            && Time.frameCount > startFrame && sample.source.time >= clip.length * .9f)
        { progress = true; Report["progressFrame"] = Time.frameCount; Report["sourceTime"] = sample.source.time; }
        if (!active && progress)
        { PlaybackPassed = true; Report["leftNativeManagerFrame"] = Time.frameCount; Report["completionReasonObserved"] = false; }
    }

    internal static void Reload()
    {
        Require(PlaybackPassed && ReferenceEquals(holder.Get(key), clip)
            && Manager.GetAudioClipLoadState(clip) == AudioDataLoadState.Loaded, "MP3 native clip did not survive gameplay reload.");
        if (!C10PublicPatchProbe.Selected) C10Mp3CorrectionProbe.AfterReload();
        foreach (var item in additional)
        {
            Require(ReferenceEquals(item.Holder.Get(item.Key), item.Clip) && Manager.GetAudioClipLoadState(item.Clip) == AudioDataLoadState.Loaded,
                "Additional natural MP3 clip changed across gameplay reload.");
            item.Receipt["gameplayReloadPassed"] = true;
        }
        Report["gameplayReloadPassed"] = true;
    }

    internal static void Controls()
    {
        Assembly core = snapshot.DeclaringType!.Assembly;
        if (C10PublicPatchProbe.Selected) { C10PublicPatchProbe.Run(core); return; }
        Type runtime = core.GetType("WakeUp.PreparedMp3IndexRuntime", true)!;
        MethodInfo push = runtime.GetMethod("PushContext", BindingFlags.Static | BindingFlags.NonPublic)!;
        MethodInfo pop = runtime.GetMethod("PopContext", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assembly native = AppDomain.CurrentDomain.GetAssemblies().Single(a => !a.ReflectionOnly && a.GetName().Name == "NAudio");
        Type readerType = native.GetType("NAudio.Wave.Mp3FileReader", true)!;
        string path = (string)natural["sourcePath"];
        C10Mp3CorrectionProbe.Lifetime(core, path);
        C10Mp3LastErrorProbe.Run(core, path);
        C10Mp3CorrectionProbe.CacheRecords(core, path);
        var control = Run(false);
        var reused = Run(true);
        var reusedIndex = (Dictionary<string, object>)reused["index"];
        Require(Count(reusedIndex, "skippedScans") == 1 && Count(reusedIndex, "nativeScans") == 0,
            "Explicit native MP3 control did not actually skip the index scan.");
        Require(Equals(control["operations"], reused["operations"])
            && Equals(control["metadata"], reused["metadata"])
            && Equals(control["invalidDestination"], reused["invalidDestination"]) && !Equals(control["invalidDestination"], "returned"), "Native MP3 index reuse changed decoded output, seeking or metadata.");
        Report["explicitNativeControl"] = new Dictionary<string, object> { ["native"] = control, ["reusedIndex"] = reused,
            ["passed"] = true, ["providerSelectionChanged"] = false, ["performanceClaim"] = false };
        C10Mp3MathProbe.Run(core, path);

        Dictionary<string, object> Run(bool useCache)
        {
            var result = new Dictionary<string, object>();
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            object? previous = null;
            bool context = false, success = false;
            IDisposable? reader = null;
            try
            {
                if (useCache) { previous = push.Invoke(null, new object[] { stream, natural["cacheKey"] }); context = true; }
                try { reader = (IDisposable)readerType.GetConstructor(new[] { typeof(Stream) })!.Invoke(new object[] { stream }); success = true; }
                finally { if (context) { result["index"] = pop.Invoke(null, new object?[] { previous, success })!; context = false; } }
                if (useCache)
                    core.GetType("WakeUp.PreparedMp3FramesRuntime", true)!.GetMethod("Attach", BindingFlags.NonPublic | BindingFlags.Static)!
                        .Invoke(null, new object[] { reader, natural["cacheKey"] });
                PropertyInfo position = readerType.GetProperty("Position")!;
                long length = Convert.ToInt64(readerType.GetProperty("Length")!.GetValue(reader, null));
                object format = readerType.GetProperty("WaveFormat")!.GetValue(reader, null)!;
                object mp3Format = readerType.GetProperty("Mp3WaveFormat")!.GetValue(reader, null)!;
                string Format(object value) => string.Join("|", new[] { "Encoding", "Channels", "SampleRate", "BitsPerSample", "BlockAlign", "AverageBytesPerSecond", "ExtraSize" }
                    .Select(n => Convert.ToString(value.GetType().GetProperty(n)!.GetValue(value, null), CultureInfo.InvariantCulture)));
                result["metadata"] = length + ":" + Format(format) + ":" + Format(mp3Format);
                var operations = new List<string>();
                MethodInfo read = readerType.GetMethod("Read", new[] { typeof(byte[]), typeof(int), typeof(int) })!;
                int block = Convert.ToInt32(format.GetType().GetProperty("BlockAlign")!.GetValue(format, null));
                foreach (long at in new[] { 0L, length / 2 / block * block, 0L, Math.Max(0, length - 4096) / block * block, length })
                {
                    position.SetValue(reader, at, null);
                    operations.Add("seek:" + at + ":" + position.GetValue(reader, null));
                    var bytes = new byte[4096];
                    int returned = (int)read.Invoke(reader, new object[] { bytes, 0, bytes.Length })!;
                    using var hash = SHA256.Create();
                    operations.Add("read:" + returned + ":" + position.GetValue(reader, null) + ":" + BitConverter.ToString(hash.ComputeHash(bytes, 0, returned)).Replace("-", ""));
                }
                result["operations"] = string.Join("\n", operations);
                position.SetValue(reader, 0, null);
                var invalid = new byte[64];
                try { read.Invoke(reader, new object[] { invalid, -1, invalid.Length }); result["invalidDestination"] = "returned"; }
                catch (TargetInvocationException error) { result["invalidDestination"] = error.InnerException!.GetType().FullName + ":" + error.InnerException.HResult; }
            }
            finally
            {
                if (context) pop.Invoke(null, new object?[] { previous, false });
                reader?.Dispose();
                if (useCache && reader != null)
                    result["frames"] = core.GetType("WakeUp.PreparedMp3FramesRuntime", true)!.GetMethod("Snapshot", BindingFlags.NonPublic | BindingFlags.Static)!
                        .Invoke(null, new object[] { reader })!;
                Require(stream.CanRead, "Native borrowed MP3 source was incorrectly disposed.");
                result["nativeReaderDisposed"] = true;
                result["borrowedSourceRemainedOpen"] = true;
            }
            return result;
        }
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static Dictionary<string, object> EqualSamples(AudioClip clip, string path)
    {
        using var reader = new Mp3FileReader(path);
        var channel = new NAudio.Wave.SampleProviders.SampleChannel(reader, false);
        var expected = new float[clip.samples * clip.channels];
        var actual = new float[expected.Length];
        int read = 0, next;
        while (read < expected.Length && (next = channel.Read(expected, read, Math.Min(997, expected.Length - read))) > 0) read += next;
        Require(read == expected.Length && clip.GetData(actual, 0) && actual.SequenceEqual(expected)
            && reader.WaveFormat.SampleRate == clip.frequency && reader.WaveFormat.Channels == clip.channels,
            "Additional natural MP3 samples or metadata differ from original native loading.");
        var bytes = new byte[actual.Length * sizeof(float)]; Buffer.BlockCopy(actual, 0, bytes, 0, bytes.Length);
        using var hash = SHA256.Create();
        return new Dictionary<string, object> { ["samples"] = actual.Length, ["channels"] = clip.channels, ["frequency"] = clip.frequency,
            ["nativeEqual"] = true, ["sha256"] = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "") };
    }
    private static long Count(Dictionary<string, object> value, string key) => Convert.ToInt64(value[key], CultureInfo.InvariantCulture);
}
