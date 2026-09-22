// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Xml;
using HarmonyLib;
using UnityEngine;
using Verse;
using Object = UnityEngine.Object;

namespace FixtureMenuObserver;

// Private functional experiment. Observes published stream operations, never
// native detours, private texture writes, wait pumping or performance counters.
internal static class C10SerializedTextureProbe
{
    internal static bool Selected => Environment.GetCommandLineArgs().Contains("--fixture-c10-serialized-texture");
    private static string directory = "", input = "", assetName = "", expectedRevision = "";
    private static Texture2D? baseline, texture;
    private static AssetBundle? bundle;
    private static AssetBundleRequest? request;
    private static ObservedStream? stream;
    private static long payloadOffset, payloadBytes;
    private static int phase;
    private static bool finished, syncReady, asyncReady;
    private static readonly List<string> events = new();
    private static readonly List<long> publicationCoverage = new();
    private static string Q(object? v) => "\"" + Convert.ToString(v, CultureInfo.InvariantCulture)!
        .Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
    private static string Hash(byte[] data) { using var h = SHA256.Create(); return BitConverter.ToString(h.ComputeHash(data)).Replace("-", "").ToLowerInvariant(); }
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static object Call(string type, string method, params object[] args)
    {
        Type t = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "WakeUp").GetType("WakeUp." + type, true)!;
        return AccessTools.Method(t, method).Invoke(null, args)!;
    }
    private static void Event(string name, string details = "")
    {
        events.Add("{\"event\":" + Q(name) + ",\"payloadUniqueBytesRead\":" + (stream?.Covered ?? 0)
            + ",\"readCalls\":" + (stream?.Reads ?? 0) + ",\"details\":" + Q(details) + "}");
        File.WriteAllText(Path.Combine(directory, "c10-serialized-texture-events.jsonl"), string.Join("\n", events) + "\n");
    }
    internal static void Start(string output)
    {
        directory = output; input = Path.Combine(GenFilePaths.SaveDataFolderPath, "WakeUp", "C10SerializedProvider");
        try
        {
            var xml = new XmlDocument(); xml.Load(Path.Combine(input, "expected.xml"));
            string Value(string name) => xml.DocumentElement!.SelectSingleNode(name)!.InnerText;
            assetName = Value("AssetName");
            payloadOffset = long.Parse(Value("PayloadOffset"), CultureInfo.InvariantCulture);
            payloadBytes = long.Parse(Value("PayloadBytes"), CultureInfo.InvariantCulture);
            byte[] native = File.ReadAllBytes(Path.Combine(input, "native.bin"));
            Require(Hash(native) == Value("NativeSha256"), "native-input-hash");
            Require(Hash(File.ReadAllBytes(Path.Combine(input, "experiment.bundle"))) == Value("BundleSha256"), "bundle-input-hash");
            baseline = (Texture2D)Call("PngRuntime", "Restore", Call("NativeTextureData", "Decode", Value("Identity"), native));
            Require(baseline != null, "native-baseline-restore");
            expectedRevision = C08TexturePixels.Revision(baseline!);
            Event("native-baseline-ready", Describe(baseline!));
        }
        catch (Exception e) { Finish(e); }
    }
    internal static bool Advance()
    {
        if (!Selected || finished) return false;
        try
        {
            if (phase == 0)
            {
                Open(); Event("sync-before-load");
                texture = bundle!.LoadAsset<Texture2D>(assetName);
                Require(texture != null, "sync-asset-null");
                Event("sync-public-object-returned", Describe(texture!)); publicationCoverage.Add(stream!.Covered);
                CheckReady("sync"); syncReady = true; CloseLoaded(); phase = 1;
            }
            else if (phase == 1)
            {
                Open(); request = bundle!.LoadAssetAsync<Texture2D>(assetName);
                Event("async-request-returned", "isDone=" + request.isDone + "; no asset getter before completion"); phase = 2;
            }
            else if (phase == 2)
            {
                if (!request!.isDone) return true;
                Event("async-request-completed-before-asset-getter");
                texture = request.asset as Texture2D;
                Require(texture != null, "async-asset-null");
                Event("async-public-object-returned", Describe(texture!)); publicationCoverage.Add(stream!.Covered);
                CheckReady("async"); asyncReady = true; CloseLoaded(); Finish(null);
            }
        }
        catch (Exception e) { Finish(e); }
        return !finished;
    }
    private static void Open()
    {
        stream = new ObservedStream(Path.Combine(input, "experiment.bundle"), payloadOffset, payloadBytes);
        bundle = AssetBundle.LoadFromStream(stream, 0, 1024);
        Require(bundle != null, "bundle-load-null"); Event("bundle-returned");
        Require(bundle!.GetAllAssetNames().Contains(assetName), "asset-name-missing"); Event("names-enumerated");
    }
    private static string Describe(Texture2D t) => string.Join("/", t.width, t.height, (int)t.format, (int)t.graphicsFormat,
        t.mipmapCount, (int)t.filterMode, (int)t.wrapModeU, (int)t.wrapModeV, (int)t.wrapModeW, t.anisoLevel,
        t.mipMapBias.ToString("R", CultureInfo.InvariantCulture), t.isReadable);
    private static string CpuResult(Texture2D t)
    {
        try { byte[] data = t.GetRawTextureData(); return data == null ? "native-null" : "bytes:" + Hash(data); }
        catch (UnityException) { return "native-unreadable"; }
    }
    private static void CheckReady(string label)
    {
        Require(Describe(texture!) == Describe(baseline!), "serialized-native-descriptor");
        string cpuExpected = CpuResult(baseline!);
        Require(CpuResult(texture!) == cpuExpected, "serialized-native-cpu-readability");
        // The retained concrete reference is accessible through ordinary direct
        // dictionary enumeration and reflection without calling our provider.
        var holder = new Dictionary<string, Texture2D> { [assetName] = texture! };
        Require(ReferenceEquals(holder.Values.Single(), texture), "enumerated-identity");
        Require(ReferenceEquals(typeof(Dictionary<string, Texture2D>).GetProperty("Item")!.GetValue(holder, new object[] { assetName }), texture), "reflected-identity");
        string expected = Describe(texture!); string worker = ""; Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { Texture2D retained = holder[assetName]; worker = Describe(retained) + ":" + CpuResult(retained); }
            catch (Exception e) { failure = e; }
        }) { IsBackground = true };
        thread.Start();
        Require(thread.Join(3000), "worker-did-not-return-while-main-waited");
        Require(failure == null && worker == expected + ":" + cpuExpected, "worker-ready-getter:" + failure);
        Event(label + "-worker-returned-with-main-waiting");
        Require(C08TexturePixels.Revision(texture!) == expectedRevision, "serialized-all-mip-gpu-content");
        Event(label + "-all-mip-gpu-equal");
        using var materialOwner = new MaterialOwner(texture!);
        Require(ReferenceEquals(materialOwner.Value.mainTexture, texture), "material-texture-identity");
        Event(label + "-material-reference-equal");
    }
    private static void CloseLoaded()
    {
        bundle!.Unload(false); bundle = null;
        Event("bundle-unloaded-retaining-texture");
        stream!.Dispose(); stream = null;
        Require(Describe(texture!) == Describe(baseline!) && C08TexturePixels.Revision(texture!) == expectedRevision, "retained-after-unload");
        Object.DestroyImmediate(texture!); texture = null; request = null;
    }
    private static void Finish(Exception? error)
    {
        if (finished) return; finished = true;
        try
        {
            if (bundle != null) bundle.Unload(true);
            else if (texture != null) Object.DestroyImmediate(texture);
            stream?.Dispose();
            if (baseline != null) Object.DestroyImmediate(baseline);
        }
        catch (Exception cleanup) { error = error == null ? cleanup : new AggregateException(error, cleanup); }
        bool passed = error == null && syncReady && asyncReady;
        File.WriteAllText(Path.Combine(directory, "c10-serialized-texture.json"),
            "{\"schema\":\"c10-serialized-texture.v1\",\"completed\":true,\"functionalPassed\":" + passed.ToString().ToLowerInvariant()
            + ",\"payloadBytes\":" + payloadBytes + ",\"publicationPayloadBytesRead\":[" + string.Join(",", publicationCoverage) + "]"
            + ",\"syncReady\":" + syncReady.ToString().ToLowerInvariant() + ",\"asyncReady\":" + asyncReady.ToString().ToLowerInvariant()
            + ",\"postPublicationDeferralEstablished\":false,\"performanceMeasured\":false,\"failure\":" + Q(error?.ToString() ?? "") + "}\n");
    }
    private sealed class MaterialOwner : IDisposable
    {
        internal readonly Material Value;
        internal MaterialOwner(Texture2D t) { Value = new Material(ShaderDatabase.Cutout); Value.mainTexture = t; }
        public void Dispose() => Object.DestroyImmediate(Value);
    }
    private sealed class ObservedStream : Stream
    {
        private readonly FileStream file;
        private readonly object gate = new();
        private readonly long start, end;
        private readonly List<Tuple<long,long>> ranges = new();
        internal int Reads { get; private set; }
        internal long Covered
        {
            get { lock (gate) { long count = 0, last = start; foreach (var r in ranges.OrderBy(r => r.Item1)) { long lo = Math.Max(last, r.Item1); if (r.Item2 > lo) count += r.Item2 - lo; last = Math.Max(last, r.Item2); } return count; } }
        }
        internal ObservedStream(string path, long offset, long length)
        {
            file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            start = offset; end = checked(offset + length);
            Require(offset >= 0 && length > 0 && end <= file.Length, "payload-range");
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            lock (gate)
            {
                long pos = file.Position; int got = file.Read(buffer, offset, count); Reads++;
                long lo = Math.Max(start, pos), hi = Math.Min(end, pos + got);
                if (hi > lo) ranges.Add(Tuple.Create(lo, hi));
                return got;
            }
        }
        public override long Seek(long offset, SeekOrigin origin) { lock (gate) return file.Seek(offset, origin); }
        public override long Position { get { lock (gate) return file.Position; } set { lock (gate) file.Position = value; } }
        public override long Length => file.Length;
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) file.Dispose(); base.Dispose(disposing); }
    }
}
