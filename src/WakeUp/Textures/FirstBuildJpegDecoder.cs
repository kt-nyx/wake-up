// Managed JPEG source adaptation. See third-party/StbImageSharp-JPEG.md.
// Copyright (c) 2026 kt-nyx and contributors. GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Threading;

namespace WakeUp;

internal static partial class FirstBuildJpegDecoder
{
    internal const string Contract = "stbimagesharp-2.30.15-6fd7aebe-jpeg-managed-rgb24-v2";
    internal const int MaximumSourceBytes = 96 * 1024 * 1024;
    internal const int MaximumPixelBytes = 64 * 1024 * 1024;
    internal const int MaximumDimension = 8192;
    internal const long MaximumWorkingBytes = 192L * 1024 * 1024;
    internal const int ScratchAllowance = 1024 * 1024;

    internal class Header
    {
        internal int Width, Height, Components, FrameMarker;
        internal readonly byte[] ComponentIds = new byte[4];
        internal bool Progressive, HasIccProfile, HasExif, HasOtherAppMarkers, Jfif;
        internal int AdobeTransform = -1;
        internal byte[] HorizontalSampling = new byte[4], VerticalSampling = new byte[4];
        internal long SourceBytes, PixelBytes, PlaneBytes, CoefficientBytes, RequiredBytes;
    }

    internal sealed class Result
    {
        internal readonly Header Metadata;
        internal int Width => Metadata.Width;
        internal int Height => Metadata.Height;
        // RGB24, bottom first. These are stb's decoded pixels, not a native-equality claim.
        internal readonly byte[] Pixels;
        internal Result(Header metadata, byte[] pixels) { Metadata = metadata; Pixels = pixels; }
    }

    // Parses only marker/header bytes and preserves stream position. The estimator reserves
    // the caller's complete source, output, padded planes, progressive coefficients and scratch
    // BEFORE source or component allocation. No image entropy is decoded by preflight.
    internal static Header ReadHeader(Stream source, CancellationToken token = default)
    {
        if (source == null || !source.CanRead || !source.CanSeek) throw Invalid("seekable-source");
        long saved = source.Position;
        try
        {
            if (source.Length < 4 || source.Length > MaximumSourceBytes) throw Invalid("source-size");
            source.Position = 0;
            if (Read(source) != 255 || Read(source) != 216) throw Invalid("signature");
            var h = new Header { SourceBytes = source.Length };
            for (int markerCount = 0; markerCount < 4096; markerCount++)
            {
                token.ThrowIfCancellationRequested();
                if (Read(source) != 255) throw Invalid("marker-prefix");
                int marker;
                do
                {
                    if ((source.Position & 16383) == 0) token.ThrowIfCancellationRequested();
                    marker = Read(source);
                } while (marker == 255);
                if (marker == 0 || marker == 216 || marker == 217 || marker == 218 || marker == 1
                    || (marker >= 208 && marker <= 215)) throw Invalid("frame-required");
                int length = (Read(source) << 8 | Read(source)) - 2;
                if (length < 0 || length > source.Length - source.Position) throw Invalid("marker-length");
                long end = source.Position + length;
                if (marker == 192 || marker == 193 || marker == 194)
                {
                    if (length < 6 || Read(source) != 8) throw Invalid("eight-bit-frame-required");
                    h.Height = Read(source) << 8 | Read(source); h.Width = Read(source) << 8 | Read(source);
                    h.Components = Read(source); h.Progressive = marker == 194; h.FrameMarker = marker;
                    if (h.Width < 1 || h.Height < 1 || h.Width > MaximumDimension || h.Height > MaximumDimension
                        || (h.Components != 1 && h.Components != 3 && h.Components != 4)
                        || length != 6 + 3 * h.Components) throw Invalid("frame-dimensions-components");
                    int maxH = 1, maxV = 1;
                    var ids = new bool[256];
                    for (int i = 0; i < h.Components; i++)
                    {
                        int id = Read(source), sampling = Read(source), table = Read(source);
                        h.ComponentIds[i] = (byte)id;
                        if (ids[id]) throw Invalid("duplicate-component"); ids[id] = true;
                        int hs = sampling >> 4, vs = sampling & 15;
                        if (hs < 1 || hs > 4 || vs < 1 || vs > 4 || table > 3) throw Invalid("sampling");
                        h.HorizontalSampling[i] = (byte)hs; h.VerticalSampling[i] = (byte)vs;
                        maxH = Math.Max(maxH, hs); maxV = Math.Max(maxV, vs);
                    }
                    for (int i = 0; i < h.Components; i++)
                    {
                        int hs = h.HorizontalSampling[i], vs = h.VerticalSampling[i];
                        if (maxH % hs != 0 || maxV % vs != 0) throw Invalid("sampling-ratio");
                        long w = (h.Width + maxH * 8 - 1) / (maxH * 8) * hs * 8;
                        long y = (h.Height + maxV * 8 - 1) / (maxV * 8) * vs * 8;
                        h.PlaneBytes += w * y;
                    }
                    h.PixelBytes = (long)h.Width * h.Height * 3;
                    h.CoefficientBytes = h.Progressive ? h.PlaneBytes * 2 : 0;
                    h.RequiredBytes = h.SourceBytes + h.PixelBytes + h.PlaneBytes + h.CoefficientBytes + ScratchAllowance;
                    if (h.PixelBytes > MaximumPixelBytes || h.RequiredBytes > MaximumWorkingBytes) throw Invalid("working-budget");
                    return h;
                }
                if (marker >= 192 && marker <= 207 && marker != 196) throw Invalid("unsupported-frame-or-coding");
                if (marker >= 224 && marker <= 239)
                {
                    byte[] prefix = new byte[Math.Min(length, 16)];
                    for (int n = 0; n < prefix.Length; n++) prefix[n] = (byte)Read(source);
                    ObserveApp(h, marker, prefix, 0, prefix.Length);
                }
                source.Position = end;
            }
            throw Invalid("marker-count");
        }
        finally { source.Position = saved; }
    }

