// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace WakeUp;

// Only accepts the caller's already selected, native-ordered filesystem sources.
// The caller must close this interval before any callback can mutate its sources.
internal sealed class OrderedInputQueue : IDisposable
{
    internal const int MaximumFileBytes = 16 * 1024 * 1024;
    private readonly object gate = new();
    private readonly FileInfo[] files;
    private readonly int[]? sourceGroups;
    private readonly int maxJobs;
    private readonly long maxBytes;
    private readonly Thread[] workers;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Action<int, CancellationToken>? beforeRead;
    private readonly Dictionary<int, Entry> entries = new();
    private readonly Queue<Entry> pending = new();
    private readonly HashSet<int> active = new();
    private readonly List<Tuple<int, int>> overlaps = new();
    private readonly List<Tuple<int, int>> crossGroupOverlaps = new();
    private int nextScheduled, nextTaken;
    private int? transferred;
    private bool disposed;
    private long reservedBytes, scheduled, completed, taken, differentFileOverlap, peakReservedBytes;
    private long crossGroupOverlap;
    private int maximumActive, peakOutstanding;

    private sealed class Entry
    {
        internal int Index;
        internal long Reserved;
        internal bool Done, Transferred;
        internal Snapshot? Result;
    }

    internal sealed class Snapshot : IDisposable
    {
        internal readonly byte[] Bytes;
        internal readonly byte[] Digest;
        internal readonly FileInfo File;
        private FileStream? lease;
        private Action? release;

        internal Snapshot(FileInfo file, byte[] bytes, byte[] digest, FileStream lease, Action release)
        { File = file; Bytes = bytes; Digest = digest; this.lease = lease; this.release = release; }

        public void Dispose()
        {
            // Release the file before giving its memory reservation back to workers.
            try { Interlocked.Exchange(ref lease, null)?.Dispose(); }
            finally { Interlocked.Exchange(ref release, null)?.Invoke(); }
        }
    }

    internal OrderedInputQueue(IReadOnlyList<FileInfo> files, int maxJobs = 24,
        long maxBytes = 32L * 1024 * 1024, int workers = 2,
        Action<int, CancellationToken>? beforeRead = null, IReadOnlyList<int>? sourceGroups = null)
    {
        if (files == null) throw new ArgumentNullException(nameof(files));
        if (maxJobs < 1 || maxBytes < 1 || workers < 1 || workers > maxJobs)
            throw new ArgumentOutOfRangeException(nameof(maxJobs));
        this.files = new FileInfo[files.Count];
        for (int i = 0; i < files.Count; i++) this.files[i] = files[i] ?? throw new ArgumentException("null-source", nameof(files));
        if (sourceGroups != null)
        {
            if (sourceGroups.Count != files.Count) throw new ArgumentException("source-group-count", nameof(sourceGroups));
            this.sourceGroups = new int[sourceGroups.Count];
            for (int i = 0; i < sourceGroups.Count; i++) this.sourceGroups[i] = sourceGroups[i];
        }
        this.maxJobs = maxJobs;
        this.maxBytes = maxBytes;
        this.beforeRead = beforeRead;
        this.workers = new Thread[workers];
        lock (gate) Fill();
        try
        {
            for (int i = 0; i < workers; i++)
            {
                this.workers[i] = new Thread(Work) { IsBackground = true, Name = "Wake-Up XML input " + i };
                this.workers[i].Start();
            }
        }
        catch { Dispose(); throw; }
    }

    internal long Scheduled { get { lock (gate) return scheduled; } }
    internal long Completed { get { lock (gate) return completed; } }
    internal long Taken { get { lock (gate) return taken; } }
    internal long DifferentFileOverlap { get { lock (gate) return differentFileOverlap; } }
    internal long CrossGroupOverlap { get { lock (gate) return crossGroupOverlap; } }
    internal int MaximumActive { get { lock (gate) return maximumActive; } }
    internal long PeakReservedBytes { get { lock (gate) return peakReservedBytes; } }
    internal int PeakOutstanding { get { lock (gate) return peakOutstanding; } }
    internal long ReservedBytes { get { lock (gate) return reservedBytes; } }
    internal int Outstanding { get { lock (gate) return entries.Count; } }
    internal bool WorkersAlive { get { foreach (Thread? worker in workers) if (worker?.IsAlive == true) return true; return false; } }
    internal IReadOnlyList<Tuple<int, int>> Overlaps { get { lock (gate) return overlaps.ToArray(); } }
    internal IReadOnlyList<Tuple<int, int>> CrossGroupOverlaps { get { lock (gate) return crossGroupOverlaps.ToArray(); } }

