// Copyright (c) 2026 kt-nyx and contributors.
// Licensed under GPL-3.0-or-later with LICENSE-EXCEPTION.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Verse;
using Object = UnityEngine.Object;

namespace WakeUp;

internal static class AtlasRuntime
{
    internal const string Owner = "wakeup.static-atlases";
    internal static string Status = "Atlas caching and batching are retired; native atlas baking is used.";
    private static bool reuse, batching, finished, installed, inBatch;
    private static bool nativeDrain;
    internal static void RequestNativeDrain() => nativeDrain = true;
    private sealed class ChangedBatch : Exception { internal ChangedBatch(string reason) : base(reason) { } }
    internal static bool ReuseInstalled => installed && reuse && !finished;
    internal static bool CanBatch => installed && batching && !finished && UnityData.IsInMainThread
        && SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 && Compatible();
    private static string log = "", saveRoot = "", context = "";
    private static AtlasCacheStore? store;
    private static readonly HashSet<StaticTextureAtlas> Restored = new();
    internal static bool WasRestored(StaticTextureAtlas atlas) => Restored.Contains(atlas);
    internal static void ProbeNativeBake(StaticTextureAtlas atlas)
    { bool prior = inBatch; inBatch = true; try { atlas.Bake(); } finally { inBatch = prior; } }
    private static PublishedPatchGuard[] guards = Array.Empty<PublishedPatchGuard>();
    private static readonly AccessTools.FieldRef<StaticTextureAtlas, List<Texture2D>> Textures = AccessTools.FieldRefAccess<StaticTextureAtlas, List<Texture2D>>("textures");
    private static readonly AccessTools.FieldRef<StaticTextureAtlas, Dictionary<Texture2D, Texture2D>> Masks = AccessTools.FieldRefAccess<StaticTextureAtlas, Dictionary<Texture2D, Texture2D>>("masks");
    private static readonly AccessTools.FieldRef<StaticTextureAtlas, Dictionary<Texture, StaticTextureAtlasTile>> Tiles = AccessTools.FieldRefAccess<StaticTextureAtlas, Dictionary<Texture, StaticTextureAtlasTile>>("tiles");
    private static readonly AccessTools.FieldRef<StaticTextureAtlas, Texture2D> Color = AccessTools.FieldRefAccess<StaticTextureAtlas, Texture2D>("colorTexture");
    private static readonly AccessTools.FieldRef<StaticTextureAtlas, Texture2D> Mask = AccessTools.FieldRefAccess<StaticTextureAtlas, Texture2D>("maskTexture");
    private static readonly MethodInfo Layout = AccessTools.Method(typeof(StaticTextureAtlas), "CalcRectsForAtlasNew");
    private static readonly MethodInfo ColorBlit = AccessTools.Method(typeof(StaticTextureAtlas), "BlitTexturesToColorAtlas");
    private static readonly MethodInfo MaskBlit = AccessTools.Method(typeof(StaticTextureAtlas), "BuildMaskAtlas");
    private static readonly MethodInfo Compress = AccessTools.Method(typeof(StaticTextureAtlas), "ApplyTextureCompression");
    private static readonly MethodInfo Compressor = AccessTools.Method(typeof(StaticTextureAtlas), "FastCompressDXT");
    private static readonly Func<StaticTextureAtlas, int, bool, Rect[]> NativeLayout = AccessTools.MethodDelegate<Func<StaticTextureAtlas, int, bool, Rect[]>>(Layout);
    private static readonly Action<StaticTextureAtlas, Rect[], bool> NativeColor = AccessTools.MethodDelegate<Action<StaticTextureAtlas, Rect[], bool>>(ColorBlit);
    private static readonly Action<StaticTextureAtlas, Rect[], bool> NativeMask = AccessTools.MethodDelegate<Action<StaticTextureAtlas, Rect[], bool>>(MaskBlit);
    private static readonly Action<StaticTextureAtlas, bool> NativeCompress = AccessTools.MethodDelegate<Action<StaticTextureAtlas, bool>>(Compress);
    [ThreadStatic] private static BakeState? current;
    private sealed class BakeState : IDisposable
    {
        internal string Key = "";
        internal AtlasPixels.CaptureScope? Capture;
        public void Dispose() { Capture?.Dispose(); Capture = null; if (ReferenceEquals(current, this)) current = null; }
    }

