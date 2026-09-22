// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Text;

namespace WakeUp;

public sealed class PreparationImageEstimate
{
    public bool Eligible;
    public string Reason = "", SourceKind = "", Representation = "";
    public int Width, Height, OutputWidth, OutputHeight, Mips;
    public long MinimumTextureBytes, MaximumTextureBytes;
}

// One header contract for both user interfaces and the explicit encoder. This
// does not decode pixels or promise that a corrupt compressed stream will decode.
// Alpha-capable sources report a BC1..BC3 range; admission reserves the upper end.
public static class PreparationImageInspection
{
    public static PreparationImageEstimate Inspect(byte[] source, PreparationOptions options, bool mask, bool psdEnabled = false)
    {
        var result = new PreparationImageEstimate();
        try
        {
            if (source == null || source.Length < 24 || source.Length > PreparationEncoder.MaximumSource)
                throw new InvalidDataException("Encoded source must be 24 bytes to 16 MiB");
            if (options == null || !options.Valid || options.Preset == 0) throw new InvalidDataException("Select an explicit quality preset");
            bool alpha = Read(source, result, psdEnabled, mask);
            if (result.Width < 1 || result.Height < 1 || result.Width > 8192 || result.Height > 8192
                || (long)result.Width * result.Height * 4 > 64 * 1024 * 1024)
                throw new InvalidDataException("Decoded RGBA source exceeds 8192 per axis or 64 MiB");
            result.OutputWidth = Math.Max(1, result.Width / options.Divisor);
            result.OutputHeight = Math.Max(1, result.Height / options.Divisor);
            // Non-block-aligned images use RGBA32. This avoids Unity's compressed
            // top-level alignment restriction without dropping pixels or padding UVs.
            bool rgba = mask || result.OutputWidth % 4 != 0 || result.OutputHeight % 4 != 0;
            result.Representation = rgba ? "RGBA32" : alpha ? "BC1 if opaque, otherwise BC3" : "BC1";
            for (int w = result.OutputWidth, h = result.OutputHeight; ; w = Math.Max(1, w / 2), h = Math.Max(1, h / 2))
            {
                long size = rgba ? (long)w * h * 4 : (long)((w + 3) / 4) * ((h + 3) / 4) * 8;
                result.MinimumTextureBytes += size;
                result.MaximumTextureBytes += !rgba && alpha ? size * 2 : size;
                result.Mips++;
                if (w == 1 && h == 1) break;
            }
            if (result.MaximumTextureBytes > PreparationEncoder.MaximumOutput)
                throw new InvalidDataException("Complete output mip chain exceeds the bounded helper output (including possible alpha)");
            result.Eligible = true;
            result.Reason = result.SourceKind == "PSD" ? "Optional merged PSD pixels; existing bounded decoder, saved composite only"
                : "Supported header; color metadata converts to sRGB, mask channels stay untransformed, stored orientation is retained. Pixel/profile decoding is checked during preparation";
        }
        catch (Exception ex) when (ex is InvalidDataException || ex is EndOfStreamException || ex is OverflowException || ex is ArgumentException)
        { result.Reason = ex.Message; }
        return result;
    }

    private static bool Read(byte[] b, PreparationImageEstimate result, bool psdEnabled, bool mask)
    {
        if (Ascii(b, 0, 4) == "8BPS")
        {
            result.SourceKind = "PSD";
            if (!psdEnabled) throw new InvalidDataException("PSD support is not selected");
            using var input = new MemoryStream(b, false);
            var header = PsdTextureDecoder.ReadHeader(input);
            result.Width = header.Width; result.Height = header.Height;
            return header.Channels > 3;
        }
        if (Ascii(b, 0, 4) == "DDS ") return Dds(b, result);
        if (BitConverter.ToString(b, 0, 8) == "89-50-4E-47-0D-0A-1A-0A") return Png(b, result);
        return Jpeg(b, result, mask);
    }

