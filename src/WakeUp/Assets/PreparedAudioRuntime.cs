// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using NAudio.Wave;
using RimWorld.IO;
using RuntimeAudioClipLoader;
using UnityEngine;
using Verse;

namespace WakeUp;

// Experimental route: only the lower OGG/WAV reader is delayed. The native file,
// CustomAudioFileReader, SampleChannel, AudioClip and callbacks retain ownership.
public static class PreparedAudioRuntime
{
    internal const string Owner = "kt-nyx.wake-up.prepared-audio";
    private const string BootstrapKey = "WakeUp.PreparedAudio.Bootstrap.v1";
    internal const int MaximumReaders = 16384;
    private static Dictionary<string, object>? registry;
    private static object RegistryGate => registry!["audioRegistryGate"];
    private static Dictionary<object, Func<int>> Pending => (Dictionary<object, Func<int>>)registry!["audioPending"];
    private static readonly object LifetimeGate = new();
    private static readonly List<Life> lives = new();
    private static readonly ConditionalWeakTable<AudioClip, object> nativeReaders = new();
    private static readonly ConditionalWeakTable<object, ReaderOwner> readerOwners = new();
    private static readonly ConditionalWeakTable<AudioClip, ReaderOwner> clipOwners = new();
    private static readonly ConditionalWeakTable<AudioClip, Dictionary<string, object>> mp3Owners = new();
    private static readonly ConditionalWeakTable<AudioClip, PreparedMp3FramesRuntime.Session> mp3FrameOwners = new();
    private static readonly ConditionalWeakTable<Stream, VirtualFile> nativeFiles = new();
    private static PreparedAudioCache? cache;
    private static string identity = "";
    private static bool enabled;
    private static Type? filesystemType;
    private static int cleanupFailures;
    private static int mp3CompletionFailures;
    private static long sourceBytes, admissions, recordings;
    private static long playbackOrdinal;
    internal static string Status { get; private set; } = "Prepared audio experiment is off.";
    [ThreadStatic] private static LoadFrame? loadFrame;
    private sealed class LoadFrame
    {
        internal LoadFrame? Previous;
        internal FileStream? Source;
        internal PreparedAudioWaveStream? Reader;
        internal object? NativeReader;
        internal string? Key;
        internal long Epoch;
        internal bool Streaming, Background, DisposeSource;
        internal AudioFormat Format;
        internal ReaderOwner? Owner;
        internal bool Mp3Admitted;
        internal PreparedMp3FramesRuntime.Session? Mp3Frames;
        internal Life? Lifetime;
        internal object? PreviousMp3Context;
    }
    private sealed class ReaderOwner
    {
        internal PreparedAudioWaveStream Reader = null!;
        internal string SourcePath = "", Key = "";
        internal long Epoch;
        internal bool Streaming, Background, DisposeSource, CacheHit;
        internal AudioFormat Format;
        internal Dictionary<string, object>? AtReturn;
        internal long RecordingsAtReturn, AdmissionsAtReturn;
        internal int RecordingCaptured, RecordingPublished;
        internal int ReadinessAttempts;
        internal string ReadinessCause = "";
        internal int SelectedPlaybackCalls, AdvancePreparedThread;
        internal long FirstSelectedPlaybackOrdinal, AdvancePreparedOrdinal, AdvanceGeneration;
        internal string LastSelectedPlaybackCause = "";
    }
    private sealed class ReaderDisposal
    {
        internal ReaderOwner Owner = null!;
        internal PreparedAudioData? Data;
    }
    private sealed class Life
    {
        internal FileStream Source = null!;
        internal PreparedAudioWaveStream? Reader;
        internal PreparedMp3FramesRuntime.Session? Mp3;
        internal bool Loading;
        internal AudioClip Clip = null!;
        internal object NativeReader = null!;
    }

