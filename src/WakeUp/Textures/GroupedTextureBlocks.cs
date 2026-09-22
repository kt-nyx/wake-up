// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace WakeUp;

// Companion of PreparedTextureStore's OwnedCacheStore, under that owner's lock.
// An atomically replaced index commits immutable block ranges. Each <=1 MiB block
// carries its own identity and digest; reading one texture never decodes its
// neighbours. Deflate changes disk bytes only, never the consumer payload.
internal sealed class GroupedTextureBlocks : IDisposable
{
    internal const int MaximumEntry = 96 * 1024 * 1024;
    internal const int BlockBytes = 1024 * 1024;
    internal const int GroupBytes = 16 * 1024 * 1024;
    private const int MaximumIndex = 16 * 1024 * 1024;
    private const int MaximumEntries = 32768;
    private const int InventoryLimit = 65536;
    private const int IndexMagic = 0x31495457; // WTI1
    private const int BlockMagic = 0x31425457; // WTB1
    private const int BlockHeader = 113;
    private const int BlockIndex = 77;
    private const int EntryIndex = 176;
    private const int MaximumDeltas = 128;
    private const int MaximumDeltaBytes = 16384;
    private const int DeltaMagic = 0x31445457;
    internal const long MaximumBatchBytes = 64L * 1024 * 1024;
    private readonly string root;
    private readonly bool batched;
    // Startup may borrow a small number of group streams under the category's
    // exclusive owner. Manual preparation, previews and atlas callers retain
    // per-read closure. No decoded payload or validation result is cached.
    internal const int MaximumReadHandles = 8;
    private readonly bool retainReadHandles;
    private readonly Dictionary<string, ReadHandle> readHandles = new(StringComparer.Ordinal);
    private long readSequence;
    internal int OpenReadHandleCount => readHandles.Count;
    private sealed class ReadHandle
    {
        internal FileStream Stream = null!;
        internal long Used;
    }
    private readonly struct ReadLease : IDisposable
    {
        internal readonly FileStream Stream;
        private readonly bool close;
        internal ReadLease(FileStream stream, bool close) { Stream = stream; this.close = close; }
        public void Dispose() { if (close) Stream.Dispose(); }
    }

    // One worker exclusively owns this context. The ordered caller may clear or
    // dispose it only after that worker's jobs have drained. Handles preserve
    // their independent positions, never payloads or successful validation.
    internal sealed class ReadContext : IDisposable
    {
        internal const int MaximumHandles = 8;
        internal const int ReservedBytes = 128 * 1024;
        private readonly Dictionary<string, ReadHandle> handles = new(StringComparer.Ordinal);
        private long sequence;
        private bool disposed;
        internal int OpenHandleCount => handles.Count;

