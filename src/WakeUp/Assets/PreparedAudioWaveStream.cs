// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using NAudio.Wave;

namespace WakeUp;

// No registration or owner callbacks under this lock. The owner associates the
// real clip separately and settles this stream before closing the native handle.
internal sealed class PreparedAudioWaveStream : WaveStream
{
    private readonly object gate = new();
    private readonly Func<WaveStream>? create;
    private readonly bool warm;
    private const int TraceLimit = 64, DigestBytesPerRead = 256;
    private readonly TraceEntry[] trace = new TraceEntry[TraceLimit];
    private int traceCount, firstReconstructionThread;
    private long operationCount, readCalls, returnedBytes, positionGets, positionSets;
    private string firstReconstructionCause = "";
    private struct TraceEntry
    {
        internal int Kind, Thread, Offset, Count, Returned, SampledBytes;
        internal long Ordinal, Value;
        internal uint Digest;
        internal bool Completed;
    }
    private PreparedAudioData? prepared, recording;
    private WaveStream? native;
    private WaveFormat? preparedFormat;
    private bool disposed, formatObserved, lengthObserved, recordingAbandoned;
    private int served, recordingSize = PreparedAudioData.HeaderBytes;
    private ExceptionDispatchInfo? failure;
    internal long CachedBytes { get; private set; }
    internal long ReplayedBytes { get; private set; }
    internal long RecordedBytes { get; private set; }
    internal int ReplayMismatches { get; private set; }
    internal int DecoderConstructions { get; private set; }
    internal int InitializationAttempts { get; private set; }
    internal bool IsNative { get { lock (gate) return native != null; } }
    internal bool TerminallyFaulted { get { lock (gate) return failure != null; } }
    internal bool Settled { get { lock (gate) return disposed || failure != null || native != null; } }
    internal bool RecordingAbandoned { get { lock (gate) return recordingAbandoned; } }

    internal PreparedAudioWaveStream(WaveStream native)
    {
        this.native = native ?? throw new ArgumentNullException(nameof(native));
        recording = new PreparedAudioData();
        DecoderConstructions = InitializationAttempts = 1;
    }

    internal PreparedAudioWaveStream(PreparedAudioData data, Func<WaveStream> create)
        : this(data, create, false) { }

    // Cache.Read returns a newly decoded private object, retained by no cache
    // or caller after this handoff. Other callers keep the copying constructor.
    internal static PreparedAudioWaveStream FromOwnedData(PreparedAudioData data, Func<WaveStream> create)
        => new(data, create, true);

    private PreparedAudioWaveStream(PreparedAudioData data, Func<WaveStream> create, bool takeOwnership)
    {
        data.Validate();
        prepared = takeOwnership ? data : data.Copy();
        this.create = create ?? throw new ArgumentNullException(nameof(create));
        warm = true;
        preparedFormat = prepared.RestoreFormat();
    }

    public override WaveFormat WaveFormat
    {
        get
        {
            lock (gate)
            {
                RequireAlive();
                if (native == null) { formatObserved = true; return preparedFormat!; }
                WaveFormat value;
                try { value = native.WaveFormat; }
                catch { AbandonRecording(); throw; }
                if (recording != null)
                {
                    if (formatObserved && !FormatMatches(recording, value)) AbandonRecording();
                    else if (!formatObserved)
                    {
                        // Do not repair unfamiliar or inconsistent native
                        // metadata. Only optional recording is abandoned.
                        if (!recording.CaptureFormat(value)) AbandonRecording();
                        else
                        {
                            recordingSize += recording.ExtraData.Length;
                            if (recordingSize > PreparedAudioData.MaximumBytes) AbandonRecording();
                        }
                    }
                }
                formatObserved = true;
                return value;
            }
        }
    }

