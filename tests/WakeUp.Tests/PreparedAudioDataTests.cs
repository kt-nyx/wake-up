// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using NAudio.Wave;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class PreparedAudioDataTests
{
    private string root = null!;
    private static string Key => new('A', 64);
    private static CacheLaunchPolicy Policy(string action = "normal")
    {
        CacheLaunchPolicy? selected = null;
        return CacheLaunchPolicy.Latch(ref selected, action, () => { });
    }
    [SetUp] public void SetUp() => root = Path.Combine(Path.GetTempPath(), "wake-up-audio-test-" + Guid.NewGuid().ToString("N"));
    [TearDown] public void TearDown() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    private static PreparedAudioData OneRead(params byte[] values)
    {
        var data = new PreparedAudioData { Length = 20, Channels = 2, Frequency = 44100,
            Encoding = 3, BitsPerSample = 32, BlockAlign = 8, AverageBytesPerSecond = 352800 };
        data.Operations.Add(new PreparedAudioData.Operation { Offset = 2, Count = values.Length, Returned = values.Length, Bytes = values });
        return data;
    }

    [Test]
    public void TranscriptRoundTripPreservesRawFloatBitsAndLongPositionValues()
    {
        int[] bits = { 0, unchecked((int)0x80000000), 0x3f800000, 0x7fc01234, unchecked((int)0xff800000) };
        var bytes = new byte[bits.Length * 4]; Buffer.BlockCopy(bits, 0, bytes, 0, bytes.Length);
        var data = OneRead(bytes); data.Length = (long)int.MaxValue + 1024;
        data.Operations.Add(new PreparedAudioData.Operation { Kind = PreparedAudioData.OperationKind.SetPosition, Value = -4 });
        data.Operations.Add(new PreparedAudioData.Operation { Kind = PreparedAudioData.OperationKind.GetPosition, Value = long.MaxValue });
        var restored = PreparedAudioData.Decode(data.Encode());
        Assert.That(restored.Operations[0].Bytes, Is.EqualTo(bytes));
        Assert.That((restored.Operations[0].Offset, restored.Operations[0].Count), Is.EqualTo((2, bytes.Length)));
        Assert.That(restored.Operations[1].Value, Is.EqualTo(-4));
        Assert.That(restored.Operations[2].Value, Is.EqualTo(long.MaxValue));
        Assert.That((restored.Length, restored.Channels, restored.Frequency), Is.EqualTo((data.Length, 2, 44100)));
    }

    [TestCase("former-float-version")]
    [TestCase("former-byte-version")]
    [TestCase("former-linear-wav-version")]
    [TestCase("format-kind")]
    [TestCase("format-payload-size")]
    [TestCase("channels")]
    [TestCase("encoding")]
    [TestCase("bit-depth")]
    [TestCase("block-align")]
    [TestCase("byte-rate")]
    [TestCase("extra-size")]
    [TestCase("zero-operations")]
    [TestCase("operation-kind")]
    [TestCase("negative-read")]
    [TestCase("oversized-offset")]
    [TestCase("returned-too-large")]
    [TestCase("truncated-operation")]
    [TestCase("truncated-payload")]
    [TestCase("trailing-byte")]
    public void InvalidTranscriptIsRejectedBeforePublication(string fault)
    {
        byte[] bytes = OneRead(1, 2).Encode();
        switch (fault)
        {
            case "former-float-version": Put(0, 1); break;
            case "former-byte-version": Put(0, 2); break;
            case "former-linear-wav-version": Put(0, 3); break;
            case "format-kind": Put(40, 2); break;
            case "format-payload-size": Put(44, 1); break;
            case "channels": Put(12, 0); break;
            case "encoding": Put(20, 0xfffe); break;
            case "bit-depth": Put(24, 24); break;
            case "block-align": Put(28, 7); break;
            case "byte-rate": Put(32, 1); break;
            case "extra-size": Put(36, 2); break;
            case "zero-operations": Put(PreparedAudioData.HeaderBytes - 4, 0); break;
            case "operation-kind": bytes[PreparedAudioData.HeaderBytes] = 3; break;
            case "negative-read": Put(PreparedAudioData.HeaderBytes + 5, -1); break;
            case "oversized-offset": Put(PreparedAudioData.HeaderBytes + 1, int.MaxValue); break;
            case "returned-too-large": Put(PreparedAudioData.HeaderBytes + 9, 3); break;
            case "truncated-operation": Array.Resize(ref bytes, PreparedAudioData.HeaderBytes + 6); break;
            case "truncated-payload": Array.Resize(ref bytes, bytes.Length - 1); break;
            case "trailing-byte": Array.Resize(ref bytes, bytes.Length + 1); break;
        }
        Assert.Throws<InvalidDataException>((Action)(() => PreparedAudioData.Decode(bytes)));
        void Put(int offset, int value) => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, bytes, offset, 4);
    }

    [Test]
    public void CacheReopensExactDataAndCorruptionBecomesMiss()
    {
        var data = OneRead(1, 0, 255);
        using (var cache = new PreparedAudioCache(root, Policy()))
        {
            Assert.That(cache.Publish(Key, data), Is.True);
            Assert.That(cache.Writes, Is.EqualTo(1));
            Assert.That(cache.WrittenBytes, Is.EqualTo(data.Encode().Length));
        }
        using (var cache = new PreparedAudioCache(root, Policy()))
        {
            Assert.That(cache.Read(Key)!.Encode(), Is.EqualTo(data.Encode()));
            Assert.That(cache.Hits, Is.EqualTo(1));
            Assert.That(cache.ReadBytes, Is.EqualTo(data.Encode().Length));
        }
        string path = Path.Combine(root, "WakeUp", "PreparedAudio", "v1", Key + ".bin");
        byte[] corrupted = File.ReadAllBytes(path); corrupted[corrupted.Length - 1] ^= 0x80; File.WriteAllBytes(path, corrupted);
        using var reopened = new PreparedAudioCache(root, Policy());
        Assert.That(reopened.Read(Key), Is.Null);
        Assert.That(reopened.Misses, Is.EqualTo(1));
        Assert.That(reopened.Publish(Key, data), Is.True);
        Assert.That(reopened.Read(Key), Is.Not.Null);
    }

    [Test]
    public void OldByteTranscriptInsideValidStoreEnvelopeBecomesCacheMiss()
    {
        // Actual v2 layout: 24-byte header without the five v3 format fields.
        using var payload = new MemoryStream();
        using (var writer = new BinaryWriter(payload, System.Text.Encoding.UTF8, true))
        {
            writer.Write(2); writer.Write(20L); writer.Write(2); writer.Write(44100); writer.Write(1);
            writer.Write((byte)0); writer.Write(0); writer.Write(2); writer.Write(2); writer.Write(0L);
            writer.Write(new byte[] { 1, 2 });
        }
        byte[] old = payload.ToArray();
        string storeRoot = Path.Combine(root, "WakeUp", "PreparedAudio", "v1");
        using (var store = new OwnedCacheStore(storeRoot, PreparedAudioData.MaximumBytes - OwnedCacheStore.EnvelopeBytes,
            4 * 1024 * 1024, Policy())) Assert.That(store.Publish(Key, old), Is.True);
        using var cache = new PreparedAudioCache(root, Policy());
        Assert.That(cache.Read(Key), Is.Null);
        Assert.That(cache.Misses, Is.EqualTo(1));
        Assert.That(cache.Publish(Key, OneRead(1, 2)), Is.True);
        Assert.That(cache.Read(Key), Is.Not.Null);
    }

    [Test]
    public void StorageFailureBypassAndDisposedCacheFallBackWithoutExceptions()
    {
        Directory.CreateDirectory(root);
        string blocked = Path.Combine(root, "file"); File.WriteAllText(blocked, "not a directory");
        using var unavailable = new PreparedAudioCache(blocked, Policy());
        Assert.That(unavailable.Read(Key), Is.Null);
        Assert.That(unavailable.Publish(Key, OneRead(1)), Is.False);
        using var bypass = new PreparedAudioCache(Path.Combine(root, "bypassed"), Policy("bypass"));
        Assert.That(bypass.Read(Key), Is.Null);
        Assert.That(bypass.Publish(Key, OneRead(1)), Is.False);
        Assert.That(Directory.Exists(Path.Combine(root, "bypassed")), Is.False);
        bypass.Dispose();
        Assert.That(bypass.Read(Key), Is.Null);
        Assert.That(bypass.Publish(Key, OneRead(1)), Is.False);
    }

    [Test]
    public void SourceKeyBindsBytesRuntimeAndFormatWithoutConsumingOrReopeningHandle()
    {
        Directory.CreateDirectory(root);
        string first = Path.Combine(root, "one.ogg"), second = Path.Combine(root, "other.ogg");
        File.WriteAllBytes(first, new byte[] { 1, 2, 3, 4 }); File.Copy(first, second);
        using var source = new FileStream(first, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var other = new FileStream(second, FileMode.Open, FileAccess.Read, FileShare.Read);
        source.Position = 2;
        Assert.That(PreparedAudioCache.SourceKey(source, "runtime-one", PreparedAudioData.CacheIdentity, out long skipped), Is.Null);
        Assert.That(skipped, Is.Zero);
        Assert.That(source.Position, Is.EqualTo(2));
        Assert.That(source.ReadByte(), Is.EqualTo(3));
        source.Position = 0;
        string? key = PreparedAudioCache.SourceKey(source, "runtime-one", PreparedAudioData.CacheIdentity, out long read);
        Assert.That(key, Does.Match("^[A-F0-9]{64}$"));
        // Retain the pre-acceleration cache key, including its full-content
        // digest, source length, runtime and format serialization.
        Assert.That(key, Is.EqualTo("12DC2AE2F6A0ECD245D1804C8FEAED390E37ED272E5741487B1A93C7D1E9E319"));
        Assert.That(read, Is.EqualTo(4)); Assert.That(source.Position, Is.Zero);
        Assert.That(PreparedAudioCache.SourceKey(other, "runtime-one", PreparedAudioData.CacheIdentity, out _), Is.EqualTo(key));
        Assert.That(PreparedAudioCache.SourceKey(source, "runtime-two", PreparedAudioData.CacheIdentity, out _), Is.Not.EqualTo(key));
        Assert.That(PreparedAudioCache.SourceKey(source, "runtime-one", "ogg", out _), Is.Not.EqualTo(key));
        Assert.That(source.ReadByte(), Is.EqualTo(1));
        other.Dispose();
        File.WriteAllBytes(second, new byte[] { 1, 2, 3, 5 });
        using var changed = new FileStream(second, FileMode.Open, FileAccess.Read, FileShare.Read);
        Assert.That(PreparedAudioCache.SourceKey(changed, "runtime-one", PreparedAudioData.CacheIdentity, out _), Is.Not.EqualTo(key));
        source.Dispose();
        Assert.That(PreparedAudioCache.SourceKey(source, "runtime-one", PreparedAudioData.CacheIdentity, out _), Is.Null);
    }

    // Read results depend on the operation history. Its deliberately asymmetric
    // Position getter makes replacing replay with a guessed seek observable.
    private sealed class HistoryStream : WaveStream
    {
        internal readonly List<string> History = new();
        private readonly WaveFormat format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 1);
        private int cursor, reads;
        internal int Capacity = 8, Disposals, FormatGets, LengthGets, ThrowOnRead, Bias;
        internal long ReportedLength = 8;
        internal Exception NativeFailure = new IOException("native-read-failure");
        internal bool ThrowOnDispose, ThrowOnFormat, ThrowOnLength;
        public override WaveFormat WaveFormat { get { FormatGets++; if (ThrowOnFormat) throw NativeFailure; return format; } }
        public override long Length { get { LengthGets++; if (ThrowOnLength) throw NativeFailure; return ReportedLength; } }
        public override long Position
        {
            get { History.Add("get"); return cursor * 1000L + 7; }
            set { History.Add("set:" + value); cursor = (int)Math.Max(0L, Math.Min(Capacity, value)); }
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            History.Add("read:" + offset + ":" + count);
            if (++reads == ThrowOnRead) throw NativeFailure;
            int returned = Math.Min(count, Capacity - cursor);
            for (int i = 0; i < returned; i++) buffer[offset + i] = unchecked((byte)(cursor++ * 10 + History.Count + Bias));
            return returned;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { Disposals++; if (ThrowOnDispose) throw new IOException("dispose-failure"); }
            base.Dispose(disposing);
        }
    }

    private static PreparedAudioData Transcript()
    {
        using var recording = new PreparedAudioWaveStream(new HistoryStream());
        _ = recording.WaveFormat; _ = recording.Length;
        recording.Read(new byte[8], 2, 4);
        recording.Seek(2, SeekOrigin.Begin);
        recording.Read(new byte[8], 1, 3);
        return recording.FinishRecording()!;
    }
    private static void EqualRead(WaveStream actual, WaveStream expected, int offset, int count)
    {
        byte[] expectedBytes = Enumerable.Repeat((byte)213, offset + count + 3).ToArray();
        byte[] actualBytes = expectedBytes.ToArray();
        Assert.That(actual.Read(actualBytes, offset, count), Is.EqualTo(expected.Read(expectedBytes, offset, count)));
        Assert.That(actualBytes, Is.EqualTo(expectedBytes));
    }

    [Test]
    public void RecordingCapturesMetadataOnlyWhenNativeGettersAreCalledAndFreezesOnce()
    {
        var native = new HistoryStream { ReportedLength = (long)int.MaxValue + 10 };
        using var recording = new PreparedAudioWaveStream(native);
        Assert.That((native.FormatGets, native.LengthGets), Is.EqualTo((0, 0)));
        _ = recording.WaveFormat; _ = recording.WaveFormat;
        Assert.That(recording.Length, Is.EqualTo(native.ReportedLength));
        recording.Read(new byte[8], 2, 4);
        var data = recording.FinishRecording()!;
        Assert.That((native.FormatGets, native.LengthGets), Is.EqualTo((2, 1)));
        Assert.That(data.Length, Is.EqualTo(native.ReportedLength));
        byte[] frozen = data.Encode();
        recording.Read(new byte[4], 0, 4);
        Assert.That(data.Encode(), Is.EqualTo(frozen));
        Assert.That(recording.FinishRecording(), Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ExactTranscriptAvoidsDecoderThenReplaysHistoryForContinuationAndBackwardSeek(bool takeOwnership)
    {
        using var baseline = new HistoryStream(); var delayed = new HistoryStream();
        using var playback = takeOwnership ? PreparedAudioWaveStream.FromOwnedData(Transcript(), () => delayed)
            : new PreparedAudioWaveStream(Transcript(), () => delayed);
        _ = playback.WaveFormat; _ = playback.Length;
        EqualRead(playback, baseline, 2, 4);
        Assert.That(playback.Seek(2, SeekOrigin.Begin), Is.EqualTo(baseline.Seek(2, SeekOrigin.Begin)));
        EqualRead(playback, baseline, 1, 3);
        Assert.That(playback.DecoderConstructions, Is.Zero);
        Assert.That(playback.CachedBytes, Is.EqualTo(7));
        EqualRead(playback, baseline, 0, 2);
        Assert.That(playback.DecoderConstructions, Is.EqualTo(1));
        Assert.That(playback.ReplayedBytes, Is.EqualTo(7));
        Assert.That(playback.ReplayMismatches, Is.Zero);
        Assert.That(delayed.History, Is.EqualTo(baseline.History));
        Assert.That(playback.Seek(0, SeekOrigin.Begin), Is.EqualTo(baseline.Seek(0, SeekOrigin.Begin)));
        EqualRead(playback, baseline, 2, 3);
        Assert.That(delayed.History, Is.EqualTo(baseline.History));
    }

    [Test]
    public void SelectedPlaybackPreparationPreservesHistoryAndLeavesOtherReadersDeferred()
    {
        using var baseline = new HistoryStream(); var delayed = new HistoryStream();
        using var playback = new PreparedAudioWaveStream(Transcript(), () => delayed);
        using var unrelated = new PreparedAudioWaveStream(Transcript(), () => new HistoryStream());
        EqualRead(playback, baseline, 2, 4);
        var before = playback.Snapshot();
        Assert.That(playback.PrepareForPlayback("music"), Is.True);
        Assert.That(playback.PrepareForPlayback("music"), Is.False);
        var after = playback.Snapshot();
        foreach (string key in new[] { "readCalls", "returnedBytes", "positionGets", "positionSets", "operationCount" })
            Assert.That(after[key], Is.EqualTo(before[key]), key);
        Assert.That(after["firstReconstructionCause"], Is.EqualTo("readiness-music"));
        Assert.That(delayed.History, Is.EqualTo(baseline.History));
        Assert.That(unrelated.DecoderConstructions, Is.Zero);
        EqualRead(playback, baseline, 0, 2);
        Assert.That(delayed.History, Is.EqualTo(baseline.History));
        playback.Dispose();
        Assert.That(playback.PrepareForPlayback("music"), Is.False);
        Assert.That(delayed.Disposals, Is.EqualTo(1));
    }

    [Test]
    public void PlaybackPreparationLatchesNativeFailureForTheOriginalConsumer()
    {
        var error = new IOException("selected-native-failure");
        var delayed = new HistoryStream { ThrowOnRead = 1, NativeFailure = error };
        using var playback = new PreparedAudioWaveStream(Transcript(), () => delayed);
        playback.Read(new byte[8], 2, 4);
        Assert.That(playback.PrepareForPlayback("oneshot"), Is.True);
        Assert.That(playback.PrepareForPlayback("oneshot"), Is.False);
        Assert.That(Assert.Throws<IOException>((Action)(() => playback.Read(new byte[8], 0, 1))), Is.SameAs(error));
        Assert.That(delayed.Disposals, Is.EqualTo(1));
    }

    [Test]
    public void DemandDuringAdvancePreparationSharesTheOriginalReaderWithoutMainThreadDispatch()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var demandEntered = new ManualResetEventSlim();
        var native = new HistoryStream();
        using var playback = new PreparedAudioWaveStream(Transcript(), () =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Test did not release native construction.");
            return native;
        });
        playback.Read(new byte[8], 2, 4);
        Task<bool> preparation = Task.Run(() => playback.PrepareForPlayback("advance-biome"));
        Task<int>? demand = null;
        try
        {
            Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True);
            demand = Task.Run(() => { demandEntered.Set(); return playback.Read(new byte[2], 0, 2); });
            Assert.That(demandEntered.Wait(TimeSpan.FromSeconds(5)), Is.True);
        }
        finally { release.Set(); }
        Assert.That(preparation.GetAwaiter().GetResult(), Is.True);
        Assert.That(demand!.GetAwaiter().GetResult(), Is.EqualTo(2));
        Assert.That(playback.DecoderConstructions, Is.EqualTo(1));
        Assert.That(playback.ReplayMismatches, Is.Zero);
        Assert.That(native.History, Is.EqualTo(new[] { "read:2:4", "read:0:2" }));
        Assert.That(playback.PrepareForPlayback("sustainer"), Is.False);
    }

    [Test]
    public void SnapshotsDoNotDemandNativeDataAndRetainBoundedCopiedOutputTraces()
    {
        var original = new HistoryStream { Capacity = 1024, ReportedLength = 1024 };
        using var cold = new PreparedAudioWaveStream(original);
        _ = cold.WaveFormat; _ = cold.Length;
        var coldBytes = new byte[514];
        Assert.That(cold.Read(coldBytes, 2, 512), Is.EqualTo(512));
        PreparedAudioData data = cold.FinishRecording()!;
        var delayed = new HistoryStream { Capacity = 1024, ReportedLength = 1024 };
        int constructions = 0;
        using var warm = new PreparedAudioWaveStream(data, () => { constructions++; return delayed; });
        Assert.That(warm.Snapshot()["warm"], Is.True);
        Assert.That(warm.Snapshot()["constructorCompleted"], Is.False);
        Assert.That(constructions, Is.Zero);
        _ = warm.WaveFormat; _ = warm.Length;
        var warmBytes = new byte[514];
        Assert.That(warm.Read(warmBytes, 2, 512), Is.EqualTo(512));
        Assert.That(warmBytes, Is.EqualTo(coldBytes));
        var coldSnapshot = cold.Snapshot();
        var warmSnapshot = warm.Snapshot();
        var coldRow = (Dictionary<string, object>)((object[])coldSnapshot["trace"])[0];
        var warmRow = (Dictionary<string, object>)((object[])warmSnapshot["trace"])[0];
        Assert.That(warmRow, Is.EqualTo(coldRow));
        Assert.That(warmRow["sampledBytes"], Is.EqualTo(256));
        Assert.That(warmSnapshot["returnedBytes"], Is.EqualTo(512L));
        Assert.That(constructions, Is.Zero);
        Assert.That((original.FormatGets, original.LengthGets), Is.EqualTo((1, 1)), "Snapshots must not call the original metadata getters.");
        warmRow["digest"] = "changed returned snapshot";
        Assert.That(((Dictionary<string, object>)((object[])warm.Snapshot()["trace"])[0])["digest"], Is.EqualTo(coldRow["digest"]));

        for (int i = 0; i < 70; i++) _ = warm.Position;
        var settled = warm.Snapshot();
        Assert.That(constructions, Is.EqualTo(1));
        Assert.That(settled["firstReconstructionCause"], Is.EqualTo("position-get"));
        Assert.That(settled["firstReconstructionThread"], Is.EqualTo(Thread.CurrentThread.ManagedThreadId));
        Assert.That(settled["constructorCompleted"], Is.True);
        Assert.That(settled["readCalls"], Is.EqualTo(1L), "Historical replay is not a new caller read.");
        Assert.That(settled["returnedBytes"], Is.EqualTo(512L));
        Assert.That(settled["replayedBytes"], Is.EqualTo(512L));
        Assert.That(settled["positionGets"], Is.EqualTo(70L));
        Assert.That((object[])settled["trace"], Has.Length.EqualTo(64));
        Assert.That(settled["traceOmitted"], Is.EqualTo(7L));
    }

    [Test]
    public void MismatchedRequestReplaysOnlyAlreadyServedOperations()
    {
        using var baseline = new HistoryStream(); var delayed = new HistoryStream();
        using var playback = new PreparedAudioWaveStream(Transcript(), () => delayed);
        EqualRead(playback, baseline, 2, 4); EqualRead(playback, baseline, 2, 2);
        Assert.That(delayed.History, Is.EqualTo(new[] { "read:2:4", "read:2:2" }));
        Assert.That(playback.ReplayedBytes, Is.EqualTo(4));
        Assert.That(playback.ReplayMismatches, Is.Zero);
    }

    private static Action<MethodBase>? observationMutationGuard;
    private static Action? observationMutation;
    private static int observationEntries;
    private static void ObserveDecoderMutation(MethodBase __0) => observationMutationGuard?.Invoke(__0);
    private static void PatchFromObservation()
    {
        observationEntries++;
        // One attempt bounds the regression on the former implementation:
        // recursive settlement constructs twice instead of overflowing a stack.
        Action? mutation = observationMutation;
        observationMutation = null;
        mutation?.Invoke();
    }
    private static void NoopDecoderPrefix() { }

    [Test, NonParallelizable]
    public void NativeReconstructionDoesNotEnterDiagnosticHooksThatCanPublishDecoderPatches()
    {
        const string owner = "WakeUp.PreparedAudio.Tests.ObservationMutation";
        var harmony = new Harmony(owner);
        Type registryType = typeof(Doorstop.Entrypoint).Assembly.GetType("Doorstop.AudioRegistry", true)!;
        object registry = Activator.CreateInstance(registryType, true)!;
        var state = (Dictionary<string, object>)registryType.GetProperty("State", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(registry)!;
        var pending = (Dictionary<object, Func<int>>)state["audioPending"];
        MethodInfo mutate = registryType.GetMethod("BeforeMutation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        MethodInfo update = AccessTools.Method(typeof(Harmony).Assembly.GetType("HarmonyLib.PatchFunctions", true)!, "UpdateWrapper");
        MethodInfo decoderRead = AccessTools.Method(typeof(PreparedAudioWaveStream), "Read", new[] { typeof(byte[]), typeof(int), typeof(int) });
        var created = new List<HistoryStream>();
        using var playback = new PreparedAudioWaveStream(Transcript(), () =>
        {
            var native = new HistoryStream(); created.Add(native); return native;
        });
        try
        {
            observationEntries = 0;
            observationMutationGuard = target => mutate.Invoke(registry, new object[] { target });
            harmony.Patch(update, prefix: new HarmonyMethod(typeof(PreparedAudioDataTests), nameof(ObserveDecoderMutation)));
            harmony.Patch(AccessTools.Method(typeof(LoadingObservationRuntime), "Begin"),
                prefix: new HarmonyMethod(typeof(PreparedAudioDataTests), nameof(PatchFromObservation)));
            observationMutation = () => harmony.Patch(decoderRead,
                prefix: new HarmonyMethod(typeof(PreparedAudioDataTests), nameof(NoopDecoderPrefix)));
            pending.Add(playback, playback.SettleNativeOutcome);
            playback.Read(new byte[8], 2, 4);
            Assert.That(playback.DecoderConstructions, Is.Zero);
            playback.SettleNative();
            Assert.That(observationEntries, Is.Zero, "An unsettled reader must not invoke a hookable diagnostic boundary.");
            Assert.That(created.Count, Is.EqualTo(1));
            Assert.That(playback.InitializationAttempts, Is.EqualTo(1));
            Assert.That(playback.ReplayedBytes, Is.EqualTo(4));
            Assert.That(playback.ReplayMismatches, Is.Zero);

            // Exercise the installed attack after settlement, proving this is a
            // live Harmony -> real registry -> real reader path, not a mock.
            var observation = LoadingObservationRuntime.Begin("test", "settled decoder mutation");
            LoadingObservationRuntime.End(observation);
            Assert.That(observationEntries, Is.EqualTo(1));
            Assert.That(state["audioNativeOnly"], Is.True);
            Assert.That(pending, Is.Empty);
            Assert.That(created.Count, Is.EqualTo(1));
        }
        finally
        {
            observationMutation = null;
            observationMutationGuard = null;
            harmony.UnpatchAll(owner);
            playback.Dispose();
            foreach (HistoryStream native in created) if (native.Disposals == 0) native.Dispose();
        }
    }

    [Test]
    public void ChangedOffsetCannotMatchOtherwiseIdenticalRead()
    {
        using var baseline = new HistoryStream(); var delayed = new HistoryStream();
        using var playback = new PreparedAudioWaveStream(Transcript(), () => delayed);
        EqualRead(playback, baseline, 3, 4);
        Assert.That(playback.CachedBytes, Is.Zero);
        Assert.That(playback.DecoderConstructions, Is.EqualTo(1));
        Assert.That(delayed.History, Is.EqualTo(baseline.History));
    }

    [Test]
    public void CachedPartialReadPreservesPrefixAndTailThenNativeEof()
    {
        using var recording = new PreparedAudioWaveStream(new HistoryStream());
        _ = recording.WaveFormat; _ = recording.Length;
        recording.Seek(6, SeekOrigin.Begin); recording.Read(new byte[10], 2, 5);
        var delayed = new HistoryStream();
        using var playback = new PreparedAudioWaveStream(recording.FinishRecording()!, () => delayed);
        using var baseline = new HistoryStream();
        Assert.That(playback.Seek(6, SeekOrigin.Begin), Is.EqualTo(baseline.Seek(6, SeekOrigin.Begin)));
        EqualRead(playback, baseline, 2, 5);
        Assert.That(playback.DecoderConstructions, Is.Zero);
        EqualRead(playback, baseline, 1, 3);
        Assert.That(delayed.History, Is.EqualTo(baseline.History));
        Assert.That(playback.ReplayMismatches, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RecordingLimitsAbandonOnlyCacheAndPreserveNativeReads(bool byteLimit)
    {
        var native = new HistoryStream { Capacity = byteLimit ? PreparedAudioData.MaximumBytes : 8 };
        using var recording = new PreparedAudioWaveStream(native);
        _ = recording.WaveFormat; _ = recording.Length;
        if (byteLimit) Assert.That(recording.Read(new byte[PreparedAudioData.MaximumBytes], 0, PreparedAudioData.MaximumBytes), Is.EqualTo(PreparedAudioData.MaximumBytes));
        else
        {
            recording.Read(new byte[1], 0, 1);
            for (int i = 0; i < PreparedAudioData.MaximumOperations; i++) _ = recording.Position;
        }
        Assert.That(recording.RecordingAbandoned, Is.True);
        Assert.That(recording.FinishRecording(), Is.Null);
        Assert.That(recording.IsNative, Is.True);
        Assert.DoesNotThrow((Action)(() => recording.Read(new byte[1], 0, 1)));
    }

    [Test]
    public void ReplayDifferencesAreDiagnosticAndDoNotInventCallbackFailure()
    {
        var delayed = new HistoryStream { Bias = 1 };
        using var playback = new PreparedAudioWaveStream(Transcript(), () => delayed);
        playback.Read(new byte[8], 2, 4);
        Assert.DoesNotThrow((Action)playback.SettleNative);
        Assert.That(playback.ReplayMismatches, Is.EqualTo(1));
        Assert.That(playback.TerminallyFaulted, Is.False);
        Assert.That(playback.Read(new byte[2], 0, 2), Is.EqualTo(2));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void HistoricalNativeFailureSettlesOnceAndPreservesOriginatingException(bool constructorFailure)
    {
        var error = new IOException("original-native-failure");
        var delayed = new HistoryStream { ThrowOnRead = 1, NativeFailure = error, ThrowOnDispose = true };
        using var playback = new PreparedAudioWaveStream(Transcript(), () => constructorFailure ? throw error : delayed);
        playback.Read(new byte[8], 2, 4);
        Assert.DoesNotThrow((Action)playback.SettleNative); Assert.DoesNotThrow((Action)playback.SettleNative);
        Assert.That(playback.SettleNativeOutcome(), Is.EqualTo(2));
        Assert.That(playback.Settled, Is.True);
        Assert.That(playback.TerminallyFaulted, Is.True);
        Assert.That(playback.InitializationAttempts, Is.EqualTo(1));
        Assert.That(delayed.Disposals, Is.EqualTo(constructorFailure ? 0 : 1));
        Assert.That(Assert.Throws<IOException>((Action)(() => playback.Read(new byte[8], 1, 3))), Is.SameAs(error));
        Assert.That(Assert.Throws<IOException>((Action)(() => playback.Position = 0)), Is.SameAs(error));
        Assert.That(playback.InitializationAttempts, Is.EqualTo(1));
        var diagnostic = playback.Snapshot();
        Assert.That(diagnostic["terminallyFaulted"], Is.True);
        Assert.That(diagnostic["firstReconstructionCause"], Is.EqualTo("settlement"));
        Assert.That(diagnostic["constructorCompleted"], Is.EqualTo(!constructorFailure));
        Assert.That(((Dictionary<string, object>)((object[])diagnostic["trace"]).Last())["completed"], Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MetadataFailureSettlesAndPreservesOriginDespiteCleanupFailure(bool format)
    {
        var error = new IOException("original-native-metadata-failure");
        var delayed = new HistoryStream { ThrowOnFormat = format, ThrowOnLength = !format, NativeFailure = error, ThrowOnDispose = true };
        using var playback = new PreparedAudioWaveStream(Transcript(), () => delayed);
        _ = playback.WaveFormat; _ = playback.Length;
        Assert.That(playback.SettleNativeOutcome(), Is.EqualTo(2));
        Assert.That(playback.SettleNativeOutcome(), Is.EqualTo(2));
        Assert.That(delayed.Disposals, Is.EqualTo(1));
        Assert.That(playback.InitializationAttempts, Is.EqualTo(1));
        Assert.That(Assert.Throws<IOException>((Action)(() => playback.Read(new byte[1], 0, 1))), Is.SameAs(error));
    }

    [Test]
    public void OrdinaryNativeErrorAfterTransitionDoesNotLatchOrReconstructAgain()
    {
        var delayed = new HistoryStream { ThrowOnRead = 1 };
        using var playback = new PreparedAudioWaveStream(Transcript(), () => delayed);
        playback.SettleNative();
        Assert.That(playback.SettleNativeOutcome(), Is.EqualTo(1));
        Assert.That(Assert.Throws<IOException>((Action)(() => playback.Read(new byte[4], 0, 4))), Is.SameAs(delayed.NativeFailure));
        Assert.That(playback.TerminallyFaulted, Is.False);
        Assert.That(playback.Read(new byte[4], 0, 4), Is.EqualTo(4));
        Assert.That(playback.InitializationAttempts, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DisposalIsIdempotentAndSettlingDisposedStateNeverConstructs(bool initialize)
    {
        var delayed = new HistoryStream();
        var playback = new PreparedAudioWaveStream(Transcript(), () => delayed);
        if (initialize) playback.SettleNative();
        playback.Dispose(); playback.Dispose();
        Assert.DoesNotThrow((Action)playback.SettleNative);
        Assert.That(playback.SettleNativeOutcome(), Is.EqualTo(3));
        Assert.That(playback.Settled, Is.True);
        Assert.That(delayed.Disposals, Is.EqualTo(initialize ? 1 : 0));
        Assert.Throws<ObjectDisposedException>((Action)(() => playback.Read(new byte[1], 0, 1)));
        Assert.Throws<ObjectDisposedException>((Action)(() => playback.Position = 0));
        Assert.That(playback.DecoderConstructions, Is.EqualTo(initialize ? 1 : 0));
    }

    [Test]
    public void NativeEntryDrainContinuesAfterOwnedFailureWithoutChangingLaterConsumerError()
    {
        Type type = typeof(Doorstop.Entrypoint).Assembly.GetType("Doorstop.AudioRegistry", true)!;
        object registry = Activator.CreateInstance(type, true)!;
        var state = (Dictionary<string, object>)type.GetProperty("State", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(registry)!;
        var pending = (Dictionary<object, Func<int>>)state["audioPending"];
        var error = new IOException("native-first-reader-failure");
        using var faulted = new PreparedAudioWaveStream(Transcript(), () => throw error);
        var healthyNative = new HistoryStream();
        bool earlierFaultWasLatched = false;
        using var healthy = new PreparedAudioWaveStream(Transcript(), () =>
        {
            earlierFaultWasLatched = faulted.TerminallyFaulted;
            return healthyNative;
        });
        faulted.Read(new byte[8], 2, 4);
        healthy.Read(new byte[8], 2, 4);
        pending.Add(faulted, faulted.SettleNativeOutcome);
        pending.Add(healthy, healthy.SettleNativeOutcome);
        MethodInfo drain = type.GetMethod("DrainNativeEntry", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.That(drain.Invoke(registry, null), Is.True);
        Assert.That(pending, Is.Empty);
        Assert.That(earlierFaultWasLatched, Is.True);
        Assert.That(faulted.SettleNativeOutcome(), Is.EqualTo(2));
        Assert.That(healthy.SettleNativeOutcome(), Is.EqualTo(1));
        Assert.That(faulted.InitializationAttempts, Is.EqualTo(1));
        Assert.That(healthy.InitializationAttempts, Is.EqualTo(1));
        Assert.That(healthy.ReplayedBytes, Is.EqualTo(4));
        Assert.That(Assert.Throws<IOException>((Action)(() => faulted.Read(new byte[4], 0, 4))), Is.SameAs(error));
        Assert.That(healthy.Read(new byte[4], 0, 4), Is.EqualTo(4));
        Assert.That(drain.Invoke(registry, null), Is.True);
        Assert.That(faulted.InitializationAttempts, Is.EqualTo(1));
        Assert.That(healthy.InitializationAttempts, Is.EqualTo(1));
    }
}
