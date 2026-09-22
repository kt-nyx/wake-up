// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using HarmonyLib;
using RuntimeAudioClipLoader;
using UnityEngine;

namespace FixtureMenuObserver;

// Causal observation only: native streaming loads and captured native PCM callbacks.
// No AudioSource, playback, changed samples/delegates, lazy adapter or clock scores.
internal static class C10StreamProbe
{
    private const string Owner = "local.fixture.menuobserver.c10-stream";
    private const string Input = @"Z:\Development\Large Projects\RimWorld Loading Optimizer\.rlo-test-instance\game\Mods\725130005\Sounds\Strange_Feeling.ogg";
    private const string ExpectedHash = "2f70c7ddd156d1379d0ddbb8df1f959fec1ea0df28bd62a30ae9dd2371d5f371";
    private static readonly List<Case> cases = new();
    private static readonly object gate = new();
    private static string directory = "", failure = "";
    private static Harmony? harmony;
    private static Type readerType = null!;
    private static bool compared;
    private sealed class Case
    {
        internal string Name = "";
        internal volatile string Phase = "new";
        internal readonly List<string> Events = new();
        internal TrackingStream Stream = null!;
        internal object? Reader;
        internal AudioClip? Clip;
        internal AudioClip.PCMReaderCallback? Read;
        internal AudioClip.PCMSetPositionCallback? Seek;
        internal int ReaderConstructions, ReadCalls, CreateReadCalls, PreDemandReadCalls;
        internal long ConstructorBytes, ConstructorSeeks, MaxConstructorPosition;
        internal string Metadata = "", Sample0 = "", Sample8192 = "", PublicData = "";
        internal bool PublicGetData, ReaderDisposed, StreamDisposed;
        internal void Event(string name, string fields = "")
        {
            lock (gate) Events.Add("{\"order\":" + Events.Count + ",\"event\":" + Quote(name)
                + ",\"phase\":" + Quote(Phase) + ",\"thread\":" + Thread.CurrentThread.ManagedThreadId + fields + "}");
        }
    }
    private sealed class TrackingStream : Stream
    {
        private readonly FileStream inner = File.OpenRead(Input);
        internal readonly Case Case;
        internal long Bytes, Seeks, MaxPosition;
        internal TrackingStream(Case item) { Case = item; }
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set { inner.Position = value; Seeks++; } }
        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = inner.Read(buffer, offset, count);
            Bytes += read; MaxPosition = Math.Max(MaxPosition, inner.Position); return read;
        }
        public override long Seek(long offset, SeekOrigin origin) { Seeks++; return inner.Seek(offset, origin); }
        public override void Flush() => inner.Flush();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) { inner.Dispose(); Case.StreamDisposed = true; }
            base.Dispose(disposing);
        }
    }
    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"")
        .Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
    private static string Hash(byte[] value) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(value)).Replace("-", "").ToLowerInvariant(); }
    private static string Hash(float[] value) { var bytes = new byte[value.Length * 4]; Buffer.BlockCopy(value, 0, bytes, 0, bytes.Length); return Hash(bytes); }
    private static Case? Find(object reader) { lock (gate) return cases.FirstOrDefault(c => ReferenceEquals(c.Reader, reader)); }
    private static void ReaderEntering(object __instance, Stream __0)
    {
        if (__0 is not TrackingStream stream) return;
        stream.Case.Reader = __instance; stream.Case.ReaderConstructions++;
        stream.Case.Phase = "reader-constructor"; stream.Case.Event("reader-enter");
    }
    private static void ReaderReturned(object __instance)
    {
        Case? c = Find(__instance); if (c == null) return;
        c.ConstructorBytes = c.Stream.Bytes; c.ConstructorSeeks = c.Stream.Seeks;
        c.MaxConstructorPosition = c.Stream.MaxPosition;
        c.Event("reader-return", ",\"encodedBytesRead\":" + c.ConstructorBytes + ",\"seeks\":" + c.ConstructorSeeks
            + ",\"maximumReadPosition\":" + c.MaxConstructorPosition);
        c.Phase = "native-manager-before-create";
    }
    private static void ReaderRead(object __instance, int __2)
    {
        Case? c = Find(__instance); if (c == null) return;
        Interlocked.Increment(ref c.ReadCalls);
        if (c.Phase == "audio-create") Interlocked.Increment(ref c.CreateReadCalls);
        c.Event("reader-read", ",\"requestedFloats\":" + __2);
    }
    private static void ReaderSeek(object __instance, long __0, SeekOrigin __1)
    {
        Case? c = Find(__instance); c?.Event("reader-seek", ",\"offset\":" + __0 + ",\"origin\":" + (int)__1);
    }
    private static void ReaderReadReturned(object __instance, float[] __0, int __1, int __result)
    {
        Case? c = Find(__instance); if (c == null) return;
        int nonzero = 0;
        for (int i = __1; i < __1 + __result; i++) if (__0[i] != 0f) nonzero++;
        c.Event("reader-read-return", ",\"returnedFloats\":" + __result + ",\"nonzeroFloats\":" + nonzero);
    }
    private static void CreateEntering(string __0, bool __4, AudioClip.PCMReaderCallback __5, AudioClip.PCMSetPositionCallback __6)
    {
        Case? c; lock (gate) c = cases.FirstOrDefault(c => c.Name == __0);
        if (c == null) return;
        if (!__4) throw new InvalidOperationException("Natural stream changed to nonstreaming");
        c.Read = __5; c.Seek = __6; c.Phase = "audio-create"; c.Event("audio-create-enter");
    }
    private static void CreateReturned(string __0)
    {
        Case? c; lock (gate) c = cases.FirstOrDefault(c => c.Name == __0);
        if (c == null) return;
        c.Event("audio-create-return"); c.Phase = "after-create";
    }
    internal static void Start(string output)
    {
        directory = output;
        try
        {
            byte[] bytes = File.ReadAllBytes(Input);
            if (bytes.Length <= 307200 || Hash(bytes) != ExpectedHash) throw new InvalidOperationException("Natural streamed input identity/threshold changed");
            readerType = typeof(Manager).Assembly.GetType("RuntimeAudioClipLoader.CustomAudioFileReader", true)!;
            harmony = new Harmony(Owner);
            harmony.Patch(AccessTools.Constructor(readerType, new[] { typeof(Stream), typeof(AudioFormat) }),
                prefix: new HarmonyMethod(typeof(C10StreamProbe), nameof(ReaderEntering)), postfix: new HarmonyMethod(typeof(C10StreamProbe), nameof(ReaderReturned)));
            harmony.Patch(AccessTools.Method(readerType, "Read", new[] { typeof(float[]), typeof(int), typeof(int) }),
                prefix: new HarmonyMethod(typeof(C10StreamProbe), nameof(ReaderRead)),
                postfix: new HarmonyMethod(typeof(C10StreamProbe), nameof(ReaderReadReturned)));
            harmony.Patch(AccessTools.Method(readerType.BaseType, "Seek", new[] { typeof(long), typeof(SeekOrigin) }),
                prefix: new HarmonyMethod(typeof(C10StreamProbe), nameof(ReaderSeek)));
            harmony.Patch(AccessTools.Method(typeof(AudioClip), nameof(AudioClip.Create), new[] { typeof(string), typeof(int), typeof(int), typeof(int), typeof(bool), typeof(AudioClip.PCMReaderCallback), typeof(AudioClip.PCMSetPositionCallback) }),
                prefix: new HarmonyMethod(typeof(C10StreamProbe), nameof(CreateEntering)), postfix: new HarmonyMethod(typeof(C10StreamProbe), nameof(CreateReturned)));
            for (int i = 0; i < 2; i++)
            {
                var c = new Case { Name = "C10NativeStream-" + i, Phase = "before-native-load" };
                lock (gate) cases.Add(c);
                c.Stream = new TrackingStream(c); c.Event("native-load-enter");
                c.Clip = Manager.Load(c.Stream, AudioFormat.ogg, c.Name, doStream: true);
                c.Event("native-load-return"); c.Phase = "before-explicit-consumer";
                c.PreDemandReadCalls = Volatile.Read(ref c.ReadCalls);
                if (c.Clip == null || c.Read == null || c.Seek == null || c.Reader == null) throw new InvalidOperationException("Native stream/callback observation failed");
                c.Metadata = c.Clip.samples + ":" + c.Clip.channels + ":" + c.Clip.frequency + ":"
                    + BitConverter.ToString(BitConverter.GetBytes(c.Clip.length)) + ":" + c.Clip.loadType + ":" + c.Clip.loadState
                    + ":" + Manager.GetAudioClipLoadType(c.Clip) + ":" + Manager.GetAudioClipLoadState(c.Clip);
                c.Event("metadata-observed", ",\"metadata\":" + Quote(c.Metadata));
                // These are explicit test-owned sample requests to the ORIGINAL native callbacks.
                // Preserve Manager's unchanged byte-based Seek target, including its units.
                c.Phase = "explicit-native-callback-consumer";
                var samples = new float[4096]; c.Seek(0); c.Read(samples); c.Sample0 = Hash(samples);
                c.Seek(8192); Array.Clear(samples, 0, samples.Length); c.Read(samples); c.Sample8192 = Hash(samples);
                c.Phase = "public-getdata";
                Array.Clear(samples, 0, samples.Length); c.PublicGetData = c.Clip.GetData(samples, 0); c.PublicData = Hash(samples);
                c.Event("public-getdata-return", ",\"returned\":" + c.PublicGetData.ToString().ToLowerInvariant());
                c.Phase = "destroy-requested"; UnityEngine.Object.Destroy(c.Clip);
            }
            compared = cases[0].Metadata == cases[1].Metadata && cases[0].Sample0 == cases[1].Sample0
                && cases[0].Sample8192 == cases[1].Sample8192 && cases[0].PublicGetData == cases[1].PublicGetData
                && cases[0].PublicData == cases[1].PublicData;
            if (!compared) throw new InvalidOperationException("Native comparison differs");
        }
        catch (Exception ex) { failure = ex.ToString(); }
        finally { foreach (Case c in cases) if (c.Clip != null) UnityEngine.Object.Destroy(c.Clip); Write(); }
    }
    internal static void BeforeExit()
    {
        if (directory.Length == 0) return;
        // Normal observer exit occurs after end-of-frame destruction of owned clips.
        foreach (Case c in cases)
        {
            try
            {
                if (c.Reader is IDisposable disposable) { disposable.Dispose(); c.ReaderDisposed = true; }
                foreach (string name in new[] { "audioClipLoadType", "audioLoadState" })
                    if (c.Clip is not null && AccessTools.Field(typeof(Manager), name)?.GetValue(null) is IDictionary map) map.Remove(c.Clip);
                c.Event("owned-resources-disposed");
            }
            catch (Exception ex) { failure += "\nCleanup: " + ex; }
            finally { c.Stream?.Dispose(); }
        }
        harmony?.UnpatchAll(Owner); Write();
    }
    private static void Write()
    {
        lock (gate) File.WriteAllText(Path.Combine(directory, "c10-stream-probe.json"),
            "{\"schema\":\"c10-stream-causal.v1\",\"source\":" + Quote(Input) + ",\"sourceSha256\":" + Quote(ExpectedHash)
            + ",\"failure\":" + Quote(failure) + ",\"nativeComparisonPassed\":" + compared.ToString().ToLowerInvariant()
            + ",\"audioSourceCreated\":false,\"candidateImplemented\":false,\"cases\":["
            + string.Join(",", cases.Select(c => "{\"name\":" + Quote(c.Name) + ",\"readerConstructions\":" + c.ReaderConstructions
                + ",\"constructorEncodedBytesRead\":" + c.ConstructorBytes + ",\"constructorSeeks\":" + c.ConstructorSeeks
                + ",\"maximumConstructorReadPosition\":" + c.MaxConstructorPosition + ",\"readerCallsDuringCreate\":" + c.CreateReadCalls
                + ",\"readerCallsBeforeExplicitConsumer\":" + c.PreDemandReadCalls + ",\"metadata\":" + Quote(c.Metadata)
                + ",\"sampleAt0\":" + Quote(c.Sample0) + ",\"sampleAt8192\":" + Quote(c.Sample8192)
                + ",\"publicGetData\":" + c.PublicGetData.ToString().ToLowerInvariant() + ",\"publicDataHash\":" + Quote(c.PublicData)
                + ",\"readerDisposed\":" + c.ReaderDisposed.ToString().ToLowerInvariant() + ",\"streamDisposed\":" + c.StreamDisposed.ToString().ToLowerInvariant()
                + ",\"events\":[" + string.Join(",", c.Events) + "]}")) + "]}\n", new UTF8Encoding(false));
    }
}
