// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld.IO;
using RuntimeAudioClipLoader;
using UnityEngine;
using Verse;

namespace FixtureMenuObserver;

// Silent functional exercise of native content loading, outer reader methods,
// late Harmony publication and native holder destruction. No playback or clocks.
internal static class C10PreparedAudioProbe
{
    private const string Owner = "local.fixture.menuobserver.c10-prepared-audio";
    private const string Input = @"Z:\Development\Large Projects\RimWorld Loading Optimizer\.rlo-test-instance\game\Mods\725130005\Sounds\Strange_Feeling.ogg";
    private const string ExpectedHash = "2f70c7ddd156d1379d0ddbb8df1f959fec1ea0df28bd62a30ae9dd2371d5f371";
    private static readonly string[] AdditionalInputs = { Path.Combine(Path.GetDirectoryName(Input)!, "Spacekattommy.ogg"), Path.Combine(Path.GetDirectoryName(Input)!, "The_Fallen.ogg") };
    private static readonly List<Case> cases = new();
    private static readonly List<Dictionary<string, object>> factorySnapshots = new();
    private static readonly Dictionary<string, object> report = new();
    private static string directory = "", failure = "";
    private static ModContentHolder<AudioClip>? holder, disposalHolder;
    private static MethodInfo snapshot = null!, nativeReader = null!, read = null!, seek = null!;
    private static Harmony? harmony;
    private static Action? nativeFactoryObservation;
    private static bool nativeConcurrentReaderPassed;
    private static bool NativeEntrySelected => Environment.GetCommandLineArgs().Contains("--fixture-c10-native-entry-probe");
    private static bool compared, captured, clearCompleted, cleanupCompleted, disposalSettled;
    internal static bool Gameplay => Selected && Environment.GetCommandLineArgs().Contains("--fixture-gameplay-smoke");
    internal static bool Selected
    {
        get
        {
            string[] modes = Environment.GetCommandLineArgs().Where(a => a.StartsWith("--fixture-c10-prepared-audio=", StringComparison.Ordinal)).ToArray();
            return modes.Length == 1 && (modes[0] == "--fixture-c10-prepared-audio=prepare" || modes[0] == "--fixture-c10-prepared-audio=warm");
        }
    }

    private sealed class Case
    {
        internal string Name = "", Metadata = "", Samples = "";
        internal string Source = "", SourceHash = "";
        internal AudioClip Clip = null!;
        internal object? Reader;
        internal FileStream? Stream;
        internal bool OriginalDisposable, Destroyed, StreamClosed, ReaderDisposed, DisposalOnly, NativeControl;
        internal Dictionary<string, object> Before = null!, Returned = null!, AfterDemand = null!;
        internal Dictionary<string, object>? BeforeDisposal, AfterDisposal;
    }

