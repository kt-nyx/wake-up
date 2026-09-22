// Copyright (c) 2026 kt-nyx and contributors. GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class FirstBuildJpegBoundsTests
{
    [TestCase(255, 10, false)] // ordinary AC Huffman path:1023*255 exceeds signed short
    [TestCase(512, 7, false)] // fast AC lookup path:127*512 exceeds signed short
    [TestCase(32768, 1, true)] // progressive unsigned quantizer must not become negative
    [TestCase(512, 7, true)] // progressive finish must check dequantization
    public void ExtremeAcProductsAreRefusedBeforeShortNarrowing(int quantizer, int category, bool progressive)
    {
        var error = Assert.Throws<InvalidDataException>((Action)(() => FirstBuildJpegDecoder.Decode(
            Extreme(quantizer, category, progressive, fillBlock: false), CancellationToken.None)));
        Assert.That(error!.Message, Does.Contain("coefficient-range"));
    }

    [Test]
    public void ExtremeBlockCannotWrapThirteenBitIdctIntermediates()
    {
        // Every AC coefficient is1023*32=32736, individually within signed short.
        // The combination exceeds the admitted32-bit transform intermediates.
        var error = Assert.Throws<InvalidDataException>((Action)(() => FirstBuildJpegDecoder.Decode(
            Extreme(32, 10, progressive: false, fillBlock: true), CancellationToken.None)));
        Assert.That(error!.Message, Does.Contain("arithmetic-boundary"));
        Assert.That(error.InnerException, Is.TypeOf<OverflowException>());
    }

    private static byte[] Extreme(int quantizer, int category, bool progressive, bool fillBlock)
    {
        bool wide = quantizer > 255;
        byte[] quantization = new[] { wide ? (byte)16 : (byte)0 }.Concat(Enumerable.Range(0, 64).SelectMany(_ =>
            wide ? new[] { (byte)(quantizer >> 8), (byte)quantizer } : new[] { (byte)quantizer })).ToArray();
        byte[] dc = new byte[] { 0, 1 }.Concat(new byte[15]).Concat(new byte[] { 0 }).ToArray();
        // AC category has one-bit code0; EOB has two-bit code10.
        byte[] ac = new byte[] { 16, 1, 1 }.Concat(new byte[14]).Concat(new byte[] { (byte)category, 0 }).ToArray();
        byte[] image = new byte[] { 255, 216 }.Concat(Segment(219, quantization))
            .Concat(Segment(progressive ? 194 : wide ? 193 : 192, new byte[] { 8, 0, 8, 0, 8, 1, 1, 17, 0 }))
            .Concat(Segment(196, dc.Concat(ac).ToArray())).ToArray();
        if (progressive)
            image = image.Concat(Scan(0, 0)).Concat(Pack("0" )).Concat(Scan(1, 63)).ToArray();
        else image = image.Concat(Scan(0, 63)).ToArray();
        string entropy = progressive ? "" : "0"; // DC zero
        entropy += string.Concat(Enumerable.Repeat("0" + new string('1', category), fillBlock ? 63 : 1));
        if (!fillBlock) entropy += "10";
        return image.Concat(Pack(entropy)).Concat(new byte[] { 255, 217 }).ToArray();
    }
    private static byte[] Scan(byte start, byte end) => Segment(218, new byte[] { 1, 1, 0, start, end, 0 });
    private static byte[] Segment(int marker, byte[] bytes) => new[] { (byte)255, (byte)marker,
        (byte)((bytes.Length + 2) >> 8), (byte)(bytes.Length + 2) }.Concat(bytes).ToArray();
    private static byte[] Pack(string bits)
    {
        bits = bits.PadRight((bits.Length + 7) / 8 * 8, '1');
        var result = new List<byte>();
        for (int n = 0; n < bits.Length; n += 8)
        {
            byte value = Convert.ToByte(bits.Substring(n, 8), 2); result.Add(value);
            if (value == 255) result.Add(0);
        }
        return result.ToArray();
    }
}
