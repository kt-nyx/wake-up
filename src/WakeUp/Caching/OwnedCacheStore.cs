// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace WakeUp;

// One owner per category, including across processes. Readers receive private
// bytes; no file handle or mutable shared payload escapes the owner lock.
// Formats remain the consumer's responsibility. The envelope binds the input
// key, version, length and digest to the exact published bytes.
internal sealed class OwnedCacheStore : IDisposable
{
    internal const int EnvelopeBytes = 108;
    private const int Magic = 0x31435557; // WUC1
    private const int InventoryLimit = 32768;
    private const int PruneLimit = 128;
    private readonly object gate;
    private readonly SharedCacheBudget? sharedBudget;
    private readonly string root;
    private readonly int maximumEntry;
    private readonly long maximumStore;
    private readonly CacheLaunchPolicy policy;
    private readonly Func<long>? externalUsage;
    private readonly Action? externalClear;
    private readonly FileStream? ownership;
    private readonly Dictionary<string, Record> entries = new(StringComparer.Ordinal);
    private readonly HashSet<string> used = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> reasons = new(StringComparer.Ordinal);
    private bool disposed;
    private long stored;
    private long usageBytes;
    internal long StoredBytes { get { lock (gate) return TotalStored; } }
    private long TotalStored => checked(stored + (externalUsage?.Invoke() ?? 0));
    internal long Pruned, PendingRemoved, Rejected;
    internal string LastReason = "none";
    internal IReadOnlyDictionary<string, long> Reasons { get { lock (gate) return new Dictionary<string, long>(reasons); } }
    private sealed class Record
    {
        internal long Length;
        internal long Used;
    }

