// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Object = UnityEngine.Object;

namespace FixtureMenuObserver;

// Explicit private functional fault injection. ReloadAll/ReloadTextures populate
// real holders; no probe fills contentList or calls CompleteReload on their behalf.
internal static class C08BoundaryProbe
{
    private static string directory = "", mode = "", firstPath = "", triggerPath = "", changePath = "";
    private static string receiptName = "c08-boundary-probe.jsonl";
    private static byte[] changed = Array.Empty<byte>();
    private static long originalTicks;
    private static int callbackWrites, installs, ddsCalls, callbackNativeWidth, expectedWidth, expectedHeight;
    private static bool candidate, selfRemove;
    private static bool unityExposure, unityFirstBuild;
    private sealed class CollectionCall { internal int Suspensions; }
    private static CollectionCall? collecting;
    private static bool repeatingCollection;
    private static int qualityCollections, qualityAbsent, qualityRepeated, qualityHolderCallbacks;
    private static byte[] retainedSource = Array.Empty<byte>();
    private static readonly List<Texture2D> retainedImages = new();
    private static Material? retainedMaterial;
    private static readonly Harmony foreign = new("fixture.c08.boundary.foreign");
    private static readonly Harmony schedule = new("fixture.c08.boundary.schedule");
    private static ConstructorInfo Ctor => typeof(LoadedContentItem<Texture2D>).GetConstructors().Single(c => c.GetParameters().Length == 3);
    private static Type Product(string name) => AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "WakeUp").GetType("WakeUp." + name, true)!;
    private static object? Read(object value, string name) => AccessTools.Field(value.GetType(), name)?.GetValue(value)
        ?? AccessTools.Property(value.GetType(), name)?.GetValue(value, null);
    private static object? Call(string type, string method, params object?[] args) => AccessTools.Method(Product(type), method).Invoke(null, args);
    private static object? Invoke(object value, string method, params object?[] args) => AccessTools.Method(value.GetType(), method).Invoke(value, args);
    private static void Set(string type, string field, object? value) => AccessTools.Field(Product(type), field).SetValue(null, value);
    private static void Set(object value, string field, object? next)
    {
        var f = AccessTools.Field(value.GetType(), field);
        if (f != null) f.SetValue(value, next); else AccessTools.Property(value.GetType(), field).SetValue(value, next, null);
    }
    private static void Selected(bool value) => AccessTools.Property(Product("PreparedTextureRuntime"), "UseAtStartup").SetValue(null, value, null);
    private static string Hash(byte[] bytes) { using var h = SHA256.Create(); return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-", ""); }
    private static string Quote(object? value) => "\"" + Convert.ToString(value, CultureInfo.InvariantCulture)!.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
    private static void Receipt(params object?[] values)
    {
        var fields = new List<string>();
        for (int i = 0; i < values.Length; i += 2)
        {
            object? v = values[i + 1];
            fields.Add(Quote(values[i]) + ":" + (v == null ? "null" : v is bool b ? b.ToString().ToLowerInvariant()
                : v is int || v is long ? Convert.ToString(v, CultureInfo.InvariantCulture) : Quote(v)));
        }
        File.AppendAllText(Path.Combine(directory, receiptName), "{" + string.Join(",", fields) + "}\n");
    }
    private static void Require(bool value, string reason) { if (!value) throw new InvalidDataException(reason); }
    private static MethodInfo DdsMethod => (MethodInfo)AccessTools.Field(Product("NativeDdsCapture"), "Load").GetValue(null)!;
    private static MethodInfo ConsumerMethod => AccessTools.Method(typeof(Graphic_Single), "Init");
    private static MethodInfo LoadImageMethod => AccessTools.Method(typeof(ImageConversion), "LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });
    private static MethodInfo HolderMethod => typeof(ModContentPack).GetMethods().Single(m => m.Name == "GetContentHolder"
        && m.IsGenericMethodDefinition && m.GetParameters().Length == 0).MakeGenericMethod(typeof(Texture2D));
    private static void CheckCollectionQueue(bool requireQueue)
    {
        object? session = AccessTools.Field(Product("PngRuntime"), "textureReadSession").GetValue(null);
        Require(session != null, "quality collection has no real loading session");
        object? queue = Read(session!, "queue");
        Require(!requireQueue || queue != null, "holder callback has no first-build queue");
        if (queue != null)
        {
            Require((bool)Read(queue, "IsSuspended")! && (int)Read(queue, "ActiveWorkers")! == 0
                && (int)Read(queue, "OpenStreams")! == 0, "quality collection crossed workers/source handles");
            if (requireQueue) Require((long)Read(queue, "Scheduled")! >= 2 && (long)Read(queue, "Decoded")! >= 1,
                "holder callback did not exercise a populated first-build queue");
        }
        foreach (object context in (IEnumerable)Read(Read(session!, "readContexts")!, "Values")!)
            Require((int)Read(Read(context, "handles")!, "Count")! == 0, "quality collection crossed grouped-read handles");
    }
    private static void ObserveCollection(ref Action __5, out CollectionCall? __state)
    {
        __state = null;
        if (!candidate || repeatingCollection) return;
        var observation = new CollectionCall(); __state = collecting = observation;
        Action suspend = __5;
        __5 = () =>
        {
            suspend(); observation.Suspensions++;
            CheckCollectionQueue(mode == "collection-callback");
        };
    }
    private static void AfterCollection(ModContentPack __0, string __1, FileInfo __2, string __3,
        LoadedContentItem<Texture2D> __4, CollectionCall? __state)
    {
        if (__state == null) return;
        collecting = null;
        // The private input has quality owners for color/mask and none for the
        // trigger. Observe the production decision; do not reproduce its gates.
        bool absent = __1.Replace('\\', '/').EndsWith("/Trigger.png", StringComparison.Ordinal);
        Require(__state.Suspensions == (absent ? 0 : 1), "Loaded suspended an absent owner or skipped a real collection");
        if (absent) { qualityAbsent++; return; }
        qualityCollections++;
        repeatingCollection = true;
        try
        {
            Call("PreparedQualityRuntime", "Loaded", __0, __1, __2, __3, __4,
                new Action(() => throw new InvalidDataException("already-collected Loaded suspended again")));
            qualityRepeated++;
        }
        finally { repeatingCollection = false; }
    }
    private static void ObserveHolderCollection(ModContentHolder<Texture2D> __result)
    {
        if (collecting == null) return;
        Require(collecting.Suspensions == 1, "real holder callback preceded the collection suspension");
        CheckCollectionQueue(true); qualityHolderCallbacks++;
        // An actual holder consumer observes this group's native entries. C08
        // must consequently preserve native output, as in its other callbacks.
        _ = __result.GetAllUnderPath("C08Boundary").ToArray();
    }
    private static void RetainImage(Texture2D __0, byte[] __1)
    {
        if (!__1.SequenceEqual(retainedSource)) return;
        retainedImages.Add(__0);
        // Existing: color precedes trigger. Late: DDS precedes trigger/color.
        // Bind the actual paired color, rather than an unrelated trigger image.
        if (retainedImages.Count == (selfRemove ? 2 : 1))
        {
            retainedMaterial!.mainTexture = __0;
            if (selfRemove) foreign.Unpatch(LoadImageMethod, HarmonyPatchType.All, foreign.Id);
        }
    }
    private static void CountDds() => ddsCalls++;
    private static void ConsumerHook() { }
    private static void RewriteFollowing(object __0, Texture2D __1)
    {
        if (!string.Equals((string)Read(__0, "FullPath")!, triggerPath, StringComparison.OrdinalIgnoreCase)) return;
        callbackNativeWidth = __1.width;
        File.WriteAllBytes(changePath, changed); File.SetLastWriteTimeUtc(changePath, new DateTime(originalTicks, DateTimeKind.Utc));
        callbackWrites++;
        if (selfRemove) foreign.Unpatch(Ctor, HarmonyPatchType.All, foreign.Id);
    }
    private static void NativeAfterItem(object __0)
    {
        if (string.Equals((string)Read(__0, "FullPath")!, firstPath, StringComparison.OrdinalIgnoreCase)) InstallLate();
    }
    private static void ProductAfterItem(FileInfo __2)
    {
        if (string.Equals(__2.FullName, firstPath, StringComparison.OrdinalIgnoreCase)) InstallLate();
    }
    private static void InstallLate()
    {
        if (installs != 0) return;
        installs++;
        if (mode.StartsWith("callback", StringComparison.Ordinal)) foreign.Patch(Ctor, prefix: new HarmonyMethod(typeof(C08BoundaryProbe), nameof(RewriteFollowing)));
        else if (mode.StartsWith("dds", StringComparison.Ordinal)) foreign.Patch(DdsMethod, prefix: new HarmonyMethod(typeof(C08BoundaryProbe), nameof(CountDds)));
        else if (mode == "consumer-late") foreign.Patch(ConsumerMethod, prefix: new HarmonyMethod(typeof(C08BoundaryProbe), nameof(ConsumerHook)));
        else if (mode.StartsWith("unity", StringComparison.Ordinal)) foreign.Patch(LoadImageMethod, postfix: new HarmonyMethod(typeof(C08BoundaryProbe), nameof(RetainImage)));
        else if (mode == "collection-callback") foreign.Patch(HolderMethod, postfix: new HarmonyMethod(typeof(C08BoundaryProbe), nameof(ObserveHolderCollection)));
    }
    private static void RemoveHooks()
    {
        foreign.Unpatch(Ctor, HarmonyPatchType.All, foreign.Id);
        foreign.Unpatch(DdsMethod, HarmonyPatchType.All, foreign.Id);
        foreign.Unpatch(ConsumerMethod, HarmonyPatchType.All, foreign.Id);
        foreign.Unpatch(LoadImageMethod, HarmonyPatchType.All, foreign.Id);
        foreign.Unpatch(HolderMethod, HarmonyPatchType.All, foreign.Id);
        schedule.Unpatch(Ctor, HarmonyPatchType.All, schedule.Id);
        schedule.Unpatch(AccessTools.Method(Product("PreparedQualityRuntime"), "Loaded"), HarmonyPatchType.All, schedule.Id);
    }
    private sealed class RuntimeState : IDisposable
    {
        private readonly Dictionary<FieldInfo, object?> fields = new();
        private readonly List<Action> restoreCollections = new();
        internal RuntimeState()
        {
            // Scalar/reference fields are restored exactly. The three shared
            // quality collections are readonly fields whose contents must also
            // survive a private probe, even if a caller supplied nonempty state.
            foreach (string name in new[] { "PreparedQualityRuntime", "PreparedTextureRuntime" })
                foreach (var f in Product(name).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).Where(f => !f.IsLiteral && !f.IsInitOnly))
                    fields[f] = f.GetValue(null);
            foreach (string name in new[] { "finished", "mode", "cache", "firstBuildSelected", "firstBuildReloadAllowed", "calls", "jpegCalls", "errors",
                "sourceBytes", "nativeFallbacks", "ddsNativePassThrough", "nativeReloads", "reloadFallbacks", "pngTicks", "reloadTicks", "captureTicks", "firstBuildPngUploads", "firstBuildJpegUploads" })
            { var f = AccessTools.Field(Product("PngRuntime"), name); fields[f] = f.GetValue(null); }
            foreach (string name in new[] { "loaded", "exposed", "attempted" })
            {
                object collection = AccessTools.Field(Product("PreparedQualityRuntime"), name).GetValue(null)!;
                if (collection is IDictionary dictionary)
                {
                    var prior = new List<DictionaryEntry>();
                    foreach (DictionaryEntry item in dictionary) prior.Add(item);
                    restoreCollections.Add(() => { dictionary.Clear(); foreach (var item in prior) dictionary.Add(item.Key, item.Value); });
                }
                else
                {
                    var set = (HashSet<string>)collection; string[] prior = set.ToArray();
                    restoreCollections.Add(() => { set.Clear(); set.UnionWith(prior); });
                }
            }
        }
        public void Dispose()
        {
            foreach (var pair in fields) pair.Key.SetValue(null, pair.Value);
            foreach (var restore in restoreCollections) restore();
        }
    }
    internal static bool Run(string outputDirectory)
    {
        receiptName = "c08-boundary-probe.jsonl";
        directory = outputDirectory; Directory.CreateDirectory(directory);
        int passed = 0;
        using var state = new RuntimeState();
        try
        {
            Require((bool)AccessTools.Field(Product("PngRuntime"), "installed").GetValue(null)!, "boundary probe requires installed real texture loader");
            Require(UnityData.IsInMainThread && Current.ProgramState == ProgramState.Entry, "boundary probe requires idle menu main thread");
            Require((int)AccessTools.Field(Product("PngRuntime"), "firstBuildGuardCount").GetValue(null)! >= 10,
                "collection boundary probe requires first-build images enabled at launch");
            Set("PngRuntime", "finished", false); Set("PngRuntime", "mode", "prepare");
            Set("PngRuntime", "cache", null); Set("PngRuntime", "firstBuildSelected", false);
            Set("PngRuntime", "firstBuildReloadAllowed", false);
            Selected(false);
            var source = NaturalSources();
            var colorPng = PngCopy(source.Color);
            byte[] maskDds = File.ReadAllBytes(source.Mask.FullName);
            string helperPath = (string)Call("TexturePreparationHelper", "ExactPath", AccessTools.Field(Product("PreparedTextureRuntime"), "ModRoot").GetValue(null))!;
            string helper = (string)Call("PreparationEncoder", "Identity", helperPath)!;
            object options = Activator.CreateInstance(Product("PreparationOptions"))!; Set(options, "Preset", 2);
            object Encode(byte[] bytes, bool mask) => Call("PreparationEncoder", "Convert", helperPath, helper, bytes, expectedWidth, expectedHeight,
                options, mask, CancellationToken.None, null)!;
            object colorOutput = Encode(colorPng, false), maskOutput = Encode(maskDds, true);
            Receipt("event", "source", "naturalColor", source.Color.FullName, "naturalMask", source.Mask.FullName,
                "naturalColorSha256", Hash(File.ReadAllBytes(source.Color.FullName)), "naturalMaskSha256", Hash(maskDds),
                "privateRenderedPngSha256", Hash(colorPng), "helper", helper, "width", expectedWidth, "height", expectedHeight);
            foreach (var scenario in new[] { ("clean", false), ("callback-existing", false), ("callback-late-selfremove", false),
                ("dds-existing", false), ("dds-existing", true), ("dds-late", false), ("dds-late", true), ("consumer-late", false),
                ("collection-callback", false) })
            {
                RunCase(scenario.Item1, scenario.Item2, colorPng, maskDds, colorOutput, maskOutput, options, helper);
                passed++;
            }
            Receipt("event", "complete", "passed", true, "cases", passed, "actualUiVerified", false, "performanceMeasured", false);
            return true;
        }
        catch (Exception e)
        {
            Receipt("event", "failure", "passed", false, "mode", mode, "candidate", candidate, "error", e.ToString());
            Receipt("event", "complete", "passed", false, "cases", passed, "actualUiVerified", false, "performanceMeasured", false);
            return false;
        }
        finally { RemoveHooks(); }
    }
    internal static bool RunFormats(string outputDirectory)
    {
        directory = outputDirectory; receiptName = "c08-format-probe.jsonl";
        string root = Path.Combine(directory, "C08Formats"); Directory.CreateDirectory(root);
        int passed = 0; bool psd = (bool)AccessTools.Property(Product("PreparedTextureRuntime"), "PsdSupport").GetValue(null, null)!;
        try
        {
            Require(UnityData.IsInMainThread && Current.ProgramState == ProgramState.Entry, "format probe requires idle menu main thread");
            string helperPath = (string)Call("TexturePreparationHelper", "ExactPath", AccessTools.Field(Product("PreparedTextureRuntime"), "ModRoot").GetValue(null))!;
            string helper = (string)Call("PreparationEncoder", "Identity", helperPath)!;
            void Dds(string name, uint flags, uint fourCc, uint bits, uint red, uint green, uint blue, uint alpha, int block)
                => AccessTools.Method(typeof(C05TextureProbe), "WriteDds").Invoke(null,
                    new object[] { root, name, flags, fourCc, bits, red, green, blue, alpha, block });
            Dds("BC1", 4, 0x31545844, 0, 0, 0, 0, 0, 8);
            Dds("RGB565", 64, 0, 16, 63488, 2016, 31, 0, 0);
            Dds("RGBA4444-native-ARGB4444", 65, 0, 16, 61440, 3840, 240, 15, 0);
            // Native upload and helper decode independently consume asymmetric
            // endpoint colors. Exact endpoints avoid compression-rounding noise
            // hiding the orientation or 4444 channel-layout result.
            foreach (string name in new[] { "BC1", "RGB565", "RGBA4444-native-ARGB4444" })
            {
                string path = Path.Combine(root, name + ".dds"); byte[] bytes = File.ReadAllBytes(path);
                if (name == "BC1")
                {
                    bytes[128] = 0; bytes[129] = 248; bytes[130] = 224; bytes[131] = 7;
                    bytes[132] = 0; bytes[133] = 0x55; bytes[134] = 0x11; bytes[135] = 0x44;
                }
                else
                {
                    ushort[] words = name == "RGB565" ? new ushort[] { 0xf800, 0x07e0, 0x001f, 0xffff, 0, 0xffe0, 0xf81f, 0x07ff }
                        : new ushort[] { 0xf500, 0xa05f, 0x5fa0, 0xffaf, 0x05af, 0xf05a, 0xaff0, 0x5f0a };
                    for (int i = 0; i < 16; i++)
                    { ushort word = words[(i + i / 4) % words.Length]; bytes[128 + i * 2] = (byte)word; bytes[129 + i * 2] = (byte)(word >> 8); }
                }
                File.WriteAllBytes(path, bytes);
                CheckFormat(path, 4, 4, helperPath, helper, nativeComparison: true, expectedBase: null, includeColor: false); passed++;
            }
            string psdPath = Path.Combine(root, "merged.psd");
            if (psd)
            {
                AccessTools.Method(typeof(C05TextureProbe), "WritePsd").Invoke(null, new object[] { psdPath, false, 8 });
                CheckFormat(psdPath, 4, 4, helperPath, helper, nativeComparison: true, expectedBase: null, includeColor: false); passed++;
            }
            else Receipt("event", "optional-psd", "tested", false, "reason", "Optional merged PSD decoding was not selected");
            byte[] rgba = new byte[5 * 3 * 4];
            for (int y = 0; y < 3; y++) for (int x = 0; x < 5; x++)
            {
                var pixel = (Color32)AccessTools.Method(typeof(C05TextureProbe), "Pixel").Invoke(null, new object[] { x, y, 5, 3 })!;
                int p = (y * 5 + x) * 4; rgba[p] = pixel.r; rgba[p + 1] = pixel.g; rgba[p + 2] = pixel.b; rgba[p + 3] = pixel.a;
            }
            string pngPath = Path.Combine(root, "asymmetric-5x3.png");
            var png = new Texture2D(5, 3, TextureFormat.RGBA32, false, false);
            try { png.LoadRawTextureData(rgba); png.Apply(false); File.WriteAllBytes(pngPath, ImageConversion.EncodeToPNG(png)); }
            finally { Object.DestroyImmediate(png); }
            CheckFormat(pngPath, 5, 3, helperPath, helper, nativeComparison: false, expectedBase: rgba, includeColor: true); passed++;
            Receipt("event", "complete", "passed", true, "sources", passed, "optionalPsdTested", psd,
                "visualQualityAccepted", false, "performanceMeasured", false); return true;
        }
        catch (Exception e)
        {
            Receipt("event", "failure", "passed", false, "error", e.ToString());
            Receipt("event", "complete", "passed", false, "sources", passed, "optionalPsdTested", psd); return false;
        }
        finally { receiptName = "c08-boundary-probe.jsonl"; }
    }
    internal static bool RunUnityExposure(string outputDirectory)
    {
        directory = outputDirectory; Directory.CreateDirectory(directory);
        receiptName = "c08-unity-exposure-probe.jsonl";
        int passed = 0;
        using var state = new RuntimeState();
        try
        {
            Require(UnityData.IsInMainThread && Current.ProgramState == ProgramState.Entry, "Unity ownership probe requires idle menu main thread");
            Require(!Prefs.TextureCompression, "Unity ownership probe requires fixture native texture compression disabled");
            unityFirstBuild = (bool)AccessTools.Field(Product("PngRuntime"), "firstBuildSelected").GetValue(null)!;
            Set("PngRuntime", "finished", false); Set("PngRuntime", "mode", "prepare");
            Set("PngRuntime", "cache", null);
            Set("PngRuntime", "firstBuildReloadAllowed", false); Selected(false);
            var source = NaturalSources(); byte[] color = PngCopy(source.Color), mask = File.ReadAllBytes(source.Mask.FullName);
            string helperPath = (string)Call("TexturePreparationHelper", "ExactPath", AccessTools.Field(Product("PreparedTextureRuntime"), "ModRoot").GetValue(null))!;
            string helper = (string)Call("PreparationEncoder", "Identity", helperPath)!;
            object options = Activator.CreateInstance(Product("PreparationOptions"))!; Set(options, "Preset", 2);
            object Convert(byte[] bytes, bool isMask) => Call("PreparationEncoder", "Convert", helperPath, helper, bytes, expectedWidth, expectedHeight,
                options, isMask, CancellationToken.None, null)!;
            object colorOutput = Convert(color, false), maskOutput = Convert(mask, true);
            unityExposure = true; retainedSource = color;
            foreach (var scenario in new[] { ("unity-existing", false), ("unity-late-selfremove", true) })
            {
                RunCase(scenario.Item1, scenario.Item2, color, mask, colorOutput, maskOutput, options, helper); passed++;
            }
            Receipt("event", "complete", "passed", true, "cases", passed, "textureCompression", false, "firstBuild", unityFirstBuild,
                "actualUiVerified", false, "performanceMeasured", false); return true;
        }
        catch (Exception e)
        {
            Receipt("event", "failure", "passed", false, "mode", mode, "candidate", candidate, "firstBuild", unityFirstBuild, "error", e.ToString());
            Receipt("event", "complete", "passed", false, "cases", passed); return false;
        }
        finally
        {
            RemoveHooks(); unityExposure = unityFirstBuild = false; retainedSource = Array.Empty<byte>(); retainedImages.Clear();
            if (retainedMaterial != null) Object.DestroyImmediate(retainedMaterial); retainedMaterial = null;
            receiptName = "c08-boundary-probe.jsonl";
        }
    }
    private static void CheckFormat(string path, int width, int height, string helperPath, string helper,
        bool nativeComparison, byte[]? expectedBase, bool includeColor)
    {
        byte[] source = File.ReadAllBytes(path); Texture2D? native = null;
        byte[] Render(Texture2D texture) => (byte[])AccessTools.Method(typeof(C05TextureProbe), "RenderBytes").Invoke(null, new object[] { texture, 0 })!;
        try
        {
            if (nativeComparison)
            {
                object entry = Call("PngRuntime", "CapturePrepared", new FileInfo(path), source, new Func<int, bool>(_ => true))
                    ?? throw new InvalidDataException("actual producer capture unavailable: " + path);
                native = (Texture2D)Call("PngRuntime", "Restore", entry)!;
                Require(native.width == width && native.height == height, "actual producer changed source dimensions");
            }
            foreach (bool mask in includeColor ? new[] { true, false } : new[] { true })
            foreach (int preset in new[] { 1, 2 })
            {
                object options = Activator.CreateInstance(Product("PreparationOptions"))!; Set(options, "Preset", preset);
                object output = Call("PreparationEncoder", "Convert", helperPath, helper, source, width, height, options, mask, CancellationToken.None, null)!;
                int w = Math.Max(1, width / preset), h = Math.Max(1, height / preset), mips = 1;
                for (int x = w, y = h; x > 1 || y > 1; x = Math.Max(1, x / 2), y = Math.Max(1, y / 2)) mips++;
                Require((int)Read(output, "Width")! == w && (int)Read(output, "Height")! == h && (int)Read(output, "Mips")! == mips,
                    "managed helper output dimensions/full mip chain differ");
                Require((int)Read(output, "TextureFormat")! == 4 && (int)Read(output, "GraphicsFormat")! == 8,
                    "mask or non-block-aligned color did not retain qualified Gamma-player RGBA32 upload");
                var restored = (Texture2D)Call("PngRuntime", "Restore", Call("PreparedQualityRuntime", "ToEntry", output))!;
                try
                {
                    string nativeRender = native != null ? Hash(Render(native)) : "";
                    byte[] actual = Render(restored);
                    if (preset == 1 && native != null) Require(actual.SequenceEqual(Render(native)), "full mask orientation/channels differ from actual producer: " + path);
                    if (preset == 1 && expectedBase != null)
                        Require(((byte[])Read(output, "Pixels")!).Take(width * height * 4).SequenceEqual(expectedBase), "full PNG channel/orientation roundtrip differs");
                    Receipt("event", "format", "source", path, "sourceSha256", Hash(source), "helper", helper, "mask", mask, "preset", preset,
                        "width", w, "height", h, "mips", mips, "nativeTextureFormat", native != null ? (int)native.format : -1,
                        "nativeBaseRenderSha256", nativeRender, "qualityBaseRenderSha256", Hash(actual),
                        "qualityAllMipGpuRevision", Call("AtlasProvenance", "Revision", restored),
                        "exactNativeBaseCompared", preset == 1 && native != null, "exactPngBytesCompared", preset == 1 && expectedBase != null,
                        "producer", path.EndsWith(".psd", StringComparison.Ordinal) ? "optional merged PSD decoder" : nativeComparison ? "GOG native DDS upload" : "generated asymmetric PNG",
                        "passed", true);
                }
                finally { Object.DestroyImmediate(restored); }
            }
        }
        finally { if (native != null) Object.DestroyImmediate(native); }
    }
    private static (FileInfo Color, FileInfo Mask) NaturalSources()
    {
        FileInfo? color = null, mask = null;
        foreach (var mod in LoadedModManager.RunningModsListForReading)
            foreach (var pair in (IEnumerable<KeyValuePair<string, FileInfo>>)Call("PngRuntime", "SelectedFiles", mod)!)
            {
                if (pair.Key.Replace('\\', '/') == "Textures/Things/Building/Misc/Brazier.dds") color = pair.Value;
                if (pair.Key.Replace('\\', '/') == "Textures/Things/Building/Misc/Brazier_m.dds") mask = pair.Value;
            }
        Require(color != null && mask != null, "natural Brazier DDS pair is required by private boundary probe");
        Require(color!.Length <= 1024 * 1024 && mask!.Length <= 1024 * 1024, "boundary source size");
        return (color!, mask!);
    }
    private static byte[] PngCopy(FileInfo file)
    {
        object native = Call("PngRuntime", "CapturePrepared", file, File.ReadAllBytes(file.FullName), new Func<int, bool>(_ => true))
            ?? throw new InvalidDataException("natural DDS capture unavailable");
        Texture2D texture = (Texture2D)Call("PngRuntime", "Restore", native)!, png = null!;
        try
        {
            expectedWidth = texture.width; expectedHeight = texture.height;
            Require(expectedWidth >= 8 && expectedWidth < 512 && expectedHeight >= 8 && expectedHeight < 512, "bounded source dimensions");
            byte[] rgba = (byte[])AccessTools.Method(typeof(C05TextureProbe), "RenderBytes").Invoke(null, new object[] { texture, 0 })!;
            png = new Texture2D(expectedWidth, expectedHeight, TextureFormat.RGBA32, false, true);
            png.LoadRawTextureData(rgba); png.Apply(false);
            return ImageConversion.EncodeToPNG(png);
        }
        finally { Object.DestroyImmediate(texture); if (png != null) Object.DestroyImmediate(png); }
    }
    private static void RunCase(string scenario, bool maskFirst, byte[] colorPng, byte[] maskDds,
        object colorOutput, object maskOutput, object options, string helper)
    {
        mode = scenario; selfRemove = scenario.EndsWith("selfremove", StringComparison.Ordinal);
        string root = Path.Combine(directory, "C08Boundary", scenario + (maskFirst ? "-mask-first" : "-color-first")
            + (unityExposure ? unityFirstBuild ? "-first-build-on" : "-first-build-off" : ""));
        string color = Path.Combine(root, "color", "Textures", "C08Boundary", "Color.png"),
            mask = Path.Combine(root, "mask", "Textures", "C08Boundary", "Mask.dds"),
            trigger = Path.Combine(root, "trigger", "Textures", "C08Boundary", "Trigger.png");
        foreach (string path in new[] { color, mask, trigger }) Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(color, colorPng); File.WriteAllBytes(mask, maskDds); File.WriteAllBytes(trigger, colorPng);
        var mod = new ModContentPack(new DirectoryInfo(root), "fixture.c08.boundary", "fixture.c08.boundary", 0, "Private C08 boundary", false);
        mod.foldersToLoadDescendingOrder = (maskFirst ? new[] { "mask", "trigger", "color" } : new[] { "color", "trigger", "mask" }).Select(p => Path.Combine(root, p)).ToList();
        var def = new ThingDef { defName = "C08BoundaryPrivate", modContentPack = mod, graphicData = new GraphicData {
            texPath = "C08Boundary/Color", maskPath = "C08Boundary/Mask", graphicClass = typeof(Graphic_Single), shaderType = ShaderTypeDefOf.CutoutComplex } };
        var mods = LoadedModManager.RunningModsListForReading; var defs = DefDatabase<ThingDef>.AllDefsListForReading;
        var originalMods = mods.ToArray(); var originalDefs = defs.ToArray();
        var objects = new HashSet<Texture2D>(); object? store = null;
        mods.Add(mod); defs.Add(def);
        try
        {
            var selected = ((IEnumerable<KeyValuePair<string, FileInfo>>)Call("PngRuntime", "SelectedFiles", mod)!).ToArray();
            Require(selected.Length == 3 && selected[0].Value.FullName == (maskFirst ? mask : color)
                && selected[1].Value.FullName == trigger && selected[2].Value.FullName == (maskFirst ? color : mask), "real provider enumeration order differs from requested variant");
            firstPath = selected[0].Value.FullName; triggerPath = scenario == "callback-existing" ? firstPath : trigger;
            changePath = mask; changed = (byte[])maskDds.Clone();
            int payload = BitConverter.ToUInt32(changed, 84) == 0x30315844 ? 148 : 128;
            for (int i = payload; i < Math.Min(payload + 16, changed.Length); i++) changed[i] ^= 0x35;
            string saveRoot = Path.Combine(root, "owned-profile"); Directory.CreateDirectory(saveRoot);
            Set("PreparedTextureRuntime", "SaveRoot", saveRoot);
            Set("PreparedTextureRuntime", "startupStore", null);
            store = Activator.CreateInstance(Product("PreparedTextureStore"), BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object?[] { saveRoot, null, null, false }, null)!;
            Set("PreparedTextureRuntime", "startupStore", store);
            string selection = (string)Call("PreparedTextureRuntime", "SelectionIdentity", mod, selected)!;
            string active = (string)Call("PreparedTextureRuntime", "ActiveIdentity")!;
            Array records = Array.CreateInstance(Product("PreparationRecord"), 2);
            for (int i = 0; i < 2; i++)
            {
                bool isMask = i == 1; string path = isMask ? mask : color;
                object record = Activator.CreateInstance(Product("PreparationRecord"))!;
                foreach (var field in new Dictionary<string, object> { ["Provider"] = selection, ["Logical"] = selected.Single(p => p.Value.FullName == path).Key,
                    ["Physical"] = path, ["SourceDigest"] = Hash(isMask ? maskDds : colorPng), ["Active"] = active, ["Runtime"] = AccessTools.Field(Product("PreparationContract"), "RuntimeGog").GetRawConstantValue()!,
                    ["Helper"] = helper, ["Role"] = isMask ? "mask" : "color", ["RoleIdentity"] = "provisional", ["Options"] = options, ["Output"] = isMask ? maskOutput : colorOutput }) Set(record, field.Key, field.Value);
                records.SetValue(record, i);
            }
            Require((bool)Invoke(store, "PublishQualityGroup", records, CancellationToken.None)!, "private quality group publication failed");
            var baseline = new Dictionary<string, string>(StringComparer.Ordinal);
            var holder = mod.GetContentHolder<Texture2D>();
            for (int pass = 0; pass < 2; pass++)
            {
                if (retainedMaterial != null) Object.DestroyImmediate(retainedMaterial); retainedMaterial = null;
                retainedImages.Clear();
                if (unityExposure) retainedMaterial = new Material(ShaderDatabase.Cutout);
                // A native ReloadAll preserves existing keys. Each independent
                // control/candidate load starts with the native empty holder.
                holder.ClearDestroy();
                candidate = pass == 1; RemoveHooks(); Call("PreparedQualityRuntime", "Finish");
                if (scenario == "collection-callback") Set("PngRuntime", "firstBuildSelected", candidate);
                // Each pass observes its own publication changes. Reset before
                // installing either the preexisting hook or late scheduler.
                Call("QualityUnityCallbacks", "Initialize");
                File.WriteAllBytes(mask, maskDds); originalTicks = File.GetLastWriteTimeUtc(mask).Ticks;
                callbackWrites = installs = ddsCalls = callbackNativeWidth = 0;
                qualityCollections = qualityAbsent = qualityRepeated = qualityHolderCallbacks = 0;
                long appliedBefore = Convert.ToInt64(AccessTools.Field(Product("PreparedQualityRuntime"), "Applied").GetValue(null));
                long errorsBefore = Convert.ToInt64(AccessTools.Field(Product("PngRuntime"), "errors").GetValue(null));
                long pngUploadsBefore = Convert.ToInt64(AccessTools.Field(Product("PngRuntime"), "firstBuildPngUploads").GetValue(null));
                Selected(candidate);
                if (candidate && (scenario == "clean" || scenario == "collection-callback"))
                    schedule.Patch(AccessTools.Method(Product("PreparedQualityRuntime"), "Loaded"),
                        prefix: new HarmonyMethod(typeof(C08BoundaryProbe), nameof(ObserveCollection)),
                        postfix: new HarmonyMethod(typeof(C08BoundaryProbe), nameof(AfterCollection)));
                if (scenario.EndsWith("existing", StringComparison.Ordinal) || scenario == "collection-callback") InstallLate();
                else if (scenario != "clean")
                {
                    if (candidate) schedule.Patch(AccessTools.Method(Product("PreparedQualityRuntime"), "Loaded"), postfix: new HarmonyMethod(typeof(C08BoundaryProbe), nameof(ProductAfterItem)));
                    else schedule.Patch(Ctor, postfix: new HarmonyMethod(typeof(C08BoundaryProbe), nameof(NativeAfterItem)));
                }
                if (candidate) Call("PngRuntime", "ReloadTextures", holder, false); else holder.ReloadAll(false);
                foreach (var texture in holder.contentList.Values) if (texture != null && !ReferenceEquals(texture, BaseContent.BadTex)) objects.Add(texture);
                Require(holder.contentList.Count == 3, "real ReloadAll did not populate all three holder keys");
                foreach (var pair in selected)
                {
                    string stem = (string)Call("PreparationContract", "Stem", pair.Key)!;
                    Require(holder.contentList.TryGetValue(stem, out var actual) && actual != null && !ReferenceEquals(actual, BaseContent.BadTex), "native loading failed: " + pair.Key);
                    bool converted = candidate && scenario == "clean" && pair.Value.FullName != trigger;
                    Require(actual!.width == expectedWidth / (converted ? 2 : 1) && actual.height == expectedHeight / (converted ? 2 : 1), "mixed or incorrect native/quality dimensions");
                    string revision = (string)Call("AtlasProvenance", "Revision", actual)!;
                    if (!candidate) baseline[pair.Key] = revision;
                    else if (!converted) Require(revision == baseline[pair.Key], "native callback output differs from ordinary ReloadAll: " + pair.Key);
                    else
                    {
                        object expected = Call("PreparedQualityRuntime", "ToEntry", pair.Value.FullName == mask ? maskOutput : colorOutput)!;
                        Texture2D restored = (Texture2D)Call("PngRuntime", "Restore", expected)!;
                        try { Require(revision == (string)Call("AtlasProvenance", "Revision", restored)!, "clean quality holder pixels differ from prepared representation"); }
                        finally { Object.DestroyImmediate(restored); }
                    }
                    Receipt("event", "holder", "mode", scenario, "maskFirst", maskFirst, "candidate", candidate, "logical", pair.Key,
                        "width", actual.width, "height", actual.height, "mips", actual.mipmapCount, "holderId", actual.GetInstanceID(),
                        "allMipGpuRevision", revision, "sourceSha256", Hash(File.ReadAllBytes(pair.Value.FullName)), "converted", converted);
                }
                Require(scenario == "clean" || installs == 1, "late/preexisting fault injection did not run exactly once");
                if (unityExposure)
                {
                    Require(retainedImages.Count == 2 && retainedImages.All(t => t != null && t.width == expectedWidth),
                        "native LoadImage callback was suppressed or its retained texture was destroyed/reduced");
                    var actualColor = holder.contentList["C08Boundary/Color"];
                    var actualTrigger = holder.contentList["C08Boundary/Trigger"];
                    Require(retainedImages.Any(t => ReferenceEquals(t, actualColor)) && retainedImages.Any(t => ReferenceEquals(t, actualTrigger)),
                        "native holder no longer owns the callback's retained objects");
                    Require(retainedMaterial != null && ReferenceEquals(retainedMaterial.mainTexture, actualColor),
                        "material binding lost its live native color object");
                    if (selfRemove) Require(!(Harmony.GetPatchInfo(LoadImageMethod)?.Owners.Contains(foreign.Id) ?? false),
                        "self-removing Unity callback remained installed");
                    long uploads = Convert.ToInt64(AccessTools.Field(Product("PngRuntime"), "firstBuildPngUploads").GetValue(null)) - pngUploadsBefore;
                    Require(uploads == 0, "first-build upload bypassed the real Unity callback");
                    Receipt("event", "unity-retained", "mode", scenario, "candidate", candidate, "firstBuild", unityFirstBuild,
                        "callbackCount", retainedImages.Count, "nativeColorId", actualColor.GetInstanceID(),
                        "materialTextureId", retainedMaterial!.mainTexture.GetInstanceID(), "retainedObjectsAlive", true,
                        "firstBuildUploads", uploads, "selfRemoved", selfRemove, "passed", true);
                }
                if (scenario.StartsWith("dds", StringComparison.Ordinal))
                    Require(ddsCalls == (maskFirst && scenario == "dds-late" ? 0 : 1),
                        "foreign DDS callback count differs from the actual source order");
                if (scenario.StartsWith("callback", StringComparison.Ordinal))
                {
                    Require(callbackWrites == 1 && callbackNativeWidth == expectedWidth, "callback was suppressed, repeated, or saw reduced quality instead of native input");
                    Require(File.ReadAllBytes(mask).SequenceEqual(changed) && File.GetLastWriteTimeUtc(mask).Ticks == originalTicks,
                        "callback source change did not preserve length/timestamp");
                    if (selfRemove) Require(!(Harmony.GetPatchInfo(Ctor)?.Owners.Contains(foreign.Id) ?? false), "self-removing callback hook remained installed");
                }
                long applied = Convert.ToInt64(AccessTools.Field(Product("PreparedQualityRuntime"), "Applied").GetValue(null)) - appliedBefore;
                Require(applied == (candidate && scenario == "clean" ? 2 : 0), "group applied across an unsafe boundary");
                Require(Convert.ToInt64(AccessTools.Field(Product("PngRuntime"), "errors").GetValue(null)) == errorsBefore, "source callback caused loader/locked-file error");
                if (candidate && (scenario == "clean" || scenario == "collection-callback"))
                {
                    Require(qualityCollections == 2 && qualityAbsent == 1 && qualityRepeated == 2,
                        "quality owner/no-op observations did not cover the private selection");
                    Require(qualityHolderCallbacks == (scenario == "collection-callback" ? 2 : 0),
                        "actual collection holder callback count differs");
                    Receipt("event", "collection-boundary", "mode", scenario, "passed", true,
                        "realCollections", qualityCollections, "absentOwnerNoOps", qualityAbsent,
                        "alreadyCollectedNoOps", qualityRepeated, "quiescentHolderCallbacks", qualityHolderCallbacks,
                        "populatedFirstBuildQueueChecked", scenario == "collection-callback");
                }
                Receipt("event", "pass", "mode", scenario, "maskFirst", maskFirst, "candidate", candidate, "passed", true,
                    "callbackWrites", callbackWrites, "hookInstalls", installs, "ddsHookCalls", ddsCalls, "qualityApplied", applied,
                    "qualityStatus", AccessTools.Field(Product("PreparedQualityRuntime"), "Status").GetValue(null),
                    "lateInjectionPoint", candidate ? "product Loaded postfix after actual native constructor" : "native constructor postfix");
            }
        }
        finally
        {
            collecting = null; repeatingCollection = false;
            if (scenario == "collection-callback") Set("PngRuntime", "firstBuildSelected", false);
            RemoveHooks(); Selected(false); Call("PreparedQualityRuntime", "Finish");
            if (retainedMaterial != null) Object.DestroyImmediate(retainedMaterial); retainedMaterial = null;
            foreach (var texture in retainedImages) if (texture != null) objects.Add(texture);
            retainedImages.Clear();
            Set("PreparedTextureRuntime", "startupStore", null); if (store is IDisposable disposable) disposable.Dispose();
            foreach (var texture in mod.GetContentHolder<Texture2D>().contentList.Values) if (texture != null && !ReferenceEquals(texture, BaseContent.BadTex)) objects.Add(texture);
            foreach (var texture in objects) if (texture != null) Object.DestroyImmediate(texture);
            mod.GetContentHolder<Texture2D>().contentList.Clear(); defs.Remove(def); mods.Remove(mod);
            Require(mods.SequenceEqual(originalMods) && defs.SequenceEqual(originalDefs), "private provider/metadata restoration changed original list/order");
        }
    }
}