        internal FileStream Acquire(string path)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ReadContext));
            if (handles.TryGetValue(path, out var existing))
            { existing.Used = ++sequence; return existing.Stream; }
            OwnedCacheStore.RejectLinkedPath(path);
            if (handles.Count == MaximumHandles)
            {
                var oldest = handles.OrderBy(p => p.Value.Used).First();
                oldest.Value.Stream.Dispose(); handles.Remove(oldest.Key);
            }
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096);
            handles.Add(path, new ReadHandle { Stream = stream, Used = ++sequence });
            return stream;
        }

        internal void Clear()
        {
            foreach (var handle in handles.Values) handle.Stream.Dispose();
            handles.Clear(); sequence = 0;
        }
        public void Dispose()
        {
            if (disposed) return;
            Clear(); disposed = true;
        }
    }
    private Dictionary<string, Entry>? batchPrevious;
    private HashSet<string>? batchPreviousUsed;
    private string batchPreviousTail = "";
    private readonly Dictionary<string, long> batchOriginalLengths = new(StringComparer.Ordinal);
    private readonly HashSet<string> batchTouched = new(StringComparer.Ordinal);
    private int batchCount;
    private long batchBytes;
    private bool batchStopped;
    private readonly Dictionary<string, long> files = new(StringComparer.Ordinal);
    private Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> owners = new(StringComparer.Ordinal);
    private readonly HashSet<string> deltas = new(StringComparer.Ordinal);
    private byte[] indexDigest = new byte[32];
    private long indexBytes = 92, sequence;
    private readonly HashSet<string> used = new(StringComparer.Ordinal);
    private long stored;
    private bool accountingFailed;
    private bool completed;
    private string tail = "";
    internal long StoredBytes => accountingFailed ? long.MaxValue / 4 : stored;
    internal string LastReason { get; private set; } = "none";
    internal string FailureDetail { get; private set; } = "";
    internal bool HasOwner(string owner) => OwnedCacheStore.IsKey(owner) && owners.ContainsKey(owner);
    // A self-owned slot is an explicit user selection, not a reproducible
    // automatic cache entry. C08 has always written this identity convention,
    // so existing selections acquire protection without rewriting their index.
    internal int SelectedQualityCount => entries.Count(p => IsExplicitSelection(p.Key, p.Value));
    internal long SelectedQualityRecordBytes => entries.Where(p => IsExplicitSelection(p.Key, p.Value)).Sum(p => (long)p.Value.Length);
    private static bool IsExplicitSelection(string key, Entry entry) => entry.Owner == key;

    internal static long ReadTicks, ReadAcquireTicks, ReadDecodeTicks, ReadHashTicks, ReadBlockCount;
    internal static long PublishTicks, CompressionTicks, DataFlushTicks, DeltaTicks, CheckpointTicks;

    private sealed class Entry
    {
        internal int Length;
        internal byte[] Digest = Array.Empty<byte>();
        internal string Owner = "";
        internal long UsedTicks;
        internal List<Block> Blocks = new();
    }
    private sealed class Block
    {
        internal string Group = "";
        internal int Offset, Stored, Raw;
        internal bool Compressed;
        internal byte[] Digest = Array.Empty<byte>();
    }

    // Captured only under OwnedCacheStore.ReadExternal. Each read owns its file
    // position and private decoded bytes; no stream or owner state is shared.
    internal sealed class ReadSnapshot
    {
        private readonly string root;
        private readonly Entry entry;
        internal string Key { get; }
        internal string Owner => entry.Owner;
        internal int RecordBytes => entry.Length;
        internal int MaximumScratchBytes => entry.Blocks.Where(b => b.Compressed).Select(b => b.Stored).DefaultIfEmpty(0).Max();
        private ReadSnapshot(string root, string key, Entry source)
        {
            this.root = root; Key = key;
            entry = new Entry { Length = source.Length, Owner = source.Owner, Digest = (byte[])source.Digest.Clone(),
                Blocks = source.Blocks.Select(b => new Block { Group = b.Group, Offset = b.Offset, Stored = b.Stored,
                    Raw = b.Raw, Compressed = b.Compressed, Digest = (byte[])b.Digest.Clone() }).ToList() };
        }

        internal byte[] Read(CancellationToken cancellation, Action checkLifetime, ReadContext? context = null)
        {
            cancellation.ThrowIfCancellationRequested(); checkLifetime();
            byte[] result = new byte[entry.Length];
            int output = 0;
            byte[]? singleBlockDigest = null;
            for (int ordinal = 0; ordinal < entry.Blocks.Count; ordinal++)
            {
                cancellation.ThrowIfCancellationRequested(); checkLifetime();
                Block block = entry.Blocks[ordinal];
                string path = Path.Combine(root, block.Group + ".group");
                if (context == null) OwnedCacheStore.RejectLinkedPath(path);
                using var lease = context == null
                    ? new ReadLease(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), true)
                    : new ReadLease(context.Acquire(path), false);
                var file = lease.Stream;
                if (file.Length > GroupBytes || block.Offset < 0 || (long)block.Offset + BlockHeader + block.Stored > file.Length)
                    throw new InvalidDataException("group-block-bounds");
                file.Position = block.Offset;
                using var reader = new BinaryReader(file, Encoding.UTF8, true);
                if (reader.ReadInt32() != BlockMagic || Encoding.ASCII.GetString(reader.ReadBytes(64)) != Key
                    || reader.ReadInt32() != ordinal || reader.ReadInt32() != block.Raw || reader.ReadInt32() != block.Stored
                    || reader.ReadByte() != (block.Compressed ? 1 : 0) || !reader.ReadBytes(32).SequenceEqual(block.Digest))
                    throw new InvalidDataException("group-block-identity");
                if (block.Compressed)
                {
                    byte[] encoded = reader.ReadBytes(block.Stored);
                    if (encoded.Length != block.Stored) throw new InvalidDataException("group-block-truncated");
                    using var input = new MemoryStream(encoded, false);
                    using var inflater = new DeflateStream(input, CompressionMode.Decompress);
                    ReadExact(inflater, result, output, block.Raw);
                    if (inflater.ReadByte() != -1) throw new InvalidDataException("group-block-expansion");
                }
                else ReadExact(file, result, output, block.Raw);
                byte[] digest = CacheDigest.HashInput(result, output, block.Raw);
                if (!digest.SequenceEqual(block.Digest)) throw new InvalidDataException("group-block-digest");
                if (entry.Blocks.Count == 1) singleBlockDigest = digest;
                output += block.Raw;
            }
            cancellation.ThrowIfCancellationRequested(); checkLifetime();
            if (output != result.Length || !(singleBlockDigest ?? CacheDigest.HashInput(result)).SequenceEqual(entry.Digest))
                throw new InvalidDataException("group-entry-digest");
            return result;
        }

        internal static ReadSnapshot Capture(GroupedTextureBlocks source, string key)
            => new(source.root, key, source.entries[key]);
    }

    internal ReadSnapshot? PlanNative(string owner)
    {
        if (!OwnedCacheStore.IsKey(owner) || !owners.TryGetValue(owner, out var keys) || keys.Count != 1) return null;
        string key = keys.Single();
        // Explicit quality selections use their own key as owner and must retain
        // their existing serial quality policy rather than enter native lookahead.
        return key == owner ? null : ReadSnapshot.Capture(this, key);
    }

    internal bool CompleteRead(ReadSnapshot snapshot)
    {
        if (!entries.TryGetValue(snapshot.Key, out var entry) || entry.Owner != snapshot.Owner) return false;
        used.Add(snapshot.Key); entry.UsedTicks = DateTime.UtcNow.Ticks; LastReason = "group-hit";
        return true;
    }

    internal GroupedTextureBlocks(string root, bool batched = false, bool retainReadHandles = false)
    { this.root = Path.GetFullPath(root); this.batched = batched; this.retainReadHandles = retainReadHandles; }

    private ReadLease AcquireRead(string group)
    {
        if (retainReadHandles && readHandles.TryGetValue(group, out var existing))
        { existing.Used = ++readSequence; return new ReadLease(existing.Stream, false); }
        string path = Path.Combine(root, group + ".group");
        OwnedCacheStore.RejectLinkedPath(path);
        if (retainReadHandles && readHandles.Count == MaximumReadHandles)
        {
            var oldest = readHandles.OrderBy(p => p.Value.Used).First();
            oldest.Value.Stream.Dispose(); readHandles.Remove(oldest.Key);
        }
        // On the qualified Windows platform this also prevents replacement or
        // writes while borrowed. Every read still checks bounds and all digests.
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (retainReadHandles) readHandles.Add(group, new ReadHandle { Stream = stream, Used = ++readSequence });
        return new ReadLease(stream, !retainReadHandles);
    }

    private void CloseReads()
    {
        foreach (var handle in readHandles.Values) handle.Stream.Dispose();
        readHandles.Clear();
    }
    public void Dispose() => CloseReads();

    internal void Initialize() => InitializeCore(false);
    // The preview borrows existing ownership and only observes committed data.
    // Crash leftovers remain untouched until an ordinary writer owns the store.
    internal void InitializeReadOnly() => InitializeCore(true);
    private void InitializeCore(bool readOnly)
    {
        CloseReads();
        OwnedCacheStore.RejectLinkedPath(root);
        if (!Directory.Exists(root)) return;
        foreach (string path in Directory.EnumerateFileSystemEntries(root))
        {
            if (files.Count >= InventoryLimit) throw new IOException("group-inventory-limit");
            OwnedCacheStore.RejectLinkedPath(path);
            if (Directory.Exists(path)) throw new IOException("group-unexpected-directory");
            Track(Path.GetFileName(path));
        }
        if (!readOnly) foreach (string name in files.Keys.Where(IsPending).ToArray()) Remove(name);
        if (files.ContainsKey("index"))
        {
            try
            {
                byte[] bytes = ReadFile(Path.Combine(root, "index"), MaximumIndex);
                entries = DecodeIndex(bytes, out tail);
                indexDigest = bytes.Skip(bytes.Length - 32).ToArray();
            }
            catch (Exception e) when (IsStorageError(e))
            {
                if (readOnly) throw;
                LastReason = e is InvalidDataException ? e.Message : "group-index-io";
                entries.Clear(); tail = ""; Remove("index");
            }
        }
        RebuildLookup();
        foreach (string name in files.Keys.Where(IsDelta).OrderBy(n => n, StringComparer.Ordinal).ToArray())
        {
            try
            {
                if (deltas.Count >= MaximumDeltas) throw new InvalidDataException("group-delta-count");
                byte[] bytes = ReadFile(Path.Combine(root, name), MaximumDeltaBytes);
                if (bytes.Length < 160 || bytes.Length > MaximumDeltaBytes
                    || !Hash(bytes, 0, bytes.Length - 32).SequenceEqual(bytes.Skip(bytes.Length - 32)))
                    throw new InvalidDataException("group-delta-digest");
                using var reader = new BinaryReader(new MemoryStream(bytes, false));
                if (reader.ReadInt32() != DeltaMagic || !reader.ReadBytes(32).SequenceEqual(indexDigest))
                    throw new InvalidDataException("group-delta-generation");
                long serial = reader.ReadInt64();
                if (serial <= sequence || name.Substring(0, 16) != serial.ToString("x16"))
                    throw new InvalidDataException("group-delta-sequence");
                var update = DecodeIndex(reader.ReadBytes(bytes.Length - 76), out string nextTail);
                if (update.Count != 1) throw new InvalidDataException("group-delta-entry");
                var item = update.Single();
                if (Reservation(item.Key, item.Value.Length, item.Value.Owner.Length == 0 ? null : item.Value.Owner) < 0)
                    throw new InvalidDataException("group-delta-index-limit");
                ApplyEntry(item.Key, item.Value); tail = nextTail;
                deltas.Add(name); sequence = serial;
            }
            catch (Exception e) when (IsStorageError(e))
            { LastReason = "group-delta-refused"; if (!readOnly) Remove(name); }
        }
        if (readOnly) return;
        CollectOrphans();
        // A crash may leave an appended suffix that never reached the index.
        // Only bytes beyond every committed range are eligible for truncation.
        foreach (var group in entries.Values.SelectMany(e => e.Blocks).GroupBy(b => b.Group))
            TrimSuffix(group.Key + ".group", group.Max(b => (long)b.Offset + BlockHeader + b.Stored));
    }

    internal void Clear()
    {
        CloseReads();
        ResetBatch(); batchStopped = false;
        entries.Clear(); owners.Clear(); deltas.Clear(); used.Clear(); tail = "";
        indexDigest = new byte[32]; indexBytes = 92; sequence = 0;
        foreach (string name in files.Keys.Where(n => n == "index" || IsGroup(n) || IsPending(n) || IsDelta(n)).ToArray()) Remove(name);
    }

    internal byte[]? Read(string key)
    {
        if (!OwnedCacheStore.IsKey(key) || !entries.TryGetValue(key, out Entry? entry)) { LastReason = "group-missing"; return null; }
        long started = Stopwatch.GetTimestamp();
        try
        {
            byte[] result = new byte[entry.Length];
            int output = 0;
            byte[]? singleBlockDigest = null;
            for (int ordinal = 0; ordinal < entry.Blocks.Count; ordinal++)
            {
                Block block = entry.Blocks[ordinal];
                long acquireStarted = Stopwatch.GetTimestamp();
                using var lease = AcquireRead(block.Group);
                var file = lease.Stream;
                ReadAcquireTicks += Stopwatch.GetTimestamp() - acquireStarted; ReadBlockCount++;
                if (file.Length > GroupBytes || block.Offset < 0 || (long)block.Offset + BlockHeader + block.Stored > file.Length)
                    throw new InvalidDataException("group-block-bounds");
                file.Position = block.Offset;
                using var reader = new BinaryReader(file, Encoding.UTF8, true);
                if (reader.ReadInt32() != BlockMagic || Encoding.ASCII.GetString(reader.ReadBytes(64)) != key
                    || reader.ReadInt32() != ordinal || reader.ReadInt32() != block.Raw || reader.ReadInt32() != block.Stored
                    || reader.ReadByte() != (block.Compressed ? 1 : 0) || !reader.ReadBytes(32).SequenceEqual(block.Digest))
                    throw new InvalidDataException("group-block-identity");
                if (block.Compressed)
                {
                    long decodeStarted = Stopwatch.GetTimestamp();
                    byte[] encoded = reader.ReadBytes(block.Stored);
                    if (encoded.Length != block.Stored) throw new InvalidDataException("group-block-truncated");
                    using var input = new MemoryStream(encoded, false);
                    using var inflater = new DeflateStream(input, CompressionMode.Decompress);
                    ReadExact(inflater, result, output, block.Raw);
                    if (inflater.ReadByte() != -1) throw new InvalidDataException("group-block-expansion");
                    ReadDecodeTicks += Stopwatch.GetTimestamp() - decodeStarted;
                }
                else ReadExact(file, result, output, block.Raw);
                byte[] digest = ReadHash(result, output, block.Raw);
                if (!digest.SequenceEqual(block.Digest)) throw new InvalidDataException("group-block-digest");
                // A one-block entry covers these exact same bytes. Compare the
                // freshly computed digest against both expected values without
                // hashing the payload twice; multi-block entries still need the
                // separate whole-entry digest below.
                if (entry.Blocks.Count == 1) singleBlockDigest = digest;
                output += block.Raw;
            }
            if (output != result.Length || !(singleBlockDigest ?? ReadHash(result, 0, result.Length)).SequenceEqual(entry.Digest)) throw new InvalidDataException("group-entry-digest");
            used.Add(key); entry.UsedTicks = DateTime.UtcNow.Ticks;
            LastReason = "group-hit";
            return result;
        }
        catch (Exception e) when (IsStorageError(e)) { CloseReads(); LastReason = e is InvalidDataException ? e.Message : "group-read-io"; return null; }
        finally { ReadTicks += Stopwatch.GetTimestamp() - started; }
    }

    // Admission is advisory. Publish repeats it under the owning store's lock.
    internal bool CanPublish(OwnedCacheStore owner, string key, int length, string? ownerKey = null)
    {
        CloseReads();
        if (!ValidEntry(key, length) || ownerKey != null && !OwnedCacheStore.IsKey(ownerKey)) { LastReason = "group-entry-size-or-key"; return false; }
        if (batchStopped) { LastReason = "group-batch-stopped"; return false; }
        if (batched && batchCount != 0 && (batchCount >= MaximumDeltas || NewGroupBytes(length) > MaximumBatchBytes - batchBytes)
            && !RewriteIndex(owner, entries, "group-batch-committed")) return false;
        if (deltas.Count >= MaximumDeltas && !RewriteIndex(owner, entries, "group-checkpoint")) return false;
        long reservation = Reservation(key, length, ownerKey);
        if (reservation < 0) { LastReason = "group-index-limit"; return false; }
        bool allowed = owner.CanPublishExternal(reservation);
        if (!allowed && owner.LastReason == "quota" && PruneUnused(owner, key, ownerKey, null))
        {
            reservation = Reservation(key, length, ownerKey);
            allowed = reservation >= 0 && owner.CanPublishExternal(reservation);
        }
        if (!allowed) LastReason = owner.LastReason == "quota" && SelectedQualityCount != 0
            ? "group-quota-explicit-selections-retained" : "group-" + owner.LastReason;
        return allowed;
    }

    // In opt-in automatic batching, true means a complete, readable staged
    // entry. Durability arrives at a threshold or Complete, not per image.
    internal bool Publish(OwnedCacheStore owner, string key, byte[] payload, bool compress = false, CancellationToken cancellation = default, string? ownerKey = null)
    {
        if (payload == null || !ValidEntry(key, payload.Length) || ownerKey != null && !OwnedCacheStore.IsKey(ownerKey))
        { LastReason = "group-entry-size-or-key"; return false; }
        if (cancellation.IsCancellationRequested) { LastReason = "group-cancelled"; return false; }
        if (!CanPublish(owner, key, payload.Length, ownerKey)) return false;
        long reservation = Reservation(key, payload.Length, ownerKey);
        long started = Stopwatch.GetTimestamp();
        try
        {
            bool stage = batched && NewGroupBytes(payload.Length) <= MaximumBatchBytes;
            bool result = owner.PublishExternal(reservation, () => PublishCore(key, payload, compress, cancellation, ownerKey, stage));
            if (!result && owner.LastReason != "external-refused") LastReason = "group-" + owner.LastReason;
            if (result && stage && (batchCount >= MaximumDeltas || batchBytes >= MaximumBatchBytes))
                result = RewriteIndex(owner, entries, "group-batch-committed");
            return result;
        }
        catch (Exception e) when (IsStorageError(e)) { FailureDetail = e.ToString(); LastReason = e is InvalidDataException ? e.Message : "group-write-io"; return false; }
        finally { PublishTicks += Stopwatch.GetTimestamp() - started; }
    }

    // Explicit quality groups have one publication point. Automatic per-image
    // batching cannot provide this because thresholds can commit midway through
    // a color/mask replacement. Reserve the complete replacement before writing.
    internal bool PublishAtomic(OwnedCacheStore owner, IReadOnlyList<(string Key, byte[] Payload, string Owner)> items,
        bool compress, CancellationToken cancellation)
    {
        CloseReads();
        if (items == null || items.Count == 0 || items.Count > 512 || cancellation.IsCancellationRequested)
        { LastReason="quality-group-empty-bound-or-cancelled";return false; }
        var keys=new HashSet<string>(StringComparer.Ordinal);
        long raw=0, disk=0, index=IndexSize(entries);
        foreach(var item in items)
        {
            if(item.Payload==null || !ValidEntry(item.Key,item.Payload.Length) || !OwnedCacheStore.IsKey(item.Owner)
                || !keys.Add(item.Key) || (raw+=item.Payload.Length)>MaximumEntry)
            { LastReason="quality-group-bound-or-key";return false; }
            disk+=NewGroupBytes(item.Payload.Length);
            index+=EntryIndex+((item.Payload.Length+BlockBytes-1)/BlockBytes)*(long)BlockIndex;
        }
        if(index>MaximumIndex || entries.Count+items.Count>MaximumEntries)
        {LastReason="quality-group-index-bound";return false;}
        if(batchPrevious!=null && !RewriteIndex(owner,entries,"group-before-quality-commit")) return false;
        try
        {
            bool result=owner.PublishExternal(disk+index,()=>
            {
                BeginBatch();
                bool committed=false;
                try
                {
                    foreach(var item in items)
                        if(!PublishCore(item.Key,item.Payload,compress,cancellation,item.Owner,true)) return false;
                    cancellation.ThrowIfCancellationRequested();
                    CommitIndex(entries,tail,cancellation);
                    committed=true;RebuildLookup();CollectOrphans();LastReason="quality-group-published";return true;
                }
                finally {if(!committed) RollbackBatch();}
            });
            if(!result && owner.LastReason!="external-refused") LastReason="quality-group-"+owner.LastReason;
            return result;
        }
        catch(OperationCanceledException) {RollbackBatch();LastReason="quality-group-cancelled";return false;}
        catch(Exception e) when(IsStorageError(e)) {RollbackBatch();FailureDetail=e.ToString();LastReason="quality-group-write-io";return false;}
    }

    internal bool Invalidate(OwnedCacheStore owner, string key)
    {
        CloseReads();
        if (!OwnedCacheStore.IsKey(key)) return false;
        // This is a mutation even when reads are bypassed; the owner must admit it.
        long reservation = IndexSize(entries);
        try
        {
            bool result = owner.PublishExternal(reservation, () =>
            {
                var next = new Dictionary<string, Entry>(entries, StringComparer.Ordinal);
                next.Remove(key);
                string nextTail = next.Values.Any(e => e.Blocks.Any(b => b.Group == tail)) ? tail : "";
                CommitIndex(next, nextTail, CancellationToken.None);
                entries = next; tail = nextTail; used.Remove(key); RebuildLookup(); CollectOrphans();
                LastReason = "group-invalidated"; return true;
            });
            if (!result) RollbackBatch();
            return result;
        }
        catch (Exception e) when (IsStorageError(e)) { RollbackBatch(); LastReason = "group-invalidate-io"; return false; }
    }

    // Persist reads once at normal completion. Old automatic entries expire in
    // bounded batches. Explicit selections and every paired member remain until
    // user replacement/reset/clear; the same shared ceiling still charges them.
    internal void Complete(OwnedCacheStore owner)
    {
        CloseReads();
        if (completed || entries.Count == 0) return;
        completed = true;
        var next = PrunedIndex("", null, DateTime.UtcNow.AddDays(-30).Ticks);
        RewriteIndex(owner, next, "group-completed");
    }

    private bool PruneUnused(OwnedCacheStore owner, string protectKey, string? protectOwner, long? cutoff)
    {
        var next = PrunedIndex(protectKey, protectOwner, cutoff);
        return next.Count < entries.Count && RewriteIndex(owner, next, "group-pruned");
    }

    private Dictionary<string, Entry> PrunedIndex(string protectKey, string? protectOwner, long? cutoff)
    {
        var next = new Dictionary<string, Entry>(entries, StringComparer.Ordinal);
        foreach (string key in entries.Where(p => !IsExplicitSelection(p.Key, p.Value) && p.Key != protectKey && !used.Contains(p.Key)
                     && (protectOwner == null || p.Value.Owner != protectOwner) && (!cutoff.HasValue || p.Value.UsedTicks < cutoff.Value))
                     .OrderBy(p => p.Value.UsedTicks).ThenBy(p => p.Key, StringComparer.Ordinal).Take(128).Select(p => p.Key))
            next.Remove(key);
        return next;
    }

    private bool RewriteIndex(OwnedCacheStore owner, Dictionary<string, Entry> next, string reason)
    {
        try
        {
            // There is no nested reservation. If even an index replacement
            // cannot fit, preserve the current generation and refuse safely.
            bool result = owner.PublishExternal(IndexSize(next), () =>
            {
                string nextTail = next.Values.Any(e => e.Blocks.Any(b => b.Group == tail)) ? tail : "";
                CommitIndex(next, nextTail, CancellationToken.None);
                entries = next; tail = nextTail; used.IntersectWith(next.Keys); RebuildLookup();
                CollectOrphans(); LastReason = reason; return true;
            });
            if (!result) RollbackBatch();
            return result;
        }
        catch (Exception e) when (IsStorageError(e)) { FailureDetail = e.ToString(); RollbackBatch(); LastReason = "group-maintenance-io"; return false; }
    }

    private bool PublishCore(string key, byte[] payload, bool compress, CancellationToken cancellation, string? ownerKey, bool stage)
    {
        // A complete entry record is its commit. The growing full index is
        // checkpointed only after a bounded batch, rather than for every image.
        var entry = new Entry { Length = payload.Length, Digest = CacheDigest.Hash(payload), Owner = ownerKey ?? "", UsedTicks = DateTime.UtcNow.Ticks };
        var created = new List<string>();
        FileStream? group = null;
        string groupId = "", pending = "", nextTail = tail, appended = "";
        long appendStart = 0;
        bool committed = false;
        try
        {
            if (stage) BeginBatch();
            OwnedCacheStore.RejectLinkedPath(root); Directory.CreateDirectory(root);
            if (files.Count + (payload.Length + BlockBytes - 1) / BlockBytes + 4 > InventoryLimit)
                throw new InvalidDataException("group-inventory-limit");
            for (int offset = 0, ordinal = 0; offset < payload.Length; ordinal++)
            {
                cancellation.ThrowIfCancellationRequested();
                int raw = Math.Min(BlockBytes, payload.Length - offset);
                using var encoded = compress ? TimedCompress(payload, offset, raw) : null;
                int size = encoded == null ? raw : checked((int)encoded.Length);
                if (group == null || group.Position + BlockHeader + size > GroupBytes)
                {
                    SealGroup();
                    // Append without rewriting any committed bytes. The index
                    // continues to expose only its old ranges until commit.
                    if (ordinal == 0 && tail.Length != 0
                        && files.TryGetValue(tail + ".group", out long oldLength)
                        && oldLength + BlockHeader + size <= GroupBytes)
                    {
                        string oldPath = Path.Combine(root, tail + ".group");
                        if (stage) RememberBatchGroup(tail + ".group");
                        OwnedCacheStore.RejectLinkedPath(oldPath);
                        group = new FileStream(oldPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                        if (group.Length != oldLength) throw new InvalidDataException("group-append-size");
                        groupId = tail; appended = tail + ".group"; appendStart = oldLength;
                        group.Position = oldLength;
                    }
                    else
                    {
                        groupId = Guid.NewGuid().ToString("N"); pending = groupId + ".pending";
                        if (stage) RememberBatchGroup(groupId + ".group");
                        group = new FileStream(Path.Combine(root, pending), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                    }
                }
                var block = new Block { Group = groupId, Offset = checked((int)group.Position), Raw = raw, Stored = size,
                    Compressed = encoded != null, Digest = Hash(payload, offset, raw) };
                using (var writer = new BinaryWriter(group, Encoding.UTF8, true))
                {
                    writer.Write(BlockMagic); writer.Write(Encoding.ASCII.GetBytes(key)); writer.Write(ordinal);
                    writer.Write(raw); writer.Write(size); writer.Write((byte)(block.Compressed ? 1 : 0)); writer.Write(block.Digest);
                    if (encoded == null) writer.Write(payload, offset, raw);
                    else writer.Write(encoded.GetBuffer(), 0, size);
                }
                entry.Blocks.Add(block); offset += raw;
            }
            SealGroup();
            cancellation.ThrowIfCancellationRequested();
            if (stage)
            {
                foreach (Block block in entry.Blocks) batchTouched.Add(block.Group + ".group");
                batchBytes += entry.Blocks.Sum(b => (long)BlockHeader + b.Stored); batchCount++;
            }
            else
            {
                var update = new Dictionary<string, Entry>(StringComparer.Ordinal) { [key] = entry };
                if (!files.ContainsKey("index")) CommitIndex(update, nextTail, cancellation);
                else CommitDelta(update, nextTail, cancellation);
            }
            committed = true; ApplyEntry(key, entry); tail = nextTail; used.Add(key);
            LastReason = stage ? "group-staged" : "group-published"; return true;
        }
        catch (OperationCanceledException) { LastReason = "group-cancelled"; return false; }
        catch (Exception e) when (IsStorageError(e)) { FailureDetail = e.ToString(); LastReason = e is InvalidDataException ? e.Message : "group-write-io"; return false; }
        finally
        {
            // A disk-full flush can fail again during Dispose. Continue to
            // inventory/clean the partial file so later writes cannot ignore it.
            try { group?.Dispose(); }
            catch (Exception e) when (IsStorageError(e)) { LastReason = "group-close-io"; }
            if (pending.Length != 0 && File.Exists(Path.Combine(root, pending))) { Track(pending); Remove(pending); }
            if (!committed)
            {
                foreach (string name in created) Remove(name);
                if (appended.Length != 0) TrimSuffix(appended, appendStart);
                // A failed trim may leave physical suffix bytes. Stop this
                // batch rather than admitting later writes against a smaller
                // staged-byte count than actually remains on disk.
                if (stage) RollbackBatch();
            }
        }

        void SealGroup()
        {
            if (group == null) return;
            long length = group.Length;
            long flushStarted = Stopwatch.GetTimestamp();
            try { if (!stage) group.Flush(true); group.Dispose(); group = null; }
            finally { DataFlushTicks += Stopwatch.GetTimestamp() - flushStarted; }
            if (pending.Length == 0)
            {
                Record(groupId + ".group", length); nextTail = groupId; return;
            }
            Track(pending);
            string name = groupId + ".group";
            File.Move(Path.Combine(root, pending), Path.Combine(root, name));
            // No fallible file query after installing a final name: cleanup
            // must know it even if later metadata IO fails.
            created.Add(name); Forget(pending); Record(name, length);
            pending = ""; nextTail = groupId;
        }
    }

    private void TrimSuffix(string name, long committedLength)
    {
        try
        {
            string path = Path.Combine(root, name);
            OwnedCacheStore.RejectLinkedPath(path);
            if (!File.Exists(path)) return; // Missing committed blocks fail on read.
            using (var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                if (file.Length > committedLength) { file.SetLength(committedLength); file.Flush(true); }
        }
        catch (Exception e) when (IsStorageError(e)) { LastReason = "group-truncate-io"; }
        finally { if (File.Exists(Path.Combine(root, name))) Track(name); }
    }

    private void BeginBatch()
    {
        if (batchPrevious != null) return;
        // Copy once per batch. Entry blocks remain immutable; read timestamps
        // may change, but do not alter a committed source identity or range.
        batchPrevious = new Dictionary<string, Entry>(entries, StringComparer.Ordinal);
        batchPreviousUsed = new HashSet<string>(used, StringComparer.Ordinal);
        batchPreviousTail = tail;
    }

    private void RememberBatchGroup(string name)
    {
        if (!batchOriginalLengths.ContainsKey(name))
            batchOriginalLengths.Add(name, files.TryGetValue(name, out long length) ? length : -1);
    }

    private void FlushBatch()
    {
        if (batchPrevious == null) return;
        long started = Stopwatch.GetTimestamp();
        try
        {
            foreach (string name in batchTouched)
            {
                string path = Path.Combine(root, name); OwnedCacheStore.RejectLinkedPath(path);
                using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                if (!files.TryGetValue(name, out long length) || file.Length != length)
                    throw new InvalidDataException("group-batch-size");
                file.Flush(true);
            }
        }
        finally { DataFlushTicks += Stopwatch.GetTimestamp() - started; }
    }

    private void RollbackBatch()
    {
        if (batchPrevious == null) return;
        entries = batchPrevious; tail = batchPreviousTail;
        used.Clear(); if (batchPreviousUsed != null) used.UnionWith(batchPreviousUsed);
        RebuildLookup(); batchStopped = true;
        // Keep the old checkpoint and deltas intact. Failed cleanup bytes stay
        // charged, and restart repeats recovery against the durable index.
        try
        {
            foreach (var pair in batchOriginalLengths)
                if (pair.Value < 0) Remove(pair.Key); else TrimSuffix(pair.Key, pair.Value);
        }
        finally { ResetBatch(); }
    }

    private void ResetBatch()
    {
        batchPrevious = null; batchPreviousUsed = null; batchPreviousTail = "";
        batchOriginalLengths.Clear(); batchTouched.Clear(); batchCount = 0; batchBytes = 0;
    }

    private void CommitIndex(Dictionary<string, Entry> next, string nextTail, CancellationToken cancellation)
    {
        long started = Stopwatch.GetTimestamp();
        FlushBatch();
        if (IndexSize(next) > MaximumIndex) throw new InvalidDataException("group-index-limit");
        byte[] bytes = EncodeIndex(next, nextTail);
        string pending = Guid.NewGuid().ToString("N") + ".pending";
        string path = Path.Combine(root, pending);
        OwnedCacheStore.RejectLinkedPath(root); Directory.CreateDirectory(root);
        try
        {
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
            Track(pending);
            ReadIndex(path, out _);
            cancellation.ThrowIfCancellationRequested();
            string destination = Path.Combine(root, "index");
            OwnedCacheStore.RejectLinkedPath(destination);
            if (File.Exists(destination))
            {
                // Windows can briefly refuse replacement while a scanner has
                // the index open. Keep the old index intact and retry only
                // while both names still exist: never replay a completed move.
                for (int attempt = 0; ; attempt++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    try { File.Replace(path, destination, null); break; }
                    catch (IOException) when (attempt < 2 && File.Exists(path) && File.Exists(destination))
                    { Thread.Sleep(10); }
                }
            }
            else File.Move(path, destination);
            // The commit point has passed. Updating accounting from the known
            // size cannot turn a published index into failed-group cleanup.
            Forget(pending); Record("index", bytes.Length);
            indexDigest = bytes.Skip(bytes.Length - 32).ToArray();
            // A crashed checkpoint may leave older delta files. Their base
            // digest no longer matches and reopening safely discards them.
            foreach (string name in deltas.ToArray()) Remove(name);
            deltas.Clear(); sequence = 0;
            ResetBatch();
        }
        finally { if (File.Exists(path)) { Track(pending); Remove(pending); } CheckpointTicks += Stopwatch.GetTimestamp() - started; }
    }

    private void CommitDelta(Dictionary<string, Entry> update, string nextTail, CancellationToken cancellation)
    {
        long started = Stopwatch.GetTimestamp();
        long serial = checked(sequence + 1);
        byte[] entry = EncodeIndex(update, nextTail);
        using var bytes = new MemoryStream(entry.Length + 76);
        using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
        {
            writer.Write(DeltaMagic); writer.Write(indexDigest); writer.Write(serial); writer.Write(entry);
            writer.Flush(); writer.Write(Hash(bytes.GetBuffer(), 0, checked((int)bytes.Length))); writer.Flush();
        }
        if (bytes.Length > MaximumDeltaBytes) throw new InvalidDataException("group-delta-limit");
        string id = Guid.NewGuid().ToString("N"), pending = id + ".pending";
        string name = serial.ToString("x16") + "_" + id + ".delta";
        string path = Path.Combine(root, pending);
        try
        {
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { file.Write(bytes.GetBuffer(), 0, (int)bytes.Length); file.Flush(true); }
            Track(pending);
            cancellation.ThrowIfCancellationRequested();
            // Atomic rename publishes one complete checked entry. No in-place
            // journal update can expose a partially appended record.
            File.Move(path, Path.Combine(root, name));
            Forget(pending); Record(name, bytes.Length); deltas.Add(name); sequence = serial;
        }
        finally { if (File.Exists(path)) { Track(pending); Remove(pending); } DeltaTicks += Stopwatch.GetTimestamp() - started; }
    }

    private void ApplyEntry(string key, Entry entry)
    {
        if (entry.Owner.Length != 0 && owners.TryGetValue(entry.Owner, out var prior))
            foreach (string old in prior.ToArray()) RemoveEntry(old);
        RemoveEntry(key);
        entries[key] = entry; indexBytes += EntryIndex + entry.Blocks.Count * BlockIndex;
        if (entry.Owner.Length != 0)
        {
            if (!owners.TryGetValue(entry.Owner, out var owned)) owners[entry.Owner] = owned = new HashSet<string>(StringComparer.Ordinal);
            owned.Add(key);
        }
    }

    private void RemoveEntry(string key)
    {
        if (!entries.TryGetValue(key, out var old)) return;
        entries.Remove(key); used.Remove(key); indexBytes -= EntryIndex + old.Blocks.Count * BlockIndex;
        if (old.Owner.Length != 0 && owners.TryGetValue(old.Owner, out var owned))
        { owned.Remove(key); if (owned.Count == 0) owners.Remove(old.Owner); }
    }

    private void RebuildLookup()
    {
        owners.Clear(); indexBytes = IndexSize(entries);
        foreach (var pair in entries)
            if (pair.Value.Owner.Length != 0)
            {
                if (!owners.TryGetValue(pair.Value.Owner, out var owned)) owners[pair.Value.Owner] = owned = new HashSet<string>(StringComparer.Ordinal);
                owned.Add(pair.Key);
            }
    }

    private static byte[] EncodeIndex(Dictionary<string, Entry> data, string tail)
    {
        using var bytes = new MemoryStream();
        using var writer = new BinaryWriter(bytes, Encoding.UTF8, true);
        writer.Write(IndexMagic); writer.Write(3);
        // Fresh authenticated generation even if entries return to an earlier
        // state. Old deltas must never revive a removed/replaced entry.
        writer.Write(Guid.NewGuid().ToByteArray()); writer.Write(data.Count);
        writer.Write(Encoding.ASCII.GetBytes(tail.PadRight(32, ' ')));
        foreach (var pair in data.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            writer.Write(Encoding.ASCII.GetBytes(pair.Key)); writer.Write(pair.Value.Length);
            writer.Write(pair.Value.Digest); writer.Write(pair.Value.Blocks.Count);
            writer.Write(Encoding.ASCII.GetBytes(pair.Value.Owner.PadRight(64, ' '))); writer.Write(pair.Value.UsedTicks);
            foreach (Block block in pair.Value.Blocks)
            {
                writer.Write(Encoding.ASCII.GetBytes(block.Group)); writer.Write(block.Offset);
                writer.Write(block.Stored); writer.Write(block.Raw); writer.Write((byte)(block.Compressed ? 1 : 0)); writer.Write(block.Digest);
            }
        }
        writer.Flush();
        byte[] digest = Hash(bytes.GetBuffer(), 0, checked((int)bytes.Length));
        writer.Write(digest); writer.Flush(); return bytes.ToArray();
    }

    private static Dictionary<string, Entry> ReadIndex(string path, out string tail)
        => DecodeIndex(ReadFile(path, MaximumIndex), out tail);

    private static byte[] ReadFile(string path, int limit)
    {
        OwnedCacheStore.RejectLinkedPath(path);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length < 76 || file.Length > limit) throw new InvalidDataException("group-index-size");
        byte[] bytes = new byte[(int)file.Length]; ReadExact(file, bytes, 0, bytes.Length); return bytes;
    }

    private static Dictionary<string, Entry> DecodeIndex(byte[] bytes, out string tail)
    {
        if (bytes.Length < 76 || bytes.Length > MaximumIndex) throw new InvalidDataException("group-index-size");
        if (!Hash(bytes, 0, bytes.Length - 32).SequenceEqual(bytes.Skip(bytes.Length - 32)))
            throw new InvalidDataException("group-index-digest");
        using var stream = new MemoryStream(bytes, false);
        using var reader = new BinaryReader(stream);
        if (reader.ReadInt32() != IndexMagic) throw new InvalidDataException("group-index-version");
        int version = reader.ReadInt32();
        if (version == 3) { if (reader.ReadBytes(16).Length != 16) throw new InvalidDataException("group-index-generation"); }
        else if (version != 2) throw new InvalidDataException("group-index-version");
        int count = reader.ReadInt32();
        if (count < 0 || count > MaximumEntries) throw new InvalidDataException("group-index-count");
        tail = Encoding.ASCII.GetString(reader.ReadBytes(32)).TrimEnd(' ');
        if (tail.Length != 0 && !IsGroup(tail + ".group")) throw new InvalidDataException("group-index-tail");
        var result = new Dictionary<string, Entry>(StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            string key = Encoding.ASCII.GetString(reader.ReadBytes(64));
            var entry = new Entry { Length = reader.ReadInt32(), Digest = reader.ReadBytes(32) };
            int blocks = reader.ReadInt32();
            entry.Owner = Encoding.ASCII.GetString(reader.ReadBytes(64)).TrimEnd(' '); entry.UsedTicks = reader.ReadInt64();
            if (!ValidEntry(key, entry.Length) || blocks != (entry.Length + BlockBytes - 1) / BlockBytes || entry.Digest.Length != 32)
                throw new InvalidDataException("group-index-entry");
            if (entry.Owner.Length != 0 && !OwnedCacheStore.IsKey(entry.Owner) || entry.UsedTicks < 0 || entry.UsedTicks > DateTime.MaxValue.Ticks)
                throw new InvalidDataException("group-index-owner-or-age");
            int total = 0;
            for (int j = 0; j < blocks; j++)
            {
                var block = new Block { Group = Encoding.ASCII.GetString(reader.ReadBytes(32)), Offset = reader.ReadInt32(),
                    Stored = reader.ReadInt32(), Raw = reader.ReadInt32() };
                byte compression = reader.ReadByte(); block.Compressed = compression == 1; block.Digest = reader.ReadBytes(32);
                if (!IsGroup(block.Group + ".group") || block.Offset < 0 || block.Stored < 1 || block.Stored > BlockBytes
                    || block.Raw != Math.Min(BlockBytes, entry.Length - total) || compression > 1 || (!block.Compressed && block.Stored != block.Raw)
                    || (long)block.Offset + BlockHeader + block.Stored > GroupBytes || block.Digest.Length != 32)
                    throw new InvalidDataException("group-index-block");
                entry.Blocks.Add(block); total += block.Raw;
            }
            if (total != entry.Length || result.ContainsKey(key)) throw new InvalidDataException("group-index-entry");
            result.Add(key, entry);
        }
        if (stream.Position != bytes.Length - 32) throw new InvalidDataException("group-index-layout");
        if (tail.Length != 0)
        {
            string selectedTail = tail;
            if (!result.Values.Any(e => e.Blocks.Any(b => b.Group == selectedTail))) throw new InvalidDataException("group-index-tail");
        }
        return result;
    }

    private long Reservation(string key, int length, string? ownerKey)
    {
        int count = (length + BlockBytes - 1) / BlockBytes;
        long index = indexBytes + EntryIndex + count * BlockIndex;
        int nextCount = entries.Count + 1;
        var removals = ownerKey != null && owners.TryGetValue(ownerKey, out var owned)
            ? new HashSet<string>(owned, StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
        if (entries.ContainsKey(key)) removals.Add(key);
        foreach (string old in removals) { index -= EntryIndex + entries[old].Blocks.Count * BlockIndex; nextCount--; }
        if (index > MaximumIndex || nextCount > MaximumEntries) return -1;
        // Each staged write admits enough free space for its eventual full
        // checkpoint as well as the new data. Existing staged bytes are charged.
        long metadata = batched || !files.ContainsKey("index") ? index : 168L + EntryIndex + count * BlockIndex;
        return length + count * (long)BlockHeader + metadata;
    }

    private static long NewGroupBytes(int length) => length + ((length + BlockBytes - 1) / BlockBytes) * (long)BlockHeader;
    internal static long EstimateRecordStorageBytes(int length)
    {
        if (length < 1 || length > MaximumEntry) throw new ArgumentOutOfRangeException(nameof(length));
        return NewGroupBytes(length) + EntryIndex + ((length + BlockBytes - 1) / BlockBytes) * (long)BlockIndex;
    }
    internal const int MaximumIndexReplacementBytes = MaximumIndex;
    private static long IndexSize(Dictionary<string, Entry> data) => 92L + data.Sum(p => EntryIndex + p.Value.Blocks.Count * BlockIndex);
    private static bool ValidEntry(string key, int length) => OwnedCacheStore.IsKey(key) && length > 0 && length <= MaximumEntry;
    private static byte[] ReadHash(byte[] data, int offset, int count)
    {
        long started = Stopwatch.GetTimestamp();
        try { return Hash(data, offset, count); }
        finally { ReadHashTicks += Stopwatch.GetTimestamp() - started; }
    }
    private static byte[] Hash(byte[] data, int offset, int count) => CacheDigest.Hash(data, offset, count);

    private static MemoryStream? TimedCompress(byte[] bytes, int offset, int length)
    {
        long started = Stopwatch.GetTimestamp();
        try { return Compress(bytes, offset, length); }
        finally { CompressionTicks += Stopwatch.GetTimestamp() - started; }
    }

    private static MemoryStream? Compress(byte[] bytes, int offset, int length)
    {
        var output = new LimitedMemoryStream(length);
        try
        {
            using (var deflater = new DeflateStream(output, CompressionLevel.Fastest, true)) deflater.Write(bytes, offset, length);
            if (output.Length < length) return output;
        }
        catch (InvalidDataException e) when (e.Message == "group-compression-expansion") { }
        output.Dispose(); return null;
    }
    private sealed class LimitedMemoryStream : MemoryStream
    {
        private readonly int maximum;
        internal LimitedMemoryStream(int maximum) : base(maximum) => this.maximum = maximum;
        public override void Write(byte[] buffer, int offset, int count)
        { if (count > maximum - Length) throw new InvalidDataException("group-compression-expansion"); base.Write(buffer, offset, count); }
        public override void WriteByte(byte value)
        { if (Length >= maximum) throw new InvalidDataException("group-compression-expansion"); base.WriteByte(value); }
    }

    private static void ReadExact(Stream input, byte[] buffer, int offset, int length)
    {
        for (int done = 0; done < length;)
        {
            int count = input.Read(buffer, offset + done, length - done);
            if (count == 0) throw new InvalidDataException("group-block-truncated");
            done += count;
        }
    }
    private void CollectOrphans()
    {
        var live = new HashSet<string>(entries.Values.SelectMany(e => e.Blocks).Select(b => b.Group + ".group"), StringComparer.Ordinal);
        foreach (string name in files.Keys.Where(n => IsGroup(n) && !live.Contains(n)).ToArray()) Remove(name);
    }
    private static bool IsGroup(string name) => name.Length == 38 && name.EndsWith(".group", StringComparison.Ordinal)
        && Guid.TryParseExact(name.Substring(0, 32), "N", out _);
    private static bool IsPending(string name) => name.Length == 40 && name.EndsWith(".pending", StringComparison.Ordinal)
        && Guid.TryParseExact(name.Substring(0, 32), "N", out _);
    private static bool IsDelta(string name) => name.Length == 55 && name[16] == '_' && name.EndsWith(".delta", StringComparison.Ordinal)
        && name.Substring(0, 16).All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f')
        && Guid.TryParseExact(name.Substring(17, 32), "N", out _);
    private void Track(string name)
    {
        try
        {
            string path = Path.Combine(root, name); OwnedCacheStore.RejectLinkedPath(path);
            long size = new FileInfo(path).Length; Record(name, size);
        }
        catch (Exception e) when (IsStorageError(e)) { accountingFailed = true; throw; }
    }
    private void Record(string name, long size) { Forget(name); files[name] = size; stored = checked(stored + size); }
    private void Forget(string name) { if (files.TryGetValue(name, out long size)) { stored -= size; files.Remove(name); } }
    private void Remove(string name)
    {
        try { string path = Path.Combine(root, name); OwnedCacheStore.RejectLinkedPath(path); File.Delete(path); Forget(name); }
        catch (Exception e) when (IsStorageError(e)) { LastReason = "group-cleanup-io"; }
    }
    private static bool IsStorageError(Exception e) => e is IOException || e is InvalidDataException || e is UnauthorizedAccessException
        || e is ArgumentException || e is NotSupportedException || e is OverflowException;
}
