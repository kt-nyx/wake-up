// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using RimWorld.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Verse;
using Object = UnityEngine.Object;

namespace WakeUp;

internal static class PngRuntime
{
    internal const string Owner = "wakeup.png-processing";
    internal static readonly MethodInfo Reload = AccessTools.Method(typeof(ModContentPack), "ReloadContentInt");
    internal static readonly MethodInfo Holder = AccessTools.Method(typeof(ModContentHolder<Texture2D>), "ReloadAll");
    internal static readonly MethodInfo LoadAll = AccessTools.Method(typeof(ModContentLoader<Texture2D>), "LoadAllForMod");
    internal static readonly MethodInfo LoadItem = AccessTools.Method(typeof(ModContentLoader<Texture2D>), "LoadItem");
    internal static readonly MethodInfo LoadTexture = AccessTools.Method(typeof(ModContentLoader<Texture2D>), "LoadTexture");
    internal static readonly MethodInfo Image = AccessTools.Method(typeof(ModContentLoader<Texture2D>), "LoadTextureViaImageConversion");
    internal static readonly MethodInfo Compression = AccessTools.Method(typeof(StaticTextureAtlas), "FastCompressDXT");
    internal static readonly Type IteratorType = ((IteratorStateMachineAttribute)LoadAll.GetCustomAttributes(typeof(IteratorStateMachineAttribute), false).Single()).StateMachineType;
    internal static readonly MethodInfo Iterator = AccessTools.Method(IteratorType.ContainsGenericParameters ? IteratorType.MakeGenericType(typeof(Texture2D)) : IteratorType, "MoveNext");
    internal static readonly MethodInfo[] Targets = { Reload, Holder, LoadAll, LoadItem, LoadTexture, Image, Compression, Iterator };
    internal static readonly Dictionary<string, string> Expected = new()
    {
        ["MoveNext"] = "7731692E001389BA99F4770A61EADB4E62D68BABD442A063463CD77887A49528",
        ["ReloadContentInt"] = "F098E0DEC235A018D5D516F6865528EF34046E66AEBE879D3067E41D75892DF5",
        ["ReloadAll"] = "57548597C4665113B0F43EDE58319086B2FF6108E7C0E6A602ED18A66DD0397C",
        ["LoadAllForMod"] = "7799E56A76587D1E3345A0A7ABE291A0C3BB88CC586F83C5DC87A222430109C5",
        ["LoadItem"] = "639A1F216D2CFA6D50851891D9E36FE7DA764D3EE3EE079F086884EA5F8FD9A6",
        ["LoadTexture"] = "C80C9C4771C2C66B171789815504AA01E77AAA6C1FA9AD3EE61120466CC75C64",
        ["LoadTextureViaImageConversion"] = "11D67C113AF78871A8C98472B7B0994645B2B78C3481320181AB42A5223F268D",
        ["FastCompressDXT"] = "5886FBD4F39C70EFAEC23ACF2501CCDD110F53D550B08C95A677A95075959B42",
    };
    // Linux changes LoadItem's audio branch, not texture selection/conversion.
    // Derived from the original captured rev600 assembly, never a patched body.
    internal static string? ExpectedBody(GameBuildContract build, string method) =>
        build.IsLinux && method == "LoadItem"
            ? "BA99B5D41EAB8C9EC052A993DB210E88205297F3D51798A9E02DC0B3C29F4509"
            : Expected.TryGetValue(method, out string hash) ? hash : null;
    internal static string CacheGameIdentity(Guid gameMvid) => "png-v4|" + gameMvid.ToString("D");
    private static readonly List<PublishedPatchGuard> Guards = new();
    private delegate Texture2D ImageDelegate(VirtualFile file);
    private static readonly ImageDelegate NativeImage = (ImageDelegate)Delegate.CreateDelegate(typeof(ImageDelegate), Image);
    private static readonly Func<FileInfo, VirtualFile> ToVirtualFile = (Func<FileInfo, VirtualFile>)Delegate.CreateDelegate(typeof(Func<FileInfo, VirtualFile>),
        typeof(VirtualFile).Assembly.GetType("RimWorld.IO.FilesystemFile", true).GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => (m.Name == "op_Implicit" || m.Name == "op_Explicit") && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(FileInfo) })));
    private static string mode = "off", logPath = "", platform = "";
    private static bool installed, finished, verifying, originalPngSource, floatReadback;
    private static GraphicsDeviceType backend;
    private static PngCapturePath? reportedPath;
    private static PngCache? cache;
    private static PngLoadingProgressBridge? loadingProgress;
    private static Context? current;
    private static readonly HashSet<string> VerifiedShapes = new();
    private sealed class Context
    {
        internal byte[]? Source, Pixels;
        internal List<byte[]> Blocks = new();
        internal bool CaptureFailed;
        internal PngCapturePath CapturePath;
    }
    private static long calls, errors, verified, verifiedUncompressed, nativeFallbacks, mismatches, pngTicks, reloadTicks, sourceBytes, captureTicks, verifyTicks;
    private static long nativeReloads, loadingProgressReloads, reloadFallbacks;
    private static bool CacheMode => mode == "cache" || mode == "verify-cache";
    private static bool Active => installed && !finished && !verifying && UnityData.IsInMainThread;
    private static bool Compatible() => Guards.All(g => g.AllowsOriginalContract()) && (loadingProgress?.Compatible() ?? true);

    internal static void TryInitialize(IReadOnlyList<string> args)
    {
        string[] values = args.Where(s => s.StartsWith("--wake-up-png=", StringComparison.Ordinal)).ToArray();
        var launch = StartupLaunchSelector.Parse(args);
        if (values.Length != 1 || launch.Selection != StartupSelection.Candidate || PlayDataLoader.Loaded)
            return;
        mode = values[0].Substring("--wake-up-png=".Length);
        originalPngSource = args.Contains("--wake-up-png-source=original");
        if (!new[] { "control", "cache", "verify-cache" }.Contains(mode))
            return;
        logPath = Path.Combine(launch.SaveDataRoot!, "WakeUp", "png-processing.jsonl");
        long start = Stopwatch.GetTimestamp();
        var harmony = new Harmony(Owner);
        try
        {
            if (!RuntimeIdentity.ValidateBinaryIdentity(out var reason))
                throw new InvalidOperationException(reason);
            if (LoadedModManager.RunningModsListForReading.Any(m => m.PackageId == LoadingProgressCompatibility.PackageId))
            {
                if (!LoadingProgressCompatibility.TryInitialize())
                    throw new InvalidOperationException("unsupported-loading-progress-png");
                loadingProgress = new PngLoadingProgressBridge(LoadingProgressCompatibility.FrameworkAssembly!);
            }
            foreach (var target in Targets)
            {
                if (!SemanticMethodIdentity.TryHash(target, out string hash, out _)
                    || hash != ExpectedBody(GameBuildContract.Current, target.Name))
                    throw new InvalidOperationException("body-" + target.Name);
                Func<Patch, bool>? allowed = target == Reload && loadingProgress != null ? loadingProgress.AllowsReloadPatch : null;
                if (!PublishedPatchGuard.TryCreate(target, Owner, out var guard, true, allowed) || !guard!.AllowsOriginalContract())
                    throw new InvalidOperationException("hook-" + target.Name);
                Guards.Add(guard!);
            }
            // Only our nongeneric stubs are replaced. Generic game methods are never detoured.
            harmony.CreateReversePatcher(Holder, new HarmonyMethod(typeof(PngRuntime), nameof(ReloadClone))).Patch();
            harmony.CreateReversePatcher(Image, new HarmonyMethod(typeof(PngRuntime), nameof(ImageClone))).Patch();
            harmony.CreateReversePatcher(Compression, new HarmonyMethod(typeof(PngRuntime), nameof(CompressClone))).Patch();
            harmony.Patch(Reload, transpiler: new HarmonyMethod(typeof(PngRuntime), nameof(ReloadTranspiler)));
            if (loadingProgress != null)
                harmony.Patch(loadingProgress.Iterator, transpiler: new HarmonyMethod(typeof(PngRuntime), nameof(LoadingProgressTranspiler)));
            harmony.Patch(AccessTools.Method(typeof(Root_Entry), "Update"), postfix: new HarmonyMethod(typeof(PngRuntime), nameof(Menu)));
            if (CacheMode)
                cache = new PngCache(Path.Combine(launch.SaveDataRoot!, "WakeUp", "PngCache"));
            installed = true;
            Write("installed", "\"mode\":\"" + mode + "\",\"loadingProgressBridge\":" + (loadingProgress != null ? "true" : "false")
                + ",\"ms\":" + Ms(Stopwatch.GetTimestamp() - start));
        }
        catch (Exception e) { harmony.UnpatchAll(Owner); Write("refused", "\"reason\":\"" + Escape(e.ToString()) + "\""); }
    }
    internal static IEnumerable<CodeInstruction> ReloadTranspiler(IEnumerable<CodeInstruction> instructions)
        => Replace(instructions, m => m.DeclaringType == typeof(ModContentHolder<Texture2D>) && m.Name == "ReloadAll", nameof(ReloadTextures), 1);
    internal static IEnumerable<CodeInstruction> LoadingProgressTranspiler(IEnumerable<CodeInstruction> instructions)
        => Replace(instructions, m => m == Holder, nameof(ReloadLoadingProgressTextures), 1);
    private static void ReloadTextures(ModContentHolder<Texture2D> holder, bool hotReload)
        => ReloadTexturesCore(holder, hotReload, fromLoadingProgress: false);
    private static void ReloadLoadingProgressTextures(ModContentHolder<Texture2D> holder, bool hotReload)
        => ReloadTexturesCore(holder, hotReload, fromLoadingProgress: true);
    private static void ReloadTexturesCore(ModContentHolder<Texture2D> holder, bool hotReload, bool fromLoadingProgress)
    {
        long start = Stopwatch.GetTimestamp();
        try
        {
            if (Active && Compatible())
            {
                if (fromLoadingProgress) loadingProgressReloads++; else nativeReloads++;
                ReloadClone(holder, hotReload);
            }
            else
            {
                reloadFallbacks++;
                holder.ReloadAll(hotReload);
            }
        }
        finally { reloadTicks += Stopwatch.GetTimestamp() - start; }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ReloadClone(ModContentHolder<Texture2D> holder, bool hotReload)
    {
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code) => Replace(code, m => m == LoadAll, nameof(Enumerate), 1);
        _ = Transpiler(null!);
        throw new NotImplementedException();
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Texture2D ImageClone(VirtualFile file)
    {
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code) => ImageTranspiler(code);
        _ = Transpiler(null!);
        throw new NotImplementedException();
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Texture2D CompressClone(Texture2D texture, bool deleteOriginal)
    {
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code) => Replace(code,
            m => m.DeclaringType == typeof(Graphics) && m.Name == "CopyTexture", nameof(CopyCompressionBlocks), 1);
        _ = Transpiler(null!);
        throw new NotImplementedException();
    }
    internal static IEnumerable<CodeInstruction> Replace(IEnumerable<CodeInstruction> instructions, Func<MethodInfo, bool> match, string replacement, int expected)
    {
        var list = instructions.Select(i => new CodeInstruction(i)).ToList();
        int count = 0;
        foreach (var i in list)
        if (i.operand is MethodInfo method && match(method))
        {
            i.opcode = OpCodes.Call;
            i.operand = AccessTools.Method(typeof(PngRuntime), replacement);
            count++;
        }
        if (count != expected)
            throw new InvalidOperationException("callsites-" + replacement + "-" + count);
        return list;
    }
    internal static IEnumerable<CodeInstruction> ImageTranspiler(IEnumerable<CodeInstruction> code)
    {
        code = Replace(code, m => m.Name == "ReadAllBytes" && m.DeclaringType == typeof(VirtualFile), nameof(ReadSource), 1);
        code = Replace(code, m => m.DeclaringType == typeof(Texture2D) && m.Name == "Apply", nameof(FinalizeTexture), 3);
        return Replace(code, m => m == Compression, nameof(Compress), 1);
    }
    // Preserve native file selection, DDS precedence and item/publication order.
    private static IEnumerable<Pair<string, LoadedContentItem<Texture2D>>> Enumerate(ModContentPack mod)
    {
        DeepProfiler.Start("Loading assets of type " + typeof(Texture2D) + " for mod " + mod);
        var files = ModContentPack.GetAllFilesForMod(mod, GenFilePaths.ContentPath<Texture2D>(), ModContentLoader<Texture2D>.IsAcceptableExtension);
        var png = new HashSet<string>(files.Keys.Select(k => k.ToLowerInvariant()).Where(k => k.EndsWith(".png")));
        var dds = new HashSet<string>(files.Keys.Select(k => k.ToLowerInvariant()).Where(k => k.EndsWith(".dds")
            && !(originalPngSource && png.Contains(k.Substring(0, k.Length - 4) + ".png"))));
        foreach (var pair in files)
        {
            string key = pair.Key;
            if (originalPngSource && key.EndsWith(".dds", StringComparison.OrdinalIgnoreCase) && png.Contains(key.Substring(0, key.Length - 4).ToLowerInvariant() + ".png"))
                continue;
            if (key.Length > 4 && !key.Substring(key.Length - 4).Equals(".dds", StringComparison.OrdinalIgnoreCase))
            {
                string lower = key.ToLowerInvariant();
                if (dds.Contains(lower.Substring(0, lower.Length - 4) + ".dds"))
                    continue;
            }
            var file = ToVirtualFile(pair.Value);
            LoadedContentItem<Texture2D> item;
            if (file.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && Active && Compatible())
            {
                try
                {
                    item = new LoadedContentItem<Texture2D>(file, file.Exists ? LoadPng(file) : null!);
                }
                catch (Exception e)
                {
                    errors++;
                    Log.Error($"Exception loading {typeof(Texture2D)} from file.\nabsFilePath: {file.FullPath}\nException: {e}");
                    item = new LoadedContentItem<Texture2D>(file, BaseContent.BadTex);
                }
            }
            else
                item = ModContentLoader<Texture2D>.LoadItem(file);
            if (item != null)
                yield return new Pair<string, LoadedContentItem<Texture2D>>(key, item);
        }
        DeepProfiler.End();
    }
    private static Texture2D LoadPng(VirtualFile file)
    {
        long start = Stopwatch.GetTimestamp();
        var previous = current;
        current = new Context();
        Texture2D? result = null;
        try
        {
            calls++;
            string? key = null;
            if (cache != null)
            {
                // Mod constructors run on the background loader. Graphics device
                // queries belong here, after the main-thread admission check.
                if (platform.Length == 0)
                {
                    backend = SystemInfo.graphicsDeviceType;
                    // A game update may change helpers outside the intercepted bodies.
                    // Never reuse processed output across game module identities.
                    platform = CacheGameIdentity(typeof(ModContentPack).Module.ModuleVersionId)
                        + "|" + Application.unityVersion + "|" + backend + "|" + SystemInfo.graphicsDeviceVersion
                        + "|" + SystemInfo.graphicsDeviceVendorID + "|" + SystemInfo.graphicsDeviceID + "|" + QualitySettings.activeColorSpace;
                    floatReadback = backend == GraphicsDeviceType.Direct3D11
                        && SystemInfo.IsFormatSupported(GraphicsFormat.R32G32B32A32_SFloat, FormatUsage.ReadPixels);
                }
                current.CapturePath = TexturePlatformSupport.SelectPngPath(GameBuildContract.Current, backend,
                    Prefs.TextureCompression, UnityData.ComputeShadersSupported, floatReadback);
                if (reportedPath != current.CapturePath)
                {
                    reportedPath = current.CapturePath;
                    Write("capture-path", "\"backend\":\"" + backend + "\",\"path\":\"" + current.CapturePath
                        + "\",\"computeShaders\":" + (UnityData.ComputeShadersSupported ? "true" : "false")
                        + ",\"textureCompression\":" + (Prefs.TextureCompression ? "true" : "false"));
                }
                if (current.CapturePath == PngCapturePath.Unsupported)
                {
                    nativeFallbacks++;
                    var native = NativeImage(file);
                    native.name = Path.GetFileNameWithoutExtension(file.Name);
                    return native;
                }
                current.Source = file.ReadAllBytes();
                sourceBytes += current.Source.Length;
                key = PngCache.Hex(PngCache.Hash(Encoding.UTF8.GetBytes(platform + "|" + current.CapturePath + "|" + Prefs.TextureCompression + "|" + UnityData.ComputeShadersSupported
                    + "|" + file.FullPath + "|" + PngCache.Hex(PngCache.Hash(current.Source)))));
                var entry = cache.Read(key);
                if (entry != null)
                {
                    try
                    {
                        result = Restore(entry);
                    }
                    catch (Exception e) { if (cache.Errors == 0) Write("restore-error", "\"reason\":\"" + Escape(e.ToString()) + "\""); cache.Errors++; cache.Hits--; cache.Misses++; cache.Invalidate(key); }
                }
            }
            if (result == null)
            {
                result = ImageClone(file);
                if (cache != null && key != null)
                {
                    byte[]? pixels = current.CaptureFailed ? null : current.Blocks.Count > 0 ? current.Blocks.SelectMany(b => b).ToArray() : current.Pixels;
                    if (pixels != null)
                        cache.Write(key, Describe(result, pixels));
                }
            }
            result.name = Path.GetFileNameWithoutExtension(file.Name);
            string shape = result.width + "|" + result.height + "|" + result.graphicsFormat + "|" + result.mipmapCount;
            bool uncompressed = result.format != TextureFormat.DXT5;
            if (mode == "verify-cache" && ((verified < 40 && (verified < 8 || VerifiedShapes.Add(shape))) || (uncompressed && verifiedUncompressed < 40)))
            {
                long verifyStart = Stopwatch.GetTimestamp();
                verifying = true;
                Texture2D? original = null;
                try
                {
                    original = NativeImage(file);
                    Compare(original, result);
                    verified++;
                    if (uncompressed)
                        verifiedUncompressed++;
                }
                finally { if (original != null) Object.DestroyImmediate(original); verifying = false; verifyTicks += Stopwatch.GetTimestamp() - verifyStart; }
            }
            return result;
        }
        finally { current = previous; pngTicks += Stopwatch.GetTimestamp() - start; }
    }
    private static byte[] ReadSource(VirtualFile file)
    {
        if (current?.Source != null)
            return current.Source;
        byte[] data = file.ReadAllBytes();
        sourceBytes += data.Length;
        return data;
    }
    private static void FinalizeTexture(Texture2D texture, bool updateMips, bool unreadable)
    {
        bool gpuCompression = Prefs.TextureCompression && UnityData.ComputeShadersSupported && texture.width % 4 == 0 && texture.height % 4 == 0
            && Math.Min(texture.width, texture.height) > 16;
        if (cache == null || current == null || gpuCompression || !texture.isReadable
            || !cache.CanCapture(PngCache.ExpectedBytes(texture.width, texture.height, (int)texture.format, texture.mipmapCount)))
        {
            texture.Apply(updateMips, unreadable);
            return;
        }
        // Capture the game's completed CPU compression and mipmaps, retaining
        // readability only until the copy is made. Never regenerate restored mips.
        ApplyWithReadableCapture(texture.Apply, () =>
        {
            long start = Stopwatch.GetTimestamp();
            try { current.Pixels = texture.GetRawTextureData(); }
            catch (Exception e)
            {
                current.CaptureFailed = true;
                if (cache.Errors == 0) Write("capture-error", "\"reason\":\"" + Escape(e.ToString()) + "\"");
                cache.Errors++;
            }
            finally { captureTicks += Stopwatch.GetTimestamp() - start; }
        }, updateMips, unreadable);
    }
    internal static void ApplyWithReadableCapture(Action<bool, bool> apply, Action capture, bool updateMips, bool unreadable)
    {
        apply(updateMips, false);
        try { capture(); }
        finally { if (unreadable) apply(false, true); }
    }
    private static Texture2D Compress(Texture2D texture, bool deleteOriginal)
        => cache != null && current?.CapturePath == PngCapturePath.D3D11CompressionBlocks
            ? CompressClone(texture, deleteOriginal) : StaticTextureAtlas.FastCompressDXT(texture, deleteOriginal);
    private static void CopyCompressionBlocks(Texture source, int srcElement, int srcMip, int srcX, int srcY, int width, int height,
        Texture destination, int dstElement, int dstMip, int dstX, int dstY)
    {
        Graphics.CopyTexture(source, srcElement, srcMip, srcX, srcY, width, height, destination, dstElement, dstMip, dstX, dstY);
        if (current == null || current.CaptureFailed)
            return;
        if (!(destination is Texture2D target) || cache == null
            || !cache.CanCapture(PngCache.ExpectedBytes(target.width, target.height, (int)target.format, target.mipmapCount)))
        {
            current.CaptureFailed = true;
            current.Blocks.Clear();
            return;
        }
        long start = Stopwatch.GetTimestamp();
        RenderTexture? readable = null;
        try
        {
            // D3D11 permits a bitwise copy within the R32G32B32A32 format family.
            // Unity does not expose ReadPixels for SInt, but does for SFloat.
            // CopyTexture performs no shader/numeric conversion, preserving BC blocks.
            if (!SystemInfo.IsFormatSupported(GraphicsFormat.R32G32B32A32_SFloat, FormatUsage.ReadPixels))
                throw new NotSupportedException("compression-readback-format");
            readable = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            Graphics.CopyTexture(source, srcElement, srcMip, readable, 0, 0);
            var request = AsyncGPUReadback.Request(readable, 0);
            request.WaitForCompletion();
            if (request.hasError)
                throw new InvalidOperationException("compressed-block-readback");
            current.Blocks.Add(request.GetData<byte>().ToArray());
        }
        catch (Exception e)
        {
            current.CaptureFailed = true;
            if (cache != null)
            {
                if (cache.Errors == 0)
                    Write("capture-error", "\"reason\":\"" + Escape(e.ToString()) + "\"");
                cache.Errors++;
            }
        }
        finally { if (readable != null) RenderTexture.ReleaseTemporary(readable); captureTicks += Stopwatch.GetTimestamp() - start; }
    }
    private static PngCache.Entry Describe(Texture2D t, byte[] data) => new()
    {
        Width = t.width,
        Height = t.height,
        GraphicsFormat = (int)t.graphicsFormat,
        TextureFormat = (int)t.format,
        Mips = t.mipmapCount,
        Filter = (int)t.filterMode,
        WrapU = (int)t.wrapModeU,
        WrapV = (int)t.wrapModeV,
        WrapW = (int)t.wrapModeW,
        Aniso = t.anisoLevel,
        Bias = t.mipMapBias,
        Pixels = data
    };
    private static Texture2D Restore(PngCache.Entry entry)
    {
        var texture = new Texture2D(entry.Width, entry.Height, (TextureFormat)entry.TextureFormat, entry.Mips,
            !GraphicsFormatUtility.IsSRGBFormat((GraphicsFormat)entry.GraphicsFormat), true);
        try
        {
            if ((int)texture.format != entry.TextureFormat || (int)texture.graphicsFormat != entry.GraphicsFormat || texture.mipmapCount != entry.Mips)
                throw new InvalidDataException("cache-format expected=" + entry.TextureFormat + "/" + entry.GraphicsFormat + "/" + entry.Mips + " actual=" + texture.format + "/" + texture.graphicsFormat + "/" + texture.mipmapCount);
            texture.LoadRawTextureData(entry.Pixels);
            texture.Apply(false, true);
            texture.filterMode = (FilterMode)entry.Filter;
            texture.wrapModeU = (TextureWrapMode)entry.WrapU;
            texture.wrapModeV = (TextureWrapMode)entry.WrapV;
            texture.wrapModeW = (TextureWrapMode)entry.WrapW;
            texture.anisoLevel = entry.Aniso;
            texture.mipMapBias = entry.Bias;
            return texture;
        }
        catch { Object.DestroyImmediate(texture); throw; }
    }
    private static void Compare(Texture2D a, Texture2D b)
    {
        bool equal = a.width == b.width && a.height == b.height && a.format == b.format && a.graphicsFormat == b.graphicsFormat && a.mipmapCount == b.mipmapCount
            && a.isReadable == b.isReadable && a.filterMode == b.filterMode && a.anisoLevel == b.anisoLevel && a.mipMapBias == b.mipMapBias
            && a.wrapModeU == b.wrapModeU && a.wrapModeV == b.wrapModeV && a.wrapModeW == b.wrapModeW;
        if (equal)
        for (int mip = 0; mip < a.mipmapCount; mip++)
        if (RenderHash(a, mip) != RenderHash(b, mip))
            equal = false;
        if (!equal)
            mismatches++;
        Write("comparison", "\"equal\":" + (equal ? "true" : "false") + ",\"width\":" + a.width + ",\"height\":" + a.height
            + ",\"mips\":" + a.mipmapCount + ",\"format\":\"" + a.graphicsFormat + "\"");
    }
    private static string RenderHash(Texture2D texture, int mip)
    {
        var active = RenderTexture.active;
        var filter = texture.filterMode;
        float bias = texture.mipMapBias;
        int width = Math.Max(1, texture.width >> mip), height = Math.Max(1, texture.height >> mip);
        var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Texture2D? readable = null;
        try
        {
            texture.filterMode = FilterMode.Point;
            texture.mipMapBias = 0;
            Graphics.Blit(texture, target);
            RenderTexture.active = target;
            readable = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            readable.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            return PngCache.Hex(PngCache.Hash(readable.GetRawTextureData()));
        }
        finally { texture.filterMode = filter; texture.mipMapBias = bias; RenderTexture.active = active; if (readable != null) Object.DestroyImmediate(readable); RenderTexture.ReleaseTemporary(target); }
    }
    private static void Menu()
    {
        if (!installed || finished || !PlayDataLoader.Loaded || LongEventHandler.AnyEventNowOrWaiting)
            return;
        finished = true;
        Write("complete", "\"mode\":\"" + mode + "\",\"calls\":" + calls + ",\"nativeFallbacks\":" + nativeFallbacks
            + ",\"nativeReloads\":" + nativeReloads + ",\"loadingProgressReloads\":" + loadingProgressReloads + ",\"reloadFallbacks\":" + reloadFallbacks
            + ",\"errors\":" + errors + ",\"verified\":" + verified + ",\"verifiedUncompressed\":" + verifiedUncompressed + ",\"mismatches\":" + mismatches + ",\"pngMs\":" + Ms(pngTicks)
            + ",\"reloadMs\":" + Ms(reloadTicks) + ",\"captureMs\":" + Ms(captureTicks) + ",\"verifyMs\":" + Ms(verifyTicks) + ",\"sourceBytes\":" + sourceBytes
            + ",\"hits\":" + (cache?.Hits ?? 0) + ",\"misses\":" + (cache?.Misses ?? 0) + ",\"writes\":" + (cache?.Writes ?? 0)
            + ",\"cacheErrors\":" + (cache?.Errors ?? 0) + ",\"cacheReadBytes\":" + (cache?.ReadBytes ?? 0) + ",\"cacheWrittenBytes\":" + (cache?.WrittenBytes ?? 0)
            + ",\"nativeHashCalls\":" + PngCache.NativeHashCalls + ",\"managedHashCalls\":" + PngCache.ManagedHashCalls + ",\"hashMs\":" + Ms(PngCache.HashTicks));
    }
    private static string Ms(long ticks) => (ticks * 1000d / Stopwatch.Frequency).ToString("F3", CultureInfo.InvariantCulture);
    private static string Escape(string text) => JsonLineLog.Escape(text);
    private static void Write(string kind, string fields)
        => JsonLineLog.WriteEvent(logPath, kind, fields);
}