    public override long Length
    {
        get
        {
            lock (gate)
            {
                RequireAlive();
                if (native == null) { lengthObserved = true; return prepared!.Length; }
                long value;
                try { value = native.Length; }
                catch { AbandonRecording(); throw; }
                if (recording != null)
                {
                    if (value < 1 || lengthObserved && recording.Length != value) AbandonRecording();
                    else recording.Length = value;
                }
                lengthObserved = true;
                return value;
            }
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        lock (gate)
        {
            readCalls++; operationCount++;
            int traceIndex = traceCount < TraceLimit ? traceCount++ : -1;
            if (traceIndex >= 0) trace[traceIndex] = new TraceEntry { Kind = 0, Ordinal = operationCount,
                Thread = Thread.CurrentThread.ManagedThreadId, Offset = offset, Count = count };
            RequireAlive();
            int returned;
            var op = Next(PreparedAudioData.OperationKind.Read);
            if (op != null && op.Offset == offset && op.Count == count && buffer != null
                && offset >= 0 && offset <= buffer.Length && op.Returned <= buffer.Length - offset)
            {
                Buffer.BlockCopy(op.Bytes, 0, buffer, offset, op.Returned);
                served++; CachedBytes += op.Returned;
                returned = op.Returned;
            }
            else
            {
                EnsureNative("read");
                try { returned = native!.Read(buffer, offset, count); }
                catch { AbandonRecording(); throw; }
                if (recording != null)
                {
                    if (offset < 0 || count < 0 || (long)offset + count > PreparedAudioData.MaximumBytes
                        || returned < 0 || returned > count || !CanRecord(returned)) AbandonRecording();
                    else
                    {
                        var bytes = new byte[returned];
                        Buffer.BlockCopy(buffer, offset, bytes, 0, returned);
                        Record(new PreparedAudioData.Operation { Kind = PreparedAudioData.OperationKind.Read,
                            Offset = offset, Count = count, Returned = returned, Bytes = bytes });
                        RecordedBytes += returned;
                    }
                }
            }
            returnedBytes += returned;
            if (traceIndex >= 0)
            {
                // Sample only the returned prefix, once per retained operation.
                // No diagnostic delegates, providers, logging or Harmony calls.
                uint digest = 2166136261;
                int sampled = buffer != null && offset >= 0 && offset <= buffer.Length && returned >= 0 && returned <= buffer.Length - offset
                    ? Math.Min(returned, DigestBytesPerRead) : 0;
                for (int i = 0; i < sampled; i++) digest = unchecked((digest ^ buffer![offset + i]) * 16777619);
                trace[traceIndex].Returned = returned;
                trace[traceIndex].SampledBytes = sampled;
                trace[traceIndex].Digest = digest;
                trace[traceIndex].Completed = true;
            }
            return returned;
        }
    }

    public override long Position
    {
        get
        {
            lock (gate)
            {
                positionGets++; operationCount++;
                int traceIndex = traceCount < TraceLimit ? traceCount++ : -1;
                if (traceIndex >= 0) trace[traceIndex] = new TraceEntry { Kind = 1, Ordinal = operationCount, Thread = Thread.CurrentThread.ManagedThreadId };
                RequireAlive();
                var op = Next(PreparedAudioData.OperationKind.GetPosition);
                long value;
                if (op != null) { served++; value = op.Value; }
                else
                {
                    EnsureNative("position-get");
                    try { value = native!.Position; }
                    catch { AbandonRecording(); throw; }
                    if (recording != null) Record(new PreparedAudioData.Operation { Kind = PreparedAudioData.OperationKind.GetPosition, Value = value });
                }
                if (traceIndex >= 0) { trace[traceIndex].Value = value; trace[traceIndex].Completed = true; }
                return value;
            }
        }
        set
        {
            lock (gate)
            {
                positionSets++; operationCount++;
                int traceIndex = traceCount < TraceLimit ? traceCount++ : -1;
                if (traceIndex >= 0) trace[traceIndex] = new TraceEntry { Kind = 2, Ordinal = operationCount, Thread = Thread.CurrentThread.ManagedThreadId, Value = value };
                RequireAlive();
                var op = Next(PreparedAudioData.OperationKind.SetPosition);
                if (op != null && op.Value == value) served++;
                else
                {
                    EnsureNative("position-set");
                    try { native!.Position = value; }
                    catch { AbandonRecording(); throw; }
                    if (recording != null) Record(new PreparedAudioData.Operation { Kind = PreparedAudioData.OperationKind.SetPosition, Value = value });
                }
                if (traceIndex >= 0) trace[traceIndex].Completed = true;
            }
        }
    }

    // Freeze the native loading transcript: at Manager return for streaming,
    // or original outer-reader disposal after the nonstreaming worker finishes.
    // Later operations remain native and do not extend the cache entry.
    internal PreparedAudioData? FinishRecording()
    {
        lock (gate)
        {
            var result = recording; recording = null;
            if (disposed || result == null || !formatObserved || !lengthObserved) return null;
            // The recording is detached above while holding its only owner's
            // lock. Native input buffers were copied at capture; no alias to
            // this transcript survives in the reader or native decoder.
            try { result.Validate(); return result; }
            catch (InvalidDataException) { recordingAbandoned = true; return null; }
        }
    }

    // Total at owner barriers, including repeated calls after terminal failure
    // or disposal. Actual callback calls still rethrow the original failure.
    internal void SettleNative() => SettleNativeOutcome();

    // Shared bootstrap protocol: 1 native-ready, 2 original failure latched,
    // 3 disposed, 0 still pending. Determine the result under the same lock as
    // construction; never turn an arbitrary callback exception into a fault.
    internal int SettleNativeOutcome()
    {
        lock (gate)
        {
            if (!disposed && failure == null && native == null)
            {
                try { EnsureNative("settlement"); }
                catch when (failure != null) { }
            }
            return disposed ? 3 : failure != null ? 2 : native != null ? 1 : 0;
        }
    }

    // Native consumers already selected this clip. Prepare the existing lower
    // reader before passing that same clip to Unity; do not advance its public
    // read/seek history. Any native error stays latched for the later callback.
    internal bool PrepareForPlayback(string cause)
    {
        lock (gate)
        {
            if (disposed || failure != null || native != null) return false;
            try { EnsureNative("readiness-" + cause); }
            catch when (failure != null) { }
            return true;
        }
    }

    private PreparedAudioData.Operation? Next(PreparedAudioData.OperationKind kind)
        => native == null && prepared != null && served < prepared.Operations.Count && prepared.Operations[served].Kind == kind
            ? prepared.Operations[served] : null;

    private void EnsureNative(string cause)
    {
        RequireAlive();
        if (native != null) return;
        WaveStream? next = null;
        InitializationAttempts++;
        firstReconstructionThread = Thread.CurrentThread.ManagedThreadId;
        firstReconstructionCause = cause;
        // Do not call diagnostic hooks while this reader is unsettled. A hook
        // can publish a decoder patch and recursively settle this same reader.
        // The enclosing native loading observations remain available.
        try
        {
            next = create!() ?? throw new InvalidOperationException("Native reader factory returned null.");
            DecoderConstructions++;
            if (formatObserved)
            {
                WaveFormat format = next.WaveFormat;
                if (!FormatMatches(prepared!, format)) ReplayMismatches++;
            }
            if (lengthObserved && next.Length != prepared!.Length) ReplayMismatches++;
            for (int i = 0; i < served; i++)
            {
                var op = prepared!.Operations[i];
                if (op.Kind == PreparedAudioData.OperationKind.SetPosition) next.Position = op.Value;
                else if (op.Kind == PreparedAudioData.OperationKind.GetPosition)
                { if (next.Position != op.Value) ReplayMismatches++; }
                else
                {
                    var scratch = new byte[op.Offset + op.Count];
                    int returned = next.Read(scratch, op.Offset, op.Count);
                    ReplayedBytes += returned;
                    if (returned != op.Returned || !scratch.Skip(op.Offset).Take(op.Returned).SequenceEqual(op.Bytes)) ReplayMismatches++;
                }
            }
            native = next;
            prepared = null; preparedFormat = null;
        }
        catch (Exception exception)
        {
            failure = ExceptionDispatchInfo.Capture(exception);
            prepared = null; preparedFormat = null;
            try { next?.Dispose(); } catch { /* Keep the originating native error. */ }
            failure.Throw(); throw;
        }
    }

    private static bool FormatMatches(PreparedAudioData data, WaveFormat format)
        => data.FormatMatches(format);

    // No native properties or demand are invoked. Returned rows are copies,
    // and counters continue after the bounded trace has filled. Failed calls
    // count as attempts and retain completed=false instead of pretending EOF.
    internal Dictionary<string, object> Snapshot()
    {
        TraceEntry[] rows;
        Dictionary<string, object> result;
        lock (gate)
        {
            rows = new TraceEntry[traceCount];
            Array.Copy(trace, rows, traceCount);
            result = new Dictionary<string, object>
            {
                ["warm"] = warm, ["constructorCompleted"] = DecoderConstructions != 0,
                ["nativeReady"] = native != null, ["disposed"] = disposed, ["terminallyFaulted"] = failure != null,
                ["initializationAttempts"] = InitializationAttempts, ["decoderConstructions"] = DecoderConstructions,
                ["firstReconstructionThread"] = firstReconstructionThread, ["firstReconstructionCause"] = firstReconstructionCause,
                ["readCalls"] = readCalls, ["returnedBytes"] = returnedBytes, ["positionGets"] = positionGets, ["positionSets"] = positionSets,
                ["cachedBytes"] = CachedBytes, ["replayedBytes"] = ReplayedBytes, ["replayMismatches"] = ReplayMismatches,
                ["traceLimit"] = TraceLimit, ["digestBytesPerRead"] = DigestBytesPerRead,
                ["operationCount"] = operationCount, ["traceOmitted"] = operationCount - traceCount
            };
        }
        result["trace"] = rows.Select(row => (object)new Dictionary<string, object>
        {
            ["kind"] = row.Kind == 0 ? "read" : row.Kind == 1 ? "position-get" : "position-set",
            ["ordinal"] = row.Ordinal, ["thread"] = row.Thread, ["offset"] = row.Offset, ["count"] = row.Count,
            ["returned"] = row.Returned, ["sampledBytes"] = row.SampledBytes, ["digest"] = row.Digest.ToString("x8", CultureInfo.InvariantCulture),
            ["value"] = row.Value, ["completed"] = row.Completed
        }).ToArray();
        return result;
    }

    private bool CanRecord(int bytes) => recording != null && recording.Operations.Count < PreparedAudioData.MaximumOperations
        && (long)recordingSize + PreparedAudioData.OperationBytes + bytes <= PreparedAudioData.MaximumBytes;
    private void Record(PreparedAudioData.Operation op)
    {
        if (!CanRecord(op.Bytes.Length)) { AbandonRecording(); return; }
        recording!.Operations.Add(op); recordingSize += PreparedAudioData.OperationBytes + op.Bytes.Length;
    }
    private void AbandonRecording() { if (recording != null) { recordingAbandoned = true; recording = null; } }
    private void RequireAlive()
    {
        if (disposed) throw new ObjectDisposedException(nameof(PreparedAudioWaveStream));
        failure?.Throw();
    }
    protected override void Dispose(bool disposing)
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true; recording = null; prepared = null; preparedFormat = null;
            var previous = native; native = null;
            if (disposing) previous?.Dispose();
        }
        base.Dispose(disposing);
    }
}
