// Copyright (c) 2026 kt-nyx and contributors. GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace WakeUp;

// Original managed PNG reader. Owns only private CPU bytes; never creates engine objects.
// Decoding is not a native-fidelity assertion: the caller must admit proven metadata families.
internal static class FirstBuildPngDecoder
{
    internal const int MaximumSourceBytes = 96 * 1024 * 1024;
    internal const int MaximumPixelBytes = 64 * 1024 * 1024;
    internal const int MaximumDimension = 8192;
    internal const int MaximumChunks = 4096;

    internal sealed class Result
    {
        internal int Width, Height, ColorType, BitDepth;
        internal bool Interlaced, HasTransparency, HasIccProfile, HasChromaticities,
            HasSignificantBits, HasOtherAncillaryChunks;
        internal uint? Gamma;
        internal int? SrgbIntent;
        // Straight alpha, bottom row first. 16-bit samples deliberately use the high byte;
        // that family remains separate from 8-bit native-equality admission.
        internal byte[] Pixels = Array.Empty<byte>();
    }

    internal static Result Decode(byte[] source, CancellationToken cancellationToken)
        => DecodeCore(source, cancellationToken, publishPixels: true);

    // Validates the same complete PNG/zlib contract before an external pixel
    // decoder runs. Non-palette samples need no reconstruction to validate;
    // indexed images deliberately retain full decoding to check palette bounds.
    // Indexed validation therefore has the normal decoder's allocation cost.
    // No pixels escape this method, and helper-family admission belongs to its caller.
    internal static Result Validate(byte[] source, CancellationToken cancellationToken)
        => DecodeCore(source, cancellationToken, publishPixels: false);