    internal static void TryInitialize(IReadOnlyList<string> arguments)
    {
        if (arguments.Contains("--fixture-c10-audio-measurement=off")) return;
        bool measurement = arguments.Contains("--fixture-c10-audio-measurement=cold")
            || arguments.Contains("--fixture-c10-audio-measurement=warm");
        if (!measurement && !arguments.Any(a => a.StartsWith("--fixture-c10-prepared-audio=", StringComparison.Ordinal))) return;
        try
        {
            if (AppDomain.CurrentDomain.GetData(BootstrapKey) is not Dictionary<string, object> bootstrap)
                throw new InvalidOperationException("early bootstrap is absent");
            lock (bootstrap["gate"])
            {
                if (!(bool)bootstrap["preloaderQualified"] || !(bool)bootstrap["preloaderCompleted"]
                    || !(bool)bootstrap["firstExtensionRecorded"] || ((string)bootstrap["failure"]).Length != 0
                    || !ReferenceEquals(bootstrap["oldHarmony"], bootstrap["firstExtensionHarmony"]))
                    throw new InvalidOperationException("early bootstrap is unqualified");
                registry = (Dictionary<string, object>)bootstrap["audioRegistry"];
            }
            // Reflection, identity validation and Harmony queries stay outside
            // the registry/reader locks, including initial hook installation.
            if (!PreparedAudioBootstrap.VerifyFinalGuards()) throw new InvalidOperationException("final audio guards are absent");
            bool nativeEntry = arguments.Contains("--fixture-c10-native-entry-probe");
            if (nativeEntry && (!registry.TryGetValue("audioNativeEntryReady", out object ready) || !(bool)ready))
                throw new InvalidOperationException("early native entry bridge is absent");
            if (nativeEntry && (!registry.TryGetValue("audioNativeEntryQualified", out object qualified) || !(bool)qualified))
                throw new InvalidOperationException("native entry bridge error contract is unqualified");
            if (nativeEntry && (!registry.TryGetValue("audioNativeProductStopInstalled", out object installed) || !(bool)installed))
                throw new InvalidOperationException("product-owned native shutdown is absent");
            lock (RegistryGate)
                if (!registry!.TryGetValue("audioReplacementHarmony", out object replacement)
                    || !ReferenceEquals(typeof(Harmony).Assembly, replacement))
                    throw new InvalidOperationException("runtime Harmony is not the bound replacement");
            identity = PreparedAudioContract.Validate();
            PreparedAudioNative.Initialize();
            filesystemType = typeof(Manager).Assembly.GetType("RimWorld.IO.FilesystemFile", true)!;
            DeferredAudioContract.Validate(typeof(SongDef).Assembly);
            if (Harmony.GetAllPatchedMethods().Any(Relevant))
                throw new InvalidOperationException("an existing patch changes the native audio path");
            var harmony = new Harmony(Owner);
            cache = new PreparedAudioCache(GenFilePaths.SaveDataFolderPath);
            PreparedMp3IndexRuntime.Initialize(cache, registry);
            // Every prepared-audio mode can admit the qualified selected native
            // provider. Unsupported providers retain ordinary decoding.
            if (PreparedMp3FramesRuntime.Initialize(cache, registry, nativeEntry))
            {
                OwnPatch(harmony, PreparedMp3FramesRuntime.FrameTarget, prefix: nameof(BeforeMp3Frame), finalizer: nameof(AfterMp3Frame));
                OwnPatch(harmony, PreparedMp3FramesRuntime.ResetTarget, prefix: nameof(BeforeMp3Reset), finalizer: nameof(AfterMp3Operation));
                OwnPatch(harmony, PreparedMp3FramesRuntime.DisposeTarget, prefix: nameof(BeforeMp3Dispose), finalizer: nameof(AfterMp3Operation));
            }
            OwnPatch(harmony, PreparedMp3IndexRuntime.Target, prefix: nameof(BeforeMp3Index), postfix: nameof(AfterMp3Index));
            Type native = typeof(Manager).Assembly.GetType("RuntimeAudioClipLoader.CustomAudioFileReader", true)!;
            OwnPatch(harmony, AccessTools.Method(native, "CreateReaderStream"), transpiler: nameof(ReaderFactory));
            OwnPatch(harmony, AccessTools.Method(native, "Dispose", new[] { typeof(bool) }),
                prefix: nameof(BeforeReaderDispose), finalizer: nameof(AfterReaderDispose));
            OwnPatch(harmony, AccessTools.Method(filesystemType, "CreateReadStream"), postfix: nameof(AfterFile));
            OwnPatch(harmony, AccessTools.Method(typeof(Manager), "Load", new[] { typeof(Stream), typeof(AudioFormat), typeof(string), typeof(bool), typeof(bool), typeof(bool) }),
                prefix: nameof(BeforeLoad), finalizer: nameof(AfterLoad));
            OwnPatch(harmony, AccessTools.Method(typeof(FileStream), "Dispose", new[] { typeof(bool) }), prefix: nameof(BeforeDispose), finalizer: nameof(AfterSourceDispose));
            // Both Entry and Play call the base update. Keep lifetime cleanup
            // alive through colony sessions and scene changes.
            harmony.Patch(AccessTools.Method(typeof(Root), "Update"), postfix: Hook(nameof(CleanupDestroyed)));
            if (!measurement && arguments.Any(a => a.StartsWith("--fixture-c10-prepared-audio=readiness-", StringComparison.Ordinal)
                || a.StartsWith("--fixture-c10-prepared-audio=advance-", StringComparison.Ordinal)))
                PreparedAudioReadiness.Initialize(harmony);
            if (!measurement && arguments.Any(a => a.StartsWith("--fixture-c10-prepared-audio=advance-", StringComparison.Ordinal)))
                PreparedAudioAdvance.Initialize(harmony);
            lock (RegistryGate)
            {
                if ((bool)registry["audioNativeOnly"]) throw new InvalidOperationException((string)registry["audioReason"]);
                registry["audioAdmissionClosed"] = false;
                enabled = true;
            }
            lock (bootstrap["gate"]) bootstrap["admissionClosed"] = false;
            UnityData.DisposeStatic += Close;
            Status = "Prepared OGG/WAV byte replay and native MP3 index reuse are enabled in the isolated experiment.";
        }
        catch (Exception error)
        {
            enabled = false;
            if (registry != null) lock (RegistryGate) registry["audioAdmissionClosed"] = true;
            new Harmony(Owner).UnpatchAll(Owner);
            cache?.Dispose(); cache = null;
            PreparedMp3IndexRuntime.Close();
            PreparedMp3FramesRuntime.Close();
            PreparedAudioReadiness.Close();
            PreparedAudioAdvance.Close();
            Status = "Prepared audio refused: " + error.GetBaseException().Message + ". Ordinary loading remains active.";
        }
        Log.Message("[Wake-Up] " + Status);
    }

