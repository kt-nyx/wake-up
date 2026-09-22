// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace WakeUp;

// A complete atlas is one grouped entry: both native images and every ordered
// tile rectangle become visible together. Unity objects never enter this store.
internal sealed class AtlasCacheStore : IDisposable
{
    internal const int MaximumEntry = GroupedTextureBlocks.MaximumEntry;
    internal const int MaximumTiles = 65536;
    private const int HeaderBytes = 84;
    private const int Magic = 0x31415457; // WTA1
    private readonly OwnedCacheStore store;
    private readonly GroupedTextureBlocks grouped;
    private readonly bool compress;
    private readonly Dictionary<string, long> reasons = new(StringComparer.Ordinal);
    internal long Hits, Misses, Writes, Errors, ReadBytes, WrittenBytes;
    internal long StoredBytes => store.StoredBytes;
    internal long Pruned => store.Pruned;
    internal long PendingRemoved => store.PendingRemoved;
    internal string LastReason { get; private set; } = "none";
    internal string FailureDetail => grouped.FailureDetail;
    internal IReadOnlyDictionary<string, long> Reasons => new Dictionary<string, long>(reasons);

    internal sealed class Entry
    {
        internal AtlasPixels.Image Color = null!;
        internal AtlasPixels.Image? Mask;
        // Four values per texture in native request order: x, y, width, height.
        internal float[] Rects = Array.Empty<float>();
        internal int TileCount => Rects.Length / 4;
    }

    internal AtlasCacheStore(string saveDataRoot, CacheLaunchPolicy? policy = null,
        long? maximumStore = null, bool compress = false, bool batched = false)
    {
        if (maximumStore.HasValue && maximumStore.Value < 1) throw new ArgumentOutOfRangeException(nameof(maximumStore));
        string root = Path.Combine(saveDataRoot, "WakeUp", "StaticAtlases", "v1");
        long capacity = maximumStore ?? SharedCacheBudget.ForRoot(root)?.MaximumBytes
            ?? SharedCacheBudget.DefaultMiB * 1024L * 1024;
        this.compress = compress;
        grouped = new GroupedTextureBlocks(Path.Combine(root, "groups"), batched);
        store = new OwnedCacheStore(root, MaximumEntry, capacity, policy ?? CacheLaunchPolicy.Current,
            () => grouped.StoredBytes, () => grouped.Clear(), () => grouped.Initialize());
    }

    private void Note(string reason)
    {
        LastReason = reason;
        reasons.TryGetValue(reason, out long count); reasons[reason] = count + 1;
    }

    internal static bool Valid(Entry entry)
    {
        if (entry == null || !ValidImage(entry.Color) || entry.Rects == null
            || entry.Rects.Length < 4 || entry.Rects.Length % 4 != 0 || entry.TileCount > MaximumTiles) return false;
        if (entry.Mask != null && (!ValidImage(entry.Mask)
            || entry.Mask.Gpu.Width != entry.Color.Gpu.Width || entry.Mask.Gpu.Height != entry.Color.Gpu.Height)) return false;
        for (int i = 0; i < entry.Rects.Length; i += 4)
        {
            float x = entry.Rects[i], y = entry.Rects[i + 1], w = entry.Rects[i + 2], h = entry.Rects[i + 3];
            for (int j = 0; j < 4; j++)
                if (float.IsNaN(entry.Rects[i + j]) || float.IsInfinity(entry.Rects[i + j])) return false;
            if (x < 0 || y < 0 || w <= 0 || h <= 0 || x + (double)w > 1.000001 || y + (double)h > 1.000001) return false;
        }
        return EncodedLength(entry) <= MaximumEntry;
    }

    private static long ImageLength(AtlasPixels.Image image) => 137L + image.Gpu.Pixels.Length + (image.Cpu?.Length ?? 0);
    private static long EncodedLength(Entry entry) => HeaderBytes + ImageLength(entry.Color)
        + (entry.Mask == null ? 0 : ImageLength(entry.Mask)) + entry.Rects.Length * 4L;

