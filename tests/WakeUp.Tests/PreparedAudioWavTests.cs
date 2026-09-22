// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using HarmonyLib;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture, NonParallelizable]
public sealed class PreparedAudioWavTests
{
    [TestCase(1, 8, 1)]
    [TestCase(1, 8, 2)]
    [TestCase(1, 16, 1)]
    [TestCase(1, 16, 2)]
    [TestCase(1, 24, 1)]
    [TestCase(1, 24, 2)]
    [TestCase(1, 32, 1)]
    [TestCase(1, 32, 2)]
    [TestCase(3, 32, 1)]
    [TestCase(3, 32, 2)]
    [TestCase(3, 64, 1)]
    [TestCase(3, 64, 2)]
    public void RealWavMetadataConversionReadsAndSeeksMatchBeforeAndAfterReconstruction(int encoding, int bits, int channels)
    {
        byte[] wav = Wav(encoding, bits, channels);
        using var coldInput = new MemoryStream(wav, false);
        using var controlInput = new MemoryStream(wav, false);
        using var cold = new PreparedAudioWaveStream(new WaveFileReader(coldInput));
        using var control = new WaveFileReader(controlInput);
        Exercise(cold, control);
        PreparedAudioData? captured = cold.FinishRecording();
        Assert.That(captured, Is.Not.Null);
        PreparedAudioData data = PreparedAudioData.Decode(captured!.Encode());
        Assert.That(data.HasSupportedFormat, Is.True);
        Assert.That((data.Encoding, data.BitsPerSample, data.Channels), Is.EqualTo((encoding, bits, channels)));

        using var warmInput = new MemoryStream(wav, false);
        using var secondControlInput = new MemoryStream(wav, false);
        using var secondControl = new WaveFileReader(secondControlInput);
        int constructions = 0;
        using var warm = new PreparedAudioWaveStream(data, () => { constructions++; return new WaveFileReader(warmInput); });
        Exercise(warm, secondControl);
        Assert.That(constructions, Is.Zero, "Recorded native operations must not create another WAV reader.");
        Assert.That(warm.CachedBytes, Is.GreaterThan(0));
        // The transcript ends at position zero. This new request forces real
        // construction and replays all preceding native reads and aligned seeks.
        EqualRead(warm, secondControl, 2, secondControl.WaveFormat.BlockAlign * 3);
        Assert.That(constructions, Is.EqualTo(1));
        Assert.That(warm.ReplayMismatches, Is.Zero);
        Assert.That(warm.Position, Is.EqualTo(secondControl.Position));
        warm.Dispose();
        cold.Dispose();
        Assert.That(warmInput.CanRead && coldInput.CanRead, Is.True, "Stream-taking native WAV readers borrow their original input.");
    }

    private static void Exercise(WaveStream actual, WaveFileReader expected)
    {
        Assert.That(Format(actual.WaveFormat), Is.EqualTo(Format(expected.WaveFormat)));
        Assert.That(actual.Length, Is.EqualTo(expected.Length));
        Assert.That(actual.TotalTime, Is.EqualTo(expected.TotalTime));
        var converted = new SampleChannel(actual, false);
        var control = new SampleChannel(expected, false);
        Assert.That(Format(converted.WaveFormat), Is.EqualTo(Format(control.WaveFormat)));
        int samples = expected.WaveFormat.Channels * 2;
        var actualFloats = Enumerable.Repeat(0.25f, samples + 3).ToArray();
        var expectedFloats = actualFloats.ToArray();
        Assert.That(converted.Read(actualFloats, 1, samples), Is.EqualTo(control.Read(expectedFloats, 1, samples)));
        Assert.That(FloatBytes(actualFloats), Is.EqualTo(FloatBytes(expectedFloats)), "Native SampleChannel conversion and untouched output tails must match bit for bit.");
        int block = expected.WaveFormat.BlockAlign;
        EqualRead(actual, expected, 3, block * 2);
        EqualSeek(actual, expected, block + 1, SeekOrigin.Begin);
        EqualRead(actual, expected, 1, block);
        // WaveFileReader only clamps the upper bound: a negative aligned
        // position can read preceding RIFF bytes. Preserve its actual behavior.
        EqualSeek(actual, expected, -block, SeekOrigin.Begin);
        Assert.That(actual.Position, Is.EqualTo(expected.Position));
        Assert.That(expected.Position, Is.EqualTo(-block));
        EqualRead(actual, expected, 2, block);
        EqualSeek(actual, expected, -1, SeekOrigin.End);
        EqualRead(actual, expected, 2, block * 2); // Short final read; tail stays intact.
        EqualSeek(actual, expected, 1000, SeekOrigin.End);
        Assert.That(expected.Position, Is.EqualTo(expected.Length));
        EqualRead(actual, expected, 1, block); // EOF.
        EqualSeek(actual, expected, 0, SeekOrigin.Begin);
    }

