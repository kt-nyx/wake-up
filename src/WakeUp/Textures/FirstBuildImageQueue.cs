// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace WakeUp;

// One ordered caller owns admission/publication. Workers only read leased files
// and produce private CPU data. The estimator and decoder must agree on the
// maximum live decoded allocation, including their scratch space.
internal sealed class FirstBuildImageQueue<T> : IDisposable where T : class
{
    internal const int RowOverheadBytes = 1024 * 1024;
    private readonly object gate = new();
    private readonly IReadOnlyList<FileInfo?> files;
    private readonly bool[]? barriers;
    private readonly Func<Stream, long> estimateDecodedBytes;
    private readonly Func<byte[], CancellationToken, T> decode;
    private readonly Func<int, Stream, WorkItem?>? plan;
    private readonly Func<int, WorkItem?>? unopenedPlan;
    private readonly int maximumJobs;
    private readonly long maximumBytes, maximumSourceBytes, maximumDecodedBytes;
    private readonly Queue<Lease> pending = new();
    private readonly Dictionary<int, Lease> held = new();
    private readonly List<Thread> workers = new();
    private readonly CancellationTokenSource cancellation = new();
    private Lease? publication;
    private int nextIndex, lastRequested, refusedIndex = -1;
    private bool disposed, suspended;
    private long retained, revalidated, rejectedRetained;
    private long scheduled, decoded, consumed, failed, heldBytes, maxReservedBytes;
    private long ownerPreparationTicks, workerOpenTicks, workerReadTicks, workerDecodeTicks;
    private int activeWorkers, maxActiveWorkers;

    internal FirstBuildImageQueue(IReadOnlyList<FileInfo?> files,
        Func<Stream, long> estimateDecodedBytes, Func<byte[], CancellationToken, T> decode,
        int workers = 2, int jobs = 8, long maxReservedBytes = 192L * 1024 * 1024,
        long maxSourceBytes = 96L * 1024 * 1024, long maxDecodedBytes = 64L * 1024 * 1024,
        IReadOnlyList<bool>? barriers = null, Func<int, Stream, WorkItem?>? plan = null,
        int startIndex = 0, Func<int, WorkItem?>? unopenedPlan = null)
    {
        this.files = files ?? throw new ArgumentNullException(nameof(files));
        this.estimateDecodedBytes = estimateDecodedBytes ?? throw new ArgumentNullException(nameof(estimateDecodedBytes));
        this.decode = decode ?? throw new ArgumentNullException(nameof(decode));
        this.plan = plan;
        this.unopenedPlan = unopenedPlan;
        if (startIndex < 0 || startIndex > files.Count)
            throw new ArgumentOutOfRangeException(nameof(startIndex));
        nextIndex = startIndex;
        lastRequested = startIndex - 1;
        if (barriers != null && barriers.Count != files.Count)
            throw new ArgumentException("Barriers must align with the source indices.", nameof(barriers));
        this.barriers = barriers?.ToArray();
        if (workers < 1 || jobs < 1 || maxReservedBytes < 1 || maxSourceBytes < 1 || maxDecodedBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(workers), "Worker, job and byte limits must be positive.");
        maximumJobs = jobs;
        maximumBytes = maxReservedBytes;
        maximumSourceBytes = Math.Min(maxSourceBytes, int.MaxValue);
        maximumDecodedBytes = maxDecodedBytes;
        try
        {
            for (int i = 0; i < Math.Min(workers, jobs); i++)
            {
                var worker = new Thread(Work) { IsBackground = true, Name = "Wake-Up image preparation" };
                worker.Start();
                this.workers.Add(worker);
            }
        }
        catch { Dispose(); throw; }
    }

    internal long Scheduled { get { lock (gate) return scheduled; } }
    internal long Decoded { get { lock (gate) return decoded; } }
    internal long Consumed { get { lock (gate) return consumed; } }
    internal long Failed { get { lock (gate) return failed; } }
    internal long HeldBytes { get { lock (gate) return heldBytes; } }
    internal long MaxReservedBytes { get { lock (gate) return maxReservedBytes; } }
    internal int ActiveWorkers { get { lock (gate) return activeWorkers; } }
    internal int MaxActiveWorkers { get { lock (gate) return maxActiveWorkers; } }
    internal long Retained { get { lock (gate) return retained; } }
    internal long Revalidated { get { lock (gate) return revalidated; } }
    internal long RejectedRetained { get { lock (gate) return rejectedRetained; } }
    internal int OpenStreams { get { lock (gate) return held.Values.Count(item => item.Stream != null); } }
    internal bool IsSuspended { get { lock (gate) return suspended; } }
    // Aggregate work ticks can overlap across workers; they are diagnostics,
    // never additive elapsed startup time. ActiveWorkers spans open/read/decode.
    internal long OwnerPreparationTicks { get { lock (gate) return ownerPreparationTicks; } }
    internal long WorkerOpenTicks { get { lock (gate) return workerOpenTicks; } }
    internal long WorkerReadTicks { get { lock (gate) return workerReadTicks; } }
    internal long WorkerDecodeTicks { get { lock (gate) return workerDecodeTicks; } }