    private static bool ValidImage(AtlasPixels.Image image) => image != null && NativeTextureData.Valid(image.Gpu)
        && (image.Cpu == null ? !image.Gpu.Readable : image.Gpu.Readable && image.Cpu.Length == image.Gpu.Pixels.Length)
        && image.MinimumMipmapLevel >= -1 && image.MinimumMipmapLevel <= 31
        && image.RequestedMipmapLevel >= -1 && image.RequestedMipmapLevel <= 31;

    private static byte[] EncodeImage(string identityHash, AtlasPixels.Image image)
    {
        byte[] native = NativeTextureData.Encode(identityHash, image.Gpu);
        using var bytes = new MemoryStream((int)ImageLength(image));
        using var writer = new BinaryWriter(bytes, Encoding.UTF8, true);
        writer.Write(native.Length); writer.Write(image.Cpu?.Length ?? 0);
        writer.Write(image.MinimumMipmapLevel); writer.Write(image.RequestedMipmapLevel);
        writer.Write(native); if (image.Cpu != null) writer.Write(image.Cpu);
        writer.Flush(); return bytes.ToArray();
    }

    private static AtlasPixels.Image DecodeImage(string identityHash, byte[] payload)
    {
        using var reader = new BinaryReader(new MemoryStream(payload), Encoding.UTF8);
        int nativeLength = reader.ReadInt32(), cpuLength = reader.ReadInt32();
        var image = new AtlasPixels.Image { MinimumMipmapLevel = reader.ReadInt32(), RequestedMipmapLevel = reader.ReadInt32() };
        if (nativeLength < 121 || cpuLength < 0 || 16L + nativeLength + cpuLength != payload.Length)
            throw new InvalidDataException("atlas-image-layout");
        image.Gpu = NativeTextureData.Decode(identityHash, reader.ReadBytes(nativeLength));
        if (cpuLength != 0) image.Cpu = reader.ReadBytes(cpuLength);
        if (!ValidImage(image)) throw new InvalidDataException("atlas-image-descriptor");
        return image;
    }

    // Advisory reservation before the main-thread owner captures full images.
    // payloadBytes includes native descriptor and mapping bytes, not only pixels.
    internal bool CanPublish(string identityHash, int payloadBytes, string? ownerKey = null)
    {
        if (!OwnedCacheStore.IsKey(identityHash) || ownerKey != null && !OwnedCacheStore.IsKey(ownerKey)
            || payloadBytes < HeaderBytes || payloadBytes > MaximumEntry)
        { Note("atlas-identity-or-size"); return false; }
        bool allowed = grouped.CanPublish(store, identityHash, payloadBytes, ownerKey);
        if (!allowed) Note(grouped.LastReason);
        return allowed;
    }

    internal Entry? Read(string identityHash, int expectedTileCount = -1)
    {
        if (!OwnedCacheStore.IsKey(identityHash) || expectedTileCount == 0 || expectedTileCount < -1 || expectedTileCount > MaximumTiles)
        { Misses++; Note("atlas-identity-or-tile-count"); return null; }
        try
        {
            byte[]? payload = store.ReadExternal(() => grouped.Read(identityHash));
            if (payload == null) { Misses++; Note(grouped.LastReason == "none" ? "atlas-read-policy-or-missing" : grouped.LastReason); return null; }
            Entry result = Decode(identityHash, payload);
            if (expectedTileCount != -1 && expectedTileCount != result.TileCount) throw new InvalidDataException("atlas-tile-count");
            Hits++; ReadBytes += payload.Length; Note("atlas-hit"); return result;
        }
        catch (Exception e) when (StorageError(e))
        { Errors++; Misses++; Invalidate(identityHash); Note(e is InvalidDataException ? e.Message : "atlas-read-io"); return null; }
    }

