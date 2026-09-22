// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace WakeUp;

// The same native representation is used by automatic capture and preparation.
// Compression of its stored bytes is independent of this descriptor and identity.
internal static class NativeTextureData
{
    internal const int MaximumPixels = 96 * 1024 * 1024 - 32768;
    internal const int MaximumBasePixels = 64 * 1024 * 1024;
    internal static int ExpectedBytes(int width, int height, int format, int mips)
    {
        if (width < 1 || height < 1 || width > 8192 || height > 8192 || mips < 1 || mips > 14) return -1;
        long total = 0;
        for (int mip = 0; mip < mips; mip++)
        {
            long size = format == 10 ? (long)((width + 3) / 4) * ((height + 3) / 4) * 8
                : format == 12 || format == 25 ? (long)((width + 3) / 4) * ((height + 3) / 4) * 16
                : format == 1 ? (long)width * height
                : format == 2 || format == 7 || format == 13 ? (long)width * height * 2
                : format == 3 ? (long)width * height * 3
                : format == 4 || format == 5 || format == 14 ? (long)width * height * 4 : -1;
            if (size < 1 || (total += size) > MaximumPixels) return -1;
            if (mip + 1 < mips && width == 1 && height == 1) return -1;
            width = Math.Max(1, width / 2); height = Math.Max(1, height / 2);
        }
        return (int)total;
    }

    internal static bool Valid(PngCache.Entry e) => e != null && e.Pixels != null
        && e.Pixels.Length > 0 && e.Pixels.Length == ExpectedBytes(e.Width, e.Height, e.TextureFormat, e.Mips)
        && ValidDescriptor(e);

    private static bool ValidDescriptor(PngCache.Entry e) =>
        // GOG Unity returns legacy Alpha8 (54) and ARGB32 sRGB (88) although the public
        // GraphicsFormat enum omits it. Exact restoration checks it again.
        e.GraphicsFormat > 0 && (Enum.IsDefined(typeof(UnityEngine.Experimental.Rendering.GraphicsFormat), e.GraphicsFormat)
            || e.TextureFormat == 5 && e.GraphicsFormat == 88 || e.TextureFormat == 1 && e.GraphicsFormat == 54)
        && e.Filter >= 0 && e.Filter <= 2 && e.WrapU >= 0 && e.WrapU <= 3
        && e.WrapV >= 0 && e.WrapV <= 3 && e.WrapW >= 0 && e.WrapW <= 3
        && e.Aniso >= 0 && e.Aniso <= 16 && !float.IsNaN(e.Bias) && !float.IsInfinity(e.Bias);

    internal static byte[] Encode(string identity, PngCache.Entry e)
    {
        if (!Valid(e)) throw new InvalidDataException("native-texture-layout");
        byte[] name = Encoding.UTF8.GetBytes(identity);
        int size = checked(57 + name.Length + e.Pixels.Length);
        using var bytes = new MemoryStream(size);
        using var w = new BinaryWriter(bytes, Encoding.UTF8, true);
        w.Write(0x32544E57); w.Write(name.Length); w.Write(name);
        w.Write(e.Width); w.Write(e.Height); w.Write(e.GraphicsFormat); w.Write(e.TextureFormat); w.Write(e.Mips);
        w.Write(e.Filter); w.Write(e.WrapU); w.Write(e.WrapV); w.Write(e.WrapW); w.Write(e.Aniso); w.Write(e.Bias);
        w.Write(e.Readable); w.Write(e.Pixels.Length); w.Write(e.Pixels);
        w.Flush(); if (bytes.Length != size) throw new InvalidDataException("native-texture-size"); return bytes.GetBuffer();
    }

    internal static PngCache.Entry Decode(string identity, byte[] bytes)
    {
        if (bytes.Length > MaximumPixels + 32768) throw new InvalidDataException("native-texture-size");
        using var r = new BinaryReader(new MemoryStream(bytes), Encoding.UTF8);
        if (r.ReadInt32() != 0x32544E57) throw new InvalidDataException("native-texture-identity");
        byte[] name = Encoding.UTF8.GetBytes(identity);
        if (r.ReadInt32() != name.Length || !r.ReadBytes(name.Length).SequenceEqual(name)) throw new InvalidDataException("native-texture-identity");
        var e = new PngCache.Entry { Width = r.ReadInt32(), Height = r.ReadInt32(), GraphicsFormat = r.ReadInt32(),
            TextureFormat = r.ReadInt32(), Mips = r.ReadInt32(), Filter = r.ReadInt32(), WrapU = r.ReadInt32(),
            WrapV = r.ReadInt32(), WrapW = r.ReadInt32(), Aniso = r.ReadInt32(), Bias = r.ReadSingle(), Readable = r.ReadBoolean() };
        int length = r.ReadInt32();
        if (length < 1 || length != ExpectedBytes(e.Width, e.Height, e.TextureFormat, e.Mips)
            || r.BaseStream.Length - r.BaseStream.Position != length) throw new InvalidDataException("native-texture-layout");
        e.Pixels = r.ReadBytes(length);
        if (!Valid(e)) throw new InvalidDataException("native-texture-descriptor");
        return e;
    }

