// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using NAudio.Wave;

namespace WakeUp;

// Root integration validates the pinned native contract and original patch set
// before installing the exact nongeneric scan hook and enabling load contexts.
// No Harmony API, decoder construction, or diagnostic callback occurs here.
internal static class PreparedMp3IndexRuntime
{
    private static readonly Type Reader = typeof(Mp3FileReader);
    private static readonly Type Index = Reader.Assembly.GetType("NAudio.Wave.Mp3Index", true)!;
    internal static readonly MethodInfo Target = Reader.GetMethod("CreateTableOfContents", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo Input = Reader.GetField("mp3Stream", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo Table = Reader.GetField("tableOfContents", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo Samples = Reader.GetField("totalSamples", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo DataLength = Reader.GetField("mp3DataLength", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo Format = Reader.GetField("<Mp3WaveFormat>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo FilePosition = Index.GetField("<FilePosition>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo SamplePosition = Index.GetField("<SamplePosition>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo SampleCount = Index.GetField("<SampleCount>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo ByteCount = Index.GetField("<ByteCount>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static PreparedAudioCache? cache;
    private static Dictionary<string, object>? registry;
    private static long entries, nativeScans, skippedScans, publications;
    [ThreadStatic] private static Context? context;
    internal sealed class Context
    {
        internal Stream Source = null!;
        internal string Key = "";
        internal long Epoch;
        internal int Entries, NativeScans, SkippedScans;
        internal bool Published;
        internal PreparedMp3IndexData? Captured;
    }
    internal sealed class ScanState
    {
        internal readonly Context owner;
        internal readonly PreparedMp3IndexData initial;
        internal ScanState(Context owner, PreparedMp3IndexData initial) { this.owner = owner; this.initial = initial; }
    }

    internal static void Initialize(PreparedAudioCache selectedCache, Dictionary<string, object> audioRegistry)
    {
        // Resolve the complete pinned private layout before any hook is installed.
        if (Target == null || Input == null || Table == null || Samples == null || DataLength == null || Format == null
            || FilePosition == null || SamplePosition == null || SampleCount == null || ByteCount == null)
            throw new InvalidOperationException("Native MP3 index layout changed.");
        registry = audioRegistry;
        cache = selectedCache ?? throw new ArgumentNullException(nameof(selectedCache));
        Interlocked.Exchange(ref entries, 0); Interlocked.Exchange(ref nativeScans, 0);
        Interlocked.Exchange(ref skippedScans, 0); Interlocked.Exchange(ref publications, 0);
    }

    // key is the admitted zero-position source hash plus original native/patch
    // identity and PreparedMp3IndexData.Identity. The source is not reopened.
    internal static object? PushContext(Stream source, string key)
    {
        object? previous = SuspendContext();
        TryAdmitContext(source, key);
        return previous;
    }

    internal static object? SuspendContext()
    {
        Context? previous = context; context = null; return previous;
    }

    internal static bool AdmissionOpen
    {
        get
        {
            var state = registry;
            if (state == null) return false;
            lock (state["audioRegistryGate"]) return Allowed(state);
        }
    }

    internal static bool TryAdmitContext(Stream source, string key)
    {
        var state = registry;
        if (state != null && OwnedCacheStore.IsKey(key))
            lock (state["audioRegistryGate"])
                if (Allowed(state))
                {
                    context = new Context { Source = source, Key = key, Epoch = (long)state["audioEpoch"] };
                    return true;
                }
        return false;
    }

    // A complete successful native Manager.Load is required for publication;
    // a successful scan followed by provider/constructor failure is not cached.
    internal static Dictionary<string, object> PopContext(object? previous, bool success)
        => CompleteContext(RestoreContext(previous), success);

    internal static Context? RestoreContext(object? previous)
    {
        Context? completed = context;
        context = (Context?)previous;
        return completed;
    }

    internal static Dictionary<string, object> CompleteContext(Context? completed, bool success)
    {
        if (success && completed?.Captured != null && Current(completed))
        {
            try
            {
                completed.Published = cache?.PublishIndex(completed.Key, completed.Captured) == true;
                if (completed.Published) Interlocked.Increment(ref publications);
            }
            catch (Exception error) when (OptionalFailure(error)) { }
        }
        return new Dictionary<string, object> { ["scanEntries"] = completed?.Entries ?? 0,
            ["nativeScans"] = completed?.NativeScans ?? 0, ["skippedScans"] = completed?.SkippedScans ?? 0,
            ["published"] = completed?.Published ?? false, ["loadSucceeded"] = success };
    }

    internal static bool Prefix(object __instance, out ScanState? __state)
    {
        __state = null;
        Context? owner = context;
        if (owner == null || __instance.GetType() != Reader || !ReferenceEquals(Input.GetValue(__instance), owner.Source) || !Current(owner)) return true;
        owner.Entries++; Interlocked.Increment(ref entries);
        // Multiple/reentrant scans are outside the constructor-only contract.
        if (owner.Entries != 1) { owner.Captured = null; return true; }
        var format = (WaveFormat)Format.GetValue(__instance)!;
        var initial = new PreparedMp3IndexData { SourceLength = owner.Source.Length, StartPosition = owner.Source.Position,
            DataLength = (long)DataLength.GetValue(__instance)!, InitialSamples = (long)Samples.GetValue(__instance)!,
            Frequency = format.SampleRate, Channels = format.Channels };
        PreparedMp3IndexData? cached = null;
        try { cached = cache?.ReadIndex(owner.Key); }
        catch (Exception error) when (OptionalFailure(error)) { }
        if (cached != null && Matches(initial, cached))
        {
            // Populate private backing fields without invoking extra native row
            // constructors/property methods. Foreign NAudio hooks close admission.
            IList table = (IList)Activator.CreateInstance(Table.FieldType)!;
            foreach (PreparedMp3IndexData.Row row in cached.Rows)
            {
                object nativeRow = FormatterServices.GetUninitializedObject(Index);
                FilePosition.SetValue(nativeRow, row.FilePosition); SamplePosition.SetValue(nativeRow, row.SamplePosition);
                SampleCount.SetValue(nativeRow, row.SampleCount); ByteCount.SetValue(nativeRow, row.ByteCount);
                table.Add(nativeRow);
            }
            // Do not hold the registry gate around source operations. Recheck
            // after setting the terminal position and restore on lost admission.
            try { owner.Source.Position = cached.TerminalPosition; }
            catch (Exception error) when (OptionalFailure(error)) { owner.Source.Position = initial.StartPosition; cached = null; }
            if (cached != null)
            {
                var state = registry!;
                lock (state["audioRegistryGate"])
                {
                    if (Allowed(state) && (long)state["audioEpoch"] == owner.Epoch)
                    {
                        Table.SetValue(__instance, table); Samples.SetValue(__instance, cached.TotalSamples);
                        owner.SkippedScans++; Interlocked.Increment(ref skippedScans);
                        return false;
                    }
                }
                owner.Source.Position = initial.StartPosition;
            }
        }
        owner.NativeScans++; Interlocked.Increment(ref nativeScans);
        __state = new ScanState(owner, initial);
        return true;
    }

    internal static void Postfix(object __instance, ScanState? __state, bool __runOriginal)
    {
        if (!__runOriginal || __state == null || !Current(__state.owner) || __state.owner.Entries != 1) return;
        try
        {
            var data = __state.initial;
            data.TotalSamples = (long)Samples.GetValue(__instance)!;
            data.TerminalPosition = __state.owner.Source.Position;
            var table = (IList)Table.GetValue(__instance)!;
            if (table.Count > PreparedMp3IndexData.MaximumRows) return;
            foreach (object row in table) data.Rows.Add(new PreparedMp3IndexData.Row { FilePosition = (long)FilePosition.GetValue(row)!,
                SamplePosition = (long)SamplePosition.GetValue(row)!, SampleCount = (int)SampleCount.GetValue(row)!, ByteCount = (int)ByteCount.GetValue(row)! });
            data.Validate();
            __state.owner.Captured = data;
        }
        catch (Exception error) when (OptionalFailure(error)) { }
    }

    private static bool Matches(PreparedMp3IndexData a, PreparedMp3IndexData b) => a.SourceLength == b.SourceLength
        && a.StartPosition == b.StartPosition && a.DataLength == b.DataLength && a.InitialSamples == b.InitialSamples
        && a.Frequency == b.Frequency && a.Channels == b.Channels;
    private static bool Allowed(Dictionary<string, object> state) => !(bool)state["audioNativeOnly"] && !(bool)state["audioAdmissionClosed"];
    internal static bool Current(Context owner)
    {
        var state = registry;
        if (state == null) return false;
        lock (state["audioRegistryGate"]) return Allowed(state) && (long)state["audioEpoch"] == owner.Epoch;
    }
    internal static Dictionary<string, object> Snapshot() => new Dictionary<string, object>
    {
        ["scanEntries"] = Interlocked.Read(ref entries), ["nativeScans"] = Interlocked.Read(ref nativeScans),
        ["skippedScans"] = Interlocked.Read(ref skippedScans), ["publications"] = Interlocked.Read(ref publications)
    };
    // The ordinary audio-cache owner controls disposal and the shared budget.
    internal static void Close() { cache = null; }
    private static bool OptionalFailure(Exception error) => error is IOException || error is UnauthorizedAccessException
        || error is ArgumentException || error is NotSupportedException || error is ObjectDisposedException
        || error is OverflowException || error is SecurityException || error is CryptographicException;
}