    private static bool Relevant(MethodBase method)
    {
        if (Equals(method, typeof(FileInfo).GetMethod(nameof(FileInfo.OpenRead)))) return true;
        if (method.DeclaringType == typeof(FileStream) && method.Name == "Dispose") return true;
        if (PreparedAudioContract.Relevant(method)) return true;
        for (Type? type = method.DeclaringType; type != null; type = type.DeclaringType)
        {
            string name = type.IsGenericType ? type.GetGenericTypeDefinition().FullName ?? "" : type.FullName ?? "";
            if (name == "RuntimeAudioClipLoader.Manager" || name == "RimWorld.IO.FilesystemFile" && method.Name == "CreateReadStream"
                || name == "Verse.ModContentLoader`1" && method.Name == "LoadItem"
                || name == "Verse.ModContentHolder`1" && method.Name == "ClearDestroy") return true;
        }
        return false;
    }
    private static HarmonyMethod Hook(string name) => new(typeof(PreparedAudioRuntime), name);
    private static void OwnPatch(Harmony owner, MethodBase target, string? prefix = null, string? finalizer = null, string? transpiler = null, string? postfix = null)
    {
        ((Action<MethodBase>)registry!["audioArmOwnPatch"])(target);
        try { owner.Patch(target, prefix: prefix == null ? null : Hook(prefix), finalizer: finalizer == null ? null : Hook(finalizer), transpiler: transpiler == null ? null : Hook(transpiler), postfix: postfix == null ? null : Hook(postfix)); }
        finally { ((Action)registry!["audioDisarmOwnPatch"])(); }
    }
    private static IEnumerable<CodeInstruction> ReaderFactory(IEnumerable<CodeInstruction> original)
    {
        var code = original.ToList();
        foreach (var route in new[] { new[] { "NVorbis.NAudioSupport.VorbisWaveReader", nameof(CreateReader) },
            new[] { "NAudio.Wave.WaveFileReader", nameof(CreateWaveReader) },
            new[] { "NAudio.Wave.Mp3FileReader", nameof(CreateMp3Reader) } })
        {
            var targets = code.Where(c => c.opcode == OpCodes.Newobj && c.operand is ConstructorInfo constructor
                && constructor.DeclaringType?.FullName == route[0]
                && constructor.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(Stream) })).ToArray();
            if (targets.Length != 1) throw new InvalidOperationException("native audio factory changed: " + route[0]);
            int at = code.IndexOf(targets[0]);
            // Preserve stfld receiver and branch labels; associate the actual
            // outer reader without changing its native format/conversion branch.
            targets[0].opcode = OpCodes.Ldarg_0;
            targets[0].operand = null;
            code.Insert(at + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PreparedAudioRuntime), route[1])));
        }
        return code;
    }
    private static void AfterFile(VirtualFile __instance, Stream __result)
    {
        if (enabled && __instance.GetType() == filesystemType && __result?.GetType() == typeof(FileStream))
            nativeFiles.GetValue(__result, _ => __instance);
    }
    private static void BeforeLoad(Stream __0, AudioFormat __1, bool __3, bool __4, bool __5, out LoadFrame __state)
    {
        __state = new LoadFrame { Previous = loadFrame, Streaming = __3, Background = __4, DisposeSource = __5, Format = __1 };
        // Every nested native Load masks its parent's index context, including
        // loads that are ineligible or begin after sticky native fallback.
        __state.PreviousMp3Context = PreparedMp3IndexRuntime.SuspendContext();
        loadFrame = __state;
        if (!enabled) return;
        bool admissionOpen = PreparedMp3IndexRuntime.AdmissionOpen;
        if (enabled && (__1 == AudioFormat.ogg || __1 == AudioFormat.wav || __1 == AudioFormat.mp3) && __0?.GetType() == typeof(FileStream) && nativeFiles.TryGetValue(__0, out VirtualFile file))
        {
            var source = (FileStream)__0;
            // Closed admission needs only the existing exact stream identity;
            // do not introduce path-getter callbacks into ordinary fallback.
            if (!admissionOpen || string.Equals(Path.GetFullPath(source.Name), Path.GetFullPath(file.FullPath), StringComparison.OrdinalIgnoreCase))
                __state.Source = source;
        }
        // Keep the native source/outer-reader association after sticky fallback
        // for existing ownership diagnostics. Closed admission still prevents
        // MP3 cache context and OGG/WAV prepared construction below.
        if (!admissionOpen) return;
        if (__1 == AudioFormat.mp3 && __state.Source != null)
        {
            string? key = PreparedAudioCache.SourceKey(__state.Source, identity, PreparedMp3IndexData.Identity, out long hashed);
            System.Threading.Interlocked.Add(ref sourceBytes, hashed);
            if (key != null)
            {
                __state.Key = key;
                __state.Mp3Admitted = PreparedMp3IndexRuntime.TryAdmitContext(__state.Source, key);
            }
        }
    }
    private static Exception? AfterLoad(AudioClip? __result, Exception? __exception, LoadFrame? __state)
    {
        if (__state == null) return __exception;
        // Restore first, before optional completion/storage/diagnostics. Even a
        // refused nested load or bookkeeping failure must release its scope.
        var mp3 = PreparedMp3IndexRuntime.RestoreContext(__state.PreviousMp3Context);
        loadFrame = __state.Previous;
        if (__state.Format == AudioFormat.mp3)
        {
            try
            {
                if (__state.Mp3Admitted && mp3 != null && PreparedMp3IndexRuntime.Current(mp3))
                {
                    bool succeeded = __exception == null && PreparedAudioNative.InspectLoadState(__result) == PreparedAudioNative.LoadStateInspection.Active;
                    var index = PreparedMp3IndexRuntime.CompleteContext(mp3, succeeded);
                    if (__exception == null && !ReferenceEquals(__result, null))
                    {
                        mp3Owners.Add(__result!, new Dictionary<string, object> { ["sourcePath"] = __state.Source!.Name,
                            ["cacheKey"] = __state.Key!, ["streaming"] = __state.Streaming,
                            ["loadInBackground"] = __state.Background, ["format"] = "mp3", ["index"] = index });
                        if (__state.NativeReader != null) nativeReaders.GetValue(__result!, _ => __state.NativeReader);
                        if (__state.Mp3Frames != null) mp3FrameOwners.Add(__result!, __state.Mp3Frames);
                    }
                }
            }
            // This block contains only optional bookkeeping, no original native
            // operation. Its failure must not replace the native return/error.
            catch { System.Threading.Interlocked.Increment(ref mp3CompletionFailures); }
            FinishMp3Life(__state, __result);
            return __exception;
        }
        if (__exception == null && __result != null && __state.NativeReader != null)
            nativeReaders.GetValue(__result, _ => __state.NativeReader);
        var reader = __state.Reader;
        if (reader == null)
        {
            if (__state.Lifetime != null) lock (LifetimeGate) lives.Remove(__state.Lifetime);
            return __exception;
        }
        if (__exception == null && __result != null)
        {
            lock (LifetimeGate) { __state.Lifetime!.Loading = false; __state.Lifetime.Clip = __result; }
            ReaderOwner owner = __state.Owner!;
            // The native nonstreaming worker can already have completed and
            // disposed its reader. Snapshot that fact rather than inferring it
            // from the Manager thread or resetting its recording at return.
            if (owner.Streaming || !owner.Background) PublishRecording(owner, CaptureRecording(owner));
            lock (RegistryGate)
            {
                if (reader.Settled) Pending.Remove(reader);
            }
            owner.AtReturn = reader.Snapshot();
            owner.RecordingsAtReturn = System.Threading.Interlocked.Read(ref recordings);
            owner.AdmissionsAtReturn = System.Threading.Interlocked.Read(ref admissions);
            clipOwners.GetValue(__result, _ => owner);
        }
        else
        {
            lock (LifetimeGate) lives.Remove(__state.Lifetime!);
            lock (RegistryGate) SettleAndRemove(reader);
            // There is no published clip or surviving callback owner here.
            ReleasePrivate(reader);
        }
        return __exception;
    }

    public static WaveStream CreateReader(Stream source, object nativeReader)
        => CreateNativeReader(source, nativeReader, AudioFormat.ogg);

    public static WaveStream CreateWaveReader(Stream source, object nativeReader)
        => CreateNativeReader(source, nativeReader, AudioFormat.wav);

    public static WaveStream CreateMp3Reader(Stream source, object nativeReader)
    {
        if (loadFrame?.Source != null && ReferenceEquals(source, loadFrame.Source)) loadFrame.NativeReader = nativeReader;
        WaveStream reader = PreparedAudioNative.Create(source, AudioFormat.mp3);
        var frame = loadFrame;
        if (frame?.Mp3Admitted == true && ReferenceEquals(source, frame.Source))
        {
            // Reserve shared lifetime capacity before the native factory can
            // publish a worker/callback. Loading reservations are not dead clips.
            Life? life = ReserveLife(frame, nativeReader);
            if (life != null)
            {
                try
                {
                    frame.Mp3Frames = PreparedMp3FramesRuntime.Attach(reader, frame.Key!);
                    life.Mp3 = frame.Mp3Frames;
                    if (life.Mp3 != null) frame.Lifetime = life;
                }
                finally { if (life.Mp3 == null) { lock (LifetimeGate) lives.Remove(life); frame.Lifetime = null; } }
            }
        }
        return reader;
    }

    private static Life? ReserveLife(LoadFrame frame, object nativeReader)
    {
        lock (LifetimeGate)
        {
            if (lives.Count >= MaximumReaders) return null;
            var life = new Life { Source = frame.Source!, NativeReader = nativeReader, Loading = true };
            lives.Add(life);
            frame.Lifetime = life;
            return life;
        }
    }

    private static void FinishMp3Life(LoadFrame frame, AudioClip? clip)
    {
        var life = frame.Lifetime;
        if (life == null) return;
        // A non-null result can already have a native callback owner, including
        // when optional inspection fails. Keep that identity until failure or
        // destruction is established; the original exception is never changed.
        bool retain = !ReferenceEquals(clip, null)
            && PreparedAudioNative.InspectLoadState(clip) != PreparedAudioNative.LoadStateInspection.Failed;
        lock (LifetimeGate)
        {
            life.Loading = false;
            if (retain) life.Clip = clip!;
            if (!retain || life.Mp3!.Disposed) lives.Remove(life);
        }
        // No clip/callback escaped a failed load. Drop skipped work and close
        // only the retained borrowed-input reader; do not settle by decoding.
        if (!retain) ReleaseLife(life);
    }

    private static bool BeforeMp3Frame(object __instance, Mp3Frame __0, byte[] __1, int __2, ref int __result, out PreparedMp3FramesRuntime.Call? __state)
        => PreparedMp3FramesRuntime.BeforeFrame(__instance, __0, __1, __2, ref __result, out __state);
    private static Exception? AfterMp3Frame(Exception? __exception, PreparedMp3FramesRuntime.Call? __state, int __result)
        => PreparedMp3FramesRuntime.After(__exception, __state, __result);
    private static bool BeforeMp3Reset(object __instance, out PreparedMp3FramesRuntime.Call? __state)
        => PreparedMp3FramesRuntime.BeforeReset(__instance, out __state);
    private static void BeforeMp3Dispose(object __instance, out PreparedMp3FramesRuntime.Call? __state)
        => PreparedMp3FramesRuntime.BeforeDispose(__instance, out __state);
    private static Exception? AfterMp3Operation(Exception? __exception, PreparedMp3FramesRuntime.Call? __state)
        => PreparedMp3FramesRuntime.After(__exception, __state);

    private static bool BeforeMp3Index(object __instance, out PreparedMp3IndexRuntime.ScanState? __state)
        => PreparedMp3IndexRuntime.Prefix(__instance, out __state);
    private static void AfterMp3Index(object __instance, PreparedMp3IndexRuntime.ScanState? __state, bool __runOriginal)
        => PreparedMp3IndexRuntime.Postfix(__instance, __state, __runOriginal);

    private static WaveStream CreateNativeReader(Stream source, object nativeReader, AudioFormat format)
    {
        var frame = loadFrame;
        if (frame?.Source != null && ReferenceEquals(source, frame.Source)) frame.NativeReader = nativeReader;
        if (!enabled || registry == null || frame?.Source == null || !ReferenceEquals(source, frame.Source) || frame.Format != format)
            return PreparedAudioNative.Create(source, format);
        bool refused;
        lock (RegistryGate)
        {
            refused = (bool)registry["audioNativeOnly"] || (bool)registry["audioAdmissionClosed"] || Pending.Count >= MaximumReaders;
            frame.Epoch = (long)registry["audioEpoch"];
        }
        if (refused) return PreparedAudioNative.Create(source, format);
        lock (LifetimeGate) refused = lives.Count >= MaximumReaders;
        if (refused) return PreparedAudioNative.Create(source, format);
        long position = source.Position;
        if (position != 0) return PreparedAudioNative.Create(source, format);
        string transcript = PreparedAudioData.CacheIdentity + ":" + format + (frame.Streaming ? ":streaming" : ":nonstreaming");
        frame.Key = PreparedAudioCache.SourceKey(frame.Source, identity, transcript, out long hashed);
        System.Threading.Interlocked.Add(ref sourceBytes, hashed);
        if (frame.Key == null) return PreparedAudioNative.Create(source, format);
        var data = cache?.Read(frame.Key);
        if (ReserveLife(frame, nativeReader) == null) return PreparedAudioNative.Create(source, format);
        if (data != null)
        {
            // Construction here creates managed replay state only. Register it
            // before returning to the unchanged native AudioClip.Create call.
            var prepared = PreparedAudioWaveStream.FromOwnedData(data, () => PreparedAudioNative.Create(source, position, format));
            lock (RegistryGate)
            {
                if (!(bool)registry["audioNativeOnly"] && !(bool)registry["audioAdmissionClosed"] && (long)registry["audioEpoch"] == frame.Epoch && Pending.Count < MaximumReaders)
                {
                    Pending.Add(prepared, prepared.SettleNativeOutcome);
                    AssociateReader(frame, prepared, nativeReader, true);
                    System.Threading.Interlocked.Increment(ref admissions);
                    return prepared;
                }
            }
        }
        var recording = new PreparedAudioWaveStream(PreparedAudioNative.Create(source, format));
        AssociateReader(frame, recording, nativeReader, false);
        return recording;
    }

    private static void AssociateReader(LoadFrame frame, PreparedAudioWaveStream reader, object nativeReader, bool cacheHit)
    {
        var owner = new ReaderOwner { Reader = reader, SourcePath = frame.Source!.Name, Key = frame.Key!, Epoch = frame.Epoch,
            Streaming = frame.Streaming, Background = frame.Background, DisposeSource = frame.DisposeSource, CacheHit = cacheHit, Format = frame.Format };
        frame.Reader = reader;
        frame.Lifetime!.Reader = reader;
        frame.NativeReader = nativeReader;
        frame.Owner = owner;
        // Established before the native factory returns, so the existing worker
        // can find it even when it finishes before Manager.Load's finalizer.
        readerOwners.Add(nativeReader, owner);
    }

    private static PreparedAudioData? CaptureRecording(ReaderOwner owner)
    {
        PreparedAudioData? data = owner.Reader.FinishRecording();
        if (data != null) System.Threading.Interlocked.Increment(ref owner.RecordingCaptured);
        return data;
    }

    private static void PublishRecording(ReaderOwner owner, PreparedAudioData? data)
    {
        if (data == null) return;
        bool publish;
        lock (RegistryGate)
            publish = enabled && !(bool)registry!["audioNativeOnly"] && (long)registry["audioEpoch"] == owner.Epoch;
        // FinishRecording returned an immutable snapshot and released its gate.
        // Optional storage never owns either the reader or registry lock.
        if (publish && cache?.Publish(owner.Key, data) == true)
        {
            System.Threading.Interlocked.Increment(ref recordings);
            System.Threading.Interlocked.Increment(ref owner.RecordingPublished);
        }
    }

    private static void BeforeReaderDispose(object __instance, bool __0, out ReaderDisposal? __state)
    {
        __state = null;
        if (!__0 || !readerOwners.TryGetValue(__instance, out ReaderOwner owner)) return;
        __state = new ReaderDisposal { Owner = owner };
        if (!owner.Streaming) __state.Data = CaptureRecording(owner);
    }

    private static Exception? AfterReaderDispose(Exception? __exception, ReaderDisposal? __state)
    {
        if (__state == null) return __exception;
        ReaderOwner owner = __state.Owner;
        // Keep the barrier registered until original Dispose actually settles
        // the proxy. Disposing a fully served transcript needs no native decode.
        lock (RegistryGate)
            if (owner.Reader.Settled) Pending.Remove(owner.Reader);
        if (__exception == null) PublishRecording(owner, __state.Data);
        return __exception;
    }

    private static void BeforeDispose(FileStream __instance, bool __0)
    {
        if (!__0 || !nativeFiles.TryGetValue(__instance, out _)) return;
        Life[] owned;
        lock (LifetimeGate) owned = lives.Where(life => ReferenceEquals(life.Source, __instance)).ToArray();
        if (owned.Length == 0) return;
        if (registry != null)
            lock (RegistryGate)
                foreach (Life life in owned)
                    if (life.Reader != null) SettleAndRemove(life.Reader);
        // Exactly the original per-item call, after earlier native Destroy
        // scheduling and before subsequent items. Preserve disposal exceptions.
    }
    private static Exception? AfterSourceDispose(FileStream __instance, bool __0, Exception? __exception)
    {
        if (!__0 || __exception != null || !nativeFiles.TryGetValue(__instance, out _)) return __exception;
        Life[] owned;
        lock (LifetimeGate) owned = lives.Where(life => ReferenceEquals(life.Source, __instance) && life.Mp3 != null).ToArray();
        // Original source closure happened in its native order. Future source
        // operations now fail natively; no history reconstruction is needed.
        // Keep the native reader until Unity confirms the clip is destroyed.
        foreach (Life life in owned)
        {
            // A native Read may already have copied its next compressed frame
            // before source closure. Let that entire original Read finish first.
            object gate = AccessTools.Field(life.NativeReader.GetType(), "lockObject").GetValue(life.NativeReader);
            lock (gate) life.Mp3!.SourceClosed();
        }
        return __exception;
    }

    internal static void CleanupDestroyed()
    {
        Life[] dead;
        lock (LifetimeGate)
        {
            dead = lives.Where(life => !life.Loading && (life.Clip == null
                || life.Mp3 != null && (life.Mp3.Disposed
                    || PreparedAudioNative.InspectLoadState(life.Clip) == PreparedAudioNative.LoadStateInspection.Failed))).ToArray();
            foreach (Life life in dead) lives.Remove(life);
        }
        foreach (Life life in dead)
        {
            if (life.Reader != null && registry != null) lock (RegistryGate) SettleAndRemove(life.Reader);
            ReleaseLife(life);
        }
    }
    private static void ReleaseLife(Life life)
    {
        if (life.Reader != null) ReleasePrivate(life.Reader);
        if (life.Mp3 != null)
        {
            try
            {
                // The original callback serializes Read/Position on this gate.
                // Join it before final private release after clip destruction.
                object gate = AccessTools.Field(life.NativeReader.GetType(), "lockObject").GetValue(life.NativeReader);
                lock (gate) life.Mp3.Release();
            }
            catch { System.Threading.Interlocked.Increment(ref cleanupFailures); life.Mp3.SourceClosed(); }
        }
    }
    private static void ReleasePrivate(PreparedAudioWaveStream reader)
    {
        try { reader.Dispose(); }
        catch { System.Threading.Interlocked.Increment(ref cleanupFailures); }
    }

    // Caller holds RegistryGate. Incomplete work keeps its original owner.
    private static void SettleAndRemove(PreparedAudioWaveStream reader)
    {
        int outcome = reader.SettleNativeOutcome();
        if (outcome < 1 || outcome > 3) throw new InvalidOperationException("Owned audio reader did not settle: " + outcome);
        Pending.Remove(reader);
    }
    private static void Close()
    {
        enabled = false;
        PreparedAudioReadiness.Close();
        PreparedAudioAdvance.Close();
        if (registry != null)
            lock (RegistryGate)
            {
                registry["audioAdmissionClosed"] = true;
                foreach (var entry in Pending.ToArray())
                {
                    int outcome = entry.Value();
                    if (outcome < 1 || outcome > 3) throw new InvalidOperationException("Pending audio reader did not settle during close: " + outcome);
                    Pending.Remove(entry.Key);
                }
            }
        cache?.Dispose(); cache = null;
        PreparedMp3IndexRuntime.Close();
            PreparedMp3FramesRuntime.Close();
        CleanupDestroyed();
    }

    // Read-only experiment receipt. No Unity operation or decoder construction.
    public static object? NativeReaderForClip(AudioClip clip)
    {
        return nativeReaders.TryGetValue(clip, out object reader) ? reader : null;
    }

    // Called only by guarded native clip-assignment wrappers. No Unity getter,
    // selection, registry-wide drain or extra outer Read/Seek occurs here.
    internal static AudioClip PrepareForPlayback(AudioClip clip, string cause)
    {
        if (!enabled || registry == null || ReferenceEquals(clip, null)
            || !clipOwners.TryGetValue(clip, out ReaderOwner owner) || !owner.Streaming) return clip!;
        lock (RegistryGate)
        {
            if ((bool)registry["audioNativeOnly"] || (bool)registry["audioAdmissionClosed"]
                || (long)registry["audioEpoch"] != owner.Epoch) return clip;
            owner.SelectedPlaybackCalls++;
            owner.LastSelectedPlaybackCause = cause;
            long ordinal = ++playbackOrdinal;
            if (owner.FirstSelectedPlaybackOrdinal == 0) owner.FirstSelectedPlaybackOrdinal = ordinal;
            PrepareOwner(owner, cause, 0);
        }
        return clip;
    }

    // Native loading has identified this scene's authored dependencies. This
    // shares the same private reader and failure latch as selected playback.
    internal static bool PrepareAhead(AudioClip clip, long generation, out string key)
    {
        key = "";
        if (!enabled || registry == null || ReferenceEquals(clip, null)
            || !clipOwners.TryGetValue(clip, out ReaderOwner owner) || !owner.Streaming) return false;
        lock (RegistryGate)
        {
            if ((bool)registry["audioNativeOnly"] || (bool)registry["audioAdmissionClosed"]
                || (long)registry["audioEpoch"] != owner.Epoch) return false;
            key = owner.Key;
            bool attempted = PrepareOwner(owner, "advance-biome", generation);
            return attempted && owner.Reader.IsNative;
        }
    }

    // Registry gate is already held. No Unity API or consumer callback occurs.
    private static bool PrepareOwner(ReaderOwner owner, string cause, long generation)
    {
        bool prepared = owner.Reader.PrepareForPlayback(cause);
        if (prepared)
        {
            owner.ReadinessAttempts++;
            owner.ReadinessCause = cause;
            if (generation != 0)
            {
                owner.AdvanceGeneration = generation;
                owner.AdvancePreparedOrdinal = ++playbackOrdinal;
                owner.AdvancePreparedThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            }
        }
        if (owner.Reader.Settled) Pending.Remove(owner.Reader);
        return prepared;
    }

    public static Dictionary<string, object>? SnapshotForClip(AudioClip clip)
    {
        if (!clipOwners.TryGetValue(clip, out ReaderOwner owner)) return null;
        return new Dictionary<string, object>
        {
            ["sourcePath"] = owner.SourcePath, ["streaming"] = owner.Streaming, ["format"] = owner.Format.ToString(),
            ["loadInBackground"] = owner.Background, ["disposeSourceIfNotNeeded"] = owner.DisposeSource,
            ["cacheHit"] = owner.CacheHit, ["cacheKey"] = owner.Key,
            ["reader"] = owner.Reader.Snapshot(), ["atReturn"] = owner.AtReturn!,
            ["recordingCaptured"] = System.Threading.Volatile.Read(ref owner.RecordingCaptured),
            ["recordingPublished"] = System.Threading.Volatile.Read(ref owner.RecordingPublished),
            ["readinessAttempts"] = System.Threading.Volatile.Read(ref owner.ReadinessAttempts),
            ["readinessCause"] = owner.ReadinessCause,
            ["selectedPlaybackCalls"] = System.Threading.Volatile.Read(ref owner.SelectedPlaybackCalls),
            ["firstSelectedPlaybackOrdinal"] = System.Threading.Interlocked.Read(ref owner.FirstSelectedPlaybackOrdinal),
            ["lastSelectedPlaybackCause"] = owner.LastSelectedPlaybackCause,
            ["advanceGeneration"] = System.Threading.Interlocked.Read(ref owner.AdvanceGeneration),
            ["advancePreparedOrdinal"] = System.Threading.Interlocked.Read(ref owner.AdvancePreparedOrdinal),
            ["advancePreparedThread"] = System.Threading.Volatile.Read(ref owner.AdvancePreparedThread),
            ["recordingsAtReturn"] = owner.RecordingsAtReturn, ["admissionsAtReturn"] = owner.AdmissionsAtReturn
        };
    }

    public static Dictionary<string, object>? Mp3SnapshotForClip(AudioClip clip)
        {
        if (!mp3Owners.TryGetValue(clip, out var receipt)) return null;
        var copy = new Dictionary<string, object>(receipt);
        if (mp3FrameOwners.TryGetValue(clip, out var frames)) copy["frames"] = frames.Snapshot();
        return copy;
    }

    public static Dictionary<string, object> Snapshot()
    {
        var result = new Dictionary<string, object> { ["status"] = Status, ["enabled"] = enabled,
            ["mp3CompletionFailures"] = mp3CompletionFailures,
            ["mp3Index"] = PreparedMp3IndexRuntime.Snapshot(),
            ["mp3Provider"] = PreparedMp3Provider.Snapshot(),
            ["readinessStatus"] = PreparedAudioReadiness.Status,
            ["readinessActive"] = PreparedAudioReadiness.Active,
            ["advance"] = PreparedAudioAdvance.Snapshot(),
            ["sourceBytes"] = sourceBytes, ["sourceNativeHashCalls"] = PreparedAudioSourceHash.NativeCalls,
            ["sourceManagedHashCalls"] = PreparedAudioSourceHash.ManagedCalls,
            ["admissions"] = admissions, ["recordings"] = recordings,
            ["cacheHits"] = cache?.Hits ?? 0L, ["cacheMisses"] = cache?.Misses ?? 0L,
            ["cleanupFailures"] = cleanupFailures };
        if (registry != null) lock (RegistryGate)
        {
            result["nativeOnly"] = registry["audioNativeOnly"]; result["reason"] = registry["audioReason"];
            result["epoch"] = registry["audioEpoch"]; result["pending"] = Pending.Count;
        }
        lock (LifetimeGate)
        {
            result["readers"] = lives.Count;
            result["cachedBytes"] = lives.Sum(life => (life.Reader?.CachedBytes ?? 0L));
            result["decoderConstructions"] = lives.Sum(life => (life.Reader?.DecoderConstructions ?? 0L));
            result["replayMismatches"] = lives.Sum(life => (life.Reader?.ReplayMismatches ?? 0L));
        }
        return result;
    }
}