    private static bool Png(byte[] b, PreparationImageEstimate r)
    {
        r.SourceKind = "PNG";
        bool header = false, pixels = false, palette = false, alpha = false, end = false, srgb = false, profile = false;
        double[]? chromaticities = null;
        int color = -1, depth = 0, paletteCount = 0, chunks = 0;
        for (int p = 8; p + 12 <= b.Length;)
        {
            if (++chunks > 4096) throw new InvalidDataException("PNG chunk count bound");
            int n = checked((int)Big(b, p)), d = p + 8;
            if (n > b.Length - p - 12) throw new InvalidDataException("PNG chunk bounds");
            string kind = Ascii(b, p + 4, 4);
            if (kind == "IHDR")
            {
                if (header || p != 8 || n != 13) throw new InvalidDataException("PNG IHDR");
                header = true; r.Width = checked((int)Big(b, d)); r.Height = checked((int)Big(b, d + 4));
                depth = b[d + 8]; color = b[d + 9];
                bool valid = color == 0 ? depth == 1 || depth == 2 || depth == 4 || depth == 8 || depth == 16
                    : color == 3 ? depth == 1 || depth == 2 || depth == 4 || depth == 8
                    : (color == 2 || color == 4 || color == 6) && (depth == 8 || depth == 16);
                if (!valid || b[d + 10] != 0 || b[d + 11] != 0 || b[d + 12] > 1) throw new InvalidDataException("PNG color/depth/method");
                alpha = color == 4 || color == 6;
            }
            else if (!header) throw new InvalidDataException("PNG IHDR missing");
            else if (kind == "PLTE")
            {
                if (pixels || palette || n < 3 || n > 768 || n % 3 != 0 || color == 0 || color == 4)
                    throw new InvalidDataException("PNG palette");
                palette = true; paletteCount = n / 3;
                if (color == 3 && paletteCount > 1 << depth) throw new InvalidDataException("PNG palette depth");
            }
            else if (kind == "tRNS")
            {
                if (pixels || (color == 0 ? n != 2 : color == 2 ? n != 6 : color == 3 ? !palette || n < 1 || n > paletteCount : true))
                    throw new InvalidDataException("PNG transparency");
                alpha = true;
            }
            else if (kind == "IDAT") { if (color == 3 && !palette) throw new InvalidDataException("PNG palette missing"); pixels = true; }
            else if (kind == "IEND") { if (n != 0 || p + 12 != b.Length) throw new InvalidDataException("PNG end"); end = true; break; }
            else if (kind == "sRGB") { if (n != 1 || b[d] > 3 || pixels || srgb) throw new InvalidDataException("PNG sRGB"); srgb = true; }
            else if (kind == "gAMA") { if (n != 4 || Big(b, d) == 0 || pixels) throw new InvalidDataException("PNG gamma"); }
            else if (kind == "cHRM")
            {
                if (n != 32 || pixels) throw new InvalidDataException("PNG chromaticities");
                chromaticities = new double[8];
                for (int i = 0; i < 8; i++) chromaticities[i] = Big(b, d + i * 4) / 100000.0;
                for (int i = 0; i < 4; i++)
                    if (Big(b, d + i * 8 + 4) == 0 || (ulong)Big(b, d + i * 8) + Big(b, d + i * 8 + 4) > 100000)
                        throw new InvalidDataException("PNG invalid chromaticity coordinate");
            }
            else if (kind == "iCCP")
            {
                if (profile || pixels || n < 4 || n > 1024 * 1024) throw new InvalidDataException("PNG ICC metadata bound/order");
                int zero = Array.IndexOf(b, (byte)0, d, Math.Min(n, 80));
                if (zero <= d || zero + 2 >= d + n || b[zero + 1] != 0) throw new InvalidDataException("PNG ICC name/compression");
                profile = true;
            }
            else if (kind == "acTL" || kind == "fcTL" || kind == "fdAT") throw new InvalidDataException("Animated PNG is not a single texture");
            else if (kind == "pHYs") { if (n != 9) throw new InvalidDataException("PNG physical size"); }
            else if (kind != "tEXt" && kind != "iTXt" && kind != "zTXt" && kind != "tIME" && kind != "eXIf"
                     && kind != "bKGD" && kind != "sBIT") throw new InvalidDataException("PNG unsupported metadata: " + kind);
            p += n + 12;
        }
        if (!header || !pixels || !end) throw new InvalidDataException("PNG incomplete");
        if (srgb && profile) throw new InvalidDataException("PNG has conflicting sRGB and ICC color declarations");
        if (!srgb && !profile && chromaticities != null) ValidateChromaticities(chromaticities);
        return alpha;
    }

