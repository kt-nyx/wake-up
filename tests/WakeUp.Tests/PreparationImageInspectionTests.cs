// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace WakeUp.Tests;

[TestFixture]
public sealed class PreparationImageInspectionTests
{
    private static readonly PreparationOptions Full = new() { Preset = 1 };

    [TestCase(0, 1)] [TestCase(0, 2)] [TestCase(0, 4)] [TestCase(0, 8)] [TestCase(0, 16)]
    [TestCase(2, 8)] [TestCase(2, 16)] [TestCase(3, 1)] [TestCase(3, 2)] [TestCase(3, 4)] [TestCase(3, 8)]
    [TestCase(4, 8)] [TestCase(4, 16)] [TestCase(6, 8)] [TestCase(6, 16)]
    public void LegalPngDepthsAndInterlacingShareAdmission(int color, int depth)
    {
        var estimate = PreparationImageInspection.Inspect(Png(16, 16, color, depth, interlace: 1), Full, false);
        Assert.That(estimate.Eligible, Is.True, estimate.Reason);
        Assert.That(estimate.OutputWidth, Is.EqualTo(16));
        Assert.That(estimate.Mips, Is.EqualTo(5));
        Assert.That(estimate.MaximumTextureBytes, Is.EqualTo(color == 4 || color == 6 ? 368 : 184));
    }

    [Test]
    public void PaletteTransparencyAndCanonicalColorMetadataAreAdmittedBeforeEncoding()
    {
        byte[] color = Chunk("gAMA", Big(45455)).Concat(Chunk("cHRM", new[] { 31270,32900,64000,33000,30000,60000,15000,6000 }.SelectMany(Big).ToArray()))
            .Concat(Chunk("sRGB", new byte[] { 0 })).Concat(Chunk("tRNS", new byte[] { 0,255 })).ToArray();
        var estimate = PreparationImageInspection.Inspect(Png(16,16,3,1,color), Full, false);
        Assert.That(estimate.Eligible, Is.True, estimate.Reason);
        Assert.That(estimate.MinimumTextureBytes, Is.EqualTo(184));
        Assert.That(estimate.MaximumTextureBytes, Is.EqualTo(368));
        Assert.That(PreparationImageInspection.Inspect(Png(16,16,2,16,Chunk("tRNS", new byte[6])), Full, false).Eligible, Is.True);
    }

    [TestCase(19, 11, 2, 9, 5, 224)]
    [TestCase(3, 1, 3, 1, 1, 4)]
    public void OddAndSmallOutputsKeepTheirDimensionsWithExactRgbaBytes(int width, int height, int preset, int ow, int oh, long bytes)
    {
        var estimate = PreparationImageInspection.Inspect(Png(width,height,2,8), new PreparationOptions { Preset = preset }, false);
        Assert.That(estimate.Eligible, Is.True, estimate.Reason);
        Assert.That((estimate.OutputWidth, estimate.OutputHeight), Is.EqualTo((ow,oh)));
        Assert.That(estimate.Representation, Is.EqualTo("RGBA32"));
        Assert.That(estimate.MinimumTextureBytes, Is.EqualTo(bytes));
        Assert.That(estimate.MaximumTextureBytes, Is.EqualTo(bytes));
    }

    [Test]
    public void MaskEstimateIsExactAndOutputBoundIncludesAlphaPossibility()
    {
        var mask = PreparationImageInspection.Inspect(Png(16,16,6,8), Full, true);
        Assert.That(mask.Eligible, Is.True); Assert.That(mask.MaximumTextureBytes, Is.EqualTo(1364));
        var large = PreparationImageInspection.Inspect(Png(4096,2048,6,8), Full, false);
        Assert.That(large.Eligible, Is.False); Assert.That(large.Reason, Does.Contain("output mip chain"));
    }

