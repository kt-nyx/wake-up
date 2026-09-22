// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using NAudio.Wave;

namespace WakeUp;

// The original reader and selected ACM decompressor remain alive. Only exact
// frame/reset operations can be skipped; native Read/Position own all leftovers.
internal static class PreparedMp3FramesRuntime
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo Decoder = typeof(Mp3FileReader).GetField("decompressor", Fields)!;
    private static readonly FieldInfo Position = typeof(Mp3FileReader).GetField("position", Fields)!;
    private static readonly FieldInfo Samples = typeof(Mp3FileReader).GetField("totalSamples", Fields)!;
    private static readonly FieldInfo SampleBytes = typeof(Mp3FileReader).GetField("bytesPerSample", Fields)!;
    private static readonly ConditionalWeakTable<object, Session> sessions = new();
    private static readonly ConditionalWeakTable<object, Session> readers = new();
    private static PreparedAudioCache? cache;
    private static Dictionary<string, object>? registry;
    private static bool enabled;
    [ThreadStatic] private static int replaying;
    internal static readonly MethodInfo FrameTarget = typeof(AcmMp3FrameDecompressor).GetMethod("DecompressFrame")!;
    internal static readonly MethodInfo ResetTarget = typeof(AcmMp3FrameDecompressor).GetMethod("Reset")!;
    internal static readonly MethodInfo DisposeTarget = typeof(AcmMp3FrameDecompressor).GetMethod("Dispose")!;

    internal static bool Initialize(PreparedAudioCache store, Dictionary<string, object> state, bool nativeEntry)
    {
        cache = store; registry = state;
        return enabled = nativeEntry && PreparedMp3Provider.Initialize();
    }

    internal static Session? Attach(WaveStream reader, string sourceKey)
    {
        if (!enabled || reader.GetType() != typeof(Mp3FileReader)) return null;
        object decoder = Decoder.GetValue(reader)!;
        if (decoder?.GetType() != typeof(AcmMp3FrameDecompressor)) return null;
        lock (registry!["audioRegistryGate"])
        {
            if (!Allowed() || ((Dictionary<object, Func<int>>)registry["audioPending"]).Count >= PreparedAudioRuntime.MaximumReaders) return null;
            var session = new Session((Mp3FileReader)reader, (AcmMp3FrameDecompressor)decoder, sourceKey, (long)registry["audioEpoch"]);
            sessions.Add(decoder, session); readers.Add(reader, session);
            ((Dictionary<object, Func<int>>)registry["audioPending"]).Add(session, session.Settle);
            return session;
        }
    }

    private static bool Allowed() => enabled && !(bool)registry!["audioNativeOnly"] && !(bool)registry["audioAdmissionClosed"];
    internal static bool BeforeFrame(object instance, Mp3Frame frame, byte[] destination, int offset, ref int result, out Call? state)
    {
        state = Enter(instance);
        if (state == null) return true;
        try { return state.Owner.Frame(frame, destination, offset, ref result, state); }
        catch { Monitor.Exit(state.Owner.Gate); state = null; throw; }
    }
    internal static bool BeforeReset(object instance, out Call? state)
    {
        state = Enter(instance);
        if (state == null) return true;
        try { return state.Owner.Reset(state); }
        catch { Monitor.Exit(state.Owner.Gate); state = null; throw; }
    }
    internal static void BeforeDispose(object instance, out Call? state)
    {
        state = Enter(instance, disposing: true);
        if (state == null) return;
        // Closing a complete cache hit releases the retained stream without
        // decoding skipped history. Native Dispose still owns buffers/handles.
        state.Disposing = true;
        state.Owner.History.Clear(); state.Owner.HistoryBytes = 0;
    }
    private static Call? Enter(object instance, bool disposing = false)
    {
        if (replaying != 0 || !sessions.TryGetValue(instance, out var session)) return null;
        lock (registry!["audioRegistryGate"])
        {
            Monitor.Enter(session.Gate);
            try
            {
                if (!disposing && (!Allowed() || (long)registry["audioEpoch"] != session.Epoch)) session.CatchUp();
                return new Call(session);
            }
            catch { Monitor.Exit(session.Gate); throw; }
        }
    }
    internal static Exception? After(Exception? error, Call? state, int result = 0)
    {
        if (state == null) return error;
        try { state.Owner.After(state, error, result); }
        finally { Monitor.Exit(state.Owner.Gate); }
        if (state.Disposing)
            lock (registry!["audioRegistryGate"])
                ((Dictionary<object, Func<int>>)registry["audioPending"]).Remove(state.Owner);
        // The original consumer exception always wins over optional recording.
        return error;
    }
    internal static Dictionary<string, object>? Snapshot(WaveStream? reader)
        => reader != null && readers.TryGetValue(reader, out var session) ? session.Snapshot() : null;
    internal static void Close() { enabled = false; PreparedMp3Provider.Stop(); cache = null; }

    private sealed class MathQualificationException : Exception
    { internal MathQualificationException(string message) : base(message) { } }

    internal sealed class Call
    {
        internal readonly Session Owner;
        internal PreparedMp3FrameData.Operation? Operation;
        internal byte[]? Destination;
        internal int Offset;
        internal bool First, Observing, MathObserving, Skipped, Disposing;
        internal Call(Session owner) { Owner = owner; }
    }
    internal sealed class Session
    {
        internal readonly object Gate = new();
        internal readonly long Epoch;
        private Mp3FileReader? reader;
        private AcmMp3FrameDecompressor? decoder;
        private readonly int decoderIdentity;
        private readonly string sourceKey;
        private PreparedMp3FrameData.Writer? writer;
        private PreparedMp3FrameData.Reader? tape;
        private ExceptionDispatchInfo? failure;
        private bool first = true, nativeOnly, disposed, published, warm;
        private string provider = "", refusal = "", tapeKey = "";
        private ulong token;
        private long nativeFrames, cachedFrames, cachedBytes, replayFrames, resets;
        internal readonly Queue<PreparedMp3FrameData.Operation> History = new();
        internal int HistoryBytes;

        internal Session(Mp3FileReader reader, AcmMp3FrameDecompressor decoder, string key, long epoch)
        { this.reader = reader; this.decoder = decoder; decoderIdentity = RuntimeHelpers.GetHashCode(decoder); sourceKey = key; Epoch = epoch; }

        internal bool Frame(Mp3Frame frame, byte[] destination, int offset, ref int result, Call call)
        {
            failure?.Throw();
            if (nativeOnly || disposed) { nativeFrames++; return true; }
            byte[]? compressed = null;
            try
            {
                // A malformed frame or destination must take the original path:
                // native conversion may happen before its destination exception.
                if (frame != null && frame.RawData != null && frame.FrameLength > 0
                    && frame.FrameLength <= frame.RawData.Length && frame.FrameLength <= 65536)
                    compressed = frame.RawData.Take(frame.FrameLength).ToArray();
            }
            catch { /* optional admission only */ }
            if (compressed == null) { CatchUp(); return true; }
            call.Operation = new PreparedMp3FrameData.Operation { Kind = PreparedMp3FrameData.OperationKind.Frame,
                Compressed = compressed, DestinationOffset = offset };
            call.Destination = destination; call.Offset = offset;
            if (first)
            {
                call.First = true;
                call.Observing = PreparedMp3Provider.Begin(decoder!, frame!.FrameLength);
                call.MathObserving = call.Observing && PreparedMp3Provider.MathBegin(decoder!, call.Operation.Math);
                nativeFrames++; return true;
            }
            if (tape != null)
            {
                var op = tape.Next();
                if (op != null
                    && op.Kind == PreparedMp3FrameData.OperationKind.Frame && op.Compressed.SequenceEqual(compressed)
                    && destination != null && offset >= 0 && offset <= destination.Length - op.Pcm.Length
                    && History.Count < PreparedMp3FrameData.MaximumOperations
                    && HistoryBytes + compressed.Length + 64 <= PreparedMp3FrameData.MaximumReplayBytes)
                {
                    // Validate native source-copy capacity even on a cache hit.
                    if (PreparedMp3Provider.SourceCapacity(decoder!) >= compressed.Length && PreparedMp3Provider.TryCached(decoder!, token, op.Math))
                    {
                        History.Enqueue(new PreparedMp3FrameData.Operation { Kind = op.Kind, Compressed = compressed, Math = op.Math });
                        HistoryBytes += compressed.Length + 64;
                        Array.Copy(op.Pcm, 0, destination, offset, op.Pcm.Length);
                        result = op.Pcm.Length; cachedFrames++; cachedBytes += result;
                        call.Skipped = true; return false;
                    }
                    refusal = PreparedMp3Provider.CacheRefusal();
                }
                else refusal = op == null ? "cache-operation-missing-or-corrupt" : "cache-operation-mismatch";
                CatchUp(); call.Operation = null;
            }
            if (writer != null && call.Operation != null)
                call.MathObserving = PreparedMp3Provider.MathBegin(decoder!, call.Operation.Math);
            nativeFrames++; return true;
        }

        internal bool Reset(Call call)
        {
            failure?.Throw(); resets++;
            if (first || nativeOnly || disposed) return true;
            call.Operation = new PreparedMp3FrameData.Operation { Kind = PreparedMp3FrameData.OperationKind.Reset };
            if (tape == null) return true;
            var op = tape.Next();
            if (PreparedMp3Provider.CurrentBase(decoder!, token) && op?.Kind == PreparedMp3FrameData.OperationKind.Reset
                && History.Count < PreparedMp3FrameData.MaximumOperations && HistoryBytes + 64 <= PreparedMp3FrameData.MaximumReplayBytes)
            { History.Enqueue(op); HistoryBytes += 64; call.Skipped = true; return false; }
            CatchUp(); call.Operation = null; return true;
        }

        internal void After(Call call, Exception? error, int result)
        {
            if (call.Disposing)
            {
                disposed = true;
                try
                {
                    // No public getter is invoked on completion. The original
                    // reader's byte position proves all advertised PCM was read.
                    bool complete = (long)Position.GetValue(reader)! >= checked((long)Samples.GetValue(reader)! * (int)SampleBytes.GetValue(reader)!);
                    if (error == null && complete && !nativeOnly) published = writer?.Complete() == true;
                }
                catch { }
                writer?.Dispose(); writer = null; tape = null;
                // Native managers may retain destroyed clip keys. A diagnostic
                // session must not keep disposed readers or their buffers alive.
                reader = null; decoder = null;
                return;
            }
            if (call.Skipped) return;
            try
            {
                bool mathProven = call.MathObserving && PreparedMp3Provider.MathEnd(call.Operation!.Math);
                if (call.First)
                {
                    first = false;
                    bool observed = call.Observing && PreparedMp3Provider.End(out token, out provider, out refusal);
                    if (error != null || !observed || !mathProven) { nativeOnly = true; if (!mathProven) refusal = "first-math-unproved"; return; }
                    string key;
                    using (var hash = new SHA256Managed()) key = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(sourceKey + "|mp3-frames-v2|" + provider))).Replace("-", "");
                    tapeKey = key;
                    tape = cache?.OpenMp3Tape(key); warm = tape != null;
                    if (tape == null) writer = cache?.CreateMp3TapeWriter(key);
                    else
                    {
                        var firstOp = tape.Next();
                        if (firstOp == null || firstOp.Kind != PreparedMp3FrameData.OperationKind.Frame
                            || !firstOp.Compressed.SequenceEqual(call.Operation!.Compressed)
                            || (firstOp.Math[1] & ~63UL) != (call.Operation.Math[1] & ~63UL)
                            || firstOp.Pcm.Length != result || !firstOp.Pcm.SequenceEqual(call.Destination!.Skip(call.Offset).Take(result)))
                        { nativeOnly = true; tape = null; refusal = "first-frame-mismatch"; }
                    }
                }
                if (error != null) { Abandon(); return; }
                if (call.Operation?.Kind == PreparedMp3FrameData.OperationKind.Frame && writer != null && !mathProven)
                { refusal = "recording-math-unproved"; Abandon(); return; }
                if (call.Operation != null && writer != null)
                {
                    if (call.Operation.Kind == PreparedMp3FrameData.OperationKind.Frame)
                        call.Operation.Pcm = call.Destination!.Skip(call.Offset).Take(result).ToArray();
                    if (!writer.Append(call.Operation)) Abandon();
                }
            }
            catch { Abandon(); }
        }

        private void Abandon() { nativeOnly = true; writer?.Dispose(); writer = null; tape = null; }
        internal void CatchUp()
        {
            if (disposed) return;
            failure?.Throw();
            // The retained native entry stays pinned. Historical arithmetic is
            // scoped inside its original conversion, never around managed code.
            if (History.Count != 0 && !PreparedMp3Provider.CurrentBase(decoder!, token))
                throw new InvalidOperationException("MP3 historical provider entry no longer matches its qualified retained instance.");
            Abandon();
            try
            {
                replaying++;
                // History starts after the actual retained first conversion.
                // Remove each completed operation before attempting the next.
                while (History.Count != 0)
                {
                    var operation = History.Peek();
                    if (operation.Kind == PreparedMp3FrameData.OperationKind.Reset) decoder!.Reset();
                    else
                    {
                        using var input = new MemoryStream(operation.Compressed, writable: false);
                        Mp3Frame frame = Mp3Frame.LoadFromStream(input);
                        var destination = new byte[65536];
                        var context = (ulong[])operation.Math.Clone();
                        if (!PreparedMp3Provider.MathBegin(decoder!, context, replay: true))
                            throw new MathQualificationException("Historical native math scope was refused.");
                        bool verified;
                        try { decoder!.DecompressFrame(frame, destination, 0); }
                        finally { verified = PreparedMp3Provider.MathEnd(context); }
                        if (!verified) throw new MathQualificationException("Historical native math scope did not qualify its original conversion.");
                        replayFrames++; nativeFrames++;
                    }
                    History.Dequeue();
                }
                HistoryBytes = 0;
            }
            catch (Exception error) when (error is not MathQualificationException) { failure = ExceptionDispatchInfo.Capture(error); throw; }
            finally { replaying--; }
        }
        internal bool Disposed { get { lock (Gate) return disposed; } }
        internal void SourceClosed()
        {
            lock (registry!["audioRegistryGate"])
            {
                lock (Gate)
                {
                    if (!disposed) { History.Clear(); HistoryBytes = 0; Abandon(); }
                }
                ((Dictionary<object, Func<int>>)registry["audioPending"]).Remove(this);
            }
        }
        internal void Release()
        {
            SourceClosed();
            // Constructed from Stream, so original Dispose preserves borrowed
            // source ownership and frees the selected decoder without replay.
            reader?.Dispose();
        }
        internal int Settle()
        {
            lock (Gate)
            {
                try { if (!disposed && failure == null) CatchUp(); }
                catch when (failure != null) { }
                return disposed ? 3 : failure != null ? 2 : 1;
            }
        }
        internal Dictionary<string, object> Snapshot()
        {
            lock (Gate) return new Dictionary<string, object> { ["warm"] = warm, ["published"] = published,
                ["nativeFrames"] = nativeFrames, ["cachedFrames"] = cachedFrames, ["cachedBytes"] = cachedBytes,
                ["replayFrames"] = replayFrames, ["resets"] = resets, ["disposed"] = disposed,
                ["nativeOnly"] = nativeOnly, ["faulted"] = failure != null, ["provider"] = provider,
                ["refusal"] = refusal, ["cacheKey"] = tapeKey, ["retainedDecoderIdentity"] = decoderIdentity,
                ["retainsPrivateReader"] = reader != null, ["retainsPrivateDecoder"] = decoder != null,
                ["historyBytes"] = HistoryBytes, ["firstNativeConversions"] = first ? 0 : 1 };
        }
    }
}