    // Calls must be in strictly increasing source order on the owning caller.
    // Null/refused entries are discovered barriers and return false, leaving
    // ordinary loading to that caller after earlier private work has drained.
    // An acquired lease must be disposed before asking for another item.
    // The caller must visit each barrier before invoking that item's external
    // callback. A barrier (or an index jump crossing one) closes every source,
    // retires skipped work and returns false without admitting later files. For a jump, the
    // requested item uses ordinary loading too; only the next call can resume.
    internal bool TryTake(int index, out Lease? lease)
    {
        lease = null;
        bool crossedBarrier = false;
        lock (gate)
        {
            ThrowIfDisposed();
            if (publication != null) throw new InvalidOperationException("Release the publication lease before advancing.");
            if (index < 0 || index >= files.Count || index <= lastRequested)
                throw new ArgumentOutOfRangeException(nameof(index), "Indices must advance in source order.");
            if (barriers != null)
                for (int skipped = lastRequested + 1; skipped <= index; skipped++)
                    if (barriers[skipped]) { crossedBarrier = true; break; }
            if (refusedIndex >= 0 && refusedIndex <= index) crossedBarrier = true;
            lastRequested = index;
        }

        if (crossedBarrier)
        {
            SuspendBoundary(index);
            return false;
        }

        // A retained snapshot has no source lock. Authenticate the actual file
        // before restarting workers or opening any other source; on rejection
        // ordinary loading can run with every speculative source still closed.
        Lease? retainedLease;
        lock (gate) held.TryGetValue(index, out retainedLease);
        if (retainedLease != null && retainedLease.NeedsRevalidation)
        {
            // Consume the completed batch without filling each vacated slot.
            // Otherwise every following GPU suspension waits for one newly
            // admitted decode, serializing an otherwise parallel horizon.
            // This also retires skipped indices and any mixed interval work.
            SuspendBoundary(index);
            if (!Revalidate(retainedLease))
            {
                retainedLease.Dispose();
                return false;
            }
            lock (gate)
            {
                publication = lease = retainedLease;
                return true;
            }
        }
        lock (gate)
        {
            suspended = false;
            Monitor.PulseAll(gate);
        }

        // If the caller skips indices, release those private results before
        // admitting the next bounded horizon. Never leave skipped jobs pinned.
        do
        {
            Lease[] skipped;
            lock (gate) skipped = held.Where(item => item.Key < index).Select(item => item.Value).ToArray();
            foreach (Lease item in skipped) { WaitFor(item); item.Dispose(); }
            Fill();
            if (refusedIndex >= 0 && refusedIndex <= index)
            {
                SuspendBoundary(index);
                return false;
            }
            if (nextIndex <= index)
            {
                // A replanned hole can now need more capacity than before its
                // callback. Retire the farthest retained result first rather
                // than spin forever or exceed the shared reservation budget.
                Lease? farthest;
                lock (gate) farthest = held.Where(item => item.Key > index)
                    .OrderByDescending(item => item.Key).Select(item => item.Value).FirstOrDefault();
                if (farthest != null) { WaitFor(farthest); farthest.Dispose(); }
            }
        } while (nextIndex <= index);

        lock (gate)
        {
            ThrowIfDisposed();
            if (!held.TryGetValue(index, out lease)) return false;
        }
        WaitFor(lease);
        lock (gate)
        {
            ThrowIfDisposed();
            publication = lease;
            return true;
        }
    }

    private void DrainBoundary(int index)
    {
        Quiesce();
        lock (gate)
            foreach (Lease item in held.Values.ToArray()) item.Dispose();
        // Admission stopped at the boundary. No future source lease survives
        // when the caller performs its ordinary fallback or external callback.
        nextIndex = index + 1;
        refusedIndex = -1;
    }

