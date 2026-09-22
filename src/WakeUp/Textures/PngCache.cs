// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.IO;
using System.Linq;

namespace WakeUp;

internal sealed class PngCache : IDisposable
{
    internal const int MaximumEntry = 8 * 1024 * 1024;
    internal const long MaximumStore = 512L * 1024 * 1024;
    private const int PayloadOverhead = 84;
    internal sealed class Entry
    {
        internal int Width, Height, GraphicsFormat, TextureFormat, Mips, Filter, WrapU, WrapV, WrapW, Aniso;
        internal float Bias;
        internal bool Readable;
        internal byte[] Pixels = Array.Empty<byte>();
    }
    private readonly OwnedCacheStore store;
    private readonly CacheLaunchPolicy policy;
    internal long Hits, Misses, Writes, Errors, ReadBytes, WrittenBytes;
    internal long StoredBytes => store.StoredBytes;
    internal long Pruned => store.Pruned;
    internal long PendingRemoved => store.PendingRemoved;
    internal string LastReason => store.LastReason;
    internal System.Collections.Generic.IReadOnlyDictionary<string, long> Reasons => store.Reasons;
    internal long LegacyRemoved;
    internal PngCache(string root, CacheLaunchPolicy? policy = null, long? maximumStore = null)
    {
        if (maximumStore.HasValue && (maximumStore < 1 || maximumStore > MaximumStore)) throw new ArgumentOutOfRangeException(nameof(maximumStore));
        this.policy = policy ?? CacheLaunchPolicy.Current;
        string storeRoot = Path.Combine(root, "v1");
        long capacity = maximumStore ?? SharedCacheBudget.ForRoot(storeRoot)?.MaximumBytes ?? MaximumStore;
        store = new OwnedCacheStore(storeRoot, MaximumEntry + PayloadOverhead, capacity, this.policy);
        try
        {
            // Explicit one-time rebuild of the old PNG2 flat store. Input keys and
            // PNG2 payloads are unchanged; v1 adds a bound, verified store envelope.
            // Only exact old generated filenames belong to us, never source files.
            if (this.policy.Action != CacheAction.Bypass && Directory.Exists(root))
            {
                int count = 0;
                foreach (string path in Directory.EnumerateFiles(root))
                {
                    if (++count > 32768) throw new IOException("legacy-inventory-limit");
                    string name = Path.GetFileName(path);
                    string key = name.EndsWith(".bin.pending", StringComparison.Ordinal) ? name.Substring(0, name.Length - 12)
                        : name.EndsWith(".bin", StringComparison.Ordinal) ? name.Substring(0, name.Length - 4) : "";
                    if (!OwnedCacheStore.IsKey(key)) continue;
                    OwnedCacheStore.RejectLinkedPath(path);
                    File.Delete(path);
                    LegacyRemoved++;
                }
                store.RefreshSharedInventory();
            }
        }
        catch { store.Dispose(); throw; }
    }
    internal static void Maintain(string saveDataRoot, CacheLaunchPolicy policy)
    {
        if (policy.Action != CacheAction.Clear) return;
        using var cache = new PngCache(Path.Combine(saveDataRoot, "WakeUp", "PngCache"), policy);
        JsonLineLog.WriteEvent(Path.Combine(saveDataRoot, "WakeUp", "cache-maintenance.jsonl"), "clear",
            "\"removed\":" + (cache.Pruned + cache.LegacyRemoved) + ",\"pendingRemoved\":" + cache.PendingRemoved
            + ",\"remainingBytes\":" + cache.StoredBytes + ",\"reason\":\"" + JsonLineLog.Escape(cache.LastReason) + "\"");
        if (cache.StoredBytes != 0) throw new IOException("cache-clear-incomplete");
    }
    internal void Complete() => store.Complete();
    public void Dispose() => store.Dispose();
    // Texture identity work may run while future source leases are held. Never
    // dispatch a CryptoConfig-supplied callback, including on the ordered owner.
    internal static byte[] Hash(byte[] bytes) => CacheDigest.HashInput(bytes);
    internal static long NativeHashCalls => CacheDigest.NativeHashCalls;
    internal static long ManagedHashCalls => CacheDigest.ManagedHashCalls;
    internal static long HashTicks => CacheDigest.HashTicks;
    internal static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "");
    internal static int ExpectedBytes(int width, int height, int format, int mips)
    {
        if (width < 1 || height < 1 || width > 8192 || height > 8192 || mips < 1 || mips > 14)
            return -1;
        long total = 0;
        for (int i = 0; i < mips; i++)
        {
            total += format == 10 ? (long)((width + 3) / 4) * ((height + 3) / 4) * 8
                : format == 12 ? (long)((width + 3) / 4) * ((height + 3) / 4) * 16
                : format == 1 ? (long)width * height
                : format == 3 ? (long)width * height * 3
                : format == 4 || format == 5 ? (long)width * height * 4 : MaximumEntry + 1;
            if (total > MaximumEntry)
                return -1;
            if (i + 1 < mips && width == 1 && height == 1)
                return -1;
            width = Math.Max(1, width / 2);
            height = Math.Max(1, height / 2);
        }
        return (int)total;
    }
    // Both capture routes use this gate before copying any pixels. The full
    // entry and replacement headroom must fit, possibly after bounded eviction.
    internal bool CanCapture(string? key, int bytes) => policy.AllowWrite && key != null && bytes > 0 && bytes <= MaximumEntry
        && store.CanPublish(key, bytes + PayloadOverhead);
    internal bool TryCapture(string? key, int bytes, Action capture)
    {
        if (!CanCapture(key, bytes)) return false;
        capture();
        return true;
    }
    internal void Invalidate(string key) => store.Invalidate(key);
    internal Entry? Read(string key)
    {
        long rejected = store.Rejected;
        var entry = store.Read(key, Decode);
        Errors += store.Rejected - rejected;
        if (entry == null) Misses++; else Hits++;
        return entry;
    }
    private Entry Decode(byte[] bytes)
    {
        ReadBytes += bytes.Length;
        if (bytes.Length > MaximumEntry + 256 || bytes.Length < 85)
            throw new InvalidDataException("cache-size");
        using var input = new MemoryStream(bytes);
        using var reader = new BinaryReader(input);
        if (reader.ReadInt32() != 0x32474E50)
            throw new InvalidDataException("cache-version");
        byte[] digest = reader.ReadBytes(32);
        byte[] payload = reader.ReadBytes(bytes.Length - 36);
        if (!Hash(payload).SequenceEqual(digest))
            throw new InvalidDataException("cache-digest");
        using var data = new BinaryReader(new MemoryStream(payload));
        var entry = new Entry
        {
            Width = data.ReadInt32(),
            Height = data.ReadInt32(),
            GraphicsFormat = data.ReadInt32(),
            TextureFormat = data.ReadInt32(),
            Mips = data.ReadInt32(),
            Filter = data.ReadInt32(),
            WrapU = data.ReadInt32(),
            WrapV = data.ReadInt32(),
            WrapW = data.ReadInt32(),
            Aniso = data.ReadInt32(),
            Bias = data.ReadSingle()
        };
        int length = data.ReadInt32();
        if (length != ExpectedBytes(entry.Width, entry.Height, entry.TextureFormat, entry.Mips) || length < 0
            || data.BaseStream.Length - data.BaseStream.Position != length)
            throw new InvalidDataException("cache-layout");
        entry.Pixels = data.ReadBytes(length);
        return entry;
    }
    internal void Write(string key, Entry entry)
    {
        if (entry.Pixels.Length != ExpectedBytes(entry.Width, entry.Height, entry.TextureFormat, entry.Mips))
            return;
        if (!CanCapture(key, entry.Pixels.Length))
            return;
        try
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                writer.Write(entry.Width);
                writer.Write(entry.Height);
                writer.Write(entry.GraphicsFormat);
                writer.Write(entry.TextureFormat);
                writer.Write(entry.Mips);
                writer.Write(entry.Filter);
                writer.Write(entry.WrapU);
                writer.Write(entry.WrapV);
                writer.Write(entry.WrapW);
                writer.Write(entry.Aniso);
                writer.Write(entry.Bias);
                writer.Write(entry.Pixels.Length);
                writer.Write(entry.Pixels);
            }
            byte[] payload = stream.ToArray();
            using var completed = new MemoryStream();
            using (var writer = new BinaryWriter(completed, System.Text.Encoding.UTF8, true))
            {
                writer.Write(0x32474E50);
                writer.Write(Hash(payload));
                writer.Write(payload);
            }
            long rejected = store.Rejected;
            if (store.Publish(key, completed.ToArray()))
            {
                Writes++;
                WrittenBytes += payload.Length + 36;
            }
            Errors += store.Rejected - rejected;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException) { Errors++; }
    }
}