    [Test]
    public void RealWavNativeReadAndNegativeSeekErrorsRemainRecoverable()
    {
        byte[] wav = Wav(1, 16, 2);
        using var coldInput = new MemoryStream(wav, false);
        using var cold = new PreparedAudioWaveStream(new WaveFileReader(coldInput));
        _ = cold.WaveFormat; _ = cold.Length;
        cold.Read(new byte[4], 0, 4);
        PreparedAudioData data = cold.FinishRecording()!;
        using var warmInput = new MemoryStream(wav, false);
        using var controlInput = new MemoryStream(wav, false);
        using var expected = new WaveFileReader(controlInput);
        using var warm = new PreparedAudioWaveStream(data, () => new WaveFileReader(warmInput));
        EqualRead(warm, expected, 0, 4);
        Assert.That(warm.DecoderConstructions, Is.Zero);
        EqualError(() => warm.Read(new byte[8], 0, 3), () => expected.Read(new byte[8], 0, 3));
        EqualError(() => warm.Read(new byte[8], 0, -4), () => expected.Read(new byte[8], 0, -4));
        EqualError(() => warm.Position = -10000, () => expected.Position = -10000);
        Assert.That(warm.DecoderConstructions, Is.EqualTo(1));
        Assert.That(warm.TerminallyFaulted, Is.False, "Ordinary errors after transition must not become permanent historical replay errors.");
        EqualSeek(warm, expected, 0, SeekOrigin.Begin);
        EqualRead(warm, expected, 1, 8);
        Assert.That(warm.ReplayMismatches, Is.Zero);
    }

    [Test]
    public void EighteenByteFormatWithZeroExtraSizeRecordsAndDisposesWithoutWarmConstruction()
    {
        byte[] wav = Wav(1, 16, 2, extra: Array.Empty<byte>());
        using var input = new MemoryStream(wav, false);
        using var cold = new PreparedAudioWaveStream(new WaveFileReader(input));
        int[] format = Format(cold.WaveFormat);
        int length = (int)cold.Length;
        var expected = new byte[length];
        Assert.That(cold.Read(expected, 0, length), Is.EqualTo(length));
        PreparedAudioData? data = cold.FinishRecording();
        Assert.That(data, Is.Not.Null);
        Assert.That(data!.ExtraSize, Is.Zero);
        int constructions = 0;
        using var warmInput = new MemoryStream(wav, false);
        using var warm = new PreparedAudioWaveStream(data, () => { constructions++; return new WaveFileReader(warmInput); });
        Assert.That(Format(warm.WaveFormat), Is.EqualTo(format));
        Assert.That(warm.Length, Is.EqualTo(length));
        var actual = new byte[length];
        Assert.That(warm.Read(actual, 0, length), Is.EqualTo(length));
        Assert.That(actual, Is.EqualTo(expected));
        warm.Dispose(); warm.SettleNative();
        Assert.That(constructions, Is.Zero);
        Assert.That(warmInput.CanRead, Is.True);
    }