    internal sealed class Descriptor
    {
        internal readonly int Width, Height, GraphicsFormat, TextureFormat, Mips, Filter, WrapU, WrapV, WrapW, Aniso;
        internal readonly float Bias;
        internal readonly bool Readable;
        internal Descriptor(PngCache.Entry entry)
        {
            Width = entry.Width; Height = entry.Height; GraphicsFormat = entry.GraphicsFormat;
            TextureFormat = entry.TextureFormat; Mips = entry.Mips; Filter = entry.Filter;
            WrapU = entry.WrapU; WrapV = entry.WrapV; WrapW = entry.WrapW; Aniso = entry.Aniso;
            Bias = entry.Bias; Readable = entry.Readable;
        }
        internal PngCache.Entry ToEntry() => new()
        {
            Width = Width, Height = Height, GraphicsFormat = GraphicsFormat, TextureFormat = TextureFormat,
            Mips = Mips, Filter = Filter, WrapU = WrapU, WrapV = WrapV, WrapW = WrapW, Aniso = Aniso,
            Bias = Bias, Readable = Readable
        };
    }

    // The grouped reader has already authenticated the complete private record.
    // Ownership transfers here: callers must not retain or mutate the input
    // array. Only the validated pixel range is lent to a synchronous uploader;
    // no duplicate pixel array, retained native pointer or retained pin is made.
    internal sealed class OwnedRecord
    {
        private readonly byte[] bytes;
        internal Descriptor Descriptor { get; }
        internal int RecordBytes => bytes.Length;
        internal int PixelOffset { get; }
        internal int PixelBytes { get; }
        private OwnedRecord(byte[] bytes, Descriptor descriptor, int pixelOffset, int pixelBytes)
        { this.bytes = bytes; Descriptor = descriptor; PixelOffset = pixelOffset; PixelBytes = pixelBytes; }

        // The callback must consume the pointer before returning and must not
        // retain it. The pin is local even when upload throws or re-enters.
        internal void WithPinnedPixels(Action<IntPtr, int> upload)
        {
            if (upload == null) throw new ArgumentNullException(nameof(upload));
            var pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try { upload(IntPtr.Add(pin.AddrOfPinnedObject(), PixelOffset), PixelBytes); }
            finally { pin.Free(); }
        }

        internal static OwnedRecord Decode(string identity, byte[] authenticatedRecord)
        {
            if (authenticatedRecord.Length > MaximumPixels + 32768) throw new InvalidDataException("native-texture-size");
            using var reader = new BinaryReader(new MemoryStream(authenticatedRecord, false), Encoding.UTF8);
            if (reader.ReadInt32() != 0x32544E57) throw new InvalidDataException("native-texture-identity");
            byte[] name = Encoding.UTF8.GetBytes(identity);
            if (reader.ReadInt32() != name.Length || !reader.ReadBytes(name.Length).SequenceEqual(name))
                throw new InvalidDataException("native-texture-identity");
            var entry = new PngCache.Entry { Width = reader.ReadInt32(), Height = reader.ReadInt32(), GraphicsFormat = reader.ReadInt32(),
                TextureFormat = reader.ReadInt32(), Mips = reader.ReadInt32(), Filter = reader.ReadInt32(), WrapU = reader.ReadInt32(),
                WrapV = reader.ReadInt32(), WrapW = reader.ReadInt32(), Aniso = reader.ReadInt32(), Bias = reader.ReadSingle(), Readable = reader.ReadBoolean() };
            int length = reader.ReadInt32();
            int offset = checked((int)reader.BaseStream.Position);
            if (length < 1 || length != ExpectedBytes(entry.Width, entry.Height, entry.TextureFormat, entry.Mips)
                || reader.BaseStream.Length - offset != length) throw new InvalidDataException("native-texture-layout");
            if (!ValidDescriptor(entry)) throw new InvalidDataException("native-texture-descriptor");
            return new OwnedRecord(authenticatedRecord, new Descriptor(entry), offset, length);
        }
    }

    internal static OwnedRecord DecodeOwned(string identity, byte[] authenticatedRecord) => OwnedRecord.Decode(identity, authenticatedRecord);
}