    internal static Result Decode(byte[] source, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (source == null) throw Invalid("source-null");
        using var stream = new MemoryStream(source, false);
        Header header = ReadHeader(stream, token);
        var context = new Context(source, header, token);
        var decoder = new Jpeg { s = context };
        stbi__setup_jpeg(decoder);
        try
        {
            if (stbi__decode_jpeg_image(decoder) == 0 || context.Scans == 0) throw Invalid("decode-failed");
            if (context.Position != source.Length) throw Invalid("trailing-data");
            for (int i = 0; i < header.Components; i++) if (!context.ComponentsSeen[i]) throw Invalid("missing-component");
            byte[] pixels = PublishCpuRows(decoder);
            token.ThrowIfCancellationRequested();
            return new Result(header, pixels);
        }
        catch (IndexOutOfRangeException e) { throw new InvalidDataException("first-build-jpeg:array-boundary", e); }
        catch (OverflowException e) { throw new InvalidDataException("first-build-jpeg:arithmetic-boundary", e); }
        // All data belongs to this call and uses managed arrays. Exceptions/cancellation publish
        // nothing; no pinned memory, unmanaged handles, shared failure text, or worker state exists.
    }

    private static byte[] PublishCpuRows(Jpeg z)
    {
        var context = z.s;
        var output = context.Bytes(checked((int)context.Header.PixelBytes));
        var lines = new Slice<byte>[4];
        var resamplers = new Resample[4];
        for (int k = 0; k < z.s.img_n; k++)
        {
            Component component = z.img_comp[k];
            component.linebuf = context.Bytes((int)z.s.img_x + 3);
            var r = new Resample { hs = z.img_h_max / component.h, vs = z.img_v_max / component.v };
            r.ystep = r.vs >> 1; r.w_lores = ((int)z.s.img_x + r.hs - 1) / r.hs;
            r.line0 = r.line1 = component.data;
            r.resample = r.hs == 1 && r.vs == 1 ? resample_row_1 : r.hs == 1 && r.vs == 2 ? stbi__resample_row_v_2
                : r.hs == 2 && r.vs == 1 ? stbi__resample_row_h_2 : r.hs == 2 && r.vs == 2 ? stbi__resample_row_hv_2 : stbi__resample_row_generic;
            if (r.hs == 2 && (r.vs == 1 || r.vs == 2) && r.w_lores <= 2)
                r.resample = stbi__resample_row_generic;
            resamplers[k] = r;
        }
        bool rgb = z.s.img_n == 3 && (z.rgb == 3 || (z.app14_color_transform == 0 && z.jfif == 0));
        for (int y = 0; y < z.s.img_y; y++)
        {
            context.Token.ThrowIfCancellationRequested();
            for (int k = 0; k < z.s.img_n; k++)
            {
                var r = resamplers[k]; bool bottom = r.ystep >= r.vs >> 1;
                lines[k] = r.resample(z.img_comp[k].linebuf, bottom ? r.line1 : r.line0, bottom ? r.line0 : r.line1, r.w_lores, r.hs);
                if (++r.ystep >= r.vs)
                { r.ystep = 0; r.line0 = r.line1; if (++r.ypos < z.img_comp[k].y) r.line1 += z.img_comp[k].w2; }
            }
            Slice<byte> row = output + ((long)z.s.img_y - 1 - y) * z.s.img_x * 3;
            if (z.s.img_n >= 3 && !rgb && !(z.s.img_n == 4 && z.app14_color_transform == 0))
                stbi__YCbCr_to_RGB_row(row, lines[0], lines[1], lines[2], (int)z.s.img_x, 3);
            // Ordinary YCbCr already produced the complete three-channel row.
            if (z.s.img_n == 3 && !rgb) continue;
            for (int x = 0; x < z.s.img_x; x++)
            {
                int p = x * 3;
                if (z.s.img_n == 1) row[p] = row[p + 1] = row[p + 2] = lines[0][x];
                else if (rgb) { row[p] = lines[0][x]; row[p + 1] = lines[1][x]; row[p + 2] = lines[2][x]; }
                else if (z.s.img_n == 4 && (z.app14_color_transform == 0 || z.app14_color_transform == 2))
                {
                    int k = lines[3][x];
                    for (int channel = 0; channel < 3; channel++)
                    { int c = z.app14_color_transform == 0 ? lines[channel][x] : 255 - row[p + channel]; row[p + channel] = Blinn(c, k); }
                }
            }
        }
        return output.Array!;
    }