    private static Result DecodeCore(byte[] source, CancellationToken cancellationToken, bool publishPixels)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (source == null || source.Length < 57 || source.Length > MaximumSourceBytes)
            throw Invalid("source-size");
        byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        for (int i = 0; i < 8; i++) if (source[i] != signature[i]) throw Invalid("signature");
        var result = new Result();
        var segments = new List<ArraySegment<byte>>();
        byte[]? palette = null, transparency = null;
        bool header = false, dataSeen = false, dataEnded = false, ended = false;
        var unique = new HashSet<uint>();
        int chunks = 0;
        long compressedBytes = 0;
        for (int offset = 8; offset < source.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++chunks > MaximumChunks || source.Length - offset < 12) throw Invalid("chunk-count-or-length");
            uint lengthValue = U32(source, offset), type = U32(source, offset + 4);
            if (lengthValue > source.Length - offset - 12) throw Invalid("chunk-truncated");
            int length = (int)lengthValue, start = offset + 8;
            for (int i = offset + 4; i < offset + 8; i++)
                if (!((source[i] >= 65 && source[i] <= 90) || (source[i] >= 97 && source[i] <= 122)))
                    throw Invalid("chunk-type");
            if ((source[offset + 6] & 32) != 0) throw Invalid("chunk-reserved-bit");
            if (Crc(source, offset + 4, length + 4, cancellationToken) != U32(source, start + length))
                throw Invalid("chunk-crc");
            if (!header && type != 0x49484452) throw Invalid("ihdr-first");
            if (dataSeen && type != 0x49444154) dataEnded = true;
            switch (type)
            {
                case 0x49484452: // IHDR
                    if (header || length != 13) throw Invalid("ihdr");
                    uint width = U32(source, start), height = U32(source, start + 4);
                    if (width == 0 || height == 0 || width > MaximumDimension || height > MaximumDimension
                        || (long)width * height * 4 > MaximumPixelBytes) throw Invalid("dimensions");
                    result.Width = (int)width; result.Height = (int)height;
                    result.BitDepth = source[start + 8]; result.ColorType = source[start + 9];
                    if (!LegalDepth(result.ColorType, result.BitDepth) || source[start + 10] != 0
                        || source[start + 11] != 0 || source[start + 12] > 1) throw Invalid("ihdr-format");
                    result.Interlaced = source[start + 12] == 1;
                    result.HasTransparency = result.ColorType == 4 || result.ColorType == 6;
                    header = true;
                    break;
                case 0x504C5445: // PLTE
                    if (dataSeen || palette != null || transparency != null || length == 0 || length > 768
                        || length % 3 != 0 || result.ColorType == 0 || result.ColorType == 4
                        || (result.ColorType == 3 && length / 3 > (1 << result.BitDepth))) throw Invalid("palette");
                    palette = Copy(source, start, length);
                    break;
                case 0x74524E53: // tRNS
                    if (dataSeen || transparency != null || (result.ColorType == 0 ? length != 2
                        : result.ColorType == 2 ? length != 6
                        : result.ColorType == 3 ? palette == null || length == 0 || length > palette.Length / 3 : true))
                        throw Invalid("transparency");
                    transparency = Copy(source, start, length); result.HasTransparency = true;
                    if (result.ColorType != 3)
                        for (int n = 0; n < length; n += 2)
                            if (U16(transparency, n) > ((1 << result.BitDepth) - 1)) throw Invalid("transparency-sample");
                    break;
                case 0x49444154: // IDAT
                    if (dataEnded || (result.ColorType == 3 && palette == null)) throw Invalid("data-order");
                    dataSeen = true; compressedBytes += length;
                    if (length != 0) segments.Add(new ArraySegment<byte>(source, start, length));
                    break;
                case 0x49454E44: // IEND
                    if (!dataSeen || length != 0 || start + 4 != source.Length) throw Invalid("iend");
                    ended = true;
                    break;
                case 0x67414D41: // gAMA
                    BeforePalette(dataSeen, palette, unique, type);
                    if (length != 4 || U32(source, start) == 0) throw Invalid("gamma");
                    result.Gamma = U32(source, start);
                    break;
                case 0x73524742: // sRGB
                    BeforePalette(dataSeen, palette, unique, type);
                    if (length != 1 || source[start] > 3 || result.HasIccProfile) throw Invalid("srgb");
                    result.SrgbIntent = source[start];
                    break;
                case 0x69434350: // iCCP -- retained as an admission flag; no profile decompression.
                    BeforePalette(dataSeen, palette, unique, type);
                    int nameEnd = start;
                    while (nameEnd < start + length && source[nameEnd] != 0 && nameEnd - start <= 79) nameEnd++;
                    if (nameEnd == start || nameEnd - start > 79 || nameEnd + 2 >= start + length
                        || source[nameEnd] != 0 || source[nameEnd + 1] != 0 || result.SrgbIntent.HasValue)
                        throw Invalid("icc-profile");
                    result.HasIccProfile = true;
                    break;
                case 0x6348524D: // cHRM
                    BeforePalette(dataSeen, palette, unique, type);
                    if (length != 32) throw Invalid("chromaticities");
                    result.HasChromaticities = true;
                    break;
                case 0x73424954: // sBIT
                    BeforePalette(dataSeen, palette, unique, type);
                    if (length != (result.ColorType == 3 ? 3 : Channels(result.ColorType))) throw Invalid("significant-bits");
                    for (int n = 0; n < length; n++)
                        if (source[start + n] == 0 || source[start + n] > (result.ColorType == 3 ? 8 : result.BitDepth))
                            throw Invalid("significant-bits");
                    result.HasSignificantBits = true;
                    break;
                case 0x6163544C: // acTL
                case 0x6663544C: // fcTL
                case 0x66644154: // fdAT
                    throw Invalid("animation");
                default:
                    if ((source[offset + 4] & 32) == 0) throw Invalid("unknown-critical-chunk");
                    result.HasOtherAncillaryChunks = true;
                    break;
            }
            offset = start + length + 4;
        }
        if (!ended || compressedBytes < 6) throw Invalid("missing-image-data");
        using var packed = new SegmentStream(segments, compressedBytes);
        int cmf = packed.ReadByte(), flg = packed.ReadByte();
        if ((cmf & 15) != 8 || (cmf >> 4) > 7 || ((cmf << 8) + flg) % 31 != 0 || (flg & 32) != 0)
            throw Invalid("zlib-header");
        uint expectedAdler = 0;
        for (long i = compressedBytes - 4; i < compressedBytes; i++) expectedAdler = (expectedAdler << 8) | packed.ByteAt(i);
        packed.Limit = compressedBytes - 4;
        bool reconstruct = publishPixels || result.ColorType == 3;
        if (reconstruct) result.Pixels = new byte[result.Width * result.Height * 4];
        uint adlerA = 1, adlerB = 0;
        // Covers 16-bit RGBA plus filter bytes and packed-row padding in all
        // Adam7 passes. The bound is output-count admission, not an allocation.
        long maximumFilteredBytes = (long)result.Width * result.Height * 8 + result.Height * 14L + 14;
        using (var inflater = new PngDeflateStream(packed, maximumFilteredBytes, cancellationToken, leaveOpen: true))
        {
            int[] xStart = result.Interlaced ? new[] { 0, 4, 0, 2, 0, 1, 0 } : new[] { 0 };
            int[] yStart = result.Interlaced ? new[] { 0, 0, 4, 0, 2, 0, 1 } : new[] { 0 };
            int[] xStep = result.Interlaced ? new[] { 8, 8, 4, 4, 2, 2, 1 } : new[] { 1 };
            int[] yStep = result.Interlaced ? new[] { 8, 8, 8, 4, 4, 2, 2 } : new[] { 1 };
            int bitsPerPixel = Channels(result.ColorType) * result.BitDepth;
            for (int pass = 0; pass < xStart.Length; pass++)
            {
                int width = Span(result.Width, xStart[pass], xStep[pass]);
                int height = Span(result.Height, yStart[pass], yStep[pass]);
                if (width == 0 || height == 0) continue;
                int rowLength = (width * bitsPerPixel + 7) / 8;
                var previous = reconstruct ? new byte[rowLength] : Array.Empty<byte>();
                var row = new byte[rowLength];
                int filterDistance = Math.Max(1, (bitsPerPixel + 7) / 8);
                for (int y = 0; y < height; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int filter = inflater.ReadByte();
                    if (filter < 0 || filter > 4) throw Invalid("filter");
                    Adler((byte)filter, ref adlerA, ref adlerB);
                    int filled = 0;
                    while (filled < rowLength)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int count = inflater.Read(row, filled, rowLength - filled);
                        if (count == 0) throw Invalid("row-truncated");
                        filled += count;
                    }
                    AdlerRow(row, ref adlerA, ref adlerB, cancellationToken);
                    if (!reconstruct) continue;
                    for (int i = 0; i < rowLength; i++)
                    {
                        int a = i >= filterDistance ? row[i - filterDistance] : 0, b = previous[i];
                        int c = i >= filterDistance ? previous[i - filterDistance] : 0;
                        int predictor = filter == 0 ? 0 : filter == 1 ? a : filter == 2 ? b
                            : filter == 3 ? (a + b) / 2 : Paeth(a, b, c);
                        row[i] = unchecked((byte)(row[i] + predictor));
                    }
                    WritePixels(result, row, width, xStart[pass], xStep[pass], yStart[pass] + y * yStep[pass], palette, transparency);
                    byte[] swap = previous; previous = row; row = swap;
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (inflater.ReadByte() != -1) throw Invalid("excess-pixel-data");
        }
        if ((adlerB << 16 | adlerA) != expectedAdler) throw Invalid("zlib-adler");
        cancellationToken.ThrowIfCancellationRequested();
        if (!publishPixels) result.Pixels = Array.Empty<byte>();
        return result;
    }

    private static void WritePixels(Result r, byte[] row, int width, int startX, int stepX, int y, byte[]? palette, byte[]? trns)
    {
        if (r.BitDepth == 8 && trns == null && stepX == 1)
        {
            int destination = ((r.Height - 1 - y) * r.Width + startX) * 4;
            if (r.ColorType == 6)
            {
                // Reconstructed RGBA bytes already have the exact output
                // channel values. Only their bottom-first row position changes.
                Buffer.BlockCopy(row, 0, r.Pixels, destination, width * 4);
                return;
            }
            if (r.ColorType == 2)
            {
                for (int x = 0, source = 0; x < width; x++, source += 3, destination += 4)
                {
                    r.Pixels[destination] = row[source];
                    r.Pixels[destination + 1] = row[source + 1];
                    r.Pixels[destination + 2] = row[source + 2];
                    r.Pixels[destination + 3] = 255;
                }
                return;
            }
        }
        int channels = Channels(r.ColorType), max = (1 << r.BitDepth) - 1;
        for (int x = 0; x < width; x++)
        {
            int sample = x * channels, red = Sample(row, sample, r.BitDepth), green = red, blue = red, alpha = max;
            if (r.ColorType == 3)
            {
                if (palette == null || red >= palette.Length / 3) throw Invalid("palette-index");
                alpha = trns != null && red < trns.Length ? trns[red] : 255;
                int p = red * 3; red = palette[p]; green = palette[p + 1]; blue = palette[p + 2];
            }
            else
            {
                if (r.ColorType == 2 || r.ColorType == 6)
                { green = Sample(row, sample + 1, r.BitDepth); blue = Sample(row, sample + 2, r.BitDepth); }
                if (r.ColorType == 4 || r.ColorType == 6) alpha = Sample(row, sample + channels - 1, r.BitDepth);
                if (trns != null && red == U16(trns, 0) && (r.ColorType == 0
                    || (green == U16(trns, 2) && blue == U16(trns, 4)))) alpha = 0;
                red = ToByte(red, r.BitDepth); green = ToByte(green, r.BitDepth);
                blue = ToByte(blue, r.BitDepth); alpha = ToByte(alpha, r.BitDepth);
            }
            int output = ((r.Height - 1 - y) * r.Width + startX + x * stepX) * 4;
            r.Pixels[output] = (byte)red; r.Pixels[output + 1] = (byte)green;
            r.Pixels[output + 2] = (byte)blue; r.Pixels[output + 3] = (byte)alpha;
        }
    }

    private static int ToByte(int value, int depth) => depth == 16 ? value >> 8 : value * 255 / ((1 << depth) - 1);
    private static int Sample(byte[] row, int sample, int depth) => depth == 16 ? U16(row, sample * 2)
        : depth == 8 ? row[sample] : (row[sample * depth / 8] >> (8 - depth - sample * depth % 8)) & ((1 << depth) - 1);
    private static int Span(int total, int start, int step) => total <= start ? 0 : (total - start + step - 1) / step;
    private static int Channels(int type) => type == 0 || type == 3 ? 1 : type == 2 ? 3 : type == 4 ? 2 : 4;
    private static bool LegalDepth(int type, int depth) => type == 0 ? depth == 1 || depth == 2 || depth == 4 || depth == 8 || depth == 16
        : type == 3 ? depth == 1 || depth == 2 || depth == 4 || depth == 8
        : (type == 2 || type == 4 || type == 6) && (depth == 8 || depth == 16);
    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
    private static void BeforePalette(bool dataSeen, byte[]? palette, HashSet<uint> unique, uint type)
    { if (dataSeen || palette != null || !unique.Add(type)) throw Invalid("metadata-order"); }
    private static byte[] Copy(byte[] source, int offset, int count)
    { var copy = new byte[count]; Buffer.BlockCopy(source, offset, copy, 0, count); return copy; }
    private static uint U32(byte[] b, int i) => ((uint)b[i] << 24) | ((uint)b[i + 1] << 16) | ((uint)b[i + 2] << 8) | b[i + 3];
    private static int U16(byte[] b, int i) => (b[i] << 8) | b[i + 1];
    private static void Adler(byte value, ref uint a, ref uint b) { a = (a + value) % 65521; b = (b + a) % 65521; }
    private static void AdlerRow(byte[] row, ref uint a, ref uint b, CancellationToken token)
    {
        // Reduce after bounded chunks instead of two divisions per source byte.
        // With a,b <=65520 and at most4096 bytes of255, b stays below2^32.
        for (int offset = 0; offset < row.Length;)
        {
            token.ThrowIfCancellationRequested();
            int end = Math.Min(row.Length, offset + 4096);
            uint first = a, second = b;
            while (offset < end) { first += row[offset++]; second += first; }
            a = first % 65521; b = second % 65521;
        }
    }
    private static readonly uint[] CrcTable = MakeCrcTable();
    private static uint[] MakeCrcTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        { uint c = i; for (int n = 0; n < 8; n++) c = (c & 1) != 0 ? 0xedb88320U ^ (c >> 1) : c >> 1; table[i] = c; }
        return table;
    }
    private static uint Crc(byte[] source, int offset, int count, CancellationToken token)
    {
        uint crc = uint.MaxValue;
        for (int i = 0; i < count; i++)
        { if ((i & 16383) == 0) token.ThrowIfCancellationRequested(); crc = CrcTable[(crc ^ source[offset + i]) & 255] ^ (crc >> 8); }
        return ~crc;
    }
    private static InvalidDataException Invalid(string reason) => new InvalidDataException("first-build-png:" + reason);

    // Concatenated IDAT views: no second compressed-image copy, and zlib framing is excluded
    // from DeflateStream. The fixed source cap also bounds total decompressor input work.
    private sealed class SegmentStream : Stream
    {
        private readonly List<ArraySegment<byte>> segments;
        private int segment, within;
        private long position;
        internal long Limit;
        internal SegmentStream(List<ArraySegment<byte>> segments, long length) { this.segments = segments; Limit = length; }
        internal byte ByteAt(long index)
        {
            foreach (var part in segments)
            { if (index < part.Count) return part.Array![part.Offset + (int)index]; index -= part.Count; }
            throw Invalid("zlib-truncated");
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            int written = 0;
            while (count > 0 && position < Limit && segment < segments.Count)
            {
                var part = segments[segment];
                int take = (int)Math.Min(Math.Min(count, part.Count - within), Limit - position);
                Buffer.BlockCopy(part.Array!, part.Offset + within, buffer, offset, take);
                written += take; offset += take; count -= take; within += take; position += take;
                if (within == part.Count) { segment++; within = 0; }
            }
            return written;
        }
        public override int ReadByte()
        {
            if (position >= Limit || segment >= segments.Count) return -1;
            var part = segments[segment]; int value = part.Array![part.Offset + within++]; position++;
            if (within == part.Count) { segment++; within = 0; }
            return value;
        }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => Limit;
        public override long Position { get => position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