    internal static void Start(string output)
    {
        directory = output;
        try
        {
            string[] modes = Environment.GetCommandLineArgs().Where(a => a.StartsWith("--fixture-c10-prepared-audio=", StringComparison.Ordinal)).ToArray();
            Require(modes.Length == 1 && Selected && Environment.GetCommandLineArgs().Contains("--fixture-c10-bootstrap-probe"), "Prepared probe requires one exact mode and bootstrap selection.");
            bool warm = modes[0].EndsWith("=warm", StringComparison.Ordinal);
            report["mode"] = warm ? "warm" : "prepare";
            byte[] bytes = File.ReadAllBytes(Input);
            Require(bytes.Length == 1086292 && Hash(bytes) == ExpectedHash, "Prepared probe input identity changed.");
            Assembly core = AppDomain.CurrentDomain.GetAssemblies().Single(a => !a.ReflectionOnly && a.GetName().Name == "WakeUp");
            Type runtime = core.GetType("WakeUp.PreparedAudioRuntime", true)!;
            snapshot = runtime.GetMethod("Snapshot", BindingFlags.Public | BindingFlags.Static)!;
            nativeReader = runtime.GetMethod("NativeReaderForClip", BindingFlags.Public | BindingFlags.Static)!;
            Type reader = typeof(Manager).Assembly.GetType("RuntimeAudioClipLoader.CustomAudioFileReader", true)!;
            read = AccessTools.Method(reader, "Read", new[] { typeof(float[]), typeof(int), typeof(int) });
            seek = AccessTools.Method(reader, "Seek", new[] { typeof(long), typeof(SeekOrigin) });
            holder = new ModContentHolder<AudioClip>(null!);
            report["initial"] = Snapshot();
            Require(Flag((Dictionary<string, object>)report["initial"], "enabled"), "Prepared runtime did not enable.");
            Case first = Load(warm ? "warm-demand" : "prepare-native");
            if (warm) RequireWarm(first);
            else Require(Number(first.Returned, "recordings") > Number(first.Before, "recordings")
                && Number(first.Returned, "admissions") == Number(first.Before, "admissions")
                && Number(first.Returned, "decoderConstructions") == Number(first.Before, "decoderConstructions") + 1
                && Number(first.Returned, "cachedBytes") == Number(first.Before, "cachedBytes"), "Prepare did not publish a native recording.");
            if (warm)
            {
                var beforeUnrelated = Snapshot();
                using (File.OpenRead(Input)) { }
                var afterUnrelated = Snapshot();
                Require(Number(afterUnrelated, "pending") == Number(beforeUnrelated, "pending")
                    && Number(afterUnrelated, "decoderConstructions") == Number(beforeUnrelated, "decoderConstructions"), "Unmarked file disposal affected prepared readers.");
                report["unrelatedStreamDisposeInert"] = true;
            }
            Consume(first, Gameplay);
            if (Gameplay)
            {
                var consumers = C10AudioGameplayConsumers.Exercise(first.Clip, holder!, first.Name);
                report["naturalGameplayConsumers"] = consumers;
                Require((bool)consumers["passed"], "Native gameplay consumer check failed: " + consumers["failure"]);
            }
            foreach (string input in AdditionalInputs)
            {
                Case additional = Load("additional-" + Path.GetFileNameWithoutExtension(input), input: input);
                if (warm) RequireWarm(additional);
                else Require(Number(additional.Returned, "recordings") == Number(additional.Before, "recordings") + 1, "Additional native asset was not recorded.");
                Consume(additional, Gameplay);
            }
            if (warm)
            {
                Require(Number(first.AfterDemand, "decoderConstructions") == Number(first.Returned, "decoderConstructions") + 1,
                    "Ordinary outer demand did not reconstruct the first decoder.");
                disposalHolder = new ModContentHolder<AudioClip>(null!);
                Case disposal = Load("warm-pending-disposal", disposalHolder, disposalOnly: true);
                RequireWarm(disposal);
                disposal.BeforeDisposal = Snapshot();
                Require(Number(disposal.BeforeDisposal, "pending") == Number(disposal.Before, "pending") + 1
                    && Number(disposal.BeforeDisposal, "decoderConstructions") == Number(disposal.Before, "decoderConstructions")
                    && disposal.Stream != null && disposal.Stream.CanRead, "Disposal case was not still pending with its native file open.");
                // No sample requests or observer patches occur between native
                // return and the holder's original per-file Dispose call.
                disposalHolder.ClearDestroy();
                disposal.AfterDisposal = Snapshot();
                disposal.StreamClosed = disposal.Stream != null && !disposal.Stream.CanRead;
                disposalSettled = Number(disposal.AfterDisposal, "decoderConstructions") == Number(disposal.BeforeDisposal, "decoderConstructions") + 1
                    && Number(disposal.AfterDisposal, "pending") == Number(disposal.BeforeDisposal, "pending") - 1
                    && Number(disposal.AfterDisposal, "cachedBytes") == Number(disposal.BeforeDisposal, "cachedBytes")
                    && Number(disposal.AfterDisposal, "epoch") == Number(disposal.BeforeDisposal, "epoch")
                    && !Flag(disposal.AfterDisposal, "nativeOnly") && disposal.StreamClosed
                    && disposalHolder.contentList.Count == 0 && disposalHolder.extraDisposables.Count == 0;
                Require(disposalSettled, "Native holder disposal did not reconstruct the pending decoder and close its original file.");
                Case second = Load("warm-late-mutation");
                RequireWarm(second);
                Dictionary<string, object> beforeMutation = Snapshot();
                report["beforeMutation"] = beforeMutation;
                Require(Number(beforeMutation, "pending") > 0, "Late mutation had no pending reader to settle.");
                var bootstrap = (Dictionary<string, object>)AppDomain.CurrentDomain.GetData("WakeUp.PreparedAudio.Bootstrap.v1");
                var originalHarmony = (Assembly)bootstrap["oldHarmony"];
                if (NativeEntrySelected) ConcurrentNativeReader(second, bootstrap, originalHarmony, beforeMutation);
                else
                {
                    Assembly additionalHarmony = Assembly.Load(File.ReadAllBytes(originalHarmony.Location));
                    var afterCopy = Snapshot();
                    var registry = (Dictionary<string, object>)bootstrap["audioRegistry"];
                    bool copyDrained = !ReferenceEquals(additionalHarmony, typeof(Harmony).Assembly)
                        && !ReferenceEquals(additionalHarmony, originalHarmony) && (int)registry["audioUnexpectedHarmonyCopies"] == 1
                        && Flag(afterCopy, "nativeOnly") && Number(afterCopy, "pending") == 0
                        && Number(afterCopy, "decoderConstructions") == Number(beforeMutation, "decoderConstructions") + 1;
                    report["afterUnexpectedHarmonyLoad"] = afterCopy;
                    report["unexpectedHarmonyCopyDrainPassed"] = copyDrained;
                    Require(copyDrained, "A late unexpected Harmony load returned before pending readers settled.");
                    harmony = new Harmony(Owner);
                    harmony.Patch(read, prefix: new HarmonyMethod(typeof(C10PreparedAudioProbe), nameof(PrefixFactory)));
                }
                report["afterMutation"] = Snapshot();
                Require(factorySnapshots.Count > 0 && factorySnapshots.All(s => Number(s, "pending") == 0 && Flag(s, "nativeOnly")
                    && Number(s, "decoderConstructions") == Number(beforeMutation, "decoderConstructions") + 1),
                    "Late factory ran before the pending decoder was reconstructed.");
                report["factoryDrainPassed"] = true;
                if (!NativeEntrySelected) Consume(second);
                Case control = Load("sticky-native-control");
                control.NativeControl = true;
                Require(control.Reader!.GetType().GetField("readerStream", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(control.Reader)?.GetType().FullName == "NVorbis.NAudioSupport.VorbisWaveReader", "Native control retained a prepared wrapper.");
                Require(Flag(control.Returned, "nativeOnly") && Number(control.Returned, "admissions") == Number(control.Before, "admissions")
                    && Number(control.Returned, "cachedBytes") == Number(control.Before, "cachedBytes"), "Sticky fallback admitted prepared work.");
                Consume(control);
                foreach (string input in AdditionalInputs)
                {
                    Case nativeControl = Load("native-control-" + Path.GetFileNameWithoutExtension(input), input: input);
                    nativeControl.NativeControl = true;
                    Consume(nativeControl);
                }
                compared = cases.GroupBy(c => c.Source).All(group =>
                {
                    Case native = group.Single(c => c.NativeControl);
                    return group.All(c => c.Metadata == native.Metadata)
                        && group.Where(c => !c.DisposalOnly).All(c => c.Samples == native.Samples);
                });
                Require(compared, "Warm/native metadata or sample bits differ.");
            }
            captured = cases.Where(c => !c.DisposalOnly).All(c => c.Samples.Length != 0);
            report["beforeClear"] = Snapshot();
            Require(Number((Dictionary<string, object>)report["beforeClear"], "replayMismatches") == 0, "Native replay mismatch was recorded.");
        }
        catch (Exception error) { Fail(error); }
        finally
        {
            foreach (ModContentHolder<AudioClip>? ownedHolder in new[] { holder, disposalHolder })
            {
                try { ownedHolder?.ClearDestroy(); }
                catch (Exception error) { Fail(error); }
            }
            clearCompleted = holder != null && holder.contentList.Count == 0 && holder.extraDisposables.Count == 0
                && (disposalHolder == null || disposalHolder.contentList.Count == 0 && disposalHolder.extraDisposables.Count == 0);
            foreach (Case item in cases) item.StreamClosed = item.Stream != null && !item.Stream.CanRead;
            Write();
        }
    }

    private static Case Load(string name, ModContentHolder<AudioClip>? destination = null, bool disposalOnly = false, string input = Input)
    {
        destination ??= holder!;
        var result = new Case { Name = name, Before = Snapshot(), DisposalOnly = disposalOnly, Source = input, SourceHash = Hash(File.ReadAllBytes(input)) };
        cases.Add(result);
        Type fileType = typeof(Manager).Assembly.GetType("RimWorld.IO.FilesystemFile", true)!;
        MethodInfo conversion = fileType.GetMethod("op_Implicit", BindingFlags.Public | BindingFlags.Static)!;
        var file = (VirtualFile)conversion.Invoke(null, new object[] { new FileInfo(input) })!;
        LoadedContentItem<AudioClip> item = ModContentLoader<AudioClip>.LoadItem(file);
        if (item == null || item.contentItem == null) throw new InvalidOperationException("Native LoadItem returned no clip.");
        result.Clip = item.contentItem;
        // Adopt immediately, including failure paths; these are the native
        // returned objects, with no replacement of the public disposable.
        destination.contentList.Add(name, item.contentItem);
        if (item.extraDisposable != null) destination.extraDisposables.Add(item.extraDisposable);
        result.Stream = item.extraDisposable as FileStream;
        result.OriginalDisposable = result.Stream != null && result.Stream.GetType() == typeof(FileStream)
            && ReferenceEquals(destination.extraDisposables.Last(), item.extraDisposable) && result.Stream.Name == input;
        Require(result.OriginalDisposable && ReferenceEquals(item.internalFile, file), "Native file/disposable identity changed.");
        result.Returned = Snapshot();
        result.Reader = nativeReader.Invoke(null, new object[] { result.Clip });
        Require(result.Reader?.GetType().FullName == "RuntimeAudioClipLoader.CustomAudioFileReader", "Actual native outer reader is absent.");
        result.Metadata = result.Clip.samples + ":" + result.Clip.channels + ":" + result.Clip.frequency + ":"
            + BitConverter.ToString(BitConverter.GetBytes(result.Clip.length)) + ":" + result.Clip.loadType + ":" + result.Clip.loadState
            + ":" + Manager.GetAudioClipLoadType(result.Clip) + ":" + Manager.GetAudioClipLoadState(result.Clip);
        return result;
    }

    private static void RequireWarm(Case item) => Require(Number(item.Returned, "admissions") == Number(item.Before, "admissions") + 1
        && Number(item.Returned, "cachedBytes") > Number(item.Before, "cachedBytes")
        && Number(item.Returned, "decoderConstructions") == Number(item.Before, "decoderConstructions")
        && Number(item.Returned, "pending") > 0 && !Flag(item.Returned, "nativeOnly"), "Warm native return did not retain a pending decoder after serving cached bytes.");

    private static void Consume(Case item, bool worker = false)
    {
        if (worker)
        {
            Task task = Task.Run(() => Consume(item));
            if (!task.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Native audio worker demand could not complete while main waited.");
            report["workerDemandWithoutMainProgress"] = true;
            return;
        }
        ConsumeSamples(item);
        item.AfterDemand = Snapshot();
    }

    private static void ConsumeSamples(Case item)
    {
        // Native reader demand and sample capture only. The concurrent loader
        // owns the registry gate, so no global Snapshot belongs on this worker.
        var signatures = new List<string>();
        foreach (long position in new long[] { 0, 8192, 0 })
        {
            long actual = (long)seek.Invoke(item.Reader, new object[] { position, SeekOrigin.Begin })!;
            var samples = new float[4096];
            int returned = (int)read.Invoke(item.Reader, new object[] { samples, 0, samples.Length })!;
            Require(returned >= 0 && returned <= samples.Length, "Native reader returned an invalid count.");
            var bytes = new byte[samples.Length * sizeof(float)];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
            signatures.Add(position + ":" + actual + ":" + returned + ":" + Hash(bytes));
        }
        item.Samples = string.Join("|", signatures);
    }

    private static void ConcurrentNativeReader(Case item, Dictionary<string, object> bootstrap,
        Assembly originalHarmony, Dictionary<string, object> beforeMutation)
    {
        var result = new Dictionary<string, object> { ["passed"] = false, ["actualReaderIdentity"] = false };
        report["nativeConcurrentReader"] = result;
        var registry = (Dictionary<string, object>)bootstrap["audioRegistry"];
        object gate = registry["audioRegistryGate"];
        var pending = (Dictionary<object, Func<int>>)registry["audioPending"];
        var observe = (Action<Action<IntPtr>?>)registry["audioNativeEntryObserve"];
        var stats = (Func<ulong[]>)registry["audioNativeEntryStats"];
        object lower = item.Reader!.GetType().GetField("readerStream", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(item.Reader)!;
        Require(lower.GetType().FullName == "WakeUp.PreparedAudioWaveStream", "Concurrent case is not the actual prepared lower reader.");
        MethodInfo lowerSnapshot = lower.GetType().GetMethod("Snapshot", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Dictionary<string, object> ReaderSnapshot() => (Dictionary<string, object>)lowerSnapshot.Invoke(lower, null)!;
        var initialReader = ReaderSnapshot();
        Require(Number(initialReader, "decoderConstructions") == 0 && Flag(initialReader, "warm"), "Concurrent reader was already constructed.");
        result["readerBefore"] = initialReader;
        result["nativeBefore"] = stats();
        byte[] bytes = File.ReadAllBytes(originalHarmony.Location);
        Assembly[] before = AppDomain.CurrentDomain.GetAssemblies();
        var timeout = TimeSpan.FromSeconds(15);
        using var drainStarted = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var entered = new ManualResetEventSlim();
        using var patchFinished = new ManualResetEventSlim();
        Func<int> original;
        int settled = 0, sequence = 0, settlementOrdinal = 0, factoryOrdinal = 0, factoryBeforeSettlement = 0;
        int patchThread = 0, demandThread = 0, factoryCalls = 0, settlementCalls = 0;
        IntPtr observedIdentity = IntPtr.Zero;
        IntPtr originalIdentity = (IntPtr)registry["audioNativeOriginalHarmony"];
        IntPtr replacementIdentity = (IntPtr)registry["audioNativeReplacementHarmony"];
        Func<int> wrapped;
        lock (gate)
        {
            Require(pending.TryGetValue(lower, out original!) && ReferenceEquals(original.Target, lower)
                && original.Method.Name == "SettleNativeOutcome", "Pending action does not belong to the actual native outer reader's proxy.");
            wrapped = () =>
            {
                drainStarted.Set();
                Require(release.Wait(timeout), "Concurrent native reader drain was not released.");
                int outcome = original();
                Interlocked.Increment(ref settlementCalls);
                settlementOrdinal = Interlocked.Increment(ref sequence);
                Volatile.Write(ref settled, 1);
                return outcome;
            };
            pending[lower] = wrapped;
        }
        result["actualReaderIdentity"] = true;
        Exception? loadError = null, patchError = null, demandError = null;
        Assembly? loaded = null, discovered = null;
        Thread? patcher = null, consumer = null;
        var loader = new Thread(() => { try { loaded = Assembly.Load(bytes); } catch (Exception error) { loadError = error; } })
            { IsBackground = true, Name = "C10 real-reader assembly loader" };
        bool loaderStarted = false, patcherStarted = false, consumerStarted = false;
        bool loaderJoined = false, patcherJoined = false, consumerJoined = false;
        try
        {
            nativeFactoryObservation = () =>
            {
                Interlocked.Increment(ref factoryCalls);
                factoryOrdinal = Interlocked.Increment(ref sequence);
                if (Volatile.Read(ref settled) == 0) Interlocked.Increment(ref factoryBeforeSettlement);
            };
            loader.Start(); loaderStarted = true;
            Require(drainStarted.Wait(timeout), "AssemblyLoad did not enter the actual reader's settlement action.");
            discovered = AppDomain.CurrentDomain.GetAssemblies().Single(a => !a.ReflectionOnly && a.GetName().Name == "0Harmony"
                && !before.Any(old => ReferenceEquals(old, a)));
            Action invoke = C10NativeEntryProbe.UpdateInvocation(discovered,
                typeof(C10PreparedAudioProbe).GetMethod(nameof(NativeEntryTarget), BindingFlags.NonPublic | BindingFlags.Static)!,
                typeof(C10PreparedAudioProbe).GetMethod(nameof(PrefixFactory), BindingFlags.NonPublic | BindingFlags.Static)!, true);
            observe(identity =>
            {
                if (Thread.CurrentThread.ManagedThreadId != Volatile.Read(ref patchThread)
                    || identity == originalIdentity || identity == replacementIdentity) return;
                observedIdentity = identity; entered.Set();
            });
            patcher = new Thread(() =>
            {
                Volatile.Write(ref patchThread, Thread.CurrentThread.ManagedThreadId);
                try { invoke(); } catch (Exception error) { patchError = error; } finally { patchFinished.Set(); }
            }) { IsBackground = true, Name = "C10 real-reader third Harmony entry" };
            patcher.Start(); patcherStarted = true;
            int signal = WaitHandle.WaitAny(new[] { entered.WaitHandle, patchFinished.WaitHandle }, timeout);
            Require(signal == 0 && observedIdentity != IntPtr.Zero && Volatile.Read(ref factoryCalls) == 0,
                "Third Harmony did not wait at its native entry before factory execution: " + patchError);
            consumer = new Thread(() =>
            {
                demandThread = Thread.CurrentThread.ManagedThreadId;
                try { ConsumeSamples(item); } catch (Exception error) { demandError = error; }
            }) { IsBackground = true, Name = "C10 actual outer reader demand" };
            consumer.Start(); consumerStarted = true;
            consumerJoined = consumer.Join(timeout);
            Require(consumerJoined && demandError == null, "Actual outer demand did not finish while AssemblyLoad held the registry: " + demandError);
            var afterWorker = ReaderSnapshot();
            result["readerAfterWorkerBeforeRelease"] = afterWorker;
            result["workerSamples"] = item.Samples;
            Require(Number(afterWorker, "decoderConstructions") == 1 && Number(afterWorker, "initializationAttempts") == 1
                && Number(afterWorker, "firstReconstructionThread") == demandThread && Number(afterWorker, "replayMismatches") == 0
                && Flag(afterWorker, "nativeReady") && Number(afterWorker, "returnedBytes") > Number(initialReader, "returnedBytes")
                && Volatile.Read(ref settled) == 0 && Volatile.Read(ref factoryCalls) == 0,
                "Worker demand did not reconstruct exactly once before the held settlement and factory.");
            release.Set();
            loaderJoined = loader.Join(timeout); patcherJoined = patcher.Join(timeout);
            Require(loaderJoined && patcherJoined && loadError == null, "Native reader challenge did not join successfully: " + loadError);
            Require(ReferenceEquals(loaded, discovered), "Concurrent discovery and load returned different assembly objects.");
            Require(patchError == null || (patchError.GetBaseException() is ArgumentException ordinary
                && ordinary.ParamName == "typeArguments" && ordinary.Message.StartsWith("Invalid generic arguments", StringComparison.Ordinal)),
                "Unexpected third-Harmony error after entry: " + patchError);
            Require(settlementCalls == 1 && factoryCalls > 0 && factoryBeforeSettlement == 0
                && settlementOrdinal > 0 && factoryOrdinal > settlementOrdinal, "Actual factory preceded completion of native reader settlement.");
            lock (gate) Require(!pending.ContainsKey(lower), "Settled actual reader remains registered.");
            item.AfterDemand = Snapshot();
            var finalReader = ReaderSnapshot();
            Require(Flag(item.AfterDemand, "nativeOnly") && Number(item.AfterDemand, "pending") == 0
                && Number(item.AfterDemand, "epoch") > Number(beforeMutation, "epoch")
                && Number(finalReader, "decoderConstructions") == 1 && Number(finalReader, "initializationAttempts") == 1
                && Number(finalReader, "replayMismatches") == 0, "Drain reconstructed the consumed decoder again or left admission open.");
            result["readerAfterDrain"] = finalReader;
            result["nativeAfter"] = stats();
            result["workerThread"] = demandThread; result["patchThread"] = patchThread;
            result["settlementOrdinal"] = settlementOrdinal; result["factoryOrdinal"] = factoryOrdinal;
            result["factoryBeforeSettlement"] = factoryBeforeSettlement; result["factoryCalls"] = factoryCalls;
            result["postFactoryError"] = patchError?.GetBaseException().ToString() ?? "";
            result["updateWrapperReturnedNormally"] = patchError == null;
            result["postFactoryArgumentException"] = patchError?.GetBaseException() is ArgumentException;
            result["sameAssemblyReferenceAfterLoad"] = true; result["workersJoined"] = true;
            result["nativeAssemblyIdentity"] = observedIdentity.ToString();
            result["passed"] = true;
            nativeConcurrentReaderPassed = true;
            report["afterUnexpectedHarmonyLoad"] = item.AfterDemand;
            report["unexpectedHarmonyCopyDrainPassed"] = true;
            report["workerDemandWithoutMainProgress"] = true;
        }
        finally
        {
            release.Set();
            if (consumerStarted && !consumerJoined) consumerJoined = consumer!.Join(timeout);
            if (loaderStarted && !loaderJoined) loaderJoined = loader.Join(timeout);
            if (patcherStarted && !patcherJoined) patcherJoined = patcher!.Join(timeout);
            observe(null); nativeFactoryObservation = null;
            if ((!loaderStarted || loaderJoined) && (!patcherStarted || patcherJoined) && (!consumerStarted || consumerJoined))
                lock (gate)
                    if (pending.TryGetValue(lower, out Func<int> current) && ReferenceEquals(current, wrapped)) pending[lower] = original;
            Require((!loaderStarted || loaderJoined) && (!patcherStarted || patcherJoined) && (!consumerStarted || consumerJoined),
                "A native reader challenge worker remained alive after release.");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int NativeEntryTarget() => 31;

    // Harmony calls this factory during wrapper construction, after its entry
    // guard but before publishing the replacement. The returned prefix is inert.
    private static MethodInfo PrefixFactory(MethodBase original)
    {
        nativeFactoryObservation?.Invoke();
        factorySnapshots.Add(Snapshot());
        return AccessTools.Method(typeof(C10PreparedAudioProbe), nameof(InertPrefix));
    }
    private static void InertPrefix() { }

    internal static void BeforeExit()
    {
        if (directory.Length == 0) return;
        if (Gameplay)
        {
            var state = Snapshot();
            Require(Current.ProgramState == ProgramState.Playing && Number(state, "readers") == 0,
                "Gameplay update retained destroyed prepared readers before observer cleanup.");
            report["gameplayRuntimeCleanupPassed"] = true;
        }
        foreach (Case item in cases)
        {
            try
            {
                item.Destroyed = item.Clip is not null && item.Clip == null;
                Require(item.Destroyed && item.StreamClosed, "Holder did not destroy the owned clip and close its original file.");
            }
            catch (Exception error) { Fail(error); }
            try
            {
                if (item.Reader is IDisposable disposable) { disposable.Dispose(); item.ReaderDisposed = true; }
                foreach (string field in new[] { "audioClipLoadType", "audioLoadState" })
                    if (item.Clip is not null && AccessTools.Field(typeof(Manager), field)?.GetValue(null) is IDictionary map) map.Remove(item.Clip);
            }
            catch (Exception error) { Fail(error); }
            finally
            {
                // Failure cleanup does not change the previously recorded
                // holder result; it only releases remaining test-owned handles.
                try { item.Stream?.Dispose(); }
                catch (Exception error) { Fail(error); }
                if (item.Clip != null) UnityEngine.Object.Destroy(item.Clip);
            }
        }
        try { harmony?.UnpatchAll(Owner); }
        catch (Exception error) { Fail(error); }
        try
        {
            if (snapshot != null)
            {
                Dictionary<string, object> after = Snapshot();
                report["afterCleanup"] = after;
                Require(Number(after, "pending") == 0, "Pending prepared readers remain after holder cleanup.");
                Require(Number(after, "cleanupFailures") == 0, "Private reader cleanup failed.");
            }
        }
        catch (Exception error) { Fail(error); }
        cleanupCompleted = true;
        Write();
    }

    private static Dictionary<string, object> Snapshot() => (Dictionary<string, object>)snapshot.Invoke(null, null)!;
    internal static void AfterGameplayReload()
    {
        if (!Gameplay) return;
        var state = Snapshot();
        Require(Current.ProgramState == ProgramState.Playing && Flag(state, "enabled")
            && Number(state, "readers") == 0 && Number(state, "pending") == 0
            && Number(state, "cleanupFailures") == 0, "Audio lifetime state did not survive native save/reload cleanly.");
        report["gameplaySaveReloadPassed"] = true;
        Write();
    }
    private static long Number(Dictionary<string, object> state, string key) => Convert.ToInt64(state[key], CultureInfo.InvariantCulture);
    private static bool Flag(Dictionary<string, object> state, string key) => (bool)state[key];
    private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
    private static void Fail(Exception error) { failure += (failure.Length == 0 ? "" : "\n") + error; }
    private static string Hash(byte[] bytes) { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    private static void Write()
    {
        bool disposalPassed = disposalSettled && cases.Any(c => c.DisposalOnly)
            && cases.Where(c => c.DisposalOnly).All(c => c.Destroyed && c.StreamClosed);
        report["schema"] = "c10-prepared-audio-functional.v1";
        report["source"] = Input; report["sourceSha256"] = ExpectedHash;
        report["failure"] = failure; report["sampleComparisonPassed"] = compared;
        report["nativeSamplesCaptured"] = captured;
        report["pendingDisposalSettledBeforeReturn"] = disposalSettled;
        report["pendingDisposalPassed"] = disposalPassed;
        report["nativeConcurrentReaderPassed"] = nativeConcurrentReaderPassed;
        report["holderClearCompleted"] = clearCompleted; report["cleanupCompleted"] = cleanupCompleted;
        report["functionalPassed"] = failure.Length == 0 && captured && (report.TryGetValue("mode", out object mode) && Equals(mode, "prepare") || compared && disposalPassed)
            && clearCompleted && cleanupCompleted && (!NativeEntrySelected || !Equals(report["mode"], "warm") || nativeConcurrentReaderPassed);
        report["audioSourceCreated"] = false; report["audibleTestPerformed"] = false; report["performanceClaim"] = false;
        report["factorySnapshots"] = factorySnapshots;
        report["cases"] = cases.Select(c => new Dictionary<string, object>
        {
            ["name"] = c.Name, ["metadata"] = c.Metadata, ["sampleSignatures"] = c.Samples,
            ["source"] = c.Source, ["sourceSha256"] = c.SourceHash, ["nativeControl"] = c.NativeControl,
            ["disposalOnly"] = c.DisposalOnly, ["includedInSampleComparison"] = !c.DisposalOnly,
            ["originalFileStreamDisposable"] = c.OriginalDisposable, ["clipDestroyed"] = c.Destroyed,
            ["streamClosed"] = c.StreamClosed, ["readerDisposed"] = c.ReaderDisposed,
            ["before"] = c.Before, ["nativeReturn"] = c.Returned, ["afterDemand"] = c.AfterDemand,
            ["beforeDisposal"] = c.BeforeDisposal!, ["afterDisposal"] = c.AfterDisposal!
        }).ToArray();
        File.WriteAllText(Path.Combine(directory, "c10-prepared-audio-probe.json"), Json(report) + "\n", new UTF8Encoding(false));
    }
    private static string Json(object? value)
    {
        if (value == null) return "null";
        if (value is string text)
        {
            var quoted = new StringBuilder("\"");
            foreach (char c in text)
                if (c == '\\' || c == '"') quoted.Append('\\').Append(c);
                else if (c < 32) quoted.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else quoted.Append(c);
            return quoted.Append('"').ToString();
        }
        if (value is bool flag) return flag ? "true" : "false";
        if (value is IDictionary<string, object> map) return "{" + string.Join(",", map.Select(pair => Json(pair.Key) + ":" + Json(pair.Value))) + "}";
        if (value is IEnumerable sequence) return "[" + string.Join(",", sequence.Cast<object>().Select(Json)) + "]";
        return Convert.ToString(value, CultureInfo.InvariantCulture)!;
    }
}
