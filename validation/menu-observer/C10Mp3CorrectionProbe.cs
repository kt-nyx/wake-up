// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Linq;
using System.Security.Cryptography;
using NAudio.Wave;
using RimWorld.IO;
using RuntimeAudioClipLoader;
using UnityEngine;
using Verse;

namespace FixtureMenuObserver;

internal static class C10Mp3CorrectionProbe
{
    private static Type runtime = null!;
    private static AudioClip destroyed = null!;
    private static Dictionary<string, object> teardown = null!;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    internal static bool Selected => Array.Exists(Environment.GetCommandLineArgs(), a => a.StartsWith("--fixture-c10-prepared-audio=frames-", StringComparison.Ordinal));
    private static bool LifetimeSelected => Selected || Array.Exists(Environment.GetCommandLineArgs(), a => a.StartsWith("--fixture-c10-prepared-audio=formats-", StringComparison.Ordinal));

    internal static void Lifetime(Assembly core, string path)
    {
        if (!LifetimeSelected) return;
        runtime = core.GetType("WakeUp.PreparedAudioRuntime", true)!;
        var report = new Dictionary<string, object>();
        C10NativeMp3Probe.Report["lifetimeCorrection"] = report;
        report["indeterminateInspection"] = InspectFailure(core, path);
        var before = State();
        var file = File(path);
        var source = (FileStream)file.CreateReadStream();
        var holder = new ModContentHolder<AudioClip>(null!);
        try
        {
            destroyed = Manager.Load(source, AudioFormat.mp3, "C10 owned streaming MP3 teardown", true, false, false);
            Require(destroyed != null, "Native streaming MP3 load returned no clip.");
            holder.contentList.Add("stream", destroyed!);
            holder.extraDisposables.Add(source);
            object reader = runtime.GetMethod("NativeReaderForClip", Static)!.Invoke(null, new object[] { destroyed! })!;
            var buffer = new float[4096];
            reader.GetType().GetMethod("Read", new[] { typeof(float[]), typeof(int), typeof(int) })!.Invoke(reader, new object[] { buffer, 0, buffer.Length });
            var frames = Frames(destroyed!);
            Require(Count(frames, "cachedFrames") > 0 && Count(frames, "historyBytes") > 0, "Streaming teardown did not hold skipped MP3 history.");
            Require(Count(State(), "pending") == Count(before, "pending") + 1, "MP3 streaming registration is missing or duplicated.");
            teardown = new Dictionary<string, object> { ["before"] = before, ["framesBefore"] = frames };
            report["streamingHolder"] = teardown;
            holder.ClearDestroy();
            var after = Frames(destroyed!);
            Require(!source.CanRead && holder.contentList.Count == 0 && holder.extraDisposables.Count == 0, "Native holder did not close its original source.");
            Require(Count(State(), "pending") == Count(before, "pending") && Count(after, "historyBytes") == 0
                && Count(after, "replayFrames") == Count(frames, "replayFrames"), "Source teardown retained registration or decoded skipped history.");
            teardown["afterSourceClose"] = after;
            teardown["sourceClosedInNativeHolder"] = true;
        }
        finally { if (holder.contentList.Count != 0) holder.ClearDestroy(); else source.Dispose(); }

        // Exercise the real factory between its actual Load prefix/finalizer,
        // injecting failure before a clip or worker has been published. This
        // requires no patch of the native consumer and preserves its exception.
        using var failedSource = (FileStream)File(path).CreateReadStream();
        object?[] arguments = { failedSource, AudioFormat.mp3, false, false, false, null };
        runtime.GetMethod("BeforeLoad", Static)!.Invoke(null, arguments);
        object frame = arguments[5]!;
        var expected = new InvalidOperationException("C10 injected failure after native reader construction, before publication");
        object? outer = null;
        object? returnedError = null;
        long pendingBefore = Count(State(), "pending");
        try
        {
            Type native = typeof(Manager).Assembly.GetType("RuntimeAudioClipLoader.CustomAudioFileReader", true)!;
            outer = Activator.CreateInstance(native, new object[] { failedSource, AudioFormat.mp3 });
            Require(Count(State(), "pending") == pendingBefore + 1, "Failed-load control did not create an actual admitted MP3 session.");
        }
        finally { returnedError = runtime.GetMethod("AfterLoad", Static)!.Invoke(null, new object?[] { null, expected, frame }); }
        Require(ReferenceEquals(returnedError, expected) && failedSource.CanRead && Count(State(), "pending") == pendingBefore,
            "Failed unpublished MP3 load changed its error, disposed borrowed input, or retained registration.");
        var session = frame.GetType().GetField("Mp3Frames", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(frame)!;
        var failed = (Dictionary<string, object>)session.GetType().GetMethod("Snapshot", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, null)!;
        Require((bool)failed["disposed"] && !(bool)failed["retainsPrivateReader"] && !(bool)failed["retainsPrivateDecoder"] && Count(failed, "replayFrames") == 0, "Failed load did not release its private decoder without catch-up.");
        report["unpublishedFailure"] = new Dictionary<string, object> { ["frames"] = failed, ["borrowedSourceOpen"] = true,
            ["originalErrorRetained"] = true, ["injectionBoundary"] = "real factory within actual Load prefix/finalizer, before clip publication" };
    }

    internal static void AfterReload()
    {
        if (!LifetimeSelected) return;
        Require(destroyed == null && teardown != null, "Native streaming clip destruction has not completed.");
        var after = Frames(destroyed!);
        var before = (Dictionary<string, object>)teardown!["framesBefore"];
        Require((bool)after["disposed"] && !(bool)after["retainsPrivateReader"] && !(bool)after["retainsPrivateDecoder"] && Count(after, "historyBytes") == 0 && Count(after, "replayFrames") == Count(before, "replayFrames"),
            "Destroyed streaming MP3 retained private decoder resources or replayed history.");
        teardown["afterDestroyedCleanup"] = after;
        teardown["passed"] = true;
    }

    private static Dictionary<string, object> InspectFailure(Assembly core, string path)
    {
        // Fail only the product's private reflection handle. The native table
        // and its writers remain intact; no native getter or patch is replaced.
        Type helper = core.GetType("WakeUp.PreparedAudioNative", true)!;
        FieldInfo field = helper.GetField("LoadStates", Static)!;
        object original = field.GetValue(null)!;
        MethodInfo cleanup = runtime.GetMethod("CleanupDestroyed", Static)!;
        MethodInfo inspect = helper.GetMethod("InspectLoadState", Static)!;
        AudioClip? clip = null;
        using var source = (FileStream)File(path).CreateReadStream();
        var result = new Dictionary<string, object>();
        try
        {
            field.SetValue(null, null);
            try
            {
                clip = Manager.Load(source, AudioFormat.mp3, "C10 indeterminate inspection", true, false, false);
                Require(clip != null && Owned(clip!), "Indeterminate completion lost the returned native clip ownership.");
                Require(Equals(inspect.Invoke(null, new object[] { clip! })!.ToString(), "Indeterminate"), "Optional inspection failure did not report indeterminate.");
                cleanup.Invoke(null, null);
                var retained = Frames(clip!);
                Require(Owned(clip!) && !(bool)retained["disposed"] && (bool)retained["retainsPrivateReader"]
                    && (bool)retained["retainsPrivateDecoder"] && source.CanRead, "Indeterminate cleanup released a potentially live clip.");
                result["afterIndeterminateCleanup"] = retained;
                result["originalReturnedClipRetained"] = true;
                result["cleanupDidNotThrow"] = true;
            }
            finally { field.SetValue(null, original); }
            Require(Equals(inspect.Invoke(null, new object[] { clip! })!.ToString(), "Active"), "Restored inspection did not observe the live clip.");
            cleanup.Invoke(null, null);
            Require(Owned(clip!), "Conclusive active retry lost live ownership.");
            typeof(Manager).GetMethod("SetAudioClipLoadState", Static)!.Invoke(null, new object[] { clip!, AudioDataLoadState.Failed });
            cleanup.Invoke(null, null);
            var released = Frames(clip!);
            Require(!Owned(clip!) && (bool)released["disposed"] && !(bool)released["retainsPrivateReader"]
                && !(bool)released["retainsPrivateDecoder"] && source.CanRead, "Conclusive failed retry did not release private ownership.");
            cleanup.Invoke(null, null);
            var repeated = Frames(clip!);
            Require(!Owned(clip!) && Count(repeated, "nativeFrames") == Count(released, "nativeFrames")
                && Count(repeated, "replayFrames") == Count(released, "replayFrames"), "Repeated cleanup revisited a released lifetime.");
            result["afterFailedRetry"] = released;
            result["inspectionRestored"] = ReferenceEquals(field.GetValue(null), original);
            result["activeRetryRetained"] = true;
            result["removedBeforeRepeatedCleanup"] = true;
            result["borrowedSourceOpen"] = source.CanRead;
            result["passed"] = true;
            return result;
        }
        finally
        {
            field.SetValue(null, original);
            if (clip != null) UnityEngine.Object.Destroy(clip);
        }

        bool Owned(AudioClip candidate)
        {
            foreach (object life in (IEnumerable)runtime.GetField("lives", Static)!.GetValue(null)!)
                if (ReferenceEquals(life.GetType().GetField("Clip", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(life), candidate)) return true;
            return false;
        }
    }

    internal static void CacheRecords(Assembly core, string path)
    {
        if (!Selected) return;
        Type frames = core.GetType("WakeUp.PreparedMp3FramesRuntime", true)!;
        MethodInfo attach = frames.GetMethod("Attach", Static)!;
        MethodInfo snapshot = frames.GetMethod("Snapshot", Static)!;
        string key = "C10 actual reset/chunk sequence " + Guid.NewGuid().ToString("N");
        var native = Run("native");
        var cold = Run("record");
        Require(Equals(native["samplesAndReads"], cold["samplesAndReads"]) && (bool)((Dictionary<string, object>)cold["frames"])["published"], "Repeated native read/reset recording failed.");
        var rows = new List<object> { native, cold };
        C10NativeMp3Probe.Report["actualChunkAndResetControls"] = rows;
        foreach (string mode in new[] { "warm", "missing", "corrupt", "incomplete" })
        {
            var row = Run(mode);
            rows.Add(row);
            var state = (Dictionary<string, object>)row["frames"];
            Require(Equals(native["samplesAndReads"], row["samplesAndReads"]) && Count(state, "cachedFrames") > 0,
                "Consumed MP3 cache boundary changed native reads: " + mode);
            Require(mode == "warm" ? Count(state, "replayFrames") == 0 && Count(state, "nativeFrames") == 1
                : Count(state, "cachedFrames") > 59 && Count(state, "replayFrames") > 0 && (bool)state["nativeOnly"]
                    && Equals(state["refusal"], "cache-operation-missing-or-corrupt"), "Cache boundary did not exercise its intended hit/fallback: " + mode);
        }

        Dictionary<string, object> Run(string mode)
        {
            var result = new Dictionary<string, object> { ["mode"] = mode };
            using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var reader = new Mp3FileReader(source);
            if (mode != "native") Require(attach.Invoke(null, new object[] { reader, key }) != null, "Chunk control reader was not admitted.");
            string? changedPath = null;
            byte[]? originalChunk = null;
            using var bytes = new MemoryStream();
            using var record = new BinaryWriter(bytes);
            try
            {
                var buffer = new byte[1028];
                int cycles = checked((int)(2200000L / reader.Length + 2));
                Require(cycles <= 32, "Chunk control source is too short for bounded repeated seeks.");
                for (int cycle = 0; cycle < cycles; cycle++)
                {
                    reader.Position = 0;
                    int count;
                    do
                    {
                        for (int i = 0; i < buffer.Length; i++) buffer[i] = 0xA5;
                        count = reader.Read(buffer, 3, 1022);
                        Require(buffer.Take(3).All(b => b == 0xA5) && buffer.Skip(1025).All(b => b == 0xA5), "Native MP3 read changed bytes outside its destination.");
                        record.Write(count); record.Write(reader.Position); record.Write(buffer, 3, count);
                        if (changedPath == null && mode != "native" && mode != "record" && mode != "warm")
                        {
                            C10Mp3MathProbe.PreserveEnvironment(() =>
                            {
                            var state = (Dictionary<string, object>)snapshot.Invoke(null, new object[] { reader })!;
                            if (Count(state, "cachedFrames") > 0)
                            {
                                object cache = frames.GetField("cache", Static)!.GetValue(null)!;
                                object manifest = cache.GetType().GetMethod("ReadMp3Manifest", BindingFlags.Instance | BindingFlags.NonPublic)!
                                    .Invoke(cache, new[] { state["cacheKey"] })!;
                                var chunks = (IList)manifest.GetType().GetField("Chunks", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manifest)!;
                                Require(chunks.Count >= 2, "Live corruption requires a later, not already loaded chunk.");
                                string chunkKey = (string)chunks[1]!.GetType().GetField("Key", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chunks[1])!;
                                changedPath = Path.Combine(GenFilePaths.SaveDataFolderPath, "WakeUp", "PreparedAudio", "v1", chunkKey + ".bin");
                                originalChunk = System.IO.File.ReadAllBytes(changedPath);
                                if (mode == "missing") System.IO.File.Delete(changedPath);
                                else
                                {
                                    byte[] changed = mode == "incomplete" ? originalChunk.Take(originalChunk.Length / 2).ToArray() : (byte[])originalChunk.Clone();
                                    if (mode == "corrupt") changed[changed.Length - 1] ^= 1;
                                    System.IO.File.WriteAllBytes(changedPath, changed);
                                }
                                result["changedChunkOrdinal"] = 1;
                            }
                            });
                        }
                    } while (count != 0);
                }
            }
            finally
            {
                reader.Dispose();
                if (changedPath != null) System.IO.File.WriteAllBytes(changedPath, originalChunk!);
            }
            Require(source.CanRead, "Chunk control disposed borrowed input.");
            using var hash = SHA256.Create();
            result["samplesAndReads"] = BitConverter.ToString(hash.ComputeHash(bytes.ToArray())).Replace("-", "");
            if (mode != "native") result["frames"] = snapshot.Invoke(null, new object[] { reader })!;
            return result;
        }
    }

    private static VirtualFile File(string path)
    {
        Type type = typeof(Manager).Assembly.GetType("RimWorld.IO.FilesystemFile", true)!;
        return (VirtualFile)type.GetMethod("op_Implicit", Static)!.Invoke(null, new object[] { new FileInfo(path) })!;
    }
    private static Dictionary<string, object> State() => (Dictionary<string, object>)runtime.GetMethod("Snapshot", Static)!.Invoke(null, null)!;
    private static Dictionary<string, object> Frames(AudioClip clip) => (Dictionary<string, object>)((Dictionary<string, object>)runtime.GetMethod("Mp3SnapshotForClip", Static)!.Invoke(null, new object[] { clip })!)["frames"];
    private static long Count(Dictionary<string, object> value, string key) => Convert.ToInt64(value[key]);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