    internal OwnedCacheStore(string root, int maximumEntry, long maximumStore, CacheLaunchPolicy policy,
        Func<long>? externalUsage = null, Action? externalClear = null, Action? externalInitialize = null,
        SharedCacheBudget? sharedBudget = null)
    {
        this.root = Path.GetFullPath(root);
        this.sharedBudget = policy.Action == CacheAction.Bypass ? null : sharedBudget ?? SharedCacheBudget.ForRoot(this.root);
        gate = this.sharedBudget?.Gate ?? new object();
        this.maximumEntry = maximumEntry;
        this.maximumStore = maximumStore;
        this.policy = policy;
        this.externalUsage = externalUsage;
        this.externalClear = externalClear;
        if (policy.Action == CacheAction.Bypass) return;
        lock (gate)
        {
        RejectLinkedPath(this.root);
        Directory.CreateDirectory(this.root);
        RejectLinkedPath(Path.Combine(this.root, ".owner"));
        if (this.sharedBudget != null) this.sharedBudget.Attach(this.root);
        else ownership = new FileStream(Path.Combine(this.root, ".owner"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            ownership?.SetLength(0);
            externalInitialize?.Invoke();
            int count = 0;
            foreach (string path in Directory.EnumerateFiles(this.root))
            {
                if (++count > InventoryLimit) throw new IOException("cache-inventory-limit");
                string name = Path.GetFileName(path);
                RejectLinkedPath(path);
                if (IsPending(name))
                {
                    File.Delete(path);
                    PendingRemoved++;
                }
                else if (name.EndsWith(".bin", StringComparison.Ordinal) && IsKey(name.Substring(0, name.Length - 4)))
                {
                    var file = new FileInfo(path);
                    entries.Add(name.Substring(0, name.Length - 4), new Record { Length = file.Length, Used = file.LastWriteTimeUtc.Ticks });
                    stored += file.Length;
                }
                else if (name == "usage") { usageBytes = new FileInfo(path).Length; stored += usageBytes; }
            }
            LoadUsage();
            this.sharedBudget?.Refresh(this.root, () => TotalStored);
            if (policy.Action == CacheAction.Clear) Clear();
            else if (policy.Action != CacheAction.Bypass) Prune(0, null, false);
        }
        catch { this.sharedBudget?.Detach(this.root); ownership?.Dispose(); throw; }
        }
    }

    internal static void RejectLinkedPath(string path)
    {
        for (string? item = Path.GetFullPath(path); item != null; item = Path.GetDirectoryName(item))
            if ((ExistingPathAttributes(item).GetValueOrDefault() & FileAttributes.ReparsePoint) != 0)
                throw new IOException("cache-linked-path");
    }

    [DllImport("kernel32.dll", EntryPoint = "GetFileAttributesW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern uint GetFileAttributes(string path);

    // Query every ancestor on every call. Only an absent path is admissible;
    // access failures must not turn into an unchecked cache location. A single
    // attribute query also avoids separate file/directory existence probes.
    internal static FileAttributes? ExistingPathAttributes(string path)
    {
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            try
            {
                uint attributes = GetFileAttributes(path);
                if (attributes != uint.MaxValue) return (FileAttributes)attributes;
                int error = Marshal.GetLastWin32Error();
                if (error == 2 || error == 3) return null; // File/path not found.
                throw new IOException("cache-path-attributes: " + error);
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
        try { return File.GetAttributes(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    internal static bool IsKey(string key) => key != null && key.Length == 64 && key.All(c => c >= '0' && c <= '9' || c >= 'A' && c <= 'F');
    private static bool IsPending(string name) => name.EndsWith(".pending", StringComparison.Ordinal)
        && (name.Length == 46 && name.StartsWith("usage.", StringComparison.Ordinal) && Guid.TryParseExact(name.Substring(6, 32), "N", out _)
            || name.Length == 105 && IsKey(name.Substring(0, 64)) && name[64] == '.' && Guid.TryParseExact(name.Substring(65, 32), "N", out _));
    private string EntryPath(string key)
    {
        if (!IsKey(key)) throw new ArgumentException("cache-key");
        return Path.Combine(root, key + ".bin");
    }

    internal byte[]? Read(string key) => Read(key, bytes => bytes);
    internal T? Read<T>(string key, Func<byte[], T> validate) where T : class
    {
        lock (gate)
        {
            if (disposed || !policy.AllowRead) { NoteReason(policy.Action.ToString().ToLowerInvariant()); return null; }
            try
            {
                string path = EntryPath(key);
                if (!entries.ContainsKey(key)) { NoteReason("missing"); return null; }
                RejectLinkedPath(path);
                using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                byte[] payload = Decode(input, key);
                T result = validate(payload);
                used.Add(key);
                entries[key].Used = DateTime.UtcNow.Ticks;
                LastReason = "hit";
                return result;
            }
            catch (Exception e) when (IsStorageError(e))
            {
                Rejected++;
                NoteReason(e is InvalidDataException ? e.Message : "read-io");
                Remove(key);
                return null;
            }
        }
    }

    private byte[] Decode(Stream input, string key)
    {
        if (input.Length < EnvelopeBytes || input.Length > maximumEntry + EnvelopeBytes) throw new InvalidDataException("store-size");
        using var reader = new BinaryReader(input, Encoding.UTF8, true);
        if (reader.ReadInt32() != Magic || reader.ReadInt32() != 1) throw new InvalidDataException("store-version");
        if (Encoding.ASCII.GetString(reader.ReadBytes(64)) != key) throw new InvalidDataException("store-identity");
        int length = reader.ReadInt32();
        byte[] digest = reader.ReadBytes(32);
        if (length < 1 || length > maximumEntry || input.Length - input.Position != length) throw new InvalidDataException("store-layout");
        byte[] payload = reader.ReadBytes(length);
        if (payload.Length != length || !Digest(payload).SequenceEqual(digest)) throw new InvalidDataException("store-digest");
        return payload;
    }

    // Writer callback is deliberately just a stream: workers may prepare bytes,
    // but reservation, temp creation, verification and publication stay here.
    internal bool Publish(string key, byte[] payload)
        => Publish(key, payload.Length, stream => stream.Write(payload, 0, payload.Length));

    // Admission before a consumer prepares expensive bytes. This does not hold
    // a reservation across callbacks; Publish rechecks under the same owner.
    internal bool CanPublish(string key, int length)
    {
        lock (gate)
        {
            if (disposed || !policy.AllowWrite || length < 1 || length > maximumEntry || !IsKey(key)) return false;
            int additional = entries.ContainsKey(key) ? 0 : 1;
            if (entries.Count + additional > InventoryLimit - 4) { NoteReason("entry-count"); return false; }
            long size = length + EnvelopeBytes;
            if (Fits(size + UsageReservation(entries.Count + additional))) return true;
            // Preserve the old generation while making room for its temporary
            // replacement. Repeated mip callbacks take the fast fit check above.
            Prune(size, key, false, additional);
            if (Fits(size + UsageReservation(entries.Count + additional))) return true;
            NoteReason("quota");
            return false;
        }
    }

    // Companion representations share the category's cross-process owner and
    // temporary-file reservation. Their bytes never enter a second envelope.
    internal T? ReadExternal<T>(Func<T?> read) where T : class
    { lock (gate) return disposed || !policy.AllowRead ? null : read(); }

    internal bool PublishExternal(long temporaryBytes, Func<bool> publish)
    {
        lock (gate)
        {
            if (!CanPublishExternal(temporaryBytes)) return false;
            using var reservation = sharedBudget?.Reserve(temporaryBytes);
            LastReason = "external-refused";
            bool result = publish();
            if (result) LastReason = "external-published";
            return result;
        }
    }

    internal bool CanPublishExternal(long temporaryBytes)
    {
        lock (gate)
        {
            if (disposed || !policy.AllowWrite) { NoteReason(disposed ? "disposed" : policy.Action.ToString().ToLowerInvariant()); return false; }
            if (temporaryBytes < 1 || temporaryBytes > maximumStore) { NoteReason("quota"); return false; }
            Prune(temporaryBytes, null, false, 0);
            if (Fits(temporaryBytes + UsageReservation(entries.Count))) return true;
            NoteReason("quota"); return false;
        }
    }

    internal bool Publish(string key, int length, Action<Stream> write)
    {
        lock (gate)
        {
            if (!CanPublish(key, length)) return false;
            using var reservation = sharedBudget?.Reserve(length + EnvelopeBytes);
            string? pending = null;
            try
            {
                string destination = EntryPath(key);
                long size = length + EnvelopeBytes;
                // Temporary bytes also count against disk quota. The previous
                // generation is protected until verified atomic replacement.
                pending = Path.Combine(root, key + "." + Guid.NewGuid().ToString("N") + ".pending");
                using (var file = new FileStream(pending, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
                    using var bytes = new MemoryStream();
                    // Enforce writer bounds as well as the declared size.
                    using (var bounded = new BoundedWriteStream(bytes, length)) write(bounded);
                    if (bytes.Length != length) throw new InvalidDataException("write-size");
                    byte[] payload = bytes.ToArray();
                    using var writer = new BinaryWriter(file, Encoding.UTF8, true);
                    writer.Write(Magic); writer.Write(1); writer.Write(Encoding.ASCII.GetBytes(key));
                    writer.Write(length); writer.Write(Digest(payload)); writer.Write(payload);
                    writer.Flush(); file.Flush(true); file.Position = 0;
                    Decode(file, key);
                }
                RejectLinkedPath(destination);
                long old = entries.TryGetValue(key, out var previous) ? previous.Length : 0;
                if (File.Exists(destination)) File.Replace(pending, destination, null);
                else File.Move(pending, destination);
                pending = null;
                stored += size - old;
                entries[key] = new Record { Length = size, Used = DateTime.UtcNow.Ticks };
                used.Add(key);
                LastReason = "published";
                return true;
            }
            catch (Exception e) when (IsStorageError(e)) { NoteReason(e is InvalidDataException ? e.Message : "write-io"); Rejected++; return false; }
            finally
            {
                if (pending != null)
                    try { File.Delete(pending); } catch (Exception e) when (IsStorageError(e))
                    {
                        // A failed cleanup must not make leftover disk bytes invisible
                        // to later reservations in this owner session.
                        try { stored += new FileInfo(pending).Length; }
                        catch (Exception failure) when (IsStorageError(failure)) { stored = maximumStore; }
                        LastReason = "pending-cleanup-io";
                    }
            }
        }
    }

    internal void Invalidate(string key) { lock (gate) { if (!disposed && policy.Action != CacheAction.Bypass) Remove(key); } }
    private bool Remove(string key)
    {
        try
        {
            string path = EntryPath(key);
            RejectLinkedPath(path);
            File.Delete(path);
            if (entries.TryGetValue(key, out var entry)) { stored -= entry.Length; entries.Remove(key); }
            used.Remove(key);
            return true;
        }
        catch (Exception e) when (IsStorageError(e)) { LastReason = "delete-io"; return false; }
    }

    private void Prune(long incoming, string? protect, bool expired, int? summaryAdditionalEntries = null)
    {
        long cutoff = DateTime.UtcNow.AddDays(-30).Ticks;
        foreach (string key in entries.Where(p => p.Key != protect && !used.Contains(p.Key))
                     .OrderBy(p => p.Value.Used).ThenBy(p => p.Key, StringComparer.Ordinal).Take(PruneLimit).Select(p => p.Key).ToArray())
        {
            long summary = summaryAdditionalEntries.HasValue ? UsageReservation(entries.Count + summaryAdditionalEntries.Value) : 0;
            if (Fits(incoming + summary) && (!expired || entries[key].Used >= cutoff)) break;
            if (Remove(key)) Pruned++;
        }
    }

    private void Clear()
    {
        foreach (string key in entries.Keys.ToArray()) if (Remove(key)) Pruned++;
        File.Delete(Path.Combine(root, "usage"));
        stored -= usageBytes;
        usageBytes = 0;
        externalClear?.Invoke();
    }

    private void LoadUsage()
    {
        string path = Path.Combine(root, "usage");
        if (!File.Exists(path)) return;
        try
        {
            if (usageBytes > InventoryLimit * 96L)
            {
                File.Delete(path); stored -= usageBytes; usageBytes = 0;
                LastReason = "usage-size";
                return;
            }
            foreach (string line in File.ReadLines(path))
            {
                string[] parts = line.Split(' ');
                if (parts.Length == 2 && entries.TryGetValue(parts[0], out var entry) && long.TryParse(parts[1], out long ticks)
                    && ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.UtcNow.Ticks) entry.Used = ticks;
            }
        }
        catch (Exception e) when (IsStorageError(e)) { LastReason = "usage-read-io"; }
    }

    internal void Complete()
    {
        lock (gate)
        {
            if (disposed || !policy.AllowWrite) return;
            Prune(0, null, true);
            Prune(0, null, false, 0);
            if (!Fits(UsageReservation(entries.Count))) { LastReason = "usage-quota"; return; }
            using var reservation = sharedBudget?.Reserve(UsageReservation(entries.Count));
            string pending = Path.Combine(root, "usage." + Guid.NewGuid().ToString("N") + ".pending");
            try
            {
                File.WriteAllLines(pending, entries.Select(p => p.Key + " " + p.Value.Used));
                string path = Path.Combine(root, "usage");
                RejectLinkedPath(path);
                if (File.Exists(path)) File.Replace(pending, path, null); else File.Move(pending, path);
                stored -= usageBytes;
                usageBytes = new FileInfo(path).Length;
                stored += usageBytes;
            }
            catch (Exception e) when (IsStorageError(e)) { LastReason = "usage-write-io"; }
            finally
            {
                try { File.Delete(pending); }
                catch (Exception e) when (IsStorageError(e))
                {
                    try { stored += new FileInfo(pending).Length; }
                    catch (Exception failure) when (IsStorageError(failure)) { stored = maximumStore; }
                    LastReason = "usage-cleanup-io";
                }
            }
        }
    }

    internal void RefreshSharedInventory()
    { lock (gate) { if (!disposed) sharedBudget?.Refresh(root, () => TotalStored); } }
    private bool Fits(long incoming) => TotalStored <= maximumStore && incoming <= maximumStore - TotalStored
        && (sharedBudget?.Fits(incoming) ?? true);
    public void Dispose() { lock (gate) { if (disposed) return; disposed = true; sharedBudget?.Detach(root); ownership?.Dispose(); } }
    private static byte[] Digest(byte[] bytes) => CacheDigest.Hash(bytes);
    private static long UsageReservation(int count) => count * 96L;
    private void NoteReason(string reason)
    {
        LastReason = reason;
        if (reasons.Count >= 32 && !reasons.ContainsKey(reason)) reason = "other";
        reasons.TryGetValue(reason, out long count);
        reasons[reason] = count + 1;
    }
    private static bool IsStorageError(Exception e) => e is IOException || e is InvalidDataException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException;

    private sealed class BoundedWriteStream : Stream
    {
        private readonly Stream target;
        private readonly long maximum;
        internal BoundedWriteStream(Stream target, long maximum) { this.target = target; this.maximum = maximum; }
        public override void Write(byte[] buffer, int offset, int count)
        { if (count < 0 || target.Length + count > maximum) throw new InvalidDataException("write-size"); target.Write(buffer, offset, count); }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => target.Length;
        public override long Position { get => target.Position; set => throw new NotSupportedException(); }
        public override void Flush() => target.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
