// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Text;

namespace FixtureMenuObserver;

// Private observer inputs, never a product recognition contract. PNG samples
// have top-to-bottom source rows, straight alpha, and nonzero color under zero
// alpha. Native loading is the authority for final format, mips and CPU bytes.
internal static class C06PngSources
{
    private enum Metadata { None, Gamma, LinearGamma, Srgb, Icc, Chromaticities, SignificantBits, TransparentKey }

    // textures is the caller's existing private C05KnownChannels directory.
    // The optional variant changes exactly one source without changing its byte
    // length. The caller assigns the same timestamp after staging either variant.
    internal static void Stage(string textures, bool changedSource = false)
    {
        if (!Directory.Exists(textures)) throw new DirectoryNotFoundException(textures);
        void Add(string name, int depth, int color, int width = 32, int height = 16,
            bool interlace = false, Metadata metadata = Metadata.None)
            => File.WriteAllBytes(Path.Combine(textures, "c06-" + name + ".png"),
                Encode(width, height, depth, color, interlace, metadata, changedSource && name == "rgba8"));

        Add("rgb8", 8, 2);
        Add("rgba8", 8, 6);
        Add("gray8", 8, 0, 17, 9);
        Add("grayalpha8", 8, 4, 17, 9);
        Add("palette8-trns", 8, 3);
        foreach (int depth in new[] { 1, 2, 4 })
        {
            Add("gray" + depth, depth, 0, 17, 9);
            Add("palette" + depth + "-trns", depth, 3, 17, 9);
        }
        Add("rgba16", 16, 6, 17, 9);
        Add("gray16", 16, 0, 17, 9);
        Add("rgb16", 16, 2, 17, 9);
        Add("grayalpha16", 16, 4, 17, 9);
        Add("adam7-rgba8", 8, 6, 17, 9, interlace: true);
        Add("rgba8-gamma", 8, 6, metadata: Metadata.Gamma);
        Add("rgba8-gamma-linear", 8, 6, 17, 9, metadata: Metadata.LinearGamma);
        Add("rgba8-srgb", 8, 6, metadata: Metadata.Srgb);
        Add("rgba8-icc", 8, 6, metadata: Metadata.Icc);
        Add("rgba8-chrm", 8, 6, metadata: Metadata.Chromaticities);
        Add("rgba8-sbit", 8, 6, metadata: Metadata.SignificantBits);
        Add("rgb8-trns", 8, 2, metadata: Metadata.TransparentKey);
        Add("gray8-trns", 8, 0, 17, 9, metadata: Metadata.TransparentKey);
    }