    internal bool Publish(string identityHash, Entry entry, string? ownerKey = null, CancellationToken cancellation = default)
    {
        if (cancellation.IsCancellationRequested) { Note("atlas-cancelled"); return false; }
        if (!OwnedCacheStore.IsKey(identityHash) || !Valid(entry)) { Note("atlas-descriptor-or-mapping"); return false; }
        if (!CanPublish(identityHash, (int)EncodedLength(entry), ownerKey)) return false;
        try
        {
            byte[] payload = Encode(identityHash, entry);
            if (!grouped.Publish(store, identityHash, payload, compress, cancellation, ownerKey))
            { Note(grouped.LastReason); return false; }
            Writes++; WrittenBytes += payload.Length; Note("atlas-published"); return true;
        }
        catch (Exception e) when (StorageError(e))
        { Errors++; Note(e is InvalidDataException ? e.Message : "atlas-write-io"); return false; }
    }

    internal void Invalidate(string identityHash)
    {
        if (OwnedCacheStore.IsKey(identityHash)) grouped.Invalidate(store, identityHash);
    }

    internal static byte[] Encode(string identityHash, Entry entry)
    {
        if (!OwnedCacheStore.IsKey(identityHash) || !Valid(entry)) throw new InvalidDataException("atlas-descriptor-or-mapping");
        byte[] color = EncodeImage(identityHash, entry.Color);
        byte[]? mask = entry.Mask == null ? null : EncodeImage(identityHash, entry.Mask);
        using var bytes = new MemoryStream((int)EncodedLength(entry));
        using var writer = new BinaryWriter(bytes, Encoding.UTF8, true);
        writer.Write(Magic); writer.Write(1); writer.Write(Encoding.ASCII.GetBytes(identityHash));
        writer.Write(entry.TileCount); writer.Write(color.Length); writer.Write(mask?.Length ?? 0);
        writer.Write(color); if (mask != null) writer.Write(mask);
        foreach (float value in entry.Rects) writer.Write(value);
        writer.Flush();
        if (bytes.Length != EncodedLength(entry)) throw new InvalidDataException("atlas-size");
        return bytes.ToArray();
    }

    internal static Entry Decode(string identityHash, byte[] payload)
    {
        if (!OwnedCacheStore.IsKey(identityHash) || payload == null || payload.Length < HeaderBytes || payload.Length > MaximumEntry)
            throw new InvalidDataException("atlas-size-or-identity");
        using var reader = new BinaryReader(new MemoryStream(payload), Encoding.UTF8);
        if (reader.ReadInt32() != Magic || reader.ReadInt32() != 1
            || !reader.ReadBytes(64).SequenceEqual(Encoding.ASCII.GetBytes(identityHash))) throw new InvalidDataException("atlas-version-or-identity");
        int count = reader.ReadInt32(), colorLength = reader.ReadInt32(), maskLength = reader.ReadInt32();
        if (count < 1 || count > MaximumTiles || colorLength < 137 || maskLength < 0 || maskLength > 0 && maskLength < 137
            || HeaderBytes + (long)colorLength + maskLength + count * 16L != payload.Length) throw new InvalidDataException("atlas-layout");
        var entry = new Entry { Color = DecodeImage(identityHash, reader.ReadBytes(colorLength)) };
        if (maskLength != 0) entry.Mask = DecodeImage(identityHash, reader.ReadBytes(maskLength));
        entry.Rects = new float[count * 4];
        for (int i = 0; i < entry.Rects.Length; i++) entry.Rects[i] = reader.ReadSingle();
        if (!Valid(entry)) throw new InvalidDataException("atlas-descriptor-or-mapping");
        return entry;
    }

    private static bool StorageError(Exception e) => e is IOException || e is InvalidDataException || e is UnauthorizedAccessException
        || e is ArgumentException || e is OverflowException;

    internal static void Maintain(string saveDataRoot, CacheLaunchPolicy policy)
    {
        if (policy.Action != CacheAction.Clear) return;
        using var cache = new AtlasCacheStore(saveDataRoot, policy);
        if (cache.StoredBytes != 0) throw new IOException("atlas-clear-incomplete");
    }

    internal void Complete() { grouped.Complete(store); store.Complete(); }
    public void Dispose() => store.Dispose();
}
