// Copyright (c) 2026 kt-nyx and contributors. GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class PngDeflateStreamTests
{
    [Test]
    public void StoredFinalBlockAndNativeAcceptedTrailingBytesDecode()
    {
        byte[] bytes = Stored(true, new byte[] { 0, 12, 34, 56, 78 });
        Assert.That(Decode(bytes.Concat(new byte[] { 77, 88 }).ToArray()), Is.EqualTo(new byte[] { 0, 12, 34, 56, 78 }));
    }

    [Test]
    public void MissingFinalBlockFailsEvenAfterEveryExpectedRowByteWasProduced()
    {
        using var source = new MemoryStream(Stored(false, new byte[] { 0, 12, 34, 56, 78 }));
        using var decoder = new PngDeflateStream(source, 5, CancellationToken.None);
        byte[] rows = new byte[5];
        Assert.That(decoder.Read(rows, 0, rows.Length), Is.EqualTo(5));
        Assert.That(rows, Is.EqualTo(new byte[] { 0, 12, 34, 56, 78 }));
        Assert.Throws<InvalidDataException>((Action)(() => decoder.ReadByte()));
    }

    [Test]
    public void ConsecutiveStoredBlocksRequireAndHonorFinalHeader()
    {
        byte[] bytes = Stored(false, new byte[] { 1, 2 }).Concat(Stored(true, new byte[] { 3, 4 })).ToArray();
        Assert.That(Decode(bytes), Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
        Assert.Throws<InvalidDataException>((Action)(() => Decode(new byte[] { 1, 1, 0, 0, 0, 7 })));
    }

    [Test]
    public void FixedCodesDecodeEveryLiteralAndOverlappingHistoryMatch()
    {
        var bits = new BitWriter(); bits.Number(3, 3); // BFINAL=1, BTYPE=01
        for (int i = 0; i < 256; i++) bits.Fixed(i);
        bits.Fixed(285); // length 258
        bits.Code(0, 5); // distance 1, including overlap
        bits.Fixed(256);
        byte[] expected = Enumerable.Range(0, 256).Select(i => (byte)i).Concat(Enumerable.Repeat((byte)255, 258)).ToArray();
        Assert.That(Decode(bits.Finish()), Is.EqualTo(expected));
    }

    [Test]
    public void PendingMatchesPreserveMaximumDistanceRingWrapAndMixedReads()
    {
        byte[] seed = Enumerable.Range(0, 32768).Select(i => (byte)(i * 37)).ToArray();
        var bits = new BitWriter(); bits.Number(3, 3);
        bits.Fixed(285); bits.Code(29, 5); bits.Number(8191, 13); // 258 bytes at distance 32768.
        bits.Fixed(285); bits.Code(0, 5); // Another 258 bytes at distance 1, overlapping the ring.
        bits.Fixed(256);
        using var source = new MemoryStream(Stored(false, seed).Concat(bits.Finish()).ToArray());
        using var decoder = new PngDeflateStream(source, seed.Length + 516, CancellationToken.None);
        var prefix = new byte[seed.Length];
        Assert.That(decoder.Read(prefix, 0, prefix.Length), Is.EqualTo(prefix.Length));
        Assert.That(prefix, Is.EqualTo(seed));
        var actual = new List<byte>();
        while (actual.Count < 516)
        {
            actual.Add((byte)decoder.ReadByte());
            var buffer = Enumerable.Repeat((byte)213, 19).ToArray();
            int requested = Math.Min(13, 516 - actual.Count);
            Assert.That(decoder.Read(buffer, 3, requested), Is.EqualTo(requested));
            actual.AddRange(buffer.Skip(3).Take(requested));
            Assert.That(buffer.Take(3).Concat(buffer.Skip(3 + requested)), Is.All.EqualTo((byte)213));
        }
        Assert.That(actual, Is.EqualTo(seed.Take(258).Concat(Enumerable.Repeat(seed[257], 258))));
        Assert.That(decoder.Position, Is.EqualTo(seed.Length + 516));
        Assert.That(decoder.ReadByte(), Is.EqualTo(-1));
    }

    [Test]
    public void PendingMatchChecksCancellationAndRejectsWholeOverLimitMatchBeforeEmission()
    {
        var bits = new BitWriter(); bits.Number(3, 3); bits.Fixed(65);
        bits.Fixed(285); bits.Code(0, 5); bits.Fixed(256);
        byte[] encoded = bits.Finish();
        using var cancellation = new CancellationTokenSource();
        using var source = new MemoryStream(encoded);
        using var decoder = new PngDeflateStream(source, 259, cancellation.Token);
        Assert.That(decoder.ReadByte(), Is.EqualTo(65));
        Assert.That(decoder.ReadByte(), Is.EqualTo(65)); // Leaves a pending, validated match.
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>((Action)(() => decoder.Read(new byte[257], 0, 257)));
        Assert.That(decoder.Position, Is.EqualTo(2));

        using var limitedSource = new MemoryStream(encoded);
        using var limited = new PngDeflateStream(limitedSource, 2, CancellationToken.None);
        Assert.That(limited.ReadByte(), Is.EqualTo(65));
        Assert.Throws<InvalidDataException>((Action)(() => limited.Read(new byte[1], 0, 1)));
        Assert.That(limited.Position, Is.EqualTo(1));
    }

    [Test]
    public void ShortFinalCodeAtEveryBitAlignmentUsesOnlyAvailableBits()
    {
        for (int count = 0; count < 8; count++)
        {
            var bits = new BitWriter(); bits.Number(3, 3);
            for (int i = 0; i < count; i++) bits.Fixed(144); // Nine-bit literal shifts alignment.
            bits.Fixed(256); // Seven-bit end code; sometimes less than nine bits remain at EOF.
            Assert.That(Decode(bits.Finish()), Is.EqualTo(Enumerable.Repeat((byte)144, count).ToArray()),
                "Literal count " + count);
        }
    }

    [Test]
    public void DynamicLongCodesAndShortEndDecodeWithoutFullEofLookahead()
    {
        var bits = new BitWriter(); LongCodeHeader(bits, true);
        // This complete canonical tree has one code of lengths 1..10 and
        // two of length 11. EOB is the one-bit code; literals 0..10 follow.
        for (int symbol = 0; symbol <= 10; symbol++)
        {
            int length = Math.Min(symbol + 2, 11);
            int code = symbol == 10 ? 2047 : (1 << length) - 2;
            bits.Code(code, length);
        }
        bits.Code(0, 1);
        Assert.That(Decode(bits.Finish()), Is.EqualTo(Enumerable.Range(0, 11).Select(i => (byte)i).ToArray()));
    }

    [Test]
    public void ShortEndBeforeStoredBlockPreservesPrefetchedLengthBytes()
    {
        var bits = new BitWriter(); LongCodeHeader(bits, false);
        bits.Code(0, 1); // One-bit EOB leaves lookahead containing the next LEN byte.
        bits.Number(1, 3); // Final stored block.
        bits.Align();
        byte[] expected = { 17, 23, 61, 97, 131 };
        bits.Number(expected.Length, 16); bits.Number(~expected.Length & 65535, 16);
        foreach (byte value in expected) bits.Number(value, 8);
        Assert.That(Decode(bits.Finish()), Is.EqualTo(expected));
    }

    [Test]
    public void IncompleteLongCodeAtEofCannotBeCompletedByInventedZeroBits()
    {
        var bits = new BitWriter(); LongCodeHeader(bits, true);
        bits.Code(1023, 10); // Prefix of either eleven-bit literal.
        byte[] encoded = bits.Finish();
        // The header ends two bits into a byte. Removing the last byte leaves
        // six real leading-one bits, which cannot resolve a complete symbol.
        using var source = new MemoryStream(encoded.Take(encoded.Length - 1).ToArray());
        using var decoder = new PngDeflateStream(source, 1024, CancellationToken.None);
        Assert.Throws<InvalidDataException>((Action)(() => decoder.ReadByte()));
    }

    [Test]
    public void DeflateStreamDynamicOutputRoundTripsAcrossHistoryWrapAndPartialReads()
    {
        byte[] paragraph = Encoding.ASCII.GetBytes("A PNG scanline has repeated pixels, transparent edges, and many similar adjacent values.\n");
        byte[] expected = Enumerable.Range(0, 1800).SelectMany(i => paragraph.Concat(new[] { (byte)i, (byte)(i >> 8) })).ToArray();
        using var encoded = new MemoryStream();
        using (var encoder = new DeflateStream(encoded, CompressionLevel.Optimal, true)) encoder.Write(expected, 0, expected.Length);
        byte[] source = encoded.ToArray();
        Assert.That((source[0] >> 1) & 3, Is.EqualTo(2), "The independent encoder fixture must exercise a dynamic first block.");
        Assert.That(Decode(source, expected.Length), Is.EqualTo(expected));
    }

    [Test]
    public void MatchCannotReferToMissingHistoryAndReservedCodesFail()
    {
        var beforeHistory = new BitWriter(); beforeHistory.Number(3, 3); beforeHistory.Fixed(257); beforeHistory.Code(0, 5);
        Assert.Throws<InvalidDataException>((Action)(() => Decode(beforeHistory.Finish())));
        var reservedLength = new BitWriter(); reservedLength.Number(3, 3); reservedLength.Fixed(286);
        Assert.Throws<InvalidDataException>((Action)(() => Decode(reservedLength.Finish())));
        var reservedDistance = new BitWriter(); reservedDistance.Number(3, 3); reservedDistance.Fixed(65); reservedDistance.Fixed(257); reservedDistance.Code(30, 5);
        Assert.Throws<InvalidDataException>((Action)(() => Decode(reservedDistance.Finish())));
        Assert.Throws<InvalidDataException>((Action)(() => Decode(new byte[] { 7 })));
    }

    [Test]
    public void OutputLimitAndCancellationRefuseWithoutUnboundedOutputAllocation()
    {
        Assert.Throws<InvalidDataException>((Action)(() => Decode(Stored(true, new byte[] { 1, 2, 3 }), 2)));
        using var cancellation = new CancellationTokenSource();
        using var source = new MemoryStream(Stored(true, new byte[] { 1, 2, 3 }));
        using var decoder = new PngDeflateStream(source, 3, cancellation.Token);
        Assert.That(decoder.ReadByte(), Is.EqualTo(1));
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>((Action)(() => decoder.ReadByte()));
    }

    [Test]
    public void TruncatedFixedBlockCannotMasqueradeAsEndAndSourceOwnershipIsExplicit()
    {
        var bits = new BitWriter(); bits.Number(3, 3); bits.Fixed(65);
        Assert.Throws<InvalidDataException>((Action)(() => Decode(bits.Finish())));
        using var source = new MemoryStream(Stored(true, Array.Empty<byte>()));
        using (var decoder = new PngDeflateStream(source, 0, CancellationToken.None)) Assert.That(decoder.ReadByte(), Is.EqualTo(-1));
        Assert.That(source.CanRead, Is.True);
    }

    private static byte[] Decode(byte[] bytes, long maximum = 1024 * 1024)
    {
        using var source = new MemoryStream(bytes);
        using var decoder = new PngDeflateStream(source, maximum, CancellationToken.None);
        using var output = new MemoryStream();
        var buffer = new byte[137];
        int count;
        while ((count = decoder.Read(buffer, 0, buffer.Length)) != 0) output.Write(buffer, 0, count);
        return output.ToArray();
    }
    private static byte[] Stored(bool final, byte[] output)
    {
        int length = output.Length;
        return new byte[] { final ? (byte)1 : (byte)0, (byte)length, (byte)(length >> 8), (byte)~length, (byte)(~length >> 8) }.Concat(output).ToArray();
    }
    private static void LongCodeHeader(BitWriter bits, bool final)
    {
        bits.Number(final ? 5 : 4, 3); // Dynamic block, 257 literal/length and one empty distance entry.
        bits.Number(0, 5); bits.Number(0, 5); bits.Number(15, 4);
        int[] order = { 16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15 };
        foreach (int symbol in order) bits.Number(symbol <= 15 ? 4 : 0, 3);
        // Code-length symbols 0..15 form a complete four-bit canonical tree.
        for (int symbol = 0; symbol < 257; symbol++)
            bits.Code(symbol == 256 ? 1 : symbol <= 10 ? Math.Min(symbol + 2, 11) : 0, 4);
        bits.Code(0, 4); // No distance symbols are needed for a literal-only block.
    }
    private sealed class BitWriter
    {
        private readonly List<byte> bytes = new();
        private int value, count;
        internal void Number(int number, int length)
        { for (int i = 0; i < length; i++) Bit((number >> i) & 1); }
        internal void Code(int code, int length)
        { for (int i = length - 1; i >= 0; i--) Bit((code >> i) & 1); }
        private void Bit(int bit)
        {
            value |= bit << count++;
            if (count == 8) { bytes.Add((byte)value); value = count = 0; }
        }
        internal void Fixed(int symbol)
        {
            if (symbol < 144) Code(0x30 + symbol, 8);
            else if (symbol < 256) Code(0x190 + symbol - 144, 9);
            else if (symbol < 280) Code(symbol - 256, 7);
            else Code(0xc0 + symbol - 280, 8);
        }
        internal void Align() { if (count != 0) { bytes.Add((byte)value); value = count = 0; } }
        internal byte[] Finish() { Align(); return bytes.ToArray(); }
    }
}
