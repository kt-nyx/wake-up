// Copyright (c) 2026 kt-nyx and contributors. GPL-3.0-or-later with LICENSE-EXCEPTION.md.
// Bounded adaptation of the public-domain StbImageSharp 2.30.15 PSD routines.
// See third-party/StbImageSharp-PSD.md and StbImageSharp-LICENSE.txt for provenance.
using System;
using System.IO;

namespace WakeUp;

// This is opt-in new decoding, never an assertion about Unity's native PSD output.
internal static class PsdTextureDecoder
{
    internal const string Contract = "stbimagesharp-2.30.15-6fd7aebe-psd-bounded-rgba8-v1";
    internal const int MaximumSourceBytes = 96 * 1024 * 1024;
    internal const int MaximumPixelBytes = 64 * 1024 * 1024;

    internal sealed class HeaderResult
    {
        internal int Width, Height, Channels, Depth, Compression;
        internal long DataOffset;
    }

    internal sealed class Result
    {
        internal readonly int Width, Height;
        // RGBA8, first row is bottom of image, ready for Unity LoadRawTextureData.
        internal readonly byte[] Pixels;
        internal Result(HeaderResult header, byte[] pixels)
        { Width = header.Width; Height = header.Height; Pixels = pixels; }
    }

    internal static HeaderResult ReadHeader(Stream source)
    {
        if (source == null || !source.CanRead || !source.CanSeek) throw Invalid("seekable-source");
        long start = source.Position;
        try { return ReadHeaderCore(source); }
        finally { source.Position = start; }
    }

    private static HeaderResult ReadHeaderCore(Stream source)
    {
        if (source.Length < 40 || source.Length > MaximumSourceBytes) throw Invalid("source-size");
        source.Position = 0;
        if (U32(source) != 0x38425053 || U16(source) != 1) throw Invalid("signature-version");
        for (int n = 0; n < 6; n++) if (U8(source) != 0) throw Invalid("reserved-header");
        var h = new HeaderResult { Channels = U16(source) };
        uint height = U32(source), width = U32(source);
        if (width < 1 || height < 1 || width > 8192 || height > 8192
            || (long)width * height * 4 > MaximumPixelBytes) throw Invalid("dimensions");
        h.Width = (int)width; h.Height = (int)height;
        h.Depth = U16(source);
        if (h.Channels < 3 || h.Channels > 16 || (h.Depth != 8 && h.Depth != 16)
            || U16(source) != 3) throw Invalid("rgb-channels-depth");
        // PSD's color-mode data, image resources (including ICC), and layer/mask data
        // are not rendered. The following saved merged image is the only input.
        for (int section = 0; section < 3; section++) Skip(source, U32(source));
        h.Compression = U16(source);
        if (h.Compression != 0 && h.Compression != 1) throw Invalid("raw-or-packbits-required");
        h.DataOffset = source.Position;
        long required;
        if (h.Compression == 0)
            required = (long)h.Width * h.Height * h.Channels * (h.Depth / 8);
        else
        {
            int rows = h.Height * h.Channels;
            if ((long)rows * 2 > source.Length - source.Position) throw Invalid("row-table-truncated");
            required = 0;
            for (int row = 0; row < rows; row++) required += U16(source);
        }
        if (required > source.Length - source.Position) throw Invalid("composite-truncated");
        return h;
    }

    internal static Result Decode(byte[] source)
    {
        if (source == null) throw Invalid("source-null");
        using var stream = new MemoryStream(source, false);
        HeaderResult h = ReadHeaderCore(stream);
        var pixels = new byte[h.Width * h.Height * 4];
        if (h.Channels == 3) for (int p = 3; p < pixels.Length; p += 4) pixels[p] = 255;
        int bytesPerSample = h.Depth / 8, rowBytes = h.Width * bytesPerSample;
        int data = checked((int)h.DataOffset);
        int table = data;
        if (h.Compression == 1) data += h.Channels * h.Height * 2;
        for (int channel = 0; channel < h.Channels; channel++)
        for (int row = 0; row < h.Height; row++)
        {
            int output = (h.Height - 1 - row) * h.Width * 4 + channel;
            if (h.Compression == 0)
            {
                for (int x = 0; x < h.Width; x++, data += bytesPerSample)
                    if (channel < 4) pixels[output + x * 4] = source[data];
            }
            else
            {
                int packed = (source[table] << 8) | source[table + 1]; table += 2;
                int end = checked(data + packed), decoded = 0;
                while (data < end)
                {
                    int command = source[data++];
                    if (command == 128) continue; // PackBits no-op is bounded by this row.
                    int count = command < 128 ? command + 1 : 257 - command;
                    if (count > rowBytes - decoded || (command < 128 ? count : 1) > end - data)
                        throw Invalid("packbits-row-length");
                    byte repeated = command > 128 ? source[data++] : (byte)0;
                    for (int n = 0; n < count; n++, decoded++)
                    {
                        byte value = command < 128 ? source[data++] : repeated;
                        if (channel < 4 && decoded % bytesPerSample == 0)
                            pixels[output + decoded / bytesPerSample * 4] = value;
                    }
                }
                if (decoded != rowBytes) throw Invalid("packbits-row-truncated");
            }
        }
        // stb's PSD convention removes Photoshop's white matte from nontrivial
        // alpha. Preserve zero/opaque values; saturate malformed/non-white colors
        // instead of relying on platform-dependent float-to-byte overflow.
        if (h.Channels >= 4)
        for (int p = 0; p < pixels.Length; p += 4)
        {
            byte alpha = pixels[p + 3];
            if (alpha == 0 || alpha == 255) continue;
            float reciprocal = 1.0f / (alpha / 255.0f), offset = 255.0f * (1 - reciprocal);
            for (int c = 0; c < 3; c++)
                pixels[p + c] = (byte)Math.Max(0, Math.Min(255, pixels[p + c] * reciprocal + offset));
        }
        return new Result(h, pixels);
    }

    private static int U8(Stream source)
    {
        int value = source.ReadByte();
        if (value < 0) throw Invalid("truncated");
        return value;
    }
    private static int U16(Stream source) => (U8(source) << 8) | U8(source);
    private static uint U32(Stream source) => ((uint)U16(source) << 16) | (uint)U16(source);
    private static void Skip(Stream source, uint count)
    {
        if (count > source.Length - source.Position) throw Invalid("section-truncated");
        source.Position += count;
    }
    private static InvalidDataException Invalid(string reason) => new InvalidDataException("psd-" + reason);
}