    [TestCase("extensible")]
    [TestCase("unknown-encoding")]
    [TestCase("adpcm-extra")]
    [TestCase("block-align")]
    [TestCase("byte-rate")]
    [TestCase("bit-depth")]
    [TestCase("float-bit-depth")]
    [TestCase("sample-rate")]
    [TestCase("channels")]
    public void UnsupportedOrInconsistentNativeWavFormatAbandonsOnlyRecording(string variation)
    {
        int encoding = 1, bits = 16, channels = 1;
        int? block = null, byteRate = null, rate = null;
        byte[]? extra = null;
        switch (variation)
        {
            case "extensible": encoding = 0xfffe; extra = new byte[22]; break;
            case "unknown-encoding": encoding = 0x7777; break;
            case "adpcm-extra": encoding = 2; bits = 4; block = 32; extra = new byte[] { 50, 0, 7, 0 }; break;
            case "block-align": block = 3; break;
            case "byte-rate": byteRate = 123; break;
            case "bit-depth": bits = 20; break;
            case "float-bit-depth": encoding = 3; bits = 16; break;
            case "sample-rate": rate = 0; break;
            case "channels": channels = 0; block = 2; break;
        }
        byte[] wav = Wav(encoding, bits, channels, block, byteRate, rate, extra,
            variation == "adpcm-extra" ? new byte[64] : null);
        using var input = new MemoryStream(wav, false);
        using var controlInput = new MemoryStream(wav, false);
        var original = new WaveFileReader(input);
        using var proxy = new PreparedAudioWaveStream(original);
        using var control = new WaveFileReader(controlInput);
        Assert.That(proxy.WaveFormat, Is.SameAs(original.WaveFormat), "Rejected metadata must not be normalized or replaced.");
        Assert.That(Format(proxy.WaveFormat), Is.EqualTo(Format(control.WaveFormat)));
        Assert.That(proxy.Length, Is.EqualTo(control.Length));
        EqualRead(proxy, control, 2, control.WaveFormat.BlockAlign);
        Assert.That(proxy.RecordingAbandoned, Is.True);
        Assert.That(proxy.FinishRecording(), Is.Null);
        EqualSeek(proxy, control, 0, SeekOrigin.Begin);
        EqualRead(proxy, control, 1, control.WaveFormat.BlockAlign);
        Assert.That(proxy.IsNative, Is.True);
        Assert.That(proxy.TerminallyFaulted, Is.False);
        proxy.Dispose();
        Assert.That(input.CanRead, Is.True);
    }

    [TestCase("a-law", 1)]
    [TestCase("a-law", 2)]
    [TestCase("mu-law", 1)]
    [TestCase("mu-law", 2)]
    [TestCase("ms-adpcm", 1)]
    [TestCase("ima-adpcm", 2)]
    [TestCase("gsm610", 1)]
    [TestCase("extensible-pcm", 2)]
    [TestCase("extensible-float", 2)]
    [TestCase("pcm-extra", 1)]
    public void NativeExtendedAndEncodedWavFormatsKeepRawBytesAndMarshalling(string representation, int channels)
    {
        byte[] wav = EncodedWav(representation, channels);
        using var input = new MemoryStream(wav, false);
        using var expectedInput = new MemoryStream(wav, false);
        using var expected = new WaveFileReader(expectedInput);
        using var cold = new PreparedAudioWaveStream(new WaveFileReader(input));
        ExerciseRaw(cold, expected);
        var data = PreparedAudioData.Decode(cold.FinishRecording()!.Encode());
        Assert.That(data.FormatKind, Is.EqualTo(1));
        Assert.That(data.ExtraData.Length, Is.EqualTo(100));
        using var warmInput = new MemoryStream(wav, false);
        using var secondInput = new MemoryStream(wav, false);
        using var second = new WaveFileReader(secondInput);
        using var warm = new PreparedAudioWaveStream(data, () => new WaveFileReader(warmInput));
        ExerciseRaw(warm, second);
        Assert.That(warm.DecoderConstructions, Is.Zero);
        EqualRead(warm, second, 3, second.WaveFormat.BlockAlign * 3);
        Assert.That(warm.DecoderConstructions, Is.EqualTo(1));
        Assert.That(warm.ReplayMismatches, Is.Zero);
        // The original lower reader enforces block alignment for encoded
        // frames too. Its exception remains an ordinary native exception.
        if (second.WaveFormat.BlockAlign > 1)
            EqualError(() => warm.Read(new byte[2], 0, 1), () => second.Read(new byte[2], 0, 1));
        warm.Dispose(); cold.Dispose();
        Assert.That(input.CanRead && warmInput.CanRead, Is.True);
    }

