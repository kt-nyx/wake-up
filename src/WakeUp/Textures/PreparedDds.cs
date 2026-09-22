// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;

namespace WakeUp;

// A strict reusable-file contract, not permission to inject these bytes into a
// Unity texture. DDS rows have file orientation; helper raw Unity rows differ.
internal static class PreparedDds
{
    internal static PreparationImageHeader ReadHeader(byte[] bytes)
    {
        ReadDescriptor(bytes, bytes.Length, out int width, out int height, out _, out _, out _);
        return new PreparationImageHeader { Width = width, Height = height, SourceBytes = bytes.Length,
            IsDds = true, OrdinaryMetadata = true };
    }

    internal static PreparationImageHeader ReadHeader(Stream input)
    {
        if (!input.CanSeek || input.Length < 128 || input.Length > PreparationImageHeader.MaximumSource)
            throw new InvalidDataException("dds-source-size");
        input.Position = 0;
        using var reader = new BinaryReader(input, System.Text.Encoding.ASCII, true);
        var header = reader.ReadBytes((int)Math.Min(148, input.Length));
        ReadDescriptor(header, input.Length, out int width, out int height, out _, out _, out _);
        return new PreparationImageHeader { Width = width, Height = height, SourceBytes = input.Length,
            IsDds = true, OrdinaryMetadata = true };
    }

    internal static PngCache.Entry Parse(byte[] bytes)
    {
        ReadDescriptor(bytes, bytes.Length, out int width, out int height, out int format, out int mips, out int offset);
        if (bytes.Length > TexturePreparationHelper.MaximumOutput) throw new InvalidDataException("dds-output-size");
        var pixels = new byte[bytes.Length - offset];
        Buffer.BlockCopy(bytes, offset, pixels, 0, pixels.Length);
        return new PngCache.Entry { Width = width, Height = height, TextureFormat = format, Mips = mips,
            Pixels = pixels, Filter = 2, Aniso = 0,
            GraphicsFormat = (int)(format == 10 ? UnityEngine.Experimental.Rendering.GraphicsFormat.RGBA_DXT1_SRGB
                : format == 12 ? UnityEngine.Experimental.Rendering.GraphicsFormat.RGBA_DXT5_SRGB
                : UnityEngine.Experimental.Rendering.GraphicsFormat.RGBA_BC7_SRGB) };
    }

    internal static byte[] ToFile(PngCache.Entry entry)
    {
        int size = LayoutBytes(entry.Width, entry.Height, entry.TextureFormat, entry.Mips);
        int headerSize = entry.TextureFormat == 25 ? 148 : 128;
        if (size != entry.Pixels.Length || size + headerSize > TexturePreparationHelper.MaximumOutput)
            throw new InvalidDataException("dds-output-layout");
        using var output = new MemoryStream();
        using var w = new BinaryWriter(output);
        w.Write(0x20534444u); w.Write(124); w.Write(entry.Mips > 1 ? 0xA1007 : 0x81007);
        w.Write(entry.Height); w.Write(entry.Width);
        w.Write(LayoutBytes(entry.Width, entry.Height, entry.TextureFormat, 1));
        w.Write(0); w.Write(entry.Mips);
        for (int i = 0; i < 11; i++) w.Write(0);
        w.Write(32); w.Write(4); w.Write(entry.TextureFormat == 10 ? 0x31545844 : entry.TextureFormat == 12 ? 0x35545844 : 0x30315844);
        for (int i = 0; i < 5; i++) w.Write(0);
        w.Write(entry.Mips > 1 ? 0x401008 : 0x1000);
        for (int i = 0; i < 4; i++) w.Write(0);
        if (entry.TextureFormat == 25) { w.Write(99); w.Write(3); w.Write(0); w.Write(1); w.Write(1); }
        w.Write(entry.Pixels); w.Flush();
        return output.ToArray();
    }

    internal static int LayoutBytes(int width, int height, int format, int mips)
    {
        if (width < 1 || height < 1 || width > 8192 || height > 8192 || mips < 1
            || mips > PreparationImageHeader.Mips(width, height) || format != 10 && format != 12 && format != 25)
            throw new InvalidDataException("dds-layout");
        long total = 0;
        for (int mip = 0; mip < mips; mip++)
        {
            total += (long)((width + 3) / 4) * ((height + 3) / 4) * (format == 10 ? 8 : 16);
            width = Math.Max(1, width / 2); height = Math.Max(1, height / 2);
        }
        if (total > PreparationImageHeader.MaximumSource) throw new InvalidDataException("dds-payload-size");
        return (int)total;
    }

    private static void ReadDescriptor(byte[] bytes, long completeSize, out int width, out int height, out int format, out int mips, out int offset)
    {
        if (bytes.Length < 128 || completeSize > PreparationImageHeader.MaximumSource
            || U32(bytes, 0) != 0x20534444 || U32(bytes, 4) != 124 || U32(bytes, 76) != 32
            || U32(bytes, 80) != 4 || U32(bytes, 112) != 0 || U32(bytes, 24) > 1)
            throw new InvalidDataException("dds-header-or-dimension");
        if (U32(bytes, 16) > 8192 || U32(bytes, 12) > 8192 || U32(bytes, 28) > 14)
            throw new InvalidDataException("dds-dimensions-or-mips");
        width = (int)U32(bytes, 16); height = (int)U32(bytes, 12);
        if (width < 1 || height < 1 || width > 8192 || height > 8192
            || (long)width * height * 4 > PreparationImageHeader.MaximumDecoded)
            throw new InvalidDataException("decoded-size");
        mips = Math.Max(1, (int)U32(bytes, 28)); offset = 128;
        uint fourcc = U32(bytes, 84);
        if (fourcc == 0x31545844) format = 10;
        else if (fourcc == 0x35545844) format = 12;
        else if (fourcc == 0x30315844)
        {
            if (bytes.Length < 148 || U32(bytes, 132) != 3 || U32(bytes, 136) != 0 || U32(bytes, 140) != 1
                || U32(bytes, 144) != 0 && U32(bytes, 144) != 1 && U32(bytes, 144) != 3)
                throw new InvalidDataException("dds-array-dimension-alpha");
            uint dxgi = U32(bytes, 128);
            format = dxgi == 71 || dxgi == 72 ? 10 : dxgi == 77 || dxgi == 78 ? 12 : dxgi == 98 || dxgi == 99 ? 25 : 0;
            offset = 148;
        }
        else throw new InvalidDataException("dds-format");
        if (completeSize - offset != LayoutBytes(width, height, format, mips)) throw new InvalidDataException("dds-payload-length");
    }
    private static uint U32(byte[] bytes, int offset) => (uint)(bytes[offset] | bytes[offset + 1] << 8
        | bytes[offset + 2] << 16 | bytes[offset + 3] << 24);
}