    private static byte[] Encode(int width, int height, int depth, int color, bool interlace,
        Metadata metadata, bool changed)
    {
        using var png = new MemoryStream();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
        byte[] header = new byte[13];
        Put32(header, 0, (uint)width); Put32(header, 4, (uint)height);
        header[8] = (byte)depth; header[9] = (byte)color; header[12] = (byte)(interlace ? 1 : 0);
        Chunk(png, "IHDR", header);
        if (metadata == Metadata.Gamma || metadata == Metadata.LinearGamma)
            Chunk(png, "gAMA", Words(metadata == Metadata.Gamma ? 45455u : 100000u));
        if (metadata == Metadata.Srgb) Chunk(png, "sRGB", new byte[] { 0 }); // perceptual intent
        if (metadata == Metadata.Icc)
        {
            // Fixed lcms-generated sRGB profile, freely usable profile data only.
            byte[] profile = Convert.FromBase64String("AAACTGxjbXMEQAAAbW50clJHQiBYWVogB+oACQALAAwAMgA1YWNzcE1TRlQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAPbWAAEAAAAA0y1sY21zAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAALZGVzYwAAAQgAAAA2Y3BydAAAAUAAAABMd3RwdAAAAYwAAAAUY2hhZAAAAaAAAAAsclhZWgAAAcwAAAAUYlhZWgAAAeAAAAAUZ1hZWgAAAfQAAAAUclRSQwAAAggAAAAgZ1RSQwAAAggAAAAgYlRSQwAAAggAAAAgY2hybQAAAigAAAAkbWx1YwAAAAAAAAABAAAADGVuVVMAAAAaAAAAHABzAFIARwBCACAAYgB1AGkAbAB0AC0AaQBuAABtbHVjAAAAAAAAAAEAAAAMZW5VUwAAADAAAAAcAE4AbwAgAGMAbwBwAHkAcgBpAGcAaAB0ACwAIAB1AHMAZQAgAGYAcgBlAGUAbAB5WFlaIAAAAAAAAPbWAAEAAAAA0y1zZjMyAAAAAAABDEIAAAXe///zJQAAB5MAAP2Q///7of///aIAAAPcAADAblhZWiAAAAAAAABvoAAAOPUAAAOQWFlaIAAAAAAAACSfAAAPhAAAtsNYWVogAAAAAAAAYpcAALeHAAAY2XBhcmEAAAAAAAMAAAACZmYAAPKnAAANWQAAE9AAAApbY2hybQAAAAAAAwAAAACj1wAAVHsAAEzNAACZmgAAJmYAAA9c");
            byte[] compressed = ZlibStored(profile);
            byte[] chunk = new byte[6 + compressed.Length];
            Encoding.ASCII.GetBytes("sRGB").CopyTo(chunk, 0);
            compressed.CopyTo(chunk, 6);
            Chunk(png, "iCCP", chunk);
        }
        if (metadata == Metadata.Chromaticities)
            Chunk(png, "cHRM", Words(31270, 32900, 64000, 33000, 30000, 60000, 15000, 6000));
        if (metadata == Metadata.SignificantBits) Chunk(png, "sBIT", new byte[] { 5, 6, 5, 4 });
        if (color == 3)
        {
            int count = PaletteCount(depth);
            byte[] palette = new byte[count * 3], alpha = new byte[count];
            for (int i = 0; i < count; i++)
            {
                palette[i * 3] = (byte)(41 + i * 61);
                palette[i * 3 + 1] = (byte)(113 + i * 29);
                palette[i * 3 + 2] = (byte)(227 - i * 17);
                // Two-entry palettes retain one transparent and one opaque
                // entry; larger palettes also exercise partial transparency.
                alpha[i] = i == 0 ? (byte)0 : i == count - 1 ? (byte)255 : (byte)(i * 255 / (count - 1));
            }
            Chunk(png, "PLTE", palette);
            Chunk(png, "tRNS", alpha);
        }
        if (metadata == Metadata.TransparentKey)
        {
            int channels = color == 0 ? 1 : 3;
            byte[] key = new byte[channels * 2];
            for (int c = 0; c < channels; c++)
            {
                // A non-alpha PNG key stores the exact unscaled sample in a
                // big-endian 16-bit field even when image depth is eight bits.
                int sample = Sample(0, 0, c, depth, color, metadata, false);
                key[c * 2] = (byte)(sample >> 8); key[c * 2 + 1] = (byte)sample;
            }
            Chunk(png, "tRNS", key);
        }
        using var scanlines = new MemoryStream();
        if (!interlace) Pass(scanlines, width, height, depth, color, metadata, changed, 0, 0, 1, 1);
        else
        {
            int[] startX = { 0, 4, 0, 2, 0, 1, 0 }, startY = { 0, 0, 4, 0, 2, 0, 1 };
            int[] stepX = { 8, 8, 4, 4, 2, 2, 1 }, stepY = { 8, 8, 8, 4, 4, 2, 2 };
            for (int p = 0; p < 7; p++)
                Pass(scanlines, width, height, depth, color, metadata, changed, startX[p], startY[p], stepX[p], stepY[p]);
        }
        Chunk(png, "IDAT", ZlibStored(scanlines.ToArray()));
        Chunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static void Pass(Stream output, int width, int height, int depth, int color, Metadata metadata,
        bool changed, int startX, int startY, int stepX, int stepY)
    {
        if (startX >= width || startY >= height) return;
        int channels = color == 0 || color == 3 ? 1 : color == 2 ? 3 : color == 4 ? 2 : 4;
        int columns = (width - startX + stepX - 1) / stepX;
        for (int y = startY; y < height; y += stepY)
        {
            byte[] row = new byte[(columns * channels * depth + 7) / 8];
            int bit = 0;
            for (int x = startX; x < width; x += stepX)
            for (int c = 0; c < channels; c++)
            {
                int sample = Sample(x, y, c, depth, color, metadata, changed);
                if (depth == 16) { row[bit / 8] = (byte)(sample >> 8); row[bit / 8 + 1] = (byte)sample; }
                else row[bit / 8] |= (byte)(sample << (8 - depth - bit % 8));
                bit += depth;
            }
            output.WriteByte(0); // None filter; each Adam7 pass has independent rows.
            output.Write(row, 0, row.Length);
        }
    }

    private static int Sample(int x, int y, int channel, int depth, int color, Metadata metadata, bool changed)
    {
        if (color == 3) return (x + 3 * y) % PaletteCount(depth);
        bool alpha = color == 4 && channel == 1 || color == 6 && channel == 3;
        int value;
        if (alpha)
        {
            int rank = (x + 3 * y) % 5;
            value = rank == 4 ? 65535 : rank * 16384;
        }
        else if (color == 0 || color == 4) value = (12345 + x * 7541 + y * 3571) & 65535;
        else if (channel == 0) value = (1234 + x * 4093 + y * 8191) & 65535;
        else if (channel == 1) value = (2345 + x * 3457 + y * 3761) & 65535;
        else value = (3456 + x * 2719 + y * 5501) & 65535;
        // Change a visible sample, leaving every PNG chunk length and the zlib
        // stored-block layout identical between source-freshness variants.
        if (changed && x == 1 && y == 0 && channel == 0) value ^= 0x4000;
        if (metadata == Metadata.SignificantBits)
        {
            int significant = channel == 1 ? 6 : channel == 3 ? 4 : 5;
            int quantized = value >> (16 - significant);
            return quantized * 255 / ((1 << significant) - 1);
        }
        return value >> (16 - depth);
    }

    private static int PaletteCount(int depth) => depth == 8 ? 16 : 1 << depth;

    private static byte[] ZlibStored(byte[] bytes)
    {
        using var result = new MemoryStream();
        result.WriteByte(0x78); result.WriteByte(0x01); // 32 KiB window, no dictionary, valid FCHECK
        int offset = 0;
        do
        {
            int count = Math.Min(65535, bytes.Length - offset);
            bool final = offset + count == bytes.Length;
            result.WriteByte((byte)(final ? 1 : 0)); // BFINAL and stored BTYPE, zero alignment bits
            result.WriteByte((byte)count); result.WriteByte((byte)(count >> 8));
            int complement = count ^ 65535;
            result.WriteByte((byte)complement); result.WriteByte((byte)(complement >> 8));
            result.Write(bytes, offset, count);
            offset += count;
        } while (offset < bytes.Length);
        uint a = 1, b = 0;
        foreach (byte value in bytes) { a = (a + value) % 65521; b = (b + a) % 65521; }
        byte[] adler = Words((b << 16) | a);
        result.Write(adler, 0, adler.Length);
        return result.ToArray();
    }

    private static void Chunk(Stream output, string name, byte[] data)
    {
        byte[] length = Words((uint)data.Length), type = Encoding.ASCII.GetBytes(name);
        output.Write(length, 0, length.Length); output.Write(type, 0, type.Length);
        output.Write(data, 0, data.Length);
        uint crc = 0xffffffff;
        foreach (byte value in type) crc = CrcByte(crc, value);
        foreach (byte value in data) crc = CrcByte(crc, value);
        byte[] checksum = Words(crc ^ 0xffffffff);
        output.Write(checksum, 0, checksum.Length);
    }

    private static uint CrcByte(uint crc, byte value)
    {
        crc ^= value;
        for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0);
        return crc;
    }

    private static byte[] Words(params uint[] values)
    {
        byte[] result = new byte[values.Length * 4];
        for (int i = 0; i < values.Length; i++) Put32(result, i * 4, values[i]);
        return result;
    }

    private static void Put32(byte[] target, int offset, uint value)
    {
        target[offset] = (byte)(value >> 24); target[offset + 1] = (byte)(value >> 16);
        target[offset + 2] = (byte)(value >> 8); target[offset + 3] = (byte)value;
    }
}
