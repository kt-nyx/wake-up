// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HarmonyLib;
using NAudio.Wave;
using NUnit.Framework;

namespace WakeUp.Tests;

// Actual pinned Mp3FileReader framing, indexing, positioning and reads execute.
// The native constructor's supported frame-decompressor builder avoids Windows
// ACM execution in offline tests. The fixture separately compares actual ACM.
[TestFixture, NonParallelizable]
public sealed class PreparedMp3IndexTests
{
    private string root = null!;
    private Harmony harmony = null!;
    private PreparedAudioCache cache = null!;
    private Dictionary<string, object> registry = null!;
    [SetUp]
    public void SetUp()
    {
        root = Path.Combine(Path.GetTempPath(), "wake-up-mp3-index-test-" + Guid.NewGuid().ToString("N"));
        registry = new Dictionary<string, object> { ["audioRegistryGate"] = new object(), ["audioNativeOnly"] = false,
            ["audioAdmissionClosed"] = false, ["audioEpoch"] = 0L };
        CacheLaunchPolicy? policy = null;
        cache = new PreparedAudioCache(root, CacheLaunchPolicy.Latch(ref policy, "normal", () => { }));
        PreparedMp3IndexRuntime.Initialize(cache, registry);
        harmony = new Harmony("WakeUp.Tests.PreparedMp3Index." + Guid.NewGuid().ToString("N"));
        harmony.Patch(PreparedMp3IndexRuntime.Target,
            prefix: new HarmonyMethod(typeof(PreparedMp3IndexRuntime), nameof(PreparedMp3IndexRuntime.Prefix)),
            postfix: new HarmonyMethod(typeof(PreparedMp3IndexRuntime), nameof(PreparedMp3IndexRuntime.Postfix)));
    }
    [TearDown]
    public void TearDown()
    {
        harmony?.UnpatchAll(harmony.Id);
        PreparedMp3IndexRuntime.Close();
        cache?.Dispose();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ActualNativeIndexScanIsSkippedAndNativeReadsSeeksAndDurationMatch(bool mono)
    {
        byte[] bytes = Frames(mono);
        string key = Key(bytes);
        using var firstInput = new MemoryStream(bytes, false);
        object? previous = PreparedMp3IndexRuntime.PushContext(firstInput, key);
        using var first = new Mp3FileReader(firstInput, CreateDecoder);
        var firstReceipt = PreparedMp3IndexRuntime.PopContext(previous, true);
        Assert.That(firstReceipt["nativeScans"], Is.EqualTo(1));
        Assert.That(firstReceipt["skippedScans"], Is.EqualTo(0));
        Assert.That(firstReceipt["published"], Is.True);

        using var controlInput = new MemoryStream(bytes, false);
        using var control = new Mp3FileReader(controlInput, CreateDecoder);
        using var warmInput = new MemoryStream(bytes, false);
        previous = PreparedMp3IndexRuntime.PushContext(warmInput, key);
        using var warm = new Mp3FileReader(warmInput, CreateDecoder);
        var warmReceipt = PreparedMp3IndexRuntime.PopContext(previous, true);
        Assert.That(warmReceipt["nativeScans"], Is.EqualTo(0));
        Assert.That(warmReceipt["skippedScans"], Is.EqualTo(1));
        Assert.That(warmReceipt["published"], Is.False);
        Assert.That(warm.Length, Is.EqualTo(control.Length));
        Assert.That(warm.TotalTime, Is.EqualTo(control.TotalTime));
        Assert.That(warm.Mp3WaveFormat.AverageBytesPerSecond, Is.EqualTo(control.Mp3WaveFormat.AverageBytesPerSecond));
        Assert.That(warmInput.Position, Is.EqualTo(controlInput.Position), "Surrounding constructor resets both inputs after the index scan.");
        ReadEqual(warm, control, 7, 309);
        foreach (long position in new[] { 0L, 8192L, 113L, -100L, warm.Length - 10, warm.Length + 100, 0L })
        {
            Assert.That(warm.Seek(position, SeekOrigin.Begin), Is.EqualTo(control.Seek(position, SeekOrigin.Begin)));
            ReadEqual(warm, control, 3, 4096);
            ReadEqual(warm, control, 1, 17);
            Assert.That(warm.Position, Is.EqualTo(control.Position));
        }
        warm.Dispose(); first.Dispose(); control.Dispose();
        Assert.That(warmInput.CanRead && firstInput.CanRead && controlInput.CanRead, Is.True);
    }

    [Test]
    public void NativeScanAndLaterProviderFailuresAreNotPublished()
    {
        byte[] valid = Frames(false);
        using (var input = new MemoryStream(valid, false))
        {
            object? previous = PreparedMp3IndexRuntime.PushContext(input, Key(valid));
            var sentinel = new InvalidOperationException("native-builder-failure");
            Assert.That(Assert.Throws<InvalidOperationException>((Action)(() => new Mp3FileReader(input, _ => throw sentinel))), Is.SameAs(sentinel));
            Assert.That(PreparedMp3IndexRuntime.PopContext(previous, false)["published"], Is.False);
        }
        byte[] changed = Frames(false);
        changed[417 * 4 + 3] = 0xc0; // A later native frame changes channel count.
        using (var input = new MemoryStream(changed, false))
        {
            object? previous = PreparedMp3IndexRuntime.PushContext(input, Key(changed));
            Assert.Throws<InvalidOperationException>((Action)(() => new Mp3FileReader(input, CreateDecoder)));
            Assert.That(PreparedMp3IndexRuntime.PopContext(previous, false)["published"], Is.False);
        }
        Assert.That(PreparedMp3IndexRuntime.Snapshot()["publications"], Is.EqualTo(0L));
        Assert.That(Directory.EnumerateFiles(root, "*.bin", SearchOption.AllDirectories), Is.Empty);
    }

    [Test]
    public void EpochChangeOrStickyNativeStatePreventsReuseAndPublication()
    {
        byte[] bytes = Frames(false);
        string key = Key(bytes);
        using (var input = new MemoryStream(bytes, false))
        {
            object? previous = PreparedMp3IndexRuntime.PushContext(input, key);
            using var reader = new Mp3FileReader(input, CreateDecoder);
            Assert.That(PreparedMp3IndexRuntime.PopContext(previous, true)["published"], Is.True);
        }
        using (var input = new MemoryStream(bytes, false))
        {
            object? previous = PreparedMp3IndexRuntime.PushContext(input, key);
            lock (registry["audioRegistryGate"]) { registry["audioEpoch"] = 1L; registry["audioNativeOnly"] = true; }
            using var reader = new Mp3FileReader(input, CreateDecoder);
            var receipt = PreparedMp3IndexRuntime.PopContext(previous, true);
            Assert.That(receipt["skippedScans"], Is.EqualTo(0));
            Assert.That(receipt["published"], Is.False);
        }
    }

    [Test]
    public void IndexUsesTheExistingAudioStoreAndDoesNotOwnItsLifetime()
    {
        byte[] bytes = Frames(false);
        string key = Key(bytes);
        using var input = new MemoryStream(bytes, false);
        object? previous = PreparedMp3IndexRuntime.PushContext(input, key);
        using var reader = new Mp3FileReader(input, CreateDecoder);
        Assert.That(PreparedMp3IndexRuntime.PopContext(previous, true)["published"], Is.True);
        Assert.That(cache.Writes, Is.EqualTo(1L));
        Assert.That(File.Exists(Path.Combine(root, "WakeUp", "PreparedAudio", "v1", key + ".bin")), Is.True);
        Assert.That(Directory.Exists(Path.Combine(root, "WakeUp", "PreparedAudio", "Mp3Index")), Is.False);
        PreparedMp3IndexRuntime.Close();
        Assert.That(cache.ReadIndex(key), Is.Not.Null, "Closing the index integration must leave the shared owner usable.");
    }

    [Test]
    public void CorruptCacheFallsBackToActualNativeScan()
    {
        byte[] bytes = Frames(false);
        string key = Key(bytes);
        using (var input = new MemoryStream(bytes, false))
        {
            object? previous = PreparedMp3IndexRuntime.PushContext(input, key);
            using var reader = new Mp3FileReader(input, CreateDecoder);
            PreparedMp3IndexRuntime.PopContext(previous, true);
        }
        string path = Path.Combine(root, "WakeUp", "PreparedAudio", "v1", key + ".bin");
        byte[] payload = File.ReadAllBytes(path); payload[payload.Length - 1] ^= 0x80; File.WriteAllBytes(path, payload);
        using var secondInput = new MemoryStream(bytes, false);
        object? saved = PreparedMp3IndexRuntime.PushContext(secondInput, key);
        using var second = new Mp3FileReader(secondInput, CreateDecoder);
        var receipt = PreparedMp3IndexRuntime.PopContext(saved, true);
        Assert.That(receipt["nativeScans"], Is.EqualTo(1));
        Assert.That(receipt["skippedScans"], Is.EqualTo(0));
    }

    private static void ReadEqual(Mp3FileReader actual, Mp3FileReader expected, int offset, int count)
    {
        byte[] left = Enumerable.Repeat((byte)0xa7, count + offset + 11).ToArray(), right = (byte[])left.Clone();
        Assert.That(actual.Read(left, offset, count), Is.EqualTo(expected.Read(right, offset, count)));
        Assert.That(left, Is.EqualTo(right));
    }
    private static string Key(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
    }
    private static byte[] Frames(bool mono)
    {
        var result = new byte[417 * 12]; // MPEG-1 Layer III, 128 kbit/s, 44100 Hz.
        for (int frame = 0; frame < 12; frame++)
        {
            int offset = frame * 417;
            result[offset] = 0xff; result[offset + 1] = 0xfb; result[offset + 2] = 0x90;
            result[offset + 3] = mono ? (byte)0xc0 : (byte)0;
            result[offset + 5] = (byte)(frame + 1);
        }
        return result;
    }
    private static IMp3FrameDecompressor CreateDecoder(WaveFormat format) => new FrameDecoder(format);
    private sealed class FrameDecoder : IMp3FrameDecompressor
    {
        public WaveFormat OutputFormat { get; }
        internal FrameDecoder(WaveFormat format) => OutputFormat = new WaveFormat(format.SampleRate, 16, format.Channels);
        public int DecompressFrame(Mp3Frame frame, byte[] dest, int offset)
        {
            int length = frame.SampleCount * OutputFormat.BlockAlign;
            for (int i = 0; i < length; i++) dest[offset + i] = (byte)(frame.RawData[5] + i);
            return length;
        }
        public void Reset() { }
        public void Dispose() { }
    }
}
