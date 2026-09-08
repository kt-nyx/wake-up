// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace RimWorldLoadingOptimizer.RimWorld;

internal sealed class PngCache
{
    internal const int MaximumEntry = 8 * 1024 * 1024;
    internal const long MaximumStore = 512L * 1024 * 1024;
    internal sealed class Entry
    {
        internal int Width, Height, GraphicsFormat, TextureFormat, Mips, Filter, WrapU, WrapV, WrapW, Aniso;
        internal float Bias;
        internal byte[] Pixels = Array.Empty<byte>();
    }
    private readonly string root;
    private long stored;
    internal long Hits, Misses, Writes, Errors, ReadBytes, WrittenBytes;
    internal PngCache(string root)
    {
        this.root = root;
        Directory.CreateDirectory(root);
        stored = Directory.EnumerateFiles(root, "*.bin").Sum(p => new FileInfo(p).Length);
    }
    private static bool nativeHash = Environment.OSVersion.Platform == PlatformID.Win32NT;
    internal static long NativeHashCalls, ManagedHashCalls, HashTicks;
    [DllImport("bcrypt.dll", ExactSpelling = true)]
    private static extern int BCryptHash(IntPtr algorithm, IntPtr secret, int secretLength, [In] byte[] input, int inputLength, [Out] byte[] output, int outputLength);
    internal static byte[] Hash(byte[] bytes)
    {
        long start = Stopwatch.GetTimestamp();
        try
        {
            if (nativeHash)
            {
                try
                {
                    var output = new byte[32];
                    // Windows 10+ SHA-256 algorithm pseudo-handle; no owned handle.
                    if (BCryptHash(new IntPtr(0x41), IntPtr.Zero, 0, bytes, bytes.Length, output, output.Length) == 0)
                    { NativeHashCalls++; return output; }
                    nativeHash = false;
                }
                catch (DllNotFoundException) { nativeHash = false; }
                catch (EntryPointNotFoundException) { nativeHash = false; }
            }
            ManagedHashCalls++; using var sha = SHA256.Create(); return sha.ComputeHash(bytes);
        }
        finally { HashTicks += Stopwatch.GetTimestamp() - start; }
    }
    internal static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "");
    internal static int ExpectedBytes(int width, int height, int format, int mips)
    {
        if (width < 1 || height < 1 || width > 8192 || height > 8192 || mips < 1 || mips > 14) return -1;
        long total = 0;
        for (int i = 0; i < mips; i++)
        {
            total += format == 10 ? (long)((width + 3) / 4) * ((height + 3) / 4) * 8
                : format == 12 ? (long)((width + 3) / 4) * ((height + 3) / 4) * 16
                : format == 1 ? (long)width * height
                : format == 3 ? (long)width * height * 3
                : format == 4 || format == 5 ? (long)width * height * 4 : MaximumEntry + 1;
            if (total > MaximumEntry) return -1;
            if (i + 1 < mips && width == 1 && height == 1) return -1;
            width = Math.Max(1, width / 2); height = Math.Max(1, height / 2);
        }
        return (int)total;
    }
    internal bool CanCapture(int bytes) => bytes > 0 && bytes <= MaximumEntry && stored + bytes + 256 <= MaximumStore;
    internal void Invalidate(string key)
    {
        try
        {
            string file = Path.Combine(root, key + ".bin");
            if (!File.Exists(file)) return;
            long length = new FileInfo(file).Length;
            File.Delete(file); stored = Math.Max(0, stored - length);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
    }
    internal Entry? Read(string key)
    {
        try
        {
            string file = Path.Combine(root, key + ".bin");
            if (!File.Exists(file)) { Misses++; return null; }
            if (new FileInfo(file).Length > MaximumEntry + 256) throw new InvalidDataException("cache-size");
            byte[] bytes = File.ReadAllBytes(file); ReadBytes += bytes.Length;
            if (bytes.Length > MaximumEntry + 256 || bytes.Length < 85) throw new InvalidDataException("cache-size");
            using var input = new MemoryStream(bytes);
            using var reader = new BinaryReader(input);
            if (reader.ReadInt32() != 0x32474E50) throw new InvalidDataException("cache-version");
            byte[] digest = reader.ReadBytes(32);
            byte[] payload = reader.ReadBytes(bytes.Length - 36);
            if (!Hash(payload).SequenceEqual(digest)) throw new InvalidDataException("cache-digest");
            using var data = new BinaryReader(new MemoryStream(payload));
            var entry = new Entry { Width=data.ReadInt32(), Height=data.ReadInt32(), GraphicsFormat=data.ReadInt32(), TextureFormat=data.ReadInt32(),
                Mips=data.ReadInt32(), Filter=data.ReadInt32(), WrapU=data.ReadInt32(), WrapV=data.ReadInt32(), WrapW=data.ReadInt32(), Aniso=data.ReadInt32(), Bias=data.ReadSingle() };
            int length = data.ReadInt32();
            if (length != ExpectedBytes(entry.Width, entry.Height, entry.TextureFormat, entry.Mips) || length < 0
                || data.BaseStream.Length - data.BaseStream.Position != length) throw new InvalidDataException("cache-layout");
            entry.Pixels = data.ReadBytes(length); Hits++; return entry;
        }
        catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException || e is ArgumentException)
        { Errors++; Misses++; Invalidate(key); return null; }
    }
    internal void Write(string key, Entry entry)
    {
        if (entry.Pixels.Length != ExpectedBytes(entry.Width, entry.Height, entry.TextureFormat, entry.Mips)) return;
        string file = Path.Combine(root, key + ".bin"), pending = file + ".pending";
        if (!CanCapture(entry.Pixels.Length) || File.Exists(file)) return;
        try
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                writer.Write(entry.Width); writer.Write(entry.Height); writer.Write(entry.GraphicsFormat); writer.Write(entry.TextureFormat);
                writer.Write(entry.Mips); writer.Write(entry.Filter); writer.Write(entry.WrapU); writer.Write(entry.WrapV); writer.Write(entry.WrapW);
                writer.Write(entry.Aniso); writer.Write(entry.Bias); writer.Write(entry.Pixels.Length); writer.Write(entry.Pixels);
            }
            byte[] payload = stream.ToArray();
            using (var writer = new BinaryWriter(File.Create(pending))) { writer.Write(0x32474E50); writer.Write(Hash(payload)); writer.Write(payload); }
            File.Move(pending, file); stored += payload.Length + 36; Writes++; WrittenBytes += payload.Length + 36;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException) { Errors++; }
    }
}