    [TestCase(192)] [TestCase(194)]
    public void BaselineAndProgressiveJpegAcceptExifWithoutChangingStoredOrientation(int frame)
    {
        var estimate = PreparationImageInspection.Inspect(Jpeg(frame, Segment(225, Encoding.ASCII.GetBytes("Exif\0\0metadata"))), Full, false);
        Assert.That(estimate.Eligible, Is.True, estimate.Reason);
        Assert.That(estimate.SourceKind, Is.EqualTo("JPEG"));
        Assert.That(estimate.MinimumTextureBytes, Is.EqualTo(estimate.MaximumTextureBytes));
    }

    [TestCase(192)] [TestCase(194)]
    public void CmykJpegIsColorConversionAndNeverAnRgbaMask(int frame)
    {
        byte[] jpeg=Jpeg(frame,Array.Empty<byte>(),4);
        Assert.That(PreparationImageInspection.Inspect(jpeg,Full,false).Eligible,Is.True);
        var mask=PreparationImageInspection.Inspect(jpeg,Full,true);
        Assert.That(mask.Eligible,Is.False);Assert.That(mask.Reason,Does.Contain("ink channels"));
    }

    [TestCase(24,0x40u,0xffu,0xff00u,0xff0000u,0u)]
    [TestCase(24,0x40u,0xff0000u,0xff00u,0xffu,0u)]
    [TestCase(32,0x41u,0xffu,0xff00u,0xff0000u,0xff000000u)]
    [TestCase(32,0x41u,0xff0000u,0xff00u,0xffu,0xff000000u)]
    [TestCase(16,0x40u,0xf800u,0x7e0u,0x1fu,0u)]
    [TestCase(16,0x41u,0xf000u,0xf00u,0xf0u,0xfu)]
    [TestCase(8,2u,0u,0u,0u,0xffu)]
    public void NativeUncompressedDdsLayoutsHaveTheSameBoundedPreview(int bits,uint flags,uint red,uint green,uint blue,uint alpha)
    {
        byte[] source=Dds(bits,flags,red,green,blue,alpha);
        var estimate=PreparationImageInspection.Inspect(source,Full,true);
        Assert.That(estimate.Eligible,Is.True,estimate.Reason);Assert.That(estimate.MaximumTextureBytes,Is.EqualTo(52));
        Assert.That(PreparationImageInspection.Inspect(source.Take(source.Length-1).ToArray(),Full,true).Eligible,Is.False);
    }

    [Test]
    public void Dxt3CodecAvailabilityDoesNotAdvertiseUnsupportedNativeStartup()
    {
        byte[] source=Dds(0,4,0,0,0,0);
        Array.Copy(Encoding.ASCII.GetBytes("DXT3"),0,source,84,4);
        var result=PreparationImageInspection.Inspect(source,Full,false);
        Assert.That(result.Eligible,Is.False);Assert.That(result.Reason,Does.Contain("native DDS loader does not support"));
    }

    [Test]
    public void GammaAndIccMetadataHaveConsistentAdmissionAndConflictsAreVisible()
    {
        byte[] icc=Chunk("iCCP",new byte[]{(byte)'x',0,0,120,156,1});
        foreach (byte[] bytes in new[] { Png(16,16,2,8,Chunk("gAMA",Big(100000))), Png(16,16,2,8,icc),
            Jpeg(192,Segment(226,Encoding.ASCII.GetBytes("ICC_PROFILE\0").Concat(new byte[]{1,1,1}).ToArray())) })
        {
            var result = PreparationImageInspection.Inspect(bytes,Full,false);
            Assert.That(result.Eligible, Is.True, result.Reason);
        }
        var conflict=PreparationImageInspection.Inspect(Png(16,16,2,8,icc.Concat(Chunk("sRGB",new byte[]{0})).ToArray()),Full,false);
        Assert.That(conflict.Eligible,Is.False);Assert.That(conflict.Reason,Does.Contain("conflicting"));
    }

