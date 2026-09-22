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
using HarmonyLib;
using UnityEngine;
using Verse;
using Object = UnityEngine.Object;

namespace FixtureMenuObserver;

// Private, bounded functional observer. Calling a window controller is explicitly
// not visible UI inspection. No performance result is derived from this probe.
internal static class C08QualityProbe
{
    private static readonly string[] requested = {
        "Things/Building/Misc/Brazier", "Things/Building/Misc/Brazier_m", "Things/Building/Furniture/PlantPot" };
    private sealed class Sample
    {
        internal object Source = null!, Role = null!;
        internal ModContentPack Provider = null!;
        internal FileInfo File = null!;
        internal string Logical = "", Selection = "", Stem = "", Kind = "";
        internal int Preset;
    }
    private static readonly List<Sample> samples = new();
    private static string directory = "", phase = "";
    private static bool started, finished;
    private static int cursor, checks, failures, controllerIndex;
    private static Window? window;
    private static Type Product(string name) => AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "WakeUp").GetType("WakeUp." + name, true)!;
    private static object? Read(object value, string name) => AccessTools.Field(value.GetType(), name)?.GetValue(value)
        ?? AccessTools.Property(value.GetType(), name)?.GetValue(value, null);
    private static object? Call(string type, string method, params object?[] args) => AccessTools.Method(Product(type), method).Invoke(null, args);
    private static object? Invoke(object value, string method, params object?[] args) => AccessTools.Method(value.GetType(), method).Invoke(value, args);
    private static string Hash(byte[] bytes) { using var h = SHA256.Create(); return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-", ""); }
    private static string Quote(object? v) => "\"" + Convert.ToString(v, CultureInfo.InvariantCulture)!.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
    private static void Receipt(params object?[] values)
    {
        var fields = new List<string>();
        for (int i = 0; i < values.Length; i += 2)
        {
            object? v = values[i + 1];
            fields.Add(Quote(values[i]) + ":" + (v == null ? "null" : v is bool b ? b.ToString().ToLowerInvariant()
                : v is int || v is long || v is float ? Convert.ToString(v, CultureInfo.InvariantCulture) : Quote(v)));
        }
        File.AppendAllText(Path.Combine(directory, "c08-quality-probe.jsonl"), "{" + string.Join(",", fields) + "}\n");
    }
    private static void Assert(bool condition, string reason) { if (!condition) throw new InvalidDataException(reason); checks++; }
    private static object OpenStore() => Activator.CreateInstance(Product("PreparedTextureStore"), BindingFlags.Instance | BindingFlags.NonPublic,
        null, new object?[] { GenFilePaths.SaveDataFolderPath, null, null, false }, null)!;
    private static Texture2D Holder(Sample s)
    {
        Assert(s.Provider.GetContentHolder<Texture2D>().contentList.TryGetValue(s.Stem, out var texture) && texture != null,
            "actual provider holder is missing: " + s.Logical);
        return texture!;
    }
    internal static void Start(string outputDirectory)
    {
        if (started) return;
        started = true; directory = outputDirectory; Directory.CreateDirectory(directory);
        phase = Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--fixture-c08-quality=", StringComparison.Ordinal))?.Substring(22) ?? "";
        try
        {
            Assert(new[] { "catalog", "prepare", "verify", "reset", "verify-native" }.Contains(phase), "unknown C08 probe phase: " + phase);
            Receipt("event", "start", "phase", phase, "gameMvid", typeof(Mod).Module.ModuleVersionId,
                "productMvid", Product("PreparedQualityRuntime").Module.ModuleVersionId,
                "graphics", SystemInfo.graphicsDeviceType, "controllerOnly", true, "actualUiVerified", false);
            object roles = Call("PreparedTextureRoles", "Capture")!;
            Receipt("event", "catalog", "ready", Read(roles, "Ready"), "count", Read(roles, "Count"), "status", Read(roles, "Status"));
            Assert((bool)Read(roles, "Ready")!, "role catalog not ready: " + Read(roles, "Status"));
            foreach (object source in (IEnumerable)Call("PreparedQualityRuntime", "Sources")!)
            {
                string logical = (string)Read(source, "Logical")!;
                string stem = (string)Call("PreparationContract", "Stem", logical)!;
                if (!requested.Contains(stem, StringComparer.Ordinal)) continue;
                object?[] args = { logical, null };
                bool admitted = (bool)AccessTools.Method(roles.GetType(), "TryGet").Invoke(roles, args)!;
                Receipt("event", "candidate", "logical", logical, "provider", ((ModContentPack)Read(source, "Provider")!).PackageId,
                    "roleAdmitted", admitted, "reason", Invoke(roles, "Reason", logical));
                Assert(admitted, "natural source has no qualified role: " + logical);
                var s = new Sample { Source = source, Role = args[1]!, Provider = (ModContentPack)Read(source, "Provider")!,
                    File = (FileInfo)Read(source, "File")!, Logical = logical, Selection = (string)Read(source, "Selection")!, Stem = stem,
                    Kind = Convert.ToString(Read(args[1]!, "Kind"))!.ToLowerInvariant(), Preset = stem == requested[2] ? 1 : 2 };
                Assert(s.File.Length > 0 && s.File.Length <= 1024 * 1024, "probe source exceeds bounded input selection");
                samples.Add(s);
            }
            samples.Sort((a, b) => StringComparer.Ordinal.Compare(a.Selection + a.Logical, b.Selection + b.Logical));
            Assert(requested.All(p => samples.Any(s => s.Stem == p)) && samples.Count <= 12, "three natural source paths required; maximum twelve providers");
            Assert(samples.Any(s => s.Kind == "mask"), "natural mask role required");
            Receipt("event", "selection", "samples", samples.Count, "paths", string.Join(";", samples.Select(s => s.File.FullName)),
                "nativeStartupHits", AccessTools.Field(Product("PreparedTextureRuntime"), "Hits").GetValue(null),
                "qualityStartupApplied", AccessTools.Field(Product("PreparedQualityRuntime"), "Applied").GetValue(null));
            if (phase == "catalog") Finish();
        }
        catch (Exception e) { Fail(e); Finish(); }
    }
    internal static bool Advance()
    {
        if (!started || finished) return false;
        try
        {
            if (phase == "prepare" || phase == "reset")
            {
                if (phase == "prepare" && cursor < samples.Count) { CaptureNative(samples[cursor++]); return true; }
                if (window != null)
                {
                    window.WindowUpdate();
                    if ((bool)Read(window, "running")!) return true;
                    Assert((int)Read(window, "failed")! == 0, "quality controller reported failures: " + Read(window, "status"));
                    Receipt("event", "controller-finished", "index", controllerIndex - 1, "phase", phase,
                        "prepared", Read(window, "prepared"), "reused", Read(window, "reused"), "status", Read(window, "status"), "actualUiVerified", false);
                    window.PostClose(); window = null;
                }
                if (controllerIndex < 2) { StartController(controllerIndex++); return true; }
                VerifyPublished(); Finish(); return false;
            }
            if (cursor < samples.Count) { VerifyHolder(samples[cursor++], phase == "verify-native"); return true; }
            CheckMaterial();
            if (phase == "verify") Assert(Convert.ToInt64(AccessTools.Field(Product("PreparedQualityRuntime"), "Applied").GetValue(null)) >= samples.Count,
                "actual startup did not apply every expected quality holder");
            Finish(); return false;
        }
        catch (Exception e) { Fail(e); BeforeExit(); return false; }
    }
    private static void StartController(int group)
    {
        var selected = samples.Where(s => group == 0 ? s.Stem != requested[2] : s.Stem == requested[2]).ToArray();
        object mod = LoadedModManager.GetMod(Product("WakeUpMod"));
        object? settings = Read(mod, "settings");
        if (settings == null)
        {
            var get = typeof(Mod).GetMethods().First(m => m.Name == "GetSettings" && m.IsGenericMethodDefinition);
            settings = get.MakeGenericMethod(Product("WakeUpSettings")).Invoke(mod, null);
        }
        Assert(settings != null && (bool)Read(settings!, "Enabled")!, "Wake-Up must be enabled");
        window = (Window)Activator.CreateInstance(Product("TextureQualityWindow"), BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { settings!, selected.Select(s => s.Provider).Distinct().ToArray(), string.Join(";", selected.Select(s => s.Logical).Distinct()) }, null)!;
        object options = Read(window, "options")!;
        AccessTools.Property(options.GetType(), "Preset").SetValue(options, phase == "reset" ? 0 : group == 0 ? 2 : 1, null);
        AccessTools.Property(options.GetType(), "MipBias").SetValue(options, group == 0 ? 0.5f : -0.5f, null);
        AccessTools.Property(options.GetType(), "Anisotropy").SetValue(options, 4, null);
        Invoke(window, "Scan");
        Assert(((ICollection)Read(window, "rows")!).Count == selected.Length, "effective selection differs from complete expected provider group");
        Invoke(window, "Start");
        Assert(phase == "reset" || (bool)Read(window, "running")!, "quality controller did not start: " + Read(window, "status"));
    }
    private static object NativeEntry(Sample s)
    {
        byte[] source = (byte[])Call("PreparedTextureRuntime", "ReadSource", s.File)!;
        return Call("PngRuntime", "CapturePrepared", s.File, source, new Func<int, bool>(_ => true))
            ?? throw new InvalidDataException("native producer did not capture: " + s.Logical);
    }
    private static string NativeIdentity(Sample s, byte[] source) => (string)Call("PreparedTextureRuntime", "Identity", s.Selection,
        s.Logical, s.File.FullName, source, 0, Call("PngRuntime", "PreparationPlatform"), "")!;
    private static Delegate CaptureDelegate<T>(Sample s) where T : class => new Func<Func<int, bool>, T?>(admit =>
        (T?)Call("PngRuntime", "CapturePrepared", s.File, Call("PreparedTextureRuntime", "ReadSource", s.File), admit));
    private static void CaptureNative(Sample s)
    {
        byte[] source = (byte[])Call("PreparedTextureRuntime", "ReadSource", s.File)!;
        object native = NativeEntry(s), store = OpenStore();
        try
        {
            Delegate capture = (Delegate)typeof(C08QualityProbe).GetMethod(nameof(CaptureDelegate), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(native.GetType()).Invoke(null, new object[] { s })!;
            string result = (string)Call("TexturePreparationBatch", "Native", store, NativeIdentity(s, source),
                new Func<bool>(() => Hash(source) == Hash((byte[])Call("PreparedTextureRuntime", "ReadSource", s.File)!)), capture,
                System.Threading.CancellationToken.None, null, null)!;
            Assert(result.StartsWith("prepared", StringComparison.Ordinal) || result.StartsWith("reused", StringComparison.Ordinal), "native batch refused: " + result);
            Assert(Invoke(store, "Read", NativeIdentity(s, source)) != null, "native batch entry missing");
            Invoke(store, "Complete");
            Receipt("event", "native-prepared", "logical", s.Logical, "provider", s.Provider.PackageId, "result", result,
                "sourceSha256", Hash(source), "pixelsSha256", Hash((byte[])Read(native, "Pixels")!), "width", Read(native, "Width"), "height", Read(native, "Height"));
        }
        finally { ((IDisposable)store).Dispose(); }
    }
    private static void VerifyPublished()
    {
        object store = OpenStore();
        try
        {
            foreach (var s in samples)
            {
                object? record = Invoke(store, "ReadQuality", s.Selection, s.Logical);
                Assert(phase == "reset" ? record == null : record != null, "quality publication/reset missing: " + s.Logical);
                if (record != null) Assert((int)Read(Read(record, "Options")!, "Preset")! == s.Preset, "published preset mismatch");
            }
        }
        finally { ((IDisposable)store).Dispose(); }
    }
    private static void VerifyHolder(Sample s, bool native)
    {
        Texture2D actual = Holder(s), expected = null!;
        object store = OpenStore();
        try
        {
            byte[] source = (byte[])Call("PreparedTextureRuntime", "ReadSource", s.File)!;
            object entry;
            if (native)
            {
                Assert(Invoke(store, "ReadQuality", s.Selection, s.Logical) == null, "reset left quality entry");
                entry = Invoke(store, "Read", NativeIdentity(s, source)) ?? NativeEntry(s);
            }
            else
            {
                object record = Invoke(store, "ReadQuality", s.Selection, s.Logical) ?? throw new InvalidDataException("quality record absent");
                Assert((string)Read(record, "SourceDigest")! == Hash(source), "quality source digest mismatch");
                Assert((int)Read(Read(record, "Options")!, "Preset")! == s.Preset, "quality preset mismatch");
                entry = Call("PreparedQualityRuntime", "ToEntry", Read(record, "Output"))!;
            }
            expected = (Texture2D)Call("PngRuntime", "Restore", entry)!;
            Assert(actual.width == expected.width && actual.height == expected.height && actual.format == expected.format
                && actual.graphicsFormat == expected.graphicsFormat && actual.mipmapCount == expected.mipmapCount
                && actual.filterMode == expected.filterMode && actual.anisoLevel == expected.anisoLevel && actual.mipMapBias == expected.mipMapBias
                && actual.wrapModeU == expected.wrapModeU && actual.wrapModeV == expected.wrapModeV && actual.wrapModeW == expected.wrapModeW
                && actual.isReadable == expected.isReadable, "startup holder descriptor differs: " + s.Logical);
            string observed = (string)Call("AtlasProvenance", "Revision", actual)!, baseline = (string)Call("AtlasProvenance", "Revision", expected)!;
            Assert(observed == baseline, "all-mip GPU pixels differ from completed output: " + s.Logical);
            Texture2D global = ContentFinder<Texture2D>.Get(s.Stem, false);
            var winner = LoadedModManager.RunningModsListForReading.Last(m => m.GetContentHolder<Texture2D>().contentList.ContainsKey(s.Stem));
            Assert(ReferenceEquals(global, winner.GetContentHolder<Texture2D>().contentList[s.Stem]), "global winner is not last provider own holder");
            Receipt("event", "startup-holder", "passed", true, "nativeReset", native, "logical", s.Logical, "provider", s.Provider.PackageId,
                "sourceSha256", Hash(source), "width", actual.width, "height", actual.height, "format", actual.format, "mips", actual.mipmapCount,
                "filter", actual.filterMode, "bias", actual.mipMapBias, "aniso", actual.anisoLevel, "holderId", actual.GetInstanceID(),
                "globalId", global.GetInstanceID(), "globalWinner", winner.PackageId, "allMipGpuRevision", observed, "expectedRevision", baseline,
                "preparedPixelsSha256", Hash((byte[])Read(entry, "Pixels")!), "actualStartupHolder", true);
        }
        finally { if (expected != null) Object.DestroyImmediate(expected); ((IDisposable)store).Dispose(); }
    }
    private static void CheckMaterial()
    {
        ThingDef def = DefDatabase<ThingDef>.AllDefsListForReading.First(d => d.graphicData?.texPath == requested[0]);
        Material material = def.graphicData.Graphic.MatSingle;
        Texture2D color = ContentFinder<Texture2D>.Get(requested[0]), mask = ContentFinder<Texture2D>.Get(requested[1]);
        Assert(material.HasProperty(ShaderPropertyIDs.MaskTex) && ReferenceEquals(material.mainTexture, color)
            && ReferenceEquals(material.GetTexture(ShaderPropertyIDs.MaskTex), mask), "actual native graphic material did not consume selected pair");
        Assert(color.width == mask.width && color.height == mask.height, "actual paired dimensions disagree");
        Material blueprint = def.blueprintDef.graphicData.Graphic.MatSingle;
        bool blueprintMask = blueprint.HasProperty(ShaderPropertyIDs.MaskTex);
        bool selectedColor = ReferenceEquals(blueprint.mainTexture, color);
        bool selectedMask = !blueprintMask || ReferenceEquals(blueprint.GetTexture(ShaderPropertyIDs.MaskTex), mask);
        Receipt("event", "blueprint-material", "shader", blueprint.shader.name,
            "shaderPath", def.blueprintDef.graphicData.shaderType.shaderPath,
            "selectedColor", selectedColor, "hasMask", blueprintMask, "selectedMask", selectedMask);
        Assert(def.blueprintDef.graphicData.shaderType.shaderPath == "Map/EdgeDetect" && selectedColor && selectedMask,
            "native blueprint outline must consume the selected color and any supported mask");
        Receipt("event", "native-mask-material", "passed", true, "consumerDef", def.defName, "shader", material.shader.name,
            "materialId", material.GetInstanceID(), "colorId", color.GetInstanceID(), "maskId", mask.GetInstanceID(),
            "actualGameplaySceneRendered", false, "humanVisualQualityAccepted", false);
    }
    private static void Fail(Exception e) { failures++; Receipt("event", "failure", "passed", false, "phase", phase, "error", e.ToString()); }
    internal static void BeforeExit()
    {
        if (!started || finished) return;
        try { window?.PostClose(); } catch (Exception e) { Fail(e); }
        window = null;
        if (failures == 0) Fail(new OperationCanceledException("probe interrupted before completion"));
        Finish();
    }
    private static void Finish()
    {
        if (finished) return; finished = true;
        Receipt("event", "complete", "phase", phase, "passed", failures == 0 && checks > 0,
            "checks", checks, "failures", failures, "sources", samples.Count,
            "actualUiVerified", false, "performanceMeasured", false, "humanVisualQualityAccepted", false);
    }
}
