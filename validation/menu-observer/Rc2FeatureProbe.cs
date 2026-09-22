// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace FixtureMenuObserver;

// Private, bounded functional checks. No drawing, desktop input, scenes or playback.
internal static class Rc2FeatureProbe
{
    private static string? directory;
    private static Window? preparation;
    private static ModContentPack? provider;
    private static string logical = "", selection = "";
    private static FileInfo? source;
    private static byte[]? originalSource;
    private static int preset;
    private static long started;
    private static bool begun, finished;
    private static ModContentHolder<AudioClip>? audioHolder;
    private static ModContentPack? audioProvider;
    private static AudioClip? testedClip;
    private static string audioPath = "";
    private static Type? Product(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(a => a.GetName().Name == "WakeUp")?.GetType("WakeUp." + name);
    private static object? Read(Type? type, object? instance, string name) => type == null ? null
        : AccessTools.Field(type, name)?.GetValue(instance) ?? AccessTools.Property(type, name)?.GetValue(instance, null);
    private static object? Read(string type, string name) => Read(Product(type), null, name);
    private static object? Call(string type, string method, params object[] arguments) =>
        AccessTools.Method(Product(type), method).Invoke(null, arguments);
    private static string Quote(object? value) => "\"" + Convert.ToString(value, CultureInfo.InvariantCulture)!
        .Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
    private static void Receipt(string name, params object?[] pairs)
    {
        var fields = new List<string>();
        for (int i = 0; i < pairs.Length; i += 2)
        {
            object? value = pairs[i + 1];
            fields.Add(Quote(pairs[i]) + ":" + (value == null ? "null" : value is bool flag ? flag.ToString().ToLowerInvariant()
                : value is int || value is long ? Convert.ToString(value, CultureInfo.InvariantCulture) : Quote(value)));
        }
        File.AppendAllText(Path.Combine(directory!, name + ".jsonl"), "{" + string.Join(",", fields) + "}\n");
    }
    private static void Status(string phase)
    {
        foreach (string type in new[] { "StreamingXmlRuntime", "DeferredAudioRuntime", "PreparedTextureRuntime", "LoadingTimingRuntime", "NativeLoadingSummaryRuntime", "BackgroundLoadingRuntime" })
        {
            if (Product(type) == null) continue;
            var fields = new List<object?> { "phase", phase, "feature", type };
            foreach (string key in new[] { "Status", "WorldStatus", "ColonyStatus", "installed", "enabled", "finished", "PendingCount", "completed", "refused", "failures", "exposures", "UseAtStartup", "Hits", "Misses", "Refused", "hidden" })
            {
                object? value = Read(type, key);
                if (value != null) { fields.Add(key); fields.Add(value); }
            }
            Receipt("rc2-status", fields.ToArray());
        }
        string? report = Read("LoadingTimingRuntime", "Report") as string;
        if (!string.IsNullOrEmpty(report)) File.WriteAllText(Path.Combine(directory!, "rc2-loading-report-" + phase + ".txt"), report);
    }
    internal static bool Advance()
    {
        if (!Environment.GetCommandLineArgs().Contains("--fixture-functional-probes")) return false;
        try
        {
            if (!begun)
            {
                begun = true;
                directory = Path.Combine(GenFilePaths.SaveDataFolderPath, "FixtureMenuObserver");
                started = Stopwatch.GetTimestamp();
                Status("menu");
                UnityData.DisposeStatic += Released;
                try { Audio(); }
                catch (Exception exception) { Receipt("rc2-audio", "passed", false, "error", exception.ToString()); }
                try { BeginPreparation(); }
                catch (Exception exception)
                {
                    Receipt("rc2-preparation", "passed", false, "error", exception.ToString());
                    ClosePreparation();
                }
            }
            if (finished || preparation == null) return false;
            if ((Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency >= 40)
            {
                Receipt("rc2-preparation", "passed", false, "preset", preset, "reason", "bounded-controller-timeout");
                ClosePreparation();
                return false;
            }
            preparation.WindowUpdate();
            Type type = preparation.GetType();
            if ((bool)Read(type, preparation, "scanning")! || (bool)Read(type, preparation, "running")!) return true;
            int completed = (int)Read(type, preparation, "completed")!, reused = (int)Read(type, preparation, "reused")!;
            int failed = (int)Read(type, preparation, "failed")!;
            bool published = completed + reused == 1 && failed == 0;
            Receipt("rc2-preparation", "passed", published, "preset", preset, "provider", provider!.PackageId, "path", logical,
                "prepared", completed, "reused", reused, "failed", failed,
                "status", Read(type, preparation, "status"));
            if (published)
            {
                if (preset == 0) VerifyPrepared(); else VerifyExport();
                // This bounded test selects the reviewed carpet artwork for
                // export only; no exported pixels replace native game content.
                if (preset < 3 && source != null && Path.GetFileNameWithoutExtension(source.Name).Equals("VME_TaoistCarpet", StringComparison.OrdinalIgnoreCase))
                {
                    ClosePreparation(false);
                    preset++;
                    StartController();
                    return true;
                }
            }
            ClosePreparation();
        }
        catch (Exception exception)
        {
            try { Receipt("rc2-probe-error", "passed", false, "error", exception.ToString()); } catch { }
            ClosePreparation();
        }
        return false;
    }
    internal static void BeforeExit()
    {
        if (!begun) return;
        try { ClosePreparation(); } catch { }
        try { AudioHolderCleanup(); }
        catch (Exception exception) { try { Receipt("rc2-audio-holder-cleanup", "passed", false, "error", exception.ToString()); } catch { } }
        try { Status("exit"); } catch { }
    }
    private static void Released()
    {
        try
        {
            Status("native-dispose");
            if (FeatureQualification.Setting("DeferredAudio"))
                Receipt("rc2-audio-cleanup", "pendingMetadataCleared", Convert.ToInt32(Read("DeferredAudioRuntime", "PendingCount")) == 0,
                    "status", Read("DeferredAudioRuntime", "Status"), "nativeStreamDisposalIndependentlyVerified", false);
        }
        catch { }
        UnityData.DisposeStatic -= Released;
    }
    private static void Audio()
    {
        if (!FeatureQualification.Setting("DeferredAudio")) return;
        if (!(Read("DeferredAudioRuntime", "enabled") is bool enabled) || !enabled)
        { Receipt("rc2-audio", "covered", false, "reason", Read("DeferredAudioRuntime", "Status")); return; }
        // Inspect the pending-song registry, never reflect on the public clip field.
        var songs = (IEnumerable)Read("DeferredAudioRuntime", "Songs")!;
        SongDef? song = songs.Cast<SongDef>().FirstOrDefault(s => HasPendingSource(s.clipPath));
        if (song == null)
        { Receipt("rc2-audio", "covered", false, "reason", "no-pending-song-at-menu"); return; }
        int before = (int)Read("DeferredAudioRuntime", "PendingCount")!;
        AudioClip clip = song.clip; // Prepatcher rewrites this ordinary compiled consumer.
        AudioClip first = ContentFinder<AudioClip>.Get(song.clipPath);
        AudioClip second = ContentFinder<AudioClip>.Get(song.clipPath);
        bool ready = clip != null && clip.loadState == AudioDataLoadState.Loaded && clip.samples > 0 && clip.channels > 0 && clip.frequency > 0;
        bool same = ReferenceEquals(clip, first) && ReferenceEquals(first, second);
        bool holderSame = false;
        var holders = (IDictionary)Read("DeferredAudioRuntime", "Holders")!;
        foreach (ModContentHolder<AudioClip> holder in holders.Keys)
            if (holder.contentList.TryGetValue(song.clipPath, out AudioClip winner) && ReferenceEquals(winner, clip))
            {
                holderSame = ReferenceEquals(holder.Get(song.clipPath), clip);
                audioHolder = holder;
                object state = holders[holder]!;
                audioProvider = (ModContentPack)Read(state.GetType(), state, "Mod")!;
                testedClip = clip; audioPath = song.clipPath;
            }
        int after = (int)Read("DeferredAudioRuntime", "PendingCount")!;
        Receipt("rc2-audio", "covered", true, "passed", ready && same && holderSame && after < before,
            "song", song.defName, "path", song.clipPath, "nativeReady", ready, "sameLookupObject", same,
            "sameHolderObject", holderSame, "pendingBefore", before, "pendingAfter", after,
            "samples", clip == null ? 0 : clip.samples, "channels", clip == null ? 0 : clip.channels,
            "frequency", clip == null ? 0 : clip.frequency, "playbackTested", false);
    }
    private static void AudioHolderCleanup()
    {
        if (audioHolder == null || audioProvider == null) return;
        var holders = (IDictionary)Read("DeferredAudioRuntime", "Holders")!;
        if (!holders.Contains(audioHolder))
        { Receipt("rc2-audio-holder-cleanup", "covered", false, "reason", "native-holder-already-released"); return; }
        object state = holders[audioHolder]!;
        object pending = Read(state.GetType(), state, "Pending")!;
        int pendingBefore = (int)Read(pending.GetType(), pending, "Count")!;
        if (pendingBefore > 64 || audioHolder.contentList.Count > 128)
        { Receipt("rc2-audio-holder-cleanup", "covered", false, "reason", "holder-exceeds-bounded-final-check"); return; }
        // Never dispose a stream referenced by a native audio source. This check
        // runs immediately before normal shutdown, without changing playback.
        if (UnityEngine.Object.FindObjectsOfType<AudioSource>(true).Any(s => s.clip != null
            && audioHolder.contentList.Values.Any(c => ReferenceEquals(c, s.clip))))
        { Receipt("rc2-audio-holder-cleanup", "covered", false, "reason", "native-audio-source-retains-holder-clip"); return; }
        var exposed = audioProvider.GetContentHolder<AudioClip>();
        int pendingAfter = (int)Read(pending.GetType(), pending, "Count")!;
        bool same = ReferenceEquals(exposed, audioHolder) && ReferenceEquals(exposed.Get(audioPath), testedClip);
        Stream[] streams = audioHolder.extraDisposables.OfType<Stream>().Where(s => s.CanRead).ToArray();
        int published = audioHolder.contentList.Count;
        if (streams.Length == 0)
        { Receipt("rc2-audio-holder-cleanup", "covered", false, "reason", "no-observable-open-source-stream"); return; }
        // The ordinary native API owns disposal; the observer never calls a
        // stream's Dispose or the product's cleanup implementation directly.
        audioHolder.ClearDestroy();
        int closed = streams.Count(s => !s.CanRead);
        bool removed = !holders.Contains(audioHolder);
        bool empty = audioHolder.contentList.Count == 0 && audioHolder.extraDisposables.Count == 0;
        Receipt("rc2-audio-holder-cleanup", "covered", true,
            "passed", same && pendingAfter == 0 && removed && empty && closed == streams.Length,
            "provider", audioProvider.PackageId, "pendingBeforeExposure", pendingBefore,
            "pendingAfterExposure", pendingAfter, "sameHolderAndTestedClip", same, "publishedBeforeClear", published,
            "openSourceStreamsBeforeClear", streams.Length, "closedSourceStreamsAfterClear", closed,
            "pendingHolderRemoved", removed, "nativeHolderListsCleared", empty,
            "nativeStreamDisposalIndependentlyVerified", closed == streams.Length, "ordinaryGameplayTeardownTested", false);
        audioHolder = null; audioProvider = null; testedClip = null;
    }
    private static bool HasPendingSource(string path)
    {
        foreach (object state in ((IDictionary)Read("DeferredAudioRuntime", "Holders")!).Values)
        {
            object pending = Read(state.GetType(), state, "Pending")!;
            if ((bool)AccessTools.Method(pending.GetType(), "Contains").Invoke(pending, new object[] { path })) return true;
        }
        return false;
    }
    private static void BeginPreparation()
    {
        if (!FeatureQualification.Setting("PreparedTextures")) return;
        // Maintenance runs must observe their requested outcome without creating new entries.
        object? policy = Read("CacheLaunchPolicy", "Current");
        if (policy != null && Convert.ToString(Read(policy.GetType(), policy, "Action")) != "Normal")
        { Receipt("rc2-preparation", "covered", false, "reason", "maintenance-policy"); return; }
        foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
        {
            var selected = ((IEnumerable<KeyValuePair<string, FileInfo>>)Call("PngRuntime", "SelectedFiles", mod)!).ToArray();
            foreach (var candidate in selected)
            {
                string extension = candidate.Value.Extension.ToLowerInvariant();
                if (extension != ".png" && extension != ".jpg" && extension != ".jpeg") continue;
                if (candidate.Value.Length > 1024 * 1024) continue;
                object header;
                using (var stream = candidate.Value.OpenRead()) header = Call("PreparationImageHeader", "Read", stream)!;
                if ((int)Read(header.GetType(), header, "Width")! > 1024 || (int)Read(header.GetType(), header, "Height")! > 1024
                    || (string)Call("TexturePreparationBatch", "NativeAdmission", header)! != "") continue;
                provider = mod; logical = candidate.Key; source = candidate.Value;
                originalSource = (byte[])Call("PreparedTextureRuntime", "ReadSource", source)!;
                selection = (string)Call("PreparedTextureRuntime", "SelectionIdentity", mod, selected)!;
                StartController();
                return;
            }
        }
        Receipt("rc2-preparation", "covered", false, "reason", "no-native-image-within-1MiB-and-1024x1024");
    }
    private static void StartController()
    {
        Type type = Product("TexturePreparationWindow")!;
        Type modType = Product("WakeUpMod")!;
        object settings = Read(modType, LoadedModManager.GetMod(modType), "settings")!;
        preparation = (Window)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { settings }, null)!;
        ((HashSet<ModContentPack>)Read(type, preparation, "providers")!).Add(provider!);
        AccessTools.Field(type, "paths").SetValue(preparation, logical);
        AccessTools.Field(type, "preset").SetValue(preparation, preset);
        AccessTools.Field(type, "qualityConsent").SetValue(preparation, preset != 0);
        AccessTools.Method(type, "Scan").Invoke(preparation, new object[] { true });
        Receipt("rc2-preparation", "event", "controller-start", "preset", preset, "provider", provider!.PackageId, "path", logical);
    }
    private static void VerifyExport()
    {
        object? store = null;
        try
        {
            string identity = (string)Read(preparation!.GetType(), preparation, "identity")!;
            Type type = Product("PreparedTextureStore")!;
            store = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object?[] { GenFilePaths.SaveDataFolderPath, null, 512L * 1024 * 1024, false }, null)!;
            byte[] dds = (byte[])(AccessTools.Method(type, "ReadExport").Invoke(store, new object[] { identity })
                ?? throw new InvalidOperationException("Controller DDS export could not be reopened."));
            object entry = Call("PreparedDds", "Parse", dds)!;
            object header;
            using (var input = new MemoryStream(originalSource!)) header = Call("PreparationImageHeader", "Read", input)!;
            int divisor = 1 << (preset - 1);
            int width = (int)Read(header.GetType(), header, "Width")! / divisor;
            int height = (int)Read(header.GetType(), header, "Height")! / divisor;
            bool dimensions = (int)Read(entry.GetType(), entry, "Width")! == width && (int)Read(entry.GetType(), entry, "Height")! == height;
            bool mips = (int)Read(entry.GetType(), entry, "Mips")! == (int)Call("PreparationImageHeader", "Mips", width, height)!;
            string root = (string)Read(type, store, "ExportRoot")!;
            string key = (string)Call("PngCache", "Hex", (byte[])Call("PngCache", "Hash", Encoding.UTF8.GetBytes(identity))!)!;
            string manifest = Path.Combine(root, key + ".manifest");
            string[] mapping = File.ReadAllLines(manifest);
            bool mapped = mapping.Length == 4 && mapping[2] == logical.Replace('\\', '/') && File.Exists(Path.Combine(root, mapping[3]));
            bool unchanged = originalSource!.SequenceEqual((byte[])Call("PreparedTextureRuntime", "ReadSource", source!)!);
            bool passed = dimensions && mips && mapped && unchanged;
            Receipt("rc2-export", "passed", passed, "preset", preset, "path", logical, "width", width, "height", height,
                "completeMipChain", mips, "sourceMapping", mapped, "sourceUnchanged", unchanged, "bytes", dds.Length,
                "manifest", manifest, "automaticGameSubstitution", false, "visualConsumerQualification", false);
            if (!passed) throw new InvalidOperationException("Controller export fidelity checks failed.");
        }
        finally { (store as IDisposable)?.Dispose(); }
    }
    private static void VerifyPrepared()
    {
        object? store = null;
        Texture2D? restored = null;
        try
        {
            byte[] bytes = (byte[])Call("PreparedTextureRuntime", "ReadSource", source!)!;
            string platform = (string)Call("PngRuntime", "PreparationPlatform")!;
            string key = (string)Call("PreparedTextureRuntime", "Identity", selection, logical, source!.FullName, bytes, 0, platform, "")!;
            Type type = Product("PreparedTextureStore")!;
            store = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object?[] { GenFilePaths.SaveDataFolderPath, null, 512L * 1024 * 1024, false }, null)!;
            object entry = AccessTools.Method(type, "Read").Invoke(store, new object[] { key }) ?? throw new InvalidOperationException("Controller output could not be reopened.");
            restored = (Texture2D)Call("PngRuntime", "Restore", entry)!;
            string path = logical.Replace('\\', '/');
            if (!path.StartsWith("Textures/", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected native texture root.");
            Texture2D native = provider!.GetContentHolder<Texture2D>().Get(Path.ChangeExtension(path.Substring(9), null));
            if (native == null) throw new InvalidOperationException("Native holder texture unavailable.");
            long before = Convert.ToInt64(Read("PngRuntime", "mismatches"));
            Call("PngRuntime", "Compare", native, restored);
            bool equal = Convert.ToInt64(Read("PngRuntime", "mismatches")) == before;
            Receipt("rc2-prepared-fidelity", "passed", equal, "path", logical, "nativePropertiesAndRenderedMipsEqual", equal,
                "width", restored.width, "height", restored.height, "mips", restored.mipmapCount, "format", restored.format,
                "visualInspection", false);
        }
        finally { if (restored != null) UnityEngine.Object.DestroyImmediate(restored); (store as IDisposable)?.Dispose(); }
    }
    private static void ClosePreparation(bool final = true)
    {
        finished = final;
        if (preparation == null) return;
        try { preparation.PostClose(); } finally { preparation = null; }
    }
}