    [Test]
    public void PsdUsesExistingCompositeHeaderAndExplicitOptIn()
    {
        using var input = new MemoryStream(); using var writer = new BinaryWriter(input);
        writer.Write(Encoding.ASCII.GetBytes("8BPS"));writer.Write(new byte[] { 0,1 });writer.Write(new byte[6]);writer.Write(new byte[] { 0,3 });
        writer.Write(Big(2));writer.Write(Big(3));writer.Write(new byte[] { 0,8,0,3 });writer.Write(new byte[14]);writer.Write(new byte[18]);
        byte[] source = input.ToArray();
        Assert.That(PreparationImageInspection.Inspect(source,Full,false).Eligible, Is.False);
        var estimate = PreparationImageInspection.Inspect(source,Full,false,true);
        Assert.That(estimate.Eligible, Is.True, estimate.Reason); Assert.That(estimate.SourceKind, Is.EqualTo("PSD"));
        Assert.That(estimate.MaximumTextureBytes, Is.EqualTo(28));
    }

    [Test]
    public void RgbaHelperResponseHasExactOddMipLayoutAndSampling()
    {
        using var input = new MemoryStream(); using var writer = new BinaryWriter(input,Encoding.ASCII,true);
        writer.Write(Encoding.ASCII.GetBytes("WUTXRES3"));
        foreach(int value in new[]{0,9,5,4,4,224})writer.Write(value);
        writer.Write(new byte[224]);input.Position=0;
        var result = PreparationEncoder.ReadResponse(new BinaryReader(input),19,11,new PreparationOptions {Preset=2,Filter=1,Anisotropy=3,MipBias=.5f},false);
        Assert.That(result.GraphicsFormat, Is.EqualTo(8));Assert.That(result.Pixels.Length,Is.EqualTo(224));
        Assert.That(result.Aniso,Is.EqualTo(3));Assert.That(result.Bias,Is.EqualTo(.5f));
    }

    private static byte[] Png(int width,int height,int color,int depth,byte[]? metadata=null,int interlace=0)
    {
        byte[] header=Big(width).Concat(Big(height)).Concat(new byte[]{(byte)depth,(byte)color,0,0,(byte)interlace}).ToArray();
        return new byte[]{137,80,78,71,13,10,26,10}.Concat(Chunk("IHDR",header))
            .Concat(color==3?Chunk("PLTE",new byte[]{255,0,0,0,255,0}):Array.Empty<byte>())
            .Concat(metadata??Array.Empty<byte>()).Concat(Chunk("IDAT",new byte[]{1})).Concat(Chunk("IEND",Array.Empty<byte>())).ToArray();
    }
    private static byte[] Chunk(string name,byte[] payload)=>Big(payload.Length).Concat(Encoding.ASCII.GetBytes(name)).Concat(payload).Concat(new byte[4]).ToArray();
    private static byte[] Big(int n)=>new[]{(byte)(n>>24),(byte)(n>>16),(byte)(n>>8),(byte)n};
    private static byte[] Segment(int marker,byte[] bytes)=>new byte[]{255,(byte)marker,(byte)((bytes.Length+2)>>8),(byte)(bytes.Length+2)}.Concat(bytes).ToArray();
    private static byte[] Jpeg(int frame,byte[] metadata,int channels=3)=>new byte[]{255,216}.Concat(metadata)
        .Concat(Segment(frame,new byte[]{8,0,16,0,16,(byte)channels}.Concat(Enumerable.Range(1,channels).SelectMany(c=>new byte[]{(byte)c,0x11,0})).ToArray()))
        .Concat(Segment(218,new byte[6])).ToArray();
    private static byte[] Dds(int bits,uint flags,uint red,uint green,uint blue,uint alpha)
    {
        var source=new byte[128+5*2*(bits/8)];
        void Word(int offset,uint value)=>Array.Copy(BitConverter.GetBytes(value),0,source,offset,4);
        Word(0,0x20534444);Word(4,124);Word(8,0x100f);Word(12,2);Word(16,5);Word(28,1);Word(76,32);
        Word(80,flags);Word(88,(uint)bits);Word(92,red);Word(96,green);Word(100,blue);Word(104,alpha);Word(108,0x1000);
        return source;
    }
}