    private static byte Blinn(int x, int y) { int t = x * y + 128; return (byte)((t + (t >> 8)) >> 8); }
    private static readonly byte[] JfifTag = { 74, 70, 73, 70, 0 }, AdobeTag = { 65, 100, 111, 98, 101 }, RgbTag = { 82, 71, 66 };
    private static void ObserveApp(Header h, int marker, byte[] bytes, int start, int count)
    {
        bool Match(byte[] signature)
        { if (count < signature.Length) return false; for (int i = 0; i < signature.Length; i++) if (bytes[start + i] != signature[i]) return false; return true; }
        if (marker == 224 && Match(JfifTag)) h.Jfif = true;
        else if (marker == 238 && count >= 12 && Match(AdobeTag)) h.AdobeTransform = bytes[start + 11];
        else if (marker == 225) h.HasExif = true;
        else if (marker == 226) h.HasIccProfile = true; // conservative: all APP2 metadata remains distinct.
        else h.HasOtherAppMarkers = true;
    }
    private static int Read(Stream stream) { int value = stream.ReadByte(); if (value < 0) throw Invalid("truncated"); return value; }
    private static InvalidDataException Invalid(string reason) => new InvalidDataException("first-build-jpeg:" + reason);
    private const int STBI__SCAN_load = 0, STBI__SCAN_type = 1;
    private static int stbi__err(string text) => throw Invalid(text);
    private static byte stbi__get8(Context c)
    { if ((c.Position & 16383) == 0) c.Token.ThrowIfCancellationRequested(); if (c.Position >= c.Source.Length) throw Invalid("truncated"); return c.Source[c.Position++]; }
    private static int stbi__get16be(Context c) => stbi__get8(c) << 8 | stbi__get8(c);
    private static void stbi__skip(Context c, int count)
    { if (count < 0 || count > c.Source.Length - c.Position) throw Invalid("skip-length"); c.Position += count; }
    private static int stbi__at_eof(Context c) => c.Position >= c.Source.Length ? 1 : 0;
    private static uint Rotate(uint value, int bits) => (value << bits) | (value >> (32 - bits));
    private static byte stbi__clamp(int value) => (byte)Math.Max(0, Math.Min(255, value));
    private static int stbi__addints_valid(int a, int b) => (long)a + b >= int.MinValue && (long)a + b <= int.MaxValue ? 1 : 0;
    private static int stbi__mul2shorts_valid(int a, int b) => (long)a * b >= short.MinValue && (long)a * b <= short.MaxValue ? 1 : 0;
    private static int stbi__mad3sizes_valid(int a, int b, int c, int add) => a >= 0 && b >= 0 && c >= 0 && add >= 0 && (long)a * b * c + add <= int.MaxValue ? 1 : 0;

