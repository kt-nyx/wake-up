// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Verse;
using Object = UnityEngine.Object;

namespace FixtureMenuObserver;

// Opt-in private fixture check, advanced on the main thread one source per frame.
// All generated entries use the product's ONE owned store and shared quota.
// No UI, scene changes, helper execution or performance measurements.
internal static partial class C05TextureProbe
{
    private sealed class Sample
    {
        internal ModContentPack? Provider;
        internal FileInfo File = null!;
        internal string Logical = "", Selection = "", Category = "";
        internal string MaskConsumerDef = "", MaskShader = "";
        internal int NativeLayoutBytes, MaskMaterialId;
        internal Texture2D? Holder;
        internal bool Synthetic;
    }
    private static readonly Queue<Sample> pending = new();
    private static readonly HashSet<string> covered = new(StringComparer.Ordinal);
    private static readonly string[] descriptor = { "Width", "Height", "GraphicsFormat", "TextureFormat", "Mips",
        "Filter", "WrapU", "WrapV", "WrapW", "Aniso", "Bias", "Readable" };
    private static string directory = "";
    private static bool started, finished;
    private static bool ddsFallbackChecked;
    private static bool firstUploadFallbackChecked;
    private static int refusedPlatformCalls, unexpectedSnapshotCalls;
    private static int passed, failed, refused, nativeNegativeControls;
    private static readonly CancellationTokenSource cancellation = new();
    private static object? capturedByBatch;
    private static Type Product(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .First(a => a.GetName().Name == "WakeUp").GetType("WakeUp." + name, true)!;
    private static object? Read(object value, string name) => AccessTools.Field(value.GetType(), name)?.GetValue(value)
        ?? AccessTools.Property(value.GetType(), name)?.GetValue(value, null);
    private static object? Call(string type, string method, params object?[] arguments) =>
        AccessTools.Method(Product(type), method).Invoke(null, arguments);
    private static object? Invoke(object value, string method, params object?[] arguments) =>
        AccessTools.Method(value.GetType(), method).Invoke(value, arguments);
    private static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    private static string Quote(object? value) => "\"" + Convert.ToString(value, CultureInfo.InvariantCulture)!
        .Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
    private static void Receipt(params object?[] pairs)
    {
        var fields = new List<string>();
        for (int i = 0; i < pairs.Length; i += 2)
        {
            object? v = pairs[i + 1];
            fields.Add(Quote(pairs[i]) + ":" + (v == null ? "null" : v is bool b ? b.ToString().ToLowerInvariant()
                : v is int || v is long || v is float ? Convert.ToString(v, CultureInfo.InvariantCulture) : Quote(v)));
        }
        File.AppendAllText(Path.Combine(directory, "c05-texture-probe.jsonl"), "{" + string.Join(",", fields) + "}\n");
    }
    internal static void StageSources(ModContentPack provider)
    {
        // Private validation provider only, before ordinary texture discovery.
        // Source files live inside the isolated profile, never in frozen mods.
        string root = Path.Combine(GenFilePaths.SaveDataFolderPath, "WakeUp", "C05EarlySource");
        string textures = Path.Combine(root, "Textures", "C05KnownChannels");
        Directory.CreateDirectory(textures);
        File.WriteAllBytes(Path.Combine(textures, "channels.png"), Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAABAAAAAICAYAAADwdn+XAAAAKUlEQVQoFWNk+M/QwIAH/McjB5JiIiBPUHrgDWAE+hGvNxnxSw+HMAAA3r8Fi/QMdgcAAAAASUVORK5CYII="));
        foreach (int depth in new[] { 8, 16 })
        foreach (bool rle in new[] { false, true })
            WritePsd(Path.Combine(textures, "channels-" + depth + (rle ? "-rle.psd" : "-raw.psd")), rle, depth);
        WriteDds(textures, "BC1", 4, 0x31545844, 0, 0, 0, 0, 0, 8);
        WriteDds(textures, "BC3", 4, 0x35545844, 0, 0, 0, 0, 0, 16);
        WriteDds(textures, "RGB24", 64, 0, 24, 255, 65280, 16711680, 0, 0);
        WriteDds(textures, "BGRA32", 65, 0, 32, 16711680, 65280, 255, 4278190080, 0);
        WriteDds(textures, "RGB565", 64, 0, 16, 63488, 2016, 31, 0, 0);
        WriteDds(textures, "RGBA4444", 65, 0, 16, 61440, 3840, 240, 15, 0);
        WriteDds(textures, "Alpha8", 2, 0, 8, 0, 0, 0, 255, 0);
        WriteDds(textures, "Luminance8", 131072, 0, 8, 255, 0, 0, 0, 0);
        bool change = Environment.GetCommandLineArgs().Contains("--fixture-c05-source-change");
        C06PngSources.Stage(textures, change);
        C06JpegSources.Stage(textures);
        string changedPath = Path.Combine(textures, "channels-8-raw.psd");
        if (change) { byte[] changed = File.ReadAllBytes(changedPath); changed[40] = 128; File.WriteAllBytes(changedPath, changed); }
        foreach (string path in Directory.EnumerateFiles(textures)) File.SetLastWriteTimeUtc(path, new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc));
        File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "FixtureMenuObserver", "c05-source-variant.json"),
            "{\"changed\":" + change.ToString().ToLowerInvariant() + ",\"sha256\":" + Quote(Hash(File.ReadAllBytes(changedPath)))
            + ",\"bytes\":" + new FileInfo(changedPath).Length + ",\"utcTicks\":" + File.GetLastWriteTimeUtc(changedPath).Ticks + "}");
        provider.foldersToLoadDescendingOrder.Insert(0, root);
    }
    private static void WriteDds(string root, string name, uint flags, uint fourCc, uint bits,
        uint red, uint green, uint blue, uint alpha, int block)
    {
        int length = block != 0 ? block * 3 : 21 * (int)bits / 8;
        byte[] source = new byte[128 + length];
        void Put(int offset, uint value) => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, source, offset, 4);
        Put(0, 0x20534444); Put(4, 124); Put(8, 0x20000); Put(12, 4); Put(16, 4); Put(28, 3);
        Put(76, 32); Put(80, flags); Put(84, fourCc); Put(88, bits);
        Put(92, red); Put(96, green); Put(100, blue); Put(104, alpha);
        for (int i = 128; i < source.Length; i++) source[i] = (byte)((i * 37 + 19) % 251);
        File.WriteAllBytes(Path.Combine(root, name + ".dds"), source);
    }
    private static void WritePsd(string path, bool rle, int depth)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
        void U16(int n) { writer.Write((byte)(n >> 8)); writer.Write((byte)n); }
        void U32(int n) { U16(n >> 16); U16(n); }
        writer.Write(Encoding.ASCII.GetBytes("8BPS")); U16(1); writer.Write(new byte[6]);
        U16(4); U32(4); U32(4); U16(depth); U16(3);
        U32(0); U32(0); U32(0); U16(rle ? 1 : 0);
        if (rle) for (int row = 0; row < 16; row++) U16(4 * (depth / 8) + 1);
        for (int channel = 0; channel < 4; channel++)
        for (int y = 0; y < 4; y++)
        {
            if (rle) writer.Write((byte)(4 * (depth / 8) - 1));
            for (int x = 0; x < 4; x++)
            {
                Color32 color = Pixel(x, 3 - y, 4, 4);
                byte value = channel == 0 ? color.r : channel == 1 ? color.g : channel == 2 ? color.b : color.a;
                writer.Write(value); if (depth == 16) writer.Write(value);
            }
        }
    }
    internal static void Start(string outputDirectory)
    {
        if (started) return;
        started = true; directory = outputDirectory;
        Directory.CreateDirectory(directory);
        try
        {
            Receipt("event", "start", "gameMvid", typeof(Mod).Module.ModuleVersionId,
                "productMvid", Product("PngRuntime").Module.ModuleVersionId, "unity", Application.unityVersion,
                "graphics", SystemInfo.graphicsDeviceType, "colorSpace", QualitySettings.activeColorSpace,
                "textureCompression", Prefs.TextureCompression, "copyTextureSupport", SystemInfo.copyTextureSupport);
            object policy = AccessTools.Property(Product("CacheLaunchPolicy"), "Current").GetValue(null, null)!;
            if ((bool)(AccessTools.Field(Product("PngRuntime"), "firstBuildSelected")?.GetValue(null) ?? false))
            { CheckPngEndings(); CheckHolderCallbacks(); }
            if (Convert.ToString(Read(policy, "Action")) != "Normal")
                throw new InvalidOperationException("Probe requires normal cache maintenance policy.");
            Discover();
            AddSynthetic();
        }
        catch (Exception e) { failed++; Receipt("event", "setup-error", "passed", false, "error", e.ToString()); Finish(); }
    }
    internal static bool Advance()
    {
        if (!started || finished) return false;
        if (pending.Count == 0) { Finish(); return false; }
        Sample sample = pending.Dequeue();
        try { Check(sample); }
        catch (Exception e)
        {
            failed++;
            Receipt("event", "sample-error", "passed", false, "logical", sample.Logical,
                "provider", sample.Provider?.PackageId, "synthetic", sample.Synthetic, "error", e.ToString());
        }
        finally { capturedByBatch = null; }
        if (pending.Count == 0) { Finish(); return false; }
        return true;
    }
    internal static void BeforeExit()
    {
        if (!started || finished) return;
        cancellation.Cancel();
        Receipt("event", "cancelled", "remaining", pending.Count, "passed", false);
        pending.Clear(); failed++; Finish();
    }
    private static void Finish()
    {
        if (finished) return;
        finished = true;
        foreach (string category in new[] { "png", "jpeg", "dds", "large", "native-pixels-over-8MiB", "actual-bound-mask",
            "non-square", "multiple-mips", "psd-raw", "psd-rle", "synthetic-alpha-channels" })
            Receipt("event", "coverage", "category", category, "covered", covered.Contains(category));
        Receipt("event", "complete", "passedSamples", passed, "failedSamples", failed, "refusedSamples", refused,
            "nativePsdNegativeControlsPassed", nativeNegativeControls, "ddsUnsupportedCaptureFallbackPassed", ddsFallbackChecked,
            "allRequestedSamplesPassed", failed == 0 && refused == 0 && ddsFallbackChecked, "subsequentNormalStartupProvenHere", false,
            "ordinaryGameplayRenderProvenHere", false, "performanceMeasured", false);
    }
    private static void Discover()
    {
        var candidates = new List<Sample>();
        foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
        {
            if (mod.IsOfficialMod) continue;
            var selected = ((IEnumerable<KeyValuePair<string, FileInfo>>)Call("PngRuntime", "SelectedFiles", mod)!).ToArray();
            string selection = (string)Call("PreparedTextureRuntime", "SelectionIdentity", mod, selected)!;
            // Select from actual owning holders, including direct lower-provider access.
            // Filenames are never treated as proof of a shader/mask role.
            foreach (var file in selected)
            {
                string ext = file.Value.Extension.ToLowerInvariant();
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".dds" && ext != ".psd") continue;
                string logical = file.Key.Replace('\\', '/');
                if (!logical.StartsWith("Textures/", StringComparison.Ordinal)) continue;
                if (!mod.GetContentHolder<Texture2D>().contentList.TryGetValue(Path.ChangeExtension(logical.Substring(9), null), out Texture2D native)
                    || native == null || native.width > 4096 || native.height > 4096) continue;
                candidates.Add(new Sample { Provider = mod, File = file.Value, Logical = file.Key,
                    Selection = selection, Holder = native, Category = ext == ".jpg" || ext == ".jpeg" ? "jpeg" : ext.Substring(1),
                    NativeLayoutBytes = (int)Call("NativeTextureData", "ExpectedBytes", native.width, native.height, (int)native.format, native.mipmapCount)! });
            }
        }
        var chosen = new List<Sample>();
        void Pick(Func<Sample, bool> predicate)
        {
            Sample? found = candidates.Where(predicate).OrderBy(s => chosen.Count(c => c.Provider == s.Provider))
                .ThenBy(s => (long)s.Holder!.width * s.Holder.height).FirstOrDefault(s => !chosen.Contains(s));
            if (found != null) chosen.Add(found);
        }
        foreach (string format in new[] { "png", "jpeg", "dds", "psd" }) Pick(s => s.Category == format);
        foreach (Sample known in candidates.Where(s => s.Logical.Replace('\\', '/').StartsWith("Textures/C05KnownChannels/", StringComparison.Ordinal)))
            if (!chosen.Contains(known)) chosen.Add(known);
        void PickLargest(Func<Sample, bool> predicate)
        {
            Sample? found = candidates.Where(predicate).OrderByDescending(s => (long)s.Holder!.width * s.Holder.height)
                .ThenByDescending(s => s.NativeLayoutBytes).FirstOrDefault();
            if (found != null && !chosen.Contains(found)) chosen.Add(found);
        }
        // The old limit was bytes of the native representation, not dimensions.
        // Actual captured Pixels.Length below decides coverage; this descriptor
        // layout only chooses a relevant candidate without decoding the modlist.
        PickLargest(s => s.NativeLayoutBytes > 8 * 1024 * 1024);
        PickLargest(s => s.Category == "jpeg" && s.Holder!.width != s.Holder.height);
        PickLargest(s => s.Holder!.width > 1024 || s.Holder.height > 1024);
        Pick(s => s.Holder!.mipmapCount > 1);
        Pick(s => !chosen.Any(c => c.Provider == s.Provider));
        Sample? mask = FindBoundMask(candidates);
        if (mask != null && !chosen.Contains(mask)) chosen.Add(mask);
        foreach (Sample sample in chosen) pending.Enqueue(sample);
        Receipt("event", "discovery", "selectedActualSources", chosen.Count, "maximumActualSources", 64,
            "maximumActualDimension", 4096, "maskConsumerRoleProven", mask != null,
            "nativeLayoutOver8MiBCandidate", chosen.Any(s => s.NativeLayoutBytes > 8 * 1024 * 1024));
    }
    private static Sample? FindBoundMask(List<Sample> candidates)
    {
        // Inspect material bindings that native definition loading already made.
        // Do not invoke GraphicData.Graphic, initialize new graphics, or infer a
        // mask from a filename. The Texture2D reference proves the exact source.
        foreach (ThingStyleDef style in DefDatabase<ThingStyleDef>.AllDefsListForReading
            .Where(d => d.modContentPack != null && !d.modContentPack.IsOfficialMod).Take(256))
        {
            Graphic graphic = style.Graphic;
            if (graphic == null) continue;
            foreach (Material material in ExistingMaterials(graphic, 0))
            {
                if (material == null || !material.HasProperty(ShaderPropertyIDs.MaskTex)) continue;
                Texture texture = material.GetTexture(ShaderPropertyIDs.MaskTex);
                Sample? found = candidates.FirstOrDefault(candidate => ReferenceEquals(candidate.Holder, texture));
                if (found == null) continue;
                found.MaskConsumerDef = style.defName; found.MaskShader = material.shader.name; found.MaskMaterialId = material.GetInstanceID();
                Receipt("event", "mask-consumer", "logical", found.Logical, "provider", found.Provider?.PackageId,
                    "consumerDef", style.defName, "consumerDefType", "ThingStyleDef", "shader", found.MaskShader,
                    "materialId", found.MaskMaterialId, "exactOwningHolderReference", true, "nativePostLoadMaterial", true);
                return found;
            }
        }
        int examined = 0;
        foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if (def.modContentPack == null || def.modContentPack.IsOfficialMod || def.graphicData == null) continue;
            if (++examined > 512) break;
            Graphic? graphic = AccessTools.Field(typeof(ThingDef), "graphic")?.GetValue(def) as Graphic;
            graphic ??= AccessTools.Field(typeof(GraphicData), "cachedGraphic")?.GetValue(def.graphicData) as Graphic;
            if (graphic == null) continue;
            foreach (Material material in ExistingMaterials(graphic, 0))
            {
                if (material == null || !material.HasProperty(ShaderPropertyIDs.MaskTex)) continue;
                Texture? texture = material.GetTexture(ShaderPropertyIDs.MaskTex);
                Sample? sample = candidates.FirstOrDefault(s => ReferenceEquals(s.Holder, texture));
                if (sample == null) continue;
                sample.MaskConsumerDef = def.defName;
                sample.MaskShader = material.shader == null ? "" : material.shader.name;
                sample.MaskMaterialId = material.GetInstanceID();
                Receipt("event", "mask-consumer", "logical", sample.Logical, "provider", sample.Provider?.PackageId,
                    "consumerDef", def.defName, "consumerProvider", def.modContentPack.PackageId,
                    "graphicType", graphic.GetType().FullName, "shader", sample.MaskShader,
                    "materialId", sample.MaskMaterialId, "shaderProperty", ShaderPropertyIDs.MaskTex,
                    "exactOwningHolderReference", true, "definitionsExamined", examined);
                return sample;
            }
        }
        Receipt("event", "mask-consumer", "covered", false, "definitionsExamined", examined,
            "reason", "No existing native mask material matched a selected mod texture within bounded inspection.");
        return null;
    }
    private static IEnumerable<Material> ExistingMaterials(Graphic graphic, int depth)
    {
        if (depth > 3) yield break;
        if (graphic is Graphic_Single)
        {
            if (AccessTools.Field(typeof(Graphic_Single), "mat")?.GetValue(graphic) is Material single) yield return single;
            yield break;
        }
        if (graphic is Graphic_Multi)
        {
            if (AccessTools.Field(typeof(Graphic_Multi), "mats")?.GetValue(graphic) is Material[] directional)
                foreach (Material material in directional.Take(4)) if (material != null) yield return material;
            yield break;
        }
        if (AccessTools.Field(graphic.GetType(), "subGraphic")?.GetValue(graphic) is Graphic child)
            foreach (Material material in ExistingMaterials(child, depth + 1)) yield return material;
        if (AccessTools.Field(graphic.GetType(), "subGraphics")?.GetValue(graphic) is Graphic[] children)
            foreach (Graphic childGraphic in children.Take(4))
                if (childGraphic != null)
                    foreach (Material material in ExistingMaterials(childGraphic, depth + 1)) yield return material;
    }
    private static object OpenStore(bool compressed) => Activator.CreateInstance(Product("PreparedTextureStore"),
        BindingFlags.Instance | BindingFlags.NonPublic, null,
        new object?[] { GenFilePaths.SaveDataFolderPath, null, null, compressed }, null)!;
    private static Delegate CaptureDelegate<T>(FileInfo file, byte[] source) where T : class
        => new Func<Func<int, bool>, T?>(admit => (T?)(capturedByBatch = Call("PngRuntime", "CapturePrepared", file, source, admit)));
    private static void RefusePreparationPlatform()
    {
        refusedPlatformCalls++;
        throw new NotSupportedException("c05-validation-unsupported-png-capture");
    }
    private static void RejectUnexpectedSnapshot()
    {
        unexpectedSnapshotCalls++;
        throw new InvalidOperationException("Unsupported DDS capture must return native before snapshot/hash.");
    }
    private static void CheckUnsupportedDdsFallback(Sample sample, Texture2D baseline)
    {
        // Inject only the unavailable PNG capability, on the actual GOG runtime.
        // No platform promotion or product option is introduced. The actual DDS
        // load method must use native output without touching processed storage.
        var patches = new Harmony("fixture.c05.dds-platform-refusal");
        MethodInfo platformMethod = AccessTools.Method(Product("PngRuntime"), "PreparationPlatform");
        MethodInfo snapshotMethod = AccessTools.Method(Product("PreparedTextureRuntime"), "ReadSourceSnapshot");
        FieldInfo cacheField = AccessTools.Field(Product("PngRuntime"), "cache");
        FieldInfo fallbacksField = AccessTools.Field(Product("PngRuntime"), "nativeFallbacks");
        FieldInfo passThroughField = AccessTools.Field(Product("PngRuntime"), "ddsNativePassThrough");
        PropertyInfo compressionProperty = AccessTools.Property(Product("PreparedTextureRuntime"), "CompressStorage");
        object? previousCache = cacheField.GetValue(null), previousFallbacks = fallbacksField.GetValue(null);
        object? previousPassThrough = passThroughField.GetValue(null), previousCompression = compressionProperty.GetValue(null, null);
        object store = OpenStore(false);
        Texture2D? actual = null;
        try
        {
            refusedPlatformCalls = unexpectedSnapshotCalls = 0;
            cacheField.SetValue(null, store);
            compressionProperty.SetValue(null, true, null);
            patches.Patch(platformMethod, prefix: new HarmonyMethod(typeof(C05TextureProbe), nameof(RefusePreparationPlatform)));
            patches.Patch(snapshotMethod, prefix: new HarmonyMethod(typeof(C05TextureProbe), nameof(RejectUnexpectedSnapshot)));
            var convert = (Delegate)AccessTools.Field(Product("PngRuntime"), "ToVirtualFile").GetValue(null)!;
            actual = (Texture2D)Call("PngRuntime", "LoadProcessedDds", convert.DynamicInvoke(sample.File), sample.Logical, sample.Selection)!;
            if (actual == null || ReferenceEquals(actual, BaseContent.BadTex))
                throw new InvalidDataException("Unavailable PNG capture replaced working native DDS with an error texture.");
            CompareTextures(baseline, actual);
            byte[] expected = RenderBytes(baseline, 0), observed = RenderBytes(actual, 0);
            bool untouched = Convert.ToInt64(Read(store, "Hits")) == 0 && Convert.ToInt64(Read(store, "Misses")) == 0
                && Convert.ToInt64(Read(store, "Writes")) == 0;
            // C15A retired faithful DDS recaching, including its admission cost.
            // Even compressed-storage selection must now bypass this injected
            // platform refusal and consume the native source without cache IO.
            if (refusedPlatformCalls != 0 || unexpectedSnapshotCalls != 0 || !untouched || !expected.SequenceEqual(observed))
                throw new InvalidDataException("Native DDS platform refusal did not preserve pixels or bypass processed reads.");
            Receipt("event", "dds-native-bypass-with-compression-selected", "passed", true, "logical", sample.Logical,
                "injectedUnsupportedCapture", true, "actualGogNativeDds", true, "platformRefusals", refusedPlatformCalls,
                "snapshotCalls", unexpectedSnapshotCalls, "processedStoreUntouched", untouched,
                "nativeRenderSha256", Hash(expected), "fallbackRenderSha256", Hash(observed));
            Object.DestroyImmediate(actual); actual = null;
            // Ordinary automatic caching must use DDS directly without even
            // asking for PNG capture admission or reading processed storage.
            compressionProperty.SetValue(null, false, null);
            refusedPlatformCalls = unexpectedSnapshotCalls = 0;
            long beforePassThrough = Convert.ToInt64(passThroughField.GetValue(null));
            actual = (Texture2D)Call("PngRuntime", "LoadProcessedDds", convert.DynamicInvoke(sample.File), sample.Logical, sample.Selection)!;
            CompareTextures(baseline, actual);
            observed = RenderBytes(actual, 0);
            untouched = Convert.ToInt64(Read(store, "Hits")) == 0 && Convert.ToInt64(Read(store, "Misses")) == 0
                && Convert.ToInt64(Read(store, "Writes")) == 0;
            if (refusedPlatformCalls != 0 || unexpectedSnapshotCalls != 0 || !untouched || !expected.SequenceEqual(observed)
                || Convert.ToInt64(passThroughField.GetValue(null)) != beforePassThrough + 1)
                throw new InvalidDataException("Ordinary DDS caching did not consume the native source directly.");
            Receipt("event", "dds-automatic-native-pass-through", "passed", true, "logical", sample.Logical,
                "automaticStorePresent", true, "compressionSelected", false, "platformRequests", refusedPlatformCalls,
                "snapshotCalls", unexpectedSnapshotCalls, "processedStoreUntouched", untouched,
                "nativeRenderSha256", Hash(expected), "passThroughRenderSha256", Hash(observed));
            ddsFallbackChecked = true;
        }
        finally
        {
            patches.Unpatch(platformMethod, HarmonyPatchType.All, patches.Id);
            patches.Unpatch(snapshotMethod, HarmonyPatchType.All, patches.Id);
            cacheField.SetValue(null, previousCache); fallbacksField.SetValue(null, previousFallbacks);
            passThroughField.SetValue(null, previousPassThrough); compressionProperty.SetValue(null, previousCompression, null);
            if (actual != null && !ReferenceEquals(actual, BaseContent.BadTex)) Object.DestroyImmediate(actual);
            ((IDisposable)store).Dispose();
        }
    }
    private static void Check(Sample sample)
    {
        cancellation.Token.ThrowIfCancellationRequested();
        byte[] source = (byte[])Call("PreparedTextureRuntime", "ReadSource", sample.File)!;
        string platform = (string)Call("PngRuntime", "PreparationPlatform")!;
        string identity = (string)Call("PreparedTextureRuntime", "Identity", sample.Selection, sample.Logical,
            sample.File.FullName, source, 0, platform, "")!;
        Func<bool> revalidate = () => Hash(source) == Hash((byte[])Call("PreparedTextureRuntime", "ReadSource", sample.File)!)
            && (sample.Synthetic || (bool)Call("TexturePreparationBatch", "StillSelected",
                sample.Provider!.foldersToLoadDescendingOrder.ToArray(), sample.Logical, sample.File.FullName)!);
        Texture2D? independentNative = null;
        try
        {
            Texture2D baseline;
            if (sample.Holder != null) baseline = sample.Holder;
            else
            {
                var convert = (Delegate)AccessTools.Field(Product("PngRuntime"), "ToVirtualFile").GetValue(null)!;
                object virtualFile = convert.DynamicInvoke(sample.File)!;
                var method = AccessTools.Method(typeof(ModContentLoader<Texture2D>), "LoadTextureViaImageConversion");
                independentNative = (Texture2D)method.Invoke(null, new[] { virtualFile });
                baseline = independentNative;
                int expectedWidth = sample.Category == "synthetic-alpha-channels" ? 16 : 4;
                int expectedHeight = sample.Category == "synthetic-alpha-channels" ? 8 : 4;
                bool decoded = baseline != null && baseline.width == expectedWidth && baseline.height == expectedHeight
                    && HasKnownChannels(RenderBytes(baseline, 0));
                Receipt("event", "synthetic-native-decoder", "category", sample.Category, "logical", sample.Logical,
                    "nativeDecodedKnownChannels", decoded, "width", baseline == null ? 0 : baseline.width,
                    "height", baseline == null ? 0 : baseline.height, "sourceSha256", Hash(source));
                if (baseline == null || !decoded) { if (sample.Category.StartsWith("psd-", StringComparison.Ordinal)) nativeNegativeControls++; else refused++; return; }
            }
            if (sample.Category == "psd" && !(bool)AccessTools.Property(Product("PreparedTextureRuntime"), "PsdSupport").GetValue(null, null)!)
            { refused++; Receipt("event", "psd-disabled", "logical", sample.Logical, "newDecoderSelected", false); return; }
            if (sample.Category == "psd" && sample.Logical.Replace('\\', '/').StartsWith("Textures/C05KnownChannels/", StringComparison.Ordinal))
            {
                bool known = baseline.width == 4 && baseline.height == 4 && HasKnownChannels(RenderBytes(baseline, 0));
                Receipt("event", "packaged-psd-startup-consumer", "logical", sample.Logical, "passed", known,
                    "provider", sample.Provider?.PackageId, "decoderAssembly", Product("PsdTextureDecoder").Assembly.FullName,
                    "decoderMvid", Product("PsdTextureDecoder").Module.ModuleVersionId,
                    "bundledLicenseResource", Product("PsdTextureDecoder").Assembly.GetManifestResourceNames().Contains("WakeUp.ThirdParty.StbImageSharp.LICENSE"),
                    "decoderContract", AccessTools.Field(Product("PsdTextureDecoder"), "Contract").GetRawConstantValue(),
                    "newDecoding", true, "ordinaryHolder", true);
                if (!known) throw new InvalidDataException("Packaged optional PSD loader did not publish known channels to the ordinary holder.");
            }
            if (!ddsFallbackChecked && sample.File.Extension.Equals(".dds", StringComparison.OrdinalIgnoreCase))
                CheckUnsupportedDdsFallback(sample, baseline);
            // PNG/JPEG/DDS use the game's producer. Opt-in PSD is new decoding.
            object? nativeEntry = Call("PngRuntime", "CapturePrepared", sample.File, source, new Func<int, bool>(_ => true));
            if (nativeEntry == null)
            {
                refused++; Receipt("event", "native-refused", "covered", false, "logical", sample.Logical,
                    "category", sample.Category, "provider", sample.Provider?.PackageId, "synthetic", sample.Synthetic,
                    "sourceSha256", Hash(source), "reason", "actual-native-representation-not-captured"); return;
            }
            if ((bool)(AccessTools.Field(Product("PngRuntime"), "firstBuildSelected")?.GetValue(null) ?? false))
                CheckFirstBuild(sample, source, baseline, nativeEntry);
            var captureFields = new List<object?> { "event", "native-capture", "logical", sample.Logical,
                "pixelsBytes", ((byte[])Read(nativeEntry, "Pixels")!).Length,
                "valid", Call("NativeTextureData", "Valid", nativeEntry) };
            foreach (string field in descriptor) { captureFields.Add(field); captureFields.Add(Read(nativeEntry, field)); }
            Receipt(captureFields.ToArray());
            var makeCapture = typeof(C05TextureProbe).GetMethod(nameof(CaptureDelegate), BindingFlags.Static | BindingFlags.NonPublic)!
                .MakeGenericMethod(nativeEntry.GetType());
            foreach (bool compressed in new[] { false, true })
            {
                object? store = null;
                Texture2D? restored = null;
                try
                {
                    store = OpenStore(compressed);
                    // Force this bounded sample through real publication in the requested
                    // disk mode. Identity remains independent of byte compression.
                    Invoke(store, "Invalidate", identity);
                    capturedByBatch = null;
                    Delegate capture = (Delegate)makeCapture.Invoke(null, new object[] { sample.File, source })!;
                    string result = (string)Call("TexturePreparationBatch", "Native", store, identity, revalidate,
                        capture, cancellation.Token, null, null)!;
                    if (capturedByBatch == null || !result.StartsWith("prepared", StringComparison.Ordinal))
                        throw new InvalidDataException("Native preparation did not publish: " + result + "; " + Read(store, "LastReason") + "; " + Read(store, "FailureDetail"));
                    CompareEntries(nativeEntry, capturedByBatch);
                    Invoke(store, "Complete"); ((IDisposable)store).Dispose(); store = null;
                    store = OpenStore(compressed);
                    object entry = Invoke(store, "Read", identity) ?? throw new InvalidDataException("Reopened grouped entry missing: " + Read(store, "LastReason"));
                    CompareEntries(nativeEntry, entry);
                    restored = (Texture2D)Call("PngRuntime", "Restore", entry)!;
                    CompareTextures(baseline, restored);
                    for (int mip = 0; mip < baseline.mipmapCount; mip++)
                    {
                        if ((baseline.format == TextureFormat.DXT1 || baseline.format == TextureFormat.DXT5 || baseline.format == TextureFormat.BC7)
                            && ((Math.Max(1, baseline.width >> mip) % 4) != 0 || (Math.Max(1, baseline.height >> mip) % 4) != 0))
                        {
                            Receipt("event", "mip-byte-equality", "logical", sample.Logical, "compressed", compressed, "mip", mip,
                                "equal", true, "rendered", false, "reason", "Unity standalone compressed mip dimensions unsupported; complete native byte equality checked");
                            continue;
                        }
                        byte[] expected = RenderBytes(baseline, mip), actual = RenderBytes(restored, mip);
                        bool equal = expected.SequenceEqual(actual);
                        Receipt("event", "render-mip", "logical", sample.Logical, "compressed", compressed, "mip", mip,
                            "nativeRenderSha256", Hash(expected), "restoredRenderSha256", Hash(actual), "equal", equal,
                            "explicitMipCopy", true, "width", Math.Max(1, baseline.width >> mip), "height", Math.Max(1, baseline.height >> mip));
                        if (!equal) throw new InvalidDataException("Explicit native/restored mip render differs at " + mip);
                    }
                    var fields = new List<object?> { "event", "representation", "passed", true, "logical", sample.Logical,
                        "physical", sample.File.FullName, "provider", sample.Provider?.PackageId, "synthetic", sample.Synthetic,
                        "category", sample.Category, "identity", identity, "sourceSha256", Hash(source), "compressed", compressed,
                        "maskConsumerDef", sample.MaskConsumerDef, "maskShader", sample.MaskShader, "maskMaterialId", sample.MaskMaterialId,
                        "pixelsSha256", Hash((byte[])Read(entry, "Pixels")!), "pixelsBytes", ((byte[])Read(entry, "Pixels")!).Length,
                        "batchResult", result, "storeReadReason", Read(store, "LastReason"), "reopened", true };
                    foreach (string field in descriptor) { fields.Add(field); fields.Add(Read(entry, field)); }
                    Receipt(fields.ToArray());
                    Invoke(store, "Complete");
                }
                finally { if (restored != null) Object.DestroyImmediate(restored); (store as IDisposable)?.Dispose(); capturedByBatch = null; }
            }
            if (!revalidate()) throw new InvalidDataException("Source/provider changed during probe.");
            passed++; covered.Add(sample.Category);
            if (sample.Category == "psd") covered.Add(sample.File.Name.Contains("-rle") ? "psd-rle" : "psd-raw");
            if (!sample.Synthetic)
            {
                if (baseline.width > 1024 || baseline.height > 1024) covered.Add("large");
                if (baseline.width != baseline.height) covered.Add("non-square");
                if (baseline.mipmapCount > 1) covered.Add("multiple-mips");
                if (((byte[])Read(nativeEntry, "Pixels")!).Length > 8 * 1024 * 1024) covered.Add("native-pixels-over-8MiB");
                if (sample.MaskConsumerDef != "") covered.Add("actual-bound-mask");
            }
        }
        finally { if (independentNative != null) Object.DestroyImmediate(independentNative); }
    }
    private static void CheckPngEndings()
    {
        foreach (string variant in new[] { "complete", "missing-final-block", "trailing-deflate-bytes" })
        {
            using var output = new MemoryStream();
            output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
            Action<uint> u32 = value => { for (int shift = 24; shift >= 0; shift -= 8) output.WriteByte((byte)(value >> shift)); };
            Action<string, byte[]> chunk = (name, data) =>
            {
                u32((uint)data.Length); byte[] type = Encoding.ASCII.GetBytes(name); output.Write(type, 0, type.Length); output.Write(data, 0, data.Length);
                uint crc = uint.MaxValue;
                foreach (byte b in type.Concat(data)) { crc ^= b; for (int bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320u : crc >> 1; }
                u32(~crc);
            };
            chunk("IHDR", new byte[] { 0,0,0,1,0,0,0,1,8,6,0,0,0 });
            var compressed = new List<byte> { 120,1,(byte)(variant == "missing-final-block" ? 0 : 1),5,0,250,255,0,17,34,51,255 };
            if (variant == "trailing-deflate-bytes") compressed.AddRange(new byte[] { 255,255,255,255 });
            uint a = 1, bsum = 0; foreach (byte b in new byte[] { 0,17,34,51,255 }) { a = (a + b) % 65521; bsum = (bsum + a) % 65521; }
            uint adler = (bsum << 16) | a;
            for (int shift = 24; shift >= 0; shift -= 8) compressed.Add((byte)(adler >> shift));
            chunk("IDAT", compressed.ToArray()); chunk("IEND", Array.Empty<byte>());
            byte[] source = output.ToArray();
            bool cpu = false, native = false;
            try { cpu = Call("FirstBuildPngDecoder", "Decode", source, CancellationToken.None) != null; } catch (TargetInvocationException) { }
            var texture = new Texture2D(2, 2, TextureFormat.Alpha8, true);
            try { native = texture.LoadImage(source); } catch { }
            finally { Object.DestroyImmediate(texture); }
            Receipt("event", "c06-png-ending", "variant", variant, "cpuAccepted", cpu, "nativeAccepted", native, "passed", cpu == native);
            if (cpu && !native) failed++;
        }
    }
    private static void CheckFirstBuild(Sample sample, byte[] source, Texture2D holder, object nativeEntry)
    {
        bool jpegSource = sample.Category == "jpeg";
        if (!sample.File.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            && !sample.File.Extension.Equals(".psd", StringComparison.OrdinalIgnoreCase) && !jpegSource) return;
        object decoded;
        bool admitted = true;
        try
        {
            decoded = Call("FirstBuildImageData", "Decode", source, CancellationToken.None)!;
        }
        catch (TargetInvocationException e) when (e.InnerException is NotSupportedException || e.InnerException is InvalidDataException)
        {
            Receipt("event", "c06-family-fallback", "logical", sample.Logical, "reason", e.InnerException.Message);
            if (!sample.File.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase) && !jpegSource) return;
            // Compare still-open PNG color/depth branches without admitting them
            // to the production queue. This probe cannot enable a product family.
            object cpuResult;
            try { cpuResult = Call(jpegSource ? "FirstBuildJpegDecoder" : "FirstBuildPngDecoder", "Decode", source, CancellationToken.None)!; }
            catch (TargetInvocationException) { return; }
            decoded = jpegSource ? Call("FirstBuildImageData", "FromJpeg", cpuResult)!
                : Call("FirstBuildImageData", "FromPng", cpuResult, CancellationToken.None)!;
            admitted = false;
        }
        object? png = Read(decoded, "Png");
        object? jpeg = Read(decoded, "Jpeg");
        if (sample.File.Name.StartsWith("c06-jpeg-adobe-high-", StringComparison.Ordinal) && admitted)
            throw new InvalidDataException("Adobe transform-zero input bypassed ordinary native fallback");
        if (png != null && admitted && !firstUploadFallbackChecked)
        {
            firstUploadFallbackChecked = true;
            object refused = Call("FirstBuildImageData", "Decode", source, CancellationToken.None)!;
            AccessTools.Field(refused.GetType(), "Pixels").SetValue(refused, Array.Empty<byte>());
            FieldInfo uploads = AccessTools.Field(Product("PngRuntime"), "firstBuildPngUploads");
            long before = (long)uploads.GetValue(null)!;
            object fallback = Call("PngRuntime", "CaptureDecoded", sample.File, source, new Func<int, bool>(_ => true), refused)
                ?? throw new InvalidDataException("C06 upload refusal lost native capture");
            CompareEntries(nativeEntry, fallback);
            bool noUpload = (long)uploads.GetValue(null)! == before;
            if (!noUpload) throw new InvalidDataException("C06 refused upload was counted as prepared use");
            Receipt("event", "c06-upload-fallback", "passed", true, "nativeFinalBytesAndDescriptor", true, "preparedUploads", 0);
        }
        if (png != null || jpeg != null)
        {
            Texture2D rawNative = new Texture2D(2, 2, TextureFormat.Alpha8, true);
            try
            {
                bool loaded = rawNative.LoadImage(source);
                var pixels = (byte[])Read(decoded, "Pixels")!;
                object data = png ?? jpeg!;
                byte[] nativeBytes = rawNative.GetRawTextureData().Take(pixels.Length).ToArray();
                bool equal = loaded && rawNative.width == (int)Read(data, "Width")!
                    && rawNative.height == (int)Read(data, "Height")! && rawNative.format == (png != null ? TextureFormat.ARGB32 : TextureFormat.RGB24)
                    && nativeBytes.SequenceEqual(pixels);
                Receipt("event", "c06-base-pixels", "logical", sample.Logical, "passed", equal,
                    "nativeAccepted", loaded, "nativeWidth", rawNative.width, "nativeHeight", rawNative.height,
                    "nativeFormat", rawNative.format, "depth", png == null ? 8 : Read(png, "BitDepth"), "colorType", png == null ? "jpeg" : Read(png, "ColorType"),
                    "interlaced", png == null ? null : Read(png, "Interlaced"), "legacyGamma", ImageConversion.EnableLegacyPngGammaRuntimeLoadBehavior,
                    "differentBytes", nativeBytes.Length == pixels.Length ? nativeBytes.Where((b, i) => b != pixels[i]).Count() : -1,
                    "sourceSha256", Hash(source), "pixelsSha256", Hash(pixels), "admittedAtStartup", admitted);
                if (!equal)
                {
                    if (jpeg != null && pixels.Length <= 12 * 1024 * 1024 && nativeBytes.Length == pixels.Length)
                    {
                        string stem = Path.Combine(directory, "c06-jpeg-" + Hash(source));
                        File.WriteAllBytes(stem + "-source.jpg", source);
                        File.WriteAllBytes(stem + "-native.rgb", nativeBytes);
                        File.WriteAllBytes(stem + "-candidate.rgb", pixels);
                        object metadata = Read(jpeg, "Metadata")!;
                        Receipt("event", "c06-jpeg-difference", "stem", stem, "width", rawNative.width, "height", rawNative.height,
                            "components", Read(metadata, "Components"), "progressive", Read(metadata, "Progressive"),
                            "horizontalSampling", string.Join(",", (byte[])Read(metadata, "HorizontalSampling")!),
                            "verticalSampling", string.Join(",", (byte[])Read(metadata, "VerticalSampling")!),
                            "jfif", Read(metadata, "Jfif"), "adobeTransform", Read(metadata, "AdobeTransform"),
                            "icc", Read(metadata, "HasIccProfile"), "exif", Read(metadata, "HasExif"));
                    }
                    if (admitted) throw new InvalidDataException("C06 base pixels or format differ from native decode");
                    return;
                }
            }
            finally { Object.DestroyImmediate(rawNative); }
        }
        object prepared = Call("PngRuntime", "CaptureDecoded", sample.File, source, new Func<int, bool>(_ => true), decoded)
            ?? throw new InvalidDataException("C06 decoded result was not captured");
        CompareEntries(nativeEntry, prepared);
        Texture2D restored = (Texture2D)Call("PngRuntime", "Restore", prepared)!;
        try
        {
            CompareTextures(holder, restored);
            for (int mip = 0; mip < holder.mipmapCount; mip++)
            {
                if ((holder.format == TextureFormat.DXT1 || holder.format == TextureFormat.DXT5 || holder.format == TextureFormat.BC7)
                    && (Math.Max(1, holder.width >> mip) % 4 != 0 || Math.Max(1, holder.height >> mip) % 4 != 0)) continue;
                if (!RenderBytes(holder, mip).SequenceEqual(RenderBytes(restored, mip)))
                    throw new InvalidDataException("C06 actual holder mip differs at " + mip);
            }
        }
        finally { Object.DestroyImmediate(restored); }
        Receipt("event", "c06-final-equality", "logical", sample.Logical, "passed", true,
            "ordinaryHolder", sample.Holder != null, "allMipBytes", true, "descriptor", true,
            "sourceSha256", Hash(source), "pixelBytes", ((byte[])Read(prepared, "Pixels")!).Length,
            "nativePreservingPng", png != null, "nativePreservingJpeg", jpeg != null,
            "newPsdDecoding", png == null && jpeg == null, "admittedAtStartup", admitted);
    }
    private static void CompareEntries(object a, object b)
    {
        foreach (string field in descriptor)
            if (!Equals(Read(a, field), Read(b, field))) throw new InvalidDataException("Native descriptor changed: " + field);
        byte[] expected = (byte[])Read(a, "Pixels")!, actual = (byte[])Read(b, "Pixels")!;
        if (!expected.SequenceEqual(actual)) throw new InvalidDataException("Native all-mip bytes changed.");
    }
    private static void CompareTextures(Texture2D a, Texture2D b)
    {
        if (a.width != b.width || a.height != b.height || a.format != b.format || a.graphicsFormat != b.graphicsFormat
            || a.mipmapCount != b.mipmapCount || a.isReadable != b.isReadable || a.filterMode != b.filterMode
            || a.wrapModeU != b.wrapModeU || a.wrapModeV != b.wrapModeV || a.wrapModeW != b.wrapModeW
            || a.anisoLevel != b.anisoLevel || a.mipMapBias != b.mipMapBias)
            throw new InvalidDataException("Native/restored Unity texture descriptor differs.");
    }
    private static byte[] RenderBytes(Texture2D texture, int mip)
    {
        int width = Math.Max(1, texture.width >> mip), height = Math.Max(1, texture.height >> mip);
        RenderTexture previous = RenderTexture.active;
        bool previousSrgb = GL.sRGBWrite;
        Texture2D? exactMip = null, readable = null;
        RenderTexture? target = null;
        try
        {
            exactMip = new Texture2D(width, height, texture.format, 1,
                !GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat), true);
            exactMip.filterMode = FilterMode.Point;
            // This selects the source mip explicitly before any shader sampling.
            Graphics.CopyTexture(texture, 0, mip, exactMip, 0, 0);
            target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            GL.sRGBWrite = false;
            Graphics.Blit(exactMip, target);
            RenderTexture.active = target;
            readable = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            readable.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            return readable.GetRawTextureData();
        }
        finally
        {
            RenderTexture.active = previous; GL.sRGBWrite = previousSrgb;
            if (readable != null) Object.DestroyImmediate(readable);
            if (exactMip != null) Object.DestroyImmediate(exactMip);
            if (target != null) RenderTexture.ReleaseTemporary(target);
        }
    }
    private static bool HasKnownChannels(byte[] rgba)
    {
        bool red = false, green = false, blue = false, translucent = false;
        for (int i = 0; i + 3 < rgba.Length; i += 4)
        {
            red |= rgba[i] > 180 && rgba[i + 1] < 80 && rgba[i + 2] < 80;
            green |= rgba[i + 1] > 180 && rgba[i] < 80 && rgba[i + 2] < 80;
            blue |= rgba[i + 2] > 180 && rgba[i] < 80 && rgba[i + 1] < 80;
            translucent |= rgba[i + 3] > 30 && rgba[i + 3] < 220;
        }
        return red && green && blue && translucent;
    }
    private static Color32 Pixel(int x, int y, int width, int height) => x < width / 2
        ? (y < height / 2 ? new Color32(255, 0, 0, 255) : new Color32(0, 255, 0, 128))
        : (y < height / 2 ? new Color32(0, 0, 255, 255) : new Color32(255, 255, 0, 128));
    private static void AddSynthetic()
    {
        string root = Path.Combine(GenFilePaths.SaveDataFolderPath, "WakeUp", "C05Probe");
        Directory.CreateDirectory(root);
        Texture2D? png = null;
        try
        {
            png = new Texture2D(16, 8, TextureFormat.RGBA32, false);
            png.SetPixels32(Enumerable.Range(0, 128).Select(i => Pixel(i % 16, i / 16, 16, 8)).ToArray());
            png.Apply(false);
            Type conversion = AccessTools.TypeByName("UnityEngine.ImageConversion")
                ?? throw new InvalidOperationException("Native PNG encoding module missing.");
            byte[] encoded = (byte[])AccessTools.Method(conversion, "EncodeToPNG", new[] { typeof(Texture2D) }).Invoke(null, new object[] { png });
            File.WriteAllBytes(Path.Combine(root, "channels.png"), encoded);
        }
        finally { if (png != null) Object.DestroyImmediate(png); }
        Add("channels.png", "synthetic-alpha-channels");
        foreach (bool rle in new[] { false, true })
        {
            string name = rle ? "channels-rle.psd" : "channels-raw.psd";
            using var stream = File.Create(Path.Combine(root, name));
            using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
            void U16(int n) { writer.Write((byte)(n >> 8)); writer.Write((byte)n); }
            void U32(int n) { U16(n >> 16); U16(n); }
            writer.Write(Encoding.ASCII.GetBytes("8BPS")); U16(1); writer.Write(new byte[6]);
            U16(4); U32(4); U32(4); U16(8); U16(3); // RGBA planar, 8-bit RGB.
            U32(0); U32(0); U32(0); U16(rle ? 1 : 0);
            if (rle) for (int row = 0; row < 16; row++) U16(5);
            for (int channel = 0; channel < 4; channel++)
            for (int y = 0; y < 4; y++)
            {
                if (rle) writer.Write((byte)3); // PackBits literal run, four bytes.
                for (int x = 0; x < 4; x++)
                {
                    Color32 color = Pixel(x, 3 - y, 4, 4);
                    writer.Write(channel == 0 ? color.r : channel == 1 ? color.g : channel == 2 ? color.b : color.a);
                }
            }
            writer.Flush();
            Add(name, rle ? "psd-rle" : "psd-raw");
        }
        void Add(string name, string category) => pending.Enqueue(new Sample { File = new FileInfo(Path.Combine(root, name)),
            Logical = "C05Probe/" + name, Selection = "private-c05-known-channels", Category = category, Synthetic = true });
    }
}