    // Stop dequeue before waiting: pending jobs must not start while the owner
    // is preparing to enter an unknown callback. Only already active workers
    // finish. Their bounded buffers remain reserved until release or retention.
    private void Quiesce()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            suspended = true;
            while (activeWorkers != 0 && !disposed) Monitor.Wait(gate);
            ThrowIfDisposed();
            while (pending.Count != 0)
            {
                Lease item = pending.Dequeue();
                item.Done = true;
                item.Dispose();
            }
        }
    }

    private void SuspendBoundary(int index)
    {
        Quiesce();
        lock (gate)
        {
            foreach (Lease item in held.Values.ToArray())
            {
                item.Stream?.Dispose();
                item.Stream = null;
                if ((item.Index < index && item != publication) || !item.RetainAcrossCallbacks ||
                    item.Value == null || item.Error != null || !item.SourceComplete)
                {
                    item.Dispose();
                    continue;
                }
                if (!item.NeedsRevalidation) retained++;
                item.NeedsRevalidation = true;
            }
            // Fill skips retained entries and replans only holes left by warm,
            // failed or pending work. Current publication remains caller-owned.
            nextIndex = index + 1;
            refusedIndex = -1;
        }
    }

    // Suspend releases all file handles but keeps successful cold snapshots,
    // including the current publication until its caller disposes it. Repeated
    // calls do no additional IO and never restart workers inside a callback.
    internal void SuspendAfter(int index)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            if (index < 0 || index != lastRequested)
                throw new ArgumentOutOfRangeException(nameof(index), "Suspend must follow the current requested index.");
            // Ordered retained reuse reopens its publication stream without
            // restarting workers. Close that stream at the next callback too.
            if (suspended && held.Values.All(item => item.Stream == null)) return;
        }
        SuspendBoundary(index);
    }

    private bool Revalidate(Lease item)
    {
        FileStream? stream = null;
        long started = Stopwatch.GetTimestamp();
        try
        {
            byte[] source = item.Source;
            stream = new FileStream(item.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length != source.Length) return false;
            // The source snapshot already belongs to this reservation. This
            // scratch fits within its charged RowOverheadBytes; no second
            // source-sized array or metadata identity shortcut is needed.
            byte[] scratch = new byte[Math.Min(64 * 1024, source.Length)];
            int offset = 0;
            while (offset < source.Length)
            {
                int read = stream.Read(scratch, 0, Math.Min(scratch.Length, source.Length - offset));
                if (read == 0) return false;
                int i = 0;
                // Compare complete words only inside this actual read. Both
                // arrays use the same byte order; short reads and the final
                // partial word still compare every remaining real byte.
                for (; i <= read - sizeof(long); i += sizeof(long))
                    if (BitConverter.ToInt64(scratch, i) != BitConverter.ToInt64(source, offset + i)) return false;
                for (; i < read; i++)
                    if (scratch[i] != source[offset + i]) return false;
                offset += read;
            }
            if (stream.ReadByte() != -1) return false;
            item.Stream = stream;
            stream = null;
            item.NeedsRevalidation = false;
            item.WasRevalidated = true;
            lock (gate) revalidated++;
            return true;
        }
        catch (Exception) { return false; }
        finally
        {
            stream?.Dispose();
            lock (gate)
            {
                ownerPreparationTicks += Stopwatch.GetTimestamp() - started;
                if (item.NeedsRevalidation) rejectedRetained++;
            }
        }
    }

    // The ordered caller invokes this before a store/source mutation, outside
    // that store's lock. Release even the current publication and discard the
    // speculative horizon, but retain idle workers for freshly planned items.
    internal void DrainAfter(int index)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            if (index < 0 || index != lastRequested)
                throw new ArgumentOutOfRangeException(nameof(index), "Drain must follow the current requested index.");
        }
        DrainBoundary(index);
    }

    // A nested reload may interrupt a publication whose pixels/source are still
    // referenced by the outer caller's stack. Retire speculative work completely,
    // but charge that current publication until its caller releases it. Nested
    // callers must independently suppress queue admission while this is held.
    internal void RetireSpeculationAfter(int index)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            if (index < 0 || index != lastRequested)
                throw new ArgumentOutOfRangeException(nameof(index), "Retirement must follow the current requested index.");
        }
        Quiesce();
        lock (gate)
        {
            foreach (Lease item in held.Values.ToArray())
            {
                if (item != publication) item.Dispose();
                else
                {
                    item.Stream?.Dispose();
                    item.Stream = null;
                }
            }
            nextIndex = index + 1;
            refusedIndex = -1;
        }
    }

    private void Fill()
    {
        while (nextIndex < files.Count)
        {
            lock (gate)
            {
                ThrowIfDisposed();
                if (suspended) return;
                if (refusedIndex >= 0) return;
                if (barriers != null && barriers[nextIndex]) return;
                if (held.ContainsKey(nextIndex)) { nextIndex++; continue; }
                if (held.Count >= maximumJobs) return;
            }
            FileInfo? file = files[nextIndex];
            if (file == null) { refusedIndex = nextIndex; return; }
            FileStream? stream = null;
            long preparing = Stopwatch.GetTimestamp();
            try
            {
                // Warm metadata may supply a conservative source-size hint
                // without opening the file here. A null unopened plan retains
                // the original C06 stream/header estimator path unchanged.
                WorkItem? recipe = unopenedPlan?.Invoke(nextIndex);
                long sourceLength;
                if (recipe != null) sourceLength = recipe.ExpectedSourceBytes ?? 0;
                else
                {
                    // Share.Read blocks replacement and writers through
                    // publication on Windows. Probe this exact open source.
                    stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                    sourceLength = stream.Length;
                    if (sourceLength < 1 || sourceLength > maximumSourceBytes) { refusedIndex = nextIndex; return; }
                    recipe = plan == null
                        ? new WorkItem(estimateDecodedBytes(stream), decode)
                        : plan(nextIndex, stream);
                    stream.Position = 0;
                }
                if (sourceLength < 1 || sourceLength > maximumSourceBytes) { refusedIndex = nextIndex; return; }
                if (recipe == null || recipe.RequiredBytes < 1 || recipe.RequiredBytes > maximumDecodedBytes)
                { refusedIndex = nextIndex; return; }
                long reservation = checked(sourceLength + recipe.RequiredBytes + RowOverheadBytes);
                if (reservation > maximumBytes) { refusedIndex = nextIndex; return; }
                lock (gate)
                {
                    ThrowIfDisposed();
                    if (reservation > maximumBytes - heldBytes) return;
                    var job = new Lease(this, nextIndex++, file.FullName, stream, (int)sourceLength, reservation,
                        recipe.Decode, recipe.RetainAcrossCallbacks);
                    held.Add(job.Index, job);
                    pending.Enqueue(job);
                    stream = null; // owned by job until it is released
                    heldBytes += reservation;
                    maxReservedBytes = Math.Max(maxReservedBytes, heldBytes);
                    scheduled++;
                    Monitor.PulseAll(gate);
                }
            }
            catch (Exception)
            {
                lock (gate)
                {
                    if (disposed) throw;
                    failed++;
                }
                refusedIndex = nextIndex; // native caller retains its own normal error path
                return;
            }
            finally
            {
                stream?.Dispose();
                lock (gate) ownerPreparationTicks += Stopwatch.GetTimestamp() - preparing;
            }
        }
    }

    private void Work()
    {
        while (true)
        {
            Lease job;
            lock (gate)
            {
                while (!disposed && (suspended || pending.Count == 0)) Monitor.Wait(gate);
                if (disposed) return;
                job = pending.Dequeue();
                activeWorkers++;
                maxActiveWorkers = Math.Max(maxActiveWorkers, activeWorkers);
            }
            long opening = 0, reading = 0, decoding = 0;
            try
            {
                CancellationToken token = cancellation.Token;
                token.ThrowIfCancellationRequested();
                if (job.Stream == null)
                {
                    long started = Stopwatch.GetTimestamp();
                    try
                    {
                        job.Stream = new FileStream(job.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        // A hint only reserves capacity. Check this exact leased
                        // file before allocation; unchanged size or timestamp
                        // never replaces the private recipe's content identity.
                        long actualLength = job.Stream.Length;
                        if (actualLength < 1 || actualLength > maximumSourceBytes || actualLength != job.SourceLength)
                            throw new InvalidDataException("Image source size changed before its leased read.");
                    }
                    finally { opening = Stopwatch.GetTimestamp() - started; }
                }
                token.ThrowIfCancellationRequested();
                byte[] source;
                long readStarted = Stopwatch.GetTimestamp();
                try
                {
                    source = new byte[job.SourceLength];
                    job.Source = source;
                    int offset = 0;
                    while (offset < source.Length)
                    {
                        token.ThrowIfCancellationRequested();
                        int read = job.Stream.Read(source, offset, Math.Min(64 * 1024, source.Length - offset));
                        if (read == 0) throw new EndOfStreamException("Image source ended during its leased read.");
                        offset += read;
                    }
                }
                finally { reading = Stopwatch.GetTimestamp() - readStarted; }
                job.SourceComplete = true;
                T value;
                long decodeStarted = Stopwatch.GetTimestamp();
                try { value = job.Decode(source, token); }
                finally { decoding = Stopwatch.GetTimestamp() - decodeStarted; }
                token.ThrowIfCancellationRequested();
                if (value == null) throw new InvalidDataException("Image decoder returned no private CPU result.");
                lock (gate) { job.Value = value; decoded++; }
            }
            catch (Exception error)
            {
                lock (gate)
                {
                    job.Error = error;
                    if (!(error is OperationCanceledException)) failed++;
                }
            }
            finally
            {
                lock (gate)
                {
                    job.Done = true;
                    workerOpenTicks += opening;
                    workerReadTicks += reading;
                    workerDecodeTicks += decoding;
                    activeWorkers--;
                    Monitor.PulseAll(gate);
                }
            }
        }
    }

    private void WaitFor(Lease lease)
    {
        lock (gate)
        {
            while (!lease.Done && !disposed) Monitor.Wait(gate);
            ThrowIfDisposed();
        }
    }

    private void Release(Lease lease)
    {
        lock (gate)
        {
            if (lease.Released) return;
            if (!lease.Done && !disposed) throw new InvalidOperationException("Cannot release an active image job.");
            lease.Released = true;
            lease.Source = Array.Empty<byte>();
            lease.SourceComplete = false;
            lease.Value = null;
            lease.Error = null;
            lease.Stream?.Dispose();
            lease.Stream = null;
            held.Remove(lease.Index);
            heldBytes -= lease.Reservation;
            if (publication == lease) publication = null;
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed) throw new ObjectDisposedException(nameof(FirstBuildImageQueue<T>));
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            Monitor.PulseAll(gate);
        }
        try { cancellation.Cancel(); }
        finally
        {
            // There is no timed abandon: no stream or decoded buffer is released
            // until all workers have stopped touching it.
            foreach (Thread worker in workers) worker.Join();
            lock (gate)
            {
                pending.Clear();
                foreach (Lease lease in held.Values.ToArray()) Release(lease);
            }
            cancellation.Dispose();
        }
    }

    // RequiredBytes reserves all simultaneously live result buffers, decode
    // scratch and extra copies. The queue additionally reserves its source
    // byte array and per-row overhead, including while completed results wait.
    internal sealed class WorkItem
    {
        internal long RequiredBytes { get; }
        // Required only for unopened admission. This is an allocation bound,
        // not an identity check; workers reject a different actual length.
        internal long? ExpectedSourceBytes { get; }
        internal Func<byte[], CancellationToken, T> Decode { get; }
        internal bool RetainAcrossCallbacks { get; }

        internal WorkItem(long requiredBytes, Func<byte[], CancellationToken, T> decode, long? expectedSourceBytes = null,
            bool retainAcrossCallbacks = true)
        {
            RequiredBytes = requiredBytes;
            ExpectedSourceBytes = expectedSourceBytes;
            RetainAcrossCallbacks = retainAcrossCallbacks;
            Decode = decode ?? throw new ArgumentNullException(nameof(decode));
        }
    }

    internal sealed class Lease : IDisposable
    {
        private readonly FirstBuildImageQueue<T> owner;
        internal readonly int Index, SourceLength;
        internal readonly long Reservation;
        internal readonly string SourcePath;
        internal FileStream? Stream;
        internal readonly Func<byte[], CancellationToken, T> Decode;
        internal readonly bool RetainAcrossCallbacks;
        internal bool Done, Released, WasConsumed;
        internal bool NeedsRevalidation, WasRevalidated;
        internal byte[] Source { get; set; } = Array.Empty<byte>();
        // A decoder refusal may still leave a complete, leased source snapshot
        // that the ordered caller can use for its ordinary fallback without IO.
        internal bool SourceComplete { get; set; }
        internal T? Value { get; set; }
        internal Exception? Error { get; set; }

        internal Lease(FirstBuildImageQueue<T> owner, int index, string sourcePath, FileStream? stream, int sourceLength,
            long reservation, Func<byte[], CancellationToken, T> decode, bool retainAcrossCallbacks)
        {
            this.owner = owner;
            Index = index;
            SourcePath = sourcePath;
            Stream = stream;
            SourceLength = sourceLength;
            Reservation = reservation;
            Decode = decode;
            RetainAcrossCallbacks = retainAcrossCallbacks;
        }

        // Only publication of this decoded result counts as consumed; native
        // fallback or an independently loaded store hit must not receipt it.
        internal void MarkConsumed()
        {
            lock (owner.gate)
            {
                if (Released || !Done || Value == null || Error != null)
                    throw new InvalidOperationException("Only a live successful decode can be consumed.");
                if (!WasConsumed) { WasConsumed = true; owner.consumed++; }
            }
        }

        public void Dispose() => owner.Release(this);
    }
}