    private sealed class Context
    {
        internal readonly byte[] Source;
        internal readonly Header Header;
        internal readonly CancellationToken Token;
        internal readonly int[] IdctScratch = new int[64], HuffmanScratch = new int[16];
        internal readonly short[] BlockScratch = new short[64];
        internal int Position, Markers, Scans, img_n, PaddingBits, DecodedBlocks;
        internal readonly bool[] ComponentsSeen = new bool[4], QuantizationTables = new bool[4];
        private int expectedBlocks;
        internal uint img_x, img_y;
        private long allocated;
        internal Context(byte[] source, Header header, CancellationToken token) { Source = source; Header = header; Token = token; }
        private void Charge(long bytes)
        {
            Token.ThrowIfCancellationRequested();
            // Small tables/slices/context fit the remaining scratch allowance. Large arrays and
            // row buffers are charged before construction, including the final output exactly once.
            if (bytes < 0 || bytes > Header.RequiredBytes - Header.SourceBytes - ScratchAllowance / 2 - allocated) throw Invalid("allocation-budget");
            allocated += bytes;
        }
        internal Slice<byte> Bytes(int bytes) { Charge(bytes + 32L); return new byte[bytes]; }
        internal Slice<short> Shorts(int count) { Charge(count * 2L + 32); return new short[count]; }
        internal void CheckFrame(Jpeg j)
        {
            if (img_x != Header.Width || img_y != Header.Height || img_n != Header.Components || (j.progressive != 0) != Header.Progressive) throw Invalid("frame-changed");
            for (int i = 0; i < img_n; i++)
                if (j.img_comp[i].h != Header.HorizontalSampling[i] || j.img_comp[i].v != Header.VerticalSampling[i]) throw Invalid("sampling-changed");
        }
        internal void ObserveApp(int marker, int length)
        { if (length < 0 || length > Source.Length - Position) throw Invalid("app-length"); FirstBuildJpegDecoder.ObserveApp(Header, marker, Source, Position, length); }
        internal void CheckEntropy(Jpeg j)
        { Token.ThrowIfCancellationRequested(); if (PaddingBits > j.code_bits) throw Invalid("truncated-entropy"); }
        internal void BeginScan(Jpeg j)
        {
            expectedBlocks = 0; DecodedBlocks = 0;
            int seen = 0;
            if (j.progressive != 0 && ((j.spec_start == 0 && j.spec_end != 0) || (j.spec_start != 0 && j.scan_n != 1)
                || (j.succ_high != 0 && j.succ_low != j.succ_high - 1))) throw Invalid("progressive-scan-contract");
            for (int i = 0; i < j.scan_n; i++)
            {
                int n = j.order[i]; Component c = j.img_comp[n];
                if ((seen & (1 << n)) != 0) throw Invalid("duplicate-scan-component"); seen |= 1 << n;
                if (!QuantizationTables[c.tq]) throw Invalid("missing-quantization-table");
                if ((j.progressive == 0 || (j.spec_start == 0 && j.succ_high == 0)) && !j.huff_dc[c.hd].Defined) throw Invalid("missing-dc-table");
                if ((j.progressive == 0 || j.spec_start != 0) && !j.huff_ac[c.ha].Defined) throw Invalid("missing-ac-table");
                if (j.progressive == 0 && ComponentsSeen[n]) throw Invalid("duplicate-sequential-scan");
                if (j.progressive != 0 && j.spec_start != 0 && !ComponentsSeen[n]) throw Invalid("dc-before-ac");
                expectedBlocks += j.scan_n == 1 ? ((c.x + 7) / 8) * ((c.y + 7) / 8)
                    : j.img_mcu_x * j.img_mcu_y * c.h * c.v;
            }
        }
        internal void EndScan(Jpeg j)
        {
            CheckEntropy(j);
            if (DecodedBlocks != expectedBlocks) throw Invalid("incomplete-scan");
            if (j.progressive == 0 || (j.spec_start == 0 && j.succ_high == 0))
                for (int i = 0; i < j.scan_n; i++) ComponentsSeen[j.order[i]] = true;
        }
    }