    [Test]
    public void NativeParsedFormatRoundTripKeepsWholeInlineArrayAndIndependentCopies()
    {
        using var input = new MemoryStream(EncodedWav("extensible-pcm", 2), false);
        using var native = new WaveFileReader(input);
        var format = (WaveFormatExtraData)native.WaveFormat;
        // The parser initially zeros unused storage. Preserve it even if a
        // consumer has changed that public array before its first observation.
        format.ExtraData[99] = 0x9d;
        var data = new PreparedAudioData();
        Assert.That(data.CaptureFormat(format), Is.True);
        var restored = data.RestoreFormat();
        EqualNativeFormat(restored, format);
        var copy = data.Copy();
        format.ExtraData[99] = 0;
        data.ExtraData[6] ^= 1;
        Assert.That(((WaveFormatExtraData)restored).ExtraData[99], Is.EqualTo(0x9d));
        Assert.That(copy.ExtraData[99], Is.EqualTo(0x9d));
        Assert.That(copy.ExtraData[6], Is.Not.EqualTo(data.ExtraData[6]));
    }

    [TestCase("a-law", 1)]
    [TestCase("mu-law", 2)]
    [TestCase("ms-adpcm", 1)]
    [TestCase("ima-adpcm", 2)]
    [TestCase("gsm610", 1)]
    [TestCase("extensible-pcm", 2)]
    [TestCase("extensible-float", 2)]
    [Platform("Win")]
    public void OriginalWindowsConversionGetsEquivalentFormatBytesOrOriginalErrors(string representation, int channels)
    {
        byte[] wav = EncodedWav(representation, channels);
        using var input = new MemoryStream(wav, false);
        using var cold = new PreparedAudioWaveStream(new WaveFileReader(input));
        var expected = ConvertWithOriginalNativeBranch(cold, out WaveStream? coldConversion);
        TestContext.Out.WriteLine(representation + ": " + (expected.Error.Length != 0
            ? "native error " + expected.Error : "native conversion returned " + expected.Reads.Sum() + " bytes"));
        PreparedAudioData? data = cold.FinishRecording();
        coldConversion?.Dispose();
        // A provider can reject a format before reading any source bytes. A
        // separately recorded raw transcript still lets us check that warm
        // metadata produces the same original provider error in that case.
        if (data == null)
        {
            using var rawInput = new MemoryStream(wav, false);
            using var raw = new PreparedAudioWaveStream(new WaveFileReader(rawInput));
            int block = raw.WaveFormat.BlockAlign;
            _ = raw.Length;
            raw.Read(new byte[block], 0, block);
            data = raw.FinishRecording();
        }
        Assert.That(data, Is.Not.Null);
        using var warmInput = new MemoryStream(wav, false);
        using var warm = new PreparedAudioWaveStream(PreparedAudioData.Decode(data!.Encode()), () => new WaveFileReader(warmInput));
        var actual = ConvertWithOriginalNativeBranch(warm, out WaveStream? warmConversion);
        try
        {
            Assert.That(actual.Error, Is.EqualTo(expected.Error));
            Assert.That(actual.Format, Is.EqualTo(expected.Format));
            Assert.That(actual.Reads, Is.EqualTo(expected.Reads));
            Assert.That(actual.Bytes, Is.EqualTo(expected.Bytes));
            Assert.That(warm.ReplayMismatches, Is.Zero);
            Assert.That(warm.TerminallyFaulted, Is.False);
        }
        finally { warmConversion?.Dispose(); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WarmFormatRestoreDoesNotInvokePatchableNativeConstructorsOrHelpers(bool extraData)
    {
        using var input = new MemoryStream(EncodedWav("extensible-pcm", 2), false);
        using var native = new WaveFileReader(input);
        WaveFormat original = extraData ? native.WaveFormat : WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        var data = new PreparedAudioData();
        Assert.That(data.CaptureFormat(original), Is.True);
        byte[] expectedSerialized = SerializedFormat(original), expectedMarshalled = MarshalledFormat(original);
        var harmony = new Harmony("WakeUp.Tests.WarmFormat." + Guid.NewGuid().ToString("N"));
        try
        {
            var prefix = new HarmonyMethod(typeof(PreparedAudioWavTests), nameof(RejectWarmNativeFormatCall));
            harmony.Patch(typeof(WaveFormatExtraData).GetConstructor(new[] { typeof(BinaryReader) }), prefix: prefix);
            harmony.Patch(AccessTools.Method(typeof(WaveFormat), nameof(WaveFormat.CreateCustomFormat)), prefix: prefix);
            foreach (var constructor in typeof(WaveFormat).GetConstructors()) harmony.Patch(constructor, prefix: prefix);
            WaveFormat restored = data.RestoreFormat();
            Assert.That(restored.GetType(), Is.EqualTo(original.GetType()));
            Assert.That(SerializedFormat(restored), Is.EqualTo(expectedSerialized));
            Assert.That(MarshalledFormat(restored), Is.EqualTo(expectedMarshalled));
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    private static void RejectWarmNativeFormatCall() => throw new InvalidOperationException("Warm format restoration invoked patchable native code.");

    private static (string Error, byte[] Format, int[] Reads, byte[] Bytes) ConvertWithOriginalNativeBranch(
        WaveStream source, out WaveStream? conversion)
    {
        conversion = null;
        byte[] format = Array.Empty<byte>();
        var reads = new System.Collections.Generic.List<int>();
        using var output = new MemoryStream();
        try
        {
            // This is the unchanged CustomAudioFileReader WAV branch. Native
            // ACM output is compared here, but never put in PreparedAudioData.
            conversion = WaveFormatConversionStream.CreatePcmStream(source);
            conversion = new BlockAlignReductionStream(conversion);
            format = SerializedFormat(conversion.WaveFormat);
            for (int i = 0; i < 2; i++)
            {
                var bytes = Enumerable.Repeat((byte)0xd5, 1031).ToArray();
                reads.Add(conversion.Read(bytes, 3, 1024));
                output.Write(bytes, 0, bytes.Length);
            }
            return ("", format, reads.ToArray(), output.ToArray());
        }
        catch (Exception exception)
        {
            return (exception.GetType().FullName + ":" + exception.Message, format, reads.ToArray(), output.ToArray());
        }
    }

    [Test]
    public void ExtensibleCacheRejectsTruncatedOrInconsistentFormatPayload()
    {
        using var input = new MemoryStream(EncodedWav("extensible-pcm", 2), false);
        using var cold = new PreparedAudioWaveStream(new WaveFileReader(input));
        int block = cold.WaveFormat.BlockAlign;
        _ = cold.Length;
        cold.Read(new byte[block], 0, block);
        byte[] bytes = cold.FinishRecording()!.Encode();
        var shortPayload = bytes.Take(PreparedAudioData.HeaderBytes + 99).ToArray();
        Assert.Throws<InvalidDataException>((Action)(() => PreparedAudioData.Decode(shortPayload)));
        var wrongSize = (byte[])bytes.Clone();
        Buffer.BlockCopy(BitConverter.GetBytes(99), 0, wrongSize, 44, 4);
        Assert.Throws<InvalidDataException>((Action)(() => PreparedAudioData.Decode(wrongSize)));
        var wrongValidBits = (byte[])bytes.Clone();
        wrongValidBits[PreparedAudioData.HeaderBytes] = 33;
        Assert.Throws<InvalidDataException>((Action)(() => PreparedAudioData.Decode(wrongValidBits)));
        var oldVersion = (byte[])bytes.Clone();
        Buffer.BlockCopy(BitConverter.GetBytes(3), 0, oldVersion, 0, 4);
        Assert.Throws<InvalidDataException>((Action)(() => PreparedAudioData.Decode(oldVersion)));
    }

    private static void ExerciseRaw(WaveStream actual, WaveFileReader expected)
    {
        EqualNativeFormat(actual.WaveFormat, expected.WaveFormat);
        Assert.That(actual.Length, Is.EqualTo(expected.Length));
        Assert.That(actual.TotalTime, Is.EqualTo(expected.TotalTime));
        int block = expected.WaveFormat.BlockAlign;
        EqualRead(actual, expected, 2, block * 2);
        EqualSeek(actual, expected, block + 1, SeekOrigin.Begin);
        Assert.That(actual.Position, Is.EqualTo(expected.Position));
        EqualRead(actual, expected, 1, block);
        EqualSeek(actual, expected, -1, SeekOrigin.End);
        EqualRead(actual, expected, 1, block * 2);
        EqualSeek(actual, expected, 0, SeekOrigin.Begin);
    }

    private static void EqualNativeFormat(WaveFormat actual, WaveFormat expected)
    {
        Assert.That(actual.GetType(), Is.EqualTo(expected.GetType()));
        Assert.That(Format(actual), Is.EqualTo(Format(expected)));
        Assert.That(SerializedFormat(actual), Is.EqualTo(SerializedFormat(expected)));
        Assert.That(MarshalledFormat(actual), Is.EqualTo(MarshalledFormat(expected)),
            "The unchanged native ACM consumer must receive the same complete WAVEFORMATEX representation.");
    }

    private static byte[] SerializedFormat(WaveFormat format)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        format.Serialize(writer); writer.Flush();
        return stream.ToArray();
    }

    private static byte[] MarshalledFormat(WaveFormat format)
    {
        IntPtr pointer = WaveFormat.MarshalToPtr(format);
        try
        {
            var bytes = new byte[Marshal.SizeOf(format)];
            Marshal.Copy(pointer, bytes, 0, bytes.Length);
            return bytes;
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    private static byte[] EncodedWav(string representation, int channels)
    {
        int encoding = 6, bits = 8, block = channels, rate = 8000;
        byte[] extra = Array.Empty<byte>();
        if (representation == "mu-law") encoding = 7;
        else if (representation == "ms-adpcm")
        {
            encoding = 2; bits = 4; block = 256;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write((ushort)500); writer.Write((ushort)7);
            foreach (short coefficient in new short[] { 256, 0, 512, -256, 0, 0, 192, 64, 240, 0, 460, -208, 392, -232 })
                writer.Write(coefficient);
            writer.Flush(); extra = stream.ToArray();
        }
        else if (representation == "ima-adpcm") { encoding = 17; bits = 4; block = 256; extra = new byte[] { 249, 0 }; }
        else if (representation == "gsm610") { encoding = 49; bits = 0; block = 65; extra = new byte[] { 64, 1 }; }
        else if (representation == "pcm-extra") { encoding = 1; bits = 16; block = channels * 2; extra = new byte[] { 1, 2 }; }
        else if (representation.StartsWith("extensible-", StringComparison.Ordinal))
        {
            encoding = 0xfffe; bits = 32; block = channels * 4;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            bool pcm = representation == "extensible-pcm";
            writer.Write((ushort)(pcm ? 24 : 32)); writer.Write(channels == 2 ? 3 : 4);
            writer.Write(new Guid(pcm ? "00000001-0000-0010-8000-00aa00389b71" : "00000003-0000-0010-8000-00aa00389b71").ToByteArray());
            writer.Flush(); extra = stream.ToArray();
        }
        int average = encoding == 2 ? 4096 : encoding == 17 ? 8224 : encoding == 49 ? 1625 : rate * block;
        // Raw encoded source bytes, not fabricated decoded samples. No ACM
        // provider is invoked by this lower-reader contract test.
        byte[] payload = Enumerable.Range(0, block * 5).Select(i => unchecked((byte)(i * 47 + 13))).ToArray();
        return Wav(encoding, bits, channels, block, average, rate, extra, payload);
    }

    private static int[] Format(WaveFormat format) => new[] { (int)format.Encoding, format.BitsPerSample, format.Channels,
        format.SampleRate, format.BlockAlign, format.AverageBytesPerSecond, format.ExtraSize };
    private static byte[] FloatBytes(float[] values)
    {
        var bytes = new byte[values.Length * 4];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return bytes;
    }
    private static void EqualRead(WaveStream actual, WaveStream expected, int offset, int count)
    {
        var left = Enumerable.Repeat((byte)0xd5, offset + count + 5).ToArray();
        var right = left.ToArray();
        Assert.That(actual.Read(left, offset, count), Is.EqualTo(expected.Read(right, offset, count)));
        Assert.That(left, Is.EqualTo(right));
    }
    private static void EqualSeek(WaveStream actual, WaveStream expected, long offset, SeekOrigin origin)
        => Assert.That(actual.Seek(offset, origin), Is.EqualTo(expected.Seek(offset, origin)));
    private static void EqualError(Action actual, Action expected)
    {
        Exception? control = Assert.Catch((Action)expected);
        Exception? observed = Assert.Catch((Action)actual);
        Assert.That(observed!.GetType(), Is.EqualTo(control!.GetType()));
        Assert.That(observed.Message, Is.EqualTo(control.Message));
    }

    private static byte[] Wav(int encoding, int bits, int channels, int? block = null, int? byteRate = null,
        int? rate = null, byte[]? extra = null, byte[]? rawPayload = null)
    {
        int frequency = rate ?? (channels == 2 ? 48000 : 44100);
        int alignment = block ?? channels * (bits / 8);
        using var body = new MemoryStream();
        using (var samples = new BinaryWriter(body, Encoding.UTF8, true))
            for (int i = 0; i < 9 * Math.Max(1, channels); i++)
            {
                if (encoding == 3 && bits == 64) samples.Write(new[] { 0d, -0d, 1d, -1d, double.Epsilon, double.MaxValue, double.NaN }[i % 7]);
                else if (encoding == 3 && bits == 32) samples.Write(new[] { 0f, -0f, 1f, -1f, float.Epsilon, float.MaxValue, float.NaN }[i % 7]);
                else for (int part = 0; part < bits / 8; part++) samples.Write(unchecked((byte)(i * 43 + part * 97)));
            }
        byte[] payload = rawPayload ?? body.ToArray();
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(0);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(extra == null ? 16 : 18 + extra.Length);
        writer.Write((ushort)encoding); writer.Write((short)channels); writer.Write(frequency);
        writer.Write(byteRate ?? frequency * alignment); writer.Write((short)alignment); writer.Write((short)bits);
        if (extra != null) { writer.Write((short)extra.Length); writer.Write(extra); }
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(payload.Length); writer.Write(payload);
        if (payload.Length % 2 != 0) writer.Write((byte)0);
        writer.Flush();
        stream.Position = 4; writer.Write((int)stream.Length - 8); writer.Flush();
        return stream.ToArray();
    }
}
