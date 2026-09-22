// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;

namespace WakeUp;

// Header-only admission precedes native/helper decoding. A decoder still validates
// the complete input. Bounds concern encoded bytes and expanded pixels separately.
internal sealed class PreparationImageHeader
{
    internal const int MaximumSource = 16 * 1024 * 1024;
    internal const int MaximumDecoded = 64 * 1024 * 1024;
    internal int Width, Height;
    internal bool OrdinaryMetadata = true;
    internal bool IsDds;
    internal bool IsPsd;
    internal long SourceBytes;
    internal int FullMips => Mips(Width, Height);
    internal static int Mips(int w, int h) { int n = 1; while (w > 1 || h > 1) { w = Math.Max(1, w / 2); h = Math.Max(1, h / 2); n++; } return n; }
    internal bool LossyFits(int preset) => OrdinaryMetadata && Width >= 4 && Height >= 4
        && (preset != 2 || Width >= 8 && Height >= 8)
        && (preset != 3 || Width >= 16 && Height >= 16);
    internal static PreparationImageHeader Read(Stream input, bool native = false)
    {
        if (!input.CanSeek || input.Length < 24 || input.Length > (native ? PreparedTextureRuntime.MaximumNativeSource : MaximumSource)) throw new InvalidDataException("encoded-size");
        using var r = new BinaryReader(input, System.Text.Encoding.UTF8, true);
        var h = new PreparationImageHeader { SourceBytes = input.Length };
        var signature = r.ReadBytes(8);
        if (signature[0] == '8' && signature[1] == 'B' && signature[2] == 'P' && signature[3] == 'S')
        {
            if (!native || !PreparedTextureRuntime.PsdSupport) throw new InvalidDataException("PSD decoding is not selected");
            input.Position = 0;
            var psd = PsdTextureDecoder.ReadHeader(input);
            return new PreparationImageHeader { Width = psd.Width, Height = psd.Height, IsPsd = true,
                OrdinaryMetadata = false, SourceBytes = input.Length };
        }
        if (EncodingIsDds(signature))
        {
            input.Position = 0;
            return native ? NativeDdsCapture.ReadHeader(input) : PreparedDds.ReadHeader(input);
        }
        if (BitConverter.ToString(signature) == "89-50-4E-47-0D-0A-1A-0A")
        {
            if (U32(r) != 13 || new string(r.ReadChars(4)) != "IHDR") throw new InvalidDataException("png-header");
            h.Width = checked((int)U32(r)); h.Height = checked((int)U32(r));
            int depth = r.ReadByte(), color = r.ReadByte();
            h.OrdinaryMetadata = depth == 8 && (color == 2 || color == 6 || color == 0 || color == 4);
            if (r.ReadByte() != 0 || r.ReadByte() != 0 || r.ReadByte() > 1) throw new InvalidDataException("png-method");
            input.Position += 4;
            // Inspect bounded metadata through the first IDAT; no compressed pixels read.
            int chunks = 0;
            while (input.Position + 12 <= input.Length && ++chunks <= 1024)
            {
                uint length = U32(r); string kind = new string(r.ReadChars(4));
                if (length > input.Length - input.Position - 4) throw new InvalidDataException("png-chunk");
                if (kind == "IDAT") break;
                if (kind != "sRGB" && kind != "pHYs" && kind != "tEXt" && kind != "iTXt" && kind != "zTXt") h.OrdinaryMetadata = false;
                input.Position += length + 4;
            }
        }
        else
        {
            input.Position = 0;
            if (r.ReadByte() != 255 || r.ReadByte() != 216) throw new InvalidDataException("image-kind");
            int segments = 0;
            while (input.Position < input.Length && ++segments <= 1024)
            {
                if (r.ReadByte() != 255) throw new InvalidDataException("jpeg-marker");
                int marker; do { marker = r.ReadByte(); } while (marker == 255);
                if (marker == 0xDA || marker == 0xD9) break;
                int length = U16(r);
                if (length < 2 || input.Position + length - 2 > input.Length) throw new InvalidDataException("jpeg-segment");
                long next = input.Position + length - 2;
                if (marker >= 0xE1 && marker <= 0xEF) h.OrdinaryMetadata = false;
                if (marker == 0xC0 || marker == 0xC2)
                {
                    if (length < 8 || r.ReadByte() != 8) throw new InvalidDataException("jpeg-frame");
                    h.Height = U16(r); h.Width = U16(r);
                    int components = r.ReadByte();
                    if (components != 1 && components != 3) h.OrdinaryMetadata = false;
                }
                input.Position = next;
            }
        }
        if (h.Width < 1 || h.Height < 1 || h.Width > 8192 || h.Height > 8192
            || (long)h.Width * h.Height * 4 > MaximumDecoded) throw new InvalidDataException("decoded-size");
        return h;
    }
    private static int U16(BinaryReader r) => (r.ReadByte() << 8) | r.ReadByte();
    private static bool EncodingIsDds(byte[] b) => b.Length >= 4 && b[0] == 68 && b[1] == 68 && b[2] == 83 && b[3] == 32;
    private static uint U32(BinaryReader r) => ((uint)r.ReadByte() << 24) | ((uint)r.ReadByte() << 16) | ((uint)r.ReadByte() << 8) | r.ReadByte();
}
