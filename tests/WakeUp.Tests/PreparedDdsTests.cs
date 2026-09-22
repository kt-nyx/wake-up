// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class PreparedDdsTests
{
    [TestCase(10, 20, 12)]
    [TestCase(12, 4, 4)]
    [TestCase(25, 16, 8)]
    public void FileRoundTripKeepsEveryBlockAndBoundedHeaderRead(int format, int width, int height)
    {
        int mips = PreparationImageHeader.Mips(width, height);
        var entry = new PngCache.Entry { Width = width, Height = height, TextureFormat = format, Mips = mips,
            Pixels = new byte[PreparedDds.LayoutBytes(width, height, format, mips)] };
        for (int i = 0; i < entry.Pixels.Length; i++) entry.Pixels[i] = (byte)i;
        var dds = PreparedDds.ToFile(entry);
        var parsed = PreparedDds.Parse(dds);
        Assert.That(parsed.Pixels, Is.EqualTo(entry.Pixels));
        Assert.That(parsed.TextureFormat, Is.EqualTo(format));
        Assert.That(parsed.Aniso, Is.Zero);
        using var stream = new MemoryStream(dds);
        var header = PreparedDds.ReadHeader(stream);
        Assert.That(header.IsDds, Is.True);
        Assert.That(header.Width, Is.EqualTo(width));
        Assert.That(stream.Position, Is.LessThanOrEqualTo(148));
    }

    [TestCase(16, uint.MaxValue)]
    [TestCase(28, uint.MaxValue)]
    [TestCase(112, 512u)]
    [TestCase(84, 0x34545844u)]
    public void UnsupportedOrMaliciousDdsIsAControlledRefusal(int offset, uint value)
    {
        var bytes = TinyDds();
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, bytes, offset, 4);
        Assert.Throws<InvalidDataException>((Action)(() => PreparedDds.ReadHeader(bytes)));
    }

    [Test]
    public void DdsResponseIsIndependentlyBoundToRequestedDimensionsAndDescriptor()
    {
        var bytes = TinyDds();
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Encoding.ASCII, true);
        writer.Write(Encoding.ASCII.GetBytes("WUTXRES2"));
        foreach (int value in new[] { 0, 4, 4, 10, 3, bytes.Length }) writer.Write(value);
        writer.Write(bytes); writer.Flush(); output.Position = 0;
        var header = new PreparationImageHeader { Width = 16, Height = 16 };
        using var reader = new BinaryReader(output, Encoding.ASCII, true);
        Assert.That(TexturePreparationHelper.ReadDdsResponse(reader, header, 3), Is.EqualTo(bytes));
        output.Position = 0;
        Assert.Throws<InvalidDataException>((Action)(() => TexturePreparationHelper.ReadDdsResponse(reader, header, 2)));
        output.Position = 24; writer.Write(12); writer.Flush(); output.Position = 0;
        Assert.Throws<InvalidDataException>((Action)(() => TexturePreparationHelper.ReadDdsResponse(reader, header, 3)));
    }

    private static byte[] TinyDds() => PreparedDds.ToFile(new PngCache.Entry { Width = 4, Height = 4,
        TextureFormat = 10, Mips = 3, Pixels = new byte[24] });
}