    // Value-type managed slice: pointer arithmetic becomes checked offsets and normal CLR
    // array bounds checks. No allocations for indexing, increments or view construction.
    private readonly struct Slice<T>
    {
        internal readonly T[]? Array;
        private readonly int offset;
        private Slice(T[]? array, int offset) { Array = array; this.offset = offset; }
        internal ref T this[long index] => ref (Array ?? throw Invalid("missing-buffer"))[checked(offset + (int)index)];
        internal void Clear(int count) => System.Array.Clear(Array ?? throw Invalid("missing-buffer"), offset, count);
        public static implicit operator Slice<T>(T[]? array) => new Slice<T>(array, 0);
        public static Slice<T> operator +(Slice<T> slice, long count) => new Slice<T>(slice.Array, checked(slice.offset + (int)count));
        public static Slice<T> operator ++(Slice<T> slice) => slice + 1;
    }
    private delegate void Idct(Slice<byte> output, int stride, Slice<short> data);
    private delegate void ColorRow(Slice<byte> output, Slice<byte> y, Slice<byte> cb, Slice<byte> cr, int count, int step);
    private delegate Slice<byte> ResampleRow(Slice<byte> output, Slice<byte> near, Slice<byte> far, int width, int hs);
    private sealed class Huffman : IDisposable
    {
        internal bool Defined;
        internal readonly byte[] fast = new byte[512], values = new byte[256], size = new byte[257];
        internal readonly ushort[] code = new ushort[256];
        internal readonly uint[] maxcode = new uint[18];
        internal readonly int[] delta = new int[17];
        public void Dispose() { } // keeps the generated fixed-block scopes as managed references.
    }
    private sealed class Component
    {
        internal int id, h, v, tq, hd, ha, dc_pred, x, y, w2, h2, coeff_w, coeff_h;
        internal Slice<byte> data, linebuf;
        internal Slice<short> coeff;
    }
    private sealed class Jpeg
    {
        internal Context s = null!;
        internal int app14_color_transform, code_bits, eob_run, img_h_max, img_mcu_h, img_mcu_w, img_mcu_x, img_mcu_y,
            img_v_max, jfif, nomore, progressive, restart_interval, rgb, scan_n, spec_end, spec_start, succ_high, succ_low, todo;
        internal uint code_buffer;
        internal byte marker;
        internal readonly int[] order = new int[4];
        internal readonly ushort[][] dequant = { new ushort[64], new ushort[64], new ushort[64], new ushort[64] };
        internal readonly short[][] fast_ac = { new short[512], new short[512], new short[512], new short[512] };
        internal readonly Huffman[] huff_ac = { new Huffman(), new Huffman(), new Huffman(), new Huffman() },
            huff_dc = { new Huffman(), new Huffman(), new Huffman(), new Huffman() };
        internal readonly Component[] img_comp = { new Component(), new Component(), new Component(), new Component() };
        internal Idct idct_block_kernel = null!;
        internal ColorRow YCbCr_to_RGB_kernel = null!;
        internal ResampleRow resample_row_hv_2_kernel = null!;
    }
    private sealed class Resample
    {
        internal int hs, vs, w_lores, ypos, ystep;
        internal Slice<byte> line0, line1;
        internal ResampleRow resample = null!;
    }
}
