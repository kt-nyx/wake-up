// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace WakeUp;

// Native automatic and manual output share indexed groups under the same
// owner and budget as explicit DDS exports. The 8 MiB constant is export-only.
internal sealed class PreparedTextureStore : IDisposable
{
    internal const int MaximumEntry = 8 * 1024 * 1024;
    internal const long MaximumStore = 512L * 1024 * 1024;
    internal const int MaximumIdentityBytes = 16 * 1024;
    internal const int MaximumMips = 14;
    // magic, version, identity length; eleven descriptor fields; pixel length;
    // final nonreadability; pixel digest; fourteen offset/length pairs.
    internal const int ManifestBytes = 12 + 44 + 4 + 4 + 32 + MaximumMips * 8;
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
    private readonly OwnedCacheStore store;
    private readonly PreparedTextureExports exports;
    private readonly GroupedTextureBlocks grouped;
    private readonly bool compress;
    private long generation;
    private bool disposed;
    private ReadLifetime lifetime = new();
    // The ordered consumer drains workers here, before any owner lock is held.
    internal event Action? BeforeMutation;
    internal sealed class ReadLifetime
    {
        private int invalid;
        internal void Invalidate() => Interlocked.Exchange(ref invalid, 1);
        internal bool IsValid => Volatile.Read(ref invalid) == 0;
        internal void Check() { if (!IsValid) throw new OperationCanceledException("prepared-read-stale"); }
    }
    internal sealed class ReadPlan
    {
        internal readonly GroupedTextureBlocks.ReadSnapshot Snapshot;
        internal readonly ReadLifetime Lifetime;
        internal string Key => Snapshot.Key;
        internal string Owner => Snapshot.Owner;
        internal long Generation { get; }
        internal int RecordBytes => Snapshot.RecordBytes;
        // During Decode both the complete record and its copied pixel array are
        // live. Reserve an additional full record conservatively plus one block's
        // compressed input, stream buffers and immutable descriptor overhead.
        internal long ExtraLiveBytes => 2L * RecordBytes + Snapshot.MaximumScratchBytes + 64 * 1024;
        // The warm upload owns the authenticated record itself; only the copied
        // pixel array disappears. Source bytes and stream contexts stay reserved.
        internal long OwnedRecordLiveBytes => ExtraLiveBytes - RecordBytes;
        internal ReadPlan(GroupedTextureBlocks.ReadSnapshot snapshot, ReadLifetime lifetime, long generation)
        { Snapshot = snapshot; Lifetime = lifetime; Generation = generation; }
        internal ReadOutcome Read(string fullIdentity, CancellationToken cancellation, GroupedTextureBlocks.ReadContext? context = null, bool ownedRecord = false)
        {
            try
            {
                cancellation.ThrowIfCancellationRequested(); Lifetime.Check();
                byte[]? identity = IdentityBytes(fullIdentity);
                if (identity == null || StableOwner(fullIdentity) != Owner || PngCache.Hex(CacheDigest.HashInput(identity)) != Key)
                    return new ReadOutcome(this, null, "prepared-source-miss");
                byte[] record = Snapshot.Read(cancellation, Lifetime.Check, context);
                cancellation.ThrowIfCancellationRequested(); Lifetime.Check();
                var view = ownedRecord ? NativeTextureData.DecodeOwned(fullIdentity, record) : null;
                var result = ownedRecord ? null : NativeTextureData.Decode(fullIdentity, record);
                cancellation.ThrowIfCancellationRequested(); Lifetime.Check();
                return new ReadOutcome(this, result, "group-hit", ownedRecord: view);
            }
            catch (OperationCanceledException) { return new ReadOutcome(this, null, "prepared-read-cancelled", cancelled: true); }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException || e is ArgumentException
                || e is NotSupportedException || e is OverflowException)
            { return new ReadOutcome(this, null, e is InvalidDataException ? e.Message : "prepared-read-io", isError: true); }
        }
    }
    internal sealed class ReadOutcome
    {
        internal readonly ReadPlan Plan;
        internal readonly PngCache.Entry? Result;
        internal readonly NativeTextureData.OwnedRecord? OwnedRecord;
        internal bool Completed;
        internal string Reason { get; }
        internal bool IsError { get; }
        internal bool Cancelled { get; }
        internal ReadOutcome(ReadPlan plan, PngCache.Entry? result, string reason, bool isError = false, bool cancelled = false,
            NativeTextureData.OwnedRecord? ownedRecord = null)
        { Plan = plan; Result = result; OwnedRecord = ownedRecord; Reason = reason; IsError = isError; Cancelled = cancelled; }
    }

    private bool BeginMutation()
    {
        if (disposed) return false;
        BeforeMutation?.Invoke();
        lifetime.Invalidate(); lifetime = new ReadLifetime(); generation++;
        return !disposed;
    }

    internal ReadPlan? PlanNative(string ownerKey) => store.ReadExternal(() =>
    {
        if (disposed) return null;
        var snapshot = grouped.PlanNative(ownerKey);
        return snapshot == null ? null : new ReadPlan(snapshot, lifetime, generation);
    });

    // Read usage is accounting, not a storage mutation: ordered consumption must
    // not drain its own queue. No validation, file IO, callbacks or decode occurs
    // while this short owner lock is held. Failed data is left for serial fallback.
    internal PngCache.Entry? CompleteRead(ReadPlan plan, ReadOutcome outcome) =>
        outcome.OwnedRecord == null && CompleteReadCore(plan, outcome) ? outcome.Result : null;
    internal NativeTextureData.OwnedRecord? CompleteOwnedRead(ReadPlan plan, ReadOutcome outcome) =>
        outcome.Result == null && CompleteReadCore(plan, outcome) ? outcome.OwnedRecord : null;
    private bool CompleteReadCore(ReadPlan plan, ReadOutcome outcome) => store.ReadExternal(() =>
    {
        if (disposed || !ReferenceEquals(plan.Lifetime, lifetime) || !plan.Lifetime.IsValid
            || plan.Generation != generation || !ReferenceEquals(outcome.Plan, plan) || outcome.Completed) return null;
        outcome.Completed = true;
        if (outcome.Result == null && outcome.OwnedRecord == null)
        { if (!outcome.Cancelled) { Misses++; if (outcome.IsError) Errors++; } return null; }
        if (!grouped.CompleteRead(plan.Snapshot)) { Misses++; return null; }
        Hits++; ReadBytes += plan.RecordBytes; return outcome;
    }) != null;
    internal static long DecodeTicks;
    internal long Hits, Misses, Writes, Errors, ReadBytes, WrittenBytes;
    internal long Pruned => store.Pruned;
    internal long PendingRemoved => store.PendingRemoved;
    internal long LegacyRemoved;
    internal string LastReason => grouped.LastReason;
    internal string FailureDetail => grouped.FailureDetail;
    internal System.Collections.Generic.IReadOnlyDictionary<string, long> Reasons => store.Reasons;
    internal long StoredBytes => store.StoredBytes;
    internal string ExportRoot => exports.Root;
    internal string ExportLastReason => exports.LastReason;

    internal PreparedTextureStore(string saveDataRoot, CacheLaunchPolicy? policy = null, long? maximumStore = null, bool compress = false)
        : this(saveDataRoot, policy, maximumStore, compress, false) { }

    internal PreparedTextureStore(string saveDataRoot, CacheLaunchPolicy? policy, long? maximumStore, bool compress, bool batched)
    {
        if (maximumStore.HasValue && (maximumStore < 1 || maximumStore > MaximumStore)) throw new ArgumentOutOfRangeException(nameof(maximumStore));
        string root = Path.Combine(saveDataRoot, "WakeUp", "PreparedTextures", "v1");
        long capacity = maximumStore ?? SharedCacheBudget.ForRoot(root)?.MaximumBytes ?? MaximumStore;
        this.compress = compress;
        exports = new PreparedTextureExports(Path.Combine(root, "exports"));
        grouped = new GroupedTextureBlocks(Path.Combine(root, "groups"), batched, retainReadHandles: batched);
        store = new OwnedCacheStore(root, MaximumEntry - OwnedCacheStore.EnvelopeBytes, capacity,
            policy ?? CacheLaunchPolicy.Current, () => exports.StoredBytes + grouped.StoredBytes,
            () => { exports.Clear(); grouped.Clear(); }, () => { exports.Initialize(); grouped.Initialize(); });
        try
        {
            if ((policy ?? CacheLaunchPolicy.Current).Action != CacheAction.Bypass)
            {
                // Old individual native formats cannot share the new source
                // contract. Retire only exact owned entries; exports remain.
                foreach (string path in Directory.EnumerateFiles(root, "*.bin"))
                {
                    string key = Path.GetFileNameWithoutExtension(path);
                    if (OwnedCacheStore.IsKey(key)) { store.Invalidate(key); LegacyRemoved++; }
                }
                CacheLaunchPolicy? clear = null;
                using var old = new PngCache(Path.Combine(saveDataRoot, "WakeUp", "PngCache"),
                    CacheLaunchPolicy.Latch(ref clear, "clear", () => { }));
                LegacyRemoved += old.Pruned + old.LegacyRemoved;
            }
        }
        catch { try { grouped.Dispose(); } finally { store.Dispose(); } throw; }
    }

    // Export reads retire corrupt manifests, so they are a mutation boundary too.
    internal byte[]? ReadExport(string identity) => BeginMutation() ? store.ReadExternal(() => exports.Read(identity)) : null;
    internal PreparationRecord? ReadQuality(string provider, string logical)
        => PreparationStorage.Read(store, grouped, provider, logical);
    internal bool PublishQuality(PreparationRecord record)
        => BeginMutation() && PreparationStorage.Publish(store, grouped, record, compress);
    internal bool PublishQualityGroup(System.Collections.Generic.IReadOnlyList<PreparationRecord> records, System.Threading.CancellationToken cancellation)
        => BeginMutation() && PreparationStorage.PublishGroup(store, grouped, records, compress, cancellation);
    internal bool ResetQuality(string provider, string logical)
        => BeginMutation() && PreparationStorage.Reset(store, grouped, provider, logical);
    internal bool PublishExport(string identity, string logicalPath, byte[] dds)
        => BeginMutation() && exports.Publish(store, identity, logicalPath, dds);
    // Advisory preflight; actual publication reserves its precise file+mapping
    // size again while the same exclusive category owner is held.
    internal bool CanPrepareExport(string identity, int maximumDdsBytes)
        => BeginMutation() && IdentityBytes(identity) != null && maximumDdsBytes > 0
            && store.CanPublishExternal(Math.Min(MaximumEntry, (long)maximumDdsBytes + 32768));

    internal static void Maintain(string saveDataRoot, CacheLaunchPolicy policy)
    {
        if (policy.Action != CacheAction.Clear) return;
        using var cache = new PreparedTextureStore(saveDataRoot, policy);
        if (cache.StoredBytes != 0) throw new IOException("prepared-clear-incomplete");
    }

    private static byte[]? IdentityBytes(string identity)
    {
        if (string.IsNullOrEmpty(identity) || identity.Length > MaximumIdentityBytes) return null;
        try
        {
            int count = Utf8.GetByteCount(identity);
            return count <= MaximumIdentityBytes ? Utf8.GetBytes(identity) : null;
        }
        catch (EncoderFallbackException) { return null; }
    }

    private static string? StableOwner(string identity) => identity.StartsWith("prepared-v3:", StringComparison.Ordinal)
        && identity.Length > 76 && identity[76] == ':' && OwnedCacheStore.IsKey(identity.Substring(12, 64))
        ? identity.Substring(12, 64) : null;
    private static string Key(byte[] identity) => PngCache.Hex(PngCache.Hash(identity));
    // Advisory presence only; the normal Read still validates the full identity
    // and payload. Respect the category's read policy and disposal boundary.
    internal bool HasOwner(string ownerKey)
        => store.ReadExternal(() => grouped.HasOwner(ownerKey) ? ownerKey : null) != null;
    internal bool CanPublish(string identity, int pixelBytes)
    {
        if (!BeginMutation()) return false;
        byte[]? bytes = IdentityBytes(identity);
        return bytes != null && pixelBytes > 0 && pixelBytes <= NativeTextureData.MaximumPixels
            && grouped.CanPublish(store, Key(bytes), pixelBytes + bytes.Length + 128, StableOwner(identity));
    }
    internal bool CanCapture(string? identity, int bytes) => identity != null && CanPublish(identity, bytes);
    internal bool TryCapture(string? identity, int bytes, Action capture)
    {
        if (!CanCapture(identity, bytes)) return false;
        capture(); return true;
    }
    internal void Invalidate(string identity)
    {
        if (!BeginMutation()) return;
        byte[]? bytes = IdentityBytes(identity);
        if (bytes != null) grouped.Invalidate(store, Key(bytes));
    }
    internal PngCache.Entry? Read(string identity)
    {
        byte[]? bytes = IdentityBytes(identity);
        if (bytes == null) { Misses++; return null; }
        try
        {
            var payload = store.ReadExternal(() => grouped.Read(Key(bytes)));
            if (payload == null) { Misses++; return null; }
            long started = Stopwatch.GetTimestamp();
            PngCache.Entry result;
            try { result = NativeTextureData.Decode(identity, payload); }
            finally { DecodeTicks += Stopwatch.GetTimestamp() - started; }
            ReadBytes += payload.Length; Hits++; return result;
        }
        catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException || e is ArgumentException || e is OverflowException)
        { Errors++; Misses++; Invalidate(identity); return null; }
    }
    internal bool Publish(string identity, PngCache.Entry entry)
    {
        if (!BeginMutation()) return false;
        byte[]? bytes = IdentityBytes(identity);
        if (bytes == null || !NativeTextureData.Valid(entry) || !CanPublish(identity, entry.Pixels.Length)) return false;
        try
        {
            byte[] payload = NativeTextureData.Encode(identity, entry);
            if (!grouped.Publish(store, Key(bytes), payload, compress, ownerKey: StableOwner(identity))) return false;
            Writes++; WrittenBytes += payload.Length; return true;
        }
        catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException || e is ArgumentException || e is OverflowException)
        { Errors++; return false; }
    }
    internal void Write(string identity, PngCache.Entry entry) => Publish(identity, entry);

    internal void Complete() { if (!BeginMutation()) return; grouped.Complete(store); store.Complete(); }
    public void Dispose()
    {
        if (!BeginMutation()) return;
        disposed = true; lifetime.Invalidate();
        try { grouped.Dispose(); } finally { store.Dispose(); }
    }
}
