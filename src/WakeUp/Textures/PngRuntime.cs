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

internal static partial class PngRuntime
{
    internal const string Owner = "wakeup.png-processing";
    private static bool cacheRequested;
    private static string cacheRefusal = "";
    // Sampled only by the optional display's formatting budget; no per-image
    // formatting, disk reads, new receipt writes or change to supplier routing.
    internal static LoadingCacheSnapshot DisplaySnapshot()
        => new(cacheRequested, installed, cacheRefusal, CacheLaunchPolicy.Current.Action,
            cache?.Hits ?? 0, cache?.Misses ?? 0, cache?.Writes ?? 0,
            errors + (cache?.Errors ?? 0), cache?.StoredBytes ?? 0, cache?.LastReason ?? "none");
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
    internal static bool BodySupported(GameBuildContract build, string method, string hash)
        => hash == ExpectedBody(build, method) || method == "ReloadAll"
            // Production hashes the closed texture holder, unlike the open
            // generic definition used by the separate audio contract.
            && hash == "4898DC920548ECFC0A95B632A2810F7C30469A96F5E5B37FF194CF7DB9795252";
    internal static string CacheGameIdentity(Guid gameMvid) => "png-v4|" + gameMvid.ToString("D");
    // JPEG follows the same native image-conversion pipeline as PNG. Optional
    // PSD decoding is a separate explicit contract; DDS retains its own producer.
    internal static bool IsCacheableImageSource(string name) =>
        name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);
    internal static string SourceKey(string platformIdentity, PngCapturePath path, bool compression, bool compute,
        string sourcePath, byte[] source) => PngCache.Hex(PngCache.Hash(Encoding.UTF8.GetBytes(
            platformIdentity + "|" + path + "|" + compression + "|" + compute + "|" + sourcePath + "|" + PngCache.Hex(PngCache.Hash(source)))));
    private static readonly List<PublishedPatchGuard> Guards = new();
    private delegate Texture2D ImageDelegate(VirtualFile file);
    private static readonly ImageDelegate nativeTexture = (ImageDelegate)Delegate.CreateDelegate(typeof(ImageDelegate), LoadTexture);
    private static readonly ImageDelegate nativeImage = (ImageDelegate)Delegate.CreateDelegate(typeof(ImageDelegate), Image);
    private static Texture2D NativeTexture(VirtualFile file)
    { textureReadSession?.Suspend(); return nativeTexture(file); }
    private static Texture2D NativeImage(VirtualFile file)
    { textureReadSession?.Suspend(); return nativeImage(file); }
    private static readonly Func<FileInfo, VirtualFile> ToVirtualFile = (Func<FileInfo, VirtualFile>)Delegate.CreateDelegate(typeof(Func<FileInfo, VirtualFile>),
        typeof(VirtualFile).Assembly.GetType("RimWorld.IO.FilesystemFile", true).GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => (m.Name == "op_Implicit" || m.Name == "op_Explicit") && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(FileInfo) })));
    private static string mode = "off", logPath = "", platform = "";
    private static bool installed, finished, verifying, originalPngSource, floatReadback;
    private static Func<int, bool>? preparationAdmission;
    private static GraphicsDeviceType backend;
    private static PngCapturePath? reportedPath;
    private static PreparedTextureStore? cache;
    private static PngLoadingProgressBridge? loadingProgress;
    private static Context? current;
    private static readonly HashSet<string> VerifiedShapes = new();
    private sealed class Context
    {
        internal byte[]? Source, Pixels;
        internal string? Key;
        internal List<byte[]> Blocks = new();
        internal bool CaptureFailed;
        internal PngCapturePath CapturePath;
        internal FirstBuildImageData? Prepared;
        internal readonly List<Texture2D> OwnedTextures = new();
    }
    private static long restoreTicks, ddsIdentityTicks, preparationPlatformTicks;
    private static long calls, jpegCalls, errors, verified, verifiedUncompressed, nativeFallbacks, ddsNativePassThrough, mismatches, pngTicks, reloadTicks, sourceBytes, captureTicks, verifyTicks;
    private static long nativeReloads, loadingProgressReloads, reloadFallbacks;
    private static bool CacheMode => mode == "cache" || mode == "verify-cache";
    private static bool Active => installed && !finished && !verifying && UnityData.IsInMainThread;
    private static bool Compatible() => Guards.All(g => g.AllowsOriginalContract()) && (loadingProgress?.Compatible() ?? true);
    // Atlas capture replaces only the native compression copy operation. Its
    // exact coordinated patch may arrive after these guards are installed.
    // Other patches, including other methods under that owner, still refuse.
    internal static bool AllowsAtlasCompressionPatch(Patch patch) => patch.owner == AtlasRuntime.Owner
        && patch.PatchMethod == AccessTools.Method(typeof(AtlasRuntime), "CompressionTranspiler");

    internal static void TryInitialize(IReadOnlyList<string> args, bool preparationOnly = false)
    {
        string[] values = args.Where(s => s.StartsWith("--wake-up-png=", StringComparison.Ordinal)).ToArray();
        var launch = StartupLaunchSelector.Parse(args);
        if (installed || values.Length != 1 || launch.Selection != StartupSelection.Candidate || (PlayDataLoader.Loaded && !preparationOnly))
            return;
        mode = values[0].Substring("--wake-up-png=".Length);
        originalPngSource = args.Contains("--wake-up-png-source=original");
        if (!new[] { "control", "cache", "verify-cache", "prepare" }.Contains(mode))
            return;
        cacheRequested = CacheMode;
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
                    || !BodySupported(GameBuildContract.Current, target.Name, hash))
                    throw new InvalidOperationException("body-" + target.Name);
                Func<Patch, bool>? allowed = target == Reload ? p => LoadingObservationRuntime.AllowsHook(target, p)
                    || loadingProgress?.AllowsReloadPatch(p) == true : null;
                if (target == Compression) allowed = AllowsAtlasCompressionPatch;
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
            if (CacheMode && CacheLaunchPolicy.Current.AllowWrite)
                cache = PreparedTextureRuntime.StartupStore;
            NativeDdsCapture.Initialize(harmony);
            QualityUnityCallbacks.Initialize();
            PreparedQualityRuntime.Initialize(harmony);
            InitializeFirstBuild(args);
            installed = true;
            Write("installed", "\"mode\":\"" + mode + "\",\"loadingProgressBridge\":" + (loadingProgress != null ? "true" : "false")
                + ",\"ms\":" + Ms(Stopwatch.GetTimestamp() - start));
        }
        catch (Exception e) { cacheRefusal = e.Message; cache?.Dispose(); cache = null; harmony.UnpatchAll(Owner);
            if (PreparedTextureRuntime.UseAtStartup) PreparedTextureRuntime.Status = "Prepared texture loader refused: " + e.Message;
            Write("refused", "\"reason\":\"" + Escape(e.ToString()) + "\""); }
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
        textureReadSession?.RetireForNested(); // Nested work cannot inherit speculative buffers or another queue budget.
        try
        {
            if (Active && Compatible() && (mode != "prepare" || PreparedTextureRuntime.UseAtStartup))
            {
                if (fromLoadingProgress) loadingProgressReloads++; else nativeReloads++;
                bool previousPreparation = firstBuildReloadAllowed;
                firstBuildReloadAllowed = !hotReload;
                var previousHolder = consumerHolder;
                consumerHolder = holder;
                try { ReloadClone(holder, hotReload); PreparedQualityRuntime.CompleteReload(); }
                finally { consumerHolder = previousHolder; firstBuildReloadAllowed = previousPreparation; }
            }
            else
            {
                PreparedQualityRuntime.UnsafeBoundary("native reload has foreign callbacks");
                reloadFallbacks++;
                holder.ReloadAll(hotReload);
            }
        }
        finally { reloadTicks += Stopwatch.GetTimestamp() - start; }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ReloadClone(ModContentHolder<Texture2D> holder, bool hotReload)
    {
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code) => Replace(code, m => m == LoadAll, nameof(EnumerateKnownConsumer), 1);
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
    private static ModContentHolder<Texture2D>? consumerHolder;
    private static IEnumerable<Pair<string, LoadedContentItem<Texture2D>>> EnumerateKnownConsumer(ModContentPack mod)
        => EnumerateCore(mod, consumerHolder == null ? null : PngConsumerContract.Capture(consumerHolder));
    private static IEnumerable<Pair<string, LoadedContentItem<Texture2D>>> Enumerate(ModContentPack mod)
        => EnumerateCore(mod, null);
    private static IEnumerable<Pair<string, LoadedContentItem<Texture2D>>> EnumerateCore(ModContentPack mod, PngConsumerContract? consumer)
    {
        DeepProfiler.Start("Loading assets of type " + typeof(Texture2D) + " for mod " + mod);
        var selected = SelectedFiles(mod).ToArray();
        string selectionIdentity = PreparedTextureRuntime.SelectionIdentity(mod, selected);
        var previousSession = textureReadSession;
        previousSession?.RetireForNested();
        using var preparation = new TextureReadSession(selected, selectionIdentity, allowQueue: previousSession == null);
        Action beforeQualityCollection = preparation.Suspend;
        textureReadSession = preparation;
        void BeforeHolderConstructor()
        {
            PreparedQualityRuntime.BeforeCallback();
            preparation.BeforeCallback();
        }
        try
        {
        for (int index = 0; index < selected.Length; index++)
        {
            var pair = selected[index];
            PreparedQualityRuntime.BeforeSource(pair.Value);
            string key = pair.Key;
            var file = ToVirtualFile(pair.Value);
            LoadedContentItem<Texture2D> item;
            FirstBuildImageQueue<FirstBuildImageData>.Lease? lease = null;
            lease = preparation.Take(index);
            using (lease)
            {
            var previousFlight = firstBuildFlight;
            firstBuildFlight = lease;
            try
            {
            if ((IsCacheableImageSource(file.Name) || PreparedTextureRuntime.PsdSupport && file.Name.EndsWith(".psd", StringComparison.OrdinalIgnoreCase) || file.Name.EndsWith(".dds", StringComparison.OrdinalIgnoreCase) && NativeDdsCapture.Compatible) && Active && Compatible()
                && (!PreparedTextureRuntime.UseAtStartup || QualityUnityCallbacks.WarmArrayAllowed))
            {
                try
                {
                    // Retained PNG/JPEG/PSD reuse precedes native cold loading.
                    // Authored DDS stays native; C08 quality follows publication.
                    Texture2D? prepared = file.Exists ? preparation.RestoreWarm(lease) : null;
                    if (prepared == null && lease?.Released == true) firstBuildFlight = null;
                    prepared ??= file.Exists && (cache == null || PreparedTextureRuntime.UseAtStartup)
                        ? PreparedTextureRuntime.TryResolve(mod, pair.Key, pair.Value, selectionIdentity) : null;
                    Texture2D image = file.Exists ? prepared ?? (file.Name.EndsWith(".psd", StringComparison.OrdinalIgnoreCase) ? LoadProcessedPsd(file, pair.Key, selectionIdentity) : file.Name.EndsWith(".dds", StringComparison.OrdinalIgnoreCase) ? LoadProcessedDds(file, pair.Key, selectionIdentity) : LoadProcessedImage(file, pair.Key, selectionIdentity)) : null!;
                    BeforeHolderConstructor();
                    item = new LoadedContentItem<Texture2D>(file, image);
                }
                catch (Exception e)
                {
                    preparation.Suspend();
                    errors++;
                    Log.Error($"Exception loading {typeof(Texture2D)} from file.\nabsFilePath: {file.FullPath}\nException: {e}");
                    BeforeHolderConstructor();
                    item = new LoadedContentItem<Texture2D>(file, BaseContent.BadTex);
                }
            }
            else
            {
                preparation.Suspend();
                BeforeHolderConstructor();
                item = ModContentLoader<Texture2D>.LoadItem(file);
            }
            }
            finally
            {
                firstBuildFlight = previousFlight;
                if (PreparedTextureRuntime.UseAtStartup) QualityUnityCallbacks.Check();
            }
            }
            if (item != null)
            {
                PreparedQualityRuntime.Loaded(mod, key, pair.Value, selectionIdentity, item, beforeQualityCollection);
                preparation.BeforeYield(consumer, key);
                yield return new Pair<string, LoadedContentItem<Texture2D>>(key, item);
            }
        }
        }
        finally
        {
            preparation.Dispose();
            textureReadSession = previousSession;
        DeepProfiler.End();
        }
    }
    internal static bool QualityCallbacksAllowed(FileInfo file) => Active && Compatible() && QualityUnityCallbacks.Check()
        && (!file.Extension.Equals(".dds", StringComparison.OrdinalIgnoreCase) || NativeDdsCapture.Compatible);
    internal static bool QualityRouteAllowed(FileInfo file) => QualityCallbacksAllowed(file)
        && (IsCacheableImageSource(file.Name)
            || file.Extension.Equals(".dds", StringComparison.OrdinalIgnoreCase) && NativeDdsCapture.Compatible
            || file.Extension.Equals(".psd", StringComparison.OrdinalIgnoreCase) && PreparedTextureRuntime.PsdSupport);
    internal static IEnumerable<KeyValuePair<string, FileInfo>> SelectedFiles(ModContentPack mod)
    {
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
            yield return pair;
        }
    }
    private static Texture2D LoadProcessedPsd(VirtualFile file, string logical, string selection)
    {
        try
        {
            byte[] source = FirstBuildSource ?? PreparedTextureRuntime.ReadSource(new FileInfo(file.FullPath));
            string identity = PreparedTextureRuntime.Identity(selection, logical, file.FullPath, source, 0, PreparationPlatform(), "");
            var entry = cache?.Read(identity);
            if (entry != null)
            {
                try { var reused = Restore(entry); reused.name = Path.GetFileNameWithoutExtension(file.Name); return reused; }
                catch { cache!.Hits--; cache.Misses++; cache.Errors++; cache.Invalidate(identity); }
            }
            Action<PngCache.Entry>? publish = cache == null ? null :
                new Action<PngCache.Entry>(value => cache.Publish(identity, value));
            Func<int, bool>? admit = cache == null ? null : new Func<int, bool>(bytes => cache.CanPublish(identity, bytes));
            var decoded = firstBuildFlight?.Value?.Psd;
            textureReadSession?.Suspend();
            Texture2D result = decoded != null ? PsdTextureRuntime.CreateDecoded(decoded, publish, admit) : PsdTextureRuntime.Create(source, publish, admit);
            if (decoded != null && firstBuildFlight?.Released == false) firstBuildFlight.MarkConsumed();
            result.name = Path.GetFileNameWithoutExtension(file.Name); return result;
        }
        catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException || e is OverflowException || e is NotSupportedException)
        { nativeFallbacks++; return NativeImage(file); }
    }
    private static Texture2D LoadProcessedImage(VirtualFile file, string logicalPath, string selectionIdentity)
    {
        long start = Stopwatch.GetTimestamp();
        var previous = current;
        current = new Context { Source = FirstBuildSource,
            Prepared = FirstBuildCompatible() ? firstBuildFlight?.Value : null };
        Texture2D? result = null;
        try
        {
            calls++;
            if (cache == null && current.Source != null) sourceBytes += current.Source.Length;
            if (!file.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) jpegCalls++;
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
                // An exact snapshot supplies both the content key and native decoder.
                // Oversized sources keep the ordinary path without an extra copy.
                var info = new FileInfo(file.FullPath);
                if (info.Length > PreparedTextureRuntime.MaximumNativeSource)
                { nativeFallbacks++; var native = NativeImage(file); native.name = Path.GetFileNameWithoutExtension(file.Name); return native; }
                current.Source ??= PreparedTextureRuntime.ReadSource(info);
                sourceBytes += current.Source.Length;
                try { using var header = new MemoryStream(current.Source); _ = PreparationImageHeader.Read(header, true); }
                catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException || e is ArgumentException || e is OverflowException)
                { nativeFallbacks++; var native = ImageClone(file); native.name = Path.GetFileNameWithoutExtension(file.Name); return native; }
                key = PreparedTextureRuntime.Identity(selectionIdentity, logicalPath, file.FullPath, current.Source, 0, PreparationPlatform(), "");
                current.Key = key;
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
            VerifyImageResult(file, result);
            return result;
        }
        finally
        {
            foreach (var owned in current.OwnedTextures)
                if (owned != null && !ReferenceEquals(owned, result)) Object.DestroyImmediate(owned);
            current = previous; pngTicks += Stopwatch.GetTimestamp() - start;
        }
    }
    private static void VerifyImageResult(VirtualFile file, Texture2D result)
    {
            string shape = result.width + "|" + result.height + "|" + result.graphicsFormat + "|" + result.mipmapCount;
            bool uncompressed = result.format != TextureFormat.DXT5;
            if ((mode == "verify-cache" || mode == "verify-first-build") && ((verified < 40 && (verified < 8 || VerifiedShapes.Add(shape))) || (uncompressed && verifiedUncompressed < 40)))
            {
                long verifyStart = Stopwatch.GetTimestamp();
                verifying = true;
                Texture2D? original = null;
                try
                {
                    textureReadSession?.Suspend();
                    original = NativeImage(file);
                    Compare(original, result);
                    verified++;
                    if (uncompressed)
                        verifiedUncompressed++;
                }
                finally { if (original != null) Object.DestroyImmediate(original); verifying = false; verifyTicks += Stopwatch.GetTimestamp() - verifyStart; }
            }
    }
    private static Texture2D LoadProcessedDds(VirtualFile file, string logical, string selection)
    {
        ddsNativePassThrough++;
        return NativeTexture(file);
    }

    // Historical C05 implementation, deliberately disconnected from release loading.
    private static Texture2D LoadProcessedDdsResearch(VirtualFile file, string logical, string selection)
    {
        textureReadSession?.Suspend();
        // DDS already contains the game's GPU-ready representation. Ordinary
        // automatic caching consumes it directly; explicit prepared output was
        // tried by Enumerate, and optional disk compression retains grouped DDS.
        if (!PreparedTextureRuntime.CompressStorage)
        { ddsNativePassThrough++; return NativeTexture(file); }
        if (cache == null || !NativeDdsCapture.Compatible) return NativeTexture(file);
        // Native DDS does not need PNG's optional GPU readback. If the shared
        // preparation contract is unavailable, retain the game's DDS producer
        // before opening a second source read or hashing a cache identity.
        long identityStarted = Stopwatch.GetTimestamp();
        if (!TryPreparationPlatform(PreparationPlatform, out string capturePlatform))
        { nativeFallbacks++; return NativeTexture(file); }
        // Hold this lease until native mmap/capture completes. It excludes writes
        // and replacement, so the native reader consumes exactly the keyed bytes.
        using var lease = new FileStream(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (lease.Length > PreparedTextureRuntime.MaximumNativeSource) { lease.Dispose(); return NativeTexture(file); }
        byte[] source = PreparedTextureRuntime.ReadSourceSnapshot(lease);
        string identity = PreparedTextureRuntime.Identity(selection, logical, file.FullPath, source, 0, capturePlatform, "");
        ddsIdentityTicks += Stopwatch.GetTimestamp() - identityStarted;
        var entry = cache.Read(identity);
        Texture2D? result = null;
        if (entry != null)
        {
            try { result = Restore(entry); }
            catch { cache.Errors++; cache.Invalidate(identity); }
        }
        if (result == null)
            result = NativeDdsCapture.Capture(file, (texture, pixels) => cache.Publish(identity, Describe(texture, pixels)),
                bytes => cache.CanPublish(identity, bytes), lease);
        // Native LoadTexture tries image conversion when DDS declines the file.
        result ??= NativeImage(file);
        result.name = Path.GetFileNameWithoutExtension(file.Name);
        return result;
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
        textureReadSession?.Suspend();
        bool gpuCompression = Prefs.TextureCompression && UnityData.ComputeShadersSupported && texture.width % 4 == 0 && texture.height % 4 == 0
            && Math.Min(texture.width, texture.height) > 16;
        if ((cache == null && preparationAdmission == null) || current == null || gpuCompression || !texture.isReadable)
        {
            texture.Apply(updateMips, unreadable);
            return;
        }
        // Capture the game's completed CPU compression and mipmaps, retaining
        // readability only until the copy is made. Never regenerate restored mips.
        if (!TryCapture(NativeTextureData.ExpectedBytes(texture.width, texture.height, (int)texture.format, texture.mipmapCount),
            () => ApplyWithReadableCapture(texture.Apply, () =>
        {
            long start = Stopwatch.GetTimestamp();
            try { current.Pixels = texture.GetRawTextureData(); }
            catch (Exception e)
            {
                current.CaptureFailed = true;
                if ((cache?.Errors ?? 0) == 0) Write("capture-error", "\"reason\":\"" + Escape(e.ToString()) + "\"");
                if (cache != null) cache.Errors++;
            }
            finally { captureTicks += Stopwatch.GetTimestamp() - start; }
        }, updateMips, unreadable)))
            texture.Apply(updateMips, unreadable);
    }
    private static bool TryCapture(int bytes, Action capture)
    {
        if (preparationAdmission == null) return cache?.TryCapture(current?.Key, bytes, capture) ?? false;
        if (bytes < 1 || !preparationAdmission(bytes)) return false;
        capture();
        return true;
    }
    internal static bool PreparationAvailable => installed && UnityData.IsInMainThread && PlayDataLoader.Loaded
        && !LongEventHandler.AnyEventNowOrWaiting && current == null && Compatible();
    internal static bool TryPreparationPlatform(Func<string> getPlatform, out string value)
    {
        try { value = getPlatform(); return true; }
        catch (NotSupportedException) { value = ""; return false; }
    }
    internal static string PreparationPlatform()
    {
        long started = Stopwatch.GetTimestamp();
        try { return PreparationPlatformCore(); }
        finally { preparationPlatformTicks += Stopwatch.GetTimestamp() - started; }
    }
    private static string PreparationPlatformCore() => new NativePlatformState().Identity;

    // Keep the complete engine contract, but do not rebuild/hash its cache key
    // for every ordered warm result. Live comparison still checks every input.
    private sealed class NativePlatformState
    {
        private readonly Guid game;
        private readonly string unity, version;
        private readonly GraphicsDeviceType device;
        private readonly int vendor, deviceId;
        private readonly ColorSpace color;
        private readonly bool compression, compute, readback;
        private readonly PngCapturePath path;
        internal NativePlatformState()
        {
            if (!UnityData.IsInMainThread) throw new InvalidOperationException("preparation-main-thread");
            game = typeof(ModContentPack).Module.ModuleVersionId;
            unity = Application.unityVersion;
            device = SystemInfo.graphicsDeviceType;
            version = SystemInfo.graphicsDeviceVersion;
            vendor = SystemInfo.graphicsDeviceVendorID; deviceId = SystemInfo.graphicsDeviceID;
            color = QualitySettings.activeColorSpace;
            compression = Prefs.TextureCompression; compute = UnityData.ComputeShadersSupported;
            readback = device == GraphicsDeviceType.Direct3D11 && SystemInfo.IsFormatSupported(GraphicsFormat.R32G32B32A32_SFloat, FormatUsage.ReadPixels);
            path = TexturePlatformSupport.SelectPngPath(GameBuildContract.Current, device, compression, compute, readback);
            if (path == PngCapturePath.Unsupported) throw new NotSupportedException("preparation-platform");
        }
        internal string Identity => SourceKey(CacheGameIdentity(game) + "|" + unity + "|" + device + "|" + version
            + "|" + vendor + "|" + deviceId + "|" + color, path, compression, compute, "preparation", Array.Empty<byte>());
        internal bool MatchesLive()
        {
            var live = new NativePlatformState();
            return game == live.game && unity == live.unity && device == live.device && version == live.version
                && vendor == live.vendor && deviceId == live.deviceId && color == live.color
                && compression == live.compression && compute == live.compute && readback == live.readback && path == live.path;
        }
    }
    internal static string AutomaticPreparationKey(FileInfo file, byte[] source)
    {
        if (!UnityData.IsInMainThread) throw new InvalidOperationException("preparation-main-thread");
        var device = SystemInfo.graphicsDeviceType;
        var path = TexturePlatformSupport.SelectPngPath(GameBuildContract.Current, device, Prefs.TextureCompression,
            UnityData.ComputeShadersSupported, device == GraphicsDeviceType.Direct3D11
            && SystemInfo.IsFormatSupported(GraphicsFormat.R32G32B32A32_SFloat, FormatUsage.ReadPixels));
        return SourceKey(CacheGameIdentity(typeof(ModContentPack).Module.ModuleVersionId) + "|" + Application.unityVersion
            + "|" + device + "|" + SystemInfo.graphicsDeviceVersion + "|" + SystemInfo.graphicsDeviceVendorID + "|" + SystemInfo.graphicsDeviceID
            + "|" + QualitySettings.activeColorSpace, path, Prefs.TextureCompression, UnityData.ComputeShadersSupported, file.FullName, source);
    }
    internal static void ReleaseAutomaticPrepared(FileInfo file, byte[] source)
    {
        // Shared native entries no longer require promotion or duplicate removal.
    }
    internal static PngCache.Entry? CapturePrepared(FileInfo file, byte[] source, Func<int, bool> admit)
        => CapturePreparedCore(file, source, admit, null);
    internal static PngCache.Entry? CaptureDecoded(FileInfo file, byte[] source, Func<int, bool> admit, FirstBuildImageData decoded)
        => CapturePreparedCore(file, source, admit, decoded);
    private static PngCache.Entry? CapturePreparedCore(FileInfo file, byte[] source, Func<int, bool> admit, FirstBuildImageData? decoded)
    {
        if (!PreparationAvailable) throw new InvalidOperationException("preparation-session-unavailable");
        _ = PreparationPlatform();
        Texture2D? texture = null;
        var device = SystemInfo.graphicsDeviceType;
        current = new Context { Source = source, Prepared = decoded, CapturePath = TexturePlatformSupport.SelectPngPath(GameBuildContract.Current, device,
            Prefs.TextureCompression, UnityData.ComputeShadersSupported, device == GraphicsDeviceType.Direct3D11
            && SystemInfo.IsFormatSupported(GraphicsFormat.R32G32B32A32_SFloat, FormatUsage.ReadPixels)) };
        preparationAdmission = admit;
        try
        {
            if (file.Extension.Equals(".psd", StringComparison.OrdinalIgnoreCase))
            {
                if (decoded?.Psd == null) return PreparedTextureRuntime.PsdSupport ? PsdTextureRuntime.Capture(source, admit) : null;
                PngCache.Entry? psdEntry = null;
                texture = PsdTextureRuntime.CreateDecoded(decoded.Psd, entry => psdEntry = entry, admit);
                return psdEntry;
            }
            if (file.Extension.Equals(".dds", StringComparison.OrdinalIgnoreCase))
            {
                if (!NativeDdsCapture.Compatible) return null;
                using var lease = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
                if (!PngCache.Hash(PreparedTextureRuntime.ReadSourceSnapshot(lease)).SequenceEqual(PngCache.Hash(source))) return null;
                PngCache.Entry? captured = null;
                texture = NativeDdsCapture.Capture(ToVirtualFile(file), (native, data) => captured = Describe(native, data), admit, lease);
                return captured;
            }
            using (var header = new MemoryStream(source)) _ = PreparationImageHeader.Read(header, true);
            texture = ImageClone(ToVirtualFile(file));
            byte[]? pixels = current.CaptureFailed ? null : current.Blocks.Count > 0 ? current.Blocks.SelectMany(b => b).ToArray() : current.Pixels;
            return pixels == null || texture.isReadable ? null : Describe(texture, pixels);
        }
        finally
        {
            foreach (var owned in current.OwnedTextures)
                if (owned != null && !ReferenceEquals(owned, texture)) Object.DestroyImmediate(owned);
            preparationAdmission = null; current = null;
            if (texture != null) Object.DestroyImmediate(texture);
        }
    }
    internal static void ApplyWithReadableCapture(Action<bool, bool> apply, Action capture, bool updateMips, bool unreadable)
    {
        apply(updateMips, false);
        try { capture(); }
        finally { if (unreadable) apply(false, true); }
    }
    private static Texture2D Compress(Texture2D texture, bool deleteOriginal)
    {
        // The whole native compression/copy path may dispatch callbacks, including
        // GPU completion. No speculative source or grouped-reader handle survives.
        textureReadSession?.Suspend();
        return (cache != null || preparationAdmission != null) && current?.CapturePath == PngCapturePath.D3D11CompressionBlocks
            ? CompressClone(texture, deleteOriginal) : StaticTextureAtlas.FastCompressDXT(texture, deleteOriginal);
    }
    private static void CopyCompressionBlocks(Texture source, int srcElement, int srcMip, int srcX, int srcY, int width, int height,
        Texture destination, int dstElement, int dstMip, int dstX, int dstY)
    {
        Graphics.CopyTexture(source, srcElement, srcMip, srcX, srcY, width, height, destination, dstElement, dstMip, dstX, dstY);
        if (current == null || current.CaptureFailed)
            return;
        if (!(destination is Texture2D target)
            || !TryCapture( NativeTextureData.ExpectedBytes(target.width, target.height, (int)target.format, target.mipmapCount),
                () => CaptureCompressionBlocks(source, srcElement, srcMip, width, height)))
        {
            current.CaptureFailed = true;
            current.Blocks.Clear();
            return;
        }
    }
    private static void CaptureCompressionBlocks(Texture source, int srcElement, int srcMip, int width, int height)
    {
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
            current!.Blocks.Add(request.GetData<byte>().ToArray());
        }
        catch (Exception e)
        {
            current!.CaptureFailed = true;
            if (cache != null)
            {
                if (cache.Errors == 0)
                    Write("capture-error", "\"reason\":\"" + Escape(e.ToString()) + "\"");
                cache.Errors++;
            }
        }
        finally { if (readable != null) RenderTexture.ReleaseTemporary(readable); captureTicks += Stopwatch.GetTimestamp() - start; }
    }
    internal static PngCache.Entry Describe(Texture2D t, byte[] data) => new()
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
        Pixels = data,
        Readable = t.isReadable
    };
    internal static Texture2D Restore(PngCache.Entry entry)
    {
        long started = Stopwatch.GetTimestamp();
        try { return RestoreCore(entry, false); }
        finally { restoreTicks += Stopwatch.GetTimestamp() - started; }
    }
    internal static Texture2D RestoreQuality(PngCache.Entry entry) => RestoreCore(entry, true);
    private static Texture2D RestoreOwned(NativeTextureData.OwnedRecord record)
    {
        if (!WarmTextureUploadContract.Allowed) throw new InvalidOperationException("warm-upload-contract-changed");
        long started = Stopwatch.GetTimestamp();
        try { return RestoreCore(record.Descriptor.ToEntry(), false, record); }
        finally { restoreTicks += Stopwatch.GetTimestamp() - started; }
    }
    private static Texture2D RestoreCore(PngCache.Entry entry, bool quality, NativeTextureData.OwnedRecord? record = null)
    {
        if (quality && !QualityUnityCallbacks.Check()) throw new InvalidOperationException("quality-private-ownership-unavailable");
        var texture = new Texture2D(entry.Width, entry.Height, (TextureFormat)entry.TextureFormat, entry.Mips,
            !GraphicsFormatUtility.IsSRGBFormat((GraphicsFormat)entry.GraphicsFormat), true);
        try
        {
            if ((int)texture.format != entry.TextureFormat || (int)texture.graphicsFormat != entry.GraphicsFormat || texture.mipmapCount != entry.Mips)
                throw new InvalidDataException("cache-format expected=" + entry.TextureFormat + "/" + entry.GraphicsFormat + "/" + entry.Mips + " actual=" + texture.format + "/" + texture.graphicsFormat + "/" + texture.mipmapCount);
            if (record == null) texture.LoadRawTextureData(entry.Pixels);
            else
            {
                if (!WarmTextureUploadContract.Allowed || !QualityUnityCallbacks.WarmArrayAllowed)
                    throw new InvalidOperationException("warm-upload-contract-changed");
                record.WithPinnedPixels(texture.LoadRawTextureData);
            }
            texture.Apply(false, !entry.Readable);
            texture.filterMode = (FilterMode)entry.Filter;
            texture.wrapModeU = (TextureWrapMode)entry.WrapU;
            texture.wrapModeV = (TextureWrapMode)entry.WrapV;
            texture.wrapModeW = (TextureWrapMode)entry.WrapW;
            texture.anisoLevel = entry.Aniso;
            texture.mipMapBias = entry.Bias;
            return texture;
        }
        catch
        {
            // A callback can retain the new object even when construction fails.
            // Native/cache callers retain their established cleanup contract.
            if (!(quality || PreparedTextureRuntime.UseAtStartup) || QualityUnityCallbacks.Check()) Object.DestroyImmediate(texture);
            throw;
        }
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
        Write("raw-pixel-helper", RawPixelTrial.Finish());
        try { cache?.Complete(); }
        finally { PreparedTextureRuntime.FinishStartup(); }
        Write("complete", "\"mode\":\"" + mode + "\",\"calls\":" + calls + ",\"jpegCalls\":" + jpegCalls + ",\"nativeFallbacks\":" + nativeFallbacks
            + ",\"ddsNativePassThrough\":" + ddsNativePassThrough
            + ",\"nativeReloads\":" + nativeReloads + ",\"loadingProgressReloads\":" + loadingProgressReloads + ",\"reloadFallbacks\":" + reloadFallbacks
            + ",\"errors\":" + errors + ",\"verified\":" + verified + ",\"verifiedUncompressed\":" + verifiedUncompressed + ",\"mismatches\":" + mismatches + ",\"pngMs\":" + Ms(pngTicks)
            + ",\"reloadMs\":" + Ms(reloadTicks) + ",\"captureMs\":" + Ms(captureTicks) + ",\"verifyMs\":" + Ms(verifyTicks) + ",\"sourceBytes\":" + sourceBytes
            + ",\"hits\":" + (cache?.Hits ?? 0) + ",\"misses\":" + (cache?.Misses ?? 0) + ",\"writes\":" + (cache?.Writes ?? 0)
            + ",\"cacheErrors\":" + (cache?.Errors ?? 0) + ",\"cacheReadBytes\":" + (cache?.ReadBytes ?? 0) + ",\"cacheWrittenBytes\":" + (cache?.WrittenBytes ?? 0)
            + ",\"cacheStoredBytes\":" + (cache?.StoredBytes ?? 0) + ",\"cachePruned\":" + (cache?.Pruned ?? 0)
            + ",\"cachePendingRemoved\":" + (cache?.PendingRemoved ?? 0) + ",\"cacheReason\":\"" + Escape(cache?.LastReason ?? CacheLaunchPolicy.Current.Action.ToString()) + "\""
            + ",\"cacheLegacyRemoved\":" + (cache?.LegacyRemoved ?? 0)
            + ",\"cacheReasons\":{" + (cache == null ? "" : string.Join(",", cache.Reasons.Select(p => "\"" + Escape(p.Key) + "\":" + p.Value.ToString(CultureInfo.InvariantCulture)))) + "}"
            + ",\"groupReadMs\":" + Ms(GroupedTextureBlocks.ReadTicks) + ",\"groupAcquireMs\":" + Ms(GroupedTextureBlocks.ReadAcquireTicks)
            + ",\"groupDecodeMs\":" + Ms(GroupedTextureBlocks.ReadDecodeTicks) + ",\"groupHashMs\":" + Ms(GroupedTextureBlocks.ReadHashTicks)
            + ",\"groupReadBlocks\":" + GroupedTextureBlocks.ReadBlockCount + ",\"nativeRecordDecodeMs\":" + Ms(PreparedTextureStore.DecodeTicks)
            + ",\"textureRestoreMs\":" + Ms(restoreTicks) + ",\"ddsIdentityMs\":" + Ms(ddsIdentityTicks)
            + ",\"preparationPlatformMs\":" + Ms(preparationPlatformTicks)
            + ",\"sourceSnapshotMs\":" + Ms(PreparedTextureRuntime.SourceSnapshotTicks) + ",\"sourceIdentityMs\":" + Ms(PreparedTextureRuntime.IdentityTicks)
            + ",\"preparedHits\":" + PreparedTextureRuntime.Hits + ",\"preparedMisses\":" + PreparedTextureRuntime.Misses + ",\"preparedRefused\":" + PreparedTextureRuntime.Refused
            + ",\"groupPublishMs\":" + Ms(GroupedTextureBlocks.PublishTicks) + ",\"groupCompressionMs\":" + Ms(GroupedTextureBlocks.CompressionTicks)
            + ",\"groupDataFlushMs\":" + Ms(GroupedTextureBlocks.DataFlushTicks) + ",\"groupDeltaMs\":" + Ms(GroupedTextureBlocks.DeltaTicks)
            + ",\"groupCheckpointMs\":" + Ms(GroupedTextureBlocks.CheckpointTicks)
            + ",\"nativeHashCalls\":" + PngCache.NativeHashCalls + ",\"managedHashCalls\":" + PngCache.ManagedHashCalls + ",\"hashMs\":" + Ms(PngCache.HashTicks));
    }
    private static string Ms(long ticks) => (ticks * 1000d / Stopwatch.Frequency).ToString("F3", CultureInfo.InvariantCulture);
    private static string Escape(string text) => JsonLineLog.Escape(text);
    private static void Write(string kind, string fields)
        => JsonLineLog.WriteEvent(logPath, kind, fields);
}