    private static void ValidateChromaticities(double[] xy)
    {
        var p = new double[9];
        for (int c = 0; c < 3; c++)
        { double x = xy[2 + c * 2], y = xy[3 + c * 2]; p[c] = x / y; p[3 + c] = 1; p[6 + c] = (1 - x - y) / y; }
        double Determinant(double[] m) => m[0] * (m[4] * m[8] - m[5] * m[7]) - m[1] * (m[3] * m[8] - m[5] * m[6]) + m[2] * (m[3] * m[7] - m[4] * m[6]);
        double determinant = Determinant(p);
        if (Math.Abs(determinant) <= 1e-10) throw new InvalidDataException("PNG degenerate chromaticities");
        double[] white = { xy[0] / xy[1], 1, (1 - xy[0] - xy[1]) / xy[1] };
        for (int c = 0; c < 3; c++)
        {
            var replaced = (double[])p.Clone();
            for (int r = 0; r < 3; r++) replaced[r * 3 + c] = white[r];
            if (Determinant(replaced) / determinant <= 0) throw new InvalidDataException("PNG white lies outside the declared primary gamut");
        }
        double[] bradford = { .8951,.2664,-.1614,-.7502,1.7135,.0367,.0389,-.0685,1.0296 };
        for (int r = 0; r < 3; r++)
            if (bradford[r * 3] * white[0] + bradford[r * 3 + 1] + bradford[r * 3 + 2] * white[2] <= 0)
                throw new InvalidDataException("PNG white cannot use the supported chromatic adaptation");
    }

    private static bool Jpeg(byte[] b, PreparationImageEstimate r, bool mask)
    {
        r.SourceKind = "JPEG";
        if (b[0] != 255 || b[1] != 216) throw new InvalidDataException("Unsupported image signature");
        bool frame = false; int segments = 0, profileParts = 0, profileTotal = 0, profileBytes = 0;
        var profileSeen = new bool[256];
        for (int p = 2; p + 4 <= b.Length;)
        {
            if (++segments > 4096 || b[p++] != 255) throw new InvalidDataException("JPEG marker bounds");
            while (p < b.Length && b[p] == 255) p++;
            if (p >= b.Length) throw new InvalidDataException("JPEG marker");
            int marker = b[p++];
            if (marker == 0 || marker == 216 || marker == 217 || marker >= 208 && marker <= 215) throw new InvalidDataException("JPEG marker order");
            if (p + 2 > b.Length) throw new InvalidDataException("JPEG segment missing");
            int n = Big16(b, p), d = p + 2;
            if (n < 2 || n > b.Length - p) throw new InvalidDataException("JPEG segment bounds");
            if (marker == 226 && n >= 14 && Ascii(b, d, 12) == "ICC_PROFILE\0")
            {
                if (n < 17 || b[d + 12] == 0 || b[d + 13] == 0 || b[d + 12] > b[d + 13]
                    || profileSeen[b[d + 12]] || profileTotal != 0 && profileTotal != b[d + 13]) throw new InvalidDataException("JPEG ICC segment sequence");
                profileSeen[b[d + 12]] = true; profileTotal = b[d + 13]; profileParts++; profileBytes += n - 16;
                if (profileBytes > 1024 * 1024) throw new InvalidDataException("JPEG ICC profile exceeds 1 MiB");
            }
            if (marker == 192 || marker == 194)
            {
                if (frame || n < 11 || b[d] != 8 || (b[d + 5] != 1 && b[d + 5] != 3 && b[d + 5] != 4) || n != 8 + 3 * b[d + 5])
                    throw new InvalidDataException("JPEG requires baseline/progressive 8-bit gray, RGB or CMYK");
                if (mask && b[d + 5] == 4) throw new InvalidDataException("CMYK JPEG requires color-to-RGB conversion; its four ink channels are not an RGBA mask");
                frame = true; r.Height = Big16(b, d + 1); r.Width = Big16(b, d + 3);
            }
            else if (marker >= 193 && marker <= 207 && marker != 196 && marker != 200 && marker != 204)
                throw new InvalidDataException("JPEG unsupported frame coding");
            if (marker == 218) { if (!frame || profileParts != profileTotal) throw new InvalidDataException("JPEG frame/profile incomplete"); return false; }
            p += n;
        }
        throw new InvalidDataException("JPEG scan missing");
    }