    internal static void Initialize(string[] args, string root)
    {
        saveRoot = root; log = Path.Combine(root, "WakeUp", "atlases.jsonl");
        // Retired release features: neither saved settings nor legacy CLI flags
        // may install their hooks. Keep old-cache maintenance and the log sink
        // used independently by the retained loading-display scheduler.
        reuse = batching = installed = false;
        AtlasCacheStore.Maintain(root, CacheLaunchPolicy.Current);
    }
    private static string Bool(bool value) => value ? "true" : "false";
    private static readonly HashSet<MethodBase> ReportedOverlaps = new();
    private static bool Compatible()
    {
        foreach (var guard in guards)
        {
            if (guard.AllowsOriginalContract()) continue;
            ReportOverlap(guard.Target, "Atlas reuse and batching");
            return false;
        }
        return true;
    }
    internal static void ReportOverlap(MethodBase target, string feature)
    {
        if (!ReportedOverlaps.Add(target)) return;
        string owners;
        try { owners = string.Join(", ", Harmony.GetPatchInfo(target)?.Owners.Where(o => o != Owner && o != AtlasBatchScheduling.Owner) ?? Enumerable.Empty<string>()); }
        catch { owners = "unavailable"; }
        Status = feature + " retained ordinary atlas baking because another hook changed "
            + target.DeclaringType?.FullName + "." + target.Name + ". Owners: " + owners
            + ". This behavior is not replaced; other supplier features remain enabled.";
        Record("overlap-refused", "\"method\":" + JsonLineLog.Quote(target.ToString())
            + ",\"owners\":" + JsonLineLog.Quote(owners) + ",\"coverage\":" + JsonLineLog.Quote(Status));
        Log.Message("[Wake-Up] " + Status);
    }
    internal static void Record(string name, string fields = "") => JsonLineLog.WriteEvent(log, name, fields);
    private static AtlasCacheStore Store => store ??= new AtlasCacheStore(saveRoot, compress: PreparedTextureRuntime.CompressStorage, batched: true);