    // Admission is ordered: a large next source waits for earlier consumption,
    // instead of allowing later completed buffers to exhaust its required budget.
    private void Fill()
    {
        while (!disposed && nextScheduled < files.Length && entries.Count < maxJobs)
        {
            long length = 0;
            try
            {
                FileInfo file = files[nextScheduled];
                file.Refresh();
                if (file.Exists) length = file.Length;
            }
            catch { /* Native consumption reports this source's error in order. */ }
            bool eligible = length > 0 && length <= MaximumFileBytes && length <= maxBytes;
            if (eligible && length > maxBytes - reservedBytes) break;
            var entry = new Entry { Index = nextScheduled++, Reserved = eligible ? length : 0, Done = !eligible };
            entries.Add(entry.Index, entry);
            reservedBytes += entry.Reserved;
            peakReservedBytes = Math.Max(peakReservedBytes, reservedBytes);
            peakOutstanding = Math.Max(peakOutstanding, entries.Count);
            if (eligible) { pending.Enqueue(entry); scheduled++; }
        }
        Monitor.PulseAll(gate);
    }

    internal Snapshot? Take(int index)
    {
        lock (gate)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OrderedInputQueue));
            if (index != nextTaken || index >= files.Length) throw new ArgumentOutOfRangeException(nameof(index), "native-order-required");
            if (transferred.HasValue) throw new InvalidOperationException("dispose-previous-source-before-take");
            Entry entry;
            while (!entries.TryGetValue(index, out entry!) || !entry.Done)
            {
                Monitor.Wait(gate);
                if (disposed) throw new ObjectDisposedException(nameof(OrderedInputQueue));
            }
            nextTaken++;
            if (entry.Result == null) { Release(entry); return null; }
            entry.Transferred = true;
            transferred = index;
            taken++;
            return entry.Result;
        }
    }

    private void Work()
    {
        while (true)
        {
            Entry entry;
            lock (gate)
            {
                while (!disposed && pending.Count == 0) Monitor.Wait(gate);
                if (disposed) return;
                entry = pending.Dequeue();
                foreach (int other in active)
                {
                    differentFileOverlap++;
                    if (overlaps.Count < 64) overlaps.Add(Tuple.Create(other, entry.Index));
                    if (sourceGroups != null && sourceGroups[other] != sourceGroups[entry.Index])
                    {
                        crossGroupOverlap++;
                        if (crossGroupOverlaps.Count < 64) crossGroupOverlaps.Add(Tuple.Create(other, entry.Index));
                    }
                }
                active.Add(entry.Index);
                maximumActive = Math.Max(maximumActive, active.Count);
            }
            Snapshot? result = null;
            try { result = Capture(entry); }
            catch { /* Private failures are discarded; no worker-side native errors. */ }
            lock (gate)
            {
                active.Remove(entry.Index);
                completed++;
                if (disposed)
                {
                    if (result != null) result.Dispose();
                    else Release(entry);
                }
                else
                {
                    entry.Result = result;
                    entry.Done = true;
                    if (result == null) { reservedBytes -= entry.Reserved; entry.Reserved = 0; Fill(); }
                }
                Monitor.PulseAll(gate);
            }
        }
    }

    private Snapshot? Capture(Entry entry)
    {
        CancellationToken token = cancellation.Token;
        token.ThrowIfCancellationRequested();
        // Deterministic test seam; production never supplies a callback.
        beforeRead?.Invoke(entry.Index, token);
        token.ThrowIfCancellationRequested();
        FileInfo file = files[entry.Index];
        OwnedCacheStore.RejectLinkedPath(file.FullName);
        FileStream? stream = null;
        try
        {
            stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
            // Recheck after open and bind the allocation to the admitted length.
            OwnedCacheStore.RejectLinkedPath(file.FullName);
            if (stream.Length != entry.Reserved) return null;
            var bytes = new byte[(int)entry.Reserved];
            int offset = 0;
            while (offset < bytes.Length)
            {
                token.ThrowIfCancellationRequested();
                int read = stream.Read(bytes, offset, Math.Min(65536, bytes.Length - offset));
                if (read == 0) return null;
                offset += read;
            }
            if (stream.Length != bytes.Length || stream.ReadByte() != -1) return null;
            byte[] digest = CacheDigest.HashInput(bytes);
            token.ThrowIfCancellationRequested();
            var snapshot = new Snapshot(file, bytes, digest, stream, () => { lock (gate) Release(entry); });
            stream = null;
            return snapshot;
        }
        finally { stream?.Dispose(); }
    }

    private void Release(Entry entry)
    {
        if (!entries.Remove(entry.Index)) return;
        reservedBytes -= entry.Reserved;
        entry.Reserved = 0;
        entry.Result = null;
        if (transferred == entry.Index) transferred = null;
        Fill();
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            Monitor.PulseAll(gate);
        }
        cancellation.Cancel();
        foreach (Thread? worker in workers)
            if (worker != null && (worker.ThreadState & ThreadState.Unstarted) == 0) worker.Join();
        lock (gate)
        {
            foreach (Entry entry in new List<Entry>(entries.Values))
            {
                if (entry.Transferred) continue; // Its consumer still owns the lease.
                if (entry.Result != null) entry.Result.Dispose();
                else Release(entry);
            }
            pending.Clear();
        }
        cancellation.Dispose();
    }
}