    private static bool Dds(byte[] b, PreparationImageEstimate r)
    {
        r.SourceKind = "DDS";
        if (b.Length < 128 || Little(b, 4) != 124 || Little(b, 76) != 32 || Little(b, 112) != 0 || Little(b, 24) > 1)
            throw new InvalidDataException("DDS requires one ordinary 2D texture");
        r.Width = checked((int)Little(b, 16)); r.Height = checked((int)Little(b, 12));
        int count = (Little(b, 8) & 0x20000) != 0 ? checked((int)Math.Max(1, Little(b, 28))) : 1, offset = 128;
        uint flags = Little(b, 80), format = Little(b, 84); int block = 0, stride = 0; bool alpha = true;
        if ((flags & 4) != 0 && format != 0)
        {
            block = format == 0x31545844 ? 8 : format == 0x35545844 ? 16 : 0;
            if (format == 0x33545844) throw new InvalidDataException("DXT3/BC2 can be decoded by the helper, but the qualified GOG native DDS loader does not support it");
            if (format == 0x30315844)
            {
                if (b.Length < 148 || Little(b, 132) != 3 || Little(b, 136) != 0 || Little(b, 140) != 1
                    || Little(b, 144) != 0 && Little(b, 144) != 1 && Little(b, 144) != 3) throw new InvalidDataException("DDS array/alpha metadata");
                format = Little(b, 128);
                // GOG treats the DX10 marker as BC7 regardless of its format.
                // Do not advertise formats its native producer misinterprets.
                block = format == 98 || format == 99 ? 16 : 0; offset = 148;
            }
        }
        else if ((flags & 64) != 0)
        {
            uint bits = Little(b, 88), red = Little(b, 92), green = Little(b, 96), blue = Little(b, 100), a = Little(b, 104);
            alpha = (flags & 1) != 0;
            bool rgb = red == 255 && green == 65280 && blue == 16711680;
            bool bgr = red == 16711680 && green == 65280 && blue == 255;
            if ((rgb || bgr) && bits == (alpha ? 32u : 24u) && (!alpha || a == 0xff000000)) stride = alpha ? 4 : 3;
            else if (!alpha && bits == 16 && red == 63488 && green == 2016 && blue == 31) stride = 2;
            else if (alpha && bits == 16 && red == 61440 && green == 3840 && blue == 240 && a == 15) stride = 2;
            else throw new InvalidDataException("DDS uncompressed layout is not supported by the qualified native loader");
        }
        else if ((flags & 0x20002) != 0 && Little(b, 88) == 8) { stride = 1; alpha = true; }
        if (block == 0 && stride == 0 || count < 1 || count > 14 || r.Width < 1 || r.Height < 1 || r.Width > 8192 || r.Height > 8192) throw new InvalidDataException("DDS format or dimensions");
        long bytes = 0; int w = r.Width, h = r.Height;
        for (int mip = 0; mip < count; mip++)
        {
            bytes += stride > 0 ? (long)w * h * stride : (long)((w + 3) / 4) * ((h + 3) / 4) * block;
            if (w == 1 && h == 1 && mip != count - 1) throw new InvalidDataException("DDS mip count");
            w = Math.Max(1, w / 2); h = Math.Max(1, h / 2);
        }
        if (bytes != b.Length - offset) throw new InvalidDataException("DDS payload length");
        return alpha; // BC1 may itself contain one-bit alpha; headers cannot prove opacity.
    }
    private static uint Big(byte[] b, int p) => ((uint)b[p] << 24) | ((uint)b[p + 1] << 16) | ((uint)b[p + 2] << 8) | b[p + 3];
    private static int Big16(byte[] b, int p) => (b[p] << 8) | b[p + 1];
    private static uint Little(byte[] b, int p) => BitConverter.ToUInt32(b, p);
    private static string Ascii(byte[] b, int p, int length) => Encoding.ASCII.GetString(b, p, length);
}