    private static string Identity(StaticTextureAtlas atlas) => IdentityOf(atlas, null);
    private static string IdentityOf(StaticTextureAtlas atlas, AtlasInputSnapshot? snapshot)
    {
        var parts = IdentityHeader(atlas);
        foreach (var texture in Textures(atlas)) AppendIdentity(parts, atlas, texture, snapshot);
        return PreparedTextureRuntime.Frame(parts.ToArray());
    }
    private static List<string> IdentityHeader(StaticTextureAtlas atlas)
    {
        if (!UnityData.IsInMainThread) throw new InvalidOperationException("atlas-identity-main-thread");
        if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Direct3D11)
            throw new NotSupportedException("atlas-backend-unqualified");
        if (context.Length == 0) context = PreparedTextureRuntime.Frame("complete-atlas-v2-frozen-layout", typeof(StaticTextureAtlas).Assembly.ManifestModule.ModuleVersionId.ToString(),
            typeof(Texture2D).Assembly.ManifestModule.ModuleVersionId.ToString(), Application.unityVersion,
            SystemInfo.graphicsDeviceType.ToString(), SystemInfo.graphicsDeviceVersion, QualitySettings.activeColorSpace.ToString());
        var inputs = Textures(atlas);
        if (inputs.Count < 1 || inputs.Count > AtlasCacheStore.MaximumTiles || inputs.Distinct().Count() != inputs.Count
            || Tiles(atlas).Count != 0 || Mask(atlas) != null) throw new InvalidDataException("atlas-new-group-required");
        return new List<string> { context, atlas.groupKey.ToString(), Prefs.TextureCompression.ToString(),
            UnityData.ComputeShadersSupported.ToString(), StaticTextureAtlas.MaxAtlasSize.ToString() };
    }
    private static void AppendIdentity(List<string> parts, StaticTextureAtlas atlas, Texture2D texture, AtlasInputSnapshot? snapshot)
    {
        Texture2D colorInput = snapshot?.Get(texture) ?? texture;
        parts.Add(AtlasProvenance.Revision(colorInput));
        bool hasMask = Masks(atlas).TryGetValue(texture, out var mask);
        Texture2D? maskInput = hasMask ? snapshot?.Get(mask) ?? mask : null;
        if (hasMask != atlas.groupKey.hasMask || hasMask && (maskInput!.width != colorInput.width || maskInput.height != colorInput.height))
            throw new InvalidDataException("atlas-mask-association");
        parts.Add(hasMask ? AtlasProvenance.Revision(maskInput!) : "no-mask");
    }

    // Returns false only after privately validating and committing a complete hit.
    private static bool Begin(StaticTextureAtlas __instance, bool rebake, out BakeState? __state)
    {
        __state = null;
        if (!ReuseInstalled || rebake || inBatch || current != null || !UnityData.IsInMainThread || !Compatible()) return true;
        bool native = Start(__instance, out __state);
        if (native && __state != null) __state.Capture = AtlasPixels.BeginCapture();
        return native;
    }
    private static bool Start(StaticTextureAtlas atlas, out BakeState? state, string? identity = null)
    {
        state = null;
        if (!ReuseInstalled || !CacheLaunchPolicy.Current.AllowRead && !CacheLaunchPolicy.Current.AllowWrite) return true;
        try
        {
            string key = identity ?? Identity(atlas);
            var entry = Store.Read(key, Textures(atlas).Count);
            if (entry != null)
            {
                if ((entry.Mask != null) != atlas.groupKey.hasMask) Store.Invalidate(key);
                else if (RestoreGroup(atlas, entry))
                { Restored.Add(atlas); Record("group-restored", "\"key\":" + JsonLineLog.Quote(key) + ",\"group\":" + JsonLineLog.Quote(atlas.groupKey.ToString()) + ",\"tiles\":" + entry.TileCount + ",\"skippedBake\":true"); return false; }
                else Store.Invalidate(key);
            }
            if (CacheLaunchPolicy.Current.AllowWrite)
                current = state = new BakeState { Key = key };
        }
        catch (Exception e) { Record("group-native", "\"reason\":" + JsonLineLog.Quote(e.Message)); state?.Dispose(); state = null; }
        return true;
    }
    internal static bool RestoreGroup(StaticTextureAtlas atlas, AtlasCacheStore.Entry entry)
    {
        Texture2D? color = null, mask = null;
        var tiles = new Dictionary<Texture, StaticTextureAtlasTile>();
        bool committed = false;
        try
        {
            var textures = Textures(atlas);
            if (!AtlasCacheStore.Valid(entry) || textures.Count != entry.TileCount || Tiles(atlas).Count != 0
                || Mask(atlas) != null || (entry.Mask != null) != atlas.groupKey.hasMask) return false;
            color = AtlasPixels.Restore(entry.Color);
            if (entry.Mask != null) mask = AtlasPixels.Restore(entry.Mask);
            for (int i = 0; i < textures.Count; i++)
            {
                int n = i * 4; var rect = new Rect(entry.Rects[n], entry.Rects[n + 1], entry.Rects[n + 2], entry.Rects[n + 3]);
                Mesh mesh = TextureAtlasHelper.CreateMeshForUV(rect, 0.5f);
                try { mesh.name = "TextureAtlasMesh_" + atlas.groupKey + "_" + mesh.GetInstanceID(); tiles.Add(textures[i], new StaticTextureAtlasTile { atlas = atlas, mesh = mesh, uvRect = rect }); }
                catch { Object.DestroyImmediate(mesh); throw; }
            }
            color.name = "TextureAtlas_" + atlas.groupKey + "_" + color.GetInstanceID();
            if (mask != null) mask.name = "Mask_" + color.name;
            Texture2D placeholder = Color(atlas);
            // Field-ref stores cannot call user code. All allocations, Unity
            // calls and dictionary insertions have completed before this point.
            Color(atlas) = color; Mask(atlas) = mask!; Tiles(atlas) = tiles;
            committed = true;
            Object.DestroyImmediate(placeholder);
            return true;
        }
        catch (Exception e) { Record("restore-failed", "\"reason\":" + JsonLineLog.Quote(e.Message)); return committed; }
        finally
        {
            if (!committed)
            {
                foreach (var tile in tiles.Values) Object.DestroyImmediate(tile.mesh);
                if (color != null) Object.DestroyImmediate(color);
                if (mask != null) Object.DestroyImmediate(mask);
            }
        }
    }
    private static void End(StaticTextureAtlas __instance, BakeState? __state)
    {
        if (__state == null) return;
        try
        {
            var textures = Textures(__instance); var tiles = Tiles(__instance);
            if (textures.Count < 1 || tiles.Count != textures.Count) return;
            long incoming = textures.Count * 16L + 2048;
            foreach (var image in new[] { Color(__instance), Mask(__instance) })
                if (image != null)
                {
                    int length = NativeTextureData.ExpectedBytes(image.width, image.height, (int)image.format, image.mipmapCount);
                    if (length < 1) { Record("capture-native", "\"reason\":\"atlas-output-format-or-size\""); return; }
                    incoming += length * (image.isReadable ? 2L : 1L);
                }
            if (incoming > AtlasCacheStore.MaximumEntry || !Store.CanPublish(__state.Key, (int)incoming))
            { Record("capture-native", "\"reason\":\"atlas-group-size-or-shared-budget\""); return; }
            __state.Capture ??= AtlasPixels.BeginCapture();
            var entry = new AtlasCacheStore.Entry { Color = __state.Capture.Capture(Color(__instance)),
                Mask = Mask(__instance) == null ? null : __state.Capture.Capture(Mask(__instance)), Rects = new float[textures.Count * 4] };
            for (int i = 0; i < textures.Count; i++)
            {
                var r = tiles[textures[i]].uvRect; int n = i * 4;
                entry.Rects[n] = r.x; entry.Rects[n + 1] = r.y; entry.Rects[n + 2] = r.width; entry.Rects[n + 3] = r.height;
            }
            bool published = Store.Publish(__state.Key, entry);
            Record("group-baked", "\"key\":" + JsonLineLog.Quote(__state.Key) + ",\"group\":" + JsonLineLog.Quote(__instance.groupKey.ToString()) + ",\"tiles\":" + textures.Count + ",\"published\":" + Bool(published) + ",\"reason\":" + JsonLineLog.Quote(Store.LastReason));
        }
        catch (Exception e) { Record("capture-failed", "\"reason\":" + JsonLineLog.Quote(e.ToString())); }
        finally { __state.Dispose(); }
    }
    private static void FinalizeBake(BakeState? __state) => __state?.Dispose();
    private static IEnumerable<CodeInstruction> CompressionTranspiler(IEnumerable<CodeInstruction> source)
    {
        var code = source.ToList(); int count = 0;
        foreach (var c in code)
            if (c.operand is MethodInfo m && m.DeclaringType == typeof(Graphics) && m.Name == "CopyTexture" && m.GetParameters().Length == 12)
            { c.opcode = OpCodes.Call; c.operand = AccessTools.Method(typeof(AtlasPixels), nameof(AtlasPixels.CopyCompressionBlocks)); count++; }
        if (count != 1) throw new InvalidOperationException("atlas-compressor-copy-shape");
        return code;
    }

    // Each yielded value returns through the native asynchronous-event update.
    // The scheduler retains the original callback suffix until this completes.
    internal static IEnumerator BakeBatches()
    {
        BuildingsDamageSectionLayerUtility.TryInsertIntoAtlas();
        RimWorld.MinifiedThing.TryInsertIntoAtlas();
        var queue = (Dictionary<TextureAtlasGroupKey, (List<Texture2D>, HashSet<Texture2D>)>)AccessTools.Field(typeof(GlobalTextureAtlasManager), "buildQueue").GetValue(null);
        var masks = (Dictionary<Texture2D, Texture2D>)AccessTools.Field(typeof(GlobalTextureAtlasManager), "buildQueueMasks").GetValue(null);
        var published = (List<StaticTextureAtlas>)AccessTools.Field(typeof(GlobalTextureAtlasManager), "staticTextureAtlases").GetValue(null);
        // Snapshot membership after the same native insertion callbacks. New
        // foreign requests are not discarded or removed from the native queue.
        var groups = queue.Select(p => (p.Key, Inputs: p.Value.Item1.ToArray())).ToArray();
        int completed = 0;
        foreach (var group in groups)
        {
            int index = 0;
            do
            {
                var atlas = new StaticTextureAtlas(group.Key); bool committed = false;
                try
                {
                    int pixels = 0;
                    while (index < group.Inputs.Length)
                    {
                        var t = group.Inputs[index]; int area = t.width * t.height;
                        if (pixels + area > StaticTextureAtlas.MaxPixelsPerAtlas && Textures(atlas).Count > 0) break;
                        atlas.Insert(t, group.Key.hasMask && masks.TryGetValue(t, out var m) ? m : null);
                        pixels += area; index++;
                    }
                    var work = BakeOne(atlas); bool retry = false;
                    try
                    {
                        while (true)
                        {
                            bool more;
                            try { more = work.MoveNext(); }
                            catch (ChangedBatch e) { Record("batch-group-native", "\"reason\":" + JsonLineLog.Quote(e.Message)); retry = true; break; }
                            if (!more) break;
                            yield return work.Current;
                        }
                    }
                    finally { (work as IDisposable)?.Dispose(); }
                    if (retry)
                    {
                        var originalInputs = Textures(atlas).ToArray(); var originalMasks = new Dictionary<Texture2D, Texture2D>(Masks(atlas));
                        atlas.Destroy(); atlas = new StaticTextureAtlas(group.Key);
                        foreach (var t in originalInputs) atlas.Insert(t, originalMasks.TryGetValue(t, out var mask) ? mask : null);
                        ProbeNativeBake(atlas);
                    }
                    published.Add(atlas); committed = true; completed++;
                    Record("batch-group-complete", "\"groups\":" + completed + ",\"tiles\":" + Textures(atlas).Count);
                }
                finally { if (!committed) atlas.Destroy(); }
                yield return null;
            } while (index < group.Inputs.Length);
        }
        Record("batch-complete", "\"groups\":" + completed);
    }
    private static IEnumerator BakeOne(StaticTextureAtlas atlas)
    {
        if (nativeDrain || !Compatible()) { ProbeNativeBake(atlas); yield break; }
        using var inputs = new AtlasInputSnapshot();
        List<string>? identityParts = null;
        if (ReuseInstalled && (CacheLaunchPolicy.Current.AllowRead || CacheLaunchPolicy.Current.AllowWrite))
        {
            try { identityParts = IdentityHeader(atlas); }
            catch (Exception e) { Record("group-native", "\"reason\":" + JsonLineLog.Quote(e.Message)); }
        }
        long inputStart = Stopwatch.GetTimestamp(); int inputCount = 0;
        foreach (var t in Textures(atlas))
        {
            CheckBatch(inputs);
            // Freeze each color/mask pair in the same main-thread step. Their
            // revisions describe exactly these owned pixels even if a producer
            // changes its public images on a later frame.
            try { inputs.Add(t); if (Masks(atlas).TryGetValue(t, out var m)) inputs.Add(m); }
            catch (Exception e) { throw new ChangedBatch("snapshot:" + e.Message); }
            if (identityParts != null)
            {
                try { AppendIdentity(identityParts, atlas, t, inputs); }
                catch (Exception e) { identityParts = null; Record("group-native", "\"reason\":" + JsonLineLog.Quote(e.Message)); }
            }
            inputCount++;
            if ((Stopwatch.GetTimestamp() - inputStart) / (double)Stopwatch.Frequency >= .008)
            { Record("input-yield", "\"completed\":" + inputCount + ",\"total\":" + Textures(atlas).Count); yield return null; inputStart = Stopwatch.GetTimestamp(); }
        }
        CheckBatch(inputs);
        string? identity = identityParts == null ? null : PreparedTextureRuntime.Frame(identityParts.ToArray());
        BakeState? state = null;
        if (identity != null && !Start(atlas, out state, identity)) yield break;
        try
        {
            Rect[] rects = LayoutFrozen(atlas, inputs);
            if (rects.Length != Textures(atlas).Count)
            { Log.Error("Texture packing failed! Clearing out atlas..."); Textures(atlas).Clear(); yield break; }
            yield return null;
            CheckBatch(inputs);
            bool cpu = !inputs.Compute;
            foreach (bool mask in atlas.groupKey.hasMask ? new[] { false, true } : new[] { false })
            {
                var draw = DrawBatches(atlas, rects, cpu, mask, inputs);
                try { while (draw.MoveNext()) yield return draw.Current; }
                finally { (draw as IDisposable)?.Dispose(); }
            }
            // Mesh construction is owned, splittable work. Adapt how many mesh
            // units run before checking the eight millisecond slice budget.
            int checkEvery = 16, sinceCheck = 0; long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < rects.Length; i++)
            {
                CheckBatch(inputs);
                Mesh mesh = TextureAtlasHelper.CreateMeshForUV(rects[i], 0.5f);
                try { mesh.name = "TextureAtlasMesh_" + atlas.groupKey + "_" + mesh.GetInstanceID(); Tiles(atlas).Add(Textures(atlas)[i], new StaticTextureAtlasTile { atlas = atlas, mesh = mesh, uvRect = rects[i] }); }
                catch { Object.DestroyImmediate(mesh); throw; }
                if (++sinceCheck >= checkEvery)
                {
                    double elapsed = (Stopwatch.GetTimestamp() - start) / (double)Stopwatch.Frequency;
                    if (elapsed >= .008)
                    { checkEvery = Math.Max(1, checkEvery / 2); Record("mesh-yield", "\"completed\":" + (i + 1) + ",\"total\":" + rects.Length); yield return null; start = Stopwatch.GetTimestamp(); }
                    else checkEvery = Math.Min(256, checkEvery * 2);
                    sinceCheck = 0;
                }
            }
            CheckBatch(inputs);
            if (state != null) state.Capture = AtlasPixels.BeginCapture();
            if (inputs.Compression) NativeCompress(atlas, cpu);
            if (cpu)
            { if (Color(atlas) != null) Color(atlas).Apply(false, true); if (Mask(atlas) != null) Mask(atlas).Apply(false, true); }
            End(atlas, state);
        }
        finally { state?.Dispose(); }
    }
    private static Rect[] LayoutFrozen(StaticTextureAtlas atlas, AtlasInputSnapshot inputs)
    {
        // Both native packing routes read dimensions (and the fallback reads
        // sampling state) from this private list. Use the very same immutable
        // copies as identity and drawing. No yield or foreign atlas hook can
        // observe the temporary list; native recursion also sees these copies.
        // Keep the borrowed objects as the eventual public tile keys.
        List<Texture2D> originals = Textures(atlas);
        Textures(atlas) = originals.Select(inputs.Get).ToList();
        try { return NativeLayout(atlas, 1, false); }
        finally { Textures(atlas) = originals; }
    }
    private static void CheckBatch(AtlasInputSnapshot inputs)
    {
        if (nativeDrain || !Compatible() || inputs.Compression != Prefs.TextureCompression
            || inputs.Compute != UnityData.ComputeShadersSupported || inputs.MaximumSize != StaticTextureAtlas.MaxAtlasSize)
            throw new ChangedBatch("atlas-hook-or-settings-changed");
    }
    private static IEnumerator DrawBatches(StaticTextureAtlas atlas, Rect[] rects, bool cpu, bool mask, AtlasInputSnapshot inputs)
    {
        Texture2D color = Color(atlas);
        if (mask) Mask(atlas) = new Texture2D(color.width, color.height, TextureFormat.ARGB32, false);
        Texture2D target = mask ? Mask(atlas) : color;
        var rt = new RenderTexture(target.width, target.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Material? material = null;
        try
        {
            rt.Create(); material = new Material(Shader.Find("Custom/BlitExact"));
            var previous = RenderTexture.active;
            try { Graphics.SetRenderTarget(rt); GL.Clear(true, true, UnityEngine.Color.clear); }
            finally { RenderTexture.active = previous; }
            int i = 0, batch = 16;
            while (i < rects.Length)
            {
                CheckBatch(inputs);
                long start = Stopwatch.GetTimestamp(); int done = 0;
                previous = RenderTexture.active; bool pushed = false;
                try
                {
                    Graphics.SetRenderTarget(rt); GL.PushMatrix(); pushed = true;
                    GL.LoadPixelMatrix(0, rt.width, rt.height, 0);
                    do
                    {
                        Texture2D source = Textures(atlas)[i];
                        if (!mask || Masks(atlas).TryGetValue(source, out source))
                        {
                            source = inputs.Get(source);
                            Rect r = rects[i]; int x = Mathf.RoundToInt(r.x * target.width);
                            int w = Mathf.RoundToInt(r.width * target.width), h = Mathf.RoundToInt(r.height * target.height);
                            int y = Mathf.RoundToInt((1f - r.y) * target.height) - h;
                            Graphics.DrawTexture(new Rect(x, y, w, h), source, new Rect(0, 0, 1, 1), 0, 0, 0, 0, material, -1);
                        }
                        i++; done++;
                    } while (i < rects.Length && done < batch && (Stopwatch.GetTimestamp() - start) / (double)Stopwatch.Frequency < .008);
                }
                finally { if (pushed) GL.PopMatrix(); RenderTexture.active = previous; }
                double seconds = (Stopwatch.GetTimestamp() - start) / (double)Stopwatch.Frequency;
                batch = Math.Max(1, Math.Min(1024, (int)Math.Min(batch * 2d, done * .008 / Math.Max(.000001, seconds))));
                Record("draw-yield", "\"mask\":" + Bool(mask) + ",\"completed\":" + i + ",\"total\":" + rects.Length);
                yield return null;
            }
            previous = RenderTexture.active;
            CheckBatch(inputs);
            try
            {
                if (cpu)
                { RenderTexture.active = rt; target.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false); target.Apply(true, false); }
                else
                {
                    Graphics.CopyTexture(rt, 0, 0, 0, 0, target.width, target.height, target, 0, 0, 0, 0);
                    if (!mask) AccessTools.MethodDelegate<Action<StaticTextureAtlas, Texture2D>>(AccessTools.Method(typeof(StaticTextureAtlas), "GenerateMipmapsWithCompute"))(atlas, target);
                }
            }
            finally { RenderTexture.active = previous; }
            target.name = mask ? "Mask_" + color.name : "TextureAtlas_" + atlas.groupKey + "_" + target.GetInstanceID();
        }
        finally { if (material != null) Object.Destroy(material); rt.Release(); Object.DestroyImmediate(rt); }
    }
    private static void Menu()
    {
        if (!installed || finished || !PlayDataLoader.Loaded || LongEventHandler.AnyEventNowOrWaiting) return;
        finished = true;
        try { store?.Complete(); Record("complete", "\"hits\":" + (store?.Hits ?? 0) + ",\"writes\":" + (store?.Writes ?? 0) + ",\"bytes\":" + (store?.StoredBytes ?? 0)); }
        finally { store?.Dispose(); store = null; }
    }
    private static void Clear()
    {
        if (!installed) return;
        AtlasBatchScheduling.Cancel("play-data-cleared"); Restored.Clear();
        finished = true; current?.Dispose(); store?.Dispose(); store = null;
        Record("reload-native");
    }
}
