// Copyright (c) 2026 kt-nyx and contributors. GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class FirstBuildJpegDecoderTests
{
    [TestCase(1, false)]
    [TestCase(3, false)]
    [TestCase(1, true)]
    [TestCase(3, true)]
    public void BaselineAndProgressiveNeutralBlocksProduceRgb24(int components, bool progressive)
    {
        byte[] jpeg = Neutral(8, 8, components, progressive);
        var result = FirstBuildJpegDecoder.Decode(jpeg, CancellationToken.None);
        Assert.That(result.Width, Is.EqualTo(8)); Assert.That(result.Height, Is.EqualTo(8));
        Assert.That(result.Metadata.Progressive, Is.EqualTo(progressive));
        Assert.That(result.Metadata.Components, Is.EqualTo(components));
        Assert.That(result.Pixels, Is.EqualTo(Enumerable.Repeat((byte)128, 8 * 8 * 3).ToArray()));
    }

    [Test]
    public void NonSquareSubsampledPlanesAreCroppedToActualDimensions()
    {
        var result = FirstBuildJpegDecoder.Decode(Neutral(13, 7, 3, false, subsampled: true), CancellationToken.None);
        Assert.That(result.Metadata.HorizontalSampling.Take(3), Is.EqualTo(new byte[] { 2, 1, 1 }));
        Assert.That(result.Metadata.VerticalSampling.Take(3), Is.EqualTo(new byte[] { 2, 1, 1 }));
        Assert.That(result.Pixels, Is.EqualTo(Enumerable.Repeat((byte)128, 13 * 7 * 3).ToArray()));
    }

    [Test]
    public void RowOrderIsBottomFirstAndDcPredictorIsPreserved()
    {
        // Grayscale upper block DC0 =>128; lower block DC8 with quantizer8 =>136.
        byte[] tables = Huffman(0, new byte[] { 1, 1 }, new byte[] { 0, 4 }).Concat(Huffman(16, new byte[] { 1 }, new byte[] { 0 })).ToArray();
        byte[] jpeg = Soi.Concat(Segment(219, new byte[] { 0 }.Concat(Enumerable.Repeat((byte)8, 64)).ToArray()))
            .Concat(Frame(8, 16, 1, false, false)).Concat(Segment(196, tables)).Concat(Scan(new byte[] { 1 }, 0, 63))
            .Concat(new byte[] { 0x28, 0x7f }).Concat(Eoi).ToArray();
        var result = FirstBuildJpegDecoder.Decode(jpeg, CancellationToken.None);
        Assert.That(result.Pixels.Take(8 * 8 * 3), Is.All.EqualTo(136));
        Assert.That(result.Pixels.Skip(8 * 8 * 3), Is.All.EqualTo(128));
    }

    [Test]
    public void HeaderEstimatorIncludesPaddedPlanesCoefficientsOutputAndSourceAndPreservesPosition()
    {
        byte[] jpeg = Neutral(13, 7, 3, true, subsampled: true);
        using var stream = new MemoryStream(jpeg); stream.Position = 3;
        var h = FirstBuildJpegDecoder.ReadHeader(stream);
        Assert.That(stream.Position, Is.EqualTo(3));
        Assert.That(h.PlaneBytes, Is.EqualTo(16 * 16 + 8 * 8 + 8 * 8));
        Assert.That(h.CoefficientBytes, Is.EqualTo(h.PlaneBytes * 2));
        Assert.That(h.RequiredBytes, Is.EqualTo(jpeg.Length + 13 * 7 * 3 + h.PlaneBytes * 3 + FirstBuildJpegDecoder.ScratchAllowance));
    }

    [Test]
    public void MetadataAfterFrameRemainsVisibleAndExifDoesNotRotatePixels()
    {
        byte[] jpeg = Neutral(8, 8, 1, false);
        // Insert after SOF (whose offset is SOI2 + DQT69, length13 for one component).
        int insert = 2 + 69 + 13;
        jpeg = jpeg.Take(insert).Concat(Segment(225, new byte[] { 69, 120, 105, 102, 0, 0 }))
            .Concat(Segment(226, new byte[] { 73, 67, 67, 95, 80, 82, 79, 70, 73, 76, 69, 0 }))
            .Concat(jpeg.Skip(insert)).ToArray();
        var result = FirstBuildJpegDecoder.Decode(jpeg, CancellationToken.None);
        Assert.That(result.Metadata.HasExif, Is.True); Assert.That(result.Metadata.HasIccProfile, Is.True);
        Assert.That(result.Width, Is.EqualTo(8)); Assert.That(result.Pixels, Is.All.EqualTo(128));
    }

    [TestCase(false, 0)]
    [TestCase(true, 0)]
    [TestCase(false, 1)]
    [TestCase(true, 1)]
    [TestCase(false, 255)]
    [TestCase(true, 255)]
    public void AdobeDirectRgbMarkerCannotBypassNativeFamilyAdmission(bool late, byte versionHigh)
    {
        byte[] jpeg = Neutral(8, 8, 3, false);
        int insert = late ? 2 + 69 + 19 : 2; // before or after SOF
        byte[] adobe = new byte[] { 65, 100, 111, 98, 101, versionHigh, 100, 0, 0, 0, 0, 0 };
        jpeg = jpeg.Take(insert).Concat(Segment(238, adobe)).Concat(jpeg.Skip(insert)).ToArray();
        using var input = new MemoryStream(jpeg);
        Assert.That(FirstBuildJpegDecoder.ReadHeader(input).AdobeTransform, Is.EqualTo(late ? -1 : 0));
        var decoded = FirstBuildJpegDecoder.Decode(jpeg, CancellationToken.None);
        Assert.That(decoded.Metadata.AdobeTransform, Is.Zero);
        Assert.That(decoded.Pixels, Is.All.EqualTo(128), "Numeric component IDs with Adobe transform zero select direct RGB.");
        Assert.Throws<NotSupportedException>((Action)(() => FirstBuildImageData.Decode(jpeg, CancellationToken.None)));
    }

    [Test]
    public void DirectRgbComponentIdsAreRetainedAndNotAdmittedAsYcbcr()
    {
        byte[] jpeg = Neutral(8, 8, 3, false);
        int ids = 2 + 69 + 10;
        jpeg[ids] = (byte)'R'; jpeg[ids + 3] = (byte)'G'; jpeg[ids + 6] = (byte)'B';
        using var input = new MemoryStream(jpeg);
        Assert.That(FirstBuildJpegDecoder.ReadHeader(input).ComponentIds.Take(3), Is.EqualTo(new byte[] { 82, 71, 66 }));
        input.Position = 0;
        Assert.Throws<NotSupportedException>((Action)(() => FirstBuildImageData.Estimate(input)));
    }

    [TestCase(0, 8, false)]
    [TestCase(8193, 8, false)]
    [TestCase(8192, 8192, false)]
    [TestCase(8192, 2048, true)]
    public void HeaderRejectsDimensionsAndTotalWorkingBudgetWithoutImageAllocation(int width, int height, bool progressive)
    {
        byte[] jpeg = Soi.Concat(Frame(width, height, 3, progressive, false)).Concat(Eoi).ToArray();
        using var stream = new MemoryStream(jpeg);
        Assert.Throws<InvalidDataException>((Action)(() => FirstBuildJpegDecoder.ReadHeader(stream)));
    }

    [Test]
    public void CancelAndMalformedCallsCannotPolluteOtherWorkers()
    {
        byte[] jpeg = Neutral(8, 8, 1, false);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>((Action)(() => FirstBuildJpegDecoder.Decode(jpeg, cancellation.Token)));
        Assert.Throws<InvalidDataException>((Action)(() => FirstBuildJpegDecoder.Decode(jpeg.Take(jpeg.Length - 2).ToArray(), CancellationToken.None)));
        var tasks = Enumerable.Range(0, 4).Select(_ => Task.Run(() => FirstBuildJpegDecoder.Decode(jpeg, CancellationToken.None))).ToArray();
        Task.WaitAll(tasks);
        foreach (var task in tasks) Assert.That(task.Result.Pixels, Is.All.EqualTo(128));
    }

    [Test]
    public void MissingScansTruncatedEntropyAndTrailingDataAreRefused()
    {
        byte[] jpeg = Neutral(8, 8, 1, false);
        Assert.Throws<InvalidDataException>((Action)(() => FirstBuildJpegDecoder.Decode(jpeg.Concat(new byte[] { 0 }).ToArray(), CancellationToken.None)));
        Assert.Throws<InvalidDataException>((Action)(() => FirstBuildJpegDecoder.Decode(jpeg.Take(jpeg.Length - 3).Concat(Eoi).ToArray(), CancellationToken.None)));
        byte[] noScan = Soi.Concat(Frame(8, 8, 1, false, false)).Concat(Eoi).ToArray();
        Assert.Throws<InvalidDataException>((Action)(() => FirstBuildJpegDecoder.Decode(noScan, CancellationToken.None)));
    }

    private static readonly byte[] Soi = { 255, 216 }, Eoi = { 255, 217 };
    [TestCase(false, 0xdb)]
    [TestCase(true, 0xdb)]
    [TestCase(false, 0xc4)]
    [TestCase(true, 0xc4)]
    public void MalformedTableAfterCompleteScanCannotSubstituteForEndMarker(bool progressive, int marker)
    {
        byte[] complete = Neutral(8, 8, 1, progressive);
        byte[] malformed = complete.Take(complete.Length - 2).Concat(new byte[] { 255, (byte)marker, 0, 1 }).ToArray();
        Assert.Throws<InvalidDataException>((Action)(() => FirstBuildJpegDecoder.Decode(malformed, CancellationToken.None)));
    }
    private static byte[] Neutral(int width, int height, int components, bool progressive, bool subsampled = false)
    {
        byte[] table = Segment(219, new byte[] { 0 }.Concat(Enumerable.Repeat((byte)1, 64)).ToArray());
        byte[] huffman = Segment(196, Huffman(0, new byte[] { 1 }, new byte[] { 0 }).Concat(Huffman(16, new byte[] { 1 }, new byte[] { 0 })).ToArray());
        byte[] jpeg = Soi.Concat(table).Concat(Frame(width, height, components, progressive, subsampled)).Concat(huffman).ToArray();
        int max = subsampled ? 2 : 1, mcuCount = ((width + 8 * max - 1) / (8 * max)) * ((height + 8 * max - 1) / (8 * max));
        int blocks = components == 1 ? ((width + 7) / 8) * ((height + 7) / 8) : mcuCount * (subsampled ? 6 : components);
        byte[] ids = Enumerable.Range(1, components).Select(n => (byte)n).ToArray();
        jpeg = jpeg.Concat(Scan(ids, 0, progressive ? 0 : 63)).Concat(ZeroBits(blocks * (progressive ? 1 : 2))).ToArray();
        if (progressive)
            for (int c = 0; c < components; c++)
            {
                int div = subsampled && c != 0 ? 2 : 1;
                int compWidth = (width + div - 1) / div, compHeight = (height + div - 1) / div;
                int count = ((compWidth + 7) / 8) * ((compHeight + 7) / 8);
                jpeg = jpeg.Concat(Scan(new[] { (byte)(c + 1) }, 1, 63)).Concat(ZeroBits(count)).ToArray();
            }
        return jpeg.Concat(Eoi).ToArray();
    }
    private static byte[] ZeroBits(int count)
    {
        var bytes = new byte[(count + 7) / 8];
        int padding = bytes.Length * 8 - count;
        if (padding != 0) bytes[bytes.Length - 1] = (byte)((1 << padding) - 1);
        return bytes;
    }
    private static byte[] Frame(int width, int height, int components, bool progressive, bool subsampled)
        => Segment(progressive ? 194 : 192, new byte[] { 8, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, (byte)components }
            .Concat(Enumerable.Range(1, components).SelectMany(n => new byte[] { (byte)n, subsampled && n == 1 ? (byte)34 : (byte)17, 0 })).ToArray());
    private static byte[] Huffman(byte type, byte[] counts, byte[] symbols)
        => new[] { type }.Concat(counts).Concat(new byte[16 - counts.Length]).Concat(symbols).ToArray();
    private static byte[] Scan(byte[] ids, int start, int end)
        => Segment(218, new[] { (byte)ids.Length }.Concat(ids.SelectMany(id => new byte[] { id, 0 })).Concat(new byte[] { (byte)start, (byte)end, 0 }).ToArray());
    private static byte[] Segment(int marker, byte[] bytes)
        => new[] { (byte)255, (byte)marker, (byte)((bytes.Length + 2) >> 8), (byte)(bytes.Length + 2) }.Concat(bytes).ToArray();
}
