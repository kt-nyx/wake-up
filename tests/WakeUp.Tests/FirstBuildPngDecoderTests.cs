// Copyright (c) 2026 kt-nyx and contributors. GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class FirstBuildPngDecoderTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void EveryFilterReconstructsUnpremultipliedBottomFirstRgba(int filter)
    {
        byte[] upper = { 1, 2, 3, 0, 11, 18, 240, 37, 250, 8, 100, 255 };
        byte[] lower = { 90, 0, 222, 8, 80, 90, 3, 99, 180, 254, 91, 197 };
        byte[] rows = Filter(upper, new byte[12], filter, 4).Concat(Filter(lower, upper, filter, 4)).ToArray();
        var image = Decode(Png(3, 2, 8, 6, rows));
        Assert.That(image.Width, Is.EqualTo(3)); Assert.That(image.Height, Is.EqualTo(2));
        Assert.That(image.Pixels, Is.EqualTo(lower.Concat(upper).ToArray()));
        Assert.That(image.HasTransparency, Is.True);
    }

    [TestCase(0)]
    [TestCase(2)]
    [TestCase(4)]
    [TestCase(6)]
    public void ValidationReturnsMatchingMetadataWithoutPixelsForNonPaletteImages(int colorType)
    {
        int channels = colorType == 0 ? 1 : colorType == 2 ? 3 : colorType == 4 ? 2 : 4;
        byte[] upper = Enumerable.Range(0, channels * 2).Select(i => (byte)(i * 29)).ToArray();
        byte[] lower = upper.Reverse().ToArray();
        byte[] rows = Filter(upper, new byte[upper.Length], 1, channels)
            .Concat(Filter(lower, upper, 4, channels)).ToArray();
        byte[] source = Png(2, 2, 8, colorType, rows, extras: new[] {
            Chunk("gAMA", U32(45455)), Chunk("sRGB", new byte[] { 2 }),
            Chunk("cHRM", new byte[32]), Chunk("tEXt", Encoding.ASCII.GetBytes("Key\0value")) });
        var decoded = Decode(source);
        var validated = FirstBuildPngDecoder.Validate(source, CancellationToken.None);
        Assert.That(validated.Pixels, Is.Empty);
        Assert.That(new object?[] { validated.Width, validated.Height, validated.ColorType, validated.BitDepth,
            validated.Interlaced, validated.HasTransparency, validated.HasIccProfile, validated.HasChromaticities,
            validated.HasSignificantBits, validated.HasOtherAncillaryChunks, validated.Gamma, validated.SrgbIntent },
            Is.EqualTo(new object?[] { decoded.Width, decoded.Height, decoded.ColorType, decoded.BitDepth,
                decoded.Interlaced, decoded.HasTransparency, decoded.HasIccProfile, decoded.HasChromaticities,
                decoded.HasSignificantBits, decoded.HasOtherAncillaryChunks, decoded.Gamma, decoded.SrgbIntent }));
    }

    [Test]
    public void ValidationKeepsStrictShortEndMalformedInputAndPaletteBoundsChecks()
    {
        byte[] rows = { 0, 144, 144, 144, 144, 144 };
        byte[] fixedPacked = FixedLiteralZlib(rows);
        byte[] valid = Assemble(5, 1, 8, 0, new[] { Chunk("IDAT", fixedPacked) });
        Assert.That(Decode(valid).Pixels.Length, Is.EqualTo(20));
        Assert.That(FirstBuildPngDecoder.Validate(valid, CancellationToken.None).Pixels, Is.Empty);
        // The final seven-bit EOB occupies the last raw byte, without enough
        // real bits for a complete nine-bit lookahead. Removing it must fail.
        byte[] truncated = fixedPacked.Take(fixedPacked.Length - 5).Concat(fixedPacked.Skip(fixedPacked.Length - 4)).ToArray();
        byte[] missingFinal = Zlib(new byte[] { 0, 8 }); missingFinal[2] = 0;
        byte[] badAdler = Zlib(new byte[] { 0, 8 }); badAdler[badAdler.Length - 1] ^= 1;
        byte[] badHeader = Zlib(new byte[] { 0, 8 }); badHeader[0] = 0;
        byte[] badCrc = Png(1, 1, 8, 0, new byte[] { 0, 8 }); badCrc[29] ^= 1;
        byte[] palette = Chunk("PLTE", new byte[] { 17, 31, 53 });
        byte[] validPalette = Png(1, 1, 8, 3, new byte[] { 0, 0 }, extras: new[] { palette });
        Assert.That(FirstBuildPngDecoder.Validate(validPalette, CancellationToken.None).Pixels, Is.Empty);
        foreach (byte[] malformed in new[] {
            Assemble(5, 1, 8, 0, new[] { Chunk("IDAT", truncated) }),
            Assemble(1, 1, 8, 0, new[] { Chunk("IDAT", missingFinal) }),
            Assemble(1, 1, 8, 0, new[] { Chunk("IDAT", badAdler) }),
            Assemble(1, 1, 8, 0, new[] { Chunk("IDAT", badHeader) }), badCrc,
            Png(1, 1, 8, 0, new byte[] { 5, 8 }), Png(1, 1, 8, 0, new byte[] { 0 }),
            Png(1, 1, 8, 0, new byte[] { 0, 8, 9 }),
            Png(1, 1, 8, 3, new byte[] { 0, 1 }, extras: new[] { palette }) })
        {
            var decodeError = Assert.Throws<InvalidDataException>((Action)(() => Decode(malformed)));
            var validateError = Assert.Throws<InvalidDataException>((Action)(() => FirstBuildPngDecoder.Validate(malformed, CancellationToken.None)));
            Assert.That(validateError!.Message, Is.EqualTo(decodeError!.Message));
        }
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(4)]
    [TestCase(8)]
    public void PackedGrayscaleExpandsAcrossPartialBytesAndHonorsTransparentSample(int depth)
    {
        int max = (1 << depth) - 1;
        int[] samples = { 0, max, max / 2, 0, max };
        byte[] rows = new byte[] { 0 }.Concat(Pack(samples, depth)).ToArray();
        byte[] trns = { 0, (byte)max };
        var image = Decode(Png(5, 1, depth, 0, rows, extras: new[] { Chunk("tRNS", trns) }));
        byte[] expected = samples.SelectMany(s => new[] { (byte)(s * 255 / max), (byte)(s * 255 / max),
            (byte)(s * 255 / max), s == max ? (byte)0 : (byte)255 }).ToArray();
        Assert.That(image.Pixels, Is.EqualTo(expected));
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(4)]
    [TestCase(8)]
    public void PaletteAlphaDefaultsOpaqueForUnspecifiedEntries(int depth)
    {
        var extra = new[] { Chunk("PLTE", new byte[] { 11, 22, 33, 44, 55, 66 }), Chunk("tRNS", new byte[] { 13 }) };
        byte[] rows = new byte[] { 0 }.Concat(Pack(new[] { 0, 1, 1, 0, 1 }, depth)).ToArray();
        var image = Decode(Png(5, 1, depth, 3, rows, extras: extra));
        Assert.That(image.Pixels, Is.EqualTo(new byte[] { 11, 22, 33, 13, 44, 55, 66, 255, 44, 55, 66, 255,
            11, 22, 33, 13, 44, 55, 66, 255 }));
    }

    [Test]
    public void RgbTransparencyMatchesAllSourceChannels()
    {
        byte[] rows = { 0, 11, 22, 33, 11, 22, 34 };
        var image = Decode(Png(2, 1, 8, 2, rows, extras: new[] { Chunk("tRNS", new byte[] { 0, 11, 0, 22, 0, 33 }) }));
        Assert.That(image.Pixels, Is.EqualTo(new byte[] { 11, 22, 33, 0, 11, 22, 34, 255 }));
    }

    [Test]
    public void OpaqueEightBitRgbRowsExpandAfterFilteringIntoBottomFirstRgba()
    {
        byte[] upper = { 0, 127, 255, 255, 0, 127, 17, 231, 63 };
        byte[] lower = { 255, 255, 0, 0, 127, 255, 233, 7, 19 };
        byte[] rows = Filter(upper, new byte[9], 1, 3).Concat(Filter(lower, upper, 4, 3)).ToArray();
        byte[] bottomFirst = lower.Concat(upper).ToArray();
        byte[] expected = Enumerable.Range(0, 6).SelectMany(i => new[] {
            bottomFirst[i * 3], bottomFirst[i * 3 + 1], bottomFirst[i * 3 + 2], (byte)255 }).ToArray();
        var image = Decode(Png(3, 2, 8, 2, rows));
        Assert.That(image.Pixels, Is.EqualTo(expected));
        Assert.That(image.HasTransparency, Is.False);
    }

    [Test]
    public void GrayscaleAlphaIsStraightAndDuplicatedToRgb()
    {
        var image = Decode(Png(2, 1, 8, 4, new byte[] { 0, 199, 0, 17, 123 }));
        Assert.That(image.Pixels, Is.EqualTo(new byte[] { 199, 199, 199, 0, 17, 17, 17, 123 }));
    }

    [TestCase(0)]
    [TestCase(2)]
    [TestCase(4)]
    [TestCase(6)]
    public void SixteenBitIsExplicitAndUsesHighByteWithoutGammaOrAlphaConversion(int colorType)
    {
        int channels = colorType == 0 ? 1 : colorType == 2 ? 3 : colorType == 4 ? 2 : 4;
        byte[] row = new byte[] { 0 }.Concat(Enumerable.Range(0, channels).SelectMany(i => new[] { (byte)(10 + i * 40), (byte)199 })).ToArray();
        var image = Decode(Png(1, 1, 16, colorType, row));
        Assert.That(image.BitDepth, Is.EqualTo(16));
        byte[] expected = colorType == 0 ? new byte[] { 10, 10, 10, 255 }
            : colorType == 2 ? new byte[] { 10, 50, 90, 255 }
            : colorType == 4 ? new byte[] { 10, 10, 10, 50 } : new byte[] { 10, 50, 90, 130 };
        Assert.That(image.Pixels, Is.EqualTo(expected));
    }

    [Test]
    public void SixteenBitTransparencyComparesLowBytesBeforeReduction()
    {
        var image = Decode(Png(2, 1, 16, 0, new byte[] { 0, 17, 22, 17, 23 },
            extras: new[] { Chunk("tRNS", new byte[] { 17, 22 }) }));
        Assert.That(image.Pixels, Is.EqualTo(new byte[] { 17, 17, 17, 0, 17, 17, 17, 255 }));
    }

    [TestCase(1, 1)]
    [TestCase(2, 9)]
    [TestCase(9, 2)]
    [TestCase(9, 9)]
    public void Adam7PassesReconstructNonSquareImagesAndSkipEmptyPasses(int width, int height)
    {
        byte[] topDown = Enumerable.Range(0, width * height).SelectMany(i => new[] { (byte)i, (byte)(i * 7), (byte)(255 - i), (byte)(i * 3) }).ToArray();
        var rows = new List<byte>();
        int[] xs = { 0, 4, 0, 2, 0, 1, 0 }, ys = { 0, 0, 4, 0, 2, 0, 1 };
        int[] dx = { 8, 8, 4, 4, 2, 2, 1 }, dy = { 8, 8, 8, 4, 4, 2, 2 };
        for (int pass = 0; pass < 7; pass++)
        {
            if (xs[pass] >= width) continue;
            byte[]? previous = null;
            for (int y = ys[pass]; y < height; y += dy[pass])
            {
                var row = new List<byte>();
                for (int x = xs[pass]; x < width; x += dx[pass]) row.AddRange(topDown.Skip((y * width + x) * 4).Take(4));
                byte[] current = row.ToArray();
                rows.AddRange(Filter(current, previous ?? new byte[current.Length], 4, 4)); previous = current;
            }
        }
        var image = Decode(Png(width, height, 8, 6, rows.ToArray(), interlace: true));
        Assert.That(image.Interlaced, Is.True);
        Assert.That(image.Pixels, Is.EqualTo(Enumerable.Range(0, height).Reverse().SelectMany(y => topDown.Skip(y * width * 4).Take(width * 4)).ToArray()));
    }

    [Test]
    public void Adam7PackedSamplesRestartAtEachPassRow()
    {
        // 3x3 one-bit checkerboard, passes 1,4,5,6,7 have data; every row starts a new packed byte.
        byte[] rows = { 0, 0, 0, 0, 0, 0, 0, 128, 0, 128, 0, 160 };
        var image = Decode(Png(3, 3, 1, 0, rows, interlace: true));
        byte[] expected = new[] { 0, 255, 0, 255, 0, 255, 0, 255, 0 }
            .SelectMany(v => new[] { (byte)v, (byte)v, (byte)v, (byte)255 }).ToArray();
        Assert.That(image.Pixels, Is.EqualTo(expected));
    }

    [Test]
    public void MetadataRemainsVisibleToNativeAdmissionWithoutApplyingTransforms()
    {
        var extras = new[] { Chunk("gAMA", U32(45455)), Chunk("sRGB", new byte[] { 2 }),
            Chunk("cHRM", new byte[32]), Chunk("sBIT", new byte[] { 7, 7, 7, 7 }), Chunk("tEXt", Encoding.ASCII.GetBytes("Key\0value")) };
        var image = Decode(Png(1, 1, 8, 6, new byte[] { 0, 17, 31, 53, 127 }, extras: extras));
        Assert.That(image.Gamma, Is.EqualTo(45455)); Assert.That(image.SrgbIntent, Is.EqualTo(2));
        Assert.That(image.HasChromaticities, Is.True); Assert.That(image.HasSignificantBits, Is.True);
        Assert.That(image.HasOtherAncillaryChunks, Is.True); Assert.That(image.HasIccProfile, Is.False);
        Assert.That(image.Pixels, Is.EqualTo(new byte[] { 17, 31, 53, 127 }));
        image = Decode(Png(1, 1, 8, 0, new byte[] { 0, 17 },
            extras: new[] { Chunk("iCCP", new byte[] { 65, 0, 0 }.Concat(Zlib(new byte[] { 1 })).ToArray()) }));
        Assert.That(image.HasIccProfile, Is.True);
    }

    [Test]
    public void ArbitraryIdatBoundariesCanSplitZlibHeaderPayloadAndTrailer()
    {
        byte[] packed = Zlib(new byte[] { 0, 1, 2, 3, 4 });
        byte[][] chunks = packed.Select(b => Chunk("IDAT", new[] { b })).ToArray();
        var image = Decode(Assemble(1, 1, 8, 6, chunks));
        Assert.That(image.Pixels, Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
    }

    [Test]
    public void BadCrcAndAdlerAndZlibHeadersAreRefused()
    {
        byte[] png = Png(1, 1, 8, 0, new byte[] { 0, 8 }); png[29] ^= 1;
        Assert.Throws<InvalidDataException>((Action)(() => Decode(png)));
        byte[] packed = Zlib(new byte[] { 0, 8 }); packed[packed.Length - 1] ^= 1;
        Assert.Throws<InvalidDataException>((Action)(() => Decode(Assemble(1, 1, 8, 0, new[] { Chunk("IDAT", packed) }))));
        packed = Zlib(new byte[] { 0, 8 }); packed[0] = 0;
        Assert.Throws<InvalidDataException>((Action)(() => Decode(Assemble(1, 1, 8, 0, new[] { Chunk("IDAT", packed) }))));
    }

    [Test]
    public void MaximumByteRowsKeepChecksumAcrossBatchTailsAndFilterReconstruction()
    {
        const int width = 8191;
        byte[] filtered = Enumerable.Repeat((byte)255, width).ToArray();
        // Every encoded sample maximizes checksum accumulation. Each row has
        // a full 4096-byte batch and a tail, and later rows change on unfilter.
        byte[] rows = new byte[] { 0 }.Concat(filtered).Concat(new byte[] { 1 }).Concat(filtered)
            .Concat(new byte[] { 2 }).Concat(filtered).ToArray();
        byte[] middle = Enumerable.Range(0, width).Select(i => unchecked((byte)(255 - i))).ToArray();
        byte[] bottom = middle.Select(value => unchecked((byte)(value - 1))).ToArray();
        byte[] expected = bottom.Concat(middle).Concat(filtered)
            .SelectMany(value => new[] { value, value, value, (byte)255 }).ToArray();
        Assert.That(Decode(Png(width, 3, 8, 0, rows)).Pixels, Is.EqualTo(expected));
        // The independent fixture writer computes Adler byte by byte. Repairing
        // PNG CRC after changing that trailer must still fail the zlib checksum.
        byte[] packed = Zlib(rows); packed[packed.Length - 1] ^= 1;
        Assert.Throws<InvalidDataException>((Action)(() => Decode(Assemble(width, 3, 8, 0,
            new[] { Chunk("IDAT", packed) }))));
    }

    [TestCase("acTL")]
    [TestCase("fcTL")]
    [TestCase("fdAT")]
    [TestCase("ABCD")]
    [TestCase("abca")]
    public void AnimationUnknownCriticalAndInvalidReservedChunksAreRefused(string type)
    {
        Assert.Throws<InvalidDataException>((Action)(() => Decode(Png(1, 1, 8, 0, new byte[] { 0, 1 }, extras: new[] { Chunk(type, new byte[0]) }))));
    }

    [Test]
    public void MissingExcessAndInvalidFilteredRowsAreRefused()
    {
        foreach (byte[] rows in new[] { new byte[] { 0 }, new byte[] { 0, 1, 2 }, new byte[] { 5, 1 } })
            Assert.Throws<InvalidDataException>((Action)(() => Decode(Png(1, 1, 8, 0, rows))));
    }

    [Test]
    public void PaletteIndicesOrderAndTransparencyContractsAreChecked()
    {
        var palette = Chunk("PLTE", new byte[] { 1, 2, 3 });
        Assert.Throws<InvalidDataException>((Action)(() => Decode(Png(1, 1, 8, 3, new byte[] { 0, 1 }, extras: new[] { palette }))));
        Assert.Throws<InvalidDataException>((Action)(() => Decode(Png(1, 1, 8, 3, new byte[] { 0, 0 }))));
        Assert.Throws<InvalidDataException>((Action)(() => Decode(Png(1, 1, 8, 6, new byte[] { 0, 0, 0, 0, 0 }, extras: new[] { Chunk("tRNS", new byte[] { 0, 0 }) }))));
        Assert.Throws<InvalidDataException>((Action)(() => Decode(Png(1, 1, 1, 0, new byte[] { 0, 0 }, extras: new[] { Chunk("tRNS", new byte[] { 0, 2 }) }))));
        byte[] packed = Zlib(new byte[] { 0, 0 });
        Assert.Throws<InvalidDataException>((Action)(() => Decode(Assemble(1, 1, 8, 0, new[] {
            Chunk("IDAT", packed.Take(4).ToArray()), Chunk("tEXt", new byte[0]), Chunk("IDAT", packed.Skip(4).ToArray()) }))));
    }

    [TestCase(0, 1)]
    [TestCase(8193, 1)]
    [TestCase(8192, 8192)]
    public void DimensionsAndOutputBudgetAreCheckedBeforeDecode(int width, int height)
    {
        Assert.Throws<InvalidDataException>((Action)(() => Decode(Png(width, height, 8, 6, new byte[] { 0, 1, 2, 3, 4 }))));
    }

    [Test]
    public void CancelledWorkDoesNotReturnAnImage()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>((Action)(() => FirstBuildPngDecoder.Decode(Png(1, 1, 8, 0, new byte[] { 0, 1 }), cancellation.Token)));
        Assert.Throws<OperationCanceledException>((Action)(() => FirstBuildPngDecoder.Validate(Png(1, 1, 8, 0, new byte[] { 0, 1 }), cancellation.Token)));
    }

    [Test]
    public void ChunkCountTruncationAndTrailingDataAreRefused()
    {
        byte[] png = Png(1, 1, 8, 0, new byte[] { 0, 1 });
        Assert.Throws<InvalidDataException>((Action)(() => Decode(png.Take(png.Length - 1).ToArray())));
        Assert.Throws<InvalidDataException>((Action)(() => Decode(png.Concat(new byte[] { 0 }).ToArray())));
        var chunks = Enumerable.Repeat(Chunk("tEXt", new byte[0]), FirstBuildPngDecoder.MaximumChunks).ToArray();
        Assert.Throws<InvalidDataException>((Action)(() => Decode(Png(1, 1, 8, 0, new byte[] { 0, 1 }, extras: chunks))));
    }

    private static FirstBuildPngDecoder.Result Decode(byte[] source) => FirstBuildPngDecoder.Decode(source, CancellationToken.None);
    private static byte[] Png(int width, int height, int depth, int type, byte[] rows, bool interlace = false, byte[][]? extras = null)
        => Assemble(width, height, depth, type, (extras ?? Array.Empty<byte[]>()).Concat(new[] { Chunk("IDAT", Zlib(rows)) }).ToArray(), interlace);
    private static byte[] Assemble(int width, int height, int depth, int type, byte[][] chunks, bool interlace = false)
        => new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.Concat(Chunk("IHDR", U32(width).Concat(U32(height))
            .Concat(new byte[] { (byte)depth, (byte)type, 0, 0, interlace ? (byte)1 : (byte)0 }).ToArray()))
            .Concat(chunks.SelectMany(c => c)).Concat(Chunk("IEND", new byte[0])).ToArray();
    private static byte[] U32(int v) => new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };
    private static byte[] Chunk(string type, byte[] data)
    {
        byte[] body = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        uint crc = uint.MaxValue;
        foreach (byte b in body)
        { crc ^= b; for (int n = 0; n < 8; n++) crc = (crc & 1) == 1 ? (crc >> 1) ^ 0xedb88320U : crc >> 1; }
        return U32(data.Length).Concat(body).Concat(U32(unchecked((int)~crc))).ToArray();
    }
    private static byte[] Zlib(byte[] data)
    {
        // Independently authored stored DEFLATE block; no production encoder dependency.
        if (data.Length > 65535) throw new ArgumentOutOfRangeException(nameof(data));
        uint a = 1, b = 0;
        foreach (byte value in data) { a = (a + value) % 65521; b = (b + a) % 65521; }
        return new byte[] { 0x78, 0x01, 0x01, (byte)data.Length, (byte)(data.Length >> 8),
            (byte)~data.Length, (byte)(~data.Length >> 8) }.Concat(data).Concat(U32(unchecked((int)(b << 16 | a)))).ToArray();
    }
    private static byte[] FixedLiteralZlib(byte[] data)
    {
        var bits = new List<int> { 1, 1, 0 }; // Final fixed-Huffman block.
        foreach (byte value in data)
        {
            int code = value < 144 ? 0x30 + value : 0x190 + value - 144;
            for (int bit = (value < 144 ? 8 : 9) - 1; bit >= 0; bit--) bits.Add((code >> bit) & 1);
        }
        bits.AddRange(Enumerable.Repeat(0, 7)); // End-of-block symbol 256.
        var deflate = new byte[(bits.Count + 7) / 8];
        for (int bit = 0; bit < bits.Count; bit++) deflate[bit / 8] |= (byte)(bits[bit] << (bit & 7));
        byte[] stored = Zlib(data); // Reuse only the independent fixture Adler trailer.
        return new byte[] { 0x78, 0x01 }.Concat(deflate).Concat(stored.Skip(stored.Length - 4)).ToArray();
    }
    private static byte[] Pack(int[] values, int depth)
    {
        var packed = new byte[(values.Length * depth + 7) / 8];
        for (int n = 0; n < values.Length; n++) packed[n * depth / 8] |= (byte)(values[n] << (8 - depth - n * depth % 8));
        return packed;
    }
    private static byte[] Filter(byte[] row, byte[] previous, int filter, int distance)
    {
        var output = new byte[row.Length + 1]; output[0] = (byte)filter;
        for (int n = 0; n < row.Length; n++)
        {
            int left = n < distance ? 0 : row[n - distance], up = previous[n], upperLeft = n < distance ? 0 : previous[n - distance];
            int p = left + up - upperLeft;
            int[] distances = { Math.Abs(p - left), Math.Abs(p - up), Math.Abs(p - upperLeft) };
            int paeth = distances[0] <= distances[1] && distances[0] <= distances[2] ? left : distances[1] <= distances[2] ? up : upperLeft;
            int predictor = filter == 0 ? 0 : filter == 1 ? left : filter == 2 ? up : filter == 3 ? (left + up) / 2 : paeth;
            output[n + 1] = unchecked((byte)(row[n] - predictor));
        }
        return output;
    }
}
