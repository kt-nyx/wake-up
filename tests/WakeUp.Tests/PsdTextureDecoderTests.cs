// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class PsdTextureDecoderTests
{
    [TestCase(8, false)]
    [TestCase(8, true)]
    [TestCase(16, false)]
    [TestCase(16, true)]
    public void CompositePlanesBecomeBottomUpRgbaAndSixteenBitUsesHighByte(int depth, bool rle)
    {
        byte[][] channels = { new byte[] { 1, 2, 3, 4 }, new byte[] { 5, 6, 7, 8 }, new byte[] { 9, 10, 11, 12 } };
        var result = PsdTextureDecoder.Decode(Psd(2, 2, depth, rle, channels));
        Assert.That(result.Width, Is.EqualTo(2)); Assert.That(result.Height, Is.EqualTo(2));
        Assert.That(result.Pixels, Is.EqualTo(new byte[] { 3, 7, 11, 255, 4, 8, 12, 255,
            1, 5, 9, 255, 2, 6, 10, 255 }));
    }

    [TestCase(8, false)]
    [TestCase(8, true)]
    [TestCase(16, false)]
    [TestCase(16, true)]
    public void CompositeAlphaUsesStbWhiteMatteConventionAndIgnoresExtraPlanes(int depth, bool rle)
    {
        byte[][] channels = { new byte[] { 191, 17, 19 }, new byte[] { 127, 18, 20 },
            new byte[] { 255, 19, 21 }, new byte[] { 128, 0, 255 }, new byte[] { 99, 98, 97 } };
        Assert.That(PsdTextureDecoder.Decode(Psd(3, 1, depth, rle, channels)).Pixels,
            Is.EqualTo(new byte[] { 127, 0, 255, 128, 17, 18, 19, 0, 19, 20, 21, 255 }));
    }

    [Test]
    public void PackBitsRepeatsNoOpsAndLiteralRowsRespectDeclaredBoundaries()
    {
        byte[] psd = Psd(4, 1, 8, true, Planes(4));
        // Three planes, each row uses noop + repeat-four, exactly 3 packed bytes.
        psd = psd.Take(40).Concat(new byte[] { 0, 3, 0, 3, 0, 3,
            128, 253, 10, 128, 253, 20, 128, 253, 30 }).ToArray();
        Assert.That(PsdTextureDecoder.Decode(psd).Pixels,
            Is.EqualTo(Enumerable.Repeat(new byte[] { 10, 20, 30, 255 }, 4).SelectMany(x => x).ToArray()));
        psd[47] = 252; // Five output bytes cannot spill into another row/channel.
        Assert.Throws<InvalidDataException>((Action)(() => PsdTextureDecoder.Decode(psd)));
    }

    [Test]
    public void ReadHeaderPreservesPositionAndRejectsOversizeBeforePixelAllocation()
    {
        byte[] psd = Psd(1, 1, 8, false, Planes(1));
        using var stream = new MemoryStream(psd); stream.Position = 7;
        Assert.That(PsdTextureDecoder.ReadHeader(stream).Width, Is.EqualTo(1));
        Assert.That(stream.Position, Is.EqualTo(7));
        psd[14] = 0; psd[15] = 0; psd[16] = 0x20; psd[17] = 0; // height8192
        psd[18] = 0; psd[19] = 0; psd[20] = 0x20; psd[21] = 0; // width8192
        Assert.Throws<InvalidDataException>((Action)(() => PsdTextureDecoder.Decode(psd)));
    }

    [TestCase(5, 2)] // PSB version2
    [TestCase(23, 32)] // float depth
    [TestCase(25, 4)] // CMYK
    [TestCase(39, 2)] // ZIP compression
    [TestCase(13, 2)] // fewer than RGB planes
    [TestCase(6, 1)] // reserved header
    public void UnsupportedContractsAreRefused(int offset, byte value)
    {
        byte[] psd = Psd(1, 1, 8, false, Planes(1)); psd[offset] = value;
        Assert.Throws<InvalidDataException>((Action)(() => PsdTextureDecoder.Decode(psd)));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TruncatedCompositeAndOversizedSectionsAreRefused(bool rle)
    {
        byte[] psd = Psd(2, 2, 16, rle, Planes(4));
        Assert.Throws<InvalidDataException>((Action)(() => PsdTextureDecoder.Decode(psd.Take(psd.Length - 1).ToArray())));
        psd[26] = 255;
        Assert.Throws<InvalidDataException>((Action)(() => PsdTextureDecoder.Decode(psd)));
    }

    [Test]
    public void MalformedPackBitsCannotReadPastRowOrAcceptMissingPixels()
    {
        byte[] psd = Psd(1, 1, 8, true, Planes(1));
        psd[46] = 1; // Literal-two packet has only one following byte in this row.
        Assert.Throws<InvalidDataException>((Action)(() => PsdTextureDecoder.Decode(psd)));
        psd[46] = 128; psd[47] = 128; // no output, though row is complete on disk.
        Assert.Throws<InvalidDataException>((Action)(() => PsdTextureDecoder.Decode(psd)));
    }

    private static byte[][] Planes(int count) => new[] { new byte[count], new byte[count], new byte[count] };

    private static byte[] Psd(int width, int height, int depth, bool rle, byte[][] planes)
    {
        using var data = new MemoryStream();
        void U16(int value) { data.WriteByte((byte)(value >> 8)); data.WriteByte((byte)value); }
        void U32(int value) { U16(value >> 16); U16(value); }
        U32(0x38425053); U16(1); U32(0); U16(0); U16(planes.Length);
        U32(height); U32(width); U16(depth); U16(3); U32(0); U32(0); U32(0); U16(rle ? 1 : 0);
        if (rle) for (int row = 0; row < planes.Length * height; row++) U16(width * (depth / 8) + 1);
        foreach (byte[] plane in planes)
        for (int row = 0; row < height; row++)
        {
            if (rle) data.WriteByte((byte)(width * (depth / 8) - 1));
            for (int x = 0; x < width; x++)
            {
                data.WriteByte(plane[row * width + x]);
                if (depth == 16) data.WriteByte(0xAB);
            }
        }
        return data.ToArray();
    }
}

